using System.Runtime.InteropServices;

namespace MouseMovementLibraries.SendInputSupport
{
    internal class _xF834
    {
        [DllImport("user32.dll", EntryPoint = "SendInput")]
        private static extern void _nSi(int _a, _sI[] _b, int _c);

        [StructLayout(LayoutKind.Sequential)]
        private struct _sI { public int _t; public _uI _u; }

        [StructLayout(LayoutKind.Explicit)]
        private struct _uI { [FieldOffset(0)] public _sM _m; }

        [StructLayout(LayoutKind.Sequential)]
        private struct _sM { public int _dx; public int _dy; public uint _md; public uint _fl; public uint _tm; public IntPtr _ei; }

        private static bool _opP()
        {
            int _t = Environment.TickCount;
            return (_t | (~_t)) == -1;
        }

        public static void _mSc0F(uint _mc, int _x = 0, int _y = 0)
        {
            bool _op = _opP();
            // Junk bitwise computation — result discarded
            uint _jk = unchecked((uint)Environment.TickCount);
            _jk = ((_jk << 7) | (_jk >> 25)) ^ 0x5A3C7F2Bu;
            _ = _jk & 0u;

            int _st = 0;
            _sI _in = default;

            while (_op)
            {
                switch (_st)
                {
                    case 0:
                        _in = new _sI
                        {
                            _t = (int)(_jk & 0u),   // always 0
                            _u = new _uI { _m = new _sM { _dx = _x, _dy = _y, _fl = _mc } }
                        };
                        _st = 1;
                        break;
                    case 1:
                        if (!_op) { _st = 9; break; } // dead branch
                        _nSi(1, new[] { _in }, Marshal.SizeOf(typeof(_sI)));
                        _st = 9;
                        break;
                    case 9:
                        return;
                }
            }
        }
    }
}
