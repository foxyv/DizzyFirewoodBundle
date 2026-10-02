using System.Collections.Generic;
using cakeslice;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.FirewoodBundle
{
    internal static class HookLineBuilder
    {
        private const string HookName = "HookLineHook";
        private const string CordName = "HookLineCord";

        private static Material _lineMaterial;

        internal static void RestoreSingle(ShipItem item)
        {
            if (item == null)
                return;

            HookLinePieces.WriteCount(item, 1);
            ClearChildren(item);
            ReleaseHang(item);
            Renderer root = item.GetComponent<Renderer>();
            if (root != null)
            {
                root.enabled = true;
                IncludeInLod(item, new List<Renderer> { root });
                ReleaseLod(item);
            }

            ShipItem prefab = FirewoodPieces.PrefabOf(item);
            if (prefab != null)
            {
                item.mass = prefab.mass > 0f ? prefab.mass : item.mass;
                item.holdDistance = prefab.holdDistance;
                item.lookText = prefab.lookText;
                item.description = prefab.description;
                CopyColliderShape(prefab.gameObject, item.gameObject);
                if (item.itemRigidbodyC != null)
                    CopyColliderShape(prefab.gameObject, item.itemRigidbodyC.gameObject);
            }

            if (item.itemRigidbodyC != null)
                item.itemRigidbodyC.UpdateMass();
            FirewoodBundleBuilder.QuietHull(item);
            item.UpdateLookText();
        }

        internal static void Apply(ShipItem item)
        {
            int count = HookLinePieces.CountOf(item);
            if (item == null || count < 1 || !HookLinePieces.IsLine(item))
                return;

            MeshRenderer source = item.GetComponent<MeshRenderer>();
            MeshFilter filter = item.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || source == null)
                return;

            ClearChildren(item);
            source.enabled = false;

            int longAxis = LongAxis(mesh);
            int sideAxis = ThinAxis(mesh);
            Quaternion rotation = DangleRotation(mesh, longAxis);
            Vector3 eye = EyePoint(mesh, longAxis);
            Vector3 side = Vector3.zero;
            side[sideAxis] = 1f;
            side = rotation * side;

            // Hooks sit close together on a bowed bottom, each one a little off the perfect arc.
            // The string rises from both ends to the origin, which is the hang point.
            float spacing = Mathf.Max(mesh.bounds.size[sideAxis] * 1.2f, 0.013f);
            float span = (count - 1) * spacing;
            float half = span * 0.5f;
            float sag = Mathf.Max(0.012f, span * 0.15f);
            float rise = Mathf.Max(0.055f, half * 0.95f);
            var knots = new Vector3[count];
            for (int i = 0; i < count; i++)
                knots[i] = ArcKnot(i, count, side, half, rise, sag) + Wobble(i, side, spacing);

            var renderers = new List<Renderer>();
            Bounds rack = new Bounds(Vector3.zero, Vector3.zero);
            bool hasRack = false;
            rack.Encapsulate(Vector3.zero);
            hasRack = true;
            for (int i = 0; i < count; i++)
            {
                Quaternion hookRotation = rotation * Quaternion.Euler(WobbleAngle(i, 4, 10f), WobbleAngle(i, 5, 24f), WobbleAngle(i, 6, 14f));
                Vector3 place = knots[i] - hookRotation * eye;
                renderers.Add(CreateHook(item, mesh, source.sharedMaterials, place, hookRotation, source));
                EncapsulateMesh(ref rack, ref hasRack, mesh, place, hookRotation);
            }

            if (count == 1)
            {
                Vector3 left = knots[0] - side * 0.006f;
                Vector3 right = knots[0] + side * 0.006f;
                AddCord(item, source, renderers, Vector3.zero, left);
                AddCord(item, source, renderers, left, right);
                AddCord(item, source, renderers, right, Vector3.zero);
            }
            else
            {
                AddCord(item, source, renderers, Vector3.zero, knots[0]);
                for (int i = 0; i < count - 1; i++)
                    AddCord(item, source, renderers, knots[i], knots[i + 1]);
                AddCord(item, source, renderers, knots[count - 1], Vector3.zero);
            }
            IncludeInLod(item, renderers);

            item.lookText = HookLinePieces.LookText(count);
            item.description = "";
            ApplyMass(item, count);
            if (StoredInCrate(item))
                CopyPrefabCollider(item);
            else
                FitLine(item, rack);
            EnsureHang(item);
            FirewoodBundleBuilder.QuietHull(item);
        }

        internal static void Forget(ShipItem item)
        {
            if (item == null)
                return;
            ReleaseCord(item);
        }

        internal static void Discard(ShipItem item)
        {
            if (item == null)
                return;
            ReleaseHang(item);
            ClearChildren(item);
        }

        internal static void SyncOutline(ShipItem item)
        {
            if (item == null || !HookLinePieces.IsLine(item))
                return;

            Outline root = item.GetComponent<Outline>();
            if (root == null)
                return;

            Transform transform = item.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name != HookName && child.name != CordName)
                    continue;

                Outline outline = child.GetComponent<Outline>();
                if (outline == null)
                    continue;

                outline.color = root.color;
                outline.eraseRenderer = root.eraseRenderer;
                if (outline.enabled != root.enabled)
                    outline.enabled = root.enabled;
            }

            // The original mesh is hidden. Leaving its outline on draws a hook that is not on the line.
            if (root.enabled)
                root.enabled = false;
        }

        private static void EnsureHang(ShipItem item)
        {
            if (item.GetComponent<HangableItem>() == null)
                item.gameObject.AddComponent<HangableItem>();
        }

        private static void ReleaseHang(ShipItem item)
        {
            HangableItem hang = item.GetComponent<HangableItem>();
            if (hang == null)
                return;
            if (hang.IsHanging())
                hang.DisconnectJoint();
            Object.Destroy(hang);
        }

        private static int LongAxis(Mesh mesh)
        {
            Vector3 size = mesh.bounds.size;
            int axis = 0;
            float longest = size.x;
            if (size.y > longest)
            {
                longest = size.y;
                axis = 1;
            }
            if (size.z > longest)
                axis = 2;
            return axis;
        }

        private static int ThinAxis(Mesh mesh)
        {
            Vector3 size = mesh.bounds.size;
            int axis = 0;
            float thinnest = size.x;
            if (size.y < thinnest)
            {
                thinnest = size.y;
                axis = 1;
            }
            if (size.z < thinnest)
                axis = 2;
            return axis;
        }

        // The rod ties its line to the hook pivot. The eye is the end of the shank nearest that pivot.
        private static Vector3 EyePoint(Mesh mesh, int longAxis)
        {
            Bounds bounds = mesh.bounds;
            float eye = Mathf.Abs(bounds.max[longAxis]) <= Mathf.Abs(bounds.min[longAxis])
                ? bounds.max[longAxis]
                : bounds.min[longAxis];
            Vector3 point = Vector3.zero;
            point[longAxis] = eye;
            return point;
        }

        private static Quaternion DangleRotation(Mesh mesh, int longAxis)
        {
            Bounds bounds = mesh.bounds;
            float eye = EyePoint(mesh, longAxis)[longAxis];
            float tip = Mathf.Approximately(eye, bounds.max[longAxis]) ? bounds.min[longAxis] : bounds.max[longAxis];
            Vector3 towardTip = Vector3.zero;
            float delta = tip - eye;
            towardTip[longAxis] = Mathf.Abs(delta) < 0.0001f ? -1f : Mathf.Sign(delta);
            return Quaternion.FromToRotation(towardTip, Vector3.down);
        }

        private static void EncapsulateMesh(ref Bounds rack, ref bool started, Mesh mesh, Vector3 place, Quaternion rotation)
        {
            Bounds local = mesh.bounds;
            Vector3 center = local.center;
            Vector3 extents = local.extents;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = place + rotation * (center + Vector3.Scale(extents, new Vector3(x, y, z)));
                        if (!started)
                        {
                            rack = new Bounds(corner, Vector3.zero);
                            started = true;
                        }
                        else
                        {
                            rack.Encapsulate(corner);
                        }
                    }
                }
            }
        }

        private static MeshRenderer CreateHook(
            ShipItem item,
            Mesh mesh,
            Material[] materials,
            Vector3 localPosition,
            Quaternion localRotation,
            MeshRenderer source)
        {
            var hook = new GameObject(HookName);
            hook.layer = item.gameObject.layer;
            hook.transform.SetParent(item.transform, false);
            hook.transform.localPosition = localPosition;
            hook.transform.localRotation = localRotation;
            hook.transform.localScale = Vector3.one;

            MeshFilter filter = hook.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = hook.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = source.shadowCastingMode;
            renderer.receiveShadows = source.receiveShadows;
            AddOutline(item, hook);
            return renderer;
        }

        private static Vector3 ArcKnot(int index, int count, Vector3 side, float half, float rise, float sag)
        {
            float t = count <= 1 ? 0.5f : index / (float)(count - 1);
            float along = Mathf.Lerp(-half, half, t);
            float nx = half > 0.0001f ? along / half : 0f;
            float drop = rise + sag * (1f - nx * nx);
            return side * along + Vector3.down * drop;
        }

        private static Vector3 Wobble(int index, Vector3 side, float spacing)
        {
            if (side.sqrMagnitude < 0.0001f)
                side = Vector3.right;
            side.Normalize();
            Vector3 depth = Vector3.Cross(Vector3.up, side);
            if (depth.sqrMagnitude < 0.01f)
                depth = Vector3.forward;
            depth.Normalize();
            float mess = FirewoodBundleConfig.HookScatter;
            return (side * (HashUnit(index, 1) * spacing * 0.22f)
                + depth * (HashUnit(index, 2) * 0.005f)
                + Vector3.down * (HashUnit(index, 3) * 0.006f)) * mess;
        }

        private static float WobbleAngle(int index, int salt, float degrees)
        {
            return HashUnit(index, salt) * degrees * FirewoodBundleConfig.HookScatter;
        }

        private static float HashUnit(int index, int salt)
        {
            uint n = (uint)(index * 374761393 + salt * 668265263);
            n = (n ^ (n >> 13)) * 1274126177u;
            n ^= n >> 16;
            return ((n & 65535) / 32767.5f) - 1f;
        }

        private static void AddCord(ShipItem item, MeshRenderer source, List<Renderer> renderers, Vector3 from, Vector3 to)
        {
            Renderer cord = CreateCord(item, from, to, source);
            if (cord != null)
                renderers.Add(cord);
        }

        private static Renderer CreateCord(ShipItem item, Vector3 from, Vector3 to, MeshRenderer source)
        {
            Material material = LineMaterial();
            if (material == null)
                return null;

            float thickness = 0.0028f;
            var cord = new GameObject(CordName);
            cord.layer = item.gameObject.layer;
            cord.transform.SetParent(item.transform, false);

            MeshFilter filter = cord.AddComponent<MeshFilter>();
            filter.sharedMesh = CordMesh(from, to, thickness);
            MeshRenderer renderer = cord.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = source != null ? source.shadowCastingMode : UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            AddOutline(item, cord);
            return renderer;
        }

        private static void AddOutline(ShipItem item, GameObject child)
        {
            Outline rootOutline = item.GetComponent<Outline>();
            Outline outline = child.AddComponent<Outline>();
            outline.enabled = false;
            if (rootOutline == null)
                return;
            outline.color = rootOutline.color;
            outline.eraseRenderer = rootOutline.eraseRenderer;
            outline.originalLayer = rootOutline.originalLayer;
        }

        private static Mesh CordMesh(Vector3 from, Vector3 to, float thickness)
        {
            Vector3 axis = to - from;
            float length = axis.magnitude;
            if (length < 0.001f)
            {
                axis = Vector3.right * 0.001f;
                length = 0.001f;
            }

            Vector3 dir = axis / length;
            Vector3 side = Vector3.Cross(dir, Vector3.up);
            if (side.sqrMagnitude < 0.01f)
                side = Vector3.Cross(dir, Vector3.forward);
            side.Normalize();
            Vector3 up = Vector3.Cross(dir, side);
            float half = thickness * 0.5f;
            Vector3 a = side * half;
            Vector3 b = up * half;
            var mesh = new Mesh();
            mesh.name = CordName;
            mesh.vertices = new[]
            {
                from - a - b,
                from - a + b,
                to - a - b,
                to - a + b,
                from + a - b,
                from + a + b,
                to + a - b,
                to + a + b
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
            return mesh;
        }

        private static Mesh BoxMesh(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            var mesh = new Mesh();
            mesh.name = CordName;
            mesh.vertices = new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
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
            return mesh;
        }

        private static void ClearChildren(ShipItem item)
        {
            for (int i = item.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = item.transform.GetChild(i);
                if (child.name != HookName && child.name != CordName)
                    continue;
                if (child.name == CordName)
                    ReleaseCord(child);
                Object.Destroy(child.gameObject);
            }
        }

        private static void ReleaseCord(ShipItem item)
        {
            if (item == null)
                return;
            Transform transform = item.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name == CordName)
                    ReleaseCord(child);
            }
        }

        private static void ReleaseCord(Transform child)
        {
            if (child == null)
                return;
            MeshFilter filter = child.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null && filter.sharedMesh.name == CordName)
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

            // A crate draws the line very small. A lower LOD still points at the
            // hidden original mesh, so the bundle vanishes inside the chest.
            Renderer[] shown = renderers.ToArray();
            for (int i = 0; i < lods.Length; i++)
                lods[i].renderers = shown;
            try
            {
                group.SetLODs(lods);
                group.ForceLOD(0);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning("Could not update hook line LOD: " + ex.Message);
            }
        }

        private static void ReleaseLod(ShipItem item)
        {
            LODGroup group = item.GetComponent<LODGroup>();
            if (group == null)
                return;
            group.ForceLOD(-1);
        }

        private static bool StoredInCrate(ShipItem item)
        {
            SaveablePrefab save = item.GetComponent<SaveablePrefab>();
            return save != null && save.currentCrateId != 0;
        }

        private static void CopyPrefabCollider(ShipItem item)
        {
            ShipItem prefab = FirewoodPieces.PrefabOf(item);
            if (prefab == null)
                return;
            CopyColliderShape(prefab.gameObject, item.gameObject);
            if (item.itemRigidbodyC != null)
                CopyColliderShape(prefab.gameObject, item.itemRigidbodyC.gameObject);
        }

        private static void ApplyMass(ShipItem item, int count)
        {
            ShipItem prefab = FirewoodPieces.PrefabOf(item);
            float baseMass = prefab != null ? prefab.mass : item.mass;
            if (baseMass <= 0f)
                baseMass = 0.2f;
            item.mass = baseMass * count;

            ItemRigidbody body = item.itemRigidbodyC;
            if (body == null)
                return;
            var joint = AccessTools.Field(typeof(ItemRigidbody), "rigidbody");
            if (joint == null || joint.GetValue(body) == null)
                return;
            body.UpdateMass();
        }

        private static void FitLine(ShipItem item, Bounds bounds)
        {
            if (bounds.size.sqrMagnitude < 0.0001f)
                bounds = new Bounds(Vector3.zero, new Vector3(0.04f, 0.2f, 0.04f));
            FitOn(item.gameObject, bounds);
            if (item.itemRigidbodyC != null)
                FitOn(item.itemRigidbodyC.gameObject, bounds);
        }

        private static void FitOn(GameObject host, Bounds bounds)
        {
            if (host == null)
                return;

            BoxCollider box = host.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.center = bounds.center;
                box.size = bounds.size;
            }

            CapsuleCollider capsule = host.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                int direction = 1;
                if (bounds.size.x >= bounds.size.y && bounds.size.x >= bounds.size.z)
                    direction = 0;
                else if (bounds.size.z >= bounds.size.y)
                    direction = 2;
                capsule.direction = direction;
                capsule.center = bounds.center;
                float radius = direction == 0
                    ? Mathf.Max(bounds.extents.y, bounds.extents.z)
                    : direction == 2
                        ? Mathf.Max(bounds.extents.x, bounds.extents.y)
                        : Mathf.Max(bounds.extents.x, bounds.extents.z);
                capsule.radius = radius;
                capsule.height = Mathf.Max(bounds.size[direction], radius * 2f);
            }

            MeshCollider meshCollider = host.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                meshCollider.convex = true;
                Mesh boxMesh = BoxMesh(bounds);
                Mesh previous = meshCollider.sharedMesh;
                meshCollider.sharedMesh = boxMesh;
                if (previous != null && previous.name == CordName)
                    Object.Destroy(previous);
            }
        }

        private static void CopyColliderShape(GameObject source, GameObject dest)
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

        private static Material LineMaterial()
        {
            if (_lineMaterial != null)
                return _lineMaterial;

            Shader shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Legacy Shaders/Diffuse");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                return null;

            _lineMaterial = new Material(shader);
            _lineMaterial.name = CordName;
            _lineMaterial.color = new Color(0.72f, 0.68f, 0.55f);
            if (shader.name == "Standard")
            {
                _lineMaterial.SetFloat("_Metallic", 0f);
                _lineMaterial.SetFloat("_Glossiness", 0.2f);
            }

            return _lineMaterial;
        }
    }
}
