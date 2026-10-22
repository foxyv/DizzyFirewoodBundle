using System;
using System.Collections.Generic;

namespace Dizzy.FirewoodBundle
{
    public struct ShotPlace
    {
        public float X;
        public float Y;
        public float Z;

        public ShotPlace(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    // A square pyramid of round shot. Layer 0 sits on the ground. Each ball above
    // rests in the pocket between four balls. A count that is not a whole pyramid
    // fills the ground first, then the pockets, so every ball has something under it.
    public static class ShotPyramid
    {
        public static int Capacity(int side)
        {
            if (side < 1)
                return 0;
            return side * (side + 1) * (2 * side + 1) / 6;
        }

        public static int BaseSide(int count)
        {
            if (count < 1)
                return 0;
            int side = 1;
            while (Capacity(side) < count)
                side++;
            return side;
        }

        public static ShotPlace[] Layout(int count, float diameter)
        {
            if (count < 1)
                return new ShotPlace[0];
            if (diameter <= 0f || float.IsNaN(diameter) || float.IsInfinity(diameter))
                diameter = 0.1f;

            int side = BaseSide(count);
            float rise = diameter * (float)(Math.Sqrt(2.0) * 0.5);
            var places = new List<ShotPlace>(count);
            int left = count;
            for (int layer = 0; layer < side && left > 0; layer++)
            {
                int width = side - layer;
                int filled = Math.Min(left, width * width);
                bool looseGround = layer == 0 && filled < width * width;
                int rows = (filled + width - 1) / width;
                for (int i = 0; i < filled; i++)
                {
                    int row = i / width;
                    int col = i % width;
                    float x;
                    float z;
                    if (looseGround)
                    {
                        int inRow = Math.Min(width, filled - row * width);
                        x = (col - (inRow - 1) * 0.5f) * diameter;
                        z = (row - (rows - 1) * 0.5f) * diameter;
                    }
                    else
                    {
                        x = (col - (width - 1) * 0.5f) * diameter;
                        z = (row - (width - 1) * 0.5f) * diameter;
                    }

                    places.Add(new ShotPlace(x, layer * rise, z));
                }

                left -= filled;
            }

            return places.ToArray();
        }

        // How far the pyramid collider reaches toward the iron. Short of 1, so the
        // faces fall a little inside the balls.
        public const float ColliderFit = 0.96f;

        // Five corners of one pyramid: four around the base, then the apex.
        // Each corner lies just inside a ball, so the flat faces stay inside the stack.
        public static ShotPlace[] Collider(ShotPlace[] balls, float radius)
        {
            if (balls == null || balls.Length == 0)
                return new ShotPlace[0];
            float reach = radius * ColliderFit;
            if (reach < 0.001f || float.IsNaN(reach) || float.IsInfinity(reach))
                reach = 0.001f;

            float minY = balls[0].Y;
            int top = 0;
            for (int i = 1; i < balls.Length; i++)
            {
                if (balls[i].Y < minY)
                    minY = balls[i].Y;
                if (balls[i].Y > balls[top].Y)
                    top = i;
            }

            float cut = minY + Math.Max(radius, 0.001f);
            var bottom = new List<int>();
            for (int i = 0; i < balls.Length; i++)
            {
                if (balls[i].Y <= cut + 0.0001f)
                    bottom.Add(i);
            }
            if (bottom.Count == 0)
                bottom.Add(0);

            float cx = 0f;
            float cz = 0f;
            for (int b = 0; b < bottom.Count; b++)
            {
                cx += balls[bottom[b]].X;
                cz += balls[bottom[b]].Z;
            }
            cx /= bottom.Count;
            cz /= bottom.Count;

            // Around the base, then the point. Down weighs more than out, so the
            // base sits near the underside of the lowest balls.
            float[] qx = { 1f, 1f, -1f, -1f };
            float[] qz = { 1f, -1f, -1f, 1f };
            var points = new ShotPlace[5];
            for (int k = 0; k < 4; k++)
            {
                float qlen = (float)Math.Sqrt(qx[k] * qx[k] + qz[k] * qz[k]);
                float ux = qx[k] / qlen;
                float uz = qz[k] / qlen;
                int best = bottom[0];
                float bestDot = float.MinValue;
                for (int b = 0; b < bottom.Count; b++)
                {
                    int i = bottom[b];
                    float dot = (balls[i].X - cx) * ux + (balls[i].Z - cz) * uz;
                    if (dot > bestDot)
                    {
                        bestDot = dot;
                        best = i;
                    }
                }

                float vx = ux;
                float vy = -3f;
                float vz = uz;
                float vlen = (float)Math.Sqrt(vx * vx + vy * vy + vz * vz);
                points[k] = new ShotPlace(
                    balls[best].X + vx / vlen * reach,
                    balls[best].Y + vy / vlen * reach,
                    balls[best].Z + vz / vlen * reach);
            }

            points[4] = new ShotPlace(balls[top].X, balls[top].Y + reach, balls[top].Z);
            return points;
        }
    }
}
