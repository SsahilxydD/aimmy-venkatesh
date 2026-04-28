using AILogic;
using Venkatesh2.Class;
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

namespace Venkatesh2.AILogic
{
    internal class AIManager : IDisposable
    {
        #region Variables

        private int _fI01;
        private readonly object _fL02 = new object();
        private volatile bool _fG02 = false;

        public void RequestSizeChange(int newSize)
        {
            lock (_fL02)
            {
                _fG02 = true;
            }
        }

        public int IMAGE_SIZE => _fI01;
        private int _fND03 { get; set; } = 8400;
        private bool _fDM04 { get; set; } = false;

        public static bool CurrentModelIsDynamic { get; private set; } = false;
        private int _fNC05 { get; set; } = 1;
        private Dictionary<int, string> _fC03 = new Dictionary<int, string>
        {
            { 0, _xB9D2._c33 }
        };
        public Dictionary<int, string> ModelClasses => _fC03;
        public static event Action<Dictionary<int, string>>? ClassesUpdated;
        public static event Action<int>? ImageSizeUpdated;
        public static event Action<bool>? DynamicModelStatusChanged;

        private const int _fK18 = 500;

        private long _fS04 = 0;
        private List<string>? _fN06;
        private RectangleF _fD05x;

        private int ScreenWidth => DisplayManager.ScreenWidth;
        private int ScreenHeight => DisplayManager.ScreenHeight;
        private int ScreenLeft => DisplayManager.ScreenLeft;
        private int ScreenTop => DisplayManager.ScreenTop;

        private readonly RunOptions? _fO0D;
        private InferenceSession? _fX0C;

        private CancellationTokenSource? _fC0F;
        private Task? _fT10;

        private RectangleF _fR05;
        private bool _fH06;

        private Prediction? _fT07 = null;
        private int _fW08 = 0;
        private int _fA0E = 0;

        private float _fVX09 = 0f;
        private float _fVY0A = 0f;
        private float _fAX0B = 0f;
        private float _fAY0C = 0f;
        private const float _fR19 = 10000f;

        private int _fDX { get; set; }
        private int _fDY { get; set; }

        public double AIConf = 0;
        private static int _fTX, _fTY;

        private float _scaleX => ScreenWidth / (float)IMAGE_SIZE;
        private float _scaleY => ScreenWidth / (float)IMAGE_SIZE;

        private DenseTensor<float>? _fR13;
        private float[]? _fR14;
        private List<NamedOnnxValue>? _fR15;

        private readonly List<Prediction> _fK16 = new(32);

        private readonly Prediction _fG17 = new();

        private bool _fA23;
        private bool _fA24;
        private bool _fA25;
        private bool _fA26;
        private bool _fA27;
        private bool _fA28;
        private bool _fA29;
        private bool _fA2A;
        private bool _fA2B;
        private float _fA2C;
        private float _fA2D;
        private double _fA2E;
        private double _fA2F;
        private double _fA30;
        private double _fA31;
        private double _fA32;
        private string _fA33 = _xB9D2._c17;
        private System.Drawing.Point _fA34;

        private string _fT35 = "";
        private int _fT36 = -1;

        private void _mR01()
        {
            uint _jv = unchecked((uint)(Environment.TickCount ^ 0xA5F3)) & 0u; _ = _jv;
            _fA23           = Convert.ToBoolean(Dictionary.toggleState[_xB9D2._c01]);
            _fA24  = Convert.ToBoolean(Dictionary.toggleState[_xB9D2._c02]);
            _fA25  = Convert.ToBoolean(Dictionary.toggleState[_xB9D2._c03]);
            _fA26           = Convert.ToBoolean(Dictionary.toggleState[_xB9D2._c04]);
            _fA27            = Convert.ToBoolean(Dictionary.toggleState[_xB9D2._c05]);
            _fA28            = Convert.ToBoolean(Dictionary.toggleState[_xB9D2._c06]);
            _fA29         = Convert.ToBoolean(Dictionary.toggleState[_xB9D2._c07]);
            _fA2A          = Convert.ToBoolean(Dictionary.toggleState[_xB9D2._c08]);
            _fA2C             = Convert.ToSingle(Dictionary.sliderSettings[_xB9D2._c09]);
            _fA2D       = Convert.ToSingle(Dictionary.sliderSettings[_xB9D2._c0A]) / 100.0f;
            _fA2E             = Convert.ToDouble(Dictionary.sliderSettings[_xB9D2._c0B]);
            _fA2F             = Convert.ToDouble(Dictionary.sliderSettings[_xB9D2._c0C]);
            _fA30          = Convert.ToDouble(Dictionary.sliderSettings[_xB9D2._c0D]);
            _fA31          = Convert.ToDouble(Dictionary.sliderSettings[_xB9D2._c0E]);
            _fA32     = Convert.ToDouble(Dictionary.sliderSettings[_xB9D2._c0F]);
            _fA33     = Convert.ToString(Dictionary.dropdownState[_xB9D2._c10]) ?? _xB9D2._c17;
            _fA2B      = Convert.ToString(Dictionary.dropdownState[_xB9D2._c11]) == _xB9D2._c12;

            _fA34 = WinAPICaller.GetCursorPosition();

            string tc = Convert.ToString(Dictionary.dropdownState[_xB9D2._c13]) ?? _xB9D2._c14;
            if (tc != _fT35)
            {
                _fT35 = tc;
                _fT36  = tc == _xB9D2._c14 ? -1
                    : _fC03.FirstOrDefault(c => c.Value == tc).Key;
            }
        }

        private readonly CaptureManager _fP3B = new();
        #endregion Variables

