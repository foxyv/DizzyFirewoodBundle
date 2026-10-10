using System.Collections.Generic;
using System.Reflection;
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
        private static readonly FieldInfo LanternInitialHealthField = AccessTools.Field(typeof(ShipItemLight), "initialHealth");

        private static float _pickedUpAt = -10f;

        // Firewood, a candle or a sausage: anything that makes a two-handed bundle.
        internal static bool IsPiece(Component component)
        {
            return KindOf(component) != null;
        }

        internal static BundleKind KindOf(Component component)
        {
            return BundleKind.Of(AsShip(component));
        }

        internal static bool IsFirewood(Component component)
        {
            return KindOf(component) == BundleKind.Firewood;
        }

        // A piece whose kind is turned on in the config.
        internal static bool IsActive(Component component)
        {
            BundleKind kind = KindOf(component);
            return kind != null && kind.IsEnabled;
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

        // A bundle saves its count and tie color in the item's amount, which the game
        // already saves: amount = single + (count - 1) + color * ColorStride.
        internal const int ColorStride = 1000;
        // Both saved encodings keep the count below 1000, so no bundle may reach it.
        internal const int MaxCount = ColorStride - 1;

        internal static int CountOf(ShipItem item)
        {
            BundleKind kind = KindOf(item);
            if (kind == null)
                return 0;
            if (!kind.CountInAmount)
                return SausageStacks.CountOf(item);
            int extra = Mathf.RoundToInt(item.amount) - SingleAmount(item);
            if (extra <= 0)
                return 1;
            return extra % ColorStride + 1;
        }

        internal static int ColorOf(ShipItem item)
        {
            BundleKind kind = KindOf(item);
            if (kind == null || kind.ColorCount == 0)
                return 0;
            if (!kind.CountInAmount)
                return Mathf.Clamp(SausageStacks.ColorOf(item), 0, kind.ColorCount - 1);
            int extra = Mathf.RoundToInt(item.amount) - SingleAmount(item);
            if (extra <= 0)
                return 0;
            int color = extra / ColorStride;
            return color < kind.ColorCount ? color : 0;
        }

        // Keeps the bundle's tie color. A single piece has no tie, so it has no color.
        internal static void WriteCount(ShipItem item, int count)
        {
            WriteCount(item, count, ColorOf(item));
        }

        private static void WriteCount(ShipItem item, int count, int color)
        {
            BundleKind kind = KindOf(item);
            if (kind != null && !kind.CountInAmount)
            {
                SausageStacks.WriteCount(item, count);
                SausageStacks.WriteColor(item, color);
                return;
            }

            int single = SingleAmount(item);
            item.amount = count <= 1 ? single : single + (count - 1) + color * ColorStride;
        }

        // \ while holding or looking at a bundle ties it with the next color.
        internal static void CycleLookedAtColor()
        {
            if (!BundleKind.AnyEnabled)
                return;
            if (GameState.inCursorMenu || GameState.sleeping || BoatCamera.on)
                return;
            if (!Input.GetKeyDown(FirewoodBundleConfig.ColorKeyCode))
                return;

            GoPointer[] pointers = Object.FindObjectsOfType<GoPointer>();
            for (int i = 0; i < pointers.Length; i++)
            {
                GoPointer pointer = pointers[i];
                if (pointer == null)
                    continue;
                ShipItem bundle = AsShip(pointer.GetHeldItem());
                if (bundle == null)
                    bundle = pointer.GetPointedAtItem();
                if (!IsActive(bundle) || KindOf(bundle).ColorCount == 0)
                    continue;
                if (CountOf(bundle) <= 1 && !KindOf(bundle).Hangs)
                    continue;

                BundleKind kind = KindOf(bundle);
                int color = (ColorOf(bundle) + 1) % kind.ColorCount;
                WriteCount(bundle, CountOf(bundle), color);
                FirewoodBundleBuilder.Recolor(bundle);
                Plugin.Log.LogInfo(kind.BundleName + " " + kind.TieName.ToLowerInvariant() + " is now " + kind.ColorNames[color].ToLowerInvariant() + ".");
                return;
            }
        }

        internal static string ColorPrompt(ShipItem bundle)
        {
            BundleKind kind = KindOf(bundle);
            if (kind == null)
                return "";
            if (kind.ColorCount == 0)
                return kind == BundleKind.Sausage ? SausageStacks.WidthPrompt(bundle) : "";
            return FirewoodBundleConfig.ColorKeyLabel + " " + kind.TieName + ": " + kind.ColorNames[ColorOf(bundle)];
        }

        internal static string LookText(ShipItem item, int count)
        {
            BundleKind kind = KindOf(item);
            if (kind != null && kind.IsFood)
                return SausageStacks.Label(item, count);
            return kind != null ? kind.LookText(count) : "";
        }

        internal static void NotePickedUp()
        {
            _pickedUpAt = Time.time;
        }

        // A matching piece that can be bundled with the held one.
        internal static bool CanTarget(ShipItem held, ShipItem target)
        {
            return CanTargetKind(held, target) && SausageStacks.Rejection(held, target) == null;
        }

        // Why the held piece cannot join this matching piece, or null.
        internal static string Refusal(ShipItem held, ShipItem target)
        {
            return CanTargetKind(held, target) ? SausageStacks.Rejection(held, target) : null;
        }

        // Same kind, both sold, and the target is out in the world. A sausage can still
        // be refused for its state, so it highlights and says why.
        internal static bool CanTargetKind(ShipItem held, ShipItem target)
        {
            BundleKind kind = KindOf(held);
            BundleKind targetKind = KindOf(target);
            // Sausages or bananas in hand also go onto a hanging bundle of their own kind.
            bool onHook = targetKind != null && kind != null && targetKind.Hangs && targetKind.Loose == kind.Loose;
            // A bunch in hand picks up loose bananas of its own.
            bool intoHand = targetKind != null && kind != null && kind.Hangs && targetKind == kind.Loose && targetKind.AlwaysHung;
            if (kind == null || !kind.IsEnabled || held == target)
                return false;
            if (targetKind != kind && !onHook && !intoHand)
                return false;
            if (!onHook && !kind.Bundles)
                return false;
            if (onHook && !targetKind.IsEnabled)
                return false;
            if (!held.sold || !target.sold || target.unclickable)
                return false;
            if (target.held != null)
                return false;

            ItemRigidbody body = target.itemRigidbodyC;
            if (body != null)
            {
                // Hanging marks the body as held in place, the same flag a stove uses.
                if (body.inStove && !targetKind.Hangs)
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
            BundleKind kind = KindOf(template);
            return kind != null && count <= kind.Limit;
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

        // The piece under the crosshair, ignoring the log already in hand.
        // A log sitting on the deck can be a little behind the floor hit.
        internal static ShipItem PieceInFront(ShipItem held, Ray ray, out float distance)
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
                if (IsHeldCollider(held, Hits[i].collider))
                    continue;

                ShipItem ship = Resolve(Hits[i].collider);
                if (ship == held)
                    continue;
                if (CanTargetKind(held, ship))
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

            if (wood == null || woodDistance > blockedAt + 0.45f)
            {
                distance = 0f;
                return null;
            }

            distance = woodDistance;
            return wood;
        }

        internal static bool IsHeldCollider(ShipItem held, Collider collider)
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

        internal static bool TryGlue(ShipItem held)
        {
            if (!IsActive(held) || held.held == null)
                return false;

            GoPointer pointer = held.held;
            Ray ray = MakeRay(
                pointer.debugEditorPointer,
                new Ray(pointer.transform.position, pointer.transform.forward));
            float distance;
            ShipItem target = PieceInFront(held, ray, out distance);
            if (!CanTargetKind(held, target))
                return false;

            // The click is used up by the refusal, so it does not fall through to eating.
            string refusal = SausageStacks.Rejection(held, target);
            if (refusal != null)
            {
                SausageStacks.Notify(refusal);
                return true;
            }

            int count = CountOf(held) + CountOf(target);
            if (count < 2)
                return false;
            if (!Fits(target, count))
            {
                // A held bundle tops up a pile that has room and keeps the rest.
                int room = RoomIn(held, target);
                if (room > 0)
                    return FillPile(held, target, room);
                Plugin.Log.LogInfo("A " + KindOf(target).BundleName + " already holds " + KindOf(target).Limit + " " + KindOf(target).Plural + ".");
                return false;
            }

            // A hanging bundle stays on its hook, even with one sausage, and takes the held ones.
            if (CountOf(target) > 1 || target.nailed || KindOf(target).Hangs)
                return AddHeldToPile(held, target, pointer, count);

            int previousCount = CountOf(held);
            SausageStacks.MergeFood(held, target, previousCount, CountOf(target));
            // Two loose apples make a bag, not a stack.
            if (KindOf(held).AlwaysHung)
                SausageStacks.MakeHanging(held);
            WriteCount(held, count);
            try
            {
                FirewoodBundleBuilder.Apply(held);
                target.ForceUnlook();
                Consume(target);
            }
            catch (System.Exception ex)
            {
                WriteCount(held, previousCount);
                Plugin.Log.LogError("Could not tie a " + KindOf(held).BundleName + ": " + ex);
                return false;
            }

            Plugin.Log.LogInfo("Bundled " + count + " " + KindOf(held).Plural + ".");
            return true;
        }

        // ExitBoat unparents the log's physics body. On a heeling, moving ship that body
        // is left in the hull's path and the boat plays an impact, the same bang as cutting a fish.
        internal static void Silence(ShipItem item)
        {
            if (item == null)
                return;

            ItemRigidbody body = item.itemRigidbodyC;
            if (body == null)
                return;

            Rigidbody joint = body.GetBody();
            if (joint != null)
            {
                if (!joint.isKinematic)
                {
                    joint.velocity = Vector3.zero;
                    joint.angularVelocity = Vector3.zero;
                }
                joint.detectCollisions = false;
                joint.isKinematic = true;
            }

            Collider[] cols = body.GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                    cols[i].enabled = false;
            }
        }

        internal static void Consume(ShipItem item)
        {
            if (item == null)
                return;
            Silence(item);
            item.ForceUnlook();
            item.DestroyItem();
        }

        // The pile stays where it is. The wood in hand is what gets used up.
        private static bool AddHeldToPile(ShipItem held, ShipItem pile, GoPointer pointer, int count)
        {
            int previousCount = CountOf(pile);
            SausageStacks.MergeFood(pile, held, previousCount, CountOf(held));
            WriteCount(pile, count);
            try
            {
                FirewoodBundleBuilder.Apply(pile);
                Silence(held);
                pointer.DropItem();
                Consume(held);
            }
            catch (System.Exception ex)
            {
                WriteCount(pile, previousCount);
                Plugin.Log.LogError("Could not add to the " + KindOf(pile).BundleName + ": " + ex);
                return false;
            }

            Plugin.Log.LogInfo("Added to the " + KindOf(pile).BundleName + ". It now holds " + count + ".");
            return true;
        }

        // How many pieces a held bundle can move onto a pile that cannot take all of it.
        // 0 when the pile is full, is a single piece, or the held bundle would fit whole.
        internal static int RoomIn(ShipItem held, ShipItem pile)
        {
            BundleKind kind = KindOf(pile);
            int heldCount = CountOf(held);
            int pileCount = CountOf(pile);
            if (kind == null)
                return 0;
            bool keeps = pileCount > 1 || pile.nailed || kind.Hangs;
            if (heldCount < 1 || !keeps)
                return 0;
            if (heldCount <= 1 && !kind.Hangs)
                return 0;
            if (heldCount + pileCount <= kind.Limit)
                return 0;
            return Mathf.Max(0, kind.Limit - pileCount);
        }

        // Moves pieces from the held bundle onto the pile until the pile is full.
        private static bool FillPile(ShipItem held, ShipItem pile, int move)
        {
            int heldCount = CountOf(held);
            int pileCount = CountOf(pile);
            int pileTotal = pileCount + move;
            int heldLeft = heldCount - move;
            SausageStacks.MergeFood(pile, held, pileCount, move);
            WriteCount(pile, pileTotal);
            try
            {
                FirewoodBundleBuilder.Apply(pile);
                if (heldLeft <= 1)
                {
                    WriteCount(held, 1);
                    FirewoodBundleBuilder.RestoreSingle(held);
                }
                else
                {
                    WriteCount(held, heldLeft);
                    FirewoodBundleBuilder.Apply(held);
                }
            }
            catch (System.Exception ex)
            {
                WriteCount(pile, pileCount);
                WriteCount(held, heldCount);
                Plugin.Log.LogError("Could not fill the " + KindOf(pile).BundleName + ": " + ex);
                return false;
            }

            Plugin.Log.LogInfo("Moved " + move + " " + KindOf(pile).Plural + " onto the " + KindOf(pile).BundleName + ". It now holds " + pileTotal + ".");
            return true;
        }

        // A stove click on a bundle feeds one loose log and leaves the rest held.
        internal static bool TryFeedOneLog(ShipItem bundle, StoveFuelTrigger trigger)
        {
            if (!FirewoodBundleConfig.IsEnabled || !IsFirewood(bundle) || trigger == null || CountOf(bundle) <= 1)
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
                Plugin.Log.LogError("Could not feed the stove: " + ex);
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

        // Right-click a bundle sitting in the world, nailed or not, to take one piece.
        // The bundle stays where it is. A held stack only grows when the click hits another piece.
        internal static bool TrySplit(ShipItem bundle)
        {
            BundleKind bundleKind = KindOf(bundle);
            if (bundleKind != null && bundleKind.Hangs)
                return TakeHanging(bundle);
            if (!IsActive(bundle) || CountOf(bundle) <= 1)
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
            Plugin.Log.LogInfo("Took 1 from the " + KindOf(bundle).BundleName + ". " + left + " left.");
            return true;
        }

        // A hanging bundle gives one sausage to an empty hand. The last one comes off the
        // hook with the strings gone, a plain sausage again.
        private static bool TakeHanging(ShipItem bundle)
        {
            if (!IsActive(bundle) || bundle.held != null || !bundle.sold)
                return false;
            GoPointer pointer = PointerFor(bundle);
            if (pointer == null || pointer.GetHeldItem() != null)
                return false;

            int count = CountOf(bundle);
            if (count <= 1)
            {
                FirewoodBundleBuilder.Unhang(bundle);
                pointer.PickUpItem(bundle);
                Plugin.Log.LogInfo("Took the last sausage off the hook.");
                return true;
            }

            ShipItem template = PrefabOf(bundle);
            float distance = template != null ? template.holdDistance : 0.4f;
            float height = template != null ? template.holdHeight : 0f;
            Vector3 position = pointer.transform.position + pointer.transform.forward * distance + pointer.transform.up * height;
            ShipItem single = SpawnSingle(bundle, position, pointer.transform.rotation);
            if (single == null || single.itemRigidbodyC == null)
            {
                if (single != null)
                    Object.Destroy(single.gameObject);
                return false;
            }

            SaveablePrefab saveable = single.GetComponent<SaveablePrefab>();
            SaveablePrefab bundleSave = bundle.GetComponent<SaveablePrefab>();
            if (saveable != null)
            {
                if (bundleSave != null)
                    saveable.SetParentObject(bundleSave.GetParentObject());
                saveable.RegisterToSave();
            }

            WriteCount(bundle, count - 1);
            FirewoodBundleBuilder.Apply(bundle);
            pointer.PickUpItem(single);
            Plugin.Log.LogInfo("Took 1 sausage off the hook. " + (count - 1) + " left.");
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
            {
                log.sold = true;
                SausageStacks.CopyFood(bundle, log);
            }
            return log;
        }

        // Highlighting a log blocks the game's normal "click to put down" path.
        // Left click still drops the held piece. Right click is what glues.
        internal static void ReleaseHeldPiece()
        {
            if (!BundleKind.AnyEnabled)
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
                if (!CanTargetKind(heldShip, target))
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

        // A candle lantern click with a bundle loads one candle and keeps the rest in hand.
        // A lantern that is already full keeps its candle, so nothing is used up.
        internal static bool TryLightLantern(ShipItem bundle, ShipItemLight lantern)
        {
            if (lantern == null || lantern.usesOil || KindOf(bundle) != BundleKind.Candle || !bundle.sold)
                return false;
            if (!BundleKind.Candle.IsEnabled || CountOf(bundle) <= 1)
                return false;

            float full = LanternInitialHealthField != null ? (float)LanternInitialHealthField.GetValue(lantern) : 0f;
            if (full <= 0f || lantern.health >= full)
                return true;

            lantern.health = full;
            UISoundPlayer.instance.PlayUISound(UISounds.itemInventoryIn, 0.5f, 0.5f);

            int left = CountOf(bundle) - 1;
            WriteCount(bundle, left);
            if (left <= 1)
                FirewoodBundleBuilder.RestoreSingle(bundle);
            else
                FirewoodBundleBuilder.Apply(bundle);

            Plugin.Log.LogInfo("Lantern took 1 candle. " + left + " left in the bundle.");
            return true;
        }

        // G on a loose piece in an open crate picks up a bundle of that crate's loose pieces.
        // A bundle is two-handed, and a crate will not hold one, so it goes straight to hand.
        internal static void GatherLookedAtPiece()
        {
            if (!BundleKind.AnyEnabled)
                return;
            if (GameState.inCursorMenu || GameState.sleeping || BoatCamera.on)
                return;
            if (!Input.GetKeyDown(KeyCode.G))
                return;

            GoPointer[] pointers = Object.FindObjectsOfType<GoPointer>();
            for (int i = 0; i < pointers.Length; i++)
            {
                GoPointer pointer = pointers[i];
                if (pointer == null || pointer.GetHeldItem() != null)
                    continue;
                ShipItem piece = CrateSlots.ItemOnSlot(CrateSlots.PointedButton(pointer));
                if (CanGatherPiece(piece))
                {
                    Gather(piece, pointer);
                    return;
                }
                ShipItem ground = pointer.GetPointedAtItem();
                if (CanGatherGround(ground))
                {
                    GatherGround(ground, pointer);
                    return;
                }
            }
        }

        // G on a loose log, piece of dried fruit or wedge of cheese lying out in the world
        // gathers the loose ones of its kind near it into a bundle in hand.
        internal const float GroundGatherRadius = 3f;

        internal static bool CanGatherGround(ShipItem piece)
        {
            BundleKind kind = KindOf(piece);
            if (kind == null || (kind != BundleKind.Firewood && kind != BundleKind.Cheese && !kind.AlwaysHung))
                return false;
            if (!kind.IsEnabled || !kind.Hung.IsEnabled || !IsLoose(piece))
                return false;
            if (kind.IsFood && SausageStacks.HangRefusal(piece) != null)
                return false;
            // The look text asks every frame, so the search is reused for a moment.
            if (piece == _nearPiece && Time.time - _nearAt < 0.3f)
                return _nearCount >= 1;
            _nearPiece = piece;
            _nearAt = Time.time;
            _nearCount = LooseNear(piece).Count;
            return _nearCount >= 1;
        }

        private static ShipItem _nearPiece;
        private static float _nearAt = -10f;
        private static int _nearCount;

        private static bool IsLoose(ShipItem item)
        {
            if (item == null || !item.sold || item.held != null || item.unclickable || item.nailed || CountOf(item) > 1)
                return false;
            StoveFuel burning = item.GetComponent<StoveFuel>();
            if (burning != null && burning.inserted)
                return false;
            if (CrateSlots.OpenCrateHolding(item) != null)
                return false;
            ItemRigidbody body = item.itemRigidbodyC;
            if (body != null && (body.inStove || body.GetCurrentInventorySlot() != null || body.GetCurrentBox() != null))
                return false;
            return true;
        }

        private static List<ShipItem> LooseNear(ShipItem piece)
        {
            var loose = new List<ShipItem>();
            BundleKind kind = KindOf(piece);
            ShipItem[] items = kind != null && kind.IsFood
                ? Object.FindObjectsOfType<ShipItemFood>()
                : Object.FindObjectsOfType<ShipItem>();
            float reach = GroundGatherRadius * GroundGatherRadius;
            for (int i = 0; i < items.Length; i++)
            {
                ShipItem item = items[i];
                if (item == piece || KindOf(item) != kind || !IsLoose(item))
                    continue;
                if ((item.transform.position - piece.transform.position).sqrMagnitude > reach)
                    continue;
                if (SausageStacks.Rejection(piece, item) != null)
                    continue;
                loose.Add(item);
            }

            loose.Sort((a, b) => (b.transform.position - piece.transform.position).sqrMagnitude
                .CompareTo((a.transform.position - piece.transform.position).sqrMagnitude));
            return loose;
        }

        private static void GatherGround(ShipItem piece, GoPointer pointer)
        {
            BundleKind kind = KindOf(piece);
            List<ShipItem> loose = LooseNear(piece);
            int size = Mathf.Min(kind.Limit, loose.Count + 1);
            if (size < 2)
                return;

            // The nearest ones are last in the list, so they go first.
            for (int extra = 1; extra < size; extra++)
            {
                ShipItem other = loose[loose.Count - extra];
                SausageStacks.MergeFood(piece, other, extra, 1);
                Consume(other);
            }

            ShipItem template = PrefabOf(piece);
            float distance = template != null ? template.holdDistance : 0.4f;
            float height = template != null ? template.holdHeight : 0f;
            piece.transform.position = pointer.transform.position + pointer.transform.forward * distance + pointer.transform.up * height;
            piece.transform.rotation = pointer.transform.rotation;
            pointer.PickUpItem(piece);

            if (kind.AlwaysHung)
                SausageStacks.MakeHanging(piece);
            WriteCount(piece, size);
            FirewoodBundleBuilder.Apply(piece);
            _nearPiece = null;
            Plugin.Log.LogInfo("Gathered a " + kind.Hung.BundleName + " of " + size + " " + kind.Plural + " from nearby.");
        }

        internal static bool CanGatherPiece(ShipItem piece)
        {
            if (!IsActive(piece) || CountOf(piece) > 1 || KindOf(piece).Hangs)
                return false;
            // A banana gathers into a bunch, so the bunch has to be turned on too.
            if (KindOf(piece).AlwaysHung && !KindOf(piece).Hung.IsEnabled)
                return false;
            if (KindOf(piece).IsFood && SausageStacks.HangRefusal(piece) != null)
                return false;
            CrateInventory inventory = CrateSlots.OpenCrateHolding(piece);
            return inventory != null && LoosePieces(inventory, KindOf(piece), piece).Count >= 1;
        }

        private static List<ShipItem> LoosePieces(CrateInventory inventory, BundleKind kind, ShipItem except)
        {
            var loose = new List<ShipItem>();
            if (inventory == null || inventory.containedItems == null)
                return loose;
            for (int i = 0; i < inventory.containedItems.Count; i++)
            {
                ShipItem item = inventory.containedItems[i];
                if (item == null || item == except || KindOf(item) != kind || CountOf(item) > 1)
                    continue;
                if (SausageStacks.Rejection(except, item) != null)
                    continue;
                loose.Add(item);
            }

            return loose;
        }

        private static void Gather(ShipItem piece, GoPointer pointer)
        {
            CrateInventory inventory = CrateSlots.OpenCrateHolding(piece);
            BundleKind kind = KindOf(piece);
            List<ShipItem> loose = LoosePieces(inventory, kind, piece);
            int size = Mathf.Min(kind.Limit, loose.Count + 1);
            if (size < 2)
                return;

            for (int extra = 1; extra < size; extra++)
            {
                ShipItem other = loose[loose.Count - extra];
                SausageStacks.MergeFood(piece, other, extra, 1);
                inventory.WithdrawItem(other);
                CrateSlots.ClearCrateSlot(other);
                Consume(other);
            }

            // Vanilla takes an item out of a crate with WithdrawItem, then PickUpItem.
            // Put it at the hold point first so the two-handed grip is not set from the crate.
            inventory.WithdrawItem(piece);
            CrateSlots.ClearCrateSlot(piece);
            ShipItem template = PrefabOf(piece);
            float distance = template != null ? template.holdDistance : 0.4f;
            float height = template != null ? template.holdHeight : 0f;
            piece.transform.position = pointer.transform.position + pointer.transform.forward * distance + pointer.transform.up * height;
            piece.transform.rotation = pointer.transform.rotation;
            pointer.PickUpItem(piece);

            // Bananas only come as a bunch and apples as a bag.
            if (kind.AlwaysHung)
            {
                SausageStacks.MakeHanging(piece);
                kind = kind.Hung;
            }
            WriteCount(piece, size);
            FirewoodBundleBuilder.Apply(piece);
            CrateSlots.Refresh(inventory);
            Plugin.Log.LogInfo("Picked up a " + kind.BundleName + " of " + size + " " + kind.Plural + " from the crate.");
        }

        internal static List<Vector3> Centers(int count, float pitch, int longAxis)
        {
            return Centers(count, pitch, pitch, (longAxis + 1) % 3, (longAxis + 2) % 3, 0);
        }

        // Columns run along columnAxis, rows stack along upAxis. maxColumns 0 keeps the
        // grid as close to square as the count allows. 1 stacks every piece in one column.
        internal static List<Vector3> Centers(int count, float columnPitch, float rowPitch, int columnAxis, int upAxis, int maxColumns)
        {
            var centers = new List<Vector3>(Mathf.Max(count, 0));
            if (count < 1)
                return centers;

            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            if (maxColumns > 0)
                columns = Mathf.Min(columns, maxColumns);
            int rows = Mathf.CeilToInt(count / (float)columns);
            int placed = 0;
            for (int row = 0; row < rows && placed < count; row++)
            {
                int inRow = Mathf.Min(columns, count - placed);
                float rowWidth = (inRow - 1) * columnPitch;
                // The bottom row stays on the nail point. Extra rows stack upward.
                float y = row * rowPitch;
                for (int column = 0; column < inRow; column++)
                {
                    Vector3 local = Vector3.zero;
                    local[columnAxis] = column * columnPitch - rowWidth * 0.5f;
                    local[upAxis] = y;
                    centers.Add(local);
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

    }
}
