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
        private const float RibbonThickness = 0.0016f;
        // Local Y. If a placed sausage tower lies on its side, this is the axis to change.
        private const int StackUpAxis = 1;

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
            if (item == null || FirewoodPieces.CountOf(item) <= 1)
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
            float pitch = cross * (kind != null ? kind.Pitch : 0.97f);
            List<Quaternion> turns = null;
            List<Vector3> centers;
            if (kind == BundleKind.Sausage && mesh != null && SausageStacks.WidthOf(item) == SausageStacks.PileWidth)
                centers = PileCenters(item, count, mesh, longAxis, FirewoodBundleConfig.SausageSpacing, out turns);
            else if (kind == BundleKind.Sausage && mesh != null)
                centers = StackCenters(item, count, mesh, longAxis, FirewoodBundleConfig.SausageSpacing);
            else
                centers = FirewoodPieces.Centers(count, pitch, longAxis);
            ClearSticks(item);
            if (mesh != null && source != null)
            {
                source.enabled = false;
                Material[] materials = source.sharedMaterials;
                var renderers = new List<Renderer>();
                for (int i = 0; i < centers.Count; i++)
                    renderers.Add(CreateStick(item, mesh, materials, centers[i], turns != null ? turns[i] : Quaternion.identity, source));
                if (kind == null || kind.Tie != TieStyle.None)
                {
                    Renderer tie = CreateString(item, centers, mesh, longAxis, cross, source);
                    if (tie != null)
                        renderers.Add(tie);
                    Renderer knot = CreateKnot(item, centers, mesh, longAxis, cross);
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
            FitColliders(item, centers, mesh, turns);
            // Growing the physics shape while the hull is heeled over shoves the boat.
            // Ignore the hull capsule until the bundle is clear of it.
            QuietHull(item);
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

        // A pile grows toward a pyramid in shells. Shell S adds one sausage to the outer
        // edge of every layer, alternating sides, and starts a new layer on top, so S
        // shells make a pyramid S wide and S high. Each shell fills from the bottom up,
        // so a new sausage always has one under it. Layers cross at roughly right angles
        // with some play. A sausage's place depends only on its index and the stack's
        // saved id, so adding one never moves the rest, and a reloaded pile looks the same.
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

            var centers = new List<Vector3>(count);
            turns = new List<Quaternion>(count);
            int shell = 1;
            int shellStart = 0;
            for (int i = 0; i < count; i++)
            {
                if (i - shellStart >= shell)
                {
                    shellStart += shell;
                    shell++;
                }

                int layer = i - shellStart;
                int slot = shell - 1 - layer;
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
            }

            return centers;
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
            MeshRenderer source)
        {
            var stick = new GameObject(StickName);
            stick.layer = item.gameObject.layer;
            stick.transform.SetParent(item.transform, false);
            stick.transform.localPosition = localPosition;
            stick.transform.localRotation = localRotation;
            stick.transform.localScale = Vector3.one;

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
            if (item == null || FirewoodPieces.CountOf(item) <= 1)
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
            if (item == null || FirewoodPieces.CountOf(item) <= 1)
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
        }

        private static Renderer CreateString(
            ShipItem item,
            List<Vector3> centers,
            Mesh logMesh,
            int longAxis,
            float cross,
            MeshRenderer source)
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
            int axisA = (longAxis + 1) % 3;
            int axisB = (longAxis + 2) % 3;
            float halfA = bundle.size[axisA] * 0.5f + cord;
            float halfB = bundle.size[axisB] * 0.5f + cord;
            float corner = Mathf.Min(cross * 0.5f + cord, halfA, halfB);
            if (corner < 0.002f)
                corner = Mathf.Min(halfA, halfB);

            List<Vector3> loop = PartialTopLoop(centers, logMesh, bundle.center, axisA, axisB, cord, cross);
            if (loop == null)
                loop = StringLoop(bundle.center, axisA, axisB, halfA, halfB, corner, cross);
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
            float cross)
        {
            if (item == null || centers == null || centers.Count < 2 || logMesh == null)
                return null;

            BundleKind kind = FirewoodPieces.KindOf(item);
            Material material = StringMaterial(kind, FirewoodPieces.ColorOf(item));
            if (material == null)
                return null;
            bool ribbon = kind != null && kind.Ribbon;

            int axisA = (longAxis + 1) % 3;
            int upAxis = (longAxis + 2) % 3;
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
            origin[upAxis] = topRow + logMesh.bounds.max[upAxis];
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
            if (kind == null)
                kind = BundleKind.Firewood;
            if (color < 0 || color >= kind.ColorCount)
                color = 0;
            string key = kind.BundleName + "/" + color;
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
                Plugin.Log.LogWarning("Could not find a shader for the " + kind.BundleName + " tie.");
                return null;
            }

            var material = new Material(shader);
            material.name = StringName;
            material.color = kind.Colors[color];
            if (shader.name == "Standard")
            {
                material.SetFloat("_Metallic", 0f);
                // A ribbon is satin, a little shinier than cord.
                material.SetFloat("_Glossiness", kind.Ribbon ? 0.35f : 0.12f);
            }

            StringMaterials[key] = material;
            return material;
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

        private static void FitColliders(ShipItem item, List<Vector3> centers, Mesh mesh, List<Quaternion> turns)
        {
            Bounds bundle = BundleBounds(centers, mesh, turns);
            bundle.Expand(0.04f);
            bool twinNeedsBox = item.itemRigidbodyC != null && HasMeshCollider(item.itemRigidbodyC.gameObject);
            Mesh box = null;
            if (HasMeshCollider(item.gameObject) || twinNeedsBox)
                box = BoxMesh(item.GetInstanceID(), bundle);

            FitOn(item.gameObject, bundle, box);
            if (item.itemRigidbodyC != null)
                FitOn(item.itemRigidbodyC.gameObject, bundle, box);
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
