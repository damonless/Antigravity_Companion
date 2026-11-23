using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace AntigravityCompanion
{
    public enum RenameProcessStrategy
    {
        Cancel,
        CloseAndFullWrite,
        HotWritePersistentLayers
    }
    static class Program
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.ThreadException += (s, e) => {
                ShowCrashReport(e.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                ShowCrashReport(e.ExceptionObject as Exception);
            };

            bool legacyUi = false;
            bool noBrowser = false;
            string customHome = null;
            string customRepo = null;
            string explicitToken = null;
            int preferredPort = 0;

            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i] ?? string.Empty;
                    if (string.Equals(a, "--legacy-ui", StringComparison.OrdinalIgnoreCase))
                        legacyUi = true;
                    else if (string.Equals(a, "--no-browser", StringComparison.OrdinalIgnoreCase))
                        noBrowser = true;
                    else if (string.Equals(a, "--home", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        customHome = args[++i];
                    else if (string.Equals(a, "--repo", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        customRepo = args[++i];
                    else if (string.Equals(a, "--token", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        explicitToken = args[++i];
                    else if (string.Equals(a, "--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        int p;
                        if (int.TryParse(args[++i], out p) && p > 0) preferredPort = p;
                    }
                }
            }

            try
            {
                if (legacyUi)
                {
                    Application.Run(new MainForm());
                }
                else
                {
                    Environment.ExitCode = WebShellLauncher.Run(
                        customHome,
                        customRepo,
                        preferredPort,
                        noBrowser,
                        explicitToken,
                        null);
                }
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
        private string _geminiPath = string.Empty;
        private string _repositoryPath = string.Empty;
        private List<ConversationSummaryInfo> _localSummaries = new List<ConversationSummaryInfo>();
        private Dictionary<string, ProjectMetadata> _localProjects = new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
        private List<CapabilityItem> _localCapabilities = new List<CapabilityItem>();
        private List<CapabilityItem> _repoCapabilities = new List<CapabilityItem>();
        private BackupArchiveIndex _backupIndex;
        private List<ProjectDiffNode> _currentDiffNodes = new List<ProjectDiffNode>();
        private List<CapabilityDiffNode> _currentCapDiffNodes = new List<CapabilityDiffNode>();
        private string _loadedBackupPath;
        private ViewFilterMode _viewFilter = ViewFilterMode.All;
        private RightPaneSourceMode _rightSourceMode = RightPaneSourceMode.BuiltInRepository;
        private readonly ImportHighlightTracker _highlightTracker = new ImportHighlightTracker();

        // ── 持久选择状态集（防止搜索/过滤刷新时勾选丢失） ──
        private readonly HashSet<string> _checkedSessionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _checkedProjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _checkedBackupCapabilityKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _checkedLocalSessionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _checkedLocalProjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _checkedLocalCapabilityKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ── UI 控件成员 ──
        private DoubleBufferedTreeView tvLocal;
        private DoubleBufferedTreeView tvBackup;
        private ContextMenuStrip ctxLocal;
        private ContextMenuStrip ctxBackup;
        private Label lblLocalHeader;
        private Label lblBackupHeader;
        private Label lblStatsSummary;
        private Label lblConvCount;
        private TextBox txtSearch;
        private CheckBox chkSearchContent;
        private ComboBox cmbViewFilter;
        private CheckBox chkHideTemp;
        private Button btnDetect;
        private Button btnSwitchRepoMode;
        private Button btnOpenBackup;
        private Button btnFullBackup;
        private Button btnImportSelected;
        private Button btnSelectAll;
        private Button btnDeselectAll;
        private Button btnSelectNewOnly;
        private Button btnRepoDeleteSelected;
        private Button btnLocalPushToRepo;
        private Button btnLocalBatchRename;
        private Button btnLocalExportSelected;
        private Button btnLocalDeleteSelected;
        private Button btnLocalOrphanGc;
        private RichTextBox rtbLog;
        private SplitContainer splitMain;

        private int _checkUpdateDepth = 0;
        private bool _isUpdatingChecks
        {
            get { return _checkUpdateDepth > 0; }
            set
            {
                if (value) _checkUpdateDepth++;
                else if (_checkUpdateDepth > 0) _checkUpdateDepth--;
            }
        }
        private BackgroundWorker _worker;
        private Timer _searchDebounceTimer;
        private Font _regularTreeFont;
        private Font _boldTreeFont;
        private Icon _appIcon;
        private bool _splitterInitialized = false;
        private bool _hasInitializedRepoDefaultChecks = false;
        private bool _hasLocalTreeStateCaptured = false;
        private bool _hasBackupTreeStateCaptured = false;
        private ToolStripMenuItem _miLocalBatchHeader;
        private ToolStripMenuItem _miLocalBatchPush;
        private ToolStripMenuItem _miLocalBatchExport;
        private ToolStripMenuItem _miLocalBatchMove;
        private ToolStripMenuItem _miLocalBatchDelete;
        private ToolStripMenuItem _miLocalRename;
        private ToolStripMenuItem _miLocalBatchRename;
        private ToolStripMenuItem _miLocalPreviewCap;
        private ToolStripMenuItem _miLocalPreviewConv;
        private ToolStripMenuItem _miLocalRelocateCap;
        private ToolStripMenuItem _miLocalRelocateProj;
        private ToolStripMenuItem _miLocalExportMd;
        private ToolStripMenuItem _miLocalSinglePush;
        private ToolStripMenuItem _miLocalSingleMove;
        private ToolStripMenuItem _miLocalSingleExportAgy;
        private ToolStripMenuItem _miLocalSingleDelete;
        private ToolStripSeparator _sepLocalBatch;
        private ToolStripMenuItem _miBackupSingleImport;
        private ToolStripMenuItem _miBackupPreviewCap;
        private ToolStripMenuItem _miBackupPreviewConv;
        private ToolStripMenuItem _miBackupRelocateCap;
        private ToolStripMenuItem _miBackupExportMd;
        private ToolStripMenuItem _miBackupSingleRepoDelete;
        private ToolStripSeparator _sepBackupBatch;
        private ToolStripMenuItem _miBackupBatchImport;
        private ToolStripMenuItem _miBackupBatchRepoDelete;

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
            _repositoryPath = CapabilityRepositoryManager.ResolveDefaultRepositoryPath();

            BuildUI();

            this.Load += (s, e) => ClampFormToWorkingArea(this);
            this.Shown += (s, e) => {
                AdjustSplitterLayouts();
                DetectPaths();
            };
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                if (_searchDebounceTimer != null)
                {
                    _searchDebounceTimer.Stop();
                    _searchDebounceTimer.Dispose();
                    _searchDebounceTimer = null;
                }
                if (_worker != null)
                {
                    _worker.Dispose();
                    _worker = null;
                }
                if (_regularTreeFont != null)
                {
                    _regularTreeFont.Dispose();
                    _regularTreeFont = null;
                }
                if (_boldTreeFont != null)
                {
                    _boldTreeFont.Dispose();
                    _boldTreeFont = null;
                }
                if (_appIcon != null)
                {
                    _appIcon.Dispose();
                    _appIcon = null;
                }
            }
        }

        private static Icon ResolveAppIcon()
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(iconPath))
                    return new Icon(iconPath);
            }
            catch {}
            try
            {
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch {}
            return null;
        }

        private static string NormalizeLineEndings(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
        }

        private string PadBoldNodeText(string cleanText)
        {
            if (string.IsNullOrEmpty(cleanText) || _regularTreeFont == null || _boldTreeFont == null)
                return cleanText ?? string.Empty;
            int boldWidth = TextRenderer.MeasureText(cleanText, _boldTreeFont, Size.Empty, TextFormatFlags.NoPadding).Width;
            int regWidth = TextRenderer.MeasureText(cleanText, _regularTreeFont, Size.Empty, TextFormatFlags.NoPadding).Width;
            int diff = boldWidth - regWidth;
            if (diff <= 0) return cleanText + " ";
            int spaceWidth = TextRenderer.MeasureText("x x", _regularTreeFont, Size.Empty, TextFormatFlags.NoPadding).Width
                           - TextRenderer.MeasureText("xx", _regularTreeFont, Size.Empty, TextFormatFlags.NoPadding).Width;
            if (spaceWidth < 2) spaceWidth = 4;
            int spaces = (diff / spaceWidth) + 2;
            if (spaces > 24) spaces = 24;
            return cleanText + new string(' ', spaces);
        }

        private void UpdateSplitMainMinSizes(bool centerSplitter)
        {
            if (splitMain == null) return;
            try
            {
                int w = splitMain.ClientSize.Width;
                int sw = splitMain.SplitterWidth;
                if (w <= sw + 80) return;

                int safeMin = (w >= 720) ? 340 : Math.Max(40, (w - sw - 20) / 2);
                int maxMin = Math.Max(40, (w - sw - 20) / 2);
                if (safeMin > maxMin) safeMin = maxMin;

                // 必须先将 MinSize 归零再调整 SplitterDistance，避免窄屏/高 DPI 下 WinForms 抛 InvalidOperationException
                splitMain.Panel1MinSize = 0;
                splitMain.Panel2MinSize = 0;

                int maxDist = w - sw - safeMin;
                if (centerSplitter)
                {
                    int target = (w - sw) / 2;
                    if (target >= safeMin && target <= maxDist)
                        splitMain.SplitterDistance = target;
                }
                else
                {
                    int cur = splitMain.SplitterDistance;
                    if (cur < safeMin) splitMain.SplitterDistance = safeMin;
                    else if (cur > maxDist && maxDist >= safeMin) splitMain.SplitterDistance = maxDist;
                }

                splitMain.Panel1MinSize = safeMin;
                splitMain.Panel2MinSize = safeMin;
            }
            catch {}
        }

        internal static void ClampFormToWorkingArea(Form form)
        {
            if (form == null) return;
            try
            {
                if (form.Owner != null && form.Owner.Icon != null)
                {
                    form.Icon = form.Owner.Icon;
                }

                Rectangle wa = Screen.FromControl(form).WorkingArea;
                if (form.MinimumSize.Width > wa.Width || form.MinimumSize.Height > wa.Height)
                {
                    form.MinimumSize = new Size(
                        Math.Min(form.MinimumSize.Width, Math.Max(640, wa.Width - 20)),
                        Math.Min(form.MinimumSize.Height, Math.Max(480, wa.Height - 20))
                    );
                }

                int w = Math.Min(form.Width, wa.Width);
                int h = Math.Min(form.Height, wa.Height);
                int x = wa.Left + Math.Max(0, (wa.Width - w) / 2);
                int y = wa.Top + Math.Max(0, (wa.Height - h) / 2);
                form.Bounds = new Rectangle(x, y, w, h);
            }
            catch { }
        }

        internal static void ClearInitialEditSelections(Form form, params Control[] controls)
        {
            ClearInitialEditSelections(form, false, controls);
        }

        internal static void ClearInitialEditSelections(Form form, bool resetToStart, params Control[] controls)
        {
            if (form == null || controls == null || controls.Length == 0) return;
            Action clearAction = () =>
            {
                for (int i = 0; i < controls.Length; i++)
                {
                    Control ctrl = controls[i];
                    if (ctrl == null || ctrl.IsDisposed) continue;
                    TextBox tb = ctrl as TextBox;
                    if (tb != null)
                    {
                        tb.SelectionStart = resetToStart ? 0 : tb.TextLength;
                        tb.SelectionLength = 0;
                        if (resetToStart) tb.ScrollToCaret();
                        continue;
                    }
                    ComboBox cb = ctrl as ComboBox;
                    if (cb != null && cb.DropDownStyle == ComboBoxStyle.DropDown)
                    {
                        int len = (!resetToStart && cb.Text != null) ? cb.Text.Length : 0;
                        cb.SelectionStart = len;
                        cb.SelectionLength = 0;
                    }
                }
            };
            clearAction();
            if (form.IsHandleCreated && !form.IsDisposed)
            {
                try { form.BeginInvoke(clearAction); } catch { }
            }
        }

        private void AdjustSplitterLayouts()
        {
            if (!_splitterInitialized)
            {
                UpdateSplitMainMinSizes(true);
                _splitterInitialized = true;
            }
        }

        // ══════════════════════════════════════════════════════════
        // 极简素雅现代化 UI 构建 (彻底移除初始化越界陷阱与 DPI 自适应)
        // ══════════════════════════════════════════════════════════
        private void BuildUI()
        {
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Text = "Antigravity Companion V0.7 - 模块化能力仓库 (Skills/Rules/MCP/Plugins) 与会话迁移中心";
            this.Size = new Size(1360, 840);
            this.MinimumSize = new Size(920, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            _regularTreeFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);
            _boldTreeFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            this.Font = _regularTreeFont;
            this.BackColor = Color.FromArgb(248, 250, 252);

            _appIcon = ResolveAppIcon();
            if (_appIcon != null) this.Icon = _appIcon;

            _searchDebounceTimer = new Timer { Interval = 150 };
            _searchDebounceTimer.Tick += (s, e) =>
            {
                _searchDebounceTimer.Stop();
                ApplySearchFilter();
            };

            // 1. 顶部操作栏与状态看板 (DPI 自适应自动换行排版)
            Panel pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Padding = new Padding(12, 8, 12, 6)
            };
            pnlTop.Paint += (s, pe) =>
            {
                using (Pen borderPen = new Pen(Color.FromArgb(226, 232, 240)))
                    pe.Graphics.DrawLine(borderPen, 0, pnlTop.Height - 1, pnlTop.Width, pnlTop.Height - 1);
            };

            FlowLayoutPanel flowTopControls = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent
            };

            // 操作按钮栏
            btnDetect = MakeNeutralBtn("刷新本机与仓库", 118);
            btnDetect.TabIndex = 0;
            btnDetect.Margin = new Padding(0, 4, 6, 4);
            btnDetect.Click += (s, e) => DetectPaths();

            btnSwitchRepoMode = MakeNeutralBtn("内置能力仓库", 116);
            btnSwitchRepoMode.TabIndex = 1;
            btnSwitchRepoMode.Margin = new Padding(0, 4, 6, 4);
            btnSwitchRepoMode.Click += (s, e) => SwitchToBuiltInRepositoryMode();

            btnOpenBackup = MakeNeutralBtn("打开备份包(.agy)...", 136);
            btnOpenBackup.TabIndex = 2;
            btnOpenBackup.Margin = new Padding(0, 4, 6, 4);
            btnOpenBackup.Click += BtnOpenBackup_Click;

            btnFullBackup = MakeNeutralBtn("全量备份导出...", 115);
            btnFullBackup.TabIndex = 3;
            btnFullBackup.Margin = new Padding(0, 4, 12, 4);
            btnFullBackup.Click += BtnFullBackup_Click;

            Label lblViewFilter = new Label
            {
                Text = "视图:",
                AutoSize = true,
                Margin = new Padding(0, 10, 2, 4),
                Font = _regularTreeFont,
                ForeColor = Color.FromArgb(100, 116, 139)
            };

            cmbViewFilter = new ComboBox
            {
                Width = 152,
                TabIndex = 4,
                Margin = new Padding(0, 6, 10, 4),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = _regularTreeFont
            };
            cmbViewFilter.Items.AddRange(new object[] {
                "全部 (能力库 + 会话)",
                "仅看能力仓库 (5大类)",
                "仅看工程与对话会话"
            });
            cmbViewFilter.SelectedIndex = 0;
            cmbViewFilter.SelectedIndexChanged += (s, e) =>
            {
                if (cmbViewFilter.SelectedIndex == 1) _viewFilter = ViewFilterMode.CapabilitiesOnly;
                else if (cmbViewFilter.SelectedIndex == 2) _viewFilter = ViewFilterMode.SessionsOnly;
                else _viewFilter = ViewFilterMode.All;

                RenderLocalTree();
                RenderBackupTree();
            };

            chkHideTemp = new CheckBox
            {
                Text = "过滤临时与子任务",
                Checked = true,
                AutoSize = true,
                TabIndex = 5,
                Margin = new Padding(0, 9, 10, 4),
                Font = _regularTreeFont,
                ForeColor = Color.FromArgb(51, 65, 85)
            };
            chkHideTemp.CheckedChanged += (s, e) =>
            {
                RenderLocalTree();
                RenderBackupTree();
            };

            Label lblSearchIcon = new Label
            {
                Text = "搜索:",
                AutoSize = true,
                Margin = new Padding(0, 10, 4, 4),
                Font = _regularTreeFont,
                ForeColor = Color.FromArgb(100, 116, 139)
            };

            txtSearch = new TextBox
            {
                Width = 148,
                TabIndex = 6,
                Margin = new Padding(0, 7, 6, 4),
                Font = _regularTreeFont,
                BorderStyle = BorderStyle.FixedSingle
            };
            txtSearch.TextChanged += TxtSearch_TextChanged;
            txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    if (_searchDebounceTimer != null) _searchDebounceTimer.Stop();
                    ApplySearchFilter();
                }
            };

            chkSearchContent = new CheckBox
            {
                Text = "搜对话正文",
                Checked = false,
                AutoSize = true,
                TabIndex = 7,
                Margin = new Padding(0, 9, 12, 4),
                Font = _regularTreeFont,
                ForeColor = Color.FromArgb(51, 65, 85)
            };
            chkSearchContent.CheckedChanged += (s, e) => ApplySearchFilter();

            btnImportSelected = MakePrimaryBtn("← 导入所选到本地", 148);
            btnImportSelected.TabIndex = 8;
            btnImportSelected.Margin = new Padding(0, 4, 6, 4);
            btnImportSelected.Enabled = true;
            btnImportSelected.Click += BtnPerformSelectiveImport_Click;

            flowTopControls.Controls.AddRange(new Control[] {
                btnDetect, btnSwitchRepoMode, btnOpenBackup, btnFullBackup,
                lblViewFilter, cmbViewFilter, chkHideTemp,
                lblSearchIcon, txtSearch, chkSearchContent, btnImportSelected
            });

            // 状态指示条（使用 Dock.Top 顺序堆叠，防止与 AutoSize FlowLayoutPanel 在换行时重叠）
            lblStatsSummary = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 24),
                Text = "本地: 正在扫描... | 右侧仓库: 正在加载... | 待同步选中: 0 项",
                Font = _regularTreeFont,
                ForeColor = Color.FromArgb(71, 85, 105),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 4, 0, 2),
                AutoEllipsis = true
            };

            pnlTop.Controls.Add(lblStatsSummary);
            pnlTop.Controls.Add(flowTopControls);

            // 2. 主体 SplitContainer (窄屏与高 DPI 下动态计算 MinSize，避免 InvalidOperationException)
            splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(226, 232, 240)
            };
            splitMain.Resize += (s, e) => UpdateSplitMainMinSizes(false);

            int treeItemHeight = Math.Max(22, TextRenderer.MeasureText("测Ay", _regularTreeFont).Height + 6);

            // ── 左侧面板 (本地环境) ──
            Panel pnlLeft = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12, 8, 12, 10) };
            Panel pnlLeftHeader = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.FromArgb(248, 250, 252)
            };
            pnlLeftHeader.Paint += (s, pe) =>
            {
                using (Pen borderPen = new Pen(Color.FromArgb(226, 232, 240)))
                    pe.Graphics.DrawLine(borderPen, 0, pnlLeftHeader.Height - 1, pnlLeftHeader.Width, pnlLeftHeader.Height - 1);
            };

            Panel pnlLeftTitleRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(4, 2, 4, 0)
            };

            lblLocalHeader = new Label
            {
                Text = "本机环境 (~/.gemini)",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = _boldTreeFont,
                ForeColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(4, 0, 0, 0),
                AutoEllipsis = true
            };

            lblConvCount = new Label
            {
                Text = "能力: 0 | 会话: 0",
                Dock = DockStyle.Right,
                AutoSize = true,
                MinimumSize = new Size(124, 26),
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Microsoft YaHei UI", 8.5f),
                ForeColor = Color.FromArgb(100, 116, 139),
                Padding = new Padding(6, 4, 4, 0)
            };
            pnlLeftTitleRow.Controls.Add(lblLocalHeader);
            pnlLeftTitleRow.Controls.Add(lblConvCount);

            FlowLayoutPanel pnlLocalActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(6, 2, 6, 6),
                BackColor = Color.FromArgb(248, 250, 252)
            };

            btnLocalPushToRepo = MakeSmallBtn("存入仓库(→Repo)");
            btnLocalPushToRepo.TabIndex = 9;
            btnLocalPushToRepo.Margin = new Padding(0, 2, 6, 2);
            btnLocalPushToRepo.BackColor = Color.FromArgb(239, 246, 255);
            btnLocalPushToRepo.ForeColor = Color.FromArgb(29, 78, 216);
            btnLocalPushToRepo.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
            btnLocalPushToRepo.Click += (s, e) => ActionPushSelectedLocalToRepository();

            btnLocalBatchRename = MakeSmallBtn("批量重命名");
            btnLocalBatchRename.TabIndex = 10;
            btnLocalBatchRename.Margin = new Padding(0, 2, 6, 2);
            btnLocalBatchRename.Click += (s, e) => ActionOpenBatchRenameWorkbench();

            btnLocalExportSelected = MakeSmallBtn("导出所选(.agy)");
            btnLocalExportSelected.TabIndex = 11;
            btnLocalExportSelected.Margin = new Padding(0, 2, 6, 2);
            btnLocalExportSelected.Click += (s, e) => ActionExportSelectedLocalToAgy();

            btnLocalDeleteSelected = MakeSmallBtn("删除所选");
            btnLocalDeleteSelected.TabIndex = 12;
            btnLocalDeleteSelected.Margin = new Padding(0, 2, 6, 2);
            btnLocalDeleteSelected.ForeColor = Color.FromArgb(185, 28, 28);
            btnLocalDeleteSelected.Click += (s, e) => ActionDeleteSelectedLocal();

            btnLocalOrphanGc = MakeSmallBtn("清理孤儿垃圾");
            btnLocalOrphanGc.TabIndex = 13;
            btnLocalOrphanGc.Margin = new Padding(0, 2, 4, 2);
            btnLocalOrphanGc.Click += (s, e) => ActionRunOrphanGarbageCollector();

            pnlLocalActions.Controls.AddRange(new Control[] { btnLocalPushToRepo, btnLocalBatchRename, btnLocalExportSelected, btnLocalDeleteSelected, btnLocalOrphanGc });

            pnlLeftHeader.Controls.Add(pnlLocalActions);
            pnlLeftHeader.Controls.Add(pnlLeftTitleRow);

            BuildContextMenus();

            tvLocal = new DoubleBufferedTreeView
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = _regularTreeFont,
                ItemHeight = treeItemHeight,
                CheckBoxes = true,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                ShowNodeToolTips = true,
                FullRowSelect = true,
                HideSelection = false,
                TabIndex = 8,
                ContextMenuStrip = ctxLocal
            };
            tvLocal.AfterCheck += TvLocal_AfterCheck;
            tvLocal.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    TreeViewHitTestInfo hit = tvLocal.HitTest(e.Location);
                    tvLocal.SelectedNode = hit.Node;
                }
            };
            tvLocal.NodeMouseDoubleClick += (s, e) =>
            {
                if (e.Node != null)
                {
                    DiffItemNodeTag tag = e.Node.Tag as DiffItemNodeTag;
                    if (tag != null)
                    {
                        if (tag.Category == ItemCategory.Capability)
                            ActionPreviewCapability(true);
                        else if (tag.Category == ItemCategory.Conversation)
                            ActionPreviewSessionMarkdown(true);
                    }
                }
            };
            tvLocal.KeyDown += TvLocal_KeyDown;

            pnlLeft.Controls.Add(tvLocal);
            pnlLeft.Controls.Add(pnlLeftHeader);
            splitMain.Panel1.Controls.Add(pnlLeft);

            // ── 右侧面板 (内置能力仓库 Repository/ 或 备份归档包 .agy) ──
            Panel pnlRight = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12, 8, 12, 10) };
            Panel pnlRightHeader = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.FromArgb(248, 250, 252)
            };
            pnlRightHeader.Paint += (s, pe) =>
            {
                using (Pen borderPen = new Pen(Color.FromArgb(226, 232, 240)))
                    pe.Graphics.DrawLine(borderPen, 0, pnlRightHeader.Height - 1, pnlRightHeader.Width, pnlRightHeader.Height - 1);
            };

            Panel pnlRightTitleRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(4, 2, 4, 0)
            };

            lblBackupHeader = new Label
            {
                Text = "内置能力仓库 (Repository/)",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = _boldTreeFont,
                ForeColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(4, 0, 0, 0),
                AutoEllipsis = true
            };
            pnlRightTitleRow.Controls.Add(lblBackupHeader);

            // 快速选择工具栏
            FlowLayoutPanel pnlQuickSelect = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(6, 2, 6, 6),
                BackColor = Color.FromArgb(248, 250, 252)
            };

            btnSelectNewOnly = MakeSmallBtn("仅选新增/更新");
            btnSelectNewOnly.TabIndex = 14;
            btnSelectNewOnly.Margin = new Padding(0, 2, 6, 2);
            btnSelectNewOnly.Click += BtnSelectNewOnly_Click;

            btnSelectAll = MakeSmallBtn("全选");
            btnSelectAll.TabIndex = 15;
            btnSelectAll.Margin = new Padding(0, 2, 6, 2);
            btnSelectAll.Click += (s, e) => SetAllBackupChecks(true);

            btnDeselectAll = MakeSmallBtn("全不选");
            btnDeselectAll.TabIndex = 16;
            btnDeselectAll.Margin = new Padding(0, 2, 6, 2);
            btnDeselectAll.Click += (s, e) => SetAllBackupChecks(false);

            btnRepoDeleteSelected = MakeSmallBtn("从仓库删除");
            btnRepoDeleteSelected.TabIndex = 17;
            btnRepoDeleteSelected.Margin = new Padding(0, 2, 4, 2);
            btnRepoDeleteSelected.ForeColor = Color.FromArgb(185, 28, 28);
            btnRepoDeleteSelected.Click += (s, e) => ActionDeleteSelectedFromRepo();

            pnlQuickSelect.Controls.AddRange(new Control[] { btnSelectNewOnly, btnSelectAll, btnDeselectAll, btnRepoDeleteSelected });
            pnlRightHeader.Controls.Add(pnlQuickSelect);
            pnlRightHeader.Controls.Add(pnlRightTitleRow);

            tvBackup = new DoubleBufferedTreeView
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = _regularTreeFont,
                ItemHeight = treeItemHeight,
                CheckBoxes = true,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                ShowNodeToolTips = true,
                FullRowSelect = true,
                HideSelection = false,
                TabIndex = 13,
                ContextMenuStrip = ctxBackup
            };
            tvBackup.AfterCheck += TvBackup_AfterCheck;
            tvBackup.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    TreeViewHitTestInfo hit = tvBackup.HitTest(e.Location);
                    tvBackup.SelectedNode = hit.Node;
                }
            };
            tvBackup.NodeMouseDoubleClick += (s, e) =>
            {
                if (e.Node != null)
                {
                    DiffItemNodeTag tag = e.Node.Tag as DiffItemNodeTag;
                    if (tag != null)
                    {
                        if (tag.Category == ItemCategory.Capability)
                            ActionPreviewCapability(false);
                        else if (tag.Category == ItemCategory.Conversation)
                            ActionPreviewSessionMarkdown(false);
                    }
                }
            };
            tvBackup.KeyDown += TvBackup_KeyDown;

            pnlRight.Controls.Add(tvBackup);
            pnlRight.Controls.Add(pnlRightHeader);
            splitMain.Panel2.Controls.Add(pnlRight);

            // 3. 底部紧凑运行日志区
            Panel pnlBottomLog = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 120,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(12, 6, 12, 6)
            };
            pnlBottomLog.Paint += (s, pe) =>
            {
                using (Pen borderPen = new Pen(Color.FromArgb(226, 232, 240)))
                    pe.Graphics.DrawLine(borderPen, 0, 0, pnlBottomLog.Width, 0);
            };

            rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                TabIndex = 18,
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = Color.FromArgb(30, 41, 59),
                Font = _regularTreeFont,
                BorderStyle = BorderStyle.None
            };
            pnlBottomLog.Controls.Add(rtbLog);

            // 组装到主窗体
            this.Controls.Add(splitMain);
            this.Controls.Add(pnlBottomLog);
            this.Controls.Add(pnlTop);
        }

        // ══════════════════════════════════════════════════════════
        // 本地环境与内置仓库探测与解析 (Local & Repository Detection)
        // ══════════════════════════════════════════════════════════
        private sealed class TreeViewUiState
        {
            public readonly HashSet<string> ExpandedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public string SelectedKey = string.Empty;
            public string TopNodeKey = string.Empty;
        }

        private static string GetTreeNodeStateKey(TreeNode node)
        {
            if (node == null) return string.Empty;
            DiffItemNodeTag tag = node.Tag as DiffItemNodeTag;
            if (tag == null) return "TEXT:" + (node.Text ?? string.Empty).Trim();

            switch (tag.Category)
            {
                case ItemCategory.CapabilityCategoryGroup:
                    return "CAPGRP:" + tag.CapKind.ToString();
                case ItemCategory.Capability:
                    return "CAP:" + (tag.Id ?? tag.DisplayName ?? string.Empty);
                case ItemCategory.Project:
                    string bucketKey = tag.TagData as string;
                    return "PROJ:" + (!string.IsNullOrEmpty(bucketKey) ? bucketKey : (tag.ProjectName ?? tag.DisplayName ?? string.Empty));
                case ItemCategory.Conversation:
                    return "CONV:" + (tag.Id ?? tag.DisplayName ?? string.Empty);
                default:
                    return "TAG:" + tag.Category.ToString() + ":" + (tag.Id ?? tag.DisplayName ?? string.Empty);
            }
        }

        private static TreeViewUiState CaptureTreeViewUiState(TreeView tv)
        {
            TreeViewUiState state = new TreeViewUiState();
            if (tv == null) return state;
            CaptureExpandedRecursive(tv.Nodes, state.ExpandedKeys);
            if (tv.SelectedNode != null) state.SelectedKey = GetTreeNodeStateKey(tv.SelectedNode);
            if (tv.TopNode != null) state.TopNodeKey = GetTreeNodeStateKey(tv.TopNode);
            return state;
        }

        private static void CaptureExpandedRecursive(TreeNodeCollection nodes, HashSet<string> expandedKeys)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode n = nodes[i];
                if (n.IsExpanded)
                {
                    string key = GetTreeNodeStateKey(n);
                    if (!string.IsNullOrEmpty(key)) expandedKeys.Add(key);
                }
                if (n.Nodes.Count > 0) CaptureExpandedRecursive(n.Nodes, expandedKeys);
            }
        }

        private static void ApplyTreeViewUiState(TreeView tv, TreeViewUiState state, bool expandAllMatches, bool restoreSavedExpansion)
        {
            if (tv == null) return;
            if (expandAllMatches)
            {
                tv.ExpandAll();
            }
            else if (restoreSavedExpansion && state != null)
            {
                RestoreExpandedRecursive(tv.Nodes, state.ExpandedKeys);
            }
            else
            {
                // 初次渲染仅展开能力分类根节点，工程会话折叠保持清爽
                for (int i = 0; i < tv.Nodes.Count; i++)
                {
                    DiffItemNodeTag tag = tv.Nodes[i].Tag as DiffItemNodeTag;
                    if (tag != null && tag.Category == ItemCategory.CapabilityCategoryGroup)
                        tv.Nodes[i].Expand();
                }
            }

            if (state != null)
            {
                if (!string.IsNullOrEmpty(state.SelectedKey))
                {
                    TreeNode sel = FindNodeByStateKey(tv.Nodes, state.SelectedKey);
                    if (sel != null) tv.SelectedNode = sel;
                }
                if (!string.IsNullOrEmpty(state.TopNodeKey))
                {
                    TreeNode top = FindNodeByStateKey(tv.Nodes, state.TopNodeKey);
                    if (top != null)
                    {
                        try { tv.TopNode = top; } catch {}
                    }
                }
            }
        }

        private static void RestoreExpandedRecursive(TreeNodeCollection nodes, HashSet<string> expandedKeys)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode n = nodes[i];
                string key = GetTreeNodeStateKey(n);
                if (!string.IsNullOrEmpty(key) && expandedKeys.Contains(key))
                    n.Expand();
                if (n.Nodes.Count > 0) RestoreExpandedRecursive(n.Nodes, expandedKeys);
            }
        }

        private static TreeNode FindNodeByStateKey(TreeNodeCollection nodes, string key)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode n = nodes[i];
                if (string.Equals(GetTreeNodeStateKey(n), key, StringComparison.OrdinalIgnoreCase))
                    return n;
                if (n.Nodes.Count > 0)
                {
                    TreeNode child = FindNodeByStateKey(n.Nodes, key);
                    if (child != null) return child;
                }
            }
            return null;
        }

        private void ApplyNodeVisualStyle(TreeNode node, string cleanText, string extraTooltip, bool isBold, Color foreColor)
        {
            if (node == null) return;
            node.ForeColor = foreColor;
            node.NodeFont = isBold ? _boldTreeFont : _regularTreeFont;
            node.Text = isBold ? PadBoldNodeText(cleanText) : cleanText;
            node.ToolTipText = string.IsNullOrEmpty(extraTooltip)
                ? cleanText
                : (cleanText + "\n" + extraTooltip);
        }

        private void SwitchToBuiltInRepositoryMode()
        {
            _rightSourceMode = RightPaneSourceMode.BuiltInRepository;
            _repoCapabilities = CapabilityRepositoryManager.ScanRepositoryCapabilities(_repositoryPath, HomePath, _geminiPath);
            _currentCapDiffNodes = VisualDiffEngine.ComputeCapabilityDiff(_localCapabilities, _repoCapabilities);

            // 默认勾选新增或有更新的能力项
            _checkedBackupCapabilityKeys.Clear();
            for (int i = 0; i < _currentCapDiffNodes.Count; i++)
            {
                CapabilityDiffNode cd = _currentCapDiffNodes[i];
                if (cd.RemoteItem != null && (cd.Status == DiffStatus.NewInBackup || cd.Status == DiffStatus.Modified))
                {
                    _checkedBackupCapabilityKeys.Add(cd.UniqueKey);
                }
            }
            _hasInitializedRepoDefaultChecks = true;

            if (btnRepoDeleteSelected != null) btnRepoDeleteSelected.Visible = true;
            lblBackupHeader.Text = string.Format("内置能力仓库 (Repository/ — 共 {0} 项能力)", _repoCapabilities.Count);
            btnImportSelected.Enabled = true;

            RenderBackupTree();
            Log("已切换右侧视图至「内置能力仓库 (Repository/)」: " + _repositoryPath, Color.FromArgb(30, 96, 145));
        }

        internal static List<ConversationSummaryInfo> LoadLocalThreeLayerSummaries(
            string geminiPath,
            out Dictionary<string, ProjectMetadata> localProjects,
            out List<string> warnings)
        {
            return WorkspaceService.LoadLocalThreeLayerSummaries(geminiPath, out localProjects, out warnings);
        }

        private void DetectPaths()
        {
            try
            {
                _geminiPath = Path.Combine(HomePath, ".gemini");
                if (string.IsNullOrEmpty(_repositoryPath))
                    _repositoryPath = CapabilityRepositoryManager.ResolveDefaultRepositoryPath();

                _localProjects.Clear();
                _localSummaries.Clear();
                _localCapabilities.Clear();

                Log("正在探测本地 Antigravity 环境与五大类能力资产: " + _geminiPath, Color.FromArgb(71, 85, 105));

                Dictionary<string, ProjectMetadata> loadedProjects;
                List<string> loadWarnings;
                _localSummaries = LoadLocalThreeLayerSummaries(_geminiPath, out loadedProjects, out loadWarnings);
                _localProjects = loadedProjects;
                for (int w = 0; w < loadWarnings.Count; w++)
                {
                    Log("⚠ " + loadWarnings[w], Color.FromArgb(180, 83, 9));
                }

                // 3. 扫描本地五大类能力配置 (Plugins / Skills / Rules / MCP / Hooks)
                _localCapabilities = CapabilityRepositoryManager.ScanLocalCapabilities(_geminiPath, HomePath);

                // 4. 扫描内置 Repository/ 目录或同步右侧备份包对比
                _repoCapabilities = CapabilityRepositoryManager.ScanRepositoryCapabilities(_repositoryPath, HomePath, _geminiPath);

                if (_rightSourceMode == RightPaneSourceMode.BuiltInRepository)
                {
                    _currentCapDiffNodes = VisualDiffEngine.ComputeCapabilityDiff(_localCapabilities, _repoCapabilities);
                    if (!_hasInitializedRepoDefaultChecks)
                    {
                        for (int i = 0; i < _currentCapDiffNodes.Count; i++)
                        {
                            CapabilityDiffNode cd = _currentCapDiffNodes[i];
                            if (cd.RemoteItem != null && (cd.Status == DiffStatus.NewInBackup || cd.Status == DiffStatus.Modified))
                            {
                                _checkedBackupCapabilityKeys.Add(cd.UniqueKey);
                            }
                        }
                        _hasInitializedRepoDefaultChecks = true;
                    }
                    lblBackupHeader.Text = string.Format("内置能力仓库 (Repository/ — 共 {0} 项能力)", _repoCapabilities.Count);
                    if (btnRepoDeleteSelected != null) btnRepoDeleteSelected.Visible = true;
                }
                else if (_backupIndex != null)
                {
                    _currentDiffNodes = VisualDiffEngine.ComputeDiff(_localSummaries, _backupIndex.Conversations, _backupIndex.SourceHome, HomePath);
                    _currentCapDiffNodes = VisualDiffEngine.ComputeCapabilityDiff(_localCapabilities, _backupIndex.Capabilities);
                    if (btnRepoDeleteSelected != null) btnRepoDeleteSelected.Visible = false;
                }

                RenderLocalTree();
                RenderBackupTree();
                Log(string.Format("本地环境扫描完成：共 {0} 项能力配置，{1} 个工程，{2} 条会话。",
                    _localCapabilities.Count, _localProjects.Count, _localSummaries.Count), Color.FromArgb(21, 128, 61));
            }
            catch (Exception ex)
            {
                Log("本地扫描异常: " + ex.Message, Color.FromArgb(185, 28, 28));
            }
        }

        private void RenderLocalTree()
        {
            TreeViewUiState uiState = CaptureTreeViewUiState(tvLocal);
            bool hadCapturedState = _hasLocalTreeStateCaptured;

            tvLocal.BeginUpdate();
            _isUpdatingChecks = true;
            int visibleProjects = 0;
            int visibleConvsCount = 0;
            int visibleCapsCount = 0;
            string query = txtSearch.Text.Trim();

            try
            {
                tvLocal.Nodes.Clear();
                bool hideTemp = chkHideTemp.Checked;

                // ── A. 渲染本机五大类能力资产节点 ──
                if (_viewFilter != ViewFilterMode.SessionsOnly)
                {
                    CapabilityKind[] kinds = new CapabilityKind[] {
                        CapabilityKind.Plugin,
                        CapabilityKind.Skill,
                        CapabilityKind.Rule,
                        CapabilityKind.McpServer,
                        CapabilityKind.Hook
                    };

                    for (int kIdx = 0; kIdx < kinds.Length; kIdx++)
                    {
                        CapabilityKind kind = kinds[kIdx];
                        List<CapabilityItem> matchedCaps = new List<CapabilityItem>();
                        for (int i = 0; i < _localCapabilities.Count; i++)
                        {
                            CapabilityItem cap = _localCapabilities[i];
                            if (cap.Kind != kind) continue;
                            if (!string.IsNullOrEmpty(query) &&
                                cap.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                                cap.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                                (cap.Description == null || cap.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0))
                            {
                                continue;
                            }
                            matchedCaps.Add(cap);
                        }

                        if (matchedCaps.Count == 0) continue;
                        visibleCapsCount += matchedCaps.Count;

                        string catTitle = string.Format("❖ {0} ({1} 项)", CapabilityItem.GetCategoryTitle(kind), matchedCaps.Count);
                        TreeNode catNode = new TreeNode(catTitle);
                        catNode.Tag = new DiffItemNodeTag {
                            Category = ItemCategory.CapabilityCategoryGroup,
                            DisplayName = catTitle,
                            CapKind = kind
                        };
                        ApplyNodeVisualStyle(catNode, catTitle, null, true, Color.FromArgb(30, 96, 145));

                        bool anyCapChecked = false;
                        for (int c = 0; c < matchedCaps.Count; c++)
                        {
                            CapabilityItem cap = matchedCaps[c];
                            string warnBadge = cap.HasMissingPaths ? " [⚠ 路径缺失]" : "";
                            string descSuffix = !string.IsNullOrEmpty(cap.Description) ? (" — " + cap.Description) : "";
                            string cText = string.Format("[{0}] {1}{2}{3}", CapabilityItem.GetKindShortTag(cap.Kind), cap.DisplayName, warnBadge, descSuffix);

                            TreeNode cNode = new TreeNode(cText);
                            cNode.Tag = new DiffItemNodeTag {
                                Category = ItemCategory.Capability,
                                Id = cap.UniqueKey,
                                DisplayName = cap.DisplayName,
                                CapKind = cap.Kind,
                                CapabilityInfo = cap,
                                Timestamp = cap.LastModified
                            };

                            bool isChecked = _checkedLocalCapabilityKeys.Contains(cap.UniqueKey);
                            cNode.Checked = isChecked;
                            if (isChecked) anyCapChecked = true;

                            string capTooltip = cap.HasMissingPaths && cap.MissingLocalPaths.Count > 0
                                ? ("缺失本地引用路径: " + string.Join("; ", cap.MissingLocalPaths.ToArray()))
                                : (cap.ReferencedLocalPaths != null && cap.ReferencedLocalPaths.Count > 0
                                    ? ("引用路径: " + string.Join("; ", cap.ReferencedLocalPaths.ToArray()))
                                    : "双击查看详情，右键可单独导出或重定位脚本路径");

                            if (cap.HasMissingPaths)
                            {
                                ApplyNodeVisualStyle(cNode, cText, capTooltip, true, Color.FromArgb(180, 83, 9));
                            }
                            else if (_highlightTracker.IsCapabilityHighlighted(cap.UniqueKey))
                            {
                                ApplyNodeVisualStyle(cNode, cText, capTooltip, true, Color.FromArgb(21, 128, 61));
                            }
                            else
                            {
                                ApplyNodeVisualStyle(cNode, cText, capTooltip, false, Color.FromArgb(30, 41, 59));
                            }

                            catNode.Nodes.Add(cNode);
                        }

                        catNode.Checked = anyCapChecked;
                        tvLocal.Nodes.Add(catNode);
                    }
                }

                // ── B. 渲染本机工程与对话会话节点 (按 ProjectId / GhostKey 精确分桶，防同名不同盘工程混淆) ──
                if (_viewFilter != ViewFilterMode.CapabilitiesOnly)
                {
                    HashSet<string> fullTextHits = null;
                    if (chkSearchContent != null && chkSearchContent.Checked && !string.IsNullOrEmpty(query) && !string.IsNullOrEmpty(_geminiPath))
                    {
                        string summaryDbPath = Path.Combine(_geminiPath, "antigravity", "conversation_summaries.db");
                        if (File.Exists(summaryDbPath))
                        {
                            try
                            {
                                fullTextHits = SummarySqliteBridge.SearchConversationsFullText(summaryDbPath, query);
                                if (!string.IsNullOrEmpty(SummarySqliteBridge.LastError))
                                {
                                    Log("⚠ 全文检索 conversation_summaries.db 告警: " + SummarySqliteBridge.LastError, Color.FromArgb(180, 83, 9));
                                }
                            }
                            catch (Exception ex)
                            {
                                Log("⚠ 全文检索 conversation_summaries.db 失败: " + ex.Message, Color.FromArgb(180, 83, 9));
                                fullTextHits = null;
                            }
                        }
                    }

                    Dictionary<string, LocalProjectBucket> buckets = new Dictionary<string, LocalProjectBucket>(StringComparer.OrdinalIgnoreCase);
                    Dictionary<string, int> nameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                    for (int i = 0; i < _localSummaries.Count; i++)
                    {
                        ConversationSummaryInfo s = _localSummaries[i];
                        string p = string.IsNullOrWhiteSpace(s.ProjectName) ? "Outside of Project" : s.ProjectName;
                        string pid = !string.IsNullOrWhiteSpace(s.ProjectId) ? s.ProjectId.Trim() : string.Empty;
                        string ghostKey = string.IsNullOrEmpty(pid) ? LocalWorkspaceManager.BuildGhostProjectKey(p, s.FolderUri) : string.Empty;
                        string bucketKey = !string.IsNullOrEmpty(pid) ? ("ID:" + pid) : ("GHOST:" + ghostKey);

                        LocalProjectBucket b;
                        if (!buckets.TryGetValue(bucketKey, out b))
                        {
                            b = new LocalProjectBucket
                            {
                                BucketKey = bucketKey,
                                ProjectId = pid,
                                GhostKey = ghostKey,
                                ProjectName = p,
                                FolderUri = s.FolderUri
                            };
                            buckets[bucketKey] = b;
                            nameCounts[p] = (nameCounts.ContainsKey(p) ? nameCounts[p] : 0) + 1;
                        }
                        if (string.IsNullOrEmpty(b.FolderUri) && !string.IsNullOrEmpty(s.FolderUri))
                            b.FolderUri = s.FolderUri;
                        b.Conversations.Add(s);
                    }

                    foreach (KeyValuePair<string, LocalProjectBucket> kvp in buckets)
                    {
                        LocalProjectBucket bucket = kvp.Value;
                        string pName = bucket.ProjectName;
                        List<ConversationSummaryInfo> list = bucket.Conversations;
                        bool isJunk = SmartProjectFilter.IsJunkOrEphemeral(pName);

                        int checkedTotalInBucket = 0;
                        List<ConversationSummaryInfo> visibleList = new List<ConversationSummaryInfo>();
                        for (int k = 0; k < list.Count; k++)
                        {
                            if (!string.IsNullOrEmpty(list[k].ConversationId) && _checkedLocalSessionIds.Contains(list[k].ConversationId))
                                checkedTotalInBucket++;

                            if (!hideTemp || !list[k].IsSubagent)
                            {
                                string dispTitle = list[k].DisplayTitle ?? string.Empty;
                                string cidVal = list[k].ConversationId ?? string.Empty;
                                if (string.IsNullOrEmpty(query) ||
                                    dispTitle.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    cidVal.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    pName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    (fullTextHits != null && !string.IsNullOrEmpty(cidVal) && fullTextHits.Contains(cidVal)))
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

                        string disambigSuffix = string.Empty;
                        if (nameCounts.ContainsKey(pName) && nameCounts[pName] > 1 && !string.IsNullOrEmpty(bucket.FolderUri))
                        {
                            string decoded = UriToDisplayPath(bucket.FolderUri);
                            if (!string.IsNullOrEmpty(decoded)) disambigSuffix = " [" + decoded + "]";
                        }

                        int checkedVisibleCount = 0;
                        for (int j = 0; j < visibleList.Count; j++)
                        {
                            if (_checkedLocalSessionIds.Contains(visibleList[j].ConversationId))
                                checkedVisibleCount++;
                        }

                        bool partialChecked = (checkedVisibleCount > 0 && checkedVisibleCount < visibleList.Count) ||
                                              (checkedTotalInBucket > 0 && checkedTotalInBucket < list.Count);
                        string partialBadge = partialChecked ? " [部分选中]" : string.Empty;
                        string countLabel = (visibleList.Count != list.Count)
                            ? string.Format("{0}/{1} 条", visibleList.Count, list.Count)
                            : string.Format("{0} 条", visibleList.Count);

                        string pNodeText = string.Format("■ {0}{1} ({2}){3}", pName, disambigSuffix, countLabel, partialBadge);
                        if (isJunk) pNodeText = "[临时] " + pNodeText;

                        TreeNode pNode = new TreeNode(pNodeText);
                        pNode.Tag = new DiffItemNodeTag
                        {
                            Category = ItemCategory.Project,
                            Id = bucket.ProjectId,
                            DisplayName = pName + disambigSuffix,
                            ProjectName = pName,
                            SourcePath = bucket.FolderUri,
                            IsTemporaryOrJunk = isJunk,
                            TagData = bucket.BucketKey
                        };

                        string projTooltip = !string.IsNullOrEmpty(bucket.FolderUri) ? ("工程路径: " + UriToDisplayPath(bucket.FolderUri)) : null;
                        if (isJunk)
                        {
                            ApplyNodeVisualStyle(pNode, pNodeText, projTooltip, false, Color.FromArgb(100, 116, 139));
                        }
                        else if (_highlightTracker.IsProjectHighlighted(pName))
                        {
                            ApplyNodeVisualStyle(pNode, pNodeText, projTooltip, true, Color.FromArgb(21, 128, 61));
                        }
                        else
                        {
                            ApplyNodeVisualStyle(pNode, pNodeText, projTooltip, false, Color.FromArgb(51, 65, 85));
                        }

                        for (int j = 0; j < visibleList.Count; j++)
                        {
                            ConversationSummaryInfo s = visibleList[j];
                            string timeStr = s.UpdatedTime > DateTime.MinValue ? s.UpdatedTime.ToString("MM-dd HH:mm") : "-";
                            string sNodeText = string.Format("{0} [{1}]", s.DisplayTitle, timeStr);
                            if (s.IsSubagent) sNodeText = "[子任务] " + sNodeText;

                            TreeNode sNode = new TreeNode(sNodeText);
                            sNode.Tag = new DiffItemNodeTag
                            {
                                Category = ItemCategory.Conversation,
                                Id = s.ConversationId,
                                DisplayName = s.DisplayTitle,
                                ProjectName = pName,
                                SummaryInfo = s,
                                Timestamp = s.UpdatedTime,
                                IsTemporaryOrJunk = s.IsSubagent,
                                TagData = bucket.BucketKey
                            };

                            sNode.Checked = _checkedLocalSessionIds.Contains(s.ConversationId);

                            string convTooltip = string.Format("会话ID: {0} | 步数: {1} (双击按 Markdown 预览)", s.ConversationId, s.StepCount);
                            if (s.IsSubagent)
                            {
                                ApplyNodeVisualStyle(sNode, sNodeText, convTooltip, false, Color.FromArgb(100, 116, 139));
                            }
                            else if (_highlightTracker.IsConversationHighlighted(s.ConversationId))
                            {
                                ApplyNodeVisualStyle(sNode, sNodeText, convTooltip, true, Color.FromArgb(21, 128, 61));
                            }
                            else
                            {
                                ApplyNodeVisualStyle(sNode, sNodeText, convTooltip, false, Color.FromArgb(30, 41, 59));
                            }

                            pNode.Nodes.Add(sNode);
                        }

                        pNode.Checked = (visibleList.Count > 0) ? (checkedVisibleCount > 0) : _checkedLocalProjectNames.Contains(bucket.BucketKey);
                        tvLocal.Nodes.Add(pNode);
                    }
                }

                ApplyTreeViewUiState(tvLocal, uiState, !string.IsNullOrEmpty(query), hadCapturedState);
                if (tvLocal.Nodes.Count > 0) _hasLocalTreeStateCaptured = true;
            }
            finally
            {
                _isUpdatingChecks = false;
                tvLocal.EndUpdate();
            }

            lblConvCount.Text = string.Format("能力: {0} | 会话: {1}", visibleCapsCount, visibleConvsCount);
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
            _rightSourceMode = RightPaneSourceMode.BackupArchive;
            if (btnRepoDeleteSelected != null) btnRepoDeleteSelected.Visible = false;

            Log("正在流式探测备份包元数据与能力配置: " + Path.GetFileName(archivePath) + "...", Color.FromArgb(71, 85, 105));
            this.Cursor = Cursors.WaitCursor;

            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                _backupIndex = StreamingZipIndexReader.ReadIndex(archivePath);
                sw.Stop();

                _currentDiffNodes = VisualDiffEngine.ComputeDiff(_localSummaries, _backupIndex.Conversations, _backupIndex.SourceHome, HomePath);
                _currentCapDiffNodes = VisualDiffEngine.ComputeCapabilityDiff(_localCapabilities, _backupIndex.Capabilities);
                
                // 默认仅选中新增与有更新/修改的会话及能力项（复用单一事实判定函数 ShouldAutoSelectBackupSession）
                _checkedSessionIds.Clear();
                _checkedProjectNames.Clear();
                _checkedBackupCapabilityKeys.Clear();

                for (int i = 0; i < _currentCapDiffNodes.Count; i++)
                {
                    CapabilityDiffNode cd = _currentCapDiffNodes[i];
                    if (cd.RemoteItem != null && (cd.Status == DiffStatus.NewInBackup || cd.Status == DiffStatus.Modified))
                    {
                        _checkedBackupCapabilityKeys.Add(cd.UniqueKey);
                    }
                }
                _hasInitializedRepoDefaultChecks = true;

                Dictionary<string, ConversationSummaryInfo> localConvMap = new Dictionary<string, ConversationSummaryInfo>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < _localSummaries.Count; i++)
                {
                    if (!string.IsNullOrEmpty(_localSummaries[i].ConversationId))
                        localConvMap[_localSummaries[i].ConversationId] = _localSummaries[i];
                }

                for (int p = 0; p < _currentDiffNodes.Count; p++)
                {
                    ProjectDiffNode pDiff = _currentDiffNodes[p];
                    if (pDiff.BackupConversations == null) continue;
                    for (int k = 0; k < pDiff.BackupConversations.Count; k++)
                    {
                        ConversationSummaryInfo bConv = pDiff.BackupConversations[k];
                        if (string.IsNullOrEmpty(bConv.ConversationId)) continue;
                        ConversationSummaryInfo loc = null;
                        localConvMap.TryGetValue(bConv.ConversationId, out loc);
                        if (ShouldAutoSelectBackupSession(bConv, loc, pDiff))
                        {
                            _checkedSessionIds.Add(bConv.ConversationId);
                        }
                    }
                }

                RenderBackupTree();

                lblBackupHeader.Text = "备份归档包: " + Path.GetFileName(archivePath);
                btnImportSelected.Enabled = true;

                Log(string.Format("备份包解析完成！共发现 {0} 项能力配置，{1} 个工程，{2} 条会话，耗时 {3}ms",
                    _backupIndex.Capabilities.Count, _backupIndex.Projects.Count, _backupIndex.Conversations.Count, sw.ElapsedMilliseconds), Color.FromArgb(21, 128, 61));
            }
            catch (Exception ex)
            {
                Log("解析备份包失败: " + ex.Message, Color.FromArgb(185, 28, 28));
                MessageBox.Show("解析备份包失败:\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void RenderBackupTree()
        {
            TreeViewUiState uiState = CaptureTreeViewUiState(tvBackup);
            bool hadCapturedState = _hasBackupTreeStateCaptured;

            tvBackup.BeginUpdate();
            _isUpdatingChecks = true;
            string query = txtSearch.Text.Trim();
            try
            {
                tvBackup.Nodes.Clear();
                bool hideTemp = chkHideTemp.Checked;

                // ── A. 渲染右侧能力资产节点 (Repository/ 或 .agy) ──
                if (_viewFilter != ViewFilterMode.SessionsOnly)
                {
                    CapabilityKind[] kinds = new CapabilityKind[] {
                        CapabilityKind.Plugin,
                        CapabilityKind.Skill,
                        CapabilityKind.Rule,
                        CapabilityKind.McpServer,
                        CapabilityKind.Hook
                    };

                    int remoteCapFound = 0;
                    for (int kIdx = 0; kIdx < kinds.Length; kIdx++)
                    {
                        CapabilityKind kind = kinds[kIdx];
                        List<CapabilityDiffNode> matchedDiffs = new List<CapabilityDiffNode>();
                        for (int i = 0; i < _currentCapDiffNodes.Count; i++)
                        {
                            CapabilityDiffNode cd = _currentCapDiffNodes[i];
                            if (cd.Kind != kind || cd.RemoteItem == null) continue;

                            CapabilityItem cap = cd.RemoteItem;
                            if (!string.IsNullOrEmpty(query) &&
                                cap.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                                cap.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                                (cap.Description == null || cap.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0))
                            {
                                continue;
                            }
                            matchedDiffs.Add(cd);
                        }

                        if (matchedDiffs.Count == 0) continue;
                        remoteCapFound += matchedDiffs.Count;

                        string catTitle = string.Format("❖ {0} ({1} 项)", CapabilityItem.GetCategoryTitle(kind), matchedDiffs.Count);
                        TreeNode catNode = new TreeNode(catTitle);
                        catNode.Tag = new DiffItemNodeTag {
                            Category = ItemCategory.CapabilityCategoryGroup,
                            DisplayName = catTitle,
                            CapKind = kind
                        };
                        ApplyNodeVisualStyle(catNode, catTitle, null, true, Color.FromArgb(30, 96, 145));

                        bool anyCapChecked = false;
                        for (int c = 0; c < matchedDiffs.Count; c++)
                        {
                            CapabilityDiffNode cd = matchedDiffs[c];
                            CapabilityItem cap = cd.RemoteItem;
                            string badge = cd.GetStatusBadge();
                            string descSuffix = !string.IsNullOrEmpty(cap.Description) ? (" — " + cap.Description) : "";
                            string cText = string.Format("[{0}] {1} {2}{3}", CapabilityItem.GetKindShortTag(cap.Kind), cap.DisplayName, badge, descSuffix);

                            TreeNode cNode = new TreeNode(cText);
                            cNode.Tag = new DiffItemNodeTag {
                                Category = ItemCategory.Capability,
                                Id = cd.UniqueKey,
                                DisplayName = cap.DisplayName,
                                CapKind = cap.Kind,
                                Status = cd.Status,
                                CapabilityInfo = cap,
                                CapabilityDiff = cd,
                                Timestamp = cap.LastModified
                            };

                            bool isChecked = _checkedBackupCapabilityKeys.Contains(cd.UniqueKey);
                            cNode.Checked = isChecked;
                            if (isChecked) anyCapChecked = true;

                            string capTooltip = cap.HasMissingPaths && cap.MissingLocalPaths.Count > 0
                                ? ("缺失本地引用路径: " + string.Join("; ", cap.MissingLocalPaths.ToArray()))
                                : "双击查看详情，右键可重定位路径或单独操作";

                            if (cap.HasMissingPaths)
                            {
                                ApplyNodeVisualStyle(cNode, cText, capTooltip, true, Color.FromArgb(180, 83, 9));
                            }
                            else
                            {
                                ApplyDiffStyle(cNode, cText, capTooltip, cd.Status, false);
                            }

                            catNode.Nodes.Add(cNode);
                        }

                        catNode.Checked = anyCapChecked;
                        tvBackup.Nodes.Add(catNode);
                    }

                    if (_rightSourceMode == RightPaneSourceMode.BuiltInRepository && remoteCapFound == 0 && string.IsNullOrEmpty(query))
                    {
                        TreeNode emptyNode = new TreeNode("提示: 内置能力仓库 (Repository/) 当前为空 — 请在左侧勾选能力后点击「存入仓库(→Repo)」");
                        ApplyNodeVisualStyle(emptyNode, emptyNode.Text, null, false, Color.FromArgb(100, 116, 139));
                        tvBackup.Nodes.Add(emptyNode);
                    }
                }

                // ── B. 渲染右侧备份包中的工程与对话会话节点 (仅当加载了 .agy 且非纯能力库视图时) ──
                if (_rightSourceMode == RightPaneSourceMode.BackupArchive && _backupIndex != null && _viewFilter != ViewFilterMode.CapabilitiesOnly)
                {
                    Dictionary<string, ConversationSummaryInfo> localConvMap = new Dictionary<string, ConversationSummaryInfo>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < _localSummaries.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(_localSummaries[i].ConversationId))
                            localConvMap[_localSummaries[i].ConversationId] = _localSummaries[i];
                    }

                    for (int i = 0; i < _currentDiffNodes.Count; i++)
                    {
                        ProjectDiffNode pDiff = _currentDiffNodes[i];
                        if (pDiff.BackupConversations == null || pDiff.BackupConversations.Count == 0)
                            continue;

                        bool isJunk = pDiff.IsTemporaryOrJunk;
                        int totalInProj = pDiff.BackupConversations.Count;
                        int checkedTotalInProj = 0;
                        List<ConversationSummaryInfo> visibleConvs = new List<ConversationSummaryInfo>();
                        for (int k = 0; k < pDiff.BackupConversations.Count; k++)
                        {
                            ConversationSummaryInfo bc = pDiff.BackupConversations[k];
                            if (!string.IsNullOrEmpty(bc.ConversationId) && _checkedSessionIds.Contains(bc.ConversationId))
                                checkedTotalInProj++;

                            if (!hideTemp || !bc.IsSubagent)
                            {
                                if (string.IsNullOrEmpty(query) ||
                                    bc.DisplayTitle.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    bc.ConversationId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    pDiff.ProjectName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    visibleConvs.Add(bc);
                                }
                            }
                        }

                        if (visibleConvs.Count == 0 && !string.IsNullOrEmpty(query)) continue;
                        if (hideTemp && isJunk && visibleConvs.Count == 0) continue;
                        if (hideTemp && visibleConvs.Count == 0) continue;

                        int checkedVisible = 0;
                        for (int j = 0; j < visibleConvs.Count; j++)
                        {
                            if (_checkedSessionIds.Contains(visibleConvs[j].ConversationId))
                                checkedVisible++;
                        }

                        bool partialChecked = (checkedVisible > 0 && checkedVisible < visibleConvs.Count) ||
                                              (checkedTotalInProj > 0 && checkedTotalInProj < totalInProj);
                        string partialBadge = partialChecked ? " [部分选中]" : string.Empty;
                        string badge = pDiff.GetStatusBadge();
                        string countText = (visibleConvs.Count != totalInProj)
                            ? string.Format("{0}/{1} 条", visibleConvs.Count, totalInProj)
                            : string.Format("{0} 条", visibleConvs.Count);
                        string pText = string.Format("■ {0} ({1}) {2}{3}", pDiff.ProjectName, countText, badge, partialBadge).Trim();
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

                        string projTooltip = pDiff.LocalOnlySessionCount > 0
                            ? string.Format("本地另有 {0} 条未在备份包中的会话", pDiff.LocalOnlySessionCount)
                            : null;
                        ApplyDiffStyle(pNode, pText, projTooltip, pDiff.Status, isJunk);

                        for (int j = 0; j < visibleConvs.Count; j++)
                        {
                            ConversationSummaryInfo s = visibleConvs[j];
                            ConversationSummaryInfo loc = null;
                            bool existsLocal = !string.IsNullOrEmpty(s.ConversationId) && localConvMap.TryGetValue(s.ConversationId, out loc);

                            DiffStatus sStatus = DiffStatus.Aligned;
                            string diffBadge = "[已对齐]";

                            if (!existsLocal)
                            {
                                sStatus = DiffStatus.NewInBackup;
                                diffBadge = "[新增]";
                            }
                            else if (loc != null)
                            {
                                ConversationDiffDetail detail = VisualDiffEngine.EvaluateConversationDiff(s, loc, pDiff);
                                if (detail.IsModified)
                                {
                                    sStatus = DiffStatus.Modified;
                                    List<string> tags = new List<string>();
                                    if (detail.TitleDiff)
                                    {
                                        string oldT = string.IsNullOrEmpty(loc.DisplayTitle) ? "(空)" : loc.DisplayTitle;
                                        tags.Add("已改名: 本地为 '" + oldT + "'");
                                    }
                                    if (detail.StepDiff)
                                    {
                                        int diff = s.StepCount - loc.StepCount;
                                        if (diff > 0) tags.Add(string.Format("备份多 {0} 步", diff));
                                        else if (diff < 0) tags.Add(string.Format("本地多 {0} 步", -diff));
                                        else tags.Add("对话轮次有差异");
                                    }
                                    else if (detail.TimeDiff)
                                    {
                                        if (s.UpdatedTime > loc.UpdatedTime) tags.Add("备份较新");
                                        else tags.Add("本地较新");
                                    }
                                    if (detail.ProjectDiff)
                                    {
                                        tags.Add("所属工程变更");
                                    }
                                    diffBadge = string.Format("[{0}]", string.Join(", ", tags.ToArray()));
                                }
                                else
                                {
                                    sStatus = DiffStatus.Aligned;
                                    diffBadge = "[已对齐]";
                                }
                            }

                            string timeStr = s.UpdatedTime > DateTime.MinValue ? s.UpdatedTime.ToString("MM-dd HH:mm") : "-";
                            string sText = string.Format("{0} [{1}] {2}", s.DisplayTitle, timeStr, diffBadge);
                            if (s.IsSubagent) sText = "[子任务] " + sText;

                            TreeNode sNode = new TreeNode(sText);
                            sNode.Tag = new DiffItemNodeTag {
                                Category = ItemCategory.Conversation,
                                Id = s.ConversationId,
                                DisplayName = s.DisplayTitle,
                                ProjectName = pDiff.ProjectName,
                                Status = sStatus,
                                SummaryInfo = s,
                                ProjectNode = pDiff,
                                Timestamp = s.UpdatedTime,
                                IsTemporaryOrJunk = s.IsSubagent
                            };

                            sNode.Checked = _checkedSessionIds.Contains(s.ConversationId);

                            string convTooltip = string.Format("会话ID: {0} | 步数: {1} (双击按 Markdown 预览)", s.ConversationId, s.StepCount);
                            ApplyDiffStyle(sNode, sText, convTooltip, sStatus, s.IsSubagent);
                            pNode.Nodes.Add(sNode);
                        }

                        pNode.Checked = (visibleConvs.Count > 0) ? (checkedVisible > 0) : _checkedProjectNames.Contains(pDiff.ProjectName);
                        tvBackup.Nodes.Add(pNode);
                    }
                }

                ApplyTreeViewUiState(tvBackup, uiState, !string.IsNullOrEmpty(query), hadCapturedState);
                if (tvBackup.Nodes.Count > 0) _hasBackupTreeStateCaptured = true;
            }
            finally
            {
                _isUpdatingChecks = false;
                tvBackup.EndUpdate();
            }
            UpdateSelectionStatistics();
        }

        private void ApplyDiffStyle(TreeNode node, string cleanText, string extraTooltip, DiffStatus status, bool isJunk)
        {
            if (isJunk)
            {
                ApplyNodeVisualStyle(node, cleanText, extraTooltip, false, Color.FromArgb(100, 116, 139));
                return;
            }

            switch (status)
            {
                case DiffStatus.NewInBackup:
                    ApplyNodeVisualStyle(node, cleanText, extraTooltip, true, Color.FromArgb(21, 128, 61));
                    break;
                case DiffStatus.Modified:
                    ApplyNodeVisualStyle(node, cleanText, extraTooltip, true, Color.FromArgb(109, 40, 217));
                    break;
                case DiffStatus.Aligned:
                    ApplyNodeVisualStyle(node, cleanText, extraTooltip, false, Color.FromArgb(51, 65, 85));
                    break;
                case DiffStatus.LocalOnly:
                    ApplyNodeVisualStyle(node, cleanText, extraTooltip, false, Color.FromArgb(30, 96, 145));
                    break;
                default:
                    ApplyNodeVisualStyle(node, cleanText, extraTooltip, false, Color.FromArgb(51, 65, 85));
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
                if (n.Nodes.Count > 0)
                {
                    SyncCheckedRecursive(n.Nodes);
                }

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
                        if (!n.Checked)
                        {
                            _checkedProjectNames.Remove(tag.ProjectName);
                        }
                        else
                        {
                            // 过滤隐藏或部分选中时，不得将工程整包加入全局选中集合
                            int totalInProj = (tag.ProjectNode != null && tag.ProjectNode.BackupConversations != null)
                                ? tag.ProjectNode.BackupConversations.Count
                                : 0;
                            if (totalInProj == 0)
                            {
                                _checkedProjectNames.Add(tag.ProjectName);
                            }
                            else
                            {
                                bool allInProjChecked = true;
                                for (int k = 0; k < totalInProj; k++)
                                {
                                    string cid = tag.ProjectNode.BackupConversations[k].ConversationId;
                                    if (!string.IsNullOrEmpty(cid) && !_checkedSessionIds.Contains(cid))
                                    {
                                        allInProjChecked = false;
                                        break;
                                    }
                                }
                                if (allInProjChecked) _checkedProjectNames.Add(tag.ProjectName);
                                else _checkedProjectNames.Remove(tag.ProjectName);
                            }
                        }
                    }
                    else if (tag.Category == ItemCategory.Capability && !string.IsNullOrEmpty(tag.Id))
                    {
                        if (n.Checked) _checkedBackupCapabilityKeys.Add(tag.Id);
                        else _checkedBackupCapabilityKeys.Remove(tag.Id);
                    }
                }
            }
        }

        private void SetAllBackupChecks(bool isChecked)
        {
            _isUpdatingChecks = true;
            try
            {
                _hasInitializedRepoDefaultChecks = true;
                for (int i = 0; i < tvBackup.Nodes.Count; i++)
                {
                    TreeNode root = tvBackup.Nodes[i];
                    root.Checked = isChecked;
                    SetChildNodesChecked(root, isChecked);
                }

                if (!isChecked)
                {
                    // 全不选必须彻底清空持久选中集，防止筛选隐藏项残留误导入
                    _checkedSessionIds.Clear();
                    _checkedProjectNames.Clear();
                    _checkedBackupCapabilityKeys.Clear();
                }
                else
                {
                    SyncCheckedStateToMemory();
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
            Dictionary<string, ConversationSummaryInfo> localConvMap = new Dictionary<string, ConversationSummaryInfo>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _localSummaries.Count; i++)
            {
                if (!string.IsNullOrEmpty(_localSummaries[i].ConversationId))
                    localConvMap[_localSummaries[i].ConversationId] = _localSummaries[i];
            }

            _isUpdatingChecks = true;
            try
            {
                _hasInitializedRepoDefaultChecks = true;
                for (int i = 0; i < tvBackup.Nodes.Count; i++)
                {
                    TreeNode root = tvBackup.Nodes[i];
                    root.Checked = false;
                    SetChildNodesChecked(root, false);
                }
                _checkedSessionIds.Clear();
                _checkedProjectNames.Clear();
                _checkedBackupCapabilityKeys.Clear();
                SelectNewNodesRecursive(tvBackup.Nodes, localConvMap);
                SyncCheckedStateToMemory();
            }
            finally
            {
                _isUpdatingChecks = false;
            }
            UpdateSelectionStatistics();
        }

        private void SelectNewNodesRecursive(TreeNodeCollection nodes, Dictionary<string, ConversationSummaryInfo> localConvMap)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode node = nodes[i];
                DiffItemNodeTag tag = node.Tag as DiffItemNodeTag;
                if (tag != null)
                {
                    if (tag.Category == ItemCategory.Conversation && tag.SummaryInfo != null)
                    {
                        ConversationSummaryInfo loc = null;
                        if (localConvMap != null && !string.IsNullOrEmpty(tag.SummaryInfo.ConversationId))
                            localConvMap.TryGetValue(tag.SummaryInfo.ConversationId, out loc);

                        if (ShouldAutoSelectBackupSession(tag.SummaryInfo, loc, tag.ProjectNode))
                        {
                            node.Checked = true;
                            UpdateParentNodeChecked(node);
                        }
                    }
                    else if (tag.Category == ItemCategory.Capability)
                    {
                        if ((tag.Status == DiffStatus.NewInBackup || tag.Status == DiffStatus.Modified) && !tag.IsTemporaryOrJunk)
                        {
                            node.Checked = true;
                            UpdateParentNodeChecked(node);
                        }
                    }
                    else if (tag.Category == ItemCategory.Project)
                    {
                        if (node.Nodes.Count == 0 && (tag.Status == DiffStatus.NewInBackup || tag.Status == DiffStatus.Modified) && !tag.IsTemporaryOrJunk)
                        {
                            node.Checked = true;
                            UpdateParentNodeChecked(node);
                        }
                    }
                }
                if (node.Nodes.Count > 0)
                {
                    SelectNewNodesRecursive(node.Nodes, localConvMap);
                }
            }
        }

        private void UpdateSelectionStatistics()
        {
            int visProjects = 0;
            int visConvs = 0;
            int visCaps = 0;
            CountCheckedItemsRecursive(tvBackup.Nodes, ref visProjects, ref visConvs, ref visCaps);

            SelectionStatsSummary stats = ComputeSelectionStats(
                _currentCapDiffNodes,
                _rightSourceMode == RightPaneSourceMode.BackupArchive ? _currentDiffNodes : null,
                _checkedBackupCapabilityKeys,
                _checkedSessionIds,
                _checkedProjectNames,
                visCaps,
                visConvs);

            string rightState = _rightSourceMode == RightPaneSourceMode.BuiltInRepository
                ? string.Format("内置仓库 Repository/ ({0} 项能力)", _repoCapabilities.Count)
                : ((_backupIndex != null) ? ("备份包 " + Path.GetFileName(_loadedBackupPath)) : "未加载");

            string hiddenSuffix = stats.HiddenSelectedCount > 0
                ? string.Format(" [含筛选隐藏 {0} 项]", stats.HiddenSelectedCount)
                : string.Empty;

            lblStatsSummary.Text = string.Format(
                "本机: {0} 项能力, {1} 条会话 | 右侧源: {2} | 待导入选中: {3} 项能力, {4} 个工程 ({5} 条会话){6}",
                _localCapabilities.Count, _localSummaries.Count, rightState,
                stats.TotalSelectedCapabilities, stats.TotalSelectedProjects, stats.TotalSelectedConversations, hiddenSuffix
            );
        }

        private void CountCheckedItemsRecursive(TreeNodeCollection nodes, ref int projCount, ref int convCount, ref int capCount)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode n = nodes[i];
                DiffItemNodeTag tag = n.Tag as DiffItemNodeTag;
                if (tag != null && n.Checked)
                {
                    if (tag.Category == ItemCategory.Project) projCount++;
                    else if (tag.Category == ItemCategory.Conversation) convCount++;
                    else if (tag.Category == ItemCategory.Capability) capCount++;
                }
                if (n.Nodes.Count > 0)
                {
                    CountCheckedItemsRecursive(n.Nodes, ref projCount, ref convCount, ref capCount);
                }
            }
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            if (_searchDebounceTimer != null)
            {
                _searchDebounceTimer.Stop();
                _searchDebounceTimer.Start();
            }
            else
            {
                ApplySearchFilter();
            }
        }

        private void ApplySearchFilter()
        {
            RenderLocalTree();
            RenderBackupTree();
        }

        // ══════════════════════════════════════════════════════════
        // 细粒度选择性导入执行 (支持从 Repository/ 或 .agy 导入单品能力与会话)
        // ══════════════════════════════════════════════════════════
        private sealed class ResolvedImportProjectInfo
        {
            public string TargetId;
            public string BackupId;
            public string ProjectName;
            public string FolderUri;
        }

        private sealed class ImportWorkResult
        {
            public int FilesImported;
            public List<string> ImportedCapKeys = new List<string>();
            public List<string> ImportedConvIds = new List<string>();
            public List<string> ImportedProjNames = new List<string>();
            public List<string> Warnings = new List<string>();
        }

        private void BtnPerformSelectiveImport_Click(object sender, EventArgs e)
        {
            PerformSelectiveImport(false);
        }

        private void PerformSelectiveImport(bool forceSelectedNodeOnly)
        {
            if (_worker != null && _worker.IsBusy)
            {
                MessageBox.Show("当前正在执行后台任务，请等待完成后再试。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<ConversationSummaryInfo> selectedConvs = new List<ConversationSummaryInfo>();
            HashSet<string> selectedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<CapabilityItem> selectedCaps = new List<CapabilityItem>();
            Dictionary<string, ProjectDiffNode> convProjDiffMap = new Dictionary<string, ProjectDiffNode>(StringComparer.OrdinalIgnoreCase);
            int hiddenCapsCount = 0;
            int hiddenConvsCount = 0;

            if (!forceSelectedNodeOnly)
            {
                HashSet<string> visibleCapKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> visibleConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> visibleProjNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                CollectVisibleTreeKeys(tvBackup.Nodes, visibleCapKeys, visibleConvIds, visibleProjNames);

                BackupImportSelectionResult resolved = ResolveBackupImportSelectionFromSets(
                    _currentCapDiffNodes,
                    _rightSourceMode == RightPaneSourceMode.BackupArchive ? _currentDiffNodes : null,
                    _checkedBackupCapabilityKeys,
                    _checkedSessionIds,
                    _checkedProjectNames,
                    visibleCapKeys,
                    visibleConvIds,
                    visibleProjNames);

                selectedCaps = resolved.Capabilities;
                selectedConvs = resolved.Conversations;
                selectedProjects = resolved.ProjectNames;
                convProjDiffMap = resolved.ConvProjDiffMap;
                hiddenCapsCount = resolved.HiddenCapabilitiesCount;
                hiddenConvsCount = resolved.HiddenConversationsCount;
            }

            // 仅在“右键单节点”或“未勾选任何复选框”时，读取当前高亮节点
            if ((forceSelectedNodeOnly || (selectedConvs.Count == 0 && selectedCaps.Count == 0)) && tvBackup.SelectedNode != null)
            {
                CollectSubtreeAllItems(tvBackup.SelectedNode, selectedConvs, selectedProjects, selectedCaps, convProjDiffMap);
            }

            // 去重保护
            selectedConvs = DeduplicateConversations(selectedConvs);
            selectedCaps = DeduplicateCapabilities(selectedCaps);

            if (selectedConvs.Count == 0 && selectedCaps.Count == 0)
            {
                MessageBox.Show("请先在右侧「内置能力仓库」或「备份归档包」树中勾选或选中要导入的能力配置或会话！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 统计潜在覆盖与路径缺失警告 (X-01)
            Dictionary<string, CapabilityDiffNode> capDiffByKey = new Dictionary<string, CapabilityDiffNode>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _currentCapDiffNodes.Count; i++)
            {
                CapabilityDiffNode cdn = _currentCapDiffNodes[i];
                if (cdn != null && !string.IsNullOrEmpty(cdn.UniqueKey) && !capDiffByKey.ContainsKey(cdn.UniqueKey))
                {
                    capDiffByKey[cdn.UniqueKey] = cdn;
                }
            }

            HashSet<string> localConvIdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _localSummaries.Count; i++)
            {
                if (!string.IsNullOrEmpty(_localSummaries[i].ConversationId))
                    localConvIdSet.Add(_localSummaries[i].ConversationId);
            }

            int overwriteCapsCount = 0;
            int missingPathCapsCount = 0;
            int overwriteConvsCount = 0;
            List<string> previewLabels = new List<string>();

            for (int i = 0; i < selectedCaps.Count; i++)
            {
                CapabilityItem cap = selectedCaps[i];
                CapabilityDiffNode cdn;
                bool isOverwrite = capDiffByKey.TryGetValue(cap.UniqueKey, out cdn) &&
                                   (cdn.Status == DiffStatus.Modified || cdn.Status == DiffStatus.Aligned);
                if (isOverwrite) overwriteCapsCount++;
                if (cap.HasMissingPaths) missingPathCapsCount++;

                previewLabels.Add(string.Format("[{0}] {1}{2}{3}",
                    CapabilityItem.GetKindShortTag(cap.Kind),
                    cap.DisplayName,
                    isOverwrite ? " (覆盖本机)" : string.Empty,
                    cap.HasMissingPaths ? " [⚠ 路径缺失]" : string.Empty));
            }

            for (int i = 0; i < selectedConvs.Count; i++)
            {
                ConversationSummaryInfo sc = selectedConvs[i];
                bool isOverwrite = !string.IsNullOrEmpty(sc.ConversationId) && localConvIdSet.Contains(sc.ConversationId);
                if (isOverwrite) overwriteConvsCount++;

                previewLabels.Add(string.Format("[会话] {0}{1}",
                    sc.DisplayTitle,
                    isOverwrite ? " (覆盖本机)" : string.Empty));
            }

            StringBuilder confirmMsg = new StringBuilder();
            confirmMsg.AppendFormat("确认导入以下 {0} 项能力配置与 {1} 条对话会话到本机 .gemini 目录？\r\n\r\n",
                selectedCaps.Count, selectedConvs.Count);
            confirmMsg.AppendLine(FormatItemPreviewList(previewLabels, 12));
            if (hiddenCapsCount > 0 || hiddenConvsCount > 0)
            {
                confirmMsg.AppendFormat("\r\n⚠ 提示：其中 {0} 项能力、{1} 条会话当前被搜索或过滤条件隐藏但仍保持勾选；如不打算导入隐藏项，请先点「全不选」后再重新勾选。",
                    hiddenCapsCount, hiddenConvsCount);
            }
            if (overwriteCapsCount > 0 || overwriteConvsCount > 0)
            {
                confirmMsg.AppendFormat("\r\n⚠ 注意：其中 {0} 项能力、{1} 条会话已存在于本机，导入将覆盖对应内容（配置文件会自动生成 .bak 备份）。",
                    overwriteCapsCount, overwriteConvsCount);
            }
            if (missingPathCapsCount > 0)
            {
                confirmMsg.AppendFormat("\r\n⚠ 警告：其中 {0} 项 MCP/Hook 含本机不存在的绝对路径，导入后需按本机路径重定向。",
                    missingPathCapsCount);
            }

            if (MessageBox.Show(confirmMsg.ToString(), "确认选择性导入", MessageBoxButtons.YesNo,
                (missingPathCapsCount > 0 || overwriteCapsCount > 0 || overwriteConvsCount > 0 || hiddenCapsCount > 0 || hiddenConvsCount > 0)
                    ? MessageBoxIcon.Warning
                    : MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            if (!EnsureAntigravityClosed(true)) return;

            // 在 UI 线程预计算所有项目映射与只读快照，避免后台线程竞争 UI/字段状态 (C-33)
            string geminiSnap = _geminiPath;
            string curHomeSnap = HomePath;
            RightPaneSourceMode modeSnap = _rightSourceMode;
            string loadedBackupPathSnap = _loadedBackupPath;
            string srcHomeSnap = _backupIndex != null ? _backupIndex.SourceHome : string.Empty;
            Dictionary<string, ProjectMetadata> localProjectsSnap = new Dictionary<string, ProjectMetadata>(_localProjects, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ProjectMetadata> backupProjectsSnap = (_backupIndex != null && _backupIndex.Projects != null)
                ? new Dictionary<string, ProjectMetadata>(_backupIndex.Projects, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
            List<ProjectDiffNode> diffNodesSnap = new List<ProjectDiffNode>(_currentDiffNodes);

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

            List<string> initCapKeys = new List<string>(selectedCaps.Count);
            for (int c = 0; c < selectedCaps.Count; c++)
            {
                initCapKeys.Add(selectedCaps[c].UniqueKey);
            }

            List<string> initConvIds = new List<string>(selectedConvs.Count);
            List<string> initProjNames = new List<string>();
            HashSet<string> initProjSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < selectedConvs.Count; i++)
            {
                ConversationSummaryInfo sc = selectedConvs[i];
                if (sc == null || string.IsNullOrEmpty(sc.ConversationId)) continue;
                convIdsToExtract.Add(sc.ConversationId);
                convMap[sc.ConversationId] = sc;
                initConvIds.Add(sc.ConversationId);

                ProjectDiffNode pDiff;
                if (!convProjDiffMap.TryGetValue(sc.ConversationId, out pDiff) || pDiff == null)
                {
                    pDiff = FindProjectDiffForBackupConv(diffNodesSnap, sc.ConversationId);
                }

                bool isOutside = VisualDiffEngine.IsOutsideProject(sc.ProjectName) ||
                                 string.Equals(sc.ProjectId, "outside-of-project", StringComparison.OrdinalIgnoreCase);

                if (isOutside)
                {
                    convResolvedProjId[sc.ConversationId] = "outside-of-project";
                    convResolvedProjName[sc.ConversationId] = "Outside of Project";
                    if (initProjSeen.Add("Outside of Project")) initProjNames.Add("Outside of Project");
                    continue;
                }

                string localProjId = null;
                string localProjName = null;
                string localFolderUri = pDiff != null ? pDiff.LocalFolderUri : null;

                // 优先使用已配对本地会话的真实 ProjectId
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

                // 其次按 LocalFolderUri 或重映射后的备份 FolderUri 精确匹配 _localProjects
                if (string.IsNullOrEmpty(localProjId))
                {
                    string lookupUri = !string.IsNullOrEmpty(localFolderUri) ? localFolderUri : remappedBackupUri;
                    if (!string.IsNullOrEmpty(lookupUri))
                    {
                        foreach (KeyValuePair<string, ProjectMetadata> kvp in localProjectsSnap)
                        {
                            if (UriEqualsNormalized(kvp.Value.FolderUri, lookupUri))
                            {
                                localProjId = kvp.Key;
                                localProjName = kvp.Value.Name;
                                localFolderUri = kvp.Value.FolderUri;
                                break;
                            }
                        }
                    }
                }

                // 再次按项目名匹配 _localProjects（仅当未发生不同物理路径冲突且非垃圾项）
                if (string.IsNullOrEmpty(localProjId) && !string.IsNullOrEmpty(sc.ProjectName) && !SmartProjectFilter.IsJunkOrEphemeral(sc.ProjectName))
                {
                    foreach (KeyValuePair<string, ProjectMetadata> kvp in localProjectsSnap)
                    {
                        if (!string.Equals(kvp.Value.Name, sc.ProjectName, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!string.IsNullOrEmpty(remappedBackupUri) && !string.IsNullOrEmpty(kvp.Value.FolderUri) &&
                            !UriEqualsNormalized(kvp.Value.FolderUri, remappedBackupUri))
                            continue;

                        localProjId = kvp.Key;
                        localProjName = kvp.Value.Name;
                        if (string.IsNullOrEmpty(localFolderUri))
                            localFolderUri = kvp.Value.FolderUri;
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
                            if (UriEqualsNormalized(bKvp.Value.FolderUri, sc.FolderUri))
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
                            if (!string.Equals(bKvp.Value.Name, sc.ProjectName, StringComparison.OrdinalIgnoreCase))
                                continue;
                            if (!string.IsNullOrEmpty(sc.FolderUri) && !string.IsNullOrEmpty(bKvp.Value.FolderUri) &&
                                !UriEqualsNormalized(bKvp.Value.FolderUri, sc.FolderUri))
                                continue;
                            backupProjId = bKvp.Key;
                            break;
                        }
                    }
                }

                string effectiveProjName = !string.IsNullOrEmpty(localProjName) ? localProjName : sc.ProjectName;
                string effectiveUri = localFolderUri;
                if (string.IsNullOrEmpty(effectiveUri) && !string.IsNullOrEmpty(remappedBackupUri))
                {
                    effectiveUri = remappedBackupUri;
                }
                if (string.IsNullOrEmpty(effectiveUri) && !string.IsNullOrEmpty(effectiveProjName))
                {
                    effectiveUri = LocalWorkspaceManager.ToFileUri(Path.Combine(curHomeSnap, effectiveProjName));
                }

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
                            ? ("URI:" + UriToDisplayPath(effectiveUri).TrimEnd('\\', '/'))
                            : ("NAME:" + (effectiveProjName ?? sc.ConversationId));
                        if (!generatedProjIdsByUriOrName.TryGetValue(genKey, out effectiveProjId))
                        {
                            effectiveProjId = Guid.NewGuid().ToString();
                            generatedProjIdsByUriOrName[genKey] = effectiveProjId;
                        }
                    }
                }

                // 优先写入无歧义的 CID / ID 前缀键，并对 ProjectName 兜底键实施冲突隔离 (C-05/C-10/C-25)
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
                    initProjNames.Add(effectiveProjName);

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

            _highlightTracker.Clear();
            ProgressForm progress = new ProgressForm("正在导入选中项目...", "正在安全合流所选能力配置与会话数据...");

            _worker = new BackgroundWorker();
            _worker.DoWork += (ws, we) =>
            {
                ImportWorkResult workRes = new ImportWorkResult();
                workRes.ImportedCapKeys.AddRange(initCapKeys);
                workRes.ImportedConvIds.AddRange(initConvIds);
                workRes.ImportedProjNames.AddRange(initProjNames);

                // 1. 细粒度导入选中的单品能力 (Skills, Plugins, Rules, MCP, Hooks — 支持键值级合流与 {{HOME}} 还原)
                if (selectedCaps.Count > 0)
                {
                    progress.UpdateMsg("正在按键值级无损合流所选能力配置 (Skills / Rules / MCP / Plugins / Hooks)...");
                    workRes.FilesImported += CapabilityRepositoryManager.ImportCapabilitiesToLocal(geminiSnap, curHomeSnap, selectedCaps);
                }

                // 2. 若勾选了备份包中的对话会话，执行两阶段会话、工程与脑区增量合流
                if (selectedConvs.Count > 0 && modeSnap == RightPaneSourceMode.BackupArchive &&
                    !string.IsNullOrEmpty(loadedBackupPathSnap) && File.Exists(loadedBackupPathSnap))
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

                    // 建立 config/projects/<backupId>.json -> TargetId 的查表映射
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

                    using (FileStream fs = new FileStream(loadedBackupPathSnap, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
                    {
                        progress.UpdateMsg("正在安全提取工程定义、对话数据库、标注与脑区文件...");
                        for (int i = 0; i < archive.Entries.Count; i++)
                        {
                            ZipArchiveEntry entry = archive.Entries[i];
                            string rawNorm = (entry.FullName ?? string.Empty).Replace('\\', '/').TrimStart('/');
                            if (string.IsNullOrEmpty(rawNorm) || rawNorm.EndsWith("/")) continue;
                            string norm = rawNorm.StartsWith("data/", StringComparison.OrdinalIgnoreCase)
                                ? rawNorm.Substring(5)
                                : rawNorm;

                            if (norm.StartsWith("antigravity/conversations/", StringComparison.OrdinalIgnoreCase))
                            {
                                string fn = norm.Substring("antigravity/conversations/".Length);
                                if (string.IsNullOrEmpty(fn) || fn.IndexOf('/') >= 0) continue;

                                // 跳过 .db-shm 易失共享内存索引，防止 SQLite WAL 损坏 (C-03)
                                if (fn.EndsWith(".db-shm", StringComparison.OrdinalIgnoreCase))
                                    continue;

                                string cid = null;
                                if (fn.EndsWith(".db-wal", StringComparison.OrdinalIgnoreCase))
                                    cid = fn.Substring(0, fn.Length - 7);
                                else if (fn.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                                    cid = fn.Substring(0, fn.Length - 3);
                                else
                                    continue;

                                if (!SessionMetadataExtractor.IsLikelyConversationId(cid) || !convIdsToExtract.Contains(cid))
                                    continue;

                                // 首次覆盖某条会话前，先备份现有 .db 至 .db.bak，再清理本机残留的 .db / .db-wal / .db-shm，防止旧 WAL 回放错乱 (X-03 / C-03)
                                if (cleanedConvIds.Add(cid))
                                {
                                    string existingDbPath = Path.Combine(convDir, cid + ".db");
                                    if (File.Exists(existingDbPath))
                                    {
                                        string dbBakPath = existingDbPath + ".bak";
                                        try
                                        {
                                            if (File.Exists(dbBakPath)) File.SetAttributes(dbBakPath, FileAttributes.Normal);
                                        }
                                        catch { }
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
                                {
                                    extractedDbPaths[cid] = dest;
                                }
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

                        // 阶段 2a-bis：对缺少 annotations/<cid>.pbtxt 的旧版 .agy 归档会话，若拥有自定义或非合成标题，自动合成 Layer 1 标注文件
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
                                    string existingRaw = null;
                                    if (File.Exists(pbtxtDest))
                                    {
                                        using (FileStream efs = new FileStream(pbtxtDest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                                        using (StreamReader esr = new StreamReader(efs, Encoding.UTF8))
                                        {
                                            existingRaw = esr.ReadToEnd();
                                        }
                                    }
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

                        // 阶段 2b：若归档未携带某工程真实 projects/*.json，再以兜底模板生成 (C-30)
                        foreach (KeyValuePair<string, ResolvedImportProjectInfo> kvp in neededProjectsByTargetId)
                        {
                            ResolvedImportProjectInfo pInfo = kvp.Value;
                            string dest = CapabilityRepositoryManager.CombineAndEnsureUnderRoot(projDir, pInfo.TargetId + ".json");
                            if (!File.Exists(dest))
                            {
                                string fallbackJson = GenerateStandardProjectJson(pInfo.TargetId, pInfo.ProjectName, pInfo.FolderUri);
                                LocalWorkspaceManager.WriteUtf8TextAtomicWithBackup(dest, fallbackJson, dest + ".bak");
                                workRes.FilesImported++;
                            }
                        }

                        // 阶段 2c：确保所有 .db 与 .db-wal 均已落盘后，再执行 SQLite 元数据重映射并显式采集失败告警 (C-03)
                        progress.UpdateMsg("正在同步重映射 SQLite 会话数据库元数据与摘要索引 (.pb / summary.sqlite)...");
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
                        byte[] localPbBytes = File.Exists(localPb) ? File.ReadAllBytes(localPb) : new byte[0];

                        byte[] mergedPb = IncrementalProtoShardMerger.MergeShards(
                            localPbBytes, selectedConvs, srcHomeSnap, curHomeSnap, projectUriOverrides, projectNewIds
                        );

                        LocalWorkspaceManager.WriteBytesAtomicWithBackup(localPb, mergedPb, localPb + ".bak");
                        workRes.FilesImported++;

                        // 阶段 2d：若本机存在 conversation_summaries.db，同步 Upsert 导入会话摘要与 FTS 归属
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
                                upsertItem.ParentConversationId = sc.ParentConversationId ?? string.Empty;
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
                                {
                                    workRes.Warnings.AddRange(sumDbWarnings);
                                }
                                if (upserted == 0 && (sumDbWarnings == null || sumDbWarnings.Count == 0) && !string.IsNullOrEmpty(SummarySqliteBridge.LastError))
                                {
                                    workRes.Warnings.Add("同步写入 conversation_summaries.db 失败: " + SummarySqliteBridge.LastError);
                                }
                            }
                        }
                    }
                }

                we.Result = workRes;
            };

            _worker.RunWorkerCompleted += (ws, we) =>
            {
                try
                {
                    progress.SafeClose();
                    progress.Dispose();

                    if (we.Error != null)
                    {
                        Log("导入失败: " + we.Error.Message, Color.FromArgb(220, 38, 38));
                        MessageBox.Show("导入失败:\n" + we.Error.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        ImportWorkResult res = we.Result as ImportWorkResult ?? new ImportWorkResult();
                        for (int i = 0; i < res.ImportedCapKeys.Count; i++)
                        {
                            _highlightTracker.AddImportedCapability(res.ImportedCapKeys[i]);
                            _checkedBackupCapabilityKeys.Remove(res.ImportedCapKeys[i]);
                        }
                        for (int i = 0; i < res.ImportedConvIds.Count; i++)
                        {
                            _highlightTracker.AddImportedConversation(res.ImportedConvIds[i]);
                            _checkedSessionIds.Remove(res.ImportedConvIds[i]);
                        }
                        for (int i = 0; i < res.ImportedProjNames.Count; i++)
                        {
                            _highlightTracker.AddImportedProject(res.ImportedProjNames[i]);
                            _checkedProjectNames.Remove(res.ImportedProjNames[i]);
                        }

                        Log(string.Format("细粒度导入成功！共合流 {0} 项能力配置、{1} 条会话（写入 {2} 个文件/节点）",
                            selectedCaps.Count, selectedConvs.Count, res.FilesImported), Color.FromArgb(21, 128, 61));

                        if (res.Warnings.Count > 0)
                        {
                            for (int w = 0; w < res.Warnings.Count; w++)
                                Log("⚠ " + res.Warnings[w], Color.FromArgb(180, 83, 9));

                            MessageBox.Show(
                                string.Format("选择性导入完成，但存在 {0} 项告警：\n\n{1}\n\n建议检查对应会话或查看底部运行日志。",
                                    res.Warnings.Count, FormatItemPreviewList(res.Warnings, 8)),
                                "导入完成（含告警）", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        else
                        {
                            MessageBox.Show(
                                string.Format("细粒度导入完成！\n\n• 导入能力配置：{0} 项 (按 JSON Key 无损合并并还原当前电脑路径)\n• 导入对话会话：{1} 条\n• 共写入文件/配置节点：{2} 个\n\n新导入的项目已在左侧「本机环境」树中以绿色粗体高亮标出。",
                                    selectedCaps.Count, selectedConvs.Count, res.FilesImported),
                                "导入完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }

                        DetectPaths();
                    }
                }
                finally
                {
                    BackgroundWorker finishedWorker = ws as BackgroundWorker;
                    if (finishedWorker != null) finishedWorker.Dispose();
                    if (object.ReferenceEquals(_worker, finishedWorker)) _worker = null;
                }
            };

            _worker.RunWorkerAsync();
            if (_worker != null && _worker.IsBusy)
            {
                progress.ShowDialog(this);
            }
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
            return string.Equals(UriToDisplayPath(uriA).TrimEnd('\\', '/'), UriToDisplayPath(uriB).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
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
            catch {}
        }

        private static List<ConversationSummaryInfo> DeduplicateConversations(List<ConversationSummaryInfo> list)
        {
            List<ConversationSummaryInfo> result = new List<ConversationSummaryInfo>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < list.Count; i++)
            {
                ConversationSummaryInfo item = list[i];
                if (item != null && !string.IsNullOrEmpty(item.ConversationId) && seen.Add(item.ConversationId))
                    result.Add(item);
            }
            return result;
        }

        private static List<CapabilityItem> DeduplicateCapabilities(List<CapabilityItem> list)
        {
            List<CapabilityItem> result = new List<CapabilityItem>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < list.Count; i++)
            {
                CapabilityItem item = list[i];
                if (item != null && !string.IsNullOrEmpty(item.UniqueKey) && seen.Add(item.UniqueKey))
                    result.Add(item);
            }
            return result;
        }

        public static string GenerateStandardProjectJson(string projId, string projName, string folderUri)
        {
            return WorkspaceService.GenerateStandardProjectJson(projId, projName, folderUri);
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
                    try { File.SetAttributes(destPath, FileAttributes.Normal); } catch {}
                    File.Delete(destPath);
                }
                File.Move(tmpDest, destPath);
            }
            catch
            {
                try { if (File.Exists(tmpDest)) File.Delete(tmpDest); } catch {}
                throw;
            }
        }

        private static void CollectSubtreeAllItems(
            TreeNode rootNode,
            List<ConversationSummaryInfo> convList,
            HashSet<string> projSet,
            List<CapabilityItem> capList,
            Dictionary<string, ProjectDiffNode> convProjDiffMap)
        {
            if (rootNode == null) return;
            DiffItemNodeTag tag = rootNode.Tag as DiffItemNodeTag;
            if (tag != null)
            {
                if (tag.Category == ItemCategory.Conversation && tag.SummaryInfo != null)
                {
                    convList.Add(tag.SummaryInfo);
                    if (!string.IsNullOrEmpty(tag.ProjectName) && projSet != null) projSet.Add(tag.ProjectName);
                    if (convProjDiffMap != null && !string.IsNullOrEmpty(tag.SummaryInfo.ConversationId) && tag.ProjectNode != null)
                        convProjDiffMap[tag.SummaryInfo.ConversationId] = tag.ProjectNode;
                }
                else if (tag.Category == ItemCategory.Project && !string.IsNullOrEmpty(tag.ProjectName) && projSet != null)
                {
                    projSet.Add(tag.ProjectName);
                }
                else if (tag.Category == ItemCategory.Capability && tag.CapabilityInfo != null && capList != null)
                {
                    capList.Add(tag.CapabilityInfo);
                }
            }

            for (int i = 0; i < rootNode.Nodes.Count; i++)
            {
                CollectSubtreeAllItems(rootNode.Nodes[i], convList, projSet, capList, convProjDiffMap);
            }
        }

        private void CollectSelectedItems(
            TreeNodeCollection nodes,
            List<ConversationSummaryInfo> convList,
            HashSet<string> projSet,
            List<CapabilityItem> capList)
        {
            CollectSelectedItems(nodes, convList, projSet, capList, null);
        }

        private void CollectSelectedItems(
            TreeNodeCollection nodes,
            List<ConversationSummaryInfo> convList,
            HashSet<string> projSet,
            List<CapabilityItem> capList,
            Dictionary<string, ProjectDiffNode> convProjDiffMap)
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
                        if (convProjDiffMap != null && !string.IsNullOrEmpty(tag.SummaryInfo.ConversationId) && tag.ProjectNode != null)
                            convProjDiffMap[tag.SummaryInfo.ConversationId] = tag.ProjectNode;
                    }
                    else if (tag.Category == ItemCategory.Project && !string.IsNullOrEmpty(tag.ProjectName))
                    {
                        projSet.Add(tag.ProjectName);
                    }
                    else if (tag.Category == ItemCategory.Capability && tag.CapabilityInfo != null && capList != null)
                    {
                        capList.Add(tag.CapabilityInfo);
                    }
                }
                if (n.Nodes.Count > 0)
                {
                    CollectSelectedItems(n.Nodes, convList, projSet, capList, convProjDiffMap);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        // 一键全量备份导出 (Create Full Backup)
        // ══════════════════════════════════════════════════════════
        private void BtnFullBackup_Click(object sender, EventArgs e)
        {
            if (_worker != null && _worker.IsBusy)
            {
                MessageBox.Show("当前正在执行后台任务，请等待完成后再试。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

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

        private sealed class FullBackupWorkResult
        {
            public int FileCount;
            public List<string> Warnings = new List<string>();
        }

        private void CreateFullBackup(string dest)
        {
            if (_worker != null && _worker.IsBusy)
            {
                MessageBox.Show("当前正在执行后台任务，请等待完成后再试。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string geminiSnap = _geminiPath;
            if (!Directory.Exists(geminiSnap))
            {
                MessageBox.Show("未找到 .gemini 目录: " + geminiSnap, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!EnsureAntigravityClosed(true)) return;

            // UI 线程快照，禁止后台线程触碰控件属性或丢失子智能体会话 (C-11 / C-33 / C-34)
            string homeSnap = HomePath;
            string userSnap = Environment.UserName;
            List<ConversationSummaryInfo> localSumsSnap = new List<ConversationSummaryInfo>(_localSummaries);
            ProgressForm progressDlg = new ProgressForm("正在打包全量导出备份...", "正在执行高速流式压缩，请稍候...");

            _worker = new BackgroundWorker();
            _worker.DoWork += (ws, we) =>
            {
                FullBackupWorkResult backupRes = new FullBackupWorkResult();
                string dDir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(dDir) && !Directory.Exists(dDir)) Directory.CreateDirectory(dDir);

                string tmpDest = dest + ".tmp." + Guid.NewGuid().ToString("N").Substring(0, 8);
                int n = 0;
                try
                {
                    HashSet<string> writtenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    using (FileStream fs = new FileStream(tmpDest, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Create))
                    {
                        // 1. metadata.json
                        if (writtenEntries.Add("metadata.json"))
                        {
                            ZipArchiveEntry metaEntry = archive.CreateEntry("metadata.json", CompressionLevel.Fastest);
                            using (StreamWriter sw = new StreamWriter(metaEntry.Open(), Encoding.UTF8))
                            {
                                string meta = "{\r\n" +
                                              "  \"source_home\": \"" + JsonKeyManipulator.EscapeJsonString(homeSnap) + "\",\r\n" +
                                              "  \"source_user\": \"" + JsonKeyManipulator.EscapeJsonString(userSnap) + "\",\r\n" +
                                              "  \"backup_time\": \"" + JsonKeyManipulator.EscapeJsonString(DateTime.Now.ToString("o")) + "\",\r\n" +
                                              "  \"version\": \"0.7\"\r\n}";
                                sw.Write(meta);
                            }
                            n++;
                        }

                        // 2. 对话与脑区 (保留全部有效会话含子智能体，严格跳过易失 .db-shm)
                        progressDlg.UpdateMsg("正在归档全部有效会话、标题标注与脑区索引...");
                        HashSet<string> validCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < localSumsSnap.Count; i++)
                        {
                            ConversationSummaryInfo s = localSumsSnap[i];
                            if (s == null || string.IsNullOrEmpty(s.ConversationId)) continue;
                            if (SessionMetadataExtractor.IsLikelyConversationId(s.ConversationId))
                            {
                                validCids.Add(s.ConversationId.Trim());
                            }
                        }

                        string convDir = Path.Combine(geminiSnap, "antigravity", "conversations");
                        string brainDir = Path.Combine(geminiSnap, "antigravity", "brain");

                        foreach (string cid in validCids)
                        {
                            string dbFile = Path.Combine(convDir, cid + ".db");
                            n += AddFileToArchive(archive, writtenEntries, dbFile, "data/antigravity/conversations/" + cid + ".db");
                            string walFile = Path.Combine(convDir, cid + ".db-wal");
                            n += AddFileToArchive(archive, writtenEntries, walFile, "data/antigravity/conversations/" + cid + ".db-wal");
                            string bDir = Path.Combine(brainDir, cid);
                            n += AddDirToArchive(archive, writtenEntries, bDir, "data/antigravity/brain/" + cid, false);
                        }

                        string annotationsDir = Path.Combine(geminiSnap, "antigravity", "annotations");
                        if (Directory.Exists(annotationsDir))
                        {
                            string[] pbtxtFiles = Directory.GetFiles(annotationsDir, "*.pbtxt");
                            for (int i = 0; i < pbtxtFiles.Length; i++)
                            {
                                string pf = pbtxtFiles[i];
                                string fn = Path.GetFileName(pf);
                                if (!string.IsNullOrEmpty(fn) && fn.EndsWith(".pbtxt", StringComparison.OrdinalIgnoreCase))
                                {
                                    n += AddFileToArchive(archive, writtenEntries, pf, "data/antigravity/annotations/" + fn);
                                }
                            }
                        }

                        n += AddFileToArchive(archive, writtenEntries,
                            Path.Combine(geminiSnap, "antigravity", "agyhub_summaries_proto.pb"),
                            "data/antigravity/agyhub_summaries_proto.pb");

                        string summaryDbPath = Path.Combine(geminiSnap, "antigravity", "conversation_summaries.db");
                        if (File.Exists(summaryDbPath))
                        {
                            string tempSummaryDb = Path.Combine(Path.GetTempPath(), "agy_sum_bk_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".db");
                            try
                            {
                                progressDlg.UpdateMsg("正在安全快照 conversation_summaries.db...");
                                if (SummarySqliteBridge.BackupDatabaseToFile(summaryDbPath, tempSummaryDb))
                                {
                                    n += AddFileToArchive(archive, writtenEntries, tempSummaryDb, "data/antigravity/conversation_summaries.db");
                                }
                                else
                                {
                                    backupRes.Warnings.Add("归档 conversation_summaries.db 失败（已跳过）: " + (SummarySqliteBridge.LastError ?? "未知错误"));
                                }
                            }
                            finally
                            {
                                DeleteFileIfExistsQuiet(tempSummaryDb);
                                DeleteFileIfExistsQuiet(tempSummaryDb + "-wal");
                                DeleteFileIfExistsQuiet(tempSummaryDb + "-shm");
                                DeleteFileIfExistsQuiet(tempSummaryDb + "-journal");
                            }
                        }

                        // 3. 用户全局规则、项目配置、自定义技能/插件与 MCP 配置 (排除 builtin、installation_id 与敏感缓存)
                        progressDlg.UpdateMsg("正在归档项目配置、用户规则与 MCP 配置...");
                        n += AddFileToArchive(archive, writtenEntries, Path.Combine(geminiSnap, ".geminirules"), "data/.geminirules");
                        n += AddFileToArchive(archive, writtenEntries, Path.Combine(geminiSnap, "GEMINI.md"), "data/GEMINI.md");
                        n += AddFileToArchive(archive, writtenEntries, Path.Combine(geminiSnap, "AGENTS.md"), "data/AGENTS.md");
                        n += AddDirToArchive(archive, writtenEntries, Path.Combine(geminiSnap, "config"), "data/config", true);
                        n += AddDirToArchive(archive, writtenEntries, Path.Combine(geminiSnap, "antigravity", "mcp"), "data/antigravity/mcp", false);
                    }

                    if (File.Exists(dest))
                    {
                        File.SetAttributes(dest, FileAttributes.Normal);
                        File.Delete(dest);
                    }
                    File.Move(tmpDest, dest);
                }
                catch
                {
                    try { if (File.Exists(tmpDest)) File.Delete(tmpDest); } catch {}
                    throw;
                }

                backupRes.FileCount = n;
                we.Result = backupRes;
            };

            _worker.RunWorkerCompleted += (ws, we) =>
            {
                try
                {
                    progressDlg.SafeClose();
                    progressDlg.Dispose();

                    if (we.Error != null)
                    {
                        Log("备份失败: " + we.Error.Message, Color.FromArgb(220, 38, 38));
                        MessageBox.Show("备份失败:\n" + we.Error.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        FullBackupWorkResult bRes = we.Result as FullBackupWorkResult ?? new FullBackupWorkResult();
                        for (int w = 0; w < bRes.Warnings.Count; w++)
                            Log("⚠ " + bRes.Warnings[w], Color.FromArgb(180, 83, 9));

                        Log("全量备份完成！共打包 " + bRes.FileCount + " 个文件 → " + dest, Color.FromArgb(22, 101, 52));
                        if (bRes.Warnings.Count > 0)
                        {
                            MessageBox.Show(
                                string.Format("备份导出完成（含 {0} 项告警）：\n共打包 {1} 个文件\n路径: {2}\n\n告警:\n{3}",
                                    bRes.Warnings.Count, bRes.FileCount, dest, FormatItemPreviewList(bRes.Warnings, 6)),
                                "完成（含告警）", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        else
                        {
                            MessageBox.Show("备份导出成功！\n共打包 " + bRes.FileCount + " 个文件\n路径: " + dest, "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
                finally
                {
                    BackgroundWorker finishedWorker = ws as BackgroundWorker;
                    if (finishedWorker != null) finishedWorker.Dispose();
                    if (object.ReferenceEquals(_worker, finishedWorker)) _worker = null;
                }
            };

            _worker.RunWorkerAsync();
            if (_worker != null && _worker.IsBusy)
            {
                progressDlg.ShowDialog(this);
            }
        }

        private static int AddFileToArchive(ZipArchive archive, HashSet<string> writtenEntries, string filePath, string entryName)
        {
            if (archive == null || string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return 0;

            string normEntry = (entryName ?? string.Empty).Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrEmpty(normEntry) || (writtenEntries != null && !writtenEntries.Add(normEntry)))
                return 0;

            // 先以只读共享流打开本地文件确认可读，避免 CreateEntry 后留空条目 (C-34)
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

        private static Button MakeNeutralBtn(string text, int minWidth)
        {
            Button btn = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(minWidth, 30),
                Padding = new Padding(10, 0, 10, 0),
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btn.FlatAppearance.BorderSize = 1;
            return btn;
        }

        private static Button MakePrimaryBtn(string text, int minWidth)
        {
            Button btn = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(minWidth, 30),
                Padding = new Padding(10, 0, 10, 0),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
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
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 26),
                Padding = new Padding(6, 0, 6, 0),
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = Color.FromArgb(51, 65, 85),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 8.5f),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
            return btn;
        }

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private static readonly string[] ParentProcessNames = new string[] {
            "Antigravity", "Antigravity IDE", "antigravity", "antigravity-hans-windows-amd64", "agy-node"
        };

        private static readonly string[] LanguageServerProcessNames = new string[] {
            "language_server", "language_server_windows_x64"
        };

        internal static string GetProcessImagePathSafe(Process p)
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
            catch {}
            finally
            {
                if (hProc != IntPtr.Zero)
                {
                    try { CloseHandle(hProc); } catch {}
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
                try
                {
                    procs = Process.GetProcessesByName(processNames[n]);
                }
                catch {}

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
                        try { p.Dispose(); } catch {}
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
                    try { p.Dispose(); } catch {}
                }
            }
            return filtered;
        }

        private static void CollectAntigravityProcesses(out List<Process> parentProcs, out List<Process> lsProcs)
        {
            int currentPid;
            using (Process selfProc = Process.GetCurrentProcess())
            {
                currentPid = selfProc.Id;
            }
            parentProcs = CollectTargetProcesses(ParentProcessNames, currentPid);
            lsProcs = CollectAntigravityLanguageServers(currentPid);
        }

        private static void DisposeProcessList(List<Process> list)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                try { list[i].Dispose(); } catch {}
            }
            list.Clear();
        }

        private bool EnsureAntigravityClosed(bool promptUser)
        {
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

                if (promptUser)
                {
                    string msg = (parentProcs.Count > 0 && lsProcs.Count > 0)
                        ? string.Format("检测到 {0} 个 Antigravity 主进程及 {1} 个 Antigravity 语言服务进程正在运行。\n为防止数据库与索引写入冲突，是否先关闭 Antigravity？", parentProcs.Count, lsProcs.Count)
                        : (parentProcs.Count > 0
                            ? string.Format("检测到 {0} 个 Antigravity 相关进程正在运行。\n为防止数据库与索引写入冲突，是否先尝试优雅关闭 Antigravity？", parentProcs.Count)
                            : string.Format("检测到 {0} 个 Antigravity 语言服务进程 (language_server) 仍在后台运行。\n为防止索引与数据库锁冲突，是否终止这些进程？", lsProcs.Count));
                    DialogResult res = MessageBox.Show(msg, "确认关闭 Antigravity", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (res != DialogResult.Yes) return false;
                }

                // 1. 先关闭/终止 parentProcs（防止 IDE 在中途重新拉起 language_server）
                if (parentProcs.Count > 0)
                {
                    for (int i = 0; i < parentProcs.Count; i++)
                    {
                        try
                        {
                            if (parentProcs[i].MainWindowHandle != IntPtr.Zero)
                                parentProcs[i].CloseMainWindow();
                        }
                        catch {}
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
                            catch {}
                        }
                        if (!anyAlive) break;
                    }

                    stillParents = CollectTargetProcesses(ParentProcessNames, currentPid);
                    if (stillParents.Count > 0)
                    {
                        if (promptUser)
                        {
                            DialogResult forceRes = MessageBox.Show(
                                string.Format("仍有 {0} 个 Antigravity 主进程未响应优雅关闭。\n是否强制终止这些残留进程以继续执行当前操作？", stillParents.Count),
                                "确认强制结束进程", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                            if (forceRes != DialogResult.Yes) return false;
                        }

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
                            catch {}
                        }

                        finalParents = CollectTargetProcesses(ParentProcessNames, currentPid);
                        if (finalParents.Count > 0)
                        {
                            MessageBox.Show(
                                "无法完全终止 Antigravity 主进程（可能权限不足）。请手动关闭 Antigravity 后再试，以防数据冲突！",
                                "进程占用提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }
                    }
                }

                // 2. 待 parentProcs 全部退出后，再清理仍残留的 Antigravity language_server 进程
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
                            catch {}
                        }
                        if (!anyLsAlive) break;
                    }

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
                        catch {}
                    }

                    finalLs = CollectAntigravityLanguageServers(currentPid);
                    if (finalLs.Count > 0)
                    {
                        MessageBox.Show(
                            "无法完全终止 Antigravity 语言服务进程（可能权限不足）。请手动结束后再试，以防数据冲突！",
                            "进程占用提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        internal RenameProcessStrategy PromptRenameProcessStrategy(string actionLabel)
        {
            List<Process> parentProcs = null;
            List<Process> lsProcs = null;
            try
            {
                CollectAntigravityProcesses(out parentProcs, out lsProcs);
                if (parentProcs.Count == 0 && lsProcs.Count == 0)
                {
                    return RenameProcessStrategy.CloseAndFullWrite;
                }

                RenameProcessStrategy chosen;
                using (RenameProcessStrategyDialog dlg = new RenameProcessStrategyDialog(actionLabel, parentProcs.Count, lsProcs.Count))
                {
                    IWin32Window owner = (Form.ActiveForm != null && !Form.ActiveForm.IsDisposed) ? (IWin32Window)Form.ActiveForm : this;
                    dlg.ShowDialog(owner);
                    chosen = dlg.SelectedStrategy;
                }

                if (chosen == RenameProcessStrategy.CloseAndFullWrite)
                {
                    if (!EnsureAntigravityClosed(false))
                        return RenameProcessStrategy.Cancel;
                    return RenameProcessStrategy.CloseAndFullWrite;
                }
                return chosen;
            }
            finally
            {
                DisposeProcessList(parentProcs);
                DisposeProcessList(lsProcs);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // F1 ~ F4 & V0.7 模块化能力仓库交互实现
        // ══════════════════════════════════════════════════════════════════
        private static void NormalizeContextMenuSeparators(ContextMenuStrip ctx)
        {
            if (ctx == null) return;
            bool anyVisibleBefore = false;
            ToolStripSeparator lastVisibleSep = null;
            for (int i = 0; i < ctx.Items.Count; i++)
            {
                ToolStripItem item = ctx.Items[i];
                ToolStripSeparator sep = item as ToolStripSeparator;
                if (sep != null)
                {
                    if (!anyVisibleBefore)
                    {
                        sep.Visible = false;
                    }
                    else
                    {
                        sep.Visible = true;
                        lastVisibleSep = sep;
                        anyVisibleBefore = false;
                    }
                }
                else if (item.Available || item.Visible)
                {
                    if (item.Visible)
                    {
                        anyVisibleBefore = true;
                        lastVisibleSep = null;
                    }
                }
            }
            if (lastVisibleSep != null)
            {
                lastVisibleSep.Visible = false;
            }
        }

        private void BuildContextMenus()
        {
            Font menuFont = new Font("Microsoft YaHei UI", 9f);
            Font menuBoldFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);

            ctxLocal = new ContextMenuStrip { Font = menuFont };

            _miLocalSinglePush = new ToolStripMenuItem("存入内置仓库 (→ Repository)...");
            _miLocalSinglePush.Font = menuBoldFont;
            _miLocalSinglePush.ForeColor = Color.FromArgb(30, 96, 145);
            _miLocalSinglePush.Click += (s, e) => ActionPushSelectedLocalToRepository(true);

            _miLocalPreviewCap = new ToolStripMenuItem("预览 / 对比此能力配置内容 (双击/Enter)...");
            _miLocalPreviewCap.Click += (s, e) => ActionPreviewCapability(true);

            _miLocalRelocateCap = new ToolStripMenuItem("重定向此 MCP / Hook 脚本路径...");
            _miLocalRelocateCap.Click += (s, e) => ActionRelocateCapabilityScriptPath(true);

            _miLocalPreviewConv = new ToolStripMenuItem("直接预览会话内容 (Markdown) [双击/Enter]...");
            _miLocalPreviewConv.Font = menuBoldFont;
            _miLocalPreviewConv.Click += (s, e) => ActionPreviewSessionMarkdown(true);

            _miLocalRename = new ToolStripMenuItem("重命名此会话 (F2)...");
            _miLocalRename.Click += (s, e) => ActionRenameLocalSession();

            _miLocalBatchRename = new ToolStripMenuItem("批量重命名工作台...");
            _miLocalBatchRename.ForeColor = Color.FromArgb(30, 96, 145);
            _miLocalBatchRename.Click += (s, e) => ActionOpenBatchRenameWorkbench();

            _miLocalSingleMove = new ToolStripMenuItem("移动此会话/工程会话到其他工程...");
            _miLocalSingleMove.Click += (s, e) => ActionMoveLocalSessionsToProject(true);

            _miLocalRelocateProj = new ToolStripMenuItem("重定向此工程本地路径 / 修复工程注册...");
            _miLocalRelocateProj.Click += (s, e) => ActionRelocateLocalProjectPath();

            _miLocalExportMd = new ToolStripMenuItem("导出此会话对话记录为 Markdown (.md)...");
            _miLocalExportMd.Click += (s, e) => ActionExportLocalSessionMarkdown();

            _miLocalSingleExportAgy = new ToolStripMenuItem("导出此节点为轻量备份包 (.agy)...");
            _miLocalSingleExportAgy.Click += (s, e) => ActionExportSelectedLocalToAgy(true);

            _miLocalSingleDelete = new ToolStripMenuItem("彻底删除此节点 (Delete)...");
            _miLocalSingleDelete.ForeColor = Color.FromArgb(185, 28, 28);
            _miLocalSingleDelete.Click += (s, e) => ActionDeleteSelectedLocal(true);

            _sepLocalBatch = new ToolStripSeparator();
            _miLocalBatchHeader = new ToolStripMenuItem("── 已勾选批量操作 ──") { Enabled = false };

            _miLocalBatchPush = new ToolStripMenuItem("批量存入内置仓库 (→ Repository)...");
            _miLocalBatchPush.ForeColor = Color.FromArgb(30, 96, 145);
            _miLocalBatchPush.Click += (s, e) => ActionPushSelectedLocalToRepository(false);

            _miLocalBatchMove = new ToolStripMenuItem("批量移动已勾选会话到其他工程...");
            _miLocalBatchMove.Click += (s, e) => ActionMoveLocalSessionsToProject(false);

            _miLocalBatchExport = new ToolStripMenuItem("批量导出已勾选为轻量备份包 (.agy)...");
            _miLocalBatchExport.Click += (s, e) => ActionExportSelectedLocalToAgy(false);

            _miLocalBatchDelete = new ToolStripMenuItem("批量彻底删除已勾选本机项...");
            _miLocalBatchDelete.ForeColor = Color.FromArgb(185, 28, 28);
            _miLocalBatchDelete.Click += (s, e) => ActionDeleteSelectedLocal(false);

            ToolStripMenuItem miSelectAllLocal = new ToolStripMenuItem("全选本机项");
            miSelectAllLocal.Click += (s, e) => SetAllLocalChecks(true);

            ToolStripMenuItem miDeselectAllLocal = new ToolStripMenuItem("全不选本机项");
            miDeselectAllLocal.Click += (s, e) => SetAllLocalChecks(false);

            ToolStripMenuItem miOrphanGc = new ToolStripMenuItem("扫描并清理孤儿数据库与脑区 (GC)...");
            miOrphanGc.Click += (s, e) => ActionRunOrphanGarbageCollector();

            ctxLocal.Items.AddRange(new ToolStripItem[] {
                _miLocalSinglePush, _miLocalPreviewCap, _miLocalRelocateCap,
                new ToolStripSeparator(),
                _miLocalPreviewConv, _miLocalRename, _miLocalBatchRename, _miLocalSingleMove, _miLocalRelocateProj,
                new ToolStripSeparator(),
                _miLocalExportMd, _miLocalSingleExportAgy, _miLocalSingleDelete,
                _sepLocalBatch, _miLocalBatchHeader, _miLocalBatchPush, _miLocalBatchMove, _miLocalBatchExport, _miLocalBatchDelete,
                new ToolStripSeparator(),
                miSelectAllLocal, miDeselectAllLocal,
                new ToolStripSeparator(),
                miOrphanGc
            });
            ctxLocal.Opening += CtxLocal_Opening;

            ctxBackup = new ContextMenuStrip { Font = menuFont };

            _miBackupSingleImport = new ToolStripMenuItem("导入此节点到本机 (← Local)...");
            _miBackupSingleImport.Font = menuBoldFont;
            _miBackupSingleImport.ForeColor = Color.FromArgb(21, 128, 61);
            _miBackupSingleImport.Click += (s, e) => PerformSelectiveImport(true);

            _miBackupPreviewCap = new ToolStripMenuItem("预览 / 对比此能力配置内容 (双击/Enter)...");
            _miBackupPreviewCap.Click += (s, e) => ActionPreviewCapability(false);

            _miBackupRelocateCap = new ToolStripMenuItem("重定向仓库中此 MCP / Hook 脚本路径...");
            _miBackupRelocateCap.Click += (s, e) => ActionRelocateCapabilityScriptPath(false);

            _miBackupPreviewConv = new ToolStripMenuItem("直接预览备份会话 (Markdown) [双击/Enter/免导入]...");
            _miBackupPreviewConv.Font = menuBoldFont;
            _miBackupPreviewConv.Click += (s, e) => ActionPreviewSessionMarkdown(false);

            _miBackupExportMd = new ToolStripMenuItem("直接导出备份会话为 Markdown (.md) [免导入]...");
            _miBackupExportMd.Click += (s, e) => ActionExportBackupSessionMarkdown();

            _miBackupSingleRepoDelete = new ToolStripMenuItem("从内置仓库 (Repository/) 删除此项...");
            _miBackupSingleRepoDelete.ForeColor = Color.FromArgb(185, 28, 28);
            _miBackupSingleRepoDelete.Click += (s, e) => ActionDeleteSelectedFromRepo(true);

            _sepBackupBatch = new ToolStripSeparator();
            _miBackupBatchImport = new ToolStripMenuItem("批量导入已勾选项目到本机 (← Local)...");
            _miBackupBatchImport.Font = menuBoldFont;
            _miBackupBatchImport.ForeColor = Color.FromArgb(21, 128, 61);
            _miBackupBatchImport.Click += (s, e) => PerformSelectiveImport(false);

            _miBackupBatchRepoDelete = new ToolStripMenuItem("从内置仓库 (Repository/) 批量删除已勾选...");
            _miBackupBatchRepoDelete.ForeColor = Color.FromArgb(185, 28, 28);
            _miBackupBatchRepoDelete.Click += (s, e) => ActionDeleteSelectedFromRepo(false);

            ToolStripMenuItem miBackupSelectNew = new ToolStripMenuItem("仅选新增与更新项");
            miBackupSelectNew.Click += BtnSelectNewOnly_Click;

            ToolStripMenuItem miBackupSelectAll = new ToolStripMenuItem("全选右侧项");
            miBackupSelectAll.Click += (s, e) => SetAllBackupChecks(true);

            ToolStripMenuItem miBackupDeselectAll = new ToolStripMenuItem("全不选右侧项");
            miBackupDeselectAll.Click += (s, e) => SetAllBackupChecks(false);

            ctxBackup.Items.AddRange(new ToolStripItem[] {
                _miBackupSingleImport, _miBackupPreviewCap, _miBackupRelocateCap,
                new ToolStripSeparator(),
                _miBackupPreviewConv, _miBackupExportMd, _miBackupSingleRepoDelete,
                _sepBackupBatch, _miBackupBatchImport, _miBackupBatchRepoDelete,
                new ToolStripSeparator(),
                miBackupSelectNew, miBackupSelectAll, miBackupDeselectAll
            });
            ctxBackup.Opening += CtxBackup_Opening;
        }

        private void CtxLocal_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool isBusy = (_worker != null && _worker.IsBusy);
            TreeNode sel = tvLocal.SelectedNode;
            DiffItemNodeTag tag = sel != null ? (sel.Tag as DiffItemNodeTag) : null;
            string nodeLabel = (tag != null && !string.IsNullOrEmpty(tag.DisplayName))
                ? tag.DisplayName
                : (sel != null ? sel.Text.Trim() : "当前节点");
            if (nodeLabel.Length > 32) nodeLabel = nodeLabel.Substring(0, 29) + "...";

            bool isCap = tag != null && tag.Category == ItemCategory.Capability && tag.CapabilityInfo != null;
            bool isCapGroup = tag != null && tag.Category == ItemCategory.CapabilityCategoryGroup;
            bool isConv = tag != null && tag.Category == ItemCategory.Conversation && tag.SummaryInfo != null;
            bool isProj = tag != null && tag.Category == ItemCategory.Project;
            bool isMcpOrHook = isCap && (tag.CapabilityInfo.Kind == CapabilityKind.McpServer || tag.CapabilityInfo.Kind == CapabilityKind.Hook);

            _miLocalSinglePush.Visible = isCap || isCapGroup;
            _miLocalSinglePush.Enabled = !isBusy;
            _miLocalSinglePush.Text = string.Format("将 [{0}] 存入内置仓库 (→ Repository)...", nodeLabel);
            _miLocalPreviewCap.Visible = isCap;
            _miLocalRelocateCap.Visible = isMcpOrHook;
            _miLocalRelocateCap.Enabled = !isBusy;
            _miLocalPreviewConv.Visible = isConv;
            _miLocalPreviewConv.Enabled = !isBusy;
            _miLocalRename.Visible = isConv;
            _miLocalRename.Enabled = !isBusy;
            _miLocalSingleMove.Visible = isConv || isProj;
            _miLocalSingleMove.Enabled = !isBusy;
            _miLocalSingleMove.Text = string.Format("移动 [{0}] 会话到其他工程...", nodeLabel);
            _miLocalRelocateProj.Visible = isProj || isConv;
            _miLocalRelocateProj.Enabled = !isBusy;
            _miLocalExportMd.Visible = isConv;
            _miLocalExportMd.Enabled = !isBusy;
            _miLocalSingleExportAgy.Visible = isCap || isCapGroup || isConv || isProj;
            _miLocalSingleExportAgy.Enabled = !isBusy;
            _miLocalSingleExportAgy.Text = string.Format("仅导出 [{0}] 为轻量包 (.agy)...", nodeLabel);
            _miLocalSingleDelete.Visible = isCap || isCapGroup || isConv || isProj;
            _miLocalSingleDelete.Enabled = !isBusy;
            _miLocalSingleDelete.Text = string.Format("彻底删除 [{0}] (Delete)...", nodeLabel);

            List<ConversationSummaryInfo> checkedConvs;
            HashSet<string> checkedConvIds;
            HashSet<string> checkedProjIds;
            HashSet<string> checkedGhostKeys;
            HashSet<string> checkedProjBucketKeys;
            List<string> checkedProjLabels;
            List<CapabilityItem> checkedCaps;
            int hiddenCaps;
            int hiddenConvs;
            GetTargetLocalSelection(false, true, out checkedConvs, out checkedConvIds, out checkedProjIds, out checkedGhostKeys, out checkedProjBucketKeys, out checkedProjLabels, out checkedCaps, out hiddenCaps, out hiddenConvs);

            int totalChecked = checkedCaps.Count + checkedConvIds.Count;
            int totalBatchProjOnly = checkedProjIds.Count + checkedGhostKeys.Count;
            bool hasBatch = totalChecked > 0 || totalBatchProjOnly > 0;

            _miLocalBatchRename.Visible = isConv || isProj || checkedConvIds.Count > 0;
            _miLocalBatchRename.Enabled = !isBusy;

            string hiddenInfo = (hiddenCaps + hiddenConvs > 0)
                ? string.Format("，含筛选隐藏 {0} 项", hiddenCaps + hiddenConvs)
                : string.Empty;

            _sepLocalBatch.Visible = hasBatch;
            _miLocalBatchHeader.Visible = hasBatch;
            _miLocalBatchHeader.Text = string.Format("── 已勾选批量操作（{0} 项能力 / {1} 条会话 / {2} 个完整工程{3}）──",
                checkedCaps.Count, checkedConvIds.Count, totalBatchProjOnly, hiddenInfo);

            _miLocalBatchPush.Visible = checkedCaps.Count > 0;
            _miLocalBatchPush.Enabled = !isBusy;
            _miLocalBatchPush.Text = string.Format("批量将已勾选 {0} 项能力存入仓库...", checkedCaps.Count);

            _miLocalBatchMove.Visible = checkedConvIds.Count > 0;
            _miLocalBatchMove.Enabled = !isBusy;
            _miLocalBatchMove.Text = string.Format("批量移动已勾选 {0} 条会话到其他工程...", checkedConvIds.Count);

            _miLocalBatchExport.Visible = checkedCaps.Count > 0 || checkedConvIds.Count > 0;
            _miLocalBatchExport.Enabled = !isBusy;
            _miLocalBatchExport.Text = string.Format("批量导出已勾选 ({0} 能力 + {1} 会话) 为 .agy...", checkedCaps.Count, checkedConvIds.Count);

            _miLocalBatchDelete.Visible = hasBatch;
            _miLocalBatchDelete.Enabled = !isBusy;
            _miLocalBatchDelete.Text = string.Format("批量彻底删除已勾选 ({0} 能力 / {1} 会话 / {2} 工程)...",
                checkedCaps.Count, checkedConvIds.Count, totalBatchProjOnly);

            NormalizeContextMenuSeparators(ctxLocal);
        }

        private void CtxBackup_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool isBusy = (_worker != null && _worker.IsBusy);
            TreeNode sel = tvBackup.SelectedNode;
            DiffItemNodeTag tag = sel != null ? (sel.Tag as DiffItemNodeTag) : null;
            string nodeLabel = (tag != null && !string.IsNullOrEmpty(tag.DisplayName))
                ? tag.DisplayName
                : (sel != null ? sel.Text.Trim() : "当前节点");
            if (nodeLabel.Length > 32) nodeLabel = nodeLabel.Substring(0, 29) + "...";

            bool isRepoMode = (_rightSourceMode == RightPaneSourceMode.BuiltInRepository);
            bool isCap = tag != null && tag.Category == ItemCategory.Capability && tag.CapabilityInfo != null;
            bool isCapGroup = tag != null && tag.Category == ItemCategory.CapabilityCategoryGroup;
            bool isConv = tag != null && tag.Category == ItemCategory.Conversation && tag.SummaryInfo != null;
            bool isProj = tag != null && tag.Category == ItemCategory.Project;
            bool isMcpOrHook = isCap && (tag.CapabilityInfo.Kind == CapabilityKind.McpServer || tag.CapabilityInfo.Kind == CapabilityKind.Hook);

            _miBackupSingleImport.Visible = isCap || isCapGroup || isConv || isProj;
            _miBackupSingleImport.Enabled = !isBusy;
            _miBackupSingleImport.Text = string.Format("仅导入 [{0}] 到本机 (← Local)...", nodeLabel);
            _miBackupPreviewCap.Visible = isCap;
            _miBackupRelocateCap.Visible = isRepoMode && isMcpOrHook;
            _miBackupRelocateCap.Enabled = !isBusy;
            _miBackupPreviewConv.Visible = !isRepoMode && isConv;
            _miBackupPreviewConv.Enabled = !isBusy;
            _miBackupExportMd.Visible = !isRepoMode && isConv;
            _miBackupExportMd.Enabled = !isBusy;
            _miBackupSingleRepoDelete.Visible = isRepoMode && (isCap || isCapGroup);
            _miBackupSingleRepoDelete.Enabled = !isBusy;
            _miBackupSingleRepoDelete.Text = string.Format("从内置仓库删除 [{0}]...", nodeLabel);

            HashSet<string> visibleCapKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> visibleConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> visibleProjNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectVisibleTreeKeys(tvBackup.Nodes, visibleCapKeys, visibleConvIds, visibleProjNames);

            BackupImportSelectionResult resolved = ResolveBackupImportSelectionFromSets(
                _currentCapDiffNodes,
                !isRepoMode ? _currentDiffNodes : null,
                _checkedBackupCapabilityKeys,
                _checkedSessionIds,
                _checkedProjectNames,
                visibleCapKeys,
                visibleConvIds,
                visibleProjNames);

            int totalChecked = resolved.Capabilities.Count + resolved.Conversations.Count;
            int totalHidden = resolved.HiddenCapabilitiesCount + resolved.HiddenConversationsCount;
            string hiddenTag = totalHidden > 0 ? string.Format(" [含隐藏 {0} 项]", totalHidden) : string.Empty;

            _sepBackupBatch.Visible = totalChecked > 0;
            _miBackupBatchImport.Visible = totalChecked > 0;
            _miBackupBatchImport.Enabled = !isBusy;
            _miBackupBatchImport.Text = string.Format("批量导入已勾选 ({0} 项能力 + {1} 条会话){2}...",
                resolved.Capabilities.Count, resolved.Conversations.Count, hiddenTag);
            _miBackupBatchRepoDelete.Visible = isRepoMode && resolved.Capabilities.Count > 0;
            _miBackupBatchRepoDelete.Enabled = !isBusy;
            _miBackupBatchRepoDelete.Text = string.Format("从内置仓库批量删除已勾选 ({0} 项能力){1}...",
                resolved.Capabilities.Count, hiddenTag);

            NormalizeContextMenuSeparators(ctxBackup);
        }

        private void TvLocal_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_isUpdatingChecks) return;
            _isUpdatingChecks = true;
            try
            {
                SetChildNodesChecked(e.Node, e.Node.Checked);
                UpdateParentNodeChecked(e.Node);
                SyncLocalCheckedRecursive(tvLocal.Nodes);
            }
            finally
            {
                _isUpdatingChecks = false;
            }
            UpdateSelectionStatistics();
        }

        private static string ResolveLocalSummaryBucketKey(ConversationSummaryInfo s)
        {
            if (s == null) return string.Empty;
            string p = string.IsNullOrWhiteSpace(s.ProjectName) ? "Outside of Project" : s.ProjectName;
            string pid = !string.IsNullOrWhiteSpace(s.ProjectId) ? s.ProjectId.Trim() : string.Empty;
            string ghostKey = string.IsNullOrEmpty(pid) ? LocalWorkspaceManager.BuildGhostProjectKey(p, s.FolderUri) : string.Empty;
            return !string.IsNullOrEmpty(pid) ? ("ID:" + pid) : ("GHOST:" + ghostKey);
        }

        private void SyncLocalCheckedRecursive(TreeNodeCollection nodes)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode n = nodes[i];
                if (n.Nodes.Count > 0)
                {
                    SyncLocalCheckedRecursive(n.Nodes);
                }

                DiffItemNodeTag tag = n.Tag as DiffItemNodeTag;
                if (tag != null)
                {
                    if (tag.Category == ItemCategory.Conversation && !string.IsNullOrEmpty(tag.Id))
                    {
                        if (n.Checked) _checkedLocalSessionIds.Add(tag.Id);
                        else _checkedLocalSessionIds.Remove(tag.Id);
                    }
                    else if (tag.Category == ItemCategory.Project)
                    {
                        string bucketKey = (tag.TagData as string) ?? tag.ProjectName;
                        if (!string.IsNullOrEmpty(bucketKey))
                        {
                            if (!n.Checked)
                            {
                                _checkedLocalProjectNames.Remove(bucketKey);
                            }
                            else
                            {
                                int totalInBucket = 0;
                                int checkedInBucket = 0;
                                for (int k = 0; k < _localSummaries.Count; k++)
                                {
                                    ConversationSummaryInfo ls = _localSummaries[k];
                                    if (ls == null || string.IsNullOrEmpty(ls.ConversationId)) continue;
                                    if (string.Equals(ResolveLocalSummaryBucketKey(ls), bucketKey, StringComparison.OrdinalIgnoreCase))
                                    {
                                        totalInBucket++;
                                        if (_checkedLocalSessionIds.Contains(ls.ConversationId)) checkedInBucket++;
                                    }
                                }
                                if (totalInBucket == 0 || checkedInBucket == totalInBucket)
                                    _checkedLocalProjectNames.Add(bucketKey);
                                else
                                    _checkedLocalProjectNames.Remove(bucketKey);
                            }
                        }
                    }
                    else if (tag.Category == ItemCategory.Capability && !string.IsNullOrEmpty(tag.Id))
                    {
                        if (n.Checked) _checkedLocalCapabilityKeys.Add(tag.Id);
                        else _checkedLocalCapabilityKeys.Remove(tag.Id);
                    }
                }
            }
        }

        private void SetAllLocalChecks(bool isChecked)
        {
            _isUpdatingChecks = true;
            try
            {
                for (int i = 0; i < tvLocal.Nodes.Count; i++)
                {
                    TreeNode root = tvLocal.Nodes[i];
                    root.Checked = isChecked;
                    SetChildNodesChecked(root, isChecked);
                }
                if (!isChecked)
                {
                    // 全不选始终彻底清空所有本机持久选中集（含被搜索/过滤隐藏项）
                    _checkedLocalSessionIds.Clear();
                    _checkedLocalProjectNames.Clear();
                    _checkedLocalCapabilityKeys.Clear();
                }
                else
                {
                    SyncLocalCheckedRecursive(tvLocal.Nodes);
                }
            }
            finally
            {
                _isUpdatingChecks = false;
            }
            UpdateSelectionStatistics();
        }

        private void TvLocal_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                ActionDeleteSelectedLocal(false);
            }
            else if (e.KeyCode == Keys.F2)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                ActionRenameLocalSession();
            }
            else if (e.KeyCode == Keys.Enter && tvLocal.SelectedNode != null)
            {
                DiffItemNodeTag tag = tvLocal.SelectedNode.Tag as DiffItemNodeTag;
                if (tag != null)
                {
                    if (tag.Category == ItemCategory.Capability)
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        ActionPreviewCapability(true);
                    }
                    else if (tag.Category == ItemCategory.Conversation)
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        ActionPreviewSessionMarkdown(true);
                    }
                }
            }
        }

        private void TvBackup_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && tvBackup.SelectedNode != null)
            {
                DiffItemNodeTag tag = tvBackup.SelectedNode.Tag as DiffItemNodeTag;
                if (tag != null)
                {
                    if (tag.Category == ItemCategory.Capability)
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        ActionPreviewCapability(false);
                    }
                    else if (tag.Category == ItemCategory.Conversation && _rightSourceMode == RightPaneSourceMode.BackupArchive)
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        ActionPreviewSessionMarkdown(false);
                    }
                }
            }
            else if (e.KeyCode == Keys.Delete && _rightSourceMode == RightPaneSourceMode.BuiltInRepository)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                ActionDeleteSelectedFromRepo(false);
            }
        }

        private LocalSelectionSnapshot CaptureLocalTreeSnapshot()
        {
            LocalSelectionSnapshot snap = new LocalSelectionSnapshot();
            for (int i = 0; i < tvLocal.Nodes.Count; i++)
            {
                TreeNode root = tvLocal.Nodes[i];
                DiffItemNodeTag rootTag = root.Tag as DiffItemNodeTag;
                if (rootTag == null) continue;

                if (rootTag.Category == ItemCategory.CapabilityCategoryGroup)
                {
                    for (int j = 0; j < root.Nodes.Count; j++)
                    {
                        TreeNode cNode = root.Nodes[j];
                        DiffItemNodeTag cTag = cNode.Tag as DiffItemNodeTag;
                        if (cTag != null && cTag.CapabilityInfo != null)
                        {
                            snap.Capabilities.Add(new LocalCapEntry
                            {
                                Item = cTag.CapabilityInfo,
                                Checked = cNode.Checked,
                                Selected = (cNode == tvLocal.SelectedNode),
                                ParentSelected = (root == tvLocal.SelectedNode)
                            });
                        }
                    }
                }
                else if (rootTag.Category == ItemCategory.Project)
                {
                    LocalProjectEntry pEntry = new LocalProjectEntry
                    {
                        ProjectId = rootTag.Id,
                        ProjectName = rootTag.ProjectName,
                        FolderUri = rootTag.SourcePath,
                        DisplayName = rootTag.DisplayName,
                        BucketKey = (rootTag.TagData as string) ?? rootTag.ProjectName,
                        Checked = root.Checked,
                        Selected = (root == tvLocal.SelectedNode)
                    };

                    for (int j = 0; j < root.Nodes.Count; j++)
                    {
                        TreeNode sNode = root.Nodes[j];
                        DiffItemNodeTag sTag = sNode.Tag as DiffItemNodeTag;
                        if (sTag != null && sTag.SummaryInfo != null)
                        {
                            pEntry.Sessions.Add(new LocalSessionEntry
                            {
                                Summary = sTag.SummaryInfo,
                                Checked = sNode.Checked,
                                Selected = (sNode == tvLocal.SelectedNode)
                            });
                        }
                    }
                    snap.Projects.Add(pEntry);
                }
            }
            return snap;
        }

        private void GetTargetLocalSelection(
            bool forceSelectedNodeOnly,
            bool skipFallbackToSelectedNode,
            out List<ConversationSummaryInfo> targetConvs,
            out HashSet<string> targetConvIds,
            out HashSet<string> targetProjectIds,
            out HashSet<string> targetGhostKeys,
            out HashSet<string> targetProjBucketKeys,
            out List<string> targetProjectLabels,
            out List<CapabilityItem> targetCapabilities,
            out int hiddenCapsCount,
            out int hiddenConvsCount)
        {
            LocalSelectionSnapshot snap = CaptureLocalTreeSnapshot();
            LocalActionTargets targets = ResolveLocalActionTargets(snap, forceSelectedNodeOnly, skipFallbackToSelectedNode, _localSummaries);
            hiddenCapsCount = 0;
            hiddenConvsCount = 0;

            if (!forceSelectedNodeOnly)
            {
                targets = ResolveLocalTargetsFromSets(
                    targets,
                    _localCapabilities,
                    _localSummaries,
                    _checkedLocalCapabilityKeys,
                    _checkedLocalSessionIds,
                    _checkedLocalProjectNames,
                    out hiddenCapsCount,
                    out hiddenConvsCount);

                bool hasAnyResolved = targets.Capabilities.Count > 0 || targets.ConversationIds.Count > 0 ||
                                      targets.ProjectIds.Count > 0 || targets.GhostProjectKeys.Count > 0;
                if (!skipFallbackToSelectedNode && !hasAnyResolved)
                {
                    targets = ResolveLocalActionTargets(snap, true, false, _localSummaries);
                }
            }

            targetConvs = targets.Conversations;
            targetConvIds = targets.ConversationIds;
            targetProjectIds = targets.ProjectIds;
            targetGhostKeys = targets.GhostProjectKeys;
            targetProjBucketKeys = targets.ProjectBucketKeys;
            targetProjectLabels = targets.ProjectDisplayLabels;
            targetCapabilities = targets.Capabilities;
        }

        private void ClearCheckedLocalAfterAction(bool forceSelectedNodeOnly, HashSet<string> actedConvIds, HashSet<string> actedProjKeys, List<CapabilityItem> actedCaps)
        {
            if (actedConvIds != null)
            {
                foreach (string cid in actedConvIds) _checkedLocalSessionIds.Remove(cid);
            }
            if (actedProjKeys != null)
            {
                foreach (string pk in actedProjKeys) _checkedLocalProjectNames.Remove(pk);
            }
            if (actedCaps != null)
            {
                for (int i = 0; i < actedCaps.Count; i++)
                {
                    if (actedCaps[i] != null && !string.IsNullOrEmpty(actedCaps[i].UniqueKey))
                        _checkedLocalCapabilityKeys.Remove(actedCaps[i].UniqueKey);
                }
            }
        }

        // ── V0.7-A: 将所选本机能力资产推入内置明文仓库 (Local -> Repository/) ──
        private void ActionPushSelectedLocalToRepository(bool forceSelectedNodeOnly = false)
        {
            if (_worker != null && _worker.IsBusy)
            {
                MessageBox.Show("当前正在执行后台任务，请等待完成后再试。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<ConversationSummaryInfo> targetConvs;
            HashSet<string> targetConvIds;
            HashSet<string> targetProjectIds;
            HashSet<string> targetGhostKeys;
            HashSet<string> targetProjBucketKeys;
            List<string> targetProjLabels;
            List<CapabilityItem> targetCaps;
            int hiddenCaps;
            int hiddenConvs;
            GetTargetLocalSelection(forceSelectedNodeOnly, false, out targetConvs, out targetConvIds, out targetProjectIds, out targetGhostKeys, out targetProjBucketKeys, out targetProjLabels, out targetCaps, out hiddenCaps, out hiddenConvs);

            if (targetCaps.Count == 0)
            {
                MessageBox.Show(
                    "请先在左侧「本机环境」树的五大能力分类（Plugins / Skills / Rules / MCP / Hooks）中勾选或点击选中要存入内置仓库的能力项！\n\n（注：对话会话体积较大，请使用「导出所选(.agy)」打包对话）",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<string> capNames = new List<string>();
            for (int i = 0; i < targetCaps.Count; i++)
            {
                capNames.Add(string.Format("[{0}] {1}", CapabilityItem.GetKindShortTag(targetCaps[i].Kind), targetCaps[i].DisplayName));
            }

            string hiddenNotice = hiddenCaps > 0
                ? string.Format("\r\n\r\n⚠ 提示：其中 {0} 项能力当前被搜索或视图条件隐藏但仍处于勾选状态。", hiddenCaps)
                : string.Empty;

            DialogResult confirm = MessageBox.Show(
                string.Format("即将把以下 {0} 项本机能力配置存入 Antigravity_Companion 内置仓库：\n\n• 仓库目录：{1}\n• 选中项：\n{2}{3}\n\n存入时将自动把本机用户目录泛化为跨电脑通用的 {{{{HOME}}}} 占位符，方便直接通过 Git 或文件夹复制到其他电脑按需导入。是否继续？",
                    targetCaps.Count, _repositoryPath, FormatItemPreviewList(capNames, 12), hiddenNotice),
                "存入内置能力仓库 (→ Repository/)",
                MessageBoxButtons.YesNo,
                hiddenCaps > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                this.Cursor = Cursors.WaitCursor;
                int written = CapabilityRepositoryManager.PushCapabilitiesToRepository(_repositoryPath, HomePath, targetCaps);
                for (int i = 0; i < targetCaps.Count; i++)
                {
                    _highlightTracker.AddImportedCapability(targetCaps[i].UniqueKey);
                }

                ClearCheckedLocalAfterAction(forceSelectedNodeOnly, null, null, targetCaps);
                _rightSourceMode = RightPaneSourceMode.BuiltInRepository;
                DetectPaths();

                Log(string.Format("成功将 {0} 项本机能力存入内置仓库 Repository/（共写入 {1} 个文件）", targetCaps.Count, written), Color.FromArgb(21, 128, 61));
                MessageBox.Show(
                    string.Format("已成功将 {0} 项能力存入内置仓库！\n\n• 仓库路径：{1}\n• 写入文件数：{2} 个\n\n在另一台电脑打开 Antigravity_Companion 时，右侧将自动列出这些能力供任意勾选导入。",
                        targetCaps.Count, _repositoryPath, written),
                    "入库完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("存入内置仓库失败: " + ex.Message, Color.FromArgb(185, 28, 28));
                MessageBox.Show("存入内置仓库失败:\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        // ── V0.7-B: 从内置仓库 (Repository/) 删除所选能力项 ──
        private void ActionDeleteSelectedFromRepo(bool forceSelectedNodeOnly = false)
        {
            if (_worker != null && _worker.IsBusy)
            {
                MessageBox.Show("当前正在执行后台任务，请等待完成后再试。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_rightSourceMode != RightPaneSourceMode.BuiltInRepository)
            {
                MessageBox.Show("当前右侧处于外部 .agy 备份包只读预览模式。若要管理内置仓库，请先点击顶部「内置能力仓库」按钮。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<CapabilityItem> selectedCaps = new List<CapabilityItem>();
            int hiddenCaps = 0;
            if (!forceSelectedNodeOnly)
            {
                HashSet<string> visibleCapKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> visibleConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> visibleProjNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                CollectVisibleTreeKeys(tvBackup.Nodes, visibleCapKeys, visibleConvIds, visibleProjNames);

                BackupImportSelectionResult resolved = ResolveBackupImportSelectionFromSets(
                    _currentCapDiffNodes,
                    null,
                    _checkedBackupCapabilityKeys,
                    _checkedSessionIds,
                    _checkedProjectNames,
                    visibleCapKeys,
                    visibleConvIds,
                    visibleProjNames);
                selectedCaps = resolved.Capabilities;
                hiddenCaps = resolved.HiddenCapabilitiesCount;
            }

            if ((forceSelectedNodeOnly || selectedCaps.Count == 0) && tvBackup.SelectedNode != null)
            {
                List<ConversationSummaryInfo> dummyConvs = new List<ConversationSummaryInfo>();
                HashSet<string> dummyProjs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                CollectSubtreeAllItems(tvBackup.SelectedNode, dummyConvs, dummyProjs, selectedCaps, null);
                hiddenCaps = 0;
            }

            selectedCaps = DeduplicateCapabilities(selectedCaps);
            if (selectedCaps.Count == 0)
            {
                MessageBox.Show("请先在右侧「内置能力仓库」树中勾选或选中要从仓库移除的能力项！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<string> capNames = new List<string>(selectedCaps.Count);
            for (int i = 0; i < selectedCaps.Count; i++)
            {
                capNames.Add(string.Format("[{0}] {1}", CapabilityItem.GetKindShortTag(selectedCaps[i].Kind), selectedCaps[i].DisplayName));
            }

            string hiddenNotice = hiddenCaps > 0
                ? string.Format("\r\n\r\n⚠ 提示：其中 {0} 项能力当前被搜索或过滤隐藏但仍处于勾选状态，也将一并删除！", hiddenCaps)
                : string.Empty;

            DialogResult confirm = MessageBox.Show(
                string.Format("确定要从内置仓库 (Repository/) 中物理删除以下 {0} 项能力吗？\n\n{1}{2}\n\n（注：此操作仅删除仓库目录中的副本，不会影响本机 ~/.gemini 环境）",
                    selectedCaps.Count, FormatItemPreviewList(capNames, 12), hiddenNotice),
                "确认从仓库删除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            int deleted = CapabilityRepositoryManager.DeleteCapabilitiesFromRepository(_repositoryPath, selectedCaps);
            for (int i = 0; i < selectedCaps.Count; i++)
            {
                if (!string.IsNullOrEmpty(selectedCaps[i].UniqueKey))
                    _checkedBackupCapabilityKeys.Remove(selectedCaps[i].UniqueKey);
            }
            DetectPaths();
            Log(string.Format("已从内置仓库 Repository/ 中移除 {0} 项能力", deleted), Color.FromArgb(21, 128, 61));
        }

        // ── V0.7-C: 预览 / 对比能力配置内容 ──
        private void ActionPreviewCapability(bool fromLocal)
        {
            TreeNode sel = fromLocal ? tvLocal.SelectedNode : tvBackup.SelectedNode;
            DiffItemNodeTag tag = sel != null ? (sel.Tag as DiffItemNodeTag) : null;
            if (tag == null || tag.Category != ItemCategory.Capability || tag.CapabilityInfo == null)
            {
                MessageBox.Show("请先点击选中一项具体的能力节点（Plugin / Skill / Rule / MCP / Hook）！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            CapabilityItem targetCap = tag.CapabilityInfo;
            CapabilityItem counterpart = null;

            if (fromLocal)
            {
                List<CapabilityItem> rightCaps = _rightSourceMode == RightPaneSourceMode.BuiltInRepository
                    ? _repoCapabilities
                    : (_backupIndex != null ? _backupIndex.Capabilities : null);
                if (rightCaps != null)
                {
                    counterpart = rightCaps.Find(x => string.Equals(x.UniqueKey, targetCap.UniqueKey, StringComparison.OrdinalIgnoreCase));
                }
            }
            else
            {
                counterpart = _localCapabilities.Find(x => string.Equals(x.UniqueKey, targetCap.UniqueKey, StringComparison.OrdinalIgnoreCase));
            }

            using (CapabilityPreviewDialog dlg = new CapabilityPreviewDialog(targetCap, counterpart, fromLocal))
            {
                dlg.ShowDialog(this);
            }
        }

        // ── V0.7-D: 重定向 MCP / Hook 脚本路径 ──
        private void ActionRelocateCapabilityScriptPath(bool isLocalTarget)
        {
            if (_worker != null && _worker.IsBusy)
            {
                MessageBox.Show("当前正在执行后台任务，请等待完成后再试。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            TreeNode sel = isLocalTarget ? tvLocal.SelectedNode : tvBackup.SelectedNode;
            DiffItemNodeTag tag = sel != null ? (sel.Tag as DiffItemNodeTag) : null;
            if (tag == null || tag.Category != ItemCategory.Capability || tag.CapabilityInfo == null)
            {
                MessageBox.Show("请先点击选中一个 MCP 服务节点或 Hook 钩子节点！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            CapabilityItem cap = tag.CapabilityInfo;
            if (cap.Kind != CapabilityKind.McpServer && cap.Kind != CapabilityKind.Hook)
            {
                MessageBox.Show("仅 MCP 服务节点与 Hook 钩子包含可重定向的外部命令/脚本路径。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!isLocalTarget && _rightSourceMode != RightPaneSourceMode.BuiltInRepository)
            {
                MessageBox.Show("外部 .agy 压缩包为只读归档；请先将该项导入本机或切回内置仓库后再重定向路径。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (RelocateCapabilityPathDialog dlg = new RelocateCapabilityPathDialog(cap))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (isLocalTarget && !EnsureAntigravityClosed(true)) return;

                    if (CapabilityRepositoryManager.RelocateCapabilityPath(
                        _geminiPath, _repositoryPath, isLocalTarget, HomePath, cap, dlg.OldPathValue, dlg.NewPathValue))
                    {
                        _highlightTracker.AddImportedCapability(cap.UniqueKey);
                        DetectPaths();
                        Log(string.Format("已重定向 [{0}] 的脚本路径: {1} → {2}", cap.DisplayName, dlg.OldPathValue, dlg.NewPathValue), Color.FromArgb(21, 128, 61));
                        MessageBox.Show("脚本/工具路径重定向成功！", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("未在目标配置中匹配到原路径或新旧路径相同，未执行写入。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        // ── F1-A: 级联物理删除所选本地能力、会话或工程 ──
        private void ActionDeleteSelectedLocal(bool forceSelectedNodeOnly = false)
        {
            if (_worker != null && _worker.IsBusy)
            {
                MessageBox.Show("当前正在执行后台任务，请等待完成后再试。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<ConversationSummaryInfo> targetConvs;
            HashSet<string> targetConvIds;
            HashSet<string> targetProjectIds;
            HashSet<string> targetGhostKeys;
            HashSet<string> targetProjBucketKeys;
            List<string> targetProjLabels;
            List<CapabilityItem> targetCaps;
            int hiddenCaps;
            int hiddenConvs;
            GetTargetLocalSelection(forceSelectedNodeOnly, false, out targetConvs, out targetConvIds, out targetProjectIds, out targetGhostKeys, out targetProjBucketKeys, out targetProjLabels, out targetCaps, out hiddenCaps, out hiddenConvs);

            if (targetConvIds.Count == 0 && targetProjectIds.Count == 0 && targetGhostKeys.Count == 0 && targetCaps.Count == 0)
            {
                MessageBox.Show("请先在左侧「本机环境」树中勾选或点击选中要删除的能力项、工程或会话！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<string> previewItems = new List<string>();
            for (int i = 0; i < targetCaps.Count; i++)
            {
                previewItems.Add(string.Format("[能力/{0}] {1}", CapabilityItem.GetKindShortTag(targetCaps[i].Kind), targetCaps[i].DisplayName));
            }
            for (int i = 0; i < targetProjLabels.Count; i++)
            {
                previewItems.Add(string.Format("[整工程] {0}", targetProjLabels[i]));
            }
            for (int i = 0; i < targetConvs.Count; i++)
            {
                previewItems.Add(string.Format("[会话] {0}", targetConvs[i].DisplayTitle));
            }

            string hiddenNotice = (hiddenCaps > 0 || hiddenConvs > 0)
                ? string.Format("\r\n\r\n⚠ 警告：其中 {0} 项能力、{1} 条会话当前被搜索或过滤隐藏但仍处于勾选状态，将被一并物理删除！如不想删隐藏项请点「全不选」后重选。", hiddenCaps, hiddenConvs)
                : string.Empty;

            DialogResult confirm = MessageBox.Show(
                string.Format("确定要从本机环境中彻底物理删除以下内容（{0} 项能力 / {1} 个完整工程 / {2} 条会话）吗？\n\n{3}{4}\n\n此操作将清理对应的本机配置文件、JSON 注册节点、.pb 索引条目及 .db/brain 数据。",
                    targetCaps.Count, targetProjectIds.Count + targetGhostKeys.Count, targetConvIds.Count, FormatItemPreviewList(previewItems, 12), hiddenNotice),
                "确认彻底物理删除 (F1)",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;
            if (!EnsureAntigravityClosed(true)) return;

            try
            {
                int deletedCaps = 0;
                if (targetCaps.Count > 0)
                {
                    deletedCaps = CapabilityRepositoryManager.DeleteCapabilitiesFromLocal(_geminiPath, targetCaps);
                }

                DeletionResult res = new DeletionResult();
                if (targetConvIds.Count > 0 || targetProjectIds.Count > 0 || targetGhostKeys.Count > 0)
                {
                    res = LocalWorkspaceManager.DeleteSessionsAndProjects(
                        _geminiPath, _localSummaries, _localProjects, targetConvIds, null, targetProjectIds, targetGhostKeys
                    );
                }

                ClearCheckedLocalAfterAction(forceSelectedNodeOnly, targetConvIds, targetProjBucketKeys, targetCaps);

                Log(string.Format("物理删除完成：移除 {0} 项本机能力，移除 {1} 条会话（含子代理），释放 {2:F2} MB 空间",
                    deletedCaps, res.DeletedSessionCount, res.FreedMegabytes),
                    Color.FromArgb(21, 128, 61));

                if (!string.IsNullOrEmpty(LocalWorkspaceManager.LastWarning))
                {
                    Log("⚠ " + LocalWorkspaceManager.LastWarning, Color.FromArgb(180, 83, 9));
                    MessageBox.Show(
                        string.Format("删除已完成，但存在部分文件锁定/告警：\n\n• 移除本机能力配置：{0} 项\n• 清理会话总数：{1} 条\n• 告警：{2}",
                            deletedCaps, res.DeletedSessionCount, LocalWorkspaceManager.LastWarning),
                        "删除完成（含告警）", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show(
                        string.Format("彻底删除成功！\n\n• 移除本机能力配置：{0} 项\n• 清理会话总数（含子代理）：{1} 条\n• 删除数据库文件：{2} 个\n• 删除脑区文件夹：{3} 个\n• 释放磁盘空间：{4:F2} MB",
                            deletedCaps, res.DeletedSessionCount, res.DeletedDbFilesCount, res.DeletedBrainDirsCount, res.FreedMegabytes),
                        "删除完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                DetectPaths();
            }
            catch (Exception ex)
            {
                Log("删除操作异常: " + ex.Message, Color.FromArgb(185, 28, 28));
                MessageBox.Show("删除失败:\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── F1-B: 扫描并一键清理孤儿数据库与残余脑区 (Orphan GC) ──
        private void ActionRunOrphanGarbageCollector()
        {
            if (_worker != null && _worker.IsBusy)
            {
                MessageBox.Show("当前正在执行后台任务，请等待完成后再试。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrEmpty(_geminiPath) || !Directory.Exists(_geminiPath))
            {
                MessageBox.Show("未检测到有效的本地 .gemini 目录！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!EnsureAntigravityClosed(true)) return;

            try
            {
                OrphanScanResult scan = LocalWorkspaceManager.ScanOrphanedData(_geminiPath);
                if (scan.TotalOrphanItems == 0)
                {
                    Log("孤儿垃圾扫描完成：本地 conversations/ 与 brain/ 非常干净，无任何孤儿残留。", Color.FromArgb(21, 128, 61));
                    MessageBox.Show("扫描完成：本地环境非常干净，未发现任何脱离索引的孤儿数据库或脑区目录！", "孤儿垃圾体检 (GC)", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                DialogResult confirm = MessageBox.Show(
                    string.Format("扫描发现以下已脱离 agyhub_summaries_proto.pb 索引的孤儿残留数据：\n\n• 残留会话 UUID 数量：{0} 个\n• 孤儿数据库文件 (.db/.wal/.shm)：{1} 个\n• 孤儿脑区文件夹 (brain/<uuid>)：{2} 个\n• 可回收磁盘空间：{3:F2} MB\n\n是否立即一键物理清理这些孤儿数据？",
                        scan.OrphanSessionIds.Count, scan.OrphanDbFiles.Count, scan.OrphanBrainDirs.Count, scan.TotalOrphanMegabytes),
                    "孤儿数据库与脑区垃圾清理 (GC)",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirm != DialogResult.Yes) return;

                DeletionResult res = LocalWorkspaceManager.CleanOrphanedData(scan);
                Log(string.Format("孤儿垃圾清理完成：清理 {0} 个数据库文件，{1} 个脑区目录，释放 {2:F2} MB",
                    res.DeletedDbFilesCount, res.DeletedBrainDirsCount, res.FreedMegabytes), Color.FromArgb(21, 128, 61));

                MessageBox.Show(
                    string.Format("孤儿垃圾清理完成！\n\n• 清理孤儿数据库文件：{0} 个\n• 清理孤儿脑区目录：{1} 个\n• 实际释放磁盘空间：{2:F2} MB",
                        res.DeletedDbFilesCount, res.DeletedBrainDirsCount, res.FreedMegabytes),
                    "清理完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("孤儿垃圾扫描/清理失败: " + ex.Message, Color.FromArgb(180, 83, 9));
                MessageBox.Show("孤儿垃圾扫描或清理已安全中止（拒绝在摘要索引异常时误删数据）：\n\n" + ex.Message,
                    "孤儿垃圾体检受保护中止 (GC)", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ── F2-A: 本地会话重命名 (三层持久化 + 进程双模策略) ──
        private void ActionRenameLocalSession()
        {
            if (_worker != null && _worker.IsBusy)
            {
                MessageBox.Show("当前正在执行后台任务，请等待完成后再试。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            TreeNode sel = tvLocal.SelectedNode;
            DiffItemNodeTag tag = sel != null ? (sel.Tag as DiffItemNodeTag) : null;
            if (tag == null || tag.Category != ItemCategory.Conversation || tag.SummaryInfo == null)
            {
                MessageBox.Show("请先在左侧树中点击选中一条需要重命名的会话！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ConversationSummaryInfo info = tag.SummaryInfo;
            using (TextInputDialog dlg = new TextInputDialog("重命名本地会话 (F2)", "请输入新的会话自定义标题（将同步写入 .pbtxt / summary.sqlite / .pb 三层）：", info.DisplayTitle))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string newTitle = dlg.InputValue.Trim();
                    if (string.IsNullOrEmpty(newTitle) || string.Equals(newTitle, info.DisplayTitle, StringComparison.Ordinal))
                        return;

                    RenameProcessStrategy strategy = PromptRenameProcessStrategy("重命名会话");
                    if (strategy == RenameProcessStrategy.Cancel) return;
                    bool allowHotWrite = (strategy == RenameProcessStrategy.HotWritePersistentLayers);

                    Dictionary<string, string> batch = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    batch[info.ConversationId] = newTitle;

                    RenameBatchResult batchRes;
                    bool ok = LocalWorkspaceManager.RenameSessionsBatch(_geminiPath, batch, allowHotWrite, out batchRes);
                    if (ok && batchRes != null && batchRes.RenamedCount > 0)
                    {
                        _highlightTracker.AddImportedConversation(info.ConversationId);
                        string modeNote = allowHotWrite
                            ? "（免关机热写：已更新持久化层，切换工程或重启 Antigravity 后刷新侧边栏）"
                            : "（三层全写完成）";
                        Log(string.Format("会话 [{0}] 已重命名为: {1} {2}", info.ConversationId, newTitle, modeNote), Color.FromArgb(21, 128, 61));

                        if (batchRes.Warnings != null && batchRes.Warnings.Count > 0)
                        {
                            for (int w = 0; w < batchRes.Warnings.Count; w++)
                                Log("⚠ " + batchRes.Warnings[w], Color.FromArgb(180, 83, 9));
                        }

                        DetectPaths();
                    }
                    else if (batchRes != null && batchRes.SkippedRunningIds != null && batchRes.SkippedRunningIds.Count > 0)
                    {
                        MessageBox.Show(
                            "该会话当前处于运行中状态 (RUNNING / NotFullyIdle)，免关机热写模式已自动跳过以避免冲突。\n\n如需强制重命名，请选择「关闭 Antigravity 并三层全写 (推荐)」。",
                            "会话运行中已跳过", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        string warnDetail = !string.IsNullOrEmpty(LocalWorkspaceManager.LastWarning)
                            ? LocalWorkspaceManager.LastWarning
                            : "未能在三层存储中找到或更新该会话节点。";
                        MessageBox.Show(warnDetail, "重命名失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        // ── F2-A+: 批量重命名与 AI 润色工作台 ──
        private void ActionOpenBatchRenameWorkbench()
        {
            if (_worker != null && _worker.IsBusy)
            {
                MessageBox.Show("当前正在执行后台任务，请等待完成后再试。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrEmpty(_geminiPath) || !Directory.Exists(_geminiPath))
            {
                MessageBox.Show("未检测到有效的本地 .gemini 目录！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 读取左侧勾选/选中的工程或会话作为初始缩窄范围（显式忽略能力项复选框）
            HashSet<string> scopedConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool scopedFromExplicitSessions = false;
            string initialProjectName = null;
            bool hideTemp = (chkHideTemp != null && chkHideTemp.Checked);

            Dictionary<string, ConversationSummaryInfo> byCid = new Dictionary<string, ConversationSummaryInfo>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _localSummaries.Count; i++)
            {
                ConversationSummaryInfo s = _localSummaries[i];
                if (s != null && !string.IsNullOrEmpty(s.ConversationId))
                {
                    byCid[s.ConversationId.Trim()] = s;
                }
            }

            for (int i = 0; i < _localSummaries.Count; i++)
            {
                ConversationSummaryInfo s = _localSummaries[i];
                if (s == null || string.IsNullOrEmpty(s.ConversationId)) continue;
                string cid = s.ConversationId.Trim();
                if (_checkedLocalSessionIds.Contains(cid))
                {
                    scopedConvIds.Add(cid);
                    scopedFromExplicitSessions = true;
                }
                else
                {
                    string bk = ResolveLocalSummaryBucketKey(s);
                    if (!string.IsNullOrEmpty(bk) && _checkedLocalProjectNames.Contains(bk))
                    {
                        if (!hideTemp || !s.IsSubagent)
                        {
                            scopedConvIds.Add(cid);
                        }
                    }
                }
            }

            if (scopedConvIds.Count > 0 && _checkedLocalProjectNames.Count == 1 && !scopedFromExplicitSessions)
            {
                string singleName = null;
                bool allSameName = true;
                foreach (string cid in scopedConvIds)
                {
                    ConversationSummaryInfo found;
                    if (!byCid.TryGetValue(cid, out found)) continue;
                    string pName = string.IsNullOrWhiteSpace(found.ProjectName) ? "Outside of Project" : found.ProjectName.Trim();
                    if (singleName == null) singleName = pName;
                    else if (!string.Equals(singleName, pName, StringComparison.OrdinalIgnoreCase))
                    {
                        allSameName = false;
                        break;
                    }
                }
                if (allSameName && !string.IsNullOrEmpty(singleName))
                {
                    initialProjectName = singleName;
                }
            }

            if (scopedConvIds.Count == 0 && tvLocal.SelectedNode != null)
            {
                DiffItemNodeTag tag = tvLocal.SelectedNode.Tag as DiffItemNodeTag;
                if (tag != null)
                {
                    if (tag.Category == ItemCategory.Conversation && tag.SummaryInfo != null && !string.IsNullOrEmpty(tag.SummaryInfo.ConversationId))
                    {
                        scopedConvIds.Add(tag.SummaryInfo.ConversationId.Trim());
                        scopedFromExplicitSessions = true;
                    }
                    else if (tag.Category == ItemCategory.Project)
                    {
                        string targetBk = tag.TagData as string;
                        string targetProjName = string.IsNullOrWhiteSpace(tag.ProjectName) ? "Outside of Project" : tag.ProjectName.Trim();
                        for (int i = 0; i < _localSummaries.Count; i++)
                        {
                            ConversationSummaryInfo s = _localSummaries[i];
                            if (s == null || string.IsNullOrEmpty(s.ConversationId)) continue;
                            if (hideTemp && s.IsSubagent) continue;
                            string sProjName = string.IsNullOrWhiteSpace(s.ProjectName) ? "Outside of Project" : s.ProjectName.Trim();
                            bool match = !string.IsNullOrEmpty(targetBk)
                                ? string.Equals(ResolveLocalSummaryBucketKey(s), targetBk, StringComparison.OrdinalIgnoreCase)
                                : string.Equals(sProjName, targetProjName, StringComparison.OrdinalIgnoreCase);
                            if (match)
                            {
                                scopedConvIds.Add(s.ConversationId.Trim());
                            }
                        }
                        if (scopedConvIds.Count > 0)
                        {
                            initialProjectName = targetProjName;
                        }
                    }
                }
            }

            using (BatchRenameWorkbenchDialog dlg = new BatchRenameWorkbenchDialog(
                _geminiPath,
                _repositoryPath,
                _localSummaries,
                scopedConvIds,
                scopedFromExplicitSessions,
                initialProjectName,
                PromptRenameProcessStrategy))
            {
                dlg.ShowDialog(this);
                if (dlg.AnyDataChanged)
                {
                    Log("批量重命名/回滚工作台已写入变更，正在刷新本机三层元数据视图...", Color.FromArgb(21, 128, 61));
                    DetectPaths();
                }
            }
        }

        // ── F2-B: 会话跨工程迁移与归属重绑定 ──
        private void ActionMoveLocalSessionsToProject(bool forceSelectedNodeOnly = false)
        {
            if (_worker != null && _worker.IsBusy)
            {
                MessageBox.Show("当前正在执行后台任务，请等待完成后再试。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<ConversationSummaryInfo> targetConvs;
            HashSet<string> targetConvIds;
            HashSet<string> targetProjectIds;
            HashSet<string> targetGhostKeys;
            HashSet<string> targetProjBucketKeys;
            List<string> targetProjLabels;
            List<CapabilityItem> targetCaps;
            int hiddenCaps;
            int hiddenConvs;
            GetTargetLocalSelection(forceSelectedNodeOnly, false, out targetConvs, out targetConvIds, out targetProjectIds, out targetGhostKeys, out targetProjBucketKeys, out targetProjLabels, out targetCaps, out hiddenCaps, out hiddenConvs);

            if (targetConvIds.Count == 0)
            {
                MessageBox.Show("请先在左侧树中勾选或选中要迁移归属的会话！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (hiddenConvs > 0)
            {
                DialogResult warnRes = MessageBox.Show(
                    string.Format("当前已勾选的 {0} 条会话中，包含 {1} 条被搜索或过滤条件隐藏的会话，迁移操作将一并移动它们。\r\n\r\n是否继续选择目标工程？",
                        targetConvIds.Count, hiddenConvs),
                    "提示（含筛选隐藏勾选）",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (warnRes != DialogResult.Yes) return;
            }

            using (MoveToProjectDialog dlg = new MoveToProjectDialog(_localProjects, _localSummaries, HomePath))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (!EnsureAntigravityClosed(true)) return;

                    List<string> cidList = new List<string>(targetConvIds);
                    int moved = LocalWorkspaceManager.MoveSessionsToProject(
                        _geminiPath, cidList, dlg.SelectedProjectName, dlg.SelectedProjectId, dlg.SelectedFolderUri
                    );

                    if (moved > 0)
                    {
                        ClearCheckedLocalAfterAction(forceSelectedNodeOnly, targetConvIds, targetProjBucketKeys, null);
                        _highlightTracker.AddImportedProject(dlg.SelectedProjectName);
                        for (int i = 0; i < cidList.Count; i++)
                            _highlightTracker.AddImportedConversation(cidList[i]);

                        Log(string.Format("成功将 {0} 条会话（含子代理）迁移绑定至工程 [{1}]", moved, dlg.SelectedProjectName), Color.FromArgb(21, 128, 61));
                        if (!string.IsNullOrEmpty(LocalWorkspaceManager.LastWarning))
                        {
                            Log("⚠ " + LocalWorkspaceManager.LastWarning, Color.FromArgb(180, 83, 9));
                            MessageBox.Show(
                                string.Format("已迁移 {0} 条会话至工程 [{1}]，但部分 SQLite 更新存在告警：\n\n{2}",
                                    moved, dlg.SelectedProjectName, LocalWorkspaceManager.LastWarning),
                                "迁移完成（含告警）", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        else
                        {
                            MessageBox.Show(string.Format("已成功将 {0} 条会话迁移至工程 [{1}]，并同步更新了 Proto 索引与 SQLite 数据库！", moved, dlg.SelectedProjectName),
                                "迁移完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }

                        DetectPaths();
                    }
                    else if (!string.IsNullOrEmpty(LocalWorkspaceManager.LastWarning))
                    {
                        MessageBox.Show("会话迁移未写入：" + LocalWorkspaceManager.LastWarning, "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        // ── F2-C: 工程本地物理路径重定向与幽灵工程注册修复 ──
        private void ActionRelocateLocalProjectPath()
        {
            if (_worker != null && _worker.IsBusy)
            {
                MessageBox.Show("当前正在执行后台任务，请等待完成后再试。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            TreeNode sel = tvLocal.SelectedNode;
            DiffItemNodeTag tag = sel != null ? (sel.Tag as DiffItemNodeTag) : null;
            if (tag == null || (tag.Category != ItemCategory.Project && tag.Category != ItemCategory.Conversation))
            {
                MessageBox.Show("请先在左侧树中点击选中一个工程（或其下任意会话）！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string projName = tag.ProjectName;
            if (string.IsNullOrEmpty(projName) || VisualDiffEngine.IsOutsideProject(projName))
            {
                MessageBox.Show("「Outside of Project」为全局无工程容器，无需绑定固定物理目录。若要将其下会话移入具体工程，请使用「移动所选会话到其他工程...」。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 优先按真实 ProjectId / SummaryInfo.ProjectId / FolderUri 精确查找，防止同名多工程错配 (C-10)
            string existingProjId = null;
            string currentUri = tag.SourcePath;
            if (!string.IsNullOrEmpty(tag.Id) && _localProjects.ContainsKey(tag.Id))
            {
                existingProjId = tag.Id;
                currentUri = _localProjects[tag.Id].FolderUri;
            }
            else if (tag.SummaryInfo != null && !string.IsNullOrEmpty(tag.SummaryInfo.ProjectId) && _localProjects.ContainsKey(tag.SummaryInfo.ProjectId))
            {
                existingProjId = tag.SummaryInfo.ProjectId;
                currentUri = _localProjects[existingProjId].FolderUri;
            }
            else if (!string.IsNullOrEmpty(tag.SourcePath))
            {
                foreach (KeyValuePair<string, ProjectMetadata> kvp in _localProjects)
                {
                    if (UriEqualsNormalized(kvp.Value.FolderUri, tag.SourcePath))
                    {
                        existingProjId = kvp.Key;
                        currentUri = kvp.Value.FolderUri;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(existingProjId))
            {
                foreach (KeyValuePair<string, ProjectMetadata> kvp in _localProjects)
                {
                    if (string.Equals(kvp.Value.Name, projName, StringComparison.OrdinalIgnoreCase))
                    {
                        existingProjId = kvp.Key;
                        currentUri = kvp.Value.FolderUri;
                        break;
                    }
                }
            }

            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = string.Format("请为工程 [{0}] 选择新的本地工作区物理文件夹（将同步更新 config/projects/<id>.json、.pb 索引及所有关联 .db 数据库）：", projName);
                if (!string.IsNullOrEmpty(currentUri))
                {
                    string decoded = UriToDisplayPath(currentUri);
                    if (Directory.Exists(decoded))
                        fbd.SelectedPath = decoded;
                }

                if (fbd.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(fbd.SelectedPath))
                {
                    if (!EnsureAntigravityClosed(true)) return;

                    int updated = LocalWorkspaceManager.RelocateProjectPath(
                        _geminiPath, existingProjId, projName, fbd.SelectedPath, _localSummaries
                    );

                    _highlightTracker.AddImportedProject(projName);
                    Log(string.Format("工程 [{0}] 路径已重定向至 {1}（同步更新 {2} 条会话）", projName, fbd.SelectedPath, updated), Color.FromArgb(21, 128, 61));
                    if (!string.IsNullOrEmpty(LocalWorkspaceManager.LastWarning))
                    {
                        Log("⚠ " + LocalWorkspaceManager.LastWarning, Color.FromArgb(180, 83, 9));
                        MessageBox.Show(
                            string.Format("工程 [{0}] 物理路径已更新，但部分 SQLite 更新存在告警：\n\n• 新工作区路径：{1}\n• 同步更新会话数：{2} 条\n• 告警：{3}",
                                projName, fbd.SelectedPath, updated, LocalWorkspaceManager.LastWarning),
                            "工程路径重定向（含告警）", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        MessageBox.Show(
                            string.Format("工程 [{0}] 物理路径重定向与注册修复完成！\n\n• 新工作区路径：{1}\n• 同步更新会话数：{2} 条", projName, fbd.SelectedPath, updated),
                            "工程路径重定向完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    DetectPaths();
                }
            }
        }

        // ── F3: 细粒度选择性轻量备份导出 (.agy，支持单选能力、单选会话或组合打包) ──
        private void ActionExportSelectedLocalToAgy(bool forceSelectedNodeOnly = false)
        {
            if (_worker != null && _worker.IsBusy)
            {
                MessageBox.Show("当前正在执行后台任务，请等待完成后再试。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<ConversationSummaryInfo> targetConvs;
            HashSet<string> targetConvIds;
            HashSet<string> targetProjectIds;
            HashSet<string> targetGhostKeys;
            HashSet<string> targetProjBucketKeys;
            List<string> targetProjLabels;
            List<CapabilityItem> targetCaps;
            int hiddenCaps;
            int hiddenConvs;
            GetTargetLocalSelection(forceSelectedNodeOnly, false, out targetConvs, out targetConvIds, out targetProjectIds, out targetGhostKeys, out targetProjBucketKeys, out targetProjLabels, out targetCaps, out hiddenCaps, out hiddenConvs);

            if (targetConvs.Count == 0 && targetCaps.Count == 0)
            {
                MessageBox.Show("请先在左侧「本机环境」树中勾选或选中要导出的能力配置、工程或会话！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<string> previewLabels = new List<string>();
            for (int i = 0; i < targetCaps.Count; i++)
            {
                previewLabels.Add(string.Format("[{0}] {1}", CapabilityItem.GetKindShortTag(targetCaps[i].Kind), targetCaps[i].DisplayName));
            }
            for (int i = 0; i < targetConvs.Count; i++)
            {
                previewLabels.Add(string.Format("[会话] {0}", targetConvs[i].DisplayTitle));
            }

            string hiddenNotice = (hiddenCaps > 0 || hiddenConvs > 0)
                ? string.Format("\r\n\r\n⚠ 提示：其中 {0} 项能力、{1} 条会话当前被搜索或过滤隐藏但仍处于勾选状态。", hiddenCaps, hiddenConvs)
                : string.Empty;

            bool includeAllGlobal = false;
            if (targetCaps.Count == 0 && targetConvs.Count > 0)
            {
                DialogResult incGlobal = MessageBox.Show(
                    string.Format("您当前选中了以下 {0} 条会话：\n\n{1}{2}\n\n是否在备份包中顺便附带本机全部全局能力配置（Plugins/Skills/Rules/MCP/Hooks）？\n• 点击「是」：附带全部全局配置\n• 点击「否」：仅导出纯净会话\n• 点击「取消」：中止导出",
                        targetConvs.Count, FormatItemPreviewList(previewLabels, 12), hiddenNotice),
                    "选择性轻量备份导出 (F3)",
                    MessageBoxButtons.YesNoCancel,
                    (hiddenCaps + hiddenConvs > 0) ? MessageBoxIcon.Warning : MessageBoxIcon.Question);

                if (incGlobal == DialogResult.Cancel) return;
                includeAllGlobal = (incGlobal == DialogResult.Yes);
            }
            else
            {
                DialogResult confirm = MessageBox.Show(
                    string.Format("确认将以下 {0} 项能力与 {1} 条会话打包为轻量备份包 (.agy)？\n\n{2}{3}",
                        targetCaps.Count, targetConvs.Count, FormatItemPreviewList(previewLabels, 12), hiddenNotice),
                    "确认选择性导出 (F3)",
                    MessageBoxButtons.YesNo,
                    (hiddenCaps + hiddenConvs > 0) ? MessageBoxIcon.Warning : MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes) return;
            }

            // 若导出包含会话，必须先检查并关闭 Antigravity，防止热拷 WAL 损坏归档 (C-34)
            if (targetConvs.Count > 0 && !EnsureAntigravityClosed(true)) return;

            SaveFileDialog dlg = new SaveFileDialog
            {
                Filter = "Antigravity 备份包 (*.agy)|*.agy|ZIP 压缩包 (*.zip)|*.zip",
                FileName = string.Format("antigravity_selective_{0}caps_{1}sessions_{2}.agy", targetCaps.Count, targetConvs.Count, DateTime.Now.ToString("yyyyMMdd_HHmmss")),
                Title = "保存选择性轻量备份包 (.agy)"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                this.Cursor = Cursors.WaitCursor;
                int count = LocalWorkspaceManager.ExportSelectiveBackup(
                    _geminiPath, HomePath, dlg.FileName, targetConvs, _localProjects, includeAllGlobal, targetCaps
                );

                ClearCheckedLocalAfterAction(forceSelectedNodeOnly, targetConvIds, targetProjBucketKeys, targetCaps);
                RenderLocalTree();
                UpdateSelectionStatistics();

                Log(string.Format("选择性轻量备份导出成功：共打包 {0} 项能力、{1} 条会话（{2} 个归档条目）→ {3}", targetCaps.Count, targetConvs.Count, count, dlg.FileName), Color.FromArgb(21, 128, 61));
                if (!string.IsNullOrEmpty(LocalWorkspaceManager.LastWarning))
                {
                    Log("⚠ " + LocalWorkspaceManager.LastWarning, Color.FromArgb(180, 83, 9));
                    MessageBox.Show(
                        string.Format("选择性轻量备份包已导出，但部分条目存在跳过/告警：\n\n• 导出单品能力：{0} 项\n• 导出对话会话：{1} 条\n• 包含归档条目：{2} 个\n• 保存路径：{3}\n• 告警：{4}",
                            targetCaps.Count, targetConvs.Count, count, dlg.FileName, LocalWorkspaceManager.LastWarning),
                        "导出完成（含告警）", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show(
                        string.Format("选择性轻量备份包导出成功！\n\n• 导出单品能力：{0} 项\n• 导出对话会话：{1} 条\n• 包含归档条目：{2} 个\n• 保存路径：{3}", targetCaps.Count, targetConvs.Count, count, dlg.FileName),
                        "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                Log("选择性导出失败: " + ex.Message, Color.FromArgb(185, 28, 28));
                MessageBox.Show("选择性导出失败:\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        // ── F4: 异步 Markdown 会话预览与完整导出 (防大对话卡死 UI) ──
        private void ActionPreviewSessionMarkdown(bool fromLocal)
        {
            if (_worker != null && _worker.IsBusy)
            {
                MessageBox.Show("当前正在执行后台任务，请等待完成后再试。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!fromLocal && (_backupIndex == null || string.IsNullOrEmpty(_loadedBackupPath) || _rightSourceMode != RightPaneSourceMode.BackupArchive))
            {
                MessageBox.Show("请先打开一个 .agy 备份归档包！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            TreeNode sel = fromLocal ? tvLocal.SelectedNode : tvBackup.SelectedNode;
            DiffItemNodeTag tag = sel != null ? (sel.Tag as DiffItemNodeTag) : null;
            if (tag == null || tag.Category != ItemCategory.Conversation || tag.SummaryInfo == null)
            {
                MessageBox.Show("请先在树中点击选中一条具体的对话会话！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ConversationSummaryInfo info = tag.SummaryInfo;
            string geminiSnap = _geminiPath;
            string backupSnap = _loadedBackupPath;
            const int MaxPreviewChars = 512 * 1024;

            ProgressForm progress = new ProgressForm("正在生成会话 Markdown 预览...", "正在解析会话数据库与轨迹记录，请稍候...");
            _worker = new BackgroundWorker();
            _worker.DoWork += (ws, we) =>
            {
                bool truncated;
                string md = fromLocal
                    ? TranscriptMarkdownExporter.PreviewLocalSessionMarkdown(info, geminiSnap, MaxPreviewChars, out truncated)
                    : TranscriptMarkdownExporter.PreviewBackupSessionMarkdown(info, backupSnap, MaxPreviewChars, out truncated);
                we.Result = new KeyValuePair<string, bool>(md ?? string.Empty, truncated);
            };
            _worker.RunWorkerCompleted += (ws, we) =>
            {
                BackgroundWorker finishedWorker = ws as BackgroundWorker;
                if (finishedWorker != null) finishedWorker.Dispose();
                if (object.ReferenceEquals(_worker, finishedWorker)) _worker = null;

                progress.SafeClose();
                progress.Dispose();

                if (we.Error != null)
                {
                    Log("生成会话 Markdown 预览失败: " + we.Error.Message, Color.FromArgb(185, 28, 28));
                    MessageBox.Show("生成会话预览失败:\n" + we.Error.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    KeyValuePair<string, bool> res = (we.Result is KeyValuePair<string, bool>)
                        ? (KeyValuePair<string, bool>)we.Result
                        : new KeyValuePair<string, bool>(string.Empty, false);
                    using (MarkdownPreviewDialog dlg = new MarkdownPreviewDialog(
                        info, res.Key, fromLocal, res.Value, () => StartExportSessionMarkdownAsync(info, fromLocal)))
                    {
                        dlg.ShowDialog(this);
                    }
                }
            };
            _worker.RunWorkerAsync();
            if (_worker != null && _worker.IsBusy)
            {
                progress.ShowDialog(this);
            }
        }

        private void ActionExportLocalSessionMarkdown()
        {
            TreeNode sel = tvLocal.SelectedNode;
            DiffItemNodeTag tag = sel != null ? (sel.Tag as DiffItemNodeTag) : null;
            if (tag == null || tag.Category != ItemCategory.Conversation || tag.SummaryInfo == null)
            {
                MessageBox.Show("请先在左侧树中点击选中一条要导出为 Markdown 的会话！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            StartExportSessionMarkdownAsync(tag.SummaryInfo, true);
        }

        private void ActionExportBackupSessionMarkdown()
        {
            if (_backupIndex == null || string.IsNullOrEmpty(_loadedBackupPath))
            {
                MessageBox.Show("请先打开一个 .agy 备份归档包！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            TreeNode sel = tvBackup.SelectedNode;
            DiffItemNodeTag tag = sel != null ? (sel.Tag as DiffItemNodeTag) : null;
            if (tag == null || tag.Category != ItemCategory.Conversation || tag.SummaryInfo == null)
            {
                MessageBox.Show("请先在右侧备份树中点击选中一条要预览/导出的备份会话！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            StartExportSessionMarkdownAsync(tag.SummaryInfo, false);
        }

        private void StartExportSessionMarkdownAsync(ConversationSummaryInfo info, bool fromLocal)
        {
            if (info == null) return;
            if (_worker != null && _worker.IsBusy)
            {
                MessageBox.Show("当前正在执行后台任务，请等待完成后再试。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!fromLocal && (_backupIndex == null || string.IsNullOrEmpty(_loadedBackupPath)))
            {
                MessageBox.Show("请先打开一个 .agy 备份归档包！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            IWin32Window owner = (Form.ActiveForm != null && !Form.ActiveForm.IsDisposed) ? (IWin32Window)Form.ActiveForm : this;
            string safeTitle = SanitizeFileName(info.DisplayTitle);
            string shortId = info.ConversationId.Length >= 8 ? info.ConversationId.Substring(0, 8) : info.ConversationId;
            SaveFileDialog dlg = new SaveFileDialog
            {
                Filter = "Markdown 文档 (*.md)|*.md|所有文件 (*.*)|*.*",
                FileName = string.Format("{0}{1}_{2}.md", fromLocal ? "" : "backup_", safeTitle, shortId),
                Title = fromLocal ? "导出本地会话对话记录为 Markdown (.md)" : "从备份包直接流式导出会话为 Markdown (.md)"
            };

            if (dlg.ShowDialog(owner) != DialogResult.OK) return;

            string outputPath = dlg.FileName;
            string geminiSnap = _geminiPath;
            string backupSnap = _loadedBackupPath;

            ProgressForm progress = new ProgressForm("正在导出会话为 Markdown...", "正在解析全部会话记录并写入文件，请稍候...");
            _worker = new BackgroundWorker();
            _worker.DoWork += (ws, we) =>
            {
                if (fromLocal)
                    TranscriptMarkdownExporter.ExportLocalSessionToMarkdown(info, geminiSnap, outputPath);
                else
                    TranscriptMarkdownExporter.ExportBackupSessionToMarkdown(info, backupSnap, outputPath);
            };
            _worker.RunWorkerCompleted += (ws, we) =>
            {
                BackgroundWorker finishedWorker = ws as BackgroundWorker;
                if (finishedWorker != null) finishedWorker.Dispose();
                if (object.ReferenceEquals(_worker, finishedWorker)) _worker = null;

                progress.SafeClose();
                progress.Dispose();

                IWin32Window msgOwner = (Form.ActiveForm != null && !Form.ActiveForm.IsDisposed) ? (IWin32Window)Form.ActiveForm : this;
                if (we.Error != null)
                {
                    Log((fromLocal ? "导出 Markdown 失败: " : "备份会话导出 Markdown 失败: ") + we.Error.Message, Color.FromArgb(185, 28, 28));
                    MessageBox.Show(msgOwner, "导出 Markdown 失败:\n" + we.Error.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    Log(string.Format("已{0}导出会话 [{1}] 为 Markdown: {2}",
                        fromLocal ? "" : "从备份包免导入流式", info.DisplayTitle, outputPath), Color.FromArgb(21, 128, 61));
                    MessageBox.Show(msgOwner,
                        (fromLocal ? "会话 Markdown 导出成功！\n路径: " : "已从备份归档包直接流式提取并导出为 Markdown！\n路径: ") + outputPath,
                        fromLocal ? "导出完成" : "免导入导出完成",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            };
            _worker.RunWorkerAsync();
            if (_worker != null && _worker.IsBusy)
            {
                progress.ShowDialog(owner);
            }
        }

        private sealed class LocalProjectBucket
        {
            public string BucketKey;
            public string ProjectId;
            public string GhostKey;
            public string ProjectName;
            public string FolderUri;
            public List<ConversationSummaryInfo> Conversations = new List<ConversationSummaryInfo>();
        }

        public sealed class LocalCapEntry
        {
            public CapabilityItem Item;
            public bool Checked;
            public bool Selected;
            public bool ParentSelected;
        }

        public sealed class LocalSessionEntry
        {
            public ConversationSummaryInfo Summary;
            public bool Checked;
            public bool Selected;
        }

        public sealed class LocalProjectEntry
        {
            public string ProjectId;
            public string ProjectName;
            public string FolderUri;
            public string DisplayName;
            public string BucketKey;
            public bool Checked;
            public bool Selected;
            public List<LocalSessionEntry> Sessions = new List<LocalSessionEntry>();
        }

        public sealed class LocalSelectionSnapshot
        {
            public List<LocalCapEntry> Capabilities = new List<LocalCapEntry>();
            public List<LocalProjectEntry> Projects = new List<LocalProjectEntry>();
        }

        public sealed class LocalActionTargets
        {
            public List<ConversationSummaryInfo> Conversations = new List<ConversationSummaryInfo>();
            public HashSet<string> ConversationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> ProjectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> GhostProjectKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> ProjectBucketKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public List<string> ProjectDisplayLabels = new List<string>();
            public List<CapabilityItem> Capabilities = new List<CapabilityItem>();
        }

        public sealed class MoveDialogProjectState
        {
            public string NormalizedName;
            public string FolderDisplayPath;
            public bool FolderEnabled;
            public bool IsOutsideOfProject;
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

        public static string FormatItemPreviewList(IList<string> items, int maxItems = 12)
        {
            if (items == null || items.Count == 0) return string.Empty;
            int limit = maxItems > 0 ? Math.Min(maxItems, items.Count) : items.Count;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < limit; i++)
            {
                if (i > 0) sb.AppendLine();
                sb.Append("  • ").Append(items[i]);
            }
            if (items.Count > limit)
            {
                sb.AppendLine();
                sb.AppendFormat("  ... 以及其余 {0} 项（共 {1} 项）", items.Count - limit, items.Count);
            }
            return sb.ToString();
        }

        public static bool ShouldAutoSelectBackupSession(
            ConversationSummaryInfo backupConv,
            ConversationSummaryInfo localConv,
            ProjectDiffNode pDiff)
        {
            if (backupConv == null) return false;
            if (localConv == null) return true;

            if (backupConv.StepCount > localConv.StepCount) return true;
            if (backupConv.StepCount < localConv.StepCount) return false;

            if (backupConv.UserInteractions > localConv.UserInteractions) return true;
            if (backupConv.UserInteractions < localConv.UserInteractions) return false;

            if (backupConv.UpdatedTime > DateTime.MinValue &&
                (localConv.UpdatedTime == DateTime.MinValue || backupConv.UpdatedTime > localConv.UpdatedTime.AddSeconds(2)))
            {
                return true;
            }

            return false;
        }

        public sealed class SelectionStatsSummary
        {
            public int TotalSelectedCapabilities;
            public int TotalSelectedConversations;
            public int TotalSelectedProjects;
            public int VisibleSelectedCount;
            public int HiddenSelectedCount;
        }

        public sealed class BackupImportSelectionResult
        {
            public List<CapabilityItem> Capabilities = new List<CapabilityItem>();
            public List<ConversationSummaryInfo> Conversations = new List<ConversationSummaryInfo>();
            public HashSet<string> ProjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, ProjectDiffNode> ConvProjDiffMap = new Dictionary<string, ProjectDiffNode>(StringComparer.OrdinalIgnoreCase);
            public int HiddenCapabilitiesCount;
            public int HiddenConversationsCount;
        }

        private static void CollectVisibleTreeKeys(
            TreeNodeCollection nodes,
            HashSet<string> visibleCapKeys,
            HashSet<string> visibleConvIds,
            HashSet<string> visibleProjNames)
        {
            if (nodes == null) return;
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode n = nodes[i];
                DiffItemNodeTag tag = n.Tag as DiffItemNodeTag;
                if (tag != null)
                {
                    if (tag.Category == ItemCategory.Capability && !string.IsNullOrEmpty(tag.Id) && visibleCapKeys != null)
                        visibleCapKeys.Add(tag.Id);
                    else if (tag.Category == ItemCategory.Conversation && !string.IsNullOrEmpty(tag.Id) && visibleConvIds != null)
                        visibleConvIds.Add(tag.Id);
                    else if (tag.Category == ItemCategory.Project && !string.IsNullOrEmpty(tag.ProjectName) && visibleProjNames != null)
                        visibleProjNames.Add(tag.ProjectName);
                }
                if (n.Nodes.Count > 0)
                {
                    CollectVisibleTreeKeys(n.Nodes, visibleCapKeys, visibleConvIds, visibleProjNames);
                }
            }
        }

        public static void ApplyCheckAllOrNone(HashSet<string> targetSet, IEnumerable<string> visibleKeys, bool selectAll)
        {
            if (targetSet == null) return;
            if (!selectAll)
            {
                targetSet.Clear();
                return;
            }
            if (visibleKeys != null)
            {
                foreach (string key in visibleKeys)
                {
                    if (!string.IsNullOrEmpty(key)) targetSet.Add(key);
                }
            }
        }

        public static SelectionStatsSummary ComputeSelectionStats(
            List<CapabilityDiffNode> allCapDiffs,
            List<ProjectDiffNode> allProjDiffs,
            HashSet<string> checkedCapKeys,
            HashSet<string> checkedSessionIds,
            HashSet<string> checkedProjectNames,
            int visibleCheckedCaps,
            int visibleCheckedConvs)
        {
            SelectionStatsSummary summary = new SelectionStatsSummary();
            HashSet<string> seenCaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> seenConvs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> seenProjs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (checkedCapKeys != null)
            {
                if (allCapDiffs != null)
                {
                    for (int i = 0; i < allCapDiffs.Count; i++)
                    {
                        CapabilityDiffNode cd = allCapDiffs[i];
                        if (cd != null && cd.RemoteItem != null && !string.IsNullOrEmpty(cd.UniqueKey) && checkedCapKeys.Contains(cd.UniqueKey))
                            seenCaps.Add(cd.UniqueKey);
                    }
                }
                else
                {
                    foreach (string k in checkedCapKeys)
                    {
                        if (!string.IsNullOrEmpty(k)) seenCaps.Add(k);
                    }
                }
            }

            if (allProjDiffs != null)
            {
                for (int i = 0; i < allProjDiffs.Count; i++)
                {
                    ProjectDiffNode pDiff = allProjDiffs[i];
                    if (pDiff == null || string.IsNullOrEmpty(pDiff.ProjectName)) continue;

                    bool projChecked = checkedProjectNames != null && checkedProjectNames.Contains(pDiff.ProjectName);
                    bool anyChildChecked = false;
                    if (pDiff.BackupConversations != null)
                    {
                        for (int k = 0; k < pDiff.BackupConversations.Count; k++)
                        {
                            ConversationSummaryInfo bc = pDiff.BackupConversations[k];
                            if (bc == null || string.IsNullOrEmpty(bc.ConversationId)) continue;
                            if (projChecked || (checkedSessionIds != null && checkedSessionIds.Contains(bc.ConversationId)))
                            {
                                seenConvs.Add(bc.ConversationId);
                                anyChildChecked = true;
                            }
                        }
                    }
                    if (projChecked || anyChildChecked)
                    {
                        seenProjs.Add(pDiff.ProjectName);
                    }
                }
            }
            else
            {
                if (checkedSessionIds != null)
                {
                    foreach (string sid in checkedSessionIds)
                    {
                        if (!string.IsNullOrEmpty(sid)) seenConvs.Add(sid);
                    }
                }
                if (checkedProjectNames != null)
                {
                    foreach (string pn in checkedProjectNames)
                    {
                        if (!string.IsNullOrEmpty(pn)) seenProjs.Add(pn);
                    }
                }
            }

            summary.TotalSelectedCapabilities = seenCaps.Count;
            summary.TotalSelectedConversations = seenConvs.Count;
            summary.TotalSelectedProjects = seenProjs.Count;
            summary.VisibleSelectedCount = Math.Max(0, visibleCheckedCaps) + Math.Max(0, visibleCheckedConvs);
            int totalLeafSelected = summary.TotalSelectedCapabilities + summary.TotalSelectedConversations;
            summary.HiddenSelectedCount = Math.Max(0, totalLeafSelected - summary.VisibleSelectedCount);
            return summary;
        }

        public static BackupImportSelectionResult ResolveBackupImportSelectionFromSets(
            List<CapabilityDiffNode> allCapDiffs,
            List<ProjectDiffNode> allProjDiffs,
            HashSet<string> checkedCapKeys,
            HashSet<string> checkedSessionIds,
            HashSet<string> checkedProjectNames,
            HashSet<string> visibleCapKeys,
            HashSet<string> visibleSessionIds,
            HashSet<string> visibleProjectNames)
        {
            BackupImportSelectionResult res = new BackupImportSelectionResult();
            HashSet<string> capSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> convSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (allCapDiffs != null && checkedCapKeys != null)
            {
                for (int i = 0; i < allCapDiffs.Count; i++)
                {
                    CapabilityDiffNode cd = allCapDiffs[i];
                    if (cd == null || cd.RemoteItem == null || string.IsNullOrEmpty(cd.UniqueKey)) continue;
                    if (!checkedCapKeys.Contains(cd.UniqueKey)) continue;
                    if (capSeen.Add(cd.UniqueKey))
                    {
                        res.Capabilities.Add(cd.RemoteItem);
                        if (visibleCapKeys != null && !visibleCapKeys.Contains(cd.UniqueKey))
                            res.HiddenCapabilitiesCount++;
                    }
                }
            }

            if (allProjDiffs != null)
            {
                for (int i = 0; i < allProjDiffs.Count; i++)
                {
                    ProjectDiffNode pDiff = allProjDiffs[i];
                    if (pDiff == null) continue;
                    bool wholeProjChecked = !string.IsNullOrEmpty(pDiff.ProjectName) &&
                                            checkedProjectNames != null &&
                                            checkedProjectNames.Contains(pDiff.ProjectName);
                    if (wholeProjChecked)
                        res.ProjectNames.Add(pDiff.ProjectName);

                    if (pDiff.BackupConversations == null) continue;
                    for (int j = 0; j < pDiff.BackupConversations.Count; j++)
                    {
                        ConversationSummaryInfo bc = pDiff.BackupConversations[j];
                        if (bc == null || string.IsNullOrEmpty(bc.ConversationId)) continue;
                        if (wholeProjChecked || (checkedSessionIds != null && checkedSessionIds.Contains(bc.ConversationId)))
                        {
                            if (convSeen.Add(bc.ConversationId))
                            {
                                res.Conversations.Add(bc);
                                res.ConvProjDiffMap[bc.ConversationId] = pDiff;
                                if (!string.IsNullOrEmpty(pDiff.ProjectName))
                                    res.ProjectNames.Add(pDiff.ProjectName);
                                if (visibleSessionIds != null && !visibleSessionIds.Contains(bc.ConversationId))
                                    res.HiddenConversationsCount++;
                            }
                        }
                    }
                }
            }

            return res;
        }

        public static LocalActionTargets ResolveLocalTargetsFromSets(
            LocalActionTargets visibleTargets,
            List<CapabilityItem> allLocalCaps,
            List<ConversationSummaryInfo> allLocalSummaries,
            HashSet<string> checkedLocalCapKeys,
            HashSet<string> checkedLocalSessionIds,
            HashSet<string> checkedLocalProjectBucketKeys,
            out int hiddenCapsCount,
            out int hiddenConvsCount)
        {
            hiddenCapsCount = 0;
            hiddenConvsCount = 0;
            LocalActionTargets merged = new LocalActionTargets();

            HashSet<string> visCapSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> visConvSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> capSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (visibleTargets != null)
            {
                for (int i = 0; i < visibleTargets.Capabilities.Count; i++)
                {
                    CapabilityItem cap = visibleTargets.Capabilities[i];
                    if (cap != null && !string.IsNullOrEmpty(cap.UniqueKey) && capSeen.Add(cap.UniqueKey))
                    {
                        merged.Capabilities.Add(cap);
                        visCapSeen.Add(cap.UniqueKey);
                    }
                }
                for (int i = 0; i < visibleTargets.Conversations.Count; i++)
                {
                    ConversationSummaryInfo cs = visibleTargets.Conversations[i];
                    if (cs != null && !string.IsNullOrEmpty(cs.ConversationId) && merged.ConversationIds.Add(cs.ConversationId))
                    {
                        merged.Conversations.Add(cs);
                        visConvSeen.Add(cs.ConversationId);
                    }
                }
                foreach (string pid in visibleTargets.ProjectIds) merged.ProjectIds.Add(pid);
                foreach (string gk in visibleTargets.GhostProjectKeys) merged.GhostProjectKeys.Add(gk);
                foreach (string bk in visibleTargets.ProjectBucketKeys) merged.ProjectBucketKeys.Add(bk);
                for (int i = 0; i < visibleTargets.ProjectDisplayLabels.Count; i++)
                    merged.ProjectDisplayLabels.Add(visibleTargets.ProjectDisplayLabels[i]);
            }

            if (allLocalCaps != null && checkedLocalCapKeys != null)
            {
                for (int i = 0; i < allLocalCaps.Count; i++)
                {
                    CapabilityItem cap = allLocalCaps[i];
                    if (cap == null || string.IsNullOrEmpty(cap.UniqueKey)) continue;
                    if (!checkedLocalCapKeys.Contains(cap.UniqueKey)) continue;
                    if (capSeen.Add(cap.UniqueKey))
                    {
                        merged.Capabilities.Add(cap);
                        if (!visCapSeen.Contains(cap.UniqueKey)) hiddenCapsCount++;
                    }
                }
            }

            if (allLocalSummaries != null)
            {
                for (int i = 0; i < allLocalSummaries.Count; i++)
                {
                    ConversationSummaryInfo s = allLocalSummaries[i];
                    if (s == null || string.IsNullOrEmpty(s.ConversationId)) continue;
                    string bucketKey = ResolveLocalSummaryBucketKey(s);
                    bool bySession = checkedLocalSessionIds != null && checkedLocalSessionIds.Contains(s.ConversationId);
                    bool byBucket = checkedLocalProjectBucketKeys != null && !string.IsNullOrEmpty(bucketKey) && checkedLocalProjectBucketKeys.Contains(bucketKey);
                    if (bySession || byBucket)
                    {
                        if (merged.ConversationIds.Add(s.ConversationId))
                        {
                            merged.Conversations.Add(s);
                            if (!visConvSeen.Contains(s.ConversationId)) hiddenConvsCount++;
                        }
                    }
                }

                // 仅当同一工程分桶下全部未过滤本地会话均被勾选（或分桶为 0 会话）时，才加入整工程删除集合
                if (checkedLocalProjectBucketKeys != null && checkedLocalProjectBucketKeys.Count > 0)
                {
                    Dictionary<string, List<ConversationSummaryInfo>> bucketMap = new Dictionary<string, List<ConversationSummaryInfo>>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < allLocalSummaries.Count; i++)
                    {
                        ConversationSummaryInfo s = allLocalSummaries[i];
                        if (s == null) continue;
                        string bk = ResolveLocalSummaryBucketKey(s);
                        if (string.IsNullOrEmpty(bk)) continue;
                        List<ConversationSummaryInfo> bList;
                        if (!bucketMap.TryGetValue(bk, out bList))
                        {
                            bList = new List<ConversationSummaryInfo>();
                            bucketMap[bk] = bList;
                        }
                        bList.Add(s);
                    }

                    foreach (string bk in checkedLocalProjectBucketKeys)
                    {
                        List<ConversationSummaryInfo> bList;
                        if (!bucketMap.TryGetValue(bk, out bList) || bList.Count == 0) continue;
                        bool allChecked = true;
                        for (int k = 0; k < bList.Count; k++)
                        {
                            if (!merged.ConversationIds.Contains(bList[k].ConversationId))
                            {
                                allChecked = false;
                                break;
                            }
                        }
                        if (!allChecked) continue;

                        ConversationSummaryInfo sample = bList[0];
                        string projName = string.IsNullOrWhiteSpace(sample.ProjectName) ? "Outside of Project" : sample.ProjectName;
                        if (VisualDiffEngine.IsOutsideProject(projName)) continue;

                        if (bk.StartsWith("ID:", StringComparison.OrdinalIgnoreCase))
                            merged.ProjectIds.Add(bk.Substring(3));
                        else if (bk.StartsWith("GHOST:", StringComparison.OrdinalIgnoreCase))
                            merged.GhostProjectKeys.Add(bk.Substring(6));

                        if (merged.ProjectBucketKeys.Add(bk))
                            merged.ProjectDisplayLabels.Add(projName);
                    }
                }
            }

            return merged;
        }

        public static string ComputeRelocatePathOnSelectionChanged(
            string previousAutoFilledOldPath,
            string currentNewPathText,
            string selectedOldPath)
        {
            string prev = previousAutoFilledOldPath ?? string.Empty;
            string cur = currentNewPathText ?? string.Empty;
            string sel = selectedOldPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(cur) || string.Equals(cur.Trim(), prev.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return sel;
            }
            return currentNewPathText;
        }

        public static LocalActionTargets ResolveLocalActionTargets(
            LocalSelectionSnapshot snap,
            bool forceSelectedNodeOnly,
            bool skipFallbackToSelectedNode,
            List<ConversationSummaryInfo> allLocalSummaries)
        {
            LocalActionTargets targets = new LocalActionTargets();
            if (snap == null) return targets;

            HashSet<string> capSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!forceSelectedNodeOnly)
            {
                for (int i = 0; i < snap.Capabilities.Count; i++)
                {
                    LocalCapEntry c = snap.Capabilities[i];
                    if (c != null && c.Checked && c.Item != null && capSeen.Add(c.Item.UniqueKey))
                    {
                        targets.Capabilities.Add(c.Item);
                    }
                }

                for (int i = 0; i < snap.Projects.Count; i++)
                {
                    LocalProjectEntry p = snap.Projects[i];
                    if (p == null) continue;
                    int checkedChildren = 0;
                    for (int j = 0; j < p.Sessions.Count; j++)
                    {
                        LocalSessionEntry s = p.Sessions[j];
                        if (s != null && s.Checked && s.Summary != null && !string.IsNullOrEmpty(s.Summary.ConversationId))
                        {
                            checkedChildren++;
                            if (targets.ConversationIds.Add(s.Summary.ConversationId))
                                targets.Conversations.Add(s.Summary);
                        }
                    }

                    // 必须对比同一工程分桶下的未过滤总会话数，防止搜索隐藏时勾选局部会话误删整个工程 (X-05)
                    int totalInBucket = 0;
                    if (allLocalSummaries != null)
                    {
                        string pGhost = LocalWorkspaceManager.BuildGhostProjectKey(p.ProjectName, p.FolderUri);
                        for (int k = 0; k < allLocalSummaries.Count; k++)
                        {
                            ConversationSummaryInfo ls = allLocalSummaries[k];
                            if (ls == null || string.IsNullOrEmpty(ls.ConversationId)) continue;
                            bool sameProj = !string.IsNullOrEmpty(p.ProjectId)
                                ? string.Equals(ls.ProjectId, p.ProjectId, StringComparison.OrdinalIgnoreCase)
                                : string.Equals(LocalWorkspaceManager.BuildGhostProjectKey(ls.ProjectName, ls.FolderUri), pGhost, StringComparison.OrdinalIgnoreCase);
                            if (sameProj) totalInBucket++;
                        }
                    }

                    bool allBucketSessionsChecked = (totalInBucket == 0)
                        ? (p.Sessions.Count == 0 || checkedChildren == p.Sessions.Count)
                        : (checkedChildren == totalInBucket);

                    if (p.Checked && allBucketSessionsChecked)
                    {
                        if (!VisualDiffEngine.IsOutsideProject(p.ProjectName))
                        {
                            if (!string.IsNullOrEmpty(p.ProjectId) &&
                                !string.Equals(p.ProjectId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
                            {
                                targets.ProjectIds.Add(p.ProjectId);
                            }
                            else
                            {
                                targets.GhostProjectKeys.Add(LocalWorkspaceManager.BuildGhostProjectKey(p.ProjectName, p.FolderUri));
                            }
                            if (!string.IsNullOrEmpty(p.BucketKey))
                                targets.ProjectBucketKeys.Add(p.BucketKey);
                            targets.ProjectDisplayLabels.Add(!string.IsNullOrEmpty(p.DisplayName) ? p.DisplayName : p.ProjectName);
                        }
                    }
                }
            }

            bool hasCheckedAny = targets.Capabilities.Count > 0 || targets.ConversationIds.Count > 0 ||
                                 targets.ProjectIds.Count > 0 || targets.GhostProjectKeys.Count > 0;

            if (forceSelectedNodeOnly || (!skipFallbackToSelectedNode && !hasCheckedAny))
            {
                for (int i = 0; i < snap.Capabilities.Count; i++)
                {
                    LocalCapEntry c = snap.Capabilities[i];
                    if (c != null && (c.Selected || c.ParentSelected) && c.Item != null && capSeen.Add(c.Item.UniqueKey))
                    {
                        targets.Capabilities.Add(c.Item);
                    }
                }

                for (int i = 0; i < snap.Projects.Count; i++)
                {
                    LocalProjectEntry p = snap.Projects[i];
                    if (p == null) continue;

                    if (p.Selected)
                    {
                        if (!VisualDiffEngine.IsOutsideProject(p.ProjectName))
                        {
                            if (!string.IsNullOrEmpty(p.ProjectId) &&
                                !string.Equals(p.ProjectId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
                            {
                                targets.ProjectIds.Add(p.ProjectId);
                            }
                            else
                            {
                                targets.GhostProjectKeys.Add(LocalWorkspaceManager.BuildGhostProjectKey(p.ProjectName, p.FolderUri));
                            }
                            if (!string.IsNullOrEmpty(p.BucketKey))
                                targets.ProjectBucketKeys.Add(p.BucketKey);
                            targets.ProjectDisplayLabels.Add(!string.IsNullOrEmpty(p.DisplayName) ? p.DisplayName : p.ProjectName);
                        }

                        for (int j = 0; j < p.Sessions.Count; j++)
                        {
                            LocalSessionEntry s = p.Sessions[j];
                            if (s != null && s.Summary != null && !string.IsNullOrEmpty(s.Summary.ConversationId))
                            {
                                if (targets.ConversationIds.Add(s.Summary.ConversationId))
                                    targets.Conversations.Add(s.Summary);
                            }
                        }

                        // 若整工程被选中（可能含折叠或隐藏的子任务），补齐同一归属的全部本地会话 (C-10)
                        if (allLocalSummaries != null)
                        {
                            string pGhost = LocalWorkspaceManager.BuildGhostProjectKey(p.ProjectName, p.FolderUri);
                            for (int k = 0; k < allLocalSummaries.Count; k++)
                            {
                                ConversationSummaryInfo ls = allLocalSummaries[k];
                                if (ls == null || string.IsNullOrEmpty(ls.ConversationId)) continue;
                                bool sameProj = !string.IsNullOrEmpty(p.ProjectId)
                                    ? string.Equals(ls.ProjectId, p.ProjectId, StringComparison.OrdinalIgnoreCase)
                                    : string.Equals(LocalWorkspaceManager.BuildGhostProjectKey(ls.ProjectName, ls.FolderUri), pGhost, StringComparison.OrdinalIgnoreCase);
                                if (sameProj && targets.ConversationIds.Add(ls.ConversationId))
                                    targets.Conversations.Add(ls);
                            }
                        }
                    }
                    else
                    {
                        for (int j = 0; j < p.Sessions.Count; j++)
                        {
                            LocalSessionEntry s = p.Sessions[j];
                            if (s != null && s.Selected && s.Summary != null && !string.IsNullOrEmpty(s.Summary.ConversationId))
                            {
                                if (targets.ConversationIds.Add(s.Summary.ConversationId))
                                    targets.Conversations.Add(s.Summary);
                            }
                        }
                    }
                }
            }

            return targets;
        }

        public static MoveDialogProjectState ComputeMoveDialogProjectState(
            string projectNameInput,
            Dictionary<string, ProjectMetadata> nameToProject,
            string homePath)
        {
            string selName = (projectNameInput ?? string.Empty).Trim();
            MoveDialogProjectState state = new MoveDialogProjectState();
            state.NormalizedName = selName;

            if (VisualDiffEngine.IsOutsideProject(selName))
            {
                state.NormalizedName = "Outside of Project";
                state.IsOutsideOfProject = true;
                state.FolderEnabled = false;
                state.FolderDisplayPath = string.Empty;
                return state;
            }

            state.IsOutsideOfProject = false;
            state.FolderEnabled = true;

            if (string.IsNullOrEmpty(selName))
            {
                state.FolderDisplayPath = string.Empty;
                return state;
            }

            ProjectMetadata m;
            if (nameToProject != null && nameToProject.TryGetValue(selName, out m) && m != null && !string.IsNullOrEmpty(m.FolderUri))
            {
                state.FolderDisplayPath = UriToDisplayPath(m.FolderUri);
            }
            else if (!string.IsNullOrEmpty(homePath))
            {
                state.FolderDisplayPath = Path.Combine(homePath, selName);
            }
            else
            {
                state.FolderDisplayPath = string.Empty;
            }

            return state;
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "session";
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (Array.IndexOf(invalid, c) >= 0 || c == ' ')
                    sb.Append('_');
                else
                    sb.Append(c);
            }
            string res = sb.ToString().Trim('_');
            if (res.Length > 48) res = res.Substring(0, 48);
            return string.IsNullOrEmpty(res) ? "session" : res;
        }
    }

    internal static class DialogResourceHelper
    {
        public static Font TrackFont(List<Font> ownedFonts, Font font)
        {
            if (ownedFonts != null && font != null) ownedFonts.Add(font);
            return font;
        }

        public static void DisposeFonts(List<Font> ownedFonts)
        {
            if (ownedFonts == null) return;
            for (int i = 0; i < ownedFonts.Count; i++)
            {
                if (ownedFonts[i] != null)
                {
                    try { ownedFonts[i].Dispose(); } catch {}
                }
            }
            ownedFonts.Clear();
        }
    }

    /// <summary>
    /// 通用单行文本输入对话框 (用于 F2 会话重命名)
    /// </summary>
    public class TextInputDialog : Form
    {
        private readonly List<Font> _ownedFonts = new List<Font>();
        private TextBox txtInput;
        public string InputValue
        {
            get { return txtInput != null ? txtInput.Text : string.Empty; }
        }

        public TextInputDialog(string title, string prompt, string defaultValue)
        {
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Text = title;
            this.Size = new Size(480, 175);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = DialogResourceHelper.TrackFont(_ownedFonts, new Font("Microsoft YaHei UI", 9f));
            this.BackColor = Color.White;

            Label lbl = new Label
            {
                Text = prompt,
                Location = new Point(16, 14),
                Size = new Size(430, 22),
                ForeColor = Color.FromArgb(30, 41, 59)
            };

            txtInput = new TextBox
            {
                Text = defaultValue ?? string.Empty,
                Location = new Point(16, 42),
                Size = new Size(430, 26),
                Font = DialogResourceHelper.TrackFont(_ownedFonts, new Font("Microsoft YaHei UI", 9.5f))
            };

            Button btnOk = new Button
            {
                Text = "确定",
                Location = new Point(276, 86),
                Size = new Size(80, 30),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtInput.Text))
                {
                    MessageBox.Show(this, "输入内容不能为空。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            Button btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(366, 86),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat
            };

            this.Controls.AddRange(new Control[] { lbl, txtInput, btnOk, btnCancel });
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
            this.Load += (s, e) => MainForm.ClampFormToWorkingArea(this);
            this.Shown += (s, e) =>
            {
                txtInput.Focus();
                MainForm.ClearInitialEditSelections(this, txtInput);
            };
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) DialogResourceHelper.DisposeFonts(_ownedFonts);
        }
    }

    /// <summary>
    /// 会话跨工程迁移选择对话框 (用于 F2 移动会话至现有或新建工程)
    /// </summary>
    public class MoveToProjectDialog : Form
    {
        private readonly List<Font> _ownedFonts = new List<Font>();
        private ComboBox cmbProjects;
        private TextBox txtFolderPath;
        private readonly string _homePath;
        private string _cachedNewProjectGuid;
        private readonly Dictionary<string, ProjectMetadata> _nameToProject = new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);

        public string SelectedProjectName
        {
            get { return cmbProjects != null ? cmbProjects.Text.Trim() : string.Empty; }
        }

        public string SelectedProjectId
        {
            get
            {
                string name = SelectedProjectName;
                if (VisualDiffEngine.IsOutsideProject(name))
                    return "outside-of-project";
                ProjectMetadata meta;
                if (_nameToProject.TryGetValue(name, out meta) && !string.IsNullOrEmpty(meta.Id))
                    return meta.Id;
                if (string.IsNullOrEmpty(_cachedNewProjectGuid))
                    _cachedNewProjectGuid = Guid.NewGuid().ToString();
                return _cachedNewProjectGuid;
            }
        }

        public string SelectedFolderUri
        {
            get
            {
                string name = SelectedProjectName;
                if (VisualDiffEngine.IsOutsideProject(name))
                    return string.Empty;
                return LocalWorkspaceManager.ToFileUri(txtFolderPath != null ? txtFolderPath.Text.Trim() : string.Empty);
            }
        }

        public MoveToProjectDialog(Dictionary<string, ProjectMetadata> localProjects, List<ConversationSummaryInfo> localSummaries, string homePath)
        {
            _homePath = homePath;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Text = "移动所选会话到其他工程 (F2)";
            this.Size = new Size(520, 240);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = DialogResourceHelper.TrackFont(_ownedFonts, new Font("Microsoft YaHei UI", 9f));
            this.BackColor = Color.White;

            if (localProjects != null)
            {
                foreach (KeyValuePair<string, ProjectMetadata> kvp in localProjects)
                {
                    if (!string.IsNullOrEmpty(kvp.Value.Name))
                        _nameToProject[kvp.Value.Name] = kvp.Value;
                }
            }
            if (localSummaries != null)
            {
                for (int i = 0; i < localSummaries.Count; i++)
                {
                    ConversationSummaryInfo s = localSummaries[i];
                    if (!string.IsNullOrEmpty(s.ProjectName) && !_nameToProject.ContainsKey(s.ProjectName))
                    {
                        _nameToProject[s.ProjectName] = new ProjectMetadata
                        {
                            Id = s.ProjectId,
                            Name = s.ProjectName,
                            FolderUri = s.FolderUri
                        };
                    }
                }
            }

            Label lblProj = new Label
            {
                Text = "选择目标工程（也可直接输入新工程名称）：",
                Location = new Point(16, 14),
                Size = new Size(470, 20)
            };

            cmbProjects = new ComboBox
            {
                Location = new Point(16, 38),
                Size = new Size(470, 26),
                DropDownStyle = ComboBoxStyle.DropDown
            };

            foreach (KeyValuePair<string, ProjectMetadata> kvp in _nameToProject)
            {
                if (!SmartProjectFilter.IsJunkOrEphemeral(kvp.Key))
                    cmbProjects.Items.Add(kvp.Key);
            }
            if (!cmbProjects.Items.Contains("Outside of Project"))
                cmbProjects.Items.Add("Outside of Project");

            Label lblFolder = new Label
            {
                Text = "目标工程物理目录 (Folder Path)：",
                Location = new Point(16, 76),
                Size = new Size(470, 20)
            };

            txtFolderPath = new TextBox
            {
                Location = new Point(16, 100),
                Size = new Size(380, 25)
            };

            Button btnBrowse = new Button
            {
                Text = "浏览...",
                Location = new Point(404, 98),
                Size = new Size(82, 27),
                FlatStyle = FlatStyle.Flat
            };
            btnBrowse.Click += (s, e) =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    if (fbd.ShowDialog(this) == DialogResult.OK)
                    {
                        if (string.IsNullOrWhiteSpace(cmbProjects.Text))
                        {
                            cmbProjects.Text = Path.GetFileName(fbd.SelectedPath.TrimEnd('\\', '/'));
                        }
                        txtFolderPath.Enabled = true;
                        txtFolderPath.Text = fbd.SelectedPath;
                    }
                }
            };

            cmbProjects.SelectedIndexChanged += (s, e) =>
            {
                _cachedNewProjectGuid = null;
                ApplyProjectSelectionState();
            };
            cmbProjects.TextChanged += (s, e) =>
            {
                _cachedNewProjectGuid = null;
                ApplyProjectSelectionState();
            };

            if (cmbProjects.Items.Count > 0)
                cmbProjects.SelectedIndex = 0;

            Button btnOk = new Button
            {
                Text = "确定迁移",
                Location = new Point(306, 148),
                Size = new Size(86, 32),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(SelectedProjectName))
                {
                    MessageBox.Show(this, "请选择或输入目标工程名称。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (!VisualDiffEngine.IsOutsideProject(SelectedProjectName) && string.IsNullOrWhiteSpace(txtFolderPath.Text))
                {
                    MessageBox.Show(this, "请输入或浏览选择目标工程物理目录。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            Button btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(400, 148),
                Size = new Size(86, 32),
                FlatStyle = FlatStyle.Flat
            };

            this.Controls.AddRange(new Control[] { lblProj, cmbProjects, lblFolder, txtFolderPath, btnBrowse, btnOk, btnCancel });
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
            this.Load += (s, e) => MainForm.ClampFormToWorkingArea(this);
            this.Shown += (s, e) =>
            {
                MainForm.ClearInitialEditSelections(this, cmbProjects, txtFolderPath);
            };
        }

        private void ApplyProjectSelectionState()
        {
            MainForm.MoveDialogProjectState st = MainForm.ComputeMoveDialogProjectState(cmbProjects.Text, _nameToProject, _homePath);
            txtFolderPath.Enabled = st.FolderEnabled;
            txtFolderPath.Text = st.FolderDisplayPath;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) DialogResourceHelper.DisposeFonts(_ownedFonts);
        }
    }

    /// <summary>
    /// 模态进度指示弹窗 (Progress Dialog)
    /// </summary>
    public class ProgressForm : Form
    {
        private readonly List<Font> _ownedFonts = new List<Font>();
        private ProgressBar pb;
        private Label lbl;
        private bool _allowClose = false;

        public ProgressForm(string title, string initialMsg)
        {
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Text = title;
            this.Size = new Size(460, 140);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.ControlBox = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = DialogResourceHelper.TrackFont(_ownedFonts, new Font("Microsoft YaHei UI", 9f));
            this.BackColor = Color.White;

            lbl = new Label { Text = initialMsg, Location = new Point(20, 16), Size = new Size(410, 36), AutoEllipsis = true };
            pb = new ProgressBar { Location = new Point(20, 56), Size = new Size(405, 22), Style = ProgressBarStyle.Marquee };

            this.Controls.Add(lbl);
            this.Controls.Add(pb);

            this.Load += (s, e) => MainForm.ClampFormToWorkingArea(this);
            this.Shown += (s, e) => {
                if (_allowClose)
                {
                    this.Close();
                }
            };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !_allowClose)
            {
                e.Cancel = true;
                return;
            }
            base.OnFormClosing(e);
        }

        public void SafeClose()
        {
            _allowClose = true;
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

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) DialogResourceHelper.DisposeFonts(_ownedFonts);
        }
    }

    /// <summary>
    /// 能力资产源码预览与本机/仓库双栏差异对比弹窗
    /// </summary>
    public class CapabilityPreviewDialog : Form
    {
        private readonly List<Font> _ownedFonts = new List<Font>();
        private SplitContainer _previewSplit;
        private readonly List<TextBox> _previewBoxes = new List<TextBox>();
        private readonly Font _headerFont;
        private readonly Font _bodyFont;

        public CapabilityPreviewDialog(CapabilityItem primaryItem, CapabilityItem counterpart, bool fromLocal)
        {
            CapabilityItem localItem = fromLocal ? primaryItem : counterpart;
            CapabilityItem repoItem = fromLocal ? counterpart : primaryItem;
            string titleSuffix = primaryItem != null ? string.Format("[{0}] {1}", CapabilityItem.GetKindShortTag(primaryItem.Kind), primaryItem.DisplayName) : "能力预览";

            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Text = "能力资产详情与源码对比 - " + titleSuffix;
            this.Size = new Size(980, 660);
            this.MinimumSize = new Size(760, 480);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = DialogResourceHelper.TrackFont(_ownedFonts, new Font("Microsoft YaHei UI", 9f));
            _headerFont = DialogResourceHelper.TrackFont(_ownedFonts, new Font("Microsoft YaHei UI", 9f, FontStyle.Bold));
            _bodyFont = DialogResourceHelper.TrackFont(_ownedFonts, new Font("Microsoft YaHei UI", 9.5f));
            this.BackColor = Color.FromArgb(245, 247, 250);

            Panel pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MaximumSize = new Size(10000, 150),
                AutoScroll = true,
                BackColor = Color.White,
                Padding = new Padding(14, 10, 14, 10)
            };

            string summaryInfo = primaryItem != null
                ? string.Format("类别: {0}    |    ID: {1}    |    包含文件: {2} 个\r\n摘要: {3}",
                    CapabilityItem.GetCategoryTitle(primaryItem.Kind),
                    primaryItem.Id,
                    primaryItem.FilesMap != null ? primaryItem.FilesMap.Count : 0,
                    string.IsNullOrEmpty(primaryItem.Description) ? "(无)" : primaryItem.Description)
                : "";

            if (primaryItem != null && primaryItem.HasMissingPaths)
            {
                summaryInfo += "\r\n⚠ 缺失路径: " + string.Join(", ", primaryItem.MissingLocalPaths.ToArray());
            }

            Label lblMeta = new Label
            {
                Text = summaryInfo,
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = (primaryItem != null && primaryItem.HasMissingPaths) ? Color.FromArgb(185, 28, 28) : Color.FromArgb(44, 62, 80),
                Font = _headerFont
            };
            pnlTop.Controls.Add(lblMeta);
            pnlTop.SizeChanged += (s, e) =>
            {
                lblMeta.MaximumSize = new Size(Math.Max(260, pnlTop.ClientSize.Width - 36), 0);
            };

            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                Padding = new Padding(14, 8, 14, 8),
                BackColor = Color.FromArgb(236, 240, 245)
            };

            Button btnClose = new Button
            {
                Text = "关闭 (Esc)",
                DialogResult = DialogResult.Cancel,
                Dock = DockStyle.Right,
                Width = 110,
                BackColor = Color.FromArgb(52, 73, 94),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnClose.FlatAppearance.BorderSize = 0;
            pnlBottom.Controls.Add(btnClose);
            this.CancelButton = btnClose;

            bool hasBoth = (localItem != null && repoItem != null);
            if (hasBoth)
            {
                _previewSplit = new SplitContainer
                {
                    Dock = DockStyle.Fill,
                    Orientation = Orientation.Vertical,
                    SplitterWidth = 6,
                    BackColor = Color.FromArgb(210, 218, 226)
                };

                _previewSplit.Panel1.Controls.Add(BuildPreviewBox("本机当前内容 (Local .gemini)", localItem.PreviewText, Color.FromArgb(30, 96, 145)));
                _previewSplit.Panel2.Controls.Add(BuildPreviewBox("仓库/备份内容 (Repository / .agy)", repoItem.PreviewText, Color.FromArgb(21, 128, 61)));

                this.Controls.Add(_previewSplit);
            }
            else
            {
                string header = localItem != null ? "本机当前内容 (Local .gemini)" : "仓库/备份内容 (Repository / .agy)";
                string content = primaryItem != null ? primaryItem.PreviewText : "";
                this.Controls.Add(BuildPreviewBox(header, content, Color.FromArgb(30, 96, 145)));
            }

            this.Controls.Add(pnlBottom);
            this.Controls.Add(pnlTop);

            this.Load += (s, e) => MainForm.ClampFormToWorkingArea(this);
            this.Shown += (s, e) =>
            {
                if (_previewSplit != null && _previewSplit.ClientSize.Width > 200)
                {
                    int target = (_previewSplit.ClientSize.Width - _previewSplit.SplitterWidth) / 2;
                    if (target > 50 && target < _previewSplit.ClientSize.Width - 50)
                    {
                        try { _previewSplit.SplitterDistance = target; } catch {}
                    }
                }
                if (_previewBoxes.Count > 0)
                {
                    MainForm.ClearInitialEditSelections(this, true, _previewBoxes.ToArray());
                }
            };
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) DialogResourceHelper.DisposeFonts(_ownedFonts);
        }

        private Control BuildPreviewBox(string headerTitle, string content, Color bannerColor)
        {
            Panel container = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };

            Label lblHeader = new Label
            {
                Text = "  " + headerTitle,
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = bannerColor,
                ForeColor = Color.White,
                Font = _headerFont
            };

            TextBox txt = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Font = _bodyFont,
                BackColor = Color.FromArgb(252, 253, 255),
                ForeColor = Color.FromArgb(30, 41, 59),
                Text = NormalizeLineEndings(content ?? "")
            };
            txt.SelectionStart = 0;
            txt.SelectionLength = 0;
            _previewBoxes.Add(txt);

            container.Controls.Add(txt);
            container.Controls.Add(lblHeader);
            return container;
        }

        private static string NormalizeLineEndings(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        }
    }

    /// <summary>
    /// 会话 Markdown 只读预览弹窗 (支持自动换行、复制及触发异步完整导出)
    /// </summary>
    public class MarkdownPreviewDialog : Form
    {
        private readonly List<Font> _ownedFonts = new List<Font>();
        private TextBox _txtPreview;
        private readonly Font _headerFont;
        private readonly Font _bodyFont;

        public MarkdownPreviewDialog(
            ConversationSummaryInfo info,
            string markdownContent,
            bool fromLocal,
            bool isTruncated,
            Action onExportFull)
        {
            string titleText = info != null ? info.DisplayTitle : "会话记录";
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Text = string.Format("会话 Markdown 预览 ({0}) - {1}", fromLocal ? "本机" : "备份归档免导入", titleText);
            this.Size = new Size(960, 700);
            this.MinimumSize = new Size(720, 480);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = DialogResourceHelper.TrackFont(_ownedFonts, new Font("Microsoft YaHei UI", 9f));
            _headerFont = DialogResourceHelper.TrackFont(_ownedFonts, new Font("Microsoft YaHei UI", 9f, FontStyle.Bold));
            _bodyFont = DialogResourceHelper.TrackFont(_ownedFonts, new Font("Microsoft YaHei UI", 9.5f));
            this.BackColor = Color.FromArgb(248, 250, 252);

            Panel pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Padding = new Padding(14, 10, 14, 10)
            };

            string timeStr = (info != null && info.UpdatedTime > DateTime.MinValue)
                ? info.UpdatedTime.ToString("yyyy-MM-dd HH:mm:ss")
                : "-";
            Label lblMeta = new Label
            {
                Text = string.Format("工程: {0}  |  会话 ID: {1}  |  步数: {2}  |  更新时间: {3}",
                    info != null ? info.ProjectName : "-",
                    info != null ? info.ConversationId : "-",
                    info != null ? info.StepCount : 0,
                    timeStr),
                Dock = DockStyle.Top,
                AutoSize = true,
                Font = _headerFont,
                ForeColor = Color.FromArgb(30, 41, 59)
            };
            pnlTop.Controls.Add(lblMeta);

            if (isTruncated)
            {
                Label lblBanner = new Label
                {
                    Text = "提示: 当前对话较长，预览窗口已安全截断在 512 KB 以内；如需查看全部内容请点击右下角「完整导出为 .md...」。",
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    Padding = new Padding(0, 6, 0, 0),
                    ForeColor = Color.FromArgb(180, 83, 9)
                };
                pnlTop.Controls.Add(lblBanner);
            }

            _txtPreview = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = true,
                Dock = DockStyle.Fill,
                Font = _bodyFont,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 41, 59),
                Text = (markdownContent ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n")
            };

            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(14, 9, 14, 9),
                BackColor = Color.FromArgb(241, 245, 249)
            };

            CheckBox chkWrap = new CheckBox
            {
                Text = "自动换行",
                Checked = true,
                AutoSize = true,
                Dock = DockStyle.Left,
                Padding = new Padding(0, 6, 12, 0)
            };
            chkWrap.CheckedChanged += (s, e) =>
            {
                _txtPreview.WordWrap = chkWrap.Checked;
            };

            FlowLayoutPanel flowRight = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            Button btnCopy = new Button
            {
                Text = "复制预览内容",
                AutoSize = true,
                MinimumSize = new Size(106, 30),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 8, 0)
            };
            btnCopy.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnCopy.Click += (s, e) =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(_txtPreview.Text))
                        Clipboard.SetText(_txtPreview.Text);
                }
                catch {}
            };

            Button btnExport = new Button
            {
                Text = "完整导出为 .md...",
                AutoSize = true,
                MinimumSize = new Size(134, 30),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 8, 0)
            };
            btnExport.FlatAppearance.BorderSize = 0;
            btnExport.Click += (s, e) =>
            {
                if (onExportFull != null) onExportFull();
            };

            Button btnClose = new Button
            {
                Text = "关闭 (Esc)",
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                MinimumSize = new Size(92, 30),
                BackColor = Color.FromArgb(51, 65, 85),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0)
            };
            btnClose.FlatAppearance.BorderSize = 0;

            flowRight.Controls.AddRange(new Control[] { btnCopy, btnExport, btnClose });
            pnlBottom.Controls.Add(flowRight);
            pnlBottom.Controls.Add(chkWrap);
            this.CancelButton = btnClose;

            this.Controls.Add(_txtPreview);
            this.Controls.Add(pnlBottom);
            this.Controls.Add(pnlTop);

            this.Load += (s, e) => MainForm.ClampFormToWorkingArea(this);
            this.Shown += (s, e) =>
            {
                MainForm.ClearInitialEditSelections(this, true, _txtPreview);
            };
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) DialogResourceHelper.DisposeFonts(_ownedFonts);
        }
    }

    /// <summary>
    /// MCP / Hook 脚本或可执行文件路径重定位弹窗
    /// </summary>
    public class RelocateCapabilityPathDialog : Form
    {
        private readonly List<Font> _ownedFonts = new List<Font>();
        private ComboBox cmbOldPath;
        private TextBox txtNewPath;
        private string _lastAutoFilledOldPath = string.Empty;

        public string OldPathValue
        {
            get { return cmbOldPath.Text != null ? cmbOldPath.Text.Trim() : ""; }
        }

        public string NewPathValue
        {
            get { return txtNewPath.Text != null ? txtNewPath.Text.Trim() : ""; }
        }

        public RelocateCapabilityPathDialog(CapabilityItem item)
        {
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Text = "重定位能力脚本/可执行文件路径 - " + (item != null ? item.DisplayName : "");
            this.Size = new Size(580, 250);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = DialogResourceHelper.TrackFont(_ownedFonts, new Font("Microsoft YaHei UI", 9f));
            this.BackColor = Color.White;

            Label lblOld = new Label
            {
                Text = "要替换的原路径 (可从检测到的缺失路径中选择或手动输入):",
                Location = new Point(18, 16),
                Size = new Size(530, 20),
                Font = DialogResourceHelper.TrackFont(_ownedFonts, new Font("Microsoft YaHei UI", 9f, FontStyle.Bold))
            };

            cmbOldPath = new ComboBox
            {
                Location = new Point(18, 40),
                Size = new Size(528, 26),
                DropDownStyle = ComboBoxStyle.DropDown
            };

            if (item != null && item.MissingLocalPaths != null && item.MissingLocalPaths.Count > 0)
            {
                foreach (string mp in item.MissingLocalPaths)
                {
                    cmbOldPath.Items.Add(mp);
                }
                cmbOldPath.SelectedIndex = 0;
            }
            else if (item != null && item.ReferencedLocalPaths != null && item.ReferencedLocalPaths.Count > 0)
            {
                foreach (string rp in item.ReferencedLocalPaths)
                {
                    cmbOldPath.Items.Add(rp);
                }
                cmbOldPath.SelectedIndex = 0;
            }

            Label lblNew = new Label
            {
                Text = "本机新目标路径 (脚本文件或目录路径):",
                Location = new Point(18, 80),
                Size = new Size(530, 20),
                Font = DialogResourceHelper.TrackFont(_ownedFonts, new Font("Microsoft YaHei UI", 9f, FontStyle.Bold))
            };

            txtNewPath = new TextBox
            {
                Location = new Point(18, 104),
                Size = new Size(370, 25)
            };
            if (cmbOldPath.Text.Length > 0)
            {
                _lastAutoFilledOldPath = cmbOldPath.Text;
                txtNewPath.Text = _lastAutoFilledOldPath;
            }

            cmbOldPath.SelectedIndexChanged += (s, e) =>
            {
                string nextOld = cmbOldPath.SelectedItem != null ? cmbOldPath.SelectedItem.ToString() : (cmbOldPath.Text ?? string.Empty);
                txtNewPath.Text = MainForm.ComputeRelocatePathOnSelectionChanged(_lastAutoFilledOldPath, txtNewPath.Text, nextOld);
                _lastAutoFilledOldPath = nextOld;
            };

            Button btnBrowseFile = new Button
            {
                Text = "选文件...",
                Location = new Point(394, 102),
                Size = new Size(74, 27),
                FlatStyle = FlatStyle.Flat
            };
            btnBrowseFile.Click += (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = "选择本机脚本或可执行文件";
                    ofd.Filter = "所有文件 (*.*)|*.*";
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        txtNewPath.Text = ofd.FileName.Replace('\\', '/');
                    }
                }
            };

            Button btnBrowseDir = new Button
            {
                Text = "选目录...",
                Location = new Point(472, 102),
                Size = new Size(74, 27),
                FlatStyle = FlatStyle.Flat
            };
            btnBrowseDir.Click += (s, e) =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "选择本机工具或服务目录";
                    if (fbd.ShowDialog(this) == DialogResult.OK)
                    {
                        txtNewPath.Text = fbd.SelectedPath.Replace('\\', '/');
                    }
                }
            };

            Button btnOk = new Button
            {
                Text = "确认重定位",
                Location = new Point(342, 158),
                Size = new Size(104, 34),
                BackColor = Color.FromArgb(30, 96, 145),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrEmpty(OldPathValue) || string.IsNullOrEmpty(NewPathValue))
                {
                    MessageBox.Show(this, "原路径与新目标路径均不能为空。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            Button btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(456, 158),
                Size = new Size(90, 34),
                FlatStyle = FlatStyle.Flat
            };

            this.Controls.AddRange(new Control[] { lblOld, cmbOldPath, lblNew, txtNewPath, btnBrowseFile, btnBrowseDir, btnOk, btnCancel });
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
            this.Load += (s, e) => MainForm.ClampFormToWorkingArea(this);
            this.Shown += (s, e) =>
            {
                MainForm.ClearInitialEditSelections(this, cmbOldPath, txtNewPath);
            };
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) DialogResourceHelper.DisposeFonts(_ownedFonts);
        }
    }

    internal sealed class DoubleBufferedDataGridView : DataGridView
    {
        public DoubleBufferedDataGridView()
        {
            this.DoubleBuffered = true;
        }
    }

    /// <summary>
    /// 进程占用双模写入策略选择弹窗（关闭全写 vs 免关机热写持久化层）
    /// </summary>
    public class RenameProcessStrategyDialog : Form
    {
        private readonly List<Font> _ownedFonts = new List<Font>();
        public RenameProcessStrategy SelectedStrategy { get; private set; }

        public RenameProcessStrategyDialog(string actionLabel, int parentCount, int lsCount)
        {
            this.SelectedStrategy = RenameProcessStrategy.Cancel;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Text = "检测到 Antigravity 进程正在运行 - 选择写入策略";
            this.Size = new Size(560, 310);
            this.MinimumSize = new Size(500, 280);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;

            Font baseFont = TrackFont(new Font("Microsoft YaHei UI", 9f));
            Font boldFont = TrackFont(new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold));
            Font noteFont = TrackFont(new Font("Microsoft YaHei UI", 8.5f));
            this.Font = baseFont;

            string safeAction = string.IsNullOrWhiteSpace(actionLabel) ? "重命名会话" : actionLabel.Trim();

            Label lblTitle = new Label
            {
                Text = string.Format("准备执行「{0}」，检测到 {1} 个 Antigravity 主进程与 {2} 个语言服务进程正在运行。",
                    safeAction, parentCount, lsCount),
                Location = new Point(20, 16),
                Size = new Size(506, 40),
                Font = boldFont,
                ForeColor = Color.FromArgb(15, 23, 42)
            };

            Label lblBody = new Label
            {
                Text =
                    "请选择写入模式：\r\n\r\n" +
                    "1. 关闭 Antigravity 并三层全写 (推荐)：安全关闭主进程与语言服务后，同时写入 .pbtxt / summary.sqlite / .pb 三层，打开 IDE 立即生效。\r\n\r\n" +
                    "2. 免关机热写持久化层 (跳过运行中会话)：不关闭 IDE，直接写入 .pbtxt 与 SQLite 持久化层（自动跳过 RUNNING 会话；切换工程或重启 Antigravity 后侧边栏缓存即可刷新同步）。",
                Location = new Point(20, 62),
                Size = new Size(506, 128),
                Font = noteFont,
                ForeColor = Color.FromArgb(51, 65, 85)
            };

            Button btnCloseFull = new Button
            {
                Text = "关闭 Antigravity 并三层全写 (推荐)",
                Location = new Point(20, 204),
                Size = new Size(236, 36),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = boldFont,
                Cursor = Cursors.Hand
            };
            btnCloseFull.FlatAppearance.BorderSize = 0;
            btnCloseFull.Click += (s, e) =>
            {
                this.SelectedStrategy = RenameProcessStrategy.CloseAndFullWrite;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            Button btnHotWrite = new Button
            {
                Text = "免关机热写持久化层 (跳过运行中会话)",
                Location = new Point(264, 204),
                Size = new Size(262, 36),
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Font = baseFont,
                Cursor = Cursors.Hand
            };
            btnHotWrite.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnHotWrite.Click += (s, e) =>
            {
                this.SelectedStrategy = RenameProcessStrategy.HotWritePersistentLayers;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            Button btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(446, 248),
                Size = new Size(80, 28),
                FlatStyle = FlatStyle.Flat,
                Font = baseFont,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.Click += (s, e) =>
            {
                this.SelectedStrategy = RenameProcessStrategy.Cancel;
                this.Close();
            };

            this.Controls.AddRange(new Control[] { lblTitle, lblBody, btnCloseFull, btnHotWrite, btnCancel });
            this.AcceptButton = btnCloseFull;
            this.CancelButton = btnCancel;
            this.Load += (s, e) => MainForm.ClampFormToWorkingArea(this);
        }

        private Font TrackFont(Font f)
        {
            if (f != null) _ownedFonts.Add(f);
            return f;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                for (int i = 0; i < _ownedFonts.Count; i++)
                {
                    if (_ownedFonts[i] != null)
                    {
                        try { _ownedFonts[i].Dispose(); } catch {}
                    }
                }
                _ownedFonts.Clear();
            }
        }
    }

    /// <summary>
    /// 批量会话重命名、规则规范化与 AI 润色协同工作台
    /// </summary>
    public class BatchRenameWorkbenchDialog : Form
    {
        private const string FilterItemScopedPrefix = "当前选中范围 (";
        private const string FilterAllFormalProjects = "全部正式工程";
        private const string FilterAllSessions = "全部会话";

        private readonly string _geminiPath;
        private readonly string _repositoryPath;
        private readonly Func<string, RenameProcessStrategy> _promptStrategy;
        private readonly HashSet<string> _initialScopedConvIds;
        private readonly bool _scopedFromExplicitSessions;
        private readonly string _initialProjectName;
        private readonly string _scopedFilterLabel;
        private readonly List<Font> _ownedFonts = new List<Font>();

        private RenameRulesConfig _rules;
        private List<ConversationSummaryInfo> _rawSummaries = new List<ConversationSummaryInfo>();
        private List<SessionRenameCandidate> _allCandidates = new List<SessionRenameCandidate>();
        private List<SessionRenameCandidate> _visibleCandidates = new List<SessionRenameCandidate>();
        private readonly HashSet<string> _manuallyEditedCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private ComboBox cmbProjectFilter;
        private CheckBox chkIncludeTagged;
        private CheckBox chkIncludeOutside;
        private CheckBox chkIncludeRunning;
        private SplitContainer splitCenter;
        private DoubleBufferedDataGridView dgvCandidates;
        private DataGridViewCheckBoxColumn colSelect;
        private DataGridViewTextBoxColumn colProject;
        private DataGridViewTextBoxColumn colOldTitle;
        private DataGridViewTextBoxColumn colFirstPrompt;
        private DataGridViewTextBoxColumn colNewTitle;
        private DataGridViewTextBoxColumn colStatus;
        private TextBox txtPreview;
        private Label lblSummary;

        private bool _isPopulatingGrid = false;
        private bool _isSuppressingFilterEvents = false;
        private bool _splitterInitialized = false;

        public bool AnyDataChanged { get; private set; }

        public BatchRenameWorkbenchDialog(
            string geminiPath,
            string repositoryPath,
            List<ConversationSummaryInfo> initialSummaries,
            HashSet<string> initialScopedConvIds,
            bool scopedFromExplicitSessions,
            string initialProjectName,
            Func<string, RenameProcessStrategy> promptStrategy)
        {
            _geminiPath = geminiPath ?? string.Empty;
            _repositoryPath = string.IsNullOrWhiteSpace(repositoryPath)
                ? CapabilityRepositoryManager.ResolveDefaultRepositoryPath()
                : repositoryPath;
            _promptStrategy = promptStrategy;
            _initialScopedConvIds = initialScopedConvIds != null
                ? new HashSet<string>(initialScopedConvIds, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _scopedFromExplicitSessions = scopedFromExplicitSessions;
            _initialProjectName = initialProjectName;
            _scopedFilterLabel = _initialScopedConvIds.Count > 0
                ? string.Format("{0}{1} 条)", FilterItemScopedPrefix, _initialScopedConvIds.Count)
                : null;

            if (initialSummaries != null)
            {
                _rawSummaries.AddRange(initialSummaries);
            }

            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Text = "批量重命名与 AI 标题润色工作台";
            this.Size = new Size(1180, 760);
            this.MinimumSize = new Size(860, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(248, 250, 252);

            Font baseFont = TrackFont(new Font("Microsoft YaHei UI", 9f));
            Font boldFont = TrackFont(new Font("Microsoft YaHei UI", 9f, FontStyle.Bold));
            Font smallFont = TrackFont(new Font("Microsoft YaHei UI", 8.5f));
            Font previewFont = TrackFont(new Font("Microsoft YaHei UI", 9.5f));
            this.Font = baseFont;

            // 1. 顶部筛选与工具栏
            Panel pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Padding = new Padding(10, 8, 10, 8)
            };
            pnlTop.Paint += (s, pe) =>
            {
                using (Pen p = new Pen(Color.FromArgb(226, 232, 240)))
                    pe.Graphics.DrawLine(p, 0, pnlTop.Height - 1, pnlTop.Width, pnlTop.Height - 1);
            };

            FlowLayoutPanel flowTop = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent
            };

            Label lblScope = new Label
            {
                Text = "工程范围:",
                AutoSize = true,
                Margin = new Padding(0, 8, 4, 4),
                ForeColor = Color.FromArgb(71, 85, 105),
                Font = baseFont
            };

            cmbProjectFilter = new ComboBox
            {
                Width = 190,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 4, 10, 4),
                Font = baseFont
            };
            cmbProjectFilter.SelectedIndexChanged += (s, e) =>
            {
                if (_isSuppressingFilterEvents) return;
                ApplyFiltersAndPopulateGrid();
            };

            chkIncludeTagged = new CheckBox
            {
                Text = "显示已带【...】前缀",
                AutoSize = true,
                Checked = false,
                Margin = new Padding(0, 7, 10, 4),
                Font = baseFont,
                ForeColor = Color.FromArgb(51, 65, 85)
            };
            chkIncludeTagged.CheckedChanged += (s, e) =>
            {
                if (_isSuppressingFilterEvents) return;
                ApplyFiltersAndPopulateGrid();
            };

            chkIncludeOutside = new CheckBox
            {
                Text = "包含 Outside of Project",
                AutoSize = true,
                Checked = false,
                Margin = new Padding(0, 7, 10, 4),
                Font = baseFont,
                ForeColor = Color.FromArgb(51, 65, 85)
            };
            chkIncludeOutside.CheckedChanged += (s, e) =>
            {
                if (_isSuppressingFilterEvents) return;
                ApplyFiltersAndPopulateGrid();
            };

            chkIncludeRunning = new CheckBox
            {
                Text = "包含运行中会话",
                AutoSize = true,
                Checked = false,
                Margin = new Padding(0, 7, 12, 4),
                Font = baseFont,
                ForeColor = Color.FromArgb(51, 65, 85)
            };
            chkIncludeRunning.CheckedChanged += (s, e) =>
            {
                if (_isSuppressingFilterEvents) return;
                ApplyFiltersAndPopulateGrid();
            };

            Button btnSelectAll = MakeToolbarButton("全选", baseFont, false);
            btnSelectAll.Click += (s, e) => SetAllVisibleChecked(true);

            Button btnDeselectAll = MakeToolbarButton("全不选", baseFont, false);
            btnDeselectAll.Click += (s, e) => SetAllVisibleChecked(false);

            Button btnRecompute = MakeToolbarButton("重算建议", baseFont, false);
            btnRecompute.Click += BtnRecompute_Click;

            Button btnEditRules = MakeToolbarButton("编辑规则(JSON)...", baseFont, false);
            btnEditRules.Click += BtnEditRules_Click;

            Button btnRollback = MakeToolbarButton("⏪ 回滚历史...", baseFont, false);
            btnRollback.Click += BtnRollbackHistory_Click;

            flowTop.Controls.AddRange(new Control[] {
                lblScope, cmbProjectFilter,
                chkIncludeTagged, chkIncludeOutside, chkIncludeRunning,
                btnSelectAll, btnDeselectAll, btnRecompute, btnEditRules, btnRollback
            });
            pnlTop.Controls.Add(flowTop);

            // 2. 中部垂直分割容器：上方 DataGridView，下方全文/Branch/Status 预览框
            splitCenter = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(226, 232, 240)
            };
            splitCenter.Resize += (s, e) => AdjustSplitterLayout(false);

            int rowHeight = Math.Max(26, TextRenderer.MeasureText("测Ay", baseFont).Height + 10);
            dgvCandidates = new DoubleBufferedDataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(226, 232, 240),
                Font = baseFont,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = Math.Max(28, rowHeight + 2),
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2
            };
            dgvCandidates.RowTemplate.Height = rowHeight;
            dgvCandidates.EnableHeadersVisualStyles = false;
            dgvCandidates.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            dgvCandidates.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(30, 41, 59);
            dgvCandidates.ColumnHeadersDefaultCellStyle.Font = boldFont;
            dgvCandidates.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            dgvCandidates.DefaultCellStyle.SelectionForeColor = Color.FromArgb(15, 23, 42);

            colSelect = new DataGridViewCheckBoxColumn
            {
                HeaderText = "[选择]",
                Width = 56,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                ReadOnly = false
            };
            colProject = new DataGridViewTextBoxColumn
            {
                HeaderText = "工程",
                FillWeight = 16,
                MinimumWidth = 90,
                ReadOnly = true
            };
            colOldTitle = new DataGridViewTextBoxColumn
            {
                HeaderText = "原标题",
                FillWeight = 24,
                MinimumWidth = 130,
                ReadOnly = true
            };
            colFirstPrompt = new DataGridViewTextBoxColumn
            {
                HeaderText = "首问摘要",
                FillWeight = 24,
                MinimumWidth = 130,
                ReadOnly = true
            };
            colNewTitle = new DataGridViewTextBoxColumn
            {
                HeaderText = "拟定新标题",
                FillWeight = 30,
                MinimumWidth = 180,
                ReadOnly = false
            };
            colStatus = new DataGridViewTextBoxColumn
            {
                HeaderText = "状态",
                Width = 104,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                ReadOnly = true
            };

            dgvCandidates.Columns.AddRange(new DataGridViewColumn[] {
                colSelect, colProject, colOldTitle, colFirstPrompt, colNewTitle, colStatus
            });

            dgvCandidates.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvCandidates.IsCurrentCellDirty && dgvCandidates.CurrentCell is DataGridViewCheckBoxCell)
                {
                    dgvCandidates.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            dgvCandidates.DataError += (s, e) => { e.ThrowException = false; };
            dgvCandidates.CellValueChanged += DgvCandidates_CellValueChanged;
            dgvCandidates.SelectionChanged += (s, e) => UpdatePreviewBox();
            dgvCandidates.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == colNewTitle.Index)
                {
                    dgvCandidates.BeginEdit(true);
                }
            };

            splitCenter.Panel1.Controls.Add(dgvCandidates);

            Panel pnlPreview = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(8, 6, 8, 6)
            };
            Label lblPreviewCap = new Label
            {
                Text = "选中会话详情预览 (ConversationId / GitBranchHint / RunStatus / 首问完整内容):",
                Dock = DockStyle.Top,
                Height = 22,
                Font = smallFont,
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            txtPreview = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = true,
                Font = previewFont,
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = Color.FromArgb(30, 41, 59),
                BorderStyle = BorderStyle.FixedSingle
            };
            pnlPreview.Controls.Add(txtPreview);
            pnlPreview.Controls.Add(lblPreviewCap);
            splitCenter.Panel2.Controls.Add(pnlPreview);

            // 3. 底部 AI 润色交换与执行栏
            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.FromArgb(241, 245, 249),
                Padding = new Padding(10, 8, 10, 8)
            };
            pnlBottom.Paint += (s, pe) =>
            {
                using (Pen p = new Pen(Color.FromArgb(203, 213, 225)))
                    pe.Graphics.DrawLine(p, 0, 0, pnlBottom.Width, 0);
            };

            FlowLayoutPanel flowBottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent
            };

            Button btnCopyPrompt = MakeToolbarButton("📋 复制 AI 润色提示词", baseFont, false);
            btnCopyPrompt.Click += BtnCopyAiPrompt_Click;

            Button btnPasteAi = MakeToolbarButton("📥 从剪贴板回填", baseFont, false);
            btnPasteAi.Click += BtnPasteAiJson_Click;

            Button btnExportJson = MakeToolbarButton("导出 JSON...", baseFont, false);
            btnExportJson.Click += BtnExportJson_Click;

            Button btnImportJson = MakeToolbarButton("导入 JSON...", baseFont, false);
            btnImportJson.Click += BtnImportJson_Click;

            lblSummary = new Label
            {
                Text = "显示: 0 条 | 已勾选: 0 条",
                AutoSize = true,
                Margin = new Padding(8, 8, 12, 4),
                Font = smallFont,
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            Button btnExecute = MakeToolbarButton("执行重命名", boldFont, true);
            btnExecute.Click += BtnExecuteRename_Click;

            Button btnClose = MakeToolbarButton("关闭", baseFont, false);
            btnClose.DialogResult = DialogResult.Cancel;

            flowBottom.Controls.AddRange(new Control[] {
                btnCopyPrompt, btnPasteAi, btnExportJson, btnImportJson,
                lblSummary, btnExecute, btnClose
            });
            pnlBottom.Controls.Add(flowBottom);
            this.CancelButton = btnClose;

            this.Controls.Add(splitCenter);
            this.Controls.Add(pnlBottom);
            this.Controls.Add(pnlTop);

            this.Load += (s, e) => MainForm.ClampFormToWorkingArea(this);
            this.Shown += (s, e) =>
            {
                AdjustSplitterLayout(true);
                MainForm.ClearInitialEditSelections(this, txtPreview);
            };

            ReloadRulesFromDisk(false);
            RebuildCandidatesFromRawSummaries(true);
            InitializeFilterStateFromInitialScope();
            ApplyFiltersAndPopulateGrid();
        }

        private Font TrackFont(Font f)
        {
            if (f != null) _ownedFonts.Add(f);
            return f;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                for (int i = 0; i < _ownedFonts.Count; i++)
                {
                    if (_ownedFonts[i] != null)
                    {
                        try { _ownedFonts[i].Dispose(); } catch {}
                    }
                }
                _ownedFonts.Clear();
            }
        }

        private static Button MakeToolbarButton(string text, Font font, bool isPrimary)
        {
            Button btn = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(isPrimary ? 104 : 0, 28),
                Padding = new Padding(8, 0, 8, 0),
                Margin = new Padding(0, 2, 6, 2),
                FlatStyle = FlatStyle.Flat,
                Font = font,
                Cursor = Cursors.Hand,
                BackColor = isPrimary ? Color.FromArgb(37, 99, 235) : Color.White,
                ForeColor = isPrimary ? Color.White : Color.FromArgb(30, 41, 59)
            };
            if (isPrimary)
                btn.FlatAppearance.BorderSize = 0;
            else
                btn.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            return btn;
        }

        private void AdjustSplitterLayout(bool initial)
        {
            if (splitCenter == null) return;
            if (initial && _splitterInitialized) return;
            try
            {
                int h = splitCenter.ClientSize.Height;
                int sw = splitCenter.SplitterWidth;
                if (h <= sw + 120) return;

                int minP1 = Math.Min(180, (h - sw) / 3);
                int minP2 = Math.Min(110, (h - sw) / 4);
                splitCenter.Panel1MinSize = 0;
                splitCenter.Panel2MinSize = 0;

                int maxDist = h - sw - minP2;
                if (initial)
                {
                    int target = (int)(h * 0.68);
                    if (target < minP1) target = minP1;
                    if (target > maxDist) target = maxDist;
                    splitCenter.SplitterDistance = target;
                    _splitterInitialized = true;
                }
                else
                {
                    int cur = splitCenter.SplitterDistance;
                    if (cur < minP1) splitCenter.SplitterDistance = minP1;
                    else if (cur > maxDist && maxDist >= minP1) splitCenter.SplitterDistance = maxDist;
                }

                splitCenter.Panel1MinSize = minP1;
                splitCenter.Panel2MinSize = minP2;
            }
            catch {}
        }

        private static bool IsSessionRunning(ConversationSummaryInfo s)
        {
            if (s == null) return false;
            return string.Equals(s.RunStatus, "CASCADE_RUN_STATUS_RUNNING", StringComparison.OrdinalIgnoreCase) ||
                   s.NotFullyIdle;
        }

        private void ReloadRulesFromDisk(bool warnOnFallback)
        {
            string ruleErr;
            _rules = AutoTitleSynthesizer.LoadRules(_repositoryPath, out ruleErr);
            if (warnOnFallback && !string.IsNullOrEmpty(ruleErr))
            {
                MessageBox.Show(this, ruleErr, "规则解析警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RebuildCandidatesFromRawSummaries(bool preserveManualEdits)
        {
            Dictionary<string, string> prevManualTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> prevChecks = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (preserveManualEdits)
            {
                for (int i = 0; i < _allCandidates.Count; i++)
                {
                    SessionRenameCandidate prev = _allCandidates[i];
                    if (prev == null || string.IsNullOrEmpty(prev.ConversationId)) continue;
                    prevChecks[prev.ConversationId] = prev.Selected;
                    if (_manuallyEditedCids.Contains(prev.ConversationId) && !string.IsNullOrWhiteSpace(prev.NewTitle))
                    {
                        prevManualTitles[prev.ConversationId] = prev.NewTitle;
                    }
                }
            }
            else
            {
                _manuallyEditedCids.Clear();
            }

            List<SessionRenameCandidate> list = new List<SessionRenameCandidate>(_rawSummaries.Count);
            HashSet<string> seenCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _rawSummaries.Count; i++)
            {
                ConversationSummaryInfo s = _rawSummaries[i];
                if (s == null || string.IsNullOrWhiteSpace(s.ConversationId)) continue;
                string cid = s.ConversationId.Trim();
                if (!seenCids.Add(cid)) continue;

                SessionRenameCandidate c = new SessionRenameCandidate();
                c.ConversationId = cid;
                c.ProjectName = string.IsNullOrWhiteSpace(s.ProjectName) ? "Outside of Project" : s.ProjectName.Trim();
                c.ProjectId = s.ProjectId ?? string.Empty;
                c.FolderUri = s.FolderUri ?? string.Empty;
                c.OldTitle = s.DisplayTitle ?? string.Empty;
                c.OldAutoTitle = s.AutoSummaryTitle ?? string.Empty;
                c.OldCustomTitle = s.CustomTitle ?? string.Empty;
                c.HasCustomTitle = s.HasCustomTitle;
                c.FirstPromptSnippet = s.FirstPromptSnippet ?? string.Empty;
                c.GitBranchHint = s.GitBranchHint ?? string.Empty;
                c.UpdatedTime = s.UpdatedTime;
                c.StepCount = s.StepCount;
                c.Summary = s;

                AutoTitleSynthesizer.SynthesizeCandidate(c, _rules);

                string manual;
                if (prevManualTitles.TryGetValue(cid, out manual))
                {
                    c.NewTitle = manual;
                }

                bool wasChecked;
                if (prevChecks.TryGetValue(cid, out wasChecked))
                {
                    c.Selected = wasChecked;
                }
                else
                {
                    c.Selected = !string.Equals(c.NewTitle, c.OldTitle, StringComparison.Ordinal) && !IsSessionRunning(s);
                }

                list.Add(c);
            }

            list.Sort(delegate(SessionRenameCandidate a, SessionRenameCandidate b)
            {
                int cmp = DateTime.Compare(b.UpdatedTime, a.UpdatedTime);
                if (cmp != 0) return cmp;
                cmp = string.Compare(a.ProjectName, b.ProjectName, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0) return cmp;
                return string.Compare(a.OldTitle, b.OldTitle, StringComparison.OrdinalIgnoreCase);
            });

            _allCandidates = list;
        }

        private void PopulateProjectFilterDropdown(string preferredSelection)
        {
            _isSuppressingFilterEvents = true;
            try
            {
                cmbProjectFilter.Items.Clear();
                if (!string.IsNullOrEmpty(_scopedFilterLabel))
                {
                    cmbProjectFilter.Items.Add(_scopedFilterLabel);
                }
                cmbProjectFilter.Items.Add(FilterAllFormalProjects);
                cmbProjectFilter.Items.Add(FilterAllSessions);

                HashSet<string> projNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                bool hasOutside = false;
                for (int i = 0; i < _allCandidates.Count; i++)
                {
                    SessionRenameCandidate c = _allCandidates[i];
                    if (c == null || string.IsNullOrEmpty(c.ProjectName)) continue;
                    if (VisualDiffEngine.IsOutsideProject(c.ProjectName))
                    {
                        hasOutside = true;
                        continue;
                    }
                    if (SmartProjectFilter.IsJunkOrEphemeral(c.ProjectName) || (c.Summary != null && c.Summary.IsSubagent))
                        continue;
                    projNames.Add(c.ProjectName);
                }

                List<string> sortedProjs = new List<string>(projNames);
                sortedProjs.Sort(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < sortedProjs.Count; i++)
                    cmbProjectFilter.Items.Add(sortedProjs[i]);
                if (hasOutside)
                    cmbProjectFilter.Items.Add("Outside of Project");

                if (!string.IsNullOrEmpty(preferredSelection))
                {
                    for (int i = 0; i < cmbProjectFilter.Items.Count; i++)
                    {
                        if (string.Equals(cmbProjectFilter.Items[i].ToString(), preferredSelection, StringComparison.OrdinalIgnoreCase))
                        {
                            cmbProjectFilter.SelectedIndex = i;
                            return;
                        }
                    }
                }

                cmbProjectFilter.SelectedIndex = 0;
            }
            finally
            {
                _isSuppressingFilterEvents = false;
            }
        }

        private void InitializeFilterStateFromInitialScope()
        {
            _isSuppressingFilterEvents = true;
            try
            {
                bool anyTaggedInScope = false;
                bool anyOutsideInScope = false;
                bool anyRunningInScope = false;

                if (_initialScopedConvIds.Count > 0)
                {
                    for (int i = 0; i < _allCandidates.Count; i++)
                    {
                        SessionRenameCandidate c = _allCandidates[i];
                        if (!_initialScopedConvIds.Contains(c.ConversationId)) continue;
                        if (AutoTitleSynthesizer.HasStandardBracketedPrefix(c.OldTitle)) anyTaggedInScope = true;
                        if (VisualDiffEngine.IsOutsideProject(c.ProjectName)) anyOutsideInScope = true;
                        if (IsSessionRunning(c.Summary)) anyRunningInScope = true;
                    }
                }

                if (_scopedFromExplicitSessions)
                {
                    chkIncludeTagged.Checked = anyTaggedInScope;
                    chkIncludeOutside.Checked = anyOutsideInScope;
                    chkIncludeRunning.Checked = anyRunningInScope;
                }
                else if (!string.IsNullOrEmpty(_initialProjectName) && VisualDiffEngine.IsOutsideProject(_initialProjectName))
                {
                    chkIncludeOutside.Checked = true;
                }
            }
            finally
            {
                _isSuppressingFilterEvents = false;
            }

            string preferred = FilterAllFormalProjects;
            if (_scopedFromExplicitSessions && !string.IsNullOrEmpty(_scopedFilterLabel))
            {
                preferred = _scopedFilterLabel;
            }
            else if (!string.IsNullOrEmpty(_initialProjectName))
            {
                preferred = _initialProjectName;
            }
            else if (!string.IsNullOrEmpty(_scopedFilterLabel))
            {
                preferred = _scopedFilterLabel;
            }

            PopulateProjectFilterDropdown(preferred);
        }

        private void ApplyFiltersAndPopulateGrid()
        {
            string selectedFilter = cmbProjectFilter.SelectedItem != null ? cmbProjectFilter.SelectedItem.ToString() : FilterAllFormalProjects;
            bool isScopedMode = !string.IsNullOrEmpty(_scopedFilterLabel) && string.Equals(selectedFilter, _scopedFilterLabel, StringComparison.Ordinal);
            bool isAllFormal = string.Equals(selectedFilter, FilterAllFormalProjects, StringComparison.Ordinal);
            bool isAllSessions = string.Equals(selectedFilter, FilterAllSessions, StringComparison.Ordinal);
            bool isSpecificOutside = VisualDiffEngine.IsOutsideProject(selectedFilter);

            bool includeTagged = chkIncludeTagged.Checked;
            bool includeOutside = chkIncludeOutside.Checked || isSpecificOutside;
            bool includeRunning = chkIncludeRunning.Checked;

            List<SessionRenameCandidate> visible = new List<SessionRenameCandidate>();
            for (int i = 0; i < _allCandidates.Count; i++)
            {
                SessionRenameCandidate c = _allCandidates[i];
                if (c == null) continue;

                bool isSubagent = c.Summary != null && c.Summary.IsSubagent;
                bool isJunk = SmartProjectFilter.IsJunkOrEphemeral(c.ProjectName);
                bool isOutside = VisualDiffEngine.IsOutsideProject(c.ProjectName);
                bool isTagged = AutoTitleSynthesizer.HasStandardBracketedPrefix(c.OldTitle);
                bool isRunning = IsSessionRunning(c.Summary);

                if (isScopedMode)
                {
                    if (!_initialScopedConvIds.Contains(c.ConversationId)) continue;
                }
                else if (isAllFormal)
                {
                    if (isSubagent || isJunk) continue;
                }
                else if (isAllSessions)
                {
                    // 包含所有会话（仍尊重下方三个复选开关）
                }
                else
                {
                    if (isSubagent || (!isSpecificOutside && isJunk)) continue;
                    if (!string.Equals(c.ProjectName, selectedFilter, StringComparison.OrdinalIgnoreCase)) continue;
                }

                if (isOutside && !includeOutside) continue;
                if (isTagged && !includeTagged) continue;
                if (isRunning && !includeRunning) continue;

                visible.Add(c);
            }

            _visibleCandidates = visible;

            _isPopulatingGrid = true;
            try
            {
                dgvCandidates.Rows.Clear();
                for (int i = 0; i < _visibleCandidates.Count; i++)
                {
                    SessionRenameCandidate c = _visibleCandidates[i];
                    string promptPreview = !string.IsNullOrEmpty(c.CleanedPrompt)
                        ? c.CleanedPrompt
                        : AutoTitleSynthesizer.CleanFirstPrompt(c.FirstPromptSnippet);
                    int rowIdx = dgvCandidates.Rows.Add(
                        c.Selected,
                        c.ProjectName,
                        c.OldTitle,
                        promptPreview,
                        c.NewTitle,
                        BuildStatusText(c));
                    DataGridViewRow row = dgvCandidates.Rows[rowIdx];
                    row.Tag = c;
                    ApplyCandidateRowVisualStyle(row, c);
                }

                if (dgvCandidates.Rows.Count > 0)
                {
                    dgvCandidates.Rows[0].Selected = true;
                }
            }
            finally
            {
                _isPopulatingGrid = false;
            }

            UpdateSummaryLabel();
            UpdatePreviewBox();
        }

        private string BuildStatusText(SessionRenameCandidate c)
        {
            if (c == null) return string.Empty;
            bool isRunning = IsSessionRunning(c.Summary);
            bool isEdited = _manuallyEditedCids.Contains(c.ConversationId);
            bool changed = !string.Equals((c.NewTitle ?? string.Empty).Trim(), (c.OldTitle ?? string.Empty).Trim(), StringComparison.Ordinal);

            if (isRunning)
                return isEdited ? "运行中(已编辑)" : "运行中";
            if (isEdited)
                return changed ? "已编辑" : "已手改(同原)";
            return changed ? "待重命名" : "无变化";
        }

        private void ApplyCandidateRowVisualStyle(DataGridViewRow row, SessionRenameCandidate c)
        {
            if (row == null || c == null) return;
            row.Cells[colStatus.Index].Value = BuildStatusText(c);

            bool isEdited = _manuallyEditedCids.Contains(c.ConversationId);
            bool isRunning = IsSessionRunning(c.Summary);
            bool changed = !string.Equals((c.NewTitle ?? string.Empty).Trim(), (c.OldTitle ?? string.Empty).Trim(), StringComparison.Ordinal);

            if (isEdited)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(220, 252, 231);
                row.DefaultCellStyle.ForeColor = Color.FromArgb(20, 83, 45);
            }
            else if (isRunning)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 251, 235);
                row.DefaultCellStyle.ForeColor = Color.FromArgb(146, 64, 14);
            }
            else if (!changed)
            {
                row.DefaultCellStyle.BackColor = Color.White;
                row.DefaultCellStyle.ForeColor = Color.FromArgb(100, 116, 139);
            }
            else
            {
                row.DefaultCellStyle.BackColor = Color.White;
                row.DefaultCellStyle.ForeColor = Color.FromArgb(30, 41, 59);
            }
        }

        private void DgvCandidates_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_isPopulatingGrid || e.RowIndex < 0 || e.RowIndex >= dgvCandidates.Rows.Count) return;

            DataGridViewRow row = dgvCandidates.Rows[e.RowIndex];
            SessionRenameCandidate cand = row.Tag as SessionRenameCandidate;
            if (cand == null) return;

            if (e.ColumnIndex == colSelect.Index)
            {
                object val = row.Cells[colSelect.Index].Value;
                cand.Selected = (val is bool) && (bool)val;
                UpdateSummaryLabel();
            }
            else if (e.ColumnIndex == colNewTitle.Index)
            {
                string raw = Convert.ToString(row.Cells[colNewTitle.Index].Value, CultureInfo.InvariantCulture) ?? string.Empty;
                string normalized = raw.Replace("\r", " ").Replace("\n", " ").Trim();
                if (!string.Equals(raw, normalized, StringComparison.Ordinal))
                {
                    _isPopulatingGrid = true;
                    try { row.Cells[colNewTitle.Index].Value = normalized; }
                    finally { _isPopulatingGrid = false; }
                }

                cand.NewTitle = normalized;
                _manuallyEditedCids.Add(cand.ConversationId);

                if (!string.IsNullOrWhiteSpace(normalized) &&
                    !string.Equals(normalized, (cand.OldTitle ?? string.Empty).Trim(), StringComparison.Ordinal))
                {
                    cand.Selected = true;
                    _isPopulatingGrid = true;
                    try { row.Cells[colSelect.Index].Value = true; }
                    finally { _isPopulatingGrid = false; }
                }

                ApplyCandidateRowVisualStyle(row, cand);
                UpdateSummaryLabel();
                UpdatePreviewBox();
            }
        }

        private void UpdateSummaryLabel()
        {
            int checkedCount = 0;
            int editedCount = 0;
            int runningCount = 0;
            for (int i = 0; i < _visibleCandidates.Count; i++)
            {
                SessionRenameCandidate c = _visibleCandidates[i];
                if (c.Selected) checkedCount++;
                if (_manuallyEditedCids.Contains(c.ConversationId)) editedCount++;
                if (IsSessionRunning(c.Summary)) runningCount++;
            }

            if (_visibleCandidates.Count == 0 && !chkIncludeTagged.Checked)
            {
                lblSummary.Text = "显示: 0 条（可勾选「显示已带【...】前缀」或切换工程范围）";
            }
            else
            {
                lblSummary.Text = string.Format("显示: {0} 条 | 已勾选: {1} 条 | 已编辑: {2} 条 | 运行中: {3} 条",
                    _visibleCandidates.Count, checkedCount, editedCount, runningCount);
            }
        }

        private void UpdatePreviewBox()
        {
            if (dgvCandidates.SelectedRows.Count == 0)
            {
                txtPreview.Text = "（请在上方列表中点击选中一条会话查看完整 ID、Git 分支提示、运行状态与首问原文）";
                return;
            }

            DataGridViewRow row = dgvCandidates.SelectedRows[0];
            SessionRenameCandidate c = row.Tag as SessionRenameCandidate;
            if (c == null)
            {
                txtPreview.Text = string.Empty;
                return;
            }

            string runStatus = (c.Summary != null && !string.IsNullOrWhiteSpace(c.Summary.RunStatus))
                ? c.Summary.RunStatus
                : "CASCADE_RUN_STATUS_IDLE";
            if (c.Summary != null && c.Summary.NotFullyIdle)
                runStatus += " (NotFullyIdle=true)";

            StringBuilder sb = new StringBuilder();
            sb.Append("ConversationId: ").AppendLine(c.ConversationId ?? string.Empty);
            sb.Append("工程: ").Append(c.ProjectName ?? string.Empty);
            if (!string.IsNullOrEmpty(c.FolderUri))
                sb.Append("  |  路径: ").Append(MainForm.UriToDisplayPath(c.FolderUri));
            sb.AppendLine();
            sb.Append("GitBranchHint: ").Append(string.IsNullOrWhiteSpace(c.GitBranchHint) ? "(无)" : c.GitBranchHint);
            sb.Append("  |  RunStatus: ").Append(runStatus);
            sb.Append("  |  建议前缀: ").AppendLine(c.SuggestedPrefix ?? string.Empty);
            sb.Append("原标题: ").AppendLine(c.OldTitle ?? string.Empty);
            sb.Append("拟定新标题: ").AppendLine(c.NewTitle ?? string.Empty);
            sb.AppendLine("── 首问全文 (FirstPromptSnippet) ──");
            string snippet = string.IsNullOrWhiteSpace(c.FirstPromptSnippet) ? "(暂无首问正文摘要)" : c.FirstPromptSnippet;
            sb.Append(snippet.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n"));

            txtPreview.Text = sb.ToString();
            txtPreview.SelectionStart = 0;
            txtPreview.SelectionLength = 0;
        }

        private void SetAllVisibleChecked(bool isChecked)
        {
            dgvCandidates.EndEdit();
            _isPopulatingGrid = true;
            try
            {
                for (int i = 0; i < dgvCandidates.Rows.Count; i++)
                {
                    DataGridViewRow row = dgvCandidates.Rows[i];
                    SessionRenameCandidate c = row.Tag as SessionRenameCandidate;
                    if (c != null) c.Selected = isChecked;
                    row.Cells[colSelect.Index].Value = isChecked;
                }
            }
            finally
            {
                _isPopulatingGrid = false;
            }
            UpdateSummaryLabel();
        }

        private List<SessionRenameCandidate> GetTargetRowsForExchange()
        {
            dgvCandidates.EndEdit();
            List<SessionRenameCandidate> checkedRows = new List<SessionRenameCandidate>();
            for (int i = 0; i < _visibleCandidates.Count; i++)
            {
                if (_visibleCandidates[i].Selected)
                    checkedRows.Add(_visibleCandidates[i]);
            }
            return checkedRows.Count > 0 ? checkedRows : new List<SessionRenameCandidate>(_visibleCandidates);
        }

        private void BtnRecompute_Click(object sender, EventArgs e)
        {
            dgvCandidates.EndEdit();
            ReloadRulesFromDisk(true);

            bool overwriteManual = false;
            if (_manuallyEditedCids.Count > 0)
            {
                DialogResult res = MessageBox.Show(
                    this,
                    string.Format("检测到 {0} 条已手动或通过 AI 回填编辑的标题。\n\n• 是：全部用最新规则重算并覆盖\n• 否：仅重算未手动编辑的行\n• 取消：中止重算", _manuallyEditedCids.Count),
                    "重算建议标题",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);
                if (res == DialogResult.Cancel) return;
                overwriteManual = (res == DialogResult.Yes);
            }

            if (overwriteManual)
                _manuallyEditedCids.Clear();

            for (int i = 0; i < _allCandidates.Count; i++)
            {
                SessionRenameCandidate c = _allCandidates[i];
                if (c == null) continue;
                bool keepManual = !overwriteManual && _manuallyEditedCids.Contains(c.ConversationId);
                string savedManual = keepManual ? c.NewTitle : null;
                c.NewTitle = null;
                AutoTitleSynthesizer.SynthesizeCandidate(c, _rules);
                if (keepManual && !string.IsNullOrWhiteSpace(savedManual))
                {
                    c.NewTitle = savedManual;
                }
            }

            ApplyFiltersAndPopulateGrid();
        }

        private void BtnEditRules_Click(object sender, EventArgs e)
        {
            dgvCandidates.EndEdit();
            using (RuleJsonEditorDialog dlg = new RuleJsonEditorDialog(_repositoryPath))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    ReloadRulesFromDisk(true);
                    BtnRecompute_Click(sender, EventArgs.Empty);
                }
            }
        }

        private void BtnRollbackHistory_Click(object sender, EventArgs e)
        {
            dgvCandidates.EndEdit();
            using (RollbackHistoryDialog dlg = new RollbackHistoryDialog(_geminiPath, _promptStrategy))
            {
                dlg.ShowDialog(this);
                if (dlg.RolledBackAny)
                {
                    this.AnyDataChanged = true;
                    ReloadLocalSummariesAndRefreshGrid();
                }
            }
        }

        private void ReloadLocalSummariesAndRefreshGrid()
        {
            Dictionary<string, ProjectMetadata> dummyProjects;
            List<string> warnings;
            _rawSummaries = MainForm.LoadLocalThreeLayerSummaries(_geminiPath, out dummyProjects, out warnings);

            string currentFilter = cmbProjectFilter.SelectedItem != null ? cmbProjectFilter.SelectedItem.ToString() : FilterAllFormalProjects;
            RebuildCandidatesFromRawSummaries(false);
            PopulateProjectFilterDropdown(currentFilter);
            ApplyFiltersAndPopulateGrid();
        }

        private void BtnCopyAiPrompt_Click(object sender, EventArgs e)
        {
            List<SessionRenameCandidate> rows = GetTargetRowsForExchange();
            if (rows.Count == 0)
            {
                MessageBox.Show(this, "当前视图没有任何会话可导出给 AI 润色。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string prompt = AiPolishPromptExchange.BuildPolishPrompt(rows);
            try
            {
                Clipboard.SetText(prompt);
                MessageBox.Show(
                    this,
                    string.Format("已将 {0} 条会话的 AI 润色提示词（含完整 JSON 与规范前缀约束）复制到剪贴板！\n\n请粘贴发送给大模型后，复制其返回的 JSON 代码块，再点击「📥 从剪贴板回填」。", rows.Count),
                    "复制成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "复制到剪贴板失败:\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyParsedTitlesToVisibleRows(Dictionary<string, string> cidToNewTitle, string sourceLabel)
        {
            if (cidToNewTitle == null || cidToNewTitle.Count == 0) return;

            int updatedCount = 0;
            _isPopulatingGrid = true;
            try
            {
                for (int i = 0; i < dgvCandidates.Rows.Count; i++)
                {
                    DataGridViewRow row = dgvCandidates.Rows[i];
                    SessionRenameCandidate c = row.Tag as SessionRenameCandidate;
                    if (c == null) continue;

                    string newTitle;
                    if (cidToNewTitle.TryGetValue(c.ConversationId, out newTitle) && !string.IsNullOrWhiteSpace(newTitle))
                    {
                        c.NewTitle = newTitle.Trim();
                        c.Selected = true;
                        _manuallyEditedCids.Add(c.ConversationId);
                        row.Cells[colNewTitle.Index].Value = c.NewTitle;
                        row.Cells[colSelect.Index].Value = true;
                        ApplyCandidateRowVisualStyle(row, c);
                        updatedCount++;
                    }
                }
            }
            finally
            {
                _isPopulatingGrid = false;
            }

            UpdateSummaryLabel();
            UpdatePreviewBox();
            MessageBox.Show(
                this,
                string.Format("已从{0}成功回填 {1} 条标题（已自动勾选并以浅绿色高亮标出）。\n\n确认无误后请点击「执行重命名」落盘生效。", sourceLabel, updatedCount),
                "回填成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnPasteAiJson_Click(object sender, EventArgs e)
        {
            dgvCandidates.EndEdit();
            if (_visibleCandidates.Count == 0)
            {
                MessageBox.Show(this, "当前列表为空，无法匹配回填。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string clipText = string.Empty;
            try
            {
                if (Clipboard.ContainsText()) clipText = Clipboard.GetText();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "读取剪贴板失败:\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Dictionary<string, string> parsed;
            string err;
            if (!AiPolishPromptExchange.ParseAiClipboardJson(clipText, _visibleCandidates, out parsed, out err))
            {
                MessageBox.Show(this, "剪贴板内容回填失败:\n\n" + err, "JSON 校验失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ApplyParsedTitlesToVisibleRows(parsed, "剪贴板");
        }

        private void BtnExportJson_Click(object sender, EventArgs e)
        {
            List<SessionRenameCandidate> rows = GetTargetRowsForExchange();
            if (rows.Count == 0)
            {
                MessageBox.Show(this, "当前视图没有任何会话可导出。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*";
                sfd.Title = "导出待重命名会话清单 JSON";
                sfd.FileName = string.Format("rename_candidates_{0}.json", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        string json = AiPolishPromptExchange.ExportRowsToJson(rows);
                        LocalWorkspaceManager.WriteUtf8TextAtomicWithBackup(sfd.FileName, json, false, null);
                        MessageBox.Show(this, string.Format("已导出 {0} 条候选会话至:\n{1}", rows.Count, sfd.FileName), "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "导出 JSON 失败:\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnImportJson_Click(object sender, EventArgs e)
        {
            dgvCandidates.EndEdit();
            if (_visibleCandidates.Count == 0)
            {
                MessageBox.Show(this, "当前列表为空，无法匹配导入。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*";
                ofd.Title = "选择回填标题 JSON 文件";
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        string rawText = File.ReadAllText(ofd.FileName, Encoding.UTF8);
                        Dictionary<string, string> parsed;
                        string err;
                        if (!AiPolishPromptExchange.ParseAiClipboardJson(rawText, _visibleCandidates, out parsed, out err))
                        {
                            MessageBox.Show(this, "JSON 导入校验失败:\n\n" + err, "JSON 校验失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        ApplyParsedTitlesToVisibleRows(parsed, "JSON 文件");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "读取或解析 JSON 文件失败:\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnExecuteRename_Click(object sender, EventArgs e)
        {
            dgvCandidates.EndEdit();

            Dictionary<string, string> targets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<string> previews = new List<string>();
            int unchangedSkipped = 0;

            for (int i = 0; i < _visibleCandidates.Count; i++)
            {
                SessionRenameCandidate c = _visibleCandidates[i];
                if (c == null || !c.Selected) continue;

                string proposed = (c.NewTitle ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
                if (string.IsNullOrWhiteSpace(proposed))
                {
                    MessageBox.Show(this, string.Format("会话 [{0}]（{1}）的拟定新标题不能为空！", c.OldTitle, c.ConversationId),
                        "输入校验失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (IncrementalProtoShardMerger.IsSyntheticPlaceholderTitle(proposed))
                {
                    MessageBox.Show(this, string.Format("会话 [{0}] 拒绝写入系统合成占位标题: {1}", c.ConversationId, proposed),
                        "输入校验失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (string.Equals(proposed, (c.OldTitle ?? string.Empty).Trim(), StringComparison.Ordinal))
                {
                    unchangedSkipped++;
                    continue;
                }

                targets[c.ConversationId] = proposed;
                previews.Add(string.Format("[{0}] {1}  →  {2}", c.ProjectName, c.OldTitle, proposed));
            }

            if (targets.Count == 0)
            {
                string msg = unchangedSkipped > 0
                    ? string.Format("已勾选的 {0} 条会话新旧标题完全一致，无需写入。", unchangedSkipped)
                    : "请先在表格左侧勾选至少一条需要重命名的会话！";
                MessageBox.Show(this, msg, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult confirm = MessageBox.Show(
                this,
                string.Format("即将对以下 {0} 条会话执行三层重命名写入（操作前自动保存完整回滚快照）：\n\n{1}{2}",
                    targets.Count,
                    MainForm.FormatItemPreviewList(previews, 10),
                    unchangedSkipped > 0 ? string.Format("\n\n（另有 {0} 条勾选但标题无变化的会话将自动跳过）", unchangedSkipped) : string.Empty),
                "确认批量重命名",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            RenameProcessStrategy strategy = _promptStrategy != null
                ? _promptStrategy("批量重命名会话")
                : RenameProcessStrategy.CloseAndFullWrite;
            if (strategy == RenameProcessStrategy.Cancel) return;

            bool allowHotWrite = (strategy == RenameProcessStrategy.HotWritePersistentLayers);
            RenameBatchResult res;
            bool ok = LocalWorkspaceManager.RenameSessionsBatch(_geminiPath, targets, allowHotWrite, out res);

            if (ok && res != null && res.RenamedCount > 0)
            {
                this.AnyDataChanged = true;

                StringBuilder report = new StringBuilder();
                report.AppendFormat("批量重命名完成！成功更新 {0} 条会话。\n\n", res.RenamedCount);
                report.AppendFormat("• Layer 1 (.pbtxt 标注): {0}\n", res.Layer1Updated ? "已更新" : "未触发");
                report.AppendFormat("• Layer 2 (summary.sqlite): {0}\n", res.Layer2Updated ? "已更新" : "未存在或未更新");
                report.AppendFormat("• Layer 3 (agyhub_summaries_proto.pb): {0}\n", res.Layer3Updated ? "已更新" : (allowHotWrite ? "热写模式跳过/被占用" : "未更新"));
                if (res.SkippedRunningIds != null && res.SkippedRunningIds.Count > 0)
                    report.AppendFormat("• 热写跳过运行中会话: {0} 条\n", res.SkippedRunningIds.Count);
                if (!string.IsNullOrEmpty(res.BackupSnapshotDir))
                    report.AppendFormat("• 回滚快照已保存: {0}\n", Path.GetFileName(res.BackupSnapshotDir));
                if (allowHotWrite)
                    report.AppendLine("\n提示: 免关机热写模式下，切换工程或重启 Antigravity 后侧边栏缓存即可刷新同步。");
                if (res.Warnings != null && res.Warnings.Count > 0)
                    report.AppendFormat("\n告警 ({0} 项):\n{1}", res.Warnings.Count, MainForm.FormatItemPreviewList(res.Warnings, 6));

                MessageBox.Show(
                    this,
                    report.ToString(),
                    (res.Warnings != null && res.Warnings.Count > 0) ? "批量重命名完成（含告警）" : "批量重命名完成",
                    MessageBoxButtons.OK,
                    (res.Warnings != null && res.Warnings.Count > 0) ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

                ReloadLocalSummariesAndRefreshGrid();
            }
            else
            {
                string detail = (res != null && res.Warnings != null && res.Warnings.Count > 0)
                    ? string.Join("\n", res.Warnings.ToArray())
                    : (LocalWorkspaceManager.LastWarning ?? "未能写入任何会话。");
                MessageBox.Show(this, "批量重命名未写入:\n\n" + detail, "操作提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    /// <summary>
    /// 批量重命名三层快照回滚历史弹窗
    /// </summary>
    public class RollbackHistoryDialog : Form
    {
        private readonly string _geminiPath;
        private readonly Func<string, RenameProcessStrategy> _promptStrategy;
        private readonly List<Font> _ownedFonts = new List<Font>();

        private List<RenameSnapshotInfo> _snapshots = new List<RenameSnapshotInfo>();
        private SplitContainer _split;
        private DoubleBufferedDataGridView dgvSnapshots;
        private TextBox txtPreview;
        private Button btnRollback;
        private bool _splitterInitialized = false;

        public bool RolledBackAny { get; private set; }

        public RollbackHistoryDialog(string geminiPath, Func<string, RenameProcessStrategy> promptStrategy)
        {
            _geminiPath = geminiPath ?? string.Empty;
            _promptStrategy = promptStrategy;

            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Text = "三层重命名快照回滚历史";
            this.Size = new Size(920, 600);
            this.MinimumSize = new Size(680, 440);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(248, 250, 252);

            Font baseFont = TrackFont(new Font("Microsoft YaHei UI", 9f));
            Font boldFont = TrackFont(new Font("Microsoft YaHei UI", 9f, FontStyle.Bold));
            Font previewFont = TrackFont(new Font("Microsoft YaHei UI", 9f));
            this.Font = baseFont;

            Panel pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Padding = new Padding(12, 10, 12, 10)
            };
            Label lblTop = new Label
            {
                Text = "选择要恢复的历史重命名快照（按会话 UUID 精准还原 Layer 1 .pbtxt / Layer 2 SQLite / Layer 3 .pb，绝不覆盖此后新增的其他会话）：",
                Dock = DockStyle.Top,
                AutoSize = true,
                Font = baseFont,
                ForeColor = Color.FromArgb(51, 65, 85)
            };
            pnlTop.Controls.Add(lblTop);

            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(226, 232, 240)
            };
            _split.Resize += (s, e) => AdjustSplitterLayout(false);

            int rowH = Math.Max(26, TextRenderer.MeasureText("测Ay", baseFont).Height + 10);
            dgvSnapshots = new DoubleBufferedDataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = Math.Max(28, rowH + 2),
                Font = baseFont
            };
            dgvSnapshots.RowTemplate.Height = rowH;
            dgvSnapshots.EnableHeadersVisualStyles = false;
            dgvSnapshots.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            dgvSnapshots.ColumnHeadersDefaultCellStyle.Font = boldFont;
            dgvSnapshots.DataError += (s, e) => { e.ThrowException = false; };

            dgvSnapshots.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "创建时间", FillWeight = 24, MinimumWidth = 140 });
            dgvSnapshots.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "条目数", Width = 78, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            dgvSnapshots.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = ".pb 整库备份", Width = 98, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            dgvSnapshots.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "快照目录", FillWeight = 46, MinimumWidth = 180 });
            dgvSnapshots.SelectionChanged += (s, e) => UpdatePreview();
            _split.Panel1.Controls.Add(dgvSnapshots);

            txtPreview = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = previewFont,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 41, 59),
                BorderStyle = BorderStyle.FixedSingle
            };
            _split.Panel2.Controls.Add(txtPreview);

            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                Padding = new Padding(12, 9, 12, 9),
                BackColor = Color.FromArgb(241, 245, 249)
            };

            FlowLayoutPanel flowRight = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            Button btnRefresh = new Button
            {
                Text = "刷新",
                AutoSize = true,
                MinimumSize = new Size(76, 30),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 8, 0)
            };
            btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnRefresh.Click += (s, e) => LoadSnapshots();

            btnRollback = new Button
            {
                Text = "回滚选中快照",
                AutoSize = true,
                MinimumSize = new Size(124, 30),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = boldFont,
                Margin = new Padding(0, 0, 8, 0)
            };
            btnRollback.FlatAppearance.BorderSize = 0;
            btnRollback.Click += BtnRollback_Click;

            Button btnClose = new Button
            {
                Text = "关闭",
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                MinimumSize = new Size(80, 30),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0)
            };
            btnClose.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);

            flowRight.Controls.AddRange(new Control[] { btnRefresh, btnRollback, btnClose });
            pnlBottom.Controls.Add(flowRight);
            this.CancelButton = btnClose;

            this.Controls.Add(_split);
            this.Controls.Add(pnlBottom);
            this.Controls.Add(pnlTop);

            this.Load += (s, e) => MainForm.ClampFormToWorkingArea(this);
            this.Shown += (s, e) =>
            {
                AdjustSplitterLayout(true);
                MainForm.ClearInitialEditSelections(this, txtPreview);
            };

            LoadSnapshots();
        }

        private Font TrackFont(Font f)
        {
            if (f != null) _ownedFonts.Add(f);
            return f;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                for (int i = 0; i < _ownedFonts.Count; i++)
                {
                    if (_ownedFonts[i] != null)
                    {
                        try { _ownedFonts[i].Dispose(); } catch {}
                    }
                }
                _ownedFonts.Clear();
            }
        }

        private void AdjustSplitterLayout(bool initial)
        {
            if (_split == null) return;
            if (initial && _splitterInitialized) return;
            try
            {
                int h = _split.ClientSize.Height;
                int sw = _split.SplitterWidth;
                if (h <= sw + 100) return;

                int minP1 = Math.Min(140, (h - sw) / 3);
                int minP2 = Math.Min(120, (h - sw) / 3);
                _split.Panel1MinSize = 0;
                _split.Panel2MinSize = 0;

                int maxDist = h - sw - minP2;
                if (initial)
                {
                    int target = (int)(h * 0.45);
                    if (target < minP1) target = minP1;
                    if (target > maxDist) target = maxDist;
                    _split.SplitterDistance = target;
                    _splitterInitialized = true;
                }
                else
                {
                    int cur = _split.SplitterDistance;
                    if (cur < minP1) _split.SplitterDistance = minP1;
                    else if (cur > maxDist && maxDist >= minP1) _split.SplitterDistance = maxDist;
                }

                _split.Panel1MinSize = minP1;
                _split.Panel2MinSize = minP2;
            }
            catch {}
        }

        private void LoadSnapshots()
        {
            _snapshots = RenameBackupManager.ListSnapshots(_geminiPath);
            dgvSnapshots.Rows.Clear();

            for (int i = 0; i < _snapshots.Count; i++)
            {
                RenameSnapshotInfo snap = _snapshots[i];
                string timeStr = snap.CreatedTime > DateTime.MinValue
                    ? snap.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    : (snap.CreatedAt ?? "-");
                int rowIdx = dgvSnapshots.Rows.Add(
                    timeStr,
                    snap.ItemCount,
                    snap.HasPbBackup ? "有" : "无",
                    snap.SnapshotName);
                dgvSnapshots.Rows[rowIdx].Tag = snap;
            }

            if (dgvSnapshots.Rows.Count > 0)
            {
                dgvSnapshots.Rows[0].Selected = true;
            }
            btnRollback.Enabled = (dgvSnapshots.Rows.Count > 0);
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (dgvSnapshots.SelectedRows.Count == 0)
            {
                txtPreview.Text = _snapshots.Count == 0
                    ? "（暂无任何重命名回滚快照；每次执行单条或批量重命名时会自动生成快照）"
                    : "（请选择一条快照记录查看修改前后的标题对照）";
                return;
            }

            RenameSnapshotInfo snap = dgvSnapshots.SelectedRows[0].Tag as RenameSnapshotInfo;
            if (snap == null)
            {
                txtPreview.Text = string.Empty;
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("快照目录: ").AppendLine(snap.SnapshotDir ?? string.Empty);
            sb.Append("创建时间: ").Append(snap.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.Append("  |  记录条数: ").Append(snap.ItemCount);
            sb.Append("  |  .pb 备份: ").AppendLine(snap.HasPbBackup ? "有" : "无");
            sb.AppendLine("── 变更明细 (原标题 → 修改后标题) ──");

            if (snap.Items != null)
            {
                for (int i = 0; i < snap.Items.Count; i++)
                {
                    RenameSnapshotItem item = snap.Items[i];
                    if (item == null) continue;
                    string shortCid = (!string.IsNullOrEmpty(item.ConversationId) && item.ConversationId.Length >= 8)
                        ? item.ConversationId.Substring(0, 8)
                        : (item.ConversationId ?? string.Empty);
                    sb.AppendFormat("{0}. [{1}] ({2})  \"{3}\"  ->  \"{4}\"\r\n",
                        i + 1,
                        string.IsNullOrEmpty(item.ProjectName) ? "-" : item.ProjectName,
                        shortCid,
                        item.OldTitle ?? string.Empty,
                        item.NewTitle ?? string.Empty);
                }
            }

            txtPreview.Text = sb.ToString();
            txtPreview.SelectionStart = 0;
            txtPreview.SelectionLength = 0;
        }

        private void BtnRollback_Click(object sender, EventArgs e)
        {
            if (dgvSnapshots.SelectedRows.Count == 0)
            {
                MessageBox.Show(this, "请先选择一条快照记录！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            RenameSnapshotInfo snap = dgvSnapshots.SelectedRows[0].Tag as RenameSnapshotInfo;
            if (snap == null || string.IsNullOrEmpty(snap.SnapshotDir)) return;

            DialogResult confirm = MessageBox.Show(
                this,
                string.Format("确定要将快照 [{0}] 中的 {1} 条会话标题回滚到修改前状态吗？", snap.SnapshotName, snap.ItemCount),
                "确认回滚快照",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            RenameProcessStrategy strategy = _promptStrategy != null
                ? _promptStrategy("回滚重命名快照")
                : RenameProcessStrategy.CloseAndFullWrite;
            if (strategy == RenameProcessStrategy.Cancel) return;

            bool allowHotWrite = (strategy == RenameProcessStrategy.HotWritePersistentLayers);
            RenameRollbackResult rbRes;
            bool ok = RenameBackupManager.RollbackSnapshot(_geminiPath, snap.SnapshotDir, allowHotWrite, out rbRes);

            if (ok && rbRes != null && rbRes.Success)
            {
                this.RolledBackAny = true;

                StringBuilder report = new StringBuilder();
                report.AppendLine("快照回滚完成！");
                report.AppendFormat("\n• Layer 1 (.pbtxt): 还原 {0} 个，删除新建 {1} 个，跳过已删除会话 {2} 个",
                    rbRes.RestoredPbtxtCount, rbRes.DeletedPbtxtCount, rbRes.SkippedOrphanPbtxtCount);
                report.AppendFormat("\n• Layer 2 (summary.sqlite): 还原 {0} 条", rbRes.RestoredDbCount);
                report.AppendFormat("\n• Layer 3 (.pb 索引): {0}",
                    rbRes.PbSkippedDueToLock ? "热写模式跳过（文件被占用）" : string.Format("还原 {0} 条", rbRes.RestoredPbCount));

                if (allowHotWrite)
                    report.AppendLine("\n\n提示: 免关机热写模式下，切换工程或重启 Antigravity 后侧边栏缓存即可刷新同步。");
                if (rbRes.Warnings != null && rbRes.Warnings.Count > 0)
                    report.AppendFormat("\n\n告警 ({0} 项):\n{1}", rbRes.Warnings.Count, MainForm.FormatItemPreviewList(rbRes.Warnings, 6));

                MessageBox.Show(
                    this,
                    report.ToString(),
                    (rbRes.Warnings != null && rbRes.Warnings.Count > 0) ? "回滚完成（含告警）" : "回滚完成",
                    MessageBoxButtons.OK,
                    (rbRes.Warnings != null && rbRes.Warnings.Count > 0) ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            else
            {
                string errMsg = rbRes != null && !string.IsNullOrEmpty(rbRes.ErrorMessage) ? rbRes.ErrorMessage : "未知错误";
                MessageBox.Show(this, "回滚失败:\n\n" + errMsg, "回滚失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    /// <summary>
    /// 命名规范规则 JSON 编辑器弹窗 (Repository/rename_rules.json)
    /// </summary>
    public class RuleJsonEditorDialog : Form
    {
        private readonly string _repositoryPath;
        private readonly List<Font> _ownedFonts = new List<Font>();
        private TextBox txtJson;

        public RuleJsonEditorDialog(string repositoryPath)
        {
            _repositoryPath = string.IsNullOrWhiteSpace(repositoryPath)
                ? CapabilityRepositoryManager.ResolveDefaultRepositoryPath()
                : repositoryPath.Trim();

            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Text = "编辑会话重命名规范规则 (rename_rules.json)";
            this.Size = new Size(820, 620);
            this.MinimumSize = new Size(620, 440);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(248, 250, 252);

            Font baseFont = TrackFont(new Font("Microsoft YaHei UI", 9f));
            Font boldFont = TrackFont(new Font("Microsoft YaHei UI", 9f, FontStyle.Bold));
            Font monoFont = TrackFont(new Font("Consolas", 10f));
            this.Font = baseFont;

            string rulesPath = Path.Combine(_repositoryPath, "rename_rules.json");

            Panel pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Padding = new Padding(12, 10, 12, 10)
            };
            Label lblPath = new Label
            {
                Text = string.Format("规则文件路径: {0} ({1})", rulesPath, File.Exists(rulesPath) ? "已存在，保存时将校验 JSON 与正则语法" : "尚未创建，当前预览内置默认规则"),
                Dock = DockStyle.Top,
                AutoSize = true,
                Font = baseFont,
                ForeColor = Color.FromArgb(51, 65, 85)
            };
            pnlTop.Controls.Add(lblPath);

            string initialText = AutoTitleSynthesizer.GetDefaultRulesJsonText();
            if (File.Exists(rulesPath))
            {
                try
                {
                    using (FileStream fs = new FileStream(rulesPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        initialText = sr.ReadToEnd();
                    }
                }
                catch {}
            }

            txtJson = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                AcceptsTab = true,
                AcceptsReturn = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = monoFont,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(15, 23, 42),
                Text = NormalizeCrLf(initialText)
            };

            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                Padding = new Padding(12, 9, 12, 9),
                BackColor = Color.FromArgb(241, 245, 249)
            };

            Button btnResetDefault = new Button
            {
                Text = "恢复默认规则",
                Dock = DockStyle.Left,
                AutoSize = true,
                MinimumSize = new Size(110, 30),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnResetDefault.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnResetDefault.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "确定要将编辑器内容重置为内置默认规则吗？（需点击「保存」才会写入磁盘）", "确认重置",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    txtJson.Text = NormalizeCrLf(AutoTitleSynthesizer.GetDefaultRulesJsonText());
                    txtJson.SelectionStart = 0;
                    txtJson.SelectionLength = 0;
                }
            };

            FlowLayoutPanel flowRight = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            Button btnSave = new Button
            {
                Text = "保存",
                AutoSize = true,
                MinimumSize = new Size(88, 30),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = boldFont,
                Margin = new Padding(0, 0, 8, 0)
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) =>
            {
                string err;
                if (!AutoTitleSynthesizer.ValidateAndSaveRules(_repositoryPath, txtJson.Text, out err))
                {
                    MessageBox.Show(this, "规则校验失败，未保存：\n\n" + err, "JSON 校验失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            Button btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                MinimumSize = new Size(80, 30),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0)
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);

            flowRight.Controls.AddRange(new Control[] { btnSave, btnCancel });
            pnlBottom.Controls.Add(flowRight);
            pnlBottom.Controls.Add(btnResetDefault);
            this.CancelButton = btnCancel;

            this.Controls.Add(txtJson);
            this.Controls.Add(pnlBottom);
            this.Controls.Add(pnlTop);

            this.Load += (s, e) => MainForm.ClampFormToWorkingArea(this);
            this.Shown += (s, e) => MainForm.ClearInitialEditSelections(this, txtJson);
        }

        private Font TrackFont(Font f)
        {
            if (f != null) _ownedFonts.Add(f);
            return f;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                for (int i = 0; i < _ownedFonts.Count; i++)
                {
                    if (_ownedFonts[i] != null)
                    {
                        try { _ownedFonts[i].Dispose(); } catch {}
                    }
                }
                _ownedFonts.Clear();
            }
        }

        private static string NormalizeCrLf(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        }
    }
}
