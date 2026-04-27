using System.Drawing;
using System.Runtime.CompilerServices;

namespace InputLogic
{
    class MovementPaths
    {
        // Standard Ken Perlin permutation table (256 values, doubled to 512 to avoid modular wrapping).
        // The old table was new int[512] left at all-zeros, which made Perlin noise produce a degenerate
        // constant output (every gradient hash resolved to the same case). This is the canonical table.
        private static readonly int[] permutation;

        static MovementPaths()
        {
            ReadOnlySpan<int> p = [
                151,160,137, 91, 90, 15,131, 13,201, 95, 96, 53,194,233,  7,225,
                140, 36,103, 30, 69,142,  8, 99, 37,240, 21, 10, 23,190,  6,148,
                247,120,234, 75,  0, 26,197, 62, 94,252,219,203,117, 35, 11, 32,
                 57,177, 33, 88,237,149, 56, 87,174, 20,125,136,171,168, 68,175,
                 74,165, 71,134,139, 48, 27,166, 77,146,158,231, 83,111,229,122,
                 60,211,133,230,220,105, 92, 41, 55, 46,245, 40,244,102,143, 54,
                 65, 25, 63,161,  1,216, 80, 73,209, 76,132,187,208, 89, 18,169,
                200,196,135,130,116,188,159, 86,164,100,109,198,173,186,  3, 64,
                 52,217,226,250,124,123,  5,202, 38,147,118,126,255, 82, 85,212,
                207,206, 59,227, 47, 16, 58, 17,182,189, 28, 42,223,183,170,213,
                119,248,152,  2, 44,154,163, 70,221,153,101,155,167, 43,172,  9,
                129, 22, 39,253, 19, 98,108,110, 79,113,224,232,178,185,112,104,
                218,246, 97,228,251, 34,242,193,238,210,144, 12,191,179,162,241,
                 81, 51,145,235,249, 14,239,107, 49,192,214, 31,181,199,106,157,
                184, 84,204,176,115,121, 50, 45,127,  4,150,254,138,236,205, 93,
                222,114, 67, 29, 24, 72,243,141,128,195, 78, 66,215, 61,156,180
            ];
            permutation = new int[512];
            for (int i = 0; i < 512; i++) permutation[i] = p[i & 255];
        }

        // PD controller state — persists across frames to track error derivative.
        private static double _pdErrPrevX = 0;
        private static double _pdErrPrevY = 0;

        private static long _noiseFrame = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Point Direct(Point end)
        {
            // No interpolation — send the full delta each frame.
            // Combined with the ±150 clamp in MoveCrosshair this is the minimum-latency path.
            return end;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Point VelocityDamped(Point end, double sensitivity)
        {
            double gain = 1.0 - sensitivity;
            double P = gain * 1.6;
            double D = gain * 0.5;

            double errX = end.X;
            double errY = end.Y;

            double dErrX = errX - _pdErrPrevX;
            double dErrY = errY - _pdErrPrevY;
            double jumpSq = dErrX * dErrX + dErrY * dErrY;
            double errSq = errX * errX + errY * errY;
            if (jumpSq > errSq * 4.0)
            {
                dErrX = 0;
                dErrY = 0;
            }

            double outX = P * errX + D * dErrX;
            double outY = P * errY + D * dErrY;

            _pdErrPrevX = errX;
            _pdErrPrevY = errY;

            return new Point((int)outX, (int)outY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Point CubicBezier(Point start, Point end, Point control1, Point control2, double t)
        {
            double u   = 1 - t;
            double tt  = t * t;
            double uu  = u * u;
            double ttt = tt * t;
            double uuu = uu * u;

            double x = uuu * start.X + 3 * uu * t * control1.X + 3 * u * tt * control2.X + ttt * end.X;
            double y = uuu * start.Y + 3 * uu * t * control1.Y + 3 * u * tt * control2.Y + ttt * end.Y;

            return new Point((int)x, (int)y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Point Lerp(Point start, Point end, double t)
        {
            return new Point(
                (int)(start.X + (end.X - start.X) * t),
                (int)(start.Y + (end.Y - start.Y) * t));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Point Exponential(Point start, Point end, double t, double exponent = 2.0)
        {
            // Fast path for the common integer exponents — avoids Math.Pow (ln + exp internally).
            double tPow = exponent == 2.0 ? t * t
                        : exponent == 3.0 ? t * t * t
                        : Math.Pow(t, exponent);
            return new Point(
                (int)(start.X + (end.X - start.X) * tPow),
                (int)(start.Y + (end.Y - start.Y) * tPow));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Point Adaptive(Point start, Point end, double t, double threshold = 100.0)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double distSq = dx * dx + dy * dy;
            double threshSq = threshold * threshold;

            double adaptiveT;
            if (distSq < threshSq)
            {
                double ratio = Math.Sqrt(distSq / threshSq);
                adaptiveT = t * (2.0 - ratio);
            }
            else
            {
                double ratio = threshSq / distSq;
                adaptiveT = t * (0.5 + 0.5 * ratio);
            }

            adaptiveT = Math.Clamp(adaptiveT, 0.05, 0.95);

            return new Point(
                (int)(start.X + dx * adaptiveT),
                (int)(start.Y + dy * adaptiveT));
        }

        internal static Point PerlinNoise(Point start, Point end, double t, double amplitude = 10.0, double frequency = 0.1)
        {
            double baseX = start.X + (end.X - start.X) * t;
            double baseY = start.Y + (end.Y - start.Y) * t;

            double phase = _noiseFrame++ * frequency;
            double noiseX = Noise(phase, 0)   * amplitude;
            double noiseY = Noise(phase, 100) * amplitude;

            double perpX = -(end.Y - start.Y);
            double perpY =   end.X - start.X;
            double perpLenSq = perpX * perpX + perpY * perpY;

            if (perpLenSq > 0)
            {
                double invLen = 1.0 / Math.Sqrt(perpLenSq);
                perpX *= invLen;
                perpY *= invLen;
            }

            return new Point(
                (int)(baseX + perpX * noiseX + noiseY * 0.3),
                (int)(baseY + perpY * noiseX + noiseY * 0.3));
        }

        private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);

        private static double Lerp(double a, double b, double t) => a + t * (b - a);

        private static double Grad(int hash, double x, double y)
        {
            int h = hash & 15;
            double u = h < 8 ? x : y;
            double v = h < 4 ? y : (h == 12 || h == 14 ? x : 0);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        private static double Noise(double x, double y)
        {
            int X = (int)Math.Floor(x) & 255;
            int Y = (int)Math.Floor(y) & 255;

            x -= Math.Floor(x);
            y -= Math.Floor(y);

            double u = Fade(x);
            double v = Fade(y);

            int A  = permutation[X]     + Y;
            int AA = permutation[A];
            int AB = permutation[A + 1];
            int B  = permutation[X + 1] + Y;
            int BA = permutation[B];
            int BB = permutation[B + 1];

            return Lerp(
                Lerp(Grad(permutation[AA], x,     y    ), Grad(permutation[BA], x - 1, y    ), u),
                Lerp(Grad(permutation[AB], x,     y - 1), Grad(permutation[BB], x - 1, y - 1), u),
                v);
        }
    }
}
