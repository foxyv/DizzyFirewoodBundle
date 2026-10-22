using System;
using System.Collections.Generic;

namespace Dizzy.FirewoodBundle
{
    public enum ColliderStrategy
    {
        BagHull,
        HangAabb,
        Hull,
        PieceBoxes,
        StackAabb
    }

    // What an empty-hand look ray hit, for choosing a hanging bag behind a lamp hook.
    public enum BagLookHit
    {
        Other,
        LampHook,
        HangingBag
    }

    public struct ColliderVec3
    {
        public float X;
        public float Y;
        public float Z;

        public ColliderVec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public struct ColliderBounds
    {
        public ColliderVec3 Center;
        public ColliderVec3 Size;

        public ColliderBounds(ColliderVec3 center, ColliderVec3 size)
        {
            Center = center;
            Size = size;
        }

        public float MinY
        {
            get { return Center.Y - Size.Y * 0.5f; }
        }
    }

    // Pure shape and policy math for bundle colliders. No Unity types, so unit tests
    // can run without Sailwind or a Unity player.
    public static class BundleColliderMath
    {
        public const float CoreShare = 0.35f;
        public const float HangExpand = 0.01f;
        public const float StackExpand = 0.04f;
        // A pile sits on a slab this tall, grown this far past the sausages that touch the deck.
        public const float PileStandHeight = 0.04f;
        public const float PileStandPad = 0.01f;
        public const float HookLineMassCap = 2f;
        // GoPointer puts a held item on this layer, and the look ray skips it.
        public const int HeldLayer = 2;
        public const int WorldLayer = 0;

        // Breaks in the goat-cheese silhouette, as shares of half the round's height.
        // The side is piecewise linear between these, so a convex hull that includes
        // them contains the round.
        public static readonly float[] RoundShoulders = { -1f, -0.79f, 0.77f, 1f };

        public static float RoundWidth(float height)
        {
            if (height <= RoundShoulders[1])
                return Lerp(0.84f, 1f, InverseLerp(RoundShoulders[0], RoundShoulders[1], height));
            if (height <= RoundShoulders[2])
                return Lerp(1f, 0.9f, InverseLerp(RoundShoulders[1], RoundShoulders[2], height));
            return Lerp(0.9f, 0.725f, InverseLerp(RoundShoulders[2], RoundShoulders[3], height));
        }

        // Indices of the upper envelope of (height, radius): points a convex ring
        // collider has to keep so its straight sides do not cut the shape. Points on
        // or under a chord are left out. height must be sorted ascending.
        public static List<int> UpperHull(IList<float> height, IList<float> radius)
        {
            var stack = new List<int>();
            int count = height == null ? 0 : height.Count;
            for (int i = 0; i < count; i++)
            {
                while (stack.Count >= 2 && Cross(height, radius, stack[stack.Count - 2], stack[stack.Count - 1], i) >= -0.0000001f)
                    stack.RemoveAt(stack.Count - 1);
                stack.Add(i);
            }

            return stack;
        }

        // Drops the interior ring whose chord loses the least radius, until the hull
        // fits in maxRings. The first and last rings stay.
        public static void LimitHull(IList<float> height, IList<float> radius, List<int> hull, int maxRings)
        {
            if (hull == null)
                return;
            if (maxRings < 2)
                maxRings = 2;
            while (hull.Count > maxRings)
            {
                int drop = -1;
                float least = float.MaxValue;
                for (int i = 1; i < hull.Count - 1; i++)
                {
                    float gap = RadiusGap(height, radius, hull[i - 1], hull[i], hull[i + 1]);
                    if (gap < least)
                    {
                        least = gap;
                        drop = i;
                    }
                }

                if (drop < 0)
                    break;
                hull.RemoveAt(drop);
            }
        }

        // How far a middle sample sticks out past the chord between its neighbors.
        public static float RadiusGap(IList<float> height, IList<float> radius, int below, int at, int above)
        {
            float span = height[above] - height[below];
            if (Math.Abs(span) < 0.0000001f)
                return 0f;
            float t = (height[at] - height[below]) / span;
            float chord = radius[below] + (radius[above] - radius[below]) * t;
            return radius[at] - chord;
        }

