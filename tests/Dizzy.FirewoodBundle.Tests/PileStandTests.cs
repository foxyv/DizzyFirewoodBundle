using System.Collections.Generic;
using Dizzy.FirewoodBundle;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests
{
    public class PileStandTests
    {
        [Fact]
        public void FloorCorners_SetTheStand_AHighSausageDoesNot()
        {
            var corners = new List<ColliderVec3>
            {
                new ColliderVec3(-0.4f, 0f, -0.2f),
                new ColliderVec3(0.4f, 0.02f, 0.2f),
                new ColliderVec3(2f, 0.8f, 2f)
            };

            ColliderBounds stand = BundleColliderMath.PileStand(corners, 1, 0.05f);

            Assert.Equal(0f, stand.Center.X, 3);
            Assert.Equal(0f, stand.Center.Z, 3);
            Assert.Equal(0.82f, stand.Size.X, 3);
            Assert.Equal(0.42f, stand.Size.Z, 3);
        }

        [Fact]
        public void Stand_IsShort_AndSitsOnTheLowestPoint()
        {
            var corners = new List<ColliderVec3>
            {
                new ColliderVec3(0f, -0.3f, 0f),
                new ColliderVec3(0.2f, -0.28f, 0.1f)
            };

            ColliderBounds stand = BundleColliderMath.PileStand(corners, 1, 0.05f);

            Assert.Equal(BundleColliderMath.PileStandHeight, stand.Size.Y, 3);
            Assert.Equal(-0.3f + BundleColliderMath.PileStandHeight * 0.5f, stand.Center.Y, 3);
            Assert.Equal(-0.3f, stand.Center.Y - stand.Size.Y * 0.5f, 3);
        }

        [Fact]
        public void EmptyPile_GetsASmallStand()
        {
            ColliderBounds stand = BundleColliderMath.PileStand(new List<ColliderVec3>(), 1, 0.05f);

            Assert.True(stand.Size.X > 0f);
            Assert.True(stand.Size.Y > 0f);
            Assert.True(stand.Size.Z > 0f);
        }
    }
}
