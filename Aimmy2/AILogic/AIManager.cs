using AILogic;
using Aimmy2.Class;
using Class;
using InputLogic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Newtonsoft.Json.Linq;
using Other;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Visuality;
using static AILogic.MathUtil;
using static Other.LogManager;

namespace Aimmy2.AILogic
{
    internal class AIManager : IDisposable
    {

        private int _f0030;
        private readonly object _f0031 = new object();
        private volatile bool _f0032 = false;

        public void RequestSizeChange(int newSize)
        {
            lock (_f0031)
            {
                _f0032 = true;
            }
        }

        
        public int IMAGE_SIZE => _f0030;
        private int NUM_DETECTIONS { get; set; } = 8400; 
        private bool IsDynamicModel { get; set; } = false;

        
        public static bool CurrentModelIsDynamic { get; private set; } = false;
        private int ModelFixedSize { get; set; } = 640; 
        private int NUM_CLASSES { get; set; } = 1;
        private Dictionary<int, string> _f0033 = new Dictionary<int, string>
        {
            { 0, _xB9D2._c3C }
        };
        public Dictionary<int, string> ModelClasses => _f0033; 
        public static event Action<Dictionary<int, string>>? ClassesUpdated;
        public static event Action<int>? ImageSizeUpdated;
        public static event Action<bool>? DynamicModelStatusChanged;

        private const int _f0034 = 500;

        private DateTime _f0035 = DateTime.MinValue;
        private List<string>? _f0036;
        private RectangleF _f0037;
        private KalmanPrediction _f0038;
        private WiseTheFoxPrediction _f0039;

        private byte[]? _f003A; 

        
        private int ScreenWidth => DisplayManager.ScreenWidth;
        private int ScreenHeight => DisplayManager.ScreenHeight;
        private int ScreenLeft => DisplayManager.ScreenLeft;
        private int ScreenTop => DisplayManager.ScreenTop;

        private readonly RunOptions? _f003B;
        private InferenceSession? _f003C;

        private Thread? _f003D;
        private volatile bool _f003E;

        
        private bool _f003F = false;

        
        private Prediction? _f0040 = null;
        private int _f0041 = 0;
        private const int _f0042 = 3; 

        
        private float _f0043 = 0f;
        private float _f0044 = 0f;
        private float _f0045 = 0f;           
        private const float _f0046 = 0.85f;  
        private const float _f0047 = 15f;     
        private const float _f0048 = 100f;     
        private const float _f0049 = 10000f; 
        private int _f004A = 0;           

        private double _f004B = 0;
        private double _f004C = 0;
        private static int _dK6 = 0x4E2D;
        private static long _dK7 = 0L;
        private static byte _dK8 = 0;

        
        private int _f004D = 0;
        private long _f004E = 0;

        private int _f004F { get; set; }
        private int _f0050 { get; set; }

        public double AIConf = 0;
        private static int _f0051, _f0052;

        
        private float _scaleX => ScreenWidth / (float)IMAGE_SIZE;
        private float _scaleY => ScreenHeight / (float)IMAGE_SIZE;

        
        private DenseTensor<float>? _f0053;
        private float[]? _f0054;
        private List<NamedOnnxValue>? _f0055;

        
        private readonly Dictionary<string, _cBD> _f0056 = new();
        private readonly object _f0057 = new();

        private readonly CaptureManager _f0058 = new();

        private class _cBD
        {
            public long TotalTime { get; set; }
            public int CallCount { get; set; }
            public long MinTime { get; set; } = long.MaxValue;
            public long MaxTime { get; set; }
            public double AverageTime => CallCount > 0 ? (double)TotalTime / CallCount : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IDisposable _m004B(string name)
        {
            return new _cBS(this, name);
        }

        private class _cBS : IDisposable
        {
            private readonly AIManager _manager;
            private readonly string _name;
            private readonly Stopwatch _sw;

            public _cBS(AIManager manager, string name)
            {
                _manager = manager;
                _name = name;
                _sw = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _sw.Stop();
                _manager._m004C(_name, _sw.ElapsedMilliseconds);
            }
        }

        private void _m004C(string name, long elapsedMs)
        {
            lock (_f0057)
            {
                if (!_f0056.TryGetValue(name, out var data))
                {
                    data = new _cBD();
                    _f0056[name] = data;
                }

                data.TotalTime += elapsedMs;
                data.CallCount++;
                data.MinTime = Math.Min(data.MinTime, elapsedMs);
                data.MaxTime = Math.Max(data.MaxTime, elapsedMs);
            }
        }

        public void PrintBenchmarks()
        {
            lock (_f0057)
            {
                var lines = new List<string>
                {
                    "=== AIManager Performance Benchmarks ==="
                };

                foreach (var kvp in _f0056.OrderBy(x => x.Key))
                {
                    var data = kvp.Value;
                    lines.Add($"{kvp.Key}: Avg={data.AverageTime:F2}ms, Min={data.MinTime}ms, Max={data.MaxTime}ms, Count={data.CallCount}");
                }

                lines.Add($"Overall FPS: {(_f004D > 0 ? 1000.0 / (_f004E / (double)_f004D) : 0):F2}");

                

                Log(LogLevel.Info, string.Join(Environment.NewLine, lines));
            }
        }

