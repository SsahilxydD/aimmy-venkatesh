using Venkatesh2.Class;
using Other;
using SharpGen.Runtime;
using System.Drawing;
using System.Drawing.Imaging;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using LogLevel = Other.LogManager.LogLevel;

namespace AILogic
{
    internal class CaptureManager
    {
        #region Variables
        private string _currentCaptureMethod = ""; // Track current method
        private bool _directXFailedPermanently = false; // Track if DirectX failed with unsupported error

        // Capturing
        public Bitmap? screenCaptureBitmap { get; private set; }
        public Bitmap? directXBitmap { get; private set; }
        private ID3D11Device? _dxDevice;
        private IDXGIOutputDuplication? _deskDuplication;
        private ID3D11Texture2D? _stagingTex;

        // Display change handling
        public readonly object _displayLock = new();
        public bool _displayChangesPending { get; set; } = false;

        // Performance tracking
        private int _consecutiveFailures = 0;
        private const int MAX_CONSECUTIVE_FAILURES = 5;

        // Capture-state tracking — when false the input float tensor is still zero-initialised,
        // so we must block until AcquireNextFrame succeeds at least once.
        private bool _hasProducedFrame = false;
        #endregion
        #region Handlers
        public CaptureManager()
        {
            // Subscribe to display changes FIRST
            DisplayManager.DisplayChanged += OnDisplayChanged;
        }

        private void OnDisplayChanged(object? sender, DisplayChangedEventArgs e)
        {
            lock (_displayLock)
            {
                _displayChangesPending = true;
                _consecutiveFailures = 0;
                _hasProducedFrame = false; // DXGI is about to be rebuilt — force a blocking wait again.
                DisposeDxgiResources();
            }
            LogManager.Log(LogLevel.Info, "Display change detected. DirectX resources will be reinitialized.");
        }

        public void HandlePendingDisplayChanges()
        {
            lock (_displayLock)
            {
                if (!_displayChangesPending) return;

                try
                {
                    InitializeDxgiDuplication();
                    _displayChangesPending = false;
                }
                catch (Exception)
                {
                }
            }
        }

