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
            if (!FirewoodBundleConfig.IsEnabled)
                return;

            ShipItem held = FirewoodPieces.AsShip(___heldItem);
            if (!FirewoodPieces.IsPiece(held))
            {
                if (___heldItem == null && ___pointedAtButton == null)
                    AimAtNailedBundle(__instance, ___debugEditorPointer, ___raycastRay, ref ___pointedAtButton, ref ___currentLookDistance);
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

            if (___pointedAtButton != null && !FirewoodPieces.IsPiece(___pointedAtButton))
                return;

            float distance;
            GoPointerButton surface = SurfaceBehind(___heldItem, ___debugEditorPointer, ___raycastRay, out distance);
            if (___pointedAtButton != null)
                ___pointedAtButton.ForceUnlook();

            if (surface == null)
            {
                ___pointedAtButton = null;
                ___currentLookDistance = 0f;
                return;
            }

            ___pointedAtButton = surface;
            surface.Look(__instance);
            ___currentLookDistance = distance;
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
            if (body == null)
                return null;
            return body.GetShipItem();
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

    [HarmonyPatch(typeof(GoPointerButton), "UpdateColor")]
    internal static class FirewoodOutlinePatch
    {
        // The game outlines only the renderer on the item itself, which is the
        // center log. The other logs in the bundle follow that same highlight.
        private static void Postfix(GoPointerButton __instance)
        {
            FirewoodBundleBuilder.SyncOutline(__instance as ShipItem);
        }
    }

    [HarmonyPatch(typeof(ShipItem), "OnAltActivate", new System.Type[] { })]
    internal static class FirewoodGluePatch
    {
        private static bool Prefix(ShipItem __instance)
        {
            if (!FirewoodBundleConfig.IsEnabled)
                return true;
            if (FirewoodPieces.TryGlue(__instance))
                return false;
            if (FirewoodPieces.TrySplit(__instance))
                return false;
            return true;
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
            if (!FirewoodBundleConfig.IsEnabled)
                return true;

            ShipItem held = FirewoodPieces.AsShip(heldItem);
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
        }
    }

    [HarmonyPatch(typeof(ShipItem), nameof(ShipItem.UpdateLookText))]
    internal static class FirewoodTooltipPatch
    {
        private static void Postfix(ShipItem __instance)
        {
            int count = FirewoodPieces.CountOf(__instance);
            if (count <= 1)
                return;
            __instance.lookText = FirewoodPieces.LookText(count);
            ShipItem prefab = FirewoodPieces.PrefabOf(__instance);
            if (prefab != null)
                __instance.description = prefab.description;
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

            if (!FirewoodBundleConfig.IsEnabled || ___pointer == null || ___controlsText == null)
                return;

            ShipItem held = FirewoodPieces.AsShip(___pointer.GetHeldItem());
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
        }
    }

    [HarmonyPatch(typeof(SaveablePrefab), nameof(SaveablePrefab.Load))]
    internal static class FirewoodLoadPatch
    {
        private static void Postfix(SaveablePrefab __instance)
        {
            ShipItem item = __instance.GetComponent<ShipItem>();
            if (FirewoodPieces.CountOf(item) > 1)
                FirewoodBundleBuilder.Apply(item);
        }
    }

    [HarmonyPatch(typeof(ShipItem), "OnDestroy")]
    internal static class FirewoodCleanupPatch
    {
        private static void Prefix(ShipItem __instance)
        {
            FirewoodBundleBuilder.Forget(__instance);
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
