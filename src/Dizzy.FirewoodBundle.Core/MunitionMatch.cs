namespace Dizzy.FirewoodBundle
{
    // Loose shot and powder from Dangerous Waters. A crate of shot is a container,
    // not a piece, so its prefab is not one of these.
    public static class MunitionMatch
    {
        public const int Shot4 = 31001;
        public const int Shot12 = 31002;
        public const int Shot18 = 31003;
        public const int Canister = 31004;
        public const int KegSmall = 31005;
        public const int KegLarge = 31006;

        public const int SmallCharges = 25;
        public const int LargeCharges = 100;

        public const string DifferentCalibre = "Cannot bundle round shot of different calibres";
        public const string DifferentKeg = "Cannot bundle different powder kegs";
        public const string OpenedKeg = "Cannot bundle a keg that has been opened";

        public static bool IsRoundShot(int index)
        {
            return index == Shot4 || index == Shot12 || index == Shot18;
        }

        public static bool IsCanister(int index)
        {
            return index == Canister;
        }

        public static bool IsKeg(int index)
        {
            return index == KegSmall || index == KegLarge;
        }

        public static int FullCharges(int index)
        {
            return index == KegLarge ? LargeCharges : SmallCharges;
        }

        // Null when these two pieces can share a bundle. A message when they cannot.
        // Shot of one calibre only joins itself. A keg joins its own size, and only
        // while both still hold a full charge.
        public static string Refusal(int heldIndex, int targetIndex, int heldCharges, int targetCharges)
        {
            if (IsRoundShot(heldIndex) || IsRoundShot(targetIndex))
                return heldIndex == targetIndex ? null : DifferentCalibre;
            if (IsCanister(heldIndex) || IsCanister(targetIndex))
                return heldIndex == targetIndex ? null : DifferentCalibre;
            if (IsKeg(heldIndex) || IsKeg(targetIndex))
            {
                if (heldIndex != targetIndex)
                    return DifferentKeg;
                int full = FullCharges(heldIndex);
                if (heldCharges < full || targetCharges < full)
                    return OpenedKeg;
                return null;
            }

            return null;
        }
    }
}
