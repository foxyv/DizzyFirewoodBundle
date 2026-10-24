using System.Linq;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests.CoopSim
{
    // These check the simulator, not Bundle Up: that it behaves as Coop does, so that the
    // regression tests built on it mean something.
    public class SimModelTests
    {
        private const int Candle = 10;
        private const int Sausage = 20;

        [Fact]
        public void Join_SendsTheWholeWorld_FoodCountToo()
        {
            var net = new SimNetwork();
            SimItem sausages = net.Host.Create(Sausage, SimKind.Sausage, 1, 4);
            net.Flush();

            SimPeer guest = net.Join("guest");
            net.Flush();

            Assert.Equal(4, guest.Items[sausages.Id].Count);
            Assert.Empty(net.Differences());
        }

        [Fact]
        public void BundleMadeAfterJoin_NeedsTheBundlePacket()
        {
            var net = new SimNetwork();
            SimItem candle = net.Host.Create(Candle, SimKind.Candle, 1);
            SimPeer guest = net.Join("guest");
            net.Flush();

            net.Host.SetBundle(candle, 3);
            net.Host.EndFrame();
            net.Flush();

            Assert.Equal(3, guest.Items[candle.Id].Count);
            Assert.Empty(net.Differences());
        }

        [Fact]
        public void BundleMadeAfterJoin_WithoutThePacket_LeavesTheGuestWithOne()
        {
            // How J-001 looked: Coop sends amount only with a health change.
            var net = new SimNetwork(new SimRules { SendBundlePacket = false });
            SimItem candle = net.Host.Create(Candle, SimKind.Candle, 1);
            SimPeer guest = net.Join("guest");
            net.Flush();

            net.Host.SetBundle(candle, 3);
            net.Host.EndFrame();
            net.Flush();

            Assert.Equal(1, guest.Items[candle.Id].Count);
            Assert.NotEmpty(net.Differences());
        }

        [Fact]
        public void FoodBundleMadeAfterJoin_WithoutThePacket_NeverArrives_EvenWithAHealthChange()
        {
            // How J-002 looked: a health change sends amount, and food keeps its count elsewhere.
            var net = new SimNetwork(new SimRules { SendBundlePacket = false });
            SimItem sausages = net.Host.Create(Sausage, SimKind.Sausage, 1);
            SimPeer guest = net.Join("guest");
            net.Flush();

            net.Host.SetBundle(sausages, 4);
            net.Host.ChangeHealth(sausages, 0.5f);
            net.Flush();

            Assert.Equal(1, guest.Items[sausages.Id].Count);
        }

        [Fact]
        public void GuestBundle_ReachesAnotherGuestThroughTheHost()
        {
            var net = new SimNetwork();
            SimPeer one = net.Join("one");
            SimPeer two = net.Join("two");
            net.Flush();
            SimItem candle = one.Create(Candle, SimKind.Candle, 1);
            net.Flush();

            one.SetBundle(candle, 5);
            one.EndFrame();
            net.Flush();

            Assert.Equal(5, two.Items[candle.Id].Count);
            Assert.Equal(5, net.Host.Items[candle.Id].Count);
            Assert.Empty(net.Differences());
        }

        [Fact]
        public void GuestBundle_DoesNotReachAnotherGuestWhenTheHostDoesNotRelay()
        {
            var net = new SimNetwork(new SimRules { CoopHostRelays = false });
            SimPeer one = net.Join("one");
            SimPeer two = net.Join("two");
            net.Flush();
            SimItem candle = one.Create(Candle, SimKind.Candle, 1);
            net.Flush();

            one.SetBundle(candle, 5);
            one.EndFrame();
            net.Flush();

            Assert.False(two.Items.ContainsKey(candle.Id));
        }

        [Fact]
        public void PieceTakenOff_IsAnnounced_AndThePickUpKeepsItsOwnId()
        {
            var net = new SimNetwork();
            SimItem bundle = net.Host.Create(Candle, SimKind.Candle, 1, 4);
            SimPeer guest = net.Join("guest");
            net.Flush();

            SimItem piece = net.Host.TakeOne(bundle);
            net.Host.EndFrame();
            net.Flush();
            guest.PickUp(guest.Items[piece.Id]);
            net.Flush();

            Assert.Equal(3, net.Host.Items[bundle.Id].Count);
            Assert.Contains(piece.Id, net.Host.RemoteHeld);
            Assert.Empty(net.Differences());
        }

        [Fact]
        public void PieceTakenOffByAGuest_NotAnnounced_TakesTheBundleIdAtTheHost()
        {
            // How J-007 looked: the pick-up of an unknown item takes the nearest of its prefab.
            var net = new SimNetwork(new SimRules { AnnounceSpawn = false });
            SimPeer guest = net.Join("guest");
            net.Flush();
            SimItem bundle = guest.Create(Candle, SimKind.Candle, 5, 4);
            net.Flush();

            SimItem piece = guest.TakeOne(bundle);
            guest.EndFrame();
            guest.PickUp(piece);
            net.Flush();

            Assert.False(net.Host.Items.ContainsKey(bundle.Id));
            Assert.True(net.Host.Items.ContainsKey(piece.Id));
            Assert.NotEmpty(net.Differences());
        }

        [Fact]
        public void BundleHeldByAnotherPlayer_CannotBeChanged()
        {
            var net = new SimNetwork();
            SimItem bundle = net.Host.Create(Candle, SimKind.Candle, 1, 4);
            SimPeer guest = net.Join("guest");
            net.Flush();
            guest.PickUp(guest.Items[bundle.Id]);
            net.Flush();

            Assert.Null(net.Host.TakeOne(bundle));
            Assert.False(net.Host.SetBundle(bundle, 9));
            Assert.Equal(4, bundle.Count);
        }

        [Fact]
        public void BundleHeldByAnotherPlayer_IsChangedWhenTheRuleIsOff()
        {
            var net = new SimNetwork(new SimRules { RefuseWhenHeldByOther = false });
            SimItem bundle = net.Host.Create(Candle, SimKind.Candle, 1, 4);
            SimPeer guest = net.Join("guest");
            net.Flush();
            guest.PickUp(guest.Items[bundle.Id]);
            net.Flush();

            Assert.NotNull(net.Host.TakeOne(bundle));
        }

        [Fact]
        public void LoadingASave_SendsNothing()
        {
            var net = new SimNetwork();
            SimItem candle = net.Host.Create(Candle, SimKind.Candle, 1);
            net.Join("guest");
            net.Flush();

            net.Host.Loading = true;
            net.Host.SetBundle(candle, 3);
            net.Host.Loading = false;
            net.Host.EndFrame();

            Assert.Equal(0, net.SentBundlePackets);
        }

        [Fact]
        public void AChangeMadeInSteps_GoesOutOnceAtTheEndOfTheFrame()
        {
            var net = new SimNetwork();
            SimItem candle = net.Host.Create(Candle, SimKind.Candle, 1, 5);
            net.Join("guest");
            net.Flush();

            net.Host.TakeOne(candle);
            net.Host.TakeOne(candle);
            net.Host.TakeOne(candle);
            net.Host.EndFrame();

            Assert.Equal(1, net.SentBundlePackets);
        }

        [Fact]
        public void Shuffle_GivesTheSameOrderForTheSameSeed()
        {
            string Run(int seed)
            {
                var net = new SimNetwork();
                SimPeer a = net.Join("a");
                net.Flush();
                for (int i = 0; i < 6; i++)
                    a.Create(Candle, SimKind.Candle, i);
                net.Shuffle(seed);
                net.Flush();
                return string.Join(",", net.Host.Items.Keys);
            }

            Assert.Equal(Run(7), Run(7));
        }
    }
}
