using Venkatesh2.Class;
using System.Diagnostics;

namespace AILogic
{
    internal class KalmanPrediction
    {
        public struct Detection
        {
            public int X;
            public int Y;
            // Timestamp removed — callers never read it, and DateTime.UtcNow is a syscall per frame.
        }

        private double _x, _y, _vx, _vy;

        private double _p00 = 1.0, _p11 = 1.0, _p22 = 1.0, _p33 = 1.0;

        private const double ProcessNoise = 0.1;
        private const double MeasurementNoise = 0.5;
        private const double MaxVelocity = 5000.0;

        // Stopwatch ticks instead of DateTime — no syscall, just a TSC read.
        private long _lastUpdateTick = 0;
        private bool _initialized = false;

        public void UpdateKalmanFilter(Detection detection)
        {
            long now = Stopwatch.GetTimestamp();

            if (!_initialized)
            {
                _x = detection.X;
                _y = detection.Y;
                _vx = 0;
                _vy = 0;
                _lastUpdateTick = now;
                _initialized = true;
                return;
            }

            double dt = (double)(now - _lastUpdateTick) / Stopwatch.Frequency;
            dt = Math.Clamp(dt, 0.001, 0.1);

            double predictedX = _x + _vx * dt;
            double predictedY = _y + _vy * dt;

            _p00 += ProcessNoise;
            _p11 += ProcessNoise;
            _p22 += ProcessNoise * 10;
            _p33 += ProcessNoise * 10;

            double innovationX = detection.X - predictedX;
            double innovationY = detection.Y - predictedY;

            double K = _p00 / (_p00 + MeasurementNoise);

            _x = predictedX + K * innovationX;
            _y = predictedY + K * innovationY;

            _vx += K * innovationX / dt;
            _vy += K * innovationY / dt;

            _vx = Math.Clamp(_vx, -MaxVelocity, MaxVelocity);
            _vy = Math.Clamp(_vy, -MaxVelocity, MaxVelocity);

            _p00 *= (1 - K);
            _p11 *= (1 - K);

            _lastUpdateTick = now;
        }

        public Detection GetKalmanPosition(double mouseSpeed = 0)
        {
            long now = Stopwatch.GetTimestamp();
            double dt = (double)(now - _lastUpdateTick) / Stopwatch.Frequency;

            double currentX = _x + _vx * dt;
            double currentY = _y + _vy * dt;

            double leadTime = (double)Dictionary.sliderSettings["Kalman Lead Time"];

            if (mouseSpeed > 0.0)
            {
                double estimatedCompletionTime = 100.0 / mouseSpeed;
                double dynamicLead = estimatedCompletionTime * 0.4;
                leadTime = dynamicLead * (leadTime / 0.10);
                leadTime = Math.Clamp(leadTime, 0.02, 0.3);
            }

            double predictedX = currentX + _vx * leadTime;
            double predictedY = currentY + _vy * leadTime;

            return new Detection
            {
                X = (int)predictedX,
                Y = (int)predictedY
            };
        }

        public void Reset()
        {
            _x = _y = _vx = _vy = 0;
            _p00 = _p11 = _p22 = _p33 = 1.0;
            _initialized = false;
        }
    }

    internal class WiseTheFoxPrediction
    {
        public struct WTFDetection
        {
            public int X;
            public int Y;
            // Timestamp removed — was DateTime.UtcNow, callers only use X/Y.
        }

        private long _lastUpdateTick = 0;
        private const double Alpha = 0.5;

        private double _emaX, _emaY;
        private double _velocityX, _velocityY;
        private double _prevX, _prevY;
        private bool _initialized = false;