        private static bool _opP()
        {
            int _t = Environment.TickCount;
            return (_t | (~_t)) == -1;
        }

        public AIManager(string modelPath)
        {
            _fI01 = int.Parse(Dictionary.dropdownState[_xB9D2._c19]);

            if (Dictionary.dropdownState[_xB9D2._c1A] == _xB9D2._c2C)
            {
                _fP3B._mI03();
            }

            _fO0D = new RunOptions();

            Task.Run(() => _mI05(modelPath));
        }

        #region Models

        private static SessionOptions _mB03()
        {
            return new SessionOptions
            {
                EnableCpuMemArena = true,
                EnableMemoryPattern = false,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                InterOpNumThreads = 1,
                IntraOpNumThreads = 4
            };
        }

        private static string _mF06(string modelPath)
        {
            string dir  = Path.GetDirectoryName(modelPath) ?? "";
            string stem = Path.GetFileNameWithoutExtension(modelPath);
            string fp16 = Path.Combine(dir, stem + "_fp16.onnx");
            return File.Exists(fp16) ? fp16 : modelPath;
        }

        private async Task _mI05(string modelPath)
        {
            string originalPath = modelPath;
            string resolved = _mF06(modelPath);
            bool usingFp16 = !string.Equals(resolved, originalPath, StringComparison.OrdinalIgnoreCase);

            if (usingFp16)
                Log(LogLevel.Info, $"FP16 model found — using {Path.GetFileName(resolved)} for faster inference.");

            if (await _mT06(resolved, useDirectML: true))
            { FileManager.CurrentlyLoadingModel = false; return; }

            if (await _mT06(resolved, useDirectML: false))
            { FileManager.CurrentlyLoadingModel = false; return; }

            if (usingFp16)
            {
                Log(LogLevel.Warning, "FP16 model failed on all providers — retrying with original FP32 model.", true);
                if (await _mT06(originalPath, useDirectML: true))
                { FileManager.CurrentlyLoadingModel = false; return; }

                if (await _mT06(originalPath, useDirectML: false))
                { FileManager.CurrentlyLoadingModel = false; return; }
            }

            Log(LogLevel.Error, "All model loading attempts failed. Aim assist will not work.", true);
            FileManager.CurrentlyLoadingModel = false;
        }

        private async Task<bool> _mT06(string modelPath, bool useDirectML)
        {
            try
            {
                await _mL07(_mB03(), modelPath, useDirectML);
                return _fT10 != null;
            }
            catch (Exception ex)
            {
                string ep = useDirectML ? "DirectML" : "CPU";
                Log(LogLevel.Error, $"Model load via {ep} failed: {ex.Message}");
                return false;
            }
        }

        private static void _mD04(SessionOptions sessionOptions)
        {
            sessionOptions.AppendExecutionProvider_DML(0);
        }

        private Task _mL07(SessionOptions sessionOptions, string modelPath, bool useDirectML)
        {
            try
            {
                if (useDirectML)
                {
                    _mD04(sessionOptions);
                }
                else
                {
                    sessionOptions.AppendExecutionProvider_CPU();
                }

                _fX0C = new InferenceSession(modelPath, sessionOptions);
                _fN06 = new List<string>(_fX0C.OutputMetadata.Keys);

                if (!_mV08())
                {
                    _fX0C?.Dispose();
                    return Task.CompletedTask;
                }

            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error loading the model: {ex.Message}", true);
                _fX0C?.Dispose();
                return Task.CompletedTask;
            }

            _fC0F = new CancellationTokenSource();
            _fT10 = Task.Run(() => _mL0C(_fC0F.Token));
            return Task.CompletedTask;
        }

