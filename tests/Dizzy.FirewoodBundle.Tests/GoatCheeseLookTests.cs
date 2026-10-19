using Dizzy.FirewoodBundle;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests
{
    // Issue #26: a hanging goat cheese bag is often only clickable at the lamp hook,
    // because the hook's box is closer to the camera than the cheese.
    public class GoatCheeseLookTests
    {
        [Fact]
        public void HookInFrontOfBag_AimsAtTheBag()
        {
            float[] distances = { 1.0f, 1.2f };
            BagLookHit[] kinds = { BagLookHit.LampHook, BagLookHit.HangingBag };
            int aim = BundleColliderMath.HangingBagToAim(distances, kinds, true, 1.0f, true, false);
            Assert.Equal(1, aim);
        }

        [Fact]
        public void NothingLookedAt_AimsAtTheBagBehindTheHook()
        {
            float[] distances = { 0.9f, 1.15f };
            BagLookHit[] kinds = { BagLookHit.LampHook, BagLookHit.HangingBag };
            int aim = BundleColliderMath.HangingBagToAim(distances, kinds, false, 0f, false, false);
            Assert.Equal(1, aim);
        }

        [Fact]
        public void WallInFront_LeavesTheLook()
        {
            float[] distances = { 0.7f, 1.0f, 1.2f };
            BagLookHit[] kinds = { BagLookHit.Other, BagLookHit.LampHook, BagLookHit.HangingBag };
            int aim = BundleColliderMath.HangingBagToAim(distances, kinds, true, 0.7f, false, false);
            Assert.Equal(-1, aim);
        }

        [Fact]
        public void CloserCrate_StaysTheTarget()
        {
            float[] distances = { 0.8f, 1.3f };
            BagLookHit[] kinds = { BagLookHit.Other, BagLookHit.HangingBag };
            int aim = BundleColliderMath.HangingBagToAim(distances, kinds, true, 0.8f, false, false);
            Assert.Equal(-1, aim);
        }

        [Fact]
        public void AlreadyLookingAtTheBag_Stays()
        {
            float[] distances = { 1.0f, 1.2f };
            BagLookHit[] kinds = { BagLookHit.LampHook, BagLookHit.HangingBag };
            int aim = BundleColliderMath.HangingBagToAim(distances, kinds, true, 1.2f, false, true);
            Assert.Equal(-1, aim);
        }

        [Fact]
        public void NoBagOnTheRay_LeavesTheLook()
        {
            float[] distances = { 1.0f };
            BagLookHit[] kinds = { BagLookHit.LampHook };
            int aim = BundleColliderMath.HangingBagToAim(distances, kinds, true, 1.0f, true, false);
            Assert.Equal(-1, aim);
        }
    }
}
