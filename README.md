# Antigravity 伴侣 (V1.1)

**面向 Windows 平台 Google Antigravity (AGY) 生态的本地单文件伴侣与资产治理工作台**（Local Companion, 30-Day Trash Safety Net, Online Hot Backup & Storage Cleanup）。

`Antigravity_Companion` 是 Antigravity 主窗口与 `antigravity-cli` 的伴生桌面工具。它将本机 `~/.gemini/` 下的项目快速启动、对话管理与批量整理标题、全局配置与扩展开关、空间清理与清理前安全备份、在线热备与跨机恢复，以及 30 天统一回收站和 `.bak` 历史版本整合为**零外部运行时依赖的单个 `.exe`（< 1.2 MB）**。系统托盘常驻、全局唤醒快捷键与开机自动启动均采用 **Opt-In（默认关闭）** 设计，并支持定时自动备份。

| 维度 | 当前实现 (V1.1) |
| :--- | :--- |
| **交付形态** | 纯原生 C# 5.0（.NET Framework 4.5+）+ 内嵌模块化离线 SPA（`ui/` **15 项**内嵌资源），单文件编译为 `Antigravity_Companion.exe` |
| **运行与托盘** | 纯用户态 `127.0.0.1` 动态回环端口 HTTP/1.1 + SSE 服务，Edge/Chrome `--app` 桌面壳（精准窗口匹配与断连自动重连）；托盘常驻、全局快捷键与开机自动启动默认关闭，可在「应用设置」按需开启 |
| **安全与可逆性** | 30 天统一回收站（对话/项目/残留垃圾/扩展配置均可一键还原，跨同步库保护）、配置修改自动留存 `.bak` 历史版本（带 JSON 损坏拦截）、空间清理强制先生成可读可恢复的清理前安全备份 (`.agy`)、`IDE_CLOSE_TIMEOUT` 二步强杀逃生舱 |
| **数据底座** | 无损 Protobuf AST 编解码、`winsqlite3.dll` P/Invoke（WAL 在线热备 / FTS5 + LIKE 全文检索）、流式 ZIP（`.agy`）、保注释 JSONC 局部合流与字段删除（`UnifiedJson`）、子目录实时 `FileSystemWatcher` |
| **详细操作手册** | 见 [Antigravity_Companion使用说明书.md](./Antigravity_Companion使用说明书.md) |

---

## 一、 V1.1 核心架构与模块化能力

### 1. 5 大主导航 + 底部回收站与应用设置（5+1 导航架构，按需懒加载）

1. **工作台 (`overview`)**：展示本机与配置同步库路径健康审计、可整理 vs 进行中对话统计、待补刷改名提醒、已登记项目快速启动台（目录不存在时自动隐藏无效启动按钮并就地显示「修复目录位置」），以及残留垃圾文件只读体检与快捷移入回收站。首屏仅加载核心与当前视图数据，其余视图切换时按需懒加载，侧边栏 0 计数角标自动隐藏。
2. **对话管理 (`sessions`)**：顶部双子页签整合 **「对话列表」** 与 **「批量整理标题」**：
   - **对话列表**：按正式项目、其他分组（CLI / 未关联项目 / 后台子任务）与只读旧版 CLI 历史聚合浏览（只读历史仅保留预览与导出）；支持正文全文检索、安全高亮预览导出 `.md`、更换项目、重定位目录、导出备份文件 (`.agy`) 及可撤销移入回收站。
   - **批量整理标题**：三步式向导（挑选待整理对话 → 本地规则 / 免密钥 `agy` CLI / 外部 AI 剪贴板润色 → 预览并保存）；进行中对话自动禁用跳过；默认采用 `SmartAuto` 不打断改名，主窗口退出后后台自动补刷 `.pb` 侧边栏，并保留最近 15 批改名撤销记录。
