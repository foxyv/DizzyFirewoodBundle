using UnityEngine;

namespace Dizzy.FirewoodBundle
{
    // What can be tied into a two-handed bundle. Each kind only bundles with itself.
    internal sealed class BundleKind
    {
        // The tie colors \ cycles through. Firewood starts on its natural cord brown.
        private static readonly string[] DyeNames = { "Red", "Blue", "Green", "Gold", "White", "Black", "Purple", "Pink" };
        private static readonly Color[] Dyes =
        {
            new Color(0.72f, 0.08f, 0.08f),
            new Color(0.12f, 0.25f, 0.65f),
            new Color(0.12f, 0.45f, 0.18f),
            new Color(0.85f, 0.65f, 0.15f),
            new Color(0.92f, 0.92f, 0.88f),
            new Color(0.06f, 0.06f, 0.06f),
            new Color(0.42f, 0.15f, 0.55f),
            new Color(0.92f, 0.45f, 0.62f)
        };

        internal static readonly BundleKind Firewood = new BundleKind(
            "firewood bundle",
            "firewood",
            "Log",
            "Cord",
            Prepend("Brown", DyeNames),
            Prepend(new Color(0.55f, 0.38f, 0.2f), Dyes),
            false,
            0.97f);

        internal static readonly BundleKind Candle = new BundleKind(
            "candle bundle",
            "candles",
            "Candle",
            "Ribbon",
            DyeNames,
            Dyes,
            true,
            1.02f);

        internal readonly string BundleName;
        internal readonly string Plural;
        internal readonly string Single;
        internal readonly string TieName;
        internal readonly string[] ColorNames;
        internal readonly Color[] Colors;
        internal readonly bool Ribbon;
        internal readonly float Pitch;

        private BundleKind(
            string bundleName,
            string plural,
            string single,
            string tieName,
            string[] colorNames,
            Color[] colors,
            bool ribbon,
            float pitch)
        {
            BundleName = bundleName;
            Plural = plural;
            Single = single;
            TieName = tieName;
            ColorNames = colorNames;
            Colors = colors;
            Ribbon = ribbon;
            Pitch = pitch;
        }

        internal bool IsEnabled
        {
            get { return this == Firewood ? FirewoodBundleConfig.IsEnabled : FirewoodBundleConfig.CandlesAreEnabled; }
        }

        // The count shares the saved amount with the tie color, so it has to stay under the stride.
        internal int Limit
        {
            get
            {
                int limit = this == Firewood ? FirewoodBundleConfig.PieceLimit : FirewoodBundleConfig.CandleLimit;
                return Mathf.Clamp(limit, 2, FirewoodPieces.ColorStride);
            }
        }

        internal int ColorCount
        {
            get { return Colors.Length; }
        }

        internal string GatherPrompt
        {
            get { return "G Bundle " + (this == Firewood ? "Firewood" : "Candles"); }
        }

        internal string LookText(int count)
        {
            return BundleName + "\n" + count + " " + Plural;
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
            get { return Firewood.IsEnabled || Candle.IsEnabled; }
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
            return null;
        }
    }
}