        private bool _mV08()
        {
            if (_fX0C != null)
            {
                var inputMetadata = _fX0C.InputMetadata;
                var outputMetadata = _fX0C.OutputMetadata;

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

                _fDM04 = isDynamic;
                CurrentModelIsDynamic = isDynamic;

                if (_fDM04)
                {
                    _fND03 = _mN05(IMAGE_SIZE);
                    _mC08x();
                    ImageSizeUpdated?.Invoke(IMAGE_SIZE);
                    Log(LogLevel.Info, $"Loaded dynamic model - using selected image size {IMAGE_SIZE}x{IMAGE_SIZE} with {_fND03} detections", true, 3000);
                }
                else
                {
                    var supportedSizes = new[] { "640", "512", "416", "320", "256", "160" };
                    var fixedSizeStr = fixedInputSize.ToString();

                    if (!supportedSizes.Contains(fixedSizeStr))
                    {
                        Log(LogLevel.Error,
                            $"Model requires unsupported size {fixedInputSize}x{fixedInputSize}. Supported sizes are: {string.Join(", ", supportedSizes)}",
                            true, 10000);
                        return false;
                    }

                    _fND03 = _mN05(fixedInputSize);
                    _fI01 = fixedInputSize;

                    if (fixedInputSize != int.Parse(Dictionary.dropdownState[_xB9D2._c19]))
                    {
                        Log(LogLevel.Warning,
                            $"Fixed-size model expects {fixedInputSize}x{fixedInputSize}. Automatically adjusting Image Size setting.",
                            true, 3000);

                        Dictionary.dropdownState[_xB9D2._c19] = fixedSizeStr;

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
                    _mC08x();

                    var expectedShape = new int[] { 1, 4 + _fNC05, _fND03 };
                    if (!outputMetadata.Values.All(metadata => metadata.Dimensions.SequenceEqual(expectedShape)))
                    {
                        Log(LogLevel.Error,
                            $"Output shape does not match the expected shape of {string.Join("x", expectedShape)}.\nThis model will not work with Venkatesh, please use an YOLOv8 model converted to ONNXv8.",
                            true, 10000);
                        return false;
                    }

                    Log(LogLevel.Info, $"Loaded fixed-size model: {fixedInputSize}x{fixedInputSize}", true, 2000);
                }

                DynamicModelStatusChanged?.Invoke(_fDM04);

                return true;
            }

            return false;
        }

        private void _mC08x()
        {
            if (_fX0C == null) return;
            _fC03.Clear();

            try
            {
                var metadata = _fX0C.ModelMetadata;

                if (metadata != null &&
                    metadata.CustomMetadataMap.TryGetValue("names", out string? value) &&
                    !string.IsNullOrEmpty(value))
                {
                    JObject data = JObject.Parse(value);
                    if (data != null && data.Type == JTokenType.Object)
                    {
                        foreach (var item in data)
                        {
                            if (int.TryParse(item.Key, out int classId) && item.Value?.Type == JTokenType.String)
                            {
                                _fC03[classId] = item.Value.ToString();
                            }
                        }
                        _fNC05 = _fC03.Count > 0 ? _fC03.Keys.Max() + 1 : 1;
                        Log(LogLevel.Info, $"Loaded {_fC03.Count} class(es) from model metadata: {data.ToString(Newtonsoft.Json.Formatting.None)}", false);
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
                ClassesUpdated?.Invoke(new Dictionary<int, string>(_fC03));
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error loading classes: {ex.Message}", true);
            }
        }

        #endregion Models

        #region AI

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool _mP09() =>
            _fA24 ||
            _fA25 ||
            InputBindingManager.IsHoldingBinding(_xB9D2._c15) ||
            InputBindingManager.IsHoldingBinding(_xB9D2._c16);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool _mS0A() =>
            _fA23 ||
            _fA24;

        private const double TARGET_FRAME_MS = 1000.0 / 144.0;
        private const int IDLE_DELAY_MS = 33;

        private async Task _mL0C(CancellationToken ct)
        {
            bool _op = _opP();
            uint _jv = unchecked((uint)(ct.GetHashCode() ^ 0xDEAD)) & 0u; _ = _jv;
            Stopwatch frameTimer = new();

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    lock (_fL02)
                    {
                        if (_fG02)
                        {
                            Task.Delay(5, ct).Wait(ct);
                            continue;
                        }
                    }

                    frameTimer.Restart();
                    _mR01();
                    _fP3B._mH02();
                    _mF0B();

                    if (!_mS0A())
                    {
                        await Task.Delay(IDLE_DELAY_MS, ct);
                        continue;
                    }

                    if (!_mP09())
                    {
                        await Task.Delay(IDLE_DELAY_MS, ct);
                        continue;
                    }

                    Prediction? closestPrediction = _mG0B();
                    DetectedPlayerWindow? DetectedPlayerOverlay = Dictionary.DetectedPlayerOverlay;

                    if (closestPrediction == null)
                    {
                        _mD07(DetectedPlayerOverlay);
                    }
                    else
                    {
                        _mC09(DetectedPlayerOverlay, closestPrediction, _scaleX, _scaleY);
                        _mH0A2(closestPrediction);
                    }

                    double elapsed = frameTimer.Elapsed.TotalMilliseconds;
                    int remaining = (int)(TARGET_FRAME_MS - elapsed);
                    if (remaining > 0)
                        await Task.Delay(remaining, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Error, $"_mL0C frame error: {ex.GetType().Name}: {ex.Message}");
                    try { await Task.Delay(IDLE_DELAY_MS, ct); } catch { break; }
                }
            }
        }

        #region AI Loop Functions

        private void _mF0B()
        {
            if (!_fA2B || !_fA2A) return;
            if (Dictionary.FOVWindow == null) return;

            var mousePosition = _fA34;
            if (!DisplayManager.IsPointInCurrentDisplay(new System.Windows.Point(mousePosition.X, mousePosition.Y)))
                return;

            var displayRelativeX = mousePosition.X - DisplayManager.ScreenLeft;
            var displayRelativeY = mousePosition.Y - DisplayManager.ScreenTop;

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (Dictionary.FOVWindow == null) return;
                Dictionary.FOVWindow.FOVStrictEnclosure.Margin = new Thickness(
                    Convert.ToInt16(displayRelativeX / WinAPICaller.scalingFactorX) - 320,
                    Convert.ToInt16(displayRelativeY / WinAPICaller.scalingFactorY) - 320, 0, 0);
            });
        }

