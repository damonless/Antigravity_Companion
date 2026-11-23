using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // EDGE / CHROMIUM --APP STANDALONE DESKTOP SHELL LAUNCHER
    // Zero-dependency Chromium App Mode window + lifecycle watchdog (C# 5.0)
    // ══════════════════════════════════════════════════════════════════════

    public static class WebShellLauncher
    {
        public static int Run(
            string customHomePath,
            string customRepoPath,
            int preferredPort,
            bool noBrowserMode,
            string explicitToken,
            string rawArgsForFallback)
        {
            string homePath = string.IsNullOrWhiteSpace(customHomePath)
                ? WorkspaceService.ResolveDefaultHomePath()
                : customHomePath.Trim();
            string geminiPath = Path.Combine(homePath, ".gemini");
            string repoPath = string.IsNullOrWhiteSpace(customRepoPath)
                ? CapabilityRepositoryManager.ResolveDefaultRepositoryPath()
                : customRepoPath.Trim();

            using (LocalWebHost host = new LocalWebHost(homePath, geminiPath, repoPath, preferredPort, explicitToken))
            {
                host.Start();
                string launchUrl = string.Format("http://127.0.0.1:{0}/?t={1}", host.Port, host.SessionToken);

                if (noBrowserMode)
                {
                    Console.WriteLine("LISTENING=" + launchUrl);
                    Console.WriteLine("PORT=" + host.Port);
                    Console.WriteLine("TOKEN=" + host.SessionToken);
                    while (!host.ShutdownRequested)
                    {
                        Thread.Sleep(200);
                    }
                    return 0;
                }

                string appExe;
                bool isAppMode;
                if (!TryFindBrowserExecutable(out appExe, out isAppMode))
                {
                    // 无 Chromium 浏览器时尝试系统默认浏览器；彻底失败则引导用户切换 --legacy-ui
                    if (!TryOpenDefaultBrowser(launchUrl))
                    {
                        DialogResult res = MessageBox.Show(
                            "未检测到 Microsoft Edge / Chrome，或系统默认浏览器无法打开本地工作台。\r\n\r\n是否立即切换回经典 WinForms 界面 (--legacy-ui)？",
                            "无法启动现代工作台",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);
                        if (res == DialogResult.Yes)
                        {
                            host.Stop();
                            Application.EnableVisualStyles();
                            Application.SetCompatibleTextRenderingDefault(false);
                            Application.Run(new MainForm());
                            return 0;
                        }
                        return 1;
                    }
                }
                else
                {
                    string shellArgs = BuildBrowserArguments(appExe, isAppMode, launchUrl);
                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo
                        {
                            FileName = appExe,
                            Arguments = shellArgs,
                            UseShellExecute = false
                        };
                        using (Process p = Process.Start(psi))
                        {
                            // Edge/Chrome 若已有同 Profile 实例可能会把窗口转交后在 ~1s 内退出父启动器，不绑死 WaitForExit
                        }
                    }
                    catch
                    {
                        if (!TryOpenDefaultBrowser(launchUrl))
                        {
                            MessageBox.Show("启动 Edge/Chrome 独立窗口失败。\r\n请尝试使用 --legacy-ui 参数启动经典界面。",
                                "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return 1;
                        }
                    }
                }

                // 生命周期守护：等待首条 SSE/页面握手（宽限 25s），握手后仅在全部连接断开且心跳超时 > 8s 时退出
                DateTime startUtc = DateTime.UtcNow;
                while (!host.ShutdownRequested)
                {
                    Thread.Sleep(350);
                    DateTime nowUtc = DateTime.UtcNow;

                    if (!host.HasEverConnected)
                    {
                        if ((nowUtc - startUtc).TotalSeconds > 30.0)
                        {
                            break;
                        }
                    }
                    else
                    {
                        if (host.ActiveSseCount <= 0 &&
                            (nowUtc - host.LastClientActivityUtc).TotalSeconds > 8.0)
                        {
                            break;
                        }
                    }
                }

                host.Stop();
                return 0;
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
