using BepInEx.Configuration;

namespace Dizzy.FirewoodBundle
{
    internal static class FirewoodBundleConfig
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> MaxPieces;

        internal static bool IsEnabled
        {
            get { return Enabled == null || Enabled.Value; }
        }

        internal static int PieceLimit
        {
            get { return MaxPieces != null ? MaxPieces.Value : 100; }
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
        }
    }
}
