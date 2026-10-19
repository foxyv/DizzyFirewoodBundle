using Dizzy.FirewoodBundle;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests
{
    public class ColliderStrategyTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Bag_UsesHullWhetherHangingOrNot(bool hanging)
        {
            Assert.Equal(
                ColliderStrategy.BagHull,
                BundleColliderMath.ChooseStrategy(true, false, hanging, false));
            Assert.Equal(
                ColliderStrategy.BagHull,
                BundleColliderMath.ChooseStrategy(true, false, hanging, true));
        }

        [Fact]
        public void HangingSausage_UsesABoxPerSausage()
        {
            Assert.Equal(
                ColliderStrategy.PieceBoxes,
                BundleColliderMath.ChooseStrategy(false, false, true, true));
        }

        [Fact]
        public void HangingDate_UsesHangAabb()
        {
            Assert.Equal(
                ColliderStrategy.HangAabb,
                BundleColliderMath.ChooseStrategy(false, true, true, false));
        }

        [Fact]
        public void TreeWithTurns_UsesHull()
        {
            Assert.Equal(
                ColliderStrategy.Hull,
                BundleColliderMath.ChooseStrategy(false, false, false, true));
        }

        [Fact]
        public void SausagePile_UsesABoxPerSausage()
        {
            Assert.Equal(
                ColliderStrategy.PieceBoxes,
                BundleColliderMath.ChooseStrategy(false, false, false, true, true));
        }

        [Fact]
        public void FirewoodAutoStack_UsesStackAabb()
        {
            Assert.Equal(
                ColliderStrategy.StackAabb,
                BundleColliderMath.ChooseStrategy(false, false, false, false));
        }

        [Fact]
        public void ExpandPad_MatchesHangAndStack()
        {
            Assert.Equal(0.01f, BundleColliderMath.ExpandPad(ColliderStrategy.HangAabb));
            Assert.Equal(0.04f, BundleColliderMath.ExpandPad(ColliderStrategy.StackAabb));
            Assert.Equal(0f, BundleColliderMath.ExpandPad(ColliderStrategy.BagHull));
            Assert.Equal(0f, BundleColliderMath.ExpandPad(ColliderStrategy.Hull));
        }
    }
}
