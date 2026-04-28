using System.Diagnostics;
using Venkatesh2.Class;

namespace InputLogic
{
    internal static class RecoilManager
    {
        private static Thread? _thread;
        private static volatile bool _running;

        public static void Start()
        {
            if (_thread != null && _thread.IsAlive) return;
            _running = true;
            _thread = new Thread(Run)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
                Name = "RecoilLoop",
            };
            _thread.Start();
        }

        public static void Stop()
        {
            _running = false;
        }

        private static void Run()
        {
            var sw = Stopwatch.StartNew();
            long nextDownTick = sw.ElapsedTicks;
            long nextUpTick = sw.ElapsedTicks;

            while (_running)
            {
                bool downActive = Dictionary.toggleState["Recoil Control"]
                               && InputBindingManager.IsHoldingBinding("Recoil Keybind");
                bool upActive = Dictionary.toggleState["Up Recoil Control"]
                             && InputBindingManager.IsHoldingBinding("Up Recoil Keybind");

                long now = sw.ElapsedTicks;

                if (downActive)
                {
                    if (now >= nextDownTick)
                    {
                        int px = (int)Convert.ToDouble(Dictionary.sliderSettings["Recoil Strength"]);
                        if (px > 0) MouseManager.DragDown(px);

                        double hz = Convert.ToDouble(Dictionary.sliderSettings["Recoil Tick Rate"]);
                        if (hz < 1) hz = 1;
                        nextDownTick = now + (long)(Stopwatch.Frequency / hz);
                    }
                }
                else
                {
                    nextDownTick = now;
                }

                if (upActive)
                {
                    if (now >= nextUpTick)
                    {
                        int px = (int)Convert.ToDouble(Dictionary.sliderSettings["Up Recoil Strength"]);
                        if (px > 0) MouseManager.DragUp(px);

                        double hz = Convert.ToDouble(Dictionary.sliderSettings["Up Recoil Tick Rate"]);
                        if (hz < 1) hz = 1;
                        nextUpTick = now + (long)(Stopwatch.Frequency / hz);
                    }
                }
                else
                {
                    nextUpTick = now;
                }

                if (!downActive && !upActive)
                {
                    Thread.Sleep(5);
                    continue;
                }

                long target = long.MaxValue;
                if (downActive) target = Math.Min(target, nextDownTick);
                if (upActive) target = Math.Min(target, nextUpTick);

                long remaining = target - sw.ElapsedTicks;
                if (remaining > 0)
                {
                    int ms = (int)(remaining * 1000 / Stopwatch.Frequency);
                    if (ms > 0) Thread.Sleep(ms);
                }
            }
        }
    }
}
