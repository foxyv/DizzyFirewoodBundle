using Xunit;

namespace Dizzy.FirewoodBundle.Tests.CoopSim
{
    // One test for each co-op bug found so far (docs/dev-journal.md). Each has a pair: the
    // test with the fix, and the same steps with the fix switched off, which must go wrong.
    // If the second one passes, the first guards nothing.
    public class CoopRegressionTests
    {
        private const int Candle = 10;
        private const int Sausage = 20;
        private const int Pork = 21;
        private const int GoatCheese = 30;
        private const int Log = 40;
        private const int Hook = 50;

        // R1 (J-001): a candle bundle made after the other player joined showed as one candle.
        [Fact]
        public void R1_CandleBundleAfterJoin_ShowsToTheGuest()
        {
            var net = new SimNetwork();
            SimItem candle = net.Host.Create(Candle, SimKind.Candle, 1);
            SimPeer guest = net.Join("guest");
            net.Settle();

            net.Host.SetBundle(candle, 3);
            net.Settle();

            Assert.Equal(3, guest.Items[candle.Id].Count);
            Assert.Empty(net.Differences());
        }

        [Fact]
        public void R1_WithoutTheBundlePacket_TheGuestSeesOne()
        {
            var net = new SimNetwork(new SimRules { SendBundlePacket = false });
            SimItem candle = net.Host.Create(Candle, SimKind.Candle, 1);
            SimPeer guest = net.Join("guest");
            net.Settle();

            net.Host.SetBundle(candle, 3);
            net.Settle();

            Assert.NotEmpty(net.Differences());
        }

        // R2 (J-002): food bundles were not shared. Coop never sends the food count.
        [Fact]
        public void R2_FoodBundlesAfterJoin_ShowToTheGuest()
        {
            var net = new SimNetwork();
            SimItem sausages = net.Host.Create(Sausage, SimKind.Sausage, 1);
            SimItem chops = net.Host.Create(Pork, SimKind.Sausage, 2);
            SimPeer guest = net.Join("guest");
            net.Settle();

            net.Host.SetBundle(sausages, 4);
            net.Host.SetBundle(chops, 3);
            net.Settle();

            Assert.Equal(4, guest.Items[sausages.Id].Count);
            Assert.Equal(3, guest.Items[chops.Id].Count);
            Assert.Empty(net.Differences());
        }

        [Fact]
        public void R2_WithoutTheBundlePacket_FoodNeverArrives()
        {
            var net = new SimNetwork(new SimRules { SendBundlePacket = false });
            SimItem sausages = net.Host.Create(Sausage, SimKind.Sausage, 1);
            SimPeer guest = net.Join("guest");
            net.Settle();

            net.Host.SetBundle(sausages, 4);
            net.Host.ChangeHealth(sausages, 0.9f);
            net.Settle();

            Assert.NotEmpty(net.Differences());
        }

        // R3 (J-005): a bundle another player holds could be combined, taken from or gathered.
        [Fact]
        public void R3_BundleHeldByTheGuest_IsRefusedOnTheHost()
        {
            var net = new SimNetwork();
            SimItem held = net.Host.Create(Candle, SimKind.Candle, 1, 4);
            SimItem other = net.Host.Create(Candle, SimKind.Candle, 2, 2);
            SimPeer guest = net.Join("guest");
            net.Settle();
            guest.PickUp(guest.Items[held.Id]);
            net.Settle();

            Assert.False(net.Host.Combine(other, held));
            Assert.False(net.Host.Combine(held, other));
            Assert.Null(net.Host.TakeOne(held));
            net.Settle();

            Assert.Equal(4, held.Count);
            Assert.Equal(2, other.Count);
            Assert.Empty(net.Differences());
        }

        [Fact]
        public void R3_AfterTheDrop_TheHostMayChangeItAgain()
        {
            var net = new SimNetwork();
            SimItem bundle = net.Host.Create(Candle, SimKind.Candle, 1, 4);
            SimPeer guest = net.Join("guest");
            net.Settle();
            guest.PickUp(guest.Items[bundle.Id]);
            net.Settle();
            guest.Drop(guest.Items[bundle.Id], 3);
            net.Settle();

            Assert.NotNull(net.Host.TakeOne(bundle));
        }

        [Fact]
        public void R3_WithoutTheCheck_TheHostChangesWhatTheGuestHolds()
        {
            var net = new SimNetwork(new SimRules { RefuseWhenHeldByOther = false });
            SimItem held = net.Host.Create(Candle, SimKind.Candle, 1, 4);
            SimItem other = net.Host.Create(Candle, SimKind.Candle, 2, 2);
            SimPeer guest = net.Join("guest");
            net.Settle();
            guest.PickUp(guest.Items[held.Id]);
            net.Settle();

            Assert.True(net.Host.Combine(other, held));
        }

