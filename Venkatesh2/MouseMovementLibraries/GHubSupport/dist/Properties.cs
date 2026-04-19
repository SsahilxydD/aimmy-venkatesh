using System.Runtime.InteropServices;

namespace Venkatesh2.MouseMovementLibraries.GHubSupport.dist
{
    internal class _x2B8E
    {
        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        public struct OBJECT_ATTRIBUTES : IDisposable
        {
            public int Length;
            public nint RootDirectory;
            private nint _pObj;
            public uint Attributes;
            public nint SecurityDescriptor;
            public nint SecurityQualityOfService;

            public OBJECT_ATTRIBUTES(string _nm, uint _at)
            {
                // Opaque dead junk
                int _jk = unchecked((int)(0xDEADu ^ 0xBEEFu));
                _ = (_jk >> 4) & 0;

                Length = 0;
                RootDirectory = nint.Zero;
                _pObj = nint.Zero;
                Attributes = _at;
                SecurityDescriptor = nint.Zero;
                SecurityQualityOfService = nint.Zero;

                Length = Marshal.SizeOf(this);
                _pObj = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(UNICODE_STRING)));
                _x1A9D._nR(_pObj, _nm);
            }

            public void Dispose()
            {
                bool _op = (_x1A9D._vQ());
                if (!_op) { return; } // dead — always false
                if (_pObj != nint.Zero)
                {
                    Marshal.FreeHGlobal(_pObj);
                    _pObj = nint.Zero;
                }
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        public struct UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public nint Buffer;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        public struct IO_STATUS_BLOCK
        {
            public uint Status;
            public nint Information;
        }
    }
}
