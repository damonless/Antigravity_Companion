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
        private string _geminiPath = string.Empty;
        private List<ConversationSummaryInfo> _localSummaries = new List<ConversationSummaryInfo>();
        private Dictionary<string, ProjectMetadata> _localProjects = new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
        private BackupArchiveIndex _backupIndex;
        private List<ProjectDiffNode> _currentDiffNodes = new List<ProjectDiffNode>();
        private string _loadedBackupPath;
        private readonly ImportHighlightTracker _highlightTracker = new ImportHighlightTracker();

        // ── 持久选择状态集（防止搜索/过滤刷新时勾选丢失） ──
        private readonly HashSet<string> _checkedSessionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _checkedProjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _checkedLocalSessionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _checkedLocalProjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
        private CheckBox chkHideTemp;
        private CheckBox chkImportGlobal;
        private Button btnDetect;
        private Button btnOpenBackup;
        private Button btnFullBackup;
        private Button btnImportSelected;
        private Button btnSelectAll;
        private Button btnDeselectAll;
        private Button btnSelectNewOnly;
        private Button btnLocalExportSelected;
        private Button btnLocalDeleteSelected;
        private Button btnLocalOrphanGc;
        private RichTextBox rtbLog;
        private SplitContainer splitMain;

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
                Margin = new Padding(0, 9, 12, 4),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(51, 65, 85)
            };
            chkHideTemp.CheckedChanged += (s, e) =>
            {
                RenderLocalTree();
                if (_backupIndex != null) RenderBackupTree();
            };

            chkImportGlobal = new CheckBox
            {
                Text = "同步导入全局配置",
                Checked = false,
                Enabled = false,
                AutoSize = true,
                Margin = new Padding(0, 9, 16, 4),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(51, 65, 85)
            };
            chkImportGlobal.CheckedChanged += (s, e) => UpdateSelectionStatistics();

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

            flowTopControls.Controls.AddRange(new Control[] {
                btnDetect, btnOpenBackup, btnFullBackup, chkHideTemp, chkImportGlobal,
                lblSearchIcon, txtSearch, btnImportSelected
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

            FlowLayoutPanel pnlLocalActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 4, 6, 0)
            };

            btnLocalExportSelected = MakeSmallBtn("导出所选(.agy)");
            btnLocalExportSelected.Click += (s, e) => ActionExportSelectedLocalToAgy();

            btnLocalDeleteSelected = MakeSmallBtn("删除所选");
            btnLocalDeleteSelected.ForeColor = Color.FromArgb(185, 28, 28);
            btnLocalDeleteSelected.Click += (s, e) => ActionDeleteSelectedLocal();

            btnLocalOrphanGc = MakeSmallBtn("清理孤儿垃圾");
            btnLocalOrphanGc.Click += (s, e) => ActionRunOrphanGarbageCollector();

            pnlLocalActions.Controls.AddRange(new Control[] { btnLocalExportSelected, btnLocalDeleteSelected, btnLocalOrphanGc });

            lblConvCount = new Label
            {
                Text = "活跃主会话: 0",
                Dock = DockStyle.Right,
                AutoSize = false,
                Width = 115,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(100, 116, 139),
                Padding = new Padding(0, 0, 6, 0)
            };
            pnlLeftHeader.Controls.Add(lblLocalHeader);
            pnlLeftHeader.Controls.Add(lblConvCount);
            pnlLeftHeader.Controls.Add(pnlLocalActions);

            BuildContextMenus();

            tvLocal = new DoubleBufferedTreeView
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f),
                CheckBoxes = true,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                FullRowSelect = true,
                HideSelection = false,
                ContextMenuStrip = ctxLocal
            };
            tvLocal.AfterCheck += TvLocal_AfterCheck;
            tvLocal.NodeMouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Right && e.Node != null)
                {
                    tvLocal.SelectedNode = e.Node;
                }
            };
            tvLocal.KeyDown += TvLocal_KeyDown;

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

            btnSelectNewOnly = MakeSmallBtn("仅选新增/更新");
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
                HideSelection = false,
                ContextMenuStrip = ctxBackup
            };
            tvBackup.AfterCheck += TvBackup_AfterCheck;
            tvBackup.NodeMouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Right && e.Node != null)
                {
                    tvBackup.SelectedNode = e.Node;
                }
            };

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
                pe.Graphics.DrawLine(new Pen(Color.FromArgb(226, 232, 240)), 0, 0, pnlBottomLog.Width, 0);
            };

            rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = Color.FromArgb(30, 41, 59),
                Font = new Font("Consolas", 9f),
                BorderStyle = BorderStyle.None
            };
            pnlBottomLog.Controls.Add(rtbLog);

            // 组装到主窗体
            this.Controls.Add(splitMain);
            this.Controls.Add(pnlBottomLog);
            this.Controls.Add(pnlTop);
        }

        // ══════════════════════════════════════════════════════════
        // 本地环境探测与解析 (Local Detection)
        // ══════════════════════════════════════════════════════════
        private void DetectPaths()
        {
            try
            {
                _geminiPath = Path.Combine(HomePath, ".gemini");

                _localProjects.Clear();
                _localSummaries.Clear();

                Log("正在探测本地 Antigravity 工作区环境: " + _geminiPath, Color.FromArgb(71, 85, 105));

                // 1. 读取本地工程项目列表
                string projDir = Path.Combine(_geminiPath, "config", "projects");
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
                string pbPath = Path.Combine(_geminiPath, "antigravity", "agyhub_summaries_proto.pb");
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
            _isUpdatingChecks = true;
            int visibleProjects = 0;
            int visibleConvsCount = 0;

            try
            {
                tvLocal.Nodes.Clear();
                bool hideTemp = chkHideTemp.Checked;
                string query = txtSearch.Text.Trim();

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

                    bool anyChildChecked = false;
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

                        bool isChecked = _checkedLocalSessionIds.Contains(s.ConversationId);
                        sNode.Checked = isChecked;
                        if (isChecked) anyChildChecked = true;

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

                    pNode.Checked = (visibleList.Count > 0) ? anyChildChecked : _checkedLocalProjectNames.Contains(pName);
                    tvLocal.Nodes.Add(pNode);
                }
            }
            finally
            {
                _isUpdatingChecks = false;
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
                
                // 默认仅选中新增与有更新/修改的会话
                _checkedSessionIds.Clear();
                _checkedProjectNames.Clear();
                Dictionary<string, ConversationSummaryInfo> localConvMap = new Dictionary<string, ConversationSummaryInfo>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < _localSummaries.Count; i++)
                {
                    if (!string.IsNullOrEmpty(_localSummaries[i].ConversationId))
                        localConvMap[_localSummaries[i].ConversationId] = _localSummaries[i];
                }

                for (int i = 0; i < _backupIndex.Conversations.Count; i++)
                {
                    var c = _backupIndex.Conversations[i];
                    if (c.IsSubagent) continue;

                    ConversationSummaryInfo loc;
                    bool exists = localConvMap.TryGetValue(c.ConversationId, out loc);
                    if (!exists)
                    {
                        _checkedSessionIds.Add(c.ConversationId);
                    }
                    else if (loc != null)
                    {
                        bool titleDiff = !string.Equals(c.DisplayTitle, loc.DisplayTitle, StringComparison.Ordinal);
                        bool stepDiff = c.StepCount != loc.StepCount || c.UserInteractions != loc.UserInteractions;
                        bool timeDiff = Math.Abs((c.UpdatedTime - loc.UpdatedTime).TotalSeconds) > 2 && c.UpdatedTime > DateTime.MinValue && loc.UpdatedTime > DateTime.MinValue;
                        bool projDiff = !string.Equals(c.ProjectName, loc.ProjectName, StringComparison.OrdinalIgnoreCase);

                        if (titleDiff || stepDiff || timeDiff || projDiff)
                        {
                            _checkedSessionIds.Add(c.ConversationId);
                        }
                    }
                }

                bool hasGlobalInBackup = _backupIndex.HasRules || _backupIndex.HasSkills || _backupIndex.HasMcp;
                chkImportGlobal.Enabled = hasGlobalInBackup;
                if (!hasGlobalInBackup) chkImportGlobal.Checked = false;

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
                            bool titleDiff = !string.Equals(s.DisplayTitle, loc.DisplayTitle, StringComparison.Ordinal);
                            bool stepDiff = s.StepCount != loc.StepCount || s.UserInteractions != loc.UserInteractions;
                            bool timeDiff = Math.Abs((s.UpdatedTime - loc.UpdatedTime).TotalSeconds) > 2 && s.UpdatedTime > DateTime.MinValue && loc.UpdatedTime > DateTime.MinValue;
                            bool projDiff = !string.Equals(s.ProjectName, loc.ProjectName, StringComparison.OrdinalIgnoreCase);

                            if (titleDiff || stepDiff || timeDiff || projDiff)
                            {
                                sStatus = DiffStatus.Modified;
                                List<string> tags = new List<string>();
                                if (titleDiff)
                                {
                                    string oldT = string.IsNullOrEmpty(loc.DisplayTitle) ? "(空)" : loc.DisplayTitle;
                                    tags.Add("已改名: 本地为 '" + oldT + "'");
                                }
                                if (stepDiff)
                                {
                                    int diff = s.StepCount - loc.StepCount;
                                    if (diff > 0) tags.Add(string.Format("备份多 {0} 步", diff));
                                    else if (diff < 0) tags.Add(string.Format("本地多 {0} 步", -diff));
                                    else tags.Add("对话轮次有差异");
                                }
                                else if (timeDiff)
                                {
                                    if (s.UpdatedTime > loc.UpdatedTime) tags.Add("备份较新");
                                    else tags.Add("本地较新");
                                }
                                if (projDiff)
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
                    tvBackup.Nodes.Add(pNode);
                }
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
                case DiffStatus.Modified:
                    node.ForeColor = Color.FromArgb(124, 58, 237); // 典雅紫 (突出显示已修改/更新)
                    node.NodeFont = new Font(tvBackup.Font, FontStyle.Bold);
                    break;
                case DiffStatus.Aligned:
                    node.ForeColor = Color.FromArgb(71, 85, 105);  // 中性石板灰
                    node.NodeFont = new Font(tvBackup.Font, FontStyle.Regular);
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
                    if (tag.Category == ItemCategory.Conversation)
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
                    SelectNewNodesRecursive(node.Nodes);
                }
            }
        }

        private void UpdateSelectionStatistics()
        {
            int selProjects = 0;
            int selConvs = 0;

            CountCheckedItemsRecursive(tvBackup.Nodes, ref selProjects, ref selConvs);

            string backupState = (_backupIndex != null) ? Path.GetFileName(_loadedBackupPath) : "未加载";
            
            int visibleProjects = tvLocal.Nodes.Count;
            int visibleConvs = 0;
            for (int i = 0; i < tvLocal.Nodes.Count; i++)
            {
                visibleConvs += tvLocal.Nodes[i].Nodes.Count;
            }

            bool importGlobal = chkImportGlobal != null && chkImportGlobal.Enabled && chkImportGlobal.Checked;
            lblStatsSummary.Text = string.Format(
                "本地: {0} 个工程 ({1} 条活跃主会话) | 备份包: {2} | 待合流选中: {3} 个工程 ({4} 条会话) {5}",
                visibleProjects, visibleConvs, backupState, selProjects, selConvs,
                importGlobal ? "[含全局配置]" : ""
            );
        }

        private void CountCheckedItemsRecursive(TreeNodeCollection nodes, ref int projCount, ref int convCount)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode n = nodes[i];
                DiffItemNodeTag tag = n.Tag as DiffItemNodeTag;
                if (tag != null && n.Checked)
                {
                    if (tag.Category == ItemCategory.Project) projCount++;
                    else if (tag.Category == ItemCategory.Conversation) convCount++;
                }
                if (n.Nodes.Count > 0)
                {
                    CountCheckedItemsRecursive(n.Nodes, ref projCount, ref convCount);
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
            bool importGlobal = chkImportGlobal != null && chkImportGlobal.Enabled && chkImportGlobal.Checked;

            CollectSelectedItems(tvBackup.Nodes, selectedConvs, selectedProjects);

            if (selectedConvs.Count == 0 && !importGlobal)
            {
                MessageBox.Show("未勾选任何需要导入的会话或配置项！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!EnsureAntigravityClosed(true)) return;

            // 本地已有同名工程时，自动锁定本地工作区路径并执行无损增量合流 (Merge)
            Dictionary<string, string> projectUriOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string pName in selectedProjects)
            {
                ProjectDiffNode pDiff = _currentDiffNodes.Find(x => string.Equals(x.ProjectName, pName, StringComparison.OrdinalIgnoreCase));
                if (pDiff != null && !string.IsNullOrEmpty(pDiff.LocalFolderUri))
                {
                    projectUriOverrides[pName] = pDiff.LocalFolderUri;
                }
            }

            _highlightTracker.Clear();
            ProgressForm progress = new ProgressForm("正在导入选中项目...", "正在从备份包中安全提取所选会话与脑区索引...");

            _worker = new BackgroundWorker();
            _worker.DoWork += (ws, we) =>
            {
                string gemini = _geminiPath;
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
                        // 1. 建立选中的会话 ID 集合与所属工程映射
                        HashSet<string> convIdsToExtract = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        Dictionary<string, ConversationSummaryInfo> convMap = new Dictionary<string, ConversationSummaryInfo>(StringComparer.OrdinalIgnoreCase);
                        Dictionary<string, string> projectNewIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                        for (int i = 0; i < selectedConvs.Count; i++)
                        {
                            var sc = selectedConvs[i];
                            convIdsToExtract.Add(sc.ConversationId);
                            convMap[sc.ConversationId] = sc;
                            _highlightTracker.AddImportedConversation(sc.ConversationId);
                            _highlightTracker.AddImportedProject(sc.ProjectName);
                        }

                        // 确保每个导入工程在本地 .gemini/config/projects/ 都有合法的标准 Project JSON
                        foreach (string pName in selectedProjects)
                        {
                            if (string.IsNullOrEmpty(pName) || (SmartProjectFilter.IsJunkOrEphemeral(pName) && pName != "Outside of Project"))
                                continue;

                            // 查找本地是否已有同名工程
                            string existingProjId = null;
                            foreach (KeyValuePair<string, ProjectMetadata> kvp in _localProjects)
                            {
                                if (string.Equals(kvp.Value.Name, pName, StringComparison.OrdinalIgnoreCase))
                                {
                                    existingProjId = kvp.Key;
                                    break;
                                }
                            }

                            string pId = existingProjId;
                            if (string.IsNullOrEmpty(pId))
                            {
                                // 从备份包中查找原有 ID
                                if (_backupIndex != null && _backupIndex.Projects != null)
                                {
                                    foreach (KeyValuePair<string, ProjectMetadata> bKvp in _backupIndex.Projects)
                                    {
                                        if (string.Equals(bKvp.Value.Name, pName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            pId = bKvp.Key;
                                            break;
                                        }
                                    }
                                }
                                if (string.IsNullOrEmpty(pId)) pId = Guid.NewGuid().ToString();
                            }

                            projectNewIds[pName] = pId;

                            // 写入标准 Project JSON (仅在本地尚不存在时创建，绝不覆盖破坏本地已有工程路径)
                            string projFile = Path.Combine(projDir, pId + ".json");
                            if (!File.Exists(projFile))
                            {
                                string folderUri = projectUriOverrides.ContainsKey(pName) 
                                    ? projectUriOverrides[pName] 
                                    : ("file:///" + DirectStreamArchiver.UrlEncodePath(Path.Combine(HomePath, pName).Replace('\\', '/'), false));

                                string jsonContent = GenerateStandardProjectJson(pId, pName, folderUri);
                                File.WriteAllText(projFile, jsonContent, Encoding.UTF8);
                                filesImported++;
                            }
                        }

                        // 2. 解压与处理文件
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

                                    // 若是主数据库 .db 文件，深度重映射 SQLite 内部 trajectory_metadata_blob
                                    if (fn.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                                    {
                                        ConversationSummaryInfo sc = convMap.ContainsKey(cid) ? convMap[cid] : null;
                                        string pName = sc != null ? sc.ProjectName : null;
                                        string pId = (pName != null && projectNewIds.ContainsKey(pName)) ? projectNewIds[pName] : null;
                                        string dTitle = sc != null ? sc.DisplayTitle : null;

                                        SqliteMetadataUpdater.RemapSqliteDbMetadata(
                                            dest, srcHome, curHome, projectUriOverrides, pName, pId, dTitle
                                        );
                                    }
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
                            // 工程配置 JSON 同步/提取 (仅当本地尚无该工程文件且属于本次选中导入的工程时提取并重映射)
                            else if (norm.Contains("config/projects/") && norm.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                            {
                                string pId = Path.GetFileNameWithoutExtension(norm);
                                string dest = Path.Combine(gemini, "config", "projects", pId + ".json");
                                
                                if (!File.Exists(dest))
                                {
                                    using (StreamReader sr = new StreamReader(entry.Open(), Encoding.UTF8))
                                    {
                                        string rawJson = sr.ReadToEnd();
                                        string pNameInJson = DirectStreamArchiver.ExtractJsonValue(rawJson, "name");
                                        if (!string.IsNullOrEmpty(pNameInJson) && selectedProjects.Contains(pNameInJson))
                                        {
                                            string remappedJson = DirectStreamArchiver.RemapAllPathFormats(rawJson, srcHome, curHome);
                                            File.WriteAllText(dest, remappedJson, Encoding.UTF8);
                                            filesImported++;
                                        }
                                    }
                                }
                            }
                            // 全局配置 (用户规则、用户技能与插件、MCP 配置，严格排除 builtin/skills)
                            else if (importGlobal && !norm.Contains("builtin/skills") && (
                                     norm.Contains("rules/") || norm.EndsWith(".geminirules") || norm.EndsWith("GEMINI.md") || norm.EndsWith("AGENTS.md") ||
                                     norm.Contains("skills/") || norm.Contains("plugins/") ||
                                     norm.Contains("mcp/") || norm.EndsWith("mcp_config.json")))
                            {
                                int idx = norm.IndexOf("data/");
                                string rel = idx >= 0 ? norm.Substring(idx + 5) : norm;
                                string dest = Path.Combine(gemini, rel.Replace('/', '\\'));
                                ExtractZipEntrySafe(entry, dest);
                                filesImported++;
                            }
                        }

                        // 3. 合流 agyhub_summaries_proto.pb (带标题双向固化与 ProjectId 自动绑定)
                        progress.UpdateMsg("正在增量合流会话摘要与自定义标题索引 (.pb)...");
                        string localPb = Path.Combine(gemini, "antigravity", "agyhub_summaries_proto.pb");
                        byte[] localPbBytes = File.Exists(localPb) ? File.ReadAllBytes(localPb) : new byte[0];

                        byte[] mergedPb = IncrementalProtoShardMerger.MergeShards(
                            localPbBytes, selectedConvs, srcHome, curHome, projectUriOverrides, projectNewIds
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

        public static string GenerateStandardProjectJson(string projId, string projName, string folderUri)
        {
            string escUri = LocalWorkspaceManager.ToFileUri(folderUri);

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
                "    \"fileAccessPolicy\": \"AGENT_SETTING_POLICY_ALLOW\",\r\n" +
                "    \"sandboxMode\": false,\r\n" +
                "    \"autoExecutionPolicy\": \"CASCADE_COMMANDS_AUTO_EXECUTION_EAGER\",\r\n" +
                "    \"artifactReviewMode\": \"ARTIFACT_REVIEW_MODE_TURBO\"\r\n" +
                "  }},\r\n" +
                "  \"updatedAt\": \"{3}\",\r\n" +
                "  \"isWorkspaceOnly\": false\r\n" +
                "}}",
                projId, projName, escUri, nowIso
            );
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

        private void CollectSelectedItems(TreeNodeCollection nodes, List<ConversationSummaryInfo> convList, HashSet<string> projSet)
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
                }
                if (n.Nodes.Count > 0)
                {
                    CollectSelectedItems(n.Nodes, convList, projSet);
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
            string gemini = _geminiPath;
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
                            if (SessionMetadataExtractor.IsLikelyConversationId(s.ConversationId))
                            {
                                validCids.Add(s.ConversationId.Trim());
                            }
                        }
                    }

                    string convDir = Path.Combine(gemini, "antigravity", "conversations");
                    string brainDir = Path.Combine(gemini, "antigravity", "brain");

                    foreach (string cid in validCids)
                    {
                        try
                        {
                            string dbFile = Path.Combine(convDir, cid + ".db");
                            n += AddFileToArchive(archive, dbFile, "data/antigravity/conversations/" + cid + ".db");
                            string walFile = Path.Combine(convDir, cid + ".db-wal");
                            n += AddFileToArchive(archive, walFile, "data/antigravity/conversations/" + cid + ".db-wal");
                            string shmFile = Path.Combine(convDir, cid + ".db-shm");
                            n += AddFileToArchive(archive, shmFile, "data/antigravity/conversations/" + cid + ".db-shm");
                            string bDir = Path.Combine(brainDir, cid);
                            n += AddDirToArchive(archive, bDir, "data/antigravity/brain/" + cid);
                        }
                        catch {}
                    }

                    n += AddFileToArchive(archive, Path.Combine(gemini, "antigravity", "agyhub_summaries_proto.pb"), "data/antigravity/agyhub_summaries_proto.pb");

                    // 3. 用户全局规则、项目配置、自定义技能/插件与 MCP 配置 (严格排除 builtin/skills 与本机 installation_id)
                    progressDlg.UpdateMsg("正在归档项目配置、用户规则与 MCP 配置...");
                    n += AddFileToArchive(archive, Path.Combine(gemini, ".geminirules"), "data/.geminirules");
                    n += AddFileToArchive(archive, Path.Combine(gemini, "GEMINI.md"), "data/GEMINI.md");
                    n += AddFileToArchive(archive, Path.Combine(gemini, "AGENTS.md"), "data/AGENTS.md");
                    n += AddDirToArchive(archive, Path.Combine(gemini, "config"), "data/config");
                    n += AddDirToArchive(archive, Path.Combine(gemini, "antigravity", "mcp"), "data/antigravity/mcp");
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
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return 0;
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
            try
            {
                if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath)) return 0;
                int count = 0;
                string[] files = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    string f = files[i];
                    string rel = f.Substring(dirPath.Length).TrimStart('\\', '/');
                    string entryName = baseEntryDir.TrimEnd('/') + "/" + rel.Replace('\\', '/');
                    count += AddFileToArchive(archive, f, entryName);
                }
                return count;
            }
            catch { return 0; }
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

        // ══════════════════════════════════════════════════════════════════
        // F1 ~ F4 本地工作区治理与 Markdown 导出交互实现
        // ══════════════════════════════════════════════════════════════════
        private void BuildContextMenus()
        {
            ctxLocal = new ContextMenuStrip { Font = new Font("Segoe UI", 9f) };

            ToolStripMenuItem miRename = new ToolStripMenuItem("重命名此会话 (F2)...");
            miRename.Click += (s, e) => ActionRenameLocalSession();

            ToolStripMenuItem miMove = new ToolStripMenuItem("移动所选会话到其他工程...");
            miMove.Click += (s, e) => ActionMoveLocalSessionsToProject();

            ToolStripMenuItem miRelocate = new ToolStripMenuItem("重定向此工程本地路径 / 修复工程注册...");
            miRelocate.Click += (s, e) => ActionRelocateLocalProjectPath();

            ToolStripMenuItem miExportMd = new ToolStripMenuItem("导出此会话对话记录为 Markdown (.md)...");
            miExportMd.Click += (s, e) => ActionExportLocalSessionMarkdown();

            ToolStripMenuItem miExportAgy = new ToolStripMenuItem("导出所选工程/会话为轻量备份包 (.agy)...");
            miExportAgy.Click += (s, e) => ActionExportSelectedLocalToAgy();

            ToolStripMenuItem miSelectAllLocal = new ToolStripMenuItem("全选本地项");
            miSelectAllLocal.Click += (s, e) => SetAllLocalChecks(true);

            ToolStripMenuItem miDeselectAllLocal = new ToolStripMenuItem("全不选本地项");
            miDeselectAllLocal.Click += (s, e) => SetAllLocalChecks(false);

            ToolStripMenuItem miDelete = new ToolStripMenuItem("彻底删除所选工程/会话 (Delete)...");
            miDelete.ForeColor = Color.FromArgb(185, 28, 28);
            miDelete.Click += (s, e) => ActionDeleteSelectedLocal();

            ToolStripMenuItem miOrphanGc = new ToolStripMenuItem("扫描并清理孤儿数据库与脑区 (GC)...");
            miOrphanGc.Click += (s, e) => ActionRunOrphanGarbageCollector();

            ctxLocal.Items.AddRange(new ToolStripItem[] {
                miRename, miMove, miRelocate,
                new ToolStripSeparator(),
                miExportMd, miExportAgy,
                new ToolStripSeparator(),
                miSelectAllLocal, miDeselectAllLocal,
                new ToolStripSeparator(),
                miDelete, miOrphanGc
            });

            ctxBackup = new ContextMenuStrip { Font = new Font("Segoe UI", 9f) };

            ToolStripMenuItem miBackupExportMd = new ToolStripMenuItem("直接预览/导出此备份会话为 Markdown (.md) [免导入]...");
            miBackupExportMd.Click += (s, e) => ActionExportBackupSessionMarkdown();

            ToolStripMenuItem miBackupSelectNew = new ToolStripMenuItem("仅选新增与更新项");
            miBackupSelectNew.Click += BtnSelectNewOnly_Click;

            ToolStripMenuItem miBackupSelectAll = new ToolStripMenuItem("全选备份项");
            miBackupSelectAll.Click += (s, e) => SetAllBackupChecks(true);

            ToolStripMenuItem miBackupDeselectAll = new ToolStripMenuItem("全不选备份项");
            miBackupDeselectAll.Click += (s, e) => SetAllBackupChecks(false);

            ctxBackup.Items.AddRange(new ToolStripItem[] {
                miBackupExportMd,
                new ToolStripSeparator(),
                miBackupSelectNew, miBackupSelectAll, miBackupDeselectAll
            });
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
        }

        private void SyncLocalCheckedRecursive(TreeNodeCollection nodes)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode n = nodes[i];
                DiffItemNodeTag tag = n.Tag as DiffItemNodeTag;
                if (tag != null)
                {
                    if (tag.Category == ItemCategory.Conversation && !string.IsNullOrEmpty(tag.Id))
                    {
                        if (n.Checked) _checkedLocalSessionIds.Add(tag.Id);
                        else _checkedLocalSessionIds.Remove(tag.Id);
                    }
                    else if (tag.Category == ItemCategory.Project && !string.IsNullOrEmpty(tag.ProjectName))
                    {
                        if (n.Checked) _checkedLocalProjectNames.Add(tag.ProjectName);
                        else _checkedLocalProjectNames.Remove(tag.ProjectName);
                    }
                }
                if (n.Nodes.Count > 0)
                {
                    SyncLocalCheckedRecursive(n.Nodes);
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
                SyncLocalCheckedRecursive(tvLocal.Nodes);
            }
            finally
            {
                _isUpdatingChecks = false;
            }
        }

        private void TvLocal_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                e.Handled = true;
                ActionDeleteSelectedLocal();
            }
            else if (e.KeyCode == Keys.F2)
            {
                e.Handled = true;
                ActionRenameLocalSession();
            }
        }

        /// <summary>
        /// 获取本地树的目标选中项：优先读取复选框勾选集；若未勾选任何复选框，则自动回退至当前高亮选中节点 (SelectedNode)
        /// </summary>
        private void GetTargetLocalSelection(
            out List<ConversationSummaryInfo> targetConvs,
            out HashSet<string> targetConvIds,
            out HashSet<string> targetWholeProjects)
        {
            targetConvs = new List<ConversationSummaryInfo>();
            targetConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            targetWholeProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < tvLocal.Nodes.Count; i++)
            {
                TreeNode pNode = tvLocal.Nodes[i];
                DiffItemNodeTag pTag = pNode.Tag as DiffItemNodeTag;
                string pName = pTag != null ? pTag.ProjectName : null;

                int checkedChildren = 0;
                for (int j = 0; j < pNode.Nodes.Count; j++)
                {
                    TreeNode sNode = pNode.Nodes[j];
                    if (sNode.Checked)
                    {
                        checkedChildren++;
                        DiffItemNodeTag sTag = sNode.Tag as DiffItemNodeTag;
                        if (sTag != null && sTag.SummaryInfo != null)
                        {
                            targetConvs.Add(sTag.SummaryInfo);
                            targetConvIds.Add(sTag.SummaryInfo.ConversationId);
                        }
                    }
                }

                // 若工程节点本身被勾选且其下所有可见子会话均被勾选，视为整工程选中
                if (pNode.Checked && !string.IsNullOrEmpty(pName) &&
                    (pNode.Nodes.Count == 0 || checkedChildren == pNode.Nodes.Count))
                {
                    targetWholeProjects.Add(pName);
                }
            }

            // 若未勾选任何复选框，则回退使用当前鼠标选中节点 (SelectedNode)
            if (targetConvIds.Count == 0 && targetWholeProjects.Count == 0 && tvLocal.SelectedNode != null)
            {
                TreeNode sel = tvLocal.SelectedNode;
                DiffItemNodeTag tag = sel.Tag as DiffItemNodeTag;
                if (tag != null)
                {
                    if (tag.Category == ItemCategory.Project && !string.IsNullOrEmpty(tag.ProjectName))
                    {
                        targetWholeProjects.Add(tag.ProjectName);
                        for (int i = 0; i < _localSummaries.Count; i++)
                        {
                            ConversationSummaryInfo s = _localSummaries[i];
                            if (string.Equals(s.ProjectName, tag.ProjectName, StringComparison.OrdinalIgnoreCase))
                            {
                                targetConvs.Add(s);
                                targetConvIds.Add(s.ConversationId);
                            }
                        }
                    }
                    else if (tag.Category == ItemCategory.Conversation && tag.SummaryInfo != null)
                    {
                        targetConvs.Add(tag.SummaryInfo);
                        targetConvIds.Add(tag.SummaryInfo.ConversationId);
                    }
                }
            }
        }

        // ── F1-A: 级联物理删除所选本地会话或工程 ──
        private void ActionDeleteSelectedLocal()
        {
            List<ConversationSummaryInfo> targetConvs;
            HashSet<string> targetConvIds;
            HashSet<string> targetWholeProjects;
            GetTargetLocalSelection(out targetConvs, out targetConvIds, out targetWholeProjects);

            if (targetConvIds.Count == 0 && targetWholeProjects.Count == 0)
            {
                MessageBox.Show("请先在左侧「本地环境」树中勾选或点击选中要删除的工程或会话！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<string> projList = new List<string>(targetWholeProjects);
            string targetDesc = targetWholeProjects.Count > 0
                ? string.Format("{0} 个完整工程（[{1}]）及所选的 {2} 条主会话", targetWholeProjects.Count, string.Join(", ", projList.ToArray()), targetConvIds.Count)
                : string.Format("{0} 条会话", targetConvIds.Count);

            DialogResult confirm = MessageBox.Show(
                string.Format("确定要从本地环境中彻底物理删除以下内容吗？\n\n目标：{0}\n\n此操作将同时清理：\n1. agyhub_summaries_proto.pb 索引条目（自动备份 .bak）\n2. 关联的所有隐藏 Subagent 子会话\n3. conversations/<uuid>.db 数据库文件\n4. brain/<uuid>/ 脑区目录及所选整工程的 config/projects/<id>.json", targetDesc),
                "确认彻底物理删除 (F1)",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;
            if (!EnsureAntigravityClosed(true)) return;

            try
            {
                DeletionResult res = LocalWorkspaceManager.DeleteSessionsAndProjects(
                    _geminiPath, _localSummaries, _localProjects, targetConvIds, targetWholeProjects
                );

                _checkedLocalSessionIds.Clear();
                _checkedLocalProjectNames.Clear();

                Log(string.Format("物理删除完成：共移除 {0} 条会话（含子代理），删除 {1} 个 .db 文件、{2} 个脑区目录、{3} 个工程配置，释放 {4:F2} MB 空间",
                    res.DeletedSessionCount, res.DeletedDbFilesCount, res.DeletedBrainDirsCount, res.DeletedProjectJsonCount, res.FreedMegabytes),
                    Color.FromArgb(22, 101, 52));

                MessageBox.Show(
                    string.Format("彻底删除成功！\n\n• 清理会话总数（含子代理）：{0} 条\n• 删除数据库文件：{1} 个\n• 删除脑区文件夹：{2} 个\n• 移除工程配置文件：{3} 个\n• 释放磁盘空间：{4:F2} MB",
                        res.DeletedSessionCount, res.DeletedDbFilesCount, res.DeletedBrainDirsCount, res.DeletedProjectJsonCount, res.FreedMegabytes),
                    "删除完成", MessageBoxButtons.OK, MessageBoxIcon.Information);

                DetectPaths();
                if (_backupIndex != null)
                {
                    _currentDiffNodes = VisualDiffEngine.ComputeDiff(_localSummaries, _backupIndex.Conversations);
                    RenderBackupTree();
                }
            }
            catch (Exception ex)
            {
                Log("删除操作异常: " + ex.Message, Color.FromArgb(220, 38, 38));
                MessageBox.Show("删除失败:\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── F1-B: 扫描并一键清理孤儿数据库与残余脑区 (Orphan GC) ──
        private void ActionRunOrphanGarbageCollector()
        {
            if (string.IsNullOrEmpty(_geminiPath) || !Directory.Exists(_geminiPath))
            {
                MessageBox.Show("未检测到有效的本地 .gemini 目录！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            OrphanScanResult scan = LocalWorkspaceManager.ScanOrphanedData(_geminiPath);
            if (scan.TotalOrphanItems == 0)
            {
                Log("孤儿垃圾扫描完成：本地 conversations/ 与 brain/ 非常干净，无任何孤儿残留。", Color.FromArgb(22, 101, 52));
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
            if (!EnsureAntigravityClosed(true)) return;

            DeletionResult res = LocalWorkspaceManager.CleanOrphanedData(scan);
            Log(string.Format("孤儿垃圾清理完成：清理 {0} 个数据库文件，{1} 个脑区目录，释放 {2:F2} MB",
                res.DeletedDbFilesCount, res.DeletedBrainDirsCount, res.FreedMegabytes), Color.FromArgb(22, 101, 52));

            MessageBox.Show(
                string.Format("孤儿垃圾清理完成！\n\n• 清理孤儿数据库文件：{0} 个\n• 清理孤儿脑区目录：{1} 个\n• 实际释放磁盘空间：{2:F2} MB",
                    res.DeletedDbFilesCount, res.DeletedBrainDirsCount, res.FreedMegabytes),
                "清理完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ── F2-A: 本地会话重命名 (双向固化 Field 1 & Field 15) ──
        private void ActionRenameLocalSession()
        {
            TreeNode sel = tvLocal.SelectedNode;
            DiffItemNodeTag tag = sel != null ? (sel.Tag as DiffItemNodeTag) : null;
            if (tag == null || tag.Category != ItemCategory.Conversation || tag.SummaryInfo == null)
            {
                MessageBox.Show("请先在左侧树中点击选中一条需要重命名的会话！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ConversationSummaryInfo info = tag.SummaryInfo;
            using (TextInputDialog dlg = new TextInputDialog("重命名本地会话 (F2)", "请输入新的会话自定义标题（将双向固化至 Field 1 与 Field 15）：", info.DisplayTitle))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string newTitle = dlg.InputValue.Trim();
                    if (string.IsNullOrEmpty(newTitle) || string.Equals(newTitle, info.DisplayTitle, StringComparison.Ordinal))
                        return;

                    if (!EnsureAntigravityClosed(true)) return;

                    if (LocalWorkspaceManager.RenameSession(_geminiPath, info.ConversationId, newTitle))
                    {
                        _highlightTracker.AddImportedConversation(info.ConversationId);
                        Log(string.Format("会话 [{0}] 已重命名为: {1}", info.ConversationId, newTitle), Color.FromArgb(22, 101, 52));
                        DetectPaths();
                        if (_backupIndex != null)
                        {
                            _currentDiffNodes = VisualDiffEngine.ComputeDiff(_localSummaries, _backupIndex.Conversations);
                            RenderBackupTree();
                        }
                    }
                    else
                    {
                        MessageBox.Show("未能在 agyhub_summaries_proto.pb 中找到该会话节点。", "重命名失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        // ── F2-B: 会话跨工程迁移与归属重绑定 ──
        private void ActionMoveLocalSessionsToProject()
        {
            List<ConversationSummaryInfo> targetConvs;
            HashSet<string> targetConvIds;
            HashSet<string> targetWholeProjects;
            GetTargetLocalSelection(out targetConvs, out targetConvIds, out targetWholeProjects);

            if (targetConvIds.Count == 0)
            {
                MessageBox.Show("请先在左侧树中勾选或选中要迁移归属的会话！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
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
                        _checkedLocalSessionIds.Clear();
                        _checkedLocalProjectNames.Clear();
                        _highlightTracker.AddImportedProject(dlg.SelectedProjectName);
                        for (int i = 0; i < cidList.Count; i++)
                            _highlightTracker.AddImportedConversation(cidList[i]);

                        Log(string.Format("成功将 {0} 条会话（含子代理）迁移绑定至工程 [{1}]", moved, dlg.SelectedProjectName), Color.FromArgb(22, 101, 52));
                        MessageBox.Show(string.Format("已成功将 {0} 条会话迁移至工程 [{1}]，并同步更新了 Proto 索引与 SQLite 数据库！", moved, dlg.SelectedProjectName),
                            "迁移完成", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        DetectPaths();
                        if (_backupIndex != null)
                        {
                            _currentDiffNodes = VisualDiffEngine.ComputeDiff(_localSummaries, _backupIndex.Conversations);
                            RenderBackupTree();
                        }
                    }
                }
            }
        }

        // ── F2-C: 工程本地物理路径重定向与幽灵工程注册修复 ──
        private void ActionRelocateLocalProjectPath()
        {
            TreeNode sel = tvLocal.SelectedNode;
            DiffItemNodeTag tag = sel != null ? (sel.Tag as DiffItemNodeTag) : null;
            if (tag == null)
            {
                MessageBox.Show("请先在左侧树中点击选中一个工程（或其下任意会话）！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string projName = tag.ProjectName;
            if (string.IsNullOrEmpty(projName) || string.Equals(projName, "Outside of Project", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("「Outside of Project」为全局无工程容器，无需绑定固定物理目录。若要将其下会话移入具体工程，请使用「移动所选会话到其他工程...」。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string existingProjId = null;
            string currentUri = null;
            foreach (KeyValuePair<string, ProjectMetadata> kvp in _localProjects)
            {
                if (string.Equals(kvp.Value.Name, projName, StringComparison.OrdinalIgnoreCase))
                {
                    existingProjId = kvp.Key;
                    currentUri = kvp.Value.FolderUri;
                    break;
                }
            }

            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = string.Format("请为工程 [{0}] 选择新的本地工作区物理文件夹（将同步更新 config/projects/<id>.json、.pb 索引及所有关联 .db 数据库）：", projName);
                if (!string.IsNullOrEmpty(currentUri))
                {
                    string decoded = DirectStreamArchiver.UnescapeUriString(currentUri);
                    if (decoded.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                        decoded = decoded.Substring(8).Replace('/', '\\');
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
                    Log(string.Format("工程 [{0}] 路径已重定向至 {1}（同步更新 {2} 条会话）", projName, fbd.SelectedPath, updated), Color.FromArgb(22, 101, 52));
                    MessageBox.Show(
                        string.Format("工程 [{0}] 物理路径重定向与注册修复完成！\n\n• 新工作区路径：{1}\n• 同步更新会话数：{2} 条", projName, fbd.SelectedPath, updated),
                        "工程路径重定向完成", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    DetectPaths();
                }
            }
        }

        // ── F3: 细粒度选择性轻量备份导出 (.agy) ──
        private void ActionExportSelectedLocalToAgy()
        {
            List<ConversationSummaryInfo> targetConvs;
            HashSet<string> targetConvIds;
            HashSet<string> targetWholeProjects;
            GetTargetLocalSelection(out targetConvs, out targetConvIds, out targetWholeProjects);

            if (targetConvs.Count == 0)
            {
                MessageBox.Show("请先在左侧「本地环境」树中勾选或选中要导出的工程或会话！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult incGlobal = MessageBox.Show(
                string.Format("即将导出所选的 {0} 条会话为轻量级 .agy 备份包。\n\n是否在备份包中同时附带本机全局规则 (.geminirules / GEMINI.md)、插件与 MCP 配置？\n（点击「是」附带全局配置，点击「否」仅导出纯净会话与所属工程）", targetConvs.Count),
                "选择性轻量备份导出 (F3)",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (incGlobal == DialogResult.Cancel) return;
            bool includeGlobal = (incGlobal == DialogResult.Yes);

            SaveFileDialog dlg = new SaveFileDialog
            {
                Filter = "Antigravity 备份包 (*.agy)|*.agy|ZIP 压缩包 (*.zip)|*.zip",
                FileName = string.Format("antigravity_selective_{0}sessions_{1}.agy", targetConvs.Count, DateTime.Now.ToString("yyyyMMdd_HHmmss")),
                Title = "保存选择性轻量备份包 (.agy)"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                this.Cursor = Cursors.WaitCursor;
                int count = LocalWorkspaceManager.ExportSelectiveBackup(
                    _geminiPath, HomePath, dlg.FileName, targetConvs, _localProjects, includeGlobal
                );
                Log(string.Format("选择性轻量备份导出成功：共打包 {0} 条会话（{1} 个归档条目）→ {2}", targetConvs.Count, count, dlg.FileName), Color.FromArgb(22, 101, 52));
                MessageBox.Show(
                    string.Format("选择性轻量备份包导出成功！\n\n• 导出会话数：{0} 条\n• 包含归档条目：{1} 个\n• 保存路径：{2}", targetConvs.Count, count, dlg.FileName),
                    "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("选择性导出失败: " + ex.Message, Color.FromArgb(220, 38, 38));
                MessageBox.Show("选择性导出失败:\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        // ── F4-A: 导出本地会话对话记录与 Artifacts 为 Markdown (.md) ──
        private void ActionExportLocalSessionMarkdown()
        {
            TreeNode sel = tvLocal.SelectedNode;
            DiffItemNodeTag tag = sel != null ? (sel.Tag as DiffItemNodeTag) : null;
            if (tag == null || tag.Category != ItemCategory.Conversation || tag.SummaryInfo == null)
            {
                MessageBox.Show("请先在左侧树中点击选中一条要导出为 Markdown 的会话！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ConversationSummaryInfo info = tag.SummaryInfo;
            string safeTitle = SanitizeFileName(info.DisplayTitle);
            SaveFileDialog dlg = new SaveFileDialog
            {
                Filter = "Markdown 文档 (*.md)|*.md|所有文件 (*.*)|*.*",
                FileName = string.Format("{0}_{1}.md", safeTitle, info.ConversationId.Length >= 8 ? info.ConversationId.Substring(0, 8) : info.ConversationId),
                Title = "导出本地会话对话记录为 Markdown (.md)"
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    TranscriptMarkdownExporter.ExportLocalSessionToMarkdown(info, _geminiPath, dlg.FileName);
                    Log(string.Format("已将本地会话 [{0}] 导出为 Markdown: {1}", info.DisplayTitle, dlg.FileName), Color.FromArgb(22, 101, 52));
                    MessageBox.Show("会话 Markdown 导出成功！\n路径: " + dlg.FileName, "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Log("导出 Markdown 失败: " + ex.Message, Color.FromArgb(220, 38, 38));
                    MessageBox.Show("导出 Markdown 失败:\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ── F4-B: 免导入直接从右侧备份包流式导出会话为 Markdown (.md) ──
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

            ConversationSummaryInfo info = tag.SummaryInfo;
            string safeTitle = SanitizeFileName(info.DisplayTitle);
            SaveFileDialog dlg = new SaveFileDialog
            {
                Filter = "Markdown 文档 (*.md)|*.md|所有文件 (*.*)|*.*",
                FileName = string.Format("backup_{0}_{1}.md", safeTitle, info.ConversationId.Length >= 8 ? info.ConversationId.Substring(0, 8) : info.ConversationId),
                Title = "从备份包直接流式导出会话为 Markdown (.md)"
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    TranscriptMarkdownExporter.ExportBackupSessionToMarkdown(info, _loadedBackupPath, dlg.FileName);
                    Log(string.Format("已从备份包免导入流式导出会话 [{0}] 为 Markdown: {1}", info.DisplayTitle, dlg.FileName), Color.FromArgb(22, 101, 52));
                    MessageBox.Show("已从备份归档包直接流式提取并导出为 Markdown！\n路径: " + dlg.FileName, "免导入导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Log("备份会话导出 Markdown 失败: " + ex.Message, Color.FromArgb(220, 38, 38));
                    MessageBox.Show("备份会话导出 Markdown 失败:\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
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

    /// <summary>
    /// 通用单行文本输入对话框 (用于 F2 会话重命名)
    /// </summary>
    public class TextInputDialog : Form
    {
        private TextBox txtInput;
        public string InputValue
        {
            get { return txtInput != null ? txtInput.Text : string.Empty; }
        }

        public TextInputDialog(string title, string prompt, string defaultValue)
        {
            this.Text = title;
            this.Size = new Size(480, 175);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9f);
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
                Font = new Font("Segoe UI", 9.5f)
            };

            Button btnOk = new Button
            {
                Text = "确定",
                DialogResult = DialogResult.OK,
                Location = new Point(276, 86),
                Size = new Size(80, 30),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;

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
        }
    }

    /// <summary>
    /// 会话跨工程迁移选择对话框 (用于 F2 移动会话至现有或新建工程)
    /// </summary>
    public class MoveToProjectDialog : Form
    {
        private ComboBox cmbProjects;
        private TextBox txtFolderPath;
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
                if (string.Equals(name, "Outside of Project", StringComparison.OrdinalIgnoreCase))
                    return "outside-of-project";
                ProjectMetadata meta;
                if (_nameToProject.TryGetValue(name, out meta) && !string.IsNullOrEmpty(meta.Id))
                    return meta.Id;
                return Guid.NewGuid().ToString();
            }
        }

        public string SelectedFolderUri
        {
            get
            {
                string name = SelectedProjectName;
                if (string.Equals(name, "Outside of Project", StringComparison.OrdinalIgnoreCase))
                    return string.Empty;
                return LocalWorkspaceManager.ToFileUri(txtFolderPath != null ? txtFolderPath.Text.Trim() : string.Empty);
            }
        }

        public MoveToProjectDialog(Dictionary<string, ProjectMetadata> localProjects, List<ConversationSummaryInfo> localSummaries, string homePath)
        {
            this.Text = "移动所选会话到其他工程 (F2)";
            this.Size = new Size(520, 240);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9f);
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
                Text = "目标工程工作区路径 (Folder URI / 物理目录)：",
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
                        txtFolderPath.Text = fbd.SelectedPath;
                        if (string.IsNullOrWhiteSpace(cmbProjects.Text))
                        {
                            cmbProjects.Text = Path.GetFileName(fbd.SelectedPath.TrimEnd('\\', '/'));
                        }
                    }
                }
            };

            cmbProjects.SelectedIndexChanged += (s, e) =>
            {
                string selName = cmbProjects.Text.Trim();
                if (string.Equals(selName, "Outside of Project", StringComparison.OrdinalIgnoreCase))
                {
                    txtFolderPath.Text = string.Empty;
                    txtFolderPath.Enabled = false;
                }
                else
                {
                    txtFolderPath.Enabled = true;
                    ProjectMetadata m;
                    if (_nameToProject.TryGetValue(selName, out m) && !string.IsNullOrEmpty(m.FolderUri))
                    {
                        txtFolderPath.Text = m.FolderUri;
                    }
                    else if (!string.IsNullOrEmpty(homePath))
                    {
                        txtFolderPath.Text = Path.Combine(homePath, selName);
                    }
                }
            };

            if (cmbProjects.Items.Count > 0)
                cmbProjects.SelectedIndex = 0;

            Button btnOk = new Button
            {
                Text = "确定迁移",
                DialogResult = DialogResult.OK,
                Location = new Point(306, 148),
                Size = new Size(86, 32),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;

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
