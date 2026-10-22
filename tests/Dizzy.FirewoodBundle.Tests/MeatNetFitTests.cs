using System;
using System.Collections.Generic;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests
{
    // How well the meat bag's net fits the real meat. The chops are the game's own meshes,
    // flat, so the tests measure the net against what a player would see.
    public class MeatNetFitTests
    {
        private const float Cord = 0.00375f;
        private const float Gap = 0.004f;
        private const float Thickness = 0.054f;
        private const int Directions = 720;

        // A strand is straight lines between points, so at the lamb's sharpest tip it cuts the
        // corner by up to about 2 mm. That is a third of the cord's thickness (7.5 mm).
        private const float Tolerance = 0.0025f;

        // ---- The real meat ----

        private static float[] Triangles(string chop)
        {
            return chop == "lamb" ? ChopFixtures.LambTriangles : ChopFixtures.PorkTriangles;
        }

        // How far the meat reaches from the middle of its bounds, in a direction.
        private static float Reach(float[] triangles, double angle)
        {
            double dx = Math.Cos(angle);
            double dz = Math.Sin(angle);
            double far = 0d;
            for (int i = 0; i < triangles.Length; i += 6)
            {
                for (int edge = 0; edge < 3; edge++)
                {
                    int a = i + edge * 2;
                    int b = i + ((edge + 1) % 3) * 2;
                    double px = triangles[a];
                    double pz = triangles[a + 1];
                    double ex = triangles[b] - px;
                    double ez = triangles[b + 1] - pz;
                    double denom = dx * ez - dz * ex;
                    if (Math.Abs(denom) < 1e-12)
                        continue;
                    double t = (px * ez - pz * ex) / denom;
                    double s = (px * dz - pz * dx) / denom;
                    if (t > far && s >= -1e-9 && s <= 1d + 1e-9)
                        far = t;
                }
            }
            return (float)far;
        }

        private static float[] Reaches(string chop)
        {
            float[] triangles = Triangles(chop);
            var reach = new float[Directions];
            for (int i = 0; i < Directions; i++)
                reach[i] = Reach(triangles, i * 2d * Math.PI / Directions);
            return reach;
        }

        // The points of the mesh, the way the builder reads them when the game lets it.
        private static List<float[]> Corners(string chop)
        {
            float[] triangles = Triangles(chop);
            var seen = new HashSet<string>();
            var corners = new List<float[]>();
            for (int i = 0; i < triangles.Length; i += 2)
            {
                string key = triangles[i].ToString("0.0000") + "," + triangles[i + 1].ToString("0.0000");
                if (seen.Add(key))
                    corners.Add(new[] { triangles[i], triangles[i + 1] });
            }
            return corners;
        }

        private static float NetAt(float[] outline, int direction)
        {
            return MeatBagLayout.NetRadius(outline, direction * 2f * (float)Math.PI / Directions, 0.05f, 0f, 0.10f, Cord, Gap);
        }

        // The net is a cord and a gap beyond the meat. Slack is anything more than that.
        // Less than a gap short of that is the cord pressing into the meat.
        private static float[] Slack(string chop, float[] outline)
        {
            float[] reach = Reaches(chop);
            var slack = new float[Directions];
            for (int i = 0; i < Directions; i++)
                slack[i] = NetAt(outline, i) - reach[i] - Cord - Gap;
            return slack;
        }

        // ---- The test data itself ----

        [Fact]
        public void Fixture_MatchesTheMeasuredMeshes()
        {
            // From scripts/measure_outline.py: pork reaches 10.5 to 20.5 cm.
            float[] pork = Reaches("pork");
            Assert.InRange(Min(pork), 0.103f, 0.108f);
            Assert.InRange(Max(pork), 0.203f, 0.207f);
            Assert.Equal(32, Corners("pork").Count);
            Assert.Equal(32, Corners("lamb").Count);
        }

        // ---- The net against the meat ----

        [Theory]
        [InlineData("pork")]
        [InlineData("lamb")]
        public void TableInTheCode_NeverGoesInsideTheMeat(string chop)
        {
            float[] slack = Slack(chop, MeatBagLayout.OutlineOf(chop == "lamb"));
            int inside = 0;
            for (int i = 0; i < Directions; i++)
            {
                if (slack[i] < -Gap - 0.0001f)
                    inside++;
            }
            Assert.True(inside == 0, chop + ": the net is inside the meat in " + inside + " of " + Directions + " directions.");
        }

        [Theory]
        [InlineData("pork")]
        [InlineData("lamb")]
        public void TableInTheCode_HugsTheMeat_AtMostOneCentimetreOfSlack(string chop)
        {
            float worst = Max(Slack(chop, MeatBagLayout.OutlineOf(chop == "lamb")));
            Assert.True(worst <= 0.010f, chop + ": the net stands " + (worst * 100f).ToString("0.0") + " cm further off the meat than it should.");
        }

        [Theory]
        [InlineData("pork")]
        [InlineData("lamb")]
        public void TableInTheCode_HugsTheMeat_OnAverageUnderFourMillimetres(string chop)
        {
            float[] slack = Slack(chop, MeatBagLayout.OutlineOf(chop == "lamb"));
            float total = 0f;
            for (int i = 0; i < slack.Length; i++)
                total += slack[i];
            float average = total / slack.Length;
            Assert.True(average <= 0.004f, chop + ": the net stands " + (average * 100f).ToString("0.0") + " cm off the meat on average.");
        }

        [Theory]
        [InlineData("pork")]
        [InlineData("lamb")]
        public void ShapesInTheCode_MatchTheMeshTriangles(string chop)
        {
            // The data in the code, not the table made from it: the table is judged by the
            // tests above.
            float[] edges = chop == "lamb" ? MeatChopShapes.LambEdges : MeatChopShapes.PorkEdges;
            float[] reach = Reaches(chop);
            float worst = 0f;
            for (int i = 0; i < Directions; i++)
                worst = Math.Max(worst, Math.Abs(MeatBagLayout.ReachAt(edges, i * 2d * Math.PI / Directions) - reach[i]));
            Assert.True(worst <= 0.0005f, chop + ": the shape in the code is " + (worst * 100f).ToString("0.00") + " cm away from the mesh in one direction.");
        }

        // ---- The strands ----

        // The tube's height for a number of chops, the way the builder lays it out.
        private static void Tube(int count, out float bottom, out float top, out float low, out float high)
        {
            float pitch;
            MeatBagLayout.Slot(1, Thickness, out pitch);
            low = 0f;
            high = (count - 1) * pitch + Thickness;
            bottom = low - MeatBagLayout.DefaultBottomDepth;
            top = high + MeatBagLayout.CapDepth;
        }

        // The distance between each strand and the next one round the tube at a height,
        // both families together.
        private static List<float> RingGaps(MeatNetPath path, float y, float bottom, float top, float low, float high)
        {
            var points = new List<float[]>();
            for (int family = 0; family < 2; family++)
            {
                for (int strand = 0; strand < path.Strands; strand++)
                {
                    float x;
                    float z;
                    MeatBagLayout.StrandPoint(path, strand, family, y, bottom, top, low, high, out x, out z);
                    points.Add(new[] { x, z, (float)Math.Atan2(z, x) });
                }
            }
            points.Sort((a, b) => a[2].CompareTo(b[2]));

            var gaps = new List<float>();
            for (int i = 0; i < points.Count; i++)
            {
                float[] a = points[i];
                float[] b = points[(i + 1) % points.Count];
                gaps.Add((float)Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1])));
            }
            return gaps;
        }

        private static string Where(float y, float low, float high)
        {
            return y <= low + 0.0001f ? "at the bottom of the meat" : y >= high - 0.0001f ? "at the top of the meat" : "in the middle";
        }

        [Theory]
        [InlineData("pork", 2)]
        [InlineData("pork", 8)]
        [InlineData("lamb", 2)]
        [InlineData("lamb", 8)]
        public void Strands_AreNeverMoreThanFiveCentimetresApart(string chop, int count)
        {
            float bottom, top, low, high;
            Tube(count, out bottom, out top, out low, out high);
            var path = new MeatNetPath(MeatBagLayout.OutlineOf(chop == "lamb"), Cord, Gap, MeatBagLayout.DefaultSpacing, MeatBagLayout.DefaultBottomEnd);

            var tooWide = new List<string>();
            foreach (float y in new[] { low, (low + high) / 2f, high })
            {
                float widest = Max(RingGaps(path, y, bottom, top, low, high));
                if (widest > 0.05f)
                    tooWide.Add((widest * 100f).ToString("0.0") + " cm " + Where(y, low, high));
            }
            Assert.True(tooWide.Count == 0, chop + " with " + count + " chops, gaps in the net: " + string.Join("; ", tooWide) + ".");
        }

        private static float[] StrandAt(MeatNetPath path, int strand, int family, float y, float bottom, float top, float low, float high)
        {
            float x;
            float z;
            MeatBagLayout.StrandPoint(path, strand, family, y, bottom, top, low, high, out x, out z);
            return new[] { x, z };
        }

        // Where a strand drawn through the net's heights is at y: the line between the two
        // heights either side of it.
        private static float[] DrawnAt(MeatNetPath path, int strand, int family, float y, float bottom, float top, float low, float high, float[] heights)
        {
            int j = 0;
            while (j < heights.Length - 2 && heights[j + 1] < y)
                j++;
            float[] a = StrandAt(path, strand, family, heights[j], bottom, top, low, high);
            float[] b = StrandAt(path, strand, family, heights[j + 1], bottom, top, low, high);
            float share = heights[j + 1] > heights[j] ? (y - heights[j]) / (heights[j + 1] - heights[j]) : 0f;
            return new[] { a[0] + (b[0] - a[0]) * share, a[1] + (b[1] - a[1]) * share };
        }

        private static float DistanceToHoop(MeatNetPath path, float y, float low, float high, float[] point)
        {
            var ring = new List<float[]>();
            for (int k = 0; k <= MeatBagLayout.HoopSteps; k++)
            {
                float x;
                float z;
                MeatBagLayout.HoopPoint(path, k, MeatBagLayout.HoopSteps, y, low, high, out x, out z);
                ring.Add(new[] { x, z });
            }

            float nearest = float.MaxValue;
            for (int k = 0; k < ring.Count - 1; k++)
            {
                float ax = ring[k][0];
                float az = ring[k][1];
                float ex = ring[k + 1][0] - ax;
                float ez = ring[k + 1][1] - az;
                float length = ex * ex + ez * ez;
                float share = length > 0f ? Math.Max(0f, Math.Min(1f, ((point[0] - ax) * ex + (point[1] - az) * ez) / length)) : 0f;
                float dx = point[0] - (ax + ex * share);
                float dz = point[1] - (az + ez * share);
                nearest = Math.Min(nearest, (float)Math.Sqrt(dx * dx + dz * dz));
            }
            return nearest;
        }

        [Theory]
        [InlineData("pork", 2)]
        [InlineData("pork", 8)]
        [InlineData("lamb", 2)]
        [InlineData("lamb", 8)]
        public void Strands_MeetTheHoopsAtTheEdgesOfTheMeat(string chop, int count)
        {
            float bottom, top, low, high;
            Tube(count, out bottom, out top, out low, out high);
            var path = new MeatNetPath(MeatBagLayout.OutlineOf(chop == "lamb"), Cord, Gap, MeatBagLayout.DefaultSpacing, MeatBagLayout.DefaultBottomEnd);
            float[] heights = MeatBagLayout.NetHeights(path, bottom, low, high, top);

            var apart = new List<string>();
            foreach (float edge in new[] { low, high })
            {
                float worst = 0f;
                for (int family = 0; family < 2; family++)
                {
                    for (int strand = 0; strand < path.Strands; strand++)
                        worst = Math.Max(worst, DistanceToHoop(path, edge, low, high, DrawnAt(path, strand, family, edge, bottom, top, low, high, heights)));
                }
                if (worst > Tolerance)
                    apart.Add("a strand is " + (worst * 1000f).ToString("0.0") + " mm off the hoop " + (edge == low ? "at the bottom" : "at the top"));
            }
            Assert.True(apart.Count == 0, chop + " with " + count + " chops: " + string.Join("; ", apart) + ".");
        }

        [Theory]
        [InlineData("pork", 2)]
        [InlineData("pork", 8)]
        [InlineData("lamb", 2)]
        [InlineData("lamb", 8)]
        public void Strands_FollowTheNet_BetweenTheHeightsTheyAreDrawnAt(string chop, int count)
        {
            float bottom, top, low, high;
            Tube(count, out bottom, out top, out low, out high);
            var path = new MeatNetPath(MeatBagLayout.OutlineOf(chop == "lamb"), Cord, Gap, MeatBagLayout.DefaultSpacing, MeatBagLayout.DefaultBottomEnd);
            float[] heights = MeatBagLayout.NetHeights(path, bottom, low, high, top);

            float worst = 0f;
            for (int step = 0; step <= 200; step++)
            {
                float y = bottom + (top - bottom) * step / 200f;
                for (int strand = 0; strand < path.Strands; strand += 3)
                {
                    float[] exact = StrandAt(path, strand, 0, y, bottom, top, low, high);
                    float[] drawn = DrawnAt(path, strand, 0, y, bottom, top, low, high, heights);
                    worst = Math.Max(worst, (float)Math.Sqrt((exact[0] - drawn[0]) * (exact[0] - drawn[0]) + (exact[1] - drawn[1]) * (exact[1] - drawn[1])));
                }
            }
            Assert.True(worst <= Tolerance, chop + " with " + count + " chops: a drawn strand is " + (worst * 1000f).ToString("0.0") + " mm off the net.");
        }

        [Theory]
        [InlineData("pork", 0.025f)]
        [InlineData("pork", 0.05f)]
        [InlineData("lamb", 0.025f)]
        [InlineData("lamb", 0.05f)]
        public void RingAtTheBottom_IsAPolygonThroughTheStrandEnds_OnACircleOfTheEndSize(string chop, float endSize)
        {
            float bottom, top, low, high;
            Tube(8, out bottom, out top, out low, out high);
            var path = new MeatNetPath(MeatBagLayout.OutlineOf(chop == "lamb"), Cord, Gap, MeatBagLayout.DefaultSpacing, endSize);

            float[] ring = MeatBagLayout.BottomRing(path, bottom, top, low, high);
            // One corner for each strand, and the first again to close it.
            Assert.Equal((path.Strands + 1) * 2, ring.Length);
            Assert.Equal(ring[0], ring[ring.Length - 2], 5);
            Assert.Equal(ring[1], ring[ring.Length - 1], 5);

            for (int strand = 0; strand < path.Strands; strand++)
            {
                for (int family = 0; family < 2; family++)
                {
                    float[] end = StrandAt(path, strand, family, bottom, bottom, top, low, high);
                    Assert.Equal(ring[strand * 2], end[0], 4);
                    Assert.Equal(ring[strand * 2 + 1], end[1], 4);
                }
                Assert.Equal(endSize, (float)Math.Sqrt(ring[strand * 2] * ring[strand * 2] + ring[strand * 2 + 1] * ring[strand * 2 + 1]), 4);
            }
        }

        [Theory]
        [InlineData("pork", 0.05f)]
        [InlineData("pork", 0.10f)]
        [InlineData("lamb", 0.05f)]
        [InlineData("lamb", 0.10f)]
        public void Strands_MeetTheHoop_AtTheHeightItIsSetTo(string chop, float hoopHeight)
        {
            float bottom, top, low, high;
            Tube(8, out bottom, out top, out low, out high);
            var path = new MeatNetPath(MeatBagLayout.OutlineOf(chop == "lamb"), Cord, Gap, MeatBagLayout.DefaultSpacing, MeatBagLayout.DefaultBottomEnd, MeatBagLayout.DefaultBottomDepth, hoopHeight);
            float[] heights = MeatBagLayout.NetHeights(path, bottom, low, high, top);
            float hoopY = MeatBagLayout.HoopY(path, low, high);

            Assert.Equal(low + hoopHeight, hoopY, 5);
            Assert.Contains(heights, h => Math.Abs(h - hoopY) < 0.00001f);

            float worst = 0f;
            for (int family = 0; family < 2; family++)
            {
                for (int strand = 0; strand < path.Strands; strand++)
                    worst = Math.Max(worst, DistanceToHoop(path, hoopY, low, high, DrawnAt(path, strand, family, hoopY, bottom, top, low, high, heights)));
            }
            Assert.True(worst <= Tolerance, chop + ": a strand is " + (worst * 1000f).ToString("0.0") + " mm off a hoop " + (hoopHeight * 100f).ToString("0") + " cm up.");
        }

        [Theory]
        [InlineData("pork", 2)]
        [InlineData("pork", 8)]
        [InlineData("lamb", 2)]
        [InlineData("lamb", 8)]
        public void Strands_AllMeetAtThePointAtTheTop(string chop, int count)
        {
            float bottom, top, low, high;
            Tube(count, out bottom, out top, out low, out high);
            var path = new MeatNetPath(MeatBagLayout.OutlineOf(chop == "lamb"), Cord, Gap, MeatBagLayout.DefaultSpacing, MeatBagLayout.DefaultBottomEnd);

            float widest = 0f;
            for (int family = 0; family < 2; family++)
            {
                for (int strand = 0; strand < path.Strands; strand++)
                {
                    float[] end = StrandAt(path, strand, family, top, bottom, top, low, high);
                    widest = Math.Max(widest, (float)Math.Sqrt(end[0] * end[0] + end[1] * end[1]));
                }
            }
            Assert.True(widest <= 0.0005f, chop + " with " + count + " chops: the strands end up to " + (widest * 100f).ToString("0.0") + " cm from the cord at the top, and do not meet.");
        }

        [Fact]
        public void NetHeights_RunFromBottomToTop_AndIncludeTheEdgesOfTheMeat()
        {
            float bottom, top, low, high;
            Tube(8, out bottom, out top, out low, out high);
            var path = new MeatNetPath(MeatBagLayout.OutlineOf(false), Cord, Gap, MeatBagLayout.DefaultSpacing, MeatBagLayout.DefaultBottomEnd);
            float[] heights = MeatBagLayout.NetHeights(path, bottom, low, high, top);

            Assert.Equal(bottom, heights[0], 5);
            Assert.Equal(top, heights[heights.Length - 1], 5);
            Assert.Contains(heights, h => Math.Abs(h - low) < 0.00001f);
            Assert.Contains(heights, h => Math.Abs(h - high) < 0.00001f);
            for (int i = 1; i < heights.Length; i++)
                Assert.True(heights[i] > heights[i - 1], "The heights must rise.");
        }

        [Fact]
        public void NetSpacing_SetsHowManyStrands()
        {
            Assert.Equal(27, MeatBagLayout.StrandCount(1.2f, 0.045f));
            Assert.Equal(13, MeatBagLayout.StrandCount(1.2f, 0.09f));
            Assert.Equal(MeatBagLayout.MaxStrands, MeatBagLayout.StrandCount(1.2f, 0.001f));
            Assert.Equal(MeatBagLayout.MinStrands, MeatBagLayout.StrandCount(1.2f, 5f));
            Assert.Equal(MeatBagLayout.MaxStrands, MeatBagLayout.StrandCount(1.2f, 0f));
        }

        [Theory]
        [InlineData("pork", 0.03f)]
        [InlineData("pork", 0.08f)]
        [InlineData("lamb", 0.03f)]
        [InlineData("lamb", 0.08f)]
        public void Strands_AreAboutAsFarApartAsAsked(string chop, float spacing)
        {
            float bottom, top, low, high;
            Tube(8, out bottom, out top, out low, out high);
            var path = new MeatNetPath(MeatBagLayout.OutlineOf(chop == "lamb"), Cord, Gap, spacing, MeatBagLayout.DefaultBottomEnd);

            float widest = 0f;
            foreach (float y in new[] { low, (low + high) / 2f, high })
                widest = Math.Max(widest, Max(RingGaps(path, y, bottom, top, low, high)));
            Assert.True(widest <= spacing * 1.1f, chop + ": strands " + (spacing * 100f).ToString("0.0") + " cm apart gave an opening of " + (widest * 100f).ToString("0.0") + " cm.");
        }

        [Theory]
        [InlineData("pork", 2)]
        [InlineData("pork", 8)]
        [InlineData("lamb", 2)]
        [InlineData("lamb", 8)]
        public void Strands_AreEvenlySpacedAroundTheMeat(string chop, int count)
        {
            float bottom, top, low, high;
            Tube(count, out bottom, out top, out low, out high);
            var path = new MeatNetPath(MeatBagLayout.OutlineOf(chop == "lamb"), Cord, Gap, MeatBagLayout.DefaultSpacing, MeatBagLayout.DefaultBottomEnd);

            var uneven = new List<string>();
            foreach (float y in new[] { low, (low + high) / 2f, high })
            {
                // One family, strand by strand: the width of each mesh opening.
                var points = new List<float[]>();
                for (int strand = 0; strand < path.Strands; strand++)
                {
                    float x;
                    float z;
                    MeatBagLayout.StrandPoint(path, strand, 0, y, bottom, top, low, high, out x, out z);
                    points.Add(new[] { x, z });
                }
                var gaps = new List<float>();
                for (int i = 0; i < points.Count; i++)
                {
                    float[] a = points[i];
                    float[] b = points[(i + 1) % points.Count];
                    gaps.Add((float)Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1])));
                }
                float ratio = Max(gaps) / Min(gaps);
                if (ratio > 1.25f)
                    uneven.Add("widest opening " + ratio.ToString("0.0") + " times the narrowest " + Where(y, low, high));
            }
            Assert.True(uneven.Count == 0, chop + " with " + count + " chops: " + string.Join("; ", uneven) + ".");
        }

        private static float Min(IList<float> values)
        {
            float least = float.MaxValue;
            for (int i = 0; i < values.Count; i++)
                least = Math.Min(least, values[i]);
            return least;
        }

        private static float Max(IList<float> values)
        {
            float most = float.MinValue;
            for (int i = 0; i < values.Count; i++)
                most = Math.Max(most, values[i]);
            return most;
        }
    }
}
