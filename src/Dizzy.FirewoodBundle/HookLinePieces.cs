using System.Collections.Generic;
using UnityEngine;

namespace Dizzy.FirewoodBundle
{
    internal static class HookLinePieces
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        private static float _pickedUpAt = -10f;

        internal static bool IsHook(Component component)
        {
            ShipItem ship = FirewoodPieces.AsShip(component);
            return ship is ShipItemFishingHook;
        }

        internal static int CountOf(ShipItem item)
        {
            if (!IsHook(item))
                return 0;
            int stored = Mathf.RoundToInt(item.amount);
            int single = SingleAmount(item);
            if (stored <= single)
                return 1;
            return stored - single + 1;
        }

        internal static void WriteCount(ShipItem item, int count)
        {
            WriteCount(item, count, false);
        }

        internal static void WriteCount(ShipItem item, int count, bool keepLine)
        {
            int single = SingleAmount(item);
            if (count <= 1)
                item.amount = keepLine ? single - 1 : single;
            else
                item.amount = single + (count - 1);
        }

        // A line of one uses an amount a loose hook never has, so it can stay strung.
        internal static bool IsLine(ShipItem item)
        {
            if (!IsHook(item))
                return false;
            int stored = Mathf.RoundToInt(item.amount);
            int single = SingleAmount(item);
            return stored > single || stored == single - 1;
        }

        internal static string LookText(int count)
        {
            string hooks = count == 1 ? "1 hook" : count + " hooks";
            return "hook line\n" + hooks;
        }

        internal static bool Fits(int count)
        {
            return count <= FirewoodBundleConfig.HookLimit;
        }

        internal static void NotePickedUp()
        {
            _pickedUpAt = Time.time;
        }

        internal static bool CanTarget(ShipItem held, ShipItem target)
        {
            if (!IsHook(held) || !IsHook(target) || held == target)
                return false;
            if (!held.sold || !target.sold || target.unclickable)
                return false;
            if (target.held != null)
                return false;

            // A hung line can take more hooks. Hanging marks the body as in a stove.
            bool hanging = IsHanging(target);
            ItemRigidbody body = target.itemRigidbodyC;
            if (body != null)
            {
                if (body.inStove && !hanging)
                    return false;
                if (body.GetCurrentInventorySlot() != null)
                    return false;
                if (body.GetCurrentBox() != null)
                    return false;
            }

            return true;
        }

        internal static bool IsHanging(ShipItem item)
        {
            if (item == null)
                return false;
            HangableItem hang = item.GetComponent<HangableItem>();
            return hang != null && hang.IsHanging();
        }

        internal static ShipItem HookInFront(ShipItem held, Ray ray, out float distance)
        {
            int count = Physics.RaycastNonAlloc(
                ray,
                Hits,
                FirewoodPieces.Reach,
                FirewoodPieces.LayerMask,
                QueryTriggerInteraction.Collide);
            ShipItem hook = null;
            float hookDistance = float.MaxValue;
            float blockedAt = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float hitDistance = Hits[i].distance;
                if (IsHeldCollider(held, Hits[i].collider))
                    continue;

                ShipItem ship = Resolve(Hits[i].collider);
                if (ship == held)
                    continue;
                if (CanTarget(held, ship))
                {
                    if (hitDistance < hookDistance)
                    {
                        hookDistance = hitDistance;
                        hook = ship;
                    }
                    continue;
                }

                if (hitDistance < blockedAt)
                    blockedAt = hitDistance;
            }

            if (hook == null || hookDistance > blockedAt + 0.45f)
            {
                distance = 0f;
                return null;
            }

