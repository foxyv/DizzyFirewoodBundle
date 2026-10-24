using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Dizzy.FirewoodBundle.Tests.CoopSim
{
    // F: every kind of bundle. J: joining and timing. X: two players, one bundle.
    public class CoopScenarioTests
    {
        private const int Candle = 10;
        private const int Sausage = 20;

        private static int PrefabOf(SimKind kind)
        {
            return kind == SimKind.Candle ? Candle : Sausage;
        }

        // ---- F: each kind of bundle ----

        [Theory]
        [InlineData(SimKind.Candle)]
        [InlineData(SimKind.Sausage)]
        public void F1_BundleMadeOnTheHost_MatchesOnTheGuest(SimKind kind)
        {
            var net = new SimNetwork();
            SimItem item = net.Host.Create(PrefabOf(kind), kind, 1);
            net.Join("guest");
            net.Settle();

            net.Host.SetBundle(item, 6);
            net.Settle();

            Assert.Empty(net.Differences());
            Assert.Equal(6, net["guest"].Items[item.Id].Count);
        }

        [Theory]
        [InlineData(SimKind.Candle)]
        [InlineData(SimKind.Sausage)]
        public void F2_BundleMadeOnAGuest_MatchesOnTheHostAndTheOtherGuest(SimKind kind)
        {
            var net = new SimNetwork();
            SimPeer one = net.Join("one");
            net.Join("two");
            net.Settle();
            SimItem item = one.Create(PrefabOf(kind), kind, 1);
            net.Settle();

            one.SetBundle(item, 6);
            net.Settle();

            Assert.Empty(net.Differences());
            Assert.Equal(6, net["two"].Items[item.Id].Count);
        }

        [Theory]
        [InlineData(SimKind.Candle)]
        [InlineData(SimKind.Sausage)]
        public void F3_CountingDown_EveryPeerAgreesAtEveryStep_AndOneIsASingle(SimKind kind)
        {
            var net = new SimNetwork();
            SimItem item = net.Host.Create(PrefabOf(kind), kind, 1, 5);
            net.Join("guest");
            net.Settle();

            for (int left = 4; left >= 1; left--)
            {
                Assert.NotNull(net.Host.TakeOne(item));
                net.Settle();
                Assert.Empty(net.Differences());
                Assert.Equal(left, net["guest"].Items[item.Id].Count);
            }

            Assert.Null(net.Host.TakeOne(item));
            if (kind == SimKind.Sausage)
            {
                // A single carries no stack number at all, on either side.
                Assert.Equal(0f, item.Extra4);
                Assert.Equal(0f, net["guest"].Items[item.Id].Extra4);
            }
        }

        [Theory]
        [InlineData(SimKind.Candle)]
        [InlineData(SimKind.Sausage)]
        public void F3_TheLastPieceUsedUp_IsGoneEverywhere(SimKind kind)
        {
            var net = new SimNetwork();
            SimItem item = net.Host.Create(PrefabOf(kind), kind, 1);
            net.Join("guest");
            net.Settle();

            net.Host.Destroy(item);
            net.Settle();

            Assert.Empty(net.Differences());
            Assert.Empty(net["guest"].Items);
        }

        [Fact]
        public void F4_HungBundle_StaysABundleAtOne_AndComesOffAsASingle()
        {
            var net = new SimNetwork();
            SimItem item = net.Host.Create(Sausage, SimKind.Sausage, 1, 3);
            SimPeer guest = net.Join("guest");
            net.Settle();

            Assert.True(net.Host.Hang(item, true));
            net.Settle();
            Assert.True(guest.Items[item.Id].Hung);

            net.Host.TakeOne(item);
            net.Host.TakeOne(item);
            net.Settle();
            Assert.Equal(1, guest.Items[item.Id].Count);
            Assert.True(guest.Items[item.Id].Hung);
            Assert.Empty(net.Differences());

            Assert.True(net.Host.Hang(item, false));
            net.Settle();
            Assert.False(guest.Items[item.Id].Hung);
            Assert.Equal(0f, guest.Items[item.Id].Extra4);
            Assert.Empty(net.Differences());
        }

        [Fact]
        public void F4_HangingIsRefusedWhileAnotherPlayerHoldsTheBundle()
        {
            var net = new SimNetwork();
            SimItem item = net.Host.Create(Sausage, SimKind.Sausage, 1, 3);
            SimPeer guest = net.Join("guest");
            net.Settle();
            guest.PickUp(guest.Items[item.Id]);
            net.Settle();

            Assert.False(net.Host.Hang(item, true));
        }

        [Fact]
        public void F5_ReloadingASave_SendsNothing_AndAJoinAfterwardsMatches()
        {
            var net = new SimNetwork();
            SimItem a = net.Host.Create(Candle, SimKind.Candle, 1, 4);
            SimItem b = net.Host.Create(Sausage, SimKind.Sausage, 2, 3);
            net.Host.Hang(b, true);

            net.Host.Reload();
            net.Host.EndFrame();
            Assert.Equal(0, net.SentBundlePackets);

            net.Join("guest");
            net.Settle();
            Assert.Empty(net.Differences());
            Assert.Equal(4, net["guest"].Items[a.Id].Count);
            Assert.True(net["guest"].Items[b.Id].Hung);
            Assert.Equal(0, net.SentBundlePackets);
        }

        // ---- J: joining and timing ----

        [Fact]
        public void J1_JoiningWithFiftyBundles_SendsNoBundlePackets_AndAllMatch()
        {
            var net = new SimNetwork();
            for (int i = 0; i < 50; i++)
            {
                SimKind kind = i % 2 == 0 ? SimKind.Candle : SimKind.Sausage;
                SimItem item = net.Host.Create(PrefabOf(kind) + i * 100, kind, i);
                net.Host.SetBundle(item, 2 + i % 5);
            }
            net.Host.Reload();
            net.Host.EndFrame();

            net.Join("guest");
            net.Settle();

            Assert.Equal(0, net.SentBundlePackets);
            Assert.Equal(50, net["guest"].Items.Count);
            Assert.Empty(net.Differences());
        }

        [Fact]
        public void J2_AChangeMadeWhileTheSnapshotIsInFlight_Wins()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var net = new SimNetwork();
                SimItem item = net.Host.Create(Candle, SimKind.Candle, 1, 2);
                net.Join("guest");

                net.Host.SetBundle(item, 7);
                net.SettleInterleaved(seed);

                Assert.True(net["guest"].Items[item.Id].Count == 7, "seed " + seed + ": " + net.Summary());
                Assert.Empty(net.Differences());
            }
        }

        [Fact]
        public void J3_ASecondGuestJoiningLate_MatchesTheOthers()
        {
            var net = new SimNetwork();
            SimItem item = net.Host.Create(Sausage, SimKind.Sausage, 1);
            net.Join("one");
            net.Settle();
            net.Host.SetBundle(item, 4);
            net.Settle();
            SimItem made = net["one"].Create(Candle, SimKind.Candle, 2, 3);
            net.Settle();

            net.Join("two");
            net.Settle();

            Assert.Empty(net.Differences());
            Assert.Equal(4, net["two"].Items[item.Id].Count);
            Assert.Equal(3, net["two"].Items[made.Id].Count);
        }

        [Fact]
        public void J4_ADisconnectMidMessage_AndARejoin_LeavesNoDuplicatesOrHalfBundles()
        {
            var net = new SimNetwork();
            SimItem item = net.Host.Create(Candle, SimKind.Candle, 1, 2);
            net.Join("guest");
            net.Settle();

            net.Host.SetBundle(item, 6);
            net.Host.EndFrame();
            Assert.True(net.InFlight > 0);

            net.Disconnect("guest");
            Assert.Equal(0, net.InFlight);
            net.Join("guest");
            net.Settle();

            Assert.Equal(2, net.Peers.Count);
            Assert.Single(net["guest"].Items);
            Assert.Equal(6, net["guest"].Items[item.Id].Count);
            Assert.Empty(net.Differences());
        }

        // ---- X: two players, one bundle ----

        [Fact]
        public void X1_TwoTakeOffsAtOnce_EndWithTheSameCountEverywhere()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var net = new SimNetwork();
                SimItem bundle = net.Host.Create(Candle, SimKind.Candle, 1, 4);
                SimPeer guest = net.Join("guest");
                net.Settle();

                net.Host.TakeOne(bundle);
                guest.TakeOne(guest.Items[bundle.Id]);
                net.SettleInterleaved(seed);

                Assert.True(net.Differences().Count == 0, "seed " + seed + ": " + net.Summary());
            }
        }

        // R7: each took one off. Two pieces came off, so two are gone from the bundle.
        [Fact]
        public void X1_TwoTakeOffsAtOnce_NoCandleAppears()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var net = new SimNetwork();
                SimItem bundle = net.Host.Create(Candle, SimKind.Candle, 1, 4);
                SimPeer guest = net.Join("guest");
                net.Settle();

                net.Host.TakeOne(bundle);
                guest.TakeOne(guest.Items[bundle.Id]);
                net.SettleInterleaved(seed);

                Assert.True(net.Total(net.Host, Candle) == 4, "seed " + seed + ": " + net.Total(net.Host, Candle) + " candles");
                Assert.Equal(2, net.Host.Items[bundle.Id].Count);
                Assert.True(net.Differences().Count == 0, "seed " + seed + ": " + net.Summary());
            }
        }

        [Fact]
        public void X1_WithoutTheMerge_ACandleAppears()
        {
            var net = new SimNetwork(new SimRules { MergeConcurrentCounts = false });
            SimItem bundle = net.Host.Create(Candle, SimKind.Candle, 1, 4);
            SimPeer guest = net.Join("guest");
            net.Settle();

            net.Host.TakeOne(bundle);
            guest.TakeOne(guest.Items[bundle.Id]);
            net.Settle();

            Assert.Equal(5, net.Total(net.Host, Candle));
        }

        [Fact]
        public void X1_ThreePlayersTakingAtOnce_AndAGuestTakingTwice_StillAddUp()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var net = new SimNetwork();
                SimItem bundle = net.Host.Create(Candle, SimKind.Candle, 1, 9);
                SimPeer one = net.Join("one");
                SimPeer two = net.Join("two");
                net.Settle();

                net.Host.TakeOne(bundle);
                one.TakeOne(one.Items[bundle.Id]);
                one.EndFrame();
                one.TakeOne(one.Items[bundle.Id]);
                two.TakeOne(two.Items[bundle.Id]);
                net.SettleInterleaved(seed);

                Assert.True(net.Total(net.Host, Candle) == 9, "seed " + seed + ": " + net.Total(net.Host, Candle));
                Assert.True(net.Differences().Count == 0, "seed " + seed + ": " + net.Summary());
            }
        }

        [Fact]
        public void X1_BothTakeFromABundleOfTwo_NeverGoesBelowOne()
        {
            var net = new SimNetwork();
            SimItem bundle = net.Host.Create(Candle, SimKind.Candle, 1, 2);
            SimPeer guest = net.Join("guest");
            net.Settle();

            net.Host.TakeOne(bundle);
            guest.TakeOne(guest.Items[bundle.Id]);
            net.Settle();

            Assert.Equal(1, net.Host.Items[bundle.Id].Count);
            Assert.Empty(net.Differences());
        }

        // R8: the host combined B into A and the guest picked B up at the same time.
        [Fact]
        public void X2_CombiningWhileTheGuestPicksTheOtherUp_TheBundleKeepsItsId()
        {
            var net = new SimNetwork();
            SimItem a = net.Host.Create(Candle, SimKind.Candle, 1, 3);
            SimItem b = net.Host.Create(Candle, SimKind.Candle, 2, 2);
            SimPeer guest = net.Join("guest");
            net.Settle();

            net.Host.Combine(a, b);
            guest.PickUp(guest.Items[b.Id]);
            net.Settle();

            Assert.Equal(5, net.Host.Items[a.Id].Count);
            Assert.Equal(5, net.Total(net.Host, Candle));
            Assert.Empty(net.Differences());
        }

        [Fact]
        public void X2_WithoutIgnoringIt_TheBundleTakesTheOthersId()
        {
            var net = new SimNetwork(new SimRules { IgnorePickUpOfDestroyed = false });
            SimItem a = net.Host.Create(Candle, SimKind.Candle, 1, 3);
            SimItem b = net.Host.Create(Candle, SimKind.Candle, 2, 2);
            SimPeer guest = net.Join("guest");
            net.Settle();

            net.Host.Combine(a, b);
            guest.PickUp(guest.Items[b.Id]);
            net.Settle();

            Assert.NotEmpty(net.Differences());
        }

        [Fact]
        public void X3_ANewPieceBesideTwoBundles_TakesNeitherBundlesId()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var net = new SimNetwork();
                SimItem a = net.Host.Create(Candle, SimKind.Candle, 1, 3);
                SimItem b = net.Host.Create(Candle, SimKind.Candle, 1.1, 3);
                SimPeer guest = net.Join("guest");
                net.Settle();

                SimItem piece = net.Host.TakeOne(a);
                net.SettleInterleaved(seed);
                guest.PickUp(guest.Items[piece.Id]);
                net.SettleInterleaved(seed + 1000);

                Assert.True(net.Host.Items.ContainsKey(a.Id) && net.Host.Items.ContainsKey(b.Id), "seed " + seed);
                Assert.True(net.Differences().Count == 0, "seed " + seed + ": " + net.Summary());
            }
        }
    }

    // S: random play across a host and two guests.
    public class CoopStressTests
    {
        private readonly ITestOutputHelper _output;

        public CoopStressTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static readonly int[] Prefabs = { 10, 20, 21 };

        private static SimKind KindOf(int prefab)
        {
            return prefab == 10 ? SimKind.Candle : SimKind.Sausage;
        }

        // One random action by one peer. Does nothing if the choice is not possible.
        private static void Act(SimNetwork net, Random random)
        {
            SimPeer actor = net.Peers[random.Next(net.Peers.Count)];
            List<SimItem> items = actor.Items.Values.OrderBy(i => i.Id).ToList();
            int op = random.Next(9);
            if (items.Count == 0 || op == 0)
            {
                int prefab = Prefabs[random.Next(Prefabs.Length)];
                actor.Create(prefab, KindOf(prefab), random.Next(5), 1 + random.Next(4));
                return;
            }

            SimItem item = items[random.Next(items.Count)];
            switch (op)
            {
                case 1: actor.SetBundle(item, 1 + random.Next(6)); break;
                case 2: actor.TakeOne(item); break;
                case 3:
                    if (!actor.RemoteHeld.Contains(item.Id) && !actor.HeldByMe.Contains(item.Id))
                        actor.PickUp(item);
                    break;
                case 4:
                    if (actor.HeldByMe.Contains(item.Id))
                        actor.Drop(item, random.Next(5));
                    break;
                case 5:
                    SimItem other = items[random.Next(items.Count)];
                    if (other != item && other.Prefab == item.Prefab)
                        actor.Combine(item, other);
                    break;
                case 6: actor.Hang(item, random.Next(2) == 0); break;
                case 7: actor.ChangeHealth(item, random.Next(10) / 10f); break;
                case 8:
                    if (!actor.RemoteHeld.Contains(item.Id) && item.Count == 1 && random.Next(4) == 0)
                        actor.Destroy(item);
                    break;
            }
        }

        private static SimNetwork ThreePeers(SimRules rules = null)
        {
            var net = new SimNetwork(rules);
            net.Join("one");
            net.Join("two");
            net.Settle();
            return net;
        }

        // Sequential: one peer acts, then everything arrives, before the next acts.
        private static void PlaySequential(SimNetwork net, int seed, int actions)
        {
            var random = new Random(seed);
            for (int i = 0; i < actions; i++)
            {
                Act(net, random);
                net.SettleInterleaved(seed * 7919 + i);
            }
        }

        // S1: 10,000 actions in all, no two at once.
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void S1_TwoThousandSequentialActions_EndInAgreement(int seed)
        {
            SimNetwork net = ThreePeers();

            PlaySequential(net, seed, 2000);

            Assert.True(net.Differences().Count == 0, "seed " + seed + ": " + net.Summary());
        }

        // S1b: peers act at the same time. Records how often they end up disagreeing.
        [Fact]
        public void S1b_ConcurrentPlay_NeverThrows_AndRecordsHowOftenPeersDisagree()
        {
            int diverged = 0;
            const int Runs = 20;
            for (int seed = 0; seed < Runs; seed++)
            {
                SimNetwork net = ThreePeers();
                var random = new Random(seed);
                for (int i = 0; i < 100; i++)
                {
                    int together = 1 + random.Next(3);
                    for (int k = 0; k < together; k++)
                        Act(net, random);
                    net.SettleInterleaved(seed * 31 + i);
                }
                if (net.Differences().Count > 0)
                    diverged++;
            }
            _output.WriteLine("Concurrent play: " + diverged + " of " + Runs + " runs ended with peers disagreeing.");
            Assert.InRange(diverged, 0, Runs);
        }

        // S2: a lost message is never sent again, so a lost change stays lost.
        [Fact]
        public void S2_FivePercentLoss_LeavesPeersDisagreeing()
        {
            int diverged = 0;
            const int Runs = 20;
            for (int seed = 0; seed < Runs; seed++)
            {
                SimNetwork net = ThreePeers();
                net.LoseMessages(0.05, seed);
                PlaySequential(net, seed, 200);
                if (net.Differences().Count > 0)
                    diverged++;
            }
            _output.WriteLine("5% loss: " + diverged + " of " + Runs + " runs ended with peers disagreeing.");
            Assert.True(diverged > 0, "Nothing diverged, so the loss is not being applied.");
        }

        // S3: switch each of our rules off in turn. What fails shows which tests guard it.
        [Theory]
        [InlineData("SendBundlePacket")]
        [InlineData("AnnounceSpawn")]
        [InlineData("SuppressEchoes")]
        public void S3_WithARuleOff_TheStressRunGoesWrong(string rule)
        {
            var rules = new SimRules();
            typeof(SimRules).GetField(rule).SetValue(rules, false);
            SimNetwork net = ThreePeers(rules);

            bool wrong;
            try
            {
                PlaySequential(net, 1, 300);
                wrong = net.Differences().Count > 0;
            }
            catch (InvalidOperationException)
            {
                wrong = true;
            }

            Assert.True(wrong, rule + " is off and nothing went wrong, so nothing guards it.");
        }

        // The model has nothing that goes wrong when RefuseWhenHeldByOther is off: peers
        // still agree, because the harm in J-005 was to the holder's own copy in the game.
        // No test here guards that rule beyond R3.
        [Fact]
        public void S3_WithTheHeldCheckOff_ThePeersStillAgree_SoOnlyR3GuardsIt()
        {
            SimNetwork net = ThreePeers(new SimRules { RefuseWhenHeldByOther = false });

            PlaySequential(net, 1, 300);

            Assert.Empty(net.Differences());
        }

        // S4: a change goes out once, however many pieces it was made of.
        [Fact]
        public void S4_OneBundlePacketForEachRebuild()
        {
            var net = new SimNetwork();
            SimItem item = net.Host.Create(Candle, SimKind.Candle, 1);
            net.Join("guest");
            net.Settle();

            for (int i = 0; i < 100; i++)
            {
                net.Host.SetBundle(item, 2 + i % 5);
                net.Host.SetBundle(item, 3 + i % 4);
                net.Settle();
            }

            Assert.Equal(100, net.SentBundlePackets);
        }

        private const int Candle = 10;
    }
}
