using System;
using System.Collections.Generic;

namespace Dizzy.FirewoodBundle
{
    // Flat chops in a meat bag. They all face the same way and stack into a tube.
    // The net is pulled in against the chop's outline. The only shape so far is the bag.
    public static class MeatBagLayout
    {
        public const int Bag = 0;

        // Not a const, so adding a shape does not leave a dead branch in NextShape.
        public static readonly int ShapeCount = 1;

        // How much of one chop's thickness separates the next. Just under 1, so the
        // tube is solid and the net is not asked to bridge a gap.
        public const float PitchShare = 0.98f;

        // A box on a chop is this share of the mesh bounds, so it stays inside the meat.
        public const float BoxFit = 0.92f;

        // Slots in an outline: how far the meat reaches, every half degree round the chop.
        public const int OutlineBins = 720;

        // How far the net turns onto each end, and how small that cinch is. The bottom end's
        // circle is set in the config. The top end's is this.
        public const float CapDepth = 0.016f;
        public const float GatherRadius = 0.012f;

        // At the top the strands all meet at the cord that runs up to the hook.
        public const float TopEnd = 0f;

        // The cord rises this far above the point a hung item is pinned at, which is the
        // middle of the lamp hook's peg. The peg is 1.55 cm above its middle at the top, and
        // the game's own lanterns reach 2.4 to 3.3 cm above it, with their handle over the peg.
        public const float HookReach = 0.02f;

        // Setting a held item on a table puts its origin this far above the surface
        // (PickupableItem.furniturePlaceHeight, 0.15 m for most items). A bag hangs down from its
        // origin, so it needs the whole distance down to its foot, and a little over.
        public const float PlaceClearance = 0.01f;

        public static float PlaceHeight(float footY)
        {
            return Math.Max(PlaceClearance, PlaceClearance - footY);
        }

        // The lowest point of the bag, where it rests: the underside of the ring at the bottom.
        public static float FootY(float low, float bottomDepth, float ringRadius)
        {
            return low - bottomDepth - ringRadius;
        }
        public const float DefaultBottomEnd = 0.025f;

        // How far below the meat the bottom ring is, and how far up the meat the hoop is.
        public const float DefaultBottomDepth = 0.008f;
        public const float DefaultHoopHeight = 0f;

        // The cord's inner edge stays this far outside the meat.
        public const float NetGap = 0.004f;

        // The strands of the net are spaced evenly along its path round the meat, so the
        // openings are the same size wherever the chop is wide or narrow. This is how far
        // apart they are by default, in metres, and how few or many a net may have.
        public const float DefaultSpacing = 0.045f;
        public const int MinStrands = 6;
        public const int MaxStrands = 60;

        // The two families of strands turn this share of the way round, in opposite
        // directions, over the net's height.
        public const float TwistShare = 0.55f;

        // How many strands in each family, to be about spacing apart round the meat.
        public static int StrandCount(float perimeter, float spacing)
        {
            if (spacing <= 0f)
                return MaxStrands;
            int count = (int)Math.Round(perimeter / spacing);
            return Math.Max(MinStrands, Math.Min(MaxStrands, count));
        }

        // The outline of the pork or lamb chop, from its silhouette.
        // Made once and shared, so nothing may change it.
        private static float[] _pork;
        private static float[] _lamb;

        public static float[] OutlineOf(bool lamb)
        {
            if (lamb)
                return _lamb ?? (_lamb = OutlineFromEdges(MeatChopShapes.LambEdges, OutlineBins));
            return _pork ?? (_pork = OutlineFromEdges(MeatChopShapes.PorkEdges, OutlineBins));
        }

        // How far a silhouette reaches from its centre in a direction: the furthest an edge
        // is crossed. A notch cut into the edge is followed down to its floor. edges holds
        // x0, z0, x1, z1 for each edge.
        public static float ReachAt(float[] edges, double angle)
        {
            double dx = Math.Cos(angle);
            double dz = Math.Sin(angle);
            double far = 0d;
            for (int i = 0; i + 3 < edges.Length; i += 4)
            {
                double px = edges[i];
                double pz = edges[i + 1];
                double ex = edges[i + 2] - px;
                double ez = edges[i + 3] - pz;
                double denom = dx * ez - dz * ex;
                if (Math.Abs(denom) < 1e-12)
                    continue;
                double t = (px * ez - pz * ex) / denom;
                double s = (px * dz - pz * dx) / denom;
                if (t > far && s >= -1e-9 && s <= 1d + 1e-9)
                    far = t;
            }
            return (float)far;
        }

