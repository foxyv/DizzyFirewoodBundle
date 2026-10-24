using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Dizzy.FirewoodBundle
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.dizzy.sailwind.fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(HookLinePieces.BetterFishingGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(TentPoles.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(Munitions.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(VirtualCrew.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(CoopBridge.CoopGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInProcess("Sailwind.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.dizzy.sailwind.firewoodbundle";
        public const string PluginName = "Dizzy Bundle Up";
        public const string PluginVersion = "0.15.6";

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            FirewoodBundleConfig.Bind(Config);
            Stall.Start(this);

            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll(typeof(Plugin).Assembly);
                FirewoodBigLookPatch.Apply(_harmony);
                TentPoles.Patch(_harmony);
                Munitions.Patch(_harmony);
                CoopBridge.Apply(_harmony);
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
                + ", Meat=" + FirewoodBundleConfig.MeatEnabled.Value
                + ", MaxMeat=" + FirewoodBundleConfig.MaxMeat.Value
                + ", Orange Bags=" + FirewoodBundleConfig.OrangesEnabled.Value
                + ", MaxOranges=" + FirewoodBundleConfig.MaxOranges.Value
                + ", Wood Poles=" + FirewoodBundleConfig.PolesEnabled.Value
                + ", MaxPoles=" + FirewoodBundleConfig.MaxPoles.Value
                + ", Round Shot=" + FirewoodBundleConfig.ShotEnabled.Value
                + ", MaxShot=" + FirewoodBundleConfig.MaxShot.Value
                + ", Dates=" + FirewoodBundleConfig.DatesEnabled.Value
                + ", MaxDates=" + FirewoodBundleConfig.MaxDates.Value
                + ", Hooks=" + FirewoodBundleConfig.HooksEnabled.Value
                + ", MaxHooks=" + FirewoodBundleConfig.MaxHooks.Value
                + ", Hook Messiness=" + FirewoodBundleConfig.HookMessiness.Value
                + ", Hook Flare=" + FirewoodBundleConfig.HookFlareAmount.Value
                + ", Spacing=" + FirewoodBundleConfig.HookSpacingAmount.Value
                + ", Line Length=" + FirewoodBundleConfig.HookLineLengthAmount.Value + ").");
        }

        // Every plugin has loaded by now, so the crew mod can be found whatever it is called.
        private void Start()
        {
            try
            {
                VirtualCrew.Patch(_harmony);
            }
            catch (System.Exception ex)
            {
                Log.LogWarning("Could not link to the crew mod: " + ex.Message);
            }
        }

        private void FixedUpdate()
        {
            Stall.Mark("before physics (FixedUpdate)");
        }

        private void Update()
        {
            Stall.Mark("Update");
        }

        private void LateUpdate()
        {
            Stall.Mark("LateUpdate (Bundle Up)");
            FirewoodPieces.ReleaseHeldPiece();
            HookLinePieces.ReleaseHeldHook();
            HookLinePieces.GatherLookedAtHook();
            FirewoodPieces.GatherLookedAtPiece();
            FirewoodPieces.CycleLookedAtColor();
            SausageStacks.CycleLookedAtWidth();
            CoopBridge.Flush();
            CoopBridge.ProbeIfAsked();
            Stall.Frame();
            Stall.Mark("end of LateUpdate (Bundle Up)");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            if (Instance == this)
                Instance = null;
        }
    }
}
