using Dizzy.FirewoodBundle;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests
{
    // A long line used to exceed mass 2 and could not go in an item box. In a crate the
    // line keeps the prefab collider so the UI can draw it small.
    public class HookLineColliderPolicyTests
    {
        [Fact]
        public void CrateId_KeepsPrefabCollider()
        {
            Assert.True(BundleColliderMath.UsePrefabColliderInCrate(12));
            Assert.False(BundleColliderMath.UsePrefabColliderInCrate(0));
        }

        [Fact]
        public void Mass_IsCappedAtTwo()
        {
            Assert.Equal(2f, BundleColliderMath.HookLineMass(0.2f, 20), 3);
            Assert.Equal(2f, BundleColliderMath.HookLineMass(1f, 5), 3);
        }

        [Fact]
        public void Mass_ScalesBelowTheCap()
        {
            Assert.Equal(0.6f, BundleColliderMath.HookLineMass(0.2f, 3), 3);
        }

        [Fact]
        public void Mass_UsesFallbackBaseWhenMissing()
        {
            Assert.Equal(0.4f, BundleColliderMath.HookLineMass(0f, 2), 3);
        }
    }
}
