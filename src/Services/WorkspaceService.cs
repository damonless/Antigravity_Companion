using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // WORKSPACE SERVICE, PROCESS GUARD & NATIVE DIALOG BRIDGE
    // Hardened for P0-3 (CLI legacy archive read-only), P2-4 (graceful IDE close)
    // Strictly C# 5.0 (.NET Framework 4.x)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Antigravity 主进程与专属 language_server 侦测及安全父优先关闭服务 (P2-4: never kill unless forceKill=true)
    /// </summary>
    public static class ProcessGuardService
    {
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private static readonly string[] ParentProcessNames = new string[]
        {
            "Antigravity", "Antigravity IDE", "antigravity", "antigravity-hans-windows-amd64", "agy-node"
        };

        private static readonly string[] LanguageServerProcessNames = new string[]
        {
            "language_server", "language_server_windows_x64"
        };

        public static string GetProcessImagePathSafe(Process p)
        {
            if (p == null) return string.Empty;
            IntPtr hProc = IntPtr.Zero;
            try
            {
                int pid = p.Id;
                hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (hProc == IntPtr.Zero) return string.Empty;

                int capacity = 1024;
                StringBuilder sb = new StringBuilder(capacity);
                if (QueryFullProcessImageName(hProc, 0, sb, ref capacity))
                    return sb.ToString();

                if (Marshal.GetLastWin32Error() == 122)
                {
                    capacity = 4096;
                    sb = new StringBuilder(capacity);
                    if (QueryFullProcessImageName(hProc, 0, sb, ref capacity))
                        return sb.ToString();
                }
            }
            catch { }
            finally
            {
                if (hProc != IntPtr.Zero)
                {
                    try { CloseHandle(hProc); } catch { }
                }
            }
            return string.Empty;
        }

        private static List<Process> CollectTargetProcesses(string[] processNames, int excludePid)
        {
            List<Process> result = new List<Process>();
            HashSet<int> seenPids = new HashSet<int>();
            for (int n = 0; n < processNames.Length; n++)
            {
                Process[] procs = null;
                try { procs = Process.GetProcessesByName(processNames[n]); } catch { }
                if (procs == null) continue;
                for (int i = 0; i < procs.Length; i++)
                {
                    Process p = procs[i];
                    try
                    {
                        if (p.Id == excludePid || !seenPids.Add(p.Id))
                        {
                            p.Dispose();
                            continue;
                        }
                        result.Add(p);
                    }
                    catch
                    {
                        try { p.Dispose(); } catch { }
                    }
                }
            }
            return result;
        }

        private static List<Process> CollectAntigravityLanguageServers(int excludePid)
        {
            List<Process> rawLs = CollectTargetProcesses(LanguageServerProcessNames, excludePid);
            List<Process> filtered = new List<Process>();
            for (int i = 0; i < rawLs.Count; i++)
            {
                Process p = rawLs[i];
                try
                {
                    string imgPath = GetProcessImagePathSafe(p);
                    if (!string.IsNullOrEmpty(imgPath) &&
                        imgPath.IndexOf("\\antigravity\\", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        filtered.Add(p);
                    }
                    else
                    {
                        p.Dispose();
                    }
                }
                catch
                {
                    try { p.Dispose(); } catch { }
                }
            }
            return filtered;
        }

        internal static void CollectAntigravityProcesses(out List<Process> parentProcs, out List<Process> lsProcs)
        {
            int currentPid;
            using (Process selfProc = Process.GetCurrentProcess())
            {
                currentPid = selfProc.Id;
            }
            parentProcs = CollectTargetProcesses(ParentProcessNames, currentPid);
            lsProcs = CollectAntigravityLanguageServers(currentPid);
        }

        internal static void DisposeProcessList(List<Process> list)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                try { list[i].Dispose(); } catch { }
            }
            list.Clear();
        }

        public static void GetRunningAntigravityProcessCounts(out int parentCount, out int lsCount)
        {
            List<Process> parents = null;
            List<Process> ls = null;
            try
            {
                CollectAntigravityProcesses(out parents, out ls);
                parentCount = parents != null ? parents.Count : 0;
                lsCount = ls != null ? ls.Count : 0;
            }
            finally
            {
                DisposeProcessList(parents);
                DisposeProcessList(ls);
            }
        }

        public static bool IsAntigravityRunning()
        {
            int p, l;
            GetRunningAntigravityProcessCounts(out p, out l);
            return p > 0 || l > 0;
        }

        public static bool CloseAntigravityProcesses(Action<string> onLog, out string errorMsg)
        {
            return CloseAntigravityProcesses(false, onLog, out errorMsg);
        }

        public static bool CloseAntigravityProcesses(bool forceKill, Action<string> onLog, out string errorMsg)
        {
            errorMsg = null;
            int currentPid;
            using (Process selfProc = Process.GetCurrentProcess())
            {
                currentPid = selfProc.Id;
            }

            List<Process> parentProcs = null;
            List<Process> lsProcs = null;
            List<Process> stillParents = null;
            List<Process> finalParents = null;
            List<Process> remainingLs = null;
            List<Process> finalLs = null;
            try
            {
                CollectAntigravityProcesses(out parentProcs, out lsProcs);
                if (parentProcs.Count == 0 && lsProcs.Count == 0) return true;

                if (parentProcs.Count > 0)
                {
                    if (onLog != null) onLog(string.Format("正在优雅关闭 {0} 个 Antigravity 主进程...", parentProcs.Count));
                    for (int i = 0; i < parentProcs.Count; i++)
                    {
                        try
                        {
                            if (parentProcs[i].MainWindowHandle != IntPtr.Zero)
                                parentProcs[i].CloseMainWindow();
                        }
                        catch { }
                    }

                    Stopwatch waitSw = Stopwatch.StartNew();
                    while (waitSw.ElapsedMilliseconds < 5000)
                    {
                        bool anyAlive = false;
                        for (int i = 0; i < parentProcs.Count; i++)
                        {
                            try
                            {
                                if (!parentProcs[i].HasExited)
                                {
                                    parentProcs[i].WaitForExit(150);
                                    if (!parentProcs[i].HasExited) anyAlive = true;
                                }
                            }
                            catch { }
                        }
                        if (!anyAlive) break;
                    }

                    stillParents = CollectTargetProcesses(ParentProcessNames, currentPid);
                    if (stillParents.Count > 0)
                    {
                        if (!forceKill)
                        {
                            errorMsg = string.Format("仍有 {0} 个 Antigravity 主进程未响应优雅关闭。", stillParents.Count);
                            return false;
                        }

                        if (onLog != null) onLog(string.Format("正在强制结束 {0} 个未响应的 Antigravity 主进程...", stillParents.Count));
                        for (int i = 0; i < stillParents.Count; i++)
                        {
                            try
                            {
                                if (!stillParents[i].HasExited)
                                {
                                    stillParents[i].Kill();
                                    stillParents[i].WaitForExit(3000);
                                }
                            }
                            catch { }
                        }

                        finalParents = CollectTargetProcesses(ParentProcessNames, currentPid);
                        if (finalParents.Count > 0)
                        {
                            errorMsg = "无法完全终止 Antigravity 主进程（可能权限不足），请手动关闭后再试。";
                            return false;
                        }
                    }
                }

                remainingLs = CollectAntigravityLanguageServers(currentPid);
                if (remainingLs.Count > 0)
                {
                    Stopwatch lsWait = Stopwatch.StartNew();
                    while (lsWait.ElapsedMilliseconds < 1500)
                    {
                        bool anyLsAlive = false;
                        for (int i = 0; i < remainingLs.Count; i++)
                        {
                            try
                            {
                                if (!remainingLs[i].HasExited)
                                {
                                    remainingLs[i].WaitForExit(150);
                                    if (!remainingLs[i].HasExited) anyLsAlive = true;
                                }
                            }
                            catch { }
                        }
                        if (!anyLsAlive) break;
                    }

                    DisposeProcessList(remainingLs);
                    remainingLs = CollectAntigravityLanguageServers(currentPid);
                    if (remainingLs.Count > 0)
                    {
                        // FIX P2-4: Never unconditionally Kill() unless forceKill == true!
                        if (!forceKill)
                        {
                            errorMsg = string.Format("仍有 {0} 个 Antigravity 语言服务进程尚未退出（未启用强制终止）。", remainingLs.Count);
                            return false;
                        }

                        if (onLog != null) onLog(string.Format("正在终止 {0} 个残留 Antigravity 语言服务进程...", remainingLs.Count));
                        for (int i = 0; i < remainingLs.Count; i++)
                        {
                            try
                            {
                                if (!remainingLs[i].HasExited)
                                {
                                    remainingLs[i].Kill();
                                    remainingLs[i].WaitForExit(3000);
                                }
                            }
                            catch { }
                        }

                        finalLs = CollectAntigravityLanguageServers(currentPid);
                        if (finalLs.Count > 0)
                        {
                            errorMsg = "无法完全终止 Antigravity 语言服务进程（可能权限不足），请手动结束后再试。";
                            return false;
                        }
                    }
                }

                return true;
            }
            finally
            {
                DisposeProcessList(parentProcs);
                DisposeProcessList(lsProcs);
                DisposeProcessList(stillParents);
                DisposeProcessList(finalParents);
                DisposeProcessList(remainingLs);
                DisposeProcessList(finalLs);
            }
        }
    }

    public class LegacyCliArchiveEntry : ConversationSummaryInfo
    {
        public bool IsLegacyCliArchive;

        public LegacyCliArchiveEntry()
        {
            IsLegacyCliArchive = true;
            IsCliSession = true;
        }
    }

    public class ProjectDisplayGroup
    {
        public string BucketKey;
        public string GroupKind;          // "formal" | "cli" | "outside" | "subagents" | "ephemeral"
        public string ProjectName;
        public string ProjectId;
        public string FolderUri;
        public string FullPath;
        public string ShortPath;
        public string DistinguishingLabel;
        public bool IsMultiRoot;
        public bool IsDuplicateBasename;
        public List<string> RootPaths = new List<string>();
        public int TotalCount;
        public int PrimaryCount;
        public DateTime LatestUpdatedTime;
        public List<ConversationSummaryInfo> Conversations = new List<ConversationSummaryInfo>();
    }

    public class ProjectGroupsSnapshot
    {
        public List<ProjectDisplayGroup> Groups = new List<ProjectDisplayGroup>();
        public int FormalProjectsCount;
        public int TotalRawSessions;
        public int FormalPrimarySessions;
        public int CliSessions;
        public int OutsideSessions;
        public int SubagentSessions;
        public int EphemeralSessions;
    }

    /// <summary>
    /// 无 UI 的工作区加载、工程路径分组、只读 CLI 旧库读取、能力矩阵及两阶段安全孤儿 GC 服务
    /// </summary>
    public static class WorkspaceService
    {
        public static string ResolveDefaultHomePath()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(userProfile))
                userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            if (string.IsNullOrEmpty(userProfile))
            {
                string sysDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                userProfile = Path.Combine(sysDrive, "Users", Environment.UserName);
            }
            return userProfile;
        }

        public static string GenerateStandardProjectJson(string projId, string projName, string folderUri)
        {
            string escId = UnifiedJson.EscapeString(projId ?? string.Empty);
            string escName = UnifiedJson.EscapeString(projName ?? string.Empty);
            string escUri = UnifiedJson.EscapeString(SessionLifecycleService.ToFileUri(folderUri));
            string nowIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffff000Z", CultureInfo.InvariantCulture);

            return string.Format(
                "{{\r\n" +
                "  \"id\": \"{0}\",\r\n" +
                "  \"name\": \"{1}\",\r\n" +
                "  \"projectResources\": {{\r\n" +
                "    \"resources\": [\r\n" +
                "      {{\r\n" +
                "        \"gitFolder\": {{\r\n" +
                "          \"folderUri\": \"{2}\",\r\n" +
                "          \"defaultBranch\": \"main\"\r\n" +
                "        }}\r\n" +
                "      }}\r\n" +
                "    ]\r\n" +
                "  }},\r\n" +
                "  \"settings\": {{\r\n" +
                "    \"sandboxMode\": true\r\n" +
                "  }},\r\n" +
                "  \"updatedAt\": \"{3}\",\r\n" +
                "  \"isWorkspaceOnly\": false\r\n" +
                "}}",
                escId, escName, escUri, nowIso
            );
        }

        public static string UriToDisplayPath(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri)) return string.Empty;
            string decoded = DirectStreamArchiver.UnescapeUriString(uri.Trim());
            if (decoded.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                decoded = decoded.Substring(8);
            else if (decoded.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                decoded = decoded.Substring(7);

            decoded = decoded.Replace('/', '\\');
            if (decoded.Length >= 2 && decoded[1] == ':' && decoded[0] >= 'a' && decoded[0] <= 'z')
                decoded = char.ToUpperInvariant(decoded[0]) + decoded.Substring(1);
            return decoded;
        }

        public static string ShortenHomeDisplayPath(string physicalPath, string homePath)
        {
            if (string.IsNullOrWhiteSpace(physicalPath)) return string.Empty;
            string clean = physicalPath.Trim().TrimEnd('\\', '/');
            if (!string.IsNullOrWhiteSpace(homePath))
            {
                string cleanHome = homePath.Trim().TrimEnd('\\', '/');
                if (string.Equals(clean, cleanHome, StringComparison.OrdinalIgnoreCase))
                    return "~";
                if (clean.Length > cleanHome.Length &&
                    clean.StartsWith(cleanHome, StringComparison.OrdinalIgnoreCase) &&
                    (clean[cleanHome.Length] == '\\' || clean[cleanHome.Length] == '/'))
                {
                    clean = "~" + clean.Substring(cleanHome.Length);
                }
            }
            return clean.Replace('\\', '/');
        }

        public static Dictionary<string, ProjectMetadata> LoadLocalProjects(string geminiPath)
        {
            Dictionary<string, ProjectMetadata> localProjects = new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(geminiPath)) return localProjects;

            string projDir = Path.Combine(geminiPath, "config", "projects");
            if (!Directory.Exists(projDir)) return localProjects;

            string[] files = Directory.GetFiles(projDir, "*.json");
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    string json = SessionLifecycleService.ReadUtf8TextShared(files[i]);
                    string id = Path.GetFileNameWithoutExtension(files[i]);
                    string name = DirectStreamArchiver.ExtractJsonValue(json, "name");
                    List<string> uris = DirectStreamArchiver.ExtractAllProjectFolderUris(json);
                    string firstUri = uris.Count > 0 ? uris[0] : string.Empty;
                    if (!string.IsNullOrEmpty(name))
                    {
                        ProjectMetadata pm = new ProjectMetadata
                        {
                            Id = id,
                            Name = name,
                            FolderUri = firstUri
                        };
                        if (uris.Count > 0) pm.FolderUris.AddRange(uris);
                        localProjects[id] = pm;
                    }
                }
                catch { }
            }
            return localProjects;
        }

        /// <summary>
        /// 只读读取 antigravity-cli/conversation_summaries.db 作为历史 CLI 归档条目 (IsLegacyCliArchive = true)。
        /// </summary>
        public static List<LegacyCliArchiveEntry> LoadLegacyCliArchiveEntries(string geminiPath, out List<string> warnings)
        {
            warnings = new List<string>();
            List<LegacyCliArchiveEntry> entries = new List<LegacyCliArchiveEntry>();
            if (string.IsNullOrEmpty(geminiPath)) return entries;

            string cliSummaryDbPath = Path.Combine(geminiPath, "antigravity-cli", "conversation_summaries.db");
            if (!File.Exists(cliSummaryDbPath)) return entries;

            try
            {
                string readErr;
                Dictionary<string, SummaryDbRowInfo> snap = SummarySqliteBridge.ReadSummaryDbSnapshot(cliSummaryDbPath, out readErr);
                if (!string.IsNullOrEmpty(readErr))
                {
                    warnings.Add("读取 antigravity-cli/conversation_summaries.db 只读归档告警: " + readErr);
                }
                if (snap == null || snap.Count == 0) return entries;

                foreach (KeyValuePair<string, SummaryDbRowInfo> kvp in snap)
                {
                    SummaryDbRowInfo row = kvp.Value;
                    if (row == null || !SessionMetadataExtractor.IsLikelyConversationId(row.ConversationId))
                        continue;

                    DateTime modTime = DateTime.MinValue;
                    if (!string.IsNullOrEmpty(row.LastModifiedTime))
                    {
                        DateTime parsedDt;
                        if (DateTime.TryParse(row.LastModifiedTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsedDt))
                            modTime = parsedDt.ToLocalTime();
                    }

                    string folderUri = ExtractFirstWorkspaceUri(row.WorkspaceUris);
                    string rawTitle = !string.IsNullOrWhiteSpace(row.Title) ? row.Title.Trim() : row.ConversationId.Trim();

                    LegacyCliArchiveEntry item = new LegacyCliArchiveEntry
                    {
                        ConversationId = row.ConversationId.Trim(),
                        DisplayTitle = rawTitle,
                        AutoSummaryTitle = rawTitle,
                        ProjectId = !string.IsNullOrWhiteSpace(row.ProjectId) ? row.ProjectId.Trim() : "default-cli-project",
                        ProjectName = !string.IsNullOrWhiteSpace(row.ProjectId) ? row.ProjectId.Trim() : "default-cli-project",
                        FolderUri = folderUri,
                        StepCount = row.StepCount,
                        UpdatedTime = modTime,
                        FirstPromptSnippet = row.FirstPromptSnippet,
                        GitBranchHint = row.GitBranchHint,
                        RepoBranchHints = row.RepoBranchHints,
                        ParentConversationId = row.ParentConversationId,
                        IsSubagent = !string.IsNullOrWhiteSpace(row.ParentConversationId),
                        AgentName = row.AgentName,
                        RunStatus = row.Status,
                        NotFullyIdle = row.NotFullyIdle,
                        IsCliSession = true,
                        IsLegacyCliArchive = true
                    };
                    entries.Add(item);
                }
            }
            catch (Exception ex)
            {
                warnings.Add("读取 antigravity-cli/conversation_summaries.db 只读归档失败: " + ex.Message);
            }

            return entries;
        }

        private static string ExtractFirstWorkspaceUri(string workspaceUrisJson)
        {
            if (string.IsNullOrWhiteSpace(workspaceUrisJson)) return string.Empty;
            object parsed;
            if (UnifiedJson.TryParse(workspaceUrisJson, out parsed))
            {
                List<object> list = UnifiedJson.AsList(parsed);
                if (list != null && list.Count > 0)
                {
                    string s = list[0] as string;
                    if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
                }
            }
            return string.Empty;
        }

        public static List<ConversationSummaryInfo> LoadLocalThreeLayerSummaries(
            string geminiPath,
            out Dictionary<string, ProjectMetadata> localProjects,
            out List<string> warnings)
        {
            localProjects = LoadLocalProjects(geminiPath);
            List<ConversationSummaryInfo> summaries = new List<ConversationSummaryInfo>();
            warnings = new List<string>();
            if (string.IsNullOrEmpty(geminiPath)) return summaries;

            HashSet<string> seenCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string pbPath = Path.Combine(geminiPath, "antigravity", "agyhub_summaries_proto.pb");
            if (File.Exists(pbPath))
            {
                try
                {
                    byte[] pbBytes = SessionLifecycleService.ReadAllBytesShared(pbPath);
                    List<ConversationSummaryInfo> ideList = SessionMetadataExtractor.ExtractSummaries(pbBytes, localProjects);
                    for (int i = 0; i < ideList.Count; i++)
                    {
                        ConversationSummaryInfo s = ideList[i];
                        if (s == null) continue;
                        summaries.Add(s);
                        if (!string.IsNullOrEmpty(s.ConversationId))
                            seenCids.Add(s.ConversationId.Trim());
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add("读取本地 summaries proto 异常: " + ex.Message);
                }
            }

            string cliPbPath = Path.Combine(geminiPath, "antigravity-cli", "agyhub_summaries_proto.pb");
            if (File.Exists(cliPbPath))
            {
                try
                {
                    byte[] cliPbBytes = SessionLifecycleService.ReadAllBytesShared(cliPbPath);
                    List<ConversationSummaryInfo> cliList = SessionMetadataExtractor.ExtractSummaries(cliPbBytes, localProjects);
                    for (int i = 0; i < cliList.Count; i++)
                    {
                        ConversationSummaryInfo s = cliList[i];
                        if (s == null) continue;
                        s.IsCliSession = true;
                        if (string.IsNullOrEmpty(s.ConversationId) || seenCids.Add(s.ConversationId.Trim()))
                            summaries.Add(s);
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add("读取 CLI summaries proto 异常: " + ex.Message);
                }
            }

            string summaryDbPath = Path.Combine(geminiPath, "antigravity", "conversation_summaries.db");
            Dictionary<string, SummaryDbRowInfo> dbSnap = null;
            if (File.Exists(summaryDbPath))
            {
                try
                {
                    dbSnap = SummarySqliteBridge.ReadSummaryDbSnapshot(summaryDbPath);
                    if (!string.IsNullOrEmpty(SummarySqliteBridge.LastError))
                    {
                        warnings.Add("读取 conversation_summaries.db 告警（已降级使用 .pb/.pbtxt）: " + SummarySqliteBridge.LastError);
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add("读取 conversation_summaries.db 失败（已降级使用 .pb/.pbtxt）: " + ex.Message);
                }
            }

            SessionMetadataExtractor.OverlayLocalThreeLayerMetadata(summaries, geminiPath, dbSnap, localProjects);

            // Read antigravity-cli/conversation_summaries.db as read-only legacy archive entries
            List<string> cliLegacyWarnings;
            List<LegacyCliArchiveEntry> legacyCliEntries = LoadLegacyCliArchiveEntries(geminiPath, out cliLegacyWarnings);
            if (cliLegacyWarnings != null && cliLegacyWarnings.Count > 0)
                warnings.AddRange(cliLegacyWarnings);

            for (int i = 0; i < legacyCliEntries.Count; i++)
            {
                LegacyCliArchiveEntry le = legacyCliEntries[i];
                if (le != null && !string.IsNullOrEmpty(le.ConversationId) && seenCids.Add(le.ConversationId.Trim()))
                {
                    summaries.Add(le);
                }
            }

            return summaries;
        }

        public static List<string> ComputeShortestDistinguishingLabels(List<string> fullPaths, string commonName)
        {
            List<string> results = new List<string>();
            if (fullPaths == null || fullPaths.Count == 0) return results;

            List<string[]> segList = new List<string[]>(fullPaths.Count);
            int minSegCount = int.MaxValue;
            for (int i = 0; i < fullPaths.Count; i++)
            {
                string norm = (fullPaths[i] ?? string.Empty).Replace('\\', '/').Trim().TrimEnd('/');
                string[] rawSegs = string.IsNullOrEmpty(norm)
                    ? new string[0]
                    : norm.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                segList.Add(rawSegs);
                if (rawSegs.Length > 0 && rawSegs.Length < minSegCount)
                    minSegCount = rawSegs.Length;
            }
            if (minSegCount == int.MaxValue) minSegCount = 0;

            int commonPrefix = 0;
            while (commonPrefix < minSegCount)
            {
                string firstSeg = segList[0][commonPrefix];
                bool allMatch = true;
                for (int i = 1; i < segList.Count; i++)
                {
                    if (segList[i].Length <= commonPrefix ||
                        !string.Equals(segList[i][commonPrefix], firstSeg, StringComparison.OrdinalIgnoreCase))
                    {
                        allMatch = false;
                        break;
                    }
                }
                if (!allMatch) break;
                commonPrefix++;
            }

            for (int i = 0; i < segList.Count; i++)
            {
                string[] segs = segList[i];
                if (segs.Length == 0)
                {
                    results.Add(string.Empty);
                    continue;
                }
                int start = Math.Min(commonPrefix, segs.Length - 1);
                int end = segs.Length;
                if (!string.IsNullOrEmpty(commonName) &&
                    end - start > 1 &&
                    string.Equals(segs[end - 1], commonName, StringComparison.OrdinalIgnoreCase))
                {
                    end--;
                }
                if (end <= start)
                {
                    start = Math.Max(0, segs.Length - 2);
                    end = segs.Length;
                }

                List<string> slice = new List<string>();
                for (int k = start; k < end; k++) slice.Add(segs[k]);
                results.Add(string.Join("/", slice.ToArray()));
            }

            return results;
        }

        public static ProjectGroupsSnapshot BuildProjectDisplayGroups(
            List<ConversationSummaryInfo> summaries,
            Dictionary<string, ProjectMetadata> localProjects,
            string homePath)
        {
            ProjectGroupsSnapshot snap = new ProjectGroupsSnapshot();
            if (summaries == null || summaries.Count == 0) return snap;

            snap.TotalRawSessions = summaries.Count;

            Dictionary<string, ProjectDisplayGroup> formalMap = new Dictionary<string, ProjectDisplayGroup>(StringComparer.OrdinalIgnoreCase);
            ProjectDisplayGroup cliGroup = new ProjectDisplayGroup
            {
                BucketKey = "AUX:CLI",
                GroupKind = "cli",
                ProjectName = "CLI 命令行会话",
                ProjectId = "default-cli-project",
                ShortPath = "antigravity-cli"
            };
            ProjectDisplayGroup outsideGroup = new ProjectDisplayGroup
            {
                BucketKey = "AUX:OUTSIDE",
                GroupKind = "outside",
                ProjectName = "未归属工程 (Outside)",
                ProjectId = "outside-of-project",
                ShortPath = "全局临时工作区"
            };
            ProjectDisplayGroup subagentGroup = new ProjectDisplayGroup
            {
                BucketKey = "AUX:SUBAGENTS",
                GroupKind = "subagents",
                ProjectName = "后台子任务 (Subagents)",
                ShortPath = "由主对话自动派生"
            };
            ProjectDisplayGroup ephemeralGroup = new ProjectDisplayGroup
            {
                BucketKey = "AUX:EPHEMERAL",
                GroupKind = "ephemeral",
                ProjectName = "临时与测试工程",
                ShortPath = "temp / eval / playground"
            };

            for (int i = 0; i < summaries.Count; i++)
            {
                ConversationSummaryInfo s = summaries[i];
                if (s == null) continue;

                if (s.IsSubagent)
                {
                    snap.SubagentSessions++;
                    subagentGroup.Conversations.Add(s);
                    subagentGroup.TotalCount++;
                    if (s.UpdatedTime > subagentGroup.LatestUpdatedTime) subagentGroup.LatestUpdatedTime = s.UpdatedTime;
                    continue;
                }

                if (s.IsCliSession || string.Equals(s.ProjectId, "default-cli-project", StringComparison.OrdinalIgnoreCase))
                {
                    snap.CliSessions++;
                    cliGroup.Conversations.Add(s);
                    cliGroup.TotalCount++;
                    cliGroup.PrimaryCount++;
                    if (s.UpdatedTime > cliGroup.LatestUpdatedTime) cliGroup.LatestUpdatedTime = s.UpdatedTime;
                    continue;
                }

                if (VisualDiffEngine.IsOutsideProject(s.ProjectName) ||
                    string.Equals(s.ProjectId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
                {
                    snap.OutsideSessions++;
                    outsideGroup.Conversations.Add(s);
                    outsideGroup.TotalCount++;
                    outsideGroup.PrimaryCount++;
                    if (s.UpdatedTime > outsideGroup.LatestUpdatedTime) outsideGroup.LatestUpdatedTime = s.UpdatedTime;
                    continue;
                }

                if (SmartProjectFilter.IsJunkOrEphemeral(s.ProjectName))
                {
                    snap.EphemeralSessions++;
                    ephemeralGroup.Conversations.Add(s);
                    ephemeralGroup.TotalCount++;
                    ephemeralGroup.PrimaryCount++;
                    if (s.UpdatedTime > ephemeralGroup.LatestUpdatedTime) ephemeralGroup.LatestUpdatedTime = s.UpdatedTime;
                    continue;
                }

                snap.FormalPrimarySessions++;
                string pName = string.IsNullOrWhiteSpace(s.ProjectName) ? "未命名工程" : s.ProjectName.Trim();
                string pId = !string.IsNullOrEmpty(s.ProjectId) ? s.ProjectId.Trim() : string.Empty;
                string bucketKey = !string.IsNullOrEmpty(pId)
                    ? ("ID:" + pId)
                    : ("GHOST:" + SessionLifecycleService.BuildGhostProjectKey(pName, s.FolderUri));

                ProjectDisplayGroup grp;
                if (!formalMap.TryGetValue(bucketKey, out grp))
                {
                    ProjectMetadata pm = null;
                    if (!string.IsNullOrEmpty(pId) && localProjects != null)
                        localProjects.TryGetValue(pId, out pm);

                    string effectiveUri = (pm != null && !string.IsNullOrEmpty(pm.FolderUri)) ? pm.FolderUri : s.FolderUri;
                    string fullPath = UriToDisplayPath(effectiveUri);
                    List<string> roots = new List<string>();
                    if (pm != null && pm.FolderUris != null && pm.FolderUris.Count > 0)
                    {
                        for (int u = 0; u < pm.FolderUris.Count; u++)
                        {
                            string dp = UriToDisplayPath(pm.FolderUris[u]);
                            if (!string.IsNullOrEmpty(dp)) roots.Add(dp);
                        }
                    }
                    else if (!string.IsNullOrEmpty(fullPath))
                    {
                        roots.Add(fullPath);
                    }

                    bool isMulti = roots.Count > 1;
                    string shortPath = isMulti
                        ? string.Format("多目录 · {0} 个根目录 ({1})", roots.Count, ShortenHomeDisplayPath(fullPath, homePath))
                        : ShortenHomeDisplayPath(fullPath, homePath);
                    if (string.IsNullOrEmpty(shortPath)) shortPath = "未绑定本地路径";

                    grp = new ProjectDisplayGroup
                    {
                        BucketKey = bucketKey,
                        GroupKind = "formal",
                        ProjectName = (pm != null && !string.IsNullOrEmpty(pm.Name)) ? pm.Name : pName,
                        ProjectId = pId,
                        FolderUri = effectiveUri,
                        FullPath = fullPath,
                        ShortPath = shortPath,
                        IsMultiRoot = isMulti,
                        RootPaths = roots
                    };
                    formalMap[bucketKey] = grp;
                }

                grp.Conversations.Add(s);
                grp.TotalCount++;
                grp.PrimaryCount++;
                if (s.UpdatedTime > grp.LatestUpdatedTime) grp.LatestUpdatedTime = s.UpdatedTime;
            }

            List<ProjectDisplayGroup> formalList = new List<ProjectDisplayGroup>(formalMap.Values);
            Dictionary<string, List<ProjectDisplayGroup>> byName = new Dictionary<string, List<ProjectDisplayGroup>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < formalList.Count; i++)
            {
                ProjectDisplayGroup g = formalList[i];
                g.Conversations.Sort(delegate(ConversationSummaryInfo a, ConversationSummaryInfo b)
                {
                    return DateTime.Compare(b.UpdatedTime, a.UpdatedTime);
                });
                List<ProjectDisplayGroup> cluster;
                if (!byName.TryGetValue(g.ProjectName, out cluster))
                {
                    cluster = new List<ProjectDisplayGroup>();
                    byName[g.ProjectName] = cluster;
                }
                cluster.Add(g);
            }

            List<List<ProjectDisplayGroup>> clusters = new List<List<ProjectDisplayGroup>>(byName.Values);
            for (int i = 0; i < clusters.Count; i++)
            {
                List<ProjectDisplayGroup> cluster = clusters[i];
                cluster.Sort(delegate(ProjectDisplayGroup a, ProjectDisplayGroup b)
                {
                    return DateTime.Compare(b.LatestUpdatedTime, a.LatestUpdatedTime);
                });
                if (cluster.Count > 1)
                {
                    List<string> fulls = new List<string>(cluster.Count);
                    for (int k = 0; k < cluster.Count; k++) fulls.Add(cluster[k].FullPath);
                    List<string> shortDist = ComputeShortestDistinguishingLabels(fulls, cluster[0].ProjectName);
                    for (int k = 0; k < cluster.Count; k++)
                    {
                        cluster[k].IsDuplicateBasename = true;
                        string dist = shortDist[k];
                        if (!string.IsNullOrEmpty(dist))
                        {
                            cluster[k].DistinguishingLabel = dist;
                            if (!cluster[k].IsMultiRoot)
                                cluster[k].ShortPath = dist;
                        }
                    }
                }
            }

            clusters.Sort(delegate(List<ProjectDisplayGroup> a, List<ProjectDisplayGroup> b)
            {
                DateTime ta = a.Count > 0 ? a[0].LatestUpdatedTime : DateTime.MinValue;
                DateTime tb = b.Count > 0 ? b[0].LatestUpdatedTime : DateTime.MinValue;
                return DateTime.Compare(tb, ta);
            });

            for (int i = 0; i < clusters.Count; i++)
            {
                for (int k = 0; k < clusters[i].Count; k++)
                    snap.Groups.Add(clusters[i][k]);
            }
            snap.FormalProjectsCount = formalList.Count;

            Action<ProjectDisplayGroup> appendAux = delegate(ProjectDisplayGroup g)
            {
                if (g.Conversations.Count > 0)
                {
                    g.Conversations.Sort(delegate(ConversationSummaryInfo a, ConversationSummaryInfo b)
                    {
                        return DateTime.Compare(b.UpdatedTime, a.UpdatedTime);
                    });
                    snap.Groups.Add(g);
                }
            };
            appendAux(cliGroup);
            appendAux(outsideGroup);
            appendAux(subagentGroup);
            appendAux(ephemeralGroup);

            return snap;
        }

        public static OrphanScanResult ScanOrphansReadOnly(string geminiPath)
        {
            return SessionLifecycleService.ScanOrphanedData(geminiPath);
        }

        public static bool CleanOrphansSafe(
            string geminiPath,
            bool closeIdeIfRunning,
            Action<string> onLog,
            out DeletionResult result,
            out string error)
        {
            return CleanOrphansSafe(geminiPath, closeIdeIfRunning, false, onLog, out result, out error);
        }

        public static bool CleanOrphansSafe(
            string geminiPath,
            bool closeIdeIfRunning,
            bool forceKillUnresponsive,
            Action<string> onLog,
            out DeletionResult result,
            out string error)
        {
            result = null;
            error = null;
            if (string.IsNullOrEmpty(geminiPath) || !Directory.Exists(geminiPath))
            {
                error = "未检测到有效的 .gemini 目录。";
                return false;
            }

            if (ProcessGuardService.IsAntigravityRunning())
            {
                if (!closeIdeIfRunning)
                {
                    error = "Antigravity IDE 正在运行，请确认先关闭 IDE 后再执行物理清理。";
                    return false;
                }
                if (!ProcessGuardService.CloseAntigravityProcesses(forceKillUnresponsive, onLog, out error))
                {
                    return false;
                }
            }

            OrphanScanResult freshScan = SessionLifecycleService.ScanOrphanedData(geminiPath);
            if (freshScan.TotalOrphanItems == 0)
            {
                result = new DeletionResult();
                return true;
            }

            result = SessionLifecycleService.CleanOrphanedData(freshScan);
            return true;
        }

        public static void LoadCapabilityMatrix(
            string geminiPath,
            string homePath,
            string repoPath,
            string customBaselinePath,
            out List<CapabilityItem> localCaps,
            out List<CapabilityItem> repoCaps,
            out List<CapabilityDiffNode> diffs,
            out Dictionary<string, string> baseline)
        {
            localCaps = CapabilityVaultService.ScanLocalCapabilities(geminiPath, homePath);
            repoCaps = CapabilityVaultService.ScanRepositoryCapabilities(repoPath, homePath, geminiPath);
            baseline = CapabilitySyncBaselineStore.Load(customBaselinePath);
            diffs = VisualDiffEngine.ComputeCapabilityDiff(localCaps, repoCaps, baseline);
            if (CapabilitySyncBaselineStore.RecordAlignedFromDiffs(baseline, diffs))
            {
                CapabilitySyncBaselineStore.Save(customBaselinePath, baseline);
            }
        }
    }

    /// <summary>
    /// 专职在独立 STA 线程弹出原生 Win32 文件/目录选择对话框
    /// </summary>
    public static class NativeDialogBridge
    {
        private static int _dialogBusy = 0;

        public static string PickOpenFile(string title, string filter, out bool busy)
        {
            return RunStaDialog(delegate(Form owner)
            {
                using (OpenFileDialog dlg = new OpenFileDialog())
                {
                    dlg.Title = string.IsNullOrEmpty(title) ? "选择文件" : title;
                    dlg.Filter = string.IsNullOrEmpty(filter) ? "所有文件 (*.*)|*.*" : filter;
                    if (dlg.ShowDialog(owner) == DialogResult.OK)
                        return dlg.FileName;
                    return null;
                }
            }, out busy);
        }

        public static string PickSaveFile(string title, string filter, string defaultFileName, out bool busy)
        {
            return RunStaDialog(delegate(Form owner)
            {
                using (SaveFileDialog dlg = new SaveFileDialog())
                {
                    dlg.Title = string.IsNullOrEmpty(title) ? "保存文件" : title;
                    dlg.Filter = string.IsNullOrEmpty(filter) ? "所有文件 (*.*)|*.*" : filter;
                    if (!string.IsNullOrEmpty(defaultFileName))
                        dlg.FileName = defaultFileName;
                    if (dlg.ShowDialog(owner) == DialogResult.OK)
                        return dlg.FileName;
                    return null;
                }
            }, out busy);
        }

        public static string PickFolder(string title, string initialPath, out bool busy)
        {
            return RunStaDialog(delegate(Form owner)
            {
                string vistaResult;
                if (TryPickFolderVista(owner.Handle, title, initialPath, out vistaResult))
                    return vistaResult;

                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = string.IsNullOrEmpty(title) ? "选择目录" : title;
                    if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
                        fbd.SelectedPath = initialPath;
                    if (fbd.ShowDialog(owner) == DialogResult.OK)
                        return fbd.SelectedPath;
                    return null;
                }
            }, out busy);
        }

        private static string RunStaDialog(Func<Form, string> action, out bool busy)
        {
            if (Interlocked.CompareExchange(ref _dialogBusy, 1, 0) != 0)
            {
                busy = true;
                return null;
            }
            busy = false;

            string selected = null;
            Thread t = new Thread(delegate()
            {
                Form owner = null;
                try
                {
                    Application.EnableVisualStyles();
                    owner = new Form
                    {
                        StartPosition = FormStartPosition.Manual,
                        Location = new Point(-32000, -32000),
                        Size = new Size(1, 1),
                        ShowInTaskbar = false,
                        FormBorderStyle = FormBorderStyle.None,
                        TopMost = true
                    };
                    owner.Show();
                    owner.Activate();
                    selected = action(owner);
                }
                catch
                {
                    selected = null;
                }
                finally
                {
                    if (owner != null)
                    {
                        try { owner.Close(); owner.Dispose(); } catch { }
                    }
                }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            try
            {
                t.Start();
                t.Join();
            }
            finally
            {
                Interlocked.Exchange(ref _dialogBusy, 0);
            }
            return selected;
        }

        #region Vista IFileOpenDialog Folder Picker COM Interop

        private const uint FOS_PICKFOLDERS = 0x00000020;
        private const uint FOS_FORCEFILESYSTEM = 0x00000040;
        private const uint FOS_PATHMUSTEXIST = 0x00000800;
        private const uint SIGDN_FILESYSPATH = 0x80058000;
        private static readonly Guid CLSID_FileOpenDialog = new Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7");
        private static readonly Guid IID_IShellItem = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

        [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(uint fos);
            void GetOptions(out uint pfos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace(IShellItem psi, int fdap);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close(int hr);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr pFilter);
            void GetResults(out IntPtr ppenum);
            void GetSelectedItems(out IntPtr ppsai);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        private static bool TryPickFolderVista(IntPtr ownerHwnd, string title, string initialPath, out string selectedPath)
        {
            selectedPath = null;
            IFileOpenDialog dialog = null;
            IShellItem initItem = null;
            IShellItem resultItem = null;
            try
            {
                Type dlgType = Type.GetTypeFromCLSID(CLSID_FileOpenDialog, false);
                if (dlgType == null) return false;
                dialog = Activator.CreateInstance(dlgType) as IFileOpenDialog;
                if (dialog == null) return false;

                uint options;
                dialog.GetOptions(out options);
                dialog.SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST);
                if (!string.IsNullOrEmpty(title)) dialog.SetTitle(title);

                if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
                {
                    Guid iid = IID_IShellItem;
                    if (SHCreateItemFromParsingName(initialPath, IntPtr.Zero, ref iid, out initItem) == 0 && initItem != null)
                        dialog.SetFolder(initItem);
                }

                int hr = dialog.Show(ownerHwnd);
                if (hr == 0)
                {
                    dialog.GetResult(out resultItem);
                    if (resultItem != null)
                    {
                        string pathOut;
                        resultItem.GetDisplayName(SIGDN_FILESYSPATH, out pathOut);
                        selectedPath = pathOut;
                    }
                    return true;
                }
                return unchecked((uint)hr) == 0x800704C7;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (resultItem != null) try { Marshal.FinalReleaseComObject(resultItem); } catch { }
                if (initItem != null) try { Marshal.FinalReleaseComObject(initItem); } catch { }
                if (dialog != null) try { Marshal.FinalReleaseComObject(dialog); } catch { }
            }
        }

        #endregion
    }
}
