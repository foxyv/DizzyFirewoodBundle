using Dizzy.FirewoodBundle;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests
{
    // A bag rebuilt while held is on the held layer. Drop and hang move the bag
    // back to the world layer and used to leave the hull behind, so the look ray
    // passed through the net and hit only the small core.
    public class LookSurfaceLayerTests
    {
        [Fact]
        public void BuiltWhileHeld_FollowsTheBagBackToTheWorld()
        {
            int surface = BundleColliderMath.LookSurfaceLayer(BundleColliderMath.WorldLayer);
            Assert.Equal(BundleColliderMath.WorldLayer, surface);
            Assert.NotEqual(BundleColliderMath.HeldLayer, surface);
        }

        [Fact]
        public void StillHeld_StaysOffTheLookRay()
        {
            int surface = BundleColliderMath.LookSurfaceLayer(BundleColliderMath.HeldLayer);
            Assert.Equal(BundleColliderMath.HeldLayer, surface);
        }
    }
}
