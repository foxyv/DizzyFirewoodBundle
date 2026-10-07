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
        internal static ConfigEntry<float> CandleSpacingAmount;
        internal static ConfigEntry<bool> SausagesEnabled;
        internal static ConfigEntry<int> MaxSausages;
        internal static ConfigEntry<KeyCode> WidthKey;
        internal static ConfigEntry<float> SausageSpacingAmount;
        internal static ConfigEntry<float> PileSteepnessAmount;
        internal static ConfigEntry<bool> HangingEnabled;
        internal static ConfigEntry<int> MaxHanging;
        internal static ConfigEntry<int> BunchSizeAmount;
        internal static ConfigEntry<float> BunchRadiusAmount;
        internal static ConfigEntry<float> BunchDropAmount;
        internal static ConfigEntry<float> HangStringAmount;
        internal static ConfigEntry<float> HangFlareAmount;
        internal static ConfigEntry<bool> BananasEnabled;
        internal static ConfigEntry<int> MaxBananas;
        internal static ConfigEntry<int> BananaHandSizeAmount;
        internal static ConfigEntry<float> BananaHandDropAmount;
        internal static ConfigEntry<float> BananaSplayAmount;
        internal static ConfigEntry<bool> ApplesEnabled;
        internal static ConfigEntry<int> MaxApples;
        internal static ConfigEntry<int> AppleLayerSizeAmount;
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
            get { return SausageSpacingAmount != null ? SausageSpacingAmount.Value : 0.75f; }
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

        internal static bool HangingAreEnabled
        {
            get { return HangingEnabled == null || HangingEnabled.Value; }
        }

        internal static int HangingLimit
        {
            get { return MaxHanging != null ? MaxHanging.Value : 48; }
        }

        internal static int BunchSize
        {
            get { return BunchSizeAmount != null ? Mathf.Max(2, BunchSizeAmount.Value) : 12; }
        }

        internal static float BunchRadius
        {
            get { return BunchRadiusAmount != null ? BunchRadiusAmount.Value : 0.75f; }
        }

        internal static float BunchDrop
        {
            get { return BunchDropAmount != null ? BunchDropAmount.Value : 2f; }
        }

        internal static float HangStringLength
        {
            get { return HangStringAmount != null ? HangStringAmount.Value : 1f; }
        }

        internal static float CandleSpacing
        {
            get { return CandleSpacingAmount != null ? CandleSpacingAmount.Value : 0.9f; }
        }

        internal static float HangFlare
        {
            get { return HangFlareAmount != null ? HangFlareAmount.Value : 0.75f; }
        }

        internal static bool BananasAreEnabled
        {
            get { return BananasEnabled == null || BananasEnabled.Value; }
        }

        internal static int BananaLimit
        {
            get { return MaxBananas != null ? MaxBananas.Value : 45; }
        }

        internal static int BananaHandSize
        {
            get { return BananaHandSizeAmount != null ? Mathf.Max(2, BananaHandSizeAmount.Value) : 8; }
        }

        internal static float BananaHandDrop
        {
            get { return BananaHandDropAmount != null ? BananaHandDropAmount.Value : 1f; }
        }

        internal static float BananaSplay
        {
            get { return BananaSplayAmount != null ? BananaSplayAmount.Value : 35f; }
        }

        internal static bool ApplesAreEnabled
        {
            get { return ApplesEnabled == null || ApplesEnabled.Value; }
        }

        internal static int AppleLimit
        {
            get { return MaxApples != null ? MaxApples.Value : 24; }
        }

        internal static int AppleLayerSize
        {
            get { return AppleLayerSizeAmount != null ? Mathf.Max(2, AppleLayerSizeAmount.Value) : 7; }
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

            CandleSpacingAmount = config.Bind(
                "Candles",
                "Spacing",
                0.9f,
                new ConfigDescription(
                    "How far apart candles sit in a bundle, as a share of one candle's thickness. 1 has them just touching. Lower packs them tighter; higher spreads them out. A bundle picks up the change when it is rebuilt: on load, or when a candle is added or taken.",
                    new AcceptableValueRange<float>(0.5f, 2f)));

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
                "Hold or look at a sausage stack and press this key to change its shape: Auto, then 1 to 5 wide, then Tree, then Pile. Width 1 stacks them into a single tower. Tree lays them in crossing layers that narrow toward the top. Pile drops them onto each other in a loose heap.");

            SausageSpacingAmount = config.Bind(
                "Sausages",
                "Spacing",
                0.75f,
                new ConfigDescription(
                    "How far apart sausages sit in a stack, as a share of one sausage's size. 1 spaces them by their full size. Lower packs them tighter; in a Pile it also lets them settle closer together. A stack picks up the new spacing when it changes: on load, when a sausage is added or taken, or when its shape changes.",
                    new AcceptableValueRange<float>(0.25f, 1.5f)));

            PileSteepnessAmount = config.Bind(
                "Sausages",
                "Pile Steepness",
                1f,
                new ConfigDescription(
                    "How steep a sausage Tree or Pile grows. 1 is the usual shape. 0.5 is fairly flat, 2 very steep. A stack picks up the change when it is rebuilt: on load, when a sausage is added or taken, or when its shape changes.",
                    new AcceptableValueRange<float>(0.25f, 3f)));

            HangingEnabled = config.Bind(
                "Hanging Sausages",
                "Enabled",
                true,
                "Click an empty lamp hook with a smoked or dried sausage to hang it. Right-click the hanging bundle with more sausages of the same kind to add them; they hang in bunches around a central string. Right-click it with empty hands to take one off.");

            MaxHanging = config.Bind(
                "Hanging Sausages",
                "MaxSausages",
                48,
                new ConfigDescription(
                    "Most sausages one hook can hold, up to 999.",
                    new AcceptableValueRange<int>(1, FirewoodPieces.MaxCount)));

            BunchSizeAmount = config.Bind(
                "Hanging Sausages",
                "Bunch Size",
                12,
                new ConfigDescription(
                    "How many sausages hang around the string at one height before a new bunch starts lower down.",
                    new AcceptableValueRange<int>(2, 12)));

            BunchRadiusAmount = config.Bind(
                "Hanging Sausages",
                "Bunch Radius",
                0.75f,
                new ConfigDescription(
                    "Multiplier for how far the sausages in a bunch hang out from the central string. 1 just keeps them from touching.",
                    new AcceptableValueRange<float>(0.5f, 2f)));

            BunchDropAmount = config.Bind(
                "Hanging Sausages",
                "Bunch Drop",
                2f,
                new ConfigDescription(
                    "Multiplier for how much lower each new bunch hangs than the one above it.",
                    new AcceptableValueRange<float>(0.3f, 3f)));

            HangStringAmount = config.Bind(
                "Hanging Sausages",
                "String Length",
                1f,
                new ConfigDescription(
                    "Multiplier for the length of the string from each bunch's knot to its sausages. Longer lets them hang lower under the knot; at its shortest they hang level with the knot.",
                    new AcceptableValueRange<float>(0.2f, 5f)));

            HangFlareAmount = config.Bind(
                "Hanging Sausages",
                "Flare",
                0.75f,
                new ConfigDescription(
                    "How far each sausage leans out from its tie. 1 leans just enough to clear the bunch below. 0 hangs them straight down.",
                    new AcceptableValueRange<float>(0f, 3f)));

            BananasEnabled = config.Bind(
                "Banana Bunches",
                "Enabled",
                true,
                "Click an empty lamp hook with a dried banana to hang it. Right-click the bunch with more dried bananas to add them; they grow from a stalk in hands, like a bunch on the tree turned upside down. Right-click it with empty hands to take one off.");

            MaxBananas = config.Bind(
                "Banana Bunches",
                "MaxBananas",
                45,
                new ConfigDescription(
                    "Most bananas one hook can hold, up to 999.",
                    new AcceptableValueRange<int>(1, FirewoodPieces.MaxCount)));

            BananaHandSizeAmount = config.Bind(
                "Banana Bunches",
                "Hand Size",
                8,
                new ConfigDescription(
                    "How many bananas grow around the stalk at one height before a new hand starts lower down.",
                    new AcceptableValueRange<int>(2, 16)));

            BananaHandDropAmount = config.Bind(
                "Banana Bunches",
                "Hand Drop",
                1f,
                new ConfigDescription(
                    "Multiplier for how much lower each new hand grows than the one above it.",
                    new AcceptableValueRange<float>(0.3f, 3f)));

            BananaSplayAmount = config.Bind(
                "Banana Bunches",
                "Splay",
                35f,
                new ConfigDescription(
                    "Degrees each banana points out from straight down. 0 hangs them straight down.",
                    new AcceptableValueRange<float>(0f, 80f)));

            ApplesEnabled = config.Bind(
                "Apple Bags",
                "Enabled",
                true,
                "Hold a dried apple and right-click another to put them in a net bag, or click an empty lamp hook with a dried apple to start a hanging bag. Right-click the bag with more dried apples to add them, or with empty hands to take one out. Press the tie color key to change the net's color.");

            MaxApples = config.Bind(
                "Apple Bags",
                "MaxApples",
                24,
                new ConfigDescription(
                    "Most apples in one bag, up to 999.",
                    new AcceptableValueRange<int>(1, FirewoodPieces.MaxCount)));

            AppleLayerSizeAmount = config.Bind(
                "Apple Bags",
                "Layer Size",
                7,
                new ConfigDescription(
                    "How many apples sit in the bottom layer of a bag. Each layer above holds one fewer.",
                    new AcceptableValueRange<int>(2, 12)));

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
