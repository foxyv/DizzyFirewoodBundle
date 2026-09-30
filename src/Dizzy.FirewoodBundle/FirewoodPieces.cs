using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.FirewoodBundle
{
    internal static class FirewoodPieces
    {
        internal const string ItemName = "firewood";
        internal const int LayerMask = -604165;
        internal const float Reach = 1.8f;

        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        private static float _pickedUpAt = -10f;

        internal static bool IsPiece(Component component)
        {
            ShipItem ship = AsShip(component);
            return ship != null && ship.name == ItemName;
        }

        internal static ShipItem AsShip(Component component)
        {
            if (component == null)
                return null;
            ShipItem ship = component as ShipItem;
            if (ship != null)
                return ship;
            return component.GetComponent<ShipItem>();
        }

        internal static int CountOf(ShipItem item)
        {
            if (!IsPiece(item))
                return 0;
            int stored = Mathf.RoundToInt(item.amount);
            int single = SingleAmount(item);
            if (stored <= single)
                return 1;
            return stored - single + 1;
        }

        internal static void WriteCount(ShipItem item, int count)
        {
            int single = SingleAmount(item);
            item.amount = count <= 1 ? single : single + (count - 1);
        }

        internal static string LookText(int count)
        {
            return "firewood bundle\n" + count + " firewood";
        }

        internal static void NotePickedUp()
        {
            _pickedUpAt = Time.time;
        }

        internal static bool CanTarget(ShipItem held, ShipItem target)
        {
            if (!IsPiece(held) || !IsPiece(target) || held == target)
                return false;
            if (!held.sold || !target.sold || target.unclickable)
                return false;
            if (target.held != null)
                return false;

            ItemRigidbody body = target.itemRigidbodyC;
            if (body != null)
            {
                if (body.inStove)
                    return false;
                if (body.GetCurrentInventorySlot() != null)
                    return false;
                if (body.GetCurrentBox() != null)
                    return false;
            }

            return true;
        }

        internal static bool Fits(ShipItem template, int count)
        {
            return count <= FirewoodBundleConfig.PieceLimit;
        }

        internal static float CrossDiameter(List<Vector3> centers, int longAxis, float cross)
        {
            if (centers == null || centers.Count == 0)
                return cross;

            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < centers.Count; i++)
                centroid += centers[i];
            centroid /= centers.Count;

            float reach = cross * 0.5f;
            for (int i = 0; i < centers.Count; i++)
            {
                Vector3 offset = centers[i] - centroid;
                offset[longAxis] = 0f;
                reach = Mathf.Max(reach, offset.magnitude + cross * 0.5f);
            }

            return reach * 2f;
        }

        internal static Ray MakeRay(bool debugEditorPointer, Ray raycastRay)
        {
            if (debugEditorPointer && Camera.main != null)
                return Camera.main.ScreenPointToRay(Input.mousePosition);
            return new Ray(raycastRay.origin, raycastRay.direction.normalized);
        }

        // Other bundles stay off the look ray so the held one can be set down.
        // Right-click still glues when a bundle is the first thing along that ray.
        internal static ShipItem PieceInFront(ShipItem held, Ray ray)
        {
            int count = Physics.RaycastNonAlloc(
                ray,
                Hits,
                Reach,
                LayerMask,
                QueryTriggerInteraction.Collide);
            ShipItem wood = null;
            float woodDistance = float.MaxValue;
            float blockedAt = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float hitDistance = Hits[i].distance;
                ShipItem ship = Resolve(Hits[i].collider);
                if (ship == held)
                    continue;
                if (CanTarget(held, ship))
                {
                    if (hitDistance < woodDistance)
                    {
                        woodDistance = hitDistance;
                        wood = ship;
                    }
                    continue;
                }

                if (hitDistance < blockedAt)
                    blockedAt = hitDistance;
            }

            if (wood == null || woodDistance > blockedAt + 0.02f)
                return null;
            return wood;
        }

        internal static bool TryGlue(ShipItem held)
        {
            if (!FirewoodBundleConfig.IsEnabled || !IsPiece(held) || held.held == null)
                return false;

            GoPointer pointer = held.held;
            Ray ray = MakeRay(
                pointer.debugEditorPointer,
                new Ray(pointer.transform.position, pointer.transform.forward));
            ShipItem target = PieceInFront(held, ray);
            if (!CanTarget(held, target))
                return false;

            int count = CountOf(held) + CountOf(target);
            if (count < 2 || !Fits(target, count))
            {
                if (count >= 2)
                    Plugin.Log.LogInfo("Firewood bundle already holds " + FirewoodBundleConfig.PieceLimit + " firewood.");
                return false;
            }

            if (CountOf(target) > 1 || target.nailed)
                return AddHeldToPile(held, target, pointer, count);

            float previousAmount = held.amount;
            WriteCount(held, count);
            try
            {
                FirewoodBundleBuilder.Apply(held);
                target.ForceUnlook();
                target.DestroyItem();
            }
            catch (System.Exception ex)
            {
                held.amount = previousAmount;
                Plugin.Log.LogError("Could not bundle firewood: " + ex);
                return false;
            }

            Plugin.Log.LogInfo("Bundled " + count + " firewood.");
            return true;
        }

        // The pile stays where it is. The wood in hand is what gets used up.
        private static bool AddHeldToPile(ShipItem held, ShipItem pile, GoPointer pointer, int count)
        {
            float previousAmount = pile.amount;
            WriteCount(pile, count);
            try
            {
                FirewoodBundleBuilder.Apply(pile);
                pointer.DropItem();
                held.ForceUnlook();
                held.DestroyItem();
            }
            catch (System.Exception ex)
            {
                pile.amount = previousAmount;
                Plugin.Log.LogError("Could not add firewood to the pile: " + ex);
                return false;
            }

            Plugin.Log.LogInfo("Added firewood to the pile. It now holds " + count + ".");
            return true;
        }

        // A stove click on a bundle feeds one loose log and leaves the rest held.
        internal static bool TryFeedOneLog(ShipItem bundle, StoveFuelTrigger trigger)
        {
            if (!FirewoodBundleConfig.IsEnabled || trigger == null || CountOf(bundle) <= 1)
                return false;

            ShipItem log = SpawnSingle(bundle);
            if (log == null || log.itemRigidbodyC == null)
            {
                if (log != null)
                    Object.Destroy(log.gameObject);
                return false;
            }

            try
            {
                trigger.InsertFuel(log);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError("Could not feed the stove: " + ex.Message);
                Object.Destroy(log.gameObject);
                return false;
            }

            StoveFuel fuel = log.GetComponent<StoveFuel>();
            if (fuel == null || !fuel.inserted)
            {
                Object.Destroy(log.gameObject);
                return false;
            }

            SaveablePrefab saveable = log.GetComponent<SaveablePrefab>();
            SaveablePrefab bundleSave = bundle.GetComponent<SaveablePrefab>();
            if (saveable != null)
            {
                if (bundleSave != null)
                    saveable.SetParentObject(bundleSave.GetParentObject());
                saveable.RegisterToSave();
            }

            bundle.health = log.health;
            int left = CountOf(bundle) - 1;
            WriteCount(bundle, left);
            if (left <= 1)
                FirewoodBundleBuilder.RestoreSingle(bundle);
            else
                FirewoodBundleBuilder.Apply(bundle);

            Plugin.Log.LogInfo("Stove took 1 firewood. " + left + " left in the bundle.");
            return true;
        }

        // Right-click a bundle sitting in the world, nailed or not, to take one log.
        // The bundle stays where it is. A held stack only grows when the click hits more firewood.
        internal static bool TrySplit(ShipItem bundle)
        {
            if (!FirewoodBundleConfig.IsEnabled || bundle == null || CountOf(bundle) <= 1)
                return false;
            if (bundle.held != null)
                return false;
            if (!bundle.sold)
                return false;

            StoveFuel burning = bundle.GetComponent<StoveFuel>();
            if (burning != null && burning.inserted)
                return false;

            ItemRigidbody body = bundle.itemRigidbodyC;
            if (body != null)
            {
                if (body.inStove)
                    return false;
                if (body.GetCurrentInventorySlot() != null)
                    return false;
                if (body.GetCurrentBox() != null)
                    return false;
            }

            GoPointer pointer = PointerFor(bundle);
            if (pointer == null || pointer.GetHeldItem() != null)
                return false;

            ShipItem template = PrefabOf(bundle);
            float distance = template != null ? template.holdDistance : 0.4f;
            float height = template != null ? template.holdHeight : 0f;
            Vector3 position = pointer.transform.position + pointer.transform.forward * distance + pointer.transform.up * height;
            ShipItem log = SpawnSingle(bundle, position, pointer.transform.rotation);
            if (log == null || log.itemRigidbodyC == null)
            {
                if (log != null)
                    Object.Destroy(log.gameObject);
                return false;
            }

            SaveablePrefab saveable = log.GetComponent<SaveablePrefab>();
            SaveablePrefab bundleSave = bundle.GetComponent<SaveablePrefab>();
            if (saveable != null)
            {
                if (bundleSave != null)
                    saveable.SetParentObject(bundleSave.GetParentObject());
                saveable.RegisterToSave();
            }

            int left = CountOf(bundle) - 1;
            WriteCount(bundle, left);
            if (left <= 1)
                FirewoodBundleBuilder.RestoreSingle(bundle);
            else
                FirewoodBundleBuilder.Apply(bundle);

            pointer.PickUpItem(log);
            Plugin.Log.LogInfo("Took 1 firewood. " + left + " left in the bundle.");
            return true;
        }

        private static GoPointer PointerFor(ShipItem bundle)
        {
            var field = AccessTools.Field(typeof(GoPointerButton), "pointedAtBy");
            if (field != null)
            {
                GoPointer looking = field.GetValue(bundle) as GoPointer;
                if (looking != null)
                    return looking;
            }

            GoPointer[] pointers = Object.FindObjectsOfType<GoPointer>();
            for (int i = 0; i < pointers.Length; i++)
            {
                GoPointer pointer = pointers[i];
                if (pointer != null && pointer.GetHeldItem() == null)
                    return pointer;
            }

            return null;
        }

        private static ShipItem SpawnSingle(ShipItem bundle)
        {
            return SpawnSingle(bundle, bundle.transform.position, bundle.transform.rotation);
        }

        private static ShipItem SpawnSingle(ShipItem bundle, Vector3 position, Quaternion rotation)
        {
            ShipItem prefab = PrefabOf(bundle);
            if (prefab == null)
                return null;

            GameObject spawned = Object.Instantiate(prefab.gameObject, position, rotation);
            ShipItem log = spawned.GetComponent<ShipItem>();
            if (log != null)
                log.sold = true;
            return log;
        }

        // Highlighting a log blocks the game's normal "click to put down" path.
        // Left click still drops the held piece. Right click is what glues.
        internal static void ReleaseHeldPiece()
        {
            if (!FirewoodBundleConfig.IsEnabled)
                return;
            if (GameState.inCursorMenu || GameState.sleeping || BoatCamera.on)
                return;
            if (Time.time - _pickedUpAt < 0.66f)
                return;

            bool drop = GameInput.GetKeyUp(InputName.PickUp);
            bool toss = GameInput.GetKeyUp(InputName.Throw);
            if (!drop && !toss)
                return;

            GoPointer[] pointers = Object.FindObjectsOfType<GoPointer>();
            for (int i = 0; i < pointers.Length; i++)
            {
                GoPointer pointer = pointers[i];
                if (pointer == null)
                    continue;

                PickupableItem held = pointer.GetHeldItem();
                ShipItem heldShip = AsShip(held);
                ShipItem target = pointer.GetPointedAtItem();
                if (!CanTarget(heldShip, target))
                    continue;

                if (!toss
                    && held.colChecker != null
                    && held.colChecker.collisions > 0
                    && !held.colChecker.allowObstructedDropping)
                    continue;

                held.OnDrop();
                pointer.DropItem();
            }
        }

        internal static List<Vector3> Centers(int count, float pitch, int longAxis)
        {
            var centers = new List<Vector3>(Mathf.Max(count, 0));
            if (count < 1)
                return centers;

            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt(count / (float)columns);
            int placed = 0;
            float rowSpan = (rows - 1) * pitch;
            for (int row = 0; row < rows && placed < count; row++)
            {
                int inRow = Mathf.Min(columns, count - placed);
                float rowWidth = (inRow - 1) * pitch;
                float y = row * pitch - rowSpan * 0.5f;
                for (int column = 0; column < inRow; column++)
                {
                    float x = column * pitch - rowWidth * 0.5f;
                    centers.Add(OnCrossSection(x, y, longAxis));
                    placed++;
                }
            }

            return centers;
        }

        internal static bool TryMeasure(ShipItem item, out float cross, out int longAxis)
        {
            cross = 0.08f;
            longAxis = 2;
            MeshFilter filter = item != null ? item.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null)
                return false;

            Vector3 size = filter.sharedMesh.bounds.size;
            longAxis = 0;
            float longest = size.x;
            if (size.y > longest)
            {
                longest = size.y;
                longAxis = 1;
            }
            if (size.z > longest)
                longAxis = 2;

            cross = 0f;
            for (int axis = 0; axis < 3; axis++)
            {
                if (axis != longAxis)
                    cross = Mathf.Max(cross, size[axis]);
            }

            if (cross < 0.01f)
                cross = 0.08f;
            return true;
        }

        private static int SingleAmount(ShipItem item)
        {
            ShipItem prefab = PrefabOf(item);
            if (prefab == null)
                return 0;
            int amount = Mathf.RoundToInt(prefab.amount);
            return amount < 0 ? 0 : amount;
        }

        internal static ShipItem PrefabOf(ShipItem item)
        {
            if (item == null || PrefabsDirectory.instance == null || PrefabsDirectory.instance.directory == null)
                return null;

            SaveablePrefab saveable = item.GetComponent<SaveablePrefab>();
            if (saveable == null)
                return null;

            int index = saveable.prefabIndex;
            GameObject[] directory = PrefabsDirectory.instance.directory;
            if (index < 0 || index >= directory.Length || directory[index] == null)
                return null;
            return directory[index].GetComponent<ShipItem>();
        }

        private static ShipItem Resolve(Collider collider)
        {
            if (collider == null)
                return null;
            if (collider.CompareTag("ItemSubcollider") && collider.transform.parent != null)
            {
                Collider parent = collider.transform.parent.GetComponent<Collider>();
                if (parent != null)
                    collider = parent;
            }

            ShipItem ship = collider.GetComponent<ShipItem>();
            if (ship != null)
                return ship;

            ItemRigidbody body = collider.GetComponent<ItemRigidbody>();
            if (body != null)
                return body.GetShipItem();
            return collider.GetComponentInParent<ShipItem>();
        }

        private static Vector3 OnCrossSection(float x, float y, int longAxis)
        {
            int a = (longAxis + 1) % 3;
            int b = (longAxis + 2) % 3;
            Vector3 local = Vector3.zero;
            local[a] = x;
            local[b] = y;
            return local;
        }

    }
}
