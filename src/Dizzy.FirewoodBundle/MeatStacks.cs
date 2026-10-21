using UnityEngine;

namespace Dizzy.FirewoodBundle
{
    // Pork and lamb. Count lives in StackState, because amount is how cooked the meat is.
    // \ changes the net's color. Shapes can be added later.
    internal static class MeatStacks
    {
        internal const string PorkName = "pork";
        internal const string LambName = "lamb";

        internal const string FreshPork = "Only dried or smoked pork goes in a bag";
        internal const string EatenPork = "Cannot bag eaten pork";
        internal const string FreshLamb = "Only dried or smoked lamb goes in a bag";
        internal const string EatenLamb = "Cannot bag eaten lamb";
        internal const string DifferentMeat = "Cannot mix different meats";

        internal static bool IsPork(ShipItem item)
        {
            return FirewoodPieces.KindOf(item) == BundleKind.Pork;
        }

        internal static bool IsLamb(ShipItem item)
        {
            return FirewoodPieces.KindOf(item) == BundleKind.Lamb;
        }

        internal static bool IsMeat(ShipItem item)
        {
            return IsPork(item) || IsLamb(item);
        }

        // Why these two cannot share a bag, or null when they can. A caller that
        // already knows both are meat gets null only when they match.
        internal static string Rejection(ShipItem held, ShipItem target)
        {
            bool pork = IsPork(held) && IsPork(target);
            bool lamb = IsLamb(held) && IsLamb(target);
            if (!pork && !lamb)
                return IsMeat(held) || IsMeat(target) ? DifferentMeat : null;

            string fresh = pork ? FreshPork : FreshLamb;
            string eaten = pork ? EatenPork : EatenLamb;
            if (!SausageStacks.IsPreserved(held) || !SausageStacks.IsPreserved(target))
                return fresh;
            if (!SausageStacks.IsWhole(held) || !SausageStacks.IsWhole(target))
                return eaten;
            return SausageStacks.KindWord(held) != SausageStacks.KindWord(target) ? DifferentMeat : null;
        }

        internal static string HangRefusal(ShipItem item)
        {
            if (IsPork(item))
                return !SausageStacks.IsPreserved(item) ? FreshPork : !SausageStacks.IsWhole(item) ? EatenPork : null;
            if (IsLamb(item))
                return !SausageStacks.IsPreserved(item) ? FreshLamb : !SausageStacks.IsWhole(item) ? EatenLamb : null;
            return null;
        }
    }
}
