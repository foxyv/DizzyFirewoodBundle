using BepInEx.Configuration;
using UnityEngine;

namespace Dizzy.FirewoodBundle
{
    internal static class FirewoodBundleConfig
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> MaxPieces;
        internal static ConfigEntry<KeyCode> ColorKey;
        internal static ConfigEntry<bool> CandlesEnabled;
        internal static ConfigEntry<int> MaxCandles;
        internal static ConfigEntry<bool> SausagesEnabled;
        internal static ConfigEntry<int> MaxSausages;
        internal static ConfigEntry<KeyCode> WidthKey;
        internal static ConfigEntry<float> SausageSpacingAmount;
        internal static ConfigEntry<float> PileSteepnessAmount;
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

        internal static KeyCode ColorKeyCode
        {
            get { return ColorKey != null ? ColorKey.Value : KeyCode.Backslash; }
        }

        internal static string ColorKeyLabel
        {
            get { return KeyLabel(ColorKeyCode); }
        }

        internal static string KeyLabel(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Backslash:
                    return "\\";
                case KeyCode.LeftBracket:
                    return "[";
                case KeyCode.RightBracket:
                    return "]";
                default:
                    return key.ToString();
            }
        }

        internal static bool SausagesAreEnabled
        {
            get { return SausagesEnabled == null || SausagesEnabled.Value; }
        }

        internal static int SausageLimit
        {
            get { return MaxSausages != null ? MaxSausages.Value : 20; }
        }

        internal static float SausageSpacing
        {
            get { return SausageSpacingAmount != null ? SausageSpacingAmount.Value : 0.85f; }
        }

        internal static float PileSteepness
        {
            get { return PileSteepnessAmount != null ? Mathf.Max(0.25f, PileSteepnessAmount.Value) : 1f; }
        }

        internal static KeyCode WidthKeyCode
        {
            get { return WidthKey != null ? WidthKey.Value : KeyCode.RightBracket; }
        }

        internal static bool CandlesAreEnabled
        {
            get { return CandlesEnabled == null || CandlesEnabled.Value; }
        }

        internal static int CandleLimit
        {
            get { return MaxCandles != null ? MaxCandles.Value : 24; }
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
                "Glue logs into a bundle when you right-click one log with another. Look at a loose log in an open crate and press G to pick up a bundle of the loose logs. Turn this off to leave firewood alone. Candles and fishing hooks keep working.");

            MaxPieces = config.Bind(
                "General",
                "MaxPieces",
                100,
                "Most logs a bundle can hold. They pack in a square grid, as close to square as the count allows. 100 logs is 10 by 10.");

            ColorKey = config.Bind(
                "General",
                "Tie Color Key",
                KeyCode.Backslash,
                "Hold or look at a firewood or candle bundle and press this key to tie it with the next color. Firewood starts on brown cord, candles on a red ribbon.");

            CandlesEnabled = config.Bind(
                "Candles",
                "Enabled",
                true,
                "Hold a candle and right-click another candle to tie them into a bundle with a red ribbon. Click a candle lantern with the bundle to load one candle. Look at a loose candle in an open crate and press G to pick up a bundle of the loose candles.");

            MaxCandles = config.Bind(
                "Candles",
                "MaxCandles",
                24,
                new ConfigDescription(
                    "Most candles a bundle can hold, up to 999. They pack in a square grid, as close to square as the count allows.",
                    new AcceptableValueRange<int>(2, FirewoodPieces.MaxCount)));

            SausagesEnabled = config.Bind(
                "Sausages",
                "Enabled",
                true,
                "Hold a smoked or dried sausage and right-click another of the same kind to stack them. Only sausages that will not spoil stack. Take one off the stack to eat, cook, salt or slice it. Look at a loose sausage in an open crate and press G to pick up a stack.");

            MaxSausages = config.Bind(
                "Sausages",
                "MaxSausages",
                20,
                new ConfigDescription(
                    "Most sausages one stack can hold, up to 999.",
                    new AcceptableValueRange<int>(2, FirewoodPieces.MaxCount)));

            WidthKey = config.Bind(
                "Sausages",
                "Width Key",
                KeyCode.RightBracket,
                "Hold or look at a sausage stack and press this key to change its shape: Auto, then 1 to 5 wide, then Pile. Width 1 stacks them into a single tower. Pile heaps them in loose crossing layers that grow into a pyramid as sausages are added.");

            SausageSpacingAmount = config.Bind(
                "Sausages",
                "Spacing",
                0.85f,
                new ConfigDescription(
                    "How far apart sausages sit in a stack, as a share of one sausage's size. 1 spaces them by their full size. Lower packs them tighter. A stack picks up the new spacing when it changes: on load, when a sausage is added or taken, or when its width changes.",
                    new AcceptableValueRange<float>(0.5f, 1.5f)));

            PileSteepnessAmount = config.Bind(
                "Sausages",
                "Pile Steepness",
                1f,
                new ConfigDescription(
                    "How steep a sausage pile grows. 1 makes a pyramid about as high as it is wide. 0.5 is a fairly flat pile, 2 a very steep one. A pile picks up the change when it is rebuilt: on load, when a sausage is added or taken, or when its width changes.",
                    new AcceptableValueRange<float>(0.25f, 3f)));

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