3. **扩展与配置 (`cockpit`)**：顶部三子页签整合 **「全局配置与开关」**、**「规则与技能编辑」** 与 **「配置同步库」**：
   - **全局配置与开关**：无损读写 `~/.gemini/config/config.json` 的 `userSettings`（保留 JSONC 注释与未知字段，支持清空重置对话栏宽度），统一切换插件 / MCP / 钩子启用状态、检测重复 MCP 定义并一键实测 MCP 连通性与延迟；表单含未保存切页拦截与后台刷新防覆盖保护。
   - **规则与技能编辑**：展示规则文件体量与参考估算 Token（默认不设强制标红阈值，可在应用设置自选参考阈值），提供白名单安全校验的在线编辑器与 `.bak` 历史版本一键还原。
   - **配置同步库**：按配置同步库路径自动隔离同步基线，支持旧版 `./Repository` 一键迁移、源码差异对比、缺失路径修复、冲突/反向覆盖二次确认弹窗与逐项列清单的可撤销移除。
4. **空间清理 (`storage`)**：字节级盘点磁盘占用，全中文展示保护与跳过原因；支持残留垃圾文件一键移入回收站（无残留时自动禁用弱化）、陈旧后台子任务「先建清理前安全备份 (`.agy`) 后清理」（带后果确认弹窗与阈值变更重扫提醒）、清理前安全备份历史直接查看并恢复，以及默认折叠在底部的「高级选项（实验性）— 数据库原位压缩」。
5. **备份与同步 (`backup`)**：支持**只读在线热备**（无需关闭 Antigravity 主窗口即可创建「日常备份」或「完整备份」`.agy`），集中展示自动备份历史与清理前安全备份列表，打开任意 `.agy` 文件后支持免解压预览、局部刷新切页/筛选与按需还原。
6. **底部入口：回收站 (`trash`) 与 应用设置 (`settings`)**：
   - **回收站**：双子页签提供 **「已删除内容回收站」**（30 天保留，对话/项目/残留垃圾/扩展配置一键完整还原，支持跨同步库防误还保护）与 **「配置历史版本 (.bak)」**（自动留存的配置文件版本一键还原，含 JSON 语法校验防损坏）。
   - **应用设置**：集中管理配置同步库目录、免密钥 `agy` CLI 路径、默认关闭的托盘常驻/全局快捷键/开机自动启动、定时自动备份计划与规则参考 Token 阈值。

### 2. 分层源码架构

