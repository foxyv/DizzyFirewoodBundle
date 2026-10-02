using BepInEx.Configuration;

namespace Dizzy.FirewoodBundle
{
    internal static class FirewoodBundleConfig
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> MaxPieces;
        internal static ConfigEntry<bool> HooksEnabled;
        internal static ConfigEntry<int> MaxHooks;
        internal static ConfigEntry<float> HookMessiness;
        internal static ConfigEntry<float> HookFlareAmount;
        internal static ConfigEntry<float> HookSpacingAmount;
        internal static ConfigEntry<float> HookLineLengthAmount;

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

        internal static float HookScatter
        {
            get { return HookMessiness != null ? HookMessiness.Value : 1f; }
        }

        internal static float HookFlare
        {
            get { return HookFlareAmount != null ? HookFlareAmount.Value : 1f; }
        }

        internal static float HookSpacing
        {
            get { return HookSpacingAmount != null ? HookSpacingAmount.Value : 1f; }
        }

        internal static float HookLineLength
        {
            get { return HookLineLengthAmount != null ? HookLineLengthAmount.Value : 1f; }
        }

        internal static void Bind(ConfigFile config)
        {
            Enabled = config.Bind(
                "General",
                "Stack Firewood",
                LegacyBool(config, "General", "Enabled", true),
                "Glue logs into a bundle when you right-click one log with another. Turn this off to leave firewood alone. Fishing hooks keep working.");

            MaxPieces = config.Bind(
                "General",
                "MaxPieces",
                100,
                "Most logs a bundle can hold. They pack in a square grid, as close to square as the count allows. 100 logs is 10 by 10.");

            HooksEnabled = config.Bind(
                "Hooks",
                "Enabled",
                true,
                "Hold a fishing hook and right-click another hook to string them on a line. A line can be hung from a lamp hook. Look at a loose hook in an open crate and press G to bundle the loose hooks in that crate.");

            MaxHooks = config.Bind(
                "Hooks",
                "MaxHooks",
                20,
                "Most fishing hooks one line can hold. They hang down the line, one under the next.");

            HookMessiness = config.Bind(
                "Hooks",
                "Hook Messiness",
                LegacyFloat(config, "Messiness", 1f),
                new ConfigDescription(
                    "Multiplier for how far each hook is shifted and tilted off a neat line. 0 hangs them evenly. 1 is the usual scatter.",
                    new AcceptableValueRange<float>(0f, 3f)));

            HookFlareAmount = config.Bind(
                "Hooks",
                "Hook Flare",
                LegacyFloat(config, "Flare", 1f),
                new ConfigDescription(
                    "How far the hook points swing from the string. Positive values flare them out. Negative values bunch them in. 0 hangs them straight down. The hooks at the ends move farthest.",
                    new AcceptableValueRange<float>(-3f, 3f)));

            HookSpacingAmount = config.Bind(
                "Hooks",
                "Spacing",
                1f,
                new ConfigDescription(
                    "Multiplier for the gap between hooks on a line. 1 is the usual gap. Lower packs them tighter. Higher spreads them out.",
                    new AcceptableValueRange<float>(0.4f, 3f)));

            HookLineLengthAmount = config.Bind(
                "Hooks",
                "Line Length",
                1f,
                new ConfigDescription(
                    "Multiplier for how far the hooks hang below the top of the string. 1 is the usual length. Lower shortens the hanger. Higher drops the hooks farther down.",
                    new AcceptableValueRange<float>(0.4f, 3f)));
        }

        private static bool LegacyBool(ConfigFile config, string section, string key, bool fallback)
        {
            ConfigEntry<bool> oldEntry;
            if (!config.TryGetEntry(section, key, out oldEntry))
                return fallback;

            fallback = oldEntry.Value;
            config.Remove(oldEntry.Definition);
            return fallback;
        }

        private static float LegacyFloat(ConfigFile config, string key, float fallback)
        {
            ConfigEntry<float> oldEntry;
            if (!config.TryGetEntry("Hooks", key, out oldEntry))
                return fallback;

            fallback = oldEntry.Value;
            config.Remove(oldEntry.Definition);
            return fallback;
        }
    }
}
