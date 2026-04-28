using Aimmy2.Class;
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
        private string _f0020 = "";
        private bool _f0021 = false;
        private bool _f0022 = false;
        private static int _dK4 = 0x2C9F;
        private static long _dK5 = 0L;

        
        public Bitmap? screenCaptureBitmap { get; private set; }
        public Bitmap? directXBitmap { get; private set; }
        private ID3D11Device? _f0023;
        private IDXGIOutputDuplication? _f0024;
        private ID3D11Texture2D? _f0025;

        
        private Bitmap? _f0026;
        private Rectangle _f0027;
        private DateTime _f0028 = DateTime.MinValue;
        private readonly TimeSpan _f0029 = TimeSpan.FromMilliseconds(15); 

        
        public readonly object _displayLock = new();
        public bool _displayChangesPending { get; set; } = false;

        
        private int _f002A = 0;
        private const int _f002B = 5;

        
        private bool _f002C = true;
        private int _f002D = 0;
        private int _f002E = 0;
        public CaptureManager()
        {
            
            DisplayManager.DisplayChanged += _m0023;
        }

        private void _m0023(object? sender, DisplayChangedEventArgs e)
        {
            lock (_displayLock)
            {
                _displayChangesPending = true;
                _f002A = 0;
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
                catch (Exception ex)
                {

                }
            }
        }
        public void InitializeDxgiDuplication()
        {
            DisposeDxgiResources();
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
                    FeatureLevel.Level_12_2, 
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

                
                var result = D3D11.D3D11CreateDevice(
                    targetAdapter,
                    DriverType.Unknown,
                    DeviceCreationFlags.None,
                    featureLevels,
                    out _f0023);

                if (result.Failure || _f0023 == null)
                {
                    result = D3D11.D3D11CreateDevice(
                      targetAdapter,
                      DriverType.Unknown,
                      DeviceCreationFlags.None,
                      null,
                      out _f0023);

                    if (result.Failure || _f0023 == null)
                    {
                        LogManager.Log(LogLevel.Error, $"Failed to create D3D11 device: {result}", true, 6000);
                        throw new Exception($"Failed to create D3D11 device: {result}");
                    }
                }

                
                _f0024 = targetOutput1.DuplicateOutput(_f0023);
                _f002A = 0; 

                LogManager.Log(LogLevel.Info, "DirectX Desktop Duplication initialized successfully.");
            }
            catch (SharpGenException ex) when (ex.ResultCode == Vortice.DXGI.ResultCode.Unsupported || ex.HResult == unchecked((int)0x887A0004))
            {
                LogManager.Log(LogLevel.Error, $"DirectX Desktop Duplication not supported on this system: {ex.Message}", true, 6000);
                _f0021 = true;
                DisposeDxgiResources();

                Dictionary.dropdownState[_xB9D2._c1E] = _xB9D2._c27;
                _f0020 = _xB9D2._c27;

                LogManager.Log(LogLevel.Error, "DirectX Desktop Duplication not supported on this system. Switched to GDI+ capture.", true, 6000);
            }
            catch (Exception ex)
            {
                LogManager.Log(LogLevel.Error, $"Failed to initialize DirectX Desktop Duplication: {ex.Message}", true, 6000);
                DisposeDxgiResources();
                throw;
            }
        }
        private Bitmap? _m0020(Rectangle detectionBox)
        {
            int w = detectionBox.Width;
            int h = detectionBox.Height;
            bool frameAcquired = false;
            IDXGIResource? desktopResource = null;

            Bitmap? resultBitmap = null;

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

                
                if (_f0023 == null || _f0023.ImmediateContext == null || _f0024 == null)
                {
                    InitializeDxgiDuplication();
                    if (_f0023 == null || _f0023.ImmediateContext == null || _f0024 == null)
                    {
                        lock (_displayLock) { _displayChangesPending = true; }
                        return _m0022(detectionBox);
                    }
                }

                if (directXBitmap == null || directXBitmap.Width != w || directXBitmap.Height != h)
                {
                    directXBitmap?.Dispose();
                    directXBitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                }

                
                if (_f0025 == null ||
                    _f0025.Description.Width != w ||
                    _f0025.Description.Height != h)
                {
                    _f0025?.Dispose();
                    _f0025 = _f0023.CreateTexture2D(new Texture2DDescription
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

                int timeout = _f002A > 0 ? 5 : 1;
                var result = _f0024!.AcquireNextFrame((uint)timeout, out var frameInfo, out desktopResource);

                if (result == Vortice.DXGI.ResultCode.WaitTimeout)
                {
                    
                    _f002A = 0; 
                    return _m0022(detectionBox);
                }
                else if (result == Vortice.DXGI.ResultCode.DeviceRemoved || result == Vortice.DXGI.ResultCode.AccessLost)
                { 
                    _f002A++;

                    if (_f002A >= _f002B)
                        lock (_displayLock) { _displayChangesPending = true; }

                    return _m0022(detectionBox);
                }
                else if (result != Result.Ok)
                {
                    
                    _f002A++;
                    return _m0022(detectionBox);
                }

                frameAcquired = true;
                _f002A = 0; 

                using (var screenTexture = desktopResource.QueryInterface<ID3D11Texture2D>())
                {
                    var displayBounds = new Rectangle(DisplayManager.ScreenLeft,
                                                  DisplayManager.ScreenTop,
                                                  DisplayManager.ScreenWidth,
                                                  DisplayManager.ScreenHeight);

                    
                    
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

                        _f0023.ImmediateContext.CopySubresourceRegion(
                               _f0025, 0,
                               (uint)(srcLeft - relativeDetectionLeft),
                               (uint)(srcTop - relativeDetectionTop),
                               0,
                               screenTexture, 0, box);
                    }
                    else
                    {
                        LogManager.Log(LogLevel.Warning, "No visible region to copy from DirectX capture.", true, 3000);
                        return _m0022(detectionBox);
                    }
                    var map = _f0023.ImmediateContext.Map(_f0025, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                    var boundsRect = new Rectangle(0, 0, w, h);
                    BitmapData? mapDest = directXBitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, directXBitmap.PixelFormat);

                    try
                    {
                        unsafe
                        {
                            byte* src = (byte*)map.DataPointer;
                            byte* dst = (byte*)mapDest.Scan0;
                            int srcStride = (int)map.RowPitch;
                            int dstStride = mapDest.Stride;

                            int copyBytesPerRow = Math.Min(srcStride, dstStride);
                            for (int y = 0; y < h; y++)
                            {
                                Buffer.MemoryCopy(src, dst, dstStride, copyBytesPerRow);
                                src += srcStride;
                                dst += dstStride;
                            }

                            if (Dictionary.toggleState[_xB9D2._c0D]) 
                            {
                                int width = w / 2;
                                int height = h / 2;
                                int startY = h - height;

                                byte* basePtr = (byte*)mapDest.Scan0;
                                for (int y = startY; y < h; y++)
                                {
                                    byte* rowPtr = basePtr + (y * dstStride);
                                    for (int x = 0; x < width; x++)
                                    {
                                        int pixelOffset = x * 4;
                                        
                                        rowPtr[pixelOffset + 0] = 0;   
                                        rowPtr[pixelOffset + 1] = 0;   
                                        rowPtr[pixelOffset + 2] = 0;   
                                        rowPtr[pixelOffset + 3] = 255; 
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        directXBitmap.UnlockBits(mapDest);
                        _f0023.ImmediateContext.Unmap(_f0025, 0);
                    }

                    resultBitmap = (Bitmap)directXBitmap.Clone();
                    _m0021(resultBitmap, detectionBox);
                    return resultBitmap;
                }
            }
            catch (Exception e)
            {
                LogManager.Log(LogLevel.Error, $"DirectX capture error: {e.Message}");

                if (++_f002A >= _f002B)
                    lock (_displayLock) { _displayChangesPending = true; }

                return _m0022(detectionBox);
            }
            finally
            {
                desktopResource?.Dispose();
                try
                {
                    if (frameAcquired && _f0024 != null)
                    {
                        _f0024.ReleaseFrame();
                    }
                }
                catch { }

            }
        }

        private void _m0021(Bitmap frame, Rectangle bounds)
        {
            if (_f0026 == null ||
                !_f0027.Equals(bounds) ||
                DateTime.Now - _f0028 > _f0029)
            {
                _f0026?.Dispose();
                _f0026 = (Bitmap)frame.Clone();
                _f0027 = bounds;
            }
            _f0028 = DateTime.Now;
        }

        private Bitmap? _m0022(Rectangle detectionBox)
        {
            if (_f0026 != null &&
                _f0027.Equals(detectionBox) &&
                DateTime.Now - _f0028 <= _f0029)
            {
                return (Bitmap)_f0026.Clone();
            }
            return null;
        }
        public Bitmap GDIScreen(Rectangle detectionBox)
        {
            if (_f0023 != null || _f0024 != null)
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

                    if (Dictionary.toggleState[_xB9D2._c0D])
                    {
                        int width = screenCaptureBitmap.Width / 2;
                        int height = screenCaptureBitmap.Height / 2;
                        int startY = screenCaptureBitmap.Height - height;

                        using var brush = new SolidBrush(System.Drawing.Color.Black);
                        g.FillRectangle(brush, 0, startY, width, height);
                    }
                }

                
                
                
                return (Bitmap)screenCaptureBitmap.Clone();
            }
            catch (Exception ex)
            {
                LogManager.Log(LogLevel.Error, $"GDI+ screen capture failed: {ex.Message}");
                throw;
            }
        }

        public Bitmap? ScreenGrab(Rectangle detectionBox)
        {
            if (!_xB9D2._opP()) { _dK5 = _dK4 ^ DateTime.UtcNow.Ticks; return null; }
            string selectedMethod = Dictionary.dropdownState[_xB9D2._c1E];

            
            if (_f0021 && selectedMethod == _xB9D2._c26)
            {
                Dictionary.dropdownState[_xB9D2._c1E] = _xB9D2._c27;
                selectedMethod = _xB9D2._c27;
                _f0020 = _xB9D2._c27;
            }

            
            if (selectedMethod != _f0020)
            {
                
                screenCaptureBitmap?.Dispose();
                screenCaptureBitmap = null;

                directXBitmap?.Dispose();
                directXBitmap = null;

                _f0020 = selectedMethod;
                _f0022 = false; 

                
                if (selectedMethod == _xB9D2._c27)
                {
                    DisposeDxgiResources();
                }
                else
                {
                    InitializeDxgiDuplication();
                }
            }

            if (selectedMethod == _xB9D2._c26 && !_f0021)
            {
                return _m0020(detectionBox);
            }
            else
            {
                return GDIScreen(detectionBox);
            }
        }
        public void DisposeDxgiResources()
        {
            lock (_displayLock)
            {
                try
                {

                    
                    if (_f0024 != null)
                    {
                        try
                        {
                            _f0024.ReleaseFrame();
                        }
                        catch { }
                    }

                    _f0024?.Dispose();
                    _f0025?.Dispose();
                    _f0023?.Dispose();
                    _f0026?.Dispose();
                    directXBitmap?.Dispose();

                    _f0024 = null;
                    _f0025 = null;
                    _f0023 = null;
                    _f0026 = null;

                    
                    
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogLevel.Error, $"Error disposing DXGI resources: {ex.Message}");
                }
            }
        }
        public void Dispose()
        {
            DisplayManager.DisplayChanged -= _m0023;
            DisposeDxgiResources();
            screenCaptureBitmap?.Dispose();
        }
    }
}