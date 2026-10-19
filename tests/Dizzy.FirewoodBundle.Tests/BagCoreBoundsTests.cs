using Dizzy.FirewoodBundle;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests
{
    // Bag nets once used piece AABBs and sat too far apart. The core must sit inside
    // the widest ring and above the rim.
    public class BagCoreBoundsTests
    {
        [Fact]
        public void Core_IsNoWiderThanReachTimesCoreShare()
        {
            const float plusX = 0.2f;
            const float plusZ = 0.18f;
            const float minusX = 0.2f;
            const float minusZ = 0.18f;
            ColliderBounds core = BundleColliderMath.BagCoreBounds(plusX, plusZ, minusX, minusZ, -0.1f, 0f, 0.05f);

            Assert.Equal((plusX + minusX) * BundleColliderMath.CoreShare, core.Size.X, 3);
            Assert.Equal((plusZ + minusZ) * BundleColliderMath.CoreShare, core.Size.Z, 3);
        }

        [Fact]
        public void Core_SitsAboveTheRim()
        {
            const float rim = -0.12f;
            ColliderBounds core = BundleColliderMath.BagCoreBounds(0.25f, 0.25f, 0.25f, 0.25f, rim, -0.02f, 0.04f);
            Assert.True(core.MinY >= rim - 0.0001f);
            Assert.True(core.Center.Y > rim);
        }

        [Fact]
        public void Core_HasMinimumFootprint()
        {
            ColliderBounds core = BundleColliderMath.BagCoreBounds(0.01f, 0.01f, 0.01f, 0.01f, 0f, 0.05f, 0.1f);
            Assert.True(core.Size.X >= 0.05f);
            Assert.True(core.Size.Z >= 0.05f);
            Assert.True(core.Size.Y >= 0.02f);
        }
    }
}
