using Venkatesh2.AILogic;
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
            // Base score from distance to predicted position (where we expect current target to be)
            float dx = candidate.ScreenCenterX - predictedX;
            float dy = candidate.ScreenCenterY - predictedY;
            float distSq = dx * dx + dy * dy;

            // Normalize distance score (0 = far, 1 = close)
            float thresholdSq = threshold * threshold;
            float distanceScore = Math.Max(0f, 1f - (distSq / thresholdSq));

            // Confidence bonus (0-0.3 range)
            float confidenceBonus = candidate.Confidence * 0.3f;

            // Size bonus - larger targets are more stable (0-0.2 range)
            float area = candidate.Rectangle.Width * candidate.Rectangle.Height;
            float sizeBonus = Math.Min(0.2f, area / 50000f);

            // Lock bonus for current target (0-0.5 range based on accumulated score)
            float lockBonus = (currentTarget != null && distanceScore > 0.3f)
                ? (currentLockScore / maxLockScore) * 0.5f
                : 0f;

            return distanceScore + confidenceBonus + sizeBonus + lockBonus;
        }
        public static int CalculateNumDetections(int imageSize)
        {
            // YOLOv8 detection calculation: (size/8)² + (size/16)² + (size/32)²
            int stride8 = imageSize / 8;
            int stride16 = imageSize / 16;
            int stride32 = imageSize / 32;

            return (stride8 * stride8) + (stride16 * stride16) + (stride32 * stride32);
        }
        // LUT = look up table
        // REFERENCE: https://stackoverflow.com/questions/1089235/where-can-i-find-a-byte-to-float-lookup-table
        // "In this case, the lookup table should be faster than using direct calculation. The more complex the math (trigonometry, etc.), the bigger the performance gain."
        // although we used small calculations, something is better than nothing.
        private static readonly float[] _byteToFloatLut = CreateByteToFloatLut();
        private static float[] CreateByteToFloatLut()
        {
            var lut = new float[256];
            for (int i = 0; i < 256; i++)
                lut[i] = i / 255f;
            return lut;
        }

        // this new function reduces gc pressure as i stopped using array.copy
        // REFERENCE: https://www.codeproject.com/Articles/617613/Fast-Pixel-Operations-in-NET-With-and-Without-unsa
        public static unsafe void BitmapToFloatArrayInPlace(Bitmap image, float[] result, int IMAGE_SIZE)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));

            var rect = new Rectangle(0, 0, IMAGE_SIZE, IMAGE_SIZE);
            var bmpData = image.LockBits(rect, ImageLockMode.ReadOnly, image.PixelFormat);
            try
            {
                BgraPointerToFloatArray(
                    (byte*)bmpData.Scan0,
                    Math.Abs(bmpData.Stride),
                    result,
                    IMAGE_SIZE,
                    applyThirdPersonMask: false);
            }
            finally
            {
                image.UnlockBits(bmpData);
            }
        }

        // Fast path: convert a BGRA pixel buffer (pointer + stride) to a planar RGB float[] in one pass.
        // Lets the capture path skip materializing an intermediate Bitmap when the AI loop is the only consumer.
        public static unsafe void BgraPointerToFloatArray(
            byte* basePtr, int stride,
            float[] result, int IMAGE_SIZE,
            bool applyThirdPersonMask)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            int width = IMAGE_SIZE;
            int height = IMAGE_SIZE;
            int totalPixels = width * height;

            if (result.Length != 3 * totalPixels)
                throw new ArgumentException($"result must be length {3 * totalPixels}", nameof(result));

            const int bytesPerPixel = 4;
            const int pixelsPerIteration = 4;

            int rOffset = 0;
            int gOffset = totalPixels;
            int bOffset = totalPixels * 2;

            fixed (float* dest = result)
            fixed (float* lutPtr = _byteToFloatLut)
            {
                float* rPtr = dest + rOffset;
                float* gPtr = dest + gOffset;
                float* bPtr = dest + bOffset;

                for (int y = 0; y < height; y++)
                {
                    byte* row = basePtr + (long)y * stride;
                    int rowStart = y * width;
                    int x = 0;

                    int widthLimit = width - pixelsPerIteration + 1;
                    for (; x < widthLimit; x += pixelsPerIteration)
                    {
                        int baseIdx = rowStart + x;
                        byte* p = row + (x * bytesPerPixel);

                        bPtr[baseIdx]     = lutPtr[p[0]];
                        gPtr[baseIdx]     = lutPtr[p[1]];
                        rPtr[baseIdx]     = lutPtr[p[2]];

                        bPtr[baseIdx + 1] = lutPtr[p[4]];
                        gPtr[baseIdx + 1] = lutPtr[p[5]];
                        rPtr[baseIdx + 1] = lutPtr[p[6]];

                        bPtr[baseIdx + 2] = lutPtr[p[8]];
                        gPtr[baseIdx + 2] = lutPtr[p[9]];
                        rPtr[baseIdx + 2] = lutPtr[p[10]];

                        bPtr[baseIdx + 3] = lutPtr[p[12]];
                        gPtr[baseIdx + 3] = lutPtr[p[13]];
                        rPtr[baseIdx + 3] = lutPtr[p[14]];
                    }

                    for (; x < width; x++)
                    {
                        int idx = rowStart + x;
                        byte* p = row + (x * bytesPerPixel);

                        bPtr[idx] = lutPtr[p[0]];
                        gPtr[idx] = lutPtr[p[1]];
                        rPtr[idx] = lutPtr[p[2]];
                    }
                }

                if (applyThirdPersonMask)
                {
                    int halfW = width / 2;
                    int halfH = height / 2;
                    int startY = height - halfH;

                    for (int y = startY; y < height; y++)
                    {
                        int rowStart = y * width;
                        for (int x = 0; x < halfW; x++)
                        {
                            int idx = rowStart + x;
                            rPtr[idx] = 0f;
                            gPtr[idx] = 0f;
                            bPtr[idx] = 0f;
                        }
                    }
                }
            }
        }
    }
}
