using Venkatesh2.Class;
using Venkatesh2.MouseMovementLibraries.GHubSupport;
using Class;
using MouseMovementLibraries.ddxoftSupport;
using MouseMovementLibraries.RazerSupport;
using MouseMovementLibraries.SendInputSupport;
using System.Drawing;
using System.Runtime.InteropServices;

namespace InputLogic
{
    internal class MouseManager
    {
        private static readonly double _sW = WinAPICaller.ScreenWidth;
        private static readonly double _sH = WinAPICaller.ScreenHeight;

        // Click tracking: TickCount64 (ms) instead of DateTime.UtcNow — no kernel clock syscall.
        private static long _lCT = 0;
        private static bool _iSp = false;

        private static readonly uint _eLD = (uint)((0x1 << 1));
        private static readonly uint _eLU = (uint)((0x1 << 2));
        private static readonly uint _eMV = (uint)(0x1);

        private static double _spX = 0.0;
        private static double _spY = 0.0;

        private static double _pvX = 0.0;
        private static double _pvY = 0.0;
        public static double smoothingFactor = 0.5;
        public static bool IsEMASmoothingEnabled = false;

        [DllImport("user32.dll", EntryPoint = "mouse_event")]
        private static extern void _mEv(uint _a, uint _b, uint _c, uint _d, int _e);

        private static readonly Random _rng = new();

        private static double _gauss(double _sg)
        {
            double _u1 = 1.0 - _rng.NextDouble();
            double _u2 = 1.0 - _rng.NextDouble();
            return _sg * Math.Sqrt(-2.0 * Math.Log(_u1)) * Math.Cos(2.0 * Math.PI * _u2);
        }

        private static bool _opP()
        {
            int _t = Environment.TickCount;
            return (_t | (~_t)) == -1;
        }

        private static double _ema(double _pv, double _cv, double _sf) =>
            _cv * _sf + _pv * (1.0 - _sf);

        // ── Click action cache ────────────────────────────────────────────────────────────
        // _gMA() used to allocate two fresh lambda closures on every call (heap pressure at
        // 144 fps). Cache the pair by method string — reallocates only on settings change.
        private static string _cachedGmaMM = "";
        private static (Action dn, Action up) _cachedGma = (() => { }, () => { });

        private static (Action dn, Action up) _gMA()
        {
            bool _op = _opP();
            uint _jk = unchecked((uint)Environment.TickCount ^ 0xF1C2A3B4u) & 0u; _ = _jk;
            string _mm = Convert.ToString(Dictionary.dropdownState["Mouse Movement Method"]) ?? "Mouse Event";
            int _st = 0;

            // Return cached pair without allocation when method hasn't changed.
            if (_mm == _cachedGmaMM) return _cachedGma;

            while (_op)
            {
                switch (_st)
                {
                    case 0:
                        _st = _mm == "SendInput" ? 1
                            : _mm == "LG HUB" ? 2
                            : _mm == "Razer Synapse (Require Razer Peripheral)" ? 3
                            : _mm == "ddxoft Virtual Input Driver" ? 4 : 5;
                        break;
                    case 1: _cachedGmaMM = _mm; _cachedGma = (() => _xF834._mSc0F(_eLD), () => _xF834._mSc0F(_eLU)); return _cachedGma;
                    case 2: _cachedGmaMM = _mm; _cachedGma = (() => _xA3F1._mMv03(1, 0, 0, 0), () => _xA3F1._mMv03(0, 0, 0, 0)); return _cachedGma;
                    case 3: _cachedGmaMM = _mm; _cachedGma = (() => _xC59A._mMc0A(1), () => _xC59A._mMc0A(0)); return _cachedGma;
                    case 4: _cachedGmaMM = _mm; _cachedGma = (() => _xE723._dIn0E._fBt!(1), () => _xE723._dIn0E._fBt!(2)); return _cachedGma;
                    case 5: _cachedGmaMM = _mm; _cachedGma = (() => _mEv(_eLD, 0, 0, 0, 0), () => _mEv(_eLU, 0, 0, 0, 0)); return _cachedGma;
                }
            }
            return (() => { }, () => { });
        }

        // ── Move dispatch cache ───────────────────────────────────────────────────────────
        // Cached method → index so we pay one string comparison per frame instead of
        // a full Dictionary hash lookup + 5 string comparisons inside _dispatchMove.
        private static string _cachedDispMM = "";
        private static int _cachedDispIdx = 5;

