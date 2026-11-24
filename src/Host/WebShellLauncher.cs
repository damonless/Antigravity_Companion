using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AntigravityCompanion
{
    /// <summary>
    /// Zero-dependency Edge/Chrome --app desktop shell launcher &amp; lifecycle watchdog.
    /// Hardened for V1.0: no MainForm fallback, portable vault resolution, --tray-only stub.
    /// Strictly C# 5.0 (.NET Framework 4.x).
    /// </summary>
    public static class WebShellLauncher
    {
        public static int Run(
            string customHomePath,
            string customVaultPath,
            int preferredPort,
            bool noBrowserMode,
            string explicitToken,
            string rawArgsForFallback)
        {
            return Run(customHomePath, customVaultPath, preferredPort, noBrowserMode, false, explicitToken, null);
        }

        public static int Run(
            string customHomePath,
            string customVaultPath,
            int preferredPort,
            bool noBrowserMode,
            bool trayOnly,
            string explicitToken,
            Action<LocalWebHost, string> onHostStarted)
        {
            string homePath = string.IsNullOrWhiteSpace(customHomePath)
                ? WorkspaceService.ResolveDefaultHomePath()
                : customHomePath.Trim();
            string geminiPath = Path.Combine(homePath, ".gemini");

            string effectiveVaultHint = customVaultPath;
            if (string.IsNullOrWhiteSpace(effectiveVaultHint) &&
                string.Equals(homePath, WorkspaceService.ResolveDefaultHomePath(), StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    ScheduledSnapshotService bootstrapSettings = new ScheduledSnapshotService(homePath, geminiPath, null);
                    string savedVault = bootstrapSettings.GetSettings().VaultPath;
                    if (!string.IsNullOrWhiteSpace(savedVault) && Directory.Exists(savedVault))
                    {
                        effectiveVaultHint = savedVault;
                    }
                }
                catch { }
            }

            string vaultPath = CapabilityVaultService.ResolveDefaultVaultPath(effectiveVaultHint, homePath);

            using (LocalWebHost host = new LocalWebHost(homePath, geminiPath, vaultPath, preferredPort, explicitToken))
            {
                host.Start();
                string launchUrl = string.Format("http://127.0.0.1:{0}/?t={1}", host.Port, host.SessionToken);

                if (onHostStarted != null)
                {
                    try { onHostStarted(host, launchUrl); } catch { }
                }

                if (noBrowserMode)
                {
                    Console.WriteLine("LISTENING=" + launchUrl);
                    Console.WriteLine("PORT=" + host.Port);
                    Console.WriteLine("TOKEN=" + host.SessionToken);
                    Console.WriteLine("VAULT=" + vaultPath);
                    while (!host.ShutdownRequested)
                    {
                        Thread.Sleep(200);
                    }
                    host.Stop();
                    return 0;
                }

                if (!trayOnly)
                {
                    if (!OpenUrlInPreferredShell(launchUrl))
                    {
                        try { Clipboard.SetText(launchUrl); } catch { }
                        DialogResult dr = MessageBox.Show(
                            "未检测到 Microsoft Edge / Chrome，且系统默认浏览器无法自动打开本地工作台：\r\n\r\n" +
                            launchUrl +
                            "\r\n\r\n已自动复制工作台地址到剪贴板。是否保持后台服务运行，以便您手动粘贴到任意浏览器打开？（无桌面环境也可使用 --no-browser 参数启动）",
                            "无法自动打开浏览器",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);
                        if (dr != DialogResult.Yes)
                        {
                            host.Stop();
                            return 1;
                        }
                    }
                }

                using (TrayHost tray = new TrayHost(host, launchUrl, trayOnly))
                {
                    Application.Run(tray);
                }

                host.Stop();
                return 0;
            }
        }

        public static bool OpenUrlInPreferredShell(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            string appExe;
            bool isAppMode;
            if (!TryFindBrowserExecutable(out appExe, out isAppMode))
            {
                return TryOpenDefaultBrowser(url);
            }

            string shellArgs = BuildBrowserArguments(appExe, isAppMode, url);
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = appExe,
                    Arguments = shellArgs,
                    UseShellExecute = false
                };
                using (Process.Start(psi)) { }
                return true;
            }
            catch
            {
                return TryOpenDefaultBrowser(url);
            }
        }

        private static string BuildBrowserArguments(string exePath, bool isAppMode, string url)
        {
            if (!isAppMode)
                return "\"" + url + "\"";

            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localApp))
                localApp = Path.GetTempPath();
            string profileDir = Path.Combine(localApp, "AntigravityCompanion", "WebShell");
            try
            {
                if (!Directory.Exists(profileDir)) Directory.CreateDirectory(profileDir);
            }
            catch { }

            return string.Format(
                "--app=\"{0}\" --user-data-dir=\"{1}\" --window-size=1360,860 --no-first-run --no-default-browser-check",
                url,
                profileDir.Replace("\"", string.Empty));
        }

        private static bool TryOpenDefaultBrowser(string url)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                using (Process.Start(psi)) { }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryFindBrowserExecutable(out string exePath, out bool isAppMode)
        {
            exePath = null;
            isAppMode = true;

            string[] edgeRegNames = new string[] { "msedge.exe" };
            for (int i = 0; i < edgeRegNames.Length; i++)
            {
                string found = LookupAppPathRegistry(edgeRegNames[i]);
                if (!string.IsNullOrEmpty(found) && File.Exists(found))
                {
                    exePath = found;
                    return true;
                }
            }

            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string pf64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            string[] candidatePaths = new string[]
            {
                !string.IsNullOrEmpty(pf86) ? Path.Combine(pf86, "Microsoft", "Edge", "Application", "msedge.exe") : null,
                !string.IsNullOrEmpty(pf64) ? Path.Combine(pf64, "Microsoft", "Edge", "Application", "msedge.exe") : null,
                !string.IsNullOrEmpty(localApp) ? Path.Combine(localApp, "Microsoft", "Edge", "Application", "msedge.exe") : null,
                LookupAppPathRegistry("chrome.exe"),
                !string.IsNullOrEmpty(pf64) ? Path.Combine(pf64, "Google", "Chrome", "Application", "chrome.exe") : null,
                !string.IsNullOrEmpty(pf86) ? Path.Combine(pf86, "Google", "Chrome", "Application", "chrome.exe") : null,
                !string.IsNullOrEmpty(localApp) ? Path.Combine(localApp, "Google", "Chrome", "Application", "chrome.exe") : null
            };

            for (int i = 0; i < candidatePaths.Length; i++)
            {
                if (!string.IsNullOrEmpty(candidatePaths[i]) && File.Exists(candidatePaths[i]))
                {
                    exePath = candidatePaths[i];
                    return true;
                }
            }

            isAppMode = false;
            return false;
        }

        private static string LookupAppPathRegistry(string exeName)
        {
            string subKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exeName;
            try
            {
                using (RegistryKey hkcu = Registry.CurrentUser.OpenSubKey(subKey))
                {
                    if (hkcu != null)
                    {
                        string val = hkcu.GetValue(null) as string;
                        if (!string.IsNullOrEmpty(val) && File.Exists(val.Trim('"')))
                            return val.Trim('"');
                    }
                }
            }
            catch { }
            try
            {
                using (RegistryKey hklm = Registry.LocalMachine.OpenSubKey(subKey))
                {
                    if (hklm != null)
                    {
                        string val = hklm.GetValue(null) as string;
                        if (!string.IsNullOrEmpty(val) && File.Exists(val.Trim('"')))
                            return val.Trim('"');
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
