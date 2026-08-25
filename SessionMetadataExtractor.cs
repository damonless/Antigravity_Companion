using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // SESSION METADATA & DATA MODELS (Milestone 1 — F1, F3, F5)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 会话摘要与元数据模型
    /// </summary>
    public class ConversationSummaryInfo
    {
        public string ConversationId;      // UUID (Field 1)
        public string DisplayTitle;        // 最终呈现标题 (Field 15 用户自定义标题 优先于 Field 1 自动摘要)
        public string CustomTitle;         // Field 15 -> Field 1 (用户重命名真实标题)
        public bool HasCustomTitle;        // 是否包含有效的用户自定义重命名标题
        public string AutoSummaryTitle;    // Field 1 (系统自动生成摘要)
        public string FolderUri;           // Field 9 -> Field 1 或 Field 17 -> Field 7
        public string ProjectName;         // 所属工程名称
        public string ProjectId;           // Field 17 -> Field 18 (工程 UUID 或 "outside-of-project")
        public DateTime UpdatedTime;       // 最近更新时间
        public DateTime CreatedTime;       // 创建时间
        public int StepCount;              // 对话步数/消息数
        public int UserInteractions;       // 用户交互步数 (Field 16)
        public int? Field23;               // Field 23 标志位 (0 为 Subagent / Ephemeral)
        public bool IsSubagent;            // 是否为临时子代理会话
        public string SubagentRole;        // 子代理角色
        public string ParentConversationId;// 父会话 UUID
        public ProtoNode RawProtoEntry;    // AST 原始节点
        public byte[] RawBytes;            // 原始二进制切片

        public override string ToString()
        {
            return string.Format("{0} [{1}] ({2})", DisplayTitle, ConversationId, ProjectName);
        }
    }

    /// <summary>
    /// 工程配置元数据
    /// </summary>
    public class ProjectMetadata
    {
        public string Id;
        public string Name;
        public string FolderUri;
    }

    /// <summary>
    /// 备份包/本地环境内存索引
    /// </summary>
    public class BackupArchiveIndex
    {
        public string SourceHome;
        public string SourceUser;
        public string BackupTime;
        public string Version;
        public Dictionary<string, ProjectMetadata> Projects;
        public List<ConversationSummaryInfo> Conversations;
        public HashSet<string> ExistingConvoDbFiles;
        public HashSet<string> ExistingBrainDirs;
        public bool HasRules;
        public bool HasSkills;
        public bool HasMcp;
        public long TotalZipSizeBytes;
        public long ExtractedMetadataSizeBytes;
        public double ElapsedMilliseconds;

        public BackupArchiveIndex()
        {
            Projects = new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
            Conversations = new List<ConversationSummaryInfo>();
            ExistingConvoDbFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ExistingBrainDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 流式 Zip 内存解析包
    /// </summary>
    public class ZipMetadataPackage
    {
        public byte[] SummaryProtoBytes;
        public Dictionary<string, string> ProjectConfigs;
        public string MetadataJson;
        public List<string> ConversationDbNames;
        public List<string> BrainDirectoryNames;
        public bool HasGlobalRules;
        public bool HasGlobalSkills;
        public bool HasGlobalMcp;

        public ZipMetadataPackage()
        {
            ProjectConfigs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ConversationDbNames = new List<string>();
            BrainDirectoryNames = new List<string>();
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
            if (lower.StartsWith("temp-") || lower.StartsWith("temp_"))
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

        public static ConversationSummaryInfo ParseEntryNode(ProtoNode entryNode)
        {
            if (entryNode == null) return null;
            ConversationSummaryInfo info = new ConversationSummaryInfo();
            info.RawProtoEntry = entryNode;

            if (entryNode.Children == null || entryNode.Children.Count == 0)
            {
                if (entryNode.RawBytes != null && entryNode.RawBytes.Length > 0)
                {
                    List<ProtoNode> sub = ProtoCodec.Decode(entryNode.RawBytes);
                    if (sub != null && sub.Count > 0) entryNode.Children = sub;
                }
            }

            if (entryNode.Children == null)
            {
                entryNode.Children = new List<ProtoNode>();
            }

            ProtoNode metaSubmessage = entryNode.GetChild(2);
            bool isRealProtoLayout = metaSubmessage != null && metaSubmessage.HasChildren;

            if (isRealProtoLayout)
            {
                ProtoNode f1 = entryNode.GetChild(1);
                if (f1 != null) info.ConversationId = f1.GetString();

                for (int i = 0; i < metaSubmessage.Children.Count; i++)
                {
                    ProtoNode child = metaSubmessage.Children[i];
                    if (child.FieldNumber == 1) // Auto Summary Title
                    {
                        info.AutoSummaryTitle = child.GetString();
                    }
                    else if (child.FieldNumber == 2) // Step count
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
                    else if (child.FieldNumber == 16) // User Interactions count
                    {
                        info.UserInteractions = child.GetInt32();
                    }
                    else if (child.FieldNumber == 17) // CascadeInfo
                    {
                        ParseCascadeNode(child, info);
                    }
                    else if (child.FieldNumber == 23) // Subagent / Ephemeral flag ★
                    {
                        info.Field23 = child.GetInt32();
                        if (info.Field23 == 0) info.IsSubagent = true;
                    }
                }
            }
            else
            {
                // Flat / Mock layout
                for (int i = 0; i < entryNode.Children.Count; i++)
                {
                    ProtoNode child = entryNode.Children[i];
                    if (child.FieldNumber == 1)
                    {
                        string s = child.GetString();
                        if (entryNode.GetChild(2) != null)
                        {
                            info.AutoSummaryTitle = s;
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(info.ConversationId)) info.ConversationId = s;
                            else info.AutoSummaryTitle = s;
                        }
                    }
                    else if (child.FieldNumber == 2)
                    {
                        if (child.HasChildren)
                        {
                            for (int c = 0; c < child.Children.Count; c++)
                            {
                                ProtoNode sc = child.Children[c];
                                if (sc.FieldNumber == 15) ParseCustomTitleNode(sc, info);
                                else if (sc.FieldNumber == 1) info.AutoSummaryTitle = sc.GetString();
                                else if (sc.FieldNumber == 16) info.UserInteractions = sc.GetInt32();
                                else if (sc.FieldNumber == 23) { info.Field23 = sc.GetInt32(); if (info.Field23 == 0) info.IsSubagent = true; }
                            }
                        }
                        else
                        {
                            info.ConversationId = child.GetString();
                        }
                    }
                    else if (child.FieldNumber == 3)
                    {
                        if (child.WireType == 0) info.CreatedTime = ConvertEpochToDateTime(child.VarintValue);
                        else info.CreatedTime = ParseTimestampNode(child);
                    }
                    else if (child.FieldNumber == 4)
                    {
                        if (child.WireType == 0) info.UpdatedTime = ConvertEpochToDateTime(child.VarintValue);
                        else info.UpdatedTime = ParseTimestampNode(child);
                    }
                    else if (child.FieldNumber == 5)
                    {
                        if (child.WireType == 0) info.StepCount = (int)child.VarintValue;
                    }
                    else if (child.FieldNumber == 7)
                    {
                        info.CreatedTime = ParseTimestampNode(child);
                    }
                    else if (child.FieldNumber == 9)
                    {
                        ParseWorkspaceNode(child, info);
                    }
                    else if (child.FieldNumber == 10)
                    {
                        DateTime t = ParseTimestampNode(child);
                        if (t > info.UpdatedTime) info.UpdatedTime = t;
                    }
                    else if (child.FieldNumber == 15)
                    {
                        ParseCustomTitleNode(child, info);
                    }
                    else if (child.FieldNumber == 16)
                    {
                        info.UserInteractions = child.GetInt32();
                    }
                    else if (child.FieldNumber == 17)
                    {
                        ParseCascadeNode(child, info);
                    }
                    else if (child.FieldNumber == 23)
                    {
                        info.Field23 = child.GetInt32();
                        if (info.Field23 == 0) info.IsSubagent = true;
                    }
                }
            }

            // 智能识别 Subagent / 临时子会话
            if (info.Field23.HasValue && info.Field23.Value == 0)
            {
                info.IsSubagent = true;
            }
            else if (info.Field23.HasValue && info.Field23.Value == 1 && info.UserInteractions == 0)
            {
                info.IsSubagent = true;
            }
            else if (!string.IsNullOrEmpty(info.ProjectName) && (info.ProjectName.StartsWith("subagent-", StringComparison.OrdinalIgnoreCase) || info.ProjectName.StartsWith("subagent_", StringComparison.OrdinalIgnoreCase)))
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

            if (string.IsNullOrEmpty(info.ConversationId))
            {
                info.ConversationId = Guid.NewGuid().ToString();
            }

            if (string.IsNullOrEmpty(info.ProjectName) && !string.IsNullOrEmpty(info.FolderUri))
            {
                info.ProjectName = ExtractProjectNameFromUri(info.FolderUri);
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

        private static void ParseCascadeNode(ProtoNode node, ConversationSummaryInfo info)
        {
            if (node == null) return;
            if (node.HasChildren)
            {
                ProtoNode nameNode = node.GetChild(1);
                if (nameNode != null && string.IsNullOrEmpty(info.ProjectName))
                {
                    info.ProjectName = nameNode.GetString();
                }
                ProtoNode roleNode = node.GetChild(4);
                if (roleNode != null)
                {
                    info.SubagentRole = roleNode.GetString();
                    if (!string.IsNullOrEmpty(info.SubagentRole)) info.IsSubagent = true;
                }
                ProtoNode parentNode = node.GetChild(11);
                if (parentNode != null)
                {
                    info.ParentConversationId = parentNode.GetString();
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
            else if (node.RawBytes != null && node.RawBytes.Length == 8)
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
                if (epoch > 1000000000000000) // 微秒/纳秒
                {
                    epoch = epoch / 1000000;
                }
                else if (epoch > 100000000000) // 毫秒
                {
                    epoch = epoch / 1000;
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

        private static void ResolveProjectInfo(ConversationSummaryInfo info, Dictionary<string, ProjectMetadata> projectsMap)
        {
            if (projectsMap != null && projectsMap.Count > 0)
            {
                if (!string.IsNullOrEmpty(info.FolderUri))
                {
                    string normUri = DirectStreamArchiver.NormalizeUri(info.FolderUri);
                    foreach (KeyValuePair<string, ProjectMetadata> kvp in projectsMap)
                    {
                        string pNorm = DirectStreamArchiver.NormalizeUri(kvp.Value.FolderUri);
                        if (string.Equals(normUri, pNorm, StringComparison.OrdinalIgnoreCase))
                        {
                            info.ProjectName = kvp.Value.Name;
                            info.ProjectId = kvp.Value.Id;
                            return;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(info.ProjectName) && !string.IsNullOrEmpty(info.FolderUri))
            {
                info.ProjectName = ExtractProjectNameFromUri(info.FolderUri);
            }

            if (string.IsNullOrEmpty(info.ProjectName))
            {
                info.ProjectName = "Outside of Project";
            }
        }

        public static string ExtractProjectNameFromUri(string uriStr)
        {
            if (string.IsNullOrEmpty(uriStr)) return "Outside of Project";
            try
            {
                string decoded = DirectStreamArchiver.UnescapeUriString(uriStr).TrimEnd('/', '\\');
                int idx = decoded.LastIndexOf('/');
                if (idx < 0) idx = decoded.LastIndexOf('\\');
                if (idx >= 0 && idx < decoded.Length - 1)
                {
                    string name = decoded.Substring(idx + 1).Trim();
                    if (!string.IsNullOrEmpty(name)) return name;
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
