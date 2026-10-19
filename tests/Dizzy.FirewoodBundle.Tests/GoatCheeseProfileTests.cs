using Dizzy.FirewoodBundle;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests
{
    // Locks the goat cheese round silhouette used by bag nets and colliders (#26).
    public class GoatCheeseProfileTests
    {
        [Fact]
        public void RoundWidth_IsWidestOnTheLowSide()
        {
            float low = BundleColliderMath.RoundWidth(-0.79f);
            float mid = BundleColliderMath.RoundWidth(0f);
            float high = BundleColliderMath.RoundWidth(0.77f);
            Assert.Equal(1f, low, 3);
            Assert.InRange(mid, 0.9f, 1f);
            Assert.Equal(0.9f, high, 3);
            Assert.True(low >= mid);
            Assert.True(mid >= high);
        }

        [Fact]
        public void RoundWidth_BevelsBothFaces()
        {
            float bottomFace = BundleColliderMath.RoundWidth(-1f);
            float topFace = BundleColliderMath.RoundWidth(1f);
            Assert.Equal(0.84f, bottomFace, 3);
            Assert.Equal(0.725f, topFace, 3);
            Assert.True(bottomFace < 1f);
            Assert.True(topFace < 0.9f);
        }

        [Theory]
        [InlineData(-0.4f)]
        [InlineData(0.2f)]
        [InlineData(0.5f)]
        public void RoundWidth_MidHeightsStayBetweenNineTenthsAndOne(float height)
        {
            float width = BundleColliderMath.RoundWidth(height);
            Assert.InRange(width, 0.9f, 1f);
        }
    }
}