        public static float RingRadius(int size, float radius)
        {
            if (size <= 1)
                return 0f;
            if (size >= 7)
                return radius * 2f;
            return radius / (float)Math.Sin(Math.PI / size);
        }

        public static List<int> PackBagLayers(int count, int baseLayer, bool rounds)
        {
            var layerSizes = new List<int>();
            if (count < 1)
                return layerSizes;

            int left = count;
            while (left > 0)
            {
                int size = Math.Min(left, Math.Max(1, baseLayer - layerSizes.Count));
                layerSizes.Add(size);
                left -= size;
            }

            if (count >= 3 && layerSizes.Count < 3)
            {
                layerSizes.Clear();
                for (int row = 0; row < 3; row++)
                    layerSizes.Add(count / 3 + (row < count % 3 ? 1 : 0));
            }

            int last = layerSizes.Count - 1;
            if (last > 0 && layerSizes[last] == 1 && layerSizes[last - 1] > 2)
            {
                layerSizes[last] = 2;
                layerSizes[last - 1]--;
            }

            if (rounds && count == 2)
            {
                layerSizes.Clear();
                layerSizes.Add(1);
                layerSizes.Add(1);
            }
            if (rounds && layerSizes.Count == 3 && layerSizes[0] == 2 && layerSizes[1] == 1 && layerSizes[2] == 1)
            {
                layerSizes[0] = 1;
                layerSizes[1] = 2;
            }

            return layerSizes;
        }

        // isBag: apple / orange / goat cheese bag.
        // isHangBunch: a date rack, one box around the rack.
        // hanging: a hanging sausage bunch. One box per sausage. One box around the
        // bunch is the air between them.
        // loosePile: a sausage pile. Each sausage keeps its own box. One hull around the
        // heap is the air between them.
        public static ColliderStrategy ChooseStrategy(bool isBag, bool isHangBunch, bool hanging, bool hasTurns, bool loosePile = false)
        {
            if (isBag)
                return ColliderStrategy.BagHull;
            if (isHangBunch)
                return ColliderStrategy.HangAabb;
            if (hanging || loosePile)
                return ColliderStrategy.PieceBoxes;
            if (hasTurns)
                return ColliderStrategy.Hull;
            return ColliderStrategy.StackAabb;
        }

        // A flat stand under the sausages that rest on the deck. Corners more than
        // `reach` above the lowest point belong to sausages stacked on top, and do not
        // widen it. The stand's bottom is the lowest point.
        public static ColliderBounds PileStand(IList<ColliderVec3> corners, int upAxis, float reach)
        {
            if (corners == null || corners.Count == 0)
                return new ColliderBounds(new ColliderVec3(0f, 0f, 0f), new ColliderVec3(0.05f, 0.05f, 0.05f));
            if (upAxis < 0 || upAxis > 2)
                upAxis = 1;
            if (reach < 0f)
                reach = 0f;

            float low = float.MaxValue;
            for (int i = 0; i < corners.Count; i++)
                low = Math.Min(low, At(corners[i], upAxis));

            int axisA = (upAxis + 1) % 3;
            int axisB = (upAxis + 2) % 3;
            float minA = float.MaxValue;
            float maxA = float.MinValue;
            float minB = float.MaxValue;
            float maxB = float.MinValue;
            bool any = false;
            for (int i = 0; i < corners.Count; i++)
            {
                if (At(corners[i], upAxis) > low + reach + 0.0001f)
                    continue;
                any = true;
                float a = At(corners[i], axisA);
                float b = At(corners[i], axisB);
                minA = Math.Min(minA, a);
                maxA = Math.Max(maxA, a);
                minB = Math.Min(minB, b);
                maxB = Math.Max(maxB, b);
            }

            if (!any)
            {
                for (int i = 0; i < corners.Count; i++)
                {
                    float a = At(corners[i], axisA);
                    float b = At(corners[i], axisB);
                    minA = Math.Min(minA, a);
                    maxA = Math.Max(maxA, a);
                    minB = Math.Min(minB, b);
                    maxB = Math.Max(maxB, b);
                }
            }

            var center = new ColliderVec3(0f, 0f, 0f);
            var size = new ColliderVec3(0f, 0f, 0f);
            center = Set(center, axisA, (minA + maxA) * 0.5f);
            center = Set(center, axisB, (minB + maxB) * 0.5f);
            center = Set(center, upAxis, low + PileStandHeight * 0.5f);
            size = Set(size, axisA, Math.Max(0.05f, maxA - minA + PileStandPad * 2f));
            size = Set(size, axisB, Math.Max(0.05f, maxB - minB + PileStandPad * 2f));
            size = Set(size, upAxis, PileStandHeight);
            return new ColliderBounds(center, size);
        }

