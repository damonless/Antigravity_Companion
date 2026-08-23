using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AntigravityCompanion
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    // ── 配置浏览器列表项 ──
    public class BrowserItem
    {
        public string DisplayName;
        public string FilePath;
        public BrowserItem(string name, string path) { DisplayName = name; FilePath = path; }
        public override string ToString() { return DisplayName; }
    }

    // ── 差异状态枚举 (Milestone 2) ──
    public enum DiffStatus
    {
        NewInBackup,    // [★ 新增]
        Aligned,        // [已对齐]
        Conflicted,     // [重名/路径差异]
        LocalOnly       // [本地独有]
    }

    // ── 节点类别枚举 ──
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

    // ── 树节点附加元数据 ──
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
        public bool IsTemporaryOrJunk;      // Ephemeral / Junk
        public ConversationSummaryInfo SummaryInfo;
        public ProjectDiffNode ProjectNode;
        public object TagData;              // Additional metadata
    }

    // ── 双缓冲 TreeView（彻底消除闪烁，支持 Explorer 样式）──
    public class DoubleBufferedTreeView : TreeView
    {
        public DoubleBufferedTreeView()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | 
                          ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                SendMessage(this.Handle, 0x1100 + 44, (IntPtr)0, (IntPtr)0x0040); // TVS_EX_DOUBLEBUFFER
            }
            catch {}
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
    }

    // ══════════════════════════════════════════════════════════════════════
    // SMART PROJECT FILTER & VISUAL DIFF ENGINE (Milestone 2 — F5, F6)
    // ══════════════════════════════════════════════════════════════════════
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

            return false;
        }

        public static bool IsTemporaryOrJunkProject(string projectName, string folderUri = null)
        {
            if (IsJunkOrEphemeral(projectName)) return true;
            if (!string.IsNullOrEmpty(folderUri))
            {
                string lower = folderUri.Trim().ToLowerInvariant();
                if (lower.Contains("outside-of-project") || lower.Contains("subagent-") || lower.Contains(".system_generated"))
                    return true;
            }
            return false;
        }
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
                default: return "";
            }
        }
    }

    public static class VisualDiffEngine
    {
        public static List<ProjectDiffNode> ComputeDiff(
            List<ConversationSummaryInfo> localSummaries,
            List<ConversationSummaryInfo> backupSummaries)
        {
            Dictionary<string, ProjectDiffNode> map = new Dictionary<string, ProjectDiffNode>(StringComparer.OrdinalIgnoreCase);

            // Populate Local
            if (localSummaries != null)
            {
                for (int i = 0; i < localSummaries.Count; i++)
                {
                    ConversationSummaryInfo info = localSummaries[i];
                    string pName = info.ProjectName ?? "Default Project";
                    ProjectDiffNode node;
                    if (!map.TryGetValue(pName, out node))
                    {
                        node = new ProjectDiffNode(pName);
                        map[pName] = node;
                    }
                    node.LocalConversations.Add(info);
                    if (string.IsNullOrEmpty(node.LocalFolderUri) && !string.IsNullOrEmpty(info.FolderUri))
                        node.LocalFolderUri = info.FolderUri;
                }
            }

            // Populate Backup
            if (backupSummaries != null)
            {
                for (int i = 0; i < backupSummaries.Count; i++)
                {
                    ConversationSummaryInfo info = backupSummaries[i];
                    string pName = info.ProjectName ?? "Default Project";
                    ProjectDiffNode node;
                    if (!map.TryGetValue(pName, out node))
                    {
                        node = new ProjectDiffNode(pName);
                        map[pName] = node;
                    }
                    node.BackupConversations.Add(info);
                    if (string.IsNullOrEmpty(node.BackupFolderUri) && !string.IsNullOrEmpty(info.FolderUri))
                        node.BackupFolderUri = info.FolderUri;
                }
            }

            // Compute Status
            List<ProjectDiffNode> result = new List<ProjectDiffNode>();
            foreach (KeyValuePair<string, ProjectDiffNode> kvp in map)
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
                    if (NormalizeUri(node.LocalFolderUri) == NormalizeUri(node.BackupFolderUri))
                    {
                        node.Status = DiffStatus.Aligned;
                    }
                    else
                    {
                        node.Status = DiffStatus.Conflicted;
                    }
                }
                result.Add(node);
            }

            return result;
        }

        private static string NormalizeUri(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return "";
            return uri.Replace("file:///", "").Replace("file://", "").Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // CONFLICT RESOLUTION WIZARD (Milestone 2 — F7)
    // ══════════════════════════════════════════════════════════════════════
    public enum ConflictResolutionStrategy
    {
        Merge,
        Rename,
        Overwrite,
        Skip
    }

    public class ConflictResolutionPlan
    {
        public Dictionary<string, ConflictResolutionStrategy> Strategies;
        public Dictionary<string, string> RenamedProjects;

        public ConflictResolutionPlan()
        {
            Strategies = new Dictionary<string, ConflictResolutionStrategy>(StringComparer.OrdinalIgnoreCase);
            RenamedProjects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public class ConflictResolutionDialog : Form
    {
        private RadioButton rbMerge;
        private RadioButton rbRename;
        private RadioButton rbOverwrite;
        private RadioButton rbSkip;
        private TextBox txtRename;
        private CheckBox chkApplyToAll;
        private Button btnOk;
        private Button btnCancel;

        public ConflictResolutionStrategy Strategy
        {
            get
            {
                if (rbRename.Checked) return ConflictResolutionStrategy.Rename;
                if (rbOverwrite.Checked) return ConflictResolutionStrategy.Overwrite;
                if (rbSkip.Checked) return ConflictResolutionStrategy.Skip;
                return ConflictResolutionStrategy.Merge;
            }
        }

        public string RenamedName
        {
            get { return txtRename.Text.Trim(); }
        }

        public bool ApplyToAll
        {
            get { return chkApplyToAll.Checked; }
        }

        public ConflictResolutionDialog(string projectName, string localPath, int localCount, DateTime? localTime,
                                        string backupPath, int backupCount, int newCount)
        {
            BuildUI(projectName, localPath, localCount, localTime, backupPath, backupCount, newCount);
        }

        private void BuildUI(string projectName, string localPath, int localCount, DateTime? localTime,
                             string backupPath, int backupCount, int newCount)
        {
            this.Text = "工程重名与工作区冲突解决向导 (Conflict Resolution Wizard)";
            this.Size = new Size(580, 500);
            this.MinimumSize = new Size(550, 480);
            this.Font = new Font("Microsoft YaHei UI", 9f);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.RowCount = 4;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 140f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45f));
            root.Padding = new Padding(12, 10, 12, 10);
            this.Controls.Add(root);

            Label lblHeader = new Label();
            lblHeader.Text = "检测到备份包中的工程与当前本地已有工程存在【重名但工作区路径不同】冲突：\n工程名称：【 " + projectName + " 】";
            lblHeader.Dock = DockStyle.Fill;
            lblHeader.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            lblHeader.ForeColor = Color.DarkRed;
            root.Controls.Add(lblHeader, 0, 0);

            GroupBox grpDiff = new GroupBox();
            grpDiff.Text = "工程冲突对比详情";
            grpDiff.Dock = DockStyle.Fill;
            grpDiff.Font = new Font("Microsoft YaHei UI", 8.5f);

            TableLayoutPanel diffTbl = new TableLayoutPanel();
            diffTbl.Dock = DockStyle.Fill;
            diffTbl.RowCount = 4;
            diffTbl.ColumnCount = 1;
            diffTbl.Padding = new Padding(8, 2, 8, 4);

            Label lblLocal = new Label() { Text = "[本地现有工程] 路径: " + (string.IsNullOrEmpty(localPath) ? "(未设置)" : localPath), Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = Color.DarkSlateGray };
            string localTimeStr = localTime.HasValue ? localTime.Value.ToString("yyyy-MM-dd HH:mm") : "-";
            Label lblLocalStats = new Label() { Text = "  └ 已有会话数: " + localCount + " 条对话 (最近更新: " + localTimeStr + ")", Dock = DockStyle.Fill, ForeColor = Color.DarkSlateGray };

            Label lblBackup = new Label() { Text = "[备份待导工程] 路径: " + (string.IsNullOrEmpty(backupPath) ? "(未设置)" : backupPath), Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = Color.DarkBlue };
            Label lblBackupStats = new Label() { Text = "  └ 备份中会话数: " + backupCount + " 条对话 (包含 " + newCount + " 条本地缺失的新对话)", Dock = DockStyle.Fill, ForeColor = Color.DarkBlue };

            diffTbl.Controls.Add(lblLocal, 0, 0);
            diffTbl.Controls.Add(lblLocalStats, 0, 1);
            diffTbl.Controls.Add(lblBackup, 0, 2);
            diffTbl.Controls.Add(lblBackupStats, 0, 3);
            grpDiff.Controls.Add(diffTbl);
            root.Controls.Add(grpDiff, 0, 1);

            GroupBox grpStrat = new GroupBox();
            grpStrat.Text = "请选择冲突处理策略";
            grpStrat.Dock = DockStyle.Fill;
            grpStrat.Font = new Font("Microsoft YaHei UI", 8.5f);

            TableLayoutPanel stratTbl = new TableLayoutPanel();
            stratTbl.Dock = DockStyle.Fill;
            stratTbl.RowCount = 5;
            stratTbl.ColumnCount = 1;
            stratTbl.Padding = new Padding(10, 4, 10, 4);

            rbMerge = new RadioButton();
            rbMerge.Text = "1. 合并到本地已有工程 (Merge - 推荐)\n   保留本地物理路径，仅将勾选的新对话与脑区合流导入到该工程下";
            rbMerge.Dock = DockStyle.Fill;
            rbMerge.Checked = true;
            rbMerge.Height = 40;

            Panel renamePanel = new Panel();
            renamePanel.Dock = DockStyle.Fill;
            renamePanel.Height = 32;
            rbRename = new RadioButton();
            rbRename.Text = "2. 重命名为新工程导入 (Rename) -> 新工程名:";
            rbRename.AutoSize = true;
            rbRename.Location = new Point(0, 4);
            txtRename = new TextBox();
            txtRename.Text = projectName + "_Imported";
            txtRename.Location = new Point(310, 2);
            txtRename.Width = 200;
            txtRename.Enabled = false;
            rbRename.CheckedChanged += (s, e) => { txtRename.Enabled = rbRename.Checked; };
            renamePanel.Controls.Add(rbRename);
            renamePanel.Controls.Add(txtRename);

            rbOverwrite = new RadioButton();
            rbOverwrite.Text = "3. 覆盖本地工程配置 (Overwrite)\n   使用备份包中的工程配置与路径完全覆盖本地已有配置";
            rbOverwrite.Dock = DockStyle.Fill;
            rbOverwrite.Height = 36;

            rbSkip = new RadioButton();
            rbSkip.Text = "4. 跳过此工程 (Skip)\n   放弃导入此工程及其所有会话";
            rbSkip.Dock = DockStyle.Fill;
            rbSkip.Height = 30;

            chkApplyToAll = new CheckBox();
            chkApplyToAll.Text = "记住选择：对本次导入过程中后续所有重名冲突工程自动应用相同策略";
            chkApplyToAll.Dock = DockStyle.Fill;
            chkApplyToAll.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
            chkApplyToAll.ForeColor = Color.DarkSlateBlue;

            stratTbl.Controls.Add(rbMerge, 0, 0);
            stratTbl.Controls.Add(renamePanel, 0, 1);
            stratTbl.Controls.Add(rbOverwrite, 0, 2);
            stratTbl.Controls.Add(rbSkip, 0, 3);
            stratTbl.Controls.Add(chkApplyToAll, 0, 4);
            grpStrat.Controls.Add(stratTbl);
            root.Controls.Add(grpStrat, 0, 2);

            Panel btnPanel = new Panel();
            btnPanel.Dock = DockStyle.Fill;

            btnOk = new Button();
            btnOk.Text = "确定并继续 (OK)";
            btnOk.Size = new Size(130, 28);
            btnOk.Location = new Point(280, 6);
            btnOk.BackColor = Color.FromArgb(198, 224, 180);
            btnOk.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
            btnOk.Click += (s, e) => {
                if (rbRename.Checked && string.IsNullOrEmpty(txtRename.Text.Trim()))
                {
                    MessageBox.Show("请输入有效的新工程名称！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            btnCancel = new Button();
            btnCancel.Text = "取消导入 (Cancel)";
            btnCancel.Size = new Size(120, 28);
            btnCancel.Location = new Point(420, 6);
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            btnPanel.Controls.Add(btnOk);
            btnPanel.Controls.Add(btnCancel);
            root.Controls.Add(btnPanel, 0, 3);
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // CASCADING SELECTION & SELECTIVE IMPORTER (Milestone 3 — F9, F10, F11)
    // ══════════════════════════════════════════════════════════════════════
    public class CascadingSelectionModel
    {
        private bool _isUpdating = false;

        public void ToggleProjectSelection(bool checkState, List<string> childIds, HashSet<string> selectedIds)
        {
            if (_isUpdating) return;
            try
            {
                _isUpdating = true;
                if (childIds == null) return;
                for (int i = 0; i < childIds.Count; i++)
                {
                    if (checkState)
                        selectedIds.Add(childIds[i]);
                    else
                        selectedIds.Remove(childIds[i]);
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        public void SelectAll(List<ConversationSummaryInfo> allConversations, HashSet<string> selectedIds)
        {
            selectedIds.Clear();
            if (allConversations == null) return;
            for (int i = 0; i < allConversations.Count; i++)
            {
                if (!string.IsNullOrEmpty(allConversations[i].ConversationId))
                    selectedIds.Add(allConversations[i].ConversationId);
            }
        }

        public void SelectNone(HashSet<string> selectedIds)
        {
            selectedIds.Clear();
        }

        public void SelectNewOnly(List<ProjectDiffNode> diffNodes, HashSet<string> selectedIds)
        {
            selectedIds.Clear();
            if (diffNodes == null) return;
            for (int i = 0; i < diffNodes.Count; i++)
            {
                if (diffNodes[i].Status == DiffStatus.NewInBackup)
                {
                    for (int j = 0; j < diffNodes[i].BackupConversations.Count; j++)
                    {
                        string id = diffNodes[i].BackupConversations[j].ConversationId;
                        if (!string.IsNullOrEmpty(id))
                            selectedIds.Add(id);
                    }
                }
            }
        }
    }

    public class SelectiveImportRequest
    {
        public string BackupFilePath;
        public string TargetGeminiPath;
        public List<string> SelectedConversationIds;
        public List<string> SelectedProjectNames;
        public Dictionary<string, ConflictResolutionStrategy> ProjectStrategies;
        public Dictionary<string, string> ProjectRenames;
        public bool ImportGlobalRules;
        public bool ImportGlobalSkills;
        public bool ImportGlobalMcp;

        public SelectiveImportRequest()
        {
            SelectedConversationIds = new List<string>();
            SelectedProjectNames = new List<string>();
            ProjectStrategies = new Dictionary<string, ConflictResolutionStrategy>(StringComparer.OrdinalIgnoreCase);
            ProjectRenames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public class SelectiveImportResult
    {
        public bool Success;
        public int ImportedConversationsCount;
        public int ImportedBrainDirsCount;
        public int MergedProtoSummariesCount;
        public List<string> ImportedConversationIds;
        public List<string> ImportedProjectNames;
        public List<string> Errors;

        public SelectiveImportResult()
        {
            ImportedConversationIds = new List<string>();
            ImportedProjectNames = new List<string>();
            Errors = new List<string>();
        }
    }

    public static class SelectiveCascadingImporter
    {
        public static SelectiveImportResult ExecuteImport(SelectiveImportRequest request)
        {
            SelectiveImportResult result = new SelectiveImportResult();

            if (request == null || string.IsNullOrEmpty(request.BackupFilePath) || string.IsNullOrEmpty(request.TargetGeminiPath))
            {
                result.Success = false;
                result.Errors.Add("Invalid request parameters");
                return result;
            }

            if (!File.Exists(request.BackupFilePath))
            {
                result.Success = false;
                result.Errors.Add("Backup file not found: " + request.BackupFilePath);
                return result;
            }

            try
            {
                string antigravityDir = Path.Combine(request.TargetGeminiPath, "antigravity");
                string convDestDir = Path.Combine(antigravityDir, "conversations");
                string brainDestDir = Path.Combine(antigravityDir, "brain");
                string annotDestDir = Path.Combine(antigravityDir, "annotations");
                string localPbPath = Path.Combine(antigravityDir, "agyhub_summaries_proto.pb");

                Directory.CreateDirectory(convDestDir);
                Directory.CreateDirectory(brainDestDir);
                Directory.CreateDirectory(annotDestDir);

                HashSet<string> targetIdSet = new HashSet<string>(request.SelectedConversationIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                string home = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetEnvironmentVariable("HOME") ?? "";
                string sourceHome = "";

                using (FileStream fs = new FileStream(request.BackupFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    // 1. Read metadata.json for source_home
                    ZipArchiveEntry metaEntry = archive.GetEntry("metadata.json");
                    if (metaEntry != null)
                    {
                        using (StreamReader sr = new StreamReader(metaEntry.Open(), Encoding.UTF8))
                        {
                            string meta = sr.ReadToEnd();
                            Match m = Regex.Match(meta, "\"source_home\"\\s*:\\s*\"([^\"]*)\"");
                            if (m.Success) sourceHome = m.Groups[1].Value.Replace("\\\\", "\\");
                        }
                    }

                    // 2. Read Backup Proto
                    byte[] backupPbBytes = null;
                    for (int i = 0; i < archive.Entries.Count; i++)
                    {
                        if (archive.Entries[i].FullName.Replace('\\', '/').EndsWith("agyhub_summaries_proto.pb", StringComparison.OrdinalIgnoreCase))
                        {
                            using (Stream es = archive.Entries[i].Open())
                            using (MemoryStream ms = new MemoryStream())
                            {
                                es.CopyTo(ms);
                                backupPbBytes = ms.ToArray();
                            }
                            break;
                        }
                    }

                    // Prune targetIdSet for projects configured with ConflictResolutionStrategy.Skip
                    if (backupPbBytes != null && backupPbBytes.Length > 0 && request.ProjectStrategies != null && request.ProjectStrategies.Count > 0)
                    {
                        try
                        {
                            List<ConversationSummaryInfo> backupInfos = SessionMetadataExtractor.ExtractSummaries(backupPbBytes, null);
                            if (backupInfos != null)
                            {
                                for (int i = 0; i < backupInfos.Count; i++)
                                {
                                    ConversationSummaryInfo info = backupInfos[i];
                                    if (info != null && !string.IsNullOrEmpty(info.ConversationId))
                                    {
                                        bool isSkipped = false;
                                        if (!string.IsNullOrEmpty(info.ProjectName) &&
                                            request.ProjectStrategies.ContainsKey(info.ProjectName) &&
                                            request.ProjectStrategies[info.ProjectName] == ConflictResolutionStrategy.Skip)
                                        {
                                            isSkipped = true;
                                        }
                                        else if (!string.IsNullOrEmpty(info.FolderUri))
                                        {
                                            string folderProj = SessionMetadataExtractor.ExtractProjectNameFromUri(info.FolderUri);
                                            if (!string.IsNullOrEmpty(folderProj) &&
                                                request.ProjectStrategies.ContainsKey(folderProj) &&
                                                request.ProjectStrategies[folderProj] == ConflictResolutionStrategy.Skip)
                                            {
                                                isSkipped = true;
                                            }
                                        }

                                        if (isSkipped)
                                        {
                                            targetIdSet.Remove(info.ConversationId);
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                        }
                    }

                    // 3. Extract selected files
                    for (int i = 0; i < archive.Entries.Count; i++)
                    {
                        ZipArchiveEntry entry = archive.Entries[i];
                        string normName = entry.FullName.Replace('\\', '/');
                        if (normName.StartsWith("data/", StringComparison.OrdinalIgnoreCase))
                            normName = normName.Substring(5);

                        // Global Rules
                        if (request.ImportGlobalRules)
                        {
                            if (normName.Equals(".geminirules", StringComparison.OrdinalIgnoreCase) ||
                                normName.Equals("GEMINI.md", StringComparison.OrdinalIgnoreCase) ||
                                normName.Equals("AGENTS.md", StringComparison.OrdinalIgnoreCase) ||
                                normName.StartsWith("config/rules/", StringComparison.OrdinalIgnoreCase))
                            {
                                ExtractAndRemapTextEntry(entry, Path.Combine(request.TargetGeminiPath, normName.Replace('/', '\\')), sourceHome, home);
                            }
                        }

                        // Global Skills
                        if (request.ImportGlobalSkills)
                        {
                            if (normName.StartsWith("antigravity/builtin/skills/", StringComparison.OrdinalIgnoreCase) ||
                                normName.StartsWith("config/skills/", StringComparison.OrdinalIgnoreCase))
                            {
                                ExtractEntryDirect(entry, Path.Combine(request.TargetGeminiPath, normName.Replace('/', '\\')));
                            }
                        }

                        // Global MCP
                        if (request.ImportGlobalMcp)
                        {
                            if (normName.Equals("config/mcp_config.json", StringComparison.OrdinalIgnoreCase) ||
                                normName.Equals("config/config.json", StringComparison.OrdinalIgnoreCase) ||
                                normName.Equals("settings.json", StringComparison.OrdinalIgnoreCase) ||
                                normName.StartsWith("antigravity/mcp/", StringComparison.OrdinalIgnoreCase))
                            {
                                if (normName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                                    ExtractAndRemapTextEntry(entry, Path.Combine(request.TargetGeminiPath, normName.Replace('/', '\\')), sourceHome, home);
                                else
                                    ExtractEntryDirect(entry, Path.Combine(request.TargetGeminiPath, normName.Replace('/', '\\')));
                            }
                        }

                        // Project JSONs
                        if (normName.StartsWith("config/projects/", StringComparison.OrdinalIgnoreCase) && normName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        {
                            string pJsonText = ReadEntryText(entry);
                            string pName = ExtractProjectNameFromJson(pJsonText);
                            if (!string.IsNullOrEmpty(pName) && (request.SelectedProjectNames != null && request.SelectedProjectNames.Contains(pName)))
                            {
                                ConflictResolutionStrategy strat = ConflictResolutionStrategy.Merge;
                                if (request.ProjectStrategies != null && request.ProjectStrategies.ContainsKey(pName))
                                    strat = request.ProjectStrategies[pName];

                                if (strat == ConflictResolutionStrategy.Skip)
                                {
                                    continue;
                                }
                                else if (strat == ConflictResolutionStrategy.Rename)
                                {
                                    string newName = request.ProjectRenames != null && request.ProjectRenames.ContainsKey(pName) ? request.ProjectRenames[pName] : (pName + "_Imported");
                                    string newGuid = Guid.NewGuid().ToString("N");
                                    string newFile = Path.Combine(request.TargetGeminiPath, "config", "projects", newGuid + ".json");
                                    string remapped = RemapProjectJson(pJsonText, newName, sourceHome, home);
                                    Directory.CreateDirectory(Path.GetDirectoryName(newFile));
                                    File.WriteAllText(newFile, remapped, Encoding.UTF8);
                                    if (!result.ImportedProjectNames.Contains(newName))
                                        result.ImportedProjectNames.Add(newName);
                                }
                                else if (strat == ConflictResolutionStrategy.Overwrite)
                                {
                                    string destFile = Path.Combine(request.TargetGeminiPath, normName.Replace('/', '\\'));
                                    string remapped = RemapAllPathFormatsStatic(pJsonText, sourceHome, home);
                                    Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                                    File.WriteAllText(destFile, remapped, Encoding.UTF8);
                                    if (!result.ImportedProjectNames.Contains(pName))
                                        result.ImportedProjectNames.Add(pName);
                                }
                                else // Merge
                                {
                                    string destFile = Path.Combine(request.TargetGeminiPath, normName.Replace('/', '\\'));
                                    if (!File.Exists(destFile))
                                    {
                                        string remapped = RemapAllPathFormatsStatic(pJsonText, sourceHome, home);
                                        Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                                        File.WriteAllText(destFile, remapped, Encoding.UTF8);
                                    }
                                    if (!result.ImportedProjectNames.Contains(pName))
                                        result.ImportedProjectNames.Add(pName);
                                }
                            }
                        }

                        // Conversation DBs
                        if (normName.StartsWith("conversations/", StringComparison.OrdinalIgnoreCase) ||
                            normName.StartsWith("antigravity/conversations/", StringComparison.OrdinalIgnoreCase))
                        {
                            string fileName = Path.GetFileName(normName);
                            string uuid = ExtractUuidFromFileName(fileName);
                            if (!string.IsNullOrEmpty(uuid) && targetIdSet.Contains(uuid))
                            {
                                string destFile = Path.Combine(convDestDir, fileName);
                                entry.ExtractToFile(destFile, true);
                                if (fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!result.ImportedConversationIds.Contains(uuid))
                                        result.ImportedConversationIds.Add(uuid);
                                    result.ImportedConversationsCount++;
                                }
                            }
                        }

                        // Brain Directories
                        else if (normName.StartsWith("brain/", StringComparison.OrdinalIgnoreCase) ||
                                 normName.StartsWith("antigravity/brain/", StringComparison.OrdinalIgnoreCase))
                        {
                            string prefix = normName.StartsWith("antigravity/brain/", StringComparison.OrdinalIgnoreCase) ? "antigravity/brain/" : "brain/";
                            string subPath = normName.Substring(prefix.Length);
                            string[] parts = subPath.Split('/');
                            if (parts.Length > 0)
                            {
                                string brainId = parts[0];
                                if (targetIdSet.Contains(brainId))
                                {
                                    string destPath = Path.Combine(brainDestDir, subPath.Replace('/', '\\'));
                                    if (string.IsNullOrEmpty(Path.GetFileName(destPath)))
                                    {
                                        Directory.CreateDirectory(destPath);
                                    }
                                    else
                                    {
                                        Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                                        entry.ExtractToFile(destPath, true);
                                    }
                                }
                            }
                        }

                        // Annotations
                        else if (normName.StartsWith("annotations/", StringComparison.OrdinalIgnoreCase) ||
                                 normName.StartsWith("antigravity/annotations/", StringComparison.OrdinalIgnoreCase))
                        {
                            string fileName = Path.GetFileName(normName);
                            string uuid = ExtractUuidFromFileName(fileName);
                            if (!string.IsNullOrEmpty(uuid) && targetIdSet.Contains(uuid))
                            {
                                string destPath = Path.Combine(annotDestDir, fileName);
                                entry.ExtractToFile(destPath, true);
                            }
                        }
                    }

                    // Count imported brain dirs
                    HashSet<string> importedBrainSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < result.ImportedConversationIds.Count; i++)
                    {
                        string bDir = Path.Combine(brainDestDir, result.ImportedConversationIds[i]);
                        if (Directory.Exists(bDir))
                        {
                            importedBrainSet.Add(result.ImportedConversationIds[i]);
                        }
                    }
                    result.ImportedBrainDirsCount = importedBrainSet.Count;

                    // 4. Shard Merge into local agyhub_summaries_proto.pb
                    if (backupPbBytes != null && backupPbBytes.Length > 0 && targetIdSet.Count > 0)
                    {
                        byte[] localPbBytes = File.Exists(localPbPath) ? File.ReadAllBytes(localPbPath) : new byte[0];
                        byte[] mergedPbBytes = MergeProtoShards(localPbBytes, backupPbBytes, targetIdSet, request.ProjectRenames, request.ProjectStrategies);
                        IncrementalProtoShardMerger.AtomicSaveWithBackup(localPbPath, mergedPbBytes);
                        result.MergedProtoSummariesCount = targetIdSet.Count;
                    }
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add(ex.ToString());
            }

            return result;
        }

        private static void ExtractEntryDirect(ZipArchiveEntry entry, string destPath)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destPath);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destPath));
            entry.ExtractToFile(destPath, true);
        }

        private static void ExtractAndRemapTextEntry(ZipArchiveEntry entry, string destPath, string srcHome, string curHome)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destPath);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destPath));
            string content;
            using (StreamReader sr = new StreamReader(entry.Open(), Encoding.UTF8))
            {
                content = sr.ReadToEnd();
            }
            string remapped = RemapAllPathFormatsStatic(content, srcHome, curHome);
            File.WriteAllText(destPath, remapped, Encoding.UTF8);
        }

        private static string ReadEntryText(ZipArchiveEntry entry)
        {
            using (StreamReader sr = new StreamReader(entry.Open(), Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        private static string ExtractProjectNameFromJson(string json)
        {
            Match m = Regex.Match(json, "\"name\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : "";
        }

        private static string RemapProjectJson(string json, string newProjectName, string srcHome, string curHome)
        {
            string text = RemapAllPathFormatsStatic(json, srcHome, curHome);
            text = Regex.Replace(text, "\"name\"\\s*:\\s*\"[^\"]*\"", "\"name\": \"" + newProjectName + "\"");
            return text;
        }

        public static string RemapAllPathFormatsStatic(string text, string srcHome, string curHome)
        {
            if (string.IsNullOrEmpty(srcHome) || string.IsNullOrEmpty(curHome) || srcHome.Equals(curHome, StringComparison.OrdinalIgnoreCase))
                return text;

            text = text.Replace(srcHome, curHome);
            string srcSlash = srcHome.Replace('\\', '/');
            string curSlash = curHome.Replace('\\', '/');
            text = text.Replace(srcSlash, curSlash);
            string srcEsc = srcHome.Replace("\\", "\\\\");
            string curEsc = curHome.Replace("\\", "\\\\");
            text = text.Replace(srcEsc, curEsc);
            return text;
        }

        public static byte[] MergeProtoShards(
            byte[] localPbBytes,
            byte[] backupPbBytes,
            HashSet<string> targetConversationIds,
            Dictionary<string, string> projectRenames,
            Dictionary<string, ConflictResolutionStrategy> projectStrategies)
        {
            List<ProtoNode> localRoot = ProtoAstCodec.Decode(localPbBytes);
            List<ProtoNode> backupRoot = ProtoAstCodec.Decode(backupPbBytes);

            HashSet<string> existingLocalUuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < localRoot.Count; i++)
            {
                ConversationSummaryInfo info = SessionMetadataExtractor.ParseEntryNode(localRoot[i]);
                if (info != null && !string.IsNullOrEmpty(info.ConversationId))
                {
                    existingLocalUuids.Add(info.ConversationId);
                }
            }

            for (int i = 0; i < backupRoot.Count; i++)
            {
                ProtoNode backupEntry = backupRoot[i];
                ConversationSummaryInfo info = SessionMetadataExtractor.ParseEntryNode(backupEntry);
                if (info != null && !string.IsNullOrEmpty(info.ConversationId) && targetConversationIds.Contains(info.ConversationId))
                {
                    if (projectStrategies != null && projectStrategies.ContainsKey(info.ProjectName))
                    {
                        ConflictResolutionStrategy strat = projectStrategies[info.ProjectName];
                        if (strat == ConflictResolutionStrategy.Skip)
                        {
                            continue;
                        }
                    }

                    if (projectRenames != null && projectRenames.ContainsKey(info.ProjectName))
                    {
                        string newProjName = projectRenames[info.ProjectName];
                        RemapEntryProject(backupEntry, newProjName);
                    }

                    if (existingLocalUuids.Contains(info.ConversationId))
                    {
                        for (int k = localRoot.Count - 1; k >= 0; k--)
                        {
                            ConversationSummaryInfo localInfo = SessionMetadataExtractor.ParseEntryNode(localRoot[k]);
                            if (localInfo != null && string.Equals(localInfo.ConversationId, info.ConversationId, StringComparison.OrdinalIgnoreCase))
                            {
                                localRoot.RemoveAt(k);
                                break;
                            }
                        }
                    }

                    localRoot.Add(backupEntry);
                }
            }

            return ProtoAstCodec.Encode(localRoot);
        }

        private static void RemapEntryProject(ProtoNode entryNode, string newProjectName)
        {
            if (entryNode == null || string.IsNullOrEmpty(newProjectName)) return;
            RemapEntryProjectRecursive(entryNode, newProjectName);
        }

        private static void RemapEntryProjectRecursive(ProtoNode node, string newProjectName)
        {
            if (node == null) return;

            if (node.FieldNumber == 17)
            {
                if (node.Children != null && node.Children.Count > 0)
                {
                    ProtoNode nameNode = node.FindChild(1);
                    if (nameNode != null)
                    {
                        nameNode.SetString(newProjectName);
                    }
                    ProtoNode uriNode = node.FindChild(7);
                    if (uriNode != null)
                    {
                        string oldUri = uriNode.GetString();
                        if (!string.IsNullOrEmpty(oldUri))
                        {
                            string clean = oldUri.Replace('\\', '/').TrimEnd('/');
                            int lastSlash = clean.LastIndexOf('/');
                            if (lastSlash >= 0)
                            {
                                string newUri = clean.Substring(0, lastSlash + 1) + newProjectName;
                                uriNode.SetString(newUri);
                            }
                        }
                    }
                }
                else if (node.WireType == 2 && node.RawBytes != null && node.RawBytes.Length > 0)
                {
                    node.SetString(newProjectName);
                }
            }
            else if (node.FieldNumber == 9)
            {
                if (node.Children != null && node.Children.Count > 0)
                {
                    ProtoNode furi = node.FindChild(1);
                    if (furi != null)
                    {
                        string oldUri = furi.GetString();
                        if (!string.IsNullOrEmpty(oldUri))
                        {
                            string clean = oldUri.Replace('\\', '/').TrimEnd('/');
                            int lastSlash = clean.LastIndexOf('/');
                            if (lastSlash >= 0)
                            {
                                string newUri = clean.Substring(0, lastSlash + 1) + newProjectName;
                                furi.SetString(newUri);
                            }
                        }
                    }
                }
                else
                {
                    string oldUri = node.GetString();
                    if (!string.IsNullOrEmpty(oldUri))
                    {
                        string clean = oldUri.Replace('\\', '/').TrimEnd('/');
                        int lastSlash = clean.LastIndexOf('/');
                        if (lastSlash >= 0)
                        {
                            string newUri = clean.Substring(0, lastSlash + 1) + newProjectName;
                            node.SetString(newUri);
                        }
                    }
                }
            }

            if (node.Children != null && node.Children.Count > 0)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    RemapEntryProjectRecursive(node.Children[i], newProjectName);
                }
            }
        }

        private static string ExtractUuidFromFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            string raw = fileName;
            if (raw.EndsWith(".db-wal", StringComparison.OrdinalIgnoreCase))
                raw = raw.Substring(0, raw.Length - 7);
            else if (raw.EndsWith(".db-shm", StringComparison.OrdinalIgnoreCase))
                raw = raw.Substring(0, raw.Length - 7);
            else if (raw.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                raw = raw.Substring(0, raw.Length - 3);
            else if (raw.EndsWith(".pbtxt", StringComparison.OrdinalIgnoreCase))
                raw = raw.Substring(0, raw.Length - 6);

            return !string.IsNullOrEmpty(raw) ? raw : null;
        }
    }

    public class HighlightTracker
    {
        public HashSet<string> HighlightedConversationIds;
        public HashSet<string> HighlightedProjectNames;

        public HighlightTracker()
        {
            HighlightedConversationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HighlightedProjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public void MarkImported(List<string> convIds, List<string> projNames)
        {
            if (convIds != null)
            {
                for (int i = 0; i < convIds.Count; i++)
                    HighlightedConversationIds.Add(convIds[i]);
            }
            if (projNames != null)
            {
                for (int i = 0; i < projNames.Count; i++)
                    HighlightedProjectNames.Add(projNames[i]);
            }
        }

        public bool IsConversationHighlighted(string convId)
        {
            return convId != null && HighlightedConversationIds.Contains(convId);
        }

        public bool IsProjectHighlighted(string projName)
        {
            return projName != null && HighlightedProjectNames.Contains(projName);
        }
    }


    // ══════════════════════════════════════════════════════════════════════
    // MAIN FORM — 双栏可视化差异对比与细粒度选择性导入管理器 (V0.5)
    // ══════════════════════════════════════════════════════════════════════
    public class MainForm : Form
    {
        // ── 顶部操作栏与统计 ──
        private Button btnOpenBackup, btnRefreshLocal, btnImportSelected, btnFullBackup;
        private CheckBox chkHideTemp;
        private TextBox txtSearch;
        private Label lblStatsSummary;
        private TextBox txtGeminiPath;

        // ── 双栏树状视图 ──
        private SplitContainer splitMain;
        private SplitContainer splitTrees;
        private DoubleBufferedTreeView tvLocal;
        private DoubleBufferedTreeView tvBackup;
        private Label lblLocalHeader;
        private Label lblBackupHeader;
        private Button btnSelectAll, btnSelectNone, btnSelectNewOnly;

        // ── 底部工作区 (3 Tabs) ──
        private TabControl tabBottom;
        private RichTextBox rtbLog;
        private RichTextBox rtbInspector;

        // ── 配置浏览器 (Tab 2) ──
        private Button btnCatRules, btnCatSkills, btnCatMcp;
        private ListBox lstFiles;
        private RichTextBox rtbEditor;
        private Label lblCurrentFile;

        // ── 状态栏 ──
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatus, lblGeminiFound, lblConvCount, lblSelectionCount, lblHome;

        // ── 内存状态与缓存 ──
        private string _currentEditPath = "";
        private string _currentCategory = "rules";
        private BackgroundWorker _worker;
        private string _loadedBackupPath = "";
        private BackupArchiveIndex _backupIndex = null;
        private List<ConversationSummaryInfo> _localSummaries = new List<ConversationSummaryInfo>();
        private List<ProjectDiffNode> _currentDiffNodes = new List<ProjectDiffNode>();
        private HighlightTracker _highlightTracker = new HighlightTracker();
        private bool _isUpdatingChecks = false;

        private string HomePath
        {
            get { return Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetEnvironmentVariable("HOME") ?? ""; }
        }

        public MainForm()
        {
            BuildUI();
            DetectPaths();
        }

        // ══════════════════════════════════════════════════════════
        // UI 构建（双栏树状视图 + 底部三标签工作区）
        // ══════════════════════════════════════════════════════════
        private void BuildUI()
        {
            this.Text = "Antigravity Companion V0.5 - 双栏可视化差异对比与细粒度选择性导入管理器";
            this.Size = new Size(1100, 750);
            this.MinimumSize = new Size(950, 620);
            this.Font = new Font("Microsoft YaHei UI", 8.5f);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;

            // ══ 状态栏 ══
            statusStrip = new StatusStrip();
            lblStatus          = new ToolStripStatusLabel("就绪") { BorderSides = ToolStripStatusLabelBorderSides.Right };
            lblGeminiFound     = new ToolStripStatusLabel(".gemini: 未检测") { ForeColor = Color.DarkRed, BorderSides = ToolStripStatusLabelBorderSides.Right };
            lblConvCount       = new ToolStripStatusLabel("本地会话: -") { BorderSides = ToolStripStatusLabelBorderSides.Right };
            lblSelectionCount  = new ToolStripStatusLabel("待合流选中: 0 对话") { ForeColor = Color.DarkBlue, BorderSides = ToolStripStatusLabelBorderSides.Right };
            lblHome            = new ToolStripStatusLabel("主目录: " + HomePath) { Spring = true, TextAlign = ContentAlignment.MiddleRight };
            statusStrip.Items.AddRange(new ToolStripItem[] { lblStatus, lblGeminiFound, lblConvCount, lblSelectionCount, lblHome });
            this.Controls.Add(statusStrip);

            // ══ 根 TableLayoutPanel (顶部操作区 + 主工作区) ══
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.RowCount = 2;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.Margin = new Padding(0);
            root.Padding = new Padding(0);
            this.Controls.Add(root);

            // ── 顶部操作栏 ──
            Panel topPanel = BuildTopToolbar();
            root.Controls.Add(topPanel, 0, 0);

            // ── 主工作区 (上下 SplitContainer: 上双栏树 / 下多标签工作区) ──
            splitMain = new SplitContainer();
            splitMain.Dock = DockStyle.Fill;
            splitMain.Orientation = Orientation.Horizontal;
            splitMain.SplitterDistance = 420;
            splitMain.Margin = new Padding(0);
            root.Controls.Add(splitMain, 0, 1);

            // ── 上半部：双栏树状视图 (左右 SplitContainer) ──
            splitTrees = new SplitContainer();
            splitTrees.Dock = DockStyle.Fill;
            splitTrees.Orientation = Orientation.Vertical;
            splitTrees.SplitterDistance = 540;
            splitTrees.Margin = new Padding(0);
            splitMain.Panel1.Controls.Add(splitTrees);

            // 左侧：本地环境资产树 (tvLocal)
            BuildLocalTreePanel(splitTrees.Panel1);

            // 右侧：备份包数据树 (tvBackup)
            BuildBackupTreePanel(splitTrees.Panel2);

            // ── 下半部：三标签工作区 ──
            tabBottom = new TabControl();
            tabBottom.Dock = DockStyle.Fill;
            tabBottom.Padding = new Point(8, 4);

            TabPage tabLog = new TabPage("📋 实时操作日志 (Log Console)");
            BuildLogPanel(tabLog);
            tabBottom.TabPages.Add(tabLog);

            TabPage tabBrowser = new TabPage("🛠️ 规则与配置在线编辑器 (Config Browser)");
            BuildBrowserPanel(tabBrowser);
            tabBottom.TabPages.Add(tabBrowser);

            TabPage tabInspector = new TabPage("🔍 节点元数据与差异详情 (Metadata & Diff Inspector)");
            BuildInspectorPanel(tabInspector);
            tabBottom.TabPages.Add(tabInspector);

            splitMain.Panel2.Controls.Add(tabBottom);

            this.Load += (s, e) => {
                try {
                    splitTrees.SplitterDistance = splitTrees.Width / 2;
                } catch {}
            };
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.S)
            {
                e.SuppressKeyPress = true;
                BtnSaveFile_Click(this, EventArgs.Empty);
            }
        }

        // ══════════════════════════════════════════════════════════
        // 顶部操作栏与统计栏
        // ══════════════════════════════════════════════════════════
        private Panel BuildTopToolbar()
        {
            Panel pnl = new Panel();
            pnl.Dock = DockStyle.Fill;
            pnl.BackColor = Color.FromArgb(248, 249, 250);
            pnl.Padding = new Padding(6, 6, 6, 4);

            btnOpenBackup = MakeBtn("📂 打开备份包 (.agy)", 150, Color.FromArgb(155, 194, 230));
            btnOpenBackup.Location = new Point(6, 6);
            btnOpenBackup.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
            btnOpenBackup.Click += BtnOpenBackup_Click;

            btnRefreshLocal = MakeBtn("🔄 刷新本地", 90, Color.FromArgb(198, 224, 180));
            btnRefreshLocal.Location = new Point(162, 6);
            btnRefreshLocal.Click += (s, e) => DetectPaths();

            btnImportSelected = MakeBtn("📥 导入所选 (细粒度合流)", 165, Color.FromArgb(255, 217, 102));
            btnImportSelected.Location = new Point(258, 6);
            btnImportSelected.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
            btnImportSelected.Enabled = false;
            btnImportSelected.Click += BtnImportSelected_Click;

            btnFullBackup = MakeBtn("⚡ 一键全量备份", 120, Color.FromArgb(242, 220, 219));
            btnFullBackup.Location = new Point(429, 6);
            btnFullBackup.Click += BtnFullBackup_Click;

            chkHideTemp = new CheckBox();
            chkHideTemp.Text = "过滤临时工程与子代理会话 (推荐)";
            chkHideTemp.Checked = true;
            chkHideTemp.AutoSize = true;
            chkHideTemp.Location = new Point(560, 9);
            chkHideTemp.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular);
            chkHideTemp.CheckedChanged += ChkHideTemp_CheckedChanged;

            Label lblSearch = new Label() { Text = "🔍 搜索:", Location = new Point(880, 9), AutoSize = true, TextAlign = ContentAlignment.MiddleRight };
            txtSearch = new TextBox();
            txtSearch.Location = new Point(935, 6);
            txtSearch.Width = 140;
            txtSearch.Font = new Font("Microsoft YaHei UI", 8.5f);
            txtSearch.TextChanged += TxtSearch_TextChanged;

            txtGeminiPath = new TextBox() { Visible = false };

            lblStatsSummary = new Label();
            lblStatsSummary.Text = "本地: 正在扫描... | 备份包: 未加载 | 待合流选中: 0 个工程 (0 条会话)";
            lblStatsSummary.Location = new Point(8, 38);
            lblStatsSummary.Size = new Size(1060, 24);
            lblStatsSummary.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular);
            lblStatsSummary.ForeColor = Color.FromArgb(40, 50, 70);

            pnl.Controls.AddRange(new Control[] {
                btnOpenBackup, btnRefreshLocal, btnImportSelected, btnFullBackup,
                chkHideTemp, lblSearch, txtSearch, txtGeminiPath, lblStatsSummary
            });

            return pnl;
        }

        private void BuildLocalTreePanel(SplitterPanel parent)
        {
            TableLayoutPanel tbl = new TableLayoutPanel();
            tbl.Dock = DockStyle.Fill;
            tbl.RowCount = 2;
            tbl.ColumnCount = 1;
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            tbl.Padding = new Padding(2, 2, 2, 2);

            lblLocalHeader = new Label();
            lblLocalHeader.Text = "🏠 本地环境 (%USERPROFILE%\\.gemini)";
            lblLocalHeader.Dock = DockStyle.Fill;
            lblLocalHeader.BackColor = Color.FromArgb(235, 240, 245);
            lblLocalHeader.ForeColor = Color.FromArgb(30, 40, 60);
            lblLocalHeader.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            lblLocalHeader.TextAlign = ContentAlignment.MiddleLeft;
            lblLocalHeader.Padding = new Padding(4, 0, 0, 0);
            tbl.Controls.Add(lblLocalHeader, 0, 0);

            tvLocal = new DoubleBufferedTreeView();
            tvLocal.Dock = DockStyle.Fill;
            tvLocal.BorderStyle = BorderStyle.Fixed3D;
            tvLocal.Font = new Font("Microsoft YaHei UI", 8.5f);
            tvLocal.HideSelection = false;
            tvLocal.ShowLines = true;
            tvLocal.ShowPlusMinus = true;
            tvLocal.ShowRootLines = true;
            tvLocal.AfterSelect += (s, e) => ShowNodeMetadataInInspector(e.Node);
            tbl.Controls.Add(tvLocal, 0, 1);

            parent.Controls.Add(tbl);
        }

        private void BuildBackupTreePanel(SplitterPanel parent)
        {
            TableLayoutPanel tbl = new TableLayoutPanel();
            tbl.Dock = DockStyle.Fill;
            tbl.RowCount = 2;
            tbl.ColumnCount = 1;
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            tbl.Padding = new Padding(2, 2, 2, 2);

            Panel headerPanel = new Panel();
            headerPanel.Dock = DockStyle.Fill;
            headerPanel.BackColor = Color.FromArgb(235, 240, 245);

            lblBackupHeader = new Label();
            lblBackupHeader.Text = "📦 备份数据包 (.agy)";
            lblBackupHeader.Location = new Point(4, 4);
            lblBackupHeader.Size = new Size(180, 20);
            lblBackupHeader.ForeColor = Color.FromArgb(20, 30, 90);
            lblBackupHeader.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            lblBackupHeader.TextAlign = ContentAlignment.MiddleLeft;
            headerPanel.Controls.Add(lblBackupHeader);

            btnSelectAll = MakeBtn("全选", 56, SystemColors.Control);
            btnSelectAll.Location = new Point(200, 3);
            btnSelectAll.Click += BtnSelectAll_Click;

            btnSelectNone = MakeBtn("全不选", 56, SystemColors.Control);
            btnSelectNone.Location = new Point(260, 3);
            btnSelectNone.Click += BtnSelectNone_Click;

            btnSelectNewOnly = MakeBtn("★ 仅选新增", 80, Color.FromArgb(198, 224, 180));
            btnSelectNewOnly.Location = new Point(320, 3);
            btnSelectNewOnly.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
            btnSelectNewOnly.Click += BtnSelectNewOnly_Click;

            headerPanel.Controls.AddRange(new Control[] { btnSelectAll, btnSelectNone, btnSelectNewOnly });
            tbl.Controls.Add(headerPanel, 0, 0);

            tvBackup = new DoubleBufferedTreeView();
            tvBackup.Dock = DockStyle.Fill;
            tvBackup.BorderStyle = BorderStyle.Fixed3D;
            tvBackup.CheckBoxes = true;
            tvBackup.Font = new Font("Microsoft YaHei UI", 8.5f);
            tvBackup.HideSelection = false;
            tvBackup.ShowLines = true;
            tvBackup.ShowPlusMinus = true;
            tvBackup.ShowRootLines = true;
            tvBackup.AfterCheck += TvBackup_AfterCheck;
            tvBackup.AfterSelect += (s, e) => ShowNodeMetadataInInspector(e.Node);
            tbl.Controls.Add(tvBackup, 0, 1);

            parent.Controls.Add(tbl);
        }

        private void BuildLogPanel(TabPage tab)
        {
            TableLayoutPanel tbl = new TableLayoutPanel();
            tbl.Dock = DockStyle.Fill;
            tbl.RowCount = 2;
            tbl.ColumnCount = 1;
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tbl.Padding = new Padding(4);

            rtbLog = new RichTextBox();
            rtbLog.Dock = DockStyle.Fill;
            rtbLog.ReadOnly = true;
            rtbLog.BackColor = Color.White;
            rtbLog.Font = new Font("Courier New", 8.5f);
            rtbLog.ScrollBars = RichTextBoxScrollBars.Vertical;
            rtbLog.BorderStyle = BorderStyle.Fixed3D;
            tbl.Controls.Add(rtbLog, 0, 0);

            Panel btnRow = new Panel();
            btnRow.Dock = DockStyle.Fill;
            var btnClear = MakeBtn("清除日志", 72, Color.LightCoral); btnClear.Location = new Point(0, 3);
            btnClear.Click += (s, e) => rtbLog.Clear();
            var btnSaveLog = MakeBtn("保存日志", 72, Color.LightGreen); btnSaveLog.Location = new Point(76, 3);
            btnSaveLog.Click += BtnSaveLog_Click;
            btnRow.Controls.AddRange(new Control[] { btnClear, btnSaveLog });
            tbl.Controls.Add(btnRow, 0, 1);

            tab.Controls.Add(tbl);
        }

        private void BuildInspectorPanel(TabPage tab)
        {
            TableLayoutPanel tbl = new TableLayoutPanel();
            tbl.Dock = DockStyle.Fill;
            tbl.RowCount = 1;
            tbl.ColumnCount = 1;
            tbl.Padding = new Padding(4);

            rtbInspector = new RichTextBox();
            rtbInspector.Dock = DockStyle.Fill;
            rtbInspector.ReadOnly = true;
            rtbInspector.BackColor = Color.FromArgb(250, 252, 255);
            rtbInspector.Font = new Font("Courier New", 9f);
            rtbInspector.ScrollBars = RichTextBoxScrollBars.Both;
            rtbInspector.BorderStyle = BorderStyle.Fixed3D;
            rtbInspector.Text = "← 请在左侧本地树或右侧备份树中选择任意工程、会话或配置节点以查看详细元数据与差异信息。";
            tbl.Controls.Add(rtbInspector, 0, 0);

            tab.Controls.Add(tbl);
        }

        private void BuildBrowserPanel(TabPage tab)
        {
            TableLayoutPanel outer = new TableLayoutPanel();
            outer.Dock = DockStyle.Fill;
            outer.RowCount = 1;
            outer.ColumnCount = 2;
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240f));
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            outer.Padding = new Padding(2);

            TableLayoutPanel leftTbl = new TableLayoutPanel();
            leftTbl.Dock = DockStyle.Fill;
            leftTbl.RowCount = 2;
            leftTbl.ColumnCount = 1;
            leftTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            leftTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            leftTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            leftTbl.Padding = new Padding(2);

            TableLayoutPanel btnRow = new TableLayoutPanel();
            btnRow.Dock = DockStyle.Fill;
            btnRow.RowCount = 1; btnRow.ColumnCount = 3;
            btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
            btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
            btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24f));

            btnCatRules = MakeBtn("规则 Rules", 80, Color.FromArgb(255, 230, 153));
            btnCatRules.Dock = DockStyle.Fill; btnCatRules.Margin = new Padding(0, 0, 2, 0);
            btnCatRules.Click += (s, e) => { _currentCategory = "rules"; RefreshFileList(); };

            btnCatSkills = MakeBtn("技能 Skills", 80, Color.FromArgb(169, 208, 142));
            btnCatSkills.Dock = DockStyle.Fill; btnCatSkills.Margin = new Padding(0, 0, 2, 0);
            btnCatSkills.Click += (s, e) => { _currentCategory = "skills"; RefreshFileList(); };

            btnCatMcp = MakeBtn("MCP", 50, Color.FromArgb(155, 194, 230));
            btnCatMcp.Dock = DockStyle.Fill; btnCatMcp.Margin = new Padding(0);
            btnCatMcp.Click += (s, e) => { _currentCategory = "mcp"; RefreshFileList(); };

            btnRow.Controls.Add(btnCatRules, 0, 0);
            btnRow.Controls.Add(btnCatSkills, 1, 0);
            btnRow.Controls.Add(btnCatMcp, 2, 0);
            leftTbl.Controls.Add(btnRow, 0, 0);

            lstFiles = new ListBox();
            lstFiles.Dock = DockStyle.Fill;
            lstFiles.Font = new Font("Microsoft YaHei UI", 8.5f);
            lstFiles.BorderStyle = BorderStyle.Fixed3D;
            lstFiles.DoubleClick += (s, e) => OpenSelectedFile();
            lstFiles.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) OpenSelectedFile(); };
            leftTbl.Controls.Add(lstFiles, 0, 1);
            outer.Controls.Add(leftTbl, 0, 0);

            TableLayoutPanel rightTbl = new TableLayoutPanel();
            rightTbl.Dock = DockStyle.Fill;
            rightTbl.RowCount = 2;
            rightTbl.ColumnCount = 1;
            rightTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            rightTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            rightTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            rightTbl.Padding = new Padding(2, 2, 6, 6);

            lblCurrentFile = new Label();
            lblCurrentFile.Dock = DockStyle.Fill;
            lblCurrentFile.Text = "← 双击打开文件 (支持 Ctrl+S 保存)";
            lblCurrentFile.Font = new Font("Courier New", 8.5f, FontStyle.Bold);
            lblCurrentFile.ForeColor = Color.DarkBlue;
            lblCurrentFile.BackColor = Color.FromArgb(235, 244, 255);
            lblCurrentFile.TextAlign = ContentAlignment.MiddleLeft;
            lblCurrentFile.Padding = new Padding(6, 0, 0, 0);
            lblCurrentFile.BorderStyle = BorderStyle.FixedSingle;
            rightTbl.Controls.Add(lblCurrentFile, 0, 0);

            rtbEditor = new RichTextBox();
            rtbEditor.Dock = DockStyle.Fill;
            rtbEditor.Font = new Font("Courier New", 9f);
            rtbEditor.ScrollBars = RichTextBoxScrollBars.Both;
            rtbEditor.BackColor = Color.FromArgb(253, 253, 253);
            rtbEditor.BorderStyle = BorderStyle.Fixed3D;
            rightTbl.Controls.Add(rtbEditor, 0, 1);

            outer.Controls.Add(rightTbl, 1, 0);
            tab.Controls.Add(outer);
        }

        // ══════════════════════════════════════════════════════════
        // 本地环境扫描与树状视图渲染 (tvLocal)
        // ══════════════════════════════════════════════════════════
        private void DetectPaths()
        {
            string home = HomePath;
            if (string.IsNullOrEmpty(home)) { Log("错误: 无法获取用户主目录。", Color.Red); return; }

            string gemini = Path.Combine(home, ".gemini");
            txtGeminiPath.Text = gemini;

            if (Directory.Exists(gemini))
            {
                lblGeminiFound.Text = ".gemini: ✓ 已找到";
                lblGeminiFound.ForeColor = Color.DarkGreen;
                Log(".gemini 路径检测成功: " + gemini, Color.DarkGreen);

                ScanLocalEnvironment(gemini);
                RenderLocalTree();

                if (_backupIndex != null)
                {
                    _currentDiffNodes = VisualDiffEngine.ComputeDiff(_localSummaries, _backupIndex.Conversations);
                    RenderBackupTree();
                }

                _currentCategory = "rules";
                RefreshFileList();
                UpdateSelectionStatistics();
            }
            else
            {
                lblGeminiFound.Text = ".gemini: ✗ 未找到";
                lblGeminiFound.ForeColor = Color.DarkRed;
                Log("警告: 未找到 .gemini 目录: " + gemini, Color.DarkOrange);
            }
        }

        private void ScanLocalEnvironment(string gemini)
        {
            _localSummaries.Clear();

            // 1. 读取本地工程映射
            Dictionary<string, ProjectMetadata> projMap = new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
            string projConfigDir = Path.Combine(gemini, "config", "projects");
            if (Directory.Exists(projConfigDir))
            {
                foreach (string jf in Directory.GetFiles(projConfigDir, "*.json"))
                {
                    try
                    {
                        string content = File.ReadAllText(jf, Encoding.UTF8);
                        string name = ExtractJsonValue(content, "name");
                        string uri = ExtractJsonValue(content, "folderUri");
                        string id = Path.GetFileNameWithoutExtension(jf);
                        if (!string.IsNullOrEmpty(name))
                        {
                            projMap[id] = new ProjectMetadata { Id = id, Name = name, FolderUri = uri };
                        }
                    }
                    catch {}
                }
            }

            // 2. 读取本地 agyhub_summaries_proto.pb
            string pbPath = Path.Combine(gemini, "antigravity", "agyhub_summaries_proto.pb");
            if (File.Exists(pbPath))
            {
                try
                {
                    byte[] pbBytes = File.ReadAllBytes(pbPath);
                    _localSummaries = SessionMetadataExtractor.ExtractSummaries(pbBytes, projMap);
                }
                catch (Exception ex)
                {
                    Log("读取本地 summaries proto 异常: " + ex.Message, Color.DarkOrange);
                }
            }

            int mainCount = 0;
            for (int i = 0; i < _localSummaries.Count; i++)
            {
                if (!_localSummaries[i].IsSubagent) mainCount++;
            }

            lblConvCount.Text = "本地主会话: " + mainCount;
            Log("本地环境扫描完成：共发现 " + mainCount + " 条活跃主会话（已智能识别并过滤临时子代理与废弃记录）", Color.DarkBlue);
        }

        private void RenderLocalTree()
        {
            tvLocal.BeginUpdate();
            try
            {
                tvLocal.Nodes.Clear();
                string gemini = txtGeminiPath.Text.Trim();
                bool hideTemp = chkHideTemp.Checked;

                // ── 1. 全局配置 Root ──
                TreeNode rootConfig = new TreeNode("⚙️ 全局配置 (Global Config)");
                rootConfig.Tag = new DiffItemNodeTag { Category = ItemCategory.GlobalConfigRoot, DisplayName = "全局配置" };

                // 规则子项
                int rulesCount = 0;
                if (File.Exists(Path.Combine(gemini, ".geminirules"))) rulesCount++;
                if (File.Exists(Path.Combine(gemini, "GEMINI.md"))) rulesCount++;
                string rulesDir = Path.Combine(gemini, "config", "rules");
                if (Directory.Exists(rulesDir)) rulesCount += Directory.GetFiles(rulesDir, "*.md").Length;

                TreeNode nodeRules = new TreeNode(string.Format("📜 规则与规范 ({0} 项)", rulesCount));
                nodeRules.Tag = new DiffItemNodeTag { Category = ItemCategory.GlobalRules, DisplayName = "规则与规范", SourcePath = gemini };
                rootConfig.Nodes.Add(nodeRules);

                // 技能子项
                int skillsCount = 0;
                string bSkills = Path.Combine(gemini, "antigravity", "builtin", "skills");
                if (Directory.Exists(bSkills)) skillsCount += Directory.GetDirectories(bSkills).Length;
                string uSkills = Path.Combine(gemini, "config", "skills");
                if (Directory.Exists(uSkills)) skillsCount += Directory.GetDirectories(uSkills).Length;

                TreeNode nodeSkills = new TreeNode(string.Format("⚡ 内置与用户技能 ({0} 项)", skillsCount));
                nodeSkills.Tag = new DiffItemNodeTag { Category = ItemCategory.GlobalSkills, DisplayName = "内置与用户技能", SourcePath = uSkills };
                rootConfig.Nodes.Add(nodeSkills);

                // MCP 子项
                int mcpCount = 0;
                string mcpCfg = Path.Combine(gemini, "config", "mcp_config.json");
                if (File.Exists(mcpCfg)) mcpCount++;
                string mcpDir = Path.Combine(gemini, "antigravity", "mcp");
                if (Directory.Exists(mcpDir)) mcpCount += Directory.GetDirectories(mcpDir).Length;

                TreeNode nodeMcp = new TreeNode(string.Format("🔌 MCP 服务器与配置 ({0} 项)", mcpCount));
                nodeMcp.Tag = new DiffItemNodeTag { Category = ItemCategory.GlobalMcp, DisplayName = "MCP 服务器与配置", SourcePath = mcpDir };
                rootConfig.Nodes.Add(nodeMcp);

                tvLocal.Nodes.Add(rootConfig);

                // ── 2. 研发工程与会话 Root ──
                TreeNode rootProj = new TreeNode("📁 研发工程与会话 (Projects & Conversations)");
                rootProj.Tag = new DiffItemNodeTag { Category = ItemCategory.ProjectsRoot, DisplayName = "研发工程与会话" };

                // Group by project name
                Dictionary<string, List<ConversationSummaryInfo>> groups = new Dictionary<string, List<ConversationSummaryInfo>>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < _localSummaries.Count; i++)
                {
                    ConversationSummaryInfo s = _localSummaries[i];
                    string p = s.ProjectName ?? "Default Project";
                    if (!groups.ContainsKey(p)) groups[p] = new List<ConversationSummaryInfo>();
                    groups[p].Add(s);
                }

                foreach (KeyValuePair<string, List<ConversationSummaryInfo>> kvp in groups)
                {
                    string pName = kvp.Key;
                    List<ConversationSummaryInfo> list = kvp.Value;
                    bool isJunk = SmartProjectFilter.IsJunkOrEphemeral(pName);

                    List<ConversationSummaryInfo> visibleList = new List<ConversationSummaryInfo>();
                    for (int k = 0; k < list.Count; k++)
                    {
                        if (!hideTemp || !list[k].IsSubagent) visibleList.Add(list[k]);
                    }

                    if (hideTemp && isJunk && visibleList.Count == 0) continue;
                    if (hideTemp && visibleList.Count == 0) continue;

                    string pNodeText = string.Format("📦 {0} ({1} 条会话)", pName, visibleList.Count);
                    if (isJunk) pNodeText = "[临时] " + pNodeText;

                    TreeNode pNode = new TreeNode(pNodeText);
                    pNode.Tag = new DiffItemNodeTag { Category = ItemCategory.Project, DisplayName = pName, ProjectName = pName, IsTemporaryOrJunk = isJunk };

                    if (isJunk)
                    {
                        pNode.ForeColor = Color.Gray;
                        pNode.NodeFont = new Font(tvLocal.Font, FontStyle.Italic);
                    }
                    else if (_highlightTracker.IsProjectHighlighted(pName))
                    {
                        pNode.ForeColor = Color.DarkGreen;
                        pNode.NodeFont = new Font(tvLocal.Font, FontStyle.Bold);
                    }

                    for (int j = 0; j < visibleList.Count; j++)
                    {
                        ConversationSummaryInfo s = visibleList[j];
                        string timeStr = s.UpdatedTime > DateTime.MinValue ? s.UpdatedTime.ToString("MM-dd HH:mm") : "-";
                        string sNodeText = string.Format("💬 {0} [{1}]", s.DisplayTitle, timeStr);
                        if (s.IsSubagent) sNodeText = "[子任务] " + sNodeText;

                        TreeNode sNode = new TreeNode(sNodeText);
                        sNode.Tag = new DiffItemNodeTag {
                            Category = ItemCategory.Conversation,
                            Id = s.ConversationId,
                            DisplayName = s.DisplayTitle,
                            ProjectName = pName,
                            SummaryInfo = s,
                            Timestamp = s.UpdatedTime,
                            IsTemporaryOrJunk = s.IsSubagent
                        };

                        if (s.IsSubagent)
                        {
                            sNode.ForeColor = Color.Gray;
                            sNode.NodeFont = new Font(tvLocal.Font, FontStyle.Italic);
                        }
                        else if (_highlightTracker.IsConversationHighlighted(s.ConversationId))
                        {
                            sNode.ForeColor = Color.DarkGreen;
                            sNode.NodeFont = new Font(tvLocal.Font, FontStyle.Bold);
                        }

                        pNode.Nodes.Add(sNode);
                    }

                    rootProj.Nodes.Add(pNode);
                }

                tvLocal.Nodes.Add(rootProj);
                rootConfig.Expand();
                rootProj.Expand();
            }
            finally
            {
                tvLocal.EndUpdate();
            }
        }

        // ══════════════════════════════════════════════════════════
        // 打开并流式解析备份包 (.agy)
        // ══════════════════════════════════════════════════════════
        private void BtnOpenBackup_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Antigravity 备份包 (*.agy)|*.agy|ZIP 压缩包 (*.zip)|*.zip|所有文件 (*.*)|*.*";
            dlg.Title = "请选择 Antigravity 备份包 (.agy / .zip)";

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                LoadBackupArchive(dlg.FileName);
            }
        }

        private void LoadBackupArchive(string archivePath)
        {
            if (!File.Exists(archivePath)) { MessageBox.Show("备份包不存在: " + archivePath, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            _loadedBackupPath = archivePath;
            Log("正在通过流式只读管道解析备份包元数据: " + Path.GetFileName(archivePath) + "...", Color.DarkBlue);
            this.Cursor = Cursors.WaitCursor;

            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                _backupIndex = StreamingZipIndexReader.ReadIndex(archivePath);
                sw.Stop();

                _currentDiffNodes = VisualDiffEngine.ComputeDiff(_localSummaries, _backupIndex.Conversations);
                RenderBackupTree();

                lblBackupHeader.Text = "📦 备份数据包: " + Path.GetFileName(archivePath);
                btnImportSelected.Enabled = true;

                Log(string.Format("===== 备份包解析完成！共发现 {0} 个工程，{1} 条会话，耗时 {2}ms =====",
                    _backupIndex.Projects.Count, _backupIndex.Conversations.Count, sw.ElapsedMilliseconds), Color.DarkGreen);

                // 默认仅选中新增会话
                BtnSelectNewOnly_Click(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log("解析备份包失败: " + ex.Message, Color.Red);
                MessageBox.Show("解析备份包失败:\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        // ══════════════════════════════════════════════════════════
        // 备份包树状视图渲染 (tvBackup 带 CheckBox 与 Diff 徽标)
        // ══════════════════════════════════════════════════════════
        private void RenderBackupTree()
        {
            if (_backupIndex == null) return;

            tvBackup.BeginUpdate();
            _isUpdatingChecks = true;
            try
            {
                tvBackup.Nodes.Clear();
                bool hideTemp = chkHideTemp.Checked;

                // ── 1. 全局配置 Root ──
                TreeNode rootConfig = new TreeNode("⚙️ 备份全局配置 (Global Config)");
                rootConfig.Tag = new DiffItemNodeTag { Category = ItemCategory.GlobalConfigRoot, DisplayName = "备份全局配置" };

                TreeNode nodeRules = new TreeNode("📜 规则与规范 (.geminirules, GEMINI.md)");
                nodeRules.Tag = new DiffItemNodeTag { Category = ItemCategory.GlobalRules, DisplayName = "规则与规范", Status = DiffStatus.Aligned };
                rootConfig.Nodes.Add(nodeRules);

                TreeNode nodeSkills = new TreeNode("⚡ 内置与用户技能 (Skills)");
                nodeSkills.Tag = new DiffItemNodeTag { Category = ItemCategory.GlobalSkills, DisplayName = "内置与用户技能", Status = DiffStatus.Aligned };
                rootConfig.Nodes.Add(nodeSkills);

                TreeNode nodeMcp = new TreeNode("🔌 MCP 服务器与配置 (mcp_config.json)");
                nodeMcp.Tag = new DiffItemNodeTag { Category = ItemCategory.GlobalMcp, DisplayName = "MCP 服务器与配置", Status = DiffStatus.Aligned };
                rootConfig.Nodes.Add(nodeMcp);

                tvBackup.Nodes.Add(rootConfig);

                // ── 2. 研发工程与会话 Root ──
                TreeNode rootProj = new TreeNode("📁 备份研发工程与会话 (Projects & Conversations)");
                rootProj.Tag = new DiffItemNodeTag { Category = ItemCategory.ProjectsRoot, DisplayName = "备份研发工程与会话" };

                // Build local lookup map
                HashSet<string> localConvIdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < _localSummaries.Count; i++)
                {
                    if (!string.IsNullOrEmpty(_localSummaries[i].ConversationId))
                        localConvIdSet.Add(_localSummaries[i].ConversationId);
                }

                for (int i = 0; i < _currentDiffNodes.Count; i++)
                {
                    ProjectDiffNode pDiff = _currentDiffNodes[i];
                    if (pDiff.BackupConversations == null || pDiff.BackupConversations.Count == 0)
                        continue; // 本地独有，不在右侧备份树中列出

                    bool isJunk = pDiff.IsTemporaryOrJunk;
                    List<ConversationSummaryInfo> visibleConvs = new List<ConversationSummaryInfo>();
                    for (int k = 0; k < pDiff.BackupConversations.Count; k++)
                    {
                        if (!hideTemp || !pDiff.BackupConversations[k].IsSubagent)
                            visibleConvs.Add(pDiff.BackupConversations[k]);
                    }

                    if (hideTemp && isJunk && visibleConvs.Count == 0) continue;
                    if (hideTemp && visibleConvs.Count == 0) continue;

                    string badge = pDiff.GetStatusBadge();
                    string pText = string.Format("📦 {0} ({1} 条) {2}", pDiff.ProjectName, visibleConvs.Count, badge);
                    if (isJunk) pText = "[临时] " + pText;

                    TreeNode pNode = new TreeNode(pText);
                    pNode.Tag = new DiffItemNodeTag {
                        Category = ItemCategory.Project,
                        DisplayName = pDiff.ProjectName,
                        ProjectName = pDiff.ProjectName,
                        Status = pDiff.Status,
                        IsTemporaryOrJunk = isJunk,
                        ProjectNode = pDiff,
                        SourcePath = pDiff.BackupFolderUri,
                        TargetPath = pDiff.LocalFolderUri
                    };

                    ApplyDiffStyle(pNode, pDiff.Status, isJunk);

                    for (int j = 0; j < visibleConvs.Count; j++)
                    {
                        ConversationSummaryInfo s = visibleConvs[j];
                        bool existsLocal = !string.IsNullOrEmpty(s.ConversationId) && localConvIdSet.Contains(s.ConversationId);
                        DiffStatus sStatus = existsLocal ? DiffStatus.Aligned : DiffStatus.NewInBackup;

                        string sBadge = (sStatus == DiffStatus.NewInBackup) ? "[★ 新增]" : "[已对齐]";
                        string timeStr = s.UpdatedTime > DateTime.MinValue ? s.UpdatedTime.ToString("MM-dd HH:mm") : "-";
                        string sText = string.Format("💬 {0} [{1}] {2}", s.DisplayTitle, timeStr, sBadge);
                        if (s.IsSubagent) sText = "[子任务] " + sText;

                        TreeNode sNode = new TreeNode(sText);
                        sNode.Tag = new DiffItemNodeTag {
                            Category = ItemCategory.Conversation,
                            Id = s.ConversationId,
                            DisplayName = s.DisplayTitle,
                            ProjectName = pDiff.ProjectName,
                            Status = sStatus,
                            SummaryInfo = s,
                            Timestamp = s.UpdatedTime,
                            IsTemporaryOrJunk = s.IsSubagent
                        };

                        ApplyDiffStyle(sNode, sStatus, s.IsSubagent);
                        pNode.Nodes.Add(sNode);
                    }

                    rootProj.Nodes.Add(pNode);
                }

                tvBackup.Nodes.Add(rootProj);
                rootConfig.Expand();
                rootProj.Expand();
            }
            finally
            {
                _isUpdatingChecks = false;
                tvBackup.EndUpdate();
            }
            UpdateSelectionStatistics();
        }

        private void ApplyDiffStyle(TreeNode node, DiffStatus status, bool isJunk)
        {
            if (isJunk)
            {
                node.ForeColor = Color.Gray;
                node.NodeFont = new Font(tvBackup.Font, FontStyle.Italic);
                return;
            }

            switch (status)
            {
                case DiffStatus.NewInBackup:
                    node.ForeColor = Color.DarkGreen;
                    node.NodeFont = new Font(tvBackup.Font, FontStyle.Bold);
                    break;
                case DiffStatus.Conflicted:
                    node.ForeColor = Color.FromArgb(210, 105, 30); // Dark Orange
                    node.NodeFont = new Font(tvBackup.Font, FontStyle.Bold);
                    break;
                case DiffStatus.Aligned:
                    node.ForeColor = Color.DarkSlateGray;
                    node.NodeFont = new Font(tvBackup.Font, FontStyle.Regular);
                    break;
                case DiffStatus.LocalOnly:
                    node.ForeColor = Color.Navy;
                    node.NodeFont = new Font(tvBackup.Font, FontStyle.Regular);
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════
        // CheckBox 级联选择逻辑（防重入保护）
        // ══════════════════════════════════════════════════════════
        private void TvBackup_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_isUpdatingChecks) return;
            try
            {
                _isUpdatingChecks = true;

                // 1. 向下级联子节点
                SetChildNodesChecked(e.Node, e.Node.Checked);

                // 2. 向上级联父节点
                UpdateParentNodeChecked(e.Node);

                // 3. 更新选择统计
                UpdateSelectionStatistics();
            }
            finally
            {
                _isUpdatingChecks = false;
            }
        }

        private void SetChildNodesChecked(TreeNode node, bool isChecked)
        {
            for (int i = 0; i < node.Nodes.Count; i++)
            {
                TreeNode child = node.Nodes[i];
                child.Checked = isChecked;
                if (child.Nodes.Count > 0)
                {
                    SetChildNodesChecked(child, isChecked);
                }
            }
        }

        private void UpdateParentNodeChecked(TreeNode node)
        {
            TreeNode parent = node.Parent;
            if (parent == null) return;

            bool anyChecked = false;
            for (int i = 0; i < parent.Nodes.Count; i++)
            {
                if (parent.Nodes[i].Checked)
                {
                    anyChecked = true;
                    break;
                }
            }

            if (parent.Checked != anyChecked)
            {
                parent.Checked = anyChecked;
                UpdateParentNodeChecked(parent);
            }
        }

        // ══════════════════════════════════════════════════════════
        // 快速选择动作
        // ══════════════════════════════════════════════════════════
        private void BtnSelectAll_Click(object sender, EventArgs e)
        {
            _isUpdatingChecks = true;
            try
            {
                for (int i = 0; i < tvBackup.Nodes.Count; i++)
                {
                    SetNodeAndChildrenChecked(tvBackup.Nodes[i], true);
                }
            }
            finally
            {
                _isUpdatingChecks = false;
                UpdateSelectionStatistics();
            }
        }

        private void BtnSelectNone_Click(object sender, EventArgs e)
        {
            _isUpdatingChecks = true;
            try
            {
                for (int i = 0; i < tvBackup.Nodes.Count; i++)
                {
                    SetNodeAndChildrenChecked(tvBackup.Nodes[i], false);
                }
            }
            finally
            {
                _isUpdatingChecks = false;
                UpdateSelectionStatistics();
            }
        }

        private void BtnSelectNewOnly_Click(object sender, EventArgs e)
        {
            _isUpdatingChecks = true;
            try
            {
                // 先全部取消
                for (int i = 0; i < tvBackup.Nodes.Count; i++)
                {
                    SetNodeAndChildrenChecked(tvBackup.Nodes[i], false);
                }

                // 仅勾选状态为 NewInBackup 的节点
                CheckOnlyNewNodesRecursive(tvBackup.Nodes);

                // 向上刷新父级状态
                for (int i = 0; i < tvBackup.Nodes.Count; i++)
                {
                    RefreshParentChecksRecursive(tvBackup.Nodes[i]);
                }
            }
            finally
            {
                _isUpdatingChecks = false;
                UpdateSelectionStatistics();
            }
        }

        private void SetNodeAndChildrenChecked(TreeNode node, bool isChecked)
        {
            node.Checked = isChecked;
            for (int i = 0; i < node.Nodes.Count; i++)
            {
                SetNodeAndChildrenChecked(node.Nodes[i], isChecked);
            }
        }

        private void CheckOnlyNewNodesRecursive(TreeNodeCollection nodes)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode n = nodes[i];
                DiffItemNodeTag tag = n.Tag as DiffItemNodeTag;
                if (tag != null && tag.Status == DiffStatus.NewInBackup && !tag.IsTemporaryOrJunk)
                {
                    n.Checked = true;
                }
                if (n.Nodes.Count > 0)
                {
                    CheckOnlyNewNodesRecursive(n.Nodes);
                }
            }
        }

        private void RefreshParentChecksRecursive(TreeNode node)
        {
            for (int i = 0; i < node.Nodes.Count; i++)
            {
                RefreshParentChecksRecursive(node.Nodes[i]);
            }
            if (node.Nodes.Count > 0)
            {
                bool any = false;
                for (int i = 0; i < node.Nodes.Count; i++)
                {
                    if (node.Nodes[i].Checked) { any = true; break; }
                }
                node.Checked = any;
            }
        }

        // ══════════════════════════════════════════════════════════
        // 统计更新
        // ══════════════════════════════════════════════════════════
        private void UpdateSelectionStatistics()
        {
            int selConvCount = 0;
            HashSet<string> selProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            CountSelectedRecursive(tvBackup.Nodes, ref selConvCount, selProjects);

            string bkpInfo = _backupIndex != null ? string.Format("{0} 个工程 ({1} 条会话)", _backupIndex.Projects.Count, _backupIndex.Conversations.Count) : "未加载";
            string locInfo = string.Format("{0} 条会话", _localSummaries.Count);

            lblStatsSummary.Text = string.Format("本地环境: {0} | 备份包: {1} | 待合流选中: {2} 个工程 ({3} 条会话)",
                locInfo, bkpInfo, selProjects.Count, selConvCount);

            lblSelectionCount.Text = string.Format("待合流选中: {0} 工程 ({1} 对话)", selProjects.Count, selConvCount);
        }

        private void CountSelectedRecursive(TreeNodeCollection nodes, ref int convCount, HashSet<string> projects)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode n = nodes[i];
                if (n.Checked)
                {
                    DiffItemNodeTag tag = n.Tag as DiffItemNodeTag;
                    if (tag != null)
                    {
                        if (tag.Category == ItemCategory.Conversation)
                        {
                            convCount++;
                            if (!string.IsNullOrEmpty(tag.ProjectName))
                                projects.Add(tag.ProjectName);
                        }
                        else if (tag.Category == ItemCategory.Project && !string.IsNullOrEmpty(tag.DisplayName))
                        {
                            projects.Add(tag.DisplayName);
                        }
                    }
                }
                if (n.Nodes.Count > 0)
                {
                    CountSelectedRecursive(n.Nodes, ref convCount, projects);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        // 过滤器切换与搜索
        // ══════════════════════════════════════════════════════════
        private void ChkHideTemp_CheckedChanged(object sender, EventArgs e)
        {
            RenderLocalTree();
            if (_backupIndex != null)
            {
                RenderBackupTree();
            }
            UpdateSelectionStatistics();
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            string query = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(query)) return;

            SearchAndHighlight(tvLocal.Nodes, query);
            SearchAndHighlight(tvBackup.Nodes, query);
        }

        private bool SearchAndHighlight(TreeNodeCollection nodes, string query)
        {
            bool matchAny = false;
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode n = nodes[i];
                bool selfMatch = n.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                bool childMatch = SearchAndHighlight(n.Nodes, query);

                if (selfMatch || childMatch)
                {
                    n.Expand();
                    matchAny = true;
                }
            }
            return matchAny;
        }

        // ══════════════════════════════════════════════════════════
        // 节点元数据与差异 Inspector 显示
        // ══════════════════════════════════════════════════════════
        private void ShowNodeMetadataInInspector(TreeNode node)
        {
            if (node == null) return;
            DiffItemNodeTag tag = node.Tag as DiffItemNodeTag;
            if (tag == null) return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine("  ANTIGRAVITY COMPANION V0.5 — 节点元数据与差异详情检查器 (INSPECTOR)");
            sb.AppendLine("================================================================================");
            sb.AppendLine();
            sb.AppendLine("【节点类型】: " + tag.Category);
            sb.AppendLine("【节点名称】: " + tag.DisplayName);
            sb.AppendLine("【所属工程】: " + (tag.ProjectName ?? "-"));
            sb.AppendLine("【差异状态】: " + tag.Status + " (" + GetStatusBadgeDescription(tag.Status) + ")");
            sb.AppendLine("【临时工程】: " + (tag.IsTemporaryOrJunk ? "是 (subagent/recovered/outside)" : "否 (用户核心资产)"));

            if (tag.SummaryInfo != null)
            {
                ConversationSummaryInfo s = tag.SummaryInfo;
                sb.AppendLine();
                sb.AppendLine("── 会话元数据链详情 ──");
                sb.AppendLine("  • 会话 UUID       : " + s.ConversationId);
                sb.AppendLine("  • 最终显示标题     : " + s.DisplayTitle);
                sb.AppendLine("  • 用户自定义重命名 : " + (s.HasCustomTitle ? ("✓ [Field 15] " + s.CustomTitle) : "✗ (无自定义重命名)"));
                sb.AppendLine("  • 自动生成摘要     : " + (string.IsNullOrEmpty(s.AutoSummaryTitle) ? "-" : s.AutoSummaryTitle));
                sb.AppendLine("  • 工作区 URI      : " + (s.FolderUri ?? "-"));
                sb.AppendLine("  • 消息总步数       : " + s.StepCount);
                sb.AppendLine("  • 创建时间         : " + (s.CreatedTime > DateTime.MinValue ? s.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss") : "-"));
                sb.AppendLine("  • 最近更新时间     : " + (s.UpdatedTime > DateTime.MinValue ? s.UpdatedTime.ToString("yyyy-MM-dd HH:mm:ss") : "-"));
            }

            if (tag.ProjectNode != null)
            {
                ProjectDiffNode p = tag.ProjectNode;
                sb.AppendLine();
                sb.AppendLine("── 工程路径与冲突对比 ──");
                sb.AppendLine("  • 本地工作区路径   : " + (p.LocalFolderUri ?? "(本地不存在)"));
                sb.AppendLine("  • 备份原始路径     : " + (p.BackupFolderUri ?? "(备份不存在)"));
                sb.AppendLine("  • 本地会话总数     : " + p.LocalConversations.Count + " 条");
                sb.AppendLine("  • 备份会话总数     : " + p.BackupConversations.Count + " 条");

                if (p.Status == DiffStatus.Conflicted)
                {
                    sb.AppendLine();
                    sb.AppendLine("  ⚠️ 冲突警告: 检测到工程重名但物理工作区路径不一致！");
                    sb.AppendLine("  建议策略: 导入时将弹出向导，可选择【合并到本地(Merge)】或【重命名新工程(Rename)】。");
                }
            }

            rtbInspector.Text = sb.ToString();
        }

        private string GetStatusBadgeDescription(DiffStatus s)
        {
            switch (s)
            {
                case DiffStatus.NewInBackup: return "【★ 新增】仅存在于备份包中";
                case DiffStatus.Aligned: return "【已对齐】两端均存在且路径一致";
                case DiffStatus.Conflicted: return "【重名/路径差异】工程同名但工作区路径冲突";
                case DiffStatus.LocalOnly: return "【本地独有】仅存在于当前本地环境";
                default: return "-";
            }
        }

        // ══════════════════════════════════════════════════════════
        // 细粒度选择性合流导入执行 (Selective Cascading Import)
        // ══════════════════════════════════════════════════════════
        private void BtnImportSelected_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_loadedBackupPath) || !File.Exists(_loadedBackupPath))
            {
                MessageBox.Show("请先打开有效的 Antigravity 备份包 (.agy)！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 1. 收集勾选的项目、会话与全局配置
            List<string> selectedConvIds = new List<string>();
            List<string> selectedProjectNames = new List<string>();
            bool importRules = false;
            bool importSkills = false;
            bool importMcp = false;

            CollectSelectedItems(tvBackup.Nodes, selectedConvIds, selectedProjectNames, ref importRules, ref importSkills, ref importMcp);

            if (selectedConvIds.Count == 0 && !importRules && !importSkills && !importMcp)
            {
                MessageBox.Show("请先在右侧备份树中勾选需要合流导入的会话、工程或全局配置！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 2. 冲突检测与向导弹窗
            ConflictResolutionPlan plan = new ConflictResolutionPlan();
            bool applyToAllRemaining = false;
            ConflictResolutionStrategy lastStrategy = ConflictResolutionStrategy.Merge;

            for (int i = 0; i < _currentDiffNodes.Count; i++)
            {
                ProjectDiffNode pDiff = _currentDiffNodes[i];
                if (pDiff.Status == DiffStatus.Conflicted && selectedProjectNames.Contains(pDiff.ProjectName))
                {
                    if (applyToAllRemaining)
                    {
                        plan.Strategies[pDiff.ProjectName] = lastStrategy;
                        if (lastStrategy == ConflictResolutionStrategy.Rename)
                        {
                            plan.RenamedProjects[pDiff.ProjectName] = pDiff.ProjectName + "_Imported";
                        }
                    }
                    else
                    {
                        DateTime? localLastTime = null;
                        for (int k = 0; k < pDiff.LocalConversations.Count; k++)
                        {
                            if (!localLastTime.HasValue || pDiff.LocalConversations[k].UpdatedTime > localLastTime.Value)
                                localLastTime = pDiff.LocalConversations[k].UpdatedTime;
                        }

                        int newCount = 0;
                        HashSet<string> localIdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        for (int k = 0; k < pDiff.LocalConversations.Count; k++)
                            localIdSet.Add(pDiff.LocalConversations[k].ConversationId);
                        for (int k = 0; k < pDiff.BackupConversations.Count; k++)
                            if (!localIdSet.Contains(pDiff.BackupConversations[k].ConversationId)) newCount++;

                        using (ConflictResolutionDialog dlg = new ConflictResolutionDialog(
                            pDiff.ProjectName,
                            pDiff.LocalFolderUri,
                            pDiff.LocalConversations.Count,
                            localLastTime,
                            pDiff.BackupFolderUri,
                            pDiff.BackupConversations.Count,
                            newCount))
                        {
                            if (dlg.ShowDialog(this) != DialogResult.OK)
                            {
                                Log("用户取消了合流导入操作。", Color.DarkOrange);
                                return;
                            }

                            plan.Strategies[pDiff.ProjectName] = dlg.Strategy;
                            if (dlg.Strategy == ConflictResolutionStrategy.Rename)
                            {
                                plan.RenamedProjects[pDiff.ProjectName] = dlg.RenamedName;
                            }

                            if (dlg.ApplyToAll)
                            {
                                applyToAllRemaining = true;
                                lastStrategy = dlg.Strategy;
                            }
                        }
                    }
                }
            }

            // 3. 确认导入
            string confirmMsg = string.Format("即将执行细粒度选择性合流导入：\n\n" +
                                              "• 选定会话数量: {0} 条\n" +
                                              "• 涉及工程数量: {1} 个\n" +
                                              "• 全局规则/规范: {2}\n" +
                                              "• 内置与用户技能: {3}\n" +
                                              "• MCP 服务器配置: {4}\n\n" +
                                              "选定会话的 .db 数据库、brain 记忆脑区与摘要索引分片将被精确写入本地，本地其他未选会话丝毫不受影响。\n\n" +
                                              "是否立即开始合流导入？",
                                              selectedConvIds.Count, selectedProjectNames.Count,
                                              importRules ? "✓ 导入" : "✗ 跳过",
                                              importSkills ? "✓ 导入" : "✗ 跳过",
                                              importMcp ? "✓ 导入" : "✗ 跳过");

            if (MessageBox.Show(this, confirmMsg, "确认合流导入", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            // 4. 关闭 Antigravity 进程
            if (!EnsureAntigravityClosed(true)) return;

            ProgressForm progressDlg = new ProgressForm("正在执行细粒度选择性合流导入...", "正在提取会话与脑区，请稍候...");
            SelectiveImportRequest req = new SelectiveImportRequest
            {
                BackupFilePath = _loadedBackupPath,
                TargetGeminiPath = txtGeminiPath.Text.Trim(),
                SelectedConversationIds = selectedConvIds,
                SelectedProjectNames = selectedProjectNames,
                ProjectStrategies = plan.Strategies,
                ProjectRenames = plan.RenamedProjects,
                ImportGlobalRules = importRules,
                ImportGlobalSkills = importSkills,
                ImportGlobalMcp = importMcp
            };

            _worker = new BackgroundWorker();
            _worker.DoWork += (ws, we) =>
            {
                progressDlg.UpdateMsg("正在提取选定会话 .db 文件与关联 brain 记忆脑区...");
                SelectiveImportResult res = SelectiveCascadingImporter.ExecuteImport(req);
                we.Result = res;

                progressDlg.UpdateMsg("正在校验工程工作区并建立占位目录...");
                RemapAndVerifyProjects(req.TargetGeminiPath, "", HomePath);

                progressDlg.UpdateMsg("正在修复文件控制权限...");
                FixPermissions(req.TargetGeminiPath);
            };

            _worker.RunWorkerCompleted += (ws, we) =>
            {
                progressDlg.Close();
                if (we.Error != null)
                {
                    Log("合流导入失败: " + we.Error.Message, Color.Red);
                    MessageBox.Show("合流导入失败:\n" + we.Error.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    SelectiveImportResult res = we.Result as SelectiveImportResult;
                    if (res != null && res.Success)
                    {
                        // 标记已导入的高亮会话
                        _highlightTracker.MarkImported(res.ImportedConversationIds, res.ImportedProjectNames);

                        Log(string.Format("===== 细粒度选择性合流导入成功！共导入 {0} 条会话，{1} 个脑区目录，{2} 条索引分片 =====",
                            res.ImportedConversationsCount, res.ImportedBrainDirsCount, res.MergedProtoSummariesCount), Color.DarkGreen);

                        // 自动刷新本地树与 Diff 树
                        DetectPaths();

                        string okMsg = string.Format("细粒度选择性合流导入成功！\n\n" +
                                                     "• 成功导入会话: {0} 条\n" +
                                                     "• 关联脑区目录: {1} 个\n" +
                                                     "• 更新索引分片: {2} 条\n\n" +
                                                     "刚导入的工程与会话已在左侧本地树中以【加粗绿字】高亮显示。\n" +
                                                     "本地原有未选会话完好无损，请重启 Antigravity 即可查看！",
                                                     res.ImportedConversationsCount, res.ImportedBrainDirsCount, res.MergedProtoSummariesCount);

                        MessageBox.Show(this, okMsg, "合流完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        string errs = (res != null && res.Errors.Count > 0) ? string.Join("\n", res.Errors.ToArray()) : "未知错误";
                        Log("合流导入失败: " + errs, Color.Red);
                        MessageBox.Show("合流导入失败:\n" + errs, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };

            _worker.RunWorkerAsync();
            progressDlg.ShowDialog(this);
        }

        private void CollectSelectedItems(TreeNodeCollection nodes, List<string> convIds, List<string> projNames, ref bool importRules, ref bool importSkills, ref bool importMcp)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode n = nodes[i];
                if (n.Checked)
                {
                    DiffItemNodeTag tag = n.Tag as DiffItemNodeTag;
                    if (tag != null)
                    {
                        if (tag.Category == ItemCategory.Conversation && !string.IsNullOrEmpty(tag.Id))
                        {
                            if (!convIds.Contains(tag.Id)) convIds.Add(tag.Id);
                            if (!string.IsNullOrEmpty(tag.ProjectName) && !projNames.Contains(tag.ProjectName))
                                projNames.Add(tag.ProjectName);
                        }
                        else if (tag.Category == ItemCategory.Project && !string.IsNullOrEmpty(tag.DisplayName))
                        {
                            if (!projNames.Contains(tag.DisplayName)) projNames.Add(tag.DisplayName);
                        }
                        else if (tag.Category == ItemCategory.GlobalRules)
                        {
                            importRules = true;
                        }
                        else if (tag.Category == ItemCategory.GlobalSkills)
                        {
                            importSkills = true;
                        }
                        else if (tag.Category == ItemCategory.GlobalMcp)
                        {
                            importMcp = true;
                        }
                    }
                }
                if (n.Nodes.Count > 0)
                {
                    CollectSelectedItems(n.Nodes, convIds, projNames, ref importRules, ref importSkills, ref importMcp);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        // 一键全量备份 (V0.4 传统导出保留)
        // ══════════════════════════════════════════════════════════
        private void BtnFullBackup_Click(object sender, EventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "AGY备份包(*.agy)|*.agy|ZIP压缩包(*.zip)|*.zip|所有文件(*.*)|*.*";
            dlg.FileName = "antigravity_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".agy";
            dlg.Title = "请选择全量备份包保存路径";

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                CreateFullBackup(dlg.FileName);
            }
        }

        private void CreateFullBackup(string dest)
        {
            string gemini = txtGeminiPath.Text.Trim();
            if (!Directory.Exists(gemini)) { MessageBox.Show("未找到 .gemini 目录: " + gemini, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            ProgressForm progressDlg = new ProgressForm("正在打包全量导出备份...", "正在执行高速流式压缩，请稍候...");

            _worker = new BackgroundWorker();
            _worker.DoWork += (ws, we) =>
            {
                string dDir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(dDir) && !Directory.Exists(dDir)) Directory.CreateDirectory(dDir);
                if (File.Exists(dest)) File.Delete(dest);

                int n = 0;
                using (FileStream fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
                using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    // 1. 写入 metadata.json
                    ZipArchiveEntry metaEntry = archive.CreateEntry("metadata.json", CompressionLevel.Fastest);
                    using (StreamWriter sw = new StreamWriter(metaEntry.Open(), Encoding.UTF8))
                    {
                        string meta = "{\r\n  \"source_home\": \"" + HomePath.Replace("\\", "\\\\") + "\",\r\n" +
                                      "  \"source_user\": \"" + Environment.UserName + "\",\r\n" +
                                      "  \"backup_time\": \"" + DateTime.Now.ToString("o") + "\",\r\n" +
                                      "  \"version\": \"0.5\"\r\n}";
                        sw.Write(meta);
                    }
                    n++;

                    // 2. 对话、脑区与会话索引 (精准定向归档有效会话，彻底排除已删除的孤儿数据库)
                    progressDlg.UpdateMsg("正在归档有效会话与脑区索引...");
                    HashSet<string> validCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < _localSummaries.Count; i++)
                    {
                        var s = _localSummaries[i];
                        if (!string.IsNullOrEmpty(s.ConversationId))
                        {
                            if (chkHideTemp.Checked && s.IsSubagent) continue;
                            validCids.Add(s.ConversationId);
                        }
                    }

                    string convDir = Path.Combine(gemini, "antigravity\\conversations");
                    string brainDir = Path.Combine(gemini, "antigravity\\brain");

                    foreach (string cid in validCids)
                    {
                        string dbFile = Path.Combine(convDir, cid + ".db");
                        if (File.Exists(dbFile))
                        {
                            n += AddFileToArchive(archive, dbFile, "data/antigravity/conversations/" + cid + ".db");
                        }
                        string bDir = Path.Combine(brainDir, cid);
                        if (Directory.Exists(bDir))
                        {
                            n += AddDirToArchive(archive, bDir, "data/antigravity/brain/" + cid);
                        }
                    }

                    n += AddDirToArchive(archive, Path.Combine(gemini, "antigravity\\annotations"), "data/antigravity/annotations");
                    n += AddFileToArchive(archive, Path.Combine(gemini, "antigravity\\agyhub_summaries_proto.pb"), "data/antigravity/agyhub_summaries_proto.pb");
                    n += AddFileToArchive(archive, Path.Combine(gemini, "antigravity\\antigravity_state.pbtxt"), "data/antigravity/antigravity_state.pbtxt");
                    n += AddFileToArchive(archive, Path.Combine(gemini, "antigravity\\installation_id"), "data/antigravity/installation_id");
                    n += AddDirToArchive(archive, Path.Combine(gemini, "antigravity\\knowledge"), "data/antigravity/knowledge");

                    // 3. 规则与内置技能
                    progressDlg.UpdateMsg("正在归档规则与规范...");
                    n += AddFileToArchive(archive, Path.Combine(gemini, ".geminirules"), "data/.geminirules");
                    n += AddFileToArchive(archive, Path.Combine(gemini, "GEMINI.md"), "data/GEMINI.md");
                    n += AddFileToArchive(archive, Path.Combine(gemini, "AGENTS.md"), "data/AGENTS.md");
                    n += AddDirToArchive(archive, Path.Combine(gemini, "antigravity\\builtin\\skills"), "data/antigravity/builtin/skills");

                    // 4. 完整 config 目录
                    progressDlg.UpdateMsg("正在归档项目定义、自定义技能与全局配置...");
                    n += AddDirToArchive(archive, Path.Combine(gemini, "config"), "data/config");

                    // 5. MCP 与客户端配置
                    n += AddDirToArchive(archive, Path.Combine(gemini, "antigravity\\mcp"), "data/antigravity/mcp");
                    n += AddFileToArchive(archive, Path.Combine(gemini, "settings.json"), "data/settings.json");
                    n += AddDirToArchive(archive, Path.Combine(gemini, "antigravity-ide"), "data/antigravity-ide");
                    n += AddDirToArchive(archive, Path.Combine(gemini, "antigravity-cli"), "data/antigravity-cli");
                }

                we.Result = n;
            };

            _worker.RunWorkerCompleted += (ws, we) =>
            {
                progressDlg.Close();
                if (we.Error != null)
                {
                    Log("备份失败: " + we.Error.Message, Color.Red);
                    MessageBox.Show("备份失败:\n" + we.Error.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    Log("===== 全量备份完成！共打包 " + we.Result + " 个文件 → " + dest + " =====", Color.DarkGreen);
                    MessageBox.Show("备份导出成功！\n共打包 " + we.Result + " 个文件\n路径: " + dest, "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            _worker.RunWorkerAsync();
            progressDlg.ShowDialog(this);
        }

        // ══════════════════════════════════════════════════════════
        // 检查并关闭 Antigravity 进程
        // ══════════════════════════════════════════════════════════
        private bool EnsureAntigravityClosed(bool promptUser)
        {
            string[] targetProcessNames = new string[] {
                "Antigravity", "Antigravity IDE", "antigravity", "antigravity-hans-windows-amd64", "agy-node"
            };

            List<Process> running = new List<Process>();
            foreach (string name in targetProcessNames)
            {
                try
                {
                    Process[] procs = Process.GetProcessesByName(name);
                    if (procs != null && procs.Length > 0)
                    {
                        running.AddRange(procs);
                    }
                }
                catch {}
            }

            if (running.Count > 0)
            {
                if (promptUser)
                {
                    string msg = "检测到 Antigravity 正在运行（发现 " + running.Count + " 个相关进程）！\n\n" +
                                 "为防止 SQLite 数据库锁定占用以及退出时缓存覆盖还原文件，必须先退出 Antigravity。\n\n" +
                                 "点击【确定】由本工具自动关闭 Antigravity 进程并继续。\n" +
                                 "点击【取消】中止操作。";
                    DialogResult dr = MessageBox.Show(this, msg, "安全提示", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                    if (dr != DialogResult.OK)
                    {
                        Log("操作已取消：Antigravity 正在运行中。", Color.DarkOrange);
                        return false;
                    }
                }

                Log("正在终止 Antigravity 进程以释放文件占用...", Color.DarkBlue);
                foreach (Process p in running)
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(3000);
                        Log("已终止进程: " + p.ProcessName + " (PID: " + p.Id + ")", Color.DarkGreen);
                    }
                    catch (Exception ex)
                    {
                        Log("终止进程 " + p.ProcessName + " 提示: " + ex.Message, Color.DarkGray);
                    }
                }
                System.Threading.Thread.Sleep(500);
            }
            return true;
        }

        // ══════════════════════════════════════════════════════════
        // 配置浏览器：刷新文件列表
        // ══════════════════════════════════════════════════════════
        public class ScanRoot
        {
            public string Path;
            public string ProjectName;
            public ScanRoot(string path, string name) { Path = path; ProjectName = name; }
        }

        private void RefreshFileList()
        {
            lstFiles.Items.Clear();
            rtbEditor.Clear();
            lblCurrentFile.Text = "← 双击打开文件 (支持 Ctrl+S 保存)";
            _currentEditPath = "";

            btnCatRules.FlatStyle = (_currentCategory == "rules") ? FlatStyle.Popup : FlatStyle.System;
            btnCatSkills.FlatStyle = (_currentCategory == "skills") ? FlatStyle.Popup : FlatStyle.System;
            btnCatMcp.FlatStyle = (_currentCategory == "mcp") ? FlatStyle.Popup : FlatStyle.System;

            string gemini = txtGeminiPath.Text.Trim();
            if (!Directory.Exists(gemini))
            {
                lstFiles.Items.Add(new BrowserItem("（.gemini 目录未找到）", ""));
                return;
            }

            int found = 0;
            List<ScanRoot> scanRoots = new List<ScanRoot>();
            scanRoots.Add(new ScanRoot(gemini, "全局"));

            string projectsConfigDir = Path.Combine(gemini, "config", "projects");
            if (Directory.Exists(projectsConfigDir))
            {
                foreach (string jsonFile in Directory.GetFiles(projectsConfigDir, "*.json"))
                {
                    try
                    {
                        string content = File.ReadAllText(jsonFile);
                        string projName = ExtractJsonValue(content, "name");
                        string folderUri = ExtractJsonValue(content, "folderUri");

                        if (!string.IsNullOrEmpty(folderUri))
                        {
                            string decodedPath = Uri.UnescapeDataString(folderUri.Replace("file:///", ""));
                            if (decodedPath.StartsWith("/") && decodedPath.Length > 2 && decodedPath[2] == ':')
                                decodedPath = decodedPath.Substring(1);
                            decodedPath = decodedPath.Replace('/', '\\');

                            if (Directory.Exists(decodedPath))
                            {
                                if (string.IsNullOrEmpty(projName)) projName = Path.GetFileName(decodedPath);
                                scanRoots.Add(new ScanRoot(decodedPath, projName));
                            }
                        }
                    }
                    catch {}
                }
            }

            if (_currentCategory == "rules")
            {
                foreach (ScanRoot root in scanRoots)
                {
                    bool isGlobal = (root.Path == gemini);
                    if (isGlobal)
                    {
                        string p = Path.Combine(root.Path, ".geminirules");
                        if (File.Exists(p)) { lstFiles.Items.Add(new BrowserItem("[全局] .geminirules", p)); found++; }
                        foreach (string f in Directory.GetFiles(root.Path, "*.geminirules"))
                            if (Path.GetFileName(f) != ".geminirules") { lstFiles.Items.Add(new BrowserItem("[全局] " + Path.GetFileName(f), f)); found++; }

                        string agentsMd = Path.Combine(root.Path, "AGENTS.md");
                        if (File.Exists(agentsMd)) { lstFiles.Items.Add(new BrowserItem("[全局] AGENTS.md", agentsMd)); found++; }
                        string geminiMd = Path.Combine(root.Path, "GEMINI.md");
                        if (File.Exists(geminiMd)) { lstFiles.Items.Add(new BrowserItem("[全局] GEMINI.md", geminiMd)); found++; }

                        string configRulesDir = Path.Combine(root.Path, "config", "rules");
                        if (Directory.Exists(configRulesDir))
                        {
                            foreach (string rf in Directory.GetFiles(configRulesDir, "*.md"))
                            {
                                lstFiles.Items.Add(new BrowserItem("[全局] rules/" + Path.GetFileName(rf), rf));
                                found++;
                            }
                        }
                    }
                    else
                    {
                        string projName = root.ProjectName;
                        string[] possiblePaths = new string[] {
                            Path.Combine(root.Path, ".geminirules"),
                            Path.Combine(root.Path, ".agents", ".geminirules")
                        };
                        foreach (string path in possiblePaths)
                        {
                            if (File.Exists(path))
                            {
                                lstFiles.Items.Add(new BrowserItem("[" + projName + "] " + Path.GetFileName(path), path));
                                found++;
                            }
                        }
                        string projAgentsMd = Path.Combine(root.Path, "AGENTS.md");
                        if (File.Exists(projAgentsMd)) { lstFiles.Items.Add(new BrowserItem("[" + projName + "] AGENTS.md", projAgentsMd)); found++; }
                        string projGeminiMd = Path.Combine(root.Path, "GEMINI.md");
                        if (File.Exists(projGeminiMd)) { lstFiles.Items.Add(new BrowserItem("[" + projName + "] GEMINI.md", projGeminiMd)); found++; }
                    }
                }
                if (found == 0) lstFiles.Items.Add(new BrowserItem("（未找到规则文件）", ""));
            }
            else if (_currentCategory == "skills")
            {
                foreach (ScanRoot root in scanRoots)
                {
                    bool isGlobal = (root.Path == gemini);
                    if (isGlobal)
                    {
                        string skillsDir = Path.Combine(root.Path, "antigravity", "builtin", "skills");
                        if (Directory.Exists(skillsDir))
                        {
                            foreach (string dir in Directory.GetDirectories(skillsDir))
                            {
                                string md = Path.Combine(dir, "SKILL.md");
                                if (File.Exists(md)) { lstFiles.Items.Add(new BrowserItem("[内置] ▣ " + Path.GetFileName(dir), md)); found++; }
                            }
                        }
                        string userSkillsDir = Path.Combine(root.Path, "config", "skills");
                        if (Directory.Exists(userSkillsDir))
                        {
                            foreach (string dir in Directory.GetDirectories(userSkillsDir))
                            {
                                string md = Path.Combine(dir, "SKILL.md");
                                if (File.Exists(md)) { lstFiles.Items.Add(new BrowserItem("[用户] ▣ " + Path.GetFileName(dir), md)); found++; }
                            }
                        }
                    }
                    else
                    {
                        string projName = root.ProjectName;
                        string skillsDir = Path.Combine(root.Path, ".agents", "skills");
                        if (Directory.Exists(skillsDir))
                        {
                            foreach (string dir in Directory.GetDirectories(skillsDir))
                            {
                                string md = Path.Combine(dir, "SKILL.md");
                                if (File.Exists(md)) { lstFiles.Items.Add(new BrowserItem("[" + projName + "] ▣ " + Path.GetFileName(dir), md)); found++; }
                            }
                        }
                    }
                }
                if (found == 0) lstFiles.Items.Add(new BrowserItem("（未找到技能目录）", ""));
            }
            else if (_currentCategory == "mcp")
            {
                string mcpJson = Path.Combine(gemini, "config", "mcp_config.json");
                if (File.Exists(mcpJson)) { lstFiles.Items.Add(new BrowserItem("★ mcp_config.json（全局主配置）", mcpJson)); found++; }

                string mcpDir = Path.Combine(gemini, "antigravity", "mcp");
                if (Directory.Exists(mcpDir))
                {
                    foreach (string srvDir in Directory.GetDirectories(mcpDir))
                    {
                        string sn = Path.GetFileName(srvDir);
                        foreach (string f in Directory.GetFiles(srvDir))
                        { lstFiles.Items.Add(new BrowserItem("  [" + sn + "] " + Path.GetFileName(f), f)); found++; }
                    }
                }
                string cfg = Path.Combine(gemini, "config", "config.json");
                if (File.Exists(cfg)) { lstFiles.Items.Add(new BrowserItem("  config.json", cfg)); found++; }
                string settingsJson = Path.Combine(gemini, "settings.json");
                if (File.Exists(settingsJson)) { lstFiles.Items.Add(new BrowserItem("  settings.json", settingsJson)); found++; }
                if (found == 0) lstFiles.Items.Add(new BrowserItem("（未找到 MCP 配置）", ""));
            }

            lblStatus.Text = "已扫描 [" + _currentCategory + "]，共 " + found + " 项";
        }

        private void OpenSelectedFile()
        {
            if (lstFiles.SelectedItem == null) return;
            BrowserItem item = lstFiles.SelectedItem as BrowserItem;
            if (item == null || string.IsNullOrEmpty(item.FilePath)) return;
            if (!File.Exists(item.FilePath)) { Log("文件不存在: " + item.FilePath, Color.DarkOrange); return; }
            try
            {
                rtbEditor.Text = File.ReadAllText(item.FilePath, Encoding.UTF8);
                _currentEditPath = item.FilePath;
                lblCurrentFile.Text = item.FilePath;
                Log("已加载: " + item.FilePath, Color.DarkGreen);
            }
            catch (Exception ex) { Log("加载失败: " + ex.Message, Color.Red); }
        }

        private void BtnSaveFile_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentEditPath)) return;
            try
            {
                File.WriteAllText(_currentEditPath, rtbEditor.Text, Encoding.UTF8);
                Log("已保存: " + _currentEditPath, Color.DarkGreen);
                MessageBox.Show("配置保存成功！", "保存", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("保存失败: " + ex.Message, Color.Red);
                MessageBox.Show("保存失败:\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ══════════════════════════════════════════════════════════
        // 压缩流辅助
        // ══════════════════════════════════════════════════════════
        private int AddFileToArchive(ZipArchive archive, string fullPath, string entryPath)
        {
            if (!File.Exists(fullPath)) return 0;
            try
            {
                ZipArchiveEntry entry = archive.CreateEntry(entryPath.Replace('\\', '/'), CompressionLevel.Fastest);
                using (FileStream srcStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (Stream entryStream = entry.Open())
                {
                    srcStream.CopyTo(entryStream);
                }
                return 1;
            }
            catch (Exception ex)
            {
                Log("  [跳过文件] " + entryPath + " (" + ex.Message + ")", Color.DarkOrange);
                return 0;
            }
        }

        private int AddDirToArchive(ZipArchive archive, string dirPath, string baseEntryPath)
        {
            if (!Directory.Exists(dirPath)) return 0;
            int n = 0;
            try
            {
                DirectoryInfo dirInfo = new DirectoryInfo(dirPath);
                foreach (FileInfo fi in dirInfo.GetFiles())
                {
                    if (fi.Extension.ToLower() == ".lock") continue;
                    string entryName = (string.IsNullOrEmpty(baseEntryPath) ? "" : baseEntryPath + "/") + fi.Name;
                    n += AddFileToArchive(archive, fi.FullName, entryName);
                }

                foreach (DirectoryInfo subDir in dirInfo.GetDirectories())
                {
                    if (subDir.Name.Equals("worktrees", StringComparison.OrdinalIgnoreCase)) continue;
                    if (subDir.Name.Equals("crashes", StringComparison.OrdinalIgnoreCase)) continue;
                    if (subDir.Name.Equals("DawnGraphiteCache", StringComparison.OrdinalIgnoreCase)) continue;
                    if (subDir.Name.Equals("DawnWebGPUCache", StringComparison.OrdinalIgnoreCase)) continue;
                    if (subDir.Name.Equals("GPUCache", StringComparison.OrdinalIgnoreCase)) continue;
                    if (subDir.Name.Equals("Code Cache", StringComparison.OrdinalIgnoreCase)) continue;

                    string subEntryPath = (string.IsNullOrEmpty(baseEntryPath) ? "" : baseEntryPath + "/") + subDir.Name;
                    n += AddDirToArchive(archive, subDir.FullName, subEntryPath);
                }
            }
            catch (Exception ex)
            {
                Log("  [跳过目录] " + baseEntryPath + " (" + ex.Message + ")", Color.DarkOrange);
            }
            return n;
        }

        // ══════════════════════════════════════════════════════════
        // 工程工作区校验、智能重定向与占位创建
        // ══════════════════════════════════════════════════════════
        private void RemapAndVerifyProjects(string targetGemini, string srcHome, string curHome)
        {
            string projectsDir = Path.Combine(targetGemini, "config", "projects");
            if (!Directory.Exists(projectsDir)) return;

            foreach (string jsonFile in Directory.GetFiles(projectsDir, "*.json"))
            {
                if (Path.GetFileName(jsonFile).Equals("outside-of-project.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    string content = File.ReadAllText(jsonFile, Encoding.UTF8);
                    string projName = ExtractJsonValue(content, "name");
                    if (string.IsNullOrEmpty(projName)) projName = Path.GetFileNameWithoutExtension(jsonFile);

                    string folderUri = ExtractJsonValue(content, "folderUri");
                    if (!string.IsNullOrEmpty(folderUri))
                    {
                        string decodedPath = Uri.UnescapeDataString(folderUri.Replace("file:///", ""));
                        if (decodedPath.StartsWith("/") && decodedPath.Length > 2 && decodedPath[2] == ':')
                            decodedPath = decodedPath.Substring(1);
                        decodedPath = decodedPath.Replace('/', '\\');

                        if (Directory.Exists(decodedPath))
                        {
                            Log("  [工程就绪] " + projName + " -> " + decodedPath, Color.DarkGreen);
                        }
                        else
                        {
                            string dirName = Path.GetFileName(decodedPath.TrimEnd('\\', '/'));
                            string[] candidateDirs = new string[] {
                                Path.Combine(curHome, "Desktop", dirName),
                                Path.Combine(curHome, "Documents", dirName),
                                Path.Combine(curHome, "Desktop", "Tools", dirName),
                                Path.Combine(curHome, "Projects", dirName),
                                Path.Combine("D:\\", dirName),
                                Path.Combine("D:\\Projects", dirName),
                                Path.Combine("D:\\Code", dirName),
                                Path.Combine("E:\\", dirName)
                            };

                            string foundRealDir = null;
                            foreach (string cDir in candidateDirs)
                            {
                                if (Directory.Exists(cDir))
                                {
                                    foundRealDir = cDir;
                                    break;
                                }
                            }

                            if (!string.IsNullOrEmpty(foundRealDir))
                            {
                                string newUri = "file:///" + foundRealDir.Replace('\\', '/');
                                content = Regex.Replace(content, "\"folderUri\"\\s*:\\s*\"[^\"]*\"", "\"folderUri\": \"" + newUri + "\"");
                                File.WriteAllText(jsonFile, content, Encoding.UTF8);
                                Log("  [工程智能重定向] " + projName + ": 原路径未找到，已重定向至新位置 -> " + foundRealDir, Color.DarkBlue);
                            }
                            else
                            {
                                try
                                {
                                    Directory.CreateDirectory(decodedPath);
                                    string notePath = Path.Combine(decodedPath, ".antigravity_workspace_placeholder");
                                    string note = "# Antigravity Workspace Placeholder\r\n" +
                                                  "此目录由 Antigravity Companion 自动创建占位。\r\n" +
                                                  "作用：确保项目历史对话在 Antigravity 侧边栏中完整展示。\r\n" +
                                                  "请将真实工程代码复制或 Git Clone 至此目录即可开始正常编码与工具调用。\r\n";
                                    File.WriteAllText(notePath, note, Encoding.UTF8);
                                    Log("  [工程目录占位创建] " + projName + ": " + decodedPath + " (已创建占位目录，确保历史对话可见)", Color.DarkCyan);
                                }
                                catch (Exception cex)
                                {
                                    Log("  [工程占位提示] 创建 " + decodedPath + " 失败: " + cex.Message, Color.DarkOrange);
                                }
                            }
                        }
                    }
                }
                catch (Exception pex)
                {
                    Log("  [工程检查异常] " + Path.GetFileName(jsonFile) + ": " + pex.Message, Color.DarkOrange);
                }
            }
        }

        private void FixPermissions(string path)
        {
            try
            {
                var p1 = new Process(); p1.StartInfo = new ProcessStartInfo("takeown", "/f \"" + path + "\" /r /d y") { CreateNoWindow = true, UseShellExecute = false };
                p1.Start(); p1.WaitForExit(8000);
                var p2 = new Process(); p2.StartInfo = new ProcessStartInfo("icacls", "\"" + path + "\" /grant \"" + Environment.UserName + ":(OI)(CI)F\" /t") { CreateNoWindow = true, UseShellExecute = false };
                p2.Start(); p2.WaitForExit(8000);
                Log("权限修复完成。", Color.DarkGreen);
            }
            catch (Exception ex) { Log("权限修复: " + ex.Message, Color.DarkOrange); }
        }

        // ══════════════════════════════════════════════════════════
        // 日志与界面辅助
        // ══════════════════════════════════════════════════════════
        private void Log(string msg, Color? color = null)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string, Color?>(Log), msg, color); return; }
            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.SelectionColor = color ?? Color.Black;
            rtbLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + "\r\n");
            rtbLog.ScrollToCaret();
            lblStatus.Text = msg.Length > 80 ? msg.Substring(0, 80) + "..." : msg;
        }

        private Button MakeBtn(string text, int width, Color backColor)
        {
            return new Button() { Text = text, Width = width, Height = 26, BackColor = backColor, FlatStyle = FlatStyle.System };
        }

        private void BtnSaveLog_Click(object sender, EventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog() { Filter = "日志(*.log)|*.log|文本(*.txt)|*.txt", FileName = "agt_log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log" };
            if (dlg.ShowDialog() == DialogResult.OK) { File.WriteAllText(dlg.FileName, rtbLog.Text, Encoding.UTF8); Log("日志已保存: " + dlg.FileName, Color.DarkGreen); }
        }

        private string ExtractJsonValue(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "";
            Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value.Replace("\\\\", "\\") : "";
        }
    }

    // ── 进度条窗口（模态）──
    public class ProgressForm : Form
    {
        private ProgressBar prgBar;
        private Label lblMsg;
        public ProgressForm(string title, string msg)
        {
            this.Text = title;
            this.Size = new Size(350, 130);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ControlBox = false;

            lblMsg = new Label() { Text = msg, Left = 20, Top = 15, Width = 300, Height = 30, TextAlign = ContentAlignment.MiddleLeft };
            prgBar = new ProgressBar() { Left = 20, Top = 50, Width = 295, Height = 20, Style = ProgressBarStyle.Marquee };

            this.Controls.Add(lblMsg);
            this.Controls.Add(prgBar);
        }

        public void UpdateMsg(string msg)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(UpdateMsg), msg); return; }
            lblMsg.Text = msg;
        }
    }

// DATA & STORAGE ENGINE (Milestone 1 — Pure Native C# 5 / .NET 4.0/4.5+)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Protobuf AST 抽象语法树节点 (纯 C# 5 兼容)
    /// </summary>
    public class ProtoNode
    {
        public int FieldNumber;
        public int WireType; // 0: Varint, 1: Fixed64, 2: Length-Delimited, 5: Fixed32
        public ulong VarintValue;
        public ulong Fixed64Value;
        public uint Fixed32Value;
        public byte[] RawBytes;
        public List<ProtoNode> Children;

        public byte[] LengthDelimitedValue
        {
            get { return RawBytes; }
            set { RawBytes = value; }
        }

        public ProtoNode()
        {
            FieldNumber = 0;
            WireType = 0;
            VarintValue = 0;
            Fixed64Value = 0;
            Fixed32Value = 0;
            RawBytes = null;
            Children = null;
        }

        public ProtoNode(int fieldNum, int wireType)
        {
            FieldNumber = fieldNum;
            WireType = wireType;
            VarintValue = 0;
            Fixed64Value = 0;
            Fixed32Value = 0;
            RawBytes = null;
            Children = null;
        }

        public bool HasChildren
        {
            get { return Children != null && Children.Count > 0; }
        }

        public string GetString()
        {
            if (RawBytes == null || RawBytes.Length == 0) return string.Empty;
            try { return Encoding.UTF8.GetString(RawBytes); }
            catch { return string.Empty; }
        }

        public void SetString(string val)
        {
            WireType = 2;
            Children = null;
            RawBytes = val != null ? Encoding.UTF8.GetBytes(val) : new byte[0];
        }

        public long GetInt64()
        {
            return (long)VarintValue;
        }

        public void SetInt64(long val)
        {
            WireType = 0;
            VarintValue = (ulong)val;
        }

        public int GetInt32()
        {
            return (int)VarintValue;
        }

        public void SetInt32(int val)
        {
            WireType = 0;
            VarintValue = (ulong)(long)val;
        }

        public ProtoNode GetChild(int fieldNum)
        {
            if (Children == null) return null;
            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i].FieldNumber == fieldNum) return Children[i];
            }
            return null;
        }

        public ProtoNode FindChild(int fieldNum)
        {
            return GetChild(fieldNum);
        }

        public List<ProtoNode> GetChildren(int fieldNum)
        {
            List<ProtoNode> list = new List<ProtoNode>();
            if (Children == null) return list;
            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i].FieldNumber == fieldNum) list.Add(Children[i]);
            }
            return list;
        }

        public List<ProtoNode> FindAllChildren(int fieldNum)
        {
            return GetChildren(fieldNum);
        }

        public string GetStringChild(int fieldNum, string defaultValue)
        {
            ProtoNode node = GetChild(fieldNum);
            if (node != null) return node.GetString();
            return defaultValue;
        }

        public long GetInt64Child(int fieldNum, long defaultValue)
        {
            ProtoNode node = GetChild(fieldNum);
            if (node != null) return node.GetInt64();
            return defaultValue;
        }

        public int GetInt32Child(int fieldNum, int defaultValue)
        {
            ProtoNode node = GetChild(fieldNum);
            if (node != null) return node.GetInt32();
            return defaultValue;
        }

        public void SetChildString(int fieldNum, string val)
        {
            if (Children == null) Children = new List<ProtoNode>();
            ProtoNode existing = GetChild(fieldNum);
            if (existing != null)
            {
                existing.SetString(val);
            }
            else
            {
                ProtoNode newNode = new ProtoNode(fieldNum, 2);
                newNode.SetString(val);
                Children.Add(newNode);
            }
        }

        public ProtoNode Clone()
        {
            ProtoNode copy = new ProtoNode();
            copy.FieldNumber = this.FieldNumber;
            copy.WireType = this.WireType;
            copy.VarintValue = this.VarintValue;
            copy.Fixed64Value = this.Fixed64Value;
            copy.Fixed32Value = this.Fixed32Value;
            if (this.RawBytes != null)
            {
                copy.RawBytes = new byte[this.RawBytes.Length];
                Buffer.BlockCopy(this.RawBytes, 0, copy.RawBytes, 0, this.RawBytes.Length);
            }
            if (this.Children != null)
            {
                copy.Children = new List<ProtoNode>(this.Children.Count);
                for (int i = 0; i < this.Children.Count; i++)
                {
                    copy.Children.Add(this.Children[i].Clone());
                }
            }
            return copy;
        }

        public void TraverseStrings(Func<string, string> stringTransformer)
        {
            if (stringTransformer == null) return;
            if (Children != null && Children.Count > 0)
            {
                for (int i = 0; i < Children.Count; i++)
                {
                    Children[i].TraverseStrings(stringTransformer);
                }
            }
            else if (WireType == 2 && RawBytes != null && RawBytes.Length > 0)
            {
                try
                {
                    string original = Encoding.UTF8.GetString(RawBytes);
                    string modified = stringTransformer(original);
                    if (modified != null && modified != original)
                    {
                        RawBytes = Encoding.UTF8.GetBytes(modified);
                    }
                }
                catch
                {
                }
            }
        }

        public void WriteTo(Stream stream)
        {
            ulong key = (((ulong)(uint)FieldNumber) << 3) | (ulong)(uint)(WireType & 0x07);
            WriteVarint(stream, key);

            if (WireType == 0) // Varint
            {
                WriteVarint(stream, VarintValue);
            }
            else if (WireType == 1) // Fixed64
            {
                byte[] b = BitConverter.GetBytes(Fixed64Value);
                stream.Write(b, 0, 8);
            }
            else if (WireType == 2) // Length-Delimited
            {
                byte[] payload;
                if (Children != null && Children.Count > 0)
                {
                    payload = SerializeList(Children);
                }
                else
                {
                    payload = RawBytes ?? new byte[0];
                }
                WriteVarint(stream, (ulong)payload.Length);
                if (payload.Length > 0)
                {
                    stream.Write(payload, 0, payload.Length);
                }
            }
            else if (WireType == 5) // Fixed32
            {
                byte[] b = BitConverter.GetBytes(Fixed32Value);
                stream.Write(b, 0, 4);
            }
        }

        public static List<ProtoNode> ParseList(byte[] data, int offset, int length)
        {
            return ProtoCodec.Decode(data, offset, length);
        }

        public static byte[] SerializeList(List<ProtoNode> list)
        {
            return ProtoCodec.Encode(list);
        }

        public static void WriteVarint(Stream stream, ulong value)
        {
            ProtoCodec.WriteVarint(stream, value);
        }

        public static int ReadVarint(byte[] data, int offset, int end, out ulong value)
        {
            return ProtoCodec.ReadVarint(data, offset, end, out value);
        }

        public static bool LooksLikeSubmessage(byte[] data, int offset, int length)
        {
            List<ProtoNode> unused;
            return ProtoCodec.TryDecodeSubmessage(data, offset, length, out unused);
        }
    }

    /// <summary>
    /// 纯原生 Protobuf Wire Format 编解码引擎 (纯 C# 5 兼容)
    /// </summary>
    public static class ProtoCodec
    {
        public static int ReadVarint(byte[] buffer, int offset, int limit, out ulong value)
        {
            value = 0;
            int shift = 0;
            int current = offset;
            while (current < limit && shift < 64)
            {
                byte b = buffer[current++];
                value |= ((ulong)(b & 0x7F)) << shift;
                if ((b & 0x80) == 0)
                {
                    return current - offset;
                }
                shift += 7;
            }
            return -1;
        }

        public static int ReadVarint(byte[] buffer, int offset, out ulong value)
        {
            if (buffer == null || offset >= buffer.Length)
            {
                value = 0;
                return -1;
            }
            int bytes = ReadVarint(buffer, offset, buffer.Length, out value);
            return bytes > 0 ? bytes : 0;
        }

        public static void WriteVarint(Stream stream, ulong value)
        {
            while (value > 0x7F)
            {
                stream.WriteByte((byte)((value & 0x7F) | 0x80));
                value >>= 7;
            }
            stream.WriteByte((byte)(value & 0x7F));
        }

        public static byte[] EncodeVarint(ulong value)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                WriteVarint(ms, value);
                return ms.ToArray();
            }
        }

        public static List<ProtoNode> Decode(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0) return new List<ProtoNode>();
            return Decode(buffer, 0, buffer.Length);
        }

        public static List<ProtoNode> Decode(byte[] buffer, int offset, int length)
        {
            List<ProtoNode> list = new List<ProtoNode>();
            if (buffer == null || length <= 0) return list;

            int limit = offset + length;
            int current = offset;

            while (current < limit)
            {
                ulong tag;
                int tagLen = ReadVarint(buffer, current, limit, out tag);
                if (tagLen <= 0) break;
                current += tagLen;

                int fieldNum = (int)(tag >> 3);
                int wireType = (int)(tag & 0x07);

                ProtoNode node = new ProtoNode(fieldNum, wireType);

                if (wireType == 0) // Varint
                {
                    ulong val;
                    int valLen = ReadVarint(buffer, current, limit, out val);
                    if (valLen <= 0) break;
                    current += valLen;
                    node.VarintValue = val;
                }
                else if (wireType == 1) // Fixed64
                {
                    if (current + 8 > limit) break;
                    node.Fixed64Value = BitConverter.ToUInt64(buffer, current);
                    current += 8;
                }
                else if (wireType == 2) // Length-Delimited
                {
                    ulong lenVal;
                    int lenBytes = ReadVarint(buffer, current, limit, out lenVal);
                    if (lenBytes <= 0) break;
                    current += lenBytes;

                    int payloadLen = (int)lenVal;
                    if (payloadLen < 0) break;
                    if (current + payloadLen > limit)
                    {
                        int remaining = limit - current;
                        node.RawBytes = new byte[remaining];
                        if (remaining > 0)
                        {
                            Buffer.BlockCopy(buffer, current, node.RawBytes, 0, remaining);
                        }
                        list.Add(node);
                        break;
                    }

                    node.RawBytes = new byte[payloadLen];
                    if (payloadLen > 0)
                    {
                        Buffer.BlockCopy(buffer, current, node.RawBytes, 0, payloadLen);
                    }

                    List<ProtoNode> subNodes;
                    if (TryDecodeSubmessage(buffer, current, payloadLen, out subNodes))
                    {
                        node.Children = subNodes;
                    }
                    current += payloadLen;
                }
                else if (wireType == 5) // Fixed32
                {
                    if (current + 4 > limit) break;
                    node.Fixed32Value = BitConverter.ToUInt32(buffer, current);
                    current += 4;
                }
                else
                {
                    break;
                }

                list.Add(node);
            }

            return list;
        }

        public static bool TryDecodeSubmessage(byte[] buffer, int offset, int length, out List<ProtoNode> children)
        {
            children = null;
            if (length <= 0) return false;

            int firstWire = buffer[offset] & 0x07;
            if (firstWire != 0 && firstWire != 1 && firstWire != 2 && firstWire != 5) return false;
            int firstField = (buffer[offset] >> 3) | ((buffer[offset] & 0x80) != 0 ? 1 : 0);
            if (firstField == 0) return false;

            try
            {
                List<ProtoNode> parsed = Decode(buffer, offset, length);
                if (parsed == null || parsed.Count == 0) return false;

                byte[] reserialized = Encode(parsed);
                if (reserialized.Length == length)
                {
                    bool match = true;
                    for (int i = 0; i < length; i++)
                    {
                        if (reserialized[i] != buffer[offset + i])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        children = parsed;
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        public static bool LooksLikeValidSubMessage(byte[] data)
        {
            if (data == null || data.Length == 0) return false;
            List<ProtoNode> unused;
            return TryDecodeSubmessage(data, 0, data.Length, out unused);
        }

        public static byte[] Encode(List<ProtoNode> nodes)
        {
            if (nodes == null || nodes.Count == 0) return new byte[0];
            using (MemoryStream ms = new MemoryStream())
            {
                EncodeTo(nodes, ms);
                return ms.ToArray();
            }
        }

        public static void EncodeTo(List<ProtoNode> nodes, Stream stream)
        {
            if (nodes == null || stream == null) return;

            for (int i = 0; i < nodes.Count; i++)
            {
                ProtoNode node = nodes[i];
                ulong tag = (((ulong)(uint)node.FieldNumber) << 3) | (ulong)(uint)(node.WireType & 0x07);
                WriteVarint(stream, tag);

                if (node.WireType == 0) // Varint
                {
                    WriteVarint(stream, node.VarintValue);
                }
                else if (node.WireType == 1) // Fixed64
                {
                    byte[] b = BitConverter.GetBytes(node.Fixed64Value);
                    stream.Write(b, 0, 8);
                }
                else if (node.WireType == 2) // Length-Delimited
                {
                    byte[] payload;
                    if (node.Children != null && node.Children.Count > 0)
                    {
                        payload = Encode(node.Children);
                    }
                    else
                    {
                        payload = node.RawBytes ?? new byte[0];
                    }
                    WriteVarint(stream, (ulong)payload.Length);
                    if (payload.Length > 0)
                    {
                        stream.Write(payload, 0, payload.Length);
                    }
                }
                else if (node.WireType == 5) // Fixed32
                {
                    byte[] b = BitConverter.GetBytes(node.Fixed32Value);
                    stream.Write(b, 0, 4);
                }
            }
        }
    }

    /// <summary>
    /// ProtoAstCodec 别名类，兼容测试框架与接口约定
    /// </summary>
    public static class ProtoAstCodec
    {
        public static List<ProtoNode> Decode(byte[] buffer) { return ProtoCodec.Decode(buffer); }
        public static List<ProtoNode> Decode(byte[] buffer, int offset, int length) { return ProtoCodec.Decode(buffer, offset, length); }
        public static byte[] Encode(List<ProtoNode> nodes) { return ProtoCodec.Encode(nodes); }
        public static void EncodeTo(List<ProtoNode> nodes, Stream stream) { ProtoCodec.EncodeTo(nodes, stream); }
        public static int ReadVarint(byte[] buffer, int offset, out ulong value) { return ProtoCodec.ReadVarint(buffer, offset, out value); }
        public static void WriteVarint(Stream stream, ulong value) { ProtoCodec.WriteVarint(stream, value); }
        public static bool LooksLikeValidSubMessage(byte[] data) { return ProtoCodec.LooksLikeValidSubMessage(data); }
    }

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
    /// 会话元数据提取器 (100% 优先提取 Field 15 自定义标题)
    /// </summary>
    public static class SessionMetadataExtractor
    {
        public static List<ConversationSummaryInfo> ExtractSummaries(byte[] pbData, Dictionary<string, ProjectMetadata> projectsMap)
        {
            List<ConversationSummaryInfo> list = new List<ConversationSummaryInfo>();
            if (pbData == null || pbData.Length == 0) return list;

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
                info.ProjectName = "Default Project";
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
                    info.SubagentRole = roleNode.GetString().Trim();
                    if (!string.IsNullOrEmpty(info.SubagentRole)) info.IsSubagent = true;
                }
                ProtoNode parentNode = node.GetChild(6);
                if (parentNode != null)
                {
                    info.ParentConversationId = parentNode.GetString();
                }
                ProtoNode uriNode = node.GetChild(7);
                if (uriNode != null && string.IsNullOrEmpty(info.FolderUri))
                {
                    info.FolderUri = uriNode.GetString();
                }
                ProtoNode descNode = node.GetChild(8);
                if (descNode != null)
                {
                    string desc = descNode.GetString();
                    if (desc.IndexOf("subagent", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        info.IsSubagent = true;
                    }
                }
                ProtoNode projIdNode = node.GetChild(18);
                if (projIdNode != null)
                {
                    info.ProjectId = projIdNode.GetString();
                }
            }
        }

        private static DateTime ParseTimestampNode(ProtoNode node)
        {
            if (node == null) return DateTime.MinValue;
            if (node.WireType == 0 && node.VarintValue > 0)
            {
                return ConvertEpochToDateTime(node.VarintValue);
            }
            if (node.HasChildren)
            {
                ProtoNode secNode = node.GetChild(1);
                if (secNode != null && secNode.VarintValue > 0)
                {
                    return ConvertEpochToDateTime(secNode.VarintValue);
                }
            }
            return DateTime.MinValue;
        }

        public static string ExtractProjectNameFromUri(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return "Unknown";
            string clean = uri.Replace("file:///", "").Replace("file://", "").Replace('/', '\\').TrimEnd('\\');
            clean = Uri.UnescapeDataString(clean);
            int lastSlash = clean.LastIndexOf('\\');
            if (lastSlash >= 0 && lastSlash < clean.Length - 1)
            {
                return clean.Substring(lastSlash + 1);
            }
            return clean;
        }

        private static void ResolveProjectInfo(ConversationSummaryInfo info, Dictionary<string, ProjectMetadata> projectsMap)
        {
            if (projectsMap != null && !string.IsNullOrEmpty(info.ProjectId) && projectsMap.ContainsKey(info.ProjectId))
            {
                ProjectMetadata p = projectsMap[info.ProjectId];
                info.ProjectName = p.Name;
                if (string.IsNullOrEmpty(info.FolderUri)) info.FolderUri = p.FolderUri;
            }
            else if (string.IsNullOrEmpty(info.ProjectName) && !string.IsNullOrEmpty(info.FolderUri))
            {
                info.ProjectName = ExtractProjectNameFromUri(info.FolderUri);
            }
            else if (info.ProjectId == "outside-of-project")
            {
                info.ProjectName = "独立会话 (Outside of Project)";
            }
            else if (string.IsNullOrEmpty(info.ProjectName))
            {
                info.ProjectName = "Default Project";
            }
        }

        private static DateTime ConvertEpochToDateTime(ulong epoch)
        {
            try
            {
                DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                if (epoch > 1000000000000UL) // Milliseconds
                {
                    return origin.AddMilliseconds((double)epoch).ToLocalTime();
                }
                else // Seconds
                {
                    return origin.AddSeconds((double)epoch).ToLocalTime();
                }
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// 流式 Zip 内存解析引擎 (零临时磁盘开销)
    /// </summary>
    public static class StreamingZipIndexReader
    {
        public static BackupArchiveIndex ReadIndex(string zipFilePath)
        {
            Stopwatch sw = Stopwatch.StartNew();
            BackupArchiveIndex index = new BackupArchiveIndex();

            if (!File.Exists(zipFilePath))
            {
                throw new FileNotFoundException("Zip file not found", zipFilePath);
            }

            FileInfo fi = new FileInfo(zipFilePath);
            index.TotalZipSizeBytes = fi.Length;

            using (FileStream fs = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan))
            using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                ZipArchiveEntry metaEntry = archive.GetEntry("metadata.json") ?? archive.GetEntry("data/metadata.json");
                if (metaEntry != null)
                {
                    using (StreamReader sr = new StreamReader(metaEntry.Open(), Encoding.UTF8))
                    {
                        string json = sr.ReadToEnd();
                        index.SourceHome = SimpleJsonExtract(json, "source_home");
                        index.SourceUser = SimpleJsonExtract(json, "source_user");
                        index.BackupTime = SimpleJsonExtract(json, "backup_time");
                        index.Version = SimpleJsonExtract(json, "version");
                    }
                }

                byte[] pbData = null;
                var entries = archive.Entries;
                int count = entries.Count;

                for (int i = 0; i < count; i++)
                {
                    ZipArchiveEntry entry = entries[i];
                    string name = entry.FullName;
                    if (string.IsNullOrEmpty(name)) continue;

                    string norm = name.Replace('\\', '/');

                    if (norm.StartsWith("conversations/", StringComparison.OrdinalIgnoreCase) ||
                        norm.StartsWith("data/antigravity/conversations/", StringComparison.OrdinalIgnoreCase))
                    {
                        if (norm.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                        {
                            string fileName = Path.GetFileNameWithoutExtension(norm);
                            index.ExistingConvoDbFiles.Add(fileName);
                        }
                    }
                    else if (norm.StartsWith("brain/", StringComparison.OrdinalIgnoreCase) ||
                             norm.StartsWith("data/antigravity/brain/", StringComparison.OrdinalIgnoreCase))
                    {
                        string prefix = norm.StartsWith("data/antigravity/brain/", StringComparison.OrdinalIgnoreCase) ? "data/antigravity/brain/" : "brain/";
                        int prefixLen = prefix.Length;
                        if (norm.Length > prefixLen)
                        {
                            int slashIdx = norm.IndexOf('/', prefixLen);
                            string brainId = slashIdx > 0 ? norm.Substring(prefixLen, slashIdx - prefixLen) : norm.Substring(prefixLen);
                            if (!string.IsNullOrEmpty(brainId)) index.ExistingBrainDirs.Add(brainId);
                        }
                    }
                    else if (norm.StartsWith("config/projects/", StringComparison.OrdinalIgnoreCase) ||
                             norm.StartsWith("data/config/projects/", StringComparison.OrdinalIgnoreCase))
                    {
                        if (norm.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        {
                            using (Stream s = entry.Open())
                            using (MemoryStream ms = new MemoryStream())
                            {
                                s.CopyTo(ms);
                                string json = Encoding.UTF8.GetString(ms.ToArray());
                                ProjectMetadata proj = ParseProjectJson(json);
                                if (proj != null && !string.IsNullOrEmpty(proj.Id))
                                {
                                    index.Projects[proj.Id] = proj;
                                }
                            }
                        }
                    }
                    else if (norm.EndsWith("agyhub_summaries_proto.pb", StringComparison.OrdinalIgnoreCase))
                    {
                        using (Stream s = entry.Open())
                        using (MemoryStream ms = new MemoryStream())
                        {
                            s.CopyTo(ms);
                            pbData = ms.ToArray();
                            index.ExtractedMetadataSizeBytes += pbData.Length;
                        }
                    }
                    else if (norm.StartsWith("rules/", StringComparison.OrdinalIgnoreCase) ||
                             norm.StartsWith("config/rules/", StringComparison.OrdinalIgnoreCase) ||
                             norm.StartsWith("data/.geminirules", StringComparison.OrdinalIgnoreCase) ||
                             norm.StartsWith("data/GEMINI.md", StringComparison.OrdinalIgnoreCase))
                    {
                        index.HasRules = true;
                    }
                    else if (norm.StartsWith("skills/", StringComparison.OrdinalIgnoreCase) ||
                             norm.StartsWith("config/skills/", StringComparison.OrdinalIgnoreCase) ||
                             norm.StartsWith("data/antigravity/builtin/skills", StringComparison.OrdinalIgnoreCase) ||
                             norm.StartsWith("data/config/skills", StringComparison.OrdinalIgnoreCase))
                    {
                        index.HasSkills = true;
                    }
                    else if (norm.StartsWith("mcp/", StringComparison.OrdinalIgnoreCase) ||
                             norm.StartsWith("config/mcp", StringComparison.OrdinalIgnoreCase) ||
                             norm.StartsWith("data/antigravity/mcp", StringComparison.OrdinalIgnoreCase) ||
                             norm.StartsWith("data/config/mcp_config", StringComparison.OrdinalIgnoreCase))
                    {
                        index.HasMcp = true;
                    }
                }

                if (pbData != null)
                {
                    index.Conversations = SessionMetadataExtractor.ExtractSummaries(pbData, index.Projects);
                }
            }

            sw.Stop();
            index.ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds;
            return index;
        }

        public static ZipMetadataPackage ReadZipMetadata(string zipFilePath)
        {
            if (!File.Exists(zipFilePath))
                throw new FileNotFoundException("Zip file not found", zipFilePath);

            using (FileStream fs = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                return ReadZipMetadata(archive);
            }
        }

        public static ZipMetadataPackage ReadZipMetadata(ZipArchive archive)
        {
            ZipMetadataPackage package = new ZipMetadataPackage();
            HashSet<string> brainSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < archive.Entries.Count; i++)
            {
                ZipArchiveEntry entry = archive.Entries[i];
                string name = entry.FullName.Replace('\\', '/');

                if (name.EndsWith("agyhub_summaries_proto.pb", StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream es = entry.Open())
                    using (MemoryStream ms = new MemoryStream())
                    {
                        es.CopyTo(ms);
                        package.SummaryProtoBytes = ms.ToArray();
                    }
                }
                else if ((name.StartsWith("config/projects/", StringComparison.OrdinalIgnoreCase) || name.StartsWith("data/config/projects/", StringComparison.OrdinalIgnoreCase)) && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream es = entry.Open())
                    using (StreamReader reader = new StreamReader(es, Encoding.UTF8))
                    {
                        package.ProjectConfigs[name] = reader.ReadToEnd();
                    }
                }
                else if (name.Equals("metadata.json", StringComparison.OrdinalIgnoreCase) || name.Equals("data/metadata.json", StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream es = entry.Open())
                    using (StreamReader reader = new StreamReader(es, Encoding.UTF8))
                    {
                        package.MetadataJson = reader.ReadToEnd();
                    }
                }
                else if ((name.StartsWith("conversations/", StringComparison.OrdinalIgnoreCase) || name.StartsWith("data/antigravity/conversations/", StringComparison.OrdinalIgnoreCase)) && name.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = Path.GetFileName(name);
                    if (!string.IsNullOrEmpty(fileName))
                        package.ConversationDbNames.Add(fileName);
                }
                else if (name.StartsWith("brain/", StringComparison.OrdinalIgnoreCase) || name.StartsWith("data/antigravity/brain/", StringComparison.OrdinalIgnoreCase))
                {
                    string prefix = name.StartsWith("data/antigravity/brain/", StringComparison.OrdinalIgnoreCase) ? "data/antigravity/brain/" : "brain/";
                    int prefixLen = prefix.Length;
                    if (name.Length > prefixLen)
                    {
                        int slashIdx = name.IndexOf('/', prefixLen);
                        string brainId = slashIdx > 0 ? name.Substring(prefixLen, slashIdx - prefixLen) : name.Substring(prefixLen);
                        if (!string.IsNullOrEmpty(brainId)) brainSet.Add(brainId);
                    }
                }
                else if (name.StartsWith("rules/", StringComparison.OrdinalIgnoreCase) || name.StartsWith("config/rules/", StringComparison.OrdinalIgnoreCase) || name.StartsWith("data/.geminirules", StringComparison.OrdinalIgnoreCase) || name.StartsWith("data/GEMINI.md", StringComparison.OrdinalIgnoreCase))
                {
                    package.HasGlobalRules = true;
                }
                else if (name.StartsWith("skills/", StringComparison.OrdinalIgnoreCase) || name.StartsWith("config/skills/", StringComparison.OrdinalIgnoreCase) || name.StartsWith("data/antigravity/builtin/skills", StringComparison.OrdinalIgnoreCase) || name.StartsWith("data/config/skills", StringComparison.OrdinalIgnoreCase))
                {
                    package.HasGlobalSkills = true;
                }
                else if (name.StartsWith("mcp/", StringComparison.OrdinalIgnoreCase) || name.StartsWith("config/mcp", StringComparison.OrdinalIgnoreCase) || name.StartsWith("data/antigravity/mcp", StringComparison.OrdinalIgnoreCase) || name.StartsWith("data/config/mcp_config", StringComparison.OrdinalIgnoreCase))
                {
                    package.HasGlobalMcp = true;
                }
            }

            package.BrainDirectoryNames.AddRange(brainSet);
            return package;
        }

        private static ProjectMetadata ParseProjectJson(string json)
        {
            try
            {
                ProjectMetadata p = new ProjectMetadata();
                p.Id = SimpleJsonExtract(json, "id");
                p.Name = SimpleJsonExtract(json, "name");
                p.FolderUri = SimpleJsonExtract(json, "folderUri");
                if (string.IsNullOrEmpty(p.FolderUri))
                {
                    Match m = Regex.Match(json, "\"folderUri\"\\s*:\\s*\"([^\"]+)\"");
                    if (m.Success) p.FolderUri = m.Groups[1].Value;
                }
                return p;
            }
            catch
            {
                return null;
            }
        }

        private static string SimpleJsonExtract(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : "";
        }
    }

    /// <summary>
    /// 工作区路径重映射器
    /// </summary>
    public static class WorkspacePathRemapper
    {
        public static void RemapPathInShard(ProtoNode rootNode, string oldPath, string newPath)
        {
            if (rootNode == null || string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath)) return;
            List<KeyValuePair<string, string>> pairs = BuildReplacementPairs(oldPath, newPath);
            RemapRecursive(rootNode, pairs);
        }

        public static void RemapExactString(ProtoNode rootNode, string oldStr, string newStr)
        {
            if (rootNode == null || string.IsNullOrEmpty(oldStr) || string.IsNullOrEmpty(newStr)) return;
            RemapExactRecursive(rootNode, oldStr, newStr);
        }

        public static string RemapPath(string path, string oldRoot, string newRoot)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(oldRoot) || string.IsNullOrEmpty(newRoot)) return path;
            List<KeyValuePair<string, string>> pairs = BuildReplacementPairs(oldRoot, newRoot);
            for (int i = 0; i < pairs.Count; i++)
            {
                if (path.Contains(pairs[i].Key))
                {
                    path = path.Replace(pairs[i].Key, pairs[i].Value);
                }
            }
            return path;
        }

        private static void RemapRecursive(ProtoNode node, List<KeyValuePair<string, string>> pairs)
        {
            if (node == null) return;
            if (node.Children != null && node.Children.Count > 0)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    RemapRecursive(node.Children[i], pairs);
                }
            }
            else if (node.WireType == 2 && node.RawBytes != null && node.RawBytes.Length > 0)
            {
                string text = TryGetUtf8String(node.RawBytes);
                if (!string.IsNullOrEmpty(text))
                {
                    bool replaced = false;
                    for (int i = 0; i < pairs.Count; i++)
                    {
                        if (text.Contains(pairs[i].Key))
                        {
                            text = text.Replace(pairs[i].Key, pairs[i].Value);
                            replaced = true;
                        }
                    }
                    if (replaced)
                    {
                        node.RawBytes = Encoding.UTF8.GetBytes(text);
                    }
                }
            }
        }

        private static void RemapExactRecursive(ProtoNode node, string oldStr, string newStr)
        {
            if (node == null) return;
            if (node.Children != null && node.Children.Count > 0)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    RemapExactRecursive(node.Children[i], oldStr, newStr);
                }
            }
            else if (node.WireType == 2 && node.RawBytes != null && node.RawBytes.Length > 0)
            {
                string text = TryGetUtf8String(node.RawBytes);
                if (text == oldStr)
                {
                    node.RawBytes = Encoding.UTF8.GetBytes(newStr);
                }
            }
        }

        private static string TryGetUtf8String(byte[] bytes)
        {
            try
            {
                string s = Encoding.UTF8.GetString(bytes);
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (c < 32 && c != '\r' && c != '\n' && c != '\t') return null;
                }
                return s;
            }
            catch
            {
                return null;
            }
        }

        public static List<KeyValuePair<string, string>> BuildReplacementPairs(string sourcePath, string targetPath)
        {
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(targetPath)) return list;

            string sNorm = sourcePath.Replace('\\', '/').TrimEnd('/');
            string tNorm = targetPath.Replace('\\', '/').TrimEnd('/');

            AddPair(list, sNorm, tNorm);
            AddPair(list, sourcePath.Replace('/', '\\').TrimEnd('\\'), targetPath.Replace('/', '\\').TrimEnd('\\'));
            AddPair(list, LowercaseDrive(sNorm), LowercaseDrive(tNorm));
            AddPair(list, "file:///" + LowercaseDrive(sNorm), "file:///" + LowercaseDrive(tNorm));
            AddPair(list, "file:///" + UppercaseDrive(sNorm), "file:///" + UppercaseDrive(tNorm));
            AddPair(list, "file:///" + LowercaseDrive(sNorm).Replace(":", "%3A"), "file:///" + LowercaseDrive(tNorm).Replace(":", "%3A"));
            AddPair(list, "file:///" + UppercaseDrive(sNorm).Replace(":", "%3A"), "file:///" + UppercaseDrive(tNorm).Replace(":", "%3A"));

            string sBackEncoded = "file:///" + LowercaseDrive(sNorm).Replace(":", "%3A").Replace("/", "%5C");
            string tBackEncoded = "file:///" + LowercaseDrive(tNorm).Replace(":", "%3A").Replace("/", "%5C");
            AddPair(list, sBackEncoded, tBackEncoded);

            return list;
        }

        private static void AddPair(List<KeyValuePair<string, string>> list, string k, string v)
        {
            if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v) && k != v)
            {
                list.Add(new KeyValuePair<string, string>(k, v));
            }
        }

        private static string LowercaseDrive(string path)
        {
            if (path.Length >= 2 && path[1] == ':')
            {
                return char.ToLowerInvariant(path[0]) + path.Substring(1);
            }
            return path;
        }

        private static string UppercaseDrive(string path)
        {
            if (path.Length >= 2 && path[1] == ':')
            {
                return char.ToUpperInvariant(path[0]) + path.Substring(1);
            }
            return path;
        }
    }

    /// <summary>
    /// 增量 Protobuf 分片提取与合流管理器
    /// </summary>
    public static class IncrementalProtoShardMerger
    {
        public class ShardItem
        {
            public string ConversationId;
            public byte[] RawBytes;
            public ProtoNode AstNode;
        }

        public class PathRemapRule
        {
            public string OldPath;
            public string NewPath;
            public string OldProjectId;
            public string NewProjectId;
        }

        public static List<ShardItem> ExtractShards(byte[] data)
        {
            List<ShardItem> shards = new List<ShardItem>();
            if (data == null || data.Length == 0) return shards;

            int offset = 0;
            int end = data.Length;

            while (offset < end)
            {
                int entryStart = offset;
                ulong key;
                int kLen = ProtoCodec.ReadVarint(data, offset, end, out key);
                if (kLen <= 0) break;

                int fieldNum = (int)(key >> 3);
                int wireType = (int)(key & 0x07);

                if (fieldNum == 1 && wireType == 2)
                {
                    ulong lenVal;
                    int lLen = ProtoCodec.ReadVarint(data, offset + kLen, end, out lenVal);
                    if (lLen <= 0) break;

                    int payloadOffset = offset + kLen + lLen;
                    int payloadLen = (int)lenVal;
                    int entryEnd = payloadOffset + payloadLen;
                    if (entryEnd > end) break;

                    int totalLen = entryEnd - entryStart;
                    byte[] entryBytes = new byte[totalLen];
                    Buffer.BlockCopy(data, entryStart, entryBytes, 0, totalLen);

                    List<ProtoNode> parsedNodes = ProtoCodec.Decode(data, entryStart, totalLen);
                    ProtoNode entryNode = (parsedNodes.Count > 0) ? parsedNodes[0] : null;

                    string convoId = ExtractConvoId(entryNode);

                    shards.Add(new ShardItem
                    {
                        ConversationId = convoId,
                        RawBytes = entryBytes,
                        AstNode = entryNode
                    });

                    offset = entryEnd;
                }
                else
                {
                    break;
                }
            }

            return shards;
        }

        private static string ExtractConvoId(ProtoNode node)
        {
            if (node == null || node.Children == null) return null;
            for (int i = 0; i < node.Children.Count; i++)
            {
                ProtoNode child = node.Children[i];
                if (child.FieldNumber == 1 && child.WireType == 2 && child.RawBytes != null)
                {
                    if (child.GetChild(2) != null)
                    {
                        return Encoding.UTF8.GetString(child.RawBytes);
                    }
                    else
                    {
                        ProtoNode f2 = node.GetChild(2);
                        if (f2 != null && f2.RawBytes != null && !f2.HasChildren)
                        {
                            return Encoding.UTF8.GetString(f2.RawBytes);
                        }
                        return Encoding.UTF8.GetString(child.RawBytes);
                    }
                }
                else if (child.FieldNumber == 2 && child.WireType == 2 && child.RawBytes != null && !child.HasChildren)
                {
                    return Encoding.UTF8.GetString(child.RawBytes);
                }
            }
            return null;
        }

        public static byte[] MergeShards(byte[] destinationPbBytes, List<ShardItem> incomingShards, List<PathRemapRule> remapRules = null)
        {
            Dictionary<string, byte[]> map = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            List<string> order = new List<string>();

            if (destinationPbBytes != null && destinationPbBytes.Length > 0)
            {
                List<ShardItem> localShards = ExtractShards(destinationPbBytes);
                for (int i = 0; i < localShards.Count; i++)
                {
                    ShardItem item = localShards[i];
                    if (!string.IsNullOrEmpty(item.ConversationId))
                    {
                        map[item.ConversationId] = item.RawBytes;
                        order.Add(item.ConversationId);
                    }
                }
            }

            if (incomingShards != null)
            {
                for (int i = 0; i < incomingShards.Count; i++)
                {
                    ShardItem incoming = incomingShards[i];
                    if (string.IsNullOrEmpty(incoming.ConversationId)) continue;

                    byte[] finalBytes = incoming.RawBytes;

                    if (incoming.AstNode != null && remapRules != null && remapRules.Count > 0)
                    {
                        for (int r = 0; r < remapRules.Count; r++)
                        {
                            PathRemapRule rule = remapRules[r];
                            if (!string.IsNullOrEmpty(rule.OldPath) && !string.IsNullOrEmpty(rule.NewPath))
                            {
                                WorkspacePathRemapper.RemapPathInShard(incoming.AstNode, rule.OldPath, rule.NewPath);
                            }
                            if (!string.IsNullOrEmpty(rule.OldProjectId) && !string.IsNullOrEmpty(rule.NewProjectId))
                            {
                                WorkspacePathRemapper.RemapExactString(incoming.AstNode, rule.OldProjectId, rule.NewProjectId);
                            }
                        }

                        using (MemoryStream ms = new MemoryStream())
                        {
                            incoming.AstNode.WriteTo(ms);
                            finalBytes = ms.ToArray();
                        }
                    }

                    if (!map.ContainsKey(incoming.ConversationId))
                    {
                        order.Add(incoming.ConversationId);
                    }
                    map[incoming.ConversationId] = finalBytes;
                }
            }

            using (MemoryStream outMs = new MemoryStream())
            {
                for (int i = 0; i < order.Count; i++)
                {
                    byte[] slice = map[order[i]];
                    outMs.Write(slice, 0, slice.Length);
                }
                return outMs.ToArray();
            }
        }

        public static void AtomicSaveWithBackup(string targetPath, byte[] data)
        {
            if (string.IsNullOrEmpty(targetPath)) throw new ArgumentNullException("targetPath");
            if (data == null) throw new ArgumentNullException("data");

            string dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string tempFile = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            string backupFile = targetPath + ".bak";

            try
            {
                using (FileStream fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.Write(data, 0, data.Length);
                    fs.Flush(true);
                }

                if (File.Exists(targetPath))
                {
                    try
                    {
                        File.Replace(tempFile, targetPath, backupFile, true);
                    }
                    catch
                    {
                        if (File.Exists(backupFile)) File.Delete(backupFile);
                        File.Copy(targetPath, backupFile, true);
                        File.Delete(targetPath);
                        File.Move(tempFile, targetPath);
                    }
                }
                else
                {
                    File.Move(tempFile, targetPath);
                }
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                throw new IOException("Failed to atomically save protobuf file: " + targetPath, ex);
            }
        }
    }
}

