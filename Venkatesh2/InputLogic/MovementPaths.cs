using System.Drawing;
using System.Runtime.CompilerServices;

namespace InputLogic
{
    class MovementPaths
    {
        // Standard Ken Perlin permutation table (256 values, doubled to 512 to avoid modular wrapping).
        // The old table was new int[512] left at all-zeros, which made Perlin noise produce a degenerate
        // constant output (every gradient hash resolved to the same case). This is the canonical table.
        private static readonly int[] _pT0A;

        static MovementPaths()
        {
            ReadOnlySpan<int> p = [
                151,160,137, 91, 90, 15,131, 13,201, 95, 96, 53,194,233,  7,225,
                140, 36,103, 30, 69,142,  8, 99, 37,240, 21, 10, 23,190,  6,148,
                247,120,234, 75,  0, 26,197, 62, 94,252,219,203,117, 35, 11, 32,
                 57,177, 33, 88,237,149, 56, 87,174, 20,125,136,171,168, 68,175,
                 74,165, 71,134,139, 48, 27,166, 77,146,158,231, 83,111,229,122,
                 60,211,133,230,220,105, 92, 41, 55, 46,245, 40,244,102,143, 54,
                 65, 25, 63,161,  1,216, 80, 73,209, 76,132,187,208, 89, 18,169,
                200,196,135,130,116,188,159, 86,164,100,109,198,173,186,  3, 64,
                 52,217,226,250,124,123,  5,202, 38,147,118,126,255, 82, 85,212,
                207,206, 59,227, 47, 16, 58, 17,182,189, 28, 42,223,183,170,213,
                119,248,152,  2, 44,154,163, 70,221,153,101,155,167, 43,172,  9,
                129, 22, 39,253, 19, 98,108,110, 79,113,224,232,178,185,112,104,
                218,246, 97,228,251, 34,242,193,238,210,144, 12,191,179,162,241,
                 81, 51,145,235,249, 14,239,107, 49,192,214, 31,181,199,106,157,
                184, 84,204,176,115,121, 50, 45,127,  4,150,254,138,236,205, 93,
                222,114, 67, 29, 24, 72,243,141,128,195, 78, 66,215, 61,156,180
            ];
            _pT0A = new int[512];
            for (int i = 0; i < 512; i++) _pT0A[i] = p[i & 255];
        }

        private static double _eP0B = 0;
        private static double _eP0C = 0;

        private static long _nF0D = 0;

