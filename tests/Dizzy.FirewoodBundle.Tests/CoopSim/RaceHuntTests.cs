using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Dizzy.FirewoodBundle.Tests.CoopSim
{
    // Hunts for the races that still end with the peers disagreeing, and cuts each down to the
    // few actions that cause it.
    public class RaceHuntTests
    {
        private readonly ITestOutputHelper _output;

        public RaceHuntTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Shrinker_CutsAFailingRunDown_AndWhatIsLeftStillFails()
        {
            Scenario found = null;
            for (int seed = 0; seed < 200 && found == null; seed++)
            {
                Scenario s = Scenario.Generate(seed, 40, 3);
                if (s.Fails())
                    found = s;
            }
            Assert.NotNull(found);

            Scenario small = found.Shrink();

            Assert.True(small.Fails());
            Assert.True(small.ActCount <= found.ActCount);
            Assert.True(small.ActCount <= 8, "Shrunk to " + small.ActCount + " acts:\n" + small);
        }

        [Fact]
        public void Shrinker_LeavesAPassingRunAlone()
        {
            Scenario passing = Scenario.Generate(1, 5, 1);
            // One act at a time never disagrees (S1), so there is nothing to shrink.
            Assert.False(passing.Fails());
        }

        [Fact]
        public void ReplayingAScenario_GivesTheSameResult()
        {
            Scenario s = Scenario.Generate(3, 30, 3);
            Assert.Equal(s.Run().Summary(), s.Run().Summary());
        }

        // J-029: every change sends the whole bundle, and once nobody decided which of two
        // changes made at once wins, each side took the other's packet and the two swapped.
        // The host now settles the bundle and tells everyone, the sender too.
        [Fact]
        public void J29_TwoPlayersChangeOneBundleAtOnce_EndTheSame_InEveryDeliveryOrder()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                SimNetwork net = Scenario.Start();
                SimItem bundle = net.Host.Create(10, SimKind.Candle, 1, 4);
                net.Settle();
                net.Host.Recolor(bundle, 2);
                net.Peers[1].SetBundle(net.Peers[1].Items[bundle.Id], 6);
                net.Peers[2].Recolor(net.Peers[2].Items[bundle.Id], 3);
                net.SettleInterleaved(seed);
                Assert.True(net.Differences().Count == 0, "seed " + seed + ": " + net.Summary());
            }
        }

        [Fact]
        public void J29_WithoutTheHostSettling_TheSameChangesEndApart()
        {
            int apart = 0;
            for (int seed = 0; seed < 50; seed++)
            {
                SimNetwork net = Scenario.Start(new SimRules { HostArbitrates = false });
                SimItem bundle = net.Host.Create(10, SimKind.Candle, 1, 4);
                net.Settle();
                net.Host.Recolor(bundle, 2);
                net.Peers[1].SetBundle(net.Peers[1].Items[bundle.Id], 6);
                net.Peers[2].Recolor(net.Peers[2].Items[bundle.Id], 3);
                net.SettleInterleaved(seed);
                if (net.Differences().Count > 0)
                    apart++;
            }
            Assert.True(apart > 0);
        }

        // Known limit: health travels in Coop's own item update, which the host does not
        // settle. A health change at the same moment as a bundle change can still end apart.
        [Fact]
        public void KnownLimit_HealthChangedAtTheSameMomentAsABundleChange_CanEndApart()
        {
            int apart = 0;
            for (int seed = 0; seed < 20; seed++)
            {
                SimNetwork net = Scenario.Start();
                SimItem bundle = net.Host.Create(10, SimKind.Candle, 1, 4);
                net.Settle();
                net.Host.Recolor(bundle, 2);
                net.Peers[1].ChangeHealth(net.Peers[1].Items[bundle.Id], 0.3f);
                net.SettleInterleaved(seed);
                if (net.Differences().Count > 0)
                    apart++;
            }
            _output.WriteLine(apart + " of 20 delivery orders ended apart.");
            Assert.True(apart > 0, "Fixed? Then turn this into a regression test and drop the known limit.");
        }

        // J-030: one player used an item up while another picked it up. A third player heard of
        // the destroy first, then the pick-up of an item that is gone, and gave that ID to the
        // nearest item of the same prefab. Remembering remote destroys stops that.
        [Fact]
        public void J30_APickUpOfAnItemAnotherPlayerUsedUp_IsIgnoredOnEveryScreen()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                SimNetwork net = Scenario.Start();
                SimItem gone = net.Host.Create(10, SimKind.Candle, 1, 1);
                net.Peers[1].Create(10, SimKind.Candle, 3, 1);
                net.Settle();
                net.Peers[2].PickUp(net.Peers[2].Items[gone.Id]);
                net.Host.Destroy(net.Host.Items[gone.Id]);
                net.SettleInterleaved(seed);
                Assert.True(net.Differences().Count == 0, "seed " + seed + ": " + net.Summary());
            }
        }

        [Fact]
        public void J30_WithoutRememberingRemoteDestroys_TheThirdPlayerRenamesAnotherItem()
        {
            int apart = 0;
            for (int seed = 0; seed < 30; seed++)
            {
                SimNetwork net = Scenario.Start(new SimRules { RememberRemoteDestroys = false });
                SimItem gone = net.Host.Create(10, SimKind.Candle, 1, 1);
                net.Peers[1].Create(10, SimKind.Candle, 3, 1);
                net.Settle();
                net.Peers[2].PickUp(net.Peers[2].Items[gone.Id]);
                net.Host.Destroy(net.Host.Items[gone.Id]);
                net.SettleInterleaved(seed);
                if (net.Differences().Count > 0)
                    apart++;
            }
            Assert.True(apart > 0);
        }

        // Prints each distinct race. Not an assertion: it is how the races were found.
        [Fact]
        public void Hunt_ListsTheDistinctRaces()
        {
            var seen = new Dictionary<string, Scenario>();
            int failing = 0;
            const int Runs = 400;
            for (int seed = 0; seed < Runs; seed++)
            {
                Scenario s = Scenario.Generate(seed, 30, 3);
                if (!s.Fails())
                    continue;
                failing++;
                Scenario small = s.Shrink();
                string signature = string.Join(" + ", small.Batches.SelectMany(b => b.Acts).Select(a => a.Op.ToString()).OrderBy(x => x));
                if (!seen.ContainsKey(signature))
                    seen[signature] = small;
            }

            _output.WriteLine(failing + " of " + Runs + " random runs ended with the peers disagreeing. " + seen.Count + " distinct shapes:");
            foreach (KeyValuePair<string, Scenario> race in seen.OrderBy(r => r.Value.ActCount))
            {
                _output.WriteLine("");
                _output.WriteLine("== " + race.Key);
                _output.WriteLine(race.Value.ToString());
                _output.WriteLine("  ends: " + race.Value.Run().Summary());
            }
        }
    }
}
