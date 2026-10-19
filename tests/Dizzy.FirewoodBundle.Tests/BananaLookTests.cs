using Dizzy.FirewoodBundle;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests
{
    public class BananaLookTests
    {
        // Dried banana mesh, metres. The capsule is fitted to this box.
        const float SizeX = 0.49f;
        const float SizeY = 0.152f;
        const float SizeZ = 0.092f;

        [Fact]
        public void CrosshairOnABanana_HitsThatBananasCapsule()
        {
            int direction;
            ColliderVec3 center;
            float radius;
            float height;
            BundleColliderMath.CapsuleFit(SizeX, SizeY, SizeZ, 0f, 0f, 0f, out direction, out center, out radius, out height);

            // The fruit reaches the middle of each face. A ray from outside through
            // that point is a crosshair sitting on the banana.
            var faces = new[]
            {
                new ColliderVec3(SizeX * 0.5f, 0f, 0f),
                new ColliderVec3(-SizeX * 0.5f, 0f, 0f),
                new ColliderVec3(0f, SizeY * 0.5f, 0f),
                new ColliderVec3(0f, -SizeY * 0.5f, 0f),
                new ColliderVec3(0f, 0f, SizeZ * 0.5f),
                new ColliderVec3(0f, 0f, -SizeZ * 0.5f),
            };
            var eye = new ColliderVec3(0.2f, -0.4f, 1.6f);
            foreach (ColliderVec3 face in faces)
            {
                Assert.True(
                    BundleColliderMath.RayHitsCapsule(eye, face, direction, center, radius, height),
                    "Look ray missed the banana at (" + face.X + ", " + face.Y + ", " + face.Z + ").");
            }
        }

        [Fact]
        public void OuterBanana_IsOutsideTheCore_AndTheRayStillHitsIt()
        {
            int direction;
            ColliderVec3 localCenter;
            float radius;
            float height;
            BundleColliderMath.CapsuleFit(SizeX, SizeY, SizeZ, 0f, 0f, 0f, out direction, out localCenter, out radius, out height);

            // A fruit out on the bunch, and the old shrunk core near the stalk.
            var fruit = new ColliderVec3(0.4f, -0.8f, 0.2f);
            var center = new ColliderVec3(fruit.X + localCenter.X, fruit.Y + localCenter.Y, fruit.Z + localCenter.Z);
            var onBanana = new ColliderVec3(center.X, center.Y + SizeY * 0.5f, center.Z);
            var eye = new ColliderVec3(onBanana.X, onBanana.Y, onBanana.Z + 1.5f);

            var coreCenter = new ColliderVec3(0f, -0.15f, 0f);
            Assert.False(BundleColliderMath.CapsuleContains(onBanana, 1, coreCenter, 0.04f, 0.08f));
            Assert.False(BundleColliderMath.RayHitsCapsule(eye, onBanana, 1, coreCenter, 0.04f, 0.08f));
            Assert.True(BundleColliderMath.RayHitsCapsule(eye, onBanana, direction, center, radius, height));
        }

        [Fact]
        public void AirBesideABanana_IsNotACollider()
        {
            int direction;
            ColliderVec3 center;
            float radius;
            float height;
            BundleColliderMath.CapsuleFit(SizeX, SizeY, SizeZ, 0f, 0f, 0f, out direction, out center, out radius, out height);

            // 15 cm past the fruit, where the old bunch hull was solid air.
            var gap = new ColliderVec3(0f, SizeY * 0.5f + 0.15f, 0f);
            var eye = new ColliderVec3(gap.X, gap.Y, 1.5f);
            Assert.False(BundleColliderMath.CapsuleContains(gap, direction, center, radius, height));
            Assert.False(BundleColliderMath.RayHitsCapsule(eye, gap, direction, center, radius, height));
        }

        [Fact]
        public void MeanSurfaceDistance_AveragesDistanceToTheSkin()
        {
            var onSkin = new ColliderVec3(0.1f, 0f, 0f);
            var outside = new ColliderVec3(0.2f, 0f, 0f);
            float mean = BundleColliderMath.MeanSurfaceDistance(
                new[] { onSkin, outside },
                1,
                new ColliderVec3(0f, 0f, 0f),
                0.1f,
                0.4f);
            Assert.Equal(0.05f, mean, 3);
        }

        [Fact]
        public void BananaCapsule_StaysWithinAFewCentimetresOfTheMesh()
        {
            int direction;
            ColliderVec3 center;
            float radius;
            float height;
            BundleColliderMath.CapsuleFit(0.4896f, 0.1528f, 0.0920f, 0.0102f, 0.0046f, -0.0001f, out direction, out center, out radius, out height);

            ColliderVec3[] skin = BananaSkin();
            float mean = BundleColliderMath.MeanSurfaceDistance(skin, direction, center, radius, height);
            Assert.True(mean < 0.03f, "Mean distance from the banana mesh to its capsule is " + mean + " m.");

            // The same skin against a capsule that stands 8 cm clear of the fruit.
            float stoodOff = BundleColliderMath.MeanSurfaceDistance(skin, direction, center, radius + 0.08f, height);
            Assert.True(stoodOff > 0.05f, "A capsule standing off the fruit should read as a large gap.");
        }

        // Cross-section corners of the dried banana mesh, along its length.
        private static ColliderVec3[] BananaSkin()
        {
            float[] slices =
            {
                -0.2346f, 0.0336f, 0.0675f, -0.0265f, 0.0272f,
                -0.1734f, 0.0635f, 0.0635f, -0.0196f, 0.0203f,
                -0.1122f, -0.0493f, 0.0339f, -0.0462f, 0.0459f,
                0.0102f, -0.0718f, 0.0114f, -0.0462f, 0.0459f,
                0.1326f, -0.0511f, 0.0320f, -0.0462f, 0.0459f,
                0.1938f, 0.0369f, 0.0679f, -0.0183f, 0.0166f,
                0.2550f, 0.0566f, 0.0810f, -0.0193f, 0.0157f,
            };
            var skin = new ColliderVec3[slices.Length / 5 * 4];
            int n = 0;
            for (int i = 0; i < slices.Length; i += 5)
            {
                float x = slices[i];
                float y0 = slices[i + 1];
                float y1 = slices[i + 2];
                float z0 = slices[i + 3];
                float z1 = slices[i + 4];
                float y = (y0 + y1) * 0.5f;
                float z = (z0 + z1) * 0.5f;
                skin[n++] = new ColliderVec3(x, y0, z);
                skin[n++] = new ColliderVec3(x, y1, z);
                skin[n++] = new ColliderVec3(x, y, z0);
                skin[n++] = new ColliderVec3(x, y, z1);
            }

            return skin;
        }
    }
}
