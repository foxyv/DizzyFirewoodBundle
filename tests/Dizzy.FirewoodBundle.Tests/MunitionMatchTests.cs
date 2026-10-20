using Dizzy.FirewoodBundle;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests
{
    public class MunitionMatchTests
    {
        [Fact]
        public void SameCalibre_Bundles()
        {
            Assert.Null(MunitionMatch.Refusal(MunitionMatch.Shot4, MunitionMatch.Shot4, 1, 1));
            Assert.Null(MunitionMatch.Refusal(MunitionMatch.Shot18, MunitionMatch.Shot18, 1, 1));
            Assert.Null(MunitionMatch.Refusal(MunitionMatch.Canister, MunitionMatch.Canister, 1, 1));
        }

        [Fact]
        public void DifferentCalibre_Refuses()
        {
            Assert.Equal(
                MunitionMatch.DifferentCalibre,
                MunitionMatch.Refusal(MunitionMatch.Shot4, MunitionMatch.Shot12, 1, 1));
            Assert.Equal(
                MunitionMatch.DifferentCalibre,
                MunitionMatch.Refusal(MunitionMatch.Shot12, MunitionMatch.Shot18, 1, 1));
        }

        [Fact]
        public void Crates_AreNotLooseShot()
        {
            Assert.False(MunitionMatch.IsRoundShot(31012));
            Assert.False(MunitionMatch.IsCanister(31015));
            Assert.False(MunitionMatch.IsKeg(31007));
        }

        [Fact]
        public void FullKegs_OfOneSize_Bundle()
        {
            Assert.Null(MunitionMatch.Refusal(
                MunitionMatch.KegSmall,
                MunitionMatch.KegSmall,
                MunitionMatch.SmallCharges,
                MunitionMatch.SmallCharges));
            Assert.Null(MunitionMatch.Refusal(
                MunitionMatch.KegLarge,
                MunitionMatch.KegLarge,
                MunitionMatch.LargeCharges,
                MunitionMatch.LargeCharges));
            Assert.Equal(25, MunitionMatch.FullCharges(MunitionMatch.KegSmall));
            Assert.Equal(100, MunitionMatch.FullCharges(MunitionMatch.KegLarge));
        }

        [Fact]
        public void DifferentKegs_AndOpenedKegs_Refuse()
        {
            Assert.Equal(
                MunitionMatch.DifferentKeg,
                MunitionMatch.Refusal(MunitionMatch.KegSmall, MunitionMatch.KegLarge, 25, 100));
            Assert.Equal(
                MunitionMatch.OpenedKeg,
                MunitionMatch.Refusal(MunitionMatch.KegSmall, MunitionMatch.KegSmall, 24, 25));
            Assert.Equal(
                MunitionMatch.OpenedKeg,
                MunitionMatch.Refusal(MunitionMatch.KegLarge, MunitionMatch.KegLarge, 100, 0));
        }
    }
}
