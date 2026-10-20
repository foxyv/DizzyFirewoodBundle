using Dizzy.FirewoodBundle;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests
{
    public class ShotPyramidTests
    {
        const float Diameter = 0.14f;

        [Fact]
        public void Five_IsAPointOnASquare()
        {
            ShotPlace[] places = ShotPyramid.Layout(5, Diameter);
            Assert.Equal(5, places.Length);
            int ground = 0;
            int crown = 0;
            float rise = Diameter * 0.70710677f;
            for (int i = 0; i < places.Length; i++)
            {
                if (places[i].Y < 0.001f)
                    ground++;
                else
                {
                    crown++;
                    Assert.InRange(places[i].Y, rise - 0.0001f, rise + 0.0001f);
                    Assert.InRange(places[i].X, -0.0001f, 0.0001f);
                    Assert.InRange(places[i].Z, -0.0001f, 0.0001f);
                }
            }

            Assert.Equal(4, ground);
            Assert.Equal(1, crown);
            Assert.True(MinDistance(places) >= Diameter - 0.0001f);
        }

        [Fact]
        public void Fourteen_IsThreeLayers()
        {
            ShotPlace[] places = ShotPyramid.Layout(14, Diameter);
            Assert.Equal(14, places.Length);
            int[] layers = new int[3];
            float rise = Diameter * 0.70710677f;
            for (int i = 0; i < places.Length; i++)
            {
                int layer = (int)System.Math.Round(places[i].Y / rise);
                Assert.InRange(layer, 0, 2);
                layers[layer]++;
            }

            Assert.Equal(9, layers[0]);
            Assert.Equal(4, layers[1]);
            Assert.Equal(1, layers[2]);
            Assert.True(MinDistance(places) >= Diameter - 0.0001f);
        }

        [Fact]
        public void Two_SitSideBySide()
        {
            ShotPlace[] places = ShotPyramid.Layout(2, Diameter);
            Assert.Equal(2, places.Length);
            Assert.Equal(0f, places[0].Y);
            Assert.Equal(0f, places[1].Y);
            Assert.InRange(MinDistance(places), Diameter - 0.0001f, Diameter + 0.0001f);
        }

        [Fact]
        public void Collider_IsOnePyramidInsideTheBalls()
        {
            ShotPlace[] balls = ShotPyramid.Layout(14, Diameter);
            float radius = Diameter * 0.5f;
            ShotPlace[] hull = ShotPyramid.Collider(balls, radius);
            Assert.Equal(5, hull.Length);
            for (int i = 0; i < 4; i++)
                Assert.True(hull[i].Y < hull[4].Y);
            for (int i = 0; i < hull.Length; i++)
            {
                float nearest = float.MaxValue;
                for (int b = 0; b < balls.Length; b++)
                {
                    float dx = hull[i].X - balls[b].X;
                    float dy = hull[i].Y - balls[b].Y;
                    float dz = hull[i].Z - balls[b].Z;
                    float distance = (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (distance < nearest)
                        nearest = distance;
                }

                Assert.True(nearest < radius);
            }
        }

        [Fact]
        public void Collider_OfFive_PeaksOverTheMiddle()
        {
            ShotPlace[] balls = ShotPyramid.Layout(5, Diameter);
            ShotPlace[] hull = ShotPyramid.Collider(balls, Diameter * 0.5f);
            Assert.InRange(hull[4].X, -0.0001f, 0.0001f);
            Assert.InRange(hull[4].Z, -0.0001f, 0.0001f);
            Assert.True(hull[4].Y > 0f);
            for (int i = 0; i < 4; i++)
                Assert.True(hull[i].Y < 0f);
        }

        static float MinDistance(ShotPlace[] places)
        {
            float best = float.MaxValue;
            for (int i = 0; i < places.Length; i++)
            {
                for (int j = i + 1; j < places.Length; j++)
                {
                    float dx = places[i].X - places[j].X;
                    float dy = places[i].Y - places[j].Y;
                    float dz = places[i].Z - places[j].Z;
                    float distance = (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (distance < best)
                        best = distance;
                }
            }

            return best;
        }
    }
}