        private static void _dispatchMove(int _x, int _y)
        {
            bool _op = _opP();
            uint _jv = unchecked((uint)(_x ^ _y)) & 0u; _ = _jv;

            string _mm = Convert.ToString(Dictionary.dropdownState["Mouse Movement Method"]) ?? "Mouse Event";
            if (_mm != _cachedDispMM)
            {
                _cachedDispMM = _mm;
                _cachedDispIdx = _mm == "SendInput" ? 1
                               : _mm == "LG HUB" ? 2
                               : _mm == "Razer Synapse (Require Razer Peripheral)" ? 3
                               : _mm == "ddxoft Virtual Input Driver" ? 4 : 5;
            }

            int _st = _cachedDispIdx;
            while (_op)
            {
                switch (_st)
                {
                    case 1: _xF834._mSc0F(_eMV, _x, _y); return;
                    case 2: _xA3F1._mMv03(0, _x, _y, 0); return;
                    case 3: _xC59A._mMm09(_x, _y, true); return;
                    case 4: _xE723._dIn0E._fMr!(_x, _y); return;
                    default: _mEv(_eMV, (uint)_x, (uint)_y, 0, 0); return;
                }
            }
        }

        public static async Task DoTriggerClick(RectangleF? _db = null)
        {
            bool _op = _opP();
            int _jk2 = unchecked((int)(0xBEEFu ^ 0xCAFEu)); _ = _jk2;

            if (!(InputBindingManager.IsHoldingBinding("Aim Keybind") || InputBindingManager.IsHoldingBinding("Second Aim Keybind")))
            {
                ResetSprayState();
                return;
            }

            if (Convert.ToBoolean(Dictionary.toggleState["Spray Mode"]))
            {
                if (Convert.ToBoolean(Dictionary.toggleState["Cursor Check"]))
                {
                    Point _mp = WinAPICaller.GetCursorPosition();
                    if (_db.HasValue && !_db.Value.Contains(_mp.X, _mp.Y))
                    {
                        if (_iSp) ReleaseMouseButton();
                        return;
                    }
                }
                if (!_iSp) HoldMouseButton();
                return;
            }

            // TickCount64 in ms — replaces DateTime.UtcNow subtraction (syscall).
            long _now = Environment.TickCount64;
            int _tsl = _lCT == 0 ? int.MaxValue : (int)(_now - _lCT);
            int _tdm = (int)(Convert.ToDouble(Dictionary.sliderSettings["Auto Trigger Delay"]) * 1000);

            int _cd = 15 + _rng.Next(0, 14);

            if (_tsl < _tdm) return;

            int _pd = _rng.Next(0, 6);
            if (_pd > 0) await Task.Delay(_pd);

            var (_dn, _up) = _gMA();
            _dn.Invoke();
            await Task.Delay(_cd);
            _up.Invoke();
            _lCT = Environment.TickCount64;
        }

        #region Spray
        public static void HoldMouseButton()
        {
            if (_iSp) return;
            var (_dn, _) = _gMA();
            _dn.Invoke();
            _iSp = true;
        }

        public static void ReleaseMouseButton()
        {
            if (!_iSp) return;
            var (_, _up) = _gMA();
            _up.Invoke();
            _iSp = false;
        }

        public static void ResetSprayState()
        {
            if (_iSp) ReleaseMouseButton();
        }
        #endregion

        // ── Movement path cache ───────────────────────────────────────────────────────────
        // Path string looked up once per settings change, not per frame.
        // 1=CubicBezier 2=Lerp 3=Exponential 4=Adaptive 5=PerlinNoise 6=VelocityDamped 7=Direct
        private static string _cachedPath = "";
        private static int _pathIdx = 2;