        public void UpdateDetection(WTFDetection detection)
        {
            long now = Stopwatch.GetTimestamp();

            if (!_initialized)
            {
                _emaX = detection.X;
                _emaY = detection.Y;
                _prevX = detection.X;
                _prevY = detection.Y;
                _velocityX = 0;
                _velocityY = 0;
                _lastUpdateTick = now;
                _initialized = true;
                return;
            }

            double dt = (double)(now - _lastUpdateTick) / Stopwatch.Frequency;
            dt = Math.Clamp(dt, 0.001, 0.1);

            _emaX = Alpha * detection.X + (1.0 - Alpha) * _emaX;
            _emaY = Alpha * detection.Y + (1.0 - Alpha) * _emaY;

            double newVelocityX = (_emaX - _prevX) / dt;
            double newVelocityY = (_emaY - _prevY) / dt;

            _velocityX = Alpha * newVelocityX + (1.0 - Alpha) * _velocityX;
            _velocityY = Alpha * newVelocityY + (1.0 - Alpha) * _velocityY;

            _prevX = _emaX;
            _prevY = _emaY;
            _lastUpdateTick = now;
        }

        public WTFDetection GetEstimatedPosition()
        {
            double leadTime = (double)Dictionary.sliderSettings["WiseTheFox Lead Time"];

            double predictedX = _emaX + _velocityX * leadTime;
            double predictedY = _emaY + _velocityY * leadTime;

            return new WTFDetection
            {
                X = (int)predictedX,
                Y = (int)predictedY
            };
        }

        public void Reset()
        {
            _emaX = _emaY = 0;
            _velocityX = _velocityY = 0;
            _prevX = _prevY = 0;
            _initialized = false;
        }
    }

    internal class ShalloePredictionV2
    {
        private const int MaxHistorySize = 5;

        // Circular buffer replaces List<int> + RemoveAt(0) (O(n) shift) + LINQ Average (enumerator alloc).
        private static readonly int[] _velXBuf = new int[MaxHistorySize];
        private static readonly int[] _velYBuf = new int[MaxHistorySize];
        private static int _bufHead = 0;
        private static int _bufCount = 0;
        private static long _sumVelX = 0;
        private static long _sumVelY = 0;

        private static int _prevX = 0;
        private static int _prevY = 0;
        private static bool _initialized = false;

        public static void UpdatePosition(int targetX, int targetY)
        {
            if (!_initialized)
            {
                _prevX = targetX;
                _prevY = targetY;
                _initialized = true;
                return;
            }

            int velocityX = targetX - _prevX;
            int velocityY = targetY - _prevY;

            if (_bufCount == MaxHistorySize)
            {
                // Evict oldest entry from running sums, then overwrite slot.
                _sumVelX -= _velXBuf[_bufHead];
                _sumVelY -= _velYBuf[_bufHead];
                _velXBuf[_bufHead] = velocityX;
                _velYBuf[_bufHead] = velocityY;
                _bufHead = (_bufHead + 1) % MaxHistorySize;
            }
            else
            {
                int slot = (_bufHead + _bufCount) % MaxHistorySize;
                _velXBuf[slot] = velocityX;
                _velYBuf[slot] = velocityY;
                _bufCount++;
            }

            _sumVelX += velocityX;
            _sumVelY += velocityY;

            _prevX = targetX;
            _prevY = targetY;
        }

        public static (int x, int y) GetSP()
        {
            if (!_initialized || _bufCount == 0) return (_prevX, _prevY);

            double lead = (double)Dictionary.sliderSettings["Shalloe Lead Multiplier"];
            double avgVelX = (double)_sumVelX / _bufCount;
            double avgVelY = (double)_sumVelY / _bufCount;

            return ((int)(_prevX + avgVelX * lead), (int)(_prevY + avgVelY * lead));
        }

        public static int GetSPX() => GetSP().x;
        public static int GetSPY() => GetSP().y;

        public static void Reset()
        {
            _bufHead = 0;
            _bufCount = 0;
            _sumVelX = 0;
            _sumVelY = 0;
            _prevX = _prevY = 0;
            _initialized = false;
        }
    }
}
