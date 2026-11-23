using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // HEADLESS APPLICATION SERVICES (UI-Decoupled Business Orchestration)
    // Shared cleanly by LocalWebHost (V0.8 Web UI) & MainForm (--legacy-ui)
    // Strictly C# 5.0 (.NET Framework 4.0+ csc.exe)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Antigravity 主进程与专属 language_server 侦测及安全父优先关闭服务
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

        public static bool CloseAntigravityProcesses(bool forceKillUnresponsive, Action<string> onLog, out string errorMsg)
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
                        if (!forceKillUnresponsive)
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
                    while (lsWait.ElapsedMilliseconds < 450)
                    {
                        bool anyLsAlive = false;
                        for (int i = 0; i < remainingLs.Count; i++)
                        {
                            try
                            {
                                if (!remainingLs[i].HasExited)
                                {
                                    remainingLs[i].WaitForExit(100);
                                    if (!remainingLs[i].HasExited) anyLsAlive = true;
                                }
                            }
                            catch { }
                        }
                        if (!anyLsAlive) break;
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

    /// <summary>
    /// 工程与会话分组显示模型（统一短路径、多根目录展示与同名最短区分父目录）
    /// </summary>
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
    /// 无 UI 的工作区加载、工程路径分组、能力矩阵及两阶段安全孤儿 GC 服务
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
            string escId = JsonKeyManipulator.EscapeJsonString(projId ?? string.Empty);
            string escName = JsonKeyManipulator.EscapeJsonString(projName ?? string.Empty);
            string escUri = JsonKeyManipulator.EscapeJsonString(LocalWorkspaceManager.ToFileUri(folderUri));
            string nowIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffff000Z");

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
                    string json = LocalWorkspaceManager.ReadUtf8TextShared(files[i]);
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

        public static List<ConversationSummaryInfo> LoadLocalThreeLayerSummaries(
            string geminiPath,
            out Dictionary<string, ProjectMetadata> localProjects,
            out List<string> warnings)
        {
            localProjects = LoadLocalProjects(geminiPath);
            List<ConversationSummaryInfo> summaries = new List<ConversationSummaryInfo>();
            warnings = new List<string>();
            if (string.IsNullOrEmpty(geminiPath)) return summaries;

            string pbPath = Path.Combine(geminiPath, "antigravity", "agyhub_summaries_proto.pb");
            if (File.Exists(pbPath))
            {
                try
                {
                    byte[] pbBytes = LocalWorkspaceManager.ReadAllBytesShared(pbPath);
                    summaries = SessionMetadataExtractor.ExtractSummaries(pbBytes, localProjects);
                }
                catch (Exception ex)
                {
                    warnings.Add("读取本地 summaries proto 异常: " + ex.Message);
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
            return summaries;
        }

        /// <summary>
        /// 对同名多工作区计算最短区分父目录段（剥离共同祖先前缀与末尾同名目录名）
        /// </summary>
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
                    : ("GHOST:" + LocalWorkspaceManager.BuildGhostProjectKey(pName, s.FolderUri));

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
            return LocalWorkspaceManager.ScanOrphanedData(geminiPath);
        }

        public static bool CleanOrphansSafe(
            string geminiPath,
            bool closeIdeIfRunning,
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
                if (!ProcessGuardService.CloseAntigravityProcesses(true, onLog, out error))
                {
                    return false;
                }
            }

            // 关闭 IDE 后重新扫描真值集，杜绝只读扫描到执行清理之间的 TOCTOU 误删
            OrphanScanResult freshScan = LocalWorkspaceManager.ScanOrphanedData(geminiPath);
            if (freshScan.TotalOrphanItems == 0)
            {
                result = new DeletionResult();
                return true;
            }

            result = LocalWorkspaceManager.CleanOrphanedData(freshScan);
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
            localCaps = CapabilityRepositoryManager.ScanLocalCapabilities(geminiPath, homePath);
            repoCaps = CapabilityRepositoryManager.ScanRepositoryCapabilities(repoPath, homePath, geminiPath);
            baseline = CapabilitySyncBaselineStore.Load(customBaselinePath);
            diffs = VisualDiffEngine.ComputeCapabilityDiff(localCaps, repoCaps, baseline);
            if (CapabilitySyncBaselineStore.RecordAlignedFromDiffs(baseline, diffs))
            {
                CapabilitySyncBaselineStore.Save(customBaselinePath, baseline);
            }
        }
    }

    public sealed class ResolvedImportProjectInfo
    {
        public string TargetId;
        public string BackupId;
        public string ProjectName;
        public string FolderUri;
    }

    public sealed class SelectiveImportRequest
    {
        public string GeminiPath;
        public string HomePath;
        public RightPaneSourceMode SourceMode;
        public string LoadedBackupPath;
        public BackupArchiveIndex BackupIndex;
        public Dictionary<string, ProjectMetadata> LocalProjects;
        public List<ProjectDiffNode> CurrentDiffNodes;
        public List<CapabilityItem> SelectedCapabilities;
        public List<ConversationSummaryInfo> SelectedConversations;
        public Dictionary<string, ProjectDiffNode> ConvProjDiffMap;
    }

    public sealed class SelectiveImportResult
    {
        public int FilesImported;
        public int CapabilitiesImported;
        public int ConversationsImported;
        public List<string> ImportedCapKeys = new List<string>();
        public List<string> ImportedConvIds = new List<string>();
        public List<string> ImportedProjNames = new List<string>();
        public List<string> Warnings = new List<string>();
        public string ErrorMessage;
    }

    /// <summary>
    /// 选择性导入与全量备份无 UI 事务服务
    /// </summary>
    public static class ArchiveTransferService
    {
        public static SelectiveImportResult PerformSelectiveImport(
            SelectiveImportRequest req,
            Action<string, int> onProgress)
        {
            SelectiveImportResult workRes = new SelectiveImportResult();
            if (req == null || string.IsNullOrEmpty(req.GeminiPath))
            {
                workRes.ErrorMessage = "无效的导入请求参数。";
                return workRes;
            }

            List<CapabilityItem> selectedCaps = req.SelectedCapabilities ?? new List<CapabilityItem>();
            List<ConversationSummaryInfo> selectedConvs = req.SelectedConversations ?? new List<ConversationSummaryInfo>();
            if (selectedCaps.Count == 0 && selectedConvs.Count == 0)
            {
                workRes.ErrorMessage = "未选择任何能力配置或会话。";
                return workRes;
            }

            string geminiSnap = req.GeminiPath;
            string curHomeSnap = req.HomePath ?? WorkspaceService.ResolveDefaultHomePath();
            string srcHomeSnap = (req.BackupIndex != null && !string.IsNullOrEmpty(req.BackupIndex.SourceHome)) ? req.BackupIndex.SourceHome : string.Empty;
            Dictionary<string, ProjectMetadata> localProjectsSnap = req.LocalProjects != null
                ? new Dictionary<string, ProjectMetadata>(req.LocalProjects, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ProjectMetadata> backupProjectsSnap = (req.BackupIndex != null && req.BackupIndex.Projects != null)
                ? new Dictionary<string, ProjectMetadata>(req.BackupIndex.Projects, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
            List<ProjectDiffNode> diffNodesSnap = req.CurrentDiffNodes ?? new List<ProjectDiffNode>();
            Dictionary<string, ProjectDiffNode> convProjDiffMap = req.ConvProjDiffMap ?? new Dictionary<string, ProjectDiffNode>(StringComparer.OrdinalIgnoreCase);

            Dictionary<string, string> projectUriOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> projectNewIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> seenNameNewId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> ambiguousNewIdNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> seenNameUri = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> ambiguousUriNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> generatedProjIdsByUriOrName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ResolvedImportProjectInfo> neededProjectsByTargetId = new Dictionary<string, ResolvedImportProjectInfo>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> convIdsToExtract = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ConversationSummaryInfo> convMap = new Dictionary<string, ConversationSummaryInfo>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> convResolvedProjId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> convResolvedProjName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int c = 0; c < selectedCaps.Count; c++)
            {
                if (selectedCaps[c] != null && !string.IsNullOrEmpty(selectedCaps[c].UniqueKey))
                    workRes.ImportedCapKeys.Add(selectedCaps[c].UniqueKey);
            }

            HashSet<string> initProjSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < selectedConvs.Count; i++)
            {
                ConversationSummaryInfo sc = selectedConvs[i];
                if (sc == null || string.IsNullOrEmpty(sc.ConversationId)) continue;
                convIdsToExtract.Add(sc.ConversationId);
                convMap[sc.ConversationId] = sc;
                workRes.ImportedConvIds.Add(sc.ConversationId);

                ProjectDiffNode pDiff;
                if (!convProjDiffMap.TryGetValue(sc.ConversationId, out pDiff) || pDiff == null)
                    pDiff = FindProjectDiffForBackupConv(diffNodesSnap, sc.ConversationId);

                bool isOutside = VisualDiffEngine.IsOutsideProject(sc.ProjectName) ||
                                 string.Equals(sc.ProjectId, "outside-of-project", StringComparison.OrdinalIgnoreCase);
                if (isOutside)
                {
                    convResolvedProjId[sc.ConversationId] = "outside-of-project";
                    convResolvedProjName[sc.ConversationId] = "Outside of Project";
                    if (initProjSeen.Add("Outside of Project")) workRes.ImportedProjNames.Add("Outside of Project");
                    continue;
                }

                string localProjId = null;
                string localProjName = null;
                string localFolderUri = pDiff != null ? pDiff.LocalFolderUri : null;

                if (pDiff != null && pDiff.LocalConversations != null)
                {
                    for (int li = 0; li < pDiff.LocalConversations.Count; li++)
                    {
                        ConversationSummaryInfo lc = pDiff.LocalConversations[li];
                        if (lc != null && !string.IsNullOrEmpty(lc.ProjectId) && localProjectsSnap.ContainsKey(lc.ProjectId))
                        {
                            localProjId = lc.ProjectId;
                            localProjName = localProjectsSnap[lc.ProjectId].Name;
                            if (string.IsNullOrEmpty(localFolderUri))
                                localFolderUri = localProjectsSnap[lc.ProjectId].FolderUri;
                            break;
                        }
                    }
                }

                string remappedBackupUri = !string.IsNullOrEmpty(sc.FolderUri)
                    ? DirectStreamArchiver.RemapAllPathFormats(sc.FolderUri, srcHomeSnap, curHomeSnap)
                    : null;

                if (string.IsNullOrEmpty(localProjId))
                {
                    string lookupUri = !string.IsNullOrEmpty(localFolderUri) ? localFolderUri : remappedBackupUri;
                    if (!string.IsNullOrEmpty(lookupUri))
                    {
                        foreach (KeyValuePair<string, ProjectMetadata> kvp in localProjectsSnap)
                        {
                            if (DoesProjectMetadataMatchUri(kvp.Value, lookupUri))
                            {
                                localProjId = kvp.Key;
                                localProjName = kvp.Value.Name;
                                localFolderUri = kvp.Value.FolderUri;
                                break;
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(localProjId) && !string.IsNullOrEmpty(sc.ProjectName) && !SmartProjectFilter.IsJunkOrEphemeral(sc.ProjectName))
                {
                    foreach (KeyValuePair<string, ProjectMetadata> kvp in localProjectsSnap)
                    {
                        if (!string.Equals(kvp.Value.Name, sc.ProjectName, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!string.IsNullOrEmpty(remappedBackupUri) && !string.IsNullOrEmpty(kvp.Value.FolderUri) &&
                            !DoesProjectMetadataMatchUri(kvp.Value, remappedBackupUri))
                            continue;

                        localProjId = kvp.Key;
                        localProjName = kvp.Value.Name;
                        if (string.IsNullOrEmpty(localFolderUri)) localFolderUri = kvp.Value.FolderUri;
                        break;
                    }
                }

                string backupProjId = !string.IsNullOrEmpty(sc.ProjectId) ? sc.ProjectId : null;
                if (string.IsNullOrEmpty(backupProjId))
                {
                    if (!string.IsNullOrEmpty(sc.FolderUri))
                    {
                        foreach (KeyValuePair<string, ProjectMetadata> bKvp in backupProjectsSnap)
                        {
                            if (DoesProjectMetadataMatchUri(bKvp.Value, sc.FolderUri))
                            {
                                backupProjId = bKvp.Key;
                                break;
                            }
                        }
                    }
                    if (string.IsNullOrEmpty(backupProjId) && !string.IsNullOrEmpty(sc.ProjectName))
                    {
                        foreach (KeyValuePair<string, ProjectMetadata> bKvp in backupProjectsSnap)
                        {
                            if (!string.Equals(bKvp.Value.Name, sc.ProjectName, StringComparison.OrdinalIgnoreCase)) continue;
                            if (!string.IsNullOrEmpty(sc.FolderUri) && !string.IsNullOrEmpty(bKvp.Value.FolderUri) &&
                                !DoesProjectMetadataMatchUri(bKvp.Value, sc.FolderUri))
                                continue;
                            backupProjId = bKvp.Key;
                            break;
                        }
                    }
                }

                string effectiveProjName = !string.IsNullOrEmpty(localProjName) ? localProjName : sc.ProjectName;
                string effectiveUri = localFolderUri;
                if (string.IsNullOrEmpty(effectiveUri) && !string.IsNullOrEmpty(remappedBackupUri))
                    effectiveUri = remappedBackupUri;
                if (string.IsNullOrEmpty(effectiveUri) && !string.IsNullOrEmpty(effectiveProjName))
                    effectiveUri = LocalWorkspaceManager.ToFileUri(Path.Combine(curHomeSnap, effectiveProjName));

                string effectiveProjId = localProjId;
                if (string.IsNullOrEmpty(effectiveProjId))
                {
                    if (!string.IsNullOrEmpty(backupProjId) && !localProjectsSnap.ContainsKey(backupProjId))
                    {
                        effectiveProjId = backupProjId;
                    }
                    else
                    {
                        string genKey = !string.IsNullOrEmpty(effectiveUri)
                            ? ("URI:" + WorkspaceService.UriToDisplayPath(effectiveUri).TrimEnd('\\', '/'))
                            : ("NAME:" + (effectiveProjName ?? sc.ConversationId));
                        if (!generatedProjIdsByUriOrName.TryGetValue(genKey, out effectiveProjId))
                        {
                            effectiveProjId = Guid.NewGuid().ToString();
                            generatedProjIdsByUriOrName[genKey] = effectiveProjId;
                        }
                    }
                }

                projectNewIds["CID:" + sc.ConversationId] = effectiveProjId;
                if (!string.IsNullOrEmpty(sc.ProjectId) &&
                    !string.Equals(sc.ProjectId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
                {
                    projectNewIds["ID:" + sc.ProjectId] = effectiveProjId;
                    SetUnambiguousMapping(projectNewIds, seenNameNewId, ambiguousNewIdNames, sc.ProjectId, effectiveProjId);
                }
                SetUnambiguousMapping(projectNewIds, seenNameNewId, ambiguousNewIdNames, sc.ProjectName, effectiveProjId);
                SetUnambiguousMapping(projectNewIds, seenNameNewId, ambiguousNewIdNames, effectiveProjName, effectiveProjId);

                if (!string.IsNullOrEmpty(effectiveUri))
                {
                    projectUriOverrides["CID:" + sc.ConversationId] = effectiveUri;
                    if (!string.IsNullOrEmpty(sc.ProjectId) &&
                        !string.Equals(sc.ProjectId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
                    {
                        projectUriOverrides["ID:" + sc.ProjectId] = effectiveUri;
                        SetUnambiguousMapping(projectUriOverrides, seenNameUri, ambiguousUriNames, sc.ProjectId, effectiveUri);
                    }
                    if (!string.IsNullOrEmpty(effectiveProjId) &&
                        !string.Equals(effectiveProjId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
                    {
                        projectUriOverrides["ID:" + effectiveProjId] = effectiveUri;
                        SetUnambiguousMapping(projectUriOverrides, seenNameUri, ambiguousUriNames, effectiveProjId, effectiveUri);
                    }
                    SetUnambiguousMapping(projectUriOverrides, seenNameUri, ambiguousUriNames, sc.ProjectName, effectiveUri);
                    SetUnambiguousMapping(projectUriOverrides, seenNameUri, ambiguousUriNames, effectiveProjName, effectiveUri);
                }

                convResolvedProjId[sc.ConversationId] = effectiveProjId;
                convResolvedProjName[sc.ConversationId] = effectiveProjName;
                if (!string.IsNullOrEmpty(effectiveProjName) && initProjSeen.Add(effectiveProjName))
                    workRes.ImportedProjNames.Add(effectiveProjName);

                if (!string.IsNullOrEmpty(effectiveProjName) &&
                    !SmartProjectFilter.IsJunkOrEphemeral(effectiveProjName) &&
                    !neededProjectsByTargetId.ContainsKey(effectiveProjId))
                {
                    neededProjectsByTargetId[effectiveProjId] = new ResolvedImportProjectInfo
                    {
                        TargetId = effectiveProjId,
                        BackupId = backupProjId,
                        ProjectName = effectiveProjName,
                        FolderUri = effectiveUri
                    };
                }
            }

            if (selectedCaps.Count > 0)
            {
                if (onProgress != null) onProgress("正在按键值级无损合流所选能力配置 (Skills / Rules / MCP / Plugins / Hooks)...", 20);
                workRes.FilesImported += CapabilityRepositoryManager.ImportCapabilitiesToLocal(geminiSnap, curHomeSnap, selectedCaps);
                workRes.CapabilitiesImported = selectedCaps.Count;
            }

            if (selectedConvs.Count > 0 && req.SourceMode == RightPaneSourceMode.BackupArchive &&
                !string.IsNullOrEmpty(req.LoadedBackupPath) && File.Exists(req.LoadedBackupPath))
            {
                string convDir = Path.Combine(geminiSnap, "antigravity", "conversations");
                string brainDir = Path.Combine(geminiSnap, "antigravity", "brain");
                string annotationsDir = Path.Combine(geminiSnap, "antigravity", "annotations");
                string projDir = Path.Combine(geminiSnap, "config", "projects");
                if (!Directory.Exists(convDir)) Directory.CreateDirectory(convDir);
                if (!Directory.Exists(brainDir)) Directory.CreateDirectory(brainDir);
                if (!Directory.Exists(annotationsDir)) Directory.CreateDirectory(annotationsDir);
                if (!Directory.Exists(projDir)) Directory.CreateDirectory(projDir);

                HashSet<string> cleanedConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> extractedAnnotationCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, string> extractedDbPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                Dictionary<string, ResolvedImportProjectInfo> byBackupProjId = new Dictionary<string, ResolvedImportProjectInfo>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, ResolvedImportProjectInfo> byProjName = new Dictionary<string, ResolvedImportProjectInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, ResolvedImportProjectInfo> kvp in neededProjectsByTargetId)
                {
                    ResolvedImportProjectInfo pInfo = kvp.Value;
                    if (!string.IsNullOrEmpty(pInfo.BackupId) && !byBackupProjId.ContainsKey(pInfo.BackupId))
                        byBackupProjId[pInfo.BackupId] = pInfo;
                    if (!string.IsNullOrEmpty(pInfo.ProjectName) &&
                        !ambiguousUriNames.Contains(pInfo.ProjectName) &&
                        !byProjName.ContainsKey(pInfo.ProjectName))
                        byProjName[pInfo.ProjectName] = pInfo;
                }

                using (FileStream fs = new FileStream(req.LoadedBackupPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    if (onProgress != null) onProgress("正在安全提取工程定义、对话数据库、标注与脑区文件...", 45);
                    for (int i = 0; i < archive.Entries.Count; i++)
                    {
                        ZipArchiveEntry entry = archive.Entries[i];
                        string rawNorm = (entry.FullName ?? string.Empty).Replace('\\', '/').TrimStart('/');
                        if (string.IsNullOrEmpty(rawNorm) || rawNorm.EndsWith("/")) continue;
                        string norm = rawNorm.StartsWith("data/", StringComparison.OrdinalIgnoreCase) ? rawNorm.Substring(5) : rawNorm;

                        if (norm.StartsWith("antigravity/conversations/", StringComparison.OrdinalIgnoreCase))
                        {
                            string fn = norm.Substring("antigravity/conversations/".Length);
                            if (string.IsNullOrEmpty(fn) || fn.IndexOf('/') >= 0) continue;
                            if (fn.EndsWith(".db-shm", StringComparison.OrdinalIgnoreCase)) continue;

                            string cid = null;
                            if (fn.EndsWith(".db-wal", StringComparison.OrdinalIgnoreCase)) cid = fn.Substring(0, fn.Length - 7);
                            else if (fn.EndsWith(".db", StringComparison.OrdinalIgnoreCase)) cid = fn.Substring(0, fn.Length - 3);
                            else continue;

                            if (!SessionMetadataExtractor.IsLikelyConversationId(cid) || !convIdsToExtract.Contains(cid))
                                continue;

                            if (cleanedConvIds.Add(cid))
                            {
                                string existingDbPath = Path.Combine(convDir, cid + ".db");
                                if (File.Exists(existingDbPath))
                                {
                                    string dbBakPath = existingDbPath + ".bak";
                                    try { if (File.Exists(dbBakPath)) File.SetAttributes(dbBakPath, FileAttributes.Normal); } catch { }
                                    File.Copy(existingDbPath, dbBakPath, true);
                                }
                                DeleteFileIfExistsQuiet(existingDbPath);
                                DeleteFileIfExistsQuiet(Path.Combine(convDir, cid + ".db-wal"));
                                DeleteFileIfExistsQuiet(Path.Combine(convDir, cid + ".db-shm"));
                            }

                            string dest = CapabilityRepositoryManager.CombineAndEnsureUnderRoot(convDir, fn);
                            ExtractZipEntrySafe(entry, dest);
                            workRes.FilesImported++;

                            if (fn.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                                extractedDbPaths[cid] = dest;
                        }
                        else if (norm.StartsWith("antigravity/annotations/", StringComparison.OrdinalIgnoreCase) &&
                                 norm.EndsWith(".pbtxt", StringComparison.OrdinalIgnoreCase))
                        {
                            string filePart = norm.Substring("antigravity/annotations/".Length);
                            if (filePart.IndexOf('/') >= 0) continue;

                            string cid = Path.GetFileNameWithoutExtension(filePart);
                            if (!SessionMetadataExtractor.IsLikelyConversationId(cid) || !convIdsToExtract.Contains(cid))
                                continue;

                            string trimmedCid = cid.Trim();
                            string dest = CapabilityRepositoryManager.CombineAndEnsureUnderRoot(annotationsDir, trimmedCid + ".pbtxt");
                            ExtractZipEntrySafe(entry, dest);
                            extractedAnnotationCids.Add(trimmedCid);
                            workRes.FilesImported++;
                        }
                        else if (norm.StartsWith("antigravity/brain/", StringComparison.OrdinalIgnoreCase))
                        {
                            string rest = norm.Substring("antigravity/brain/".Length);
                            int slashIdx = rest.IndexOf('/');
                            if (slashIdx <= 0 || slashIdx >= rest.Length - 1) continue;

                            string cid = rest.Substring(0, slashIdx);
                            string relSubPath = rest.Substring(slashIdx + 1);
                            if (!SessionMetadataExtractor.IsLikelyConversationId(cid) || !convIdsToExtract.Contains(cid))
                                continue;

                            string cidBrainRoot = CapabilityRepositoryManager.CombineAndEnsureUnderRoot(brainDir, cid);
                            string dest = CapabilityRepositoryManager.CombineAndEnsureUnderRoot(cidBrainRoot, relSubPath);
                            ExtractZipEntrySafe(entry, dest);
                            workRes.FilesImported++;
                        }
                        else if (norm.StartsWith("config/projects/", StringComparison.OrdinalIgnoreCase) &&
                                 norm.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        {
                            string filePart = norm.Substring("config/projects/".Length);
                            if (filePart.IndexOf('/') >= 0) continue;

                            string archiveProjId = Path.GetFileNameWithoutExtension(filePart);
                            if (string.Equals(archiveProjId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
                                continue;

                            string rawJson;
                            using (StreamReader sr = new StreamReader(entry.Open(), Encoding.UTF8))
                            {
                                rawJson = sr.ReadToEnd();
                            }

                            string pNameInJson = DirectStreamArchiver.ExtractJsonValue(rawJson, "name");
                            if (VisualDiffEngine.IsOutsideProject(pNameInJson) || SmartProjectFilter.IsJunkOrEphemeral(pNameInJson))
                                continue;

                            ResolvedImportProjectInfo matchedInfo = null;
                            if (!byBackupProjId.TryGetValue(archiveProjId, out matchedInfo))
                            {
                                if (!string.IsNullOrEmpty(pNameInJson))
                                    byProjName.TryGetValue(pNameInJson, out matchedInfo);
                            }

                            if (matchedInfo != null && !string.IsNullOrEmpty(matchedInfo.TargetId))
                            {
                                string dest = CapabilityRepositoryManager.CombineAndEnsureUnderRoot(projDir, matchedInfo.TargetId + ".json");
                                if (!File.Exists(dest))
                                {
                                    string remappedJson = DirectStreamArchiver.RemapAllPathFormats(rawJson, srcHomeSnap, curHomeSnap);
                                    LocalWorkspaceManager.WriteUtf8TextAtomicWithBackup(dest, remappedJson, dest + ".bak");
                                    workRes.FilesImported++;
                                }
                            }
                        }
                    }

                    // 阶段 2a-bis：为缺少 .pbtxt 的旧版 .agy 归档合成 Layer 1 标注
                    for (int i = 0; i < selectedConvs.Count; i++)
                    {
                        ConversationSummaryInfo sc = selectedConvs[i];
                        if (sc == null || string.IsNullOrEmpty(sc.ConversationId)) continue;
                        string cid = sc.ConversationId.Trim();
                        if (!SessionMetadataExtractor.IsLikelyConversationId(cid)) continue;
                        if (extractedAnnotationCids.Contains(cid)) continue;

                        string synthTitle = null;
                        if (sc.HasCustomTitle && !string.IsNullOrWhiteSpace(sc.CustomTitle))
                            synthTitle = sc.CustomTitle.Trim();
                        else if (!string.IsNullOrWhiteSpace(sc.DisplayTitle))
                            synthTitle = sc.DisplayTitle.Trim();

                        if (!string.IsNullOrWhiteSpace(synthTitle) &&
                            !IncrementalProtoShardMerger.IsSyntheticPlaceholderTitle(synthTitle) &&
                            (sc.HasCustomTitle || !IncrementalProtoShardMerger.IsSyntheticPlaceholderTitle(sc.DisplayTitle)))
                        {
                            try
                            {
                                string pbtxtDest = CapabilityRepositoryManager.CombineAndEnsureUnderRoot(annotationsDir, cid + ".pbtxt");
                                string existingRaw = File.Exists(pbtxtDest) ? LocalWorkspaceManager.ReadUtf8TextShared(pbtxtDest) : null;
                                string synthesized = PbtxtAnnotationCodec.UpsertTitle(existingRaw, synthTitle);
                                LocalWorkspaceManager.WriteUtf8TextAtomicWithBackup(pbtxtDest, synthesized, false, null);
                                workRes.FilesImported++;
                            }
                            catch (Exception ex)
                            {
                                workRes.Warnings.Add(string.Format("合成会话 {0} 的 .pbtxt 标题标注失败: {1}", cid, ex.Message));
                            }
                        }
                    }

                    // 阶段 2b：若归档未携带某工程真实 projects/*.json，以兜底模板生成（Protected by !File.Exists）
                    foreach (KeyValuePair<string, ResolvedImportProjectInfo> kvp in neededProjectsByTargetId)
                    {
                        ResolvedImportProjectInfo pInfo = kvp.Value;
                        string dest = CapabilityRepositoryManager.CombineAndEnsureUnderRoot(projDir, pInfo.TargetId + ".json");
                        if (!File.Exists(dest))
                        {
                            string fallbackJson = WorkspaceService.GenerateStandardProjectJson(pInfo.TargetId, pInfo.ProjectName, pInfo.FolderUri);
                            LocalWorkspaceManager.WriteUtf8TextAtomicWithBackup(dest, fallbackJson, dest + ".bak");
                            workRes.FilesImported++;
                        }
                    }

                    // 阶段 2c：在所有 .db 与 .db-wal 均提取落盘后，重映射 SQLite 元数据
                    if (onProgress != null) onProgress("正在同步重映射 SQLite 会话数据库元数据与摘要索引 (.pb / summary.sqlite)...", 75);
                    foreach (KeyValuePair<string, string> dbKvp in extractedDbPaths)
                    {
                        string cid = dbKvp.Key;
                        string dbDest = dbKvp.Value;
                        ConversationSummaryInfo sc = convMap.ContainsKey(cid) ? convMap[cid] : null;
                        string pName = (sc != null && !string.IsNullOrEmpty(sc.ProjectName))
                            ? sc.ProjectName
                            : (convResolvedProjName.ContainsKey(cid) ? convResolvedProjName[cid] : null);
                        string pId = convResolvedProjId.ContainsKey(cid) ? convResolvedProjId[cid] : null;
                        string dTitle = sc != null ? sc.DisplayTitle : null;

                        bool sqlOk = SqliteMetadataUpdater.RemapSqliteDbMetadata(
                            dbDest, srcHomeSnap, curHomeSnap, projectUriOverrides, pName, pId, dTitle
                        );
                        if (!sqlOk)
                        {
                            workRes.Warnings.Add(string.Format("会话 {0} 的 SQLite 元数据重映射失败: {1}",
                                cid, SqliteMetadataUpdater.LastError ?? "未知错误"));
                        }
                    }

                    string localPb = Path.Combine(geminiSnap, "antigravity", "agyhub_summaries_proto.pb");
                    byte[] localPbBytes = File.Exists(localPb) ? LocalWorkspaceManager.ReadAllBytesShared(localPb) : new byte[0];

                    byte[] mergedPb = IncrementalProtoShardMerger.MergeShards(
                        localPbBytes, selectedConvs, srcHomeSnap, curHomeSnap, projectUriOverrides, projectNewIds
                    );

                    LocalWorkspaceManager.WriteBytesAtomicWithBackup(localPb, mergedPb, localPb + ".bak");
                    workRes.FilesImported++;

                    // 阶段 2d：同步 Upsert conversation_summaries.db
                    string localSummaryDb = Path.Combine(geminiSnap, "antigravity", "conversation_summaries.db");
                    if (File.Exists(localSummaryDb))
                    {
                        Dictionary<string, byte[]> remappedMetaByCid = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                        try
                        {
                            List<ProtoNode> mergedNodes = ProtoCodec.Decode(mergedPb);
                            for (int i = 0; i < mergedNodes.Count; i++)
                            {
                                ProtoNode entryNode = mergedNodes[i];
                                if (entryNode.FieldNumber != 1 || entryNode.WireType != 2) continue;
                                ProtoCodec.DecodeSummaryEntryTree(entryNode);
                                ProtoNode f1Node = entryNode.GetChild(1);
                                string mCid = f1Node != null ? (f1Node.GetString() ?? string.Empty).Trim() : string.Empty;
                                if (string.IsNullOrEmpty(mCid) || !convIdsToExtract.Contains(mCid)) continue;
                                ProtoNode metaNode = entryNode.GetChild(2);
                                if (metaNode != null)
                                {
                                    byte[] metaBytes = metaNode.HasChildren
                                        ? ProtoCodec.Encode(metaNode.Children)
                                        : (metaNode.RawBytes ?? new byte[0]);
                                    remappedMetaByCid[mCid] = metaBytes;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            workRes.Warnings.Add("解析合流后 .pb 摘要字节以同步 conversation_summaries.db 告警: " + ex.Message);
                        }

                        List<SummaryDbUpsertItem> upsertItems = new List<SummaryDbUpsertItem>(selectedConvs.Count);
                        for (int i = 0; i < selectedConvs.Count; i++)
                        {
                            ConversationSummaryInfo sc = selectedConvs[i];
                            if (sc == null || string.IsNullOrEmpty(sc.ConversationId)) continue;
                            string cid = sc.ConversationId.Trim();
                            if (!SessionMetadataExtractor.IsLikelyConversationId(cid)) continue;

                            string pId = convResolvedProjId.ContainsKey(cid) ? convResolvedProjId[cid] : (sc.ProjectId ?? string.Empty);
                            string pName = convResolvedProjName.ContainsKey(cid) ? convResolvedProjName[cid] : (sc.ProjectName ?? string.Empty);
                            bool isOutside = string.Equals(pId, "outside-of-project", StringComparison.OrdinalIgnoreCase) ||
                                             VisualDiffEngine.IsOutsideProject(pName);

                            string resolvedUri = null;
                            if (!isOutside)
                            {
                                projectUriOverrides.TryGetValue("CID:" + cid, out resolvedUri);
                                if (string.IsNullOrEmpty(resolvedUri) && !string.IsNullOrEmpty(pId))
                                    projectUriOverrides.TryGetValue("ID:" + pId, out resolvedUri);
                                if (string.IsNullOrEmpty(resolvedUri) && !string.IsNullOrEmpty(sc.FolderUri))
                                    resolvedUri = DirectStreamArchiver.RemapAllPathFormats(sc.FolderUri, srcHomeSnap, curHomeSnap);
                            }

                            string titleVal = string.Empty;
                            if (sc.HasCustomTitle && !string.IsNullOrWhiteSpace(sc.CustomTitle))
                                titleVal = sc.CustomTitle.Trim();
                            else if (!string.IsNullOrWhiteSpace(sc.DisplayTitle) && !IncrementalProtoShardMerger.IsSyntheticPlaceholderTitle(sc.DisplayTitle))
                                titleVal = sc.DisplayTitle.Trim();
                            else if (!string.IsNullOrWhiteSpace(sc.AutoSummaryTitle))
                                titleVal = sc.AutoSummaryTitle.Trim();

                            DateTime utcTime = sc.UpdatedTime > DateTime.MinValue ? sc.UpdatedTime.ToUniversalTime() : DateTime.UtcNow;
                            string isoTime = utcTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

                            SummaryDbUpsertItem upsertItem = new SummaryDbUpsertItem();
                            upsertItem.ConversationId = cid;
                            upsertItem.Title = titleVal;
                            upsertItem.Preview = titleVal;
                            upsertItem.StepCount = sc.StepCount;
                            upsertItem.LastModifiedTimeIso = isoTime;
                            upsertItem.LastUserInputTimeIso = isoTime;
                            upsertItem.ProjectId = isOutside ? "outside-of-project" : (pId ?? string.Empty);
                            upsertItem.WorkspaceUrisJson = (isOutside || string.IsNullOrEmpty(resolvedUri))
                                ? string.Empty
                                : "[\"" + JsonKeyManipulator.EscapeJsonString(resolvedUri) + "\"]";
                            upsertItem.NestingDepth = !string.IsNullOrEmpty(sc.ParentConversationId) ? 1 : 0;
                            byte[] rawMeta;
                            if (remappedMetaByCid.TryGetValue(cid, out rawMeta))
                                upsertItem.RawSummaryBytes = rawMeta;
                            upsertItem.ProjectName = isOutside ? string.Empty : (pName ?? string.Empty);
                            upsertItem.FtsWorkspaceToken = isOutside ? string.Empty : (resolvedUri ?? string.Empty);
                            upsertItems.Add(upsertItem);
                        }

                        if (upsertItems.Count > 0)
                        {
                            List<string> sumDbWarnings;
                            int upserted = SummarySqliteBridge.UpsertSessionsInSummaryDb(localSummaryDb, upsertItems, out sumDbWarnings);
                            if (sumDbWarnings != null && sumDbWarnings.Count > 0)
                                workRes.Warnings.AddRange(sumDbWarnings);
                            if (upserted == 0 && (sumDbWarnings == null || sumDbWarnings.Count == 0) && !string.IsNullOrEmpty(SummarySqliteBridge.LastError))
                                workRes.Warnings.Add("同步写入 conversation_summaries.db 失败: " + SummarySqliteBridge.LastError);
                        }
                    }
                }
                workRes.ConversationsImported = selectedConvs.Count;
            }

            if (onProgress != null) onProgress("选择性导入已完成。", 100);
            return workRes;
        }

        public static void CreateFullBackup(
            string geminiPath,
            string homePath,
            List<ConversationSummaryInfo> localSummaries,
            string destPath,
            Action<string, int> onProgress,
            out int fileCount,
            out List<string> warnings)
        {
            fileCount = 0;
            warnings = new List<string>();
            if (string.IsNullOrEmpty(geminiPath) || !Directory.Exists(geminiPath))
                throw new DirectoryNotFoundException("未找到 .gemini 目录: " + geminiPath);

            string dDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dDir) && !Directory.Exists(dDir)) Directory.CreateDirectory(dDir);

            string homeSnap = homePath ?? WorkspaceService.ResolveDefaultHomePath();
            string userSnap = Environment.UserName;
            string tmpDest = destPath + ".tmp." + Guid.NewGuid().ToString("N").Substring(0, 8);
            int n = 0;
            try
            {
                HashSet<string> writtenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (FileStream fs = new FileStream(tmpDest, FileMode.Create, FileAccess.Write, FileShare.None))
                using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    if (writtenEntries.Add("metadata.json"))
                    {
                        ZipArchiveEntry metaEntry = archive.CreateEntry("metadata.json", CompressionLevel.Fastest);
                        using (StreamWriter sw = new StreamWriter(metaEntry.Open(), new UTF8Encoding(false)))
                        {
                            string meta = "{\r\n" +
                                          "  \"source_home\": \"" + JsonKeyManipulator.EscapeJsonString(homeSnap) + "\",\r\n" +
                                          "  \"source_user\": \"" + JsonKeyManipulator.EscapeJsonString(userSnap) + "\",\r\n" +
                                          "  \"backup_time\": \"" + JsonKeyManipulator.EscapeJsonString(DateTime.Now.ToString("o")) + "\",\r\n" +
                                          "  \"version\": \"0.8\"\r\n}";
                            sw.Write(meta);
                        }
                        n++;
                    }

                    if (onProgress != null) onProgress("正在归档全部有效会话、标题标注与脑区索引...", 25);
                    HashSet<string> validCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (localSummaries != null)
                    {
                        for (int i = 0; i < localSummaries.Count; i++)
                        {
                            ConversationSummaryInfo s = localSummaries[i];
                            if (s != null && !string.IsNullOrEmpty(s.ConversationId) && SessionMetadataExtractor.IsLikelyConversationId(s.ConversationId))
                                validCids.Add(s.ConversationId.Trim());
                        }
                    }

                    string convDir = Path.Combine(geminiPath, "antigravity", "conversations");
                    string brainDir = Path.Combine(geminiPath, "antigravity", "brain");

                    foreach (string cid in validCids)
                    {
                        n += AddFileToArchive(archive, writtenEntries, Path.Combine(convDir, cid + ".db"), "data/antigravity/conversations/" + cid + ".db");
                        n += AddFileToArchive(archive, writtenEntries, Path.Combine(convDir, cid + ".db-wal"), "data/antigravity/conversations/" + cid + ".db-wal");
                        n += AddDirToArchive(archive, writtenEntries, Path.Combine(brainDir, cid), "data/antigravity/brain/" + cid, false);
                    }

                    string annotationsDir = Path.Combine(geminiPath, "antigravity", "annotations");
                    if (Directory.Exists(annotationsDir))
                    {
                        string[] pbtxtFiles = Directory.GetFiles(annotationsDir, "*.pbtxt");
                        for (int i = 0; i < pbtxtFiles.Length; i++)
                        {
                            string pf = pbtxtFiles[i];
                            string fn = Path.GetFileName(pf);
                            if (!string.IsNullOrEmpty(fn) && fn.EndsWith(".pbtxt", StringComparison.OrdinalIgnoreCase))
                                n += AddFileToArchive(archive, writtenEntries, pf, "data/antigravity/annotations/" + fn);
                        }
                    }

                    n += AddFileToArchive(archive, writtenEntries,
                        Path.Combine(geminiPath, "antigravity", "agyhub_summaries_proto.pb"),
                        "data/antigravity/agyhub_summaries_proto.pb");

                    string summaryDbPath = Path.Combine(geminiPath, "antigravity", "conversation_summaries.db");
                    if (File.Exists(summaryDbPath))
                    {
                        string tempSummaryDb = Path.Combine(Path.GetTempPath(), "agy_sum_bk_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".db");
                        try
                        {
                            if (onProgress != null) onProgress("正在安全快照 conversation_summaries.db...", 65);
                            if (SummarySqliteBridge.BackupDatabaseToFile(summaryDbPath, tempSummaryDb))
                                n += AddFileToArchive(archive, writtenEntries, tempSummaryDb, "data/antigravity/conversation_summaries.db");
                            else
                                warnings.Add("归档 conversation_summaries.db 失败（已跳过）: " + (SummarySqliteBridge.LastError ?? "未知错误"));
                        }
                        finally
                        {
                            DeleteFileIfExistsQuiet(tempSummaryDb);
                            DeleteFileIfExistsQuiet(tempSummaryDb + "-wal");
                            DeleteFileIfExistsQuiet(tempSummaryDb + "-shm");
                            DeleteFileIfExistsQuiet(tempSummaryDb + "-journal");
                        }
                    }

                    if (onProgress != null) onProgress("正在归档项目配置、用户规则与 MCP 配置...", 85);
                    n += AddFileToArchive(archive, writtenEntries, Path.Combine(geminiPath, ".geminirules"), "data/.geminirules");
                    n += AddFileToArchive(archive, writtenEntries, Path.Combine(geminiPath, "GEMINI.md"), "data/GEMINI.md");
                    n += AddFileToArchive(archive, writtenEntries, Path.Combine(geminiPath, "AGENTS.md"), "data/AGENTS.md");
                    n += AddDirToArchive(archive, writtenEntries, Path.Combine(geminiPath, "config"), "data/config", true);
                    n += AddDirToArchive(archive, writtenEntries, Path.Combine(geminiPath, "antigravity", "mcp"), "data/antigravity/mcp", false);
                }

                if (File.Exists(destPath))
                {
                    File.SetAttributes(destPath, FileAttributes.Normal);
                    File.Delete(destPath);
                }
                File.Move(tmpDest, destPath);
                if (onProgress != null) onProgress("全量备份归档完成。", 100);
            }
            catch
            {
                try { if (File.Exists(tmpDest)) File.Delete(tmpDest); } catch { }
                throw;
            }
            fileCount = n;
        }

        private static bool DoesProjectMetadataMatchUri(ProjectMetadata pm, string uri)
        {
            if (pm == null || string.IsNullOrEmpty(uri)) return false;
            if (UriEqualsNormalized(pm.FolderUri, uri)) return true;
            if (pm.FolderUris != null)
            {
                for (int i = 0; i < pm.FolderUris.Count; i++)
                {
                    if (UriEqualsNormalized(pm.FolderUris[i], uri)) return true;
                }
            }
            return false;
        }

        private static ProjectDiffNode FindProjectDiffForBackupConv(List<ProjectDiffNode> diffNodes, string cid)
        {
            if (diffNodes == null || string.IsNullOrEmpty(cid)) return null;
            for (int i = 0; i < diffNodes.Count; i++)
            {
                ProjectDiffNode pDiff = diffNodes[i];
                if (pDiff == null || pDiff.BackupConversations == null) continue;
                for (int j = 0; j < pDiff.BackupConversations.Count; j++)
                {
                    if (string.Equals(pDiff.BackupConversations[j].ConversationId, cid, StringComparison.OrdinalIgnoreCase))
                        return pDiff;
                }
            }
            return null;
        }

        private static void SetUnambiguousMapping(
            Dictionary<string, string> dict,
            Dictionary<string, string> seenFirst,
            HashSet<string> ambiguousKeys,
            string key,
            string value)
        {
            if (dict == null || seenFirst == null || ambiguousKeys == null ||
                string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                return;

            if (ambiguousKeys.Contains(key)) return;

            string prev;
            if (seenFirst.TryGetValue(key, out prev))
            {
                if (!string.Equals(prev, value, StringComparison.OrdinalIgnoreCase))
                {
                    dict.Remove(key);
                    ambiguousKeys.Add(key);
                }
            }
            else
            {
                seenFirst[key] = value;
                dict[key] = value;
            }
        }

        private static bool UriEqualsNormalized(string uriA, string uriB)
        {
            if (string.IsNullOrWhiteSpace(uriA) || string.IsNullOrWhiteSpace(uriB)) return false;
            return string.Equals(
                WorkspaceService.UriToDisplayPath(uriA).TrimEnd('\\', '/'),
                WorkspaceService.UriToDisplayPath(uriB).TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
        }

        private static void DeleteFileIfExistsQuiet(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }
            }
            catch { }
        }

        private static void ExtractZipEntrySafe(ZipArchiveEntry entry, string destPath)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Name)) return;
            string dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string tmpDest = destPath + ".tmp." + Guid.NewGuid().ToString("N").Substring(0, 8);
            try
            {
                using (Stream s = entry.Open())
                using (FileStream fs = new FileStream(tmpDest, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    s.CopyTo(fs);
                    fs.Flush(true);
                }

                if (File.Exists(destPath))
                {
                    try { File.SetAttributes(destPath, FileAttributes.Normal); } catch { }
                    File.Delete(destPath);
                }
                File.Move(tmpDest, destPath);
            }
            catch
            {
                try { if (File.Exists(tmpDest)) File.Delete(tmpDest); } catch { }
                throw;
            }
        }

        private static int AddFileToArchive(ZipArchive archive, HashSet<string> writtenEntries, string filePath, string entryName)
        {
            if (archive == null || string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return 0;

            string normEntry = (entryName ?? string.Empty).Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrEmpty(normEntry) || (writtenEntries != null && !writtenEntries.Add(normEntry)))
                return 0;

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                ZipArchiveEntry entry = archive.CreateEntry(normEntry, CompressionLevel.Fastest);
                using (Stream s = entry.Open())
                {
                    fs.CopyTo(s);
                }
            }
            return 1;
        }

        private sealed class DirWalkItem
        {
            public string FullDir;
            public string RelDir;
            public int Depth;
        }

        private static int AddDirToArchive(ZipArchive archive, HashSet<string> writtenEntries, string dirPath, string baseEntryDir, bool isConfigRoot)
        {
            if (archive == null || string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath)) return 0;

            int count = 0;
            string cleanBase = (baseEntryDir ?? string.Empty).Replace('\\', '/').Trim('/');
            Stack<DirWalkItem> stack = new Stack<DirWalkItem>();
            stack.Push(new DirWalkItem { FullDir = dirPath, RelDir = string.Empty, Depth = 0 });

            while (stack.Count > 0)
            {
                DirWalkItem cur = stack.Pop();
                if (cur.Depth > 32) continue;

                string[] files = Directory.GetFiles(cur.FullDir);
                for (int i = 0; i < files.Length; i++)
                {
                    string f = files[i];
                    string fn = Path.GetFileName(f);
                    string rel = string.IsNullOrEmpty(cur.RelDir) ? fn : (cur.RelDir + "/" + fn);
                    if (string.Equals(fn, "installation_id", StringComparison.OrdinalIgnoreCase)) continue;
                    if (CapabilityRepositoryManager.IsExcludedFileOrPath(rel)) continue;

                    string entryName = string.IsNullOrEmpty(cleanBase) ? rel : (cleanBase + "/" + rel);
                    count += AddFileToArchive(archive, writtenEntries, f, entryName);
                }

                string[] subDirs = Directory.GetDirectories(cur.FullDir);
                for (int i = 0; i < subDirs.Length; i++)
                {
                    string sub = subDirs[i];
                    string subName = Path.GetFileName(sub);
                    if (CapabilityRepositoryManager.IsExcludedSubdirName(subName)) continue;
                    if (isConfigRoot && cur.Depth == 0 && string.Equals(subName, "builtin", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string nextRel = string.IsNullOrEmpty(cur.RelDir) ? subName : (cur.RelDir + "/" + subName);
                    stack.Push(new DirWalkItem { FullDir = sub, RelDir = nextRel, Depth = cur.Depth + 1 });
                }
            }
            return count;
        }
    }

    /// <summary>
    /// 专职在独立 STA 线程弹出原生 Win32 文件/目录选择对话框（防重入、自动置顶于 Edge --app 窗口之上）
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
                // 用户取消 (HRESULT_FROM_WIN32(ERROR_CANCELLED) == 0x800704C7) 也算正常完成，不降级重复弹出
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
