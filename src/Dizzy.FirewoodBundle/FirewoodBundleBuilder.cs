using System.Collections;
using System.Collections.Generic;
using cakeslice;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.FirewoodBundle
{
    internal static class FirewoodBundleBuilder
    {
        private const string StickName = "FirewoodBundleStick";
        private const string StringName = "FirewoodBundleString";
        private const string KnotName = "FirewoodBundleKnot";
        private const string BoxMeshName = "FirewoodBundleBox";
        private const string HullName = "BundleColliderHull";
        // Directions and heights the hull is measured in, how far it sits outside the
        // pieces, and how much of the base's width the core box takes.
        private const int HullSides = 12;
        private const int HullBands = 5;
        private const float HullMargin = 0.01f;
        private const float CoreShare = 0.35f;
        // A bag's flat foot is this share of its rim across, and its collider ends in a
        // ring this far out around the tie.
        private const float BagFoot = 0.7f;
        private const float BagTie = 0.02f;
        private static readonly Dictionary<int, Mesh> HullMeshes = new Dictionary<int, Mesh>();
        private static readonly System.Reflection.FieldInfo SubcollidersField = AccessTools.Field(typeof(ItemRigidbody), "subcolliders");
        private static readonly System.Reflection.FieldInfo BodyField = AccessTools.Field(typeof(ItemRigidbody), "rigidbody");
        private const float RibbonThickness = 0.0016f;
        // Local Y. If a placed sausage tower lies on its side, this is the axis to change.
        private const int StackUpAxis = 1;
        // A pile's slope at steepness 1, height per meter out from the middle, and how
        // many spots each sausage tries before it settles.
        private const float PileSlope = 0.6f;
        // How far below the hang point the first bunch is tied, and how thick its strings are.
        private const float HangTop = 0.05f;
        private const float HangCord = 0.003f;
        // An apple bag's net strands, how far below the hook its neck is tied, and how
        // thick the gathered neck is.
        private const float AppleNetBase = 0.0025f;
        private static float NetWidth
        {
            get { return AppleNetBase * FirewoodBundleConfig.NetThickness; }
        }

        private const float AppleTie = 0.07f;
        // The iron wire of a hanging date rack.
        private const float RackWire = 0.003f;
        private const int RackOuter = 12;
        private const int RackInner = 4;
        private const float RackInnerShare = 0.45f;
        // How far a rack ring tips per unit of imbalance, in degrees. A half-full ring is
        // about a third out of balance, so it tips about 8 degrees.
        private const float RackTip = 25f;
        private const float AppleNeck = 0.012f;
        // Colors of the mod's own parts: the string on hanging sausages, and the iron of
        // a date rack.
        private static readonly Color SausageString = new Color(0.82f, 0.7f, 0.52f);
        private static readonly Color RackIron = new Color(0.06f, 0.06f, 0.06f);
        // A goat cheese net stands this far off the rounds, and allows this much up and
        // down for a round that is not lying quite level.
        private const float RoundGap = 0.004f;
        private const float RoundPad = 0.006f;
        private const int PileTries = 8;

        private static readonly Dictionary<int, int> Stamps = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> HullStamps = new Dictionary<int, int>();
        private static readonly Dictionary<int, Mesh> BoxMeshes = new Dictionary<int, Mesh>();
        private static readonly Dictionary<string, Material> StringMaterials = new Dictionary<string, Material>();

        internal static void RestoreSingle(ShipItem item)
        {
            if (item == null)
                return;

            int id = item.GetInstanceID();
            int stamp = 1;
            int current;
            if (Stamps.TryGetValue(id, out current))
                stamp = current + 1;
            Stamps[id] = stamp;

            ClearSticks(item);
            ClearHull(item);
            Renderer root = item.GetComponent<Renderer>();
            if (root != null)
            {
                root.enabled = true;
                IncludeInLod(item, new List<Renderer> { root });
            }

            ShipItem prefab = FirewoodPieces.PrefabOf(item);
            if (prefab != null)
            {
                item.mass = prefab.mass > 0f ? prefab.mass : item.mass;
                item.holdDistance = prefab.holdDistance;
                item.lookText = prefab.lookText;
                item.description = prefab.description;
                item.big = prefab.big;
                CopyColliders(prefab.gameObject, item.gameObject);
                if (item.itemRigidbodyC != null)
                    CopyColliders(prefab.gameObject, item.itemRigidbodyC.gameObject);
            }
            else
            {
                item.big = false;
            }

            if (item.itemRigidbodyC != null)
                item.itemRigidbodyC.UpdateMass();
            QuietHull(item);

            // A single log is not a nailable item, so the hammer cannot release it.
            if (item.nailed)
                item.nailed = false;

            Mesh box;
            if (BoxMeshes.TryGetValue(id, out box))
            {
                BoxMeshes.Remove(id);
                if (box != null)
                    Object.Destroy(box);
            }

            item.UpdateLookText();
            if (prefab != null)
                item.description = prefab.description;
        }

        internal static void Apply(ShipItem item)
        {
            if (item == null)
                return;
            BundleKind kind = FirewoodPieces.KindOf(item);
            bool hanging = kind != null && kind.Hangs;
            if (!hanging && FirewoodPieces.CountOf(item) <= 1)
                return;

            ApplyNow(item);
            if (Plugin.Instance == null)
                return;

            int id = item.GetInstanceID();
            int stamp = 1;
            int current;
            if (Stamps.TryGetValue(id, out current))
                stamp = current + 1;
            Stamps[id] = stamp;
            Plugin.Instance.StartCoroutine(ApplyLater(item, id, stamp));
        }

        internal static void Forget(ShipItem item)
        {
            if (item == null)
                return;
            int id = item.GetInstanceID();
            Stamps.Remove(id);
            HullStamps.Remove(id);
            Mesh mesh;
            if (BoxMeshes.TryGetValue(id, out mesh))
            {
                BoxMeshes.Remove(id);
                if (mesh != null)
                    Object.Destroy(mesh);
            }

            ReleaseStringMesh(item);
            Mesh hull;
            if (HullMeshes.TryGetValue(id, out hull))
            {
                HullMeshes.Remove(id);
                if (hull != null)
                    Object.Destroy(hull);
            }
        }

        private static IEnumerator ApplyLater(ShipItem item, int id, int stamp)
        {
            for (int i = 0; i < 6; i++)
                yield return new WaitForFixedUpdate();
            yield return new WaitForEndOfFrame();

            int current;
            if (item == null || !Stamps.TryGetValue(id, out current) || current != stamp)
                yield break;

            ApplyNow(item);
            if (Stamps.TryGetValue(id, out current) && current == stamp)
                Stamps.Remove(id);
        }

        private static void ApplyNow(ShipItem item)
        {
            if (FirewoodPieces.KindOf(item) == BundleKind.HangingBanana)
            {
                ApplyBananaBunch(item);
                return;
            }
            if (FirewoodPieces.KindOf(item) != null && FirewoodPieces.KindOf(item).IsBag)
            {
                ApplyAppleBag(item);
                return;
            }
            if (FirewoodPieces.KindOf(item) == BundleKind.HangingSausage || FirewoodPieces.KindOf(item) == BundleKind.HangingDate)
            {
                ApplyHanging(item);
                return;
            }

            int count = FirewoodPieces.CountOf(item);
            if (count <= 1)
                return;

            float cross;
            int longAxis;
            Mesh mesh = null;
            MeshRenderer source = item.GetComponent<MeshRenderer>();
            MeshFilter filter = item.GetComponent<MeshFilter>();
            if (filter != null)
                mesh = filter.sharedMesh;
            if (!FirewoodPieces.TryMeasure(item, out cross, out longAxis))
            {
                cross = 0.08f;
                longAxis = 2;
            }

            BundleKind kind = FirewoodPieces.KindOf(item);
            float pitch = cross * (kind != null ? kind.Spacing : 0.97f);
            List<Quaternion> turns = null;
            Vector3 pieceScale = Vector3.one;
            List<Vector3> centers;
            if (kind == BundleKind.Sausage && mesh != null && SausageStacks.WidthOf(item) == SausageStacks.TreeWidth)
                centers = TreeCenters(item, count, mesh, longAxis, FirewoodBundleConfig.SausageSpacing, out turns);
            else if (kind == BundleKind.Sausage && mesh != null && SausageStacks.WidthOf(item) == SausageStacks.PileWidth)
                centers = PileCenters(item, count, mesh, longAxis, FirewoodBundleConfig.SausageSpacing, out turns);
            else if (kind == BundleKind.Sausage && mesh != null)
                centers = StackCenters(item, count, mesh, longAxis, FirewoodBundleConfig.SausageSpacing);
            else if (kind == BundleKind.Cheese && mesh != null)
                centers = CheeseCenters(count, mesh, out turns, out pieceScale);
            else
                centers = FirewoodPieces.Centers(count, pitch, longAxis);
            ClearSticks(item);
            if (mesh != null && source != null)
            {
                source.enabled = false;
                Material[] materials = source.sharedMaterials;
                var renderers = new List<Renderer>();
                for (int i = 0; i < centers.Count; i++)
                    renderers.Add(CreateStick(item, mesh, materials, centers[i], turns != null ? turns[i] : Quaternion.identity, source, pieceScale));
                // An Auto sausage stack is a neat block, so it gets a cord like firewood.
                bool tiedStack = kind == BundleKind.Sausage && SausageStacks.WidthOf(item) == 0;
                int tieUp = tiedStack ? (longAxis == StackUpAxis ? (StackUpAxis + 1) % 3 : StackUpAxis) : -1;
                if (kind == null || kind.Tie != TieStyle.None || tiedStack)
                {
                    Renderer tie = CreateString(item, centers, mesh, longAxis, cross, source, tieUp);
                    if (tie != null)
                        renderers.Add(tie);
                    Renderer knot = CreateKnot(item, centers, mesh, longAxis, cross, tieUp);
                    if (knot != null)
                        renderers.Add(knot);
                }
                IncludeInLod(item, renderers);
            }

            item.lookText = FirewoodPieces.LookText(item, count);
            ShipItem prefab = FirewoodPieces.PrefabOf(item);
            if (prefab != null)
                item.description = prefab.description;
            bool wasTwoHanded = item.big;
            item.big = true;
            if (!wasTwoHanded)
                AdoptTwoHandedHold(item);
            ApplyMass(item, count);
            ApplyHoldDistance(item, centers, longAxis, cross);
            // A wheel is held by its middle, so it sits further out than one wedge.
            if (kind == BundleKind.Cheese && mesh != null && prefab != null)
                item.holdDistance = prefab.holdDistance + mesh.bounds.size.x * 0.6f;
            int upAxis = longAxis == StackUpAxis ? (StackUpAxis + 1) % 3 : StackUpAxis;
            FitColliders(item, centers, mesh, turns, upAxis);
            // Growing the physics shape while the hull is heeled over shoves the boat.
            // Ignore the hull capsule until the bundle is clear of it.
            QuietHull(item);
        }

        // Cheese wedges sit point to point as a wheel, a set number to the wheel, and full
        // wheels stack with their cuts half a wedge apart. The game's wedge is a slice of a
        // round: its point is at the high end of its long side, on the wheel's axis, and
        // its rind curves around the low end, a wheel's radius away. It is a little wider
        // than a tenth of a circle, so each is drawn narrower or wider to close the wheel.
        private static List<Vector3> CheeseCenters(int count, Mesh mesh, out List<Quaternion> turns, out Vector3 scale)
        {
            Bounds wedge = mesh.bounds;
            int perWheel = FirewoodBundleConfig.CheesePerWheel;
            float radius = wedge.size.x;
            float natural = Mathf.Asin(Mathf.Clamp(wedge.extents.z / Mathf.Max(radius, 0.001f), 0.05f, 0.95f));
            float slot = Mathf.PI / perWheel;
            scale = new Vector3(1f, 1f, Mathf.Tan(slot) / Mathf.Tan(natural));
            Vector3 point = Vector3.Scale(new Vector3(wedge.max.x, 0f, wedge.center.z), scale);
            var centers = new List<Vector3>(count);
            turns = new List<Quaternion>(count);
            for (int i = 0; i < count; i++)
            {
                int wheel = i / perWheel;
                int place = i % perWheel;
                Quaternion turn = Quaternion.AngleAxis((place * 2f + wheel) * slot * Mathf.Rad2Deg, Vector3.up);
                turns.Add(turn);
                // The wedge's point stays on the axis, one wheel's height up for each wheel.
                centers.Add(Vector3.up * (wheel * wedge.size.y) - turn * point);
            }

            return centers;
        }

        // A sausage stack is a tower: rows stack along the sausage's local up, and each
        // axis is spaced by the sausage's own size there, so they sit on each other.
        private static List<Vector3> StackCenters(ShipItem item, int count, Mesh mesh, int longAxis, float pitch)
        {
            int upAxis = longAxis == StackUpAxis ? (StackUpAxis + 1) % 3 : StackUpAxis;
            int columnAxis = 3 - longAxis - upAxis;
            Vector3 size = mesh.bounds.size;
            return FirewoodPieces.Centers(
                count,
                size[columnAxis] * pitch,
                size[upAxis] * pitch,
                columnAxis,
                upAxis,
                SausageStacks.WidthOf(item));
        }

        // A tree grows toward a pyramid in shells. After S shells, layer L holds
        // ceil(S - L / steepness) sausages: steepness 1 makes a pyramid S wide and S high,
        // 0.5 one about half as high, 2 one about twice as high. Each shell adds one
        // sausage to the outer edge of every layer still wide enough, alternating sides,
        // and fills from the bottom up, so a new sausage always has one under it. Layers
        // cross at roughly right angles with some play. A sausage's place depends only on
        // its index and the stack's saved id, so adding one never moves the rest, and a
        // reloaded tree looks the same.
        private static List<Vector3> TreeCenters(
            ShipItem item,
            int count,
            Mesh mesh,
            int longAxis,
            float spacing,
            out List<Quaternion> turns)
        {
            int upAxis = longAxis == StackUpAxis ? (StackUpAxis + 1) % 3 : StackUpAxis;
            int columnAxis = 3 - longAxis - upAxis;
            Vector3 size = mesh.bounds.size;
            Vector3 up = Vector3.zero;
            up[upAxis] = 1f;
            Vector3 along = Vector3.zero;
            along[longAxis] = 1f;
            Vector3 across = Vector3.zero;
            across[columnAxis] = 1f;
            SaveablePrefab save = item.GetComponent<SaveablePrefab>();
            int seed = save != null ? save.instanceId : 0;

            float steepness = FirewoodBundleConfig.PileSteepness;
            var centers = new List<Vector3>(count);
            turns = new List<Quaternion>(count);
            int shell = 1;
            int layer = 0;
            for (int i = 0; i < count; i++)
            {
                if (PileLayerWidth(layer, shell, steepness) < 1)
                {
                    shell++;
                    layer = 0;
                }

                int slot = PileLayerWidth(layer, shell, steepness) - 1;
                // Slots fan out from the middle: 0, then +1, -1, +2, -2...
                int place = slot == 0 ? 0 : ((slot + 1) / 2) * (slot % 2 == 1 ? 1 : -1);
                float layerAngle = (layer % 2) * 90f + (PileHash(seed, layer, 1) - 0.5f) * 50f;
                Quaternion layerTurn = Quaternion.AngleAxis(layerAngle, up);
                Quaternion turn = layerTurn * Quaternion.AngleAxis((PileHash(seed, i, 2) - 0.5f) * 20f, up);
                float side = place * size[columnAxis] * spacing;
                float slide = (PileHash(seed, i, 3) - 0.5f) * size[longAxis] * 0.25f;
                float height = layer * size[upAxis] * spacing;
                centers.Add(layerTurn * (across * side) + turn * (along * slide) + up * height);
                turns.Add(turn);
                layer++;
            }

            return centers;
        }

        // How many sausages layer L holds once the tree has this many shells.
        private static int PileLayerWidth(int layer, int shell, float steepness)
        {
            return Mathf.CeilToInt(shell - layer / steepness - 0.0001f);
        }

        // A pile is sausages dropped one at a time onto the ones already there. Each one
        // tries a few random spots on the heap or just past its edge, pointing any way,
        // and settles where it rests lowest, counting distance from the middle as extra
        // height. That rolls it off high spots into gaps, so the heap grows as a cone with
        // a steady slope; steepness sets that slope. Then it slides in toward the middle
        // for as long as it does not have to climb, so it ends up against the sausages
        // already there. At each spot it lies like a stiff stick on what is under it (see
        // PileShape). Each sausage depends only on the ones before it, so adding one never
        // moves the rest. Spacing sets how thick a sausage counts as, so how close they pack.
        private static List<Vector3> PileCenters(
            ShipItem item,
            int count,
            Mesh mesh,
            int longAxis,
            float spacing,
            out List<Quaternion> turns)
        {
            int upAxis = longAxis == StackUpAxis ? (StackUpAxis + 1) % 3 : StackUpAxis;
            int columnAxis = 3 - longAxis - upAxis;
            Vector3 size = mesh.bounds.size;
            Vector3 up = Vector3.zero;
            up[upAxis] = 1f;
            Vector3 along = Vector3.zero;
            along[longAxis] = 1f;
            Vector3 across = Vector3.zero;
            across[columnAxis] = 1f;
            SaveablePrefab save = item.GetComponent<SaveablePrefab>();
            int seed = save != null ? save.instanceId : 0;

            // The mesh's width is close to the sausage's real thickness; its height also
            // holds the bend along it.
            float thickness = size[columnAxis] * spacing;
            var pile = new PileShape(size[longAxis], thickness, count);
            float slope = PileSlope * FirewoodBundleConfig.PileSteepness;
            var centers = new List<Vector3>(count);
            turns = new List<Quaternion>(count);
            // How far out the heap reaches so far. A sausage lands on it or a short step past.
            float edge = 0f;

            for (int i = 0; i < count; i++)
            {
                float circle = i == 0 ? size[longAxis] * 0.2f : edge + size[longAxis] * 0.4f;
                float bestCost = float.MaxValue;
                Vector2 bestMiddle = Vector2.zero;
                Vector2 bestHeading = Vector2.right;
                float bestHeight = 0f;
                float bestTilt = 0f;
                for (int attempt = 0; attempt < PileTries; attempt++)
                {
                    int draw = i * 16 + attempt;
                    float radius = circle * Mathf.Sqrt(PileHash(seed, draw, 4));
                    float angle = PileHash(seed, draw, 5) * Mathf.PI * 2f;
                    Vector2 middle = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    // Just under 180: turning a sausage exactly backwards has no single rotation.
                    float yaw = PileHash(seed, draw, 6) * 179f * Mathf.Deg2Rad;
                    Vector2 heading = new Vector2(Mathf.Cos(yaw), Mathf.Sin(yaw));
                    float tilt;
                    float height = pile.RestingHeight(middle, heading, out tilt);
                    float cost = height + slope * middle.magnitude;
                    if (cost >= bestCost)
                        continue;
                    bestCost = cost;
                    bestMiddle = middle;
                    bestHeading = heading;
                    bestHeight = height;
                    bestTilt = tilt;
                }

                if (i > 0)
                    SlideIn(pile, thickness * 0.25f, ref bestMiddle, bestHeading, ref bestHeight, ref bestTilt);
                pile.Add(bestMiddle, bestHeading, bestHeight, bestTilt);
                edge = Mathf.Max(edge, bestMiddle.magnitude);

                // Turn about up for the heading, tip along its length for the tilt, and
                // roll it a little so its curve faces different ways.
                Vector3 pointing = along * bestHeading.x + across * bestHeading.y;
                Quaternion heading3D = Quaternion.FromToRotation(along, pointing);
                Quaternion tip = Quaternion.FromToRotation(pointing, (pointing + up * bestTilt).normalized);
                Quaternion roll = Quaternion.AngleAxis((PileHash(seed, i, 7) - 0.5f) * 50f, along);
                centers.Add(along * bestMiddle.x + across * bestMiddle.y + up * bestHeight);
                turns.Add(tip * heading3D * roll);
            }

            return centers;
        }

        // Moves a sausage toward the middle a step at a time while it rests no higher,
        // so it stops against whatever it would have to climb over.
        private static void SlideIn(PileShape pile, float step, ref Vector2 middle, Vector2 heading, ref float height, ref float tilt)
        {
            for (int i = 0; i < 200; i++)
            {
                float radius = middle.magnitude;
                if (radius < step)
                    return;
                Vector2 next = middle - middle / radius * step;
                float nextTilt;
                float nextHeight = pile.RestingHeight(next, heading, out nextTilt);
                if (nextHeight > height + 0.002f)
                    return;
                middle = next;
                height = nextHeight;
                tilt = nextTilt;
            }
        }

        // The sausages already in a pile, flattened onto the ground plane: where each
        // middle is, which way it points, how high its middle sits, and how that height
        // changes per meter along it.
        private sealed class PileShape
        {
            private const int SampleCount = 7;
            private static readonly float MaxTilt = Mathf.Tan(50f * Mathf.Deg2Rad);

            private readonly float _length;
            private readonly float _half;
            private readonly float _thickness;
            private readonly float _near;
            private readonly float[] _along = new float[SampleCount];
            private readonly float[] _under = new float[SampleCount];
            private readonly List<Vector2> _middles;
            private readonly List<Vector2> _headings;
            private readonly List<float> _heights;
            private readonly List<float> _tilts;

            internal PileShape(float length, float thickness, int capacity)
            {
                _length = length;
                _half = length * 0.5f;
                _thickness = thickness;
                // Two sausages whose middles are farther apart than this cannot touch.
                _near = (length + thickness) * (length + thickness);
                for (int k = 0; k < SampleCount; k++)
                    _along[k] = (-0.48f + 0.96f * k / (SampleCount - 1)) * length;
                _middles = new List<Vector2>(capacity);
                _headings = new List<Vector2>(capacity);
                _heights = new List<float>(capacity);
                _tilts = new List<float>(capacity);
            }

            internal void Add(Vector2 middle, Vector2 heading, float height, float tilt)
            {
                _middles.Add(middle);
                _headings.Add(heading);
                _heights.Add(height);
                _tilts.Add(tilt);
            }

            // A sausage here lies like a stiff stick: its middle sits as low as it can while
            // every point along it stays on or above what is under it. That leaves it
            // touching on both sides of its middle, so it does not hang off one end. If it
            // only touches at its middle, it tips until an end comes down.
            internal float RestingHeight(Vector2 middle, Vector2 heading, out float tilt)
            {
                for (int k = 0; k < SampleCount; k++)
                    _under[k] = SupportAt(middle, middle + heading * _along[k]);

                // The lowest middle is set by one support on each side of it, or by the
                // tilt limit, so those are the only tilts worth checking.
                float bestHeight = HeightAt(-MaxTilt);
                tilt = -MaxTilt;
                Consider(MaxTilt, ref bestHeight, ref tilt);
                for (int a = 0; a < SampleCount; a++)
                {
                    for (int b = a + 1; b < SampleCount; b++)
                    {
                        if ((_along[a] < 0f) == (_along[b] < 0f))
                            continue;
                        float through = (_under[a] - _under[b]) / (_along[a] - _along[b]);
                        Consider(Mathf.Clamp(through, -MaxTilt, MaxTilt), ref bestHeight, ref tilt);
                    }
                }

                return bestHeight;
            }

            private void Consider(float tilt, ref float bestHeight, ref float bestTilt)
            {
                float height = HeightAt(tilt);
                bool lower = height < bestHeight - 0.000001f;
                bool sameButTipped = height <= bestHeight + 0.000001f && Mathf.Abs(tilt) > Mathf.Abs(bestTilt);
                if (!lower && !sameButTipped)
                    return;
                bestHeight = height;
                bestTilt = tilt;
            }

            // How high the middle must sit at this tilt to clear every point under it.
            private float HeightAt(float tilt)
            {
                float height = 0f;
                for (int k = 0; k < SampleCount; k++)
                    height = Mathf.Max(height, _under[k] - tilt * _along[k]);
                return height;
            }

            // Two round sausages touch higher the more directly one is over the other: one
            // straight on top sits a full thickness up, one beside it rides up only a little
            // and settles into the groove.
            private float SupportAt(Vector2 middle, Vector2 point)
            {
                float support = 0f;
                float reach = _thickness * _thickness;
                for (int j = 0; j < _middles.Count; j++)
                {
                    if ((middle - _middles[j]).sqrMagnitude > _near)
                        continue;
                    float t = Mathf.Clamp(Vector2.Dot(point - _middles[j], _headings[j]), -_half, _half);
                    float apart = (point - (_middles[j] + _headings[j] * t)).sqrMagnitude;
                    if (apart >= reach)
                        continue;
                    support = Mathf.Max(support, _heights[j] + _tilts[j] * t + Mathf.Sqrt(reach - apart));
                }

                return support;
            }
        }

        private static float PileHash(int seed, int index, int salt)
        {
            unchecked
            {
                uint h = (uint)(seed * 73856093) ^ (uint)(index * 19349663) ^ (uint)(salt * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        // How far a sausage stack's cord top comes down: 15% of a sausage's thickness.
        private static float StackTopDrop(Mesh mesh, int upAxis)
        {
            return mesh.bounds.size[upAxis] * 0.15f;
        }

        // A hanging bundle: a central string drops from the hook, and the sausages hang
        // straight down around it in bunches, each on a short string tied to it. A bunch
        // holds Bunch Size sausages on a ring wide enough that they do not touch; when it
        // is full the next bunch starts lower and a little wider, turned half a place so
        // its sausages hang between the ones above. A sausage's place depends only on its
        // index, so adding one never moves the rest. The item's origin is the hang point.
        private static void ApplyHanging(ShipItem item)
        {
            MeshRenderer source = item.GetComponent<MeshRenderer>();
            MeshFilter filter = item.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            float cross;
            int longAxis;
            if (mesh == null || source == null || !FirewoodPieces.TryMeasure(item, out cross, out longAxis))
                return;

            int count = FirewoodPieces.CountOf(item);
            // The hook keeps the item's own up pointing up, so that is the way down the
            // strings, even for a piece modelled standing on end like a date skewer.
            int upAxis = StackUpAxis;
            int sideAxis = longAxis == upAxis ? (upAxis + 1) % 3 : 3 - longAxis - upAxis;
            int otherAxis = longAxis == upAxis ? (upAxis + 2) % 3 : longAxis;
            Vector3 up = Vector3.zero;
            up[upAxis] = 1f;
            Vector3 along = Vector3.zero;
            along[longAxis] = 1f;
            Vector3 sideA = Vector3.zero;
            sideA[sideAxis] = 1f;
            Vector3 sideB = Vector3.zero;
            sideB[otherAxis] = 1f;
            Bounds stick = mesh.bounds;
            float thickness = (longAxis == upAxis ? Mathf.Min(stick.size[sideAxis], stick.size[otherAxis]) : stick.size[sideAxis]) * 0.88f;
            // A date rack layer is a pack of 16: an outer ring of 12 and an inner one of 4.
            bool isRack = FirewoodPieces.KindOf(item) == BundleKind.HangingDate;
            int perBunch = isRack ? RackOuter + RackInner : FirewoodBundleConfig.BunchSize;
            float rackRadius = thickness / (2f * Mathf.Sin(Mathf.PI / RackOuter)) * FirewoodBundleConfig.BunchRadius;
            float ring = thickness / (2f * Mathf.Sin(Mathf.PI / perBunch)) * FirewoodBundleConfig.BunchRadius;
            float drop = thickness * 1.5f * (isRack ? FirewoodBundleConfig.DateLayerSpacing : FirewoodBundleConfig.BunchDrop);
            // Every bunch is tied close to the middle string, at the same radius. Each
            // sausage leans out from its top just enough that, one bunch down, it has
            // cleared the next bunch's tops, which sit in the gaps half a step around.
            float gap = Mathf.PI / perBunch;
            float cosGap = Mathf.Cos(gap);
            float clear = ring * cosGap + Mathf.Sqrt(Mathf.Max(0f, thickness * thickness - ring * ring * (1f - cosGap * cosGap)));
            float leanAngle = Mathf.Asin(Mathf.Clamp01(Mathf.Max(0f, clear - ring) / drop)) * FirewoodBundleConfig.HangFlare;
            leanAngle = Mathf.Min(leanAngle, 45f * Mathf.Deg2Rad);
            SaveablePrefab save = item.GetComponent<SaveablePrefab>();
            int seed = save != null ? save.instanceId : 0;

            ClearSticks(item);
            ClearHull(item);
            source.enabled = false;
            Material[] materials = source.sharedMaterials;
            // Tan cord, matte like cord rather than a ribbon. White stood out too brightly
            // against the sausages.
            Material cord = PlainMaterial("string", SausageString);
            var renderers = new List<Renderer>();
            var centers = new List<Vector3>(count);
            var turns = new List<Quaternion>(count);
            // Sausages hang with their long axis down. The mesh's low end along that
            // axis is the one tied, so it goes to the top.
            // A date skewer is tied by its other end, so its dates hang the right way up.
            bool flip = FirewoodPieces.KindOf(item) == BundleKind.HangingDate;
            Quaternion hang = Quaternion.FromToRotation(flip ? -along : along, -up);
            Vector3 tiedEnd = stick.center;
            tiedEnd[longAxis] = flip ? stick.max[longAxis] : stick.min[longAxis];
            float lowest = 0f;
            // Dates hang from a little iron rack instead: each bunch is a ring held out
            // from the middle cord by spokes, and the skewers hang straight down from it.
            bool rack = flip;
            Material iron = rack ? PlainMaterial("iron", RackIron) : null;
            int lastRing = -1;
            Quaternion tip = Quaternion.identity;

            for (int i = 0; i < count; i++)
            {
                int bunch = i / perBunch;
                int place = i % perBunch;
                float knot = -(HangTop + bunch * drop);
                // A bunch that is not full yet closes up around the string: its ring fits
                // the sausages it has, not the ones it could hold.
                int inBunch = Mathf.Min(perBunch, count - bunch * perBunch);
                float radius = inBunch > 1
                    ? thickness / (2f * Mathf.Sin(Mathf.PI / inBunch)) * FirewoodBundleConfig.BunchRadius
                    : thickness * 0.5f * FirewoodBundleConfig.BunchRadius;
                // A rack keeps its full ring: taking skewers off leaves empty slots, and the
                // ring tips toward the side still carrying them.
                float angle = (place + (bunch % 2) * 0.5f) * Mathf.PI * 2f / inBunch;
                if (rack)
                    RackSlot(place, bunch, rackRadius, out angle, out radius);
                Vector3 outward = sideA * Mathf.Cos(angle) + sideB * Mathf.Sin(angle);
                // The string runs from the knot out to the sausage's top. Its length sets
                // how far below the knot the sausage hangs; the bunch's radius stays put.
                float reach = Mathf.Sqrt(radius * radius + thickness * thickness * 0.36f) * FirewoodBundleConfig.HangStringLength;
                float tie = rack ? 0f : Mathf.Sqrt(Mathf.Max(0f, reach * reach - radius * radius));
                Vector3 top = outward * radius + up * (knot - tie);
                if (rack && bunch != lastRing)
                {
                    lastRing = bunch;
                    Vector3 weight = Vector3.zero;
                    for (int k = 0; k < inBunch; k++)
                    {
                        float a;
                        float r;
                        RackSlot(k, bunch, rackRadius, out a, out r);
                        weight += (sideA * Mathf.Cos(a) + sideB * Mathf.Sin(a)) * (r / rackRadius);
                    }
                    weight /= perBunch;
                    tip = weight.sqrMagnitude > 0.000001f
                        ? Quaternion.AngleAxis(weight.magnitude * RackTip, Vector3.Cross(up, weight.normalized))
                        : Quaternion.identity;
                }
                if (rack)
                    top = up * knot + tip * (outward * radius);
                if (rack && bunch == lastRing && place == 0 && iron != null)
                {
                    for (int hoopIndex = 0; hoopIndex < 2; hoopIndex++)
                    {
                        float hoopRadius = hoopIndex == 0 ? rackRadius : rackRadius * RackInnerShare;
                        var hoop = new List<Vector3>(33);
                        for (int k = 0; k <= 32; k++)
                        {
                            float a = k * Mathf.PI * 2f / 32f;
                            hoop.Add(tip * ((sideA * Mathf.Cos(a) + sideB * Mathf.Sin(a)) * hoopRadius) + up * knot);
                        }
                        Renderer ringRenderer = CreateCordLine(item, iron, hoop, RackWire);
                        if (ringRenderer != null)
                            renderers.Add(ringRenderer);
                    }
                    for (int spoke = 0; spoke < 3; spoke++)
                    {
                        float a = spoke * Mathf.PI * 2f / 3f + bunch * 0.5f;
                        Vector3 rim = tip * ((sideA * Mathf.Cos(a) + sideB * Mathf.Sin(a)) * rackRadius) + up * knot;
                        Renderer spokeRenderer = CreateCordPiece(item, iron, up * knot, rim, upAxis, RackWire);
                        if (spokeRenderer != null)
                            renderers.Add(spokeRenderer);
                    }
                }
                Quaternion spin = Quaternion.AngleAxis(PileHash(seed, i, 8) * 360f, up);
                Quaternion sway = Quaternion.AngleAxis((PileHash(seed, i, 9) - 0.5f) * 8f, outward);
                Quaternion lean = Quaternion.FromToRotation(-up, (-up * Mathf.Cos(leanAngle) + outward * Mathf.Sin(leanAngle)).normalized);
                // A rack skewer tilts out a few degrees, a little differently each, so the
                // ends of one ring clear the ring below.
                float splay = (2f + PileHash(seed, i, 18) * 3f) * Mathf.Deg2Rad;
                Quaternion outTilt = Quaternion.FromToRotation(-up, (-up * Mathf.Cos(splay) + outward * Mathf.Sin(splay)).normalized);
                Quaternion jiggle = Quaternion.AngleAxis((PileHash(seed, i, 19) - 0.5f) * 4f, outward);
                Quaternion turn = rack ? outTilt * jiggle * spin * hang : lean * sway * spin * hang;
                Vector3 place3D = top - turn * tiedEnd;
                renderers.Add(CreateStick(item, mesh, materials, place3D, turn, source));
                centers.Add(place3D);
                turns.Add(turn);
                lowest = Mathf.Min(lowest, knot);
                if (cord != null && !rack)
                    renderers.Add(CreateCordPiece(item, cord, up * knot, top, upAxis));
            }

            Material middle = rack && iron != null ? iron : cord;
            if (middle != null)
                renderers.Add(CreateCordPiece(item, middle, Vector3.zero, up * lowest, sideAxis, rack ? RackWire : HangCord));
            IncludeInLod(item, renderers);

            item.lookText = FirewoodPieces.LookText(item, count);
            item.description = "";
            item.big = false;
            ShipItem prefab = FirewoodPieces.PrefabOf(item);
            if (prefab != null)
                item.holdDistance = prefab.holdDistance + ring;
            ApplyMass(item, count);
            Bounds bundle = BundleBounds(centers, mesh, turns);
            bundle.Encapsulate(Vector3.zero);
            bundle.Expand(0.01f);
            FitBox(item, bundle);
            if (item.GetComponent<HangableItem>() == null)
                item.gameObject.AddComponent<HangableItem>();
            QuietHull(item);
        }

        // A banana bunch hangs like one on the tree turned upside down: a stalk drops
        // from the hook, and the bananas grow from it in hands, rings of Hand Size at one
        // height. Each banana's stem meets the stalk and the banana points down and out,
        // curving back in toward the stalk at its tip. A hand that is not full closes up.
        private static void ApplyBananaBunch(ShipItem item)
        {
            MeshRenderer source = item.GetComponent<MeshRenderer>();
            MeshFilter filter = item.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            float cross;
            int longAxis;
            if (mesh == null || source == null || !FirewoodPieces.TryMeasure(item, out cross, out longAxis))
                return;

            int count = FirewoodPieces.CountOf(item);
            int upAxis = longAxis == StackUpAxis ? (StackUpAxis + 1) % 3 : StackUpAxis;
            int sideAxis = 3 - longAxis - upAxis;
            Vector3 up = Vector3.zero;
            up[upAxis] = 1f;
            Vector3 along = Vector3.zero;
            along[longAxis] = 1f;
            Vector3 sideA = Vector3.zero;
            sideA[sideAxis] = 1f;
            Vector3 sideB = Vector3.zero;
            sideB[longAxis] = 1f;
            Bounds stick = mesh.bounds;
            float thickness = stick.size[sideAxis];
            float length = stick.size[longAxis];
            int perHand = FirewoodBundleConfig.BananaHandSize;
            float drop = length * 0.3f * FirewoodBundleConfig.BananaHandDrop;
            float splay = FirewoodBundleConfig.BananaSplay * Mathf.Deg2Rad;
            // A small bunch hangs close together; it opens out as hands are added, so a
            // lone hand does not spread flat like a flower.
            int hands = Mathf.Max(1, Mathf.CeilToInt(count / (float)perHand));
            splay *= Mathf.Lerp(0.45f, 1f, Mathf.Clamp01((hands - 1) / 2f));
            float stalkRadius = Mathf.Max(0.006f, thickness * 0.14f);
            float stemWidth = thickness * 0.4f;
            SaveablePrefab save = item.GetComponent<SaveablePrefab>();
            int seed = save != null ? save.instanceId : 0;

            ClearSticks(item);
            ClearHull(item);
            source.enabled = false;
            Material[] materials = source.sharedMaterials;
            Material stalk = StringMaterial(BundleKind.Firewood, 0);
            var renderers = new List<Renderer>();
            var centers = new List<Vector3>(count);
            var turns = new List<Quaternion>(count);
            // The game's banana has its thin stem at the high end of its long axis and
            // curves toward its own up, so that side is the inside of the curve.
            Vector3 stemEnd = stick.center;
            stemEnd[longAxis] = stick.max[longAxis];
            Quaternion meshFrame = Quaternion.LookRotation(along, up);
            float lowest = 0f;
            float widest = 0f;

            for (int i = 0; i < count; i++)
            {
                int hand = i / perHand;
                int place = i % perHand;
                int inHand = Mathf.Min(perHand, count - hand * perHand);
                float knot = -(HangTop + stalkRadius * 2f + hand * drop);
                float ring = stalkRadius + (inHand > 1 ? Mathf.Max(0f, stemWidth / (2f * Mathf.Sin(Mathf.PI / inHand)) - stalkRadius) : 0f);
                float angle = (place + (hand % 2) * 0.5f) * Mathf.PI * 2f / inHand;
                angle += (PileHash(seed, i, 10) - 0.5f) * 0.25f * Mathf.PI * 2f / inHand;
                Vector3 outward = sideA * Mathf.Cos(angle) + sideB * Mathf.Sin(angle);
                // Each hand down points a little steeper, so the bunch tapers to its tip.
                float handSplay = splay / (1f + 0.3f * hand);
                float tilt = Mathf.Clamp(handSplay + (PileHash(seed, i, 11) - 0.5f) * 8f * Mathf.Deg2Rad, 0f, 85f * Mathf.Deg2Rad);
                // Points down and out; the inside of its curve faces down and in.
                Vector3 pointing = -up * Mathf.Cos(tilt) + outward * Mathf.Sin(tilt);
                Vector3 inside = -up * Mathf.Sin(tilt) - outward * Mathf.Cos(tilt);
                Quaternion twist = Quaternion.AngleAxis((PileHash(seed, i, 12) - 0.5f) * 20f, pointing);
                Quaternion turn = twist * Quaternion.LookRotation(-pointing, inside) * Quaternion.Inverse(meshFrame);
                Vector3 stem = outward * ring + up * knot;
                Vector3 place3D = stem - turn * stemEnd;
                renderers.Add(CreateStick(item, mesh, materials, place3D, turn, source));
                centers.Add(place3D);
                turns.Add(turn);
                lowest = Mathf.Min(lowest, knot);
                widest = Mathf.Max(widest, ring + length * Mathf.Sin(tilt));
            }

            if (stalk != null)
                renderers.Add(CreateCordPiece(item, stalk, Vector3.zero, up * (lowest - stalkRadius * 3f), sideAxis, stalkRadius));
            IncludeInLod(item, renderers);

            item.lookText = FirewoodPieces.LookText(item, count);
            item.description = "";
            item.big = false;
            ShipItem prefab = FirewoodPieces.PrefabOf(item);
            if (prefab != null)
                item.holdDistance = prefab.holdDistance + widest * 0.5f;
            ApplyMass(item, count);
            Bounds bundle = BundleBounds(centers, mesh, turns);
            bundle.Encapsulate(Vector3.zero);
            bundle.Expand(0.01f);
            FitBox(item, bundle);
            if (item.GetComponent<HangableItem>() == null)
                item.gameObject.AddComponent<HangableItem>();
            QuietHull(item);
        }

        // An apple bag: apples settle in layers at the bottom of a net, each layer one
        // apple smaller than the one under it, and the net closes over them to a neck
        // tied below the hook. The same shape hangs from a hook or sits on the deck.
        // Oranges use it as it is. Goat cheese is a flat round, not a ball, so its rounds
        // lie flat, each layer resting on the one under it, and its net is pulled taut
        // around the rounds' real outline.
        private static void ApplyAppleBag(ShipItem item)
        {
            MeshRenderer source = item.GetComponent<MeshRenderer>();
            MeshFilter filter = item.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || source == null)
                return;

            int count = FirewoodPieces.CountOf(item);
            Bounds apple = mesh.bounds;
            BundleKind bagKind = FirewoodPieces.KindOf(item);
            bool rounds = bagKind == BundleKind.GoatCheeseBag;
            float radius = rounds
                ? Mathf.Max(apple.extents.x, apple.extents.z) * 1.01f
                : Mathf.Max(apple.extents.x, Mathf.Max(apple.extents.y, apple.extents.z)) * 0.92f;
            int baseLayer = rounds
                ? FirewoodBundleConfig.GoatCheeseLayerSize
                : bagKind == BundleKind.OrangeBag ? FirewoodBundleConfig.OrangeLayerSize : FirewoodBundleConfig.AppleLayerSize;
            SaveablePrefab save = item.GetComponent<SaveablePrefab>();
            int seed = save != null ? save.instanceId : 0;

            // Fill from the bottom: layer L holds baseLayer - L apples, at least one.
            var layerSizes = new List<int>();
            int left = count;
            while (left > 0)
            {
                int size = Mathf.Min(left, Mathf.Max(1, baseLayer - layerSizes.Count));
                layerSizes.Add(size);
                left -= size;
            }

            // A bag stays at least three rows tall: too few for that at full width are
            // shared out over three rows, the extra ones going to the lower rows.
            if (count >= 3 && layerSizes.Count < 3)
            {
                layerSizes.Clear();
                for (int row = 0; row < 3; row++)
                    layerSizes.Add(count / 3 + (row < count % 3 ? 1 : 0));
            }

            // A lone apple on top looks lost, so it gets one from the layer under it.
            int last = layerSizes.Count - 1;
            if (last > 0 && layerSizes[last] == 1 && layerSizes[last - 1] > 2)
            {
                layerSizes[last] = 2;
                layerSizes[last - 1]--;
            }

            // Two rounds of cheese stack rather than sit side by side, and four put their
            // pair in the middle row, between two single rounds.
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

            int layers = layerSizes.Count;
            float step = rounds ? apple.size.y * 1.03f : radius * 1.6f;
            float neck = -AppleTie;
            float topCenter = neck - (rounds ? apple.extents.y + radius * 0.9f : radius * 2.4f);
            var layerY = new float[layers];
            var layerRing = new float[layers];
            for (int layer = 0; layer < layers; layer++)
            {
                layerY[layer] = topCenter - (layers - 1 - layer) * step;
                layerRing[layer] = RingRadius(layerSizes[layer], radius);
            }

            ClearSticks(item);
            ClearHull(item);
            source.enabled = false;
            Material[] materials = source.sharedMaterials;
            var renderers = new List<Renderer>();
            var centers = new List<Vector3>(count);
            var turns = new List<Quaternion>(count);
            // Where each layer's fruit sit across the bag, for the net to wrap.
            var layerPlaces = new List<Vector2>[layers];
            int index = 0;
            for (int layer = 0; layer < layers; layer++)
            {
                layerPlaces[layer] = new List<Vector2>();
                int size = layerSizes[layer];
                bool middle = size >= 7;
                int around = middle ? size - 1 : size;
                float ring = middle ? radius * 2f : layerRing[layer];
                for (int k = 0; k < size; k++, index++)
                {
                    Vector3 place;
                    if (middle && k == size - 1)
                    {
                        place = new Vector3(0f, layerY[layer], 0f);
                    }
                    else
                    {
                        float angle = (k + layer * 0.5f) * Mathf.PI * 2f / Mathf.Max(1, around);
                        float r = around > 1 ? ring : 0f;
                        place = new Vector3(Mathf.Cos(angle) * r, layerY[layer], Mathf.Sin(angle) * r);
                    }
                    place += new Vector3(PileHash(seed, index, 13) - 0.5f, 0f, PileHash(seed, index, 14) - 0.5f) * radius * (rounds ? 0.04f : 0.12f);
                    layerPlaces[layer].Add(new Vector2(place.x, place.z));
                    Quaternion turn = Quaternion.AngleAxis(PileHash(seed, index, 15) * 360f, Vector3.up)
                        * Quaternion.AngleAxis((PileHash(seed, index, 16) - 0.5f) * (rounds ? 4f : 50f), Vector3.right);
                    Vector3 position = place - turn * apple.center;
                    renderers.Add(CreateStick(item, mesh, materials, position, turn, source));
                    centers.Add(position);
                    turns.Add(turn);
                }
            }

            // The net follows the heap: around each layer it bulges over the apples, above
            // the top layer it closes in to the neck, and under the bottom one it rounds off.
            // The net clears the whole apple, tilted and nudged, not just its packing size.
            float reach = Mathf.Max(apple.extents.x, Mathf.Max(apple.extents.y, apple.extents.z)) * 1.02f + radius * 0.03f;
            // The orange is a low-poly ball whose faces sit well inside its corners.
            if (bagKind == BundleKind.OrangeBag)
                reach *= 0.9f;
            // The sides come down to a rim just under the bottom fruit, then a nearly flat
            // floor runs in to the knot in the middle.
            float rim = layerY[0] - reach * 0.95f;
            float bottom = rim - reach * 0.3f;
            // Under a round of cheese the rim is at its bottom face and the floor is flatter.
            if (rounds)
            {
                rim = layerY[0] - (apple.extents.y + RoundPad);
                bottom = rim - 0.012f;
            }
            float topLayer = layerY[layers - 1];
            // How far a layer's fruit reach out in one direction across the bag, so a
            // full ring keeps the net round and two side by side give it an oval.
            System.Func<int, Vector2, float> across = (layer, way) =>
            {
                float most = float.MinValue;
                List<Vector2> places = layerPlaces[layer];
                for (int i = 0; i < places.Count; i++)
                    most = Mathf.Max(most, Vector2.Dot(places[i], way));
                return Mathf.Max(0f, most);
            };

            // The net follows the heap in every direction: around each layer it bulges over
            // the fruit, under the bottom one it rounds into a flat floor, and over the top it
            // wraps the top layer, then leaves along the line that just touches it and runs
            // straight to the neck.
            System.Func<float, Vector2, float> fruitProfile = (y, way) =>
            {
                if (y < layerY[0])
                {
                    float wall = across(0, way) + reach;
                    if (y >= rim)
                    {
                        float d = (layerY[0] - y) / (layerY[0] - rim);
                        return wall * (1f - 0.15f * d * d);
                    }
                    return wall * 0.85f * Mathf.InverseLerp(bottom, rim, y);
                }
                float widest = 0f;
                for (int layer = 0; layer < layers; layer++)
                {
                    float d = (y - layerY[layer]) / (reach * 1.25f);
                    if (d * d < 1f)
                        widest = Mathf.Max(widest, across(layer, way) + reach * Mathf.Sqrt(1f - d * d));
                }
                float topRing = across(layers - 1, way);
                // Between two layers the net runs straight from one's width to the next, so it
                // narrows with the heap instead of dipping in between or staying wide.
                for (int layer = 0; layer + 1 < layers; layer++)
                {
                    if (y < layerY[layer] || y > layerY[layer + 1])
                        continue;
                    float t = Mathf.InverseLerp(layerY[layer], layerY[layer + 1], y);
                    widest = Mathf.Max(widest, Mathf.Lerp(across(layer, way), across(layer + 1, way), t) + reach);
                    break;
                }
                if (y >= topLayer)
                {
                    Vector2 neckPoint = new Vector2(AppleNeck, neck);
                    Vector2 topCircle = new Vector2(topRing, topLayer);
                    Vector2 toNeck = neckPoint - topCircle;
                    float apart = toNeck.magnitude;
                    Vector2 touch = topCircle + new Vector2(reach, 0f);
                    if (apart > reach)
                    {
                        float heading = Mathf.Atan2(toNeck.y, toNeck.x);
                        float swing = Mathf.Acos(reach / apart);
                        Vector2 a = topCircle + new Vector2(Mathf.Cos(heading - swing), Mathf.Sin(heading - swing)) * reach;
                        Vector2 b = topCircle + new Vector2(Mathf.Cos(heading + swing), Mathf.Sin(heading + swing)) * reach;
                        touch = a.x > b.x ? a : b;
                    }
                    float over;
                    if (y <= touch.y)
                    {
                        float dy = y - topLayer;
                        over = topRing + Mathf.Sqrt(Mathf.Max(0f, reach * reach - dy * dy));
                    }
                    else
                    {
                        over = Mathf.Lerp(touch.x, AppleNeck, Mathf.InverseLerp(touch.y, neck, y));
                    }
                    widest = Mathf.Max(widest, over);
                }
                return widest;
            };

            // A net around rounds of cheese is pulled taut: it lies on the rounds' own
            // outline where it touches them and runs straight between those places, so it
            // bridges under an overhanging round instead of cutting through it. Heights are
            // taken at the same steps the strands are drawn at, from the rim up to the neck.
            int netSamples = rounds ? 48 : 28;
            float[,] roundSide = null;
            float[] grid = null;
            float[] layerOut = null;
            if (rounds)
            {
                float half = apple.extents.y;
                float wide = Mathf.Max(apple.extents.x, apple.extents.z);
                roundSide = new float[layers, netSamples + 1];
                grid = new float[netSamples + 1];
                layerOut = new float[layers];
                for (int layer = 0; layer < layers; layer++)
                {
                    for (int i = 0; i <= netSamples; i++)
                    {
                        float up = Mathf.Lerp(rim, neck, i / (float)netSamples) - layerY[layer];
                        // Outside the round's height it does not hold the net out at all.
                        if (Mathf.Abs(up) > half + RoundPad + 0.0005f)
                        {
                            roundSide[layer, i] = -1f;
                            continue;
                        }
                        // The widest the round gets within a little above and below, so a
                        // slightly tilted round still fits.
                        float most = 0f;
                        for (int k = -1; k <= 1; k++)
                            most = Mathf.Max(most, RoundWidth(Mathf.Clamp((up + k * RoundPad) / half, -1f, 1f)));
                        roundSide[layer, i] = wide * most + RoundGap;
                    }
                }
            }

            System.Func<float, Vector2, float> roundsProfile = (y, way) =>
            {
                for (int layer = 0; layer < layers; layer++)
                    layerOut[layer] = across(layer, way);
                for (int i = 0; i <= netSamples; i++)
                {
                    float width = i == netSamples ? AppleNeck : 0f;
                    for (int layer = 0; layer < layers; layer++)
                    {
                        if (roundSide[layer, i] >= 0f)
                            width = Mathf.Max(width, layerOut[layer] + roundSide[layer, i]);
                    }
                    grid[i] = width;
                }

                // Under the rim the floor runs in to the knot.
                if (y < rim)
                    return grid[0] * Mathf.InverseLerp(bottom, rim, y);
                int at = Mathf.Clamp(Mathf.RoundToInt(Mathf.InverseLerp(rim, neck, y) * netSamples), 0, netSamples);
                float taut = grid[at];
                for (int below = 0; below < at; below++)
                {
                    for (int above = at + 1; above <= netSamples; above++)
                        taut = Mathf.Max(taut, Mathf.Lerp(grid[below], grid[above], (at - below) / (float)(above - below)));
                }
                return taut;
            };

            System.Func<float, Vector2, float> profile = rounds ? roundsProfile : fruitProfile;

            Material net = StringMaterial(bagKind, FirewoodPieces.ColorOf(item));
            if (net != null)
            {
                const int strands = 14;
                int samples = netSamples;
                float twist = Mathf.PI * 1.1f;
                for (int family = 0; family < 2; family++)
                {
                    float sign = family == 0 ? 1f : -1f;
                    for (int strand = 0; strand < strands; strand++)
                    {
                        var points = new List<Vector3>(samples + 4);
                        // A few points across the floor, one right on the rim, the rest up the side.
                        for (int sample = -3; sample <= samples; sample++)
                        {
                            float y = sample < 0
                                ? Mathf.Lerp(bottom, rim, (sample + 3) / 3f)
                                : Mathf.Lerp(rim, neck, sample / (float)samples);
                            float t = Mathf.InverseLerp(bottom, neck, y);
                            float angle = strand * Mathf.PI * 2f / strands + sign * t * twist;
                            float r = profile(y, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))) + NetWidth;
                            points.Add(new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r));
                        }
                        Renderer line = CreateCordLine(item, net, points);
                        if (line != null)
                            renderers.Add(line);
                    }
                }

                // A cord around the base, where the sides turn under into the floor.
                var hoop = new List<Vector3>(49);
                for (int k = 0; k <= 48; k++)
                {
                    float angle = k * Mathf.PI * 2f / 48f;
                    var way = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float r = profile(rim, way) + NetWidth * 2f;
                    hoop.Add(new Vector3(way.x * r, rim, way.y * r));
                }
                Renderer band = CreateCordLine(item, net, hoop);
                if (band != null)
                    renderers.Add(band);

                // A small knot where the strands meet under the bag, with short loose ends.
                Renderer knot = CreateCordPiece(item, net, new Vector3(0f, bottom - 0.012f, 0f), new Vector3(0f, bottom + 0.002f, 0f), 0, AppleNeck * 0.75f);
                if (knot != null)
                    renderers.Add(knot);
                for (int end = 0; end < 3; end++)
                {
                    float angle = end * Mathf.PI * 2f / 3f + 0.4f;
                    Vector3 tip = new Vector3(Mathf.Cos(angle) * 0.012f, bottom - 0.03f, Mathf.Sin(angle) * 0.012f);
                    Renderer tail = CreateCordPiece(item, net, new Vector3(0f, bottom - 0.01f, 0f), tip, 1, NetWidth * 1.3f);
                    if (tail != null)
                        renderers.Add(tail);
                }

                // The gathered neck, a tuft above it, and the loop up to the hook.
                Renderer tie = CreateCordPiece(item, net, new Vector3(0f, neck - 0.01f, 0f), new Vector3(0f, neck + 0.012f, 0f), 0, AppleNeck);
                if (tie != null)
                    renderers.Add(tie);
                for (int tuft = 0; tuft < 6; tuft++)
                {
                    float angle = tuft * Mathf.PI / 3f;
                    Vector3 end = new Vector3(Mathf.Cos(angle) * 0.03f, neck + 0.03f, Mathf.Sin(angle) * 0.03f);
                    Renderer piece = CreateCordPiece(item, net, new Vector3(0f, neck + 0.01f, 0f), end, 1, NetWidth * 1.5f);
                    if (piece != null)
                        renderers.Add(piece);
                }
                Renderer loop = CreateCordPiece(item, net, new Vector3(0f, neck, 0f), Vector3.zero, 0, NetWidth * 1.5f);
                if (loop != null)
                    renderers.Add(loop);
            }

            IncludeInLod(item, renderers);
            item.lookText = FirewoodPieces.LookText(item, count);
            item.description = "";
            item.big = false;
            ShipItem prefab = FirewoodPieces.PrefabOf(item);
            if (prefab != null)
                item.holdDistance = prefab.holdDistance + layerRing[0] + radius;
            ApplyMass(item, count);
            // The collider follows the net, with a flat foot so the bag stands upright on the
            // deck. It reaches up to the tie, which sits inside the lamp hook's own collider:
            // the bag needs to touch the hook to hang itself back up after a reload.
            float widestAt = rounds ? layerY[0] - apple.extents.y * 0.79f : layerY[0];
            Bounds core;
            Mesh hull = BagHull(item, profile, bottom, rim, widestAt, neck, out core);
            FitBox(item, core);
            if (hull != null)
                AddHull(item, hull);
            if (item.GetComponent<HangableItem>() == null)
                item.gameObject.AddComponent<HangableItem>();
            QuietHull(item);
        }

        // How wide a round of goat cheese is at a height above its middle, as shares of its
        // half height and its widest radius. It is widest low on its side, draws in a
        // little toward the top, and is bevelled at both faces.
        private static float RoundWidth(float height)
        {
            if (height <= -0.79f)
                return Mathf.Lerp(0.84f, 1f, Mathf.InverseLerp(-1f, -0.79f, height));
            if (height <= 0.77f)
                return Mathf.Lerp(1f, 0.9f, Mathf.InverseLerp(-0.79f, 0.77f, height));
            return Mathf.Lerp(0.9f, 0.725f, Mathf.InverseLerp(0.77f, 1f, height));
        }

        // How far from the middle a ring of this many apples sits so they just touch.
        private static float RingRadius(int size, float radius)
        {
            if (size <= 1)
                return 0f;
            if (size >= 7)
                return radius * 2f;
            return radius / Mathf.Sin(Mathf.PI / size);
        }

        // A strand of net lies on the bag's surface, so its tube is framed by the way
        // out from the middle rather than by one fixed axis, which would flatten it
        // where it runs along that axis.
        private static Mesh NetStrandMesh(List<Vector3> points, float radius)
        {
            const int sides = 5;
            int count = points.Count;
            var vertices = new Vector3[count * sides];
            var normals = new Vector3[count * sides];
            var triangles = new int[(count - 1) * sides * 6];
            for (int i = 0; i < count; i++)
            {
                Vector3 tangent = points[Mathf.Min(count - 1, i + 1)] - points[Mathf.Max(0, i - 1)];
                tangent = tangent.sqrMagnitude > 0.0000001f ? tangent.normalized : Vector3.up;
                Vector3 radial = new Vector3(points[i].x, 0f, points[i].z);
                radial = radial.sqrMagnitude > 0.0000001f ? radial.normalized : Vector3.right;
                Vector3 side = Vector3.Cross(tangent, radial);
                side = side.sqrMagnitude > 0.0000001f ? side.normalized : Vector3.forward;
                Vector3 across = Vector3.Cross(side, tangent).normalized;
                for (int k = 0; k < sides; k++)
                {
                    float angle = k * Mathf.PI * 2f / sides;
                    Vector3 normal = side * Mathf.Cos(angle) + across * Mathf.Sin(angle);
                    vertices[i * sides + k] = points[i] + normal * radius;
                    normals[i * sides + k] = normal;
                }
            }

            int t = 0;
            for (int i = 0; i < count - 1; i++)
            {
                for (int k = 0; k < sides; k++)
                {
                    int k1 = (k + 1) % sides;
                    int a = i * sides + k;
                    int b = i * sides + k1;
                    int c = (i + 1) * sides + k;
                    int d = (i + 1) * sides + k1;
                    triangles[t++] = a;
                    triangles[t++] = c;
                    triangles[t++] = b;
                    triangles[t++] = b;
                    triangles[t++] = c;
                    triangles[t++] = d;
                }
            }

            var mesh = new Mesh();
            mesh.name = StringName;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        // A bent piece of string through every point.
        private static Renderer CreateCordLine(ShipItem item, Material material, List<Vector3> points, float width = -1f)
        {
            if (points == null || points.Count < 2)
                return null;
            Mesh mesh = NetStrandMesh(points, width > 0f ? width : NetWidth);
            if (mesh == null)
                return null;
            var piece = new GameObject(StringName);
            piece.layer = item.gameObject.layer;
            piece.transform.SetParent(item.transform, false);
            MeshFilter filter = piece.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Outline rootOutline = item.GetComponent<Outline>();
            Outline outline = piece.AddComponent<Outline>();
            outline.enabled = false;
            if (rootOutline != null)
            {
                outline.color = rootOutline.color;
                outline.eraseRenderer = rootOutline.eraseRenderer;
                outline.originalLayer = rootOutline.originalLayer;
            }

            return renderer;
        }

        // Where a skewer hangs on a rack layer: the first 12 around the outer ring, the
        // last 4 around the inner one, each layer turned half a step from the one above.
        private static void RackSlot(int slot, int layer, float outer, out float angle, out float radius)
        {
            float turn = (layer % 2) * 0.5f;
            if (slot < RackOuter)
            {
                angle = (slot + turn) * Mathf.PI * 2f / RackOuter;
                radius = outer;
                return;
            }
            angle = (slot - RackOuter + 0.5f + turn) * Mathf.PI * 2f / RackInner;
            radius = outer * RackInnerShare;
        }

        // One straight piece of string. The frame axis is the one most across it.
        private static Renderer CreateCordPiece(ShipItem item, Material material, Vector3 from, Vector3 to, int frameAxis, float radius = HangCord)
        {
            Vector3 direction = to - from;
            if (direction.sqrMagnitude < 0.000001f)
                return null;
            int best = frameAxis;
            float least = float.MaxValue;
            for (int axis = 0; axis < 3; axis++)
            {
                float lean = Mathf.Abs(direction.normalized[axis]);
                if (lean < least)
                {
                    least = lean;
                    best = axis;
                }
            }

            Mesh mesh = CordMesh(new List<Vector3> { from, to }, best, radius, false, StringName);
            if (mesh == null)
                return null;
            var piece = new GameObject(StringName);
            piece.layer = item.gameObject.layer;
            piece.transform.SetParent(item.transform, false);
            MeshFilter filter = piece.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Outline rootOutline = item.GetComponent<Outline>();
            Outline outline = piece.AddComponent<Outline>();
            outline.enabled = false;
            if (rootOutline != null)
            {
                outline.color = rootOutline.color;
                outline.eraseRenderer = rootOutline.eraseRenderer;
                outline.originalLayer = rootOutline.originalLayer;
            }

            return renderer;
        }

        // Off the hook and back to one plain sausage: no strings, no hanging.
        internal static void Unhang(ShipItem item)
        {
            if (item == null)
                return;
            HangableItem hang = item.GetComponent<HangableItem>();
            if (hang != null)
            {
                if (hang.IsHanging())
                    hang.DisconnectJoint();
                Object.Destroy(hang);
            }

            SausageStacks.Unhang(item);
            RestoreSingle(item);
        }

        private static void AdoptTwoHandedHold(ShipItem item)
        {
            GoPointer pointer = item.held;
            if (pointer == null)
                return;

            Vector3 localPos = pointer.transform.InverseTransformPoint(item.transform.position);
            // A small log is already pitched by heldRotationOffset. A two-handed hold
            // adds that pitch again, which tips the stack out away from the player.
            Quaternion pitch = Quaternion.Euler(item.heldRotationOffset, 0f, 0f);
            Quaternion localRot = Quaternion.Inverse(pitch) * (Quaternion.Inverse(pointer.transform.rotation) * item.transform.rotation);
            SetPointerField(pointer, "bigItemLocalPos", localPos);
            SetPointerField(pointer, "decolLocalPos", localPos);
            SetPointerField(pointer, "bigItemLocalRot", localRot);
            SetPointerField(pointer, "bigItemLocked", false);
            SetPointerField(pointer, "lastPointerRot", pointer.transform.rotation);
        }

        private static void SetPointerField(GoPointer pointer, string name, object value)
        {
            var field = AccessTools.Field(typeof(GoPointer), name);
            if (field != null)
                field.SetValue(pointer, value);
        }

        private static void ClearSticks(ShipItem item)
        {
            for (int i = item.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = item.transform.GetChild(i);
                if (child.name != StickName && child.name != StringName && child.name != KnotName)
                    continue;
                if (child.name == StringName || child.name == KnotName)
                    ReleaseStringMesh(child);
                Object.Destroy(child.gameObject);
            }
        }

        private static MeshRenderer CreateStick(
            ShipItem item,
            Mesh mesh,
            Material[] materials,
            Vector3 localPosition,
            Quaternion localRotation,
            MeshRenderer source,
            Vector3 scale = default(Vector3))
        {
            var stick = new GameObject(StickName);
            stick.layer = item.gameObject.layer;
            stick.transform.SetParent(item.transform, false);
            stick.transform.localPosition = localPosition;
            stick.transform.localRotation = localRotation;
            stick.transform.localScale = scale == Vector3.zero ? Vector3.one : scale;

            MeshFilter filter = stick.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = stick.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = source.shadowCastingMode;
            renderer.receiveShadows = source.receiveShadows;

            Outline rootOutline = item.GetComponent<Outline>();
            Outline outline = stick.AddComponent<Outline>();
            outline.enabled = false;
            if (rootOutline != null)
            {
                outline.color = rootOutline.color;
                outline.eraseRenderer = rootOutline.eraseRenderer;
                outline.originalLayer = rootOutline.originalLayer;
            }

            return renderer;
        }

        // A new tie color only swaps the material on the tie and the knot.
        internal static void Recolor(ShipItem item)
        {
            BundleKind kind = FirewoodPieces.KindOf(item);
            if (item == null || (FirewoodPieces.CountOf(item) <= 1 && (kind == null || !kind.Hangs)))
                return;
            Material material = StringMaterial(FirewoodPieces.KindOf(item), FirewoodPieces.ColorOf(item));
            if (material == null)
                return;

            Transform transform = item.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name != StringName && child.name != KnotName)
                    continue;
                MeshRenderer renderer = child.GetComponent<MeshRenderer>();
                if (renderer != null)
                    renderer.sharedMaterial = material;
            }
        }

        internal static void SyncOutline(ShipItem item)
        {
            if (item == null)
                return;
            BundleKind kind = FirewoodPieces.KindOf(item);
            if (FirewoodPieces.CountOf(item) <= 1 && (kind == null || !kind.Hangs))
                return;

            Outline root = item.GetComponent<Outline>();
            if (root == null)
                return;

            Transform transform = item.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name != StickName && child.name != StringName && child.name != KnotName)
                    continue;

                Outline outline = child.GetComponent<Outline>();
                if (outline == null)
                    continue;

                outline.color = root.color;
                outline.eraseRenderer = root.eraseRenderer;
                if (outline.enabled != root.enabled)
                    outline.enabled = root.enabled;
            }

            // The item's own mesh is hidden behind the copies. Its outline would still draw
            // a sausage at the item's origin, which on a hanging bundle is the hang point.
            // The game sets the root outline again on every color update, so the copies
            // above have already taken its state.
            Renderer own = item.GetComponent<Renderer>();
            if (own != null && !own.enabled && root.enabled)
                root.enabled = false;
        }

        private static Renderer CreateString(
            ShipItem item,
            List<Vector3> centers,
            Mesh logMesh,
            int longAxis,
            float cross,
            MeshRenderer source,
            int upAxis = -1)
        {
            if (item == null || centers == null || centers.Count < 2 || logMesh == null)
                return null;

            BundleKind kind = FirewoodPieces.KindOf(item);
            Material material = StringMaterial(kind, FirewoodPieces.ColorOf(item));
            if (material == null)
                return null;

            Bounds bundle = BundleBounds(centers, logMesh);
            bool ribbon = kind != null && kind.Ribbon;
            float ribbonWidth = Mathf.Clamp(cross * 0.55f, 0.008f, 0.025f);
            // A flat ribbon hugs the pieces. The loop runs through its middle.
            float cord = ribbon ? RibbonThickness * 0.5f + 0.0005f : Mathf.Clamp(cross * 0.055f, 0.0032f, 0.0075f);
            // The loop's second axis is the one rows stack along; a sausage stack names its own.
            int axisB = upAxis >= 0 ? upAxis : (longAxis + 2) % 3;
            int axisA = 3 - longAxis - axisB;
            float halfA = bundle.size[axisA] * 0.5f + cord;
            float halfB = bundle.size[axisB] * 0.5f + cord;
            float corner = Mathf.Min(cross * 0.5f + cord, halfA, halfB);
            if (corner < 0.002f)
                corner = Mathf.Min(halfA, halfB);

            List<Vector3> loop = PartialTopLoop(centers, logMesh, bundle.center, axisA, axisB, cord, cross);
            if (loop == null)
                loop = StringLoop(bundle.center, axisA, axisB, halfA, halfB, corner, cross);
            // A sausage's box holds its bend, so its top sits above the sausage. Pull the
            // top half of the loop down onto it, stretching the sides rather than stepping.
            if (upAxis >= 0)
            {
                float middle = bundle.center[axisB];
                float reach = halfB;
                float drop = StackTopDrop(logMesh, axisB);
                for (int i = 0; i < loop.Count; i++)
                {
                    Vector3 point = loop[i];
                    if (point[axisB] > middle && reach > drop)
                        point[axisB] = middle + (point[axisB] - middle) * (reach - drop) / reach;
                    loop[i] = point;
                }
            }
            Mesh cordMesh = ribbon
                ? BandMesh(loop, longAxis, RibbonThickness * 0.5f, ribbonWidth * 0.5f)
                : CordMesh(loop, longAxis, cord);
            if (cordMesh == null)
                return null;

            var tie = new GameObject(StringName);
            tie.layer = item.gameObject.layer;
            tie.transform.SetParent(item.transform, false);
            tie.transform.localPosition = Vector3.zero;
            tie.transform.localRotation = Quaternion.identity;
            tie.transform.localScale = Vector3.one;

            MeshFilter filter = tie.AddComponent<MeshFilter>();
            filter.sharedMesh = cordMesh;
            MeshRenderer renderer = tie.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = source != null ? source.shadowCastingMode : UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Outline rootOutline = item.GetComponent<Outline>();
            Outline outline = tie.AddComponent<Outline>();
            outline.enabled = false;
            if (rootOutline != null)
            {
                outline.color = rootOutline.color;
                outline.eraseRenderer = rootOutline.eraseRenderer;
                outline.originalLayer = rootOutline.originalLayer;
            }

            return renderer;
        }

        // Rows grow along the axis after the log's length. Two cord ends stick up
        // from that face in a V so the upright side of the bundle is easy to see.
        private static Renderer CreateKnot(
            ShipItem item,
            List<Vector3> centers,
            Mesh logMesh,
            int longAxis,
            float cross,
            int stackUp = -1)
        {
            if (item == null || centers == null || centers.Count < 2 || logMesh == null)
                return null;

            BundleKind kind = FirewoodPieces.KindOf(item);
            Material material = StringMaterial(kind, FirewoodPieces.ColorOf(item));
            if (material == null)
                return null;
            bool ribbon = kind != null && kind.Ribbon;

            int upAxis = stackUp >= 0 ? stackUp : (longAxis + 2) % 3;
            int axisA = 3 - longAxis - upAxis;
            float topRow = float.MinValue;
            for (int i = 0; i < centers.Count; i++)
                topRow = Mathf.Max(topRow, centers[i][upAxis]);

            float minA = float.MaxValue;
            float maxA = float.MinValue;
            float minLong = float.MaxValue;
            float maxLong = float.MinValue;
            for (int i = 0; i < centers.Count; i++)
            {
                if (centers[i][upAxis] < topRow - 0.0001f)
                    continue;
                minA = Mathf.Min(minA, centers[i][axisA]);
                maxA = Mathf.Max(maxA, centers[i][axisA]);
                minLong = Mathf.Min(minLong, centers[i][longAxis]);
                maxLong = Mathf.Max(maxLong, centers[i][longAxis]);
            }

            float cord = Mathf.Clamp(cross * 0.07f, 0.004f, 0.009f);
            float rise = Mathf.Max(cross * 0.9f, 0.05f);
            float spread = rise * 0.42f;
            Vector3 up = Vector3.zero;
            up[upAxis] = 1f;
            Vector3 along = Vector3.zero;
            along[longAxis] = 1f;
            Vector3 origin = Vector3.zero;
            origin[axisA] = (minA + maxA) * 0.5f;
            origin[longAxis] = (minLong + maxLong) * 0.5f;
            origin[upAxis] = topRow + logMesh.bounds.max[upAxis] - (stackUp >= 0 ? StackTopDrop(logMesh, upAxis) : 0f);
            var left = new List<Vector3> { origin, origin - along * spread + up * rise };
            var right = new List<Vector3> { origin, origin + along * spread + up * rise };
            Mesh knotMesh;
            if (ribbon)
            {
                Vector3 side = Vector3.zero;
                side[axisA] = 1f;
                knotMesh = BowMesh(origin, up, side, longAxis, axisA, cross);
            }
            else
            {
                knotMesh = JoinCords(
                    CordMesh(left, axisA, cord, false, KnotName),
                    CordMesh(right, axisA, cord, false, KnotName));
            }
            if (knotMesh == null)
                return null;

            var knot = new GameObject(KnotName);
            knot.layer = item.gameObject.layer;
            knot.transform.SetParent(item.transform, false);
            knot.transform.localPosition = Vector3.zero;
            knot.transform.localRotation = Quaternion.identity;
            knot.transform.localScale = Vector3.one;

            MeshFilter filter = knot.AddComponent<MeshFilter>();
            filter.sharedMesh = knotMesh;
            MeshRenderer renderer = knot.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Outline rootOutline = item.GetComponent<Outline>();
            Outline outline = knot.AddComponent<Outline>();
            outline.enabled = false;
            if (rootOutline != null)
            {
                outline.color = rootOutline.color;
                outline.eraseRenderer = rootOutline.eraseRenderer;
                outline.originalLayer = rootOutline.originalLayer;
            }

            return renderer;
        }

        // A shoelace bow on top of the ribbon: two loops that stand up and lean out
        // along the ribbon, two tails that lie low under them, and a wrap in the middle.
        // The ribbon's width runs along the pieces, like the band it is tied from.
        private static Mesh BowMesh(Vector3 origin, Vector3 up, Vector3 side, int longAxis, int axisA, float cross)
        {
            float half = RibbonThickness * 0.5f;
            float width = Mathf.Clamp(cross * 0.45f, 0.007f, 0.02f);
            float loopLength = Mathf.Clamp(cross * 1.2f, 0.025f, 0.06f);
            float tailLength = loopLength * 1.15f;
            Vector3 lift = up * half;

            Mesh bow = null;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 outward = side * s;
                bow = JoinCords(bow, BandMesh(BowLoop(origin + lift, outward, up, loopLength), longAxis, half, width * 0.5f, true, KnotName));

                // A tail leaves the knot, dips onto the top of the bundle and runs out
                // under its loop, with a small kick up at the cut end.
                var tail = new List<Vector3>(6);
                for (int i = 0; i <= 5; i++)
                {
                    float t = i / 5f;
                    float height = Mathf.Lerp(width * 0.18f, 0f, Mathf.Min(1f, t * 2.5f)) + Mathf.Max(0f, t - 0.75f) * width * 0.5f;
                    tail.Add(origin + lift + outward * (tailLength * t) + up * height);
                }
                bow = JoinCords(bow, BandMesh(tail, longAxis, half, width * 0.42f, false, KnotName));
            }

            // The wrap that holds the loops: a short band around the crossing.
            float wrapRadius = Mathf.Max(width * 0.22f, half * 3f);
            var wrap = new List<Vector3>(10);
            for (int i = 0; i < 10; i++)
            {
                float angle = i * Mathf.PI * 2f / 10f;
                wrap.Add(origin + lift + up * (wrapRadius * 0.9f) + up * (Mathf.Sin(angle) * wrapRadius) + side * (Mathf.Cos(angle) * wrapRadius * 0.7f));
            }
            bow = JoinCords(bow, BandMesh(wrap, longAxis, half, width * 0.32f, true, KnotName));
            return bow;
        }

        // One petal of the bow, starting and ending at the knot. It is tipped up
        // so its lower edge stays clear of the pieces under it.
        private static List<Vector3> BowLoop(Vector3 knot, Vector3 outward, Vector3 up, float length)
        {
            const int steps = 16;
            float bulge = length * 0.62f;
            float tilt = 38f * Mathf.Deg2Rad;
            Vector3 axis = outward * Mathf.Cos(tilt) + up * Mathf.Sin(tilt);
            Vector3 across = up * Mathf.Cos(tilt) - outward * Mathf.Sin(tilt);
            var loop = new List<Vector3>(steps);
            for (int i = 0; i < steps; i++)
            {
                float s = i / (float)steps;
                float u = length * Mathf.Sin(Mathf.PI * s);
                float v = bulge * 0.5f * Mathf.Sin(Mathf.PI * 2f * s);
                loop.Add(knot + axis * u + across * v);
            }

            return loop;
        }

        private static List<Vector3> StringLoop(
            Vector3 center,
            int axisA,
            int axisB,
            float halfA,
            float halfB,
            float corner,
            float cross)
        {
            float midA = center[axisA];
            float midB = center[axisB];
            float spacing = Mathf.Max(cross * 0.22f, 0.015f);
            var path = new List<Vector3>(48);

            AddStraight(path, center, axisA, axisB, midA - (halfA - corner), midB - halfB, midA + (halfA - corner), midB - halfB, spacing);
            AddArc(path, center, axisA, axisB, midA + (halfA - corner), midB - (halfB - corner), corner, -90f, 0f);
            AddStraight(path, center, axisA, axisB, midA + halfA, midB - (halfB - corner), midA + halfA, midB + (halfB - corner), spacing);
            AddArc(path, center, axisA, axisB, midA + (halfA - corner), midB + (halfB - corner), corner, 0f, 90f);
            AddStraight(path, center, axisA, axisB, midA + (halfA - corner), midB + halfB, midA - (halfA - corner), midB + halfB, spacing);
            AddArc(path, center, axisA, axisB, midA - (halfA - corner), midB + (halfB - corner), corner, 90f, 180f);
            AddStraight(path, center, axisA, axisB, midA - halfA, midB + (halfB - corner), midA - halfA, midB - (halfB - corner), spacing);
            AddArc(path, center, axisA, axisB, midA - (halfA - corner), midB - (halfB - corner), corner, 180f, 270f);
            return path;
        }

        // A short top row sits in from the rows under it. The cord leaves the
        // shoulders of that wider stack and runs up to the outer top corners
        // of the end logs, then across them.
        private static List<Vector3> PartialTopLoop(
            List<Vector3> centers,
            Mesh logMesh,
            Vector3 origin,
            int axisA,
            int axisB,
            float cord,
            float cross)
        {
            if (centers == null || centers.Count < 2 || logMesh == null)
                return null;

            float topRowB = float.MinValue;
            for (int i = 0; i < centers.Count; i++)
                topRowB = Mathf.Max(topRowB, centers[i][axisB]);

            int left = -1;
            int right = -1;
            bool anyBody = false;
            float bodyMinA = float.MaxValue;
            float bodyMaxA = float.MinValue;
            float bodyMinB = float.MaxValue;
            float bodyMaxB = float.MinValue;
            Vector3 stickMin = logMesh.bounds.min;
            Vector3 stickMax = logMesh.bounds.max;
            for (int i = 0; i < centers.Count; i++)
            {
                Vector3 log = centers[i];
                bool onTop = log[axisB] >= topRowB - 0.0001f;
                if (onTop)
                {
                    if (left < 0 || log[axisA] < centers[left][axisA])
                        left = i;
                    if (right < 0 || log[axisA] > centers[right][axisA])
                        right = i;
                    continue;
                }

                anyBody = true;
                bodyMinA = Mathf.Min(bodyMinA, log[axisA] + stickMin[axisA]);
                bodyMaxA = Mathf.Max(bodyMaxA, log[axisA] + stickMax[axisA]);
                bodyMinB = Mathf.Min(bodyMinB, log[axisB] + stickMin[axisB]);
                bodyMaxB = Mathf.Max(bodyMaxB, log[axisB] + stickMax[axisB]);
            }

            if (!anyBody || left < 0 || right < 0)
                return null;

            float topLeftA = centers[left][axisA] + stickMin[axisA];
            float topRightA = centers[right][axisA] + stickMax[axisA];
            float topB = Mathf.Max(
                centers[left][axisB] + stickMax[axisB],
                centers[right][axisB] + stickMax[axisB]);
            if (topLeftA <= bodyMinA + cross * 0.2f && topRightA >= bodyMaxA - cross * 0.2f)
                return null;

            var wood = new List<Vector2>(6)
            {
                new Vector2(bodyMinA, bodyMinB),
                new Vector2(bodyMaxA, bodyMinB),
                new Vector2(bodyMaxA, bodyMaxB),
                new Vector2(topRightA, topB),
                new Vector2(topLeftA, topB),
                new Vector2(bodyMinA, bodyMaxB)
            };
            List<Vector2> outline = OffsetOutward(wood, cord);
            float shoulder = Mathf.Clamp(cross * 0.1f, 0.002f, 0.012f);
            float bottom = cross * 0.5f;
            var radii = new[] { bottom, bottom, shoulder, shoulder, shoulder, shoulder };
            return TraceLoop(outline, radii, Mathf.Max(cross * 0.22f, 0.015f), origin, axisA, axisB);
        }

        private static List<Vector2> OffsetOutward(List<Vector2> poly, float distance)
        {
            int count = poly.Count;
            var offset = new List<Vector2>(count);
            for (int i = 0; i < count; i++)
            {
                Vector2 previous = poly[(i + count - 1) % count];
                Vector2 current = poly[i];
                Vector2 next = poly[(i + 1) % count];
                Vector2 into = current - previous;
                Vector2 away = next - current;
                if (into.sqrMagnitude < 0.0000001f || away.sqrMagnitude < 0.0000001f)
                {
                    offset.Add(current);
                    continue;
                }

                into.Normalize();
                away.Normalize();
                Vector2 outInto = new Vector2(into.y, -into.x);
                Vector2 outAway = new Vector2(away.y, -away.x);
                Vector2 hit;
                if (!LineIntersect(previous + outInto * distance, into, current + outAway * distance, away, out hit))
                    hit = current + (outInto + outAway).normalized * distance;
                offset.Add(hit);
            }

            return offset;
        }

        private static List<Vector3> TraceLoop(
            List<Vector2> poly,
            float[] radii,
            float spacing,
            Vector3 origin,
            int axisA,
            int axisB)
        {
            int count = poly.Count;
            var starts = new Vector2[count];
            var ends = new Vector2[count];
            var centers = new Vector2[count];
            var sweep = new float[count];
            var arc = new bool[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 previous = poly[(i + count - 1) % count];
                Vector2 current = poly[i];
                Vector2 next = poly[(i + 1) % count];
                Vector2 back = previous - current;
                Vector2 forward = next - current;
                float backLength = back.magnitude;
                float forwardLength = forward.magnitude;
                starts[i] = current;
                ends[i] = current;
                if (backLength < 0.0001f || forwardLength < 0.0001f)
                    continue;

                back /= backLength;
                forward /= forwardLength;
                float dot = Mathf.Clamp(Vector2.Dot(back, forward), -1f, 1f);
                float half = Mathf.Acos(dot) * 0.5f;
                if (half < 0.08f || half > 1.4f)
                    continue;

                float radius = radii != null && i < radii.Length ? radii[i] : 0.004f;
                float trim = radius / Mathf.Tan(half);
                trim = Mathf.Min(trim, backLength * 0.45f, forwardLength * 0.45f);
                if (trim < 0.0008f)
                    continue;

                radius = trim * Mathf.Tan(half);
                starts[i] = current + back * trim;
                ends[i] = current + forward * trim;
                centers[i] = current + (back + forward).normalized * (radius / Mathf.Sin(half));
                Vector2 from = starts[i] - centers[i];
                Vector2 to = ends[i] - centers[i];
                sweep[i] = Mathf.DeltaAngle(Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                arc[i] = from.sqrMagnitude > 0.0000001f;
            }

            var path = new List<Vector3>(count * 6);
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                if (arc[i])
                {
                    Vector2 from = starts[i] - centers[i];
                    float fromAngle = Mathf.Atan2(from.y, from.x);
                    float radius = from.magnitude;
                    int steps = Mathf.Max(2, Mathf.CeilToInt(radius * Mathf.Abs(sweep[i]) / spacing));
                    for (int step = 0; step < steps; step++)
                    {
                        float angle = fromAngle + sweep[i] * (step / (float)steps);
                        Vector2 point = centers[i] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                        path.Add(OnPlane(origin, axisA, axisB, point.x, point.y));
                    }
                }

                AddStraight(path, origin, axisA, axisB, ends[i].x, ends[i].y, starts[next].x, starts[next].y, spacing);
            }

            return path.Count >= 3 ? path : null;
        }

        private static bool LineIntersect(Vector2 originA, Vector2 directionA, Vector2 originB, Vector2 directionB, out Vector2 hit)
        {
            float cross = directionA.x * directionB.y - directionA.y * directionB.x;
            if (Mathf.Abs(cross) < 0.000001f)
            {
                hit = originA;
                return false;
            }

            Vector2 delta = originB - originA;
            float t = (delta.x * directionB.y - delta.y * directionB.x) / cross;
            hit = originA + directionA * t;
            return true;
        }

        private static void AddStraight(
            List<Vector3> path,
            Vector3 origin,
            int axisA,
            int axisB,
            float fromA,
            float fromB,
            float toA,
            float toB,
            float spacing)
        {
            float length = Mathf.Sqrt((toA - fromA) * (toA - fromA) + (toB - fromB) * (toB - fromB));
            if (length < 0.0005f)
                return;

            int steps = Mathf.Max(1, Mathf.CeilToInt(length / spacing));
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)steps;
                path.Add(OnPlane(origin, axisA, axisB, Mathf.Lerp(fromA, toA, t), Mathf.Lerp(fromB, toB, t)));
            }
        }

        private static void AddArc(
            List<Vector3> path,
            Vector3 origin,
            int axisA,
            int axisB,
            float centerA,
            float centerB,
            float radius,
            float fromDeg,
            float toDeg)
        {
            int steps = 4;
            for (int i = 0; i < steps; i++)
            {
                float deg = Mathf.Lerp(fromDeg, toDeg, i / (float)steps) * Mathf.Deg2Rad;
                path.Add(OnPlane(
                    origin,
                    axisA,
                    axisB,
                    centerA + Mathf.Cos(deg) * radius,
                    centerB + Mathf.Sin(deg) * radius));
            }
        }

        private static Vector3 OnPlane(Vector3 origin, int axisA, int axisB, float a, float b)
        {
            Vector3 point = origin;
            point[axisA] = a;
            point[axisB] = b;
            return point;
        }

        private static Mesh CordMesh(List<Vector3> loop, int frameAxis, float radius, bool closed = true, string meshName = null)
        {
            int count = loop.Count;
            if (count < (closed ? 3 : 2))
                return null;

            const int sides = 6;
            int segments = closed ? count : count - 1;
            Vector3 frame = Vector3.zero;
            frame[frameAxis] = 1f;
            var vertices = new Vector3[count * sides];
            var normals = new Vector3[count * sides];
            var triangles = new int[segments * sides * 6];
            Vector3 outward = Vector3.zero;

            for (int i = 0; i < count; i++)
            {
                int previousIndex = closed ? (i + count - 1) % count : Mathf.Max(0, i - 1);
                int nextIndex = closed ? (i + 1) % count : Mathf.Min(count - 1, i + 1);
                Vector3 tangent = loop[nextIndex] - loop[previousIndex];
                if (tangent.sqrMagnitude > 0.0000001f)
                    tangent.Normalize();
                else
                    tangent = frame;

                Vector3 side = Vector3.Cross(tangent, frame);
                if (side.sqrMagnitude > 0.0000001f)
                    outward = side.normalized;

                for (int s = 0; s < sides; s++)
                {
                    float angle = s * Mathf.PI * 2f / sides;
                    Vector3 normal = outward * Mathf.Cos(angle) + frame * Mathf.Sin(angle);
                    int index = i * sides + s;
                    normals[index] = normal;
                    vertices[index] = loop[i] + normal * radius;
                }
            }

            int t = 0;
            for (int i = 0; i < segments; i++)
            {
                int next = closed ? (i + 1) % count : i + 1;
                for (int s = 0; s < sides; s++)
                {
                    int s1 = (s + 1) % sides;
                    int i0 = i * sides + s;
                    int i1 = i * sides + s1;
                    int i2 = next * sides + s;
                    int i3 = next * sides + s1;
                    triangles[t++] = i0;
                    triangles[t++] = i2;
                    triangles[t++] = i1;
                    triangles[t++] = i1;
                    triangles[t++] = i2;
                    triangles[t++] = i3;
                }
            }

            var mesh = new Mesh();
            mesh.name = string.IsNullOrEmpty(meshName) ? StringName : meshName;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        // A flat band along the loop: thin across the loop, wide along frameAxis.
        // Each face gets its own vertices so the ribbon shades flat.
        private static Mesh BandMesh(List<Vector3> loop, int frameAxis, float halfThick, float halfWidth, bool closed = true, string meshName = null)
        {
            int count = loop.Count;
            if (count < (closed ? 3 : 2))
                return null;

            const int faces = 4;
            int segments = closed ? count : count - 1;
            Vector3 frame = Vector3.zero;
            frame[frameAxis] = 1f;
            var vertices = new Vector3[count * faces * 2];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[segments * faces * 6];
            Vector3 outward = Vector3.zero;

            for (int i = 0; i < count; i++)
            {
                int previousIndex = closed ? (i + count - 1) % count : Mathf.Max(0, i - 1);
                int nextIndex = closed ? (i + 1) % count : Mathf.Min(count - 1, i + 1);
                Vector3 tangent = loop[nextIndex] - loop[previousIndex];
                if (tangent.sqrMagnitude > 0.0000001f)
                    tangent.Normalize();
                else
                    tangent = frame;

                Vector3 side = Vector3.Cross(tangent, frame);
                if (side.sqrMagnitude > 0.0000001f)
                    outward = side.normalized;

                Vector3 o = outward * halfThick;
                Vector3 w = frame * halfWidth;
                // Corners go around the cross-section; each face uses two of them.
                Vector3[] corners = { o - w, o + w, -o + w, -o - w };
                Vector3[] faceNormals = { outward, frame, -outward, -frame };
                for (int f = 0; f < faces; f++)
                {
                    int index = (i * faces + f) * 2;
                    vertices[index] = loop[i] + corners[f];
                    vertices[index + 1] = loop[i] + corners[(f + 1) % faces];
                    normals[index] = faceNormals[f];
                    normals[index + 1] = faceNormals[f];
                }
            }

            int t = 0;
            for (int i = 0; i < segments; i++)
            {
                int next = closed ? (i + 1) % count : i + 1;
                for (int f = 0; f < faces; f++)
                {
                    int i0 = (i * faces + f) * 2;
                    int i1 = i0 + 1;
                    int i2 = (next * faces + f) * 2;
                    int i3 = i2 + 1;
                    triangles[t++] = i0;
                    triangles[t++] = i2;
                    triangles[t++] = i1;
                    triangles[t++] = i1;
                    triangles[t++] = i2;
                    triangles[t++] = i3;
                }
            }

            var mesh = new Mesh();
            mesh.name = string.IsNullOrEmpty(meshName) ? StringName : meshName;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh JoinCords(Mesh first, Mesh second)
        {
            if (first == null)
                return second;
            if (second == null)
                return first;

            Vector3[] firstVertices = first.vertices;
            Vector3[] secondVertices = second.vertices;
            Vector3[] firstNormals = first.normals;
            Vector3[] secondNormals = second.normals;
            int[] firstTriangles = first.triangles;
            int[] secondTriangles = second.triangles;
            var vertices = new Vector3[firstVertices.Length + secondVertices.Length];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[firstTriangles.Length + secondTriangles.Length];
            firstVertices.CopyTo(vertices, 0);
            secondVertices.CopyTo(vertices, firstVertices.Length);
            firstNormals.CopyTo(normals, 0);
            secondNormals.CopyTo(normals, firstNormals.Length);
            firstTriangles.CopyTo(triangles, 0);
            for (int i = 0; i < secondTriangles.Length; i++)
                triangles[firstTriangles.Length + i] = secondTriangles[i] + firstVertices.Length;

            Object.Destroy(first);
            Object.Destroy(second);
            var mesh = new Mesh();
            mesh.name = KnotName;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material StringMaterial(BundleKind kind, int color)
        {
            // A kind without its own colors (sausages) is tied with brown firewood cord.
            if (kind == null || kind.ColorCount == 0)
            {
                kind = BundleKind.Firewood;
                color = 0;
            }
            if (color < 0 || color >= kind.ColorCount)
                color = 0;
            return TieMaterial(kind.BundleName + "/" + color, kind.Colors[color], kind.Ribbon);
        }

        // A material for one of the mod's own parts, such as the string on hanging
        // sausages or the iron of a date rack. These are not tie colors, so they stay as
        // they are when a player changes the tie color list.
        private static Material PlainMaterial(string part, Color color)
        {
            return TieMaterial("part/" + part, color, false);
        }

        private static Material TieMaterial(string key, Color color, bool ribbon)
        {
            Material cached;
            if (StringMaterials.TryGetValue(key, out cached) && cached != null)
                return cached;

            Shader shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Legacy Shaders/Diffuse");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Plugin.Log.LogWarning("Could not find a shader for the tie " + key + ".");
                return null;
            }

            var material = new Material(shader);
            material.name = StringName;
            material.color = color;
            if (shader.name == "Standard")
            {
                material.SetFloat("_Metallic", 0f);
                // A ribbon is satin, a little shinier than cord.
                material.SetFloat("_Glossiness", ribbon ? 0.35f : 0.12f);
            }

            StringMaterials[key] = material;
            return material;
        }

        // The tie colors changed in the config. Every tie already drawn shares its
        // material with the others of its kind and color, so recoloring those shows at
        // once on the bundles in the world.
        internal static void RefreshTieColors()
        {
            BundleKind[] kinds = BundleKind.Dyed;
            for (int k = 0; k < kinds.Length; k++)
            {
                BundleKind kind = kinds[k];
                for (int color = 0; color < kind.ColorCount; color++)
                {
                    Material cached;
                    if (StringMaterials.TryGetValue(kind.BundleName + "/" + color, out cached) && cached != null)
                        cached.color = kind.Colors[color];
                }
            }
        }

        private static void ReleaseStringMesh(ShipItem item)
        {
            if (item == null)
                return;

            Transform transform = item.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name == StringName || child.name == KnotName)
                    ReleaseStringMesh(child);
            }
        }

        private static void ReleaseStringMesh(Transform child)
        {
            if (child == null)
                return;

            MeshFilter filter = child.GetComponent<MeshFilter>();
            if (filter != null
                && filter.sharedMesh != null
                && (filter.sharedMesh.name == StringName || filter.sharedMesh.name == KnotName))
                Object.Destroy(filter.sharedMesh);
        }

        private static void IncludeInLod(ShipItem item, List<Renderer> renderers)
        {
            LODGroup group = item.GetComponent<LODGroup>();
            if (group == null)
                return;

            LOD[] lods = group.GetLODs();
            if (lods == null || lods.Length == 0)
                return;

            lods[0].renderers = renderers.ToArray();
            try
            {
                group.SetLODs(lods);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning("Could not update bundle LOD: " + ex);
            }
        }

        private static void ApplyMass(ShipItem item, int count)
        {
            ShipItem prefab = FirewoodPieces.PrefabOf(item);
            float baseMass = prefab != null ? prefab.mass : item.mass;
            if (baseMass <= 0f)
                baseMass = 1f;
            item.mass = baseMass * count;

            ItemRigidbody body = item.itemRigidbodyC;
            if (body == null)
                return;

            // Load runs before ItemRigidbody.Start, so the Rigidbody is not created yet.
            // UpdateMass would throw and abort the boat's item spawn, which then retries forever.
            var joint = AccessTools.Field(typeof(ItemRigidbody), "rigidbody");
            if (joint == null || joint.GetValue(body) == null)
                return;

            body.UpdateMass();
        }

        private static void ApplyHoldDistance(ShipItem item, List<Vector3> centers, int longAxis, float cross)
        {
            ShipItem prefab = FirewoodPieces.PrefabOf(item);
            float baseDistance = prefab != null ? prefab.holdDistance : item.holdDistance;
            float extra = Mathf.Max(0f, FirewoodPieces.CrossDiameter(centers, longAxis, cross) * 0.5f - cross * 0.5f);
            item.holdDistance = baseDistance + extra;
        }

        internal static void QuietHull(ShipItem item)
        {
            if (item == null)
                return;

            ItemRigidbody body = item.itemRigidbodyC;
            Collider hull = HullOf(item);
            if (body == null || hull == null)
                return;

            IgnoreHull(body, hull, true);

            int id = item.GetInstanceID();
            int stamp = 1;
            int current;
            if (HullStamps.TryGetValue(id, out current))
                stamp = current + 1;
            HullStamps[id] = stamp;
            if (Plugin.Instance != null)
                Plugin.Instance.StartCoroutine(ReleaseHull(item, body, hull, id, stamp));
        }

        private static IEnumerator ReleaseHull(ShipItem item, ItemRigidbody body, Collider hull, int id, int stamp)
        {
            while (true)
            {
                yield return new WaitForFixedUpdate();
                int current;
                if (item == null || body == null || hull == null)
                    yield break;
                if (!HullStamps.TryGetValue(id, out current) || current != stamp)
                    yield break;
                if (OverlapsHull(body, hull))
                    continue;

                IgnoreHull(body, hull, false);
                if (HullStamps.TryGetValue(id, out current) && current == stamp)
                    HullStamps.Remove(id);
                yield break;
            }
        }

        private static Collider HullOf(ShipItem item)
        {
            if (item == null || item.currentActualBoat == null)
                return null;

            Transform boat = item.currentActualBoat;
            BoatDamage damage = boat.GetComponent<BoatDamage>();
            if (damage == null && boat.parent != null)
                damage = boat.parent.GetComponent<BoatDamage>();
            if (damage == null)
                return null;
            return damage.GetComponent<CapsuleCollider>();
        }

        private static void IgnoreHull(ItemRigidbody body, Collider hull, bool ignore)
        {
            Collider[] cols = body.GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++)
            {
                Collider col = cols[i];
                if (col != null && col != hull && col.enabled)
                    Physics.IgnoreCollision(col, hull, ignore);
            }
        }

        private static bool OverlapsHull(ItemRigidbody body, Collider hull)
        {
            Collider[] cols = body.GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++)
            {
                Collider col = cols[i];
                if (col == null || !col.enabled || col.isTrigger || col == hull)
                    continue;

                Vector3 direction;
                float distance;
                if (Physics.ComputePenetration(
                    col,
                    col.transform.position,
                    col.transform.rotation,
                    hull,
                    hull.transform.position,
                    hull.transform.rotation,
                    out direction,
                    out distance) && distance > 0.0001f)
                    return true;
            }

            return false;
        }

        private static void FitColliders(ShipItem item, List<Vector3> centers, Mesh mesh, List<Quaternion> turns, int upAxis)
        {
            ClearHull(item);
            // A tree or pile is round and narrows toward the top, so a box around it is
            // mostly empty air at its corners. It gets a convex hull that wraps the pieces,
            // and its own box shrinks to a core inside the base.
            if (turns != null && mesh != null && centers.Count > 1)
            {
                Bounds core;
                Mesh hull = HullMesh(item, centers, mesh, turns, upAxis, out core);
                FitBox(item, core);
                if (hull != null)
                    AddHull(item, hull);
                return;
            }

            Bounds bundle = BundleBounds(centers, mesh, turns);
            bundle.Expand(0.04f);
            FitBox(item, bundle);
        }

        private static void FitBox(ShipItem item, Bounds bundle)
        {
            bool twinNeedsBox = item.itemRigidbodyC != null && HasMeshCollider(item.itemRigidbodyC.gameObject);
            Mesh box = null;
            if (HasMeshCollider(item.gameObject) || twinNeedsBox)
                box = BoxMesh(item.GetInstanceID(), bundle);

            FitOn(item.gameObject, bundle, box);
            if (item.itemRigidbodyC != null)
                FitOn(item.itemRigidbodyC.gameObject, bundle, box);
        }

        // The hull is built from where the pieces reach: at each of up to HullBands
        // heights, how far they go in HullSides directions around the up axis. Each band
        // becomes a ring of points just outside them, and the collider wraps every ring.
        // The core is a box well inside the bottom band, for the item's own collider.
        private static Mesh HullMesh(ShipItem item, List<Vector3> centers, Mesh mesh, List<Quaternion> turns, int upAxis, out Bounds core)
        {
            Bounds stick = mesh.bounds;
            int axisA = (upAxis + 1) % 3;
            int axisB = (upAxis + 2) % 3;
            var corners = new List<Vector3>(centers.Count * 8);
            float low = float.MaxValue;
            float high = float.MinValue;
            for (int i = 0; i < centers.Count; i++)
            {
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = centers[i] + turns[i] * new Vector3(
                        (corner & 1) == 0 ? stick.min.x : stick.max.x,
                        (corner & 2) == 0 ? stick.min.y : stick.max.y,
                        (corner & 4) == 0 ? stick.min.z : stick.max.z);
                    corners.Add(point);
                    low = Mathf.Min(low, point[upAxis]);
                    high = Mathf.Max(high, point[upAxis]);
                }
            }

            int bands = Mathf.Clamp(Mathf.CeilToInt((high - low) / Mathf.Max(stick.size[upAxis] * 2f, 0.01f)), 1, HullBands);
            float bandHeight = Mathf.Max(high - low, 0.001f) / bands;
            var reach = new float[bands, HullSides];
            var filled = new bool[bands];
            for (int band = 0; band < bands; band++)
                for (int k = 0; k < HullSides; k++)
                    reach[band, k] = float.MinValue;

            for (int c = 0; c < corners.Count; c++)
            {
                Vector3 point = corners[c];
                int band = Mathf.Min(bands - 1, (int)((point[upAxis] - low) / bandHeight));
                filled[band] = true;
                for (int k = 0; k < HullSides; k++)
                {
                    float angle = k * Mathf.PI * 2f / HullSides;
                    float along = point[axisA] * Mathf.Cos(angle) + point[axisB] * Mathf.Sin(angle);
                    if (along > reach[band, k])
                        reach[band, k] = along;
                }
            }

            var vertices = new List<Vector3>(bands * HullSides * 2);
            var triangles = new List<int>(bands * HullSides * 6);
            float turn = Mathf.PI * 2f / HullSides;
            float sinTurn = Mathf.Sin(turn);
            for (int band = 0; band < bands; band++)
            {
                if (!filled[band])
                    continue;
                int first = vertices.Count;
                float bottom = low + band * bandHeight;
                float top = band == bands - 1 ? high : bottom + bandHeight;
                for (int level = 0; level < 2; level++)
                {
                    for (int k = 0; k < HullSides; k++)
                    {
                        // Where this direction's edge meets the next one's.
                        int next = (k + 1) % HullSides;
                        float a1 = k * turn;
                        float a2 = next * turn;
                        float s1 = reach[band, k] + HullMargin;
                        float s2 = reach[band, next] + HullMargin;
                        Vector3 point = Vector3.zero;
                        point[axisA] = (s1 * Mathf.Sin(a2) - s2 * Mathf.Sin(a1)) / sinTurn;
                        point[axisB] = (Mathf.Cos(a1) * s2 - Mathf.Cos(a2) * s1) / sinTurn;
                        point[upAxis] = level == 0 ? bottom - HullMargin : top + HullMargin;
                        vertices.Add(point);
                    }
                }

                for (int k = 0; k < HullSides; k++)
                {
                    int next = (k + 1) % HullSides;
                    triangles.Add(first + k);
                    triangles.Add(first + HullSides + k);
                    triangles.Add(first + next);
                    triangles.Add(first + next);
                    triangles.Add(first + HullSides + k);
                    triangles.Add(first + HullSides + next);
                }
            }

            // Box inside the bottom band: its reach both ways along each side axis.
            int quarter = HullSides / 4;
            float plusA = reach[0, 0];
            float minusA = reach[0, quarter * 2];
            float plusB = reach[0, quarter];
            float minusB = reach[0, quarter * 3];
            Vector3 middle = Vector3.zero;
            Vector3 size = Vector3.zero;
            middle[axisA] = (plusA - minusA) * 0.5f;
            middle[axisB] = (plusB - minusB) * 0.5f;
            size[axisA] = Mathf.Max(0.05f, (plusA + minusA) * CoreShare);
            size[axisB] = Mathf.Max(0.05f, (plusB + minusB) * CoreShare);
            // No higher than half the heap: a cone's sides close in toward the top.
            float coreHeight = Mathf.Min(bandHeight, (high - low) * 0.5f);
            middle[upAxis] = low + coreHeight * 0.5f;
            size[upAxis] = coreHeight;
            core = new Bounds(middle, size);

            return KeepHull(item, vertices, triangles);
        }

        // The collider of a net bag follows its net: a ring at each of a few heights, as
        // far out as the net goes in each of HullSides directions, from a flat foot at the
        // knot's height up to the tie. Built from the pieces' boxes instead, it stood well
        // clear of round fruit, whose boxes have corners the fruit does not, and bags
        // piled together sat far apart. The core is a box inside the bottom layer, for the
        // item's own collider.
        private static Mesh BagHull(
            ShipItem item,
            System.Func<float, Vector2, float> profile,
            float bottom,
            float rim,
            float widest,
            float neck,
            out Bounds core)
        {
            float turn = Mathf.PI * 2f / HullSides;
            float sinTurn = Mathf.Sin(turn);
            // The strands lie on the outside of the shape the net is drawn around.
            float margin = NetWidth * 2f;
            var heights = new List<float> { bottom, rim, widest };
            for (int step = 1; step <= 5; step++)
                heights.Add(Mathf.Lerp(widest, neck, step / 5f));
            heights.Add(0f);

            var vertices = new List<Vector3>(heights.Count * HullSides);
            var triangles = new List<int>((heights.Count - 1) * HullSides * 6);
            var reach = new float[HullSides];
            core = new Bounds(new Vector3(0f, rim, 0f), Vector3.one * 0.05f);
            for (int ring = 0; ring < heights.Count; ring++)
            {
                for (int k = 0; k < HullSides; k++)
                {
                    var way = new Vector2(Mathf.Cos(k * turn), Mathf.Sin(k * turn));
                    // The foot is a flat stand under the rim, and the top a small ring at
                    // the tie. Every ring between is the net itself.
                    if (ring == 0)
                        reach[k] = (profile(rim, way) + margin) * BagFoot;
                    else if (ring == heights.Count - 1)
                        reach[k] = BagTie;
                    else
                        reach[k] = profile(heights[ring], way) + margin;
                }

                // Box inside the bottom layer: its reach both ways across the bag.
                if (ring == 2)
                {
                    int quarter = HullSides / 4;
                    float plusX = reach[0];
                    float minusX = reach[quarter * 2];
                    float plusZ = reach[quarter];
                    float minusZ = reach[quarter * 3];
                    float coreHeight = Mathf.Max(0.02f, Mathf.Min((widest - rim) * 2f, (neck - rim) * 0.5f));
                    core = new Bounds(
                        new Vector3((plusX - minusX) * 0.5f, rim + coreHeight * 0.5f, (plusZ - minusZ) * 0.5f),
                        new Vector3(
                            Mathf.Max(0.05f, (plusX + minusX) * CoreShare),
                            coreHeight,
                            Mathf.Max(0.05f, (plusZ + minusZ) * CoreShare)));
                }

                for (int k = 0; k < HullSides; k++)
                {
                    // Where this direction's edge meets the next one's.
                    int next = (k + 1) % HullSides;
                    float a1 = k * turn;
                    float a2 = next * turn;
                    vertices.Add(new Vector3(
                        (reach[k] * Mathf.Sin(a2) - reach[next] * Mathf.Sin(a1)) / sinTurn,
                        heights[ring],
                        (Mathf.Cos(a1) * reach[next] - Mathf.Cos(a2) * reach[k]) / sinTurn));
                }

                if (ring == 0)
                    continue;
                int first = (ring - 1) * HullSides;
                for (int k = 0; k < HullSides; k++)
                {
                    int next = (k + 1) % HullSides;
                    triangles.Add(first + k);
                    triangles.Add(first + HullSides + k);
                    triangles.Add(first + next);
                    triangles.Add(first + next);
                    triangles.Add(first + HullSides + k);
                    triangles.Add(first + HullSides + next);
                }
            }

            return KeepHull(item, vertices, triangles);
        }

        // Makes the hull's mesh and remembers it for the item, in place of any it had.
        private static Mesh KeepHull(ShipItem item, List<Vector3> vertices, List<int> triangles)
        {
            int id = item.GetInstanceID();
            Mesh previous;
            if (HullMeshes.TryGetValue(id, out previous) && previous != null)
                Object.Destroy(previous);
            HullMeshes.Remove(id);
            if (vertices.Count < 4)
                return null;

            var hull = new Mesh();
            hull.name = HullName;
            hull.SetVertices(vertices);
            hull.SetTriangles(triangles, 0);
            hull.RecalculateBounds();
            HullMeshes[id] = hull;
            return hull;
        }

        // The hull is an item subcollider: looking at it looks at the item. Its twin on
        // the physics body joins the body's subcolliders, so it turns into a trigger while
        // the stack is held like the game's own. A body that has not started yet copies
        // the hull itself when it does.
        private static void AddHull(ShipItem item, Mesh hull)
        {
            Collider rootCollider = item.GetComponent<Collider>();
            var shape = new GameObject(HullName);
            shape.tag = "ItemSubcollider";
            shape.layer = item.gameObject.layer;
            shape.transform.SetParent(item.transform, false);
            MeshCollider collider = shape.AddComponent<MeshCollider>();
            collider.convex = true;
            collider.sharedMesh = hull;
            collider.isTrigger = rootCollider != null && rootCollider.isTrigger;

            ItemRigidbody body = item.itemRigidbodyC;
            if (body == null || BodyField == null || BodyField.GetValue(body) == null || SubcollidersField == null)
                return;
            var subcolliders = SubcollidersField.GetValue(body) as List<Collider>;
            if (subcolliders == null)
            {
                subcolliders = new List<Collider>();
                SubcollidersField.SetValue(body, subcolliders);
            }

            var twin = new GameObject(HullName);
            twin.layer = 2;
            twin.transform.SetParent(body.transform, false);
            MeshCollider twinCollider = twin.AddComponent<MeshCollider>();
            twinCollider.convex = true;
            twinCollider.sharedMesh = hull;
            twinCollider.isTrigger = item.held != null;
            subcolliders.Add(twinCollider);
        }

        private static void ClearHull(ShipItem item)
        {
            if (item == null)
                return;
            DestroyHull(item.transform, null);
            ItemRigidbody body = item.itemRigidbodyC;
            if (body == null)
                return;
            var subcolliders = SubcollidersField != null ? SubcollidersField.GetValue(body) as List<Collider> : null;
            DestroyHull(body.transform, subcolliders);
        }

        // The body's own copy of the hull is named "<HullName>(Clone)".
        private static void DestroyHull(Transform parent, List<Collider> subcolliders)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (!child.name.StartsWith(HullName, System.StringComparison.Ordinal))
                    continue;
                // The body sets every listed collider's trigger while held, so a destroyed
                // one must leave the list first.
                if (subcolliders != null)
                    subcolliders.Remove(child.GetComponent<Collider>());
                Object.Destroy(child.gameObject);
            }
        }

        private static Bounds BundleBounds(List<Vector3> centers, Mesh mesh, List<Quaternion> turns = null)
        {
            Bounds stick = mesh != null ? mesh.bounds : new Bounds(Vector3.zero, new Vector3(0.08f, 0.08f, 0.4f));
            Bounds bundle = new Bounds(centers[0] + stick.center, stick.size);
            for (int i = 0; i < centers.Count; i++)
            {
                if (turns == null)
                {
                    bundle.Encapsulate(new Bounds(centers[i] + stick.center, stick.size));
                    continue;
                }

                // A turned piece reaches out past its unturned box, so take each corner.
                if (i == 0)
                    bundle = new Bounds(centers[0] + turns[0] * stick.center, Vector3.zero);
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = new Vector3(
                        (corner & 1) == 0 ? stick.min.x : stick.max.x,
                        (corner & 2) == 0 ? stick.min.y : stick.max.y,
                        (corner & 4) == 0 ? stick.min.z : stick.max.z);
                    bundle.Encapsulate(centers[i] + turns[i] * point);
                }
            }

            return bundle;
        }

        private static void CopyColliders(GameObject source, GameObject dest)
        {
            if (source == null || dest == null)
                return;

            BoxCollider sourceBox = source.GetComponent<BoxCollider>();
            BoxCollider destBox = dest.GetComponent<BoxCollider>();
            if (sourceBox != null && destBox != null)
            {
                destBox.center = sourceBox.center;
                destBox.size = sourceBox.size;
            }

            SphereCollider sourceSphere = source.GetComponent<SphereCollider>();
            SphereCollider destSphere = dest.GetComponent<SphereCollider>();
            if (sourceSphere != null && destSphere != null)
            {
                destSphere.center = sourceSphere.center;
                destSphere.radius = sourceSphere.radius;
            }

            CapsuleCollider sourceCapsule = source.GetComponent<CapsuleCollider>();
            CapsuleCollider destCapsule = dest.GetComponent<CapsuleCollider>();
            if (sourceCapsule != null && destCapsule != null)
            {
                destCapsule.center = sourceCapsule.center;
                destCapsule.radius = sourceCapsule.radius;
                destCapsule.height = sourceCapsule.height;
                destCapsule.direction = sourceCapsule.direction;
            }

            MeshCollider sourceMesh = source.GetComponent<MeshCollider>();
            MeshCollider destMesh = dest.GetComponent<MeshCollider>();
            if (sourceMesh != null && destMesh != null && sourceMesh.sharedMesh != null)
            {
                destMesh.sharedMesh = sourceMesh.sharedMesh;
                destMesh.convex = sourceMesh.convex;
            }
        }

        private static bool HasMeshCollider(GameObject host)
        {
            return host != null && host.GetComponent<MeshCollider>() != null;
        }

        private static void FitOn(GameObject host, Bounds bundle, Mesh box)
        {
            if (host == null)
                return;

            Collider[] colliders = host.GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                BoxCollider boxCollider = colliders[i] as BoxCollider;
                if (boxCollider != null)
                {
                    boxCollider.center = bundle.center;
                    boxCollider.size = bundle.size;
                    continue;
                }

                // A round collider cannot be flat, so it shrinks to fit inside the box.
                SphereCollider sphere = colliders[i] as SphereCollider;
                if (sphere != null)
                {
                    sphere.center = bundle.center;
                    sphere.radius = Mathf.Max(0.01f, Mathf.Min(bundle.size.x, Mathf.Min(bundle.size.y, bundle.size.z)) * 0.5f);
                    continue;
                }

                CapsuleCollider capsule = colliders[i] as CapsuleCollider;
                if (capsule != null)
                {
                    int direction = capsule.direction;
                    capsule.center = bundle.center;
                    capsule.height = bundle.size[direction];
                    float radius = 0.01f;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        if (axis != direction)
                            radius = Mathf.Max(radius, bundle.size[axis] * 0.5f);
                    }
                    capsule.radius = radius;
                    if (capsule.height < capsule.radius * 2f)
                        capsule.height = capsule.radius * 2f;
                    continue;
                }

                MeshCollider meshCollider = colliders[i] as MeshCollider;
                if (meshCollider != null && box != null)
                {
                    meshCollider.convex = true;
                    meshCollider.sharedMesh = box;
                }
            }
        }

        private static Mesh BoxMesh(int ownerId, Bounds bundle)
        {
            Mesh previous;
            if (BoxMeshes.TryGetValue(ownerId, out previous) && previous != null)
                Object.Destroy(previous);

            Vector3 c = bundle.center;
            Vector3 h = bundle.size * 0.5f;
            var mesh = new Mesh();
            mesh.name = BoxMeshName;
            mesh.vertices = new[]
            {
                c + new Vector3(-h.x, -h.y, -h.z),
                c + new Vector3(-h.x, -h.y, h.z),
                c + new Vector3(-h.x, h.y, -h.z),
                c + new Vector3(-h.x, h.y, h.z),
                c + new Vector3(h.x, -h.y, -h.z),
                c + new Vector3(h.x, -h.y, h.z),
                c + new Vector3(h.x, h.y, -h.z),
                c + new Vector3(h.x, h.y, h.z)
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 1, 2, 3,
                4, 5, 6, 5, 7, 6,
                0, 1, 4, 1, 5, 4,
                2, 6, 3, 3, 6, 7,
                0, 4, 2, 2, 4, 6,
                1, 3, 5, 3, 7, 5
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            BoxMeshes[ownerId] = mesh;
            return mesh;
        }
    }
}