        // One slot per direction, each the furthest reach within half a slot either side,
        // so the line between two slots never cuts into the meat.
        public static float[] OutlineFromEdges(float[] edges, int bins)
        {
            var outline = new float[bins];
            double half = Math.PI / bins;
            for (int i = 0; i < bins; i++)
            {
                double centre = i * 2d * Math.PI / bins;
                float most = 0f;
                for (int k = -2; k <= 2; k++)
                    most = Math.Max(most, ReachAt(edges, centre + k * half / 2d));
                outline[i] = most;
            }
            return outline;
        }

        public static int NextShape(int shape)
        {
            if (ShapeCount < 2)
                return Bag;
            int next = shape + 1;
            return next >= ShapeCount ? 0 : next;
        }

        public static string ShapeName(int shape)
        {
            return "Meat bag";
        }

        public static void Slot(int index, float thickness, out float y)
        {
            Slot(index, thickness, PitchShare, out y);
        }

        // share is how far apart the chops are, as a share of one chop's thickness. 1 stacks
        // them flush. Less presses them into each other, which closes the gaps between
        // the rounded edges of two chops.
        public static void Slot(int index, float thickness, float share, out float y)
        {
            if (index < 0)
                index = 0;
            if (thickness < 0f)
                thickness = 0f;
            if (share < 0f)
                share = 0f;
            y = index * thickness * share;
        }

        // x and z are relative to the chop's center. Keeps the furthest meat in that direction.
        public static void Note(float[] outline, float x, float z)
        {
            if (outline == null || outline.Length == 0)
                return;
            float radius = (float)Math.Sqrt(x * x + z * z);
            if (radius <= 0.0001f)
                return;
            double angle = Math.Atan2(z, x);
            if (angle < 0d)
                angle += Math.PI * 2d;
            int bin = (int)(angle / (Math.PI * 2d) * outline.Length);
            if (bin < 0 || bin >= outline.Length)
                bin = 0;
            if (radius > outline[bin])
                outline[bin] = radius;
        }

        // Empty directions take the chord between the meat on either side.
        public static void FillGaps(float[] outline)
        {
            if (outline == null)
                return;
            int n = outline.Length;
            int filled = 0;
            for (int i = 0; i < n; i++)
            {
                if (outline[i] > 0f)
                    filled++;
            }
            if (filled == 0 || filled == n)
                return;

            var copy = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (outline[i] > 0f)
                {
                    copy[i] = outline[i];
                    continue;
                }
                int prev = 1;
                while (outline[(i - prev + n * 2) % n] <= 0f)
                    prev++;
                int next = 1;
                while (outline[(i + next) % n] <= 0f)
                    next++;
                float a = outline[(i - prev + n * 2) % n];
                float b = outline[(i + next) % n];
                copy[i] = a + (b - a) * (prev / (float)(prev + next));
            }
            for (int i = 0; i < n; i++)
                outline[i] = copy[i];
        }

        public static float OutlineAt(float[] outline, float angleRadians)
        {
            if (outline == null || outline.Length == 0)
                return 0f;
            int n = outline.Length;
            double turns = angleRadians / (Math.PI * 2d);
            turns -= Math.Floor(turns);
            double pos = turns * n;
            int i = (int)Math.Floor(pos) % n;
            int j = (i + 1) % n;
            double t = pos - Math.Floor(pos);
            return (float)(outline[i] * (1d - t) + outline[j] * t);
        }

        // Steps in a hoop round the net.
        public const int HoopSteps = 160;

