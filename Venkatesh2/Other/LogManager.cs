using Class;
using Venkatesh2.Class;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Visuality;
using Vortice.DXGI;

namespace Other
{
    internal static class LogManager
    {
        public enum LogLevel { Info, Warning, Error, Fatal }

        private static readonly object _lock = new();
        private static string? _logPath;

        // Exposed so crash dialogs / error messages can point the user to the file.
        public static string LogPath => _logPath ?? "bin\\logs\\(not initialized)";

        private const int MAX_LOG_FILES = 5;
        private const string LOG_DIR = "bin\\logs";

        // Call once, as the very first thing in App.OnStartup.
        public static void Initialize()
        {
            try
            {
                Directory.CreateDirectory(LOG_DIR);
                RotateOldLogs();
                _logPath = Path.Combine(LOG_DIR, $"runtime_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");

                string cpu = "Unknown", gpu = "Unknown";
                string display = $"{WinAPICaller.ScreenWidth}x{WinAPICaller.ScreenHeight}";
                try
                {
                    using var s = new ManagementObjectSearcher("root\\CIMV2", "SELECT Name FROM Win32_Processor");
                    cpu = Convert.ToString(s.Get().Cast<ManagementObject>().FirstOrDefault()?["Name"])?.Trim() ?? "Unknown";
                }
                catch { }
                try
                {
                    using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
                    for (uint i = 0; factory.EnumAdapters1(i, out var adapter).Success; i++)
                    {
                        var desc = adapter.Description1;
                        if ((desc.Flags & AdapterFlags.Software) == 0) { gpu = desc.Description.Trim(); break; }
                    }
                }
                catch { }

                var sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine($"SESSION START : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                sb.AppendLine($"OS            : {Environment.OSVersion}");
                sb.AppendLine($"CLR           : {RuntimeInformation.FrameworkDescription}");
                sb.AppendLine($"Arch          : {RuntimeInformation.ProcessArchitecture}");
                sb.AppendLine($"CPU           : {cpu}  ({Environment.ProcessorCount} logical cores)");
                sb.AppendLine($"GPU           : {gpu}");
                sb.AppendLine($"Display       : {display}");
                sb.AppendLine("================================================================================");
                File.WriteAllText(_logPath, sb.ToString());
            }
            catch { }
        }

        public static void WriteFooter()
        {
            WriteToFile(LogLevel.Info, "================================================================================");
            WriteToFile(LogLevel.Info, $"SESSION END : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            WriteToFile(LogLevel.Info, "================================================================================");
        }

        // Drop any files beyond the most recent (MAX_LOG_FILES - 1) before creating a new one.
        private static void RotateOldLogs()
        {
            try
            {
                var stale = Directory.GetFiles(LOG_DIR, "runtime_*.log")
                    .OrderByDescending(f => f)
                    .Skip(MAX_LOG_FILES - 1)
                    .ToArray();
                foreach (var f in stale) try { File.Delete(f); } catch { }
            }
            catch { }
        }

        // ── Main log entry point — signature-compatible with all existing call sites ──────
        public static void Log(LogLevel lvl, string message, bool notifyUser = false, int waitingTime = 4000)
        {
            if (notifyUser)
                Application.Current?.Dispatcher.Invoke(() => new NoticeBar(message, waitingTime).Show());

#if DEBUG
            Debug.WriteLine(message);
#endif
            WriteToFile(lvl, message);

            // debug.txt mirror — only when the user explicitly enables Debug Mode.
            try
            {
                if (Convert.ToBoolean(Dictionary.toggleState["Debug Mode"]))
                {
                    using StreamWriter w = new("debug.txt", true);
                    w.WriteLine($"[{DateTime.Now}] [{lvl.ToString().ToUpper()}]: {message}");
                }
            }
            catch { }
        }

        // Full exception with stack trace and inner chain — use instead of ex.Message in catch blocks.
        public static void LogException(string context, Exception ex)
        {
            var sb = new StringBuilder();
            sb.Append($"EXCEPTION in {context}: ");
            AppendException(sb, ex);
            WriteToFile(LogLevel.Error, sb.ToString());
        }

        // For global crash handlers where the process is about to die.
        public static void LogFatal(string context, Exception ex)
        {
            var sb = new StringBuilder();
            sb.Append($"FATAL in {context}: ");
            AppendException(sb, ex);
            WriteToFile(LogLevel.Fatal, sb.ToString());
        }

        private static void AppendException(StringBuilder sb, Exception ex, int depth = 0)
        {
            string pad = depth > 0 ? new string(' ', depth * 2) : "";
            sb.AppendLine($"{pad}{ex.GetType().FullName}: {ex.Message}");
            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                foreach (var line in ex.StackTrace.Split('\n'))
                {
                    string trimmed = line.TrimEnd('\r');
                    if (trimmed.Length > 0) sb.AppendLine($"{pad}  {trimmed}");
                }
            }
            if (ex.InnerException != null)
            {
                sb.AppendLine($"{pad}--- Inner Exception ---");
                AppendException(sb, ex.InnerException, depth + 1);
            }
        }

        private static void WriteToFile(LogLevel lvl, string message)
        {
            if (_logPath == null) return;
            try
            {
                string prefix = $"[{DateTime.Now:HH:mm:ss.fff}] [{lvl,-7}] ";
                string cont   = new string(' ', prefix.Length);
                var sb = new StringBuilder();
                bool first = true;
                foreach (var raw in message.Split('\n'))
                {
                    string line = raw.TrimEnd('\r');
                    if (line.Length == 0) continue;
                    sb.AppendLine((first ? prefix : cont) + line);
                    first = false;
                }
                lock (_lock) File.AppendAllText(_logPath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }
}
