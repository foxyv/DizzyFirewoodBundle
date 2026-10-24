using System.Linq;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests.CoopSim
{
    // A player leaves while holding a bundle (or with a change not yet sent).
    public class CoopLeaveTests
    {
        private const int Candle = 10;

        private static SimNetwork WithBundleHeldByOne(out SimItem bundle)
        {
            SimNetwork net = Scenario.Start();
            bundle = net.Host.Create(Candle, SimKind.Candle, 1, 4);
            net.Settle();
            net.Peers[1].PickUp(net.Peers[1].Items[bundle.Id]);
            net.Settle();
            return net;
        }

        [Fact]
        public void L1_WhileHeld_TheHostCannotChangeIt_AfterTheHolderLeaves_ItCan()
        {
            SimItem bundle;
            SimNetwork net = WithBundleHeldByOne(out bundle);
            Assert.False(net.Host.Recolor(bundle, 2));

            net.Disconnect("guest1");
            net.Settle();

            Assert.True(net.Host.Recolor(bundle, 2));
            net.Settle();
            Assert.Equal(2, net.Peers[1].Items[bundle.Id].Color);   // Peers[1] is now guest2
            Assert.Empty(net.Differences());
        }

        [Fact]
        public void L2_TheOtherGuest_SeesItFreedToo_AndCanPickItUp()
        {
            SimItem bundle;
            SimNetwork net = WithBundleHeldByOne(out bundle);
            SimPeer other = net.Peers[2];
            Assert.Contains(bundle.Id, other.RemoteHeld);

            net.Disconnect("guest1");
            net.Settle();

            Assert.DoesNotContain(bundle.Id, other.RemoteHeld);
            other.PickUp(other.Items[bundle.Id]);
            net.Settle();
            Assert.Contains(bundle.Id, other.HeldByMe);
            Assert.Empty(net.Differences());
        }

        [Fact]
        public void L3_AChangeNotYetSent_IsLost_AndTheOthersStillAgree()
        {
            SimItem bundle;
            SimNetwork net = WithBundleHeldByOne(out bundle);
            SimPeer leaver = net.Peers[1];
            leaver.TakeOne(leaver.Items[bundle.Id]);   // noted, not sent: the frame never ends

            net.Disconnect("guest1");
            net.Settle();

            Assert.Empty(net.Differences());
            Assert.Equal(4, net.Host.Items[bundle.Id].Count);
        }

        [Fact]
        public void L4_ARejoiningGuest_GetsTheBundleAsTheOthersHaveIt_AndNotHeld()
        {
            SimItem bundle;
            SimNetwork net = WithBundleHeldByOne(out bundle);
            net.Peers[1].Recolor(net.Peers[1].Items[bundle.Id], 3);
            net.Peers[1].EndFrame();
            net.Settle();
            net.Disconnect("guest1");
            net.Settle();

            SimPeer back = net.Join("guest1");
            net.Settle();

            Assert.Equal(3, back.Items[bundle.Id].Color);
            Assert.Equal(4, back.Items[bundle.Id].Count);
            Assert.DoesNotContain(bundle.Id, back.RemoteHeld);
            Assert.False(back.HeldByOther(back.Items[bundle.Id]));
            Assert.Empty(net.Differences());
        }

        [Fact]
        public void L5_WithoutCoopDroppingTheLeaversItems_TheBundleStaysFrozenForTheOthers()
        {
            SimNetwork net = Scenario.Start(new SimRules { CoopDropsLeaversItems = false });
            SimItem bundle = net.Host.Create(Candle, SimKind.Candle, 1, 4);
            net.Settle();
            net.Peers[1].PickUp(net.Peers[1].Items[bundle.Id]);
            net.Settle();

            net.Disconnect("guest1");
            net.Settle();

            Assert.False(net.Host.Recolor(bundle, 2));
        }

        [Fact]
        public void L6_AFoodStackHeldByTheLeaver_IsFreeAfterToo()
        {
            SimNetwork net = Scenario.Start();
            SimItem stack = net.Host.Create(20, SimKind.Sausage, 1, 4);
            net.Settle();
            net.Peers[1].PickUp(net.Peers[1].Items[stack.Id]);
            net.Settle();
            Assert.False(net.Host.CycleWidth(stack, 3));

            net.Disconnect("guest1");
            net.Settle();

            Assert.True(net.Host.CycleWidth(stack, 3));
            net.Settle();
            Assert.Empty(net.Differences());
        }
    }
}
