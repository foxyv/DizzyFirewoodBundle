using System;
using Xunit;

namespace Dizzy.FirewoodBundle.Tests
{
    public class MeatBagLayoutTests
    {
        [Fact]
        public void OnlyShape_IsTheMeatBag()
        {
            Assert.Equal(MeatBagLayout.Bag, MeatBagLayout.NextShape(0));
            Assert.Equal(MeatBagLayout.Bag, MeatBagLayout.NextShape(4));
            Assert.Equal("Meat bag", MeatBagLayout.ShapeName(0));
        }

        [Fact]
        public void Chops_Stack_Flush_TheSameWay()
        {
            float y0;
            float y1;
            MeatBagLayout.Slot(0, 0.05f, out y0);
            MeatBagLayout.Slot(1, 0.05f, out y1);
            Assert.Equal(0f, y0);
            Assert.Equal(0.05f * MeatBagLayout.PitchShare, y1, 3);
        }

        [Fact]
        public void TheCordRisesToTheTopOfTheHooksPeg_ButNotPastWhereTheGamesOwnItemsReach()
        {
            // The peg's top is 1.55 cm above the pin. The game's lanterns reach 2.4 to 3.3 cm.
            Assert.InRange(MeatBagLayout.HookReach, 0.0155f, 0.033f);
        }

        [Theory]
        [InlineData(-0.05f)]
        [InlineData(-0.1356f)]
        [InlineData(-0.52f)]
        public void SetOnATable_TheOriginGoesFarEnoughUpThatTheFootIsTheLowestThing(float footY)
        {
            // The game puts the held item's origin PlaceHeight above the table. The foot is
            // footY from the origin, so it should end up a hair above the surface, not in it.
            float foot = MeatBagLayout.PlaceHeight(footY) + footY;
            Assert.Equal(MeatBagLayout.PlaceClearance, foot, 4);
        }

        [Fact]
        public void SetOnATable_ABagWithNothingBelowItsOriginIsStillLiftedOffTheSurface()
        {
            Assert.Equal(MeatBagLayout.PlaceClearance, MeatBagLayout.PlaceHeight(0f), 4);
            Assert.Equal(MeatBagLayout.PlaceClearance, MeatBagLayout.PlaceHeight(0.2f), 4);
        }

        [Fact]
        public void TheStandReachesDownToTheFoot_SoTheBagRestsOnItsBottomRing()
        {
            // Two chops 5 cm thick, 20 cm across, the lower one's underside at 0.
            var corners = new System.Collections.Generic.List<ColliderVec3>();
            foreach (float y in new[] { 0.002f, 0.052f, 0.054f, 0.104f })
            {
                corners.Add(new ColliderVec3(-0.1f, y, -0.06f));
                corners.Add(new ColliderVec3(0.1f, y, 0.06f));
            }

            float foot = MeatBagLayout.FootY(0f, 0.008f, 0.0056f);
            Assert.Equal(-0.0136f, foot, 4);

            ColliderBounds onTheChop = BundleColliderMath.PileStand(corners, 1, 0.025f);
            corners.Add(new ColliderVec3(0f, foot, 0f));
            ColliderBounds onTheFoot = BundleColliderMath.PileStand(corners, 1, 0.025f);

            // The stand with a foot ends at the foot, and is as wide as the chop above it.
            Assert.Equal(foot, onTheFoot.Center.Y - onTheFoot.Size.Y / 2f, 4);
            Assert.True(onTheFoot.Center.Y - onTheFoot.Size.Y / 2f < onTheChop.Center.Y - onTheChop.Size.Y / 2f);
            Assert.Equal(onTheChop.Size.X, onTheFoot.Size.X, 3);
            Assert.Equal(onTheChop.Size.Z, onTheFoot.Size.Z, 3);
        }

        [Fact]
        public void MeatSpacing_SetsHowFarApartTheChopsAre()
        {
            float flush;
            float pressed;
            float spread;
            MeatBagLayout.Slot(3, 0.05f, 1.0f, out flush);
            MeatBagLayout.Slot(3, 0.05f, 0.8f, out pressed);
            MeatBagLayout.Slot(3, 0.05f, 1.2f, out spread);
            float negative;
            MeatBagLayout.Slot(3, 0.05f, -1f, out negative);

            Assert.Equal(0.15f, flush, 4);
            Assert.Equal(0.12f, pressed, 4);
            Assert.Equal(0.18f, spread, 4);
            Assert.Equal(0f, negative, 4);
        }

