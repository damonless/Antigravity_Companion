using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Text;
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

    /// <summary>
    /// 双缓冲 TreeView（彻底消除闪烁，支持 Explorer 样式）
    /// </summary>
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
            SendMessage(this.Handle, 0x1100 + 44, IntPtr.Zero, (IntPtr)0x0040); // TVS_EX_DOUBLEBUFFER
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
    }

    public partial class MainForm : Form
    {
        // ── 核心数据状态 ──
        private string HomePath;
        private List<ConversationSummaryInfo> _localSummaries = new List<ConversationSummaryInfo>();
        private Dictionary<string, ProjectMetadata> _localProjects = new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
        private BackupArchiveIndex _backupIndex;
        private List<ProjectDiffNode> _currentDiffNodes = new List<ProjectDiffNode>();
        private string _loadedBackupPath;
        private readonly ImportHighlightTracker _highlightTracker = new ImportHighlightTracker();

        // ── UI 控件成员 ──
        private DoubleBufferedTreeView tvLocal;
        private DoubleBufferedTreeView tvBackup;
        private Label lblLocalHeader;
        private Label lblBackupHeader;
        private Label lblStatsSummary;
        private Label lblConvCount;
        private TextBox txtSearch;
        private CheckBox chkHideTemp;
        private Button btnDetect;
        private Button btnOpenBackup;
        private Button btnFullBackup;
        private Button btnImportSelected;
        private Button btnSelectAll;
        private Button btnDeselectAll;
        private Button btnSelectNewOnly;
        private TextBox txtGeminiPath;
        private RichTextBox rtbLog;
        private SplitContainer splitMain;
        private TabControl tabRightBottom;
        private ListBox lbConfigFiles;
        private RichTextBox rtbConfigEditor;
        private Button btnSaveConfig;
        private Label lblEditingFile;
        private ContextMenuStrip contextMenuBackup;

        private bool _isUpdatingChecks = false;
        private BackgroundWorker _worker;

        public MainForm()
        {
            HomePath = Environment.GetEnvironmentVariable("USERPROFILE") ?? "C:\\Users\\Administrator";
            BuildUI();
            this.Load += (s, e) => { DetectPaths(); };
        }

        // ══════════════════════════════════════════════════════════
        // 现代化 UI 构建 (Modern UI/UX Layout)
        // ══════════════════════════════════════════════════════════
        private void BuildUI()
        {
            this.Text = "Antigravity Companion V0.6 - 双栏可视化差异对比与细粒度导入管理器";
            this.Size = new Size(1360, 860);
            this.MinimumSize = new Size(1100, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);
            this.BackColor = Color.FromArgb(245, 247, 250);

            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(iconPath)) this.Icon = new Icon(iconPath);
            }
            catch {}

            // 1. 顶部现代化仪表盘与工具栏容器
            Panel pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 82,
                BackColor = Color.White,
                Padding = new Padding(12, 8, 12, 8)
            };
            pnlTop.Paint += (s, pe) =>
            {
                pe.Graphics.DrawLine(new Pen(Color.FromArgb(220, 224, 230)), 0, pnlTop.Height - 1, pnlTop.Width, pnlTop.Height - 1);
            };

            // 操作按钮栏
            btnDetect = MakeBtn("🔄 重新检测本地", 130, Color.FromArgb(232, 240, 254), Color.FromArgb(26, 115, 232));
            btnDetect.Location = new Point(12, 10);
            btnDetect.Click += (s, e) => DetectPaths();

            btnOpenBackup = MakeBtn("📂 打开备份 (.agy)", 140, Color.FromArgb(230, 244, 234), Color.FromArgb(19, 115, 51));
            btnOpenBackup.Location = new Point(150, 10);
            btnOpenBackup.Click += BtnOpenBackup_Click;

            btnFullBackup = MakeBtn("⚡ 一键全量备份", 130, Color.FromArgb(254, 239, 239), Color.FromArgb(217, 48, 37));
            btnFullBackup.Location = new Point(298, 10);
            btnFullBackup.Click += BtnFullBackup_Click;

            chkHideTemp = new CheckBox
            {
                Text = "过滤临时工程与子代理会话 (推荐)",
                Checked = true,
                AutoSize = true,
                Location = new Point(440, 15),
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 64, 67)
            };
            chkHideTemp.CheckedChanged += (s, e) =>
            {
                RenderLocalTree();
                if (_backupIndex != null) RenderBackupTree();
            };

            Label lblSearchIcon = new Label
            {
                Text = "🔍 搜索:",
                AutoSize = true,
                Location = new Point(710, 15),
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(95, 99, 104)
            };

            txtSearch = new TextBox
            {
                Location = new Point(765, 12),
                Width = 180,
                Font = new Font("Microsoft YaHei UI", 9f)
            };
            txtSearch.TextChanged += TxtSearch_TextChanged;

            btnImportSelected = MakeBtn("🚀 导入所选项目到本地", 170, Color.FromArgb(26, 115, 232), Color.White);
            btnImportSelected.Location = new Point(960, 10);
            btnImportSelected.Enabled = false;
            btnImportSelected.Click += BtnPerformSelectiveImport_Click;

            txtGeminiPath = new TextBox { Visible = false };

            // 状态指示条
            lblStatsSummary = new Label
            {
                Text = "本地: 正在扫描... | 备份包: 未加载 | 待合流选中: 0 个工程 (0 条会话)",
                Location = new Point(14, 50),
                Size = new Size(1300, 24),
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(95, 99, 104)
            };

            pnlTop.Controls.AddRange(new Control[] {
                btnDetect, btnOpenBackup, btnFullBackup, chkHideTemp, 
                lblSearchIcon, txtSearch, btnImportSelected, txtGeminiPath, lblStatsSummary
            });

            // 2. 主体 SplitContainer (左树 vs 右树)
            splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 560,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(235, 238, 242)
            };

            // 左侧面板 (本地环境)
            Panel pnlLeft = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(8) };
            Panel pnlLeftHeader = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.FromArgb(248, 249, 250) };
            lblLocalHeader = new Label
            {
                Text = "💻 当前本地环境 (Local Workspace)",
                Dock = DockStyle.Left,
                AutoSize = false,
                Width = 320,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(32, 33, 36),
                Padding = new Padding(8, 0, 0, 0)
            };
            lblConvCount = new Label
            {
                Text = "活跃主会话: 0",
                Dock = DockStyle.Right,
                AutoSize = false,
                Width = 180,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Microsoft YaHei UI", 8.5f),
                ForeColor = Color.FromArgb(95, 99, 104),
                Padding = new Padding(0, 0, 8, 0)
            };
            pnlLeftHeader.Controls.AddRange(new Control[] { lblLocalHeader, lblConvCount });

            tvLocal = new DoubleBufferedTreeView
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Microsoft YaHei UI", 9f),
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                FullRowSelect = true,
                HideSelection = false
            };
            pnlLeft.Controls.Add(tvLocal);
            pnlLeft.Controls.Add(pnlLeftHeader);
            splitMain.Panel1.Controls.Add(pnlLeft);

            // 右侧面板 (备份归档)
            Panel pnlRight = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(8) };
            Panel pnlRightHeader = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.FromArgb(248, 249, 250) };
            lblBackupHeader = new Label
            {
                Text = "📦 备份数据包 (Backup Archive - 未加载)",
                Dock = DockStyle.Left,
                AutoSize = false,
                Width = 360,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(32, 33, 36),
                Padding = new Padding(8, 0, 0, 0)
            };

            // 快速选择工具栏
            FlowLayoutPanel pnlQuickSelect = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 6, 8, 0)
            };

            btnSelectAll = MakeSmallBtn("全选");
            btnSelectAll.Click += (s, e) => SetAllBackupChecks(true);

            btnDeselectAll = MakeSmallBtn("全不选");
            btnDeselectAll.Click += (s, e) => SetAllBackupChecks(false);

            btnSelectNewOnly = MakeSmallBtn("仅选新增 [★]");
            btnSelectNewOnly.Click += BtnSelectNewOnly_Click;

            pnlQuickSelect.Controls.AddRange(new Control[] { btnSelectNewOnly, btnSelectAll, btnDeselectAll });
            pnlRightHeader.Controls.AddRange(new Control[] { lblBackupHeader, pnlQuickSelect });

            tvBackup = new DoubleBufferedTreeView
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Microsoft YaHei UI", 9f),
                CheckBoxes = true,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                FullRowSelect = true,
                HideSelection = false
            };
            tvBackup.AfterCheck += TvBackup_AfterCheck;

            // 右键菜单
            contextMenuBackup = new ContextMenuStrip();
            contextMenuBackup.Items.Add("勾选当前工程下所有项", null, (s, e) => CheckSelectedNodeCascading(true));
            contextMenuBackup.Items.Add("取消勾选当前工程", null, (s, e) => CheckSelectedNodeCascading(false));
            contextMenuBackup.Items.Add(new ToolStripSeparator());
            contextMenuBackup.Items.Add("复制会话 UUID", null, (s, e) => CopySelectedId());
            tvBackup.ContextMenuStrip = contextMenuBackup;

            pnlRight.Controls.Add(tvBackup);
            pnlRight.Controls.Add(pnlRightHeader);
            splitMain.Panel2.Controls.Add(pnlRight);

            // 3. 底部选项卡 (配置浏览器 & 实时日志)
            tabRightBottom = new TabControl
            {
                Dock = DockStyle.Bottom,
                Height = 220,
                Font = new Font("Microsoft YaHei UI", 9f)
            };

            // Tab 1: 实时运行日志
            TabPage tabLog = new TabPage("📋 运行日志与执行追踪 (Log)");
            rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Consolas", 9.5f),
                BorderStyle = BorderStyle.None
            };
            tabLog.Controls.Add(rtbLog);

            // Tab 2: 配置浏览器与热编辑器
            TabPage tabConfig = new TabPage("⚙️ 规则、技能与 MCP 配置浏览器 (Editor)");
            SplitContainer splitConfig = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 320
            };

            lbConfigFiles = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Microsoft YaHei UI", 9f),
                BackColor = Color.FromArgb(250, 250, 250)
            };
            lbConfigFiles.SelectedIndexChanged += LbConfigFiles_SelectedIndexChanged;
            splitConfig.Panel1.Controls.Add(lbConfigFiles);

            Panel pnlEditor = new Panel { Dock = DockStyle.Fill };
            Panel pnlEditorTop = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.FromArgb(240, 240, 240) };
            lblEditingFile = new Label { Text = "请选择左侧配置文件进行浏览或编辑 (支持 Ctrl+S 极速热保存)", Dock = DockStyle.Left, AutoSize = false, Width = 500, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 0, 0) };
            btnSaveConfig = MakeSmallBtn("💾 保存修改 (Ctrl+S)");
            btnSaveConfig.Dock = DockStyle.Right;
            btnSaveConfig.Width = 140;
            btnSaveConfig.Click += BtnSaveConfig_Click;
            pnlEditorTop.Controls.AddRange(new Control[] { lblEditingFile, btnSaveConfig });

            rtbConfigEditor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.None,
                AcceptsTab = true
            };
            rtbConfigEditor.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.S)
                {
                    BtnSaveConfig_Click(s, EventArgs.Empty);
                    e.SuppressKeyPress = true;
                }
            };

            pnlEditor.Controls.Add(rtbConfigEditor);
            pnlEditor.Controls.Add(pnlEditorTop);
            splitConfig.Panel2.Controls.Add(pnlEditor);

            tabConfig.Controls.Add(splitConfig);

            tabRightBottom.TabPages.Add(tabLog);
            tabRightBottom.TabPages.Add(tabConfig);

            // 组装到主窗体
            this.Controls.Add(splitMain);
            this.Controls.Add(tabRightBottom);
            this.Controls.Add(pnlTop);
        }

        // ══════════════════════════════════════════════════════════
        // 核心业务：本地环境探测与解析 (Local Detection)
        // ══════════════════════════════════════════════════════════
        private void DetectPaths()
        {
            string gemini = Path.Combine(HomePath, ".gemini");
            txtGeminiPath.Text = gemini;

            _localProjects.Clear();
            _localSummaries.Clear();

            Log("========== 正在探测本地 Antigravity 工作环境 ==========", Color.DarkBlue);

            // 1. 读取本地工程项目列表
            string projDir = Path.Combine(gemini, "config", "projects");
            if (Directory.Exists(projDir))
            {
                string[] files = Directory.GetFiles(projDir, "*.json");
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        string json = File.ReadAllText(files[i], Encoding.UTF8);
                        string id = Path.GetFileNameWithoutExtension(files[i]);
                        string name = DirectStreamArchiver.ExtractJsonValue(json, "name");
                        string uri = DirectStreamArchiver.ExtractJsonValue(json, "folderUri");
                        if (!string.IsNullOrEmpty(name))
                        {
                            _localProjects[id] = new ProjectMetadata { Id = id, Name = name, FolderUri = uri };
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
                    _localSummaries = SessionMetadataExtractor.ExtractSummaries(pbBytes, _localProjects);
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

            lblConvCount.Text = "活跃主会话: " + mainCount;
            Log(string.Format("本地环境扫描完成：共发现 {0} 个工程，{1} 条活跃主会话（已智能过滤临时子任务与废弃孤儿数据库）", 
                _localProjects.Count, mainCount), Color.DarkGreen);

            RenderLocalTree();
            RefreshConfigBrowser();
            UpdateSelectionStatistics();
        }

        private void RenderLocalTree()
        {
            tvLocal.BeginUpdate();
            try
            {
                tvLocal.Nodes.Clear();
                string gemini = txtGeminiPath.Text.Trim();
                bool hideTemp = chkHideTemp.Checked;
                string query = txtSearch.Text.Trim();

                // ── 1. 全局配置 Root ──
                TreeNode rootConfig = new TreeNode("⚙️ 全局配置 (Global Config)");
                rootConfig.Tag = new DiffItemNodeTag { Category = ItemCategory.GlobalConfigRoot, DisplayName = "全局配置" };

                int rulesCount = 0;
                if (File.Exists(Path.Combine(gemini, ".geminirules"))) rulesCount++;
                if (File.Exists(Path.Combine(gemini, "GEMINI.md"))) rulesCount++;
                string rulesDir = Path.Combine(gemini, "config", "rules");
                if (Directory.Exists(rulesDir)) rulesCount += Directory.GetFiles(rulesDir, "*.md", SearchOption.AllDirectories).Length;

                TreeNode nodeRules = new TreeNode(string.Format("📜 规则与规范 ({0} 项)", rulesCount));
                nodeRules.Tag = new DiffItemNodeTag { Category = ItemCategory.GlobalRules, DisplayName = "规则与规范", SourcePath = gemini };
                rootConfig.Nodes.Add(nodeRules);

                int skillsCount = 0;
                string bSkills = Path.Combine(gemini, "antigravity", "builtin", "skills");
                if (Directory.Exists(bSkills)) skillsCount += Directory.GetDirectories(bSkills).Length;
                string uSkills = Path.Combine(gemini, "config", "skills");
                if (Directory.Exists(uSkills)) skillsCount += Directory.GetDirectories(uSkills).Length;

                TreeNode nodeSkills = new TreeNode(string.Format("⚡ 内置与用户技能 ({0} 项)", skillsCount));
                nodeSkills.Tag = new DiffItemNodeTag { Category = ItemCategory.GlobalSkills, DisplayName = "内置与用户技能", SourcePath = uSkills };
                rootConfig.Nodes.Add(nodeSkills);

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

                Dictionary<string, List<ConversationSummaryInfo>> groups = new Dictionary<string, List<ConversationSummaryInfo>>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < _localSummaries.Count; i++)
                {
                    ConversationSummaryInfo s = _localSummaries[i];
                    string p = s.ProjectName ?? "Outside of Project";
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
                        if (!hideTemp || !list[k].IsSubagent)
                        {
                            if (string.IsNullOrEmpty(query) || 
                                list[k].DisplayTitle.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                list[k].ConversationId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                pName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                visibleList.Add(list[k]);
                            }
                        }
                    }

                    if (visibleList.Count == 0 && !string.IsNullOrEmpty(query)) continue;
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
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Antigravity 备份包 (*.agy)|*.agy|ZIP 压缩包 (*.zip)|*.zip|所有文件 (*.*)|*.*",
                Title = "请选择 Antigravity 备份包 (.agy / .zip)"
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                LoadBackupArchive(dlg.FileName);
            }
        }

        private void LoadBackupArchive(string archivePath)
        {
            if (!File.Exists(archivePath)) { MessageBox.Show("备份包不存在: " + archivePath, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            _loadedBackupPath = archivePath;
            Log("正在通过内存流式只读管道解析备份包元数据: " + Path.GetFileName(archivePath) + "...", Color.DarkBlue);
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

                Log(string.Format("===== 备份包秒级解析完成！共发现 {0} 个工程，{1} 条会话，耗时 {2}ms =====",
                    _backupIndex.Projects.Count, _backupIndex.Conversations.Count, sw.ElapsedMilliseconds), Color.DarkGreen);

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

        private void RenderBackupTree()
        {
            if (_backupIndex == null) return;

            tvBackup.BeginUpdate();
            _isUpdatingChecks = true;
            try
            {
                tvBackup.Nodes.Clear();
                bool hideTemp = chkHideTemp.Checked;
                string query = txtSearch.Text.Trim();

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
                        continue;

                    bool isJunk = pDiff.IsTemporaryOrJunk;
                    List<ConversationSummaryInfo> visibleConvs = new List<ConversationSummaryInfo>();
                    for (int k = 0; k < pDiff.BackupConversations.Count; k++)
                    {
                        if (!hideTemp || !pDiff.BackupConversations[k].IsSubagent)
                        {
                            if (string.IsNullOrEmpty(query) || 
                                pDiff.BackupConversations[k].DisplayTitle.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                pDiff.BackupConversations[k].ConversationId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                pDiff.ProjectName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                visibleConvs.Add(pDiff.BackupConversations[k]);
                            }
                        }
                    }

                    if (visibleConvs.Count == 0 && !string.IsNullOrEmpty(query)) continue;
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
                    node.ForeColor = Color.FromArgb(19, 115, 51);
                    node.NodeFont = new Font(tvBackup.Font, FontStyle.Bold);
                    break;
                case DiffStatus.Aligned:
                    node.ForeColor = Color.FromArgb(95, 99, 104);
                    node.NodeFont = new Font(tvBackup.Font, FontStyle.Regular);
                    break;
                case DiffStatus.Conflicted:
                    node.ForeColor = Color.FromArgb(217, 119, 6);
                    node.NodeFont = new Font(tvBackup.Font, FontStyle.Bold);
                    break;
                case DiffStatus.LocalOnly:
                    node.ForeColor = Color.FromArgb(26, 115, 232);
                    node.NodeFont = new Font(tvBackup.Font, FontStyle.Regular);
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════
        // 复选框级联选中与快速选择
        // ══════════════════════════════════════════════════════════
        private void TvBackup_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_isUpdatingChecks) return;
            _isUpdatingChecks = true;
            try
            {
                SetChildNodesChecked(e.Node, e.Node.Checked);
                UpdateParentNodeChecked(e.Node);
            }
            finally
            {
                _isUpdatingChecks = false;
            }
            UpdateSelectionStatistics();
        }

        private void SetChildNodesChecked(TreeNode node, bool isChecked)
        {
            for (int i = 0; i < node.Nodes.Count; i++)
            {
                TreeNode child = node.Nodes[i];
                child.Checked = isChecked;
                SetChildNodesChecked(child, isChecked);
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

            parent.Checked = anyChecked;
            UpdateParentNodeChecked(parent);
        }

        private void SetAllBackupChecks(bool isChecked)
        {
            _isUpdatingChecks = true;
            try
            {
                for (int i = 0; i < tvBackup.Nodes.Count; i++)
                {
                    TreeNode root = tvBackup.Nodes[i];
                    root.Checked = isChecked;
                    SetChildNodesChecked(root, isChecked);
                }
            }
            finally
            {
                _isUpdatingChecks = false;
            }
            UpdateSelectionStatistics();
        }

        private void BtnSelectNewOnly_Click(object sender, EventArgs e)
        {
            _isUpdatingChecks = true;
            try
            {
                SetAllBackupChecks(false);
                SelectNewNodesRecursive(tvBackup.Nodes);
            }
            finally
            {
                _isUpdatingChecks = false;
            }
            UpdateSelectionStatistics();
        }

        private void SelectNewNodesRecursive(TreeNodeCollection nodes)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode node = nodes[i];
                DiffItemNodeTag tag = node.Tag as DiffItemNodeTag;
                if (tag != null)
                {
                    if (tag.Category == ItemCategory.Conversation && tag.Status == DiffStatus.NewInBackup && !tag.IsTemporaryOrJunk)
                    {
                        node.Checked = true;
                        UpdateParentNodeChecked(node);
                    }
                    else if (tag.Category == ItemCategory.Project && tag.Status == DiffStatus.NewInBackup && !tag.IsTemporaryOrJunk)
                    {
                        node.Checked = true;
                        SetChildNodesChecked(node, true);
                        UpdateParentNodeChecked(node);
                    }
                }
                if (node.Nodes.Count > 0)
                {
                    SelectNewNodesRecursive(node.Nodes);
                }
            }
        }

        private void CheckSelectedNodeCascading(bool isChecked)
        {
            if (tvBackup.SelectedNode != null)
            {
                _isUpdatingChecks = true;
                try
                {
                    tvBackup.SelectedNode.Checked = isChecked;
                    SetChildNodesChecked(tvBackup.SelectedNode, isChecked);
                    UpdateParentNodeChecked(tvBackup.SelectedNode);
                }
                finally
                {
                    _isUpdatingChecks = false;
                }
                UpdateSelectionStatistics();
            }
        }

        private void CopySelectedId()
        {
            if (tvBackup.SelectedNode != null)
            {
                DiffItemNodeTag tag = tvBackup.SelectedNode.Tag as DiffItemNodeTag;
                if (tag != null && !string.IsNullOrEmpty(tag.Id))
                {
                    Clipboard.SetText(tag.Id);
                    Log("已复制 ID 到剪贴板: " + tag.Id, Color.DarkSlateGray);
                }
            }
        }

        private void UpdateSelectionStatistics()
        {
            int selProjects = 0;
            int selConvs = 0;
            bool selRules = false;
            bool selSkills = false;
            bool selMcp = false;

            CountCheckedItemsRecursive(tvBackup.Nodes, ref selProjects, ref selConvs, ref selRules, ref selSkills, ref selMcp);

            string backupState = (_backupIndex != null) ? Path.GetFileName(_loadedBackupPath) : "未加载";
            int localMain = 0;
            for (int i = 0; i < _localSummaries.Count; i++) if (!_localSummaries[i].IsSubagent) localMain++;

            lblStatsSummary.Text = string.Format(
                "本地: {0} 个工程 ({1} 条活跃主会话) | 备份包: {2} | 待合流选中: {3} 个工程 ({4} 条会话) {5}",
                _localProjects.Count, localMain, backupState, selProjects, selConvs,
                (selRules || selSkills || selMcp) ? "[含全局配置]" : ""
            );
        }

        private void CountCheckedItemsRecursive(TreeNodeCollection nodes, ref int projCount, ref int convCount, 
                                                ref bool hasRules, ref bool hasSkills, ref bool hasMcp)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode n = nodes[i];
                DiffItemNodeTag tag = n.Tag as DiffItemNodeTag;
                if (tag != null && n.Checked)
                {
                    if (tag.Category == ItemCategory.Project) projCount++;
                    else if (tag.Category == ItemCategory.Conversation) convCount++;
                    else if (tag.Category == ItemCategory.GlobalRules) hasRules = true;
                    else if (tag.Category == ItemCategory.GlobalSkills) hasSkills = true;
                    else if (tag.Category == ItemCategory.GlobalMcp) hasMcp = true;
                }
                if (n.Nodes.Count > 0)
                {
                    CountCheckedItemsRecursive(n.Nodes, ref projCount, ref convCount, ref hasRules, ref hasSkills, ref hasMcp);
                }
            }
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            RenderLocalTree();
            if (_backupIndex != null) RenderBackupTree();
        }

        // ══════════════════════════════════════════════════════════
        // 细粒度选择性导入执行 (Milestone 3 — F10, F11)
        // ══════════════════════════════════════════════════════════
        private void BtnPerformSelectiveImport_Click(object sender, EventArgs e)
        {
            if (_backupIndex == null || string.IsNullOrEmpty(_loadedBackupPath)) return;

            List<ConversationSummaryInfo> selectedConvs = new List<ConversationSummaryInfo>();
            HashSet<string> selectedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool importRules = false;
            bool importSkills = false;
            bool importMcp = false;

            CollectSelectedItems(tvBackup.Nodes, selectedConvs, selectedProjects, ref importRules, ref importSkills, ref importMcp);

            if (selectedConvs.Count == 0 && !importRules && !importSkills && !importMcp)
            {
                MessageBox.Show("未勾选任何需要导入的会话或配置项！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!EnsureAntigravityClosed(true)) return;

            // 冲突检测与解决向导
            Dictionary<string, string> projectUriOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ConflictChoice? globalStrategy = null;

            foreach (string pName in selectedProjects)
            {
                ProjectDiffNode pDiff = _currentDiffNodes.Find(x => string.Equals(x.ProjectName, pName, StringComparison.OrdinalIgnoreCase));
                if (pDiff != null && pDiff.Status == DiffStatus.Conflicted)
                {
                    ConflictChoice choice;
                    string targetUri = pDiff.LocalFolderUri;
                    string targetName = pName;

                    if (globalStrategy.HasValue)
                    {
                        choice = globalStrategy.Value;
                    }
                    else
                    {
                        using (ConflictResolutionDialog dlg = new ConflictResolutionDialog(pName, pDiff.LocalFolderUri, pDiff.BackupFolderUri))
                        {
                            if (dlg.ShowDialog(this) != DialogResult.OK)
                            {
                                Log("用户取消了冲突处理向导，导入操作中止", Color.DarkOrange);
                                return;
                            }
                            choice = dlg.Result.Choice;
                            if (dlg.Result.ApplyToAll) globalStrategy = choice;
                            if (choice == ConflictChoice.Rename && !string.IsNullOrEmpty(dlg.Result.NewProjectName))
                            {
                                targetName = dlg.Result.NewProjectName;
                            }
                        }
                    }

                    if (choice == ConflictChoice.Skip)
                    {
                        selectedConvs.RemoveAll(x => string.Equals(x.ProjectName, pName, StringComparison.OrdinalIgnoreCase));
                        continue;
                    }
                    else if (choice == ConflictChoice.Rename)
                    {
                        projectUriOverrides[pName] = pDiff.BackupFolderUri;
                    }
                }
            }

            _highlightTracker.Clear();
            ProgressForm progress = new ProgressForm("正在执行细粒度增量合流...", "正在从备份包中安全提取所选会话与脑区索引...");

            _worker = new BackgroundWorker();
            _worker.DoWork += (ws, we) =>
            {
                string gemini = txtGeminiPath.Text.Trim();
                string srcHome = _backupIndex.SourceHome;
                string curHome = HomePath;
                int filesImported = 0;

                using (FileStream fs = new FileStream(_loadedBackupPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    // 1. 提取选中的会话数据库与脑区
                    HashSet<string> convIdsToExtract = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < selectedConvs.Count; i++)
                    {
                        convIdsToExtract.Add(selectedConvs[i].ConversationId);
                        _highlightTracker.AddImportedConversation(selectedConvs[i].ConversationId);
                        _highlightTracker.AddImportedProject(selectedConvs[i].ProjectName);
                    }

                    for (int i = 0; i < archive.Entries.Count; i++)
                    {
                        ZipArchiveEntry entry = archive.Entries[i];
                        string norm = entry.FullName.Replace('\\', '/');

                        // 对话数据库
                        if (norm.Contains("antigravity/conversations/") && norm.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                        {
                            string cid = Path.GetFileNameWithoutExtension(norm);
                            if (convIdsToExtract.Contains(cid))
                            {
                                string dest = Path.Combine(gemini, "antigravity", "conversations", cid + ".db");
                                ExtractZipEntrySafe(entry, dest);
                                filesImported++;
                            }
                        }
                        // 脑区文件夹
                        else if (norm.Contains("antigravity/brain/"))
                        {
                            foreach (string cid in convIdsToExtract)
                            {
                                if (norm.Contains("antigravity/brain/" + cid + "/"))
                                {
                                    int idx = norm.IndexOf("antigravity/brain/" + cid);
                                    string rel = norm.Substring(idx);
                                    string dest = Path.Combine(gemini, rel.Replace('/', '\\'));
                                    ExtractZipEntrySafe(entry, dest);
                                    filesImported++;
                                    break;
                                }
                            }
                        }
                        // 全局规则
                        else if (importRules && (norm.Contains("rules/") || norm.EndsWith(".geminirules") || norm.EndsWith("GEMINI.md")))
                        {
                            int idx = norm.IndexOf("data/");
                            string rel = idx >= 0 ? norm.Substring(idx + 5) : norm;
                            string dest = Path.Combine(gemini, rel.Replace('/', '\\'));
                            ExtractZipEntrySafe(entry, dest);
                            filesImported++;
                        }
                        // 全局技能
                        else if (importSkills && norm.Contains("skills/"))
                        {
                            int idx = norm.IndexOf("data/");
                            string rel = idx >= 0 ? norm.Substring(idx + 5) : norm;
                            string dest = Path.Combine(gemini, rel.Replace('/', '\\'));
                            ExtractZipEntrySafe(entry, dest);
                            filesImported++;
                        }
                        // 全局 MCP
                        else if (importMcp && (norm.Contains("mcp/") || norm.EndsWith("mcp_config.json")))
                        {
                            int idx = norm.IndexOf("data/");
                            string rel = idx >= 0 ? norm.Substring(idx + 5) : norm;
                            string dest = Path.Combine(gemini, rel.Replace('/', '\\'));
                            ExtractZipEntrySafe(entry, dest);
                            filesImported++;
                        }
                    }

                    // 2. 合流 agyhub_summaries_proto.pb
                    progress.UpdateMsg("正在增量合流会话摘要与自定义标题索引 (.pb)...");
                    string localPb = Path.Combine(gemini, "antigravity", "agyhub_summaries_proto.pb");
                    byte[] localPbBytes = File.Exists(localPb) ? File.ReadAllBytes(localPb) : new byte[0];

                    byte[] mergedPb = IncrementalProtoShardMerger.MergeShards(
                        localPbBytes, selectedConvs, srcHome, curHome, projectUriOverrides
                    );

                    if (File.Exists(localPb))
                    {
                        string bak = localPb + ".bak";
                        if (File.Exists(bak)) File.Delete(bak);
                        File.Copy(localPb, bak);
                    }

                    File.WriteAllBytes(localPb, mergedPb);
                    filesImported++;
                }

                we.Result = filesImported;
            };

            _worker.RunWorkerCompleted += (ws, we) =>
            {
                progress.Close();
                if (we.Error != null)
                {
                    Log("导入失败: " + we.Error.Message, Color.Red);
                    MessageBox.Show("导入失败:\n" + we.Error.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    Log(string.Format("===== 细粒度选择性导入成功！共写入 {0} 个文件与索引分片 =====", we.Result), Color.DarkGreen);
                    MessageBox.Show(string.Format("选择性导入完成！\n共安全合流 {0} 个文件分片\n新导入的工程与会话已在左侧树中用粗体绿色标出。", we.Result), 
                                    "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DetectPaths();
                }
            };

            _worker.RunWorkerAsync();
            progress.ShowDialog(this);
        }

        private static void ExtractZipEntrySafe(ZipArchiveEntry entry, string destPath)
        {
            if (string.IsNullOrEmpty(entry.Name)) return; // 目录项
            string dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            using (Stream s = entry.Open())
            using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                s.CopyTo(fs);
            }
        }

        private void CollectSelectedItems(TreeNodeCollection nodes, List<ConversationSummaryInfo> convList, 
                                          HashSet<string> projSet, ref bool hasRules, ref bool hasSkills, ref bool hasMcp)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode n = nodes[i];
                DiffItemNodeTag tag = n.Tag as DiffItemNodeTag;
                if (tag != null && n.Checked)
                {
                    if (tag.Category == ItemCategory.Conversation && tag.SummaryInfo != null)
                    {
                        convList.Add(tag.SummaryInfo);
                        if (!string.IsNullOrEmpty(tag.ProjectName)) projSet.Add(tag.ProjectName);
                    }
                    else if (tag.Category == ItemCategory.Project && !string.IsNullOrEmpty(tag.ProjectName))
                    {
                        projSet.Add(tag.ProjectName);
                    }
                    else if (tag.Category == ItemCategory.GlobalRules) hasRules = true;
                    else if (tag.Category == ItemCategory.GlobalSkills) hasSkills = true;
                    else if (tag.Category == ItemCategory.GlobalMcp) hasMcp = true;
                }
                if (n.Nodes.Count > 0)
                {
                    CollectSelectedItems(n.Nodes, convList, projSet, ref hasRules, ref hasSkills, ref hasMcp);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        // 一键全量备份导出 (Create Full Backup)
        // ══════════════════════════════════════════════════════════
        private void BtnFullBackup_Click(object sender, EventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog
            {
                Filter = "Antigravity 备份包 (*.agy)|*.agy|ZIP 压缩包 (*.zip)|*.zip",
                FileName = string.Format("antigravity_backup_{0}.agy", DateTime.Now.ToString("yyyyMMdd_HHmmss")),
                Title = "请选择全量备份保存路径"
            };

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
                    // 1. metadata.json
                    ZipArchiveEntry metaEntry = archive.CreateEntry("metadata.json", CompressionLevel.Fastest);
                    using (StreamWriter sw = new StreamWriter(metaEntry.Open(), Encoding.UTF8))
                    {
                        string meta = "{\r\n  \"source_home\": \"" + HomePath.Replace("\\", "\\\\") + "\",\r\n" +
                                      "  \"source_user\": \"" + Environment.UserName + "\",\r\n" +
                                      "  \"backup_time\": \"" + DateTime.Now.ToString("o") + "\",\r\n" +
                                      "  \"version\": \"0.6\"\r\n}";
                        sw.Write(meta);
                    }
                    n++;

                    // 2. 对话与脑区 (白名单定向归档有效会话，彻底排除已删除死数据库)
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

                    string convDir = Path.Combine(gemini, "antigravity", "conversations");
                    string brainDir = Path.Combine(gemini, "antigravity", "brain");

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

                    n += AddDirToArchive(archive, Path.Combine(gemini, "antigravity", "annotations"), "data/antigravity/annotations");
                    n += AddFileToArchive(archive, Path.Combine(gemini, "antigravity", "agyhub_summaries_proto.pb"), "data/antigravity/agyhub_summaries_proto.pb");
                    n += AddFileToArchive(archive, Path.Combine(gemini, "antigravity", "antigravity_state.pbtxt"), "data/antigravity/antigravity_state.pbtxt");
                    n += AddFileToArchive(archive, Path.Combine(gemini, "antigravity", "installation_id"), "data/antigravity/installation_id");
                    n += AddDirToArchive(archive, Path.Combine(gemini, "antigravity", "knowledge"), "data/antigravity/knowledge");

                    // 3. 规则与内置技能
                    progressDlg.UpdateMsg("正在归档规则与规范...");
                    n += AddFileToArchive(archive, Path.Combine(gemini, ".geminirules"), "data/.geminirules");
                    n += AddFileToArchive(archive, Path.Combine(gemini, "GEMINI.md"), "data/GEMINI.md");
                    n += AddFileToArchive(archive, Path.Combine(gemini, "AGENTS.md"), "data/AGENTS.md");
                    n += AddDirToArchive(archive, Path.Combine(gemini, "antigravity", "builtin", "skills"), "data/antigravity/builtin/skills");

                    // 4. 完整 config 目录
                    progressDlg.UpdateMsg("正在归档项目定义、自定义技能与全局配置...");
                    n += AddDirToArchive(archive, Path.Combine(gemini, "config"), "data/config");

                    // 5. MCP 与客户端配置
                    n += AddDirToArchive(archive, Path.Combine(gemini, "antigravity", "mcp"), "data/antigravity/mcp");
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

        private static int AddFileToArchive(ZipArchive archive, string filePath, string entryName)
        {
            if (!File.Exists(filePath)) return 0;
            try
            {
                ZipArchiveEntry entry = archive.CreateEntry(entryName.Replace('\\', '/'), CompressionLevel.Fastest);
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Stream s = entry.Open())
                {
                    fs.CopyTo(s);
                }
                return 1;
            }
            catch { return 0; }
        }

        private static int AddDirToArchive(ZipArchive archive, string dirPath, string baseEntryDir)
        {
            if (!Directory.Exists(dirPath)) return 0;
            int count = 0;
            try
            {
                string[] files = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    string f = files[i];
                    string rel = f.Substring(dirPath.Length).TrimStart('\\', '/');
                    string entryName = baseEntryDir.TrimEnd('/') + "/" + rel.Replace('\\', '/');
                    count += AddFileToArchive(archive, f, entryName);
                }
            }
            catch {}
            return count;
        }

        // ══════════════════════════════════════════════════════════
        // 配置浏览器与热编辑器 (Config Browser & Editor)
        // ══════════════════════════════════════════════════════════
        private void RefreshConfigBrowser()
        {
            lbConfigFiles.Items.Clear();
            string gemini = txtGeminiPath.Text.Trim();
            List<ScanRoot> roots = ConfigBrowserScanner.DetectScanRoots(gemini, _localProjects);

            List<BrowserItem> rules = ConfigBrowserScanner.ScanRules(roots);
            List<BrowserItem> skills = ConfigBrowserScanner.ScanSkills(roots);
            List<BrowserItem> mcps = ConfigBrowserScanner.ScanMcp(roots);

            for (int i = 0; i < rules.Count; i++) lbConfigFiles.Items.Add(rules[i]);
            for (int i = 0; i < skills.Count; i++) lbConfigFiles.Items.Add(skills[i]);
            for (int i = 0; i < mcps.Count; i++) lbConfigFiles.Items.Add(mcps[i]);

            if (lbConfigFiles.Items.Count > 0) lbConfigFiles.SelectedIndex = 0;
        }

        private void LbConfigFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            BrowserItem item = lbConfigFiles.SelectedItem as BrowserItem;
            if (item != null && File.Exists(item.FilePath))
            {
                try
                {
                    rtbConfigEditor.Text = File.ReadAllText(item.FilePath, Encoding.UTF8);
                    lblEditingFile.Text = "当前编辑: " + item.FilePath;
                }
                catch (Exception ex)
                {
                    rtbConfigEditor.Text = "读取文件失败: " + ex.Message;
                }
            }
        }

        private void BtnSaveConfig_Click(object sender, EventArgs e)
        {
            BrowserItem item = lbConfigFiles.SelectedItem as BrowserItem;
            if (item != null && !string.IsNullOrEmpty(item.FilePath))
            {
                try
                {
                    File.WriteAllText(item.FilePath, rtbConfigEditor.Text, Encoding.UTF8);
                    Log("配置文件保存成功: " + item.FilePath, Color.DarkGreen);
                    MessageBox.Show("配置文件已保存！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Log("保存配置文件失败: " + ex.Message, Color.Red);
                    MessageBox.Show("保存失败:\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        // 辅助方法与 UI 样式工具
        // ══════════════════════════════════════════════════════════
        private void Log(string msg, Color col)
        {
            if (rtbLog.InvokeRequired)
            {
                rtbLog.Invoke(new Action(() => Log(msg, col)));
                return;
            }
            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.SelectionLength = 0;
            rtbLog.SelectionColor = col;
            rtbLog.AppendText(string.Format("[{0}] {1}\r\n", DateTime.Now.ToString("HH:mm:ss"), msg));
            rtbLog.ScrollToCaret();
        }

        private static Button MakeBtn(string text, int width, Color backColor, Color foreColor)
        {
            Button btn = new Button
            {
                Text = text,
                Width = width,
                Height = 32,
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private static Button MakeSmallBtn(string text)
        {
            Button btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 26,
                BackColor = Color.FromArgb(240, 240, 240),
                ForeColor = Color.FromArgb(32, 33, 36),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 8.5f),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(218, 220, 224);
            return btn;
        }

        private bool EnsureAntigravityClosed(bool promptUser)
        {
            string[] targetProcessNames = new string[] {
                "Antigravity", "Antigravity IDE", "antigravity", "antigravity-hans-windows-amd64", "agy-node"
            };

            List<Process> running = new List<Process>();
            foreach (string name in targetProcessNames)
            {
                Process[] procs = Process.GetProcessesByName(name);
                if (procs != null && procs.Length > 0) running.AddRange(procs);
            }

            if (running.Count > 0)
            {
                if (promptUser)
                {
                    DialogResult res = MessageBox.Show(
                        "检测到 Antigravity 正在运行中。为防止数据库与索引写入冲突，是否自动关闭 Antigravity 进程？",
                        "确认关闭", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (res != DialogResult.Yes) return false;
                }

                for (int i = 0; i < running.Count; i++)
                {
                    try { running[i].Kill(); running[i].WaitForExit(3000); }
                    catch {}
                }
            }
            return true;
        }
    }

    /// <summary>
    /// 模态进度指示弹窗 (Progress Dialog)
    /// </summary>
    public class ProgressForm : Form
    {
        private ProgressBar pb;
        private Label lbl;

        public ProgressForm(string title, string initialMsg)
        {
            this.Text = title;
            this.Size = new Size(460, 140);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Microsoft YaHei UI", 9f);
            this.BackColor = Color.White;

            lbl = new Label { Text = initialMsg, Location = new Point(20, 16), Size = new Size(410, 36) };
            pb = new ProgressBar { Location = new Point(20, 56), Size = new Size(405, 22), Style = ProgressBarStyle.Marquee };

            this.Controls.Add(lbl);
            this.Controls.Add(pb);
        }

        public void UpdateMsg(string msg)
        {
            if (lbl.InvokeRequired)
            {
                lbl.Invoke(new Action(() => UpdateMsg(msg)));
                return;
            }
            lbl.Text = msg;
        }
    }
}
