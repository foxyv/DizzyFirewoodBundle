using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Dizzy.FirewoodBundle
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.dizzy.sailwind.fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInProcess("Sailwind.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.dizzy.sailwind.firewoodbundle";
        public const string PluginName = "Dizzy Bundle Up";
        public const string PluginVersion = "0.11.0";

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            FirewoodBundleConfig.Bind(Config);

            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll(typeof(Plugin).Assembly);
                FirewoodBigLookPatch.Apply(_harmony);
            }
            catch (System.Exception ex)
            {
                Log.LogError(PluginName + " failed to patch Sailwind: " + ex);
            }

            Log.LogInfo(
                PluginName + " v" + PluginVersion
                + " loaded (Stack Firewood=" + FirewoodBundleConfig.Enabled.Value
                + ", MaxPieces=" + FirewoodBundleConfig.MaxPieces.Value
                + ", Candles=" + FirewoodBundleConfig.CandlesEnabled.Value
                + ", MaxCandles=" + FirewoodBundleConfig.MaxCandles.Value
                + ", Sausages=" + FirewoodBundleConfig.SausagesEnabled.Value
                + ", MaxSausages=" + FirewoodBundleConfig.MaxSausages.Value
                + ", Sausage Spacing=" + FirewoodBundleConfig.SausageSpacingAmount.Value
                + ", Pile Steepness=" + FirewoodBundleConfig.PileSteepnessAmount.Value
                + ", Hanging Sausages=" + FirewoodBundleConfig.HangingEnabled.Value
                + ", MaxHanging=" + FirewoodBundleConfig.MaxHanging.Value
                + ", Banana Bunches=" + FirewoodBundleConfig.BananasEnabled.Value
                + ", MaxBananas=" + FirewoodBundleConfig.MaxBananas.Value
                + ", Apple Bags=" + FirewoodBundleConfig.ApplesEnabled.Value
                + ", MaxApples=" + FirewoodBundleConfig.MaxApples.Value
                + ", Cheese=" + FirewoodBundleConfig.CheeseEnabled.Value
                + ", MaxCheese=" + FirewoodBundleConfig.MaxCheese.Value
                + ", Goat Cheese=" + FirewoodBundleConfig.GoatCheeseEnabled.Value
                + ", MaxGoatCheese=" + FirewoodBundleConfig.MaxGoatCheese.Value
                + ", Orange Bags=" + FirewoodBundleConfig.OrangesEnabled.Value
                + ", MaxOranges=" + FirewoodBundleConfig.MaxOranges.Value
                + ", Dates=" + FirewoodBundleConfig.DatesEnabled.Value
                + ", MaxDates=" + FirewoodBundleConfig.MaxDates.Value
                + ", Hooks=" + FirewoodBundleConfig.HooksEnabled.Value
                + ", MaxHooks=" + FirewoodBundleConfig.MaxHooks.Value
                + ", Hook Messiness=" + FirewoodBundleConfig.HookMessiness.Value
                + ", Hook Flare=" + FirewoodBundleConfig.HookFlareAmount.Value
                + ", Spacing=" + FirewoodBundleConfig.HookSpacingAmount.Value
                + ", Line Length=" + FirewoodBundleConfig.HookLineLengthAmount.Value + ").");
        }

        private void LateUpdate()
        {
            FirewoodPieces.ReleaseHeldPiece();
            HookLinePieces.ReleaseHeldHook();
            HookLinePieces.GatherLookedAtHook();
            FirewoodPieces.GatherLookedAtPiece();
            FirewoodPieces.CycleLookedAtColor();
            SausageStacks.CycleLookedAtWidth();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            if (Instance == this)
                Instance = null;
        }
    }
}