        // R3 also holds when Coop does not set the held pointer, only lists the item.
        [Fact]
        public void R3_HoldsWhenCoopOnlyListsTheHeldItem()
        {
            var net = new SimNetwork(new SimRules { CoopSetsHeldPointer = false });
            SimItem held = net.Host.Create(Candle, SimKind.Candle, 1, 4);
            SimPeer guest = net.Join("guest");
            net.Settle();
            guest.PickUp(guest.Items[held.Id]);
            net.Settle();

            Assert.Null(net.Host.TakeOne(held));
        }

        // R4 (J-007): a piece taken off a bundle took the bundle's ID when a guest picked it up.
        [Fact]
        public void R4_PieceTakenOffByTheHost_KeepsItsOwnIdWhenTheGuestPicksItUp()
        {
            var net = new SimNetwork();
            SimItem bundle = net.Host.Create(Candle, SimKind.Candle, 1, 4);
            SimPeer guest = net.Join("guest");
            net.Settle();

            SimItem piece = net.Host.TakeOne(bundle);
            net.Settle();
            guest.PickUp(guest.Items[piece.Id]);
            net.Settle();

            Assert.Equal(3, net.Host.Items[bundle.Id].Count);
            Assert.Equal(1, net.Host.Items[piece.Id].Count);
            Assert.Empty(net.Differences());
        }

        [Fact]
        public void R4_PieceTakenOffByTheGuest_KeepsItsOwnIdAtTheHost()
        {
            var net = new SimNetwork();
            SimPeer guest = net.Join("guest");
            net.Settle();
            SimItem bundle = guest.Create(Candle, SimKind.Candle, 5, 4);
            net.Settle();

            SimItem piece = guest.TakeOne(bundle);
            guest.PickUp(piece);
            net.Settle();

            Assert.True(net.Host.Items.ContainsKey(bundle.Id));
            Assert.True(net.Host.Items.ContainsKey(piece.Id));
            Assert.Empty(net.Differences());
        }

        [Fact]
        public void R4_WithoutAnnouncingThePiece_TheBundleLosesItsId()
        {
            var net = new SimNetwork(new SimRules { AnnounceSpawn = false });
            SimPeer guest = net.Join("guest");
            net.Settle();
            SimItem bundle = guest.Create(Candle, SimKind.Candle, 5, 4);
            net.Settle();

            SimItem piece = guest.TakeOne(bundle);
            guest.PickUp(piece);
            net.Settle();

            Assert.False(net.Host.Items.ContainsKey(bundle.Id));
        }

        // R5: every place that makes a piece must announce it. A stove log and a hook off a line.
        [Theory]
        [InlineData(Log)]
        [InlineData(Hook)]
        public void R5_AnyNewPiece_IsAnnouncedBeforeItIsPickedUp(int prefab)
        {
            var net = new SimNetwork();
            SimItem bundle = net.Host.Create(prefab, SimKind.Candle, 1, 3);
            SimPeer guest = net.Join("guest");
            net.Settle();

            SimItem piece = net.Host.SpawnPiece(prefab, SimKind.Candle, 1.2);
            net.Settle();
            guest.PickUp(guest.Items[piece.Id]);
            net.Settle();

            Assert.True(net.Host.Items.ContainsKey(bundle.Id));
            Assert.True(net.Host.Items.ContainsKey(piece.Id));
            Assert.Empty(net.Differences());
        }

        [Theory]
        [InlineData(Log)]
        [InlineData(Hook)]
        public void R5_WithoutAnnouncing_ThePickUpTakesTheNearestItem(int prefab)
        {
            var net = new SimNetwork(new SimRules { AnnounceSpawn = false });
            SimPeer guest = net.Join("guest");
            net.Settle();
            SimItem bundle = guest.Create(prefab, SimKind.Candle, 1, 3);
            net.Settle();

            SimItem piece = guest.SpawnPiece(prefab, SimKind.Candle, 1.2);
            guest.PickUp(piece);
            net.Settle();

            Assert.False(net.Host.Items.ContainsKey(bundle.Id));
        }

        // R9: the tie color and width keys changed a bundle another player was holding.
        [Theory]
        [InlineData(SimKind.Candle)]
        [InlineData(SimKind.Sausage)]
        public void R9_TieColor_IsRefusedWhileTheGuestHoldsTheBundle(SimKind kind)
        {
            var net = new SimNetwork();
            SimItem bundle = net.Host.Create(kind == SimKind.Candle ? Candle : Sausage, kind, 1, 4);
            SimPeer guest = net.Join("guest");
            net.Settle();
            guest.PickUp(guest.Items[bundle.Id]);
            net.Settle();

            Assert.False(net.Host.Recolor(bundle, 2));
            net.Settle();

            Assert.Equal(0, bundle.Color);
            Assert.Equal(0, guest.Items[bundle.Id].Color);
            Assert.Empty(net.Differences());
        }

