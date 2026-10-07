using UnityEngine;

namespace Dizzy.FirewoodBundle
{
    internal enum TieStyle
    {
        Cord,
        Ribbon,
        None
    }

    // What can be tied into a two-handed bundle. Each kind only bundles with itself.
    internal sealed class BundleKind
    {
        // The tie colors \ cycles through. Firewood starts on its natural cord brown.
        private static readonly string[] DyeNames = { "Red", "Blue", "Green", "Gold", "White", "Black", "Purple", "Pink", "Orange" };
        private static readonly Color[] Dyes =
        {
            new Color(0.72f, 0.08f, 0.08f),
            new Color(0.12f, 0.25f, 0.65f),
            new Color(0.12f, 0.45f, 0.18f),
            new Color(0.85f, 0.65f, 0.15f),
            new Color(0.92f, 0.92f, 0.88f),
            new Color(0.06f, 0.06f, 0.06f),
            new Color(0.42f, 0.15f, 0.55f),
            new Color(0.92f, 0.45f, 0.62f),
            new Color(0.9f, 0.42f, 0.08f)
        };

        internal static readonly BundleKind Firewood = new BundleKind(
            "firewood bundle",
            "firewood",
            "Log",
            "Bundle",
            "Cord",
            Prepend("Brown", DyeNames),
            Prepend(new Color(0.55f, 0.38f, 0.2f), Dyes),
            TieStyle.Cord,
            0.97f,
            true);

        internal static readonly BundleKind Candle = new BundleKind(
            "candle bundle",
            "candles",
            "Candle",
            "Bundle",
            "Ribbon",
            DyeNames,
            Dyes,
            TieStyle.Ribbon,
            1.02f,
            true);

        // Food already uses amount for how cooked it is, so a stack keeps its count in
        // a StackState and saves it in the food's spare extra value.
        internal static readonly BundleKind Sausage = new BundleKind(
            "sausage stack",
            "sausages",
            "Sausage",
            "Stack",
            "",
            new string[0],
            new Color[0],
            TieStyle.None,
            0.75f,
            false);

        // Sausages tied by strings to a central string under a lamp hook. Made from a
        // sausage and a hook, not from two sausages, so it never mixes with a stack.
        internal static readonly BundleKind HangingSausage = new BundleKind(
            "hanging sausages",
            "sausages",
            "Sausage",
            "Bunch",
            "",
            new string[0],
            new Color[0],
            TieStyle.None,
            1f,
            false);

        // A loose banana. Bananas never bundle with each other; they only hang.
        internal static readonly BundleKind Banana = new BundleKind(
            "banana",
            "bananas",
            "Banana",
            "Bunch",
            "",
            new string[0],
            new Color[0],
            TieStyle.None,
            1f,
            false);

        // Dried bananas in hands around a stalk under a lamp hook, like a bunch on the
        // tree turned upside down.
        internal static readonly BundleKind HangingBanana = new BundleKind(
            "banana bunch",
            "bananas",
            "Banana",
            "Bunch",
            "",
            new string[0],
            new Color[0],
            TieStyle.None,
            1f,
            false);

        // A loose apple. Two dried apples make a bag.
        internal static readonly BundleKind Apple = new BundleKind(
            "apple",
            "apples",
            "Apple",
            "Bag",
            "",
            new string[0],
            new Color[0],
            TieStyle.None,
            1f,
            false);

        // Dried apples in a net bag, tied at the neck. It hangs from a lamp hook or sits
        // on the deck; \ changes the net's color.
        internal static readonly BundleKind AppleBag = new BundleKind(
            "apple bag",
            "apples",
            "Apple",
            "Bag",
            "Net",
            StartAt("Black", DyeNames),
            StartAt(System.Array.IndexOf(DyeNames, "Black"), Dyes),
            TieStyle.None,
            1f,
            false);

        internal readonly string BundleName;
        internal readonly string Plural;
        internal readonly string Single;
        internal readonly string Group;
        internal readonly string TieName;
        internal readonly string[] ColorNames;
        internal readonly Color[] Colors;
        internal readonly TieStyle Tie;
        internal readonly float Pitch;
        internal readonly bool CountInAmount;

        private BundleKind(
            string bundleName,
            string plural,
            string single,
            string group,
            string tieName,
            string[] colorNames,
            Color[] colors,
            TieStyle tie,
            float pitch,
            bool countInAmount)
        {
            BundleName = bundleName;
            Plural = plural;
            Single = single;
            Group = group;
            TieName = tieName;
            ColorNames = colorNames;
            Colors = colors;
            Tie = tie;
            Pitch = pitch;
            CountInAmount = countInAmount;
        }

        // Gap between pieces as a share of one piece's thickness. Candles take theirs
        // from the config.
        internal float Spacing
        {
            get { return this == Candle ? FirewoodBundleConfig.CandleSpacing : Pitch; }
        }

