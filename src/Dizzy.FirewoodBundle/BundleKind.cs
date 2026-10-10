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
        // The built-in tie colors \ cycles through. Each is a numbered color in the
        // config, where players can rename and recolor them and fill in spare ones of
        // their own. A bundle saves its color as a place in its kind's list, so a color
        // keeps its number for good.
        internal static readonly string[] DefaultDyeNames = { "Red", "Blue", "Green", "Gold", "White", "Black", "Purple", "Pink", "Orange", "Tan", "Brown" };
        internal static readonly Color[] DefaultDyes =
        {
            new Color(0.72f, 0.08f, 0.08f),
            new Color(0.12f, 0.25f, 0.65f),
            new Color(0.12f, 0.45f, 0.18f),
            new Color(0.85f, 0.65f, 0.15f),
            new Color(0.92f, 0.92f, 0.88f),
            new Color(0.06f, 0.06f, 0.06f),
            new Color(0.42f, 0.15f, 0.55f),
            new Color(0.92f, 0.45f, 0.62f),
            new Color(0.9f, 0.42f, 0.08f),
            new Color(0.82f, 0.7f, 0.52f),
            new Color(0.34f, 0.2f, 0.1f)
        };

        // Firewood's own cord, before any dye. It comes first on a firewood bundle, and
        // every tie color follows it.
        private const string NaturalName = "Natural";
        private static readonly Color NaturalCord = new Color(0.55f, 0.38f, 0.2f);
        // A bag's net starts on the sixth tie color, black unless a player changes it,
        // and wraps round the first nine. Colors after those follow in order.
        private const int BagFirst = 5;
        private const int BagWrap = 9;

        internal static readonly BundleKind Firewood = new BundleKind(
            "firewood bundle",
            "firewood",
            "Log",
            "Bundle",
            "Cord",
            Prepend(NaturalName, DefaultDyeNames),
            Prepend(NaturalCord, DefaultDyes),
            TieStyle.Cord,
            0.97f,
            true);

        internal static readonly BundleKind Candle = new BundleKind(
            "candle bundle",
            "candles",
            "Candle",
            "Bundle",
            "Ribbon",
            DefaultDyeNames,
            DefaultDyes,
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
            BagOrder(DefaultDyeNames),
            BagOrder(DefaultDyes),
            TieStyle.None,
            1f,
            false);

        // A loose orange. Two dried oranges make a bag, the same as apples.
        internal static readonly BundleKind Orange = new BundleKind(
            "orange",
            "oranges",
            "Orange",
            "Bag",
            "",
            new string[0],
            new Color[0],
            TieStyle.None,
            1f,
            false);

        internal static readonly BundleKind OrangeBag = new BundleKind(
            "orange bag",
            "oranges",
            "Orange",
            "Bag",
            "Net",
            BagOrder(DefaultDyeNames),
            BagOrder(DefaultDyes),
            TieStyle.None,
            1f,
            false);

        // A wedge of cheese. Dried or smoked wedges sit point to point as a wheel, and
        // full wheels stack. Goat cheese is already a round, so it is not one of these.
        internal static readonly BundleKind Cheese = new BundleKind(
            "cheese wheel",
            "cheese",
            "Wedge",
            "Wheel",
            "",
            new string[0],
            new Color[0],
            TieStyle.None,
            1f,
            false);

        // A round of goat cheese. Dried or smoked rounds go in a net bag, like fruit.
        internal static readonly BundleKind GoatCheese = new BundleKind(
            "goat cheese",
            "goat cheeses",
            "Goat Cheese",
            "Bag",
            "",
            new string[0],
            new Color[0],
            TieStyle.None,
            1f,
            false);

        internal static readonly BundleKind GoatCheeseBag = new BundleKind(
            "goat cheese bag",
            "goat cheeses",
            "Goat Cheese",
            "Bag",
            "Net",
            BagOrder(DefaultDyeNames),
            BagOrder(DefaultDyes),
            TieStyle.None,
            1f,
            false);

        // A date skewer: the game's date is a stick with three dates on it. Dried
        // skewers hang from a rack, which two of them make in hand or one starts on a hook.
        internal static readonly BundleKind Date = new BundleKind(
            "date",
            "dates",
            "Date",
            "Rack",
            "",
            new string[0],
            new Color[0],
            TieStyle.None,
            1f,
            false);

        // Date skewers on strings under a lamp hook, like hanging sausages.
        internal static readonly BundleKind HangingDate = new BundleKind(
            "date rack",
            "dates",
            "Date",
            "Rack",
            "",
            new string[0],
            new Color[0],
            TieStyle.None,
            1f,
            false);

        internal readonly string BundleName;
        internal readonly string Plural;
        internal readonly string Single;
        internal readonly string Group;
        internal readonly string TieName;
        internal string[] ColorNames;
        internal Color[] Colors;
        // Which colors the color key stops on. A color a player has left unnamed is
        // skipped, but keeps its place, so a bundle already tied in it still shows it.
        internal bool[] ColorUsed;
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
                if (this == Orange || this == OrangeBag)
                    return FirewoodBundleConfig.OrangesAreEnabled;
                if (this == Date || this == HangingDate)
                    return FirewoodBundleConfig.DatesAreEnabled;
                if (this == Cheese)
                    return FirewoodBundleConfig.CheeseIsEnabled;
                if (this == GoatCheese || this == GoatCheeseBag)
                    return FirewoodBundleConfig.GoatCheeseIsEnabled;
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
                                : this == AppleBag || this == Apple
                                    ? FirewoodBundleConfig.AppleLimit
                                    : this == OrangeBag || this == Orange
                                        ? FirewoodBundleConfig.OrangeLimit
                                        : this == Date || this == HangingDate
                                            ? FirewoodBundleConfig.DateLimit
                                            : this == Cheese
                                                ? FirewoodBundleConfig.CheeseLimit
                                                : this == GoatCheese || this == GoatCheeseBag ? FirewoodBundleConfig.GoatCheeseLimit : FirewoodBundleConfig.SausageLimit;
                return Mathf.Clamp(limit, 2, FirewoodPieces.MaxCount);
            }
        }

        // Hangs from a lamp hook and stays a bundle even with one piece left.
        internal bool Hangs
        {
            get { return this == HangingSausage || this == HangingBanana || this == AppleBag || this == OrangeBag || this == HangingDate || this == GoatCheeseBag; }
        }

        // Food whose count lives in a StackState rather than in amount.
        internal bool IsFood
        {
            get { return this == Sausage || this == HangingSausage || this == Banana || this == HangingBanana || this == Apple || this == AppleBag || this == Orange || this == OrangeBag || this == Date || this == HangingDate || this == Cheese || this == GoatCheese || this == GoatCheeseBag; }
        }

        // The loose piece a hanging kind is made of, and the other way round.
        internal BundleKind Loose
        {
            get { return this == HangingSausage ? Sausage : this == HangingBanana ? Banana : this == AppleBag ? Apple : this == OrangeBag ? Orange : this == HangingDate ? Date : this == GoatCheeseBag ? GoatCheese : this; }
        }

        internal BundleKind Hung
        {
            get { return this == Sausage ? HangingSausage : this == Banana ? HangingBanana : this == Apple ? AppleBag : this == Orange ? OrangeBag : this == Date ? HangingDate : this == GoatCheese ? GoatCheeseBag : this; }
        }

        // Fruit, or goat cheese, in a net bag.
        internal bool IsBag
        {
            get { return this == AppleBag || this == OrangeBag || this == GoatCheeseBag; }
        }

        // Food that two loose pieces make into a stack in hand: a sausage stack, or a
        // wheel of cheese.
        internal bool Stacks
        {
            get { return this == Sausage || this == Cheese; }
        }

        // Loose pieces that only ever gather into their hanging kind: bananas into a
        // bunch, apples into a bag.
        internal bool AlwaysHung
        {
            get { return this == Banana || this == Apple || this == Orange || this == Date || this == GoatCheese; }
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

        // The tie colors in the order a bag's net cycles through them.
        private static T[] BagOrder<T>(T[] all)
        {
            int wrap = Mathf.Min(BagWrap, all.Length);
            int first = all.Length > BagFirst ? BagFirst : 0;
            var ordered = new T[all.Length];
            for (int i = 0; i < all.Length; i++)
                ordered[i] = i < wrap ? all[(first + i) % wrap] : all[i];
            return ordered;
        }

        // The kinds whose tie or net takes a color.
        internal static BundleKind[] Dyed
        {
            get { return new[] { Firewood, Candle, AppleBag, OrangeBag, GoatCheeseBag }; }
        }

        // Swaps in the tie colors from the config.
        internal static void SetDyes(string[] names, Color[] colors, bool[] used)
        {
            if (names == null || colors == null || used == null || names.Length == 0)
                return;
            if (names.Length != colors.Length || names.Length != used.Length)
                return;
            Firewood.ColorNames = Prepend(NaturalName, names);
            Firewood.Colors = Prepend(NaturalCord, colors);
            Firewood.ColorUsed = Prepend(true, used);
            Candle.ColorNames = names;
            Candle.Colors = colors;
            Candle.ColorUsed = used;
            BundleKind[] bags = { AppleBag, OrangeBag, GoatCheeseBag };
            for (int i = 0; i < bags.Length; i++)
            {
                bags[i].ColorNames = BagOrder(names);
                bags[i].Colors = BagOrder(colors);
                bags[i].ColorUsed = BagOrder(used);
            }
        }

        // The color the color key moves to from this one: the next one a player has not
        // left unnamed. The same color comes back when there is no other.
        internal int NextColor(int current)
        {
            int count = ColorCount;
            for (int step = 1; step <= count; step++)
            {
                int next = (current + step) % count;
                if (ColorUsed == null || next >= ColorUsed.Length || ColorUsed[next])
                    return next;
            }
            return current;
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
            get { return Firewood.IsEnabled || Candle.IsEnabled || Sausage.IsEnabled || HangingSausage.IsEnabled || Banana.IsEnabled || Apple.IsEnabled || Orange.IsEnabled || Date.IsEnabled || Cheese.IsEnabled || GoatCheese.IsEnabled; }
        }

        private static int PrefabIndex(ShipItem ship)
        {
            SaveablePrefab saveable = ship.GetComponent<SaveablePrefab>();
            return saveable != null ? saveable.prefabIndex : -1;
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
            if (ship is ShipItemFood && ship.name == SausageStacks.OrangeName)
                return SausageStacks.IsHanging(ship) ? OrangeBag : Orange;
            // A slice of cheese is named "cheese slice", so the name alone picks out the wedge.
            if (ship is ShipItemFood && ship.name == SausageStacks.CheeseName)
                return Cheese;
            if (ship is ShipItemFood && ship.name == SausageStacks.GoatCheeseName)
                return SausageStacks.IsHanging(ship) ? GoatCheeseBag : GoatCheese;
            // The date's item name is not "date", so it is known by its prefab number.
            if (ship is ShipItemFood && (ship.name == SausageStacks.DateName || PrefabIndex(ship) == SausageStacks.DatePrefab))
                return SausageStacks.IsHanging(ship) ? HangingDate : Date;
            return null;
        }
    }
}
