using Venkatesh2.Theme;
using Class;
using Other;
using System.Runtime.InteropServices;
using System.Windows;

namespace Venkatesh2
{
    public partial class App : Application
    {
        // Windows default scheduler tick is ~15.6 ms, which caps Task.Delay / Thread.Sleep granularity.
        // The AI loop's 144 FPS frame-budget sleep depends on sub-millisecond accuracy, so request 1ms
        // timer resolution for the lifetime of the process.
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint uMilliseconds);

        private const uint HighResTimerMs = 1;
        private bool _highResTimerActive;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Initialize the runtime log before anything else so every subsequent event is captured.
            LogManager.Initialize();

            // ── Global exception hooks ────────────────────────────────────────────────────
            // WPF UI-thread exceptions (data-binding failures, event handlers, etc.)
            DispatcherUnhandledException += (_, ev) =>
            {
                LogManager.LogFatal("DispatcherUnhandledException", ev.Exception);
                // Do NOT set e.Handled — let WPF terminate so the user sees a crash.
                // The log is already written to disk at LogManager.LogPath.
            };

            // CLR exceptions that escape every catch on background threads.
            AppDomain.CurrentDomain.UnhandledException += (_, ev) =>
            {
                if (ev.ExceptionObject is Exception ex)
                    LogManager.LogFatal("AppDomain.UnhandledException", ex);
                else
                    LogManager.Log(LogManager.LogLevel.Fatal, $"AppDomain.UnhandledException (non-Exception): {ev.ExceptionObject}");
            };

            // Async tasks whose exceptions are never observed — keep the process alive but log them.
            TaskScheduler.UnobservedTaskException += (_, ev) =>
            {
                LogManager.LogException("UnobservedTask", ev.Exception);
                ev.SetObserved();
            };

            try
            {
                if (TimeBeginPeriod(HighResTimerMs) == 0) _highResTimerActive = true;
            }
            catch { /* winmm unavailable on this host — tolerate and run at default resolution */ }

            // Initialize the application theme from saved settings
            InitializeTheme();

            // Set shutdown mode to prevent app from closing when startup window closes
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

#if DEBUG
            var _mainWindow = new MainWindow();
            MainWindow = _mainWindow;
            _mainWindow.Show();
            return;
#endif
            // code IS reachable, only in release though
            try
            {
                // Create and show startup window
                var startupWindow = new StartupWindow();
                startupWindow.Show();

                // Reset shutdown mode after startup window is shown
                ShutdownMode = ShutdownMode.OnMainWindowClose;
            }
            catch (Exception ex)
            {
                // If startup window fails, launch main window directly
                MessageBox.Show($"Startup animation failed: {ex.Message}\nLaunching main application...",
                              "Venkatesh AI", MessageBoxButton.OK, MessageBoxImage.Information);

                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                mainWindow.Show();

                ShutdownMode = ShutdownMode.OnMainWindowClose;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LogManager.WriteFooter();
            if (_highResTimerActive)
            {
                try { TimeEndPeriod(HighResTimerMs); } catch { }
                _highResTimerActive = false;
            }
            base.OnExit(e);
        }

        private void InitializeTheme()
        {
            try
            {
                // Load the color state configuration
                var colorState = new Dictionary<string, dynamic>
                {
                    { "Theme Color", "#FF722ED1" }
                };

                // Load saved colors
                SaveDictionary.LoadJSON(colorState, "bin\\colors.cfg");

                // Apply theme color if found
                if (colorState.TryGetValue("Theme Color", out var themeColor) && themeColor is string colorString)
                {
                    ThemeManager.SetThemeColor(colorString);
                }
                else
                {
                    // Use default purple if no saved color
                    ThemeManager.SetThemeColor("#FF722ED1");
                }
            }
            catch (Exception)
            {
                // Log error and use default color
                ThemeManager.SetThemeColor("#FF722ED1");
            }
        }
    }
}