        internal bool Ribbon
        {
            get { return Tie == TieStyle.Ribbon; }
        }

        internal bool IsEnabled
        {
            get
            {
                if (this == Firewood)
                    return FirewoodBundleConfig.IsEnabled;
                if (this == Candle)
                    return FirewoodBundleConfig.CandlesAreEnabled;
                if (this == HangingSausage)
                    return FirewoodBundleConfig.HangingAreEnabled;
                if (this == Banana || this == HangingBanana)
                    return FirewoodBundleConfig.BananasAreEnabled;
                if (this == Apple || this == AppleBag)
                    return FirewoodBundleConfig.ApplesAreEnabled;
                return FirewoodBundleConfig.SausagesAreEnabled;
            }
        }

        // The count is saved next to the tie color or stack width, 1000 apart, so it stays under 1000.
        internal int Limit
        {
            get
            {
                int limit = this == Firewood
                    ? FirewoodBundleConfig.PieceLimit
                    : this == Candle
                        ? FirewoodBundleConfig.CandleLimit
                        : this == HangingSausage
                            ? FirewoodBundleConfig.HangingLimit
                            : this == HangingBanana || this == Banana
                                ? FirewoodBundleConfig.BananaLimit
                                : this == AppleBag || this == Apple ? FirewoodBundleConfig.AppleLimit : FirewoodBundleConfig.SausageLimit;
                return Mathf.Clamp(limit, 2, FirewoodPieces.MaxCount);
            }
        }

        // Hangs from a lamp hook and stays a bundle even with one piece left.
        internal bool Hangs
        {
            get { return this == HangingSausage || this == HangingBanana || this == AppleBag; }
        }

        // Food whose count lives in a StackState rather than in amount.
        internal bool IsFood
        {
            get { return this == Sausage || this == HangingSausage || this == Banana || this == HangingBanana || this == Apple || this == AppleBag; }
        }

        // The loose piece a hanging kind is made of, and the other way round.
        internal BundleKind Loose
        {
            get { return this == HangingSausage ? Sausage : this == HangingBanana ? Banana : this == AppleBag ? Apple : this; }
        }

        internal BundleKind Hung
        {
            get { return this == Sausage ? HangingSausage : this == Banana ? HangingBanana : this == Apple ? AppleBag : this; }
        }

        // Loose pieces that only ever gather into their hanging kind: bananas into a
        // bunch, apples into a bag.
        internal bool AlwaysHung
        {
            get { return this == Banana || this == Apple; }
        }

        // Kinds that tie two loose pieces together. A banana only hangs.
        internal bool Bundles
        {
            get { return this != Banana; }
        }

        internal int ColorCount
        {
            get { return Colors.Length; }
        }

        internal string GatherPrompt
        {
            get { return "G " + Group + " " + char.ToUpperInvariant(Plural[0]) + Plural.Substring(1); }
        }

        internal string LookText(int count)
        {
            return BundleName + "\n" + count + " " + Plural;
        }

        // The same colors in the same order, starting from the named one, so a new
        // bundle is tied in that color.
        private static string[] StartAt(string first, string[] names)
        {
            return StartAt(System.Array.IndexOf(names, first), names);
        }

        private static T[] StartAt<T>(int first, T[] all)
        {
            var turned = new T[all.Length];
            for (int i = 0; i < all.Length; i++)
                turned[i] = all[(first + i) % all.Length];
            return turned;
        }

        private static T[] Prepend<T>(T first, T[] rest)
        {
            var all = new T[rest.Length + 1];
            all[0] = first;
            rest.CopyTo(all, 1);
            return all;
        }

        internal static bool AnyEnabled
        {
            get { return Firewood.IsEnabled || Candle.IsEnabled || Sausage.IsEnabled || HangingSausage.IsEnabled || Banana.IsEnabled || Apple.IsEnabled; }
        }

        internal static BundleKind Of(ShipItem ship)
        {
            if (ship == null)
                return null;
            // A cargo crate of firewood uses the same name as a loose log.
            if (ship.name == FirewoodPieces.ItemName && !(ship is ShipItemCrate))
                return Firewood;
            ShipItemLanternFuel fuel = ship as ShipItemLanternFuel;
            if (fuel != null && !fuel.oilBottle)
                return Candle;
            if (ship is ShipItemFood && ship.name == SausageStacks.ItemName)
                return SausageStacks.IsHanging(ship) ? HangingSausage : Sausage;
            if (ship is ShipItemFood && ship.name == SausageStacks.BananaName)
                return SausageStacks.IsHanging(ship) ? HangingBanana : Banana;
            if (ship is ShipItemFood && ship.name == SausageStacks.AppleName)
                return SausageStacks.IsHanging(ship) ? AppleBag : Apple;
            return null;
        }
    }
}