        public static void MoveCrosshair(int _dX, int _dY)
        {
            bool _op = _opP();
            uint _jv2 = unchecked((uint)(_dX * 0 ^ _dY * 0)); _ = _jv2;

            double _tX = _dX - _sW / 2.0;
            double _tY = _dY - _sH / 2.0;
            double _ar = _sW / _sH;

            double _jS = Convert.ToDouble(Dictionary.sliderSettings["Mouse Jitter"]);
            double _jX = _jS > 0 ? _gauss(_jS * 0.45 + _rng.NextDouble() * 0.1) : 0;
            double _jY = _jS > 0 ? _gauss(_jS * 0.45 + _rng.NextDouble() * 0.1) : 0;

            Point _s = new(0, 0);
            Point _e = new((int)_tX, (int)_tY);
            Point _nP = new(0, 0);

            // Refresh cached path index if setting changed.
            string _mp = Convert.ToString(Dictionary.dropdownState["Movement Path"]) ?? "Lerp";
            if (_mp != _cachedPath)
            {
                _cachedPath = _mp;
                _pathIdx = _mp == "Cubic Bezier"     ? 1
                         : _mp == "Exponential"      ? 3
                         : _mp == "Adaptive"         ? 4
                         : _mp == "Perlin Noise"     ? 5
                         : _mp == "Velocity-Damped"  ? 6
                         : _mp == "Direct"           ? 7
                         : 2; // default: Lerp
            }

            int _st = 0;
            while (_op)
            {
                switch (_st)
                {
                    case 0:
                        _st = _pathIdx;
                        break;

                    case 1:
                        Point _c1 = new(_s.X + (_e.X - _s.X) / 3, _s.Y + (_e.Y - _s.Y) / 3);
                        Point _c2 = new(_s.X + 2 * (_e.X - _s.X) / 3, _s.Y + 2 * (_e.Y - _s.Y) / 3);
                        _nP = MovementPaths.CubicBezier(_s, _e, _c1, _c2, 1.0 - Convert.ToDouble(Dictionary.sliderSettings["Mouse Sensitivity (+/-)"]));
                        _st = 10; break;

                    case 2:
                        _nP = MovementPaths.Lerp(_s, _e, 1.0 - Convert.ToDouble(Dictionary.sliderSettings["Mouse Sensitivity (+/-)"]));
                        _st = 10; break;

                    case 3:
                        _nP = MovementPaths.Exponential(_s, _e, 1.0 - (Convert.ToDouble(Dictionary.sliderSettings["Mouse Sensitivity (+/-)"])) - 0.2, 3.0);
                        _st = 10; break;

                    case 4:
                        _nP = MovementPaths.Adaptive(_s, _e, 1.0 - Convert.ToDouble(Dictionary.sliderSettings["Mouse Sensitivity (+/-)"]));
                        _st = 10; break;

                    case 5:
                        _nP = MovementPaths.PerlinNoise(_s, _e, 1.0 - Convert.ToDouble(Dictionary.sliderSettings["Mouse Sensitivity (+/-)"]), 20, 0.5);
                        _st = 10; break;

                    case 6:
                        // VelocityDamped: PD controller — faster initial response + brakes near target.
                        // Reaches aim point in fewer frames than Lerp at the same sensitivity setting.
                        _nP = MovementPaths.VelocityDamped(_e, Convert.ToDouble(Dictionary.sliderSettings["Mouse Sensitivity (+/-)"]));
                        _st = 10; break;

                    case 7:
                        // Direct: full delta every frame — maximum speed, still clamped to ±150.
                        _nP = MovementPaths.Direct(_e);
                        _st = 10; break;

                    case 10:
                        if (IsEMASmoothingEnabled)
                        {
                            double _sfV = Math.Clamp(smoothingFactor + _gauss(0.03), 0.1, 0.99);
                            _nP.X = (int)_ema(_pvX, _nP.X, _sfV);
                            _nP.Y = (int)_ema(_pvY, _nP.Y, _sfV);
                        }
                        _st = 11; break;

                    case 11:
                        int _cB = 147 + _rng.Next(0, 6);
                        _nP.X = Math.Clamp(_nP.X, -_cB, _cB);
                        _nP.Y = Math.Clamp(_nP.Y, -_cB, _cB);
                        _nP.Y = (int)(_nP.Y / _ar);
                        _st = 12; break;

                    case 12:
                        _spX += _nP.X + _jX;
                        _spY += _nP.Y + _jY;
                        int _sendX = (int)_spX;
                        int _sendY = (int)_spY;
                        _spX -= _sendX;
                        _spY -= _sendY;

                        if (_sendX == 0 && _sendY == 0) { _st = 99; break; }

                        if (_rng.Next(0, 4) == 0) _sendX += _rng.Next(-1, 2);
                        if (_rng.Next(0, 4) == 0) _sendY += _rng.Next(-1, 2);

                        _dispatchMove(_sendX, _sendY);
                        _st = 99; break;

                    case 99:
                        _pvX = _nP.X;
                        _pvY = _nP.Y;
                        if (!Convert.ToBoolean(Dictionary.toggleState["Auto Trigger"])) ResetSprayState();
                        return;
                }
            }
        }
    }
}