        private static float At(ColliderVec3 value, int axis)
        {
            if (axis == 0)
                return value.X;
            if (axis == 1)
                return value.Y;
            return value.Z;
        }

        private static ColliderVec3 Set(ColliderVec3 value, int axis, float component)
        {
            if (axis == 0)
                value.X = component;
            else if (axis == 1)
                value.Y = component;
            else
                value.Z = component;
            return value;
        }

        // Index of the hanging bag an empty-hand look should switch to, or -1 to leave
        // the current target. A lamp hook in front of the bag does not block it. Any
        // other hit closer than the bag does, and so does a look target that is already
        // closer and is not the hook.
        public static int HangingBagToAim(
            IList<float> distances,
            IList<BagLookHit> kinds,
            bool hasCurrent,
            float currentDistance,
            bool currentIsHook,
            bool lookingAtBag)
        {
            if (distances == null || kinds == null || lookingAtBag)
                return -1;

            int count = Math.Min(distances.Count, kinds.Count);
            int bag = -1;
            float bagDistance = float.MaxValue;
            float block = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (kinds[i] == BagLookHit.HangingBag)
                {
                    if (distances[i] < bagDistance)
                    {
                        bagDistance = distances[i];
                        bag = i;
                    }

                    continue;
                }

                if (kinds[i] == BagLookHit.LampHook)
                    continue;
                if (distances[i] < block)
                    block = distances[i];
            }

            if (bag < 0 || bagDistance > block + 0.001f)
                return -1;
            if (hasCurrent && !currentIsHook && currentDistance <= bagDistance + 0.001f)
                return -1;
            return bag;
        }

        public static float ExpandPad(ColliderStrategy strategy)
        {
            if (strategy == ColliderStrategy.HangAabb)
                return HangExpand;
            if (strategy == ColliderStrategy.StackAabb)
                return StackExpand;
            return 0f;
        }

        public static void CapsuleFit(
            float sizeX,
            float sizeY,
            float sizeZ,
            float centerX,
            float centerY,
            float centerZ,
            out int direction,
            out ColliderVec3 center,
            out float radius,
            out float height)
        {
            center = new ColliderVec3(centerX, centerY, centerZ);
            direction = 1;
            if (sizeX >= sizeY && sizeX >= sizeZ)
                direction = 0;
            else if (sizeZ >= sizeY)
                direction = 2;

            float extentX = sizeX * 0.5f;
            float extentY = sizeY * 0.5f;
            float extentZ = sizeZ * 0.5f;
            radius = direction == 0
                ? Math.Max(extentY, extentZ)
                : direction == 2
                    ? Math.Max(extentX, extentY)
                    : Math.Max(extentX, extentZ);
            if (radius < 0.01f)
                radius = 0.01f;
            float along = direction == 0 ? sizeX : direction == 2 ? sizeZ : sizeY;
            height = Math.Max(along, radius * 2f);
        }

        // True when the point is inside the capsule or on its skin. A look ray aimed
        // at a banana hits that fruit's capsule when the banana's surface is in it.
        public static bool CapsuleContains(ColliderVec3 point, int direction, ColliderVec3 center, float radius, float height)
        {
            float along = Component(point, direction) - Component(center, direction);
            float radial = Radial(point, center, direction);
            float half = height * 0.5f;
            float cylinder = Math.Max(0f, half - radius);
            float past = Math.Abs(along) - cylinder;
            if (past <= 0f)
                return radial <= radius + 0.0001f;
            return radial * radial + past * past <= (radius + 0.0001f) * (radius + 0.0001f);
        }

