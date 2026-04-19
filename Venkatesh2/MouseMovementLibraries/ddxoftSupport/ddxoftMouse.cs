using System.Runtime.InteropServices;

namespace MouseMovementLibraries.ddxoftSupport
{
    public enum KeyModifiers
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8
    }

    internal class _xD612
    {
        [DllImport("Kernel32", EntryPoint = "LoadLibrary")]
        private static extern IntPtr _fL(string _a);

        [DllImport("Kernel32", EntryPoint = "GetProcAddress")]
        private static extern IntPtr _fG(IntPtr _a, string _b);

        [DllImport("kernel32.dll", EntryPoint = "FreeLibrary")]
        public static extern bool _fF(IntPtr _a);

        public delegate int _pBt(int _a);
        public delegate int _pWh(int _a);
        public delegate int _pKy(int _a, int _b);
        public delegate int _pMv(int _a, int _b);
        public delegate int _pMr(int _a, int _b);
        public delegate int _pSt(string _a);
        public delegate int _pTd(int _a);

        public _pBt? _fBt;
        public _pWh? _fWh;
        public _pMv? _fMv;
        public _pMr? _fMr;
        public _pKy? _fKy;
        public _pSt? _fSt;
        public _pTd? _fTd;

        private IntPtr _hi;

        private static readonly byte[] _K = { 0xC3, 0x5A, 0x87, 0x14 };
        // Encrypted export names — key _K cycling
        // "DD_btn": D=44,D=44,_=5F,b=62,t=74,n=6E => 44^C3=87,44^5A=1E,5F^87=D8,62^14=76,74^C3=B7,6E^5A=34
        private static readonly byte[] _eBt = { 0x87, 0x1E, 0xD8, 0x76, 0xB7, 0x34 };
        // "DD_whl": 44^C3=87,44^5A=1E,5F^87=D8,77^14=63,68^C3=AB,6C^5A=36
        private static readonly byte[] _eWh = { 0x87, 0x1E, 0xD8, 0x63, 0xAB, 0x36 };
        // "DD_mov": 44^C3=87,44^5A=1E,5F^87=D8,6D^14=79,6F^C3=AC,76^5A=2C
        private static readonly byte[] _eMv = { 0x87, 0x1E, 0xD8, 0x79, 0xAC, 0x2C };
        // "DD_key": 44^C3=87,44^5A=1E,5F^87=D8,6B^14=7F,65^C3=A6,79^5A=23
        private static readonly byte[] _eKy = { 0x87, 0x1E, 0xD8, 0x7F, 0xA6, 0x23 };
        // "DD_movR": 44^C3=87,44^5A=1E,5F^87=D8,6D^14=79,6F^C3=AC,76^5A=2C,52^87=D5
        private static readonly byte[] _eMr = { 0x87, 0x1E, 0xD8, 0x79, 0xAC, 0x2C, 0xD5 };
        // "DD_str": 44^C3=87,44^5A=1E,5F^87=D8,73^14=67,74^C3=B7,72^5A=28
        private static readonly byte[] _eSt = { 0x87, 0x1E, 0xD8, 0x67, 0xB7, 0x28 };
        // "DD_todc": 44^C3=87,44^5A=1E,5F^87=D8,74^14=60,6F^C3=AC,64^5A=3E,63^87=E4
        private static readonly byte[] _eTd = { 0x87, 0x1E, 0xD8, 0x60, 0xAC, 0x3E, 0xE4 };

        private static string _d(byte[] _a)
        {
            var _r = new char[_a.Length];
            for (int _i = 0; _i < _a.Length; _i++)
                _r[_i] = (char)(_a[_i] ^ _K[_i & 3]);
            return new string(_r);
        }

        private static bool _opP()
        {
            int _t = Environment.TickCount;
            return (_t | (~_t)) == -1;
        }

        ~_xD612()
        {
            bool _op = _opP();
            if (_op && !_hi.Equals(IntPtr.Zero)) { _fF(_hi); }
        }

        public int _mLd0B(string _df)
        {
            bool _op = _opP();
            int _jk = unchecked((int)(0xBAADF00Du ^ 0xCAFEBABEu));
            _ = _jk & 0;

            int _st = 0;
            int _rv = -2;

            while (_op)
            {
                switch (_st)
                {
                    case 0:
                        _hi = _fL(_df);
                        _st = _hi.Equals(IntPtr.Zero) ? 2 : 1;
                        break;
                    case 1:
                        _rv = _gA(_hi);
                        _st = 9;
                        break;
                    case 2:
                        _rv = -2;
                        _st = 9;
                        break;
                    case 9:
                        return _rv;
                }
            }
            return -2;
        }

        private int _gA(IntPtr _h)
        {
            bool _op = _opP();
            uint _jv = unchecked((uint)(Environment.TickCount ^ 0x5C3A7F2B));
            _ = _jv >> 16;

            IntPtr _p;
            int _st = 0;

            while (_op)
            {
                switch (_st)
                {
                    case 0:
                        _p = _fG(_h, _d(_eBt));
                        if (_p.Equals(IntPtr.Zero)) return -1;
                        _fBt = Marshal.GetDelegateForFunctionPointer<_pBt>(_p);
                        _st = 1; break;
                    case 1:
                        _p = _fG(_h, _d(_eWh));
                        if (_p.Equals(IntPtr.Zero)) return -1;
                        _fWh = Marshal.GetDelegateForFunctionPointer<_pWh>(_p);
                        _st = 2; break;
                    case 2:
                        _p = _fG(_h, _d(_eMv));
                        if (_p.Equals(IntPtr.Zero)) return -1;
                        _fMv = Marshal.GetDelegateForFunctionPointer<_pMv>(_p);
                        _st = 3; break;
                    case 3:
                        _p = _fG(_h, _d(_eKy));
                        if (_p.Equals(IntPtr.Zero)) return -1;
                        _fKy = Marshal.GetDelegateForFunctionPointer<_pKy>(_p);
                        _st = 4; break;
                    case 4:
                        _p = _fG(_h, _d(_eMr));
                        if (_p.Equals(IntPtr.Zero)) return -1;
                        _fMr = Marshal.GetDelegateForFunctionPointer<_pMr>(_p);
                        _st = 5; break;
                    case 5:
                        _p = _fG(_h, _d(_eSt));
                        if (_p.Equals(IntPtr.Zero)) return -1;
                        _fSt = Marshal.GetDelegateForFunctionPointer<_pSt>(_p);
                        _st = 6; break;
                    case 6:
                        _p = _fG(_h, _d(_eTd));
                        if (_p.Equals(IntPtr.Zero)) return -1;
                        _fTd = Marshal.GetDelegateForFunctionPointer<_pTd>(_p);
                        _st = 9; break;
                    case 9:
                        return 1;
                }
            }
            return -1;
        }
    }
}
