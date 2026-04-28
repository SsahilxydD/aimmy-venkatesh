using Venkatesh2.AILogic;
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

        private static double _pX = 0;
        private static double _pY = 0;

        [DllImport("user32.dll", EntryPoint = "mouse_event")]
        private static extern void _mEv(uint _a, uint _b, uint _c, uint _d, int _e);

        private static bool _opP()
        {
            int _t = Environment.TickCount;
            return (_t | (~_t)) == -1;
        }

        private static string _cachedDispMM = "";
        private static int _cachedDispIdx = 5;

        private static void _dispatchMove(int _x, int _y)
        {
            bool _op = _opP();
            uint _jv = unchecked((uint)(_x ^ _y)) & 0u; _ = _jv;

            string _mm = Convert.ToString(Dictionary.dropdownState[_xB9D2._c1B]) ?? _xB9D2._c1C;
            if (_mm != _cachedDispMM)
            {
                _cachedDispMM = _mm;
                _cachedDispIdx = _mm == _xB9D2._c1D ? 1
                               : _mm == _xB9D2._c1E ? 2
                               : _mm == _xB9D2._c1F ? 3
                               : _mm == _xB9D2._c20 ? 4 : 5;
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

        private static string _cachedPath = "";
        private static int _pathIdx = 2;

        public static void MoveCrosshair(int _dX, int _dY)
        {
            bool _op = _opP();
            uint _jv2 = unchecked((uint)(_dX * 0 ^ _dY * 0)); _ = _jv2;

            int _tX = _dX - (int)_sW / 2;
            int _tY = _dY - (int)_sH / 2;

            double _ar = _sW / _sH;

            Point _s = new(0, 0);
            Point _e = new(_tX, _tY);
            Point _nP = new(0, 0);

            string _mp = Convert.ToString(Dictionary.dropdownState[_xB9D2._c22]) ?? _xB9D2._c23;
            if (_mp != _cachedPath)
            {
                _cachedPath = _mp;
                _pathIdx = _mp == _xB9D2._c24 ? 1
                         : _mp == _xB9D2._c25 ? 3
                         : _mp == _xB9D2._c26 ? 4
                         : _mp == _xB9D2._c27 ? 5
                         : _mp == _xB9D2._c28 ? 6
                         : _mp == _xB9D2._c29 ? 7
                         : 2;
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
                        _nP = MovementPaths._mB03(_s, _e, _c1, _c2, 1.0 - Convert.ToDouble(Dictionary.sliderSettings[_xB9D2._c2A]));
                        _st = 11; break;

                    case 2:
                        _nP = MovementPaths._mL04(_s, _e, 1.0 - Convert.ToDouble(Dictionary.sliderSettings[_xB9D2._c2A]));
                        _st = 11; break;

                    case 3:
                        _nP = MovementPaths._mE05(_s, _e, 1.0 - (Convert.ToDouble(Dictionary.sliderSettings[_xB9D2._c2A]) - 0.2), 3.0);
                        _st = 11; break;

                    case 4:
                        _nP = MovementPaths._mA06(_s, _e, 1.0 - Convert.ToDouble(Dictionary.sliderSettings[_xB9D2._c2A]));
                        _st = 11; break;

                    case 5:
                        _nP = MovementPaths._mP07(_s, _e, 1.0 - Convert.ToDouble(Dictionary.sliderSettings[_xB9D2._c2A]), 20, 0.5);
                        _st = 11; break;

                    case 6:
                        _nP = MovementPaths._mV02(_e, Convert.ToDouble(Dictionary.sliderSettings[_xB9D2._c2A]));
                        _st = 11; break;

                    case 7:
                        _nP = MovementPaths._mD01(_e);
                        _st = 11; break;

                    case 11:
                        _nP.X = Math.Clamp(_nP.X, -150, 150);
                        _nP.Y = Math.Clamp(_nP.Y, -150, 150);
                        _nP.Y = (int)(_nP.Y / _ar);
                        _st = 12; break;

                    case 12:
                        if (_nP.X == 0 && _nP.Y == 0) { _st = 99; break; }
                        _dispatchMove(_nP.X, _nP.Y);
                        _pX = _nP.X;
                        _pY = _nP.Y;
                        _st = 99; break;

                    case 99:
                        return;
                }
            }
        }
    }
}