        public AIManager(string modelPath)
        {
            
            _f0030 = int.Parse(Dictionary.dropdownState[_xB9D2._c1D]);

            
            if (Dictionary.dropdownState[_xB9D2._c1E] == _xB9D2._c26)
            {
                _f0058.InitializeDxgiDuplication();
            }

            _f0038 = new KalmanPrediction();
            _f0039 = new WiseTheFoxPrediction();

            _f003B = new RunOptions();

            var sessionOptions = new SessionOptions
            {
                EnableCpuMemArena = true,
                EnableMemoryPattern = false,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                InterOpNumThreads = 1,
                IntraOpNumThreads = 4
            };

            
            Task.Run(() => _m0030(sessionOptions, modelPath));
        }

        private async Task _m0030(SessionOptions sessionOptions, string modelPath)
        {
            using (_m004B("ModelInitialization"))
            {
                try
                {
                    await _m0031(sessionOptions, modelPath, useDirectML: true);
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Error, $"Error starting the model via DirectML: {ex.Message}\n\nFalling back to CPU, performance may be poor.", true);

                    try
                    {
                        await _m0031(sessionOptions, modelPath, useDirectML: false);
                    }
                    catch (Exception e)
                    {
                        Log(LogLevel.Error, $"Error starting the model via CPU: {e.Message}, you won't be able to aim assist at all.", true);
                    }
                }

                FileManager.CurrentlyLoadingModel = false;
            }
        }

        private Task _m0031(SessionOptions sessionOptions, string modelPath, bool useDirectML)
        {
            try
            {
                if (useDirectML) { sessionOptions.AppendExecutionProvider_DML(); }
                else { sessionOptions.AppendExecutionProvider_CPU(); }

                _f003C = new InferenceSession(modelPath, sessionOptions);
                _f0036 = new List<string>(_f003C.OutputMetadata.Keys);

                
                if (!_m0032())
                {
                    _f003C?.Dispose();
                    return Task.CompletedTask;
                }

                
                _f003A = new byte[3 * IMAGE_SIZE * IMAGE_SIZE];
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error loading the model: {ex.Message}", true);
                _f003C?.Dispose();
                return Task.CompletedTask;
            }

            
            _f003E = true;
            _f003D = new Thread(_m0036)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal 
            };
            _f003D.Start();
            return Task.CompletedTask;
        }

        private bool _m0032()
        {
            if (_f003C != null)
            {
                var inputMetadata = _f003C.InputMetadata;
                var outputMetadata = _f003C.OutputMetadata;

                Log(LogLevel.Info, "=== Model Metadata ===");
                Log(LogLevel.Info, "Input Metadata:");

                bool isDynamic = false;
                int fixedInputSize = 0;

                foreach (var kvp in inputMetadata)
                {
                    string dimensionsStr = string.Join("x", kvp.Value.Dimensions);
                    Log(LogLevel.Info, $"  Name: {kvp.Key}, Dimensions: {dimensionsStr}");

                    
                    if (kvp.Value.Dimensions.Any(d => d == -1))
                    {
                        isDynamic = true;
                    }
                    else if (kvp.Value.Dimensions.Length == 4)
                    {
                        
                        fixedInputSize = kvp.Value.Dimensions[2]; 
                    }
                }

                Log(LogLevel.Info, "Output Metadata:");
                foreach (var kvp in outputMetadata)
                {
                    string dimensionsStr = string.Join("x", kvp.Value.Dimensions);
                    Log(LogLevel.Info, $"  Name: {kvp.Key}, Dimensions: {dimensionsStr}");
                }

                IsDynamicModel = isDynamic;
                CurrentModelIsDynamic = isDynamic;

                if (IsDynamicModel)
                {
                    
                    NUM_DETECTIONS = CalculateNumDetections(IMAGE_SIZE);
                    _m0033();
                    ImageSizeUpdated?.Invoke(IMAGE_SIZE);
                    Log(LogLevel.Info, $"Loaded dynamic model - using selected image size {IMAGE_SIZE}x{IMAGE_SIZE} with {NUM_DETECTIONS} detections", true, 3000);
                }
                else
                {
                    
                    ModelFixedSize = fixedInputSize;

                    
                    var supportedSizes = new[] { "640", "512", "416", "320", "256", "160" };
                    var fixedSizeStr = fixedInputSize.ToString();

                    if (!supportedSizes.Contains(fixedSizeStr))
                    {
                        Log(LogLevel.Error,
                            $"Model requires unsupported size {fixedInputSize}x{fixedInputSize}. Supported sizes are: {string.Join(", ", supportedSizes)}",
                            true, 10000);
                        return false;
                    }

                    
                    NUM_DETECTIONS = CalculateNumDetections(fixedInputSize);
                    _f0030 = fixedInputSize;

                    if (fixedInputSize != int.Parse(Dictionary.dropdownState[_xB9D2._c1D]))
                    {
                        
                        Log(LogLevel.Warning,
                            $"Fixed-size model expects {fixedInputSize}x{fixedInputSize}. Automatically adjusting Image Size setting.",
                            true, 3000);

                        Dictionary.dropdownState[_xB9D2._c1D] = fixedSizeStr;

                        
                        Application.Current?.Dispatcher.BeginInvoke(() =>
                        {
                            try
                            {
                                
                                var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                                if (mainWindow?.SettingsMenuControlInstance != null)
                                {
                                    mainWindow.SettingsMenuControlInstance.UpdateImageSizeDropdown(fixedSizeStr);
                                }
                            }
                            catch { }
                        });
                    }

                    ImageSizeUpdated?.Invoke(fixedInputSize);
                    _m0033();

                    
                    var expectedShape = new int[] { 1, 4 + NUM_CLASSES, NUM_DETECTIONS };
                    if (!outputMetadata.Values.All(metadata => metadata.Dimensions.SequenceEqual(expectedShape)))
                    {
                        Log(LogLevel.Error,
                            $"Output shape does not match the expected shape of {string.Join("x", expectedShape)}.\nThis model will not work with Xeno, please use an YOLOv8 model converted to ONNXv8.",
                            true, 10000);
                        return false;
                    }

                    Log(LogLevel.Info, $"Loaded fixed-size model: {fixedInputSize}x{fixedInputSize}", true, 2000);
                }

                
                DynamicModelStatusChanged?.Invoke(IsDynamicModel);

                return true;
            }

            return false;
        }

