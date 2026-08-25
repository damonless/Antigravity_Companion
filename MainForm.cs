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
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.ThreadException += (s, e) => {
                ShowCrashReport(e.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                ShowCrashReport(e.ExceptionObject as Exception);
            };

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                ShowCrashReport(ex);
            }
        }

        private static void ShowCrashReport(Exception ex)
        {
            if (ex == null) return;
            string msg = string.Format("应用程序启动或运行发生未捕获异常：\n\n{0}\n\n堆栈信息：\n{1}", ex.Message, ex.StackTrace);
            try
            {
                string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                File.WriteAllText(logFile, msg, Encoding.UTF8);
            }
            catch
            {
                try
                {
                    string tempLog = Path.Combine(Path.GetTempPath(), "antigravity_companion_crash.log");
                    File.WriteAllText(tempLog, msg, Encoding.UTF8);
                }
                catch {}
            }
            MessageBox.Show(msg, "启动异常捕获", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            try
            {
                SendMessage(this.Handle, 0x1100 + 44, IntPtr.Zero, (IntPtr)0x0040); // TVS_EX_DOUBLEBUFFER
            }
            catch {}
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

        // ── 持久选择状态集（防止搜索/过滤刷新时勾选丢失） ──
        private readonly HashSet<string> _checkedSessionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _checkedProjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _checkedRules = false;
        private bool _checkedSkills = false;
        private bool _checkedMcp = false;

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

        // 配置浏览器子控件
        private TabControl tabConfigs;
        private SplitContainer splitConfig;
        private ListBox lbRules;
        private ListBox lbSkills;
        private ListBox lbMcp;
        private RichTextBox rtbConfigEditor;
        private Button btnSaveConfig;
        private Button btnOpenConfigFolder;
        private Label lblEditingFile;
        private BrowserItem _currentEditingItem;

        private ContextMenuStrip contextMenuBackup;
        private bool _isUpdatingChecks = false;
        private BackgroundWorker _worker;

        private bool _splitterInitialized = false;

        public MainForm()
        {
            // 自动解析当前机器的用户主目录 (安全跨盘符推导)
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(userProfile))
                userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            if (string.IsNullOrEmpty(userProfile))
            {
                string sysDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                userProfile = Path.Combine(sysDrive, "Users", Environment.UserName);
            }
            HomePath = userProfile;

            BuildUI();
            
            this.Shown += (s, e) => {
                AdjustSplitterLayouts();
                DetectPaths();
            };
        }

        private void AdjustSplitterLayouts()
        {
            if (!_splitterInitialized)
            {
                // 主分栏 50%/50% 对称安全调整
                try
                {
                    if (splitMain != null && splitMain.ClientSize.Width > 200)
                    {
                        int target = (splitMain.ClientSize.Width - splitMain.SplitterWidth) / 2;
                        if (target > 50 && target < splitMain.ClientSize.Width - 50)
                        {
                            splitMain.SplitterDistance = target;
                        }
                    }
                }
                catch {}

                // 配置编辑器分栏自适应
                try
                {
                    if (splitConfig != null && splitConfig.ClientSize.Width > 300)
                    {
                        int target = 360;
                        if (target > 50 && target < splitConfig.ClientSize.Width - 50)
                        {
                            splitConfig.SplitterDistance = target;
                        }
                    }
                }
                catch {}

                _splitterInitialized = true;
            }
        }

        // ══════════════════════════════════════════════════════════
        // 极简素雅现代化 UI 构建 (彻底移除初始化越界陷阱)
        // ══════════════════════════════════════════════════════════
        private void BuildUI()
        {
            this.Text = "Antigravity Companion V0.6 - 数据迁移与细粒度导入管理工具";
            this.Size = new Size(1360, 860);
            this.MinimumSize = new Size(1100, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 250, 252);

            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(iconPath)) this.Icon = new Icon(iconPath);
            }
            catch {}

            // 1. 顶部操作栏与状态看板 (DPI 自适应流式排版)
            Panel pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = Color.White,
                Padding = new Padding(12, 8, 12, 4)
            };
            pnlTop.Paint += (s, pe) =>
            {
                pe.Graphics.DrawLine(new Pen(Color.FromArgb(226, 232, 240)), 0, pnlTop.Height - 1, pnlTop.Width, pnlTop.Height - 1);
            };

            FlowLayoutPanel flowTopControls = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 42,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false,
                BackColor = Color.Transparent
            };

            // 操作按钮栏
            btnDetect = MakeNeutralBtn("重新检测本地", 115);
            btnDetect.Margin = new Padding(0, 4, 8, 4);
            btnDetect.Click += (s, e) => DetectPaths();

            btnOpenBackup = MakeNeutralBtn("打开备份包...", 115);
            btnOpenBackup.Margin = new Padding(0, 4, 8, 4);
            btnOpenBackup.Click += BtnOpenBackup_Click;

            btnFullBackup = MakeNeutralBtn("全量备份导出...", 125);
            btnFullBackup.Margin = new Padding(0, 4, 16, 4);
            btnFullBackup.Click += BtnFullBackup_Click;

            chkHideTemp = new CheckBox
            {
                Text = "过滤临时工程与子代理会话",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(0, 9, 16, 4),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(51, 65, 85)
            };
            chkHideTemp.CheckedChanged += (s, e) =>
            {
                RenderLocalTree();
                if (_backupIndex != null) RenderBackupTree();
            };

            Label lblSearchIcon = new Label
            {
                Text = "搜索:",
                AutoSize = true,
                Margin = new Padding(0, 10, 4, 4),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139)
            };

            txtSearch = new TextBox
            {
                Width = 160,
                Margin = new Padding(0, 7, 16, 4),
                Font = new Font("Segoe UI", 9f),
                BorderStyle = BorderStyle.FixedSingle
            };
            txtSearch.TextChanged += TxtSearch_TextChanged;

            btnImportSelected = MakePrimaryBtn("导入所选项到本地", 150);
            btnImportSelected.Margin = new Padding(0, 4, 8, 4);
            btnImportSelected.Enabled = false;
            btnImportSelected.Click += BtnPerformSelectiveImport_Click;

            txtGeminiPath = new TextBox { Visible = false };

            flowTopControls.Controls.AddRange(new Control[] {
                btnDetect, btnOpenBackup, btnFullBackup, chkHideTemp, 
                lblSearchIcon, txtSearch, btnImportSelected, txtGeminiPath
            });

            // 状态指示条
            lblStatsSummary = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                Text = "本地: 正在扫描... | 备份包: 未加载 | 待合流选中: 0 个工程 (0 条会话)",
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(71, 85, 105),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0)
            };

            pnlTop.Controls.Add(flowTopControls);
            pnlTop.Controls.Add(lblStatsSummary);

            // 2. 主体 SplitContainer (纯净初始化，无内部越界陷阱)
            splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(226, 232, 240)
            };

            // ── 左侧面板 (本地环境) ──
            Panel pnlLeft = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12, 10, 12, 10) };
            Panel pnlLeftHeader = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.FromArgb(248, 250, 252) };
            pnlLeftHeader.Paint += (s, pe) =>
            {
                pe.Graphics.DrawLine(new Pen(Color.FromArgb(226, 232, 240)), 0, pnlLeftHeader.Height - 1, pnlLeftHeader.Width, pnlLeftHeader.Height - 1);
            };

            lblLocalHeader = new Label
            {
                Text = "本地环境 (Local Environment)",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(8, 0, 0, 0)
            };
            lblConvCount = new Label
            {
                Text = "活跃主会话: 0",
                Dock = DockStyle.Right,
                AutoSize = false,
                Width = 160,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(100, 116, 139),
                Padding = new Padding(0, 0, 8, 0)
            };
            pnlLeftHeader.Controls.Add(lblLocalHeader);
            pnlLeftHeader.Controls.Add(lblConvCount);

            tvLocal = new DoubleBufferedTreeView
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f),
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                FullRowSelect = true,
                HideSelection = false
            };
            pnlLeft.Controls.Add(tvLocal);
            pnlLeft.Controls.Add(pnlLeftHeader);
            splitMain.Panel1.Controls.Add(pnlLeft);

            // ── 右侧面板 (备份归档) ──
            Panel pnlRight = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12, 10, 12, 10) };
            Panel pnlRightHeader = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.FromArgb(248, 250, 252) };
            pnlRightHeader.Paint += (s, pe) =>
            {
                pe.Graphics.DrawLine(new Pen(Color.FromArgb(226, 232, 240)), 0, pnlRightHeader.Height - 1, pnlRightHeader.Width, pnlRightHeader.Height - 1);
            };

            lblBackupHeader = new Label
            {
                Text = "备份归档包 (Backup Archive - 未加载)",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(8, 0, 0, 0)
            };

            // 快速选择工具栏
            FlowLayoutPanel pnlQuickSelect = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 4, 8, 0)
            };

            btnSelectNewOnly = MakeSmallBtn("仅选新增");
            btnSelectNewOnly.Click += BtnSelectNewOnly_Click;

            btnSelectAll = MakeSmallBtn("全选");
            btnSelectAll.Click += (s, e) => SetAllBackupChecks(true);

            btnDeselectAll = MakeSmallBtn("全不选");
            btnDeselectAll.Click += (s, e) => SetAllBackupChecks(false);

            pnlQuickSelect.Controls.AddRange(new Control[] { btnSelectNewOnly, btnSelectAll, btnDeselectAll });
            pnlRightHeader.Controls.Add(lblBackupHeader);
            pnlRightHeader.Controls.Add(pnlQuickSelect);

            tvBackup = new DoubleBufferedTreeView
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f),
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
            contextMenuBackup.Items.Add("勾选当前工程下所有会话", null, (s, e) => CheckSelectedNodeCascading(true));
            contextMenuBackup.Items.Add("取消勾选当前工程", null, (s, e) => CheckSelectedNodeCascading(false));
            contextMenuBackup.Items.Add(new ToolStripSeparator());
            contextMenuBackup.Items.Add("复制会话 UUID", null, (s, e) => CopySelectedId());
            tvBackup.ContextMenuStrip = contextMenuBackup;

            pnlRight.Controls.Add(tvBackup);
            pnlRight.Controls.Add(pnlRightHeader);
            splitMain.Panel2.Controls.Add(pnlRight);

            // 3. 底部选项卡 (结构化配置浏览器 & 运行日志)
            tabRightBottom = new TabControl
            {
                Dock = DockStyle.Bottom,
                Height = 240,
                Font = new Font("Segoe UI", 9f)
            };

            // Tab 1: 运行日志
            TabPage tabLog = new TabPage("运行日志 (Log)");
            rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = Color.FromArgb(30, 41, 59),
                Font = new Font("Consolas", 9.5f),
                BorderStyle = BorderStyle.None
            };
            tabLog.Controls.Add(rtbLog);

            // Tab 2: 结构化配置浏览器与热编辑器 (纯净初始化)
            TabPage tabConfig = new TabPage("规则、技能与 MCP 配置 (Config Browser)");
            splitConfig = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BackColor = Color.FromArgb(226, 232, 240)
            };

            // 左侧分类子选项卡
            tabConfigs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5f) };
            
            TabPage tabRules = new TabPage("规则规范 (Rules)");
            lbRules = CreateConfigListBox();
            lbRules.SelectedIndexChanged += (s, e) => LoadSelectedItem(lbRules);
            tabRules.Controls.Add(lbRules);

            TabPage tabSkills = new TabPage("技能插件 (Skills)");
            lbSkills = CreateConfigListBox();
            lbSkills.SelectedIndexChanged += (s, e) => LoadSelectedItem(lbSkills);
            tabSkills.Controls.Add(lbSkills);

            TabPage tabMcp = new TabPage("MCP 配置 (MCP)");
            lbMcp = CreateConfigListBox();
            lbMcp.SelectedIndexChanged += (s, e) => LoadSelectedItem(lbMcp);
            tabMcp.Controls.Add(lbMcp);

            tabConfigs.TabPages.AddRange(new TabPage[] { tabRules, tabSkills, tabMcp });
            splitConfig.Panel1.Controls.Add(tabConfigs);

            // 右侧编辑器
            Panel pnlEditor = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            Panel pnlEditorTop = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.FromArgb(241, 245, 249), Padding = new Padding(4) };
            
            lblEditingFile = new Label { 
                Text = "请从左侧选择配置文件查看或编辑 (支持 Ctrl+S 热保存)", 
                Dock = DockStyle.Fill, 
                TextAlign = ContentAlignment.MiddleLeft, 
                Padding = new Padding(6, 0, 0, 0),
                ForeColor = Color.FromArgb(51, 65, 85)
            };

            FlowLayoutPanel pnlEditorBtns = new FlowLayoutPanel {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            btnOpenConfigFolder = MakeSmallBtn("打开目录");
            btnOpenConfigFolder.Click += BtnOpenConfigFolder_Click;

            btnSaveConfig = MakeSmallBtn("保存修改 (Ctrl+S)");
            btnSaveConfig.Click += BtnSaveConfig_Click;

            pnlEditorBtns.Controls.AddRange(new Control[] { btnOpenConfigFolder, btnSaveConfig });
            pnlEditorTop.Controls.Add(lblEditingFile);
            pnlEditorTop.Controls.Add(pnlEditorBtns);

            rtbConfigEditor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.None,
                AcceptsTab = true,
                BackColor = Color.White
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

        private static ListBox CreateConfigListBox()
        {
            return new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f),
                BackColor = Color.FromArgb(248, 250, 252)
            };
        }

        // ══════════════════════════════════════════════════════════
        // 本地环境探测与解析 (Local Detection)
        // ══════════════════════════════════════════════════════════
        private void DetectPaths()
        {
            try
            {
                string gemini = Path.Combine(HomePath, ".gemini");
                txtGeminiPath.Text = gemini;

                _localProjects.Clear();
                _localSummaries.Clear();

                Log("正在探测本地 Antigravity 工作区环境: " + gemini, Color.FromArgb(71, 85, 105));

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
                        Log("读取本地 summaries proto 异常: " + ex.Message, Color.FromArgb(217, 119, 6));
                    }
                }

                RenderLocalTree();
                RefreshConfigBrowser();
                UpdateSelectionStatistics();
            }
            catch (Exception ex)
            {
                Log("本地扫描异常: " + ex.Message, Color.FromArgb(220, 38, 38));
            }
        }

        private void RenderLocalTree()
        {
            tvLocal.BeginUpdate();
            int visibleProjects = 0;
            int visibleConvsCount = 0;

            try
            {
                tvLocal.Nodes.Clear();
                string gemini = txtGeminiPath.Text.Trim();
                bool hideTemp = chkHideTemp.Checked;
                string query = txtSearch.Text.Trim();

                // ── 1. 全局配置 Root ──
                TreeNode rootConfig = new TreeNode("全局配置 (Global Config)");
                rootConfig.Tag = new DiffItemNodeTag { Category = ItemCategory.GlobalConfigRoot, DisplayName = "全局配置" };

                int rulesCount = 0;
                if (File.Exists(Path.Combine(gemini, ".geminirules"))) rulesCount++;
                if (File.Exists(Path.Combine(gemini, "GEMINI.md"))) rulesCount++;
                string rulesDir = Path.Combine(gemini, "config", "rules");
                if (Directory.Exists(rulesDir)) rulesCount += Directory.GetFiles(rulesDir, "*.md", SearchOption.AllDirectories).Length;

                TreeNode nodeRules = new TreeNode(string.Format("规则与规范 ({0} 项)", rulesCount));
                nodeRules.Tag = new DiffItemNodeTag { Category = ItemCategory.GlobalRules, DisplayName = "规则与规范", SourcePath = gemini };
                rootConfig.Nodes.Add(nodeRules);

                int skillsCount = 0;
                string bSkills = Path.Combine(gemini, "antigravity", "builtin", "skills");
                if (Directory.Exists(bSkills)) skillsCount += Directory.GetDirectories(bSkills).Length;
                string uSkills = Path.Combine(gemini, "config", "skills");
                if (Directory.Exists(uSkills)) skillsCount += Directory.GetDirectories(uSkills).Length;

                TreeNode nodeSkills = new TreeNode(string.Format("内置与用户技能 ({0} 项)", skillsCount));
                nodeSkills.Tag = new DiffItemNodeTag { Category = ItemCategory.GlobalSkills, DisplayName = "内置与用户技能", SourcePath = uSkills };
                rootConfig.Nodes.Add(nodeSkills);

                int mcpCount = 0;
                string mcpCfg = Path.Combine(gemini, "config", "mcp_config.json");
                if (File.Exists(mcpCfg)) mcpCount++;
                string mcpDir = Path.Combine(gemini, "antigravity", "mcp");
                if (Directory.Exists(mcpDir)) mcpCount += Directory.GetDirectories(mcpDir).Length;

                TreeNode nodeMcp = new TreeNode(string.Format("MCP 服务器与配置 ({0} 项)", mcpCount));
                nodeMcp.Tag = new DiffItemNodeTag { Category = ItemCategory.GlobalMcp, DisplayName = "MCP 服务器与配置", SourcePath = mcpDir };
                rootConfig.Nodes.Add(nodeMcp);

                tvLocal.Nodes.Add(rootConfig);

                // ── 2. 研发工程与会话 Root ──
                TreeNode rootProj = new TreeNode("研发工程与会话 (Projects & Conversations)");
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

                    visibleProjects++;
                    visibleConvsCount += visibleList.Count;

                    string pNodeText = string.Format("{0} ({1} 条)", pName, visibleList.Count);
                    if (isJunk) pNodeText = "[临时] " + pNodeText;

                    TreeNode pNode = new TreeNode(pNodeText);
                    pNode.Tag = new DiffItemNodeTag { Category = ItemCategory.Project, DisplayName = pName, ProjectName = pName, IsTemporaryOrJunk = isJunk };

                    if (isJunk)
                    {
                        pNode.ForeColor = Color.FromArgb(148, 163, 184);
                    }
                    else if (_highlightTracker.IsProjectHighlighted(pName))
                    {
                        pNode.ForeColor = Color.FromArgb(22, 101, 52);
                        pNode.NodeFont = new Font(tvLocal.Font, FontStyle.Bold);
                    }

                    for (int j = 0; j < visibleList.Count; j++)
                    {
                        ConversationSummaryInfo s = visibleList[j];
                        string timeStr = s.UpdatedTime > DateTime.MinValue ? s.UpdatedTime.ToString("MM-dd HH:mm") : "-";
                        string sNodeText = string.Format("{0} [{1}]", s.DisplayTitle, timeStr);
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
                            sNode.ForeColor = Color.FromArgb(148, 163, 184);
                        }
                        else if (_highlightTracker.IsConversationHighlighted(s.ConversationId))
                        {
                            sNode.ForeColor = Color.FromArgb(22, 101, 52);
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

            lblConvCount.Text = "活跃主会话: " + visibleConvsCount;
            Log(string.Format("本地环境扫描完成：共呈现 {0} 个工程，{1} 条活跃主会话", visibleProjects, visibleConvsCount), Color.FromArgb(22, 101, 52));
        }

        // ══════════════════════════════════════════════════════════
        // 打开并解析备份包 (.agy)
        // ══════════════════════════════════════════════════════════
        private void BtnOpenBackup_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Antigravity 备份包 (*.agy)|*.agy|ZIP 压缩包 (*.zip)|*.zip|所有文件 (*.*)|*.*",
                Title = "请选择 Antigravity 备份归档文件"
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
            Log("正在流式探测备份包元数据: " + Path.GetFileName(archivePath) + "...", Color.FromArgb(71, 85, 105));
            this.Cursor = Cursors.WaitCursor;

            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                _backupIndex = StreamingZipIndexReader.ReadIndex(archivePath);
                sw.Stop();

                _currentDiffNodes = VisualDiffEngine.ComputeDiff(_localSummaries, _backupIndex.Conversations);
                
                // 默认仅选中新增会话
                _checkedSessionIds.Clear();
                _checkedProjectNames.Clear();
                for (int i = 0; i < _backupIndex.Conversations.Count; i++)
                {
                    var c = _backupIndex.Conversations[i];
                    bool exists = _localSummaries.Exists(x => string.Equals(x.ConversationId, c.ConversationId, StringComparison.OrdinalIgnoreCase));
                    if (!exists && !c.IsSubagent)
                    {
                        _checkedSessionIds.Add(c.ConversationId);
                    }
                }

                RenderBackupTree();

                lblBackupHeader.Text = "备份归档包: " + Path.GetFileName(archivePath);
                btnImportSelected.Enabled = true;

                Log(string.Format("备份包解析完成！共发现 {0} 个工程，{1} 条会话，耗时 {2}ms",
                    _backupIndex.Projects.Count, _backupIndex.Conversations.Count, sw.ElapsedMilliseconds), Color.FromArgb(22, 101, 52));
            }
            catch (Exception ex)
            {
                Log("解析备份包失败: " + ex.Message, Color.FromArgb(220, 38, 38));
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
                TreeNode rootConfig = new TreeNode("备份全局配置 (Global Config)");
                rootConfig.Tag = new DiffItemNodeTag { Category = ItemCategory.GlobalConfigRoot, DisplayName = "备份全局配置" };

                TreeNode nodeRules = new TreeNode("规则与规范 (.geminirules, GEMINI.md)");
                nodeRules.Tag = new DiffItemNodeTag { Category = ItemCategory.GlobalRules, DisplayName = "规则与规范", Status = DiffStatus.Aligned };
                nodeRules.Checked = _checkedRules;
                rootConfig.Nodes.Add(nodeRules);

                TreeNode nodeSkills = new TreeNode("内置与用户技能 (Skills)");
                nodeSkills.Tag = new DiffItemNodeTag { Category = ItemCategory.GlobalSkills, DisplayName = "内置与用户技能", Status = DiffStatus.Aligned };
                nodeSkills.Checked = _checkedSkills;
                rootConfig.Nodes.Add(nodeSkills);

                TreeNode nodeMcp = new TreeNode("MCP 服务器与配置 (mcp_config.json)");
                nodeMcp.Tag = new DiffItemNodeTag { Category = ItemCategory.GlobalMcp, DisplayName = "MCP 服务器与配置", Status = DiffStatus.Aligned };
                nodeMcp.Checked = _checkedMcp;
                rootConfig.Nodes.Add(nodeMcp);

                tvBackup.Nodes.Add(rootConfig);

                // ── 2. 研发工程与会话 Root ──
                TreeNode rootProj = new TreeNode("备份研发工程与会话 (Projects & Conversations)");
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
                    string pText = string.Format("{0} ({1} 条) {2}", pDiff.ProjectName, visibleConvs.Count, badge);
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

                    bool anyChildChecked = false;

                    for (int j = 0; j < visibleConvs.Count; j++)
                    {
                        ConversationSummaryInfo s = visibleConvs[j];
                        bool existsLocal = !string.IsNullOrEmpty(s.ConversationId) && localConvIdSet.Contains(s.ConversationId);
                        DiffStatus sStatus = existsLocal ? DiffStatus.Aligned : DiffStatus.NewInBackup;

                        string sBadge = (sStatus == DiffStatus.NewInBackup) ? "[新增]" : "[已对齐]";
                        string timeStr = s.UpdatedTime > DateTime.MinValue ? s.UpdatedTime.ToString("MM-dd HH:mm") : "-";
                        string sText = string.Format("{0} [{1}] {2}", s.DisplayTitle, timeStr, sBadge);
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

                        // 依据持久状态恢复勾选
                        bool isChecked = _checkedSessionIds.Contains(s.ConversationId);
                        sNode.Checked = isChecked;
                        if (isChecked) anyChildChecked = true;

                        ApplyDiffStyle(sNode, sStatus, s.IsSubagent);
                        pNode.Nodes.Add(sNode);
                    }

                    pNode.Checked = (visibleConvs.Count > 0) ? anyChildChecked : _checkedProjectNames.Contains(pDiff.ProjectName);
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
                node.ForeColor = Color.FromArgb(148, 163, 184);
                return;
            }

            switch (status)
            {
                case DiffStatus.NewInBackup:
                    node.ForeColor = Color.FromArgb(22, 101, 52); // 素雅墨绿
                    node.NodeFont = new Font(tvBackup.Font, FontStyle.Bold);
                    break;
                case DiffStatus.Aligned:
                    node.ForeColor = Color.FromArgb(71, 85, 105);  // 中性石板灰
                    node.NodeFont = new Font(tvBackup.Font, FontStyle.Regular);
                    break;
                case DiffStatus.Conflicted:
                    node.ForeColor = Color.FromArgb(194, 65, 12);  // 柔和暗橙
                    node.NodeFont = new Font(tvBackup.Font, FontStyle.Bold);
                    break;
                case DiffStatus.LocalOnly:
                    node.ForeColor = Color.FromArgb(37, 99, 235);  // 经典蓝
                    node.NodeFont = new Font(tvBackup.Font, FontStyle.Regular);
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════
        // 复选框级联选中与状态持久化
        // ══════════════════════════════════════════════════════════
        private void TvBackup_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_isUpdatingChecks) return;
            _isUpdatingChecks = true;
            try
            {
                SetChildNodesChecked(e.Node, e.Node.Checked);
                UpdateParentNodeChecked(e.Node);
                SyncCheckedStateToMemory();
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

        private void SyncCheckedStateToMemory()
        {
            SyncCheckedRecursive(tvBackup.Nodes);
        }

        private void SyncCheckedRecursive(TreeNodeCollection nodes)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode n = nodes[i];
                DiffItemNodeTag tag = n.Tag as DiffItemNodeTag;
                if (tag != null)
                {
                    if (tag.Category == ItemCategory.Conversation && !string.IsNullOrEmpty(tag.Id))
                    {
                        if (n.Checked) _checkedSessionIds.Add(tag.Id);
                        else _checkedSessionIds.Remove(tag.Id);
                    }
                    else if (tag.Category == ItemCategory.Project && !string.IsNullOrEmpty(tag.ProjectName))
                    {
                        if (n.Checked) _checkedProjectNames.Add(tag.ProjectName);
                        else _checkedProjectNames.Remove(tag.ProjectName);
                    }
                    else if (tag.Category == ItemCategory.GlobalRules) _checkedRules = n.Checked;
                    else if (tag.Category == ItemCategory.GlobalSkills) _checkedSkills = n.Checked;
                    else if (tag.Category == ItemCategory.GlobalMcp) _checkedMcp = n.Checked;
                }
                if (n.Nodes.Count > 0)
                {
                    SyncCheckedRecursive(n.Nodes);
                }
            }
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
                SyncCheckedStateToMemory();
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
                SyncCheckedStateToMemory();
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
                    SyncCheckedStateToMemory();
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
                    Log("已复制 ID 到剪贴板: " + tag.Id, Color.FromArgb(71, 85, 105));
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
            
            // 计算本地树中当前可见的工程数和会话数
            int visibleProjects = 0;
            int visibleConvs = 0;
            if (tvLocal.Nodes.Count > 1)
            {
                TreeNode projRoot = tvLocal.Nodes[1];
                visibleProjects = projRoot.Nodes.Count;
                for (int i = 0; i < projRoot.Nodes.Count; i++)
                {
                    visibleConvs += projRoot.Nodes[i].Nodes.Count;
                }
            }

            lblStatsSummary.Text = string.Format(
                "本地: {0} 个工程 ({1} 条活跃主会话) | 备份包: {2} | 待合流选中: {3} 个工程 ({4} 条会话) {5}",
                visibleProjects, visibleConvs, backupState, selProjects, selConvs,
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
        // 细粒度选择性导入执行 (包含跨机器路径重映射与工程创建)
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
            Dictionary<string, string> projectNewNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> projectOverwriteNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ConflictChoice? globalStrategy = null;

            foreach (string pName in selectedProjects)
            {
                ProjectDiffNode pDiff = _currentDiffNodes.Find(x => string.Equals(x.ProjectName, pName, StringComparison.OrdinalIgnoreCase));
                if (pDiff != null && pDiff.Status == DiffStatus.Conflicted)
                {
                    ConflictChoice choice;
                    string targetName = pName;

                    if (globalStrategy.HasValue)
                    {
                        choice = globalStrategy.Value;
                        if (choice == ConflictChoice.Rename)
                        {
                            targetName = pName + "_Imported";
                        }
                    }
                    else
                    {
                        using (ConflictResolutionDialog dlg = new ConflictResolutionDialog(pName, pDiff.LocalFolderUri, pDiff.BackupFolderUri))
                        {
                            if (dlg.ShowDialog(this) != DialogResult.OK)
                            {
                                Log("用户取消了冲突处理向导，导入操作中止", Color.FromArgb(217, 119, 6));
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
                        projectNewNames[pName] = targetName;
                    }
                    else if (choice == ConflictChoice.Overwrite)
                    {
                        projectUriOverrides[pName] = pDiff.BackupFolderUri;
                        projectOverwriteNames.Add(pName);
                    }
                }
            }

            _highlightTracker.Clear();
            ProgressForm progress = new ProgressForm("正在导入选中项目...", "正在从备份包中安全提取所选会话与脑区索引...");

            _worker = new BackgroundWorker();
            _worker.DoWork += (ws, we) =>
            {
                string gemini = txtGeminiPath.Text.Trim();
                string srcHome = _backupIndex.SourceHome;
                string curHome = HomePath;
                int filesImported = 0;

                try
                {
                    // 确保基础目录存在 (跨机器全新环境自动初始化)
                    string convDir = Path.Combine(gemini, "antigravity", "conversations");
                    string brainDir = Path.Combine(gemini, "antigravity", "brain");
                    string projDir = Path.Combine(gemini, "config", "projects");
                    if (!Directory.Exists(convDir)) Directory.CreateDirectory(convDir);
                    if (!Directory.Exists(brainDir)) Directory.CreateDirectory(brainDir);
                    if (!Directory.Exists(projDir)) Directory.CreateDirectory(projDir);

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

                            // 对话数据库及 WAL / SHM 伴随文件
                            if (norm.Contains("antigravity/conversations/"))
                            {
                                string fn = Path.GetFileName(norm);
                                string cid = fn;
                                if (cid.EndsWith(".db-wal", StringComparison.OrdinalIgnoreCase)) cid = cid.Substring(0, cid.Length - 7);
                                else if (cid.EndsWith(".db-shm", StringComparison.OrdinalIgnoreCase)) cid = cid.Substring(0, cid.Length - 7);
                                else if (cid.EndsWith(".db", StringComparison.OrdinalIgnoreCase)) cid = cid.Substring(0, cid.Length - 3);

                                if (convIdsToExtract.Contains(cid))
                                {
                                    string dest = Path.Combine(gemini, "antigravity", "conversations", fn);
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
                            // 工程配置 JSON 同步/提取 (包含跨机器路径重映射与覆盖逻辑)
                            else if (norm.Contains("config/projects/") && norm.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                            {
                                string pId = Path.GetFileNameWithoutExtension(norm);
                                string dest = Path.Combine(gemini, "config", "projects", pId + ".json");
                                
                                using (StreamReader sr = new StreamReader(entry.Open(), Encoding.UTF8))
                                {
                                    string rawJson = sr.ReadToEnd();
                                    string pNameInJson = DirectStreamArchiver.ExtractJsonValue(rawJson, "name");
                                    bool shouldOverwrite = !string.IsNullOrEmpty(pNameInJson) && projectOverwriteNames.Contains(pNameInJson);

                                    if (!File.Exists(dest) || shouldOverwrite)
                                    {
                                        string remappedJson = DirectStreamArchiver.RemapAllPathFormats(rawJson, srcHome, curHome);
                                        File.WriteAllText(dest, remappedJson, Encoding.UTF8);
                                        filesImported++;
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

                        // 2. 针对重命名工程生成合法的 project json 配置
                        foreach (KeyValuePair<string, string> kvp in projectNewNames)
                        {
                            string oldName = kvp.Key;
                            string newName = kvp.Value;
                            string newProjId = Guid.NewGuid().ToString();
                            string newProjFile = Path.Combine(projDir, newProjId + ".json");

                            string folderUri = projectUriOverrides.ContainsKey(oldName) ? projectUriOverrides[oldName] : ("file:///" + HomePath.Replace('\\', '/') + "/" + newName);
                            string jsonContent = string.Format("{{\r\n  \"id\": \"{0}\",\r\n  \"name\": \"{1}\",\r\n  \"folderUri\": \"{2}\"\r\n}}", newProjId, newName, folderUri);
                            File.WriteAllText(newProjFile, jsonContent, Encoding.UTF8);
                            filesImported++;
                        }

                        // 3. 合流 agyhub_summaries_proto.pb
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
                }
                finally
                {
                    // 确保任何情况下不挂起
                }
            };

            _worker.RunWorkerCompleted += (ws, we) =>
            {
                progress.SafeClose();
                if (we.Error != null)
                {
                    Log("导入失败: " + we.Error.Message, Color.FromArgb(220, 38, 38));
                    MessageBox.Show("导入失败:\n" + we.Error.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    Log(string.Format("选择性导入成功！共写入 {0} 个文件与索引分片", we.Result), Color.FromArgb(22, 101, 52));
                    MessageBox.Show(string.Format("选择性导入完成！\n共安全合流 {0} 个文件分片\n新导入的项目已在左侧树中用粗体标出。", we.Result), 
                                    "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DetectPaths();
                }
            };

            _worker.RunWorkerAsync();
            if (_worker.IsBusy)
            {
                progress.ShowDialog(this);
            }
        }

        private static void ExtractZipEntrySafe(ZipArchiveEntry entry, string destPath)
        {
            if (string.IsNullOrEmpty(entry.Name)) return;
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

                    // 2. 对话与脑区 (定向白名单打包有效会话及 WAL/SHM 事务日志，彻底排除已删除死数据库)
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
                        string walFile = Path.Combine(convDir, cid + ".db-wal");
                        if (File.Exists(walFile))
                        {
                            n += AddFileToArchive(archive, walFile, "data/antigravity/conversations/" + cid + ".db-wal");
                        }
                        string shmFile = Path.Combine(convDir, cid + ".db-shm");
                        if (File.Exists(shmFile))
                        {
                            n += AddFileToArchive(archive, shmFile, "data/antigravity/conversations/" + cid + ".db-shm");
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
                    n += AddFileToArchive(archive, Path.Combine(gemini, "installation_id"), "data/antigravity/installation_id");
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
                progressDlg.SafeClose();
                if (we.Error != null)
                {
                    Log("备份失败: " + we.Error.Message, Color.FromArgb(220, 38, 38));
                    MessageBox.Show("备份失败:\n" + we.Error.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    Log("全量备份完成！共打包 " + we.Result + " 个文件 → " + dest, Color.FromArgb(22, 101, 52));
                    MessageBox.Show("备份导出成功！\n共打包 " + we.Result + " 个文件\n路径: " + dest, "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            _worker.RunWorkerAsync();
            if (_worker.IsBusy)
            {
                progressDlg.ShowDialog(this);
            }
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
        // 结构化配置浏览器与热编辑器 (带 .bak 安全备份保护)
        // ══════════════════════════════════════════════════════════
        private void RefreshConfigBrowser()
        {
            lbRules.Items.Clear();
            lbSkills.Items.Clear();
            lbMcp.Items.Clear();

            string gemini = txtGeminiPath.Text.Trim();
            List<ScanRoot> roots = ConfigBrowserScanner.DetectScanRoots(gemini, _localProjects);

            List<BrowserItem> rules = ConfigBrowserScanner.ScanRules(roots);
            List<BrowserItem> skills = ConfigBrowserScanner.ScanSkills(roots);
            List<BrowserItem> mcps = ConfigBrowserScanner.ScanMcp(roots);

            for (int i = 0; i < rules.Count; i++) lbRules.Items.Add(rules[i]);
            for (int i = 0; i < skills.Count; i++) lbSkills.Items.Add(skills[i]);
            for (int i = 0; i < mcps.Count; i++) lbMcp.Items.Add(mcps[i]);

            if (lbRules.Items.Count > 0) lbRules.SelectedIndex = 0;
        }

        private void LoadSelectedItem(ListBox lb)
        {
            BrowserItem item = lb.SelectedItem as BrowserItem;
            if (item != null && File.Exists(item.FilePath))
            {
                _currentEditingItem = item;
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
            if (_currentEditingItem != null && !string.IsNullOrEmpty(_currentEditingItem.FilePath))
            {
                try
                {
                    if (File.Exists(_currentEditingItem.FilePath))
                    {
                        FileAttributes attr = File.GetAttributes(_currentEditingItem.FilePath);
                        if ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            File.SetAttributes(_currentEditingItem.FilePath, attr & ~FileAttributes.ReadOnly);
                        }
                        string bak = _currentEditingItem.FilePath + ".bak";
                        if (File.Exists(bak)) File.Delete(bak);
                        File.Copy(_currentEditingItem.FilePath, bak);
                    }

                    File.WriteAllText(_currentEditingItem.FilePath, rtbConfigEditor.Text, Encoding.UTF8);
                    Log("配置文件保存成功 (已自动备份 .bak): " + _currentEditingItem.FilePath, Color.FromArgb(22, 101, 52));
                    MessageBox.Show("配置文件已安全保存！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Log("保存配置文件失败: " + ex.Message, Color.FromArgb(220, 38, 38));
                    MessageBox.Show("保存失败:\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnOpenConfigFolder_Click(object sender, EventArgs e)
        {
            if (_currentEditingItem != null && !string.IsNullOrEmpty(_currentEditingItem.FilePath) && File.Exists(_currentEditingItem.FilePath))
            {
                try
                {
                    Process.Start("explorer.exe", "/select,\"" + _currentEditingItem.FilePath + "\"");
                }
                catch (Exception ex)
                {
                    Log("打开文件夹失败: " + ex.Message, Color.FromArgb(220, 38, 38));
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        // 辅助方法与素雅 UI 样式工具
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

        private static Button MakeNeutralBtn(string text, int width)
        {
            Button btn = new Button
            {
                Text = text,
                Width = width,
                Height = 30,
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btn.FlatAppearance.BorderSize = 1;
            return btn;
        }

        private static Button MakePrimaryBtn(string text, int width)
        {
            Button btn = new Button
            {
                Text = text,
                Width = width,
                Height = 30,
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
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
                Height = 24,
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = Color.FromArgb(51, 65, 85),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
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

                // 重新检查是否仍有未退出的进程 (例如权限不足)
                List<Process> stillRunning = new List<Process>();
                foreach (string name in targetProcessNames)
                {
                    Process[] procs = Process.GetProcessesByName(name);
                    if (procs != null && procs.Length > 0) stillRunning.AddRange(procs);
                }

                if (stillRunning.Count > 0)
                {
                    MessageBox.Show(
                        "无法完全终止 Antigravity 进程（可能权限不足）。请手动关闭 Antigravity 后再试，以防数据冲突！",
                        "进程占用提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
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
        private bool _isClosed = false;

        public ProgressForm(string title, string initialMsg)
        {
            this.Text = title;
            this.Size = new Size(460, 140);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9f);
            this.BackColor = Color.White;

            lbl = new Label { Text = initialMsg, Location = new Point(20, 16), Size = new Size(410, 36) };
            pb = new ProgressBar { Location = new Point(20, 56), Size = new Size(405, 22), Style = ProgressBarStyle.Marquee };

            this.Controls.Add(lbl);
            this.Controls.Add(pb);

            this.Shown += (s, e) => {
                if (_isClosed)
                {
                    this.Close();
                }
            };
        }

        public void SafeClose()
        {
            _isClosed = true;
            if (this.IsDisposed) return;
            if (this.InvokeRequired)
            {
                try { this.Invoke(new Action(() => this.Close())); }
                catch {}
            }
            else
            {
                this.Close();
            }
        }

        public void UpdateMsg(string msg)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            if (lbl.InvokeRequired)
            {
                try
                {
                    lbl.Invoke(new Action(() => UpdateMsg(msg)));
                }
                catch {}
                return;
            }
            lbl.Text = msg;
        }
    }
}
