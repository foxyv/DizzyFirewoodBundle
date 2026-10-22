using System;

namespace Dizzy.FirewoodBundle
{
    // The line the net takes round the meat along the tube, measured by how far along it
    // goes. Strands placed by distance along it are evenly spaced however the chop curves.
    public sealed class MeatNetPath
    {
        private const int Steps = 720;
        private readonly float[] _arc = new float[Steps + 1];

        public MeatNetPath(float[] outline, float cord, float gap, float spacing, float bottomEnd)
            : this(outline, cord, gap, spacing, bottomEnd, MeatBagLayout.DefaultBottomDepth, MeatBagLayout.DefaultHoopHeight)
        {
        }

        public MeatNetPath(float[] outline, float cord, float gap, float spacing, float bottomEnd, float bottomDepth, float hoopHeight)
        {
            Outline = outline;
            Cord = cord;
            Gap = gap;
            BottomEnd = bottomEnd;
            BottomDepth = bottomDepth;
            HoopHeight = hoopHeight;

            float lastX = 0f;
            float lastZ = 0f;
            for (int i = 0; i <= Steps; i++)
            {
                float angle = i * 2f * (float)Math.PI / Steps;
                float radius = MeatBagLayout.BodyRadius(outline, angle, cord, gap);
                float x = (float)Math.Cos(angle) * radius;
                float z = (float)Math.Sin(angle) * radius;
                if (i == 0)
                {
                    _arc[i] = 0f;
                }
                else
                {
                    float dx = x - lastX;
                    float dz = z - lastZ;
                    _arc[i] = _arc[i - 1] + (float)Math.Sqrt(dx * dx + dz * dz);
                }
                lastX = x;
                lastZ = z;
            }
            Perimeter = _arc[Steps];
            Strands = MeatBagLayout.StrandCount(Perimeter, spacing);
        }

        public float[] Outline { get; private set; }
        public float Cord { get; private set; }
        public float Gap { get; private set; }

        // The radius of the circle the strands gather into at the bottom, and how far below
        // the meat that is.
        public float BottomEnd { get; private set; }
        public float BottomDepth { get; private set; }

        // How far up from the bottom of the meat the hoop is.
        public float HoopHeight { get; private set; }

        // All the way round the meat.
        public float Perimeter { get; private set; }

        // Strands in each family.
        public int Strands { get; private set; }

        // The angle at a distance along the path, from angle 0. Wraps round.
        public float AngleAt(float arc)
        {
            arc %= Perimeter;
            if (arc < 0f)
                arc += Perimeter;

            int low = 0;
            int high = Steps;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (_arc[middle] <= arc)
                    low = middle;
                else
                    high = middle;
            }

            float span = _arc[high] - _arc[low];
            float share = span > 1e-9f ? (arc - _arc[low]) / span : 0f;
            return (low + share) * 2f * (float)Math.PI / Steps;
        }
    }
}
