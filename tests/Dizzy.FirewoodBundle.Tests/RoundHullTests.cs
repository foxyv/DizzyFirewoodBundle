using System.Collections.Generic;
using Dizzy.FirewoodBundle;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests
{
    // A convex bag hull is straight between rings. Rings have to sit on the
    // goat-cheese shoulders, or the side cuts through the round (#26).
    public class RoundHullTests
    {
        [Fact]
        public void Peak_StaysOnTheHull()
        {
            float[] height = { 0f, 1f, 2f };
            float[] radius = { 0f, 1f, 0f };
            List<int> hull = BundleColliderMath.UpperHull(height, radius);
            Assert.Equal(new[] { 0, 1, 2 }, hull);
        }

        [Fact]
        public void Valley_DropsBelowTheChord()
        {
            float[] height = { 0f, 1f, 2f };
            float[] radius = { 1f, 0f, 1f };
            List<int> hull = BundleColliderMath.UpperHull(height, radius);
            Assert.Equal(new[] { 0, 2 }, hull);
        }

        [Fact]
        public void CollinearPoint_DropsOut()
        {
            float[] height = { 0f, 1f, 2f };
            float[] radius = { 0f, 0.5f, 1f };
            List<int> hull = BundleColliderMath.UpperHull(height, radius);
            Assert.Equal(new[] { 0, 2 }, hull);
            Assert.True(BundleColliderMath.RadiusGap(height, radius, 0, 1, 2) < 0.0001f);
        }

        [Fact]
        public void RoundShoulder_SticksOutPastTheChordAboveIt()
        {
            // Widest low on the side, then the upper bevel. A chord from the
            // widest point to the top face cuts the shoulder.
            const float half = 0.0836f;
            const float wide = 0.1381f;
            float[] norm = { -0.79f, 0.77f, 1f };
            var height = new float[norm.Length];
            var radius = new float[norm.Length];
            for (int i = 0; i < norm.Length; i++)
            {
                height[i] = norm[i] * half;
                radius[i] = wide * BundleColliderMath.RoundWidth(norm[i]);
            }

            float gap = BundleColliderMath.RadiusGap(height, radius, 0, 1, 2);
            Assert.True(gap > 0.005f);

            List<int> hull = BundleColliderMath.UpperHull(height, radius);
            Assert.Contains(1, hull);
        }

        [Fact]
        public void WiderMiddleRound_StaysOutsideTheEnds()
        {
            float[] height = { 0f, 1f, 2f };
            float[] radius = { 0.14f, 0.28f, 0.14f };
            List<int> hull = BundleColliderMath.UpperHull(height, radius);
            Assert.Contains(1, hull);
            BundleColliderMath.LimitHull(height, radius, hull, 2);
            Assert.Equal(new[] { 0, 2 }, hull);
        }

        [Fact]
        public void EvenRings_CutTheUpperBevel()
        {
            // The old bag hull stepped evenly from the widest point to the neck.
            // On a goat cheese round that chord misses the upper bevel (#26).
            const float half = 0.0836f;
            const float wide = 0.1381f;
            float widest = -0.79f * half;
            float neck = half + wide * 1.01f * 0.9f;
            float shoulder = 0.77f * half;
            float shoulderRadius = wide * BundleColliderMath.RoundWidth(0.77f);

            var height = new float[6];
            var radius = new float[6];
            for (int step = 0; step <= 5; step++)
            {
                float y = widest + (neck - widest) * step / 5f;
                height[step] = y;
                float norm = y / half;
                if (norm < -1f)
                    norm = -1f;
                if (norm > 1f)
                    norm = 1f;
                radius[step] = y > half ? 0.012f : wide * BundleColliderMath.RoundWidth(norm);
            }

            float covered = Envelope(BundleColliderMath.UpperHull(height, radius), height, radius, shoulder);
            Assert.True(shoulderRadius - covered > 0.005f);
        }

        [Fact]
        public void ShoulderRings_ContainTheUpperBevel()
        {
            const float half = 0.0836f;
            const float wide = 0.1381f;
            float[] norm = BundleColliderMath.RoundShoulders;
            var height = new float[norm.Length];
            var radius = new float[norm.Length];
            for (int i = 0; i < norm.Length; i++)
            {
                height[i] = norm[i] * half;
                radius[i] = wide * BundleColliderMath.RoundWidth(norm[i]);
            }

            List<int> hull = BundleColliderMath.UpperHull(height, radius);
            float shoulder = 0.77f * half;
            float covered = Envelope(hull, height, radius, shoulder);
            float need = wide * BundleColliderMath.RoundWidth(0.77f);
            Assert.True(covered >= need - 0.0001f);
        }

        [Fact]
        public void StackedRounds_KeepTheTopShoulder()
        {
            // Two rounds, the top one a cheese-height above the bottom. Even steps
            // from the bottom widest up to the neck cut the top bevel.
            const float half = 0.0836f;
            const float wide = 0.1381f;
            float step = (half * 2f) * 1.03f;
            float top = step;
            float shoulder = top + 0.77f * half;
            float need = wide * BundleColliderMath.RoundWidth(0.77f);

            var height = new List<float>();
            var radius = new List<float>();
            for (int layer = 0; layer < 2; layer++)
            {
                float center = layer * step;
                for (int i = 0; i < BundleColliderMath.RoundShoulders.Length; i++)
                {
                    float norm = BundleColliderMath.RoundShoulders[i];
                    height.Add(center + norm * half);
                    radius.Add(wide * BundleColliderMath.RoundWidth(norm));
                }
            }

            List<int> hull = BundleColliderMath.UpperHull(height, radius);
            float covered = Envelope(hull, height, radius, shoulder);
            Assert.True(covered >= need - 0.0001f);
        }

        [Fact]
        public void LimitHull_KeepsTheEnds()
        {
            float[] height = { 0f, 1f, 2f, 3f };
            float[] radius = { 0f, 0.01f, 0.02f, 0f };
            List<int> hull = BundleColliderMath.UpperHull(height, radius);
            BundleColliderMath.LimitHull(height, radius, hull, 2);
            Assert.Equal(2, hull.Count);
            Assert.Equal(0, hull[0]);
            Assert.Equal(3, hull[1]);
        }

        private static float Envelope(List<int> hull, IList<float> height, IList<float> radius, float at)
        {
            float best = float.MinValue;
            for (int i = 0; i + 1 < hull.Count; i++)
            {
                float y0 = height[hull[i]];
                float y1 = height[hull[i + 1]];
                if (at < y0 - 0.00001f || at > y1 + 0.00001f)
                    continue;
                float span = y1 - y0;
                float t = span > 0.0000001f ? (at - y0) / span : 0f;
                float chord = radius[hull[i]] + (radius[hull[i + 1]] - radius[hull[i]]) * t;
                if (chord > best)
                    best = chord;
            }

            return best;
        }
    }
}
