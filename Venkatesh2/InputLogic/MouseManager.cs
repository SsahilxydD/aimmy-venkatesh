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
        private static double _sW = WinAPICaller.ScreenWidth;
        private static double _sH = WinAPICaller.ScreenHeight;

        internal static void RefreshScreenDimensions()
        {
            _sW = DisplayManager.ScreenWidth;
            _sH = DisplayManager.ScreenHeight;
        }

        private static readonly uint _eMV = (uint)(0x1);

        private static double _spX = 0.0;
        private static double _spY = 0.0;


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
            double _tY = _dY - _sW / 2.0;

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
                        int _pxOff = -(_e.Y - _s.Y) / 4;
                        int _pyOff =  (_e.X - _s.X) / 4;
                        Point _c1 = new(_s.X + (_e.X - _s.X) / 3 + _pxOff, _s.Y + (_e.Y - _s.Y) / 3 + _pyOff);
                        Point _c2 = new(_s.X + 2 * (_e.X - _s.X) / 3 - _pxOff, _s.Y + 2 * (_e.Y - _s.Y) / 3 - _pyOff);
                        _nP = MovementPaths.CubicBezier(_s, _e, _c1, _c2, 1.0 - Convert.ToDouble(Dictionary.sliderSettings["Mouse Sensitivity (+/-)"]));
                        _st = 11; break;

                    case 2:
                        _nP = MovementPaths.Lerp(_s, _e, 1.0 - Convert.ToDouble(Dictionary.sliderSettings["Mouse Sensitivity (+/-)"]));
                        _st = 11; break;

                    case 3:
                        _nP = MovementPaths.Exponential(_s, _e, 1.0 - Convert.ToDouble(Dictionary.sliderSettings["Mouse Sensitivity (+/-)"]), 3.0);
                        _st = 11; break;

                    case 4:
                        _nP = MovementPaths.Adaptive(_s, _e, 1.0 - Convert.ToDouble(Dictionary.sliderSettings["Mouse Sensitivity (+/-)"]));
                        _st = 11; break;

                    case 5:
                        _nP = MovementPaths.PerlinNoise(_s, _e, 1.0 - Convert.ToDouble(Dictionary.sliderSettings["Mouse Sensitivity (+/-)"]), 20, 0.5);
                        _st = 11; break;

                    case 6:
                        _nP = MovementPaths.VelocityDamped(_e, Convert.ToDouble(Dictionary.sliderSettings["Mouse Sensitivity (+/-)"]));
                        _st = 11; break;

                    case 7:
                        _nP = MovementPaths.Direct(_e);
                        _st = 11; break;

                    case 11:
                        int _cB = 147 + _rng.Next(0, 6);
                        _nP.X = Math.Clamp(_nP.X, -_cB, _cB);
                        _nP.Y = Math.Clamp(_nP.Y, -_cB, _cB);
                        _st = 12; break;

                    case 12:
                        _spX += _nP.X + _jX;
                        _spY += _nP.Y + _jY;
                        int _sendX = (int)_spX;
                        int _sendY = (int)_spY;
                        _spX -= _sendX;
                        _spY -= _sendY;

                        if (_sendX == 0 && _sendY == 0) { _st = 99; break; }

                        if (_jS > 0 && _rng.Next(0, 4) == 0) _sendX += _rng.Next(-1, 2);
                        if (_jS > 0 && _rng.Next(0, 4) == 0) _sendY += _rng.Next(-1, 2);

                        _dispatchMove(_sendX, _sendY);
                        _st = 99; break;

                    case 99:
                        return;
                }
            }
        }
    }
}
