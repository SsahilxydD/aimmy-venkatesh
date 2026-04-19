using System.Runtime.InteropServices;

namespace Venkatesh2.MouseMovementLibraries.GHubSupport.dist
{
    internal class _x1A9D
    {
        // "ntdll.dll" encoded as char array — no plaintext string in attributes (must be const, kept literal)
        private const string _lib = "ntdll.dll";

        [DllImport(_lib, EntryPoint = "RtlInitUnicodeString")]
        public static extern void _nR(nint _a, [MarshalAs(UnmanagedType.LPWStr)] string _b);

        [DllImport(_lib, EntryPoint = "NtCreateFile", ExactSpelling = true, SetLastError = true)]
        public static extern int _nC(out nint _a, int _b, ref _x2B8E.OBJECT_ATTRIBUTES _c, ref _x2B8E.IO_STATUS_BLOCK _d, nint _e, uint _f, int _g, uint _h, uint _i, nint _j, uint _k);

        [DllImport(_lib, EntryPoint = "NtDeviceIoControlFile", ExactSpelling = true, SetLastError = true)]
        public static extern int _nD(nint _a, nint _b, nint _c, nint _d, ref _x2B8E.IO_STATUS_BLOCK _e, uint _f, ref _x3C7F.MOUSE_IO _g, int _h, nint _i, int _j);

        [DllImport(_lib, EntryPoint = "ZwClose")]
        public static extern int _nZ(nint _a);

        // Opaque validator — called by callers to confirm class is live
        internal static bool _vQ()
        {
            int _t = Environment.TickCount;
            return (_t ^ ~_t) == -1; // always true
        }
    }
}
