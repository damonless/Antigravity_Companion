using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace AntigravityCompanion
{
    public static class Program
    {
        private const int ATTACH_PARENT_PROCESS = -1;
        private static volatile bool _suppressUiDialogs;

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [STAThread]
        public static void Main(string[] args)
        {
            bool noBrowser = false;
            bool trayOnly = false;
            string customHome = null;
            string customVault = null;
            string explicitToken = null;
            int preferredPort = 0;

            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i] ?? string.Empty;
                    if (string.Equals(a, "--no-browser", StringComparison.OrdinalIgnoreCase))
                        noBrowser = true;
                    else if (string.Equals(a, "--tray-only", StringComparison.OrdinalIgnoreCase))
                        trayOnly = true;
                    else if (string.Equals(a, "--home", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        customHome = args[++i];
                    else if ((string.Equals(a, "--vault", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(a, "--repo", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                        customVault = args[++i];
                    else if (string.Equals(a, "--token", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        explicitToken = args[++i];
                    else if (string.Equals(a, "--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        int p;
                        if (int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out p) && p > 0 && p <= 65535)
                            preferredPort = p;
                    }
                }
            }

            _suppressUiDialogs = noBrowser;

            if (noBrowser)
            {
                // FIX P2-5: Attach parent console so stdout/stderr work cleanly under /target:winexe
                try { AttachConsole(ATTACH_PARENT_PROCESS); } catch { }
            }

            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
            {
                ShowCrashReport(e != null ? e.Exception : null);
            };

            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                ShowCrashReport(e != null ? (e.ExceptionObject as Exception) : null);
            };

            try
            {
                string effectiveHome = string.IsNullOrWhiteSpace(customHome)
                    ? WorkspaceService.ResolveDefaultHomePath()
                    : customHome.Trim();
                if (!string.IsNullOrEmpty(effectiveHome))
                {
                    try
                    {
                        string fullHome = Path.GetFullPath(effectiveHome);
                        effectiveHome = (fullHome.Length > 3) ? fullHome.TrimEnd('\\', '/') : fullHome;
                    }
                    catch { }
                }
                string homeHash = ComputeHomePathHashPrefix(effectiveHome);
                string mutexName = GetSingleInstanceMutexName(homeHash);
                string instanceFilePath = GetInstanceFilePath(homeHash);

                bool createdNew;
                using (Mutex singleMutex = new Mutex(false, mutexName, out createdNew))
                {
                    bool acquired = false;
                    try
                    {
                        try
                        {
                            acquired = singleMutex.WaitOne(0, false);
                        }
                        catch (AbandonedMutexException)
                        {
                            acquired = true;
                        }

                        if (!acquired)
                        {
                            string existingUrl = TryReadExistingInstanceUrl(instanceFilePath);
                            for (int retry = 0; retry < 10 && string.IsNullOrEmpty(existingUrl); retry++)
                            {
                                Thread.Sleep(150);
                                existingUrl = TryReadExistingInstanceUrl(instanceFilePath);
                            }

                            if (noBrowser)
                            {
                                if (!string.IsNullOrEmpty(existingUrl))
                                {
                                    Console.WriteLine("ALREADY_RUNNING=" + existingUrl);
                                    Environment.ExitCode = 0;
                                }
                                else
                                {
                                    Console.Error.WriteLine("Antigravity Companion is already running for home: " + effectiveHome);
                                    Environment.ExitCode = 1;
                                }
                                return;
                            }

                            if (trayOnly)
                            {
                                Environment.ExitCode = 0;
                                return;
                            }

                            if (!string.IsNullOrEmpty(existingUrl))
                            {
                                WebShellLauncher.OpenUrlInPreferredShell(existingUrl);
                                Environment.ExitCode = 0;
                                return;
                            }

                            MessageBox.Show(
                                "Antigravity Companion 已在后台运行中。",
                                "实例已存在",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                            Environment.ExitCode = 0;
                            return;
                        }

                        int boundPort = 0;
                        Environment.ExitCode = WebShellLauncher.Run(
                            effectiveHome,
                            customVault,
                            preferredPort,
                            noBrowser,
                            trayOnly,
                            explicitToken,
                            delegate(LocalWebHost host, string launchUrl)
                            {
                                boundPort = host.Port;
                                WriteInstanceFile(instanceFilePath, host.Port, host.SessionToken, launchUrl, effectiveHome);
                            });

                        CleanupInstanceFileIfOwned(instanceFilePath, boundPort);
                    }
                    finally
                    {
                        if (acquired)
                        {
                            CleanupInstanceFileIfOwned(instanceFilePath, 0);
                            try { singleMutex.ReleaseMutex(); } catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowCrashReport(ex);
                Environment.ExitCode = 1;
            }
        }

        public static string ComputeHomePathHashPrefix(string homePath)
        {
            string norm = (homePath ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(norm))
            {
                try { norm = Path.GetFullPath(norm); } catch { }
                norm = norm.TrimEnd('\\', '/').ToLowerInvariant();
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(norm));
                StringBuilder sb = new StringBuilder(16);
                for (int i = 0; i < 8 && i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }
                return sb.ToString();
            }
        }

        public static string GetSingleInstanceMutexName(string homeHash)
        {
            return "Local\\AntigravityCompanion_" + (homeHash ?? string.Empty);
        }

        public static string GetInstanceFilePath(string homeHash)
        {
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localApp))
            {
                localApp = Path.GetTempPath();
            }
            return Path.Combine(localApp, "AntigravityCompanion", "instance_" + (homeHash ?? string.Empty) + ".json");
        }

        internal static void WriteInstanceFile(string filePath, int port, string token, string url, string homePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            try
            {
                int pid;
                using (Process self = Process.GetCurrentProcess())
                {
                    pid = self.Id;
                }

                Dictionary<string, object> data = new Dictionary<string, object>();
                data["pid"] = pid;
                data["port"] = port;
                data["token"] = token ?? string.Empty;
                data["url"] = url ?? string.Empty;
                data["home"] = homePath ?? string.Empty;
                data["startedAtUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

                string json = UnifiedJson.Serialize(data, true);
                SessionLifecycleService.WriteUtf8TextAtomicWithBackup(filePath, json, false, null);
            }
            catch { }
        }

        internal static string TryReadExistingInstanceUrl(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                string raw = SessionLifecycleService.ReadUtf8TextShared(filePath);
                object parsed;
                if (!UnifiedJson.TryParse(raw, out parsed))
                    return null;

                Dictionary<string, object> dict = UnifiedJson.AsDict(parsed);
                if (dict == null) return null;

                int pid = UnifiedJson.GetInt(dict, "pid", 0);
                if (pid > 0)
                {
                    try
                    {
                        using (Process p = Process.GetProcessById(pid))
                        {
                            if (p.HasExited) return null;
                        }
                    }
                    catch
                    {
                        return null;
                    }
                }

                string url = UnifiedJson.GetString(dict, "url", string.Empty).Trim();
                if (url.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase) ||
                    url.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase))
                {
                    return url;
                }

                int port = UnifiedJson.GetInt(dict, "port", 0);
                string token = UnifiedJson.GetString(dict, "token", string.Empty);
                if (port > 0 && !string.IsNullOrEmpty(token))
                {
                    return string.Format(CultureInfo.InvariantCulture, "http://127.0.0.1:{0}/?t={1}", port, token);
                }
            }
            catch { }

            return null;
        }

        private static void CleanupInstanceFileIfOwned(string filePath, int expectedPort)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
            try
            {
                int myPid;
                using (Process self = Process.GetCurrentProcess())
                {
                    myPid = self.Id;
                }

                string raw = SessionLifecycleService.ReadUtf8TextShared(filePath);
                object parsed;
                if (UnifiedJson.TryParse(raw, out parsed))
                {
                    Dictionary<string, object> dict = UnifiedJson.AsDict(parsed);
                    if (dict != null)
                    {
                        int filePid = UnifiedJson.GetInt(dict, "pid", 0);
                        int filePort = UnifiedJson.GetInt(dict, "port", 0);
                        if (filePid != myPid) return;
                        if (expectedPort > 0 && filePort != expectedPort) return;
                    }
                }

                File.Delete(filePath);
            }
            catch { }
        }

        private static void ShowCrashReport(Exception ex)
        {
            if (ex == null) return;
            string msg = string.Format("应用程序启动或运行发生未捕获异常：\n\n{0}\n\n堆栈信息：\n{1}", ex.Message, ex.StackTrace);
            try
            {
                string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                File.WriteAllText(logFile, msg, Encoding.UTF8);
            }
            catch
            {
                try
                {
                    string tempLog = Path.Combine(Path.GetTempPath(), "antigravity_companion_crash.log");
                    File.WriteAllText(tempLog, msg, Encoding.UTF8);
                }
                catch { }
            }

            if (_suppressUiDialogs)
            {
                try { Console.Error.WriteLine(msg); } catch { }
            }
            else
            {
                try { MessageBox.Show(msg, "启动异常捕获", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
            }
        }
    }
}