        [Fact]
        public void R9_StackWidth_IsRefusedWhileTheGuestHoldsTheStack()
        {
            var net = new SimNetwork();
            SimItem stack = net.Host.Create(Sausage, SimKind.Sausage, 1, 4);
            SimPeer guest = net.Join("guest");
            net.Settle();
            guest.PickUp(guest.Items[stack.Id]);
            net.Settle();

            Assert.False(net.Host.CycleWidth(stack, 3));
            net.Settle();

            Assert.Equal(0, stack.Width);
            Assert.Equal(0, guest.Items[stack.Id].Width);
        }

        [Theory]
        [InlineData(SimKind.Candle)]
        [InlineData(SimKind.Sausage)]
        public void R9_TheHolderMayRecolorTheirOwnBundle_AndTheOtherSeesIt(SimKind kind)
        {
            var net = new SimNetwork();
            SimItem bundle = net.Host.Create(kind == SimKind.Candle ? Candle : Sausage, kind, 1, 4);
            SimPeer guest = net.Join("guest");
            net.Settle();
            guest.PickUp(guest.Items[bundle.Id]);
            net.Settle();

            Assert.True(guest.Recolor(guest.Items[bundle.Id], 2));
            net.Settle();

            Assert.Equal(2, bundle.Color);
            Assert.Empty(net.Differences());
        }

        [Theory]
        [InlineData(SimKind.Candle)]
        [InlineData(SimKind.Sausage)]
        public void R9_ABundleNobodyHolds_IsRecoloredEverywhere_AndAfterTheDropAgain(SimKind kind)
        {
            var net = new SimNetwork();
            SimItem bundle = net.Host.Create(kind == SimKind.Candle ? Candle : Sausage, kind, 1, 4);
            SimPeer guest = net.Join("guest");
            net.Settle();

            Assert.True(net.Host.Recolor(bundle, 1));
            net.Settle();
            Assert.Equal(1, guest.Items[bundle.Id].Color);
            Assert.Equal(4, guest.Items[bundle.Id].Count);

            guest.PickUp(guest.Items[bundle.Id]);
            net.Settle();
            Assert.False(net.Host.Recolor(bundle, 2));
            guest.Drop(guest.Items[bundle.Id], 3);
            net.Settle();

            Assert.True(net.Host.Recolor(bundle, 2));
            net.Settle();
            Assert.Equal(2, guest.Items[bundle.Id].Color);
            Assert.Empty(net.Differences());
        }

        [Fact]
        public void R9_WithoutTheCheck_TheHostRecolorsWhatTheGuestHolds()
        {
            var net = new SimNetwork(new SimRules { RefuseWhenHeldByOther = false });
            SimItem bundle = net.Host.Create(Candle, SimKind.Candle, 1, 4);
            SimPeer guest = net.Join("guest");
            net.Settle();
            guest.PickUp(guest.Items[bundle.Id]);
            net.Settle();

            Assert.True(net.Host.Recolor(bundle, 2));
        }

        [Fact]
        public void R9_TheColorSurvivesATakeOff()
        {
            var net = new SimNetwork();
            SimItem bundle = net.Host.Create(Candle, SimKind.Candle, 1, 4);
            SimPeer guest = net.Join("guest");
            net.Settle();
            net.Host.Recolor(bundle, 3);
            net.Settle();

            net.Host.TakeOne(bundle);
            net.Settle();

            Assert.Equal(3, guest.Items[bundle.Id].Color);
            Assert.Equal(3, guest.Items[bundle.Id].Count);
            Assert.Empty(net.Differences());
        }

        // R6 (J-010): a drop of a bundle redrew it on the other side, and the redraw went back.
        [Fact]
        public void R6_DropOfABundle_IsAppliedOnceWithNoEcho()
        {
            var net = new SimNetwork();
            SimItem cheese = net.Host.Create(GoatCheese, SimKind.Sausage, 1, 3);
            SimPeer guest = net.Join("guest");
            net.Settle();
            guest.PickUp(guest.Items[cheese.Id]);
            net.Settle();
            int before = net.Sent;

            guest.Drop(guest.Items[cheese.Id], 2);
            net.Settle();

            Assert.Equal(before + 1, net.Sent);
            Assert.Equal(0, net.SentBundlePackets);
            Assert.Empty(net.Differences());
        }

        [Fact]
        public void R6_ABundleChange_IsNotEchoedBack()
        {
            var net = new SimNetwork();
            SimItem cheese = net.Host.Create(GoatCheese, SimKind.Sausage, 1);
            SimPeer guest = net.Join("guest");
            net.Settle();

            net.Host.SetBundle(cheese, 3);
            net.Settle();

            Assert.Equal(1, net.SentBundlePackets);
        }

        [Fact]
        public void R6_WithoutTheEchoGuard_ThePeersNeverStop()
        {
            var net = new SimNetwork(new SimRules { SuppressEchoes = false });
            SimItem cheese = net.Host.Create(GoatCheese, SimKind.Sausage, 1);
            net.Join("guest");
            net.Settle();

            net.Host.SetBundle(cheese, 3);

            Assert.Throws<System.InvalidOperationException>(() => net.Settle());
        }
    }
}
