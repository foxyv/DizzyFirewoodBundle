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

        // The fixes are what keep random runs from failing, so the shrinker is tried with one off.
        private static SimRules Weak { get { return new SimRules { HostArbitrates = false }; } }

        [Fact]
        public void Shrinker_CutsAFailingRunDown_AndWhatIsLeftStillFails()
        {
            Scenario found = null;
            for (int seed = 0; seed < 200 && found == null; seed++)
            {
                Scenario s = Scenario.Generate(seed, 40, 3);
                if (s.Fails(Weak))
                    found = s;
            }
            Assert.NotNull(found);

            Scenario small = found.Shrink(Weak);

            Assert.True(small.Fails(Weak));
            Assert.True(small.ActCount <= found.ActCount);
            Assert.True(small.ActCount * 3 <= found.ActCount, "Shrunk to " + small.ActCount + " acts:\n" + small);
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

        // J-031: health and amount travel in Coop's own packets, which the host only passed on.
        // A health change at the same moment as a bundle change could end apart. The host now
        // settles the bundle after applying one.
        [Fact]
        public void J31_HealthChangedAtTheSameMomentAsABundleChange_EndsTheSame()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                SimNetwork net = Scenario.Start();
                SimItem bundle = net.Host.Create(10, SimKind.Candle, 1, 4);
                net.Settle();
                net.Host.Recolor(bundle, 2);
                net.Peers[1].ChangeHealth(net.Peers[1].Items[bundle.Id], 0.3f);
                net.Peers[2].Recolor(net.Peers[2].Items[bundle.Id], 3);
                net.SettleInterleaved(seed);
                Assert.True(net.Differences().Count == 0, "seed " + seed + ": " + net.Summary());
            }
        }

        [Fact]
        public void J31_WithoutTheHostSettlingHealth_TheSameChangesEndApart()
        {
            int apart = 0;
            for (int seed = 0; seed < 50; seed++)
            {
                SimNetwork net = Scenario.Start(new SimRules { HostSettlesHealth = false });
                SimItem bundle = net.Host.Create(10, SimKind.Candle, 1, 4);
                net.Settle();
                net.Host.Recolor(bundle, 2);
                net.Peers[1].ChangeHealth(net.Peers[1].Items[bundle.Id], 0.3f);
                net.Peers[2].Recolor(net.Peers[2].Items[bundle.Id], 3);
                net.SettleInterleaved(seed);
                if (net.Differences().Count > 0)
                    apart++;
            }
            Assert.True(apart > 0);
        }

        // J-030: one guest used an item up while another picked it up. The host heard of the
        // destroy first, then the request to pick up an item that is gone, and gave that ID to
        // the nearest item of the same prefab. Remembering remote destroys stops that.
        [Fact]
        public void J30_APickUpOfAnItemAnotherPlayerUsedUp_IsIgnoredOnEveryScreen()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                SimNetwork net = Scenario.Start();
                SimItem gone = net.Host.Create(10, SimKind.Candle, 1, 1);
                net.Host.Create(10, SimKind.Candle, 3, 1);
                net.Settle();
                net.Peers[2].PickUp(net.Peers[2].Items[gone.Id]);
                net.Peers[1].Destroy(net.Peers[1].Items[gone.Id]);
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
                net.Host.Create(10, SimKind.Candle, 3, 1);
                net.Settle();
                net.Peers[2].PickUp(net.Peers[2].Items[gone.Id]);
                net.Peers[1].Destroy(net.Peers[1].Items[gone.Id]);
                net.SettleInterleaved(seed);
                if (net.Differences().Count > 0)
                    apart++;
            }
            Assert.True(apart > 0);
        }

        // Coop 0.4.0 settles two players picking up one item at once: a guest asks the host,
        // which denies it if someone holds the item, and the guest is made to drop it. The
        // simulator once let both hold it, and showed the item in two places after two drops.
        [Fact]
        public void TwoPlayersPickUpTheSameItemAtOnce_OneHoldsIt_AndTheItemEndsInOnePlace()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                SimNetwork net = Scenario.Start();
                SimItem item = net.Host.Create(10, SimKind.Candle, 1, 3);
                net.Settle();
                net.Host.PickUp(item);
                net.Peers[2].PickUp(net.Peers[2].Items[item.Id]);
                net.SettleInterleaved(seed);

                Assert.Contains(item.Id, net.Host.HeldByMe);
                Assert.DoesNotContain(item.Id, net.Peers[2].HeldByMe);
                Assert.Contains(item.Id, net.Peers[2].RemoteHeld);

                net.Host.Drop(item, 4);
                net.SettleInterleaved(seed + 1);
                Assert.True(net.Differences().Count == 0, "seed " + seed + ": " + net.Summary());
            }
        }

        [Fact]
        public void TwoGuestsPickUpTheSameItemAtOnce_TheFirstRequestWins()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                SimNetwork net = Scenario.Start();
                SimItem item = net.Host.Create(10, SimKind.Candle, 1, 3);
                net.Settle();
                net.Peers[1].PickUp(net.Peers[1].Items[item.Id]);
                net.Peers[2].PickUp(net.Peers[2].Items[item.Id]);
                net.SettleInterleaved(seed);

                int holders = net.Peers.Count(p => p.HeldByMe.Contains(item.Id));
                Assert.True(holders == 1, "seed " + seed + ": " + holders + " hold it");
            }
        }

        [Fact]
        public void WithoutCoopsPickUpCheck_BothHoldItAndDropItInDifferentPlaces()
        {
            SimNetwork net = Scenario.Start(new SimRules { CoopArbitratesPickUp = false });
            SimItem item = net.Host.Create(10, SimKind.Candle, 1, 3);
            net.Settle();
            net.Host.PickUp(item);
            net.Peers[2].PickUp(net.Peers[2].Items[item.Id]);
            net.Settle();
            net.Host.Drop(item, 4);
            net.Peers[2].Drop(net.Peers[2].Items[item.Id], 3);
            net.Settle();

            Assert.NotEmpty(net.Differences());
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
