using Dizzy.FirewoodBundle;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests
{
    // Tent and Plank poles store length in health, stepped to the nearest 0.1 m.
    // A bundle only accepts poles that land on the same step.
    public class PoleLengthTests
    {
        [Fact]
        public void SameStep_Bundles()
        {
            Assert.True(BundleColliderMath.SamePoleLength(2f, 2.04f));
            Assert.Equal(4f, BundleColliderMath.PoleMass(2f));
            Assert.Equal("2.0 m", BundleColliderMath.PoleLengthText(2.04f));
            Assert.Equal(1f, BundleColliderMath.PoleBundleMetres);
            Assert.Equal(2f, BundleColliderMath.PoleMass(BundleColliderMath.PoleBundleMetres));
        }

        [Fact]
        public void NextStep_Refuses()
        {
            Assert.False(BundleColliderMath.SamePoleLength(2f, 2.06f));
            Assert.Equal("2.1 m", BundleColliderMath.PoleLengthText(2.06f));
        }

        [Fact]
        public void OutsideTheRange_ClampsToTheEnds()
        {
            Assert.Equal(0.2f, BundleColliderMath.PoleLength(0.01f));
            Assert.Equal(8f, BundleColliderMath.PoleLength(12f));
            Assert.Equal(2f, BundleColliderMath.PoleLength(float.NaN));
            Assert.True(BundleColliderMath.SamePoleLength(0.01f, 0.2f));
        }
    }
}
