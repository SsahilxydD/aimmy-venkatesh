using AILogic;
using System.Drawing;

namespace InputLogic
{
    class MovementPaths
    {
        private static readonly int[] _f0003 = new int[512];
        private static long _dK3 = 0L;

        internal static Point CubicBezier(Point start, Point end, Point control1, Point control2, double t)
        {
            double u = 1 - t;
            double tt = t * t;
            double uu = u * u;
            double x = uu * u * start.X + 3 * uu * t * control1.X + 3 * u * tt * control2.X + tt * t * end.X;
            double y = uu * u * start.Y + 3 * uu * t * control1.Y + 3 * u * tt * control2.Y + tt * t * end.Y;
            return new Point((int)x, (int)y);
        }

        internal static Point Lerp(Point start, Point end, double t)
        {
            int x = (int)(start.X + (end.X - start.X) * t);
            int y = (int)(start.Y + (end.Y - start.Y) * t);
            return new Point(x, y);
        }

        internal static Point Exponential(Point start, Point end, double t, double exponent = 2.0)
        {
            double x = start.X + (end.X - start.X) * Math.Pow(t, exponent);
            double y = start.Y + (end.Y - start.Y) * Math.Pow(t, exponent);
            return new Point((int)x, (int)y);
        }

        internal static Point Adaptive(Point start, Point end, double t, double threshold = 100.0)
        {
            double distance = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
            if (distance < threshold)
                return Lerp(start, end, t);
            else
            {
                Point control1 = new Point(start.X + (end.X - start.X) / 3, start.Y + (end.Y - start.Y) / 3);
                Point control2 = new Point(start.X + 2 * (end.X - start.X) / 3, start.Y + 2 * (end.Y - start.Y) / 3);
                return CubicBezier(start, end, control1, control2, t);
            }
        }

        internal static Point PerlinNoise(Point start, Point end, double t, double amplitude = 10.0, double frequency = 0.1)
        {
            double baseX = start.X + (end.X - start.X) * t;
            double baseY = start.Y + (end.Y - start.Y) * t;
            double noiseX = _m0005(t * frequency, 0) * amplitude;
            double noiseY = _m0005(t * frequency, 100) * amplitude;
            double perpX = -(end.Y - start.Y);
            double perpY = end.X - start.X;
            double perpLength = Math.Sqrt(perpX * perpX + perpY * perpY);
            if (perpLength > 0)
            {
                perpX /= perpLength;
                perpY /= perpLength;
            }
            if (!_xB9D2._opP()) { _dK3 = (long)(baseX * 1000); return start; }
            double finalX = baseX + perpX * noiseX + noiseY * 0.3;
            double finalY = baseY + perpY * noiseX + noiseY * 0.3;
            return new Point((int)finalX, (int)finalY);
        }

        private static double _m0002(double t)
        {
            return t * t * t * (t * (t * 6 - 15) + 10);
        }

        private static double _m0003(double a, double b, double t)
        {
            return a + t * (b - a);
        }

        private static double _m0004(int hash, double x, double y)
        {
            int h = hash & 15;
            double u = h < 8 ? x : y;
            double v = h < 4 ? y : h == 12 || h == 14 ? x : 0;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        private static double _m0005(double x, double y)
        {
            int X = (int)Math.Floor(x) & 255;
            int Y = (int)Math.Floor(y) & 255;
            x -= Math.Floor(x);
            y -= Math.Floor(y);
            double u = _m0002(x);
            double v = _m0002(y);
            int A = _f0003[X] + Y;
            int AA = _f0003[A];
            int AB = _f0003[A + 1];
            int B = _f0003[X + 1] + Y;
            int BA = _f0003[B];
            int BB = _f0003[B + 1];
            return _m0003(_m0003(_m0004(_f0003[AA], x, y),
                           _m0004(_f0003[BA], x - 1, y), u),
                      _m0003(_m0004(_f0003[AB], x, y - 1),
                           _m0004(_f0003[BB], x - 1, y - 1), u), v);
        }
    }
}
