# Antigravity Companion (V1.0.1)

**面向 Windows 平台 Google Antigravity (AGY) 生态的本地单文件每日驾驶舱与资产治理工作台**（Local Daily Cockpit, Storage Slimming & Migration Control Plane）。

`Antigravity_Companion` 是 Antigravity IDE 与 `antigravity-cli` 的伴生桌面控制中心。它将本机 `~/.gemini/` 下的配置开关、MCP 健康体检、规则体量统计、工程会话、智能重命名、存储瘦身与跨机备份整合为**零外部运行时依赖的单个 `.exe`（< 1.2 MB）**。托盘常驻、全局热键与开机自启均采用 **Opt-In（默认关闭）** 设计，并支持定时静默快照。

| 维度 | 当前实现 (V1.0.1) |
| :--- | :--- |
| **交付形态** | 纯原生 C# 5.0（.NET Framework 4.5+）+ 内嵌模块化离线 SPA（`ui/` 14 项资源），单文件编译为 `Antigravity_Companion.exe` |
| **运行与托盘** | 纯用户态 `127.0.0.1` 动态回环端口 HTTP/1.1 + SSE 服务，Edge/Chrome `--app` 桌面壳；托盘常驻（`minimizeToTray`）与全局热键（`globalHotkey`）默认关闭，可在「系统设置」按需开启 |
| **数据底座** | 无损 Protobuf AST 编解码、`winsqlite3.dll` P/Invoke（WAL 在线快照 / FTS5）、流式 ZIP（`.agy`）、评论保留 JSONC 局部合流（`UnifiedJson`） |
| **详细操作手册** | 见 [Antigravity_Companion使用说明书.md](./Antigravity_Companion使用说明书.md) |

---

## 一、 V1.0.1 核心架构与模块化能力

### 1. 六大主导航视图 + 独立系统设置（按需懒加载前端）

1. **概览 (`overview`)**：展示本机能力与 Vault 缺失路径分离审计、可整理 vs 锁定活跃会话统计（`not_fully_idle` 并发保护）、已登记工程安全启动台（IDE 打开 / 打开目录 / 开终端 / 看会话）与只读孤儿体检。首屏仅请求核心与当前视图数据（< 5 次 API 请求），其余视图切换时按需懒加载。
2. **每日驾驶舱 (`cockpit`)**：无损读写 `~/.gemini/config/config.json` 的 `userSettings`（保留注释与未知枚举字段），统一管理 Plugin / MCP / Hook 启用开关、检测跨来源重复 MCP 定义冲突，并提供一键 **MCP Health Doctor** 实测握手协议与延迟；当 IDE 运行中修改配置时给出非阻塞黄色横幅提醒。
3. **会话管理 (`sessions`)**：统一聚合 IDE、CLI 与只读历史归档（`antigravity-cli/conversation_summaries.db`），支持 FTS5/LIKE 全文检索、安全高亮 Markdown 预览导出、工程目录重定位、级联删除（含安全放置的工程级联删除）以及一键带入「智能重命名」。
4. **智能重命名 (`rename`)**：独立一级工作台，提供三步式重命名向导（Step 1 范围与锁定检查 → Step 2 本地规则 `multiRepoRules` CRUD / **Zero-Key `agy` CLI** / 离线剪贴板交换 → Step 3 Diff 预览与行级快照回滚）。
5. **能力与规则 (`vault`)**：展示规则文件客观体量（字节 / 字符 / 行数）与参考估算 Token（默认 `ruleTokenReferenceThreshold = 0` 不设强制标红告警，可在设置中自选参考阈值）、白名单安全校验在线 Markdown/JSON 编辑器、**Portable Vault** 目录切换与旧版 `./Repository` 一键迁移、方向感双向同步矩阵（含 `NeedsReview` 核验与多路径缺失修复）。
6. **存储与备份 (`backup`)**：顶部双子页签整合 **「备份与迁移」**（**FastDaily** / **FullForensic** 两级 `.agy` 归档往返与 `RestoreUserSettings`）与 **「存储诊断与瘦身」**（字节级盘点、全量孤儿 GC、**Mode 5A 冷归档裁剪**，以及折叠在「高级微创瘦身（实验性）」面板中的 **Mode 5C 数据库抽脂**）。
7. **⚙ 系统设置 (`settings`)**：位于左侧栏底部独立入口，集中管理 Portable Vault 路径与旧版迁移、`agy` CLI 路径与可用性探测、Opt-In 最小化到托盘（默认关）、Opt-In 全局快捷键（带三态注册状态徽标）、Opt-In Windows 开机自启（带注册表写入失败自动回滚保护）、定时静默只读快照及可选规则参考 Token 阈值。