        private void _m0033()
        {
            if (_f003C == null) return;
            _f0033.Clear();

            try
            {
                var metadata = _f003C.ModelMetadata;

                if (metadata != null &&
                    metadata.CustomMetadataMap.TryGetValue(_xB9D2._c3D, out string? value) &&
                    !string.IsNullOrEmpty(value))
                {
                    JObject data = JObject.Parse(value);
                    if (data != null && data.Type == JTokenType.Object)
                    {
                        
                        foreach (var item in data)
                        {
                            if (int.TryParse(item.Key, out int classId) && item.Value.Type == JTokenType.String)
                            {
                                _f0033[classId] = item.Value.ToString();
                            }
                        }
                        NUM_CLASSES = _f0033.Count > 0 ? _f0033.Keys.Max() + 1 : 1;
                        Log(LogLevel.Info, $"Loaded {_f0033.Count} class(es) from model metadata: {data.ToString(Newtonsoft.Json.Formatting.None)}", false);
                    }
                    else
                    {
                        Log(LogLevel.Error, "Model metadata 'names' field is not a valid JSON object.", true);
                    }
                }
                else
                {
                    Log(LogLevel.Error, "Model metadata does not contain 'names' field for classes.", true);
                }
                ClassesUpdated?.Invoke(new Dictionary<int, string>(_f0033));
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error loading classes: {ex.Message}", true);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool _m0034() =>
            Dictionary.toggleState[_xB9D2._c02] ||
            Dictionary.toggleState[_xB9D2._c03] ||
            InputBindingManager.IsHoldingBinding(_xB9D2._c3A) ||
            InputBindingManager.IsHoldingBinding(_xB9D2._c3B);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool _m0035() =>
            Dictionary.toggleState[_xB9D2._c01] ||
            Dictionary.toggleState[_xB9D2._c02] ||
            Dictionary.toggleState[_xB9D2._c04];

        private async void _m0036()
        {
            Stopwatch stopwatch = new();
            DetectedPlayerWindow? DetectedPlayerOverlay = Dictionary.DetectedPlayerOverlay;

            while (_f003E)
            {
                
                lock (_f0031)
                {
                    if (_f0032)
                    {
                        
                        continue;
                    }
                }

                stopwatch.Restart();

                
                _f0058.HandlePendingDisplayChanges();

                using (_m004B("AILoopIteration"))
                {
                    _m0039();

                    if (_m0035())
                    {
                        if (_m0034())
                        {
                            Prediction? closestPrediction;
                            using (_m004B("_m0040"))
                            {
                                closestPrediction = await _m0040();
                            }

                            if (closestPrediction == null)
                            {
                                _m003A(DetectedPlayerOverlay!);
                                continue;
                            }

                            using (_m004B("_m0037"))
                            {
                                await _m0037();
                            }

                            using (_m004B("_m003C"))
                            {
                                _m003C(DetectedPlayerOverlay, closestPrediction, _scaleX, _scaleY);
                            }

                            using (_m004B("_m003E"))
                            {
                                _m003E(closestPrediction);
                            }

                            _f004E += stopwatch.ElapsedMilliseconds;
                            _f004D++;
                        }
                        else
                        {
                            
                            await Task.Delay(1);
                        }
                    }
                    else
                    {
                        
                        await Task.Delay(1);
                    }
                }

                stopwatch.Stop();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task _m0037()
        {
            
            
            
            
            if (!Dictionary.toggleState[_xB9D2._c04] ||
                !(InputBindingManager.IsHoldingBinding(_xB9D2._c3A) && !InputBindingManager.IsHoldingBinding(_xB9D2._c3B)) ||
                Dictionary.toggleState[_xB9D2._c03]) 
                                                                
            {
                _m0038();
                return;
            }

            if (Dictionary.toggleState[_xB9D2._c08])
            {
                await MouseManager.DoTriggerClick(_f0037);
                return;
            }

            if (Dictionary.toggleState[_xB9D2._c09])
            {
                var mousePos = WinAPICaller.GetCursorPosition();

                if (!DisplayManager.IsPointInCurrentDisplay(new System.Windows.Point(mousePos.X, mousePos.Y)))
                {
                    return;
                }

                if (_f0037.Contains(mousePos.X, mousePos.Y))
                {
                    await MouseManager.DoTriggerClick(_f0037);
                }
            }
            else
            {
                await MouseManager.DoTriggerClick();
            }

            if (!Dictionary.toggleState[_xB9D2._c01] || !Dictionary.toggleState[_xB9D2._c02]) return;

        }
        private void _m0038()
        {
            if (!Dictionary.toggleState[_xB9D2._c08]) return;

            
            
            bool shouldSpray = Dictionary.toggleState[_xB9D2._c04] &&
                (InputBindingManager.IsHoldingBinding(_xB9D2._c3A) && InputBindingManager.IsHoldingBinding(_xB9D2._c3B)); 
                                                                                                                                     

            
            if (!shouldSpray)
            {
                MouseManager.ResetSprayState();
            }
        }

        private async void _m0039()
        {
            if (Dictionary.dropdownState[_xB9D2._c1F] == _xB9D2._c28 && Dictionary.toggleState[_xB9D2._c06])
            {
                var mousePosition = WinAPICaller.GetCursorPosition();

                
                if (!DisplayManager.IsPointInCurrentDisplay(new System.Windows.Point(mousePosition.X, mousePosition.Y)))
                {
                    
                    return;
                }

                
                var displayRelativeX = mousePosition.X - DisplayManager.ScreenLeft;
                var displayRelativeY = mousePosition.Y - DisplayManager.ScreenTop;

                await Application.Current.Dispatcher.BeginInvoke(() =>
                    Dictionary.FOVWindow.FOVStrictEnclosure.Margin = new Thickness(
                        Convert.ToInt16(displayRelativeX / WinAPICaller.scalingFactorX) - 320, 
                        Convert.ToInt16(displayRelativeY / WinAPICaller.scalingFactorY) - 320, 0, 0));
            }
        }

        private static void _m003A(DetectedPlayerWindow DetectedPlayerOverlay)
        {
            if (Dictionary.toggleState[_xB9D2._c02] && Dictionary.DetectedPlayerOverlay != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Dictionary.toggleState[_xB9D2._c0A])
                    {
                        DetectedPlayerOverlay!.DetectedPlayerConfidence.Opacity = 0;
                    }

                    if (Dictionary.toggleState[_xB9D2._c0B])
                    {
                        DetectedPlayerOverlay!.DetectedTracers.Opacity = 0;
                    }

                    DetectedPlayerOverlay!.DetectedPlayerFocus.Opacity = 0;
                });
            }
        }

        private void _m003B(DetectedPlayerWindow DetectedPlayerOverlay, Prediction closestPrediction)
        {
            var scalingFactorX = WinAPICaller.scalingFactorX;
            var scalingFactorY = WinAPICaller.scalingFactorY;

            
            var displayRelativeX = _f0037.X - DisplayManager.ScreenLeft;
            var displayRelativeY = _f0037.Y - DisplayManager.ScreenTop;

            
            var centerX = Convert.ToInt16(displayRelativeX / scalingFactorX) + (_f0037.Width / 2.0);
            var centerY = Convert.ToInt16(displayRelativeY / scalingFactorY);

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Dictionary.toggleState[_xB9D2._c0A])
                {
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Opacity = 1;
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Content = $"{closestPrediction.ClassName}: {Math.Round((AIConf * 100), 2)}%";

                    var labelEstimatedHalfWidth = DetectedPlayerOverlay.DetectedPlayerConfidence.ActualWidth / 2.0;
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Margin = new Thickness(
                        centerX - labelEstimatedHalfWidth,
                        centerY - DetectedPlayerOverlay.DetectedPlayerConfidence.ActualHeight - 2, 0, 0);
                }
                var showTracers = Dictionary.toggleState[_xB9D2._c0B];
                DetectedPlayerOverlay.DetectedTracers.Opacity = showTracers ? 1 : 0;
                if (showTracers)
                {
                    var tracerPosition = Dictionary.dropdownState[_xB9D2._c23];

                    var boxTop = centerY;
                    var boxBottom = centerY + _f0037.Height;
                    var boxHorizontalCenter = centerX;
                    var boxVerticalCenter = centerY + (_f0037.Height / 2.0);
                    var boxLeft = centerX - (_f0037.Width / 2.0);
                    var boxRight = centerX + (_f0037.Width / 2.0);

                    if (tracerPosition == _xB9D2._c2A)
                    {
                        DetectedPlayerOverlay.DetectedTracers.X2 = boxHorizontalCenter;
                        DetectedPlayerOverlay.DetectedTracers.Y2 = boxTop;
                    }
                    else if (tracerPosition == _xB9D2._c2B)
                    {
                        DetectedPlayerOverlay.DetectedTracers.X2 = boxHorizontalCenter;
                        DetectedPlayerOverlay.DetectedTracers.Y2 = boxBottom;
                    }
                    else if (tracerPosition == _xB9D2._c2C)
                    {
                        var screenHorizontalCenter = DisplayManager.ScreenWidth / (2.0 * WinAPICaller.scalingFactorX);
                        if (boxHorizontalCenter < screenHorizontalCenter)
                        {
                            DetectedPlayerOverlay.DetectedTracers.X2 = boxRight;
                            DetectedPlayerOverlay.DetectedTracers.Y2 = boxVerticalCenter;
                        }
                        else
                        {
                            DetectedPlayerOverlay.DetectedTracers.X2 = boxLeft;
                            DetectedPlayerOverlay.DetectedTracers.Y2 = boxVerticalCenter;
                        }
                    }
                    else
                    {
                        DetectedPlayerOverlay.DetectedTracers.X2 = boxHorizontalCenter;
                        DetectedPlayerOverlay.DetectedTracers.Y2 = boxBottom;
                    }
                }

                DetectedPlayerOverlay.Opacity = Dictionary.sliderSettings[_xB9D2._c1B];

                DetectedPlayerOverlay.DetectedPlayerFocus.Opacity = 1;
                DetectedPlayerOverlay.DetectedPlayerFocus.Margin = new Thickness(
                    centerX - (_f0037.Width / 2.0), centerY, 0, 0);
                DetectedPlayerOverlay.DetectedPlayerFocus.Width = _f0037.Width;
                DetectedPlayerOverlay.DetectedPlayerFocus.Height = _f0037.Height;
            });
        }

        private void _m003C(DetectedPlayerWindow DetectedPlayerOverlay, Prediction closestPrediction, float scaleX, float scaleY)
        {
            if (!_xB9D2._opP()) { _dK7 = _dK6 * 3L; return; }
            AIConf = closestPrediction.Confidence;

            if (Dictionary.toggleState[_xB9D2._c02] && Dictionary.DetectedPlayerOverlay != null)
            {
                using (_m004B("_m003B"))
                {
                    _m003B(DetectedPlayerOverlay!, closestPrediction);
                }
                if (!Dictionary.toggleState[_xB9D2._c01]) return;
            }

            double YOffset = Dictionary.sliderSettings[_xB9D2._c16];
            double XOffset = Dictionary.sliderSettings[_xB9D2._c17];

            double YOffsetPercentage = Dictionary.sliderSettings[_xB9D2._c18];
            double XOffsetPercentage = Dictionary.sliderSettings[_xB9D2._c19];

            var rect = closestPrediction.Rectangle;

            if (Dictionary.toggleState[_xB9D2._c10])
            {
                _f004F = (int)((rect.X + (rect.Width * (XOffsetPercentage / 100))) * scaleX);
            }
            else
            {
                _f004F = (int)((rect.X + rect.Width / 2) * scaleX + XOffset);
            }

            if (Dictionary.toggleState[_xB9D2._c11])
            {
                _f0050 = (int)((rect.Y + rect.Height - (rect.Height * (YOffsetPercentage / 100))) * scaleY + YOffset);
            }
            else
            {
                _f0050 = _m003D(scaleY, YOffset, closestPrediction);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int _m003D(float scaleY, double YOffset, Prediction closestPrediction)
        {
            var rect = closestPrediction.Rectangle;
            float yBase = rect.Y;
            float yAdjustment = 0;
            int _st = 0;
            while (_st < 3)
            {
                switch (_st)
                {
                    case 0:
                        var _aba = Dictionary.dropdownState[_xB9D2._c20];
                        _st = (_aba == _xB9D2._c29) ? 1 : (_aba == _xB9D2._c2B) ? 2 : 3;
                        break;
                    case 1:
                        yAdjustment = rect.Height / 2;
                        _st = 3;
                        break;
                    case 2:
                        yAdjustment = rect.Height;
                        _st = 3;
                        break;
                }
            }
            return (int)((yBase + yAdjustment) * scaleY + YOffset);
        }

        private void _m003E(Prediction closestPrediction)
        {
            int _st = 0;
            while (_st < 4)
            {
                switch (_st)
                {
                    case 0:
                        _st = Dictionary.toggleState[_xB9D2._c01] ? 1 : 4;
                        break;
                    case 1:
                        bool _kh = Dictionary.toggleState[_xB9D2._c03] ||
                            (Dictionary.toggleState[_xB9D2._c01] && InputBindingManager.IsHoldingBinding(_xB9D2._c3A)) ||
                            (Dictionary.toggleState[_xB9D2._c01] && InputBindingManager.IsHoldingBinding(_xB9D2._c3B));
                        _st = _kh ? 2 : 4;
                        break;
                    case 2:
                        _st = Dictionary.toggleState[_xB9D2._c07] ? 3 : 4;
                        if (_st == 4) MouseManager.MoveCrosshair(_f004F, _f0050);
                        break;
                    case 3:
                        _m003F(_f0038, closestPrediction, _f004F, _f0050);
                        _st = 4;
                        break;
                }
            }
        }

        private void _m003F(KalmanPrediction _f0038, Prediction closestPrediction, int _f004F, int _f0050)
        {
            var predictionMethod = Dictionary.dropdownState[_xB9D2._c24];
            if (predictionMethod == _xB9D2._c36)
            {
                KalmanPrediction.Detection detection = new()
                {
                    X = _f004F,
                    Y = _f0050,
                    Timestamp = DateTime.UtcNow
                };

                _f0038.UpdateKalmanFilter(detection);
                var predictedPosition = _f0038.GetKalmanPosition();

                MouseManager.MoveCrosshair(predictedPosition.X, predictedPosition.Y);
            }
            else if (predictionMethod == _xB9D2._c37)
            {
                ShalloePredictionV2.UpdatePosition(_f004F, _f0050);
                MouseManager.MoveCrosshair(ShalloePredictionV2.GetSPX(), ShalloePredictionV2.GetSPY());
            }
            else if (predictionMethod == _xB9D2._c38)
            {
                WiseTheFoxPrediction.WTFDetection wtfdetection = new()
                {
                    X = _f004F,
                    Y = _f0050,
                    Timestamp = DateTime.UtcNow
                };

                _f0039.UpdateDetection(wtfdetection);
                var wtfpredictedPosition = _f0039.GetEstimatedPosition();

                MouseManager.MoveCrosshair(wtfpredictedPosition.X, wtfpredictedPosition.Y);
            }
        }

        private async Task<Prediction?> _m0040(bool useMousePosition = true)
        {
            if (!_xB9D2._opP()) { _dK7 = _dK6 ^ DateTime.UtcNow.Ticks; return null; }

            if (Dictionary.dropdownState[_xB9D2._c1F] == _xB9D2._c28)
            {
                var mousePos = WinAPICaller.GetCursorPosition();

                
                if (DisplayManager.IsPointInCurrentDisplay(new System.Windows.Point(mousePos.X, mousePos.Y)))
                {
                    
                    _f0051 = mousePos.X;
                    _f0052 = mousePos.Y;
                }
                else
                {
                    
                    _f0051 = DisplayManager.ScreenLeft + (DisplayManager.ScreenWidth / 2);
                    _f0052 = DisplayManager.ScreenTop + (DisplayManager.ScreenHeight / 2);
                }
            }
            else
            {
                
                _f0051 = DisplayManager.ScreenLeft + (DisplayManager.ScreenWidth / 2);
                _f0052 = DisplayManager.ScreenTop + (DisplayManager.ScreenHeight / 2);
            }

            Rectangle detectionBox = new(_f0051 - IMAGE_SIZE / 2, _f0052 - IMAGE_SIZE / 2, IMAGE_SIZE, IMAGE_SIZE); 

            Bitmap? frame;

            using (_m004B("ScreenGrab"))
            {
                frame = _f0058.ScreenGrab(detectionBox);
            }

            if (frame == null) return null;

            IDisposableReadOnlyCollection<DisposableNamedOnnxValue>? results = null;
            Tensor<float>? outputTensor = null;

            try
            {
                float[] inputArray;
                using (_m004B("BitmapToFloatArray"))
                {
                    if (_f0054 == null || _f0054.Length != 3 * IMAGE_SIZE * IMAGE_SIZE)
                    {
                        _f0054 = new float[3 * IMAGE_SIZE * IMAGE_SIZE];
                    }
                    inputArray = _f0054;

                    
                    BitmapToFloatArrayInPlace(frame, inputArray, IMAGE_SIZE);
                }

                
                /// this needs to be revised !!!!! - taylor
                if (_f0053 == null || _f0053.Dimensions[2] != IMAGE_SIZE)
                {
                    _f0053 = new DenseTensor<float>(inputArray, new int[] { 1, 3, IMAGE_SIZE, IMAGE_SIZE });
                    _f0055 = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", _f0053) };
                }
                else
                {
                    
                    inputArray.AsSpan().CopyTo(_f0053.Buffer.Span);
                }

                if (_f003C == null) return null;
                using (_m004B("ModelInference"))
                {
                    results = _f003C.Run(_f0055, _f0036, _f003B);
                    outputTensor = results[0].AsTensor<float>();
                }

                if (outputTensor == null)
                {
                    Log(LogLevel.Error, "Model inference returned null output tensor.", true, 2000);
                    _m004A(frame);
                    return null;
                }

                
                float FovSize = (float)Dictionary.sliderSettings[_xB9D2._c12];
                float fovMinX = (IMAGE_SIZE - FovSize) / 2.0f;
                float fovMaxX = (IMAGE_SIZE + FovSize) / 2.0f;
                float fovMinY = (IMAGE_SIZE - FovSize) / 2.0f;
                float fovMaxY = (IMAGE_SIZE + FovSize) / 2.0f;

                
                List<Prediction> KDPredictions;
                using (_m004B("_m0049"))
                {
                    KDPredictions = _m0049(outputTensor, detectionBox, fovMinX, fovMaxX, fovMinY, fovMaxY);
                }

                if (KDPredictions.Count == 0)
                {
                    _m004A(frame);
                    return null;
                }

                
                Prediction? bestCandidate = null;
                double bestDistSq = double.MaxValue;
                double center = IMAGE_SIZE / 2.0;

                
                
                
                using (_m004B("LinearSearch"))
                {
                    foreach (var p in KDPredictions)
                    {
                        var dx = p._f004B * IMAGE_SIZE - center;
                        var dy = p._f004C * IMAGE_SIZE - center;
                        double d2 = dx * dx + dy * dy; 

                        if (d2 < bestDistSq) { bestDistSq = d2; bestCandidate = p; }
                    }
                }

                Prediction? finalTarget = _m0041(bestCandidate, KDPredictions);
                if (finalTarget != null)
                {
                    _m0048(finalTarget, detectionBox);
                    _m004A(frame, finalTarget);
                    return finalTarget;
                }

                return null;
            }
            finally
            {
                
                frame.Dispose();
                results?.Dispose();
            }
        }

        private Prediction? _m0041(Prediction? bestCandidate, List<Prediction> KDPredictions)
        {
            if (!_xB9D2._opP()) { _dK8 = (byte)(_dK6 & 0xFF); return null; }
            if (!Dictionary.toggleState[_xB9D2._c05])
            {
                _f0040 = bestCandidate;
                _m0047();
                return bestCandidate;
            }

            
            if (bestCandidate == null || KDPredictions == null || KDPredictions.Count == 0)
            {
                return _m0044();
            }

            _f0041 = 0;

            
            float screenCenterX = IMAGE_SIZE / 2f;
            float screenCenterY = IMAGE_SIZE / 2f;

            
            Prediction? aimTarget = null;
            float nearestToCrosshairDistSq = float.MaxValue;

            foreach (var candidate in KDPredictions)
            {
                float distSq = _m0042(candidate.ScreenCenterX, candidate.ScreenCenterY, screenCenterX, screenCenterY);
                if (distSq < nearestToCrosshairDistSq)
                {
                    nearestToCrosshairDistSq = distSq;
                    aimTarget = candidate;
                }
            }

            if (aimTarget == null)
            {
                return _m0044();
            }

            
            if (_f0040 == null)
            {
                return _m0045(aimTarget);
            }

            
            float lastX = _f0040.ScreenCenterX;
            float lastY = _f0040.ScreenCenterY;
            float targetArea = _f0040.Rectangle.Width * _f0040.Rectangle.Height;
            float targetSize = MathF.Sqrt(targetArea);
            float sizeFactor = _m0043(targetArea);

            
            float aimToCurrentDistSq = _m0042(aimTarget.ScreenCenterX, aimTarget.ScreenCenterY, lastX, lastY);

            
            float trackingRadius = targetSize * 3f;
            float trackingRadiusSq = trackingRadius * trackingRadius;

            
            float aimTargetArea = aimTarget.Rectangle.Width * aimTarget.Rectangle.Height;
            float sizeRatio = MathF.Min(targetArea, aimTargetArea) / MathF.Max(targetArea, aimTargetArea);

            
            
            bool isSameTarget = (aimToCurrentDistSq < trackingRadiusSq) && (sizeRatio > 0.5f);

            if (isSameTarget)
            {
                
                _f004A = 0;
                _m0046(aimTarget, sizeFactor);
                _f0045 = Math.Min(_f0048, _f0045 + _f0047);
                _f0040 = aimTarget;
                return aimTarget;
            }

            
            
            _f004A++;

            
            float stickyThreshold = (float)Dictionary.sliderSettings[_xB9D2._c15];
            bool aimTargetVeryCentered = nearestToCrosshairDistSq < (stickyThreshold * stickyThreshold * 0.25f);

            if (aimTargetVeryCentered || _f004A >= 3)
            {
                
                return _m0045(aimTarget);
            }

            
            
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float _m0042(float x1, float y1, float x2, float y2)
        {
            float dx = x1 - x2;
            float dy = y1 - y2;
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// Returns a scaling factor based on target size. Smaller targets (further away) get higher factors
        /// to make thresholds more forgiving and filtering more aggressive.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float _m0043(float targetArea)
        {
            
            
            float ratio = _f0049 / Math.Max(targetArea, 100f);
            return Math.Clamp(ratio, 1.0f, 3.0f);
        }

        private Prediction? _m0044()
        {
            if (_f0040 != null && ++_f0041 <= _f0042)
            {
                
                _f0045 *= _f0046;

                
                var predicted = new Prediction
                {
                    ScreenCenterX = _f0040.ScreenCenterX + _f0043 * _f0041,
                    ScreenCenterY = _f0040.ScreenCenterY + _f0044 * _f0041,
                    Rectangle = _f0040.Rectangle,
                    Confidence = _f0040.Confidence * (1f - _f0041 * 0.2f),
                    ClassId = _f0040.ClassId,
                    ClassName = _f0040.ClassName,
                    _f004B = _f0040._f004B,
                    _f004C = _f0040._f004C
                };
                return predicted;
            }

            _m0047();
            return null;
        }

        private Prediction _m0045(Prediction target)
        {
            _f0043 = 0f;
            _f0044 = 0f;
            _f0045 = _f0047; 
            _f004A = 0;
            _f0040 = target;
            return target;
        }

        private void _m0046(Prediction newTarget, float sizeFactor)
        {
            if (_f0040 != null)
            {
                
                
                
                float smoothing = Math.Clamp(0.6f + (sizeFactor * 0.1f), 0.7f, 0.9f);
                float newWeight = 1f - smoothing;

                float newVelX = newTarget.ScreenCenterX - _f0040.ScreenCenterX;
                float newVelY = newTarget.ScreenCenterY - _f0040.ScreenCenterY;
                _f0043 = _f0043 * smoothing + newVelX * newWeight;
                _f0044 = _f0044 * smoothing + newVelY * newWeight;
            }
        }

        private void _m0047()
        {
            _f0040 = null;
            _f0041 = 0;
            _f004A = 0;
            _f0043 = 0f;
            _f0044 = 0f;
            _f0045 = 0f;
        }

        private void _m0048(Prediction target, Rectangle detectionBox)
        {
            float translatedXMin = target.Rectangle.X + detectionBox.Left;
            float translatedYMin = target.Rectangle.Y + detectionBox.Top;
            _f0037 = new(translatedXMin, translatedYMin,
                target.Rectangle.Width, target.Rectangle.Height);

            _f004B = target._f004B;
            _f004C = target._f004C;
        }
        
        private List<Prediction> _m0049(
            Tensor<float> outputTensor,
            Rectangle detectionBox,
            float fovMinX, float fovMaxX, float fovMinY, float fovMaxY)
        {
            float minConfidence = (float)Dictionary.sliderSettings[_xB9D2._c1A] / 100.0f;
            string selectedClass = Dictionary.dropdownState[_xB9D2._c25];
            int selectedClassId = selectedClass == _xB9D2._c39 ? -1 : _f0033.FirstOrDefault(c => c.Value == selectedClass).Key;

            
            
            var KDpredictions = new List<Prediction>(NUM_DETECTIONS);

            for (int i = 0; i < NUM_DETECTIONS; i++)
            {
                float x_center = outputTensor[0, 0, i];
                float y_center = outputTensor[0, 1, i];
                float width = outputTensor[0, 2, i];
                float height = outputTensor[0, 3, i];

                int bestClassId = 0;
                float bestConfidence = 0f;

                if (NUM_CLASSES == 1)
                {
                    bestConfidence = outputTensor[0, 4, i];
                }
                else
                {
                    if (selectedClassId == -1)
                    {
                        for (int classId = 0; classId < NUM_CLASSES; classId++)
                        {
                            float classConfidence = outputTensor[0, 4 + classId, i];
                            if (classConfidence > bestConfidence)
                            {
                                bestConfidence = classConfidence;
                                bestClassId = classId;
                            }
                        }
                    }
                    else
                    {
                        bestConfidence = outputTensor[0, 4 + selectedClassId, i];
                        bestClassId = selectedClassId;
                    }
                }

                if (bestConfidence < minConfidence) continue;

                float x_min = x_center - width / 2;
                float y_min = y_center - height / 2;
                float x_max = x_center + width / 2;
                float y_max = y_center + height / 2;

                if (x_min < fovMinX || x_max > fovMaxX || y_min < fovMinY || y_max > fovMaxY) continue;

                RectangleF rect = new(x_min, y_min, width, height);
                Prediction prediction = new()
                {
                    Rectangle = rect,
                    Confidence = bestConfidence,
                    ClassId = bestClassId,
                    ClassName = _f0033.GetValueOrDefault(bestClassId, $"Class_{bestClassId}"),
                    _f004B = x_center / IMAGE_SIZE,
                    _f004C = y_center / IMAGE_SIZE,
                    ScreenCenterX = detectionBox.Left + x_center,
                    ScreenCenterY = detectionBox.Top + y_center
                };

                
                KDpredictions.Add(prediction);
            }

            return KDpredictions;
        }

        private void _m004A(Bitmap frame, Prediction? DoLabel = null)
        {
            
            if (!Dictionary.toggleState[_xB9D2._c0E]) return;

            
            if (Dictionary.toggleState[_xB9D2._c03] && !Dictionary.toggleState[_xB9D2._c0F]) return;

            
            if ((DateTime.Now - _f0035).TotalMilliseconds < _f0034) return;

            try
            {
                
                if (frame == null) return;

                
                int width = frame.Width;
                int height = frame.Height;
                if (width <= 0 || height <= 0) return;

                _f0035 = DateTime.Now;
                string uuid = Guid.NewGuid().ToString();
                string imagePath = Path.Combine("bin", "images", $"{uuid}.jpg");

                
                frame.Save(imagePath, ImageFormat.Jpeg);

                if (Dictionary.toggleState[_xB9D2._c0F] && DoLabel != null)
                {
                    var labelPath = Path.Combine("bin", "labels", $"{uuid}.txt");

                    float x = (DoLabel!.Rectangle.X + DoLabel.Rectangle.Width / 2) / width;
                    float y = (DoLabel!.Rectangle.Y + DoLabel.Rectangle.Height / 2) / height;
                    float labelWidth = DoLabel.Rectangle.Width / width;
                    float labelHeight = DoLabel.Rectangle.Height / height;

                    File.WriteAllText(labelPath, $"{DoLabel.ClassId} {x} {y} {labelWidth} {labelHeight}");
                }
            }
            catch (ArgumentException)
            {
                
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"_m004A failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            
            lock (_f0031)
            {
                _f0032 = true;
            }

            
            _f003E = false;
            if (_f003D != null && _f003D.IsAlive)
            {
                if (!_f003D.Join(TimeSpan.FromSeconds(1)))
                {
                    try { _f003D.Interrupt(); }
                    catch { }
                }
            }

            
            PrintBenchmarks();

            
            _f0058.Dispose();

            
            _f0054 = null;
            _f0055 = null;
            _f003C?.Dispose();
            _f003B?.Dispose();
            _f003A = null;
        }
    }
    public class Prediction
    {
        public RectangleF Rectangle { get; set; }
        public float Confidence { get; set; }
        public int ClassId { get; set; } = 0;
        public string ClassName { get; set; } = "Enemy";
        public float _f004B { get; set; }
        public float _f004C { get; set; }
        public float ScreenCenterX { get; set; }  
        public float ScreenCenterY { get; set; }
    }
}