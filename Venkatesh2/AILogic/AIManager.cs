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

        private int _currentImageSize;
        private readonly object _sizeLock = new object();
        private volatile bool _sizeChangePending = false;

        public void RequestSizeChange(int newSize)
        {
            lock (_sizeLock)
            {
                _sizeChangePending = true;
            }
        }

        // Dynamic properties instead of constants
        public int IMAGE_SIZE => _currentImageSize;
        private int NUM_DETECTIONS { get; set; } = 8400; // Will be set dynamically for dynamic models
        private bool IsDynamicModel { get; set; } = false;

        // Public static property to check if current loaded model is dynamic
        public static bool CurrentModelIsDynamic { get; private set; } = false;
        private int NUM_CLASSES { get; set; } = 1;
        private Dictionary<int, string> _modelClasses = new Dictionary<int, string>
        {
            { 0, "enemy" }
        };
        public Dictionary<int, string> ModelClasses => _modelClasses; // apparently this is better than making _modelClasses public
        public static event Action<Dictionary<int, string>>? ClassesUpdated;
        public static event Action<int>? ImageSizeUpdated;
        public static event Action<bool>? DynamicModelStatusChanged;

        private const int SAVE_FRAME_COOLDOWN_MS = 500;

        private long _lastSavedTick = 0;
        private List<string>? _outputNames;
        private RectangleF LastDetectionBox;

        // Display-aware properties
        private int ScreenWidth => DisplayManager.ScreenWidth;
        private int ScreenHeight => DisplayManager.ScreenHeight;
        private int ScreenLeft => DisplayManager.ScreenLeft;
        private int ScreenTop => DisplayManager.ScreenTop;

        private readonly RunOptions? _modeloptions;
        private InferenceSession? _onnxModel;

        // CancellationTokenSource drives the async loop lifecycle.
        // Unlike a volatile bool + Thread.Join, cancelling the token wakes any
        // Task.Delay inside the loop immediately and propagates through async continuations.
        private CancellationTokenSource? _loopCts;
        private Task? _loopTask;

        // Non-sticky aim: IoU continuity (prevents flicker between equidistant targets)
        private RectangleF _lastAimRect;
        private bool _hasLastAimRect;

        // Sticky-Aim (SORT-inspired single-target tracker)
        private Prediction? _currentTarget = null;
        private int _consecutiveFramesWithoutTarget = 0;
        private int _trackAge = 0;

        private float _lastTargetVelocityX = 0f;
        private float _lastTargetVelocityY = 0f;
        private float _lastTargetAccelX = 0f;
        private float _lastTargetAccelY = 0f;
        private const float REFERENCE_TARGET_SIZE = 10000f;

        private int detectedX { get; set; }
        private int detectedY { get; set; }

        public double AIConf = 0;
        private static int targetX, targetY;

        // Capture is square (IMAGE_SIZE × IMAGE_SIZE, no resize), so both axes use
        // the same scale to keep movement speed equal on X and Y.
        private float _scaleX => ScreenWidth / (float)IMAGE_SIZE;
        private float _scaleY => ScreenWidth / (float)IMAGE_SIZE;

        // Tensor reuse (model inference)
        private DenseTensor<float>? _reusableTensor;
        private float[]? _reusableInputArray;
        private List<NamedOnnxValue>? _reusableInputs;

        // Reused per-frame detection list — avoids a new List<Prediction> allocation every inference call.
        private readonly List<Prediction> _kdPredictions = new(32);

        // Reused grace-period prediction — avoids a new Prediction allocation on every miss frame.
        private readonly Prediction _gracePrediction = new();

        // ── Per-frame cache ───────────────────────────────────────────────────────────────
        // Dictionary<string,dynamic> hash lookups cost ~30-50 cycles each. At 144 FPS the
        // hot path touches 20+ keys. Cache them all once at frame start and read fields.
        private bool _fcAimAssist;
        private bool _fcShowDetectedPlayer;
        private bool _fcConstantAiTracking;
        private bool _fcStickyAim;
        private bool _fcXAxisPct;
        private bool _fcYAxisPct;
        private bool _fcCollectData;
        private bool _fcFovEnabled;
        private bool _fcClosestToMouse;
        private float _fcFovSize;
        private float _fcMinConfidence;
        private double _fcYOffset;
        private double _fcXOffset;
        private double _fcYOffsetPct;
        private double _fcXOffsetPct;
        private double _fcStickyThreshold;
        private string _fcAimingAlignment = "Center";
        // Single P/Invoke mouse-position read per frame replaces 2-3 GetCursorPosition calls.
        private System.Drawing.Point _fcMousePos;

        // Target-class ID cached across frames — refreshed only when dropdown changes.
        private string _cachedTargetClassStr = "";
        private int _cachedTargetClassId = -1;

        private void RefreshFrameCache()
        {
            _fcAimAssist           = Convert.ToBoolean(Dictionary.toggleState["Aim Assist"]);
            _fcShowDetectedPlayer  = Convert.ToBoolean(Dictionary.toggleState["Show Detected Player"]);
            _fcConstantAiTracking  = Convert.ToBoolean(Dictionary.toggleState["Constant AI Tracking"]);
            _fcStickyAim           = Convert.ToBoolean(Dictionary.toggleState["Sticky Aim"]);
            _fcXAxisPct            = Convert.ToBoolean(Dictionary.toggleState["X Axis Percentage Adjustment"]);
            _fcYAxisPct            = Convert.ToBoolean(Dictionary.toggleState["Y Axis Percentage Adjustment"]);
            _fcCollectData         = Convert.ToBoolean(Dictionary.toggleState["Collect Data While Playing"]);
            _fcFovEnabled          = Convert.ToBoolean(Dictionary.toggleState["FOV"]);
            _fcFovSize             = Convert.ToSingle(Dictionary.sliderSettings["FOV Size"]);
            _fcMinConfidence       = Convert.ToSingle(Dictionary.sliderSettings["AI Minimum Confidence"]) / 100.0f;
            _fcYOffset             = Convert.ToDouble(Dictionary.sliderSettings["Y Offset (Up/Down)"]);
            _fcXOffset             = Convert.ToDouble(Dictionary.sliderSettings["X Offset (Left/Right)"]);
            _fcYOffsetPct          = Convert.ToDouble(Dictionary.sliderSettings["Y Offset (%)"]);
            _fcXOffsetPct          = Convert.ToDouble(Dictionary.sliderSettings["X Offset (%)"]);
            _fcStickyThreshold     = Convert.ToDouble(Dictionary.sliderSettings["Sticky Aim Threshold"]);
            _fcAimingAlignment     = Convert.ToString(Dictionary.dropdownState["Aiming Boundaries Alignment"]) ?? "Center";
            _fcClosestToMouse      = Convert.ToString(Dictionary.dropdownState["Detection Area Type"]) == "Closest to Mouse";

            // Single P/Invoke for the frame — replaces repeated GetCursorPosition calls.
            _fcMousePos = WinAPICaller.GetCursorPosition();

            // Target-class ID — linear search only on dropdown change.
            string tc = Convert.ToString(Dictionary.dropdownState["Target Class"]) ?? "Best Confidence";
            if (tc != _cachedTargetClassStr)
            {
                _cachedTargetClassStr = tc;
                _cachedTargetClassId  = tc == "Best Confidence" ? -1
                    : _modelClasses.FirstOrDefault(c => c.Value == tc).Key;
            }
        }

        private readonly CaptureManager _captureManager = new();
        #endregion Variables

        public AIManager(string modelPath)
        {
            // Initialize the cached image size
            _currentImageSize = int.Parse(Dictionary.dropdownState["Image Size"]);

            // Initialize DXGI capture for current display
            if (Dictionary.dropdownState["Screen Capture Method"] == "DirectX")
            {
                _captureManager.InitializeDxgiDuplication();
            }

            _modeloptions = new RunOptions();

            // Attempt to load via DirectML (else fallback to CPU). Session options are rebuilt per attempt
            // so a failed DML registration doesn't contaminate the CPU fallback's provider list.
            Task.Run(() => InitializeModel(modelPath));
        }

        #region Models

        private static SessionOptions BuildSessionOptions()
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

        // Prefer a pre-converted FP16 sibling (<name>_fp16.onnx) when available.
        // FP16 runs 2-3x faster on Ada Lovelace tensor cores via DirectML; the conversion
        // script is in tools/convert_fp16.py.
        private static string ResolveFp16Path(string modelPath)
        {
            string dir  = Path.GetDirectoryName(modelPath) ?? "";
            string stem = Path.GetFileNameWithoutExtension(modelPath);
            string fp16 = Path.Combine(dir, stem + "_fp16.onnx");
            return File.Exists(fp16) ? fp16 : modelPath;
        }

        private async Task InitializeModel(string modelPath)
        {
            string originalPath = modelPath;
            string resolved = ResolveFp16Path(modelPath);
            bool usingFp16 = !string.Equals(resolved, originalPath, StringComparison.OrdinalIgnoreCase);

            if (usingFp16)
                Log(LogLevel.Info, $"FP16 model found — using {Path.GetFileName(resolved)} for faster inference.");

            // Attempt 1: FP16 (or original) via DirectML
            if (await TryLoadModel(resolved, useDirectML: true))
            { FileManager.CurrentlyLoadingModel = false; return; }

            // Attempt 2: same model via CPU
            if (await TryLoadModel(resolved, useDirectML: false))
            { FileManager.CurrentlyLoadingModel = false; return; }

            // Attempt 3: if we were using FP16 and both EPs failed, try the original FP32 via DirectML
            if (usingFp16)
            {
                Log(LogLevel.Warning, "FP16 model failed on all providers — retrying with original FP32 model.", true);
                if (await TryLoadModel(originalPath, useDirectML: true))
                { FileManager.CurrentlyLoadingModel = false; return; }

                // Attempt 4: original FP32 via CPU
                if (await TryLoadModel(originalPath, useDirectML: false))
                { FileManager.CurrentlyLoadingModel = false; return; }
            }

            Log(LogLevel.Error, "All model loading attempts failed. Aim assist will not work.", true);
            FileManager.CurrentlyLoadingModel = false;
        }

        // Returns true if the model loaded and the loop started successfully.
        private async Task<bool> TryLoadModel(string modelPath, bool useDirectML)
        {
            try
            {
                await LoadModelAsync(BuildSessionOptions(), modelPath, useDirectML);
                return _loopTask != null; // loop was started inside LoadModelAsync on success
            }
            catch (Exception ex)
            {
                string ep = useDirectML ? "DirectML" : "CPU";
                Log(LogLevel.Error, $"Model load via {ep} failed: {ex.Message}");
                return false;
            }
        }

        // Registers the DirectML EP. deviceId=0 = primary DXGI adapter (high-perf GPU on hybrid).
        //
        // DO NOT re-enable ep.dml.enable_graph_capture / enable_dynamic_graph_fusion here.
        // Those hints cause DML to record the command list on Run 1 and replay it on every
        // subsequent Run. The replay path did not populate the output buffer on RTX 4070 Super
        // (tested 2026-04-22): frame 1 produced real output, every subsequent frame returned
        // zeros. Without graph capture, DML runs the compute path fresh each frame and the
        // output buffer is populated every time. The perf loss is measured in microseconds;
        // the correctness loss is total.
        private static void AppendDirectMLProvider(SessionOptions sessionOptions)
        {
            sessionOptions.AppendExecutionProvider_DML(0);
        }

        private Task LoadModelAsync(SessionOptions sessionOptions, string modelPath, bool useDirectML)
        {
            try
            {
                if (useDirectML)
                {
                    AppendDirectMLProvider(sessionOptions);
                }
                else
                {
                    sessionOptions.AppendExecutionProvider_CPU();
                }

                _onnxModel = new InferenceSession(modelPath, sessionOptions);
                _outputNames = new List<string>(_onnxModel.OutputMetadata.Keys);

                // Validate the onnx model output shape (ensure model is OnnxV8)
                if (!ValidateOnnxShape())
                {
                    _onnxModel?.Dispose();
                    return Task.CompletedTask;
                }

            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error loading the model: {ex.Message}", true);
                _onnxModel?.Dispose();
                return Task.CompletedTask;
            }

            // Start the AI loop as an awaitable Task so cancellation propagates through
            // all async continuations. Thread.Join only waited for the first synchronous
            // segment; now _loopTask.Wait() in Dispose waits for the entire async chain.
            _loopCts = new CancellationTokenSource();
            _loopTask = Task.Run(() => AiLoop(_loopCts.Token));
            return Task.CompletedTask;
        }

        private bool ValidateOnnxShape()
        {
            if (_onnxModel != null)
            {
                var inputMetadata = _onnxModel.InputMetadata;
                var outputMetadata = _onnxModel.OutputMetadata;

                Log(LogLevel.Info, "=== Model Metadata ===");
                Log(LogLevel.Info, "Input Metadata:");

                bool isDynamic = false;
                int fixedInputSize = 0;

                foreach (var kvp in inputMetadata)
                {
                    string dimensionsStr = string.Join("x", kvp.Value.Dimensions);
                    Log(LogLevel.Info, $"  Name: {kvp.Key}, Dimensions: {dimensionsStr}");

                    // Check if model is dynamic (dimensions are -1)
                    if (kvp.Value.Dimensions.Any(d => d == -1))
                    {
                        isDynamic = true;
                    }
                    else if (kvp.Value.Dimensions.Length == 4)
                    {
                        // For fixed models, check if it's the expected format (1x3xHxW)
                        fixedInputSize = kvp.Value.Dimensions[2]; // Height should equal Width for square models
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
                    // For dynamic models, calculate NUM_DETECTIONS based on selected image size
                    NUM_DETECTIONS = CalculateNumDetections(IMAGE_SIZE);
                    LoadClasses();
                    ImageSizeUpdated?.Invoke(IMAGE_SIZE);
                    Log(LogLevel.Info, $"Loaded dynamic model - using selected image size {IMAGE_SIZE}x{IMAGE_SIZE} with {NUM_DETECTIONS} detections", true, 3000);
                }
                else
                {
                    // List of supported sizes
                    var supportedSizes = new[] { "640", "512", "416", "320", "256", "160" };
                    var fixedSizeStr = fixedInputSize.ToString();

                    if (!supportedSizes.Contains(fixedSizeStr))
                    {
                        Log(LogLevel.Error,
                            $"Model requires unsupported size {fixedInputSize}x{fixedInputSize}. Supported sizes are: {string.Join(", ", supportedSizes)}",
                            true, 10000);
                        return false;
                    }

                    // Always calculate NUM_DETECTIONS based on the model's fixed size
                    NUM_DETECTIONS = CalculateNumDetections(fixedInputSize);
                    _currentImageSize = fixedInputSize;

                    if (fixedInputSize != int.Parse(Dictionary.dropdownState["Image Size"]))
                    {
                        // Auto-adjust the image size to match the model
                        Log(LogLevel.Warning,
                            $"Fixed-size model expects {fixedInputSize}x{fixedInputSize}. Automatically adjusting Image Size setting.",
                            true, 3000);

                        Dictionary.dropdownState["Image Size"] = fixedSizeStr;

                        // Update the UI dropdown if it exists
                        Application.Current?.Dispatcher.BeginInvoke(() =>
                        {
                            try
                            {
                                // Find the MainWindow and update the dropdown
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
                    LoadClasses();

                    // For static models, validate the expected shape
                    var expectedShape = new int[] { 1, 4 + NUM_CLASSES, NUM_DETECTIONS };
                    if (!outputMetadata.Values.All(metadata => metadata.Dimensions.SequenceEqual(expectedShape)))
                    {
                        Log(LogLevel.Error,
                            $"Output shape does not match the expected shape of {string.Join("x", expectedShape)}.\nThis model will not work with Venkatesh, please use an YOLOv8 model converted to ONNXv8.",
                            true, 10000);
                        return false;
                    }

                    Log(LogLevel.Info, $"Loaded fixed-size model: {fixedInputSize}x{fixedInputSize}", true, 2000);
                }

                // Notify UI about dynamic model status
                DynamicModelStatusChanged?.Invoke(IsDynamicModel);

                return true;
            }

            return false;
        }

        private void LoadClasses()
        {
            if (_onnxModel == null) return;
            _modelClasses.Clear();

            try
            {
                var metadata = _onnxModel.ModelMetadata;

                if (metadata != null &&
                    metadata.CustomMetadataMap.TryGetValue("names", out string? value) &&
                    !string.IsNullOrEmpty(value))
                {
                    JObject data = JObject.Parse(value);
                    if (data != null && data.Type == JTokenType.Object)
                    {
                        //int maxClassId = -1;
                        foreach (var item in data)
                        {
                            if (int.TryParse(item.Key, out int classId) && item.Value?.Type == JTokenType.String)
                            {
                                _modelClasses[classId] = item.Value.ToString();
                            }
                        }
                        NUM_CLASSES = _modelClasses.Count > 0 ? _modelClasses.Keys.Max() + 1 : 1;
                        Log(LogLevel.Info, $"Loaded {_modelClasses.Count} class(es) from model metadata: {data.ToString(Newtonsoft.Json.Formatting.None)}", false);
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
                ClassesUpdated?.Invoke(new Dictionary<int, string>(_modelClasses));
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error loading classes: {ex.Message}", true);
            }
        }

        #endregion Models

        #region AI

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldPredict() =>
            _fcShowDetectedPlayer ||
            _fcConstantAiTracking ||
            InputBindingManager.IsHoldingBinding("Aim Keybind") ||
            InputBindingManager.IsHoldingBinding("Second Aim Keybind");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldProcess() =>
            _fcAimAssist ||
            _fcShowDetectedPlayer;

        // Target 144 FPS for the active AI loop; ~6.94ms per frame budget
        private const double TARGET_FRAME_MS = 1000.0 / 144.0;
        // Sleep this long when nothing is happening — keeps CPU near-idle
        private const int IDLE_DELAY_MS = 33;

        private async Task AiLoop(CancellationToken ct)
        {
            Stopwatch frameTimer = new();

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Yield pending display changes before anything else.
                    lock (_sizeLock)
                    {
                        if (_sizeChangePending)
                        {
                            // Size change pending — wait briefly and retry.
                            // Do NOT spin; sleep so the CPU is free for the UI thread.
                            Task.Delay(5, ct).Wait(ct);
                            continue;
                        }
                    }

                    frameTimer.Restart();
                    RefreshFrameCache();
                    _captureManager.HandlePendingDisplayChanges();
                    UpdateFOV();

                    if (!ShouldProcess())
                    {
                        await Task.Delay(IDLE_DELAY_MS, ct);
                        continue;
                    }

                    if (!ShouldPredict())
                    {
                        await Task.Delay(IDLE_DELAY_MS, ct);
                        continue;
                    }

                    Prediction? closestPrediction = GetClosestPrediction();
                    DetectedPlayerWindow? DetectedPlayerOverlay = Dictionary.DetectedPlayerOverlay;

                    if (closestPrediction == null)
                    {
                        DisableOverlay(DetectedPlayerOverlay);
                    }
                    else
                    {
                        CalculateCoordinates(DetectedPlayerOverlay, closestPrediction, _scaleX, _scaleY);
                        HandleAim(closestPrediction);
                    }

                    // FPS cap: sleep the remainder of the frame budget.
                    double elapsed = frameTimer.Elapsed.TotalMilliseconds;
                    int remaining = (int)(TARGET_FRAME_MS - elapsed);
                    if (remaining > 0)
                        await Task.Delay(remaining, ct);
                }
                catch (OperationCanceledException)
                {
                    break; // Clean shutdown — cancellation requested.
                }
                catch (Exception ex)
                {
                    // Log but never let a frame exception escape and kill the loop.
                    // ObjectDisposedException here means the model was swapped mid-frame;
                    // just skip the frame and the next iteration will see ct cancelled.
                    Log(LogLevel.Error, $"AiLoop frame error: {ex.GetType().Name}: {ex.Message}");
                    try { await Task.Delay(IDLE_DELAY_MS, ct); } catch { break; }
                }
            }
        }

        #region AI Loop Functions

        private void UpdateFOV()
        {
            if (!_fcClosestToMouse || !_fcFovEnabled) return;
            if (Dictionary.FOVWindow == null) return;

            var mousePosition = _fcMousePos;
            if (!DisplayManager.IsPointInCurrentDisplay(new System.Windows.Point(mousePosition.X, mousePosition.Y)))
                return;

            var displayRelativeX = mousePosition.X - DisplayManager.ScreenLeft;
            var displayRelativeY = mousePosition.Y - DisplayManager.ScreenTop;

            // BeginInvoke is fire-and-forget on the UI thread — no await needed.
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (Dictionary.FOVWindow == null) return;
                Dictionary.FOVWindow.FOVStrictEnclosure.Margin = new Thickness(
                    Convert.ToInt16(displayRelativeX / WinAPICaller.scalingFactorX) - 320,
                    Convert.ToInt16(displayRelativeY / WinAPICaller.scalingFactorY) - 320, 0, 0);
            });
        }

        private static void DisableOverlay(DetectedPlayerWindow? DetectedPlayerOverlay)
        {
            if (DetectedPlayerOverlay == null) return;
            if (!Convert.ToBoolean(Dictionary.toggleState["Show Detected Player"])) return;

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (Convert.ToBoolean(Dictionary.toggleState["Show AI Confidence"]))
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Opacity = 0;

                if (Convert.ToBoolean(Dictionary.toggleState["Show Tracers"]))
                    DetectedPlayerOverlay.DetectedTracers.Opacity = 0;

                DetectedPlayerOverlay.DetectedPlayerFocus.Opacity = 0;
            });
        }

        private void UpdateOverlay(DetectedPlayerWindow? DetectedPlayerOverlay, Prediction closestPrediction)
        {
            var scalingFactorX = WinAPICaller.scalingFactorX;
            var scalingFactorY = WinAPICaller.scalingFactorY;

            // Convert screen coordinates to display-relative coordinates
            var displayRelativeX = LastDetectionBox.X - DisplayManager.ScreenLeft;
            var displayRelativeY = LastDetectionBox.Y - DisplayManager.ScreenTop;

            // Calculate center position in display-relative coordinates
            var centerX = Convert.ToInt16(displayRelativeX / scalingFactorX) + (LastDetectionBox.Width / 2.0);
            var centerY = Convert.ToInt16(displayRelativeY / scalingFactorY);

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (DetectedPlayerOverlay == null) return;
                if (Convert.ToBoolean(Dictionary.toggleState["Show AI Confidence"]))
                {
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Opacity = 1;
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Content = $"{closestPrediction.ClassName}: {Math.Round((AIConf * 100), 2)}%";

                    var labelEstimatedHalfWidth = DetectedPlayerOverlay.DetectedPlayerConfidence.ActualWidth / 2.0;
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Margin = new Thickness(
                        centerX - labelEstimatedHalfWidth,
                        centerY - DetectedPlayerOverlay.DetectedPlayerConfidence.ActualHeight - 2, 0, 0);
                }
                bool showTracers = Convert.ToBoolean(Dictionary.toggleState["Show Tracers"]);
                DetectedPlayerOverlay.DetectedTracers.Opacity = showTracers ? 1 : 0;
                if (showTracers)
                {
                    string tracerPosition = Convert.ToString(Dictionary.dropdownState["Tracer Position"]) ?? "Bottom";

                    var boxTop = centerY;
                    var boxBottom = centerY + LastDetectionBox.Height;
                    var boxHorizontalCenter = centerX;
                    var boxVerticalCenter = centerY + (LastDetectionBox.Height / 2.0);
                    var boxLeft = centerX - (LastDetectionBox.Width / 2.0);
                    var boxRight = centerX + (LastDetectionBox.Width / 2.0);

                    switch (tracerPosition)
                    {
                        case "Top":
                            DetectedPlayerOverlay.DetectedTracers.X2 = boxHorizontalCenter;
                            DetectedPlayerOverlay.DetectedTracers.Y2 = boxTop;
                            break;

                        case "Bottom":
                            DetectedPlayerOverlay.DetectedTracers.X2 = boxHorizontalCenter;
                            DetectedPlayerOverlay.DetectedTracers.Y2 = boxBottom;
                            break;

                        case "Middle":
                            var screenHorizontalCenter = DisplayManager.ScreenWidth / (2.0 * WinAPICaller.scalingFactorX);
                            if (boxHorizontalCenter < screenHorizontalCenter)
                            {
                                // if the box is on the left half of the screen, aim for the right-middle of the box
                                DetectedPlayerOverlay.DetectedTracers.X2 = boxRight;
                                DetectedPlayerOverlay.DetectedTracers.Y2 = boxVerticalCenter;
                            }
                            else
                            {
                                // if the box is on the right half, aim for the left-middle
                                DetectedPlayerOverlay.DetectedTracers.X2 = boxLeft;
                                DetectedPlayerOverlay.DetectedTracers.Y2 = boxVerticalCenter;
                            }
                            break;

                        default:
                            // default to the bottom-center if the setting is unrecognized
                            DetectedPlayerOverlay.DetectedTracers.X2 = boxHorizontalCenter;
                            DetectedPlayerOverlay.DetectedTracers.Y2 = boxBottom;
                            break;
                    }
                }

                DetectedPlayerOverlay.Opacity = Convert.ToDouble(Dictionary.sliderSettings["Opacity"]);

                DetectedPlayerOverlay.DetectedPlayerFocus.Opacity = 1;
                DetectedPlayerOverlay.DetectedPlayerFocus.Margin = new Thickness(
                    centerX - (LastDetectionBox.Width / 2.0), centerY, 0, 0);
                DetectedPlayerOverlay.DetectedPlayerFocus.Width = LastDetectionBox.Width;
                DetectedPlayerOverlay.DetectedPlayerFocus.Height = LastDetectionBox.Height;
            });
        }

        private void CalculateCoordinates(DetectedPlayerWindow? DetectedPlayerOverlay, Prediction closestPrediction, float scaleX, float scaleY)
        {
            AIConf = closestPrediction.Confidence;

            if (_fcShowDetectedPlayer && DetectedPlayerOverlay != null)
            {
                UpdateOverlay(DetectedPlayerOverlay, closestPrediction);
                if (!_fcAimAssist) return;
            }

            var rect = closestPrediction.Rectangle;

            detectedX = _fcXAxisPct
                ? (int)((rect.X + rect.Width * (_fcXOffsetPct / 100)) * scaleX)
                : (int)((rect.X + rect.Width / 2) * scaleX + _fcXOffset);

            detectedY = _fcYAxisPct
                ? (int)((rect.Y + rect.Height - rect.Height * (_fcYOffsetPct / 100)) * scaleY + _fcYOffset)
                : CalculateDetectedY(scaleY, _fcYOffset, closestPrediction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int CalculateDetectedY(float scaleY, double yOffset, Prediction closestPrediction)
        {
            var rect = closestPrediction.Rectangle;
            float yAdjustment = _fcAimingAlignment switch
            {
                "Center" => rect.Height / 2,
                "Bottom" => rect.Height,
                _        => 0f
            };
            return (int)((rect.Y + yAdjustment) * scaleY + yOffset);
        }

        private void HandleAim(Prediction closestPrediction)
        {
            if (_fcAimAssist &&
                (_fcConstantAiTracking ||
                 InputBindingManager.IsHoldingBinding("Aim Keybind") ||
                 InputBindingManager.IsHoldingBinding("Second Aim Keybind")))
            {
                MouseManager.MoveCrosshair(detectedX, detectedY);
            }
        }

        private Prediction? GetClosestPrediction()
        {
            if (_fcClosestToMouse)
            {
                var mousePos = _fcMousePos;
                if (DisplayManager.IsPointInCurrentDisplay(new System.Windows.Point(mousePos.X, mousePos.Y)))
                {
                    targetX = mousePos.X;
                    targetY = mousePos.Y;
                }
                else
                {
                    targetX = DisplayManager.ScreenLeft + (DisplayManager.ScreenWidth / 2);
                    targetY = DisplayManager.ScreenTop + (DisplayManager.ScreenHeight / 2);
                }
            }
            else
            {
                targetX = DisplayManager.ScreenLeft + (DisplayManager.ScreenWidth / 2);
                targetY = DisplayManager.ScreenTop + (DisplayManager.ScreenHeight / 2);
            }

            Rectangle detectionBox = new(targetX - IMAGE_SIZE / 2, targetY - IMAGE_SIZE / 2, IMAGE_SIZE, IMAGE_SIZE); // Detection box dynamic size

            IDisposableReadOnlyCollection<DisposableNamedOnnxValue>? results = null;
            Tensor<float>? outputTensor = null;

            try
            {
                // Allocate the input array + DenseTensor once per IMAGE_SIZE. The tensor wraps the
                // array by reference, so writes to _reusableInputArray update the tensor buffer
                // directly — no per-frame CopyTo needed.
                if (_reusableInputArray == null
                    || _reusableInputArray.Length != 3 * IMAGE_SIZE * IMAGE_SIZE
                    || _reusableTensor == null
                    || _reusableTensor.Dimensions[2] != IMAGE_SIZE)
                {
                    _reusableInputArray = new float[3 * IMAGE_SIZE * IMAGE_SIZE];
                    _reusableTensor = new DenseTensor<float>(_reusableInputArray, new int[] { 1, 3, IMAGE_SIZE, IMAGE_SIZE });
                    _reusableInputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", _reusableTensor) };
                }

                Bitmap? frame = _captureManager.CaptureForInference(detectionBox, _reusableInputArray!, IMAGE_SIZE, _fcCollectData);

                if (_onnxModel == null) return null;
                results = _onnxModel.Run(_reusableInputs, _outputNames, _modeloptions);
                outputTensor = results[0].AsTensor<float>();

                if (outputTensor == null)
                {
                    Log(LogLevel.Error, "Model inference returned null output tensor.", true, 2000);
                    SaveFrame(frame);
                    return null;
                }

                float fovMinX, fovMaxX, fovMinY, fovMaxY;
                if (_fcFovEnabled)
                {
                    fovMinX = (IMAGE_SIZE - _fcFovSize) / 2.0f;
                    fovMaxX = (IMAGE_SIZE + _fcFovSize) / 2.0f;
                }
                else
                {
                    fovMinX = -IMAGE_SIZE;
                    fovMaxX = IMAGE_SIZE * 2f;
                }
                fovMinY = fovMinX;
                fovMaxY = fovMaxX;

                List<Prediction> KDPredictions = PrepareKDTreeData(outputTensor, detectionBox, fovMinX, fovMaxX, fovMinY, fovMaxY);

                if (KDPredictions.Count == 0)
                {
                    SaveFrame(frame);
                    if (_fcStickyAim && _currentTarget != null)
                    {
                        Prediction? graceTarget = HandleNoDetections();
                        if (graceTarget != null)
                            UpdateDetectionBox(graceTarget, detectionBox);
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

                Prediction? finalTarget = HandleStickyAim(bestCandidate, KDPredictions);
                if (finalTarget != null)
                {
                    UpdateDetectionBox(finalTarget, detectionBox);
                    SaveFrame(frame, finalTarget);
                    return finalTarget;
                }

                return null;
            }
            finally
            {
                // Bitmap is owned and reused by CaptureManager — do not dispose it here.
                results?.Dispose();
            }
        }

        private Prediction? HandleStickyAim(Prediction? bestCandidate, List<Prediction> KDPredictions)
        {
            if (!_fcStickyAim)
            {
                ResetStickyAimState();

                if (_hasLastAimRect && bestCandidate != null && KDPredictions != null)
                {
                    Prediction? continued = null;
                    float contIoU = 0f;
                    foreach (var c in KDPredictions)
                    {
                        float iou = ComputeIoU(c.Rectangle, _lastAimRect);
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

                _hasLastAimRect = bestCandidate != null;
                if (bestCandidate != null)
                    _lastAimRect = bestCandidate.Rectangle;

                return bestCandidate;
            }

            if (bestCandidate == null || KDPredictions == null || KDPredictions.Count == 0)
                return HandleNoDetections();

            if (_currentTarget == null)
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
                return AcquireNewTarget(nearest);
            }

            float lastX = _currentTarget.ScreenCenterX;
            float lastY = _currentTarget.ScreenCenterY;
            float targetArea = _currentTarget.Rectangle.Width * _currentTarget.Rectangle.Height;
            float targetSize = MathF.Sqrt(targetArea);
            float sizeFactor = GetSizeFactor(targetArea);

            float maxRadius = IMAGE_SIZE * 0.5f;
            float trackingRadiusX = Math.Min(targetSize * 3f, maxRadius);
            float trackingRadiusY = Math.Min(targetSize * 4.5f, maxRadius * 1.5f);

            float velMagSq = _lastTargetVelocityX * _lastTargetVelocityX + _lastTargetVelocityY * _lastTargetVelocityY;
            float dt = _consecutiveFramesWithoutTarget + 1f;
            float expectedX = lastX + _lastTargetVelocityX * dt + 0.5f * _lastTargetAccelX * dt * dt;
            float expectedY = lastY + _lastTargetVelocityY * dt + 0.5f * _lastTargetAccelY * dt * dt;

            var currentRect = _currentTarget.Rectangle;
            RectangleF extrapolatedBox = new(
                currentRect.X + _lastTargetVelocityX * dt + 0.5f * _lastTargetAccelX * dt * dt,
                currentRect.Y + _lastTargetVelocityY * dt + 0.5f * _lastTargetAccelY * dt * dt,
                currentRect.Width,
                currentRect.Height);

            float imageArea = IMAGE_SIZE * IMAGE_SIZE;
            float minSizeRatio = targetArea > imageArea * 0.15f ? 0.2f : 0.4f;

            // ── Stage 1: IoU matching (SORT-inspired) ──
            // Bbox overlap is the strongest identity signal for adjacent/overlapping targets.
            // Two enemies side by side have zero IoU with each other's predicted box.
            Prediction? trackedMatch = null;
            float bestIoU = 0f;

            foreach (var candidate in KDPredictions)
            {
                float candidateArea = candidate.Rectangle.Width * candidate.Rectangle.Height;
                float sizeRatio = MathF.Min(targetArea, candidateArea) / MathF.Max(targetArea, candidateArea);
                if (sizeRatio < minSizeRatio) continue;

                float iou = ComputeIoU(candidate.Rectangle, extrapolatedBox);
                if (iou > bestIoU) { bestIoU = iou; trackedMatch = candidate; }
            }

            if (trackedMatch != null && bestIoU >= 0.15f)
            {
                _trackAge++;
                int missed = _consecutiveFramesWithoutTarget;
                _consecutiveFramesWithoutTarget = 0;
                UpdateMotion(trackedMatch, sizeFactor, missed);
                _currentTarget = trackedMatch;
                return trackedMatch;
            }

            // ── Stage 2: Distance fallback (ByteTrack-inspired) ──
            // Catches fast movement where the bbox shifted too far for IoU overlap.
            trackedMatch = null;
            float bestProximitySq = float.MaxValue;

            foreach (var candidate in KDPredictions)
            {
                float candidateArea = candidate.Rectangle.Width * candidate.Rectangle.Height;
                float sizeRatio = MathF.Min(targetArea, candidateArea) / MathF.Max(targetArea, candidateArea);
                if (sizeRatio < minSizeRatio) continue;

                float distToExpectedSq = GetDistanceSq(candidate.ScreenCenterX, candidate.ScreenCenterY, expectedX, expectedY);
                float distToLastSq = GetDistanceSq(candidate.ScreenCenterX, candidate.ScreenCenterY, lastX, lastY);

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
                _trackAge++;
                int missed = _consecutiveFramesWithoutTarget;
                _consecutiveFramesWithoutTarget = 0;
                UpdateMotion(trackedMatch, sizeFactor, missed);
                _currentTarget = trackedMatch;
                return trackedMatch;
            }

            return HandleNoDetections();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetDistanceSq(float x1, float y1, float x2, float y2)
        {
            float dx = x1 - x2;
            float dy = y1 - y2;
            return dx * dx + dy * dy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ComputeIoU(RectangleF a, RectangleF b)
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
        private float GetSizeFactor(float targetArea)
        {
            float ratio = REFERENCE_TARGET_SIZE / Math.Max(targetArea, 100f);
            return Math.Clamp(ratio, 1.0f, 3.0f);
        }

        private Prediction? HandleNoDetections()
        {
            if (_currentTarget != null)
            {
                float velMag = MathF.Sqrt(_lastTargetVelocityX * _lastTargetVelocityX + _lastTargetVelocityY * _lastTargetVelocityY);
                float lastArea = _currentTarget.Rectangle.Width * _currentTarget.Rectangle.Height;
                bool closeRange = lastArea > IMAGE_SIZE * IMAGE_SIZE * 0.15f;
                int ageBonus = Math.Min(_trackAge / 10, 3);
                int dynamicGrace = (velMag > 5f ? 6 : velMag > 2f ? 4 : closeRange ? 4 : 2) + ageBonus;

                if (++_consecutiveFramesWithoutTarget <= dynamicGrace)
                {
                    float t = _consecutiveFramesWithoutTarget;
                    float decayRate = 1f / (dynamicGrace + 1);
                    float extraX = _lastTargetVelocityX * t + 0.5f * _lastTargetAccelX * t * t;
                    float extraY = _lastTargetVelocityY * t + 0.5f * _lastTargetAccelY * t * t;
                    var lastRect = _currentTarget.Rectangle;
                    _gracePrediction.ScreenCenterX      = _currentTarget.ScreenCenterX + extraX;
                    _gracePrediction.ScreenCenterY      = _currentTarget.ScreenCenterY + extraY;
                    _gracePrediction.Rectangle          = new RectangleF(lastRect.X + extraX, lastRect.Y + extraY, lastRect.Width, lastRect.Height);
                    _gracePrediction.Confidence         = _currentTarget.Confidence * (1f - t * decayRate);
                    _gracePrediction.ClassId            = _currentTarget.ClassId;
                    _gracePrediction.ClassName          = _currentTarget.ClassName;
                    _gracePrediction.CenterXTranslated  = _currentTarget.CenterXTranslated;
                    _gracePrediction.CenterYTranslated  = _currentTarget.CenterYTranslated;
                    return _gracePrediction;
                }
            }

            ResetStickyAimState();
            return null;
        }

        private Prediction AcquireNewTarget(Prediction target)
        {
            _lastTargetVelocityX = 0f;
            _lastTargetVelocityY = 0f;
            _lastTargetAccelX = 0f;
            _lastTargetAccelY = 0f;
            _trackAge = 0;
            _currentTarget = target;
            return target;
        }

        private void UpdateMotion(Prediction newTarget, float sizeFactor, int missedFrames = 0)
        {
            if (_currentTarget == null) return;

            float elapsed = missedFrames + 1f;
            float newVelX = (newTarget.ScreenCenterX - _currentTarget.ScreenCenterX) / elapsed;
            float newVelY = (newTarget.ScreenCenterY - _currentTarget.ScreenCenterY) / elapsed;

            float newAccelX = newVelX - _lastTargetVelocityX;
            float newAccelY = newVelY - _lastTargetVelocityY;

            float predX = _currentTarget.ScreenCenterX + _lastTargetVelocityX * elapsed + 0.5f * _lastTargetAccelX * elapsed * elapsed;
            float predY = _currentTarget.ScreenCenterY + _lastTargetVelocityY * elapsed + 0.5f * _lastTargetAccelY * elapsed * elapsed;
            float predErrorSq = GetDistanceSq(newTarget.ScreenCenterX, newTarget.ScreenCenterY, predX, predY);
            float errorNorm = predErrorSq / Math.Max(MathF.Sqrt(_currentTarget.Rectangle.Width * _currentTarget.Rectangle.Height), 10f);

            float baseSmoothing = Math.Clamp(0.6f + (sizeFactor * 0.1f), 0.7f, 0.9f);
            float smoothingVel = errorNorm > 16f ? 0.3f : errorNorm > 4f ? 0.5f : baseSmoothing;
            float smoothingAccel = smoothingVel * 0.8f;

            _lastTargetVelocityX = _lastTargetVelocityX * smoothingVel + newVelX * (1f - smoothingVel);
            _lastTargetVelocityY = _lastTargetVelocityY * smoothingVel + newVelY * (1f - smoothingVel);
            _lastTargetAccelX = _lastTargetAccelX * smoothingAccel + newAccelX * (1f - smoothingAccel);
            _lastTargetAccelY = _lastTargetAccelY * smoothingAccel + newAccelY * (1f - smoothingAccel);
        }

        private void ResetStickyAimState()
        {
            _currentTarget = null;
            _consecutiveFramesWithoutTarget = 0;
            _trackAge = 0;
            _lastTargetVelocityX = 0f;
            _lastTargetVelocityY = 0f;
            _lastTargetAccelX = 0f;
            _lastTargetAccelY = 0f;
        }

        private void UpdateDetectionBox(Prediction target, Rectangle detectionBox)
        {
            float translatedXMin = target.Rectangle.X + detectionBox.Left;
            float translatedYMin = target.Rectangle.Y + detectionBox.Top;
            LastDetectionBox = new(translatedXMin, translatedYMin,
                target.Rectangle.Width, target.Rectangle.Height);
        }
        // is it really kdtreedata though....
        private List<Prediction> PrepareKDTreeData(
            Tensor<float> outputTensor,
            Rectangle detectionBox,
            float fovMinX, float fovMaxX, float fovMinY, float fovMaxY)
        {
            float minConfidence = _fcMinConfidence;
            int selectedClassId = _cachedTargetClassId;

            int nd = NUM_DETECTIONS;
            int imageSize = IMAGE_SIZE;
            float invImageSize = 1.0f / imageSize;
            float fovCenterX = imageSize * 0.5f;
            float fovCenterY = imageSize * 0.5f;
            float fovRadius = (fovMaxX - fovMinX) * 0.5f;
            float fovRadiusSq = fovRadius * fovRadius;
            _kdPredictions.Clear();
            var KDpredictions = _kdPredictions;

            // Fast path: YOLOv8 outputs a contiguous DenseTensor of shape [1, 4+classes, NUM_DETECTIONS].
            // Access it as a flat Span<float> so the inner loop is plain pointer arithmetic instead of
            // Tensor<T>'s virtual indexer with per-call bounds math.
            if (outputTensor is DenseTensor<float> dense)
            {
                ReadOnlySpan<float> span = dense.Buffer.Span;

                // Channel strides: channel c for detection i lives at span[c * nd + i].
                int xOff = 0;
                int yOff = nd;
                int wOff = 2 * nd;
                int hOff = 3 * nd;
                int clsOff = 4 * nd;
                int numClasses = NUM_CLASSES;

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
                        ClassName = _modelClasses.GetValueOrDefault(bestClassId, $"Class_{bestClassId}"),
                        CenterXTranslated = x_center * invImageSize,
                        CenterYTranslated = y_center * invImageSize,
                        ScreenCenterX = detectionBox.Left + x_center,
                        ScreenCenterY = detectionBox.Top + y_center
                    });
                }

                ApplyGreedyNMS(KDpredictions, 0.45f);
                return KDpredictions;
            }

            // Fallback for the rare case ORT hands back a non-dense Tensor<float>.
            for (int i = 0; i < nd; i++)
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
                else if (selectedClassId == -1)
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
                    ClassName = _modelClasses.GetValueOrDefault(bestClassId, $"Class_{bestClassId}"),
                    CenterXTranslated = x_center * invImageSize,
                    CenterYTranslated = y_center * invImageSize,
                    ScreenCenterX = detectionBox.Left + x_center,
                    ScreenCenterY = detectionBox.Top + y_center
                });
            }

            ApplyGreedyNMS(KDpredictions, 0.45f);
            return KDpredictions;
        }

        private static void ApplyGreedyNMS(List<Prediction> predictions, float iouThreshold)
        {
            if (predictions.Count <= 1) return;

            predictions.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));

            for (int i = 0; i < predictions.Count; i++)
            {
                var kept = predictions[i];
                for (int j = predictions.Count - 1; j > i; j--)
                {
                    float iou = ComputeIoU(kept.Rectangle, predictions[j].Rectangle);
                    if (iou > iouThreshold)
                        predictions.RemoveAt(j);
                }
            }
        }

        #endregion AI Loop Functions

        #endregion AI

        #region Screen Capture

        private void SaveFrame(Bitmap? frame, Prediction? DoLabel = null)
        {
            if (!_fcCollectData) return;
            if (frame == null) return;
            if (_fcConstantAiTracking && !Convert.ToBoolean(Dictionary.toggleState["Auto Label Data"])) return;

            // Cooldown check
            long now = Environment.TickCount64;
            if (now - _lastSavedTick < SAVE_FRAME_COOLDOWN_MS) return;

            try
            {

                // Accessing Width/Height will throw if bitmap is disposed
                int width = frame.Width;
                int height = frame.Height;
                if (width <= 0 || height <= 0) return;

                _lastSavedTick = now;
                string uuid = Guid.NewGuid().ToString();
                string imagePath = Path.Combine("bin", "images", $"{uuid}.jpg");

                // Save synchronously to avoid "Object is currently in use elsewhere" error
                frame.Save(imagePath, ImageFormat.Jpeg);

                if (Convert.ToBoolean(Dictionary.toggleState["Auto Label Data"]) && DoLabel != null)
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
                // Bitmap was disposed or invalid - silently ignore
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"SaveFrame failed: {ex.Message}");
            }
        }



        #endregion Screen Capture

        public void Dispose()
        {
            // Cancel the token — wakes any Task.Delay in the loop immediately and propagates
            // through all async continuations, so _loopTask.Wait() below actually waits for
            // the ENTIRE async chain, not just the synchronous preamble before the first await.
            _loopCts?.Cancel();

            // Wait up to 2 s for the loop to finish its current frame and exit cleanly.
            // Exceptions from cancellation are expected; suppress them.
            try { _loopTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }

            _loopCts?.Dispose();
            _loopCts = null;

            // Dispose DXGI objects
            _captureManager.Dispose();

            // Clean up other resources
            _reusableInputArray = null;
            _reusableInputs = null;
            _onnxModel?.Dispose();
            _onnxModel = null;
            _modeloptions?.Dispose();
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
        public float ScreenCenterX { get; set; }  // Absolute screen position
        public float ScreenCenterY { get; set; }
    }
}