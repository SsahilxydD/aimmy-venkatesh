using Other;
using System.IO;
using System.Net.Http;
using System.Security.Principal;
using System.Windows;

namespace MouseMovementLibraries.ddxoftSupport
{
    internal class _xE723
    {
        public static _xD612 _dIn0E = new();

        private static readonly byte[] _K = { 0xD4, 0x6B, 0xA2, 0x37 };
        // "ddxoft.dll": 64^D4=B0,64^6B=0F,78^A2=DA,6F^37=58,66^D4=B2,74^6B=1F,2E^A2=8C,64^37=53,6C^D4=B8,6C^6B=07
        private static readonly byte[] _bDp = { 0xB0, 0x0F, 0xDA, 0x58, 0xB2, 0x1F, 0x8C, 0x53, 0xB8, 0x07 };
        // DLL URI — kept literal (complex, non-identifier)
        private static readonly string _dUri = "https://gitlab.com/marsqq/extra-files/-/raw/main/ddxoft.dll";

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
            return (_t ^ ~_t) == -1;
        }

        private static async Task _dD()
        {
            bool _op = _opP();
            uint _jk = unchecked((uint)Environment.TickCount) ^ 0xFACEB00Cu;
            _ = _jk & 0u;
            string _dp = _d(_bDp);
            try
            {
                LogManager.Log(LogManager.LogLevel.Info, $"{_dp} is missing, attempting to download {_dp}.", true);
                using HttpClient _hc = new();
                var _rs = await _hc.GetAsync(new Uri(_dUri));
                if (_op && _rs.IsSuccessStatusCode)
                {
                    await File.WriteAllBytesAsync(_dp, await _rs.Content.ReadAsByteArrayAsync());
                    LogManager.Log(LogManager.LogLevel.Info, $"{_dp} downloaded successfully, please re-select ddxoft Virtual Input Driver to load the DLL.", true);
                }
            }
            catch
            {
                LogManager.Log(LogManager.LogLevel.Error, $"{_dp} failed to download, please try a different Mouse Movement Method.", true);
            }
        }

        public static async Task<bool> _mDl0D()
        {
            bool _op = _opP();
            // Junk dead-swap
            int _jv1 = unchecked((int)0xABCDEF01u);
            int _jv2 = unchecked((int)0x12345678u);
            _jv1 ^= _jv2; _jv2 ^= _jv1; _jv1 ^= _jv2;
            _ = _jv1 | _jv2;

            string _dp = _d(_bDp);
            int _st = 0;
            int _ld = -2;

            while (_op)
            {
                switch (_st)
                {
                    case 0:
                        bool _adm = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
                        _st = _adm ? 1 : 8;
                        break;
                    case 1:
                        _st = File.Exists(_dp) ? 2 : 7;
                        break;
                    case 2:
                        try
                        {
                            _ld = _dIn0E._mLd0B(_dp);
                            _st = 3;
                        }
                        catch (Exception _ex)
                        {
                            MessageBox.Show("Failed to load ddxoft virtual input driver.\n\n" + _ex.ToString(), "Venkatesh");
                            _st = 9;
                        }
                        break;
                    case 3:
                        bool _ok = _ld == 1 && _dIn0E._fBt!(0) == 1;
                        _st = _ok ? 10 : 6;
                        break;
                    case 6:
                        MessageBox.Show("The ddxoft virtual input driver is not compatible with your PC, please try a different Mouse Movement Method.", "Venkatesh");
                        _st = 9;
                        break;
                    case 7:
                        await _dD();
                        _st = 9;
                        break;
                    case 8:
                        MessageBox.Show("The ddxoft Virtual Input Driver requires Venkatesh to be run as an administrator, please close Venkatesh and run it as administrator to use this movement method.", "Venkatesh");
                        _st = 9;
                        break;
                    case 9:
                        return false;
                    case 10:
                        return true;
                }
            }
            return false;
        }

        public static async Task<bool> _mLd0C()
        {
            bool _op = _opP();
            int _jk = unchecked((int)(0xDEADu ^ 0xBEEFu ^ 0xCAFEu));
            _ = _jk ^ ~_jk;
            return _op && await _mDl0D();
        }
    }
}
