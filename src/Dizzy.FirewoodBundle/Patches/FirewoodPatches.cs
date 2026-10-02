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

        // While holding firewood, highlight the log or bundle under the crosshair.
        // Otherwise aim through other wood so the held piece can be set down.
        private static void Postfix(
            GoPointer __instance,
            PickupableItem ___heldItem,
            bool ___debugEditorPointer,
            Ray ___raycastRay,
            ref GoPointerButton ___pointedAtButton,
            ref float ___currentLookDistance)
        {
            if (!FirewoodBundleConfig.IsEnabled && !FirewoodBundleConfig.HooksAreEnabled)
                return;

            ShipItem held = FirewoodPieces.AsShip(___heldItem);
            if (FirewoodBundleConfig.HooksAreEnabled && HookLinePieces.IsHook(held))
            {
                AimHeldHook(__instance, held, ___debugEditorPointer, ___raycastRay, ref ___pointedAtButton, ref ___currentLookDistance);
                return;
            }

            if (!FirewoodBundleConfig.IsEnabled || !FirewoodPieces.IsPiece(held))
            {
                if (FirewoodBundleConfig.HooksAreEnabled && ___heldItem != null)
                {
                    ShipItemFishingRod rod = ___heldItem.GetComponent<ShipItemFishingRod>();
                    if (rod != null && rod.health <= 0f)
                        AimAtHookLine(__instance, ___debugEditorPointer, ___raycastRay, ref ___pointedAtButton, ref ___currentLookDistance);
                    if (rod != null
                        && ___pointedAtButton != null
                        && HookLinePieces.LampIsOccupied(___pointedAtButton.GetComponent<ShipItemLampHook>()))
                    {
                        ___pointedAtButton.ForceUnlook();
                        ___pointedAtButton = null;
                        ___currentLookDistance = 0f;
                    }
                }
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

            // A bundle cannot be set onto another object. A single log can, but only
            // onto a surface that accepts placed items. Any other highlight blocks the drop.
            if (!held.big && ___pointedAtButton != null && held.AllowOnItemClick(___pointedAtButton))
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
            bool debugEditorPointer,
            Ray raycastRay,
            ref GoPointerButton pointedAtButton,
            ref float currentLookDistance)
        {
            Ray ray = FirewoodPieces.MakeRay(debugEditorPointer, raycastRay);
            float distance;
            ShipItem line = HookLinePieces.LineInFront(ray, out distance);
            if (line == null)
                return;
            if (pointedAtButton != null && pointedAtButton != line)
                pointedAtButton.ForceUnlook();
            pointedAtButton = line;
            line.Look(pointer);
            currentLookDistance = distance;
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
            GoPointerButton best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float hitDistance = Hits[i].distance;
                if (hitDistance >= bestDistance)
                    continue;

                Collider collider = Hits[i].collider;
                if (collider != null && collider.CompareTag("ItemSubcollider") && collider.transform.parent != null)
                    collider = collider.transform.parent.GetComponent<Collider>();
                if (collider == null)
                    continue;

                GoPointerButton button = collider.GetComponent<GoPointerButton>();
                if (!CanPlaceAgainst(held, button))
                    continue;

                bestDistance = hitDistance;
                best = button;
            }

            if (best == null)
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
        // The deck is ignored, so a log drops on the boat. The island is not.
        // Leaving the island often never sends an exit, so the overlap stays
        // after you come back aboard and the drop is refused.
        private static void Postfix(PickupableItemCollisionChecker __instance, ShipItem ___item)
        {
            if (___item == null || ___item.held == null)
                return;
            bool wood = FirewoodBundleConfig.IsEnabled && FirewoodPieces.IsPiece(___item);
            bool hooks = FirewoodBundleConfig.HooksAreEnabled && HookLinePieces.IsHook(___item);
            if (!wood && !hooks)
                return;

            var listField = AccessTools.Field(typeof(PickupableItemCollisionChecker), "collidedCols");
            var distanceField = AccessTools.Field(typeof(PickupableItemCollisionChecker), "currentDecolDistance");
            if (listField == null)
                return;

            var collided = listField.GetValue(__instance) as List<Collider>;
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
            if (distanceField != null)
                distanceField.SetValue(__instance, penetration);
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
            if (FirewoodBundleConfig.IsEnabled)
            {
                if (FirewoodPieces.TryGlue(__instance))
                    return false;
                if (FirewoodPieces.TrySplit(__instance))
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

    // Right-click with the hammer nails whatever it is pointing at. A lamp hook
    // is where the hammer hangs, and nailing the hook leaves it occupied so the
    // hang never sticks.
    [HarmonyPatch(typeof(ShipItemHammer), nameof(ShipItemHammer.OnAltActivate))]
    [HarmonyPriority(Priority.Last)]
    internal static class FirewoodHammerHookPatch
    {
        private static bool Prefix(ShipItemHammer __instance)
        {
            if (!FirewoodBundleConfig.IsEnabled || __instance == null || __instance.held == null)
                return true;

            ShipItem hook = __instance.held.GetPointedAtItem();
            if (hook == null || hook.GetComponent<ShipItemLampHook>() == null)
                return true;

            ReleaseStuckHang(hook, __instance);
            if (hook.nailed)
                hook.nailed = false;
            if (hook.OnItemClick(__instance))
            {
                GoPointer pointer = __instance.held;
                __instance.OnDrop();
                if (pointer != null)
                    pointer.DropItem();
            }

            return false;
        }

        private static void ReleaseStuckHang(ShipItem hook, ShipItemHammer hammer)
        {
            Component holder = hook.GetComponent("AttachableItemHolder");
            if (holder == null)
                return;

            PropertyInfo attached = holder.GetType().GetProperty("AttachedItem");
            if (attached == null || !ReferenceEquals(attached.GetValue(holder, null), hammer))
                return;

            MethodInfo detach = holder.GetType().GetMethod(
                "DetachItem",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (detach != null)
                detach.Invoke(holder, null);
        }
    }

    [HarmonyPatch(typeof(ShipItemHammer), nameof(ShipItemHammer.OnAltHeld))]
    internal static class FirewoodHammerHookNailPatch
    {
        private static void Prefix(ref ShipItem ___currentlyNailedItem)
        {
            if (___currentlyNailedItem != null && ___currentlyNailedItem.GetComponent<ShipItemLampHook>() != null)
                ___currentlyNailedItem = null;
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

        private static void Postfix(HangableItem __instance, Collider hook)
        {
            if (!FirewoodBundleConfig.HooksAreEnabled || !__instance.IsHanging())
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
        // A line stays in hand. An empty rod takes one hook from it.
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
                line = HookLinePieces.LineInFront(ray, out distance);
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
            if (FirewoodPieces.CountOf(held) <= 1)
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

    [HarmonyPatch(typeof(ShipItem), nameof(ShipItem.OnItemClick))]
    internal static class FirewoodPlacePatch
    {
        // Left click while aiming at another log drops the held piece.
        // Returning true here would drop it with no collision check.
        private static bool Prefix(ShipItem __instance, PickupableItem heldItem, ref bool __result)
        {
            ShipItem held = FirewoodPieces.AsShip(heldItem);
            if (FirewoodBundleConfig.HooksAreEnabled && HookLinePieces.IsHook(held) && HookLinePieces.IsHook(__instance))
            {
                __result = false;
                return false;
            }

            if (!FirewoodBundleConfig.IsEnabled)
                return true;

            if (!FirewoodPieces.IsPiece(held) || !FirewoodPieces.IsPiece(__instance))
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
            if (count > 1)
            {
                __instance.lookText = FirewoodPieces.LookText(count);
                ShipItem prefab = FirewoodPieces.PrefabOf(__instance);
                if (prefab != null)
                    __instance.description = prefab.description;
            }

            if (HookLinePieces.IsLine(__instance))
            {
                __instance.lookText = HookLinePieces.LookText(HookLinePieces.CountOf(__instance));
                __instance.description = "";
            }

            ShipItemLampHook lamp = __instance as ShipItemLampHook;
            if (lamp != null && HookLinePieces.LineClaims(lamp))
                __instance.lookText = "occupied";
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
                item = ItemInCrateSlot(button);
            int count = FirewoodPieces.CountOf(item);
            if (count > 1)
            {
                item.lookText = FirewoodPieces.LookText(count);
                ShipItem prefab = FirewoodPieces.PrefabOf(item);
                if (prefab != null)
                    item.description = prefab.description;
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
                    item.lookText = HookLinePieces.LookText(HookLinePieces.CountOf(item));
                    item.description = "";
                    if (___extraText != null)
                        ___extraText.text = item.lookText;
                    if (___hintText != null)
                        ___hintText.text = "";
                }
                OfferCrateGather(___controlsText, item);
                return;
            }
            if (FirewoodBundleConfig.HooksAreEnabled && HookLinePieces.IsHook(item))
            {
                int hookCount = HookLinePieces.CountOf(item);
                bool line = HookLinePieces.IsLine(item);
                if (line)
                {
                    item.lookText = HookLinePieces.LookText(hookCount);
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
                    string action = "\nR String Hooks";
                    if (line && heldLine && room > 0 && heldCount > room)
                        action = "\nR Fill Line";
                    else if (!HookLinePieces.Fits(heldCount + hookCount))
                        action = "\nR Line Full";
                    else if (line)
                        action = "\nR Add to Line";
                    else if (heldCount > 1 && hookCount <= 1)
                        action = "\nR Add Hook";
                    ___controlsText.text = action;
                    if (___textLicon != null)
                        ___textLicon.gameObject.SetActive(false);
                    if (___mouseLIcon != null)
                        ___mouseLIcon.enabled = false;
                    if (___textRIcon != null)
                        ___textRIcon.gameObject.SetActive(false);
                    if (___mouseRIcon != null)
                        ___mouseRIcon.enabled = false;
                    OfferCrateGather(___controlsText, item);
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

            if (!FirewoodBundleConfig.IsEnabled)
            {
                OfferCrateGather(___controlsText, item);
                return;
            }
            if (FirewoodPieces.CanTarget(held, item))
            {
                int heldCount = FirewoodPieces.CountOf(held);
                int combined = heldCount + count;
                string action = "\nR Create Bundle";
                if (!FirewoodPieces.Fits(held, combined))
                    action = "\nR Bundle Full";
                else if (heldCount > 1 && count <= 1)
                    action = "\nR Gather Log";
                else if (count > 1)
                    action = "\nR Add to Bundle";
                ___controlsText.text = action;
                if (___textLicon != null)
                    ___textLicon.gameObject.SetActive(false);
                if (___mouseLIcon != null)
                    ___mouseLIcon.enabled = false;
                if (___textRIcon != null)
                    ___textRIcon.gameObject.SetActive(false);
                if (___mouseRIcon != null)
                    ___mouseRIcon.enabled = false;
                OfferCrateGather(___controlsText, item);
                return;
            }

            if (count > 1 && held == null)
            {
                ___controlsText.text = item.nailed ? "\ntake" : "pick up\ntake";
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

            OfferCrateGather(___controlsText, item);
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
            controls.text = "\nR Add Hook";
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

        private static void OfferCrateGather(TextMesh controls, ShipItem item)
        {
            if (controls == null || !FirewoodBundleConfig.HooksAreEnabled)
                return;
            if (!HookLinePieces.CanGather(item))
                return;
            if (controls.text != null && controls.text.IndexOf("G Bundle Hooks", System.StringComparison.Ordinal) >= 0)
                return;
            controls.text = (controls.text ?? "") + "\nG Bundle Hooks";
        }

        private static ShipItem ItemInCrateSlot(GoPointerButton button)
        {
            CrateInventoryButton slot = button as CrateInventoryButton;
            if (slot == null)
                return null;
            var field = AccessTools.Field(typeof(CrateInventoryButton), "currentItem");
            if (field == null)
                return null;
            return field.GetValue(slot) as ShipItem;
        }
    }

    [HarmonyPatch(typeof(SaveablePrefab), nameof(SaveablePrefab.Load))]
    internal static class FirewoodLoadPatch
    {
        private static void Postfix(SaveablePrefab __instance)
        {
            try
            {
                ShipItem item = __instance.GetComponent<ShipItem>();
                if (FirewoodPieces.CountOf(item) > 1)
                    FirewoodBundleBuilder.Apply(item);
                if (HookLinePieces.IsLine(item))
                    HookLineBuilder.Apply(item);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError("Could not rebuild a loaded firewood bundle: " + ex.Message);
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
            slot.lookText = HookLinePieces.LookText(hooks);
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
            if (!FirewoodPieces.CanTarget(heldItem, target))
                return;
            __result = true;
        }
    }
}
