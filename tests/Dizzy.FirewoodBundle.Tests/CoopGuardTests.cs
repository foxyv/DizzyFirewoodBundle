using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests
{
    // In a co-op session another player's held item looks, to this player, like an item they
    // hold themselves. Anything this player can do to a bundle they look at must ask
    // CoopBridge.HeldByOther first (J-005, J-006). The simulator assumes the check is made.
    // These tests read the source to see that it is.
    public class CoopGuardTests
    {
        // Calls that change what a bundle is: its count, color, width, whether it hangs, or
        // that make a new piece out of it.
        private static readonly Regex Writer = new Regex(
            @"\b(WriteCount|WriteColor|Recolor|RestoreSingle|Unhang|MakeHanging|SpawnSingle|SpawnPiece)\s*\(|Builder\s*\.\s*Apply\s*\(|\.Width\s*=[^=]");

        // The low-level pieces that do the changing. Their callers are what must be guarded.
        private static readonly HashSet<string> LowLevelClasses = new HashSet<string>
        {
            "FirewoodBundleBuilder", "HookLineBuilder", "CoopBridge", "Stall",
        };

        private static readonly HashSet<string> LowLevelMethods = new HashSet<string>
        {
            "WriteCount", "WriteColor", "Recolor", "RestoreSingle", "Unhang", "MakeHanging", "SpawnSingle", "SpawnPiece",
            "Apply", "ApplyNow", "Decode", "Encode",
        };

        // Entry points that change a bundle without asking, and why that is safe. Each acts on
        // the item in the player's own hand, or on something that is not a bundle.
        // Anything not here must reach a HeldByOther check.
        private static readonly Dictionary<string, string> OwnHeldOnly = new Dictionary<string, string>
        {
            { "FirewoodPieces.TryFeedOneLog", "Clicking a stove with the bundle in your own hand." },
            { "FirewoodPieces.TryLightLantern", "Clicking a lantern with the bundle in your own hand." },
            { "HookLinePieces.TryBaitRod", "Using the line in your own hand on a rod." },
            { "HookLinePieces.GiveOneToRod", "Using the line in your own hand on a rod." },
            { "SausageHangPatch.Prefix", "Hanging the bundle in your own hand on a lamp hook." },
            { "FirewoodPickupPatch.Postfix", "The item has just been picked up, so it is in the hand of whoever picked it up." },
            { "FirewoodLoadPatch.Postfix", "Rebuilding a bundle as a save loads. No player is acting." },
        };

        private static bool IsEntryPoint(SourceMethod method)
        {
            if (LowLevelClasses.Contains(method.Class) || LowLevelMethods.Contains(method.Name))
                return false;
            if (method.InPatch)
                return method.Name == "Prefix" || method.Name == "Postfix";
            return method.Visibility == "internal" || method.Visibility == "public";
        }

        private static bool Asks(SourceMethod method)
        {
            return method.Body.Contains("HeldByOther") || OwnHeldOnly.ContainsKey(method.Key);
        }

        private static bool Changes(SourceMethod method)
        {
            return Writer.IsMatch(method.Body);
        }

        // The entry points that can reach a change to a bundle but never reach a HeldByOther
        // check (or a method listed as acting only on the player's own hand) on the way. The
        // path shown is one way to a change.
        private static List<KeyValuePair<SourceMethod, List<SourceMethod>>> Unguarded(SourceScan scan)
        {
            var found = new List<KeyValuePair<SourceMethod, List<SourceMethod>>>();
            foreach (SourceMethod entry in scan.Methods.Where(IsEntryPoint))
            {
                List<SourceMethod> path = scan.PathTo(entry, Changes, m => false);
                if (path != null && scan.PathTo(entry, Asks, m => false) == null)
                    found.Add(new KeyValuePair<SourceMethod, List<SourceMethod>>(entry, path));
            }
            return found;
        }

        private static string Describe(KeyValuePair<SourceMethod, List<SourceMethod>> item)
        {
            return item.Key + " reaches a change to a bundle (" + string.Join(" -> ", item.Value.Select(m => m.Key))
                + ") and never asks HeldByOther.";
        }

        [Fact]
        public void EveryWayToChangeABundle_AsksWhetherAnotherPlayerHoldsIt()
        {
            SourceScan scan = SourceScan.OfMod();

            List<string> problems = Unguarded(scan)
                .Where(u => !OwnHeldOnly.ContainsKey(u.Key.Key))
                .Select(Describe)
                .OrderBy(s => s)
                .ToList();

            Assert.True(problems.Count == 0,
                "Another player's held bundle can be changed by:\n  " + string.Join("\n  ", problems));
        }

        [Fact]
        public void TheOwnHeldList_HasNothingOutOfDate()
        {
            SourceScan scan = SourceScan.OfMod();

            foreach (string key in OwnHeldOnly.Keys)
            {
                SourceMethod method = scan.Methods.FirstOrDefault(m => m.Key == key);
                Assert.True(method != null, key + " is on the own-hand list but no longer exists. Take it off.");
                Assert.True(scan.PathTo(method, Changes, m => false) != null, key + " is on the own-hand list but no longer changes a bundle. Take it off.");
                Assert.False(method.Body.Contains("HeldByOther"), key + " is on the own-hand list but now asks HeldByOther. Take it off.");
            }
        }

        [Fact]
        public void TheScan_FindsTheGuardsThatAreKnownToBeThere()
        {
            SourceScan scan = SourceScan.OfMod();

            // J-005: combine, take from and gather.
            foreach (string key in new[] { "FirewoodPieces.TrySplit", "FirewoodPieces.TryGlue", "FirewoodPieces.GatherLookedAtPiece", "HookLinePieces.TrySplit", "HookLinePieces.TryString" })
            {
                SourceMethod method = scan.Methods.FirstOrDefault(m => m.Key == key);
                Assert.True(method != null, key + " was not found, so the scan can't be trusted.");
                Assert.True(scan.PathTo(method, Asks, m => false) != null, key + " no longer reaches a HeldByOther check.");
            }
        }

        [Fact]
        public void TheScan_SeesTheMethodsItNeeds()
        {
            SourceScan scan = SourceScan.OfMod();

            Assert.True(scan.Methods.Count > 200, "Only " + scan.Methods.Count + " methods were found.");
            foreach (string key in new[] { "FirewoodPieces.CycleLookedAtColor", "SausageStacks.CycleLookedAtWidth", "FirewoodPieces.TrySplit", "HookLinePieces.GatherLookedAtHook" })
                Assert.Contains(scan.Methods, m => m.Key == key);
            Assert.Contains(scan.Methods, m => m.InPatch && m.Name == "Prefix");
        }

        // ---- The scan, on source of its own ----

        private static SourceScan Of(string source)
        {
            var scan = new SourceScan();
            scan.Add("Test.cs", source);
            return scan;
        }

        [Fact]
        public void Scan_FindsMethodsAndWhatTheyCall()
        {
            SourceScan scan = Of(@"
namespace N
{
    internal static class A
    {
        internal static void Top() { Helper(); B.Other(); item.Skip(); }
        private static void Helper() { WriteCount(item, 1); }
    }
    internal static class B
    {
        internal static void Other() { }
    }
}");
            SourceMethod top = scan.Methods.Single(m => m.Key == "A.Top");

            Assert.Equal(new[] { "A.Helper", "B.Other" }, scan.Callees(top).Select(m => m.Key).OrderBy(s => s).ToArray());
            Assert.Equal("internal", top.Visibility);
            Assert.Equal("private", scan.Methods.Single(m => m.Key == "A.Helper").Visibility);
        }

        [Fact]
        public void Scan_IgnoresCommentsAndStrings()
        {
            SourceScan scan = Of(@"
internal static class A
{
    internal static void Quiet()
    {
        // WriteCount(item, 1);
        string s = ""WriteCount( { "";
        /* Recolor(x); */
    }
}");
            SourceMethod quiet = scan.Methods.Single(m => m.Key == "A.Quiet");

            Assert.False(Changes(quiet));
        }

        [Fact]
        public void Scan_SeesPatchMethods()
        {
            SourceScan scan = Of(@"
[HarmonyPatch(typeof(Thing), ""Do"")]
internal static class DoPatch
{
    private static bool Prefix(Thing __instance)
    {
        Recolor(__instance);
        return true;
    }
}");
            SourceMethod prefix = scan.Methods.Single();

            Assert.True(prefix.InPatch);
            Assert.True(IsEntryPoint(prefix));
            Assert.True(Changes(prefix));
        }

        [Fact]
        public void Guard_AMethodThatChangesWithoutAsking_IsFound()
        {
            SourceScan scan = Of(@"
internal static class A
{
    internal static void Bad(ShipItem item) { Inner(item); }
    private static void Inner(ShipItem item) { WriteCount(item, 2); }
    internal static void Good(ShipItem item) { if (CoopBridge.HeldByOther(item)) return; Inner(item); }
}");
            SourceMethod bad = scan.Methods.Single(m => m.Key == "A.Bad");
            SourceMethod good = scan.Methods.Single(m => m.Key == "A.Good");

            Assert.NotNull(scan.PathTo(bad, Changes, m => false));
            Assert.Null(scan.PathTo(bad, Asks, m => false));
            Assert.NotNull(scan.PathTo(good, Asks, m => false));
        }
    }
}
