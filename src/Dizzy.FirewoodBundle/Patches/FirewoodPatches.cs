using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.FirewoodBundle
{
    [HarmonyPatch(typeof(GoPointer), "DoRaycast")]
    [HarmonyPriority(Priority.Last)]
    [HarmonyAfter("com.dizzy.sailwind.fixes")]
    internal static class FirewoodLookPatch
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        // While holding firewood or a candle, highlight the matching piece or bundle under
        // the crosshair. Otherwise aim through other pieces so the held one can be set down.
        private static void Postfix(
            GoPointer __instance,
            PickupableItem ___heldItem,
            bool ___debugEditorPointer,
            Ray ___raycastRay,
            ref GoPointerButton ___pointedAtButton,
            ref float ___currentLookDistance)
        {
            if (!BundleKind.AnyEnabled && !FirewoodBundleConfig.HooksAreEnabled)
                return;
            if (!VanillaLooks(__instance))
                return;

            ShipItem held = FirewoodPieces.AsShip(___heldItem);
            if (FirewoodBundleConfig.HooksAreEnabled && HookLinePieces.IsHook(held))
            {
                AimHeldHook(__instance, held, ___debugEditorPointer, ___raycastRay, ref ___pointedAtButton, ref ___currentLookDistance);
                return;
            }

            if (!FirewoodPieces.IsActive(held))
            {
                if (FirewoodBundleConfig.HooksAreEnabled && ___heldItem != null)
                {
                    ShipItemFishingRod rod = ___heldItem.GetComponent<ShipItemFishingRod>();
                    if (rod != null && rod.health <= 0f)
                        AimAtHookLine(__instance, held, ___debugEditorPointer, ___raycastRay, ref ___pointedAtButton, ref ___currentLookDistance);
                    if (rod != null
                        && ___pointedAtButton != null
                        && HookLinePieces.LampIsOccupied(___pointedAtButton.GetComponent<ShipItemLampHook>()))
                    {
                        ___pointedAtButton.ForceUnlook();
                        ___pointedAtButton = null;
                        ___currentLookDistance = 0f;
                    }
                }
                if (FirewoodBundleConfig.HooksAreEnabled
                    && ___heldItem != null
                    && ___heldItem.GetComponent<ShipItemHammer>() != null)
                    AimAtNailableLamp(__instance, ___debugEditorPointer, ___raycastRay, ref ___pointedAtButton, ref ___currentLookDistance);
                if (___heldItem == null)
                {
                    if (FirewoodBundleConfig.IsEnabled && ___pointedAtButton == null)
                        AimAtNailedBundle(__instance, ___debugEditorPointer, ___raycastRay, ref ___pointedAtButton, ref ___currentLookDistance);
                    if (FirewoodBundleConfig.HooksAreEnabled)
                        AimAtHangingLine(__instance, ___debugEditorPointer, ___raycastRay, ref ___pointedAtButton, ref ___currentLookDistance);
                }
                return;
            }

            Ray ray = FirewoodPieces.MakeRay(___debugEditorPointer, ___raycastRay);
            float woodDistance;
            ShipItem wood = FirewoodPieces.PieceInFront(held, ray, out woodDistance);
            if (wood != null)
            {
                if (___pointedAtButton != null && ___pointedAtButton != wood)
                    ___pointedAtButton.ForceUnlook();
                ___pointedAtButton = wood;
                wood.Look(__instance);
                ___currentLookDistance = woodDistance;
                return;
            }

            // A bundle cannot be set onto another object. A single piece can, but only
            // onto a surface that accepts placed items. Any other highlight blocks the drop.
            // A candle bundle still keeps a lantern lit up so it can load one candle.
            if (___pointedAtButton != null
                && held.AllowOnItemClick(___pointedAtButton)
                && (!held.big || IsCandleLantern(held, ___pointedAtButton)))
                return;

            if (___pointedAtButton != null)
                ___pointedAtButton.ForceUnlook();

            if (!held.big)
            {
                float placeDistance;
                GoPointerButton surface = SurfaceBehind(___heldItem, ___debugEditorPointer, ___raycastRay, out placeDistance);
                if (surface != null)
                {
                    ___pointedAtButton = surface;
                    surface.Look(__instance);
                    ___currentLookDistance = placeDistance;
                    return;
                }
            }

            ___pointedAtButton = null;
            ___currentLookDistance = 0f;
        }

        private static bool IsCandleLantern(ShipItem held, GoPointerButton button)
        {
            if (FirewoodPieces.KindOf(held) != BundleKind.Candle)
                return false;
            ShipItemLight lantern = button.GetComponent<ShipItemLight>();
            return lantern != null && !lantern.usesOil;
        }

        // Vanilla does not aim in cursor menus, asleep, in bed or from the boat camera.
        private static bool VanillaLooks(GoPointer pointer)
        {
            if (pointer.type == GoPointer.PointerType.crosshairMouse && GameState.inCursorMenu)
                return false;
            return !GameState.sleeping && !GameState.inBed && !BoatCamera.on;
        }

        // Another hook is the string target. A lamp hook stays highlighted so the line can be hung.
        // A single hook can still be set on an empty rod.
        private static void AimHeldHook(
            GoPointer pointer,
            ShipItem held,
            bool debugEditorPointer,
            Ray raycastRay,
            ref GoPointerButton pointedAtButton,
            ref float currentLookDistance)
        {
            Ray ray = FirewoodPieces.MakeRay(debugEditorPointer, raycastRay);
            float hookDistance;
            ShipItem hook = HookLinePieces.HookInFront(held, ray, out hookDistance);
            if (hook != null)
            {
                if (pointedAtButton != null && pointedAtButton != hook)
                    pointedAtButton.ForceUnlook();
                pointedAtButton = hook;
                hook.Look(pointer);
                currentLookDistance = hookDistance;
                return;
            }

            if (pointedAtButton != null && pointedAtButton.GetComponent<ShipItemLampHook>() != null && HookLinePieces.IsLine(held))
                return;
            if (pointedAtButton != null && pointedAtButton.GetComponentInParent<ShipItemFishingRod>() != null)
            {
                // One loose hook can go on an empty rod. A line can give that rod a single hook.
                ShipItemFishingRod rod = pointedAtButton.GetComponentInParent<ShipItemFishingRod>();
                if (!HookLinePieces.IsLine(held) || (rod != null && rod.health <= 0f))
                    return;
                pointedAtButton.ForceUnlook();
                pointedAtButton = null;
                currentLookDistance = 0f;
                return;
            }
            if (pointedAtButton != null && held.AllowOnItemClick(pointedAtButton))
                return;

            // A crate slot is not a ship item. Vanilla keeps it highlighted so the
            // held line can be put into the container. Clearing it drops the click.
            if (pointedAtButton != null && pointedAtButton.GetComponent<ShipItem>() == null)
                return;

            if (pointedAtButton != null)
                pointedAtButton.ForceUnlook();
            pointedAtButton = null;
            currentLookDistance = 0f;
        }

        // Empty hands ignore nailed items unless they are a crate, bottle, or bed.
        // A nailed firewood bundle still needs to be the look target so right-click can take a log.
        private static void AimAtNailedBundle(
            GoPointer pointer,
            bool debugEditorPointer,
            Ray raycastRay,
            ref GoPointerButton pointedAtButton,
            ref float currentLookDistance)
        {
            Ray ray = FirewoodPieces.MakeRay(debugEditorPointer, raycastRay);
            int count = Physics.RaycastNonAlloc(
                ray,
                Hits,
                FirewoodPieces.Reach,
                FirewoodPieces.LayerMask,
                QueryTriggerInteraction.Collide);
            float nearest = float.MaxValue;
            Collider nearestCollider = null;
            for (int i = 0; i < count; i++)
            {
                if (Hits[i].distance >= nearest)
                    continue;
                nearest = Hits[i].distance;
                nearestCollider = Hits[i].collider;
            }

            GoPointerButton button = ButtonOf(nearestCollider);
            ShipItem bundle = button as ShipItem;
            if (bundle == null || !bundle.nailed || bundle.unclickable || FirewoodPieces.CountOf(bundle) <= 1)
                return;

            pointedAtButton = button;
            button.Look(pointer);
            currentLookDistance = nearest;
        }

        // A table accepts the rod, so it used to win even when the line was on the ray.
        // An empty rod takes the hook line whenever the look hits that line.
        private static void AimAtHookLine(
            GoPointer pointer,
            ShipItem held,
            bool debugEditorPointer,
            Ray raycastRay,
            ref GoPointerButton pointedAtButton,
            ref float currentLookDistance)
        {
            Ray ray = FirewoodPieces.MakeRay(debugEditorPointer, raycastRay);
            float distance;
            ShipItem line = HookLinePieces.LineInFront(held, ray, out distance);
            if (line == null)
                return;
            if (pointedAtButton != null && pointedAtButton != line)
                pointedAtButton.ForceUnlook();
            pointedAtButton = line;
            line.Look(pointer);
            currentLookDistance = distance;
        }

        // A line makes its lamp hook ignore ordinary looks. The hammer still needs that
        // hook so it can be nailed while the line stays hung.
        private static void AimAtNailableLamp(
            GoPointer pointer,
            bool debugEditorPointer,
            Ray raycastRay,
            ref GoPointerButton pointedAtButton,
            ref float currentLookDistance)
        {
            Ray ray = FirewoodPieces.MakeRay(debugEditorPointer, raycastRay);
            int count = Physics.RaycastNonAlloc(
                ray,
                Hits,
                FirewoodPieces.Reach,
                FirewoodPieces.LayerMask,
                QueryTriggerInteraction.Collide);
            float nearestAny = float.MaxValue;
            float nearestLamp = float.MaxValue;
            ShipItemLampHook lampHit = null;
            ShipItem nearestShip = null;
            float nearestShipDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float distance = Hits[i].distance;
                if (distance < nearestAny)
                    nearestAny = distance;

                ShipItemLampHook lamp = HookLinePieces.LampOf(Hits[i].collider);
                if (lamp != null && lamp.unclickable && distance < nearestLamp)
                {
                    nearestLamp = distance;
                    lampHit = lamp;
                }

                GoPointerButton button = ButtonOf(Hits[i].collider);
                ShipItem ship = button as ShipItem;
                if (ship != null && distance < nearestShipDistance)
                {
                    nearestShip = ship;
                    nearestShipDistance = distance;
                }
            }

            ShipItemLampHook target = null;
            float distanceToTarget = 0f;
            if (lampHit != null && nearestLamp <= nearestAny + 0.001f)
            {
                target = lampHit;
                distanceToTarget = nearestLamp;
            }
            else if (nearestShip != null
                && HookLinePieces.IsLine(nearestShip)
                && HookLinePieces.IsHanging(nearestShip)
                && nearestShipDistance <= nearestAny + 0.001f)
            {
                HangableItem hang = nearestShip.GetComponent<HangableItem>();
                target = HookLinePieces.LampOf(HookLinePieces.CurrentHook(hang));
                distanceToTarget = nearestShipDistance;
            }

            if (target == null || !target.unclickable)
                return;

            if (pointedAtButton != null && pointedAtButton != target)
                pointedAtButton.ForceUnlook();
            pointedAtButton = target;
            target.Look(pointer);
            currentLookDistance = distanceToTarget;
        }

        // Hanging turns the physics colliders off. A line on a table keeps them, on the
        // rigidbody, which the normal look does not treat as the hook. Either way, the
        // closest hook line along the look is the target, so one hook can be taken.
        private static void AimAtHangingLine(
            GoPointer pointer,
            bool debugEditorPointer,
            Ray raycastRay,
            ref GoPointerButton pointedAtButton,
            ref float currentLookDistance)
        {
            Ray ray = FirewoodPieces.MakeRay(debugEditorPointer, raycastRay);
            int count = Physics.RaycastNonAlloc(
                ray,
                Hits,
                FirewoodPieces.Reach,
                FirewoodPieces.LayerMask,
                QueryTriggerInteraction.Collide);
            float nearestAny = float.MaxValue;
            float nearest = float.MaxValue;
            GoPointerButton lineButton = null;
            for (int i = 0; i < count; i++)
            {
                float distance = Hits[i].distance;
                if (distance < nearestAny)
                    nearestAny = distance;
                if (distance >= nearest)
                    continue;
                GoPointerButton button = ButtonOf(Hits[i].collider);
                ShipItem line = button as ShipItem;
                if (line == null || line.unclickable || !HookLinePieces.IsLine(line))
                    continue;
                nearest = distance;
                lineButton = button;
            }

            // A wall or table in front of the line stays the look target.
            float current = pointedAtButton != null ? currentLookDistance : float.MaxValue;
            if (lineButton == null || nearest > nearestAny + 0.001f || nearest >= current)
                return;

            if (pointedAtButton != null && pointedAtButton != lineButton)
                pointedAtButton.ForceUnlook();
            pointedAtButton = lineButton;
            lineButton.Look(pointer);
            currentLookDistance = nearest;
        }

        private static GoPointerButton ButtonOf(Collider collider)
        {
            Collider source = collider;
            if (source == null)
                return null;
            if (source.CompareTag("ItemSubcollider") && source.transform.parent != null)
            {
                Collider parent = source.transform.parent.GetComponent<Collider>();
                if (parent != null)
                    source = parent;
            }

            GoPointerButton button = source.GetComponent<GoPointerButton>();
            if (button != null)
                return button;

            ItemRigidbody body = source.GetComponent<ItemRigidbody>();
            if (body != null)
                return body.GetShipItem();

            return source.GetComponentInParent<ShipItem>();
        }

        private static GoPointerButton SurfaceBehind(
            PickupableItem held,
            bool debugEditorPointer,
            Ray raycastRay,
            out float distance)
        {
            distance = 0f;
            Ray ray = FirewoodPieces.MakeRay(debugEditorPointer, raycastRay);
            int count = Physics.RaycastNonAlloc(
                ray,
                Hits,
                FirewoodPieces.Reach,
                FirewoodPieces.LayerMask,
                QueryTriggerInteraction.Collide);
            ShipItem heldShip = FirewoodPieces.AsShip(held);
            GoPointerButton best = null;
            float bestDistance = float.MaxValue;
            float blockedAt = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float hitDistance = Hits[i].distance;
                Collider hit = Hits[i].collider;
                if (hit == null || FirewoodPieces.IsHeldCollider(heldShip, hit))
                    continue;

                Collider collider = hit;
                if (collider.CompareTag("ItemSubcollider") && collider.transform.parent != null)
                    collider = collider.transform.parent.GetComponent<Collider>();
                if (collider == null)
                    continue;

                GoPointerButton button = collider.GetComponent<GoPointerButton>();
                if (CanPlaceAgainst(held, button))
                {
                    if (hitDistance < bestDistance)
                    {
                        bestDistance = hitDistance;
                        best = button;
                    }
                    continue;
                }

                // Other firewood is aimed through. A wall, hull or crate lid is not.
                if (hit.isTrigger || FirewoodPieces.IsPiece(ButtonOf(hit)))
                    continue;
                if (CanPlaceAgainst(held, hit.GetComponentInParent<GoPointerButton>()))
                    continue;
                if (hitDistance < blockedAt)
                    blockedAt = hitDistance;
            }

            if (best == null || bestDistance > blockedAt + 0.05f)
                return null;
            distance = bestDistance;
            return best;
        }

        private static bool CanPlaceAgainst(PickupableItem held, GoPointerButton button)
        {
            if (button == null || button.unclickable || held == null)
                return false;
            if (FirewoodPieces.IsPiece(button) || button.GetComponent<ShipItem>() == held)
                return false;
            if (held.AllowOnItemClick(button))
                return true;
            return button.GetComponent<ShipItem>() == null && button.GetComponent<GPButtonBed>() == null;
        }
    }

    [HarmonyPatch(typeof(PickupableItemCollisionChecker), "Update")]
    internal static class FirewoodDropOverlapPatch
    {
        private static readonly FieldInfo CollidedField = AccessTools.Field(typeof(PickupableItemCollisionChecker), "collidedCols");
        private static readonly FieldInfo DecolDistanceField = AccessTools.Field(typeof(PickupableItemCollisionChecker), "currentDecolDistance");

        // The deck is ignored, so a log drops on the boat. The island is not.
        // Leaving the island often never sends an exit, so the overlap stays
        // after you come back aboard and the drop is refused.
        private static void Postfix(PickupableItemCollisionChecker __instance, ShipItem ___item)
        {
            if (___item == null || ___item.held == null)
                return;
            bool wood = FirewoodPieces.IsActive(___item);
            bool hooks = FirewoodBundleConfig.HooksAreEnabled && HookLinePieces.IsHook(___item);
            if (!wood && !hooks)
                return;

            if (CollidedField == null)
                return;

            var collided = CollidedField.GetValue(__instance) as List<Collider>;
            if (collided == null)
                return;

            Collider self = __instance.GetComponent<Collider>();
            float penetration = 0f;
            int live = 0;
            for (int i = collided.Count - 1; i >= 0; i--)
            {
                Collider other = collided[i];
                Vector3 direction;
                float distance;
                if (other == null
                    || self == null
                    || !other.enabled
                    || !other.gameObject.activeInHierarchy
                    || !Physics.ComputePenetration(
                        self,
                        __instance.transform.position,
                        __instance.transform.rotation,
                        other,
                        other.transform.position,
                        other.transform.rotation,
                        out direction,
                        out distance)
                    || distance <= 0f)
                {
                    collided.RemoveAt(i);
                    continue;
                }

                live++;
                if (distance > penetration)
                    penetration = distance;
            }

            __instance.collisions = live;
            if (DecolDistanceField != null)
                DecolDistanceField.SetValue(__instance, penetration);
            __instance.allowObstructedDropping = live == 0 || penetration < 0.06f;
        }
    }

    [HarmonyPatch(typeof(GoPointerButton), "UpdateColor")]
    internal static class FirewoodOutlinePatch
    {
        // The game outlines only the renderer on the item itself, which is the
        // center log. The other logs in the bundle follow that same highlight.
        private static void Postfix(GoPointerButton __instance)
        {
            FirewoodBundleBuilder.SyncOutline(__instance as ShipItem);
            HookLineBuilder.SyncOutline(__instance as ShipItem);
        }
    }

    [HarmonyPatch(typeof(ShipItem), "OnAltActivate", new System.Type[] { })]
    internal static class FirewoodGluePatch
    {
        private static bool Prefix(ShipItem __instance)
        {
            if (BundleKind.AnyEnabled)
            {
                if (FirewoodPieces.TryGlue(__instance))
                    return false;
                if (FirewoodPieces.TrySplit(__instance))
                    return false;
                // A right-click with a stack in hand that did not stack or fill anything
                // would eat it. It does nothing instead.
                if (__instance.held != null && SausageStacks.IsStack(__instance))
                    return false;
            }

            if (FirewoodBundleConfig.HooksAreEnabled)
            {
                if (HookLinePieces.TryBaitRod(__instance))
                    return false;
                if (HookLinePieces.TryString(__instance))
                    return false;
                if (HookLinePieces.TrySplit(__instance))
                    return false;
            }

            return true;
        }
    }

    // A hook line marks the lamp hook occupied. A fishing rod must not hang there.
    [HarmonyPatch(typeof(HangableItem), nameof(HangableItem.ConnectJoint))]
    internal static class HookLineLampClaimPatch
    {
        private static bool Prefix(HangableItem __instance, Collider hook)
        {
            if (!FirewoodBundleConfig.HooksAreEnabled || hook == null)
                return true;
            ShipItemLampHook lamp = HookLinePieces.LampOf(hook);
            if (!HookLinePieces.LampIsOccupied(lamp))
                return true;

            ShipItem self = __instance.GetComponent<ShipItem>();
            return HookLinePieces.IsLine(self) && HookLinePieces.CurrentHook(__instance) == hook;
        }

        private static void Postfix(HangableItem __instance, Collider hook, bool __runOriginal)
        {
            if (!__runOriginal || !FirewoodBundleConfig.HooksAreEnabled || !__instance.IsHanging())
                return;
            ShipItem self = __instance.GetComponent<ShipItem>();
            if (!HookLinePieces.IsLine(self))
                return;
            HookLinePieces.ClaimLamp(HookLinePieces.LampOf(hook));
        }
    }

    [HarmonyPatch(typeof(HangableItem), "LateUpdate")]
    internal static class HookLineLampHoldPatch
    {
        private static void Postfix(HangableItem __instance)
        {
            if (!FirewoodBundleConfig.HooksAreEnabled || !__instance.IsHanging())
                return;
            ShipItem self = __instance.GetComponent<ShipItem>();
            if (!HookLinePieces.IsLine(self))
                return;
            HookLinePieces.ClaimLamp(HookLinePieces.LampOf(HookLinePieces.CurrentHook(__instance)));
        }
    }

    [HarmonyPatch(typeof(HangableItem), nameof(HangableItem.DisconnectJoint))]
    internal static class HookLineLampFreePatch
    {
        private static void Prefix(HangableItem __instance)
        {
            if (!FirewoodBundleConfig.HooksAreEnabled || !__instance.IsHanging())
                return;
            ShipItem self = __instance.GetComponent<ShipItem>();
            if (!HookLinePieces.IsLine(self))
                return;
            HookLinePieces.FreeLamp(HookLinePieces.LampOf(HookLinePieces.CurrentHook(__instance)));
        }
    }

    [HarmonyPatch(typeof(ShipItem), nameof(ShipItem.AllowOnItemClick))]
    internal static class HookLineRodLampPatch
    {
        private static void Postfix(ShipItem __instance, GoPointerButton lookedAtButton, ref bool __result)
        {
            if (!__result || !FirewoodBundleConfig.HooksAreEnabled || __instance == null || lookedAtButton == null)
                return;
            if (__instance.GetComponent<ShipItemFishingRod>() == null)
                return;
            if (HookLinePieces.LampIsOccupied(lookedAtButton.GetComponent<ShipItemLampHook>()))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(ShipItemFishingHook), nameof(ShipItemFishingHook.AllowOnItemClick))]
    internal static class HookLineHangPatch
    {
        private static void Postfix(ShipItemFishingHook __instance, GoPointerButton lookedAtButton, ref bool __result)
        {
            if (!FirewoodBundleConfig.HooksAreEnabled || lookedAtButton == null)
                return;
            if (!HookLinePieces.IsLine(__instance))
                return;
            if (lookedAtButton.GetComponent<ShipItemLampHook>() != null)
                __result = true;
            ShipItemFishingRod rod = lookedAtButton.GetComponentInParent<ShipItemFishingRod>();
            if (rod != null)
                __result = rod.health <= 0f;
        }
    }

    [HarmonyPatch(typeof(ShipItemFishingRod), nameof(ShipItemFishingRod.OnItemClick))]
    internal static class HookLineRodPatch
    {
        // A line stays in hand. An empty rod takes one hook from it. Better Fishing has
        // a prefix here too that uses up whatever hook it is handed, so this one has to
        // run first, or a whole line would go onto the rod as one hook.
        [HarmonyPriority(Priority.First)]
        [HarmonyBefore(HookLinePieces.BetterFishingGuid)]
        private static bool Prefix(ShipItemFishingRod __instance, PickupableItem heldItem, ref bool __result)
        {
            if (!FirewoodBundleConfig.HooksAreEnabled)
                return true;
            ShipItem held = FirewoodPieces.AsShip(heldItem);
            if (!HookLinePieces.IsLine(held))
                return true;
            if (__instance.health > 0f)
            {
                __result = false;
                return false;
            }

            __result = HookLinePieces.GiveOneToRod(held, __instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(ShipItemFishingRod), nameof(ShipItemFishingRod.OnAltActivate))]
    internal static class HookLineRodTakePatch
    {
        private static bool Prefix(ShipItemFishingRod __instance)
        {
            if (!FirewoodBundleConfig.HooksAreEnabled || __instance == null || __instance.held == null)
                return true;
            if (__instance.health > 0f)
                return true;

            ShipItem pointed = __instance.held.GetPointedAtItem();
            if (pointed != null && !HookLinePieces.IsLine(pointed))
                return true;

            ShipItem line = pointed;
            if (line == null)
            {
                GoPointer pointer = __instance.held;
                Ray ray = FirewoodPieces.MakeRay(
                    pointer.debugEditorPointer,
                    new Ray(pointer.transform.position, pointer.transform.forward));
                float distance;
                line = HookLinePieces.LineInFront(__instance, ray, out distance);
            }

            if (!HookLinePieces.IsLine(line))
                return true;
            return !HookLinePieces.GiveOneToRod(line, __instance);
        }
    }

    [HarmonyPatch(typeof(ShipItem), nameof(ShipItem.OnEnterInventory))]
    internal static class HookLineStowPatch
    {
        private static void Postfix(ShipItem __instance)
        {
            if (!HookLinePieces.IsHook(__instance))
                return;
            HangableItem hang = __instance.GetComponent<HangableItem>();
            if (hang != null && hang.IsHanging())
                hang.DisconnectJoint();
        }
    }

    [HarmonyPatch(typeof(ShipItemStove), nameof(ShipItemStove.OnItemClick))]
    internal static class FirewoodStovePatch
    {
        // Clicking the stove with a bundle feeds one log and keeps the rest held.
        // A single log still uses the vanilla insert, which puts that log in the fire.
        private static bool Prefix(PickupableItem heldItem, StoveFuelTrigger ___fuelTrigger, ref bool __result)
        {
            if (!FirewoodBundleConfig.IsEnabled)
                return true;

            ShipItem held = FirewoodPieces.AsShip(heldItem);
            if (!FirewoodPieces.IsFirewood(held) || FirewoodPieces.CountOf(held) <= 1)
                return true;

            FirewoodPieces.TryFeedOneLog(held, ___fuelTrigger);
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(StoveFuelTrigger), nameof(StoveFuelTrigger.InsertFuel))]
    internal static class FirewoodStoveInsertPatch
    {
        // The stove pulls in any fuel whose collider overlaps the fire while the ship moves.
        // A bundle must stay outside. A single log, including one peeled off for a click, still burns.
        private static bool Prefix(ShipItem item)
        {
            if (!FirewoodBundleConfig.IsEnabled)
                return true;
            return FirewoodPieces.CountOf(item) <= 1;
        }
    }

    [HarmonyPatch(typeof(ShipItemLight), nameof(ShipItemLight.OnItemClick))]
    internal static class CandleLanternPatch
    {
        // Vanilla loads a candle by destroying the held item, which would use up the whole bundle.
        // A bundle gives the lantern one candle instead. A single candle still uses the vanilla load.
        private static bool Prefix(ShipItemLight __instance, PickupableItem heldItem, ref bool __result)
        {
            ShipItem held = FirewoodPieces.AsShip(heldItem);
            if (!FirewoodPieces.TryLightLantern(held, __instance))
                return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(ShipItem), nameof(ShipItem.OnItemClick))]
    internal static class FirewoodPlacePatch
    {
        // Left click while aiming at a matching piece drops the held one.
        // Returning true here would drop it with no collision check.
        private static bool Prefix(ShipItem __instance, PickupableItem heldItem, ref bool __result)
        {
            ShipItem held = FirewoodPieces.AsShip(heldItem);
            if (FirewoodBundleConfig.HooksAreEnabled && HookLinePieces.IsHook(held) && HookLinePieces.IsHook(__instance))
            {
                __result = false;
                return false;
            }

            BundleKind kind = FirewoodPieces.KindOf(held);
            if (kind == null || !kind.IsEnabled || FirewoodPieces.KindOf(__instance) != kind)
                return true;

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(ShipItem), nameof(ShipItem.OnPickup))]
    internal static class FirewoodPickupPatch
    {
        private static void Postfix(ShipItem __instance)
        {
            if (FirewoodPieces.IsPiece(__instance))
                FirewoodPieces.NotePickedUp();
            // Lifting a hanging bundle takes it off its hook, ready to hang somewhere else.
            if (FirewoodPieces.KindOf(__instance) != null && FirewoodPieces.KindOf(__instance).Hangs)
            {
                HangableItem hang = __instance.GetComponent<HangableItem>();
                if (hang != null && hang.IsHanging())
                    hang.DisconnectJoint();
            }
            if (HookLinePieces.IsHook(__instance))
            {
                HookLinePieces.NotePickedUp();
                HookLinePieces.ReleaseFromCrate(__instance);
                if (HookLinePieces.IsLine(__instance) && HookLinePieces.CountOf(__instance) <= 1)
                    HookLineBuilder.RestoreSingle(__instance);
                else
                {
                    HangableItem hang = __instance.GetComponent<HangableItem>();
                    if (hang != null && hang.IsHanging())
                        hang.DisconnectJoint();
                    // The crate has already let go of it by the time it is picked up.
                    HookLineBuilder.RefitOutOfCrate(__instance);
                }
            }
        }
    }

    [HarmonyPatch(typeof(ShipItem), nameof(ShipItem.UpdateLookText))]
    internal static class FirewoodTooltipPatch
    {
        private static void Postfix(ShipItem __instance)
        {
            int count = FirewoodPieces.CountOf(__instance);
            if (count > 1 || (FirewoodPieces.KindOf(__instance) != null && FirewoodPieces.KindOf(__instance).Hangs))
            {
                __instance.lookText = FirewoodPieces.LookText(__instance, count);
                // Food keeps the game's own description, e.g. "smoked sausage".
                ShipItem prefab = FirewoodPieces.PrefabOf(__instance);
                if (prefab != null && !(__instance is ShipItemFood))
                    __instance.description = prefab.description;
            }

            if (HookLinePieces.IsLine(__instance))
            {
                __instance.lookText = HookLinePieces.LookText(__instance, HookLinePieces.CountOf(__instance));
                __instance.description = "";
            }
        }
    }

    [HarmonyPatch(typeof(LookUI), nameof(LookUI.ShowLookText))]
    internal static class FirewoodLookTextPatch
    {
        private static void Postfix(
            GoPointerButton button,
            GoPointer ___pointer,
            TextMesh ___extraText,
            TextMesh ___hintText,
            TextMesh ___controlsText,
            Renderer ___mouseLIcon,
            Renderer ___mouseRIcon,
            TextMesh ___textLicon,
            TextMesh ___textRIcon)
        {
            ShipItem item = FirewoodPieces.AsShip(button);
            if (item == null)
                item = CrateSlots.ItemOnSlot(button);
            int count = FirewoodPieces.CountOf(item);
            if (count > 1)
            {
                item.lookText = FirewoodPieces.LookText(item, count);
                ShipItem prefab = FirewoodPieces.PrefabOf(item);
                if (prefab != null && !(item is ShipItemFood))
                    item.description = prefab.description;
                // The label already says what a sausage stack is, so it has no description.
                if (SausageStacks.IsStack(item))
                    item.description = "";
                if (___extraText != null)
                    ___extraText.text = item.lookText;
                if (___hintText != null)
                    ___hintText.text = item.description;
            }

            if (___pointer == null || ___controlsText == null)
                return;

            ShipItem held = FirewoodPieces.AsShip(___pointer.GetHeldItem());
            if (OfferRodBait(___controlsText, ___mouseLIcon, ___mouseRIcon, ___textLicon, ___textRIcon, held, button))
            {
                if (HookLinePieces.IsLine(item))
                {
                    item.lookText = HookLinePieces.LookText(item, HookLinePieces.CountOf(item));
                    item.description = "";
                    if (___extraText != null)
                        ___extraText.text = item.lookText;
                    if (___hintText != null)
                        ___hintText.text = "";
                }
                OfferCrateGather(___controlsText, item, held);
                return;
            }
            if (FirewoodBundleConfig.HooksAreEnabled && HookLinePieces.IsHook(item))
            {
                int hookCount = HookLinePieces.CountOf(item);
                bool line = HookLinePieces.IsLine(item);
                if (line)
                {
                    item.lookText = HookLinePieces.LookText(item, hookCount);
                    item.description = "";
                    if (___extraText != null)
                        ___extraText.text = item.lookText;
                    if (___hintText != null)
                        ___hintText.text = "";
                }

                if (HookLinePieces.CanTarget(held, item))
                {
                    int heldCount = HookLinePieces.CountOf(held);
                    int room = FirewoodBundleConfig.HookLimit - hookCount;
                    bool heldLine = heldCount > 1 || HookLinePieces.IsLine(held);
                    string noun = HookLinePieces.Noun(item);
                    string action = "\nR String " + noun + "s";
                    if (line && heldLine && room > 0 && heldCount > room)
                        action = "\nR Fill Line";
                    else if (!HookLinePieces.Fits(heldCount + hookCount))
                        action = "\nR Line Full";
                    else if (line)
                        action = "\nR Add to Line";
                    else if (heldCount > 1 && hookCount <= 1)
                        action = "\nR Add " + noun;
                    ___controlsText.text = action;
                    if (___textLicon != null)
                        ___textLicon.gameObject.SetActive(false);
                    if (___mouseLIcon != null)
                        ___mouseLIcon.enabled = false;
                    if (___textRIcon != null)
                        ___textRIcon.gameObject.SetActive(false);
                    if (___mouseRIcon != null)
                        ___mouseRIcon.enabled = false;
                    OfferCrateGather(___controlsText, item, held);
                    return;
                }

                if (line && held == null)
                {
                    bool lastHook = hookCount <= 1 && HookLinePieces.IsHanging(item);
                    ___controlsText.text = lastHook ? "\ntake" : "pick up\ntake";
                    if (lastHook)
                    {
                        if (___textLicon != null)
                            ___textLicon.gameObject.SetActive(false);
                        if (___mouseLIcon != null)
                            ___mouseLIcon.enabled = false;
                    }
                    if (___textRIcon != null)
                        ___textRIcon.gameObject.SetActive(true);
                }
            }

            if (!BundleKind.AnyEnabled)
            {
                OfferCrateGather(___controlsText, item, held);
                return;
            }
            if (FirewoodPieces.CanTargetKind(held, item))
            {
                BundleKind kind = FirewoodPieces.KindOf(held);
                int heldCount = FirewoodPieces.CountOf(held);
                int combined = heldCount + count;
                string refusal = FirewoodPieces.Refusal(held, item);
                string action = "\nR Create " + kind.Group;
                // Onto a hanging bundle the full and fill hints are about the bunch.
                if (FirewoodPieces.KindOf(item).Hangs)
                {
                    kind = FirewoodPieces.KindOf(item);
                    action = "\nR Add " + kind.Single;
                }
                if (refusal != null)
                    action = "\nR " + refusal;
                else if (!FirewoodPieces.Fits(item, combined))
                    action = FirewoodPieces.RoomIn(held, item) > 0
                        ? "\nR Fill " + kind.Group
                        : "\nR " + kind.Group + " Full";
                else if (kind.Hangs)
                    action = "\nR Add " + kind.Single;
                else if (heldCount > 1 && count <= 1)
                    action = "\nR Add " + kind.Single;
                else if (count > 1)
                    action = "\nR Add to " + kind.Group;
                ___controlsText.text = action;
                if (___textLicon != null)
                    ___textLicon.gameObject.SetActive(false);
                if (___mouseLIcon != null)
                    ___mouseLIcon.enabled = false;
                if (___textRIcon != null)
                    ___textRIcon.gameObject.SetActive(false);
                if (___mouseRIcon != null)
                    ___mouseRIcon.enabled = false;
                OfferCrateGather(___controlsText, item, held);
                return;
            }

            if ((count > 1 || (FirewoodPieces.KindOf(item) != null && FirewoodPieces.KindOf(item).Hangs)) && held == null)
            {
                string remove = "Remove " + FirewoodPieces.KindOf(item).Single;
                ___controlsText.text = item.nailed ? "\n" + remove : "pick up\n" + remove;
                AddPrompt(___controlsText, FirewoodPieces.ColorPrompt(item));
                if (item.nailed)
                {
                    if (___textLicon != null)
                        ___textLicon.gameObject.SetActive(false);
                    if (___mouseLIcon != null)
                        ___mouseLIcon.enabled = false;
                }
                if (___textRIcon != null)
                    ___textRIcon.gameObject.SetActive(true);
            }

            OfferCrateGather(___controlsText, item, held);
        }

        private static bool OfferRodBait(
            TextMesh controls,
            Renderer mouseL,
            Renderer mouseR,
            TextMesh textL,
            TextMesh textR,
            ShipItem held,
            GoPointerButton looked)
        {
            if (controls == null || !HookLinePieces.CanBaitRod(held, looked))
                return false;
            // The line is whichever of the two is not the rod.
            Component line = HookLinePieces.IsHook(held) ? (Component)held : looked;
            controls.text = "\nR Add " + HookLinePieces.Noun(line);
            if (textL != null)
                textL.gameObject.SetActive(false);
            if (mouseL != null)
                mouseL.enabled = false;
            if (textR != null)
                textR.gameObject.SetActive(false);
            if (mouseR != null)
                mouseR.enabled = false;
            return true;
        }

        private static void OfferCrateGather(TextMesh controls, ShipItem item, ShipItem held)
        {
            if (controls == null)
                return;
            if (FirewoodBundleConfig.HooksAreEnabled && HookLinePieces.CanGather(item))
                AddPrompt(controls, "G Bundle " + HookLinePieces.Noun(item) + "s");
            // The bundle goes to hand, so the hand has to be empty.
            if (held == null && (FirewoodPieces.CanGatherPiece(item) || FirewoodPieces.CanGatherGround(item)))
                AddPrompt(controls, FirewoodPieces.KindOf(item).GatherPrompt);
        }

        // The first two lines sit beside the left- and right-click icons. An extra line
        // must not move them, so pad the side the text grows from to match its anchor.
        private static void AddPrompt(TextMesh controls, string prompt)
        {
            string text = controls.text ?? "";
            if (text.IndexOf(prompt, System.StringComparison.Ordinal) >= 0)
                return;

            switch (controls.anchor)
            {
                case TextAnchor.LowerLeft:
                case TextAnchor.LowerCenter:
                case TextAnchor.LowerRight:
                    controls.text = prompt + "\n" + text;
                    break;
                case TextAnchor.MiddleLeft:
                case TextAnchor.MiddleCenter:
                case TextAnchor.MiddleRight:
                    controls.text = "\n" + text + "\n" + prompt;
                    break;
                default:
                    controls.text = text + "\n" + prompt;
                    break;
            }
        }
    }

    [HarmonyPatch(typeof(SaveablePrefab), nameof(SaveablePrefab.Load))]
    internal static class FirewoodLoadPatch
    {
        private static void Postfix(SaveablePrefab __instance, SavePrefabData data)
        {
            try
            {
                ShipItem item = __instance.GetComponent<ShipItem>();
                BundleKind kind = FirewoodPieces.KindOf(item);
                if (data != null && kind != null && kind.IsFood && !kind.Hangs)
                    SausageStacks.Decode(item, data.extraValue4);
                kind = FirewoodPieces.KindOf(item);
                if (FirewoodPieces.CountOf(item) > 1 || (kind != null && kind.Hangs))
                    FirewoodBundleBuilder.Apply(item);
                if (HookLinePieces.IsLine(item))
                    HookLineBuilder.Apply(item);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError("Could not rebuild a loaded bundle: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(CrateInventoryButton), nameof(CrateInventoryButton.ShowItem))]
    internal static class HookLineCrateCountPatch
    {
        private static void Postfix(CrateInventoryButton __instance, ShipItem item)
        {
            HookLineCrateLabel.Show(__instance, item);
        }
    }

    internal static class HookLineCrateLabel
    {
        private const string LabelName = "HookLineCount";

        internal static void Show(CrateInventoryButton slot, ShipItem item)
        {
            Clear(slot);
            if (slot == null || !HookLinePieces.IsLine(item))
                return;

            int hooks = HookLinePieces.CountOf(item);
            slot.lookText = HookLinePieces.LookText(item, hooks);
            slot.description = "";
            Create(slot, hooks);
        }

        private static void Clear(CrateInventoryButton slot)
        {
            Transform transform = slot.transform;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name == LabelName)
                    Object.Destroy(child.gameObject);
            }
        }

        private static void Create(CrateInventoryButton slot, int hooks)
        {
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
                return;

            var label = new GameObject(LabelName);
            label.layer = slot.gameObject.layer;
            label.transform.SetParent(slot.transform, false);
            label.transform.localPosition = new Vector3(0f, -0.16f, 0.08f);
            label.transform.localRotation = Quaternion.identity;
            label.transform.localScale = Vector3.one;

            TextMesh text = label.AddComponent<TextMesh>();
            text.font = font;
            text.text = hooks.ToString();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.012f;
            text.color = new Color(0.12f, 0.08f, 0.04f);
            MeshRenderer renderer = label.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = font.material;
        }
    }

    [HarmonyPatch(typeof(ShipItem), "OnDestroy")]
    internal static class FirewoodCleanupPatch
    {
        private static void Prefix(ShipItem __instance)
        {
            FirewoodBundleBuilder.Forget(__instance);
            HookLineBuilder.Forget(__instance);
        }
    }

    // A two-handed carry clears the look target unless the held item can click it.
    // Firewood cannot click other firewood, so a bundle lost the gather prompt.
    internal static class FirewoodBigLookPatch
    {
        internal static void Apply(Harmony harmony)
        {
            System.Reflection.MethodInfo method = AccessTools.Method(
                AccessTools.TypeByName("Dizzy.Fixes.BigCrateCarry"),
                "CanUse");
            if (method == null)
                return;
            harmony.Patch(method, postfix: new HarmonyMethod(typeof(FirewoodBigLookPatch), nameof(AllowFirewood)));
        }

        private static void AllowFirewood(PickupableItem held, GoPointerButton button, ref bool __result)
        {
            if (__result)
                return;
            ShipItem heldItem = FirewoodPieces.AsShip(held);
            ShipItem target = FirewoodPieces.AsShip(button);
            if (!FirewoodPieces.CanTargetKind(heldItem, target))
                return;
            __result = true;
        }
    }
}
