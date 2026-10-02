using BepInEx.Configuration;

namespace Dizzy.FirewoodBundle
{
    internal static class FirewoodBundleConfig
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> MaxPieces;
        internal static ConfigEntry<bool> HooksEnabled;
        internal static ConfigEntry<int> MaxHooks;

        internal static bool IsEnabled
        {
            get { return Enabled == null || Enabled.Value; }
        }

        internal static int PieceLimit
        {
            get { return MaxPieces != null ? MaxPieces.Value : 100; }
        }

        internal static bool HooksAreEnabled
        {
            get { return HooksEnabled == null || HooksEnabled.Value; }
        }

        internal static int HookLimit
        {
            get { return MaxHooks != null ? MaxHooks.Value : 20; }
        }

        internal static void Bind(ConfigFile config)
        {
            Enabled = config.Bind(
                "General",
                "Enabled",
                true,
                "Hold a piece of firewood and right-click another piece to glue them into a bundle. Keep adding pieces until the bundle holds MaxPieces.");

            MaxPieces = config.Bind(
                "General",
                "MaxPieces",
                100,
                "Most logs a bundle can hold. They pack in a square grid, as close to square as the count allows. 100 logs is 10 by 10.");

            HooksEnabled = config.Bind(
                "Hooks",
                "Enabled",
                true,
                "Hold a fishing hook and right-click another hook to string them on a line. A line can be hung from a lamp hook.");

            MaxHooks = config.Bind(
                "Hooks",
                "MaxHooks",
                20,
                "Most fishing hooks one line can hold. They hang down the line, one under the next.");
        }
    }
}
