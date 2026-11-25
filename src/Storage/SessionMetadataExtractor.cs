using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // SESSION METADATA EXTRACTOR & ARCHIVE INDEX MODEL (Milestone 1 — F1, F3, F5)
    // Strictly C# 5.0 (.NET Framework 4.x)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 备份包/本地环境内存索引
    /// </summary>
    public partial class BackupArchiveIndex
    {
        public string SourceHome;
        public string SourceUser;
        public string BackupTime;
        public string Version;
        public string BackupTier;
        public Dictionary<string, ProjectMetadata> Projects;
        public List<ConversationSummaryInfo> Conversations;
        public HashSet<string> ExistingConvoDbFiles;
        public HashSet<string> ExistingBrainDirs;
        public HashSet<string> CliConversationIds;
        public List<string> UserSettingsEntries;
        public bool HasRules;
        public bool HasSkills;
        public bool HasMcp;
        public bool HasUserSettings;
        public bool HasCliSessions;
        public bool IsColdArchive;
        public string ColdArchiveType;
        public bool HasLegacyCliSummaryDb;
        public long TotalZipSizeBytes;
        public long ExtractedMetadataSizeBytes;
        public double ElapsedMilliseconds;

        public BackupArchiveIndex()
        {
            Projects = new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
            Conversations = new List<ConversationSummaryInfo>();
            ExistingConvoDbFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ExistingBrainDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CliConversationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            UserSettingsEntries = new List<string>();
        }
    }

    /// <summary>
    /// 智能垃圾工程与 Subagent 会话过滤器 (Milestone 2 — F5)
    /// </summary>
    public static class SmartProjectFilter
    {
        public static bool IsJunkOrEphemeral(string projectName)
        {
            if (string.IsNullOrEmpty(projectName))
                return true;

            string lower = projectName.Trim().ToLowerInvariant();

            if (lower.StartsWith("subagent-") || lower.StartsWith("subagent_"))
                return true;
            if (lower == "temp" || lower == "tmp" ||
                lower.StartsWith("temp-playground") || lower.StartsWith("temp_playground") ||
                lower.StartsWith("temp-eval") || lower.StartsWith("antigravity-eval"))
                return true;
            if (lower.StartsWith("recovered project") || lower.StartsWith("recovered_project") ||
                lower.StartsWith("recovered workspace") || lower.StartsWith("recovered_workspace"))
                return true;
            if (lower == "default-cli-project" || lower == "default_cli_project")
                return true;

            // outside-of-project 是全局会话容器，不是垃圾工程
            return false;
        }

        public static bool IsTemporaryOrJunkProject(string projectName, string folderUri = null)
        {
            if (IsJunkOrEphemeral(projectName)) return true;
            if (!string.IsNullOrEmpty(folderUri))
            {
                string lower = folderUri.Trim().ToLowerInvariant();
                if (lower.Contains("subagent-") || lower.Contains(".system_generated"))
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 会话元数据提取器 (100% 优先提取 Field 15 自定义标题，精准识别 Subagent)
    /// </summary>
    public static class SessionMetadataExtractor
    {
        private static readonly HashSet<string> WindowsReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        internal static bool IsPathContainedUnder(string rootDir, string targetPath)
        {
            if (string.IsNullOrEmpty(rootDir) || string.IsNullOrEmpty(targetPath))
                return false;
            try
            {
                string fullRoot = Path.GetFullPath(rootDir)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string fullTarget = Path.GetFullPath(targetPath);
                return fullTarget.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsLikelyConversationId(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            if (s.Length == 0 || s.Length > 64) return false;
            if (s == "." || s == "..") return false;
            if (WindowsReservedNames.Contains(s)) return false;

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool valid = (c >= 'A' && c <= 'Z') ||
                             (c >= 'a' && c <= 'z') ||
                             (c >= '0' && c <= '9') ||
                             c == '-' || c == '_';
                if (!valid) return false;
            }
            return true;
        }

        public static List<ConversationSummaryInfo> ExtractSummaries(byte[] pbData, Dictionary<string, ProjectMetadata> projectsMap)
        {
            List<ConversationSummaryInfo> list = new List<ConversationSummaryInfo>();
            if (pbData == null || pbData.Length == 0) return list;

            try
            {
                List<ProtoNode> rootNodes = ProtoCodec.Decode(pbData);
                for (int i = 0; i < rootNodes.Count; i++)
                {
                    ProtoNode node = rootNodes[i];
                    if (node.FieldNumber == 1 && node.WireType == 2)
                    {
                        ConversationSummaryInfo info = ParseEntryNode(node);
                        if (info != null)
                        {
                            ResolveProjectInfo(info, projectsMap);
                            list.Add(info);
                        }
                    }
                }
            }
            catch
            {
                // 异常保护：降级返回已解析的部分
            }
            return list;
        }

        public static List<ConversationSummaryInfo> ExtractSummariesFromProto(byte[] pbBytes)
        {
            return ExtractSummaries(pbBytes, null);
        }

        /// <summary>
        /// 叠加本机三层数据源 (Layer 1 .pbtxt 标注 &gt; Layer 2 summary.sqlite &gt; Layer 3 .pb Field 15 &gt; Field 1)
        /// </summary>
        public static void OverlayLocalThreeLayerMetadata(
            List<ConversationSummaryInfo> list,
            string geminiPath,
            Dictionary<string, SummaryDbRowInfo> dbSnapshot)
        {
            OverlayLocalThreeLayerMetadata(list, geminiPath, dbSnapshot, null);
        }

        public static void OverlayLocalThreeLayerMetadata(
            List<ConversationSummaryInfo> list,
            string geminiPath,
            Dictionary<string, SummaryDbRowInfo> dbSnapshot,
            Dictionary<string, ProjectMetadata> localProjects)
        {
            if (list == null || list.Count == 0) return;

            string ideAnnotationsDir = !string.IsNullOrEmpty(geminiPath)
                ? Path.Combine(geminiPath, "antigravity", "annotations")
                : null;
            string cliAnnotationsDir = !string.IsNullOrEmpty(geminiPath)
                ? Path.Combine(geminiPath, "antigravity-cli", "annotations")
                : null;
            bool hasIdeAnnotationsDir = !string.IsNullOrEmpty(ideAnnotationsDir) && Directory.Exists(ideAnnotationsDir);
            bool hasCliAnnotationsDir = !string.IsNullOrEmpty(cliAnnotationsDir) && Directory.Exists(cliAnnotationsDir);

            for (int i = 0; i < list.Count; i++)
            {
                ConversationSummaryInfo s = list[i];
                if (s == null || string.IsNullOrWhiteSpace(s.ConversationId)) continue;

                string cid = s.ConversationId.Trim();
                SummaryDbRowInfo dbRow = null;
                if (dbSnapshot != null)
                {
                    dbSnapshot.TryGetValue(cid, out dbRow);
                }

                if (dbRow != null)
                {
                    if (dbRow.AgentName != null) s.AgentName = dbRow.AgentName;
                    if (!string.IsNullOrEmpty(dbRow.FirstPromptSnippet)) s.FirstPromptSnippet = dbRow.FirstPromptSnippet;
                    if (!string.IsNullOrEmpty(dbRow.GitBranchHint)) s.GitBranchHint = dbRow.GitBranchHint;
                    if (dbRow.RepoBranchHints != null && dbRow.RepoBranchHints.Count > 0) s.RepoBranchHints = dbRow.RepoBranchHints;
                    if (dbRow.Status != null) s.RunStatus = dbRow.Status;
                    s.NotFullyIdle = dbRow.NotFullyIdle;
                    if (string.IsNullOrEmpty(s.ParentConversationId) && !string.IsNullOrWhiteSpace(dbRow.ParentConversationId))
                    {
                        s.ParentConversationId = dbRow.ParentConversationId.Trim();
                    }
                    if (!string.IsNullOrWhiteSpace(s.ParentConversationId))
                    {
                        s.IsSubagent = true;
                    }

                    if (string.IsNullOrWhiteSpace(s.ProjectId) && !string.IsNullOrWhiteSpace(dbRow.ProjectId))
                    {
                        s.ProjectId = dbRow.ProjectId.Trim();
                        ResolveProjectInfo(s, localProjects);
                    }
                    else if (localProjects != null && !string.IsNullOrEmpty(s.ProjectId))
                    {
                        ResolveProjectInfo(s, localProjects);
                    }

                    if (string.Equals(dbRow.ProjectId, "default-cli-project", StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(dbRow.Source) && dbRow.Source.IndexOf("cli", StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (!string.IsNullOrEmpty(dbRow.AppDataDir) && dbRow.AppDataDir.IndexOf("antigravity-cli", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        s.IsCliSession = true;
                    }
                }

                if (string.Equals(s.ProjectId, "default-cli-project", StringComparison.OrdinalIgnoreCase))
                {
                    s.IsCliSession = true;
                }

                if (!string.IsNullOrWhiteSpace(s.AgentName))
                {
                    s.IsSubagent = true;
                }

                if (string.IsNullOrWhiteSpace(s.FirstPromptSnippet) && !string.IsNullOrEmpty(geminiPath) && IsLikelyConversationId(cid))
                {
                    string fallbackPrompt = TryReadFirstPromptFromTranscripts(geminiPath, cid);
                    if (!string.IsNullOrEmpty(fallbackPrompt))
                    {
                        s.FirstPromptSnippet = fallbackPrompt;
                    }
                }

                string pbtxtTitle = null;
                if (IsLikelyConversationId(cid))
                {
                    string primaryAnnotDir = s.IsCliSession
                        ? (hasCliAnnotationsDir ? cliAnnotationsDir : (hasIdeAnnotationsDir ? ideAnnotationsDir : null))
                        : (hasIdeAnnotationsDir ? ideAnnotationsDir : (hasCliAnnotationsDir ? cliAnnotationsDir : null));
                    string secondaryAnnotDir = s.IsCliSession
                        ? (hasIdeAnnotationsDir && primaryAnnotDir != ideAnnotationsDir ? ideAnnotationsDir : null)
                        : (hasCliAnnotationsDir && primaryAnnotDir != cliAnnotationsDir ? cliAnnotationsDir : null);

                    pbtxtTitle = TryReadAnnotationTitleFromDir(primaryAnnotDir, cid);
                    if (string.IsNullOrWhiteSpace(pbtxtTitle) && secondaryAnnotDir != null)
                    {
                        pbtxtTitle = TryReadAnnotationTitleFromDir(secondaryAnnotDir, cid);
                    }
                }

                string overlayTitle = null;
                if (!string.IsNullOrWhiteSpace(pbtxtTitle))
                {
                    string trimmedPbtxt = pbtxtTitle.Trim();
                    if (!IncrementalProtoShardMerger.IsSyntheticPlaceholderTitle(trimmedPbtxt))
                    {
                        overlayTitle = trimmedPbtxt;
                    }
                }

                if (overlayTitle == null && dbRow != null && !string.IsNullOrWhiteSpace(dbRow.Title))
                {
                    string trimmedDb = dbRow.Title.Trim();
                    if (!IncrementalProtoShardMerger.IsSyntheticPlaceholderTitle(trimmedDb))
                    {
                        overlayTitle = trimmedDb;
                    }
                }

                if (!string.IsNullOrEmpty(overlayTitle))
                {
                    if (!s.HasCustomTitle || !string.Equals(overlayTitle, s.DisplayTitle, StringComparison.Ordinal))
                    {
                        s.CustomTitle = overlayTitle;
                        s.DisplayTitle = overlayTitle;
                        s.HasCustomTitle = true;
                    }
                }
            }
        }

        private static string TryReadAnnotationTitleFromDir(string annotationsDir, string cid)
        {
            if (string.IsNullOrEmpty(annotationsDir)) return null;
            string pbtxtPath = Path.Combine(annotationsDir, cid + ".pbtxt");
            if (!IsPathContainedUnder(annotationsDir, pbtxtPath) || !File.Exists(pbtxtPath))
                return null;
            try
            {
                string rawPbtxt;
                using (FileStream fs = new FileStream(pbtxtPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                {
                    rawPbtxt = sr.ReadToEnd();
                }
                return PbtxtTitleExtractor.ExtractTitle(rawPbtxt);
            }
            catch
            {
                return null;
            }
        }

        private static string TryReadFirstPromptFromTranscripts(string geminiPath, string cid)
        {
            const int MaxLinesToScan = 60;
            const int MaxSnippetChars = 1200;
            if (string.IsNullOrEmpty(geminiPath) || !IsLikelyConversationId(cid)) return null;

            string[] subRoots = new string[]
            {
                Path.Combine(geminiPath, "antigravity", "brain"),
                Path.Combine(geminiPath, "antigravity-cli", "brain")
            };
            string[] fileNames = new string[] { "transcript_full.jsonl", "transcript.jsonl" };

            for (int r = 0; r < subRoots.Length; r++)
            {
                string brainRoot = subRoots[r];
                string convBrain = Path.Combine(brainRoot, cid);
                if (!IsPathContainedUnder(brainRoot, convBrain) || !Directory.Exists(convBrain))
                    continue;

                string logsDir = Path.Combine(convBrain, ".system_generated", "logs");
                if (!Directory.Exists(logsDir)) continue;

                for (int f = 0; f < fileNames.Length; f++)
                {
                    string logPath = Path.Combine(logsDir, fileNames[f]);
                    if (!IsPathContainedUnder(logsDir, logPath) || !File.Exists(logPath))
                        continue;

                    try
                    {
                        using (FileStream fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                        {
                            for (int lineIdx = 0; lineIdx < MaxLinesToScan; lineIdx++)
                            {
                                string line = sr.ReadLine();
                                if (line == null) break;
                                if (line.IndexOf("USER_INPUT", StringComparison.Ordinal) < 0) continue;

                                object parsed;
                                if (!UnifiedJson.TryParse(line, out parsed)) continue;
                                Dictionary<string, object> dict = UnifiedJson.AsDict(parsed);
                                if (dict == null) continue;

                                object typeObj;
                                if (!dict.TryGetValue("type", out typeObj) ||
                                    !string.Equals(typeObj as string, "USER_INPUT", StringComparison.OrdinalIgnoreCase))
                                    continue;

                                object contentObj;
                                string content = dict.TryGetValue("content", out contentObj) ? (contentObj as string) : null;
                                if (!string.IsNullOrWhiteSpace(content))
                                {
                                    string trimmed = content.Trim();
                                    return trimmed.Length > MaxSnippetChars
                                        ? trimmed.Substring(0, MaxSnippetChars)
                                        : trimmed;
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }

            return null;
        }

        public static ConversationSummaryInfo ParseEntryNode(ProtoNode entryNode)
        {
            if (entryNode == null) return null;
            ConversationSummaryInfo info = new ConversationSummaryInfo();
            info.RawProtoEntry = entryNode;

            ProtoCodec.DecodeSummaryEntryTree(entryNode);

            if (entryNode.Children == null)
            {
                entryNode.Children = new List<ProtoNode>();
            }

            ProtoNode metaSubmessage = entryNode.GetChild(2);
            bool isRealProtoLayout = metaSubmessage != null && metaSubmessage.HasChildren;

            if (isRealProtoLayout)
            {
                ProtoNode f1 = entryNode.GetChild(1);
                if (f1 != null)
                {
                    string idCandidate = f1.GetString();
                    if (IsLikelyConversationId(idCandidate))
                    {
                        info.ConversationId = idCandidate.Trim();
                    }
                }

                for (int i = 0; i < metaSubmessage.Children.Count; i++)
                {
                    ProtoNode child = metaSubmessage.Children[i];
                    if (child.FieldNumber == 1) // Auto Summary Title
                    {
                        info.AutoSummaryTitle = child.GetString();
                    }
                    else if (child.FieldNumber == 2 && child.WireType == 0) // Step count
                    {
                        info.StepCount = child.GetInt32();
                    }
                    else if (child.FieldNumber == 3) // Updated At
                    {
                        DateTime t = ParseTimestampNode(child);
                        if (t > info.UpdatedTime) info.UpdatedTime = t;
                    }
                    else if (child.FieldNumber == 7) // Created At
                    {
                        info.CreatedTime = ParseTimestampNode(child);
                    }
                    else if (child.FieldNumber == 9) // WorkspaceInfo
                    {
                        ParseWorkspaceNode(child, info);
                    }
                    else if (child.FieldNumber == 10) // Last Activity Time
                    {
                        DateTime t = ParseTimestampNode(child);
                        if (t > info.UpdatedTime) info.UpdatedTime = t;
                    }
                    else if (child.FieldNumber == 15) // Custom Title Metadata ★
                    {
                        ParseCustomTitleNode(child, info);
                    }
                    else if (child.FieldNumber == 16 && child.WireType == 0) // User Interactions count
                    {
                        info.UserInteractions = child.GetInt32();
                    }
                    else if (child.FieldNumber == 17) // CascadeInfo
                    {
                        ParseCascadeNode(child, info);
                    }
                    else if (child.FieldNumber == 23 && child.WireType == 0) // Subagent / Ephemeral flag ★
                    {
                        info.Field23 = child.GetInt32();
                        if (info.Field23 == 0) info.IsSubagent = true;
                    }
                }
            }
            else
            {
                return null;
            }

            if (string.IsNullOrEmpty(info.ProjectName) && !string.IsNullOrEmpty(info.FolderUri))
            {
                info.ProjectName = ExtractProjectNameFromUri(info.FolderUri);
            }

            if (info.Field23.HasValue && info.Field23.Value == 0)
            {
                info.IsSubagent = true;
            }
            else if (!string.IsNullOrEmpty(info.SubagentRole) || !string.IsNullOrEmpty(info.ParentConversationId))
            {
                info.IsSubagent = true;
            }
            else if (!string.IsNullOrEmpty(info.ProjectName) &&
                     (info.ProjectName.StartsWith("subagent-", StringComparison.OrdinalIgnoreCase) ||
                      info.ProjectName.StartsWith("subagent_", StringComparison.OrdinalIgnoreCase)))
            {
                info.IsSubagent = true;
            }

            // 100% Priority Custom Title Resolution
            if (!string.IsNullOrEmpty(info.CustomTitle) && !string.IsNullOrWhiteSpace(info.CustomTitle))
            {
                info.HasCustomTitle = true;
                info.DisplayTitle = info.CustomTitle.Trim();
            }
            else if (!string.IsNullOrEmpty(info.AutoSummaryTitle) && !string.IsNullOrWhiteSpace(info.AutoSummaryTitle))
            {
                info.HasCustomTitle = false;
                info.DisplayTitle = info.AutoSummaryTitle.Trim();
            }
            else
            {
                info.HasCustomTitle = false;
                string id = info.ConversationId;
                if (string.IsNullOrEmpty(id)) id = "session";
                info.DisplayTitle = "未命名会话 (" + (id.Length >= 8 ? id.Substring(0, 8) : id) + ")";
            }

            if (!IsLikelyConversationId(info.ConversationId))
            {
                info.ConversationId = Guid.NewGuid().ToString();
                ProtoNode cidNode = entryNode.GetChild(1);
                if (cidNode != null && cidNode.WireType == 2)
                {
                    cidNode.SetString(info.ConversationId);
                }
                else
                {
                    ProtoNode newCid = new ProtoNode(1, 2);
                    newCid.SetString(info.ConversationId);
                    entryNode.Children.Insert(0, newCid);
                }
                entryNode.IsModified = true;
            }

            if (string.IsNullOrEmpty(info.ProjectName))
            {
                info.ProjectName = "Outside of Project";
            }

            if (info.CreatedTime == DateTime.MinValue && info.UpdatedTime != DateTime.MinValue)
                info.CreatedTime = info.UpdatedTime;
            if (info.UpdatedTime == DateTime.MinValue && info.CreatedTime != DateTime.MinValue)
                info.UpdatedTime = info.CreatedTime;

            return info;
        }

        private static void ParseCustomTitleNode(ProtoNode node, ConversationSummaryInfo info)
        {
            if (node == null) return;
            if (node.HasChildren)
            {
                ProtoNode ct = node.GetChild(1);
                if (ct != null)
                {
                    string s = ct.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        info.CustomTitle = s;
                        info.HasCustomTitle = true;
                    }
                }
            }
            else
            {
                string s = node.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    info.CustomTitle = s;
                    info.HasCustomTitle = true;
                }
            }
        }

        private static void ParseWorkspaceNode(ProtoNode node, ConversationSummaryInfo info)
        {
            if (node == null) return;
            if (node.HasChildren)
            {
                ProtoNode furi = node.GetChild(1);
                if (furi != null && string.IsNullOrEmpty(info.FolderUri))
                {
                    info.FolderUri = furi.GetString();
                }
            }
            else
            {
                if (string.IsNullOrEmpty(info.FolderUri))
                {
                    info.FolderUri = node.GetString();
                }
            }
        }

        private static bool IsSafeDisplayProjectName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string s = name.Trim();
            if (s.StartsWith("file:///", StringComparison.OrdinalIgnoreCase)) return false;
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch < 0x20 || ch == '\uFFFD') return false;
            }
            return true;
        }

        private static void ParseCascadeNode(ProtoNode node, ConversationSummaryInfo info)
        {
            if (node == null) return;
            if (node.HasChildren)
            {
                ProtoNode nameNode = node.GetChild(1);
                if (nameNode != null && string.IsNullOrEmpty(info.ProjectName))
                {
                    if (!nameNode.HasChildren && nameNode.WireType == 2)
                    {
                        string s;
                        if (nameNode.TryGetStrictUtf8String(out s) && IsSafeDisplayProjectName(s))
                        {
                            info.ProjectName = s.Trim();
                        }
                    }
                }
                ProtoNode roleNode = node.GetChild(4);
                if (roleNode != null)
                {
                    info.SubagentRole = roleNode.GetString();
                    if (!string.IsNullOrEmpty(info.SubagentRole)) info.IsSubagent = true;
                }
                ProtoNode f7Node = node.GetChild(7);
                if (f7Node != null && string.IsNullOrEmpty(info.FolderUri))
                {
                    info.FolderUri = f7Node.GetString();
                }
                ProtoNode parentNode = node.GetChild(11);
                if (parentNode != null)
                {
                    string pid = parentNode.GetString();
                    if (IsLikelyConversationId(pid))
                    {
                        info.ParentConversationId = pid.Trim();
                        info.IsSubagent = true;
                    }
                }
                ProtoNode pIdNode = node.GetChild(18);
                if (pIdNode != null && string.IsNullOrEmpty(info.ProjectId))
                {
                    info.ProjectId = pIdNode.GetString();
                }
            }
        }

        private static DateTime ParseTimestampNode(ProtoNode node)
        {
            if (node == null) return DateTime.MinValue;
            if (node.WireType == 0)
            {
                return ConvertEpochToDateTime(node.VarintValue);
            }
            else if (node.HasChildren)
            {
                ProtoNode secNode = node.GetChild(1);
                if (secNode != null && secNode.WireType == 0)
                {
                    return ConvertEpochToDateTime(secNode.VarintValue);
                }
            }
            else if (node.WireType == 1 && node.RawBytes != null && node.RawBytes.Length == 8)
            {
                long val = BitConverter.ToInt64(node.RawBytes, 0);
                return ConvertEpochToDateTime((ulong)val);
            }
            return DateTime.MinValue;
        }

        private static DateTime ConvertEpochToDateTime(ulong epoch)
        {
            try
            {
                if (epoch > 100000000000000000UL) // 纳秒 (~1.79e18)
                {
                    epoch = epoch / 1000000000UL;
                }
                else if (epoch > 1000000000000000UL) // 微秒 (~1.79e15)
                {
                    epoch = epoch / 1000000UL;
                }
                else if (epoch > 100000000000UL) // 毫秒
                {
                    epoch = epoch / 1000UL;
                }
                if (epoch > 0 && epoch < 253402300799)
                {
                    DateTime dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    return dt.AddSeconds(epoch).ToLocalTime();
                }
            }
            catch {}
            return DateTime.MinValue;
        }

        internal static void ResolveProjectInfo(ConversationSummaryInfo info, Dictionary<string, ProjectMetadata> projectsMap)
        {
            if (info == null) return;
            if (!string.IsNullOrEmpty(info.ProjectId) &&
                string.Equals(info.ProjectId.Trim(), "outside-of-project", StringComparison.OrdinalIgnoreCase))
            {
                info.ProjectId = "outside-of-project";
                info.ProjectName = "Outside of Project";
                info.FolderUri = null;
                ApplySubagentNameFallback(info);
                return;
            }

            if (projectsMap != null && projectsMap.Count > 0)
            {
                if (!string.IsNullOrEmpty(info.ProjectId) && projectsMap.ContainsKey(info.ProjectId))
                {
                    ProjectMetadata pm = projectsMap[info.ProjectId];
                    info.ProjectName = pm.Name;
                    if (string.IsNullOrEmpty(info.FolderUri)) info.FolderUri = pm.FolderUri;
                    ApplySubagentNameFallback(info);
                    return;
                }

                if (!string.IsNullOrEmpty(info.FolderUri))
                {
                    string normUri = DirectStreamArchiver.NormalizeUri(info.FolderUri);
                    foreach (KeyValuePair<string, ProjectMetadata> kvp in projectsMap)
                    {
                        bool matched = string.Equals(normUri, DirectStreamArchiver.NormalizeUri(kvp.Value.FolderUri), StringComparison.OrdinalIgnoreCase);
                        if (!matched && kvp.Value.FolderUris != null)
                        {
                            for (int u = 0; u < kvp.Value.FolderUris.Count; u++)
                            {
                                if (string.Equals(normUri, DirectStreamArchiver.NormalizeUri(kvp.Value.FolderUris[u]), StringComparison.OrdinalIgnoreCase))
                                {
                                    matched = true;
                                    break;
                                }
                            }
                        }
                        if (matched)
                        {
                            info.ProjectName = kvp.Value.Name;
                            info.ProjectId = kvp.Value.Id;
                            ApplySubagentNameFallback(info);
                            return;
                        }
                    }
                }
            }

            if ((string.IsNullOrEmpty(info.ProjectName) || info.ProjectName == "Outside of Project") && !string.IsNullOrEmpty(info.FolderUri))
            {
                info.ProjectName = ExtractProjectNameFromUri(info.FolderUri);
            }

            if (string.IsNullOrEmpty(info.ProjectName))
            {
                info.ProjectName = "Outside of Project";
            }
            ApplySubagentNameFallback(info);
        }

        private static void ApplySubagentNameFallback(ConversationSummaryInfo info)
        {
            if (info == null || string.IsNullOrEmpty(info.ProjectName)) return;
            if (info.ProjectName.StartsWith("subagent-", StringComparison.OrdinalIgnoreCase) ||
                info.ProjectName.StartsWith("subagent_", StringComparison.OrdinalIgnoreCase))
            {
                info.IsSubagent = true;
            }
        }

        public static string ExtractProjectNameFromUri(string uriStr)
        {
            if (string.IsNullOrEmpty(uriStr)) return "Outside of Project";
            try
            {
                string decoded = DirectStreamArchiver.UnescapeUriString(uriStr).Trim();
                if (decoded.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                    decoded = decoded.Substring(8);
                else if (decoded.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                    decoded = decoded.Substring(7);

                decoded = decoded.TrimEnd('/', '\\').Trim();
                if (string.IsNullOrEmpty(decoded)) return "Outside of Project";

                if (decoded.Length == 2 && char.IsLetter(decoded[0]) && decoded[1] == ':')
                    return "Drive_" + char.ToUpperInvariant(decoded[0]);

                int idx = decoded.LastIndexOf('/');
                int bIdx = decoded.LastIndexOf('\\');
                if (bIdx > idx) idx = bIdx;
                if (idx >= 0 && idx < decoded.Length - 1)
                {
                    string name = decoded.Substring(idx + 1).Trim();
                    if (!string.IsNullOrEmpty(name))
                    {
                        if (name.Length == 2 && char.IsLetter(name[0]) && name[1] == ':')
                            return "Drive_" + char.ToUpperInvariant(name[0]);
                        return name;
                    }
                }
                return decoded;
            }
            catch
            {
                return "Outside of Project";
            }
        }
    }
}
