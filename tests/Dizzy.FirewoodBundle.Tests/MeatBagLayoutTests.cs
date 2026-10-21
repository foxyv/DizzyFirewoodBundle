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
        public void Net_FollowsTheOutline_AndCinchesTheEnds()
        {
            var outline = new float[MeatBagLayout.OutlineBins];
            MeatBagLayout.Note(outline, 0.20f, 0f);
            MeatBagLayout.Note(outline, 0f, 0.10f);
            MeatBagLayout.FillGaps(outline);

            const float cord = 0.004f;
            float side = MeatBagLayout.NetRadius(outline, 0f, 0.05f, 0f, 0.10f, cord, MeatBagLayout.NetGap);
            float narrow = MeatBagLayout.NetRadius(outline, 1.5708f, 0.05f, 0f, 0.10f, cord, MeatBagLayout.NetGap);
            float end = MeatBagLayout.NetRadius(outline, 0f, 0.10f + MeatBagLayout.CapDepth, 0f, 0.10f, cord, MeatBagLayout.NetGap);

            Assert.Equal(0.20f + cord + MeatBagLayout.NetGap, side, 3);
            Assert.True(narrow < side);
            Assert.True(narrow > 0.10f);
            Assert.Equal(MeatBagLayout.GatherRadius, end, 3);
        }

        [Fact]
        public void BakedOutline_WrapsTheChop()
        {
            float[] pork = MeatBagLayout.CopyOutline(false);
            MeatBagLayout.FillGaps(pork);
            float most = 0f;
            float least = 1f;
            for (int i = 0; i < pork.Length; i++)
            {
                if (pork[i] > most)
                    most = pork[i];
                if (pork[i] < least)
                    least = pork[i];
            }

            Assert.True(most > 0.18f);
            Assert.True(least > 0.05f);
        }
    }
}
