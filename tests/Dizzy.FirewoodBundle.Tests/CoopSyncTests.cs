using System.IO;
using Dizzy.FirewoodBundle;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests
{
    public class CoopSyncTests
    {
        private static BundleState Sample()
        {
            return new BundleState
            {
                Id = 123456,
                Amount = 3f,
                Health = 0.75f,
                HasFood = true,
                Dried = 0.1f,
                Smoked = 0.2f,
                Salted = 0.3f,
                Spoiled = 0.4f,
                Stack = StackCode.Encode(4, 2, 1),
            };
        }

        [Fact]
        public void Wire_RoundTrips_EveryField()
        {
            var stream = new MemoryStream();
            BundleWire.Write(new BinaryWriter(stream), Sample());
            stream.Position = 0;

            BundleState read;
            Assert.True(BundleWire.TryRead(new BinaryReader(stream), out read));
            Assert.Equal(Sample(), read);
            Assert.Equal(stream.Length, stream.Position);
        }

        [Fact]
        public void Wire_IgnoresAnotherVersion()
        {
            var stream = new MemoryStream();
            BundleWire.Write(new BinaryWriter(stream), Sample());
            byte[] bytes = stream.ToArray();
            bytes[0] = (byte)(BundleWire.Protocol + 1);

            BundleState read;
            Assert.False(BundleWire.TryRead(new BinaryReader(new MemoryStream(bytes)), out read));
        }

        [Fact]
        public void Wire_IsTheLayoutTheGameAlreadySends()
        {
            // 1 + 4 + 4 + 4 + 1 + 4 * 5 bytes. A change here breaks play with older versions.
            var stream = new MemoryStream();
            BundleWire.Write(new BinaryWriter(stream), Sample());
            Assert.Equal(34, stream.Length);
            Assert.Equal(1, BundleWire.Protocol);
        }

        [Theory]
        [InlineData(1, 0, 0)]
        [InlineData(5, 3, 2)]
        [InlineData(12, 8, 7)]
        [InlineData(999, 9, 9)]
        public void StackCode_RoundTrips(int count, int width, int color)
        {
            StackCode code = StackCode.Decode(StackCode.Encode(count, width, color));
            Assert.Equal(count, code.Count);
            Assert.Equal(width, code.Width);
            Assert.Equal(color, code.Color);
        }

        [Fact]
        public void StackCode_MatchesTheSavedNumber()
        {
            // count + width * 1000 + color * 10000, as SausageStacks has always saved it.
            Assert.Equal(21004f, StackCode.Encode(4, 1, 2));
        }

        [Fact]
        public void StackCode_SingleIsNotAHangingOne()
        {
            Assert.True(StackCode.Decode(StackCode.Encode(1, 0, 0)).IsSingle(8));
            Assert.False(StackCode.Decode(StackCode.Encode(1, 8, 0)).IsSingle(8));
            Assert.False(StackCode.Decode(StackCode.Encode(2, 0, 0)).IsSingle(8));
        }

        [Fact]
        public void PacketType_TakesTheFirstFree()
        {
            Assert.Equal((byte)249, SyncRules.PickPacketType(t => false));
            Assert.Equal((byte)251, SyncRules.PickPacketType(t => t == 249 || t == 250));
            Assert.Null(SyncRules.PickPacketType(t => true));
        }

        [Fact]
        public void PacketType_StaysClearOfClothesline()
        {
            foreach (byte candidate in SyncRules.PacketTypeCandidates)
                Assert.InRange(candidate, (byte)249, (byte)254);
        }

        [Theory]
        [InlineData(true, false, false, true)]
        [InlineData(false, false, false, false)]
        [InlineData(true, true, false, false)]
        [InlineData(true, false, true, false)]
        public void Note_OnlyWhenPresentAndNotApplyingOrLoading(bool present, bool applying, bool loading, bool expected)
        {
            Assert.Equal(expected, SyncRules.ShouldNote(present, applying, loading));
        }

        [Theory]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(false, false, false)]
        public void Send_NotWhileCoopAppliesAState(bool multiplayer, bool coopApplying, bool expected)
        {
            Assert.Equal(expected, SyncRules.ShouldSend(multiplayer, coopApplying));
        }

        [Fact]
        public void Relay_OnlyTheHostPassesPacketsOn()
        {
            Assert.True(SyncRules.ShouldRelay(true));
            Assert.False(SyncRules.ShouldRelay(false));
        }

        [Theory]
        [InlineData(false, true, true, true, false)]
        [InlineData(true, true, false, false, true)]
        [InlineData(true, false, true, true, true)]
        [InlineData(true, false, true, false, false)]
        [InlineData(true, false, false, true, false)]
        public void HeldByOther(bool present, bool heldBySomeoneElse, bool multiplayer, bool inRemoteHeld, bool expected)
        {
            Assert.Equal(expected, SyncRules.HeldByOther(present, heldBySomeoneElse, multiplayer, inRemoteHeld));
        }

        [Theory]
        [InlineData(true, false, true, true)]
        [InlineData(true, true, true, false)]
        [InlineData(true, false, false, false)]
        [InlineData(false, false, true, false)]
        public void Spawn_AnnouncedInASessionOutsideAnApply(bool present, bool applying, bool multiplayer, bool expected)
        {
            Assert.Equal(expected, SyncRules.ShouldAnnounceSpawn(present, applying, multiplayer));
        }
    }
}