        private static bool _opP()
        {
            int _t = Environment.TickCount;
            return (_t | (~_t)) == -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Point _mD01(Point _a)
        {
            uint _jv = unchecked((uint)(_a.X ^ _a.Y)) & 0u; _ = _jv;
            return _a;
        }

        internal static Point _mV02(Point end, double sensitivity)
        {
            bool _op = _opP();
            uint _jv = unchecked((uint)(end.X ^ end.Y)) & 0u; _ = _jv;

            double _g = 0, _p1 = 0, _d1 = 0;
            double _eX = 0, _eY = 0;
            double _dX = 0, _dY = 0;
            double _jS = 0, _eS = 0;
            double _oX = 0, _oY = 0;
            int _st = 0;
            while (_op)
            {
                switch (_st)
                {
                    case 0:
                        _g = 1.0 - sensitivity;
                        _p1 = _g * 1.6;
                        _d1 = _g * 0.5;
                        _st = 1; break;
                    case 1:
                        _eX = end.X;
                        _eY = end.Y;
                        _st = 2; break;
                    case 2:
                        _dX = _eX - _eP0B;
                        _dY = _eY - _eP0C;
                        _jS = _dX * _dX + _dY * _dY;
                        _eS = _eX * _eX + _eY * _eY;
                        if (_jS > _eS * 4.0)
                        {
                            _dX = 0;
                            _dY = 0;
                        }
                        _st = 3; break;
                    case 3:
                        _oX = _p1 * _eX + _d1 * _dX;
                        _oY = _p1 * _eY + _d1 * _dY;
                        _eP0B = _eX;
                        _eP0C = _eY;
                        _st = 4; break;
                    case 4:
                        return new Point((int)_oX, (int)_oY);
                }
            }
            return new Point(0, 0);
        }

        internal static Point _mB03(Point start, Point end, Point control1, Point control2, double t)
        {
            bool _op = _opP();
            uint _jv = unchecked((uint)(start.X ^ end.Y)) & 0u; _ = _jv;

            double _a = 0, _b = 0, _c = 0, _d = 0, _e = 0;
            double _rx = 0, _ry = 0;
            int _st = 0;
            while (_op)
            {
                switch (_st)
                {
                    case 0:
                        _a = 1 - t;
                        _b = t * t;
                        _c = _a * _a;
                        _d = _b * t;
                        _e = _c * _a;
                        _st = 1; break;
                    case 1:
                        _rx = _e * start.X + 3 * _c * t * control1.X + 3 * _a * _b * control2.X + _d * end.X;
                        _ry = _e * start.Y + 3 * _c * t * control1.Y + 3 * _a * _b * control2.Y + _d * end.Y;
                        _st = 2; break;
                    case 2:
                        return new Point((int)_rx, (int)_ry);
                }
            }
            return new Point(0, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Point _mL04(Point _a, Point _b, double _c)
        {
            uint _jv = unchecked((uint)(_a.X ^ _b.Y)) & 0u; _ = _jv;
            return new Point(
                (int)(_a.X + (_b.X - _a.X) * _c),
                (int)(_a.Y + (_b.Y - _a.Y) * _c));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Point _mE05(Point _a, Point _b, double _c, double _d = 2.0)
        {
            uint _jv = unchecked((uint)(_a.X ^ _b.Y)) & 0u; _ = _jv;
            double _tp = _d == 2.0 ? _c * _c
                       : _d == 3.0 ? _c * _c * _c
                       : Math.Pow(_c, _d);
            return new Point(
                (int)(_a.X + (_b.X - _a.X) * _tp),
                (int)(_a.Y + (_b.Y - _a.Y) * _tp));
        }

        internal static Point _mA06(Point start, Point end, double t, double threshold = 100.0)
        {
            bool _op = _opP();
            uint _jv = unchecked((uint)(start.X ^ end.Y)) & 0u; _ = _jv;

            double _a = 0, _b = 0, _c = 0, _d = 0;
            double _e = 0, _f = 0;
            int _st = 0;
            while (_op)
            {
                switch (_st)
                {
                    case 0:
                        _a = end.X - start.X;
                        _b = end.Y - start.Y;
                        _c = _a * _a + _b * _b;
                        _d = threshold * threshold;
                        _st = 1; break;
                    case 1:
                        if (_c < _d)
                        {
                            _f = Math.Sqrt(_c / _d);
                            _e = t * (2.0 - _f);
                        }
                        else
                        {
                            _f = _d / _c;
                            _e = t * (0.5 + 0.5 * _f);
                        }
                        _st = 2; break;
                    case 2:
                        _e = Math.Clamp(_e, 0.05, 0.95);
                        _st = 3; break;
                    case 3:
                        return new Point(
                            (int)(start.X + _a * _e),
                            (int)(start.Y + _b * _e));
                }
            }
            return new Point(0, 0);
        }

        internal static Point _mP07(Point start, Point end, double t, double amplitude = 10.0, double frequency = 0.1)
        {
            bool _op = _opP();
            uint _jv = unchecked((uint)(start.X ^ end.Y)) & 0u; _ = _jv;

            double _a = 0, _b = 0;
            double _c = 0, _d = 0, _e = 0;
            double _f = 0, _g = 0, _h = 0;
            int _st = 0;
            while (_op)
            {
                switch (_st)
                {
                    case 0:
                        _a = start.X + (end.X - start.X) * t;
                        _b = start.Y + (end.Y - start.Y) * t;
                        _st = 1; break;
                    case 1:
                        _c = _nF0D++ * frequency;
                        _d = _fN0B(_c, 0) * amplitude;
                        _e = _fN0B(_c, 100) * amplitude;
                        _st = 2; break;
                    case 2:
                        _f = -(end.Y - start.Y);
                        _g = end.X - start.X;
                        _h = _f * _f + _g * _g;
                        if (_h > 0)
                        {
                            double _inv = 1.0 / Math.Sqrt(_h);
                            _f *= _inv;
                            _g *= _inv;
                        }
                        _st = 3; break;
                    case 3:
                        return new Point(
                            (int)(_a + _f * _d + _e * 0.3),
                            (int)(_b + _g * _d + _e * 0.3));
                }
            }
            return new Point(0, 0);
        }

        private static double _fD08(double _a)
        {
            uint _jv = unchecked((uint)((int)(_a * 1000))) & 0u; _ = _jv;
            return _a * _a * _a * (_a * (_a * 6 - 15) + 10);
        }

        private static double _fL09(double _a, double _b, double _c)
        {
            uint _jv = unchecked((uint)((int)(_a * 1000) ^ (int)(_b * 1000))) & 0u; _ = _jv;
            return _a + _c * (_b - _a);
        }

        private static double _fG0A(int _a, double _b, double _c)
        {
            uint _jv = unchecked((uint)(_a ^ (int)(_b * 1000))) & 0u; _ = _jv;
            int _h = _a & 15;
            double _u = _h < 8 ? _b : _c;
            double _v = _h < 4 ? _c : (_h == 12 || _h == 14 ? _b : 0);
            return ((_h & 1) == 0 ? _u : -_u) + ((_h & 2) == 0 ? _v : -_v);
        }

        private static double _fN0B(double _a, double _b)
        {
            if (!_opP()) return 0.0;
            uint _jv = unchecked((uint)((int)(_a * 1000) ^ (int)(_b * 1000))) & 0u; _ = _jv;

            int _c = (int)Math.Floor(_a) & 255;
            int _d = (int)Math.Floor(_b) & 255;

            _a -= Math.Floor(_a);
            _b -= Math.Floor(_b);

            double _e = _fD08(_a);
            double _f = _fD08(_b);

            int _g  = _pT0A[_c]     + _d;
            int _h  = _pT0A[_g];
            int _i  = _pT0A[_g + 1];
            int _j  = _pT0A[_c + 1] + _d;
            int _k  = _pT0A[_j];
            int _l  = _pT0A[_j + 1];

            return _fL09(
                _fL09(_fG0A(_pT0A[_h], _a,     _b    ), _fG0A(_pT0A[_k], _a - 1, _b    ), _e),
                _fL09(_fG0A(_pT0A[_i], _a,     _b - 1), _fG0A(_pT0A[_l], _a - 1, _b - 1), _e),
                _f);
        }
    }
}
