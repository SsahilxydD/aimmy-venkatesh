using Venkatesh2.AILogic;
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
        private string _fC01 = "";
        private bool _fD02 = false;

        public Bitmap? _fS03 { get; private set; }
        public Bitmap? _fX04 { get; private set; }
        private ID3D11Device? _fV05;
        private IDXGIOutputDuplication? _fK06;
        private ID3D11Texture2D? _fT07;

        public readonly object _fL08 = new();
        public bool _fP09 { get; set; } = false;

        private int _fF0A = 0;
        private const int _fM0C = 5;

        private bool _fH0B = false;
        #endregion

        private static bool _opP()
        {
            int _t = Environment.TickCount;
            return (_t | (~_t)) == -1;
        }

        #region Handlers
        public CaptureManager()
        {
            DisplayManager.DisplayChanged += _mO01;
        }

        private void _mO01(object? sender, DisplayChangedEventArgs e)
        {
            lock (_fL08)
            {
                _fP09 = true;
                _fF0A = 0;
                _fH0B = false;
                _mR07();
            }
            LogManager.Log(LogLevel.Info, "Display change detected. DirectX resources will be reinitialized.");
        }

        public void _mH02()
        {
            uint _jv = unchecked((uint)(Environment.TickCount ^ 0xCAFE)) & 0u; _ = _jv;
            lock (_fL08)
            {
                if (!_fP09) return;

                try
                {
                    _mI03();
                    _fP09 = false;
                }
                catch (Exception)
                {
                }
            }
        }

        #endregion
        #region DirectX
        public void _mI03()
        {
            _mR07();
            _fH0B = false;
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
                    adapter.Dispose();
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
                    out _fV05);

                if (result.Failure || _fV05 == null)
                {
                    result = D3D11.D3D11CreateDevice(
                      targetAdapter,
                      DriverType.Unknown,
                      DeviceCreationFlags.None,
                      null,
                      out _fV05);

                    if (result.Failure || _fV05 == null)
                    {
                        LogManager.Log(LogLevel.Error, $"Failed to create D3D11 device: {result}", true, 6000);
                        throw new Exception($"Failed to create D3D11 device: {result}");
                    }
                }

                _fK06 = targetOutput1.DuplicateOutput(_fV05);
                _fF0A = 0;

                LogManager.Log(LogLevel.Info, "DirectX Desktop Duplication initialized successfully.");
            }
            catch (SharpGenException ex) when (ex.ResultCode == Vortice.DXGI.ResultCode.Unsupported || ex.HResult == unchecked((int)0x887A0004))
            {
                LogManager.Log(LogLevel.Error, $"DirectX Desktop Duplication not supported on this system: {ex.Message}", true, 6000);
                _fD02 = true;
                _mR07();

                Dictionary.dropdownState[_xB9D2._c1A] = _xB9D2._c2D;
                _fC01 = _xB9D2._c2D;

                LogManager.Log(LogLevel.Error, "DirectX Desktop Duplication not supported on this system. Switched to GDI+ capture.", true, 6000);
            }
            catch (Exception ex)
            {
                LogManager.Log(LogLevel.Error, $"Failed to initialize DirectX Desktop Duplication: {ex.Message}", true, 6000);
                _mR07();
                throw;
            }
        }

        private unsafe Bitmap? _mX04(
            Rectangle detectionBox,
            float[] floatDest,
            int imageSize,
            bool wantBitmap,
            bool applyMask)
        {
            if (!_opP()) return null;
            uint _jv = unchecked((uint)(detectionBox.Width ^ detectionBox.Height)) & 0u; _ = _jv;

            int w = detectionBox.Width;
            int h = detectionBox.Height;
            bool frameAcquired = false;
            IDXGIResource? desktopResource = null;

            try
            {
                lock (_fL08)
                {
                    if (_fP09)
                    {
                        _mI03();
                        _fP09 = false;
                    }
                }

                if (_fV05 == null || _fV05.ImmediateContext == null || _fK06 == null)
                {
                    _mI03();
                    if (_fV05 == null || _fV05.ImmediateContext == null || _fK06 == null)
                    {
                        lock (_fL08) { _fP09 = true; }
                        return wantBitmap ? _fX04 : null;
                    }
                }

                if (wantBitmap && (_fX04 == null || _fX04.Width != w || _fX04.Height != h))
                {
                    _fX04?.Dispose();
                    _fX04 = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                }

                if (_fT07 == null ||
                    _fT07.Description.Width != w ||
                    _fT07.Description.Height != h)
                {
                    _fT07?.Dispose();
                    _fT07 = _fV05.CreateTexture2D(new Texture2DDescription
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

                int timeout;
                if (!_fH0B)            timeout = 500;
                else if (_fF0A > 0)    timeout = 5;
                else                   timeout = 1;

                var result = _fK06!.AcquireNextFrame((uint)timeout, out var frameInfo, out desktopResource);

                if (result == Vortice.DXGI.ResultCode.WaitTimeout)
                {
                    _fF0A = 0;
                    return wantBitmap ? _fX04 : null;
                }
                else if (result == Vortice.DXGI.ResultCode.DeviceRemoved || result == Vortice.DXGI.ResultCode.AccessLost)
                {
                    _fF0A++;
                    if (_fF0A >= _fM0C)
                        lock (_fL08) { _fP09 = true; }
                    return wantBitmap ? _fX04 : null;
                }
                else if (result != Result.Ok)
                {
                    _fF0A++;
                    return wantBitmap ? _fX04 : null;
                }

                frameAcquired = true;
                _fF0A = 0;
                _fH0B = true;

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
                        _fV05.ImmediateContext.CopySubresourceRegion(
                            _fT07, 0,
                            (uint)(srcLeft - relativeDetectionLeft),
                            (uint)(srcTop - relativeDetectionTop),
                            0,
                            screenTexture, 0, box);
                    }
                    else
                    {
                        LogManager.Log(LogLevel.Warning, "No visible region to copy from DirectX capture.", true, 3000);
                        return wantBitmap ? _fX04 : null;
                    }

                    var map = _fV05.ImmediateContext.Map(_fT07, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                    try
                    {
                        byte* src = (byte*)map.DataPointer;
                        int srcStride = (int)map.RowPitch;

                        MathUtil._mP08(src, srcStride, floatDest, imageSize, applyMask);

                        if (wantBitmap && _fX04 != null)
                        {
                            BitmapData mapDest = _fX04.LockBits(
                                new Rectangle(0, 0, w, h),
                                ImageLockMode.WriteOnly,
                                _fX04.PixelFormat);
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
                                _fX04.UnlockBits(mapDest);
                            }
                        }
                    }
                    finally
                    {
                        _fV05.ImmediateContext.Unmap(_fT07, 0);
                    }

                    return wantBitmap ? _fX04 : null;
                }
            }
            catch (Exception e)
            {
                LogManager.Log(LogLevel.Error, $"DirectX capture error: {e.Message}");
                if (++_fF0A >= _fM0C)
                    lock (_fL08) { _fP09 = true; }
                return wantBitmap ? _fX04 : null;
            }
            finally
            {
                desktopResource?.Dispose();
                try
                {
                    if (frameAcquired && _fK06 != null)
                        _fK06.ReleaseFrame();
                }
                catch { }
            }
        }
        #endregion

        #region GDI
        public Bitmap _mG05(Rectangle _a)
        {
            uint _jv = unchecked((uint)(_a.Width ^ _a.Height)) & 0u; _ = _jv;

            if (_fV05 != null || _fK06 != null)
            {
                _mR07();
            }

            if (_fS03 == null || _fS03.Width != _a.Width || _fS03.Height != _a.Height)
            {
                _fS03?.Dispose();
                _fS03 = new Bitmap(_a.Width, _a.Height, PixelFormat.Format32bppArgb);
            }

            try
            {
                using (var g = Graphics.FromImage(_fS03))
                {
                    g.CopyFromScreen(
                        _a.Left,
                        _a.Top,
                        0, 0,
                        _a.Size,
                        CopyPixelOperation.SourceCopy
                    );

                    if (Convert.ToBoolean(Dictionary.toggleState[_xB9D2._c2B]))
                    {
                        int width = _fS03.Width / 2;
                        int height = _fS03.Height / 2;
                        int startY = _fS03.Height - height;

                        using var brush = new SolidBrush(System.Drawing.Color.Black);
                        g.FillRectangle(brush, 0, startY, width, height);
                    }
                }

                return _fS03;
            }
            catch (Exception ex)
            {
                LogManager.Log(LogLevel.Error, $"GDI+ screen capture failed: {ex.Message}");
                throw;
            }
        }
        #endregion

        public Bitmap? _mC06(Rectangle _a, float[] _b, int _c, bool _d)
        {
            bool _op = _opP();
            uint _jv = unchecked((uint)(_a.Width ^ _c)) & 0u; _ = _jv;

            string _sm = "";
            bool _mk = false;
            int _st = 0;
            while (_op)
            {
                switch (_st)
                {
                    case 0:
                        _sm = Convert.ToString(Dictionary.dropdownState[_xB9D2._c1A]) ?? _xB9D2._c2C;
                        if (_fD02 && _sm == _xB9D2._c2C)
                        {
                            Dictionary.dropdownState[_xB9D2._c1A] = _xB9D2._c2D;
                            _sm = _xB9D2._c2D;
                            _fC01 = _xB9D2._c2D;
                        }
                        _st = 1; break;

                    case 1:
                        if (_sm != _fC01)
                        {
                            _fS03?.Dispose();
                            _fS03 = null;
                            _fX04?.Dispose();
                            _fX04 = null;
                            _fC01 = _sm;

                            if (_sm == _xB9D2._c2D) _mR07();
                            else _mI03();
                        }
                        _st = 2; break;

                    case 2:
                        _mk = Convert.ToBoolean(Dictionary.toggleState[_xB9D2._c2B]);
                        _st = 3; break;

                    case 3:
                        if (_sm == _xB9D2._c2C && !_fD02)
                        {
                            return _mX04(_a, _b, _c, _d, _mk);
                        }
                        _st = 4; break;

                    case 4:
                        Bitmap _bmp = _mG05(_a);
                        MathUtil._mB07(_bmp, _b, _c);
                        return _bmp;
                }
            }
            return null;
        }

        #region dispose
        public void _mR07()
        {
            lock (_fL08)
            {
                try
                {
                    if (_fK06 != null)
                    {
                        try
                        {
                            _fK06.ReleaseFrame();
                        }
                        catch { }
                    }

                    _fK06?.Dispose();
                    _fT07?.Dispose();
                    _fV05?.Dispose();
                    _fX04?.Dispose();

                    _fK06 = null;
                    _fT07 = null;
                    _fV05 = null;
                    _fX04 = null;
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogLevel.Error, $"Error disposing DXGI resources: {ex.Message}");
                }
            }
        }
        public void Dispose()
        {
            DisplayManager.DisplayChanged -= _mO01;
            _mR07();
            _fS03?.Dispose();
        }
        #endregion
    }
}
