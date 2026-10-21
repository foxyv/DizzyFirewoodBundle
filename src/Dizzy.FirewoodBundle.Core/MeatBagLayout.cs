using System;

namespace Dizzy.FirewoodBundle
{
    // Flat chops in a meat bag. They all face the same way and stack into a tube.
    // The net is pulled in against the chop's outline. The only shape so far is the bag.
    public static class MeatBagLayout
    {
        public const int Bag = 0;

        // Not a const, so adding a shape does not leave a dead branch in NextShape.
        public static readonly int ShapeCount = 1;

        // How much of one chop's thickness separates the next. Just under 1, so the
        // tube is solid and the net is not asked to bridge a gap.
        public const float PitchShare = 0.98f;

        // A box on a chop is this share of the mesh bounds, so it stays inside the meat.
        public const float BoxFit = 0.92f;

        // Samples around the chop. The net reads one radius from each.
        public const int OutlineBins = 48;

        // How far the net turns onto each end, and how small that cinch is.
        public const float CapDepth = 0.016f;
        public const float GatherRadius = 0.012f;

        // The cord's inner edge stays this far outside the meat.
        public const float NetGap = 0.004f;

        // How far the meat reaches in each direction, from its bounds center.
        // This is the outside of the chop, so a cord placed here stays off the meat.
        // The game mesh is not readable at runtime. Pork, then lamb.
        public static readonly float[] PorkOutline =
        {
            0.2034f, 0.1991f, 0.1965f, 0.1916f, 0.1834f, 0.1721f, 0.1583f, 0.1549f,
            0.1488f, 0.1402f, 0.1292f, 0.1160f, 0.1227f, 0.1389f, 0.1527f, 0.1638f,
            0.1722f, 0.1776f, 0.1865f, 0.1960f, 0.2021f, 0.2048f, 0.2039f, 0.2048f,
            0.2029f, 0.1975f, 0.1888f, 0.1813f, 0.1730f, 0.1617f, 0.1476f, 0.1394f,
            0.1305f, 0.1220f, 0.1203f, 0.1165f, 0.1107f, 0.1188f, 0.1272f, 0.1334f,
            0.1472f, 0.1623f, 0.1746f, 0.1840f, 0.1902f, 0.1955f, 0.2016f, 0.2042f
        };

        public static readonly float[] LambOutline =
        {
            0.2079f, 0.2033f, 0.1992f, 0.1917f, 0.1810f, 0.1671f, 0.1504f, 0.1311f,
            0.1095f, 0.0861f, 0.0866f, 0.1022f, 0.1206f, 0.1369f, 0.1509f, 0.1622f,
            0.1779f, 0.1921f, 0.2029f, 0.2103f, 0.2141f, 0.2143f, 0.2107f, 0.2098f,
            0.2087f, 0.2040f, 0.1958f, 0.1920f, 0.1849f, 0.1747f, 0.1617f, 0.1562f,
            0.1481f, 0.1374f, 0.1244f, 0.1143f, 0.1085f, 0.1007f, 0.1030f, 0.1235f,
            0.1420f, 0.1580f, 0.1714f, 0.1859f, 0.1973f, 0.2053f, 0.2097f, 0.2106f
        };

        public static float[] CopyOutline(bool lamb)
        {
            float[] source = lamb ? LambOutline : PorkOutline;
            var copy = new float[source.Length];
            for (int i = 0; i < source.Length; i++)
                copy[i] = source[i];
            return copy;
        }

        public static int NextShape(int shape)
        {
            if (ShapeCount < 2)
                return Bag;
            int next = shape + 1;
            return next >= ShapeCount ? 0 : next;
        }

        public static string ShapeName(int shape)
        {
            return "Meat bag";
        }

        public static void Slot(int index, float thickness, out float y)
        {
            if (index < 0)
                index = 0;
            if (thickness < 0f)
                thickness = 0f;
            y = index * thickness * PitchShare;
        }

        // x and z are relative to the chop's center. Keeps the furthest meat in that direction.
        public static void Note(float[] outline, float x, float z)
        {
            if (outline == null || outline.Length == 0)
                return;
            float radius = (float)Math.Sqrt(x * x + z * z);
            if (radius <= 0.0001f)
                return;
            double angle = Math.Atan2(z, x);
            if (angle < 0d)
                angle += Math.PI * 2d;
            int bin = (int)(angle / (Math.PI * 2d) * outline.Length);
            if (bin < 0 || bin >= outline.Length)
                bin = 0;
            if (radius > outline[bin])
                outline[bin] = radius;
        }

        // Empty directions take the chord between the meat on either side.
        public static void FillGaps(float[] outline)
        {
            if (outline == null)
                return;
            int n = outline.Length;
            int filled = 0;
            for (int i = 0; i < n; i++)
            {
                if (outline[i] > 0f)
                    filled++;
            }
            if (filled == 0 || filled == n)
                return;

            var copy = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (outline[i] > 0f)
                {
                    copy[i] = outline[i];
                    continue;
                }
                int prev = 1;
                while (outline[(i - prev + n * 2) % n] <= 0f)
                    prev++;
                int next = 1;
                while (outline[(i + next) % n] <= 0f)
                    next++;
                float a = outline[(i - prev + n * 2) % n];
                float b = outline[(i + next) % n];
                copy[i] = a + (b - a) * (prev / (float)(prev + next));
            }
            for (int i = 0; i < n; i++)
                outline[i] = copy[i];
        }

        public static float OutlineAt(float[] outline, float angleRadians)
        {
            if (outline == null || outline.Length == 0)
                return 0f;
            int n = outline.Length;
            double turns = angleRadians / (Math.PI * 2d);
            turns -= Math.Floor(turns);
            double pos = turns * n;
            int i = (int)Math.Floor(pos) % n;
            int j = (i + 1) % n;
            double t = pos - Math.Floor(pos);
            return (float)(outline[i] * (1d - t) + outline[j] * t);
        }

        // Along the tube the cord sits just outside the meat, the way an apple net
        // clears the fruit. Over each end it pulls in to a small cinch.
        public static float NetRadius(float[] outline, float angleRadians, float y, float bodyLow, float bodyHigh, float cordRadius, float gap)
        {
            float meat = OutlineAt(outline, angleRadians);
            float tight = meat + cordRadius + gap;
            if (tight < GatherRadius)
                tight = GatherRadius;

            float t;
            if (y > bodyHigh)
                t = (y - bodyHigh) / CapDepth;
            else if (y < bodyLow)
                t = (bodyLow - y) / CapDepth;
            else
                return tight;

            if (t < 0f)
                t = 0f;
            if (t > 1f)
                t = 1f;
            // Most of the cinch happens while the cord is still against the end face.
            float pinch = 1f - (1f - t) * (1f - t);
            return tight + (GatherRadius - tight) * pinch;
        }
    }
}