        [Fact]
        public void Net_FollowsTheOutline_AndCinchesTheEnds()
        {
            // Meat 10 cm out in every direction, and a 20 cm spike along angle 0.
            var outline = new float[MeatBagLayout.OutlineBins];
            for (int i = 0; i < outline.Length; i++)
                outline[i] = 0.10f;
            outline[0] = 0.20f;

            const float cord = 0.004f;
            float side = MeatBagLayout.NetRadius(outline, 0f, 0.05f, 0f, 0.10f, cord, MeatBagLayout.NetGap);
            float narrow = MeatBagLayout.NetRadius(outline, 1.5708f, 0.05f, 0f, 0.10f, cord, MeatBagLayout.NetGap);
            float end = MeatBagLayout.NetRadius(outline, 0f, 0.10f + MeatBagLayout.CapDepth, 0f, 0.10f, cord, MeatBagLayout.NetGap);

            Assert.Equal(0.20f + cord + MeatBagLayout.NetGap, side, 3);
            Assert.True(narrow < side);
            Assert.True(narrow > 0.10f);
            Assert.Equal(MeatBagLayout.TopEnd, end, 3);
        }


        [Fact]
        public void BottomEnd_SetsTheCircleTheStrandsGatherInto_AndNeverFlaresOutPastTheMeat()
        {
            var outline = new float[MeatBagLayout.OutlineBins];
            for (int i = 0; i < outline.Length; i++)
                outline[i] = 0.10f;

            const float cord = 0.004f;
            float below = 0f - MeatBagLayout.CapDepth;
            float above = 0.10f + MeatBagLayout.CapDepth;
            float bottom = MeatBagLayout.NetRadius(outline, 0f, below, 0f, 0.10f, cord, MeatBagLayout.NetGap, 0.03f);
            float top = MeatBagLayout.NetRadius(outline, 0f, above, 0f, 0.10f, cord, MeatBagLayout.NetGap, 0.03f);
            float wide = MeatBagLayout.NetRadius(outline, 0f, below, 0f, 0.10f, cord, MeatBagLayout.NetGap, 0.5f);
            float body = MeatBagLayout.NetRadius(outline, 0f, 0.05f, 0f, 0.10f, cord, MeatBagLayout.NetGap);

            Assert.Equal(0.03f, bottom, 4);
            Assert.Equal(MeatBagLayout.TopEnd, top, 4);
            Assert.Equal(body, wide, 4);
        }

        [Fact]
        public void BottomDepth_SetsHowFarBelowTheMeatTheNetPullsInToTheEnd()
        {
            var outline = new float[MeatBagLayout.OutlineBins];
            for (int i = 0; i < outline.Length; i++)
                outline[i] = 0.10f;

            const float cord = 0.004f;
            foreach (float depth in new[] { 0.004f, 0.008f, 0.03f })
            {
                float atEnd = MeatBagLayout.NetRadius(outline, 0f, -depth, 0f, 0.10f, cord, MeatBagLayout.NetGap, 0.03f, depth);
                float halfway = MeatBagLayout.NetRadius(outline, 0f, -depth / 2f, 0f, 0.10f, cord, MeatBagLayout.NetGap, 0.03f, depth);
                float edge = MeatBagLayout.NetRadius(outline, 0f, 0f, 0f, 0.10f, cord, MeatBagLayout.NetGap, 0.03f, depth);

                Assert.Equal(0.03f, atEnd, 4);
                Assert.True(halfway < edge && halfway > atEnd, "The net narrows steadily to the end, " + depth + " m below.");
            }
        }

        [Fact]
        public void OutlineFromEdges_OfASquare_IsExactAndNeverShort()
        {
            // A square 20 cm across: 10 cm to a side, 14.1 cm to a corner.
            float[] square =
            {
                -0.1f, -0.1f, 0.1f, -0.1f,
                0.1f, -0.1f, 0.1f, 0.1f,
                0.1f, 0.1f, -0.1f, 0.1f,
                -0.1f, 0.1f, -0.1f, -0.1f
            };
            Assert.Equal(0.1f, MeatBagLayout.ReachAt(square, 0d), 4);
            Assert.Equal(0.14142f, MeatBagLayout.ReachAt(square, Math.PI / 4d), 4);

            float[] outline = MeatBagLayout.OutlineFromEdges(square, MeatBagLayout.OutlineBins);
            for (int i = 0; i < 2000; i++)
            {
                double angle = i * 2d * Math.PI / 2000d;
                float line = MeatBagLayout.OutlineAt(outline, (float)angle);
                Assert.True(line >= MeatBagLayout.ReachAt(square, angle) - 0.0005f, "The outline cuts into the square at " + i + "/2000 of a turn.");
            }
        }

        [Fact]
        public void OutlineFromEdges_FollowsANotchDownToItsFloor()
        {
            // A 20 cm square with a notch cut in from the right, 4 cm wide and 6 cm deep.
            float[] notched =
            {
                -0.1f, -0.1f, 0.1f, -0.1f,
                0.1f, -0.1f, 0.1f, -0.02f,
                0.1f, -0.02f, 0.04f, -0.02f,
                0.04f, -0.02f, 0.04f, 0.02f,
                0.04f, 0.02f, 0.1f, 0.02f,
                0.1f, 0.02f, 0.1f, 0.1f,
                0.1f, 0.1f, -0.1f, 0.1f,
                -0.1f, 0.1f, -0.1f, -0.1f
            };
            Assert.Equal(0.04f, MeatBagLayout.ReachAt(notched, 0d), 4);
        }
    }
}
