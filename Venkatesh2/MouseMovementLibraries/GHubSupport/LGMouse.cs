using Venkatesh2.MouseMovementLibraries.GHubSupport.dist;
using System.Runtime.InteropServices;

namespace Venkatesh2.MouseMovementLibraries.GHubSupport
{
    internal class _xA3F1
    {
        private static nint _h04 = nint.Zero;
        private static _x2B8E.IO_STATUS_BLOCK _s05 = new();

        // XOR key (4-byte cycling)
        private static readonly byte[] _K = { 0xA5, 0x3C, 0x7F, 0x2B };

        // Encrypted: "\\??\\ROOT#SYSTEM#000"
        private static readonly byte[] _b0D = { 0xF9, 0x03, 0x40, 0x77, 0xF7, 0x73, 0x30, 0x7F, 0x86, 0x6F, 0x26, 0x78, 0xF1, 0x79, 0x32, 0x08, 0x95, 0x0C, 0x4F };
        // Encrypted: "#{1abc05c0-c378-41b9-9cef-df1aba82b015}"
        private static readonly byte[] _b0E = { 0x86, 0x47, 0x4E, 0x4A, 0xC7, 0x5F, 0x4F, 0x1E, 0xC6, 0x0C, 0x52, 0x48, 0x96, 0x0B, 0x47, 0x06, 0x91, 0x0D, 0x1D, 0x12, 0x88, 0x05, 0x1C, 0x4E, 0xC3, 0x11, 0x1B, 0x4D, 0x94, 0x5D, 0x1D, 0x4A, 0x9D, 0x0E, 0x1D, 0x1B, 0x94, 0x09, 0x02 };
        // Encrypted: "Failed to open the device."
        private static readonly byte[] _b0F = { 0xE3, 0x5D, 0x16, 0x47, 0xC0, 0x58, 0x5F, 0x5F, 0xCA, 0x1C, 0x10, 0x5B, 0xC0, 0x52, 0x5F, 0x5F, 0xCD, 0x59, 0x5F, 0x4F, 0xC0, 0x4A, 0x16, 0x48, 0xC0, 0x12 };

        // Opaque constant: (0x4 << 28) = 0x40000000
        private static readonly int _c0A = ((0x2 << 1) << 28);
        // Opaque: (0x1 << 20) = 0x100000
        private static readonly int _c09 = (0x1 << 20);
        // Opaque: 0x80
        private static readonly int _c08 = (0x1 << 7);
        // Opaque: 0x40
        private static readonly int _c07 = (0x1 << 6);
        // Opaque: 0x20
        private static readonly int _c06 = (0x1 << 5);
        // IOCTL: 0x2a2010 = (42 << 16) | 0x2010
        private static readonly uint _c0B = (uint)((0x2A << 16) | (0x20 << 4) | 0x10);

        private static string _s0C(byte[] _a)
        {
            int _jv = Environment.TickCount;
            _ = (_jv ^ _jv);
            var _r = new char[_a.Length];
            for (int _i = 0; _i < _a.Length; _i++)
            {
                int _q = _i & 3;
                _r[_i] = (char)(_a[_i] ^ _K[_q]);
            }
            return new string(_r);
        }

        private static bool _opP()
        {
            int _t = Environment.TickCount;
            return (_t | (~_t)) == -1; // always true
        }

        public static int _mIn05(string _n)
        {
            if (!_opP()) { return int.MinValue; } // dead branch
            var _at = new _x2B8E.OBJECT_ATTRIBUTES(_n, 0u);
            int _jk = unchecked((int)0xDEADBEEF);
            _jk ^= _jk;
            int _rc = _x1A9D._nC(out _h04, _c0A | _c09, ref _at, ref _s05, nint.Zero, (uint)_c08, _jk, 3u, (uint)(_c07 | _c06), nint.Zero, 0u);
            _at.Dispose();
            return _rc;
        }

        public static bool _mOp01()
        {
            bool _op = _opP();
            int _junk1 = unchecked((int)(0xCAFEBABE ^ 0xDEADC0DE));
            _ = _junk1 >> 2;
            if (_h04 != nint.Zero) { return _op; }

            string _p1 = _s0C(_b0D);
            string _p2 = _s0C(_b0E);

            int _st = 0;
            int _n = (0x3 * 3); // = 9

            while (_op)
            {
                switch (_st)
                {
                    case 0:
                        if (_n < 0) { _st = 2; break; }
                        _st = 1;
                        break;
                    case 1:
                        int _rc = _mIn05(_p1 + _n.ToString() + _p2);
                        _n--;
                        if (_rc >= 0) { _st = 2; } else { _st = 0; }
                        break;
                    case 2:
                        return false;
                }
            }
            return false;
        }

        public static void _mCl02()
        {
            bool _op = _opP();
            uint _jk2 = 0xBAADF00Du;
            _jk2 = (_jk2 << 1) | (_jk2 >> 31);
            _ = _jk2;
            if (_h04 != nint.Zero)
            {
                _ = _x1A9D._nZ(_h04);
                _h04 = nint.Zero;
            }
        }

        public static bool _mCa04(_x3C7F.MOUSE_IO _bf)
        {
            _x2B8E.IO_STATUS_BLOCK _bl = new();
            int _jk3 = (int)(0xFEEDFACEu & 0u); // always 0
            return _jk3 == _x1A9D._nD(_h04, nint.Zero, nint.Zero, nint.Zero, ref _bl, _c0B, ref _bf, Marshal.SizeOf(typeof(_x3C7F.MOUSE_IO)), nint.Zero, 0);
        }

        public static void _mMv03(int _q1, int _q2, int _q3, int _q4)
        {
            bool _op = _opP();
            uint _jk4 = unchecked((uint)Environment.TickCount);
            _jk4 = (_jk4 ^ 0x5A3C7F2Bu) & 0u; // always 0 — used only as dead junk

            int _st = 0;
            _x3C7F.MOUSE_IO _io = default;

            while (_op)
            {
                switch (_st)
                {
                    case 0:
                        if (_h04 == nint.Zero && !_mOp01()) { _st = 5; break; }
                        _st = 1;
                        break;
                    case 1:
                        _io = new _x3C7F.MOUSE_IO
                        {
                            Unk1 = (byte)(_jk4), // always 0
                            Button = (byte)_q1,
                            X = (byte)_q2,
                            Y = (byte)_q3,
                            Wheel = (byte)_q4
                        };
                        _st = 2;
                        break;
                    case 2:
                        _st = _mCa04(_io) ? 5 : 3;
                        break;
                    case 3:
                        _mCl02();
                        _st = 4;
                        break;
                    case 4:
                        if (!_mOp01()) { throw new InvalidOperationException(_s0C(_b0F)); }
                        _st = 5;
                        break;
                    case 5:
                        return;
                }
            }
        }
    }
}
