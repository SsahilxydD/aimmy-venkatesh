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
            while (_running)
            {
                bool active = Dictionary.toggleState["Recoil Control"]
                           && InputBindingManager.IsHoldingBinding("Recoil Keybind");

                if (active)
                {
                    int px = (int)Convert.ToDouble(Dictionary.sliderSettings["Recoil Strength"]);
                    if (px > 0) MouseManager.DragDown(px);
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
        }
    }
}
