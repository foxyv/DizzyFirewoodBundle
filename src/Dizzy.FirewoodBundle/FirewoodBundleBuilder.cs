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
        private const string BoxMeshName = "FirewoodBundleBox";

        private static readonly Dictionary<int, int> Stamps = new Dictionary<int, int>();
        private static readonly Dictionary<int, Mesh> BoxMeshes = new Dictionary<int, Mesh>();
        private static Material _stringMaterial;

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

            float pitch = cross * 0.97f;
            List<Vector3> centers = FirewoodPieces.Centers(count, pitch, longAxis);
            ClearSticks(item);
            if (mesh != null && source != null)
            {
                source.enabled = false;
                Material[] materials = source.sharedMaterials;
                var renderers = new List<Renderer>();
                for (int i = 0; i < centers.Count; i++)
                    renderers.Add(CreateStick(item, mesh, materials, centers[i], source));
                Renderer tie = CreateString(item, centers, mesh, longAxis, cross, source);
                if (tie != null)
                    renderers.Add(tie);
                IncludeInLod(item, renderers);
            }

            item.lookText = FirewoodPieces.LookText(count);
            ShipItem prefab = FirewoodPieces.PrefabOf(item);
            if (prefab != null)
                item.description = prefab.description;
            bool wasTwoHanded = item.big;
            item.big = true;
            if (!wasTwoHanded)
                AdoptTwoHandedHold(item);
            ApplyMass(item, count);
            ApplyHoldDistance(item, centers, longAxis, cross);
            FitColliders(item, centers, mesh);
        }

        private static void AdoptTwoHandedHold(ShipItem item)
        {
            GoPointer pointer = item.held;
            if (pointer == null)
                return;

            Vector3 localPos = pointer.transform.InverseTransformPoint(item.transform.position);
            Quaternion localRot = Quaternion.Inverse(pointer.transform.rotation) * item.transform.rotation;
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
                if (child.name != StickName && child.name != StringName)
                    continue;
                if (child.name == StringName)
                    ReleaseStringMesh(child);
                Object.Destroy(child.gameObject);
            }
        }

        private static MeshRenderer CreateStick(
            ShipItem item,
            Mesh mesh,
            Material[] materials,
            Vector3 localPosition,
            MeshRenderer source)
        {
            var stick = new GameObject(StickName);
            stick.layer = item.gameObject.layer;
            stick.transform.SetParent(item.transform, false);
            stick.transform.localPosition = localPosition;
            stick.transform.localRotation = Quaternion.identity;
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
                if (child.name != StickName && child.name != StringName)
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

            Material material = StringMaterial();
            if (material == null)
                return null;

            Bounds bundle = BundleBounds(centers, logMesh);
            float cord = Mathf.Clamp(cross * 0.055f, 0.0032f, 0.0075f);
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
            Mesh cordMesh = CordMesh(loop, longAxis, cord);
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

        private static Mesh CordMesh(List<Vector3> loop, int longAxis, float radius)
        {
            int count = loop.Count;
            if (count < 3)
                return null;

            const int sides = 6;
            Vector3 along = Vector3.zero;
            along[longAxis] = 1f;
            var vertices = new Vector3[count * sides];
            var normals = new Vector3[count * sides];
            var triangles = new int[count * sides * 6];
            Vector3 outward = Vector3.zero;

            for (int i = 0; i < count; i++)
            {
                Vector3 previous = loop[(i + count - 1) % count];
                Vector3 next = loop[(i + 1) % count];
                Vector3 tangent = next - previous;
                if (tangent.sqrMagnitude > 0.0000001f)
                    tangent.Normalize();
                else
                    tangent = along;

                Vector3 side = Vector3.Cross(tangent, along);
                if (side.sqrMagnitude > 0.0000001f)
                    outward = side.normalized;

                for (int s = 0; s < sides; s++)
                {
                    float angle = s * Mathf.PI * 2f / sides;
                    Vector3 normal = outward * Mathf.Cos(angle) + along * Mathf.Sin(angle);
                    int index = i * sides + s;
                    normals[index] = normal;
                    vertices[index] = loop[i] + normal * radius;
                }
            }

            int t = 0;
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
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
            mesh.name = StringName;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material StringMaterial()
        {
            if (_stringMaterial != null)
                return _stringMaterial;

            Shader shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Legacy Shaders/Diffuse");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Plugin.Log.LogWarning("Could not find a shader for the firewood bundle string.");
                return null;
            }

            _stringMaterial = new Material(shader);
            _stringMaterial.name = StringName;
            _stringMaterial.color = new Color(0.55f, 0.38f, 0.2f);
            if (shader.name == "Standard")
            {
                _stringMaterial.SetFloat("_Metallic", 0f);
                _stringMaterial.SetFloat("_Glossiness", 0.12f);
            }

            return _stringMaterial;
        }

        private static void ReleaseStringMesh(ShipItem item)
        {
            if (item == null)
                return;

            Transform transform = item.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name == StringName)
                    ReleaseStringMesh(child);
            }
        }

        private static void ReleaseStringMesh(Transform child)
        {
            if (child == null)
                return;

            MeshFilter filter = child.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null && filter.sharedMesh.name == StringName)
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
                Plugin.Log.LogWarning("Could not update firewood bundle LOD: " + ex.Message);
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

        private static void FitColliders(ShipItem item, List<Vector3> centers, Mesh mesh)
        {
            Bounds bundle = BundleBounds(centers, mesh);
            bundle.Expand(0.04f);
            bool twinNeedsBox = item.itemRigidbodyC != null && HasMeshCollider(item.itemRigidbodyC.gameObject);
            Mesh box = null;
            if (HasMeshCollider(item.gameObject) || twinNeedsBox)
                box = BoxMesh(item.GetInstanceID(), bundle);

            FitOn(item.gameObject, bundle, box);
            if (item.itemRigidbodyC != null)
                FitOn(item.itemRigidbodyC.gameObject, bundle, box);
        }

        private static Bounds BundleBounds(List<Vector3> centers, Mesh mesh)
        {
            Bounds stick = mesh != null ? mesh.bounds : new Bounds(Vector3.zero, new Vector3(0.08f, 0.08f, 0.4f));
            Bounds bundle = new Bounds(centers[0] + stick.center, stick.size);
            for (int i = 1; i < centers.Count; i++)
                bundle.Encapsulate(new Bounds(centers[i] + stick.center, stick.size));
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