        private static void _mD07(DetectedPlayerWindow? DetectedPlayerOverlay)
        {
            if (DetectedPlayerOverlay == null) return;
            if (!Convert.ToBoolean(Dictionary.toggleState[_xB9D2._c02])) return;

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (Convert.ToBoolean(Dictionary.toggleState[_xB9D2._c2E]))
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Opacity = 0;

                if (Convert.ToBoolean(Dictionary.toggleState[_xB9D2._c2F]))
                    DetectedPlayerOverlay.DetectedTracers.Opacity = 0;

                DetectedPlayerOverlay.DetectedPlayerFocus.Opacity = 0;
            });
        }

        private void _mO08(DetectedPlayerWindow? DetectedPlayerOverlay, Prediction closestPrediction)
        {
            var scalingFactorX = WinAPICaller.scalingFactorX;
            var scalingFactorY = WinAPICaller.scalingFactorY;

            var displayRelativeX = _fD05x.X - DisplayManager.ScreenLeft;
            var displayRelativeY = _fD05x.Y - DisplayManager.ScreenTop;

            var centerX = Convert.ToInt16(displayRelativeX / scalingFactorX) + (_fD05x.Width / 2.0);
            var centerY = Convert.ToInt16(displayRelativeY / scalingFactorY);

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (DetectedPlayerOverlay == null) return;
                if (Convert.ToBoolean(Dictionary.toggleState[_xB9D2._c2E]))
                {
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Opacity = 1;
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Content = $"{closestPrediction.ClassName}: {Math.Round((AIConf * 100), 2)}%";

                    var labelEstimatedHalfWidth = DetectedPlayerOverlay.DetectedPlayerConfidence.ActualWidth / 2.0;
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Margin = new Thickness(
                        centerX - labelEstimatedHalfWidth,
                        centerY - DetectedPlayerOverlay.DetectedPlayerConfidence.ActualHeight - 2, 0, 0);
                }
                bool showTracers = Convert.ToBoolean(Dictionary.toggleState[_xB9D2._c2F]);
                DetectedPlayerOverlay.DetectedTracers.Opacity = showTracers ? 1 : 0;
                if (showTracers)
                {
                    string tracerPosition = Convert.ToString(Dictionary.dropdownState[_xB9D2._c30]) ?? _xB9D2._c18;

                    var boxTop = centerY;
                    var boxBottom = centerY + _fD05x.Height;
                    var boxHorizontalCenter = centerX;
                    var boxVerticalCenter = centerY + (_fD05x.Height / 2.0);
                    var boxLeft = centerX - (_fD05x.Width / 2.0);
                    var boxRight = centerX + (_fD05x.Width / 2.0);

                    if (tracerPosition == "Top")
                    {
                        DetectedPlayerOverlay.DetectedTracers.X2 = boxHorizontalCenter;
                        DetectedPlayerOverlay.DetectedTracers.Y2 = boxTop;
                    }
                    else if (tracerPosition == _xB9D2._c18)
                    {
                        DetectedPlayerOverlay.DetectedTracers.X2 = boxHorizontalCenter;
                        DetectedPlayerOverlay.DetectedTracers.Y2 = boxBottom;
                    }
                    else if (tracerPosition == "Middle")
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

                DetectedPlayerOverlay.Opacity = Convert.ToDouble(Dictionary.sliderSettings[_xB9D2._c31]);

                DetectedPlayerOverlay.DetectedPlayerFocus.Opacity = 1;
                DetectedPlayerOverlay.DetectedPlayerFocus.Margin = new Thickness(
                    centerX - (_fD05x.Width / 2.0), centerY, 0, 0);
                DetectedPlayerOverlay.DetectedPlayerFocus.Width = _fD05x.Width;
                DetectedPlayerOverlay.DetectedPlayerFocus.Height = _fD05x.Height;
            });
        }

        private void _mC09(DetectedPlayerWindow? DetectedPlayerOverlay, Prediction closestPrediction, float scaleX, float scaleY)
        {
            AIConf = closestPrediction.Confidence;

            if (_fA24 && DetectedPlayerOverlay != null)
            {
                _mO08(DetectedPlayerOverlay, closestPrediction);
                if (!_fA23) return;
            }

            var rect = closestPrediction.Rectangle;

            _fDX = _fA27
                ? (int)((rect.X + rect.Width * (_fA31 / 100)) * scaleX)
                : (int)((rect.X + rect.Width / 2) * scaleX + _fA2F);

            _fDY = _fA28
                ? (int)((rect.Y + rect.Height - rect.Height * (_fA30 / 100)) * scaleY + _fA2E)
                : _mY0A(scaleY, _fA2E, closestPrediction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int _mY0A(float scaleY, double yOffset, Prediction closestPrediction)
        {
            var rect = closestPrediction.Rectangle;
            float yAdjustment = _fA33 == _xB9D2._c17 ? rect.Height / 2
                              : _fA33 == _xB9D2._c18 ? rect.Height
                              : 0f;
            return (int)((rect.Y + yAdjustment) * scaleY + yOffset);
        }

        private void _mH0A2(Prediction closestPrediction)
        {
            if (_fA23 &&
                (_fA25 ||
                 InputBindingManager.IsHoldingBinding(_xB9D2._c15) ||
                 InputBindingManager.IsHoldingBinding(_xB9D2._c16)))
            {
                MouseManager.MoveCrosshair(_fDX, _fDY);
            }
        }

        private Prediction? _mG0B()
        {
            uint _jv = unchecked((uint)(_fI01 ^ 0xBEEF)) & 0u; _ = _jv;
            if (_fA2B)
            {
                var mousePos = _fA34;
                if (DisplayManager.IsPointInCurrentDisplay(new System.Windows.Point(mousePos.X, mousePos.Y)))
                {
                    _fTX = mousePos.X;
                    _fTY = mousePos.Y;
                }
                else
                {
                    _fTX = DisplayManager.ScreenLeft + (DisplayManager.ScreenWidth / 2);
                    _fTY = DisplayManager.ScreenTop + (DisplayManager.ScreenHeight / 2);
                }
            }
            else
            {
                _fTX = DisplayManager.ScreenLeft + (DisplayManager.ScreenWidth / 2);
                _fTY = DisplayManager.ScreenTop + (DisplayManager.ScreenHeight / 2);
            }

            Rectangle detectionBox = new(_fTX - IMAGE_SIZE / 2, _fTY - IMAGE_SIZE / 2, IMAGE_SIZE, IMAGE_SIZE);

            IDisposableReadOnlyCollection<DisposableNamedOnnxValue>? results = null;
            Tensor<float>? outputTensor = null;

            try
            {
                if (_fR14 == null
                    || _fR14.Length != 3 * IMAGE_SIZE * IMAGE_SIZE
                    || _fR13 == null
                    || _fR13.Dimensions[2] != IMAGE_SIZE)
                {
                    _fR14 = new float[3 * IMAGE_SIZE * IMAGE_SIZE];
                    _fR13 = new DenseTensor<float>(_fR14, new int[] { 1, 3, IMAGE_SIZE, IMAGE_SIZE });
                    _fR15 = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", _fR13) };
                }

                Bitmap? frame = _fP3B._mC06(detectionBox, _fR14!, IMAGE_SIZE, _fA29);

                if (_fX0C == null) return null;
                results = _fX0C.Run(_fR15, _fN06, _fO0D);
                outputTensor = results[0].AsTensor<float>();

                if (outputTensor == null)
                {
                    Log(LogLevel.Error, "Model inference returned null output tensor.", true, 2000);
                    _mS14(frame);
                    return null;
                }

                float fovMinX, fovMaxX, fovMinY, fovMaxY;
                if (_fA2A)
                {
                    fovMinX = (IMAGE_SIZE - _fA2C) / 2.0f;
                    fovMaxX = (IMAGE_SIZE + _fA2C) / 2.0f;
                }
                else
                {
                    fovMinX = -IMAGE_SIZE;
                    fovMaxX = IMAGE_SIZE * 2f;
                }
                fovMinY = fovMinX;
                fovMaxY = fovMaxX;

                List<Prediction> KDPredictions = _mK12(outputTensor, detectionBox, fovMinX, fovMaxX, fovMinY, fovMaxY);

                if (KDPredictions.Count == 0)
                {
                    _mS14(frame);
                    if (_fA26 && _fT07 != null)
                    {
                        Prediction? graceTarget = _mN0F();
                        if (graceTarget != null)
                            _mU11(graceTarget, detectionBox);
                        return graceTarget;
                    }
                    return null;
                }

                Prediction? bestCandidate = null;
                double bestDistSq = double.MaxValue;
                double center = IMAGE_SIZE / 2.0;

                foreach (var p in KDPredictions)
                {
                    var dx = p.CenterXTranslated * IMAGE_SIZE - center;
                    var dy = p.CenterYTranslated * IMAGE_SIZE - center;
                    double d2 = dx * dx + dy * dy;

                    if (d2 < bestDistSq) { bestDistSq = d2; bestCandidate = p; }
                }

                if (bestCandidate != null)
                {
                    double tieZone = bestCandidate.Rectangle.Width * bestCandidate.Rectangle.Height;
                    foreach (var p in KDPredictions)
                    {
                        if (p == bestCandidate) continue;
                        var dx = p.CenterXTranslated * IMAGE_SIZE - center;
                        var dy = p.CenterYTranslated * IMAGE_SIZE - center;
                        double d2 = dx * dx + dy * dy;
                        if (d2 - bestDistSq < tieZone && p.Confidence > bestCandidate.Confidence)
                            bestCandidate = p;
                    }
                }

                Prediction? finalTarget = _mS0C(bestCandidate, KDPredictions);
                if (finalTarget != null)
                {
                    _mU11(finalTarget, detectionBox);
                    _mS14(frame, finalTarget);
                    return finalTarget;
                }

                return null;
            }
            finally
            {
                results?.Dispose();
            }
        }

        private Prediction? _mS0C(Prediction? bestCandidate, List<Prediction> KDPredictions)
        {
            uint _jv = unchecked((uint)((_fA0E + 1) ^ 0xCAFE)) & 0u; _ = _jv;
            if (!_fA26)
            {
                _mX10();

                if (_fH06 && bestCandidate != null && KDPredictions != null)
                {
                    Prediction? continued = null;
                    float contIoU = 0f;
                    foreach (var c in KDPredictions)
                    {
                        float iou = _mI0D(c.Rectangle, _fR05);
                        if (iou > contIoU) { contIoU = iou; continued = c; }
                    }

                    if (continued != null && contIoU >= 0.15f && continued != bestCandidate)
                    {
                        float half = IMAGE_SIZE / 2f;
                        float bx = bestCandidate.CenterXTranslated * IMAGE_SIZE - half;
                        float by = bestCandidate.CenterYTranslated * IMAGE_SIZE - half;
                        float bestDSq = bx * bx + by * by;

                        float lx = continued.CenterXTranslated * IMAGE_SIZE - half;
                        float ly = continued.CenterYTranslated * IMAGE_SIZE - half;
                        float contDSq = lx * lx + ly * ly;

                        float tieZone = continued.Rectangle.Width * continued.Rectangle.Height;
                        if (contDSq - bestDSq < tieZone)
                            bestCandidate = continued;
                    }
                }

                _fH06 = bestCandidate != null;
                if (bestCandidate != null)
                    _fR05 = bestCandidate.Rectangle;

                return bestCandidate;
            }

            if (bestCandidate == null || KDPredictions == null || KDPredictions.Count == 0)
                return _mN0F();

            if (_fT07 == null)
            {
                float half = IMAGE_SIZE / 2f;
                Prediction? nearest = null;
                float nearestDistSq = float.MaxValue;
                foreach (var candidate in KDPredictions)
                {
                    float dx = candidate.CenterXTranslated * IMAGE_SIZE - half;
                    float dy = candidate.CenterYTranslated * IMAGE_SIZE - half;
                    float dSq = dx * dx + dy * dy;
                    if (dSq < nearestDistSq) { nearestDistSq = dSq; nearest = candidate; }
                }
                if (nearest == null) return null;
                float tieZone = nearest.Rectangle.Width * nearest.Rectangle.Height;
                foreach (var candidate in KDPredictions)
                {
                    if (candidate == nearest) continue;
                    float dx = candidate.CenterXTranslated * IMAGE_SIZE - half;
                    float dy = candidate.CenterYTranslated * IMAGE_SIZE - half;
                    float dSq = dx * dx + dy * dy;
                    if (dSq - nearestDistSq < tieZone && candidate.Confidence > nearest.Confidence)
                        nearest = candidate;
                }
                return _mQ0E(nearest);
            }

            float lastX = _fT07.ScreenCenterX;
            float lastY = _fT07.ScreenCenterY;
            float targetArea = _fT07.Rectangle.Width * _fT07.Rectangle.Height;
            float targetSize = MathF.Sqrt(targetArea);
            float sizeFactor = _mZ0E2(targetArea);

            float maxRadius = IMAGE_SIZE * 0.5f;
            float trackingRadiusX = Math.Min(targetSize * 3f, maxRadius);
            float trackingRadiusY = Math.Min(targetSize * 4.5f, maxRadius * 1.5f);

            float velMagSq = _fVX09 * _fVX09 + _fVY0A * _fVY0A;
            float dt = _fW08 + 1f;
            float expectedX = lastX + _fVX09 * dt + 0.5f * _fAX0B * dt * dt;
            float expectedY = lastY + _fVY0A * dt + 0.5f * _fAY0C * dt * dt;

            var currentRect = _fT07.Rectangle;
            RectangleF extrapolatedBox = new(
                currentRect.X + _fVX09 * dt + 0.5f * _fAX0B * dt * dt,
                currentRect.Y + _fVY0A * dt + 0.5f * _fAY0C * dt * dt,
                currentRect.Width,
                currentRect.Height);

            float imageArea = IMAGE_SIZE * IMAGE_SIZE;
            float minSizeRatio = targetArea > imageArea * 0.15f ? 0.2f : 0.4f;

            Prediction? trackedMatch = null;
            float bestIoU = 0f;

            foreach (var candidate in KDPredictions)
            {
                float candidateArea = candidate.Rectangle.Width * candidate.Rectangle.Height;
                float sizeRatio = MathF.Min(targetArea, candidateArea) / MathF.Max(targetArea, candidateArea);
                if (sizeRatio < minSizeRatio) continue;

                float iou = _mI0D(candidate.Rectangle, extrapolatedBox);
                if (iou > bestIoU) { bestIoU = iou; trackedMatch = candidate; }
            }

            if (trackedMatch != null && bestIoU >= 0.15f)
            {
                _fA0E++;
                int missed = _fW08;
                _fW08 = 0;
                _mM0G(trackedMatch, sizeFactor, missed);
                _fT07 = trackedMatch;
                return trackedMatch;
            }

            trackedMatch = null;
            float bestProximitySq = float.MaxValue;

            foreach (var candidate in KDPredictions)
            {
                float candidateArea = candidate.Rectangle.Width * candidate.Rectangle.Height;
                float sizeRatio = MathF.Min(targetArea, candidateArea) / MathF.Max(targetArea, candidateArea);
                if (sizeRatio < minSizeRatio) continue;

                float distToExpectedSq = _mG0D(candidate.ScreenCenterX, candidate.ScreenCenterY, expectedX, expectedY);
                float distToLastSq = _mG0D(candidate.ScreenCenterX, candidate.ScreenCenterY, lastX, lastY);

                float ndxLast = (candidate.ScreenCenterX - lastX) / trackingRadiusX;
                float ndyLast = (candidate.ScreenCenterY - lastY) / trackingRadiusY;
                float ellipseDistLastSq = ndxLast * ndxLast + ndyLast * ndyLast;

                float ndxExp = (candidate.ScreenCenterX - expectedX) / trackingRadiusX;
                float ndyExp = (candidate.ScreenCenterY - expectedY) / trackingRadiusY;
                float ellipseDistExpSq = ndxExp * ndxExp + ndyExp * ndyExp;

                if (!(ellipseDistLastSq < 1f || ellipseDistExpSq < 1f))
                    continue;

                float proximityScore = velMagSq > 4f ? distToExpectedSq : distToLastSq;
                if (proximityScore < bestProximitySq)
                {
                    bestProximitySq = proximityScore;
                    trackedMatch = candidate;
                }
            }

            if (trackedMatch != null)
            {
                _fA0E++;
                int missed = _fW08;
                _fW08 = 0;
                _mM0G(trackedMatch, sizeFactor, missed);
                _fT07 = trackedMatch;
                return trackedMatch;
            }

            return _mN0F();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float _mG0D(float x1, float y1, float x2, float y2)
        {
            float dx = x1 - x2;
            float dy = y1 - y2;
            return dx * dx + dy * dy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float _mI0D(RectangleF a, RectangleF b)
        {
            float x1 = MathF.Max(a.X, b.X);
            float y1 = MathF.Max(a.Y, b.Y);
            float x2 = MathF.Min(a.X + a.Width, b.X + b.Width);
            float y2 = MathF.Min(a.Y + a.Height, b.Y + b.Height);

            if (x2 <= x1 || y2 <= y1) return 0f;

            float intersection = (x2 - x1) * (y2 - y1);
            float union = a.Width * a.Height + b.Width * b.Height - intersection;
            return union > 0f ? intersection / union : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float _mZ0E2(float targetArea)
        {
            float ratio = _fR19 / Math.Max(targetArea, 100f);
            return Math.Clamp(ratio, 1.0f, 3.0f);
        }

        private Prediction? _mN0F()
        {
            if (_fT07 != null)
            {
                float velMag = MathF.Sqrt(_fVX09 * _fVX09 + _fVY0A * _fVY0A);
                float lastArea = _fT07.Rectangle.Width * _fT07.Rectangle.Height;
                bool closeRange = lastArea > IMAGE_SIZE * IMAGE_SIZE * 0.15f;
                int ageBonus = Math.Min(_fA0E / 10, 3);
                int dynamicGrace = (velMag > 5f ? 6 : velMag > 2f ? 4 : closeRange ? 4 : 2) + ageBonus;

                if (++_fW08 <= dynamicGrace)
                {
                    float t = _fW08;
                    float decayRate = 1f / (dynamicGrace + 1);
                    float extraX = _fVX09 * t + 0.5f * _fAX0B * t * t;
                    float extraY = _fVY0A * t + 0.5f * _fAY0C * t * t;
                    var lastRect = _fT07.Rectangle;
                    _fG17.ScreenCenterX      = _fT07.ScreenCenterX + extraX;
                    _fG17.ScreenCenterY      = _fT07.ScreenCenterY + extraY;
                    _fG17.Rectangle          = new RectangleF(lastRect.X + extraX, lastRect.Y + extraY, lastRect.Width, lastRect.Height);
                    _fG17.Confidence         = _fT07.Confidence * (1f - t * decayRate);
                    _fG17.ClassId            = _fT07.ClassId;
                    _fG17.ClassName          = _fT07.ClassName;
                    _fG17.CenterXTranslated  = _fT07.CenterXTranslated;
                    _fG17.CenterYTranslated  = _fT07.CenterYTranslated;
                    return _fG17;
                }
            }

            _mX10();
            return null;
        }

        private Prediction _mQ0E(Prediction target)
        {
            _fVX09 = 0f;
            _fVY0A = 0f;
            _fAX0B = 0f;
            _fAY0C = 0f;
            _fA0E = 0;
            _fT07 = target;
            return target;
        }

        private void _mM0G(Prediction newTarget, float sizeFactor, int missedFrames = 0)
        {
            if (_fT07 == null) return;

            float elapsed = missedFrames + 1f;
            float newVelX = (newTarget.ScreenCenterX - _fT07.ScreenCenterX) / elapsed;
            float newVelY = (newTarget.ScreenCenterY - _fT07.ScreenCenterY) / elapsed;

            float newAccelX = newVelX - _fVX09;
            float newAccelY = newVelY - _fVY0A;

            float predX = _fT07.ScreenCenterX + _fVX09 * elapsed + 0.5f * _fAX0B * elapsed * elapsed;
            float predY = _fT07.ScreenCenterY + _fVY0A * elapsed + 0.5f * _fAY0C * elapsed * elapsed;
            float predErrorSq = _mG0D(newTarget.ScreenCenterX, newTarget.ScreenCenterY, predX, predY);
            float errorNorm = predErrorSq / Math.Max(MathF.Sqrt(_fT07.Rectangle.Width * _fT07.Rectangle.Height), 10f);

            float baseSmoothing = Math.Clamp(0.6f + (sizeFactor * 0.1f), 0.7f, 0.9f);
            float smoothingVel = errorNorm > 16f ? 0.3f : errorNorm > 4f ? 0.5f : baseSmoothing;
            float smoothingAccel = smoothingVel * 0.8f;

            _fVX09 = _fVX09 * smoothingVel + newVelX * (1f - smoothingVel);
            _fVY0A = _fVY0A * smoothingVel + newVelY * (1f - smoothingVel);
            _fAX0B = _fAX0B * smoothingAccel + newAccelX * (1f - smoothingAccel);
            _fAY0C = _fAY0C * smoothingAccel + newAccelY * (1f - smoothingAccel);
        }

        private void _mX10()
        {
            _fT07 = null;
            _fW08 = 0;
            _fA0E = 0;
            _fVX09 = 0f;
            _fVY0A = 0f;
            _fAX0B = 0f;
            _fAY0C = 0f;
        }

        private void _mU11(Prediction target, Rectangle detectionBox)
        {
            float translatedXMin = target.Rectangle.X + detectionBox.Left;
            float translatedYMin = target.Rectangle.Y + detectionBox.Top;
            _fD05x = new(translatedXMin, translatedYMin,
                target.Rectangle.Width, target.Rectangle.Height);
        }
        private List<Prediction> _mK12(
            Tensor<float> outputTensor,
            Rectangle detectionBox,
            float fovMinX, float fovMaxX, float fovMinY, float fovMaxY)
        {
            uint _jv = unchecked((uint)((int)fovMinX ^ (int)fovMaxY)) & 0u; _ = _jv;
            float minConfidence = _fA2D;
            int selectedClassId = _fT36;

            int nd = _fND03;
            int imageSize = IMAGE_SIZE;
            float invImageSize = 1.0f / imageSize;
            float fovCenterX = imageSize * 0.5f;
            float fovCenterY = imageSize * 0.5f;
            float fovRadius = (fovMaxX - fovMinX) * 0.5f;
            float fovRadiusSq = fovRadius * fovRadius;
            _fK16.Clear();
            var KDpredictions = _fK16;

            if (outputTensor is DenseTensor<float> dense)
            {
                ReadOnlySpan<float> span = dense.Buffer.Span;

                int xOff = 0;
                int yOff = nd;
                int wOff = 2 * nd;
                int hOff = 3 * nd;
                int clsOff = 4 * nd;
                int numClasses = _fNC05;

                for (int i = 0; i < nd; i++)
                {
                    int bestClassId = 0;
                    float bestConfidence;

                    if (numClasses == 1)
                    {
                        bestConfidence = span[clsOff + i];
                    }
                    else if (selectedClassId == -1)
                    {
                        bestConfidence = 0f;
                        int baseIdx = clsOff + i;
                        for (int classId = 0; classId < numClasses; classId++)
                        {
                            float classConfidence = span[baseIdx + classId * nd];
                            if (classConfidence > bestConfidence)
                            {
                                bestConfidence = classConfidence;
                                bestClassId = classId;
                            }
                        }
                    }
                    else
                    {
                        bestConfidence = span[clsOff + selectedClassId * nd + i];
                        bestClassId = selectedClassId;
                    }

                    if (bestConfidence < minConfidence) continue;

                    float x_center = span[xOff + i];
                    float y_center = span[yOff + i];
                    float width = span[wOff + i];
                    float height = span[hOff + i];

                    float halfW = width * 0.5f;
                    float halfH = height * 0.5f;
                    float x_min = x_center - halfW;
                    float y_min = y_center - halfH;
                    float x_max = x_center + halfW;
                    float y_max = y_center + halfH;

                    float fdx = x_center - fovCenterX;
                    float fdy = y_center - fovCenterY;
                    if (fdx * fdx + fdy * fdy > fovRadiusSq) continue;

                    KDpredictions.Add(new Prediction
                    {
                        Rectangle = new RectangleF(x_min, y_min, width, height),
                        Confidence = bestConfidence,
                        ClassId = bestClassId,
                        ClassName = _fC03.GetValueOrDefault(bestClassId, $"Class_{bestClassId}"),
                        CenterXTranslated = x_center * invImageSize,
                        CenterYTranslated = y_center * invImageSize,
                        ScreenCenterX = detectionBox.Left + x_center,
                        ScreenCenterY = detectionBox.Top + y_center
                    });
                }

                _mA13(KDpredictions, 0.45f);
                return KDpredictions;
            }

            for (int i = 0; i < nd; i++)
            {
                float x_center = outputTensor[0, 0, i];
                float y_center = outputTensor[0, 1, i];
                float width = outputTensor[0, 2, i];
                float height = outputTensor[0, 3, i];

                int bestClassId = 0;
                float bestConfidence = 0f;

                if (_fNC05 == 1)
                {
                    bestConfidence = outputTensor[0, 4, i];
                }
                else if (selectedClassId == -1)
                {
                    for (int classId = 0; classId < _fNC05; classId++)
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

                if (bestConfidence < minConfidence) continue;

                float x_min = x_center - width / 2;
                float y_min = y_center - height / 2;
                float x_max = x_center + width / 2;
                float y_max = y_center + height / 2;

                {
                    float fdx = x_center - fovCenterX;
                    float fdy = y_center - fovCenterY;
                    if (fdx * fdx + fdy * fdy > fovRadiusSq) continue;
                }

                KDpredictions.Add(new Prediction
                {
                    Rectangle = new RectangleF(x_min, y_min, width, height),
                    Confidence = bestConfidence,
                    ClassId = bestClassId,
                    ClassName = _fC03.GetValueOrDefault(bestClassId, $"Class_{bestClassId}"),
                    CenterXTranslated = x_center * invImageSize,
                    CenterYTranslated = y_center * invImageSize,
                    ScreenCenterX = detectionBox.Left + x_center,
                    ScreenCenterY = detectionBox.Top + y_center
                });
            }

            _mA13(KDpredictions, 0.45f);
            return KDpredictions;
        }

        private static void _mA13(List<Prediction> predictions, float iouThreshold)
        {
            if (predictions.Count <= 1) return;

            predictions.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));

            for (int i = 0; i < predictions.Count; i++)
            {
                var kept = predictions[i];
                for (int j = predictions.Count - 1; j > i; j--)
                {
                    float iou = _mI0D(kept.Rectangle, predictions[j].Rectangle);
                    if (iou > iouThreshold)
                        predictions.RemoveAt(j);
                }
            }
        }

        #endregion AI Loop Functions

        #endregion AI

        #region Screen Capture

        private void _mS14(Bitmap? frame, Prediction? DoLabel = null)
        {
            if (!_fA29) return;
            if (frame == null) return;
            if (_fA25 && !Convert.ToBoolean(Dictionary.toggleState[_xB9D2._c32])) return;

            long now = Environment.TickCount64;
            if (now - _fS04 < _fK18) return;

            try
            {

                int width = frame.Width;
                int height = frame.Height;
                if (width <= 0 || height <= 0) return;

                _fS04 = now;
                string uuid = Guid.NewGuid().ToString();
                string imagePath = Path.Combine("bin", "images", $"{uuid}.jpg");

                frame.Save(imagePath, ImageFormat.Jpeg);

                if (Convert.ToBoolean(Dictionary.toggleState[_xB9D2._c32]) && DoLabel != null)
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
                Log(LogLevel.Error, $"_mS14 failed: {ex.Message}");
            }
        }



        #endregion Screen Capture

        public void Dispose()
        {
            _fC0F?.Cancel();

            try { _fT10?.Wait(TimeSpan.FromSeconds(2)); } catch { }

            _fC0F?.Dispose();
            _fC0F = null;

            _fP3B.Dispose();

            _fR14 = null;
            _fR15 = null;
            _fX0C?.Dispose();
            _fX0C = null;
            _fO0D?.Dispose();
        }
    }
    public class Prediction
    {
        public RectangleF Rectangle { get; set; }
        public float Confidence { get; set; }
        public int ClassId { get; set; } = 0;
        public string ClassName { get; set; } = "Enemy";
        public float CenterXTranslated { get; set; }
        public float CenterYTranslated { get; set; }
        public float ScreenCenterX { get; set; }
        public float ScreenCenterY { get; set; }
    }
}