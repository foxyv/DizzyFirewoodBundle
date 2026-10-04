using HarmonyLib;

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

    [HarmonyPatch(typeof(ShipItemFood), nameof(ShipItemFood.OnAltHeld))]
    internal static class SausageEatHoldPatch
    {
        private static bool Prefix(ShipItemFood __instance)
        {
            return SausageGuard.Allow(__instance);
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