        // Ray from `origin` through `target`. Hits when the capsule crosses that
        // segment, including a target that sits inside the capsule.
        public static bool RayHitsCapsule(
            ColliderVec3 origin,
            ColliderVec3 target,
            int direction,
            ColliderVec3 center,
            float radius,
            float height)
        {
            float dx = target.X - origin.X;
            float dy = target.Y - origin.Y;
            float dz = target.Z - origin.Z;
            float length = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (length < 0.000001f)
                return CapsuleContains(target, direction, center, radius, height);

            float inv = 1f / length;
            float t = CapsuleRayDistance(origin, dx * inv, dy * inv, dz * inv, direction, center, radius, height);
            return t >= 0f && t <= length + 0.002f;
        }

        // Distance from a point to the capsule skin. Positive when the point is
        // outside, negative when the collider stands off past it.
        public static float CapsuleSurfaceDistance(ColliderVec3 point, int direction, ColliderVec3 center, float radius, float height)
        {
            float along = Component(point, direction) - Component(center, direction);
            float radial = Radial(point, center, direction);
            float half = height * 0.5f;
            float cylinder = Math.Max(0f, half - radius);
            float past = Math.Abs(along) - cylinder;
            if (past <= 0f)
                return radial - radius;
            return (float)Math.Sqrt(radial * radial + past * past) - radius;
        }

        // Mean distance from visible-mesh points to the capsule skin. Interior
        // volume is not part of this; only where the mesh surface sits.
        public static float MeanSurfaceDistance(IList<ColliderVec3> surface, int direction, ColliderVec3 center, float radius, float height)
        {
            if (surface == null || surface.Count == 0)
                return 0f;
            double sum = 0d;
            for (int i = 0; i < surface.Count; i++)
                sum += Math.Abs(CapsuleSurfaceDistance(surface[i], direction, center, radius, height));
            return (float)(sum / surface.Count);
        }

        private static float CapsuleRayDistance(
            ColliderVec3 origin,
            float dirX,
            float dirY,
            float dirZ,
            int direction,
            ColliderVec3 center,
            float radius,
            float height)
        {
            float half = Math.Max(0f, height * 0.5f - radius);
            float ax = 0f;
            float ay = 0f;
            float az = 0f;
            if (direction == 0)
                ax = 1f;
            else if (direction == 2)
                az = 1f;
            else
                ay = 1f;

            float pax = center.X - ax * half;
            float pay = center.Y - ay * half;
            float paz = center.Z - az * half;
            float pbx = center.X + ax * half;
            float pby = center.Y + ay * half;
            float pbz = center.Z + az * half;
            float bax = pbx - pax;
            float bay = pby - pay;
            float baz = pbz - paz;
            float oax = origin.X - pax;
            float oay = origin.Y - pay;
            float oaz = origin.Z - paz;
            float baba = bax * bax + bay * bay + baz * baz;
            float bard = bax * dirX + bay * dirY + baz * dirZ;
            float baoa = bax * oax + bay * oay + baz * oaz;
            float rdoa = dirX * oax + dirY * oay + dirZ * oaz;
            float oaoa = oax * oax + oay * oay + oaz * oaz;
            float a = baba - bard * bard;
            float b = baba * rdoa - baoa * bard;
            float c = baba * oaoa - baoa * baoa - radius * radius * baba;
            float h = b * b - a * c;
            if (h >= 0f && a > 0.0000001f)
            {
                float t = (-b - (float)Math.Sqrt(h)) / a;
                float y = baoa + t * bard;
                if (y > 0f && y < baba)
                    return t;
            }

            float cap = CapsuleCapDistance(origin, dirX, dirY, dirZ, pax, pay, paz, radius);
            float capB = CapsuleCapDistance(origin, dirX, dirY, dirZ, pbx, pby, pbz, radius);
            if (cap < 0f)
                return capB;
            if (capB < 0f)
                return cap;
            return Math.Min(cap, capB);
        }

