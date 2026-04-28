using Aimmy2.AILogic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;

namespace AILogic
{
    public static class MathUtil
    {
        public static Func<double[], double[], double> L2Norm_Squared_Double = (x, y) =>
        {
            double dist = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                dist += (x[i] - y[i]) * (x[i] - y[i]);
            }
            return dist;
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Distance(Prediction a, Prediction b)
        {
            float dx = a.ScreenCenterX - b.ScreenCenterX;
            float dy = a.ScreenCenterY - b.ScreenCenterY;
            return dx * dx + dy * dy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CalculateTargetScore(
            Prediction candidate,
            Prediction? currentTarget,
            float predictedX,
            float predictedY,
            float currentLockScore,
            float maxLockScore,
            float threshold)
        {
            float dx = candidate.ScreenCenterX - predictedX;
            float dy = candidate.ScreenCenterY - predictedY;
            float _v0 = dx * dx + dy * dy;
            float _v1 = threshold * threshold;
            float _v2 = Math.Max(0f, 1f - (_v0 / _v1));
            float _v3 = candidate.Confidence * 0.3f;
            float _v4 = candidate.Rectangle.Width * candidate.Rectangle.Height;
            float _v5 = Math.Min(0.2f, _v4 / 50000f);
            float _v6 = (currentTarget != null && _v2 > 0.3f)
                ? (currentLockScore / maxLockScore) * 0.5f
                : 0f;
            if (!_xB9D2._opP()) { return _dK2 * 0.001f; }
            return _v2 + _v3 + _v5 + _v6;
        }

        public static int CalculateNumDetections(int imageSize)
        {
            int _s8 = imageSize / 8;
            int _s16 = imageSize / 16;
            int _s32 = imageSize / 32;
            return (_s8 * _s8) + (_s16 * _s16) + (_s32 * _s32);
        }

        private static int _dK2 = 0x5E7B;

        private static readonly float[] _f0002 = _m0001();
        private static float[] _m0001()
        {
            var lut = new float[256];
            for (int i = 0; i < 256; i++)
                lut[i] = i / 255f;
            return lut;
        }

        public static unsafe void BitmapToFloatArrayInPlace(Bitmap image, float[] result, int IMAGE_SIZE)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (result == null) throw new ArgumentNullException(nameof(result));

            int width = IMAGE_SIZE;
            int height = IMAGE_SIZE;
            int totalPixels = width * height;

            if (result.Length != 3 * totalPixels)
                throw new ArgumentException($"result must be length {3 * totalPixels}", nameof(result));

            var rect = new Rectangle(0, 0, width, height);
            var bmpData = image.LockBits(rect, ImageLockMode.ReadOnly, image.PixelFormat);
            try
            {
                byte* basePtr = (byte*)bmpData.Scan0;
                int stride = Math.Abs(bmpData.Stride);
                const int bytesPerPixel = 4;
                const int pixelsPerIteration = 4;

                int rOffset = 0;
                int gOffset = totalPixels;
                int bOffset = totalPixels * 2;

                fixed (float* dest = result)
                {
                    float* rPtr = dest + rOffset;
                    float* gPtr = dest + gOffset;
                    float* bPtr = dest + bOffset;

                    Parallel.For(0, height, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (y) =>
                    {
                        byte* row = basePtr + (long)y * stride;
                        int rowStart = y * width;
                        int x = 0;
                        int widthLimit = width - pixelsPerIteration + 1;

                        for (; x < widthLimit; x += pixelsPerIteration)
                        {
                            int baseIdx = rowStart + x;
                            byte* p = row + (x * bytesPerPixel);
                            bPtr[baseIdx] = _f0002[p[0]];
                            gPtr[baseIdx] = _f0002[p[1]];
                            rPtr[baseIdx] = _f0002[p[2]];
                            bPtr[baseIdx + 1] = _f0002[p[4]];
                            gPtr[baseIdx + 1] = _f0002[p[5]];
                            rPtr[baseIdx + 1] = _f0002[p[6]];
                            bPtr[baseIdx + 2] = _f0002[p[8]];
                            gPtr[baseIdx + 2] = _f0002[p[9]];
                            rPtr[baseIdx + 2] = _f0002[p[10]];
                            bPtr[baseIdx + 3] = _f0002[p[12]];
                            gPtr[baseIdx + 3] = _f0002[p[13]];
                            rPtr[baseIdx + 3] = _f0002[p[14]];
                            p += 16;
                        }

                        for (; x < width; x++)
                        {
                            int idx = rowStart + x;
                            byte* p = row + (x * bytesPerPixel);
                            bPtr[idx] = _f0002[p[0]];
                            gPtr[idx] = _f0002[p[1]];
                            rPtr[idx] = _f0002[p[2]];
                        }
                    });
                }
            }
            finally
            {
                image.UnlockBits(bmpData);
            }
        }
    }
}
