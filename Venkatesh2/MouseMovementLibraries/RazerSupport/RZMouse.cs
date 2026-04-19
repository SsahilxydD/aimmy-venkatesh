using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using Visuality;

namespace MouseMovementLibraries.RazerSupport
{
    internal class _xC59A
    {
        private const string _lp = "rzctl.dll";
        private const string _uD = "https://github.com/MarsQQ/rzctl/releases/download/1.0.0/rzctl.dll";
        private const string _uR = "https://github.com/camilia2o7/rzctl/releases/download/Release/rzctl.dll";

        [DllImport(_lp, EntryPoint = "init", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool _mIt08();

        [DllImport(_lp, EntryPoint = "mouse_move", CallingConvention = CallingConvention.Cdecl)]
        public static extern void _mMm09(int _a, int _b, bool _c);

        [DllImport(_lp, EntryPoint = "mouse_click", CallingConvention = CallingConvention.Cdecl)]
        public static extern void _mMc0A(int _a);

        private static readonly List<string> _hd = new();
        private static bool _vP = false;

        private static readonly byte[] _K = { 0xB7, 0x4E, 0x91, 0x23 };
        // "RazerAppEngine" XOR _K
        // R=52,a=61,z=7A,e=65,r=72,A=41,p=70,p=70,E=45,n=6E,g=67,i=69,n=6E,e=65
        // 52^B7=E5, 61^4E=2F, 7A^91=EB, 65^23=46, 72^B7=C5, 41^4E=0F, 70^91=E1, 70^23=53, 45^B7=F2, 6E^4E=20, 67^91=F6, 69^23=4A, 6E^B7=D9, 65^4E=2B
        private static readonly byte[] _bPn = { 0xE5, 0x2F, 0xEB, 0x46, 0xC5, 0x0F, 0xE1, 0x53, 0xF2, 0x20, 0xF6, 0x4A, 0xD9, 0x2B };
        // "SELECT * FROM Win32_PnPEntity WHERE Manufacturer LIKE 'Razer%'"
        // — runtime WMI query, encrypted below (key _K cycling)
        // S=53,E=45,L=4C,E=45,C=43,T=54, =20,*=2A, =20,F=46,R=52,O=4F,M=4D, =20,W=57,i=69,n=6E,3=33,2=32,_=5F,P=50,n=6E,P=50,E=45,n=6E,t=74,i=69,t=74,y=79, =20,W=57,H=48,E=45,R=52,E=45, =20,M=4D,a=61,n=6E,u=75,f=66,a=61,c=63,t=74,u=75,r=72,e=65,r=72, =20,L=4C,I=49,K=4B,E=45, =20,\'=27,R=52,a=61,z=7A,e=65,r=72,%=25,\'=27
        private static readonly byte[] _bWQ = {
            0xE4,0x0B,0xDD,0x66,0xF4,0x1A,0xB1,0x69,0xB3,0x08,0xE3,0x6C,0xFA,0x6D,0xE7,0x45,0xFF,0x7D,0xA3,0x7C,0xE7,0x20,0xC1,0x66,0xFF,0x2B,0xF8,0x57,0xE8,0x5C,0xE6,0x06,0xF4,0x71,0xF4,0x66,0xB1,0x4E,0xD7,0x46,0xC6,0x28,0xD0,0x57,0xD6,0x0D,0xE3,0x46,0xE3,0x6D,0xFD,0x6A,0xEC,0x66,0xB3,0x04,0xE3,0x42,0xEB,0x46,0xE3,0x6B,0x96,0x04
        };

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
            return (_t ^ ~_t) == -1; // always -1 (all bits set)
        }

        public static async Task<bool> _mLd07()
        {
            bool _op = _opP();
            int _jv = unchecked((int)0xC0FFEE00u);
            _ = _jv ^ (_jv >> 4);

            int _st = 0;
            bool _res = false;

            while (_op)
            {
                switch (_st)
                {
                    case 0:
                        _res = await _eR();
                        _st = _res ? 1 : 9;
                        break;
                    case 1:
                        if (!File.Exists(_lp)) { _st = 7; break; }
                        _st = 2;
                        break;
                    case 2:
                        if (!_dR()) { _st = 8; break; }
                        _st = 3;
                        break;
                    case 3:
                        try
                        {
                            // Reflection-based dispatch — harder to trace statically
                            MethodInfo _mi = typeof(_xC59A).GetMethod(
                                nameof(_mIt08),
                                BindingFlags.Public | BindingFlags.Static)!;
                            _res = (bool)_mi.Invoke(null, null)!;
                            _st = _res ? 10 : 9;
                        }
                        catch (TargetInvocationException _tie) when (_tie.InnerException is BadImageFormatException)
                        {
                            _st = 4;
                        }
                        catch (Exception _ex)
                        {
                            _st = 5;
                            await _hEx(_ex);
                        }
                        break;
                    case 4:
                        new NoticeBar("rzctl.dll is incompatible. Attempting release version...", 4000).Show();
                        await _dL(_uR);
                        _st = 9;
                        break;
                    case 7:
                        await _dA();
                        _st = 9;
                        break;
                    case 8:
                        new NoticeBar("No Razer device detected. This method is unusable.", 5000).Show();
                        _st = 9;
                        break;
                    case 5:
                    case 9:
                        return false;
                    case 10:
                        return _res;
                }
            }
            return false;
        }

        private static async Task _hEx(Exception _ex)
        {
            bool _op = _opP();
            _ = _op ? 0 : throw new InvalidOperationException(); // dead throw
            MessageBox.Show(
                "Failed to initialize Razer mode.\n" + _ex.Message + "\n\nAttempting to replace rzctl.dll with the release version...",
                "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            try
            {
                if (File.Exists(_lp)) File.Delete(_lp);
                await _dL(_uR);
                new NoticeBar("rzctl.dll replaced with release version. Please retry loading.", 5000).Show();
            }
            catch (Exception _ie)
            {
                MessageBox.Show("Failed to recover rzctl.dll.\n" + _ie.Message, "Recovery Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool _dR()
        {
            bool _op = _opP();
            uint _jk = (uint)Environment.TickCount ^ 0xABCDEF01u;
            _ = (_jk * 0u); // junk
            _hd.Clear();
            using var _se = new ManagementObjectSearcher(_d(_bWQ));
            var _de = _se.Get().Cast<ManagementBaseObject>();
            _hd.AddRange(_de.Select(_x => _x["DeviceID"]?.ToString() ?? string.Empty));
            return _op && _hd.Count > 0;
        }

        private static async Task<bool> _eR()
        {
            bool _op = _opP();
            int _st = 0;
            bool _rv = false;
            MessageBoxResult _r1 = default, _r2 = default;

            while (_op)
            {
                switch (_st)
                {
                    case 0:
                        // Process name decrypted at runtime
                        _rv = Process.GetProcessesByName(_d(_bPn)).Any();
                        _st = _rv ? 10 : 1;
                        break;
                    case 1:
                        _r1 = MessageBox.Show(
                            "Razer Synapse is not running. Do you have it installed?",
                            "Venkatesh - Razer Synapse", MessageBoxButton.YesNo);
                        _st = _r1 == MessageBoxResult.No ? 5 : 2;
                        break;
                    case 2:
                        _st = _iR() ? 10 : 3;
                        break;
                    case 3:
                        _r2 = MessageBox.Show(
                            "Razer Synapse is not installed. Would you like to install it?",
                            "Venkatesh - Razer Synapse", MessageBoxButton.YesNo);
                        _st = _r2 == MessageBoxResult.Yes ? 4 : 9;
                        break;
                    case 4:
                        await _dS();
                        _st = 9;
                        break;
                    case 5:
                        await _dS();
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

        private static bool _iR()
        {
            bool _op = _opP();
            bool _r = Directory.Exists(@"C:\Program Files\Razer") ||
                      Directory.Exists(@"C:\Program Files (x86)\Razer") ||
                      Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Razer") != null;
            return _op && _r;
        }

        private static async Task _dS()
        {
            try
            {
                using HttpClient _cl = new();
                var _rs = await _cl.GetAsync("https://rzr.to/synapse-new-pc-download-beta");
                if (!_rs.IsSuccessStatusCode) { new NoticeBar("Failed to download Razer Synapse installer.", 4000).Show(); return; }
                string _pa = Path.Combine(Path.GetTempPath(), "rz.exe");
                await File.WriteAllBytesAsync(_pa, await _rs.Content.ReadAsByteArrayAsync());
                Process.Start(new ProcessStartInfo { WindowStyle = ProcessWindowStyle.Hidden, FileName = "cmd.exe", Arguments = "/C start rz.exe", WorkingDirectory = Path.GetTempPath() });
                new NoticeBar("Razer Synapse downloaded. Please confirm the UAC prompt to install.", 4000).Show();
            }
            catch { new NoticeBar("Error occurred while downloading Synapse.", 4000).Show(); }
        }

        private static bool _iV()
        {
            string[] _ks = { @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64", @"SOFTWARE\Microsoft\VisualStudio\17.0\VC\Runtimes\x64" };
            foreach (string _p in _ks)
            {
                using var _k = Registry.LocalMachine.OpenSubKey(_p);
                if (_k != null && Convert.ToInt32(_k.GetValue("Installed", 0)) == 1) return true;
            }
            return false;
        }

        private static bool _iS()
        {
            string[] _rt = { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" };
            foreach (string _r in _rt)
            {
                using var _k = Registry.LocalMachine.OpenSubKey(_r);
                if (_k == null) continue;
                foreach (string _sk in _k.GetSubKeyNames())
                {
                    using var _sb = _k.OpenSubKey(_sk);
                    if ((_sb?.GetValue("DisplayName") as string ?? "").Contains("Visual Studio", StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            return false;
        }

        private static async Task _dA()
        {
            bool _op = _opP();
            int _jk2 = 0x13579BDF;
            _ = _jk2 ^ ~_jk2;
            int _st = _iS() ? 0 : 1;

            while (_op)
            {
                switch (_st)
                {
                    case 0:
                        if (!await _dL(_uD)) { _st = 2; break; }
                        _st = 9;
                        break;
                    case 2:
                        await _dL(_uR);
                        _st = 9;
                        break;
                    case 1:
                        _st = _iV() ? 3 : 4;
                        break;
                    case 4:
                        if (_vP) { _st = 9; break; }
                        var _pr = MessageBox.Show("VC++ 2015-2022 Redistributable (x64) is missing. Install now?", "Missing Dependency", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (_pr == MessageBoxResult.Yes)
                        {
                            Process.Start(new ProcessStartInfo { FileName = "https://aka.ms/vs/17/release/vc_redist.x64.exe", UseShellExecute = true });
                            _st = 9;
                        }
                        else { _vP = true; _st = 9; }
                        break;
                    case 3:
                        await _dL(_uR);
                        _st = 9;
                        break;
                    case 9:
                        return;
                }
            }
        }

        private static async Task<bool> _dL(string _url)
        {
            try
            {
                new NoticeBar("rzctl.dll is missing, attempting to download rzctl.dll.", 4000).Show();
                using HttpClient _cl = new();
                var _rs = await _cl.GetAsync(_url, HttpCompletionOption.ResponseHeadersRead);
                if (!_rs.IsSuccessStatusCode) { new NoticeBar("Failed to download rzctl.dll from the given URL.", 4000).Show(); return false; }
                using var _st2 = await _rs.Content.ReadAsStreamAsync();
                using var _fi = new FileStream(_lp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                await _st2.CopyToAsync(_fi);
                new NoticeBar("rzctl.dll has downloaded successfully, please re-select Razer Synapse to load the DLL.", 5000).Show();
                return true;
            }
            catch { new NoticeBar("Error downloading rzctl.dll.", 4000).Show(); return false; }
        }
    }
}