        private static float CapsuleCapDistance(
            ColliderVec3 origin,
            float dirX,
            float dirY,
            float dirZ,
            float cx,
            float cy,
            float cz,
            float radius)
        {
            float ocx = origin.X - cx;
            float ocy = origin.Y - cy;
            float ocz = origin.Z - cz;
            float b = dirX * ocx + dirY * ocy + dirZ * ocz;
            float c = ocx * ocx + ocy * ocy + ocz * ocz - radius * radius;
            float h = b * b - c;
            if (h < 0f)
                return -1f;
            return -b - (float)Math.Sqrt(h);
        }

        private static float Component(ColliderVec3 point, int direction)
        {
            if (direction == 0)
                return point.X;
            if (direction == 2)
                return point.Z;
            return point.Y;
        }

        private static float Radial(ColliderVec3 point, ColliderVec3 center, int direction)
        {
            float x = point.X - center.X;
            float y = point.Y - center.Y;
            float z = point.Z - center.Z;
            if (direction == 0)
                return (float)Math.Sqrt(y * y + z * z);
            if (direction == 2)
                return (float)Math.Sqrt(x * x + y * y);
            return (float)Math.Sqrt(x * x + z * z);
        }

        public static float HookLineMass(float baseMass, int count)
        {
            if (baseMass <= 0f)
                baseMass = 0.2f;
            if (count < 1)
                count = 1;
            return Math.Min(baseMass * count, HookLineMassCap);
        }

        public static bool UsePrefabColliderInCrate(int crateId)
        {
            return crateId != 0;
        }

        // The layer a bag's look surface belongs on. The surface is built with the
        // bag, and a bag rebuilt in the hand is on the held layer. Drop and hang move
        // only the bag itself back to the world layer. The surface has to follow, or
        // the look ray skips it and hits only the small core.
        public static int LookSurfaceLayer(int itemLayer)
        {
            return itemLayer;
        }

        // DogEggz Tent and Plank stores a wood pole's length in health, stepped by 0.1 m
        // from 0.2 m to 8 m. Two poles bundle only when that step is the same.
        public const float PoleMinMetres = 0.2f;
        public const float PoleMaxMetres = 8f;
        public const float PoleKgPerMetre = 2f;
        // A tied bundle is a short armful. Poles longer than this are cut down as they join.
        public const float PoleBundleMetres = 1f;

        public static int PoleTenths(float metres)
        {
            if (float.IsNaN(metres) || float.IsInfinity(metres))
                return 20;
            double clamped = metres < PoleMinMetres ? PoleMinMetres : (metres > PoleMaxMetres ? PoleMaxMetres : metres);
            return (int)Math.Round(clamped * 10.0);
        }

        public static float PoleLength(float metres)
        {
            return PoleTenths(metres) / 10f;
        }

        public static bool SamePoleLength(float a, float b)
        {
            return PoleTenths(a) == PoleTenths(b);
        }

        public static float PoleMass(float metres)
        {
            return PoleLength(metres) * PoleKgPerMetre;
        }

        public static string PoleLengthText(float metres)
        {
            return PoleLength(metres).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m";
        }

        public static ColliderBounds BagCoreBounds(
            float plusX,
            float plusZ,
            float minusX,
            float minusZ,
            float rim,
            float widest,
            float neck)
        {
            float coreHeight = Math.Max(0.02f, Math.Min((widest - rim) * 2f, (neck - rim) * 0.5f));
            return new ColliderBounds(
                new ColliderVec3((plusX - minusX) * 0.5f, rim + coreHeight * 0.5f, (plusZ - minusZ) * 0.5f),
                new ColliderVec3(
                    Math.Max(0.05f, (plusX + minusX) * CoreShare),
                    coreHeight,
                    Math.Max(0.05f, (plusZ + minusZ) * CoreShare)));
        }

        private static float Cross(IList<float> height, IList<float> radius, int a, int b, int c)
        {
            float y1 = height[b] - height[a];
            float r1 = radius[b] - radius[a];
            float y2 = height[c] - height[b];
            float r2 = radius[c] - radius[b];
            return y1 * r2 - r1 * y2;
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Clamp01(t);
        }

        private static float InverseLerp(float a, float b, float value)
        {
            if (Math.Abs(b - a) < 0.0000001f)
                return 0f;
            return Clamp01((value - a) / (b - a));
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;
            if (value > 1f)
                return 1f;
            return value;
        }
    }
}
