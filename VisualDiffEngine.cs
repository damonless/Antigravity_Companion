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
        Conflicted,     // [路径冲突]
        LocalOnly       // [本地独有]
    }

    public enum ItemCategory
    {
        GlobalConfigRoot,
        GlobalRules,
        GlobalSkills,
        GlobalMcp,
        ProjectsRoot,
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
        }

        public string GetStatusBadge()
        {
            switch (Status)
            {
                case DiffStatus.NewInBackup:
                    if (LocalConversations.Count == 0) return "[新增工程]";
                    return string.Format("[含 {0} 条新增]", NewSessionCount);
                case DiffStatus.Aligned:
                    return "[已对齐]";
                case DiffStatus.Conflicted:
                    return "[路径冲突]";
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
            HashSet<string> localSessionIdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                        localSessionIdSet.Add(s.ConversationId);
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

                // 计算备份工程中有多少条是本地缺失的新会话
                int newConvs = 0;
                for (int b = 0; b < node.BackupConversations.Count; b++)
                {
                    string bCid = node.BackupConversations[b].ConversationId;
                    if (!string.IsNullOrEmpty(bCid) && !localSessionIdSet.Contains(bCid))
                    {
                        newConvs++;
                    }
                }
                node.NewSessionCount = newConvs;

                if (!hasLocal && hasBackup)
                {
                    node.Status = DiffStatus.NewInBackup;
                }
                else if (hasLocal && !hasBackup)
                {
                    node.Status = DiffStatus.LocalOnly;
                }
                else
                {
                    // 两端均存在：首先检查工作区路径是否冲突
                    string normLocal = DirectStreamArchiver.NormalizeUri(node.LocalFolderUri);
                    string normBackup = DirectStreamArchiver.NormalizeUri(node.BackupFolderUri);

                    bool pathMatches = string.IsNullOrEmpty(normLocal) || string.IsNullOrEmpty(normBackup) || 
                                       string.Equals(normLocal, normBackup, StringComparison.OrdinalIgnoreCase);

                    if (!pathMatches)
                    {
                        node.Status = DiffStatus.Conflicted;
                    }
                    else if (newConvs > 0)
                    {
                        // 路径一致但包含新会话，精确定位为新增状态
                        node.Status = DiffStatus.NewInBackup;
                    }
                    else
                    {
                        // 路径一致且所有会话两端均已对齐
                        node.Status = DiffStatus.Aligned;
                    }
                }

                results.Add(node);
            }

            results.Sort((a, b) => string.Compare(a.ProjectName, b.ProjectName, StringComparison.OrdinalIgnoreCase));
            return results;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // CONFLICT RESOLUTION WIZARD (Milestone 2 — F7)
    // ══════════════════════════════════════════════════════════════════════

    public enum ConflictChoice
    {
        Merge,
        Rename,
        Overwrite,
        Skip
    }

    public class ConflictResolutionResult
    {
        public ConflictChoice Choice;
        public string NewProjectName;
        public bool ApplyToAll;
    }

    public class ConflictResolutionDialog : Form
    {
        public ConflictResolutionResult Result { get; private set; }

        private RadioButton rbMerge;
        private RadioButton rbRename;
        private RadioButton rbOverwrite;
        private RadioButton rbSkip;
        private TextBox txtNewName;
        private CheckBox chkApplyAll;
        private Button btnOk;
        private Button btnCancel;

        public ConflictResolutionDialog(string projectName, string localUri, string backupUri)
        {
            Result = new ConflictResolutionResult { Choice = ConflictChoice.Merge, NewProjectName = projectName + "_Imported", ApplyToAll = false };
            InitializeComponent(projectName, localUri, backupUri);
        }

        private void InitializeComponent(string projectName, string localUri, string backupUri)
        {
            this.Text = "工程路径冲突解决向导 - " + projectName;
            this.Size = new Size(580, 420);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9f);
            this.BackColor = Color.White;

            Label lblInfo = new Label
            {
                Text = string.Format("检测到工程 [{0}] 在本地已存在，但工作区路径不同：\n\n• 本地路径: {1}\n• 备份路径: {2}\n\n请选择数据合流处理策略：",
                    projectName, string.IsNullOrEmpty(localUri) ? "(未绑定路径)" : localUri, string.IsNullOrEmpty(backupUri) ? "(未绑定路径)" : backupUri),
                Location = new Point(20, 16),
                Size = new Size(520, 95),
                ForeColor = Color.FromArgb(30, 41, 59)
            };

            rbMerge = new RadioButton
            {
                Text = "合并 (Merge): 保持本地工作区路径不变，将备份会话增量合流到本地工程中 (推荐)",
                Location = new Point(24, 118),
                Size = new Size(520, 24),
                Checked = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(22, 101, 52)
            };

            rbRename = new RadioButton
            {
                Text = "重命名 (Rename): 作为独立新工程导入本地，指定新工程名称：",
                Location = new Point(24, 150),
                Size = new Size(520, 24),
                ForeColor = Color.FromArgb(30, 41, 59)
            };

            txtNewName = new TextBox
            {
                Text = projectName + "_Imported",
                Location = new Point(48, 178),
                Width = 260,
                Enabled = false
            };

            rbRename.CheckedChanged += (s, e) => { txtNewName.Enabled = rbRename.Checked; };

            rbOverwrite = new RadioButton
            {
                Text = "覆盖 (Overwrite): 强制使用备份中的工程路径与会话覆盖本地工程",
                Location = new Point(24, 212),
                Size = new Size(520, 24),
                ForeColor = Color.FromArgb(194, 65, 12)
            };

            rbSkip = new RadioButton
            {
                Text = "跳过 (Skip): 忽略此工程下的所有备份会话，不进行任何合流",
                Location = new Point(24, 242),
                Size = new Size(520, 24),
                ForeColor = Color.FromArgb(100, 116, 139)
            };

            chkApplyAll = new CheckBox
            {
                Text = "对此备份包中后续所有发生冲突的工程应用相同策略",
                Location = new Point(24, 285),
                Size = new Size(500, 24),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            btnOk = new Button
            {
                Text = "确定",
                DialogResult = DialogResult.OK,
                Location = new Point(360, 330),
                Width = 85,
                Height = 28,
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOk.Click += (s, e) =>
            {
                if (rbMerge.Checked) Result.Choice = ConflictChoice.Merge;
                else if (rbRename.Checked)
                {
                    Result.Choice = ConflictChoice.Rename;
                    Result.NewProjectName = txtNewName.Text.Trim();
                }
                else if (rbOverwrite.Checked) Result.Choice = ConflictChoice.Overwrite;
                else if (rbSkip.Checked) Result.Choice = ConflictChoice.Skip;

                Result.ApplyToAll = chkApplyAll.Checked;
            };

            btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(455, 330),
                Width = 85,
                Height = 28,
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat
            };

            this.Controls.AddRange(new Control[] {
                lblInfo, rbMerge, rbRename, txtNewName, rbOverwrite, rbSkip, chkApplyAll, btnOk, btnCancel
            });
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
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