        #endregion
        #region DirectX
        public void InitializeDxgiDuplication()
        {
            DisposeDxgiResources();
            // Fresh duplication — first AcquireNextFrame must block until a frame lands so the
            // input tensor is populated before the AI loop reads it.
            _hasProducedFrame = false;
            try
            {
                var currentDisplay = DisplayManager.CurrentDisplay;
                if (currentDisplay == null)
                {
                    LogManager.Log(LogLevel.Error, "No current display available. DisplayManager may not be initialized.");
                    throw new InvalidOperationException("No current display available. DisplayManager may not be initialized.");
                }

                using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
                IDXGIOutput1? targetOutput1 = null;
                IDXGIAdapter1? targetAdapter = null;
                bool foundTarget = false;

                for (uint adapterIndex = 0;
                    factory.EnumAdapters1(adapterIndex, out var adapter).Success;
                    adapterIndex++)
                {
                    LogManager.Log(LogLevel.Info, $"Checking Adapter {adapterIndex}: {adapter.Description.Description.TrimEnd('\0')}");

                    for (uint outputIndex = 0;
                        adapter.EnumOutputs(outputIndex, out var output).Success;
                        outputIndex++)
                    {
                        using (output)
                        {
                            var output1 = output.QueryInterface<IDXGIOutput1>();
                            var outputDesc = output1.Description;
                            var outputBounds = new Vortice.Mathematics.Rect(
                                outputDesc.DesktopCoordinates.Left,
                                outputDesc.DesktopCoordinates.Top,
                                outputDesc.DesktopCoordinates.Right - outputDesc.DesktopCoordinates.Left,
                                outputDesc.DesktopCoordinates.Bottom - outputDesc.DesktopCoordinates.Top);
                            LogManager.Log(LogLevel.Info, $"Found Output {outputIndex}: DeviceName = '{outputDesc.DeviceName.TrimEnd('\0')}', Bounds = {outputBounds}");

                            // Try different matching strategies
                            bool nameMatch = currentDisplay?.DeviceName != null && outputDesc.DeviceName.TrimEnd('\0') == currentDisplay.DeviceName.TrimEnd('\0');
                            bool boundsMatch = currentDisplay?.Bounds != null && outputBounds.Equals(currentDisplay.Bounds);

                            if (nameMatch || boundsMatch)
                            {
                                targetOutput1 = output1;
                                targetAdapter = adapter;
                                foundTarget = true;
                                break;
                            }
                            output1.Dispose();
                        }
                    }

                    if (foundTarget) break;
                }

                // Fallback to specific display index if not found
                if (!foundTarget)
                {
                    int targetIndex = currentDisplay?.Index ?? 0;
                    int currentIndex = 0;

                    for (uint adapterIndex = 0;
                        factory.EnumAdapters1(adapterIndex, out var adapter).Success;
                        adapterIndex++)
                    {
                        for (uint outputIndex = 0;
                            adapter.EnumOutputs(outputIndex, out var output).Success;
                            outputIndex++)
                        {
                            if (currentIndex == targetIndex)
                            {
                                LogManager.Log(LogLevel.Warning, $"Could not match display by name or bounds. Found a fallback index, {targetIndex}.");
                                targetOutput1 = output.QueryInterface<IDXGIOutput1>();
                                targetAdapter = adapter;
                                foundTarget = true;
                                break;
                            }
                            currentIndex++;
                            output.Dispose();
                        }

                        if (foundTarget)
                            break;
                        adapter.Dispose();
                    }
                }

                if (targetAdapter == null || targetOutput1 == null)
                {
                    LogManager.Log(LogLevel.Error, "No suitable display output found for DirectX capture.", true, 6000);
                    throw new Exception("No suitable display output found");
                }

                FeatureLevel[] featureLevels = {
                    FeatureLevel.Level_12_2, // 50 series support
                    FeatureLevel.Level_12_1,
                    FeatureLevel.Level_12_0,
                    FeatureLevel.Level_11_1,
                    FeatureLevel.Level_11_0,
                    FeatureLevel.Level_10_1,
                    FeatureLevel.Level_10_0,
                    FeatureLevel.Level_9_3,
                    FeatureLevel.Level_9_2,
                    FeatureLevel.Level_9_1
                };

                // Create D3D11 device
                var result = D3D11.D3D11CreateDevice(
                    targetAdapter,
                    DriverType.Unknown,
                    DeviceCreationFlags.None,
                    featureLevels,
                    out _dxDevice);

                if (result.Failure || _dxDevice == null)
                {
                    result = D3D11.D3D11CreateDevice(
                      targetAdapter,
                      DriverType.Unknown,
                      DeviceCreationFlags.None,
                      null,
                      out _dxDevice);

                    if (result.Failure || _dxDevice == null)
                    {
                        LogManager.Log(LogLevel.Error, $"Failed to create D3D11 device: {result}", true, 6000);
                        throw new Exception($"Failed to create D3D11 device: {result}");
                    }
                }

                // Create desktop duplication
                _deskDuplication = targetOutput1.DuplicateOutput(_dxDevice);
                _consecutiveFailures = 0; //reset on success

                LogManager.Log(LogLevel.Info, "DirectX Desktop Duplication initialized successfully.");
            }
            catch (SharpGenException ex) when (ex.ResultCode == Vortice.DXGI.ResultCode.Unsupported || ex.HResult == unchecked((int)0x887A0004))
            {
                LogManager.Log(LogLevel.Error, $"DirectX Desktop Duplication not supported on this system: {ex.Message}", true, 6000);
                _directXFailedPermanently = true;
                DisposeDxgiResources();

                Dictionary.dropdownState["Screen Capture Method"] = "GDI+";
                _currentCaptureMethod = "GDI+";

                LogManager.Log(LogLevel.Error, "DirectX Desktop Duplication not supported on this system. Switched to GDI+ capture.", true, 6000);
            }
            catch (Exception ex)
            {
                LogManager.Log(LogLevel.Error, $"Failed to initialize DirectX Desktop Duplication: {ex.Message}", true, 6000);
                DisposeDxgiResources();
                throw;
            }
        }
        // DirectX fast path: write BGRA pixels from the GPU staging map straight to the model's planar-RGB
        // float tensor, skipping the intermediate managed Bitmap whenever the caller doesn't need it
        // (Bitmap is only requested when "Collect Data While Playing" is on).
        private unsafe Bitmap? DirectXToFloat(
            Rectangle detectionBox,
            float[] floatDest,
            int imageSize,
            bool wantBitmap,
            bool applyMask)
        {
            int w = detectionBox.Width;
            int h = detectionBox.Height;
            bool frameAcquired = false;
            IDXGIResource? desktopResource = null;

            try
            {
                lock (_displayLock)
                {
                    if (_displayChangesPending)
                    {
                        InitializeDxgiDuplication();
                        _displayChangesPending = false;
                    }
                }

                if (_dxDevice == null || _dxDevice.ImmediateContext == null || _deskDuplication == null)
                {
                    InitializeDxgiDuplication();
                    if (_dxDevice == null || _dxDevice.ImmediateContext == null || _deskDuplication == null)
                    {
                        lock (_displayLock) { _displayChangesPending = true; }
                        return wantBitmap ? directXBitmap : null;
                    }
                }

                if (wantBitmap && (directXBitmap == null || directXBitmap.Width != w || directXBitmap.Height != h))
                {
                    directXBitmap?.Dispose();
                    directXBitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                }

                if (_stagingTex == null ||
                    _stagingTex.Description.Width != w ||
                    _stagingTex.Description.Height != h)
                {
                    _stagingTex?.Dispose();
                    _stagingTex = _dxDevice.CreateTexture2D(new Texture2DDescription
                    {
                        Width = (uint)w,
                        Height = (uint)h,
                        MipLevels = 1,
                        ArraySize = 1,
                        Format = Format.B8G8R8A8_UNorm,
                        SampleDescription = new(1, 0),
                        Usage = ResourceUsage.Staging,
                        CPUAccessFlags = CpuAccessFlags.Read,
                        BindFlags = BindFlags.None
                    });
                }

                // At 144 FPS we poll faster than the monitor refreshes (~6.9 ms vs 16.7 ms at 60 Hz),
                // so most AcquireNextFrame calls will WaitTimeout and reuse the previous capture —
                // that's fine once we HAVE a previous capture. Before the first successful capture
                // the float tensor is still zeros, and running inference on zeros produces a
                // deterministic bogus output with no relation to the screen. Block on the first
                // call until a real frame lands.
                int timeout;
                if (!_hasProducedFrame)       timeout = 500; // first frame after (re)init — wait
                else if (_consecutiveFailures > 0) timeout = 5;
                else                          timeout = 1;

                var result = _deskDuplication!.AcquireNextFrame((uint)timeout, out var frameInfo, out desktopResource);

                if (result == Vortice.DXGI.ResultCode.WaitTimeout)
                {
                    // No new frame — float tensor still holds the previous frame, reuse it.
                    _consecutiveFailures = 0;
                    return wantBitmap ? directXBitmap : null;
                }
                else if (result == Vortice.DXGI.ResultCode.DeviceRemoved || result == Vortice.DXGI.ResultCode.AccessLost)
                {
                    _consecutiveFailures++;
                    if (_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
                        lock (_displayLock) { _displayChangesPending = true; }
                    return wantBitmap ? directXBitmap : null;
                }
                else if (result != Result.Ok)
                {
                    _consecutiveFailures++;
                    return wantBitmap ? directXBitmap : null;
                }

                frameAcquired = true;
                _consecutiveFailures = 0;
                _hasProducedFrame = true;

                using (var screenTexture = desktopResource.QueryInterface<ID3D11Texture2D>())
                {
                    int relativeDetectionLeft = detectionBox.Left - DisplayManager.ScreenLeft;
                    int relativeDetectionTop = detectionBox.Top - DisplayManager.ScreenTop;
                    int relativeDetectionRight = relativeDetectionLeft + detectionBox.Width;
                    int relativeDetectionBottom = relativeDetectionTop + detectionBox.Height;

                    int srcLeft = Math.Max(relativeDetectionLeft, 0);
                    int srcTop = Math.Max(relativeDetectionTop, 0);
                    int srcRight = Math.Min(relativeDetectionRight, DisplayManager.ScreenWidth);
                    int srcBottom = Math.Min(relativeDetectionBottom, DisplayManager.ScreenHeight);

                    if (srcRight > srcLeft && srcBottom > srcTop)
                    {
                        var box = new Box(srcLeft, srcTop, 0, srcRight, srcBottom, 1);
                        _dxDevice.ImmediateContext.CopySubresourceRegion(
                            _stagingTex, 0,
                            (uint)(srcLeft - relativeDetectionLeft),
                            (uint)(srcTop - relativeDetectionTop),
                            0,
                            screenTexture, 0, box);
                    }
                    else
                    {
                        LogManager.Log(LogLevel.Warning, "No visible region to copy from DirectX capture.", true, 3000);
                        return wantBitmap ? directXBitmap : null;
                    }

                    var map = _dxDevice.ImmediateContext.Map(_stagingTex, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                    try
                    {
                        byte* src = (byte*)map.DataPointer;
                        int srcStride = (int)map.RowPitch;

                        // One pass: BGRA staging map -> planar RGB float tensor.
                        MathUtil.BgraPointerToFloatArray(src, srcStride, floatDest, imageSize, applyMask);

                        // Only materialize the managed Bitmap when a downstream consumer needs it.
                        if (wantBitmap && directXBitmap != null)
                        {
                            BitmapData mapDest = directXBitmap.LockBits(
                                new Rectangle(0, 0, w, h),
                                ImageLockMode.WriteOnly,
                                directXBitmap.PixelFormat);
                            try
                            {
                                byte* dst = (byte*)mapDest.Scan0;
                                int dstStride = mapDest.Stride;
                                int copyBytesPerRow = Math.Min(srcStride, dstStride);
                                for (int y = 0; y < h; y++)
                                {
                                    Buffer.MemoryCopy(
                                        src + (long)y * srcStride,
                                        dst + (long)y * dstStride,
                                        dstStride,
                                        copyBytesPerRow);
                                }

                                if (applyMask)
                                {
                                    int halfW = w / 2;
                                    int halfH = h / 2;
                                    int startY = h - halfH;
                                    byte* basePtr = (byte*)mapDest.Scan0;
                                    for (int y = startY; y < h; y++)
                                    {
                                        byte* rowPtr = basePtr + (y * dstStride);
                                        for (int x = 0; x < halfW; x++)
                                        {
                                            int off = x * 4;
                                            rowPtr[off + 0] = 0;
                                            rowPtr[off + 1] = 0;
                                            rowPtr[off + 2] = 0;
                                            rowPtr[off + 3] = 255;
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                directXBitmap.UnlockBits(mapDest);
                            }
                        }
                    }
                    finally
                    {
                        _dxDevice.ImmediateContext.Unmap(_stagingTex, 0);
                    }

                    return wantBitmap ? directXBitmap : null;
                }
            }
            catch (Exception e)
            {
                LogManager.Log(LogLevel.Error, $"DirectX capture error: {e.Message}");
                if (++_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
                    lock (_displayLock) { _displayChangesPending = true; }
                return wantBitmap ? directXBitmap : null;
            }
            finally
            {
                desktopResource?.Dispose();
                try
                {
                    if (frameAcquired && _deskDuplication != null)
                        _deskDuplication.ReleaseFrame();
                }
                catch { }
            }
        }
        #endregion

        #region GDI
        public Bitmap GDIScreen(Rectangle detectionBox)
        {
            if (_dxDevice != null || _deskDuplication != null)
            {
                DisposeDxgiResources();
            }

            if (screenCaptureBitmap == null || screenCaptureBitmap.Width != detectionBox.Width || screenCaptureBitmap.Height != detectionBox.Height)
            {
                screenCaptureBitmap?.Dispose();
                screenCaptureBitmap = new Bitmap(detectionBox.Width, detectionBox.Height, PixelFormat.Format32bppArgb);
            }

            try
            {
                using (var g = Graphics.FromImage(screenCaptureBitmap))
                {
                    g.CopyFromScreen(
                        detectionBox.Left,
                        detectionBox.Top,
                        0, 0,
                        detectionBox.Size,
                        CopyPixelOperation.SourceCopy
                    );

                    if (Convert.ToBoolean(Dictionary.toggleState["Third Person Support"]))
                    {
                        int width = screenCaptureBitmap.Width / 2;
                        int height = screenCaptureBitmap.Height / 2;
                        int startY = screenCaptureBitmap.Height - height;

                        using var brush = new SolidBrush(System.Drawing.Color.Black);
                        g.FillRectangle(brush, 0, startY, width, height);
                    }
                }

                // AI loop is single-threaded — bitmap is consumed before the next capture begins,
                // so we can return the owned buffer directly.
                return screenCaptureBitmap;
            }
            catch (Exception ex)
            {
                LogManager.Log(LogLevel.Error, $"GDI+ screen capture failed: {ex.Message}");
                throw;
            }
        }
        #endregion

        // Capture the detection box and, in the same pass, fill the model's planar-RGB float tensor.
        // When wantBitmap is false the DirectX path skips materializing a managed Bitmap entirely.
        public Bitmap? CaptureForInference(Rectangle detectionBox, float[] floatDest, int imageSize, bool wantBitmap)
        {
            string selectedMethod = Convert.ToString(Dictionary.dropdownState["Screen Capture Method"]) ?? "DirectX";

            if (_directXFailedPermanently && selectedMethod == "DirectX")
            {
                Dictionary.dropdownState["Screen Capture Method"] = "GDI+";
                selectedMethod = "GDI+";
                _currentCaptureMethod = "GDI+";
            }

            if (selectedMethod != _currentCaptureMethod)
            {
                screenCaptureBitmap?.Dispose();
                screenCaptureBitmap = null;
                directXBitmap?.Dispose();
                directXBitmap = null;
                _currentCaptureMethod = selectedMethod;

                if (selectedMethod == "GDI+") DisposeDxgiResources();
                else InitializeDxgiDuplication();
            }

            bool applyMask = Convert.ToBoolean(Dictionary.toggleState["Third Person Support"]);

            if (selectedMethod == "DirectX" && !_directXFailedPermanently)
            {
                return DirectXToFloat(detectionBox, floatDest, imageSize, wantBitmap, applyMask);
            }

            // GDI path: Graphics.FromImage needs a Bitmap, so we always produce one.
            // Third-person mask is applied to the bitmap via FillRectangle, so BitmapToFloatArray
            // reads the already-masked pixels and the float tensor matches.
            Bitmap bmp = GDIScreen(detectionBox);
            MathUtil.BitmapToFloatArrayInPlace(bmp, floatDest, imageSize);
            return bmp;
        }

        #region dispose
        public void DisposeDxgiResources()
        {
            lock (_displayLock)
            {
                try
                {

                    // Try to release any pending frame
                    if (_deskDuplication != null)
                    {
                        try
                        {
                            _deskDuplication.ReleaseFrame();
                        }
                        catch { }
                    }

                    _deskDuplication?.Dispose();
                    _stagingTex?.Dispose();
                    _dxDevice?.Dispose();
                    directXBitmap?.Dispose();

                    _deskDuplication = null;
                    _stagingTex = null;
                    _dxDevice = null;
                    directXBitmap = null;
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogLevel.Error, $"Error disposing DXGI resources: {ex.Message}");
                }
            }
        }
        public void Dispose()
        {
            DisplayManager.DisplayChanged -= OnDisplayChanged;
            DisposeDxgiResources();
            screenCaptureBitmap?.Dispose();
        }
        #endregion
    }
}