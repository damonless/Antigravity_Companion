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
        NewInBackup,    // [★ 新增]
        Aligned,        // [已对齐]
        Conflicted,     // [重名/路径差异]
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
        public bool IsTemporaryOrJunk;
        public List<ConversationSummaryInfo> LocalConversations;
        public List<ConversationSummaryInfo> BackupConversations;

        public ProjectDiffNode(string name)
        {
            ProjectName = name;
            LocalConversations = new List<ConversationSummaryInfo>();
            BackupConversations = new List<ConversationSummaryInfo>();
            IsTemporaryOrJunk = SmartProjectFilter.IsJunkOrEphemeral(name);
        }

        public string GetStatusBadge()
        {
            switch (Status)
            {
                case DiffStatus.NewInBackup: return "[★ 新增]";
                case DiffStatus.Aligned: return "[已对齐]";
                case DiffStatus.Conflicted: return "[重名/路径差异]";
                case DiffStatus.LocalOnly: return "[本地独有]";
                default: return string.Empty;
            }
        }
    }

    public static class VisualDiffEngine
    {
        public static List<ProjectDiffNode> ComputeDiff(List<ConversationSummaryInfo> localSummaries, 
                                                        List<ConversationSummaryInfo> backupSummaries)
        {
            Dictionary<string, ProjectDiffNode> nodeMap = new Dictionary<string, ProjectDiffNode>(StringComparer.OrdinalIgnoreCase);

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
                    // 两端均存在：比对工作区 URI 路径
                    string normLocal = DirectStreamArchiver.NormalizeUri(node.LocalFolderUri);
                    string normBackup = DirectStreamArchiver.NormalizeUri(node.BackupFolderUri);

                    if (string.IsNullOrEmpty(normLocal) || string.IsNullOrEmpty(normBackup) || 
                        string.Equals(normLocal, normBackup, StringComparison.OrdinalIgnoreCase))
                    {
                        node.Status = DiffStatus.Aligned;
                    }
                    else
                    {
                        node.Status = DiffStatus.Conflicted;
                    }
                }

                results.Add(node);
            }

            results.Sort((a, b) => string.Compare(a.ProjectName, b.ProjectName, StringComparison.OrdinalIgnoreCase));
            return results;
        }
    }

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
        public string NewFolderUri;
        public bool ApplyToAll;
    }

    public class ConflictResolutionDialog : Form
    {
        private RadioButton rbMerge;
        private RadioButton rbRename;
        private RadioButton rbOverwrite;
        private RadioButton rbSkip;
        private TextBox txtRename;
        private CheckBox chkApplyAll;
        private Button btnOk;

        public ConflictResolutionResult Result { get; private set; }

        public ConflictResolutionDialog(string projectName, string localUri, string backupUri)
        {
            InitializeComponent(projectName, localUri, backupUri);
        }

        private void InitializeComponent(string projectName, string localUri, string backupUri)
        {
            this.Text = "工程重名/路径冲突解决向导 (Conflict Resolution)";
            this.Size = new Size(620, 430);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Microsoft YaHei UI", 9f);
            this.BackColor = Color.White;

            Label lblHeader = new Label
            {
                Text = "⚠️ 检测到备份工程与当前环境存在重名或路径不一致冲突：",
                Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(192, 0, 0),
                Location = new Point(16, 12),
                Size = new Size(570, 24)
            };

            Panel pnlInfo = new Panel
            {
                Location = new Point(16, 40),
                Size = new Size(570, 95),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(248, 249, 250)
            };

            Label lblProj = new Label { Text = "工程名称: " + projectName, Location = new Point(10, 8), AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };
            Label lblLocal = new Label { Text = "本地路径: " + (localUri ?? "(未指定)"), Location = new Point(10, 32), AutoSize = true, ForeColor = Color.DarkSlateGray };
            Label lblBackup = new Label { Text = "备份路径: " + (backupUri ?? "(未指定)"), Location = new Point(10, 56), AutoSize = true, ForeColor = Color.DarkBlue };
            pnlInfo.Controls.AddRange(new Control[] { lblProj, lblLocal, lblBackup });

            GroupBox gbChoices = new GroupBox
            {
                Text = "请选择冲突解决策略：",
                Location = new Point(16, 145),
                Size = new Size(570, 180)
            };

            rbMerge = new RadioButton { Text = "【合并】将备份会话直接并入本地现有工程 (保留本地路径，不创建新工程)", Location = new Point(16, 24), AutoSize = true, Checked = true };
            rbRename = new RadioButton { Text = "【重命名】将备份工程作为独立工程导入，新工程名称：", Location = new Point(16, 56), AutoSize = true };
            txtRename = new TextBox { Text = projectName + "_Imported", Location = new Point(36, 82), Width = 300, Enabled = false };
            rbOverwrite = new RadioButton { Text = "【覆盖】使用备份包的工程路径与配置完全替换本地工程", Location = new Point(16, 114), AutoSize = true };
            rbSkip = new RadioButton { Text = "【跳过】跳过此工程及其会话的导入", Location = new Point(16, 144), AutoSize = true };

            rbRename.CheckedChanged += (s, e) => { txtRename.Enabled = rbRename.Checked; };

            gbChoices.Controls.AddRange(new Control[] { rbMerge, rbRename, txtRename, rbOverwrite, rbSkip });

            chkApplyAll = new CheckBox
            {
                Text = "对本次后续所有冲突工程应用相同策略 (不再重复弹窗)",
                Location = new Point(16, 335),
                AutoSize = true
            };

            btnOk = new Button
            {
                Text = "确定执行",
                DialogResult = DialogResult.OK,
                Location = new Point(486, 345),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOk.Click += BtnOk_Click;

            this.Controls.AddRange(new Control[] { lblHeader, pnlInfo, gbChoices, chkApplyAll, btnOk });
            this.AcceptButton = btnOk;
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            ConflictChoice choice = ConflictChoice.Merge;
            if (rbRename.Checked) choice = ConflictChoice.Rename;
            else if (rbOverwrite.Checked) choice = ConflictChoice.Overwrite;
            else if (rbSkip.Checked) choice = ConflictChoice.Skip;

            Result = new ConflictResolutionResult
            {
                Choice = choice,
                NewProjectName = txtRename.Text.Trim(),
                ApplyToAll = chkApplyAll.Checked
            };
            this.Close();
        }
    }

    public class ImportHighlightTracker
    {
        private readonly HashSet<string> _importedConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _importedProjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void Clear()
        {
            _importedConvIds.Clear();
            _importedProjectNames.Clear();
        }

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

        public int HighlightedCount { get { return _importedConvIds.Count; } }
    }
}