```text
Antigravity_Companion/
├── src/
│   ├── Program.cs                           # 单实例互斥锁、--tray-only / --no-browser / --vault / --home 参数解析
│   ├── Core/
│   │   ├── CompanionCommandDispatcher.cs    # UI 无关的统一命令分发器
│   │   ├── OperationResult.cs               # 标准化不可变操作结果容器
│   │   └── UnifiedJson.cs                   # 零依赖 JSON/JSONC 词法解析、保注释局部 Patch 与键删除引擎
│   ├── Storage/
│   │   ├── ProtoWireCodec.cs                # 无损 Protobuf AST 编解码
│   │   ├── SessionMetadataExtractor.cs      # 三层会话元数据提取与叠加
│   │   ├── SummarySqliteBridge.cs           # winsqlite3.dll P/Invoke（WAL / FTS5 / LIKE 回退）
│   │   ├── SqliteOnlineBackup.cs            # sqlite3_backup 在线原子热备快照与 integrity_check
│   │   ├── SqliteSessionStore.cs            # 对话 SQLite 读写与结构维护辅助
│   │   ├── IncrementalShardMerger.cs        # 增量 Protobuf 分片合流与路径重映射
│   │   ├── StreamingZipIndexReader.cs       # 流式 .agy（含清理前安全备份）索引探测与 ZipSlip 防护
│   │   └── TranscriptMarkdownExporter.cs    # 对话流与 CLI 脑区日志 Markdown 有界预览与导出
│   ├── Services/
│   │   ├── WorkspaceService.cs              # 进程门禁、IDE_CLOSE_TIMEOUT 强杀、工程分组与 STA 弹窗桥
│   │   ├── AntigravityConfigService.cs      # 全局配置扫描与无损更新、开关切换、MCP 体检与白名单编辑器
│   │   ├── StorageSlimService.cs            # 空间拆解、统一残留扫描引擎、备份后清理与安全备份历史
│   │   ├── TrashAndBackupService.cs         # 30 天统一回收站、跨同步库保护、.bak 历史版本与待补刷 .pb 队列
│   │   ├── ArchiveTransferService.cs        # 只读在线热备、日常/完整 .agy 打包与选择性/冷归档恢复
│   │   ├── AntigravityCliAdapter.cs         # 免密钥 `agy -p --output-format json` 探测与超时子进程树清理
│   │   ├── ScheduledSnapshotService.cs      # companion_settings.json 持久化、定时自动备份、滚动清理与自启注册表
│   │   ├── CapabilityVaultService.cs        # 配置同步库解析、按库隔离基线、{{HOME}} 泛化与 Key 级无损合流
│   │   ├── SessionLifecycleService.cs       # 原子写入、项目迁移、目录重定位与闭环删除
│   │   ├── SessionRenameService.cs          # .pbtxt 编解码、规则合成、SmartAuto 改名、撤销记录与 AI 交换
│   │   ├── BackupSelectionPolicy.cs         # 备份包默认勾选策略
│   │   ├── VisualDiffEngine.cs              # 三方基线方向 Diff 与有界 LCS 行级 Diff
│   │   └── WorkspaceLauncherService.cs      # Antigravity 主窗口 / 资源管理器 / PowerShell 安全启动
│   └── Host/
│       ├── HttpServerCore.cs                # 纯用户态 HTTP/1.1 + 15 项内嵌静态资源 + 子目录 FileSystemWatcher + 主窗口状态监测
│       ├── SseHub.cs                        # 有界队列独立泵线程 SSE 广播中心（progress / log / ide_status / fs_changed / job_done）
│       ├── ApiRouter.cs                     # REST API 核心入口与统一命令调度适配
│       ├── ApiRouter.Cockpit.cs             # 扩展与配置、MCP 体检、在线编辑器与工作区启动路由
│       ├── ApiRouter.Sessions.cs            # 对话列表、全文搜索、批量改名与撤销路由
│       ├── ApiRouter.Storage.cs             # 空间分析、清理前安全备份列表、空间清理与残留体检路由
│       ├── ApiRouter.VaultBackup.cs         # 配置同步库、在线备份/恢复、应用设置、回收站与 .bak 还原路由
│       ├── OverviewBuilder.cs               # 工作台、对话、备份与 Diff 序列化构建器
│       ├── TrayHost.cs                      # NotifyIcon 系统托盘菜单（统一术语 + 防双击抖动）与全局热键宿主
│       └── WebShellLauncher.cs              # Edge/Chrome --app 桌面壳精准匹配聚焦与生命周期托管
├── ui/
│   ├── index.html                           # V1.1 5+1 导航骨架、断连重连与未保存刷新保护条（严格 CSP 无内联脚本）
│   ├── styles.css                           # 浅色 / 深色双主题与响应式分栏样式
│   ├── app.js                               # 引导器、按需懒加载导航、增量日志抽屉、SSE 监听与心跳重连
│   └── js/
│       ├── api.js                           # fetch 封装、withButtonBusy 防重入、SSE 透传与全局异常捕获
│       ├── state.js                         # 集中状态存储、dirty 标记、未保存表单脏态守卫与局部更新助手
│       ├── components.js                    # 抽屉/弹窗、IDE_CLOSE_TIMEOUT 逃生舱、安全文本高亮、行级 Diff、历史版本与在线编辑器
│       └── views/                           # overview.js / cockpit.js / sessions.js / rename.js / storage.js / vault.js / backup.js / settings.js / trash.js
└── templates/vault-template/                # 干净的配置同步库默认模板 (manifest.json / rename_rules.json)
```