### 2. 分层源码架构

```text
Antigravity_Companion/
├── src/
│   ├── Program.cs                           # 单实例互斥锁、--tray-only / --no-browser / --vault / --home 参数解析
│   ├── Core/
│   │   ├── CompanionCommandDispatcher.cs    # UI 无关的统一命令分发器
│   │   ├── OperationResult.cs               # 标准化不可变操作结果容器
│   │   └── UnifiedJson.cs                   # 零依赖 JSON/JSONC 词法解析与保注释局部 Patch 引擎
│   ├── Storage/
│   │   ├── ProtoWireCodec.cs                # 无损 Protobuf AST 编解码
│   │   ├── SessionMetadataExtractor.cs      # 三层会话元数据提取与叠加
│   │   ├── SummarySqliteBridge.cs           # winsqlite3.dll P/Invoke（WAL / FTS5 / LIKE 回退）
│   │   ├── SqliteOnlineBackup.cs            # sqlite3_backup 在线原子快照与 integrity_check
│   │   ├── IncrementalShardMerger.cs        # 增量 Protobuf 分片合流与路径重映射
│   │   ├── StreamingZipIndexReader.cs       # 流式 .agy 归档索引探测与 ZipSlip 防护
│   │   └── TranscriptMarkdownExporter.cs    # 对话流与 CLI 脑区日志 Markdown 有界预览与导出
│   ├── Services/
│   │   ├── WorkspaceService.cs              # 进程门禁、工程分组、最短区分父目录与 STA 弹窗桥
│   │   ├── AntigravityConfigService.cs      # Cockpit 扫描、Switchboard 切换、MCP Doctor 与白名单编辑器
│   │   ├── StorageSlimService.cs            # 存储空间拆解、沙盒预检、5C 抽脂 / 5A 冷归档裁剪与全量 GC
│   │   ├── AntigravityCliAdapter.cs         # Zero-Key `agy -p --output-format json` 探测与超时树终止
│   │   ├── ScheduledSnapshotService.cs      # companion_settings.json 持久化、定时静默快照、滚动清理与自启注册表
│   │   ├── CapabilityVaultService.cs        # Portable Vault 解析、{{HOME}} 泛化与 Key 级无损合流
│   │   ├── SessionLifecycleService.cs       # 原子写入、工程迁移、路径重定位与闭环级联删除
│   │   ├── SessionRenameService.cs          # .pbtxt 编解码、规则合成、快照回滚与 AI 交换
│   │   ├── BackupSelectionPolicy.cs         # 归档默认勾选策略
│   │   ├── VisualDiffEngine.cs              # 三方基线方向 Diff (含 NeedsReview) 与有界 LCS 行级 Diff
│   │   └── WorkspaceLauncherService.cs      # IDE / Explorer / PowerShell 安全工作区启动
│   └── Host/
│       ├── HttpServerCore.cs                # 纯用户态 TcpListener HTTP/1.1 + 14 项内嵌静态资源路由
│       ├── SseHub.cs                        # 有界队列独立泵线程 SSE 广播中心
│       ├── ApiRouter.cs                     # REST API 与 CompanionCommandDispatcher 路由适配
│       ├── OverviewBuilder.cs               # 概览、会话、备份与 Diff 序列化构建器
│       ├── TrayHost.cs                      # NotifyIcon 系统托盘菜单与 RegisterHotKey 全局热键宿主
│       └── WebShellLauncher.cs              # Edge/Chrome --app 桌面壳启动与托盘生命周期托管
├── ui/
│   ├── index.html                           # V1.0.1 6 主视图 + 独立系统设置 SPA 骨架（严格 CSP 无内联脚本）
│   ├── styles.css                           # Slate Light / Dark 双主题与驾驶舱/存储/编辑器样式
│   ├── app.js                               # 引导器、按需懒加载导航、SSE 监听与心跳
│   └── js/
│       ├── api.js                           # fetch 封装、SSE 错误/告警透传、全局异常捕获
│       ├── state.js                         # 集中状态存储、dirty 标记与局部 DOM 增量更新助手
│       ├── components.js                    # 抽屉/弹窗、安全 DOM 文本节点高亮、行级 Diff 与在线编辑器
│       └── views/                           # overview.js / cockpit.js / sessions.js / rename.js / storage.js / vault.js / backup.js / settings.js
└── templates/vault-template/                # 干净的 Portable Vault 默认模板 (manifest.json / rename_rules.json)
```

