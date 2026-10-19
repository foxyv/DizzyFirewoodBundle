using Dizzy.FirewoodBundle;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests
{
    public class CapsuleFitTests
    {
        [Fact]
        public void LongestAxisChoosesDirection()
        {
            int direction;
            ColliderVec3 center;
            float radius;
            float height;

            BundleColliderMath.CapsuleFit(0.4f, 0.1f, 0.1f, 0f, 0f, 0f, out direction, out center, out radius, out height);
            Assert.Equal(0, direction);

            BundleColliderMath.CapsuleFit(0.1f, 0.5f, 0.1f, 0f, 0f, 0f, out direction, out center, out radius, out height);
            Assert.Equal(1, direction);

            BundleColliderMath.CapsuleFit(0.1f, 0.1f, 0.6f, 0f, 0f, 0f, out direction, out center, out radius, out height);
            Assert.Equal(2, direction);
        }

        [Fact]
        public void HeightIsAtLeastTwiceRadius()
        {
            int direction;
            ColliderVec3 center;
            float radius;
            float height;
            BundleColliderMath.CapsuleFit(
                0.05f,
                0.05f,
                0.05f,
                1f,
                2f,
                3f,
                out direction,
                out center,
                out radius,
                out height);

            Assert.Equal(1f, center.X, 3);
            Assert.Equal(2f, center.Y, 3);
            Assert.Equal(3f, center.Z, 3);
            Assert.True(height + 0.0001f >= radius * 2f);
        }

        [Fact]
        public void HangingBananaBunch_StaysACapsuleNotASphere()
        {
            // The prefab capsule points along the fruit (X). A hanging bunch is tall.
            // Fit along that length: the radius is the bunch's width, not its height.
            int direction;
            ColliderVec3 center;
            float radius;
            float height;
            BundleColliderMath.CapsuleFit(0.4f, 0.9f, 0.35f, 0f, -0.45f, 0f, out direction, out center, out radius, out height);

            Assert.Equal(1, direction);
            Assert.Equal(0.2f, radius, 3);
            Assert.Equal(0.9f, height, 3);
            Assert.True(radius * 2f < height - 0.2f);
        }
    }
}
