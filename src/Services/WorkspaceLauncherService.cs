using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // WORKSPACE LAUNCHER SERVICE (IDE / CLI / Explorer / PowerShell)
    // Strictly C# 5.0 (.NET Framework 4.x)
    // ══════════════════════════════════════════════════════════════════════

    public class ResolvedLaunchTools
    {
        public string AntigravityIdeCmd = string.Empty;
        public string AntigravityExe = string.Empty;
        public string ExplorerExe = string.Empty;
        public string PowerShellExe = string.Empty;
    }

    public class WorkspaceLaunchResult
    {
        public bool Ok;
        public int HttpStatusCode = 200;
        public string Error = string.Empty;
        public string Tool = string.Empty;
        public string Executable = string.Empty;
        public string LaunchedPath = string.Empty;
    }

    public static class WorkspaceLauncherService
    {
        public static ResolvedLaunchTools ResolveLaunchTools(string geminiPath)
        {
            return ResolveLaunchTools(geminiPath, null, null);
        }

        public static ResolvedLaunchTools ResolveLaunchTools(string geminiPath, string localAppDataOverride, string pathEnvOverride)
        {
            ResolvedLaunchTools tools = new ResolvedLaunchTools();

            tools.AntigravityIdeCmd = ResolveAntigravityIdeCmd(geminiPath, pathEnvOverride);
            tools.AntigravityExe = ResolveAntigravityExe(geminiPath, localAppDataOverride, pathEnvOverride);
            tools.ExplorerExe = ResolveSystemTool("explorer.exe", Environment.GetFolderPath(Environment.SpecialFolder.Windows), pathEnvOverride);
            tools.PowerShellExe = ResolvePowerShellExe(pathEnvOverride);

            return tools;
        }

        public static Dictionary<string, object> ResolveLaunchToolsDict(string geminiPath)
        {
            ResolvedLaunchTools tools = ResolveLaunchTools(geminiPath);
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["antigravityIdeCmd"] = tools.AntigravityIdeCmd;
            d["antigravityExe"] = tools.AntigravityExe;
            d["explorerExe"] = tools.ExplorerExe;
            d["powershellExe"] = tools.PowerShellExe;
            d["hasIdeCmd"] = !string.IsNullOrEmpty(tools.AntigravityIdeCmd);
            d["hasAntigravityExe"] = !string.IsNullOrEmpty(tools.AntigravityExe);
            d["hasExplorer"] = !string.IsNullOrEmpty(tools.ExplorerExe);
            d["hasPowerShell"] = !string.IsNullOrEmpty(tools.PowerShellExe);
            return d;
        }

        public static string ResolveAntigravityIdeCmd(string geminiPath, string pathEnvOverride)
        {
            if (!string.IsNullOrWhiteSpace(geminiPath))
            {
                string ideLink = Path.Combine(geminiPath, "antigravity-ide");
                string[] linkCandidates = new string[]
                {
                    Path.Combine(ideLink, "bin", "antigravity-ide.cmd"),
                    Path.Combine(ideLink, "antigravity-ide.cmd")
                };
                for (int i = 0; i < linkCandidates.Length; i++)
                {
                    try
                    {
                        if (File.Exists(linkCandidates[i]))
                            return Path.GetFullPath(linkCandidates[i]);
                    }
                    catch { }
                }
            }

            return FindOnPath("antigravity-ide.cmd", pathEnvOverride) ??
                   FindOnPath("antigravity-ide", pathEnvOverride) ??
                   string.Empty;
        }

        public static string ResolveAntigravityExe(string geminiPath, string localAppDataOverride, string pathEnvOverride)
        {
            string localAppData = !string.IsNullOrWhiteSpace(localAppDataOverride)
                ? localAppDataOverride.Trim()
                : Environment.GetEnvironmentVariable("LOCALAPPDATA");

            List<string> candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                candidates.Add(Path.Combine(localAppData, "Programs", "antigravity", "Antigravity.exe"));
                candidates.Add(Path.Combine(localAppData, "Programs", "Antigravity IDE", "Antigravity.exe"));
                candidates.Add(Path.Combine(localAppData, "Programs", "Antigravity IDE", "Antigravity IDE.exe"));
            }
            if (!string.IsNullOrWhiteSpace(geminiPath))
            {
                string ideLink = Path.Combine(geminiPath, "antigravity-ide");
                candidates.Add(Path.Combine(ideLink, "Antigravity.exe"));
                candidates.Add(Path.Combine(ideLink, "Antigravity IDE.exe"));
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                try
                {
                    if (File.Exists(candidates[i]))
                        return Path.GetFullPath(candidates[i]);
                }
                catch { }
            }

            return FindOnPath("Antigravity.exe", pathEnvOverride) ?? string.Empty;
        }

        private static string ResolvePowerShellExe(string pathEnvOverride)
        {
            string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (!string.IsNullOrEmpty(sysDir))
            {
                string ps = Path.Combine(sysDir, "WindowsPowerShell", "v1.0", "powershell.exe");
                if (File.Exists(ps)) return ps;
            }
            return FindOnPath("powershell.exe", pathEnvOverride) ?? "powershell.exe";
        }

        private static string ResolveSystemTool(string exeName, string preferredDir, string pathEnvOverride)
        {
            if (!string.IsNullOrEmpty(preferredDir))
            {
                string candidate = Path.Combine(preferredDir, exeName);
                if (File.Exists(candidate)) return candidate;
            }
            return FindOnPath(exeName, pathEnvOverride) ?? exeName;
        }

        private static string FindOnPath(string fileName, string pathEnvOverride)
        {
            string pathEnv = pathEnvOverride != null ? pathEnvOverride : Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathEnv)) return null;

            string[] dirs = pathEnv.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < dirs.Length; i++)
            {
                string dir = dirs[i].Trim().Trim('"');
                if (string.IsNullOrEmpty(dir)) continue;
                try
                {
                    string candidate = Path.Combine(dir, fileName);
                    if (File.Exists(candidate)) return Path.GetFullPath(candidate);
                }
                catch { }
            }
            return null;
        }

        public static bool ValidateWorkspaceDirectory(
            string geminiPath,
            string homePath,
            string repoPath,
            string rawTargetDir,
            out string canonicalDir,
            out int httpErrorCode,
            out string error)
        {
            canonicalDir = null;
            httpErrorCode = 400;
            error = null;

            string pathErr;
            if (!ApiRouter.TryValidateCanonicalLocalPath(rawTargetDir, out canonicalDir, out pathErr))
            {
                httpErrorCode = 400;
                error = pathErr;
                return false;
            }

            canonicalDir = canonicalDir.TrimEnd('\\', '/');
            if (canonicalDir.Length == 2 && canonicalDir[1] == ':')
                canonicalDir += "\\";

            if (!Directory.Exists(canonicalDir))
            {
                httpErrorCode = 404;
                error = "目标工作区目录不存在: " + canonicalDir;
                return false;
            }

            List<string> allowedRoots = CollectAllowedWorkspaceRoots(geminiPath, homePath, repoPath);
            bool isAllowed = false;
            string normTarget = canonicalDir.TrimEnd('\\', '/');
            for (int i = 0; i < allowedRoots.Count; i++)
            {
                string root = allowedRoots[i];
                if (string.IsNullOrWhiteSpace(root)) continue;
                try
                {
                    string normRoot = Path.GetFullPath(root).TrimEnd('\\', '/');
                    if (string.Equals(normTarget, normRoot, StringComparison.OrdinalIgnoreCase) ||
                        SessionLifecycleService.IsPathContainedUnder(normRoot, normTarget))
                    {
                        isAllowed = true;
                        break;
                    }
                }
                catch { }
            }

            if (!isAllowed)
            {
                httpErrorCode = 403;
                error = "安全策略拒绝启动未注册工程或允许范围之外的目录: " + canonicalDir;
                return false;
            }

            return true;
        }

        private static List<string> CollectAllowedWorkspaceRoots(string geminiPath, string homePath, string repoPath)
        {
            List<string> roots = new List<string>();
            if (!string.IsNullOrWhiteSpace(homePath)) roots.Add(homePath);
            if (!string.IsNullOrWhiteSpace(geminiPath)) roots.Add(geminiPath);
            if (!string.IsNullOrWhiteSpace(repoPath)) roots.Add(repoPath);

            if (!string.IsNullOrWhiteSpace(geminiPath))
            {
                Dictionary<string, ProjectMetadata> projects = WorkspaceService.LoadLocalProjects(geminiPath);
                if (projects != null)
                {
                    foreach (KeyValuePair<string, ProjectMetadata> kvp in projects)
                    {
                        ProjectMetadata pm = kvp.Value;
                        if (pm == null) continue;
                        if (!string.IsNullOrEmpty(pm.FolderUri))
                        {
                            string dp = WorkspaceService.UriToDisplayPath(pm.FolderUri);
                            if (!string.IsNullOrEmpty(dp)) roots.Add(dp);
                        }
                        if (pm.FolderUris != null)
                        {
                            for (int i = 0; i < pm.FolderUris.Count; i++)
                            {
                                string dp = WorkspaceService.UriToDisplayPath(pm.FolderUris[i]);
                                if (!string.IsNullOrEmpty(dp)) roots.Add(dp);
                            }
                        }
                    }
                }
            }
            return roots;
        }

        public static WorkspaceLaunchResult LaunchWorkspace(
            string geminiPath,
            string homePath,
            string repoPath,
            string rawTargetDir,
            string toolMode)
        {
            WorkspaceLaunchResult res = new WorkspaceLaunchResult();
            string mode = string.IsNullOrWhiteSpace(toolMode) ? "ide" : toolMode.Trim().ToLowerInvariant();
            res.Tool = mode;

            string canonicalDir;
            int errCode;
            string errMsg;
            if (!ValidateWorkspaceDirectory(geminiPath, homePath, repoPath, rawTargetDir, out canonicalDir, out errCode, out errMsg))
            {
                res.Ok = false;
                res.HttpStatusCode = errCode;
                res.Error = errMsg;
                return res;
            }

            ResolvedLaunchTools tools = ResolveLaunchTools(geminiPath);
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.WorkingDirectory = canonicalDir;

            if (mode == "explorer")
            {
                if (string.IsNullOrEmpty(tools.ExplorerExe))
                {
                    res.Ok = false;
                    res.HttpStatusCode = 404;
                    res.Error = "未找到 explorer.exe。";
                    return res;
                }
                string cleanDir = canonicalDir.TrimEnd('\\', '/');
                if (cleanDir.Length == 2 && cleanDir[1] == ':') cleanDir += "\\";
                psi.FileName = tools.ExplorerExe;
                psi.Arguments = "\"" + cleanDir + "\"";
                psi.UseShellExecute = false;
            }
            else if (mode == "powershell" || mode == "terminal")
            {
                if (string.IsNullOrEmpty(tools.PowerShellExe))
                {
                    res.Ok = false;
                    res.HttpStatusCode = 404;
                    res.Error = "未找到 powershell.exe。";
                    return res;
                }
                string cleanDir = canonicalDir.Replace("'", "''");
                psi.FileName = tools.PowerShellExe;
                psi.Arguments = "-NoExit -Command \"Set-Location -LiteralPath '" + cleanDir + "'\"";
                psi.UseShellExecute = true;
            }
            else if (mode == "cli")
            {
                if (string.IsNullOrEmpty(tools.AntigravityIdeCmd))
                {
                    res.Ok = false;
                    res.HttpStatusCode = 404;
                    res.Error = "未找到 antigravity-ide.cmd，无法启动 CLI 工作区。";
                    return res;
                }
                psi.FileName = tools.AntigravityIdeCmd;
                psi.Arguments = "\"" + canonicalDir.TrimEnd('\\', '/') + "\"";
                psi.UseShellExecute = true;
            }
            else if (mode == "ide")
            {
                string exe = !string.IsNullOrEmpty(tools.AntigravityExe) ? tools.AntigravityExe : tools.AntigravityIdeCmd;
                if (string.IsNullOrEmpty(exe))
                {
                    res.Ok = false;
                    res.HttpStatusCode = 404;
                    res.Error = "未找到 Antigravity.exe 或 antigravity-ide.cmd。";
                    return res;
                }
                psi.FileName = exe;
                psi.Arguments = "\"" + canonicalDir.TrimEnd('\\', '/') + "\"";
                psi.UseShellExecute = exe.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                res.Ok = false;
                res.HttpStatusCode = 400;
                res.Error = "不支持的启动模式: " + mode + "（仅支持 ide / cli / explorer / powershell）。";
                return res;
            }

            try
            {
                using (Process p = Process.Start(psi))
                {
                    // Detached launch
                }
                res.Ok = true;
                res.HttpStatusCode = 200;
                res.Executable = psi.FileName;
                res.LaunchedPath = canonicalDir;
                return res;
            }
            catch (Exception ex)
            {
                res.Ok = false;
                res.HttpStatusCode = 500;
                res.Error = "启动工作区工具失败: " + ex.Message;
                return res;
            }
        }
    }
}
