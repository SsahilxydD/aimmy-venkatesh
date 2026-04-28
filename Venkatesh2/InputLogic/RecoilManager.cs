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
            long nextTickTicks = sw.ElapsedTicks;

            while (_running)
            {
                bool active = Dictionary.toggleState["Recoil Control"]
                           && InputBindingManager.IsHoldingBinding("Recoil Keybind");

                if (active)
                {
                    int px = (int)Convert.ToDouble(Dictionary.sliderSettings["Recoil Strength"]);
                    if (px > 0) MouseManager.DragDown(px);

                    double hz = Convert.ToDouble(Dictionary.sliderSettings["Recoil Tick Rate"]);
                    if (hz < 1) hz = 1;
                    long periodTicks = (long)(Stopwatch.Frequency / hz);

                    nextTickTicks += periodTicks;
                    long now = sw.ElapsedTicks;
                    long remaining = nextTickTicks - now;
                    if (remaining > 0)
                    {
                        int ms = (int)(remaining * 1000 / Stopwatch.Frequency);
                        if (ms > 0) Thread.Sleep(ms);
                    }
                    else
                    {
                        nextTickTicks = now;
                    }
                }
                else
                {
                    nextTickTicks = sw.ElapsedTicks;
                    Thread.Sleep(5);
                }
            }
        }
    }
}
