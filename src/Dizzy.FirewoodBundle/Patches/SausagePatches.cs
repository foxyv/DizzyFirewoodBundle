using HarmonyLib;
using UnityEngine;

namespace Dizzy.FirewoodBundle
{
    // Food saves dried, smoked, salted and spoiled in extra values 0 to 3. Only soup
    // uses extra value 4, so a sausage stack keeps its count and width there.
    [HarmonyPatch(typeof(SaveablePrefab), nameof(SaveablePrefab.PrepareSaveData))]
    internal static class SausageSavePatch
    {
        private static void Postfix(SaveablePrefab __instance, SavePrefabData __result)
        {
            if (__result == null)
                return;
            ShipItem item = __instance.GetComponent<ShipItem>();
            if (SausageStacks.IsStack(item))
                __result.extraValue4 = SausageStacks.Encode(item);
        }
    }

    // FoodState writes "smoked sausage" into the description every frame. A stack's
    // label already says "76 Smoked Sausages", so the stack has no description.
    [HarmonyPatch(typeof(FoodState), "UpdateLookText")]
    internal static class SausageDescriptionPatch
    {
        private static void Postfix(FoodState __instance)
        {
            ShipItem item = __instance.GetComponent<ShipItem>();
            if (SausageStacks.IsStack(item))
                item.description = "";
        }
    }

    // A stack is never used as a whole. Each of these would eat, cook, salt, slice
    // or pour the whole stack as if it were one sausage.
    [HarmonyPatch(typeof(ShipItemFood), nameof(ShipItemFood.EatFood))]
    internal static class SausageEatPatch
    {
        private static bool Prefix(ShipItemFood __instance)
        {
            return SausageGuard.Allow(__instance);
        }
    }

    // Holding right-click raises food to the mouth, and the mouth eats it. A stack never
    // rises. This runs every frame the button is down, including right after a click
    // that stacked a sausage, so it stays quiet; the press itself says why.
    [HarmonyPatch(typeof(ShipItemFood), nameof(ShipItemFood.OnAltHeld))]
    internal static class SausageEatHoldPatch
    {
        private static bool Prefix(ShipItemFood __instance)
        {
            return !SausageStacks.IsStack(__instance);
        }
    }

    // A held food item offers "eat" on right-click. A stack cannot be eaten, so it shows
    // its width key instead.
    [HarmonyPatch(typeof(LookUI), nameof(LookUI.ShowHoldText))]
    internal static class SausageHoldTextPatch
    {
        private static void Postfix(
            PickupableItem item,
            TextMesh ___controlsText,
            Renderer ___mouseRIcon,
            TextMesh ___textRIcon)
        {
            if (!SausageStacks.IsStack(item) || ___controlsText == null)
                return;
            ___controlsText.text = SausageStacks.WidthPrompt(FirewoodPieces.AsShip(item));
            if (___mouseRIcon != null)
                ___mouseRIcon.enabled = false;
            if (___textRIcon != null)
                ___textRIcon.gameObject.SetActive(false);
        }
    }

    [HarmonyPatch(typeof(CookableFood), nameof(CookableFood.InsertIntoCookTrigger))]
    internal static class SausageCookPatch
    {
        private static bool Prefix(CookableFood __instance)
        {
            return SausageGuard.Allow(__instance);
        }
    }

    [HarmonyPatch(typeof(ShipItemSalt), nameof(ShipItemSalt.SaltFood))]
    internal static class SausageSaltPatch
    {
        private static bool Prefix(FoodState food)
        {
            return SausageGuard.Allow(food);
        }
    }

    [HarmonyPatch(typeof(ShipItemSoup), nameof(ShipItemSoup.InsertFood))]
    internal static class SausageSoupPatch
    {
        private static bool Prefix(ShipItemFood food)
        {
            return SausageGuard.Allow(food);
        }
    }

    [HarmonyPatch(typeof(ShipItemKnife), nameof(ShipItemKnife.CutFood))]
    internal static class SausageSlicePatch
    {
        private static bool Prefix(FoodState food)
        {
            return SausageGuard.Allow(food);
        }
    }

    internal static class SausageGuard
    {
        internal static bool Allow(UnityEngine.Component food)
        {
            if (!SausageStacks.IsStack(food))
                return true;
            SausageStacks.Notify(SausageStacks.TakeOneFirst);
            return false;
        }
    }
}
