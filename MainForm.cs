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

    public class MainForm : Form
    {
        // ── 顶部日志 ──
        private RichTextBox rtbLog;

        // ── 左侧控件 ──
        private TextBox txtGeminiPath;
        private CheckBox chkConversations, chkRules, chkSkills, chkMcp;
        private TextBox txtBackupPath, txtRestorePath;
        private Button btnCreateBackup, btnRestoreBackup;

        // ── 配置浏览器 ──
        private Button btnCatRules, btnCatSkills, btnCatMcp;
        private ListBox lstFiles;
        private RichTextBox rtbEditor;
        private Label lblCurrentFile;

        // ── 状态栏 ──
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatus, lblGeminiFound, lblConvCount;

        private string _currentEditPath = "";
        private string _currentCategory = "rules";
        private BackgroundWorker _worker;

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
        // UI 构建（包含快捷键支持）
        // ══════════════════════════════════════════════════════════
        private void BuildUI()
        {
            this.Text = "Antigravity Companion V0.4.1  数据迁移与管理工具";
            this.Size = new Size(970, 700);
            this.MinimumSize = new Size(860, 560);
            this.Font = new Font("Microsoft YaHei UI", 8.5f);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;

            // ══ 状态栏 ══
            statusStrip = new StatusStrip();
            lblStatus      = new ToolStripStatusLabel("就绪") { BorderSides = ToolStripStatusLabelBorderSides.Right };
            lblGeminiFound = new ToolStripStatusLabel(".gemini: 未检测") { ForeColor = Color.DarkRed, BorderSides = ToolStripStatusLabelBorderSides.Right };
            lblConvCount   = new ToolStripStatusLabel("对话数: -") { BorderSides = ToolStripStatusLabelBorderSides.Right };
            var lblHome    = new ToolStripStatusLabel("主目录: " + HomePath) { Spring = true, TextAlign = ContentAlignment.MiddleRight };
            statusStrip.Items.AddRange(new ToolStripItem[] { lblStatus, lblGeminiFound, lblConvCount, lblHome });
            this.Controls.Add(statusStrip);

            // ══ 根 TableLayoutPanel ══
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.RowCount = 2;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 160f)); // 日志区高度 160f
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // 主工作区
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.Margin = new Padding(0);
            root.Padding = new Padding(0);
            this.Controls.Add(root);

            // ── Row 0: 日志区 ──
            Panel logPanel = BuildLogPanel();
            root.Controls.Add(logPanel, 0, 0);

            // ── Row 1: 主工作区（SplitContainer） ──
            SplitContainer outerSplit = new SplitContainer();
            outerSplit.Dock = DockStyle.Fill;
            outerSplit.Orientation = Orientation.Vertical;
            outerSplit.Panel1MinSize = 250;
            outerSplit.Margin = new Padding(0);
            root.Controls.Add(outerSplit, 0, 1);

            // 左侧：备份/还原控制面板
            BuildLeftPanel(outerSplit.Panel1);

            // 右侧：配置浏览器
            TabControl tabRight = new TabControl();
            tabRight.Dock = DockStyle.Fill;
            tabRight.Padding = new System.Drawing.Point(6, 3);
            TabPage tabBrowser = new TabPage("配置浏览器  [规则 / 技能 / MCP]");
            BuildBrowserPanel(tabBrowser);
            tabRight.TabPages.Add(tabBrowser);
            outerSplit.Panel2.Controls.Add(tabRight);

            this.Load += (s, e) => {
                try { outerSplit.SplitterDistance = 286; } catch {}
            };
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl + S 保存
            if (e.Control && e.KeyCode == Keys.S)
            {
                e.SuppressKeyPress = true; // 阻止发出提示音
                BtnSaveFile_Click(this, EventArgs.Empty);
            }
        }

        // ══════════════════════════════════════════════════════════
        // 日志区
        // ══════════════════════════════════════════════════════════
        private Panel BuildLogPanel()
        {
            TableLayoutPanel tbl = new TableLayoutPanel();
            tbl.Dock = DockStyle.Fill;
            tbl.RowCount = 2;
            tbl.ColumnCount = 1;
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tbl.Margin = new Padding(0);
            tbl.Padding = new Padding(4, 4, 4, 0);

            rtbLog = new RichTextBox();
            rtbLog.Dock = DockStyle.Fill;
            rtbLog.ReadOnly = true;
            rtbLog.BackColor = Color.White;
            rtbLog.Font = new Font("Courier New", 8.5f);
            rtbLog.ScrollBars = RichTextBoxScrollBars.Vertical;
            rtbLog.BorderStyle = BorderStyle.Fixed3D;
            rtbLog.Margin = new Padding(0);
            tbl.Controls.Add(rtbLog, 0, 0);

            Panel btnRow = new Panel();
            btnRow.Dock = DockStyle.Fill;
            btnRow.Margin = new Padding(0);
            var btnClear = MakeBtn("清除窗口", 72, Color.LightCoral); btnClear.Location = new Point(0, 3);
            btnClear.Click += (s, e) => rtbLog.Clear();
            var btnSaveLog = MakeBtn("保存日志", 72, Color.LightGreen); btnSaveLog.Location = new Point(76, 3);
            btnSaveLog.Click += BtnSaveLog_Click;
            btnRow.Controls.AddRange(new Control[] { btnClear, btnSaveLog });
            tbl.Controls.Add(btnRow, 0, 1);

            Panel wrapper = new Panel();
            wrapper.Dock = DockStyle.Fill;
            wrapper.Margin = new Padding(0);
            wrapper.Controls.Add(tbl);
            return wrapper;
        }

        // ══════════════════════════════════════════════════════════
        // 左侧：备份/还原控制面板
        // ══════════════════════════════════════════════════════════
        private void BuildLeftPanel(SplitterPanel parent)
        {
            TableLayoutPanel tbl = new TableLayoutPanel();
            tbl.Dock = DockStyle.Fill;
            tbl.ColumnCount = 1;
            tbl.RowCount = 5;
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 78f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 108f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 88f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 88f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tbl.Margin = new Padding(0);
            tbl.Padding = new Padding(4, 4, 2, 4);

            // ── 路径检测 GroupBox ──
            GroupBox grpPath = new GroupBox();
            grpPath.Text = "数据路径检测";
            grpPath.Dock = DockStyle.Fill;
            grpPath.Margin = new Padding(0, 0, 0, 4);
            grpPath.Padding = new Padding(6, 2, 6, 4);

            TableLayoutPanel pathTbl = new TableLayoutPanel();
            pathTbl.Dock = DockStyle.Fill;
            pathTbl.RowCount = 2; pathTbl.ColumnCount = 2;
            pathTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));
            pathTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
            pathTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            pathTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56f));
            pathTbl.Margin = new Padding(0); pathTbl.Padding = new Padding(0);

            var lblPath = new Label() { Text = ".gemini 路径:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
            txtGeminiPath = new TextBox() { Dock = DockStyle.Fill, Font = new Font("Courier New", 7.5f), Margin = new Padding(0, 2, 4, 0) };
            var btnDetect = MakeBtn("检测", 52, Color.FromArgb(198, 224, 180));
            btnDetect.Dock = DockStyle.Fill;
            btnDetect.Margin = new Padding(0, 2, 0, 0);
            btnDetect.Click += (s, e) => DetectPaths();

            pathTbl.Controls.Add(lblPath, 0, 0);
            pathTbl.SetColumnSpan(lblPath, 2);
            pathTbl.Controls.Add(txtGeminiPath, 0, 1);
            pathTbl.Controls.Add(btnDetect, 1, 1);
            grpPath.Controls.Add(pathTbl);
            tbl.Controls.Add(grpPath, 0, 0);

            // ── 备份内容 GroupBox ──
            GroupBox grpContent = new GroupBox();
            grpContent.Text = "备份内容";
            grpContent.Dock = DockStyle.Fill;
            grpContent.Margin = new Padding(0, 0, 0, 4);

            chkConversations = new CheckBox() { Text = "历史对话 (conversations + brain + 索引)", Left = 8, Top = 18, Width = 260, Checked = true, Parent = grpContent };
            chkRules         = new CheckBox() { Text = "全局规则 (.geminirules + GEMINI.md)", Left = 8, Top = 38, Width = 260, Checked = true, Parent = grpContent };
            chkSkills        = new CheckBox() { Text = "技能 (builtin + 用户自定义)", Left = 8, Top = 58, Width = 260, Checked = true, Parent = grpContent };
            chkMcp           = new CheckBox() { Text = "MCP 服务器与配置 (config + mcp)", Left = 8, Top = 78, Width = 260, Checked = true, Parent = grpContent };
            tbl.Controls.Add(grpContent, 0, 1);

            // ── 备份导出 GroupBox ──
            GroupBox grpBackup = new GroupBox();
            grpBackup.Text = "备份导出 (高速直流压缩)";
            grpBackup.Dock = DockStyle.Fill;
            grpBackup.Margin = new Padding(0, 0, 0, 4);

            var lblBk = new Label() { Text = "导出:", Left = 6, Top = 22, Width = 36, Parent = grpBackup, TextAlign = ContentAlignment.MiddleLeft };
            txtBackupPath = new TextBox() { Left = 44, Top = 20, Width = 168, Parent = grpBackup, Font = new Font("Courier New", 7f), Text = "D:\\antigravity_backup.agy" };
            var btnBrBk = MakeBtn("...", 24, SystemColors.Control); btnBrBk.SetBounds(216, 18, 24, 22); btnBrBk.Parent = grpBackup;
            btnBrBk.Click += BtnBrowseBackup_Click;
            btnCreateBackup = MakeBtn("★ 一键导出备份", 240, Color.FromArgb(255, 217, 102));
            btnCreateBackup.SetBounds(6, 46, 240, 26); btnCreateBackup.Parent = grpBackup;
            btnCreateBackup.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
            btnCreateBackup.Click += BtnCreateBackup_Click;
            tbl.Controls.Add(grpBackup, 0, 2);

            // ── 还原导入 GroupBox ──
            GroupBox grpRestore = new GroupBox();
            grpRestore.Text = "还原导入 (智能重映射)";
            grpRestore.Dock = DockStyle.Fill;
            grpRestore.Margin = new Padding(0, 0, 0, 0);

            var lblRs = new Label() { Text = "文件:", Left = 6, Top = 22, Width = 36, Parent = grpRestore, TextAlign = ContentAlignment.MiddleLeft };
            txtRestorePath = new TextBox() { Left = 44, Top = 20, Width = 168, Parent = grpRestore, Font = new Font("Courier New", 7f), Text = "D:\\antigravity_backup.agy" };
            var btnBrRs = MakeBtn("...", 24, SystemColors.Control); btnBrRs.SetBounds(216, 18, 24, 22); btnBrRs.Parent = grpRestore;
            btnBrRs.Click += BtnBrowseRestore_Click;
            btnRestoreBackup = MakeBtn("★ 一键导入还原", 240, Color.FromArgb(155, 194, 230));
            btnRestoreBackup.SetBounds(6, 46, 240, 26); btnRestoreBackup.Parent = grpRestore;
            btnRestoreBackup.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
            btnRestoreBackup.Click += BtnRestoreBackup_Click;
            tbl.Controls.Add(grpRestore, 0, 3);

            tbl.Controls.Add(new Panel(), 0, 4);
            parent.Controls.Add(tbl);
        }

        // ══════════════════════════════════════════════════════════
        // 右侧：配置浏览器
        // ══════════════════════════════════════════════════════════
        private void BuildBrowserPanel(TabPage tab)
        {
            TableLayoutPanel outer = new TableLayoutPanel();
            outer.Dock = DockStyle.Fill;
            outer.RowCount = 1;
            outer.ColumnCount = 2;
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230f));  // 左侧文件列表栏
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            outer.Margin = new Padding(2);
            outer.Padding = new Padding(0);

            // ── 左列：分类按钮 + 文件列表 ──
            TableLayoutPanel leftTbl = new TableLayoutPanel();
            leftTbl.Dock = DockStyle.Fill;
            leftTbl.RowCount = 2;
            leftTbl.ColumnCount = 1;
            leftTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));   // 按钮行
            leftTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // 列表行
            leftTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            leftTbl.Margin = new Padding(0);
            leftTbl.Padding = new Padding(2);

            TableLayoutPanel btnRow = new TableLayoutPanel();
            btnRow.Dock = DockStyle.Fill;
            btnRow.RowCount = 1; btnRow.ColumnCount = 3;
            btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
            btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
            btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24f));
            btnRow.Margin = new Padding(0); btnRow.Padding = new Padding(0, 2, 0, 2);

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
            lstFiles.Margin = new Padding(0);
            lstFiles.DoubleClick += (s, e) => OpenSelectedFile();
            lstFiles.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) OpenSelectedFile(); };
            leftTbl.Controls.Add(lstFiles, 0, 1);

            outer.Controls.Add(leftTbl, 0, 0);

            // ── 右列：文件名标题栏 | 编辑器 ──
            TableLayoutPanel rightTbl = new TableLayoutPanel();
            rightTbl.Dock = DockStyle.Fill;
            rightTbl.RowCount = 2;
            rightTbl.ColumnCount = 1;
            rightTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));   // 文件名标签
            rightTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // 编辑器本身
            rightTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            rightTbl.Margin = new Padding(2, 0, 0, 0);
            rightTbl.Padding = new Padding(0, 2, 6, 6);

            lblCurrentFile = new Label();
            lblCurrentFile.Dock = DockStyle.Fill;
            lblCurrentFile.Text = "← 双击打开文件 (支持 Ctrl+S 保存)";
            lblCurrentFile.Font = new Font("Courier New", 8.5f, FontStyle.Bold);
            lblCurrentFile.ForeColor = Color.DarkBlue;
            lblCurrentFile.BackColor = Color.FromArgb(235, 244, 255);
            lblCurrentFile.TextAlign = ContentAlignment.MiddleLeft;
            lblCurrentFile.Padding = new Padding(6, 0, 0, 0);
            lblCurrentFile.BorderStyle = BorderStyle.FixedSingle;
            lblCurrentFile.Margin = new Padding(0, 0, 0, 2);
            rightTbl.Controls.Add(lblCurrentFile, 0, 0);

            rtbEditor = new RichTextBox();
            rtbEditor.Dock = DockStyle.Fill;
            rtbEditor.Font = new Font("Courier New", 9f);
            rtbEditor.ScrollBars = RichTextBoxScrollBars.Both;
            rtbEditor.BackColor = Color.FromArgb(253, 253, 253);
            rtbEditor.BorderStyle = BorderStyle.Fixed3D;
            rtbEditor.Margin = new Padding(0);
            rightTbl.Controls.Add(rtbEditor, 0, 1);

            outer.Controls.Add(rightTbl, 1, 0);
            tab.Controls.Add(outer);
        }

        // ══════════════════════════════════════════════════════════
        // 路径检测
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

                string convDir = Path.Combine(gemini, "antigravity", "conversations");
                if (Directory.Exists(convDir))
                {
                    int cnt = Directory.GetFiles(convDir, "*.db").Length;
                    lblConvCount.Text = "对话数: " + cnt;
                    Log("检测到历史对话: " + cnt + " 个", Color.DarkBlue);
                }
                _currentCategory = "rules";
                RefreshFileList();
            }
            else
            {
                lblGeminiFound.Text = ".gemini: ✗ 未找到";
                lblGeminiFound.ForeColor = Color.DarkRed;
                Log("警告: 未找到 .gemini 目录: " + gemini, Color.DarkOrange);
            }
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
                System.Threading.Thread.Sleep(500); // 确保 OS 句柄完全释放
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

            // 1. 获取全局和项目级路径
            List<ScanRoot> scanRoots = new List<ScanRoot>();
            scanRoots.Add(new ScanRoot(gemini, "全局"));

            // 扫描 .gemini/config/projects 下的项目配置 json，找到它们引用的物理项目路径
            string projectsConfigDir = Path.Combine(gemini, "config", "projects");
            if (Directory.Exists(projectsConfigDir))
            {
                foreach (string jsonFile in Directory.GetFiles(projectsConfigDir, "*.json"))
                {
                    try
                    {
                        string content = File.ReadAllText(jsonFile);
                        string projName = "";
                        int nameIdx = content.IndexOf("\"name\"");
                        if (nameIdx >= 0)
                        {
                            int colonIdx = content.IndexOf(":", nameIdx);
                            if (colonIdx >= 0)
                            {
                                int startQuote = content.IndexOf("\"", colonIdx);
                                if (startQuote >= 0)
                                {
                                    int endQuote = content.IndexOf("\"", startQuote + 1);
                                    if (endQuote > startQuote)
                                    {
                                        projName = content.Substring(startQuote + 1, endQuote - startQuote - 1);
                                    }
                                }
                            }
                        }

                        // 获取 JSON 中 "folderUri": "file:///..." 的值
                        int uriIdx = content.IndexOf("\"folderUri\"");
                        if (uriIdx >= 0)
                        {
                            int start = content.IndexOf("file:///", uriIdx);
                            if (start >= 0)
                            {
                                start += 8;
                                int end = content.IndexOf("\"", start);
                                if (end > start)
                                {
                                    string encodedPath = content.Substring(start, end - start);
                                    string decodedPath = Uri.UnescapeDataString(encodedPath);
                                    if (decodedPath.StartsWith("/") && decodedPath.Length > 2 && decodedPath[2] == ':')
                                        decodedPath = decodedPath.Substring(1);
                                    decodedPath = decodedPath.Replace('/', '\\');

                                    if (Directory.Exists(decodedPath))
                                    {
                                        if (string.IsNullOrEmpty(projName))
                                        {
                                            projName = Path.GetFileName(decodedPath);
                                        }
                                        scanRoots.Add(new ScanRoot(decodedPath, projName));
                                    }
                                }
                            }
                        }
                    }
                    catch {}
                }
            }

            // 2. 根据选定分类在所有扫描根中查找规则、技能和 MCP
            if (_currentCategory == "rules")
            {
                foreach (ScanRoot root in scanRoots)
                {
                    bool isGlobal = (root.Path == gemini);
                    if (isGlobal)
                    {
                        // 全局规则
                        string p = Path.Combine(root.Path, ".geminirules");
                        if (File.Exists(p)) { lstFiles.Items.Add(new BrowserItem("[全局] .geminirules", p)); found++; }
                        foreach (string f in Directory.GetFiles(root.Path, "*.geminirules"))
                            if (Path.GetFileName(f) != ".geminirules") { lstFiles.Items.Add(new BrowserItem("[全局] " + Path.GetFileName(f), f)); found++; }
                        
                        // 全局 AGENTS.md / GEMINI.md
                        string agentsMd = Path.Combine(root.Path, "AGENTS.md");
                        if (File.Exists(agentsMd)) { lstFiles.Items.Add(new BrowserItem("[全局] AGENTS.md", agentsMd)); found++; }
                        string geminiMd = Path.Combine(root.Path, "GEMINI.md");
                        if (File.Exists(geminiMd)) { lstFiles.Items.Add(new BrowserItem("[全局] GEMINI.md", geminiMd)); found++; }

                        // 全局 config/rules 下的规则
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
                        // 项目级规则
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
                        // 项目级 AGENTS.md / GEMINI.md
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
                        // 全局内置技能
                        string skillsDir = Path.Combine(root.Path, "antigravity", "builtin", "skills");
                        if (Directory.Exists(skillsDir))
                        {
                            foreach (string dir in Directory.GetDirectories(skillsDir))
                            {
                                string md = Path.Combine(dir, "SKILL.md");
                                if (File.Exists(md)) { lstFiles.Items.Add(new BrowserItem("[内置] ▣ " + Path.GetFileName(dir), md)); found++; }
                            }
                        }
                        // 用户自定义技能 (config\skills\)
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
                        // 项目级技能
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
                // MCP 配置
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

            Log("已扫描 [" + _currentCategory + "]，找到 " + found + " 项。", Color.DarkBlue);
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
            if (string.IsNullOrEmpty(_currentEditPath))
            { return; }
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
        // 高速直接流压缩：添加文件与目录进 ZipArchive（避免临时目录路径过长）
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
                    // 过滤掉临时分支工作区、崩溃缓存等超长或临时无用目录
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
        // 备份导出（Direct Stream to ZipArchive）
        // ══════════════════════════════════════════════════════════
        private void BtnCreateBackup_Click(object sender, EventArgs e)
        {
            string gemini = txtGeminiPath.Text.Trim();
            string dest = txtBackupPath.Text.Trim();
            if (!Directory.Exists(gemini)) { MessageBox.Show("未找到 .gemini 目录: " + gemini, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (string.IsNullOrEmpty(dest)) { MessageBox.Show("请输入备份导出路径。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            ProgressForm progressDlg = new ProgressForm("正在打包导出备份...", "正在执行高速流式压缩，请稍候...");
            bool bConv = chkConversations.Checked, bRules = chkRules.Checked, bSkills = chkSkills.Checked, bMcp = chkMcp.Checked;

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
                                      "  \"version\": \"0.4.1\"\r\n}";
                        sw.Write(meta);
                    }
                    n++;

                    // 2. 对话、脑区与会话索引
                    if (bConv)
                    {
                        progressDlg.UpdateMsg("正在归档历史对话与会话索引...");
                        n += AddDirToArchive(archive, Path.Combine(gemini, "antigravity\\conversations"), "data/antigravity/conversations");
                        n += AddDirToArchive(archive, Path.Combine(gemini, "antigravity\\brain"), "data/antigravity/brain");
                        n += AddDirToArchive(archive, Path.Combine(gemini, "antigravity\\annotations"), "data/antigravity/annotations");
                        n += AddFileToArchive(archive, Path.Combine(gemini, "antigravity\\agyhub_summaries_proto.pb"), "data/antigravity/agyhub_summaries_proto.pb");
                        n += AddFileToArchive(archive, Path.Combine(gemini, "antigravity\\antigravity_state.pbtxt"), "data/antigravity/antigravity_state.pbtxt");
                    }
                    n += AddFileToArchive(archive, Path.Combine(gemini, "antigravity\\installation_id"), "data/antigravity/installation_id");
                    n += AddDirToArchive(archive, Path.Combine(gemini, "antigravity\\knowledge"), "data/antigravity/knowledge");

                    // 3. 规则与内置技能
                    if (bRules)
                    {
                        progressDlg.UpdateMsg("正在归档规则与规范...");
                        n += AddFileToArchive(archive, Path.Combine(gemini, ".geminirules"), "data/.geminirules");
                        n += AddFileToArchive(archive, Path.Combine(gemini, "GEMINI.md"), "data/GEMINI.md");
                    }
                    if (bSkills)
                    {
                        progressDlg.UpdateMsg("正在归档内置与自定义技能...");
                        n += AddDirToArchive(archive, Path.Combine(gemini, "antigravity\\builtin\\skills"), "data/antigravity/builtin/skills");
                    }

                    // 4. 完整 config 目录（含 projects, skills, rules, mcp_config, config.json, sidecars）
                    progressDlg.UpdateMsg("正在归档项目定义、自定义技能与全局配置...");
                    n += AddDirToArchive(archive, Path.Combine(gemini, "config"), "data/config");

                    // 5. MCP 与客户端基础配置
                    if (bMcp)
                    {
                        n += AddDirToArchive(archive, Path.Combine(gemini, "antigravity\\mcp"), "data/antigravity/mcp");
                    }
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
                    Log("===== 备份完成！共打包 " + we.Result + " 个文件 → " + dest + " =====", Color.DarkGreen);
                    MessageBox.Show("备份导出成功！\n共打包 " + we.Result + " 个文件\n路径: " + dest, "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            _worker.RunWorkerAsync();
            progressDlg.ShowDialog(this);
        }

        // ══════════════════════════════════════════════════════════
        // 还原导入（Direct Stream from ZipArchive with In-Memory Remapping）
        // ══════════════════════════════════════════════════════════
        private void BtnRestoreBackup_Click(object sender, EventArgs e)
        {
            string zipPath = txtRestorePath.Text.Trim();
            if (!File.Exists(zipPath)) { MessageBox.Show("备份文件不存在: " + zipPath, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            // 检查 Antigravity 是否在运行，提示关闭
            if (!EnsureAntigravityClosed(true)) return;

            if (MessageBox.Show("确认还原到当前用户？\n目标: " + HomePath + "\\.gemini\n\n所有配置文件路径将自动多格式智能重映射。",
                "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            ProgressForm progressDlg = new ProgressForm("正在还原导入备份...", "正在解压并恢复文件，请稍候...");
            string home = HomePath;
            string targetGemini = Path.Combine(home, ".gemini");

            _worker = new BackgroundWorker();
            _worker.DoWork += (ws, we) =>
            {
                string sourceHome = "";
                int n = 0;

                using (FileStream fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    // 1. 读取 metadata.json
                    ZipArchiveEntry metaEntry = archive.GetEntry("metadata.json");
                    if (metaEntry != null)
                    {
                        using (StreamReader sr = new StreamReader(metaEntry.Open(), Encoding.UTF8))
                        {
                            string meta = sr.ReadToEnd();
                            sourceHome = ExtractJsonValue(meta, "source_home");
                        }
                    }

                    progressDlg.UpdateMsg("正在执行流式解压与多格式路径重映射...");
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue; // 忽略目录项
                        if (!entry.FullName.StartsWith("data/", StringComparison.OrdinalIgnoreCase) &&
                            !entry.FullName.StartsWith("data\\", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string relPath = entry.FullName.Substring(5).Replace('/', '\\');
                        string dst = Path.Combine(targetGemini, relPath);
                        string dDir = Path.GetDirectoryName(dst);
                        if (!string.IsNullOrEmpty(dDir) && !Directory.Exists(dDir))
                        {
                            try { Directory.CreateDirectory(dDir); } catch {}
                        }

                        string ext = Path.GetExtension(entry.Name).ToLower();
                        bool isText = ext == ".json" || ext == ".geminirules" || ext == ".txt" || ext == ".md" || ext == ".pbtxt" || entry.Name == ".geminirules" || entry.Name == "installation_id";

                        try
                        {
                            if (!string.IsNullOrEmpty(sourceHome) && isText)
                            {
                                string content;
                                using (StreamReader sr = new StreamReader(entry.Open(), Encoding.UTF8))
                                {
                                    content = sr.ReadToEnd();
                                }
                                string remapped = RemapAllPathFormats(content, sourceHome, home);
                                File.WriteAllText(dst, remapped, Encoding.UTF8);
                            }
                            else
                            {
                                using (Stream entryStream = entry.Open())
                                using (FileStream outStream = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    entryStream.CopyTo(outStream);
                                }
                            }
                            n++;
                        }
                        catch (Exception ex)
                        {
                            Log("  [还原跳过] " + relPath + " (" + ex.Message + ")", Color.DarkOrange);
                        }
                    }
                }

                Log("共还原 " + n + " 个核心文件。", Color.DarkGreen);

                progressDlg.UpdateMsg("正在校验并重定向工程工作区路径...");
                RemapAndVerifyProjects(targetGemini, sourceHome, home);

                progressDlg.UpdateMsg("正在修复文件控制权限...");
                FixPermissions(targetGemini);
            };

            _worker.RunWorkerCompleted += (ws, we) =>
            {
                progressDlg.Close();
                DetectPaths();
                if (we.Error != null)
                {
                    Log("还原失败: " + we.Error.Message, Color.Red);
                    MessageBox.Show("还原失败:\n" + we.Error.Message + "\n\n详情见日志。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    Log("===== 还原完成！所有项目工作区与对话索引已就绪，请启动 Antigravity =====", Color.DarkGreen);
                    MessageBox.Show("还原成功！\n对话历史、规则技能与 MCP 配置已全部恢复。\n项目工作区已自动重定向或建立占位，请重启 Antigravity。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            _worker.RunWorkerAsync();
            progressDlg.ShowDialog(this);
        }

        // ══════════════════════════════════════════════════════════
        // 多格式智能路径重映射（支持 Raw / Escaped / URL-Encoded / Uri）
        // ══════════════════════════════════════════════════════════
        private string RemapAllPathFormats(string text, string srcHome, string curHome)
        {
            if (string.IsNullOrEmpty(srcHome) || string.IsNullOrEmpty(curHome) || srcHome.Equals(curHome, StringComparison.OrdinalIgnoreCase))
                return text;

            // 1. 标准反斜杠: C:\Users\Old -> C:\Users\New
            text = text.Replace(srcHome, curHome);

            // 2. 正斜杠: C:/Users/Old -> C:/Users/New
            string srcSlash = srcHome.Replace('\\', '/');
            string curSlash = curHome.Replace('\\', '/');
            text = text.Replace(srcSlash, curSlash);

            // 3. JSON 转义双反斜杠: C:\\Users\\Old -> C:\\Users\\New
            string srcEsc = srcHome.Replace("\\", "\\\\");
            string curEsc = curHome.Replace("\\", "\\\\");
            text = text.Replace(srcEsc, curEsc);

            // 4. URL 编码变体 (%3A 和 %5C)
            string srcDrive = srcHome.Length >= 2 && srcHome[1] == ':' ? srcHome.Substring(0, 1) : "";
            string curDrive = curHome.Length >= 2 && curHome[1] == ':' ? curHome.Substring(0, 1) : "";

            if (!string.IsNullOrEmpty(srcDrive) && !string.IsNullOrEmpty(curDrive))
            {
                string srcSub = srcHome.Substring(2); // \Users\Old
                string curSub = curHome.Substring(2); // \Users\New

                string[] driveEncodings = new string[] { "%3A", "%3a" };

                // 替换常见 URL 编码组合
                foreach (string de in driveEncodings)
                {
                    // c%3A%5CUsers%5COld
                    string oldPattern1 = srcDrive.ToLower() + de + srcSub.Replace("\\", "%5C");
                    string newPattern1 = curDrive.ToLower() + "%3A" + curSub.Replace("\\", "%5C");
                    text = text.Replace(oldPattern1, newPattern1);
                    text = text.Replace(oldPattern1.ToUpper(), newPattern1);

                    // c%3A/Users/Old
                    string oldPattern2 = srcDrive.ToLower() + de + srcSub.Replace('\\', '/');
                    string newPattern2 = curDrive.ToLower() + "%3A" + curSub.Replace('\\', '/');
                    text = text.Replace(oldPattern2, newPattern2);
                    text = text.Replace(oldPattern2.ToUpper(), newPattern2);

                    // c%3A%2FUsers%2FOld
                    string oldPattern3 = srcDrive.ToLower() + de + srcSub.Replace("\\", "%2F");
                    string newPattern3 = curDrive.ToLower() + "%3A" + curSub.Replace("\\", "%2F");
                    text = text.Replace(oldPattern3, newPattern3);
                    text = text.Replace(oldPattern3.ToUpper(), newPattern3);
                }
            }

            return text;
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
                    string projName = Path.GetFileNameWithoutExtension(jsonFile);

                    // 提取 project name
                    int nameIdx = content.IndexOf("\"name\"");
                    if (nameIdx >= 0)
                    {
                        int colon = content.IndexOf(":", nameIdx);
                        if (colon >= 0)
                        {
                            int q1 = content.IndexOf("\"", colon);
                            if (q1 >= 0)
                            {
                                int q2 = content.IndexOf("\"", q1 + 1);
                                if (q2 > q1) projName = content.Substring(q1 + 1, q2 - q1 - 1);
                            }
                        }
                    }

                    // 提取 folderUri
                    int uriIdx = content.IndexOf("\"folderUri\"");
                    if (uriIdx >= 0)
                    {
                        int start = content.IndexOf("file:///", uriIdx);
                        if (start >= 0)
                        {
                            start += 8;
                            int end = content.IndexOf("\"", start);
                            if (end > start)
                            {
                                string rawUriPart = content.Substring(start, end - start);
                                string decodedPath = Uri.UnescapeDataString(rawUriPart);
                                if (decodedPath.StartsWith("/") && decodedPath.Length > 2 && decodedPath[2] == ':')
                                    decodedPath = decodedPath.Substring(1);
                                decodedPath = decodedPath.Replace('/', '\\');

                                // 检查当前主机上目标工程路径是否存在
                                if (Directory.Exists(decodedPath))
                                {
                                    Log("  [工程就绪] " + projName + " -> " + decodedPath, Color.DarkGreen);
                                }
                                else
                                {
                                    // 启发式搜索：检查当前用户桌面/文档/D盘/E盘是否存在同名工程文件夹
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
                                        // 找到了实际存在的工程路径，执行重定向更新 JSON
                                        string newUri = "file:///" + foundRealDir.Replace('\\', '/');
                                        content = content.Replace("file:///" + rawUriPart, newUri);
                                        File.WriteAllText(jsonFile, content, Encoding.UTF8);
                                        Log("  [工程智能重定向] " + projName + ": 原路径未找到，已重定向至新位置 -> " + foundRealDir, Color.DarkBlue);
                                    }
                                    else
                                    {
                                        // 无法找到实际代码仓库，自动建立目录占位，避免 Antigravity 启动时将工程标记为失效并隐藏对话
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
        // 辅助
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

        private void SetBusy(bool busy)
        {
            if (InvokeRequired) { BeginInvoke(new Action<bool>(SetBusy), busy); return; }
            btnCreateBackup.Enabled = !busy;
            btnRestoreBackup.Enabled = !busy;
            this.Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private Button MakeBtn(string text, int width, Color backColor)
        {
            return new Button() { Text = text, Width = width, Height = 22, BackColor = backColor, FlatStyle = FlatStyle.System };
        }

        private void BtnSaveLog_Click(object sender, EventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog() { Filter = "日志(*.log)|*.log|文本(*.txt)|*.txt", FileName = "agt_log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log" };
            if (dlg.ShowDialog() == DialogResult.OK) { File.WriteAllText(dlg.FileName, rtbLog.Text, Encoding.UTF8); Log("日志已保存: " + dlg.FileName, Color.DarkGreen); }
        }

        private void BtnBrowseBackup_Click(object sender, EventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog() { Filter = "AGY备份(*.agy)|*.agy|ZIP(*.zip)|*.zip", FileName = "antigravity_backup_" + DateTime.Now.ToString("yyyyMMdd") + ".agy" };
            if (dlg.ShowDialog() == DialogResult.OK) txtBackupPath.Text = dlg.FileName;
        }

        private void BtnBrowseRestore_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog() { Filter = "AGY备份(*.agy;*.zip)|*.agy;*.zip|所有文件(*.*)|*.*" };
            if (dlg.ShowDialog() == DialogResult.OK) txtRestorePath.Text = dlg.FileName;
        }

        private string ExtractJsonValue(string json, string key)
        {
            int idx = json.IndexOf("\"" + key + "\""); if (idx < 0) return "";
            idx = json.IndexOf(":", idx) + 1;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t')) idx++;
            if (idx >= json.Length || json[idx] != '"') return "";
            idx++;
            int end = json.IndexOf('"', idx); if (end < 0) return "";
            return json.Substring(idx, end - idx).Replace("\\\\", "\\");
        }
    }

    // ── 进度条窗口（模态，显示滚动条以确认程序未死锁）──
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
            this.ControlBox = false; // 禁用关闭按钮

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
}
