using Other;
using System.Windows;

namespace Venkatesh2.MouseMovementLibraries.GHubSupport
{
    internal class _xB48E
    {
        private static readonly byte[] _K = { 0xA5, 0x3C, 0x7F, 0x2B };
        // "Venkatesh"
        private static readonly byte[] _bVT = { 0xF3, 0x59, 0x11, 0x40, 0xC4, 0x48, 0x1A, 0x58, 0xCD };
        // "Unfortunately, LG HUB Mouse is not here."
        private static readonly byte[] _bM1 = { 0xF0, 0x52, 0x19, 0x44, 0xD7, 0x48, 0x0A, 0x45, 0xC4, 0x48, 0x1A, 0x47, 0xDC, 0x10, 0x5F, 0x67, 0xE2, 0x1C, 0x37, 0x7E, 0xE7, 0x1C, 0x32, 0x44, 0xD0, 0x4F, 0x1A, 0x0B, 0xCC, 0x4F, 0x5F, 0x45, 0xCA, 0x48, 0x5F, 0x43, 0xC0, 0x4E, 0x1A, 0x05 };
        // "Memory Integrity is enabled. Please disable it to use LG HUB Mouse Movement mode."
        private static readonly byte[] _bM3 = { 0xE8, 0x59, 0x15, 0x47, 0xD6, 0x59, 0x1E, 0x4C, 0xCC, 0x5E, 0x14, 0x47, 0xD4, 0x55, 0x1B, 0x67, 0xD4, 0x59, 0x1A, 0x4C, 0x5F, 0x67, 0xDF, 0x59, 0x1B, 0x47, 0xD2, 0x4B, 0x5F, 0x08, 0xD8, 0x56, 0x12, 0x44, 0x5F, 0x67, 0xE3, 0x59, 0x1A, 0x47, 0xDA, 0x4A, 0x5F, 0x4B, 0xC9, 0x4C, 0x14, 0x67, 0xE2, 0x5D, 0x1E, 0x4A, 0x5F, 0x67, 0xE2, 0x5C, 0x1B, 0x67, 0xE2, 0x53, 0x14, 0x4E, 0x5F, 0x67, 0xEA, 0x1C, 0x37, 0x7E, 0xE7, 0x1C, 0x32, 0x44, 0xD0, 0x4F, 0x1A, 0x0B, 0xD7, 0x5D, 0x16, 0x47, 0xD6, 0x4D };

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
            return (_t | (~_t)) == -1; // always true
        }

        public bool _mLd06()
        {
            bool _op = _opP();
            // Junk: bogus dead swap
            int _jv1 = unchecked((int)0xFACEB00Cu);
            int _jv2 = unchecked((int)0xDEADBABEu);
            _jv1 ^= _jv2; _jv2 ^= _jv1; _jv1 ^= _jv2;
            _ = _jv1 ^ _jv2;

            int _st = 0;
            bool _g1 = false, _g2 = false;
            Exception? _ex = null;

            while (_op)
            {
                switch (_st)
                {
                    case 0:
                        _g1 = RequirementsManager.CheckForGhub();
                        _st = _g1 ? 2 : 1;
                        break;
                    case 1:
                        MessageBox.Show(_d(_bM1), _d(_bVT));
                        _st = 99;
                        break;
                    case 2:
                        _g2 = RequirementsManager.IsMemoryIntegrityEnabled();
                        _st = _g2 ? 3 : 5;
                        break;
                    case 3:
                        try
                        {
                            _xA3F1._mOp01();
                            _xA3F1._mCl02();
                            _st = 4;
                        }
                        catch (Exception _e1)
                        {
                            _ex = _e1;
                            _st = 6;
                        }
                        break;
                    case 4:
                        return _op; // true
                    case 5:
                        MessageBox.Show(_d(_bM3), _d(_bVT));
                        _st = 99;
                        break;
                    case 6:
                        // "Unfortunately, LG HUB Mouse Movement mode cannot be ran sufficiently.\n"
                        MessageBox.Show("Unfortunately, LG HUB Mouse Movement mode cannot be ran sufficiently.\n" + _ex!.ToString(), _d(_bVT));
                        _st = 99;
                        break;
                    case 99:
                        return false;
                }
            }
            return false;
        }
    }
}
