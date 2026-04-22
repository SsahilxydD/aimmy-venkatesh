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
        private KalmanPrediction kalmanPrediction;
        private WiseTheFoxPrediction wtfpredictionManager;

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

        // Sticky-Aim
        private Prediction? _currentTarget = null;
        private int _consecutiveFramesWithoutTarget = 0;
        private const int MAX_FRAMES_WITHOUT_TARGET = 3; // Allow 3 frames of target loss

        // Enhanced Sticky Aim State
        private float _lastTargetVelocityX = 0f;
        private float _lastTargetVelocityY = 0f;
        private float _targetLockScore = 0f;           // Accumulated "stickiness" score
        private const float LOCK_SCORE_DECAY = 0.85f;  // Decay per frame when target not matched
        private const float LOCK_SCORE_GAIN = 15f;     // Gain per frame when target matched
        private const float MAX_LOCK_SCORE = 100f;     // Maximum accumulated score
        private const float REFERENCE_TARGET_SIZE = 10000f; // Reference area for "close" targets (approx 100x100)
        private int _framesWithoutMatch = 0;           // Consecutive frames where current target wasn't found

        private int detectedX { get; set; }
        private int detectedY { get; set; }

        public double AIConf = 0;
        private static int targetX, targetY;

        // Pre-calculated values - now dynamic
        private float _scaleX => ScreenWidth / (float)IMAGE_SIZE;
        private float _scaleY => ScreenHeight / (float)IMAGE_SIZE;

        // Tensor reuse (model inference). Backing buffers stay pinned via OrtValues below.
        private float[]? _reusableInputArray;
        private float[]? _reusableOutputArray;
        private DenseTensor<float>? _reusableOutputTensor; // managed view for PrepareKDTreeData

        // IoBinding path — the only reliable way to get DirectML EP to fully populate the
        // output buffer. The previous Run(inputs, outputs, options) overload with
        // NamedOnnxValue wrappers was producing *partial* writes (bbox channels populated,
        // class-score channel all zeros), even though the Run call itself succeeded.
        // OrtValue.CreateTensorValueFromMemory pins the managed array for the OrtValue's
        // lifetime, so ORT writes straight into _reusableOutputArray.
        private OrtIoBinding? _ioBinding;
        private OrtValue? _inputOrtValue;
        private OrtValue? _outputOrtValue;

        // Diagnostic heartbeat — verifies inference is producing output and tells us the
        // confidence distribution so we can tell "inference is silently broken" from
        // "inference works but no enemies on screen / threshold too high".
        private long _hbLastTick = 0;
        private long _hbFrameCounter = 0;

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
        private bool _fcPredictions;
        private bool _fcStickyAim;
        private bool _fcAutoTrigger;
        private bool _fcSprayMode;
        private bool _fcCursorCheck;
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
        private string _fcPredictionMethod = "Kalman Filter";
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
            _fcPredictions         = Convert.ToBoolean(Dictionary.toggleState["Predictions"]);
            _fcStickyAim           = Convert.ToBoolean(Dictionary.toggleState["Sticky Aim"]);
            _fcAutoTrigger         = Convert.ToBoolean(Dictionary.toggleState["Auto Trigger"]);
            _fcSprayMode           = Convert.ToBoolean(Dictionary.toggleState["Spray Mode"]);
            _fcCursorCheck         = Convert.ToBoolean(Dictionary.toggleState["Cursor Check"]);
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
            _fcPredictionMethod    = Convert.ToString(Dictionary.dropdownState["Prediction Method"]) ?? "Kalman Filter";
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

            kalmanPrediction = new KalmanPrediction();
            wtfpredictionManager = new WiseTheFoxPrediction();

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

        // Registers the DirectML EP with settings tuned for a dedicated discrete GPU (e.g. RTX 4070 Super).
        // ORT 1.23's C# surface only exposes device_id for DML directly, so perf hints are routed
        // through session config entries — ORT silently ignores keys it doesn't recognize on older
        // builds, which makes this safe to leave in.
        private static void AppendDirectMLProvider(SessionOptions sessionOptions)
        {
            // Record the DML command list on the first inference and replay it on subsequent
            // calls. Safe for our workload because input shape is fixed for the lifetime of a session
            // (IMAGE_SIZE change triggers a full model reload) and _reusableTensor stays pinned.
            try { sessionOptions.AddSessionConfigEntry("ep.dml.enable_graph_capture", "1"); } catch { }
            // Subgraph fusion hint for dynamic-shape YOLOv8 exports.
            try { sessionOptions.AddSessionConfigEntry("ep.dml.enable_dynamic_graph_fusion", "1"); } catch { }
            // Prefer the highest-performance adapter when DML has to choose between multiple.
            try { sessionOptions.AddSessionConfigEntry("ep.dml.performance_preference", "high_performance"); } catch { }

            // deviceId=0 is the primary DXGI adapter, which Windows "Graphics Settings" aliases to
            // the high-performance GPU when one is available. Explicit over implicit so a stale
            // iGPU doesn't win the pick on hybrid systems.
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
                    Log(LogLevel.Info, $"  Name: {kvp.Key}, Dimensions: {dimensionsStr}, ElementType: {kvp.Value.ElementType?.Name ?? "unknown"}");

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
                    Log(LogLevel.Info, $"  Name: {kvp.Key}, Dimensions: {dimensionsStr}, ElementType: {kvp.Value.ElementType?.Name ?? "unknown"}");
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
            _fcShowDetectedPlayer ||
            _fcAutoTrigger;

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
                        await AutoTrigger();
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
                    LogException("AiLoop", ex);
                    try { await Task.Delay(IDLE_DELAY_MS, ct); } catch { break; }
                }
            }
        }

        #region AI Loop Functions

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task AutoTrigger()
        {
            if (!_fcAutoTrigger ||
                !(InputBindingManager.IsHoldingBinding("Aim Keybind") && !InputBindingManager.IsHoldingBinding("Second Aim Keybind")) ||
                _fcConstantAiTracking)
            {
                CheckSprayRelease();
                return;
            }

            if (_fcSprayMode)
            {
                await MouseManager.DoTriggerClick(LastDetectionBox);
                return;
            }

            if (_fcCursorCheck)
            {
                var mousePos = _fcMousePos;

                if (!DisplayManager.IsPointInCurrentDisplay(new System.Windows.Point(mousePos.X, mousePos.Y)))
                    return;

                if (LastDetectionBox.Contains(mousePos.X, mousePos.Y))
                    await MouseManager.DoTriggerClick(LastDetectionBox);
            }
            else
            {
                await MouseManager.DoTriggerClick();
            }

            if (!_fcAimAssist || !_fcShowDetectedPlayer) return;
        }

        private void CheckSprayRelease()
        {
            if (!_fcSprayMode) return;

            bool shouldSpray = _fcAutoTrigger &&
                (InputBindingManager.IsHoldingBinding("Aim Keybind") && InputBindingManager.IsHoldingBinding("Second Aim Keybind"));

            if (!shouldSpray)
                MouseManager.ResetSprayState();
        }

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
                if (_fcPredictions)
                    HandlePredictions(kalmanPrediction, closestPrediction, detectedX, detectedY);
                else
                    MouseManager.MoveCrosshair(detectedX, detectedY);
            }
        }

        private void HandlePredictions(KalmanPrediction kalmanPrediction, Prediction closestPrediction, int detectedX, int detectedY)
        {
            var predictionMethod = _fcPredictionMethod;
            switch (predictionMethod)
            {
                case "Kalman Filter":
                    KalmanPrediction.Detection detection = new()
                    {
                        X = detectedX,
                        Y = detectedY
                    };

                    kalmanPrediction.UpdateKalmanFilter(detection);
                    var predictedPosition = kalmanPrediction.GetKalmanPosition();

                    MouseManager.MoveCrosshair(predictedPosition.X, predictedPosition.Y);
                    break;

                case "Shall0e's Prediction":
                    ShalloePredictionV2.UpdatePosition(detectedX, detectedY);
                    var shalloePos = ShalloePredictionV2.GetSP();
                    MouseManager.MoveCrosshair(shalloePos.x, shalloePos.y);
                    break;

                case "wisethef0x's EMA Prediction":
                    WiseTheFoxPrediction.WTFDetection wtfdetection = new()
                    {
                        X = detectedX,
                        Y = detectedY
                    };

                    wtfpredictionManager.UpdateDetection(wtfdetection);
                    var wtfpredictedPosition = wtfpredictionManager.GetEstimatedPosition();

                    // Use both predicted X and Y
                    MouseManager.MoveCrosshair(wtfpredictedPosition.X, wtfpredictedPosition.Y);
                    break;
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

            Tensor<float>? outputTensor = null;

            try
            {
                // (Re)allocate when IMAGE_SIZE, NUM_CLASSES, or NUM_DETECTIONS changes.
                //
                // Binding strategy: pin our INPUT array via OrtValue (we populate it from the
                // captured frame each tick), but let ORT allocate the OUTPUT tensor itself and
                // copy the result into our managed buffer after each Run. The previous approach
                // (pinning both with OrtValue.CreateTensorValueFromMemory + BindOutput) wrote
                // bbox coords on the first Run and then stopped populating any subsequent Run's
                // output — a pinning-lifetime bug somewhere between Memory<T>.Pin() and the
                // DirectML EP. BindOutputToDevice sidesteps it entirely.
                int expectedOutputLen = 1 * (4 + NUM_CLASSES) * NUM_DETECTIONS;
                if (_reusableInputArray == null
                    || _reusableInputArray.Length != 3 * IMAGE_SIZE * IMAGE_SIZE
                    || _reusableOutputArray == null
                    || _reusableOutputArray.Length != expectedOutputLen
                    || _ioBinding == null)
                {
                    _ioBinding?.Dispose();
                    _inputOrtValue?.Dispose();
                    _outputOrtValue?.Dispose();
                    _outputOrtValue = null;

                    _reusableInputArray  = new float[3 * IMAGE_SIZE * IMAGE_SIZE];
                    _reusableOutputArray = new float[expectedOutputLen];
                    _reusableOutputTensor = new DenseTensor<float>(_reusableOutputArray, new int[] { 1, 4 + NUM_CLASSES, NUM_DETECTIONS });

                    _inputOrtValue = OrtValue.CreateTensorValueFromMemory(
                        OrtMemoryInfo.DefaultInstance,
                        _reusableInputArray.AsMemory(),
                        new long[] { 1, 3, IMAGE_SIZE, IMAGE_SIZE });

                    string outName = (_outputNames != null && _outputNames.Count > 0) ? _outputNames[0] : "output0";
                    _ioBinding = _onnxModel!.CreateIoBinding();
                    _ioBinding.BindInput("images", _inputOrtValue);
                    _ioBinding.BindOutputToDevice(outName, OrtMemoryInfo.DefaultInstance);
                }

                Bitmap? frame = _captureManager.CaptureForInference(detectionBox, _reusableInputArray!, IMAGE_SIZE, _fcCollectData);

                if (_onnxModel == null || _ioBinding == null) return null;

                _onnxModel.RunWithBinding(_modeloptions, _ioBinding);
                _ioBinding.SynchronizeBoundOutputs(); // wait for GPU→CPU transfer

                // Copy ORT's allocated output into our managed buffer so downstream code
                // (PrepareKDTreeData) can read _reusableOutputTensor as before.
                using (var outputs = _ioBinding.GetOutputValues())
                {
                    var ortOut = outputs.ElementAt(0);
                    var span = ortOut.GetTensorDataAsSpan<float>();
                    if (span.Length == _reusableOutputArray!.Length)
                    {
                        span.CopyTo(_reusableOutputArray);
                    }
                    else
                    {
                        // Mismatch should be impossible given the static shape, but guard anyway.
                        int n = Math.Min(span.Length, _reusableOutputArray.Length);
                        span.Slice(0, n).CopyTo(_reusableOutputArray.AsSpan(0, n));
                    }
                }
                outputTensor = _reusableOutputTensor;

                LogInferenceHeartbeat();

                if (outputTensor == null)
                {
                    Log(LogLevel.Error, "Model inference returned null output tensor.", true, 2000);
                    SaveFrame(frame);
                    return null;
                }

                float fovMinX = (IMAGE_SIZE - _fcFovSize) / 2.0f;
                float fovMaxX = (IMAGE_SIZE + _fcFovSize) / 2.0f;
                float fovMinY = fovMinX;
                float fovMaxY = fovMaxX;

                List<Prediction> KDPredictions = PrepareKDTreeData(outputTensor, detectionBox, fovMinX, fovMaxX, fovMinY, fovMaxY);

                if (KDPredictions.Count == 0)
                {
                    SaveFrame(frame);
                    return null;
                }

                Prediction? bestCandidate = null;
                double bestDistSq = double.MaxValue;
                double center = IMAGE_SIZE / 2.0;

                foreach (var p in KDPredictions)
                {
                    var dx = p.CenterXTranslated * IMAGE_SIZE - center;
                    var dy = p.CenterYTranslated * IMAGE_SIZE - center;
                    double d2 = dx * dx + dy * dy; // dx^2 + dy^2

                    if (d2 < bestDistSq) { bestDistSq = d2; bestCandidate = p; }
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
                // No disposable result to clean up: pre-allocated outputs are reused every frame.
            }
        }

        private Prediction? HandleStickyAim(Prediction? bestCandidate, List<Prediction> KDPredictions)
        {
            if (!_fcStickyAim)
            {
                _currentTarget = bestCandidate;
                ResetStickyAimState();
                return bestCandidate;
            }

            // No detections available
            if (bestCandidate == null || KDPredictions == null || KDPredictions.Count == 0)
            {
                return HandleNoDetections();
            }

            _consecutiveFramesWithoutTarget = 0;

            // Screen center (where user is aiming)
            float screenCenterX = IMAGE_SIZE / 2f;
            float screenCenterY = IMAGE_SIZE / 2f;

            // STEP 1: Find what the user is aiming at (closest to crosshair)
            Prediction? aimTarget = null;
            float nearestToCrosshairDistSq = float.MaxValue;

            foreach (var candidate in KDPredictions)
            {
                float distSq = GetDistanceSq(candidate.ScreenCenterX, candidate.ScreenCenterY, screenCenterX, screenCenterY);
                if (distSq < nearestToCrosshairDistSq)
                {
                    nearestToCrosshairDistSq = distSq;
                    aimTarget = candidate;
                }
            }

            if (aimTarget == null)
            {
                return HandleNoDetections();
            }

            // No current target - acquire what user is aiming at
            if (_currentTarget == null)
            {
                return AcquireNewTarget(aimTarget);
            }

            // STEP 2: Is the aim target the SAME as our current target?
            float lastX = _currentTarget.ScreenCenterX;
            float lastY = _currentTarget.ScreenCenterY;
            float targetArea = _currentTarget.Rectangle.Width * _currentTarget.Rectangle.Height;
            float targetSize = MathF.Sqrt(targetArea);
            float sizeFactor = GetSizeFactor(targetArea);

            // Distance from aim target to our current target's last position
            float aimToCurrentDistSq = GetDistanceSq(aimTarget.ScreenCenterX, aimTarget.ScreenCenterY, lastX, lastY);

            // Tracking radius based on target size - larger targets have larger radius
            float trackingRadius = targetSize * 3f;
            float trackingRadiusSq = trackingRadius * trackingRadius;

            // Check size similarity
            float aimTargetArea = aimTarget.Rectangle.Width * aimTarget.Rectangle.Height;
            float sizeRatio = MathF.Min(targetArea, aimTargetArea) / MathF.Max(targetArea, aimTargetArea);

            // Is the aim target the same as our current target?
            // Same if: close to last position AND similar size
            bool isSameTarget = (aimToCurrentDistSq < trackingRadiusSq) && (sizeRatio > 0.5f);

            if (isSameTarget)
            {
                // User is still aiming at current target - update and continue
                _framesWithoutMatch = 0;
                UpdateVelocity(aimTarget, sizeFactor);
                _targetLockScore = Math.Min(MAX_LOCK_SCORE, _targetLockScore + LOCK_SCORE_GAIN);
                _currentTarget = aimTarget;
                return aimTarget;
            }

            // STEP 3: User is aiming at a DIFFERENT target
            // But we need hysteresis - don't switch on single-frame jitter
            _framesWithoutMatch++;

            // Quick switch if aim target is very close to crosshair (user clearly aiming at it)
            float stickyThreshold = (float)_fcStickyThreshold;
            bool aimTargetVeryCentered = nearestToCrosshairDistSq < (stickyThreshold * stickyThreshold * 0.25f);

            if (aimTargetVeryCentered || _framesWithoutMatch >= 3)
            {
                // User has clearly moved to new target - switch
                return AcquireNewTarget(aimTarget);
            }

            // Not ready to switch yet - return null to avoid flicking
            // (Don't return old target position, don't return new target position)
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetDistanceSq(float x1, float y1, float x2, float y2)
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
        private float GetSizeFactor(float targetArea)
        {
            // sizeFactor: 1.0 for large/close targets, up to 3.0 for small/distant targets
            // This makes distant targets more "sticky" to compensate for detection jitter
            float ratio = REFERENCE_TARGET_SIZE / Math.Max(targetArea, 100f);
            return Math.Clamp(ratio, 1.0f, 3.0f);
        }

        private Prediction? HandleNoDetections()
        {
            if (_currentTarget != null && ++_consecutiveFramesWithoutTarget <= MAX_FRAMES_WITHOUT_TARGET)
            {
                // Decay lock score during grace period
                _targetLockScore *= LOCK_SCORE_DECAY;

                // Reuse a single Prediction object during grace period — avoids heap allocation each miss frame.
                _gracePrediction.ScreenCenterX      = _currentTarget.ScreenCenterX + _lastTargetVelocityX * _consecutiveFramesWithoutTarget;
                _gracePrediction.ScreenCenterY      = _currentTarget.ScreenCenterY + _lastTargetVelocityY * _consecutiveFramesWithoutTarget;
                _gracePrediction.Rectangle          = _currentTarget.Rectangle;
                _gracePrediction.Confidence         = _currentTarget.Confidence * (1f - _consecutiveFramesWithoutTarget * 0.2f);
                _gracePrediction.ClassId            = _currentTarget.ClassId;
                _gracePrediction.ClassName          = _currentTarget.ClassName;
                _gracePrediction.CenterXTranslated  = _currentTarget.CenterXTranslated;
                _gracePrediction.CenterYTranslated  = _currentTarget.CenterYTranslated;
                return _gracePrediction;
            }

            ResetStickyAimState();
            return null;
        }

        private Prediction AcquireNewTarget(Prediction target)
        {
            _lastTargetVelocityX = 0f;
            _lastTargetVelocityY = 0f;
            _targetLockScore = LOCK_SCORE_GAIN; // Start with some lock score
            _framesWithoutMatch = 0;
            _currentTarget = target;
            return target;
        }

        private void UpdateVelocity(Prediction newTarget, float sizeFactor)
        {
            if (_currentTarget != null)
            {
                // EMA smoothing on velocity to reduce noise
                // Use heavier smoothing for smaller/distant targets (more weight on old velocity)
                // sizeFactor 1.0 -> 0.7/0.3, sizeFactor 3.0 -> 0.9/0.1
                float smoothing = Math.Clamp(0.6f + (sizeFactor * 0.1f), 0.7f, 0.9f);
                float newWeight = 1f - smoothing;

                float newVelX = newTarget.ScreenCenterX - _currentTarget.ScreenCenterX;
                float newVelY = newTarget.ScreenCenterY - _currentTarget.ScreenCenterY;
                _lastTargetVelocityX = _lastTargetVelocityX * smoothing + newVelX * newWeight;
                _lastTargetVelocityY = _lastTargetVelocityY * smoothing + newVelY * newWeight;
            }
        }

        private void ResetStickyAimState()
        {
            _currentTarget = null;
            _consecutiveFramesWithoutTarget = 0;
            _framesWithoutMatch = 0;
            _lastTargetVelocityX = 0f;
            _lastTargetVelocityY = 0f;
            _targetLockScore = 0f;
            ShalloePredictionV2.Reset(); // clear velocity history so stale velocity doesn't bleed into next target
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
            float invImageSize = 1.0f / imageSize; // precomputed reciprocal — replaces 2 divisions per detected box
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

                    // Check center point only — a close/large enemy whose box overflows the FOV
                    // boundary is still a valid target; filtering by corners drops them mid-track.
                    if (x_center < fovMinX || x_center > fovMaxX || y_center < fovMinY || y_center > fovMaxY) continue;

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

                if (x_center < fovMinX || x_center > fovMaxX || y_center < fovMinY || y_center > fovMaxY) continue;

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

            return KDpredictions;
        }

        #endregion AI Loop Functions

        // Logs one line every ~3 seconds with inference stats. Cheap enough to leave on;
        // doubles as proof the hot path is actually running.
        private void LogInferenceHeartbeat()
        {
            _hbFrameCounter++;
            long now = Environment.TickCount64;
            if (now - _hbLastTick < 3000) return;
            _hbLastTick = now;

            if (_reusableOutputArray == null) { Log(LogLevel.Info, "[hb] output array is null"); return; }
            if (_reusableInputArray == null)  { Log(LogLevel.Info, "[hb] input array is null"); return; }

            // Input-buffer scan — if this stays at 0/0, CaptureForInference never wrote to it
            // and inference is running on an all-zero tensor (producing the same deterministic
            // YOLO output every frame regardless of what's on screen).
            int inNonZero = 0;
            float inMax = 0f;
            for (int i = 0; i < _reusableInputArray.Length; i++)
            {
                float v = _reusableInputArray[i];
                if (v != 0f) inNonZero++;
                float a = Math.Abs(v);
                if (a > inMax) inMax = a;
            }

            int stride = 4 + NUM_CLASSES;

            // Layout A: [1, C, A] — channel-major (what the metadata claims)
            //   bbox: indices 0 .. 4*A-1
            //   conf: indices 4*A .. (4+K)*A-1
            int classStartA = 4 * NUM_DETECTIONS;
            int classEndA   = (4 + NUM_CLASSES) * NUM_DETECTIONS;

            float bboxMaxA = 0f;
            for (int i = 0; i < classStartA && i < _reusableOutputArray.Length; i++)
            {
                float a = Math.Abs(_reusableOutputArray[i]);
                if (a > bboxMaxA) bboxMaxA = a;
            }

            float classMaxA = 0f;
            int aboveHalfA = 0;
            for (int i = classStartA; i < classEndA && i < _reusableOutputArray.Length; i++)
            {
                float c = _reusableOutputArray[i];
                if (c > classMaxA) classMaxA = c;
                if (c > 0.5f) aboveHalfA++;
            }

            // Layout B: [1, A, C] — anchor-major (each anchor is 5 consecutive floats)
            //   conf for anchor a is at index a*stride + 4 (for 4 bbox + 1 class)
            float classMaxB = 0f;
            int aboveHalfB = 0;
            for (int a = 0; a < NUM_DETECTIONS && (a * stride + stride - 1) < _reusableOutputArray.Length; a++)
            {
                for (int c = 0; c < NUM_CLASSES; c++)
                {
                    float v = _reusableOutputArray[a * stride + 4 + c];
                    if (v > classMaxB) classMaxB = v;
                    if (v > 0.5f) aboveHalfB++;
                }
            }

            // Non-zero count across the whole buffer — 0 means ORT literally wrote nothing this frame.
            int nonZero = 0;
            for (int i = 0; i < _reusableOutputArray.Length; i++)
                if (_reusableOutputArray[i] != 0f) nonZero++;

            double conf = Convert.ToDouble(Dictionary.sliderSettings["AI Minimum Confidence"]) / 100.0;
            bool aimHeld = InputLogic.InputBindingManager.IsHoldingBinding("Aim Keybind")
                        || InputLogic.InputBindingManager.IsHoldingBinding("Second Aim Keybind");

            string sample = $"[{_reusableOutputArray[0]:F3}, {_reusableOutputArray[1]:F3}, {_reusableOutputArray[2]:F3}, {_reusableOutputArray[3]:F3}, {_reusableOutputArray[4]:F3}]";

            Log(LogLevel.Info,
                $"[hb] frames={_hbFrameCounter} " +
                $"INPUT(nonZero={inNonZero}/{_reusableInputArray.Length} max={inMax:F3}) " +
                $"OUT(nonZero={nonZero}/{_reusableOutputArray.Length} " +
                $"bboxMax={bboxMaxA:F2} classMax={classMaxA:F3} above0.5={aboveHalfA}) " +
                $"first5={sample} threshold={conf:F2} aimHeld={aimHeld} " +
                $"aimAssist={_fcAimAssist} showPlayer={_fcShowDetectedPlayer} autoTrig={_fcAutoTrigger}");
        }

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
            _ioBinding?.Dispose();
            _ioBinding = null;
            _inputOrtValue?.Dispose();
            _inputOrtValue = null;
            _outputOrtValue?.Dispose();
            _outputOrtValue = null;
            _reusableInputArray = null;
            _reusableOutputArray = null;
            _reusableOutputTensor = null;
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