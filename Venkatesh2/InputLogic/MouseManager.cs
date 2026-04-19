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

        private static DateTime _lCT = DateTime.MinValue;
        private static bool _iSp = false;

        // Obfuscated: MOUSEEVENTF_LEFTDOWN=0x2, LEFTUP=0x4, MOVE=0x1
        private static readonly uint _eLD = (uint)((0x1 << 1));
        private static readonly uint _eLU = (uint)((0x1 << 2));
        private static readonly uint _eMV = (uint)(0x1);

        // Sub-pixel accumulation buffers
        private static double _spX = 0.0;
        private static double _spY = 0.0;

        private static double _pvX = 0.0;
        private static double _pvY = 0.0;
        public static double smoothingFactor = 0.5;
        public static bool IsEMASmoothingEnabled = false;

        [DllImport("user32.dll", EntryPoint = "mouse_event")]
        private static extern void _mEv(uint _a, uint _b, uint _c, uint _d, int _e);

        private static readonly Random _rng = new();

        // Box-Muller Gaussian — produces human-like noise distribution
        private static double _gauss(double _sg)
        {
            double _u1 = 1.0 - _rng.NextDouble();
            double _u2 = 1.0 - _rng.NextDouble();
            return _sg * Math.Sqrt(-2.0 * Math.Log(_u1)) * Math.Cos(2.0 * Math.PI * _u2);
        }

        private static bool _opP()
        {
            int _t = Environment.TickCount;
            return (_t | (~_t)) == -1; // always true
        }

        private static double _ema(double _pv, double _cv, double _sf) =>
            _cv * _sf + _pv * (1.0 - _sf);

        private static (Action dn, Action up) _gMA()
        {
            bool _op = _opP();
            uint _jk = unchecked((uint)Environment.TickCount ^ 0xF1C2A3B4u) & 0u; _ = _jk;

            string _mm = Dictionary.dropdownState["Mouse Movement Method"];
            int _st = 0;
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
                    case 1: return (() => _xF834._mSc0F(_eLD), () => _xF834._mSc0F(_eLU));
                    case 2: return (() => _xA3F1._mMv03(1, 0, 0, 0), () => _xA3F1._mMv03(0, 0, 0, 0));
                    case 3: return (() => _xC59A._mMc0A(1), () => _xC59A._mMc0A(0));
                    case 4: return (() => _xE723._dIn0E._fBt!(1), () => _xE723._dIn0E._fBt!(2));
                    case 5: return (() => _mEv(_eLD, 0, 0, 0, 0), () => _mEv(_eLU, 0, 0, 0, 0));
                }
            }
            return (() => { }, () => { });
        }

        private static void _dispatchMove(int _x, int _y)
        {
            bool _op = _opP();
            uint _jv = unchecked((uint)(_x ^ _y)) & 0u; _ = _jv;
            int _st = 0;
            string _mm = Dictionary.dropdownState["Mouse Movement Method"];
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
                    case 1: _xF834._mSc0F(_eMV, _x, _y); return;
                    case 2: _xA3F1._mMv03(0, _x, _y, 0); return;
                    case 3: _xC59A._mMm09(_x, _y, true); return;
                    case 4: _xE723._dIn0E._fMr!(_x, _y); return;
                    case 5: _mEv(_eMV, (uint)_x, (uint)_y, 0, 0); return;
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

            if (Dictionary.toggleState["Spray Mode"])
            {
                if (Dictionary.toggleState["Cursor Check"])
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

            int _tsl = (int)(DateTime.UtcNow - _lCT).TotalMilliseconds;
            int _tdm = (int)(Dictionary.sliderSettings["Auto Trigger Delay"] * 1000);

            // Humanized click delay: 15–28ms instead of constant 20ms
            int _cd = 15 + _rng.Next(0, 14);

            if (_tsl < _tdm && _lCT != DateTime.MinValue) return;

            // Human reaction micro-variance: 0–5ms pre-delay
            int _pd = _rng.Next(0, 6);
            if (_pd > 0) await Task.Delay(_pd);

            var (_dn, _up) = _gMA();
            _dn.Invoke();
            await Task.Delay(_cd);
            _up.Invoke();
            _lCT = DateTime.UtcNow;
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

        public static void MoveCrosshair(int _dX, int _dY)
        {
            bool _op = _opP();
            uint _jv2 = unchecked((uint)(_dX * 0 ^ _dY * 0)); _ = _jv2;

            double _tX = _dX - _sW / 2.0;
            double _tY = _dY - _sH / 2.0;
            double _ar = _sW / _sH;

            // Gaussian jitter — sigma proportional to setting, more human than uniform
            double _jS = Dictionary.sliderSettings["Mouse Jitter"];
            double _jX = _jS > 0 ? _gauss(_jS * 0.45 + _rng.NextDouble() * 0.1) : 0;
            double _jY = _jS > 0 ? _gauss(_jS * 0.45 + _rng.NextDouble() * 0.1) : 0;

            Point _s = new(0, 0);
            Point _e = new((int)_tX, (int)_tY);
            Point _nP = new(0, 0);

            // CFF: movement path selection
            int _st = 0;
            while (_op)
            {
                switch (_st)
                {
                    case 0:
                        string _mp = Dictionary.dropdownState["Movement Path"];
                        _st = _mp == "Cubic Bezier" ? 1
                            : _mp == "Exponential" ? 3
                            : _mp == "Adaptive" ? 4
                            : _mp == "Perlin Noise" ? 5 : 2;
                        break;
                    case 1:
                        Point _c1 = new(_s.X + (_e.X - _s.X) / 3, _s.Y + (_e.Y - _s.Y) / 3);
                        Point _c2 = new(_s.X + 2 * (_e.X - _s.X) / 3, _s.Y + 2 * (_e.Y - _s.Y) / 3);
                        _nP = MovementPaths.CubicBezier(_s, _e, _c1, _c2, 1.0 - Dictionary.sliderSettings["Mouse Sensitivity (+/-)"]);
                        _st = 10; break;
                    case 2:
                        _nP = MovementPaths.Lerp(_s, _e, 1.0 - Dictionary.sliderSettings["Mouse Sensitivity (+/-)"]);
                        _st = 10; break;
                    case 3:
                        _nP = MovementPaths.Exponential(_s, _e, 1.0 - (Dictionary.sliderSettings["Mouse Sensitivity (+/-)"]) - 0.2, 3.0);
                        _st = 10; break;
                    case 4:
                        _nP = MovementPaths.Adaptive(_s, _e, 1.0 - Dictionary.sliderSettings["Mouse Sensitivity (+/-)"]);
                        _st = 10; break;
                    case 5:
                        _nP = MovementPaths.PerlinNoise(_s, _e, 1.0 - Dictionary.sliderSettings["Mouse Sensitivity (+/-)"], 20, 0.5);
                        _st = 10; break;

                    case 10:
                        // EMA with per-frame smoothing variance ±3% — breaks fixed periodicity fingerprint
                        if (IsEMASmoothingEnabled)
                        {
                            double _sfV = Math.Clamp(smoothingFactor + _gauss(0.03), 0.1, 0.99);
                            _nP.X = (int)_ema(_pvX, _nP.X, _sfV);
                            _nP.Y = (int)_ema(_pvY, _nP.Y, _sfV);
                        }
                        _st = 11; break;

                    case 11:
                        // Randomised clamp bounds — not always exactly ±150
                        int _cB = 147 + _rng.Next(0, 6);
                        _nP.X = Math.Clamp(_nP.X, -_cB, _cB);
                        _nP.Y = Math.Clamp(_nP.Y, -_cB, _cB);
                        _nP.Y = (int)(_nP.Y / _ar);
                        _st = 12; break;

                    case 12:
                        // Sub-pixel accumulation: accumulate with Gaussian jitter, send only integer pixels
                        _spX += _nP.X + _jX;
                        _spY += _nP.Y + _jY;
                        int _sendX = (int)_spX;
                        int _sendY = (int)_spY;
                        _spX -= _sendX;
                        _spY -= _sendY;

                        // Skip zero-move frames — avoids detectable zero-delta event spam
                        if (_sendX == 0 && _sendY == 0) { _st = 99; break; }

                        // Tiny sub-pixel rounding noise on send values
                        if (_rng.Next(0, 4) == 0) _sendX += _rng.Next(-1, 2);
                        if (_rng.Next(0, 4) == 0) _sendY += _rng.Next(-1, 2);

                        _dispatchMove(_sendX, _sendY);
                        _st = 99; break;

                    case 99:
                        _pvX = _nP.X;
                        _pvY = _nP.Y;
                        if (!Dictionary.toggleState["Auto Trigger"]) ResetSprayState();
                        return;
                }
            }
        }
    }
}