        // The heights a strand is drawn at, from bottom to top. A strand is drawn as straight
        // lines between them, so they come close together where it bends fast: over each
        // end, where it pulls in, and wherever it winds quickly round the meat. The edges of
        // the meat are always one, so the strands meet the hoops there.
        public static float[] NetHeights(MeatNetPath path, float bottom, float low, float high, float top)
        {
            const int capSteps = 10;
            const float mostTravel = 0.008f;
            float height = Math.Max(top - bottom, 0.0001f);
            float travel = TwistShare * path.Perimeter * (high - low) / height;
            int body = Math.Max(4, Math.Min(110, (int)Math.Ceiling(travel / mostTravel)));

            var heights = new List<float>(capSteps + body + capSteps + 2);
            for (int i = 0; i < capSteps; i++)
                heights.Add(bottom + (low - bottom) * i / capSteps);
            for (int i = 0; i < body; i++)
                heights.Add(low + (high - low) * i / body);
            for (int i = 0; i <= capSteps; i++)
                heights.Add(high + (top - high) * i / capSteps);

            // The hoop is one too, so the strands meet it.
            float hoop = HoopY(path, low, high);
            if (hoop > low + 0.0001f && hoop < high - 0.0001f)
            {
                heights.Add(hoop);
                heights.Sort();
            }
            return heights.ToArray();
        }

        // The height of the hoop round the meat: up from the bottom of the meat by the set
        // height, and no higher than the top of it.
        public static float HoopY(MeatNetPath path, float low, float high)
        {
            return Math.Min(low + Math.Max(path.HoopHeight, 0f), high);
        }

        // The ring at the bottom of the net: a polygon through the point where each strand
        // ends, closed by repeating the first. x, z pairs.
        public static float[] BottomRing(MeatNetPath path, float bottom, float top, float low, float high)
        {
            int n = path.Strands;
            var ring = new float[(n + 1) * 2];
            for (int i = 0; i <= n; i++)
            {
                float x;
                float z;
                StrandPoint(path, i % n, 0, bottom, bottom, top, low, high, out x, out z);
                ring[i * 2] = x;
                ring[i * 2 + 1] = z;
            }
            return ring;
        }

        // Where one strand is at a height. family is 0 or 1. The net runs from bottom to
        // top, and low and high are where the meat ends. x and z are from the tube's axis.
        public static void StrandPoint(
            MeatNetPath path, int strand, int family, float y, float bottom, float top,
            float low, float high, out float x, out float z)
        {
            float t = top > bottom ? (y - bottom) / (top - bottom) : 0f;
            float sign = family == 0 ? 1f : -1f;
            float place = strand + sign * t * TwistShare * path.Strands;
            Point(path, path.Perimeter * place / path.Strands, y, low, high, out x, out z);
        }

        // Where a hoop round the net is at a given height, step by step.
        public static void HoopPoint(
            MeatNetPath path, int step, int steps, float y, float low, float high, out float x, out float z)
        {
            Point(path, path.Perimeter * step / steps, y, low, high, out x, out z);
        }

        private static void Point(MeatNetPath path, float arc, float y, float low, float high, out float x, out float z)
        {
            float angle = path.AngleAt(arc);
            float radius = NetRadius(path.Outline, angle, y, low, high, path.Cord, path.Gap, path.BottomEnd, path.BottomDepth);
            x = (float)Math.Cos(angle) * radius;
            z = (float)Math.Sin(angle) * radius;
        }

        // Where the cord runs along the tube: just outside the meat.
        public static float BodyRadius(float[] outline, float angleRadians, float cordRadius, float gap)
        {
            float tight = OutlineAt(outline, angleRadians) + cordRadius + gap;
            return tight < GatherRadius ? GatherRadius : tight;
        }

        // Along the tube the cord sits just outside the meat, the way an apple net
        // clears the fruit. Over each end it pulls in to a small circle: bottomEnd wide at the
        // bottom, and a point at the top. A circle wider than the meat is as wide as it.
        public static float NetRadius(
            float[] outline, float angleRadians, float y, float bodyLow, float bodyHigh,
            float cordRadius, float gap, float bottomEnd = GatherRadius, float bottomDepth = CapDepth)
        {
            float tight = BodyRadius(outline, angleRadians, cordRadius, gap);

            float t;
            float end = TopEnd;
            if (y > bodyHigh)
            {
                t = (y - bodyHigh) / CapDepth;
            }
            else if (y < bodyLow)
            {
                t = (bodyLow - y) / Math.Max(bottomDepth, 0.0001f);
                end = bottomEnd;
            }
            else
            {
                return tight;
            }
            if (end > tight)
                end = tight;

            if (t < 0f)
                t = 0f;
            if (t > 1f)
                t = 1f;
            // Most of the cinch happens while the cord is still against the end face.
            float pinch = 1f - (1f - t) * (1f - t);
            return tight + (end - tight) * pinch;
        }
    }
}
