using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AntigravityCompanion
{
    /// <summary>
    /// Native Windows System Tray (NotifyIcon) + Win32 RegisterHotKey host.
    /// Runs on the STA thread via Application.Run(trayHost) without creating any visible Form.
    /// Strictly C# 5.0 (.NET Framework 4.x).
    /// </summary>
    public sealed class TrayHost : ApplicationContext
    {
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;
        private const int WM_HOTKEY = 0x0312;
        private const int HotkeyId = 0x4147;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private sealed class HotkeyNativeWindow : NativeWindow, IDisposable
        {
            private readonly Action _onHotkey;
            private bool _registered;

            public HotkeyNativeWindow(Action onHotkey)
            {
                _onHotkey = onHotkey;
                CreateParams cp = new CreateParams();
                cp.Caption = "AntigravityCompanionHotkeyWindow";
                CreateHandle(cp);
            }

            public bool TryRegister(uint modifiers, uint vk, out string error)
            {
                error = string.Empty;
                UnregisterCurrent();
                if (Handle == IntPtr.Zero)
                {
                    error = "热键窗口句柄未就绪。";
                    return false;
                }

                try
                {
                    if (RegisterHotKey(Handle, HotkeyId, modifiers | MOD_NOREPEAT, vk))
                    {
                        _registered = true;
                        return true;
                    }
                    if (RegisterHotKey(Handle, HotkeyId, modifiers, vk))
                    {
                        _registered = true;
                        return true;
                    }
                    int errCode = Marshal.GetLastWin32Error();
                    error = "全局热键已被其他程序占用 (Win32=" + errCode + ")";
                    return false;
                }
                catch (Exception ex)
                {
                    error = ex.Message ?? "注册全局热键失败";
                    return false;
                }
            }

            public void UnregisterCurrent()
            {
                if (_registered && Handle != IntPtr.Zero)
                {
                    try { UnregisterHotKey(Handle, HotkeyId); } catch { }
                    _registered = false;
                }
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
                {
                    if (_onHotkey != null)
                    {
                        try { _onHotkey(); } catch { }
                    }
                    return;
                }
                base.WndProc(ref m);
            }

            public void Dispose()
            {
                UnregisterCurrent();
                if (Handle != IntPtr.Zero)
                {
                    try { DestroyHandle(); } catch { }
                }
            }
        }

        private readonly LocalWebHost _host;
        private readonly string _launchUrl;
        private readonly bool _trayOnly;
        private readonly DateTime _startUtc;
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _itemOpen;
        private readonly ToolStripMenuItem _itemSnapshot;
        private readonly ToolStripMenuItem _itemStartup;
        private readonly ToolStripMenuItem _itemExit;
        private readonly HotkeyNativeWindow _hotkeyWindow;
        private readonly System.Windows.Forms.Timer _pollTimer;
        private readonly Control _uiMarshalControl;

        private Icon _ownedAppIcon;
        private string _activeHotkeyText = string.Empty;
        private int _lastObservedSettingsVersion = -1;
        private long _lastOpenTicksMs = -10000;
        private bool _disposed;

        public TrayHost(LocalWebHost host, string launchUrl, bool trayOnly)
            : this(host, launchUrl, trayOnly, true)
        {
        }

        internal TrayHost(LocalWebHost host, string launchUrl, bool trayOnly, bool showTrayAndRegisterHotkey)
        {
            _host = host;
            _launchUrl = launchUrl ?? string.Empty;
            _trayOnly = trayOnly;
            _startUtc = DateTime.UtcNow;
            HotkeyError = string.Empty;

            _menu = new ContextMenuStrip();
            _itemOpen = new ToolStripMenuItem("打开驾驶舱");
            _itemSnapshot = new ToolStripMenuItem("立即执行极速快照");
            _itemStartup = new ToolStripMenuItem("开机自启动");
            _itemExit = new ToolStripMenuItem("退出");

            _itemOpen.Click += delegate(object sender, EventArgs e) { OpenCockpit(); };
            _itemSnapshot.Click += delegate(object sender, EventArgs e) { TriggerQuickSnapshotAsync(); };
            _itemStartup.Click += delegate(object sender, EventArgs e) { ToggleStartupSetting(); };
            _itemExit.Click += delegate(object sender, EventArgs e) { RequestExit(); };

            _menu.Items.Add(_itemOpen);
            _menu.Items.Add(_itemSnapshot);
            _menu.Items.Add(_itemStartup);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_itemExit);

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = "Antigravity Companion V1.0";
            _notifyIcon.ContextMenuStrip = _menu;
            _notifyIcon.Icon = ResolveTrayIcon(out _ownedAppIcon);
            _notifyIcon.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                    OpenCockpit();
            };

            if (showTrayAndRegisterHotkey)
            {
                _hotkeyWindow = new HotkeyNativeWindow(OpenCockpit);
                _uiMarshalControl = new Control();
                IntPtr unusedHandle = _uiMarshalControl.Handle;
                if (unusedHandle != IntPtr.Zero && _host != null)
                {
                    _host.SetTraySyncHandler(SyncMenuStateFromAnyThread);
                }
                _notifyIcon.Visible = true;
            }

            if (_host != null)
            {
                _lastObservedSettingsVersion = _host.SettingsVersion;
            }

            RefreshMenuState();

            _pollTimer = new System.Windows.Forms.Timer();
            _pollTimer.Interval = 350;
            _pollTimer.Tick += OnPollTick;
            if (showTrayAndRegisterHotkey)
            {
                _pollTimer.Start();
            }
        }

        public bool HotkeyRegistered { get; private set; }
        public string HotkeyError { get; private set; }

        public bool StartupMenuChecked
        {
            get { return _itemStartup.Checked; }
        }

        public string[] MenuItemTexts
        {
            get
            {
                return new string[]
                {
                    _itemOpen.Text,
                    _itemSnapshot.Text,
                    _itemStartup.Text,
                    _itemExit.Text
                };
            }
        }

        public void RefreshMenuState()
        {
            CompanionSettings cfg = (_host != null && _host.SnapshotService != null)
                ? _host.SnapshotService.GetSettings()
                : new CompanionSettings();

            _itemStartup.Checked = cfg.StartWithWindows;

            string desiredHotkey = string.IsNullOrWhiteSpace(cfg.GlobalHotkey) ? string.Empty : cfg.GlobalHotkey.Trim();
            if (_hotkeyWindow != null && !string.Equals(_activeHotkeyText, desiredHotkey, StringComparison.OrdinalIgnoreCase))
            {
                _activeHotkeyText = desiredHotkey;
                if (string.IsNullOrEmpty(desiredHotkey))
                {
                    _hotkeyWindow.UnregisterCurrent();
                    HotkeyRegistered = false;
                    HotkeyError = string.Empty;
                }
                else
                {
                    uint mods;
                    uint vk;
                    if (!TryParseHotkey(desiredHotkey, out mods, out vk))
                    {
                        _hotkeyWindow.UnregisterCurrent();
                        HotkeyRegistered = false;
                        HotkeyError = "无效的全局热键格式: " + desiredHotkey;
                    }
                    else
                    {
                        string err;
                        bool ok = _hotkeyWindow.TryRegister(mods, vk, out err);
                        HotkeyRegistered = ok;
                        HotkeyError = err ?? string.Empty;
                        if (!ok && _host != null)
                        {
                            _host.AppendLog("warn", "全局热键 [" + desiredHotkey + "] 注册失败（已降级为托盘唤醒）：" + HotkeyError);
                        }
                    }
                }

                if (_host != null)
                {
                    _host.UpdateHotkeyRuntimeStatus(HotkeyRegistered, HotkeyError);
                }
            }
        }

        private void SyncMenuStateFromAnyThread(int timeoutMs)
        {
            if (_disposed || _uiMarshalControl == null || _uiMarshalControl.IsDisposed || !_uiMarshalControl.IsHandleCreated)
                return;
            if (!_uiMarshalControl.InvokeRequired)
            {
                RefreshMenuState();
                return;
            }
            using (ManualResetEvent done = new ManualResetEvent(false))
            {
                try
                {
                    _uiMarshalControl.BeginInvoke((MethodInvoker)delegate()
                    {
                        try { RefreshMenuState(); }
                        finally
                        {
                            try { done.Set(); } catch { }
                        }
                    });
                    done.WaitOne(timeoutMs > 0 ? timeoutMs : 300);
                }
                catch { }
            }
        }

        private void OnPollTick(object sender, EventArgs e)
        {
            if (_host == null) return;

            if (_host.ShutdownRequested)
            {
                ExitGracefully();
                return;
            }

            int ver = _host.SettingsVersion;
            if (ver != _lastObservedSettingsVersion)
            {
                _lastObservedSettingsVersion = ver;
                RefreshMenuState();
            }

            bool minimizeToTray = false;
            if (_host.SnapshotService != null)
            {
                minimizeToTray = _host.SnapshotService.GetSettings().MinimizeToTray;
            }

            if (!_trayOnly && !minimizeToTray)
            {
                DateTime nowUtc = DateTime.UtcNow;
                if (!_host.HasEverConnected)
                {
                    if ((nowUtc - _startUtc).TotalSeconds > 30.0)
                    {
                        _host.RequestShutdown();
                        ExitGracefully();
                    }
                }
                else
                {
                    if (_host.ActiveSseCount <= 0 &&
                        (nowUtc - _host.LastClientActivityUtc).TotalSeconds > 8.0)
                    {
                        _host.RequestShutdown();
                        ExitGracefully();
                    }
                }
            }
        }

        internal bool TryAcquireOpenCockpitDebounce(long nowMs, long cooldownMs)
        {
            long effectiveCooldown = cooldownMs > 0 ? cooldownMs : 800;
            if (_lastOpenTicksMs >= 0 && (nowMs - _lastOpenTicksMs) >= 0 && (nowMs - _lastOpenTicksMs) < effectiveCooldown)
            {
                return false;
            }
            _lastOpenTicksMs = nowMs;
            return true;
        }

        private void OpenCockpit()
        {
            long nowMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
            if (!TryAcquireOpenCockpitDebounce(nowMs, 800))
                return;

            if (!string.IsNullOrEmpty(_launchUrl))
            {
                WebShellLauncher.OpenUrlInPreferredShell(_launchUrl);
            }
        }

        private void TriggerQuickSnapshotAsync()
        {
            if (_host == null || _host.SnapshotService == null) return;
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                SnapshotRunResult res = _host.SnapshotService.RunSnapshotNow();
                try
                {
                    if (_notifyIcon != null && _notifyIcon.Visible)
                    {
                        if (res.Ok)
                        {
                            _notifyIcon.ShowBalloonTip(
                                2500,
                                "Antigravity Companion",
                                string.Format("极速快照完成（{0:F2} MB）", res.SizeBytes / (1024.0 * 1024.0)),
                                ToolTipIcon.Info);
                        }
                        else if (!res.SkippedBusy)
                        {
                            _notifyIcon.ShowBalloonTip(
                                3000,
                                "Antigravity Companion",
                                "快照失败: " + (res.ErrorMessage ?? "未知错误"),
                                ToolTipIcon.Warning);
                        }
                    }
                }
                catch { }
            });
        }

        private void ToggleStartupSetting()
        {
            if (_host == null || _host.SnapshotService == null) return;
            CompanionSettings cfg = _host.SnapshotService.GetSettings();
            bool prev = cfg.StartWithWindows;
            bool next = !prev;
            cfg.StartWithWindows = next;
            _host.SnapshotService.UpdateSettings(cfg);
            if (_host.IsDefaultHome || _host.SetStartWithWindowsOverride != null)
            {
                bool regOk = _host.TryApplyStartWithWindows(next, Application.ExecutablePath);
                if (!regOk)
                {
                    cfg.StartWithWindows = prev;
                    _host.SnapshotService.UpdateSettings(cfg);
                    _host.NotifySettingsChanged();
                    RefreshMenuState();
                    _host.AppendLog("warn", "无法写入 Windows 开机自启注册表，已自动回滚该开关。");
                    return;
                }
            }
            _host.NotifySettingsChanged();
            RefreshMenuState();
            _host.AppendLog("info", next ? "已开启开机自启动（--tray-only 托盘常驻）。" : "已关闭开机自启动。");
        }

        private void RequestExit()
        {
            if (_host != null)
            {
                _host.RequestShutdown();
            }
            ExitGracefully();
        }

        private void ExitGracefully()
        {
            if (_pollTimer != null)
            {
                try { _pollTimer.Stop(); } catch { }
            }
            if (_notifyIcon != null)
            {
                try { _notifyIcon.Visible = false; } catch { }
            }
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _disposed = true;
                if (_host != null)
                {
                    try { _host.SetTraySyncHandler(null); } catch { }
                }
                if (_pollTimer != null)
                {
                    try { _pollTimer.Stop(); _pollTimer.Dispose(); } catch { }
                }
                if (_uiMarshalControl != null)
                {
                    try { _uiMarshalControl.Dispose(); } catch { }
                }
                if (_hotkeyWindow != null)
                {
                    try { _hotkeyWindow.Dispose(); } catch { }
                }
                if (_notifyIcon != null)
                {
                    try { _notifyIcon.Visible = false; _notifyIcon.Dispose(); } catch { }
                }
                if (_menu != null)
                {
                    try { _menu.Dispose(); } catch { }
                }
                if (_ownedAppIcon != null)
                {
                    try { _ownedAppIcon.Dispose(); } catch { }
                    _ownedAppIcon = null;
                }
            }
            base.Dispose(disposing);
        }

        private static Icon ResolveTrayIcon(out Icon ownedIcon)
        {
            ownedIcon = null;
            try
            {
                string exePath = Application.ExecutablePath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    Icon extracted = Icon.ExtractAssociatedIcon(exePath);
                    if (extracted != null)
                    {
                        ownedIcon = extracted;
                        return extracted;
                    }
                }
            }
            catch { }
            return SystemIcons.Application;
        }

        public static bool TryParseHotkey(string hotkeyStr, out uint modifiers, out uint virtualKey)
        {
            modifiers = 0;
            virtualKey = 0;
            if (string.IsNullOrWhiteSpace(hotkeyStr))
                return false;

            string[] parts = hotkeyStr.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return false;

            string keyToken = null;
            for (int i = 0; i < parts.Length; i++)
            {
                string tok = parts[i].Trim();
                if (string.IsNullOrEmpty(tok))
                    return false;

                string lower = tok.ToLowerInvariant();
                if (lower == "ctrl" || lower == "control")
                {
                    modifiers |= MOD_CONTROL;
                }
                else if (lower == "alt")
                {
                    modifiers |= MOD_ALT;
                }
                else if (lower == "shift")
                {
                    modifiers |= MOD_SHIFT;
                }
                else if (lower == "win" || lower == "windows" || lower == "meta")
                {
                    modifiers |= MOD_WIN;
                }
                else
                {
                    if (keyToken != null)
                        return false;
                    keyToken = tok;
                }
            }

            if (string.IsNullOrEmpty(keyToken))
                return false;

            if (!TryParseVirtualKey(keyToken, out virtualKey))
                return false;

            bool isFunctionKey = virtualKey >= 0x70 && virtualKey <= 0x87;
            if (modifiers == 0 && !isFunctionKey)
                return false;

            return true;
        }

        private static bool TryParseVirtualKey(string token, out uint vk)
        {
            vk = 0;
            if (string.IsNullOrWhiteSpace(token))
                return false;

            string t = token.Trim();
            if (t.Length == 1)
            {
                char ch = char.ToUpperInvariant(t[0]);
                if (ch >= 'A' && ch <= 'Z') { vk = (uint)ch; return true; }
                if (ch >= '0' && ch <= '9') { vk = (uint)ch; return true; }
            }

            string upper = t.ToUpperInvariant();
            if (upper.StartsWith("F", StringComparison.Ordinal) && upper.Length >= 2 && upper.Length <= 3)
            {
                int fn;
                if (int.TryParse(upper.Substring(1), out fn) && fn >= 1 && fn <= 24)
                {
                    vk = (uint)(0x70 + (fn - 1));
                    return true;
                }
            }

            switch (upper)
            {
                case "SPACE": vk = (uint)Keys.Space; return true;
                case "ENTER":
                case "RETURN": vk = (uint)Keys.Return; return true;
                case "TAB": vk = (uint)Keys.Tab; return true;
                case "ESC":
                case "ESCAPE": vk = (uint)Keys.Escape; return true;
                case "BACKSPACE":
                case "BACK": vk = (uint)Keys.Back; return true;
                case "INSERT":
                case "INS": vk = (uint)Keys.Insert; return true;
                case "DELETE":
                case "DEL": vk = (uint)Keys.Delete; return true;
                case "HOME": vk = (uint)Keys.Home; return true;
                case "END": vk = (uint)Keys.End; return true;
                case "PAGEUP":
                case "PGUP": vk = (uint)Keys.PageUp; return true;
                case "PAGEDOWN":
                case "PGDN": vk = (uint)Keys.PageDown; return true;
                case "UP": vk = (uint)Keys.Up; return true;
                case "DOWN": vk = (uint)Keys.Down; return true;
                case "LEFT": vk = (uint)Keys.Left; return true;
                case "RIGHT": vk = (uint)Keys.Right; return true;
            }

            Keys parsedKey;
            if (Enum.TryParse<Keys>(t, true, out parsedKey) && parsedKey != Keys.None)
            {
                int code = (int)parsedKey & 0xFFFF;
                if (code > 0 && code <= 0xFF)
                {
                    vk = (uint)code;
                    return true;
                }
            }

            return false;
        }
    }
}