            distance = hookDistance;
            return hook;
        }

        internal static bool TryString(ShipItem held)
        {
            if (!FirewoodBundleConfig.HooksAreEnabled || !IsHook(held) || held.held == null)
                return false;

            GoPointer pointer = held.held;
            Ray ray = FirewoodPieces.MakeRay(
                pointer.debugEditorPointer,
                new Ray(pointer.transform.position, pointer.transform.forward));
            float distance;
            ShipItem target = HookInFront(held, ray, out distance);
            if (!CanTarget(held, target))
                return false;

            int count = CountOf(held) + CountOf(target);
            if (count < 2 || !Fits(count))
            {
                if (count >= 2)
                    Plugin.Log.LogInfo("Hook line already holds " + FirewoodBundleConfig.HookLimit + " hooks.");
                return false;
            }

            if (CountOf(target) > 1 || IsLine(target))
                return AddHeldToLine(held, target, pointer, count);

            float previousAmount = held.amount;
            WriteCount(held, count);
            try
            {
                HookLineBuilder.Apply(held);
                target.ForceUnlook();
                FirewoodPieces.Consume(target);
            }
            catch (System.Exception ex)
            {
                held.amount = previousAmount;
                Plugin.Log.LogError("Could not string fishing hooks: " + ex);
                return false;
            }

            Plugin.Log.LogInfo("Strung " + count + " fishing hooks.");
            return true;
        }

        internal static bool TrySplit(ShipItem line)
        {
            if (!FirewoodBundleConfig.HooksAreEnabled || line == null || line.held != null || !line.sold)
                return false;
            int count = CountOf(line);
            if (count <= 1 && !IsLine(line))
                return false;

            GoPointer pointer = PointerFor(line);
            if (pointer == null || pointer.GetHeldItem() != null)
                return false;

            ShipItem template = FirewoodPieces.PrefabOf(line);
            float distance = template != null ? template.holdDistance : 0.4f;
            float height = template != null ? template.holdHeight : 0f;
            Vector3 position = pointer.transform.position + pointer.transform.forward * distance + pointer.transform.up * height;
            ShipItem hook = SpawnSingle(line, position, pointer.transform.rotation);
            if (hook == null || hook.itemRigidbodyC == null)
            {
                if (hook != null)
                    Object.Destroy(hook.gameObject);
                return false;
            }

            SaveablePrefab saveable = hook.GetComponent<SaveablePrefab>();
            SaveablePrefab lineSave = line.GetComponent<SaveablePrefab>();
            if (saveable != null)
            {
                if (lineSave != null)
                    saveable.SetParentObject(lineSave.GetParentObject());
                saveable.RegisterToSave();
            }

            if (count <= 1)
            {
                HookLineBuilder.Discard(line);
                FirewoodPieces.Consume(line);
                pointer.PickUpItem(hook);
                Plugin.Log.LogInfo("Took the last fishing hook.");
                return true;
            }

            int left = count - 1;
            if (left <= 1 && IsHanging(line))
            {
                WriteCount(line, 1, true);
                HookLineBuilder.Apply(line);
            }
            else if (left <= 1)
            {
                WriteCount(line, 1);
                HookLineBuilder.RestoreSingle(line);
            }
            else
            {
                WriteCount(line, left);
                HookLineBuilder.Apply(line);
            }

            pointer.PickUpItem(hook);
            Plugin.Log.LogInfo("Took 1 fishing hook. " + left + " left on the line.");
            return true;
        }

        internal static void ReleaseHeldHook()
        {
            if (!FirewoodBundleConfig.HooksAreEnabled)
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
                ShipItem heldShip = FirewoodPieces.AsShip(held);
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

        private static bool AddHeldToLine(ShipItem held, ShipItem line, GoPointer pointer, int count)
        {
            float previousAmount = line.amount;
            WriteCount(line, count);
            try
            {
                HookLineBuilder.Apply(line);
                FirewoodPieces.Silence(held);
                pointer.DropItem();
                FirewoodPieces.Consume(held);
            }
            catch (System.Exception ex)
            {
                line.amount = previousAmount;
                Plugin.Log.LogError("Could not add a hook to the line: " + ex);
                return false;
            }

            Plugin.Log.LogInfo("Added a fishing hook to the line. It now holds " + count + ".");
            return true;
        }

        private static ShipItem SpawnSingle(ShipItem line, Vector3 position, Quaternion rotation)
        {
            ShipItem prefab = FirewoodPieces.PrefabOf(line);
            if (prefab == null)
                return null;

            GameObject spawned = Object.Instantiate(prefab.gameObject, position, rotation);
            ShipItem hook = spawned.GetComponent<ShipItem>();
            if (hook != null)
                hook.sold = true;
            return hook;
        }

        private static GoPointer PointerFor(ShipItem line)
        {
            var field = HarmonyLib.AccessTools.Field(typeof(GoPointerButton), "pointedAtBy");
            if (field != null)
            {
                GoPointer looking = field.GetValue(line) as GoPointer;
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

        private static int SingleAmount(ShipItem item)
        {
            ShipItem prefab = FirewoodPieces.PrefabOf(item);
            if (prefab == null)
                return 0;
            int amount = Mathf.RoundToInt(prefab.amount);
            return amount < 0 ? 0 : amount;
        }

        private static bool IsHeldCollider(ShipItem held, Collider collider)
        {
            if (held == null || collider == null)
                return false;
            if (collider.transform == held.transform || collider.transform.IsChildOf(held.transform))
                return true;

            ItemRigidbody body = held.itemRigidbodyC;
            if (body == null)
                return false;
            return collider.transform == body.transform || collider.transform.IsChildOf(body.transform);
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

        internal static void GatherLookedAtCrate()
        {
            if (!FirewoodBundleConfig.HooksAreEnabled)
                return;
            if (GameState.inCursorMenu || GameState.sleeping || BoatCamera.on)
                return;
            if (!Input.GetKeyDown(KeyCode.G))
                return;

            ShipItemCrate crate = LookedAtCrate();
            if (crate == null)
                crate = OpenCrate();
            if (crate == null || !CanGather(crate))
                return;

            Gather(crate);
        }

        internal static bool CanGather(ShipItemCrate crate)
        {
            return crate != null && LooseHooks(crate) >= 2;
        }

        private static ShipItemCrate LookedAtCrate()
        {
            GoPointer[] pointers = Object.FindObjectsOfType<GoPointer>();
            for (int i = 0; i < pointers.Length; i++)
            {
                GoPointer pointer = pointers[i];
                if (pointer == null)
                    continue;
                ShipItemCrate crate = pointer.GetPointedAtItem() as ShipItemCrate;
                if (crate != null)
                    return crate;
            }

            return null;
        }

        private static ShipItemCrate OpenCrate()
        {
            if (CrateInventoryUI.instance == null || !CrateInventoryUI.instance.showingUI)
                return null;
            CrateInventory inventory = CrateInventoryUI.instance.currentCrate;
            if (inventory == null)
                return null;
            return inventory.GetComponent<ShipItemCrate>();
        }

        private static int LooseHooks(ShipItemCrate crate)
        {
            int count = SealedHooks(crate);
            CrateInventory inventory = crate.GetComponent<CrateInventory>();
            if (inventory == null || inventory.containedItems == null)
                return count;

            for (int i = 0; i < inventory.containedItems.Count; i++)
            {
                ShipItem item = inventory.containedItems[i];
                if (IsSingleton(item))
                    count++;
            }

            return count;
        }

        private static int SealedHooks(ShipItemCrate crate)
        {
            if (crate.amount < 2f || IsMission(crate) || !IsHookPrefab(crate.GetContainedPrefab()))
                return 0;
            return Mathf.RoundToInt(crate.amount);
        }

        private static bool IsMission(ShipItemCrate crate)
        {
            Good good = crate.GetComponent<Good>();
            return good != null && good.GetMissionIndex() != -1;
        }

        private static bool IsHookPrefab(GameObject prefab)
        {
            if (prefab == null)
                return false;
            return prefab.GetComponent<ShipItemFishingHook>() != null;
        }

        private static bool IsSingleton(ShipItem item)
        {
            return IsHook(item) && !IsLine(item);
        }

        private static void Gather(ShipItemCrate crate)
        {
            CrateInventory inventory = crate.GetComponent<CrateInventory>();
            if (inventory == null)
                return;
            if (inventory.containedItems == null)
                inventory.containedItems = new List<ShipItem>();

            int pending = SealedHooks(crate);
            var singles = new List<ShipItem>();
            for (int i = 0; i < inventory.containedItems.Count; i++)
            {
                ShipItem item = inventory.containedItems[i];
                if (IsSingleton(item))
                    singles.Add(item);
            }

            if (pending + singles.Count < 2)
                return;

            if (pending > 0)
            {
                crate.amount = 0f;
                crate.UpdateLookText();
                if (crate.itemRigidbodyC != null)
                    crate.itemRigidbodyC.UpdateMass();
                if (UISoundPlayer.instance != null)
                    UISoundPlayer.instance.PlayUISound(UISounds.crateSealBreak, 1f, 1f);
            }

            int bundled = 0;
            while (pending + singles.Count >= 2)
            {
                int size = Mathf.Min(FirewoodBundleConfig.HookLimit, pending + singles.Count);
                if (size < 2)
                    break;

                ShipItem host = TakeHost(crate, inventory, ref pending, singles);
                for (int extra = 1; extra < size; extra++)
                    DiscardLoose(inventory, ref pending, singles);

                WriteCount(host, size);
                HookLineBuilder.Apply(host);
                bundled += size;
            }

            if (pending > 0)
                TakeHost(crate, inventory, ref pending, singles);

            if (CrateInventoryUI.instance != null
                && CrateInventoryUI.instance.showingUI
                && CrateInventoryUI.instance.currentCrate == inventory)
            {
                CrateInventoryUI.instance.RefreshButtons();
            }
            else if (bundled > 0)
            {
                inventory.OpenCrate();
            }

            Plugin.Log.LogInfo("Bundled " + bundled + " loose fishing hooks.");
        }

        private static ShipItem TakeHost(ShipItemCrate crate, CrateInventory inventory, ref int pending, List<ShipItem> singles)
        {
            if (singles.Count > 0)
            {
                ShipItem existing = singles[singles.Count - 1];
                singles.RemoveAt(singles.Count - 1);
                return existing;
            }

            pending--;
            ShipItem spawned = SpawnFromCrate(crate);
            inventory.InsertItem(spawned);
            return spawned;
        }

        private static void DiscardLoose(CrateInventory inventory, ref int pending, List<ShipItem> singles)
        {
            if (singles.Count > 0)
            {
                ShipItem extra = singles[singles.Count - 1];
                singles.RemoveAt(singles.Count - 1);
                inventory.WithdrawItem(extra);
                FirewoodPieces.Consume(extra);
                return;
            }

            pending--;
        }

        private static ShipItem SpawnFromCrate(ShipItemCrate crate)
        {
            GameObject prefab = crate.GetContainedPrefab();
            GameObject spawned = Object.Instantiate(prefab, crate.transform.position, crate.transform.rotation);
            ShipItem hook = spawned.GetComponent<ShipItem>();
            hook.sold = true;
            SaveablePrefab saveable = hook.GetComponent<SaveablePrefab>();
            SaveablePrefab crateSave = crate.GetComponent<SaveablePrefab>();
            if (saveable != null)
            {
                if (crateSave != null)
                    saveable.SetParentObject(crateSave.GetParentObject());
                saveable.RegisterToSave();
            }

            return hook;
        }
    }
}
