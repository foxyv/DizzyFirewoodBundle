using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.FirewoodBundle
{
    // Loose round shot from Dangerous Waters. Canister, kegs and shot crates stay
    // as they are. A bundle is still one shot item, with the count in amount.
    // Loading a gun spends one ball and leaves the rest.
    internal static class Munitions
    {
        internal const string PluginGuid = "com.roy.dangerouswaters";

        internal static bool Loaded
        {
            get { return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(PluginGuid); }
        }

        internal static void Patch(Harmony harmony)
        {
            if (!Loaded || harmony == null)
                return;

            Type native = AccessTools.TypeByName("DangerousWaters.DWNativeItem");
            if (native == null)
            {
                Plugin.Log.LogWarning("Dangerous Waters is loaded, but its item helper was not found. Loading a gun would spend a whole bundle.");
                return;
            }

            MethodInfo letGo = AccessTools.Method(native, "LetGo");
            MethodInfo discard = AccessTools.Method(native, "Discard");
            if (letGo != null)
                harmony.Patch(letGo, prefix: new HarmonyMethod(typeof(Munitions), nameof(KeepHeld)));
            if (discard != null)
                harmony.Patch(discard, prefix: new HarmonyMethod(typeof(Munitions), nameof(PeelOne)));

            Type serve = AccessTools.TypeByName("DangerousWaters.DWServeGun");
            MethodInfo spend = serve != null ? AccessTools.Method(serve, "Spend") : null;
            if (spend != null)
                harmony.Patch(spend, prefix: new HarmonyMethod(typeof(Munitions), nameof(KeepInStores)));
        }

        internal static BundleKind KindOf(ShipItem item)
        {
            if (!Loaded || item == null)
                return null;
            int index = IndexOf(item);
            if (MunitionMatch.IsRoundShot(index))
                return BundleKind.RoundShot;
            return null;
        }

        internal static bool Handles(ShipItem item)
        {
            BundleKind kind = BundleKind.Of(item);
            return kind != null && kind.IsMunition;
        }

        internal static string Rejection(ShipItem held, ShipItem target)
        {
            return MunitionMatch.Refusal(IndexOf(held), IndexOf(target), 0, 0);
        }

        internal static string Label(ShipItem item, int count)
        {
            BundleKind kind = BundleKind.Of(item);
            string name = PieceName(item);
            if (kind == null)
                return name;
            return name + " bundle\n" + count + " " + kind.Plural;
        }

        // Serve-the-guns takes the whole item out of its crate and destroys it.
        // A bundle stays where it is and gives up one piece.
        private static bool KeepInStores(Component it)
        {
            if (it == null || !WillPeel(it.gameObject))
                return true;
            PeelOne(it.gameObject);
            return false;
        }

        // LetGo drops whatever the gun just loaded. A bundle stays in hand; one
        // piece comes off it in PeelOne, which runs next.
        private static bool KeepHeld(GameObject go)
        {
            return !WillPeel(go);
        }

        // Discard destroys the whole item. A bundle gives up one piece instead.
        private static bool PeelOne(GameObject go)
        {
            ShipItem item = ItemOf(go);
            if (!WillPeel(item))
                return true;

            BundleKind kind = BundleKind.Of(item);
            int count = FirewoodPieces.CountOf(item);
            FirewoodPieces.WriteCount(item, count - 1);

            int left = FirewoodPieces.CountOf(item);
            if (left <= 1)
                FirewoodBundleBuilder.RestoreSingle(item);
            else
                FirewoodBundleBuilder.Apply(item);

            string leftText = left <= 1 ? "1 left." : left + " left.";
            Plugin.Log.LogInfo("Loaded one from the " + (kind != null ? kind.BundleName : "bundle") + ". " + leftText);
            SausageStacks.Notify("Loaded one. " + leftText);
            return false;
        }

        private static bool WillPeel(GameObject go)
        {
            return WillPeel(ItemOf(go));
        }

        private static bool WillPeel(ShipItem item)
        {
            if (item == null)
                return false;
            BundleKind kind = BundleKind.Of(item);
            return kind != null && kind.IsMunition && FirewoodPieces.CountOf(item) > 1;
        }

        private static string PieceName(ShipItem item)
        {
            ShipItem prefab = FirewoodPieces.PrefabOf(item);
            string source = prefab != null && !string.IsNullOrEmpty(prefab.lookText) ? prefab.lookText : null;
            if (string.IsNullOrEmpty(source) && prefab != null)
                source = prefab.name;
            if (string.IsNullOrEmpty(source) && item != null)
                source = item.name;
            if (string.IsNullOrEmpty(source))
                return "round shot";
            int line = source.IndexOf('\n');
            if (line >= 0)
                source = source.Substring(0, line);
            const string clone = "(Clone)";
            int cut = source.IndexOf(clone, System.StringComparison.Ordinal);
            if (cut >= 0)
                source = source.Substring(0, cut);
            return source.Trim();
        }

        private static ShipItem ItemOf(GameObject go)
        {
            return go != null ? go.GetComponent<ShipItem>() : null;
        }

        private static int IndexOf(ShipItem item)
        {
            if (item == null)
                return -1;
            SaveablePrefab saveable = item.GetComponent<SaveablePrefab>();
            return saveable != null ? saveable.prefabIndex : -1;
        }
    }
}
