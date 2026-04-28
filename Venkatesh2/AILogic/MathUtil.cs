using Venkatesh2.AILogic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;

namespace AILogic
{
    public static class MathUtil
    {
        public static Func<double[], double[], double> _fL01 = (_a, _b) =>
        {
            double _c = 0f;
            for (int _i = 0; _i < _a.Length; _i++)
            {
                _c += (_a[_i] - _b[_i]) * (_a[_i] - _b[_i]);
            }
            return _c;
        };

        private static bool _opP()
        {
            int _t = Environment.TickCount;
            return (_t | (~_t)) == -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float _mD03(Prediction _a, Prediction _b)
        {
            uint _jv = unchecked((uint)((int)_a.ScreenCenterX ^ (int)_b.ScreenCenterY)) & 0u; _ = _jv;
            float _c = _a.ScreenCenterX - _b.ScreenCenterX;
            float _d = _a.ScreenCenterY - _b.ScreenCenterY;
            return _c * _c + _d * _d;
        }

        public static float _mT04(
            Prediction _a,
            Prediction? _b,
            float _c,
            float _d,
            float _e,
            float _f,
            float _g)
        {
            bool _op = _opP();
            uint _jv = unchecked((uint)((int)_c ^ (int)_d)) & 0u; _ = _jv;

            float _h = 0, _i = 0, _j = 0, _k = 0, _l = 0;
            float _m = 0, _n = 0, _o = 0, _ar = 0;
            int _st = 0;
            while (_op)
            {
                switch (_st)
                {
                    case 0:
                        _h = _a.ScreenCenterX - _c;
                        _i = _a.ScreenCenterY - _d;
                        _j = _h * _h + _i * _i;
                        _k = _g * _g;
                        _l = Math.Max(0f, 1f - (_j / _k));
                        _st = 1; break;
                    case 1:
                        _m = _a.Confidence * 0.3f;
                        _ar = _a.Rectangle.Width * _a.Rectangle.Height;
                        _n = Math.Min(0.2f, _ar / 50000f);
                        _st = 2; break;
                    case 2:
                        _o = (_b != null && _l > 0.3f)
                            ? (_e / _f) * 0.5f
                            : 0f;
                        return _l + _m + _n + _o;
                }
            }
            return 0f;
        }

        public static int _mN05(int _a)
        {
            bool _op = _opP();
            uint _jv = unchecked((uint)(_a ^ 0xDEAD)) & 0u; _ = _jv;

            int _b = 0, _c = 0, _d = 0;
            int _st = 0;
            while (_op)
            {
                switch (_st)
                {
                    case 0:
                        _b = _a / 8;
                        _c = _a / 16;
                        _d = _a / 32;
                        _st = 1; break;
                    case 1:
                        return (_b * _b) + (_c * _c) + (_d * _d);
                }
            }
            return 0;
        }

        private static readonly float[] _fB02 = _mC06();
        private static float[] _mC06()
        {
            var _a = new float[256];
            for (int _i = 0; _i < 256; _i++)
                _a[_i] = _i / 255f;
            return _a;
        }

        public static unsafe void _mB07(Bitmap _a, float[] _b, int _c)
        {
            uint _jv = unchecked((uint)(_c ^ 0xBEEF)) & 0u; _ = _jv;
            if (_a == null) throw new ArgumentNullException(nameof(_a));

            var _d = new Rectangle(0, 0, _c, _c);
            var _e = _a.LockBits(_d, ImageLockMode.ReadOnly, _a.PixelFormat);
            try
            {
                _mP08(
                    (byte*)_e.Scan0,
                    Math.Abs(_e.Stride),
                    _b,
                    _c,
                    applyThirdPersonMask: false);
            }
            finally
            {
                _a.UnlockBits(_e);
            }
        }

        public static unsafe void _mP08(
            byte* _a, int _b,
            float[] _c, int _d,
            bool applyThirdPersonMask)
        {
            if (!_opP()) return;
            uint _jv = unchecked((uint)(_d ^ _b)) & 0u; _ = _jv;
            if (_c == null) throw new ArgumentNullException(nameof(_c));

            int _w = _d;
            int _h = _d;
            int _tp = _w * _h;

            if (_c.Length != 3 * _tp)
                throw new ArgumentException($"result must be length {3 * _tp}", nameof(_c));

            const int _bpp = 4;
            const int _ppi = 4;

            int _rO = 0;
            int _gO = _tp;
            int _bO = _tp * 2;

            fixed (float* _dst = _c)
            fixed (float* _lp = _fB02)
            {
                float* _rP = _dst + _rO;
                float* _gP = _dst + _gO;
                float* _bP = _dst + _bO;

                for (int _y = 0; _y < _h; _y++)
                {
                    byte* _row = _a + (long)_y * _b;
                    int _rs = _y * _w;
                    int _x = 0;

                    int _wl = _w - _ppi + 1;
                    for (; _x < _wl; _x += _ppi)
                    {
                        int _bi = _rs + _x;
                        byte* _p = _row + (_x * _bpp);

                        _bP[_bi]     = _lp[_p[0]];
                        _gP[_bi]     = _lp[_p[1]];
                        _rP[_bi]     = _lp[_p[2]];

                        _bP[_bi + 1] = _lp[_p[4]];
                        _gP[_bi + 1] = _lp[_p[5]];
                        _rP[_bi + 1] = _lp[_p[6]];

                        _bP[_bi + 2] = _lp[_p[8]];
                        _gP[_bi + 2] = _lp[_p[9]];
                        _rP[_bi + 2] = _lp[_p[10]];

                        _bP[_bi + 3] = _lp[_p[12]];
                        _gP[_bi + 3] = _lp[_p[13]];
                        _rP[_bi + 3] = _lp[_p[14]];
                    }

                    for (; _x < _w; _x++)
                    {
                        int _idx = _rs + _x;
                        byte* _p = _row + (_x * _bpp);

                        _bP[_idx] = _lp[_p[0]];
                        _gP[_idx] = _lp[_p[1]];
                        _rP[_idx] = _lp[_p[2]];
                    }
                }

                if (applyThirdPersonMask)
                {
                    int _hw = _w / 2;
                    int _hh = _h / 2;
                    int _sy = _h - _hh;

                    for (int _y = _sy; _y < _h; _y++)
                    {
                        int _rs = _y * _w;
                        for (int _x = 0; _x < _hw; _x++)
                        {
                            int _idx = _rs + _x;
                            _rP[_idx] = 0f;
                            _gP[_idx] = 0f;
                            _bP[_idx] = 0f;
                        }
                    }
                }
            }
        }
    }
}
