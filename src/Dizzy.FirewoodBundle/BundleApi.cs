namespace Dizzy.FirewoodBundle
{
    // For other mods that handle single items and meet a bundle. Call it by reflection, so
    // that Dizzy Bundle Up stays optional:
    //
    //   Type api = Type.GetType("Dizzy.FirewoodBundle.BundleApi, Dizzy.FirewoodBundle");
    //   bool bundled = (bool)api.GetMethod("IsFoodBundle").Invoke(null, new object[] { food });
    //   object piece = api.GetMethod("TakeOneFood").Invoke(null, new object[] { food });
    public static class BundleApi
    {
        // True for food that is a bundle and not a single piece: a stack, a bunch, a bag,
        // a rack or a wheel, whether or not it hangs.
        public static bool IsFoodBundle(ShipItemFood food)
        {
            return FirewoodPieces.IsBundledFood(food);
        }

        // Takes one piece off a bundle of food that nobody holds, and returns it as a loose
        // single. The bundle keeps the rest and is redrawn. Returns null when the item is not
        // a bundle of two or more, or somebody is holding it. The piece is a normal item.
        public static ShipItemFood TakeOneFood(ShipItemFood food)
        {
            return FirewoodPieces.TakePieceOff(food);
        }
    }
}