---

## 二、 关键机制与安全模型

### 1. Portable Vault 与路径泛化
- **默认解析顺序**：`--vault <dir>` 显式参数 > 已保存的伴侣设置 `vaultPath` > 程序同级 `./Vault/` > `~/.gemini/companion-vault/`（缺失时自动从 `templates/vault-template/` 初始化干净模板）。
- **跨机路径泛化**：所有能力片段中的用户主目录自动泛化为 `{{HOME}}`，导回本机时按 JSON/URI/Windows 路径上下文精准展开，并自动拦截 `auth.json`、`.env`、`*.key`、`*.pem` 等敏感凭证及 `node_modules` / `.venv` / `__pycache__`。
- **旧版迁移**：检测到 `./Repository` 存在历史资产时，在 UI「能力与规则」与「系统设置」提示一键无损迁移到当前 Portable Vault。

### 2. Zero-Key `agy` CLI 智能命名
- 自动从自定义路径、`PATH` 及标准安装目录探测本机已登录的 `agy.exe` / `agy.cmd`。
- 通过 `agy -p --output-format json` 与标准输入传递精简会话摘要，支持直接 JSON、Markdown 代码块、包裹字段及 NDJSON 多对象输出解析，无需额外配置外部 LLM API Key；支持超时强杀子进程树防止僵尸进程。

### 3. 存储瘦身安全模型（Mode 5A & 实验性 Mode 5C）
1. **严格筛选门槛**：仅针对已结束的闲置子代理会话（默认静置 `>= 7` 天且体积 `>= 5 MB`），**强制跳过**主会话、`not_fully_idle=1` 或状态非 `IDLE` 的活跃会话。
2. **Mode 5A（推荐）与全量孤儿 GC**：先将选中子代理库通过 `SqliteOnlineBackup` 流式冷归档到 `~/.gemini/companion-cold-archives/subagents_*.agy` 并校验可读性，再移除本地子代理 `.db` 文件。
3. **Mode 5C（实验性折叠面板）**：折叠在「高级微创瘦身（实验性）」中，执行前弹出显式风险确认弹窗；写入前先用 `SqliteOnlineBackup` 克隆至 `%TEMP%` 沙盒预检外键/触发器、`DELETE FROM executor_metadata; VACUUM;`、`PRAGMA integrity_check == ok` 及 Markdown 导出一致性，并强制先建冷归档 `.agy`。

### 4. Opt-In 系统托盘、全局热键与定时静默快照
- **设置持久化**：保存在 `%LOCALAPPDATA%\AntigravityCompanion\companion_settings.json`（含 `enabled`、`intervalHours`、`targetDirectory`、`retainCount`、`tier`、`minimizeToTray`、`globalHotkey`、`customAgyCliPath`、`vaultPath`、`startWithWindows`、`ruleTokenReferenceThreshold`）。
- **默认 Opt-In 行为**：首次安装默认关闭 `minimizeToTray`、`globalHotkey`（空字符串）与 `startWithWindows`；关闭浏览器窗口默认直接退出程序不残留后台进程。用户在「⚙ 系统设置」中按需开启后，可实时查看全局热键三态注册状态，且开机自启注册表写入失败会自动回滚并报错。
- **定时静默快照**：后台 `System.Threading.Timer` 按周期执行只读 `FastDaily`（或 `FullForensic`）`.agy` 快照（使用共享读锁与 `SqliteOnlineBackup`；若使用 `FullForensic` 且 IDE 正在运行则安全跳过），并按 `retainCount` 自动滚动清理旧 `snapshot_*.agy` 文件。

---

## 三、 编译与自动化验证

### 1. 编译单文件可执行程序

```bat
cmd /c build.bat
```

产物为根目录 `Antigravity_Companion.exe`（单个零依赖 `.exe`，内嵌全部 14 个前端资源，体积 < 1.2 MB）。

### 2. 运行全量生产代码测试套件

```bat
cmd /c tests\build_tests.bat
```

全套自动化测试覆盖 Protobuf AST、真实 `winsqlite3.dll` WAL/FTS5、`SqliteOnlineBackup`、三层重命名与回滚、`AntigravityConfigService` 全量扫描与 MCP Doctor、白名单编辑器、`StorageSlimService` 5C/5A 与预检、`AntigravityCliAdapter`、`ScheduledSnapshotService` 定时滚动快照、HTTP 全端点鉴权与静态资源分发、`TrayHost` 热键解析以及 `CompanionCommandDispatcher` 命令一致性校验。