---

## 二、 关键机制与安全模型

### 1. 30 天统一回收站与 `.bak` 历史版本
- **所有删除皆可撤销**：对话、项目与对话、残留垃圾文件、本机扩展配置、配置同步库项删除时均先移入统一回收站（默认保留 30 天），支持在「回收站」一键完整还原（恢复项目记录、主对话、后台子任务及 `ParentConversationId` 关联，同时清理失效的待补刷 `.pb` 队列）。
- **跨同步库保护**：从同步库 A 移除的配置项，在切换到同步库 B 后还原会被拦截且完整保留回收站数据，切回同步库 A 即可正常还原。
- **配置历史版本 (`.bak`)**：每次修改全局配置、切换开关或在线编辑规则/配置文件前自动生成时间戳 `.bak`，还原前先校验 JSON 合法性并自动备份当前版本。

### 2. 在线热备与清理前安全备份 (`.agy`)
- **只读在线热备**：借助 `SqliteOnlineBackup` 原子快照，无需关闭 Antigravity 主窗口即可创建「日常备份」或「完整备份」`.agy`，且自动备份历史与清理前安全备份均可在页面直接浏览、免解压预览和按需还原。
- **空间清理安全闭环**：
  - 仅允许清理静置超龄且超过体积阈值的空闲后台子任务，主对话、CLI 对话与进行中任务强制保护跳过。
  - 所有空间清理写入均受主窗口进程锁保护（若优雅关闭超时，弹窗提供 `IDE_CLOSE_TIMEOUT` 强制结束进程选项），并先弹出后果确认弹窗与阈值修改重扫提示。
  - 清理前强制生成可读可恢复的清理前安全备份 (`.agy`) 存于 `~/.gemini/companion-cold-archives/`。

### 3. 配置同步库与按目录隔离基线
- **默认解析顺序**：`--vault <dir>` > 应用设置已保存路径 > 程序同级 `./Vault/` > `~/.gemini/companion-vault/`。
- **按目录隔离基线**：每个配置同步库维护独立同步基线文件，切换多个同步库互不污染差异状态；用户主目录自动泛化为 `{{HOME}}` 并自动拦截敏感凭证与构建缓存目录。

### 4. 系统托盘、全局快捷键与定时自动备份
- **默认 Opt-In**：最小化到托盘、全局唤醒快捷键、随 Windows 开机自动启动默认全部关闭；关闭窗口即干净退出。
- **托盘与主窗口体验**：托盘右键菜单与全站术语统一（`打开主窗口` / `立即创建自动备份` / `开机自动启动` / `退出`，提示 `Antigravity 伴侣 V1.1`），支持防双击抖动与精准聚焦 `--app` 独立窗口。

---

## 三、 编译与自动化验证

### 1. 编译单文件可执行程序

```bat
cmd /c build.bat
```

产物为根目录 `Antigravity_Companion.exe`（单个零依赖 `.exe`，内嵌全部 **15 个**前端资源，体积 < 1.2 MB）。

### 2. 运行全量生产代码测试套件

```bat
cmd /c tests\build_tests.bat
```

全套 **90 项自动化测试（`T01` ~ `T90`）** 覆盖 Protobuf AST、真实 `winsqlite3.dll` WAL/FTS5、`SqliteOnlineBackup` 在线热备、三层改名与 `SmartAuto` 退出补刷、30 天回收站与跨同步库安全还原、`.bak` 历史版本与损坏拦截、`AntigravityConfigService` 全量扫描/清空重置字段/MCP 体检/白名单编辑器、`StorageSlimService` 空间分析/备份后清理/清理前安全备份直读恢复、子目录实时 `FileSystemWatcher`、`IDE_CLOSE_TIMEOUT` 逃生舱、按库隔离基线、HTTP 全端点鉴权与 15 项静态资源分发、系统托盘术语与热键解析以及 `CompanionCommandDispatcher` 一致性校验。
