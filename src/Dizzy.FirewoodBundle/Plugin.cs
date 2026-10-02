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
        public const string PluginName = "Dizzy Firewood Bundle";
        public const string PluginVersion = "0.2.2";

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
                Log.LogError("Dizzy Firewood Bundle failed to patch Sailwind: " + ex);
            }

            Log.LogInfo(
                PluginName + " v" + PluginVersion
                + " loaded (Enabled=" + FirewoodBundleConfig.Enabled.Value
                + ", MaxPieces=" + FirewoodBundleConfig.MaxPieces.Value
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
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            if (Instance == this)
                Instance = null;
        }
    }
}
