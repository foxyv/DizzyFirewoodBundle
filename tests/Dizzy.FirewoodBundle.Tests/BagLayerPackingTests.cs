using System.Collections.Generic;
using Dizzy.FirewoodBundle;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests
{
    public class BagLayerPackingTests
    {
        [Fact]
        public void GoatCheese_TwoRoundsStack()
        {
            List<int> layers = BundleColliderMath.PackBagLayers(2, 5, true);
            Assert.Equal(new[] { 1, 1 }, layers);
        }

        [Fact]
        public void GoatCheese_FourRoundsPutPairInMiddle()
        {
            List<int> layers = BundleColliderMath.PackBagLayers(4, 5, true);
            Assert.Equal(new[] { 1, 2, 1 }, layers);
        }

        [Fact]
        public void Fruit_ForcesThreeRowsWhenCountAtLeastThree()
        {
            List<int> layers = BundleColliderMath.PackBagLayers(3, 7, false);
            Assert.Equal(3, layers.Count);
            Assert.Equal(3, layers[0] + layers[1] + layers[2]);
        }

        [Fact]
        public void Fruit_LoneTopMergesFromLayerBelow()
        {
            // Base 5 and count 11 packs as 5,5,1. The lone top takes one from under it.
            List<int> layers = BundleColliderMath.PackBagLayers(11, 5, false);
            Assert.Equal(new[] { 5, 4, 2 }, layers);
        }

        [Fact]
        public void EmptyCount_ReturnsNoLayers()
        {
            Assert.Empty(BundleColliderMath.PackBagLayers(0, 5, false));
        }
    }
}
