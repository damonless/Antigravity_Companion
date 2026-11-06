using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // VISUAL DIFF ENGINE & CONFLICT RESOLUTION (Milestone 2 — F6, F7, F11)
    // ══════════════════════════════════════════════════════════════════════

    public enum DiffStatus
    {
        NewInBackup,    // [新增工程 / 含新增会话]
        Aligned,        // [已对齐]
        LocalOnly,      // [本地独有]
        Modified        // [已修改 / 内容有更新 / 已改名]
    }

    public enum ItemCategory
    {
        Project,
        Conversation
    }

    public class DiffItemNodeTag
    {
        public ItemCategory Category;
        public string Id;                   // UUID or Config Key
        public string DisplayName;          // Title or Name
        public string ProjectName;          // Project Name
        public string SourcePath;           // Origin path
        public string TargetPath;           // Local mapped target path
        public DateTime? Timestamp;         // Last modified timestamp
        public DiffStatus Status;           // Visual diff status
        public bool IsTemporaryOrJunk;      // Ephemeral / Junk / Subagent
        public ConversationSummaryInfo SummaryInfo;
        public ProjectDiffNode ProjectNode;
        public object TagData;              // Additional metadata
    }

    public class ProjectDiffNode
    {
        public string ProjectName;
        public string LocalFolderUri;
        public string BackupFolderUri;
        public DiffStatus Status;
        public int NewSessionCount;
        public int ModifiedSessionCount;
        public string RenamedFromOrTo;
        public bool IsTemporaryOrJunk;
        public List<ConversationSummaryInfo> LocalConversations;
        public List<ConversationSummaryInfo> BackupConversations;

        public ProjectDiffNode(string name)
        {
            ProjectName = name;
            LocalConversations = new List<ConversationSummaryInfo>();
            BackupConversations = new List<ConversationSummaryInfo>();
            IsTemporaryOrJunk = SmartProjectFilter.IsJunkOrEphemeral(name);
            NewSessionCount = 0;
            ModifiedSessionCount = 0;
        }

        public string GetStatusBadge()
        {
            switch (Status)
            {
                case DiffStatus.NewInBackup:
                    if (LocalConversations.Count == 0) return "[新增工程]";
                    if (ModifiedSessionCount > 0)
                        return string.Format("[含 {0} 新增, {1} 更新]", NewSessionCount, ModifiedSessionCount);
                    return string.Format("[含 {0} 条新增]", NewSessionCount);
                case DiffStatus.Modified:
                    if (!string.IsNullOrEmpty(RenamedFromOrTo))
                        return string.Format("[工程已改名: 本地为 '{0}']", RenamedFromOrTo);
                    return string.Format("[含 {0} 条已修改/更新]", ModifiedSessionCount);
                case DiffStatus.Aligned:
                    return "[已对齐]";
                case DiffStatus.LocalOnly:
                    return "[本地独有]";
                default:
                    return string.Empty;
            }
        }
    }

    public static class VisualDiffEngine
    {
        public static List<ProjectDiffNode> ComputeDiff(List<ConversationSummaryInfo> localSummaries, 
                                                        List<ConversationSummaryInfo> backupSummaries)
        {
            Dictionary<string, ProjectDiffNode> nodeMap = new Dictionary<string, ProjectDiffNode>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ConversationSummaryInfo> localConvMap = new Dictionary<string, ConversationSummaryInfo>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> localUriToProjectNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (localSummaries != null)
            {
                for (int i = 0; i < localSummaries.Count; i++)
                {
                    ConversationSummaryInfo s = localSummaries[i];
                    string p = s.ProjectName ?? "Outside of Project";
                    if (!nodeMap.ContainsKey(p)) nodeMap[p] = new ProjectDiffNode(p);
                    nodeMap[p].LocalConversations.Add(s);
                    if (string.IsNullOrEmpty(nodeMap[p].LocalFolderUri) && !string.IsNullOrEmpty(s.FolderUri))
                        nodeMap[p].LocalFolderUri = s.FolderUri;

                    if (!string.IsNullOrEmpty(s.ConversationId))
                        localConvMap[s.ConversationId] = s;

                    if (!string.IsNullOrEmpty(s.FolderUri))
                    {
                        string normUri = DirectStreamArchiver.NormalizeUri(s.FolderUri);
                        if (!string.IsNullOrEmpty(normUri) && !localUriToProjectNameMap.ContainsKey(normUri) && !string.Equals(p, "Outside of Project", StringComparison.OrdinalIgnoreCase))
                        {
                            localUriToProjectNameMap[normUri] = p;
                        }
                    }
                }
            }

            if (backupSummaries != null)
            {
                for (int i = 0; i < backupSummaries.Count; i++)
                {
                    ConversationSummaryInfo s = backupSummaries[i];
                    string p = s.ProjectName ?? "Outside of Project";
                    if (!nodeMap.ContainsKey(p)) nodeMap[p] = new ProjectDiffNode(p);
                    nodeMap[p].BackupConversations.Add(s);
                    if (string.IsNullOrEmpty(nodeMap[p].BackupFolderUri) && !string.IsNullOrEmpty(s.FolderUri))
                        nodeMap[p].BackupFolderUri = s.FolderUri;
                }
            }

            List<ProjectDiffNode> results = new List<ProjectDiffNode>();
            foreach (KeyValuePair<string, ProjectDiffNode> kvp in nodeMap)
            {
                ProjectDiffNode node = kvp.Value;
                bool hasLocal = node.LocalConversations.Count > 0;
                bool hasBackup = node.BackupConversations.Count > 0;

                // 计算备份工程中有多少条是新增会话，多少条是有变更会话
                int newConvs = 0;
                int modifiedConvs = 0;
                int alignedConvs = 0;

                for (int b = 0; b < node.BackupConversations.Count; b++)
                {
                    ConversationSummaryInfo bConv = node.BackupConversations[b];
                    string bCid = bConv.ConversationId;
                    if (string.IsNullOrEmpty(bCid)) continue;

                    ConversationSummaryInfo lConv;
                    if (!localConvMap.TryGetValue(bCid, out lConv))
                    {
                        newConvs++;
                    }
                    else
                    {
                        bool titleDiff = !string.Equals(bConv.DisplayTitle, lConv.DisplayTitle, StringComparison.Ordinal);
                        bool stepDiff = bConv.StepCount != lConv.StepCount || bConv.UserInteractions != lConv.UserInteractions;
                        bool timeDiff = Math.Abs((bConv.UpdatedTime - lConv.UpdatedTime).TotalSeconds) > 2 && bConv.UpdatedTime > DateTime.MinValue && lConv.UpdatedTime > DateTime.MinValue;
                        bool projDiff = !string.Equals(bConv.ProjectName, lConv.ProjectName, StringComparison.OrdinalIgnoreCase);

                        if (titleDiff || stepDiff || timeDiff || projDiff)
                        {
                            modifiedConvs++;
                        }
                        else
                        {
                            alignedConvs++;
                        }
                    }
                }
                node.NewSessionCount = newConvs;
                node.ModifiedSessionCount = modifiedConvs;

                if (!hasLocal && hasBackup)
                {
                    // 检查本地是否存在相同工作区路径但不同工程名的项目（工程改名识别）
                    string normBackupUri = DirectStreamArchiver.NormalizeUri(node.BackupFolderUri);
                    string localProjName;
                    if (!string.IsNullOrEmpty(normBackupUri) && localUriToProjectNameMap.TryGetValue(normBackupUri, out localProjName) && !string.Equals(localProjName, node.ProjectName, StringComparison.OrdinalIgnoreCase))
                    {
                        node.Status = DiffStatus.Modified;
                        node.RenamedFromOrTo = localProjName;
                    }
                    else
                    {
                        node.Status = DiffStatus.NewInBackup;
                    }
                }
                else if (hasLocal && !hasBackup)
                {
                    node.Status = DiffStatus.LocalOnly;
                }
                else
                {
                    if (newConvs > 0)
                    {
                        node.Status = DiffStatus.NewInBackup;
                    }
                    else if (modifiedConvs > 0)
                    {
                        node.Status = DiffStatus.Modified;
                    }
                    else
                    {
                        node.Status = DiffStatus.Aligned;
                    }
                }

                results.Add(node);
            }

            results.Sort((a, b) => string.Compare(a.ProjectName, b.ProjectName, StringComparison.OrdinalIgnoreCase));
            return results;
        }
    }

    public class ImportHighlightTracker
    {
        private readonly HashSet<string> _importedConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _importedProjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void AddImportedConversation(string convId)
        {
            if (!string.IsNullOrEmpty(convId)) _importedConvIds.Add(convId);
        }

        public void AddImportedProject(string projectName)
        {
            if (!string.IsNullOrEmpty(projectName)) _importedProjectNames.Add(projectName);
        }

        public bool IsConversationHighlighted(string convId)
        {
            return !string.IsNullOrEmpty(convId) && _importedConvIds.Contains(convId);
        }

        public bool IsProjectHighlighted(string projectName)
        {
            return !string.IsNullOrEmpty(projectName) && _importedProjectNames.Contains(projectName);
        }

        public void Clear()
        {
            _importedConvIds.Clear();
            _importedProjectNames.Clear();
        }
    }
}
