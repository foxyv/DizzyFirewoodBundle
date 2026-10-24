using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests
{
    // Rules the co-op code depends on that nothing at run time checks. Each reads the source,
    // like CoopGuardTests, and each names the bug that follows from breaking it.
    public class CoopSourceRuleTests
    {
        private static readonly HashSet<string> LowLevelClasses = new HashSet<string>
        {
            "FirewoodBundleBuilder", "HookLineBuilder", "CoopBridge", "Stall", "FollowLayer",
        };

        private static IEnumerable<SourceMethod> Game(SourceScan scan)
        {
            return scan.Methods.Where(m => !LowLevelClasses.Contains(m.Class));
        }

        // ---- A new piece must be announced to Coop (J-007, R4, R5) ----

        // Where a piece is made and not announced, and why that is right.
        private static readonly Dictionary<string, string> UnannouncedSpawns = new Dictionary<string, string>
        {
            { "HookLinePieces.GiveOneToRod", "With Better Fishing the rod is handed a hook off the line and uses it up in the same call. It never exists for another player to pick up." },
        };

        [Fact]
        public void EveryPieceMadeFromABundle_IsRegisteredAndAnnounced()
        {
            SourceScan scan = SourceScan.OfMod();

            List<string> problems = Game(scan)
                .Where(m => m.Name != "SpawnSingle" && Regex.IsMatch(m.Body, @"\bSpawnSingle\s*\("))
                .Where(m => !UnannouncedSpawns.ContainsKey(m.Key))
                .Where(m => !m.Body.Contains("RegisterToSave") || !m.Body.Contains("CoopBridge.Spawned("))
                .Select(m => m + " makes a piece but does not both RegisterToSave and call CoopBridge.Spawned. A player who picks the piece up would take the bundle's ID.")
                .ToList();

            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }

        [Fact]
        public void TheUnannouncedSpawnList_HasNothingOutOfDate()
        {
            SourceScan scan = SourceScan.OfMod();

            foreach (string key in UnannouncedSpawns.Keys)
            {
                List<SourceMethod> methods = scan.Methods.Where(m => m.Key == key).ToList();
                Assert.True(methods.Count > 0, key + " is on the unannounced list but no longer exists.");
                Assert.True(methods.Any(m => Regex.IsMatch(m.Body, @"\bSpawnSingle\s*\(")), key + " no longer makes a piece. Take it off the list.");
                Assert.False(methods.Any(m => m.Body.Contains("CoopBridge.Spawned(")), key + " now announces its piece. Take it off the list.");
            }
        }

        // ---- A count is written in one place (J-025) ----

        // Raw writes of a bundle's count, width or color. Everything goes through WriteCount
        // and WriteColor so that the count a change started from is noted first (CountWillChange).
        // These are the places that write the fields themselves, and why.
        private static readonly Dictionary<string, string> RawCountWrites = new Dictionary<string, string>
        {
            { "FirewoodPieces.WriteCount", "It is the writer." },
            { "HookLinePieces.WriteCount", "It is the writer." },
            { "SausageStacks.WriteCount", "It is the writer for food." },
            { "SausageStacks.WriteColor", "It is the writer for food color." },
            { "SausageStacks.MakeHanging", "A single hung on a hook becomes a bundle of one; the count it started from is one." },
            { "SausageStacks.Unhang", "A hung bundle of one goes back to a single." },
            { "SausageStacks.CycleLookedAtWidth", "Changes the width only, and asks HeldByOther." },
            { "CoopBridge.WriteStack", "Applies another player's state." },
            { "CoopBridge.ApplyState", "Applies another player's state." },
            { "CoopBridge.TryRead", "Fills the packet being sent. It is not the item." },
            { "SausageStacks.MergeFood", "Food amount is the weight of the portion, not the count." },
            { "SausageStacks.CopyFood", "Food amount is the weight of the portion, not the count." },
            { "HookLinePieces.TryString", "Puts back the amount it had before WriteCount, when stringing fails. WriteCount noted the count it started from." },
            { "HookLinePieces.AddHeldToLine", "Puts back the amount it had before WriteCount, when adding to the line fails." },
            { "HookLinePieces.FillLine", "Puts back the amounts it had before WriteCount, when filling the line fails." },
        };

        private static readonly Regex RawWrite = new Regex(
            @"\b(?:state|stack)\.(?:Count|Width|Color)\s*=[^=]|\b\w+\.amount\s*=[^=]");

        [Fact]
        public void BundleCountsAreWrittenOnlyWhereTheCountItChangedFromIsNoted()
        {
            SourceScan scan = SourceScan.OfMod();

            List<string> problems = scan.Methods
                .Where(m => RawWrite.IsMatch(m.Body))
                .Where(m => !RawCountWrites.ContainsKey(m.Key))
                .Select(m => m + " writes amount or a stack's count, width or color itself.")
                .ToList();

            Assert.True(problems.Count == 0,
                "A count written outside WriteCount skips CountWillChange, so a change made at the same time as another player's is added wrongly (J-025). Either call WriteCount, or list the method here with a reason:\n  "
                + string.Join("\n  ", problems));
        }

        [Fact]
        public void TheRawWriteList_HasNothingOutOfDate()
        {
            SourceScan scan = SourceScan.OfMod();

            foreach (string key in RawCountWrites.Keys)
            {
                List<SourceMethod> methods = scan.Methods.Where(m => m.Key == key).ToList();
                Assert.True(methods.Count > 0, key + " is on the raw-write list but no longer exists.");
                Assert.True(methods.Any(m => RawWrite.IsMatch(m.Body)), key + " no longer writes a count itself. Take it off the list.");
            }
        }

        [Fact]
        public void BothWritersNoteTheCountBeforeChangingIt()
        {
            SourceScan scan = SourceScan.OfMod();

            foreach (string key in new[] { "FirewoodPieces.WriteCount", "HookLinePieces.WriteCount" })
            {
                foreach (SourceMethod method in scan.Methods.Where(m => m.Key == key && RawWrite.IsMatch(m.Body)))
                {
                    int note = method.Body.IndexOf("CoopBridge.CountWillChange", System.StringComparison.Ordinal);
                    int write = RawWrite.Match(method.Body).Index;
                    Assert.True(note >= 0 && note < write, method + " must call CoopBridge.CountWillChange before it writes the count.");
                }
            }
        }

        // ---- A bundle used up must be remembered (J-026) ----

        // Places that destroy a ShipItem that may already be known to another player.
        // A piece made a moment ago in the same method is not known to anyone yet.
        [Fact]
        public void EveryBundleDestroyed_IsRememberedFirst()
        {
            SourceScan scan = SourceScan.OfMod();

            List<string> problems = Game(scan)
                .Where(m => Regex.IsMatch(m.Body, @"\.DestroyItem\s*\("))
                .Where(m => !m.Body.Contains("CoopBridge.Destroyed("))
                .Select(m => m + " destroys an item without CoopBridge.Destroyed. A pick-up of it that is already on its way would give its ID to another bundle.")
                .ToList();

            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }

        // Object.Destroy of an item's object, which is only right for a piece just spawned in the
        // same method (it failed to set up, so it never existed for anyone else).
        [Fact]
        public void EveryItemObjectDestroyed_WasJustMadeInTheSameMethod()
        {
            SourceScan scan = SourceScan.OfMod();
            var destroyItemObject = new Regex(@"\bObject\.Destroy\s*\(\s*(?!child\b)\w+\.gameObject\s*\)");

            List<string> problems = Game(scan)
                .Where(m => destroyItemObject.IsMatch(m.Body))
                .Where(m => !Regex.IsMatch(m.Body, @"\bSpawnSingle\s*\("))
                .Select(m => m + " destroys an item's object, but did not make it in this method.")
                .ToList();

            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }
    }
}
