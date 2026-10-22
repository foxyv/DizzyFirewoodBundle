using System;
using System.Reflection;
using HarmonyLib;

namespace Dizzy.FirewoodBundle
{
    // The Crew mod (once Virtual Crew), by Zorkinian. Its steward carries food to the player and
    // has it eaten, and a lantern hand fuels lanterns from the candles on the boat. The two
    // things that matter for bundles are handled in the game's own methods, so they work
    // whatever the crew mod is called: a bundle that is eaten gives up one piece
    // (SausageEatPatch), and a lantern takes one candle from a bundle (CandleFuelPatch).
    //
    // This goes one better where the crew mod's methods are found. The steward takes one piece
    // off a bundle at the pile, and carries only that, where it would carry the whole bundle.
    // It is also told that a bundle on a hook is not food for it. The crew mod is found by
    // looking for a plugin with "crew" in its name that has the method.
    internal static class VirtualCrew
    {
        internal const string PluginGuid = "com.zorkinian.virtualcrew";
        private const string StewardCheck = "IsStewardFoodSource";
        private const string StewardPickUp = "PickUpFoodAndGoToPlayer";
        private static System.Reflection.FieldInfo _food;

        internal static void Patch(Harmony harmony)
        {
            if (harmony == null)
                return;

            MethodInfo source = Find(StewardCheck, typeof(bool));
            if (source != null)
                harmony.Patch(source, postfix: new HarmonyMethod(typeof(VirtualCrew), nameof(StewardSkipsHungBundles)));

            MethodInfo pickUp = Find(StewardPickUp, typeof(void));
            _food = pickUp != null ? AccessTools.Field(pickUp.DeclaringType, "food") : null;
            if (pickUp != null && _food != null && _food.FieldType == typeof(ShipItemFood))
                harmony.Patch(pickUp, prefix: new HarmonyMethod(typeof(VirtualCrew), nameof(StewardTakesOnePiece)));

            if (source != null || (pickUp != null && _food != null))
                Plugin.Log.LogInfo("Crew mod found. Its steward leaves food bundles on a hook alone, and takes one piece off the others.");
        }

        private static MethodInfo Find(string method, Type returns)
        {
            foreach (BepInEx.PluginInfo info in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
            {
                if (info == null || info.Instance == null || info.Metadata == null)
                    continue;
                string name = (info.Metadata.GUID + " " + info.Metadata.Name).ToLowerInvariant();
                if (!name.Contains("crew"))
                    continue;

                Type[] types;
                try
                {
                    types = info.Instance.GetType().Assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                foreach (Type type in types)
                {
                    if (type == null)
                        continue;
                    MethodInfo found = type.GetMethod(
                        method,
                        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (found != null && found.ReturnType == returns)
                        return found;
                }
            }
            return null;
        }

        private static void StewardSkipsHungBundles(ShipItemFood food, ref bool __result)
        {
            if (__result && FirewoodPieces.IsHungBundle(food))
                __result = false;
        }

        // The steward is about to pick its food up. If that is a bundle, it takes one piece
        // off at the pile and carries that instead, so the bundle stays where it is.
        private static void StewardTakesOnePiece(object __instance)
        {
            try
            {
                ShipItemFood food = _food.GetValue(__instance) as ShipItemFood;
                if (!FirewoodPieces.IsBundledFood(food) || FirewoodPieces.IsHungBundle(food))
                    return;
                ShipItemFood piece = FirewoodPieces.TakePieceOff(food);
                if (piece != null)
                    _food.SetValue(__instance, piece);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Could not take a piece off a bundle for the steward: " + ex.Message);
            }
        }
    }
}
