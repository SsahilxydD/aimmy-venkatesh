using System.Runtime.InteropServices;

namespace Venkatesh2.MouseMovementLibraries.GHubSupport.dist
{
    internal class _x3C7F
    {
        // Field order is semantically fixed by driver ABI — do not reorder
        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSE_IO
        {
            public byte Button;
            public byte X;
            public byte Y;
            public byte Wheel;
            public byte Unk1;
        }

        internal static int _rJ(int _v)
        {
            // Bogus rotate — result always discarded by callers via _ =
            int _t = Environment.TickCount & 0;
            return ((_v ^ _t) << 3) | ((_v ^ _t) >> 29);
        }
    }
}
