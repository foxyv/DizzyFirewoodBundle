using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.FirewoodBundle
{
    internal static class HookLinePieces
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        private static readonly FieldInfo LampOccupiedField = AccessTools.Field(typeof(ShipItemLampHook), "occupied");
        private static readonly FieldInfo CurrentHookField = AccessTools.Field(typeof(HangableItem), "currentHook");
        private static readonly HashSet<int> ClaimedLamps = new HashSet<int>();
        private static float _pickedUpAt = -10f;

        // The game's own fishing hook, and Better Fishing's three lures, which carry the
        // game's hook component. Lures string onto lines only while that mod is loaded.
        // Any other mod's hook-like item is left alone.
        internal const int HookPrefab = 99;
        internal const string BetterFishingGuid = "com.raddude.betterfishing";
        private const int FirstLurePrefab = 803;
        private const int LastLurePrefab = 805;

        internal static bool BetterFishingLoaded
        {
            get { return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(BetterFishingGuid); }
        }

        // Which hook or lure this is, by prefab number, or -1 when it is neither. A line
        // only ever holds one kind: a hook strung onto a line of lures would come back
        // off it as another lure.
        internal static int KindOf(Component component)
        {
            ShipItem ship = FirewoodPieces.AsShip(component);
            if (!(ship is ShipItemFishingHook))
                return -1;
            SaveablePrefab saveable = ship.GetComponent<SaveablePrefab>();
            int prefab = saveable != null ? saveable.prefabIndex : -1;
            if (prefab == HookPrefab)
                return prefab;
            return prefab >= FirstLurePrefab && prefab <= LastLurePrefab && BetterFishingLoaded ? prefab : -1;
        }

        internal static bool IsHook(Component component)
        {
            return KindOf(component) >= 0;
        }

        internal static bool IsLure(Component component)
        {
            return KindOf(component) >= FirstLurePrefab;
        }

        // "Hook" or "Lure", for the prompts.
        internal static string Noun(Component component)
        {
            return IsLure(component) ? "Lure" : "Hook";
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

        internal static string LookText(ShipItem item, int count)
        {
            if (!IsLure(item))
            {
                string hooks = count == 1 ? "1 hook" : count + " hooks";
                return "hook line\n" + hooks;
            }

            // A line of lures is labelled with the lure's own name: "3 topwater lures".
            ShipItem prefab = FirewoodPieces.PrefabOf(item);
            string lure = prefab != null && !string.IsNullOrEmpty(prefab.name) ? prefab.name : "lure";
            return "lure line\n" + count + " " + lure + (count == 1 ? "" : "s");
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
            if (KindOf(held) != KindOf(target))
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

        internal static ShipItemLampHook LampOf(Component item)
        {
            if (item == null)
                return null;
            ShipItemLampHook lamp = item.GetComponent<ShipItemLampHook>();
            if (lamp != null)
                return lamp;
            return item.GetComponentInParent<ShipItemLampHook>();
        }

        internal static bool LampIsOccupied(ShipItemLampHook lamp)
        {
            if (lamp == null || LampOccupiedField == null)
                return false;
            object value = LampOccupiedField.GetValue(lamp);
            return value is bool occupied && occupied;
        }

        internal static void ClaimLamp(ShipItemLampHook lamp)
        {
            if (lamp == null || LampOccupiedField == null)
                return;
            LampOccupiedField.SetValue(lamp, true);
            lamp.unclickable = true;
            if (ClaimedLamps.Add(lamp.GetInstanceID()))
            {
                lamp.lookText = "";
                lamp.ForceUnlook();
            }
        }

        internal static void FreeLamp(ShipItemLampHook lamp)
        {
            if (lamp == null || LampOccupiedField == null)
                return;
            if (!ClaimedLamps.Remove(lamp.GetInstanceID()))
                return;
            LampOccupiedField.SetValue(lamp, false);
            lamp.unclickable = false;
            lamp.UpdateLookText();
        }

        internal static Collider CurrentHook(HangableItem hang)
        {
            if (hang == null || CurrentHookField == null)
                return null;
            return CurrentHookField.GetValue(hang) as Collider;
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

            int heldCount = CountOf(held);
            int targetCount = CountOf(target);
            bool targetLine = targetCount > 1 || IsLine(target);
            if (targetLine)
            {
                int room = FirewoodBundleConfig.HookLimit - targetCount;
                if (room <= 0)
                {
                    Plugin.Log.LogInfo("Hook line already holds " + FirewoodBundleConfig.HookLimit + " hooks.");
                    return false;
                }

                int move = heldCount < room ? heldCount : room;
                if (move >= heldCount)
                    return AddHeldToLine(held, target, pointer, targetCount + move);
                return FillLine(held, target, move);
            }

            int count = heldCount + targetCount;
            if (count < 2 || !Fits(count))
            {
                if (count >= 2)
                    Plugin.Log.LogInfo("Hook line already holds " + FirewoodBundleConfig.HookLimit + " hooks.");
                return false;
            }

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

        internal static bool CanBaitRod(ShipItem held, Component looked)
        {
            if (!FirewoodBundleConfig.HooksAreEnabled)
                return false;
            ShipItemFishingRod heldRod = RodOf(held);
            ShipItemFishingRod lookedRod = RodOf(looked);
            if (heldRod != null && lookedRod == null)
                return heldRod.health <= 0f && IsLine(FirewoodPieces.AsShip(looked));
            if (lookedRod != null && heldRod == null)
                return lookedRod.health <= 0f && IsLine(held);
            return false;
        }

        internal static bool TryBaitRod(ShipItem held)
        {
            if (!IsLine(held) || held == null || held.held == null)
                return false;
            ShipItemFishingRod rod = RodOf(held.held.GetPointedAtItem());
            if (rod == null || rod.health > 0f)
                return false;
            return GiveOneToRod(held, rod);
        }

        internal static bool GiveOneToRod(ShipItem line, ShipItemFishingRod rod)
        {
            if (!FirewoodBundleConfig.HooksAreEnabled || !IsLine(line) || rod == null || rod.health > 0f)
                return false;
            if (line.unclickable || !line.sold)
                return false;

            int count = CountOf(line);
            if (count < 1)
                return false;

            // Better Fishing replaces how a rod takes a hook: it shows the hook or lure it
            // was handed and remembers which one. So with that mod the rod is handed a real
            // one off the line and Better Fishing does the rest. Without it, the rod is
            // simply marked as hooked.
            if (BetterFishingLoaded)
            {
                ShipItem piece = SpawnSingle(line, rod.transform.position, rod.transform.rotation);
                if (piece == null)
                    return false;
                rod.OnItemClick(piece);
                if (rod.health <= 0f)
                {
                    Object.Destroy(piece.gameObject);
                    return false;
                }
            }
            else
            {
                rod.health = 1f;
                MethodInfo update = AccessTools.Method(typeof(ShipItemFishingRod), "UpdateHook");
                if (update != null)
                    update.Invoke(rod, null);
            }

            if (count <= 1)
            {
                GoPointer pointer = line.held;
                if (pointer != null)
                {
                    FirewoodPieces.Silence(line);
                    pointer.DropItem();
                }
                HookLineBuilder.Discard(line);
                FirewoodPieces.Consume(line);
                Plugin.Log.LogInfo("Put the last fishing hook on the rod.");
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

            Plugin.Log.LogInfo("Put 1 fishing hook on the rod. " + left + " left on the line.");
            return true;
        }

        // The closest hook line on the ray. A wall, deck or crate lid in front of it wins.
        internal static ShipItem LineInFront(ShipItem held, Ray ray, out float distance)
        {
            int count = Physics.RaycastNonAlloc(
                ray,
                Hits,
                FirewoodPieces.Reach,
                FirewoodPieces.LayerMask,
                QueryTriggerInteraction.Collide);
            ShipItem line = null;
            float nearest = float.MaxValue;
            float blockedAt = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float hitDistance = Hits[i].distance;
                Collider collider = Hits[i].collider;
                if (IsHeldCollider(held, collider))
                    continue;
                ShipItem ship = Resolve(collider);
                if (ship != null && ship == held)
                    continue;
                if (IsLine(ship))
                {
                    if (!ship.unclickable && hitDistance < nearest)
                    {
                        nearest = hitDistance;
                        line = ship;
                    }
                    continue;
                }

                if (collider != null && !collider.isTrigger && hitDistance < blockedAt)
                    blockedAt = hitDistance;
            }

            if (line == null || nearest > blockedAt + 0.05f)
            {
                distance = 0f;
                return null;
            }

            distance = nearest;
            return line;
        }

        private static ShipItemFishingRod RodOf(Component item)
        {
            if (item == null)
                return null;
            ShipItemFishingRod rod = item as ShipItemFishingRod;
            if (rod != null)
                return rod;
            rod = item.GetComponent<ShipItemFishingRod>();
            if (rod != null)
                return rod;
            return item.GetComponentInParent<ShipItemFishingRod>();
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

        private static bool FillLine(ShipItem held, ShipItem line, int move)
        {
            int lineCount = CountOf(line) + move;
            int heldLeft = CountOf(held) - move;
            float previousLine = line.amount;
            float previousHeld = held.amount;
            WriteCount(line, lineCount);
            try
            {
                HookLineBuilder.Apply(line);
                if (heldLeft <= 1)
                {
                    WriteCount(held, 1);
                    HookLineBuilder.RestoreSingle(held);
                }
                else
                {
                    WriteCount(held, heldLeft);
                    HookLineBuilder.Apply(held);
                }
            }
            catch (System.Exception ex)
            {
                line.amount = previousLine;
                held.amount = previousHeld;
                Plugin.Log.LogError("Could not fill a hook line: " + ex);
                return false;
            }

            Plugin.Log.LogInfo("Moved " + move + " fishing hooks onto the line. It now holds " + lineCount + ".");
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

        internal static void ReleaseFromCrate(ShipItem item)
        {
            if (item == null)
                return;
            SaveablePrefab save = item.GetComponent<SaveablePrefab>();
            CrateSlots.ClearCrateSlot(item);
            if (save == null || save.currentCrateId == 0)
                return;

            CrateInventory inventory = CrateSlots.CrateWithId(save.currentCrateId);
            if (inventory != null)
                inventory.WithdrawItem(item);
            CrateSlots.ClearCrateSlot(item);
            if (CrateInventoryUI.instance != null
                && CrateInventoryUI.instance.showingUI
                && CrateInventoryUI.instance.currentCrate == inventory)
            {
                CrateInventoryUI.instance.RefreshButtons();
            }
        }

        internal static void GatherLookedAtHook()
        {
            if (!FirewoodBundleConfig.HooksAreEnabled)
                return;
            if (GameState.inCursorMenu || GameState.sleeping || BoatCamera.on)
                return;
            if (!Input.GetKeyDown(KeyCode.G))
                return;

            ShipItem hook = LookedAtHook();
            if (!CanGather(hook))
                return;

            Gather(OpenCrateHolding(hook), KindOf(hook));
        }

        internal static bool CanGather(ShipItem hook)
        {
            if (!IsSingleton(hook))
                return false;
            ShipItemCrate crate = OpenCrateHolding(hook);
            return crate != null && LooseHooks(crate, KindOf(hook)) >= 2;
        }

        private static ShipItem LookedAtHook()
        {
            GoPointer[] pointers = Object.FindObjectsOfType<GoPointer>();
            for (int i = 0; i < pointers.Length; i++)
            {
                GoPointer pointer = pointers[i];
                if (pointer == null)
                    continue;

                ShipItem item = CrateSlots.ItemOnSlot(CrateSlots.PointedButton(pointer));
                if (item == null)
                    item = pointer.GetPointedAtItem();
                if (IsSingleton(item))
                    return item;
            }

            return null;
        }

        private static ShipItemCrate OpenCrateHolding(ShipItem hook)
        {
            CrateInventory inventory = CrateSlots.OpenCrateHolding(hook);
            return inventory != null ? inventory.GetComponent<ShipItemCrate>() : null;
        }

        private static int LooseHooks(ShipItemCrate crate, int kind)
        {
            CrateInventory inventory = crate.GetComponent<CrateInventory>();
            if (inventory == null || inventory.containedItems == null)
                return 0;

            int count = 0;
            for (int i = 0; i < inventory.containedItems.Count; i++)
            {
                if (IsSingleton(inventory.containedItems[i]) && KindOf(inventory.containedItems[i]) == kind)
                    count++;
            }

            return count;
        }

        private static bool IsSingleton(ShipItem item)
        {
            return IsHook(item) && !IsLine(item);
        }

        private static void Gather(ShipItemCrate crate, int kind)
        {
            CrateInventory inventory = crate.GetComponent<CrateInventory>();
            if (inventory == null || inventory.containedItems == null)
                return;

            var singles = new List<ShipItem>();
            for (int i = 0; i < inventory.containedItems.Count; i++)
            {
                ShipItem item = inventory.containedItems[i];
                if (IsSingleton(item) && KindOf(item) == kind)
                    singles.Add(item);
            }

            if (singles.Count < 2)
                return;

            int bundled = 0;
            while (singles.Count >= 2)
            {
                int size = Mathf.Min(FirewoodBundleConfig.HookLimit, singles.Count);
                if (size < 2)
                    break;

                ShipItem host = singles[singles.Count - 1];
                singles.RemoveAt(singles.Count - 1);
                for (int extra = 1; extra < size; extra++)
                {
                    ShipItem loose = singles[singles.Count - 1];
                    singles.RemoveAt(singles.Count - 1);
                    inventory.WithdrawItem(loose);
                    FirewoodPieces.Consume(loose);
                }

                WriteCount(host, size);
                HookLineBuilder.Apply(host);
                bundled += size;
            }

            CrateSlots.Refresh(inventory);
            Plugin.Log.LogInfo("Bundled " + bundled + " loose fishing hooks.");
        }
    }
}
