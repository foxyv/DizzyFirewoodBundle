using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.FirewoodBundle
{
    // Loose wood poles from DogEggz Tent and Plank. Their shop bundle (prefab 644)
    // stays their item. A Dizzy bundle is still prefab 643, with the count in amount
    // and the length in health.
    internal static class TentPoles
    {
        internal const string PluginGuid = "DogEggz.TentandPlank";
        internal const int PoleIndex = 643;
        internal const string HeldLookName = "wood pole held look";

        private static MethodInfo _setLength;
        private static FieldInfo _heldLookShown;

        internal static bool Loaded
        {
            get { return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(PluginGuid); }
        }

        internal static bool IsPole(ShipItem item)
        {
            if (!Loaded || item == null)
                return false;
            SaveablePrefab saveable = item.GetComponent<SaveablePrefab>();
            return saveable != null && saveable.prefabIndex == PoleIndex;
        }

        internal static void Patch(Harmony harmony)
        {
            if (harmony == null || !Loaded)
                return;

            System.Reflection.MethodInfo isPole = AccessTools.Method(AccessTools.TypeByName("TentandPlank.WoodPoles"), "IsPole");
            if (isPole == null)
                return;

            harmony.Patch(isPole, prefix: new HarmonyMethod(typeof(TentPoles), nameof(HideBundleFromPoleChecks)));
        }

        // Their pole controls, length keys and pickup notice all ask IsPole. A tied
        // bundle is still prefab 643, so that question has to say no until one pole is left.
        private static bool HideBundleFromPoleChecks(ShipItem item, ref bool __result)
        {
            if (FirewoodPieces.KindOf(item) != BundleKind.WoodPole || FirewoodPieces.CountOf(item) <= 1)
                return true;

            __result = false;
            return false;
        }

        // Their length fitter rebuilds the mesh, the box and the mass from health.
        internal static void ApplyLength(ShipItem pole, float metres)
        {
            if (pole == null)
                return;

            MethodInfo method = SetLength;
            if (method == null)
            {
                pole.health = BundleColliderMath.PoleLength(metres);
                pole.mass = BundleColliderMath.PoleMass(metres);
                return;
            }

            method.Invoke(null, new object[] { pole, metres });
        }

        // A loose pole is a wall attachment copied from a painting, so its physics body
        // starts stuck in the air and can keep the painting's mesh collider. A bundle is
        // a loose item: clear that stick, drop the mesh collider, and let the box fall.
        internal static void Release(ShipItem item)
        {
            if (item == null)
                return;

            item.wallAttachment = false;
            Rigidbody own = item.GetComponent<Rigidbody>();
            if (own != null)
                own.isKinematic = true;

            RemoveMeshColliders(item.gameObject);
            ItemRigidbody copy = item.itemRigidbodyC;
            if (copy == null)
                return;

            copy.attached = false;
            RemoveMeshColliders(copy.gameObject);
            Rigidbody body = copy.GetBody();
            if (body == null)
                return;

            body.useGravity = true;
            if (item.held != null)
                return;

            body.isKinematic = false;
            body.WakeUp();
        }

        private static void RemoveMeshColliders(GameObject host)
        {
            if (host == null)
                return;
            MeshCollider[] meshes = host.GetComponents<MeshCollider>();
            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] != null)
                    UnityEngine.Object.Destroy(meshes[i]);
            }
        }

        // A bundle is not one pole in the hand. Their held-look swap would show a
        // single pole over the tied ones, and turn the root mesh back on when dropped.
        internal static void SetBundled(ShipItem item, bool bundled)
        {
            if (item == null)
                return;

            Transform look = item.transform.Find(HeldLookName);
            if (look != null)
                look.gameObject.SetActive(false);

            MonoBehaviour heldLook = HeldLook(item);
            if (heldLook == null)
                return;

            if (bundled)
            {
                heldLook.enabled = false;
                return;
            }

            if (_heldLookShown != null)
                _heldLookShown.SetValue(heldLook, !item.held);
            heldLook.enabled = true;
        }

        private static MethodInfo SetLength
        {
            get
            {
                if (_setLength != null)
                    return _setLength;
                Type type = AccessTools.TypeByName("TentandPlank.WoodPoles");
                if (type == null)
                    return null;
                _setLength = AccessTools.Method(type, "SetLength");
                return _setLength;
            }
        }

        private static MonoBehaviour HeldLook(ShipItem item)
        {
            Type type = AccessTools.TypeByName("TentandPlank.PoleHeldLook");
            if (type == null)
                return null;
            if (_heldLookShown == null)
                _heldLookShown = AccessTools.Field(type, "shown");
            return item.GetComponent(type) as MonoBehaviour;
        }
    }
}
