using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;

namespace AntigravityCompanion
{
    /// <summary>
    /// Core partial of ApiRouter: command dispatcher registration, top-level API routing,
    /// IDE process guards, canonical path validation, and shared serialization helpers.
    /// Strictly C# 5.0 (.NET Framework 4.x).
    /// </summary>
    internal sealed partial class ApiRouter
    {
        private readonly LocalWebHost _host;
        private readonly CompanionCommandDispatcher _dispatcher;

        public ApiRouter(LocalWebHost host)
        {
            _host = host;
            _dispatcher = new CompanionCommandDispatcher();
            RegisterCommands();
        }

        public CompanionCommandDispatcher Dispatcher
        {
            get { return _dispatcher; }
        }

        private void RegisterCommands()
        {
            // Read commands
            _dispatcher.Register("overview.get", delegate(Dictionary<string, object> args)
            {
                return OperationResult<Dictionary<string, object>>.Success(OverviewBuilder.BuildOverviewPayload(_host));
            });

            _dispatcher.Register("sessions.list", delegate(Dictionary<string, object> args)
            {
                return OperationResult<Dictionary<string, object>>.Success(OverviewBuilder.BuildSessionsPayload(_host));
            });

            _dispatcher.Register("capabilities.list", delegate(Dictionary<string, object> args)
            {
                bool fromBackup = UnifiedJson.GetBool(args, "fromBackup", false);
                return OperationResult<Dictionary<string, object>>.Success(OverviewBuilder.BuildCapabilitiesPayload(_host, fromBackup));
            });

            _dispatcher.Register("rename.candidates", delegate(Dictionary<string, object> args)
            {
                return OperationResult<Dictionary<string, object>>.Success(OverviewBuilder.BuildRenameCandidatesPayload(_host));
            });

            _dispatcher.Register("gc.scan", delegate(Dictionary<string, object> args)
            {
                OperationResult<OrphanScanResult> scanRes = SessionLifecycleService.ScanOrphanedDataResult(_host.GeminiPath);
                if (!scanRes.Ok)
                    return OperationResult<Dictionary<string, object>>.Fail(scanRes.ErrorMessage, scanRes.Warnings);

                lock (_host.StateLock)
                {
                    _host.LastOrphanScanUnsafe = scanRes.Data;
                }
                _host.AppendLog("info", string.Format("孤儿只读体检完成：发现 {0} 个可清理项（{1:F2} MB）。",
                    scanRes.Data.TotalOrphanItems, scanRes.Data.TotalOrphanMegabytes));
                return OperationResult<Dictionary<string, object>>.Success(OverviewBuilder.SerializeOrphanScan(scanRes.Data));
            });

            _dispatcher.Register("sessions.search", delegate(Dictionary<string, object> args)
            {
                string q = UnifiedJson.GetString(args, "query", string.Empty);
                bool searchContent = UnifiedJson.GetBool(args, "searchContent", false);
                string err = null;
                HashSet<string> hitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string trimmedQ = (q ?? string.Empty).Trim();

                if (!string.IsNullOrEmpty(trimmedQ))
                {
                    if (searchContent)
                    {
                        string dbPath = Path.Combine(_host.GeminiPath, "antigravity", "conversation_summaries.db");
                        if (File.Exists(dbPath))
                        {
                            HashSet<string> dbHits = SummarySqliteBridge.SearchConversationsFullText(dbPath, trimmedQ);
                            if (!string.IsNullOrEmpty(SummarySqliteBridge.LastError))
                                err = SummarySqliteBridge.LastError;
                            if (dbHits != null)
                            {
                                foreach (string id in dbHits) hitIds.Add(id);
                            }
                        }
                    }

                    List<ConversationSummaryInfo> sumsSnap;
                    lock (_host.StateLock)
                    {
                        sumsSnap = new List<ConversationSummaryInfo>(_host.LocalSummariesUnsafe);
                    }
                    for (int i = 0; i < sumsSnap.Count; i++)
                    {
                        ConversationSummaryInfo s = sumsSnap[i];
                        if (s == null || string.IsNullOrEmpty(s.ConversationId)) continue;
                        if ((s.DisplayTitle != null && s.DisplayTitle.IndexOf(trimmedQ, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (s.ProjectName != null && s.ProjectName.IndexOf(trimmedQ, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (searchContent && s.FirstPromptSnippet != null && s.FirstPromptSnippet.IndexOf(trimmedQ, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            hitIds.Add(s.ConversationId);
                        }
                    }
                }

                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["matchedIds"] = new List<object>(OverviewBuilder.ToObjList(hitIds));
                resp["error"] = err ?? string.Empty;
                resp["searchContent"] = searchContent;
                return OperationResult<Dictionary<string, object>>.Success(resp);
            });

            _dispatcher.Register("cockpit.summary", delegate(Dictionary<string, object> args)
            {
                int refThreshold = _host.SnapshotService != null
                    ? _host.SnapshotService.GetSettings().RuleTokenReferenceThreshold
                    : 0;
                CockpitSnapshot snap = AntigravityConfigService.ScanCockpit(_host.GeminiPath, _host.HomePath, _host.RepoPath, refThreshold);
                return OperationResult<Dictionary<string, object>>.Success(
                    AntigravityConfigService.SerializeCockpitSummary(snap, _host.GeminiPath, refThreshold));
            });

            _dispatcher.Register("mcp.doctor", delegate(Dictionary<string, object> args)
            {
                string srvId = UnifiedJson.GetString(args, "id", string.Empty);
                if (string.IsNullOrEmpty(srvId)) srvId = UnifiedJson.GetString(args, "serverId", string.Empty);
                string sourcePath = UnifiedJson.GetString(args, "sourcePath", string.Empty);
                bool includeDisabled = UnifiedJson.GetBool(args, "includeDisabled", false);
                int timeoutMs = UnifiedJson.GetInt(args, "timeoutMs", 3000);

                List<McpHealthCheckResult> results = AntigravityConfigService.RunMcpHealthCheck(
                    _host.GeminiPath, _host.HomePath, srvId, sourcePath, includeDisabled, timeoutMs);
                return OperationResult<Dictionary<string, object>>.Success(
                    AntigravityConfigService.SerializeMcpHealthResults(results));
            });

            _dispatcher.Register("editor.read", delegate(Dictionary<string, object> args)
            {
                string filePath = UnifiedJson.GetString(args, "path", string.Empty);
                int refThreshold = _host.SnapshotService != null
                    ? _host.SnapshotService.GetSettings().RuleTokenReferenceThreshold
                    : 0;
                EditorFileResult ef = AntigravityConfigService.ReadAllowedCustomizationFile(
                    _host.GeminiPath, _host.HomePath, _host.RepoPath, filePath, refThreshold);
                if (!ef.Ok)
                {
                    return FailCommand(ef.HttpStatusCode > 0 ? ef.HttpStatusCode : 400, ef.Error ?? "读取失败。");
                }
                Dictionary<string, object> okDict = new Dictionary<string, object>();
                okDict["ok"] = true;
                okDict["path"] = ef.CanonicalPath;
                okDict["content"] = ef.Content;
                okDict["charCount"] = ef.CharCount;
                okDict["estimatedTokens"] = ef.EstimatedTokens;
                okDict["overBudget"] = ef.OverBudget;
                return OperationResult<Dictionary<string, object>>.Success(okDict);
            });

            _dispatcher.Register("storage.analyze", delegate(Dictionary<string, object> args)
            {
                int topN = UnifiedJson.GetInt(args, "topN", StorageSlimService.DefaultTopOffendersLimit);
                double minAgeDays = ReadDoubleParam(args, "minAgeDays", StorageSlimService.DefaultMinAgeDays);
                double minSizeMb = ReadDoubleParam(args, "minSizeMb", StorageSlimService.DefaultMinSizeMb);
                StorageAnalysisReport report = StorageSlimService.AnalyzeStorage(_host.HomePath, _host.GeminiPath, topN, minAgeDays, minSizeMb);
                return OperationResult<Dictionary<string, object>>.Success(SerializeStorageAnalysisReport(report));
            });

            _dispatcher.Register("settings.companion.get", delegate(Dictionary<string, object> args)
            {
                return OperationResult<Dictionary<string, object>>.Success(BuildCompanionSettingsPayload());
            });

            _dispatcher.Register("snapshots.status", delegate(Dictionary<string, object> args)
            {
                return OperationResult<Dictionary<string, object>>.Success(BuildSnapshotStatusPayload());
            });

            _dispatcher.Register("vault.status", delegate(Dictionary<string, object> args)
            {
                return OperationResult<Dictionary<string, object>>.Success(BuildVaultStatusPayload());
            });

            // Core Write Commands (ACC-12)
            _dispatcher.Register("cockpit.settings", delegate(Dictionary<string, object> args)
            {
                return ExecuteCockpitSettingsCommand(args);
            });

            _dispatcher.Register("cockpit.toggle", delegate(Dictionary<string, object> args)
            {
                return ExecuteCockpitToggleCommand(args);
            });

            _dispatcher.Register("storage.slim", delegate(Dictionary<string, object> args)
            {
                return ExecuteStorageSlimCommand(args);
            });

            _dispatcher.Register("settings.companion.update", delegate(Dictionary<string, object> args)
            {
                return ExecuteSettingsCompanionUpdateCommand(args);
            });

            _dispatcher.Register("rename.commit", delegate(Dictionary<string, object> args)
            {
                return ExecuteRenameCommitCommand(args);
            });

            _dispatcher.Register("vault.push", delegate(Dictionary<string, object> args)
            {
                return ExecuteVaultPushCommand(args);
            });

            _dispatcher.Register("vault.import", delegate(Dictionary<string, object> args)
            {
                return ExecuteVaultImportCommand(args);
            });

            _dispatcher.Register("backup.full", delegate(Dictionary<string, object> args)
            {
                return ExecuteBackupFullCommand(args);
            });

            _dispatcher.Register("backup.import", delegate(Dictionary<string, object> args)
            {
                return ExecuteBackupImportCommand(args);
            });
        }

        public void RouteApi(NetworkStream stream, ParsedHttpRequest req, string path)
        {
            Dictionary<string, object> body = ParseJsonBody(req.BodyText);

            if (path == "/api/overview" && req.Method == "GET")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("overview.get", null);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return;
            }
            if (path == "/api/ide/status" && req.Method == "GET")
            {
                HttpProtocolHelper.WriteJsonResponse(stream, 200, OverviewBuilder.BuildIdeStatusDict());
                return;
            }
            if (path == "/api/ide/close" && req.Method == "POST")
            {
                // FIX P2-4: Graceful close only unless forceKill: true is explicitly passed
                bool forceKill = UnifiedJson.GetBool(body, "forceKill", false);
                string closeErr;
                bool ok = ProcessGuardService.CloseAntigravityProcesses(forceKill,
                    delegate(string msg) { _host.AppendLog("info", msg); }, out closeErr);
                if (!ok)
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, closeErr ?? "无法完全关闭 Antigravity 进程。");
                    return;
                }
                _host.AppendLog("success", "已安全关闭 Antigravity 相关进程。");
                Dictionary<string, object> d = OverviewBuilder.BuildIdeStatusDict();
                d["ok"] = true;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, d);
                return;
            }

            if (TryRouteSessions(stream, req, path, body)) return;
            if (TryRouteCockpit(stream, req, path, body)) return;
            if (TryRouteStorage(stream, req, path, body)) return;
            if (TryRouteVaultBackup(stream, req, path, body)) return;

            HttpProtocolHelper.WriteJsonError(stream, 404, "未知接口路由: " + path);
        }

        // ── Dispatcher Result & Error Helpers ──

        private static OperationResult<Dictionary<string, object>> FailCommand(int httpStatusCode, string errorMessage)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["ok"] = false;
            d["httpStatusCode"] = httpStatusCode;
            d["error"] = errorMessage ?? "请求失败";
            return OperationResult<Dictionary<string, object>>.FailWithData(d["error"] as string, d);
        }

        private static OperationResult<Dictionary<string, object>> FailJobBusy(string busyMessage)
        {
            string msg = string.IsNullOrEmpty(busyMessage) ? "当前有其他后台写入任务正在执行，请稍后再试。" : busyMessage;
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["ok"] = false;
            d["code"] = "JOB_BUSY";
            d["httpStatusCode"] = 409;
            d["error"] = msg;
            return OperationResult<Dictionary<string, object>>.FailWithData(msg, d);
        }

        private static OperationResult<Dictionary<string, object>> FailCommandPayload(
            int httpStatusCode,
            Dictionary<string, object> payload,
            string errorMessage)
        {
            Dictionary<string, object> d = payload != null
                ? new Dictionary<string, object>(payload, StringComparer.Ordinal)
                : new Dictionary<string, object>();
            if (!d.ContainsKey("ok")) d["ok"] = false;
            d["httpStatusCode"] = httpStatusCode;
            if (!d.ContainsKey("error")) d["error"] = errorMessage ?? "请求失败";
            return OperationResult<Dictionary<string, object>>.FailWithData(
                UnifiedJson.GetString(d, "error", errorMessage ?? "请求失败"), d);
        }

        private static void WriteDispatcherResult(NetworkStream stream, OperationResult<Dictionary<string, object>> res)
        {
            if (res == null)
            {
                HttpProtocolHelper.WriteJsonError(stream, 500, "内部命令返回空结果。");
                return;
            }
            if (!res.Ok)
            {
                Dictionary<string, object> data = res.Data;
                if (data != null)
                {
                    int statusCode = UnifiedJson.GetInt(data, "httpStatusCode", 500);
                    Dictionary<string, object> cleanErr = new Dictionary<string, object>(data, StringComparer.Ordinal);
                    cleanErr.Remove("httpStatusCode");
                    if (!cleanErr.ContainsKey("ok")) cleanErr["ok"] = false;
                    if (!cleanErr.ContainsKey("error")) cleanErr["error"] = res.ErrorMessage;
                    HttpProtocolHelper.WriteJsonResponse(stream, statusCode > 0 ? statusCode : 400, cleanErr);
                    return;
                }
                HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage);
                return;
            }

            Dictionary<string, object> okData = res.Data ?? new Dictionary<string, object>();
            int okStatus = UnifiedJson.GetInt(okData, "httpStatusCode", 200);
            if (okData.ContainsKey("httpStatusCode"))
            {
                Dictionary<string, object> cleanOk = new Dictionary<string, object>(okData, StringComparer.Ordinal);
                cleanOk.Remove("httpStatusCode");
                HttpProtocolHelper.WriteJsonResponse(stream, okStatus > 0 ? okStatus : 200, cleanOk);
                return;
            }
            HttpProtocolHelper.WriteJsonResponse(stream, okStatus > 0 ? okStatus : 200, okData);
        }

        // ── Path Validation Helpers (P2-5) ──

        internal static bool TryValidateCanonicalLocalPath(string rawPath, out string canonicalPath, out string error)
        {
            canonicalPath = null;
            error = null;

            if (string.IsNullOrWhiteSpace(rawPath))
            {
                error = "路径不能为空。";
                return false;
            }

            string trimmed = rawPath.Trim();
            for (int i = 0; i < trimmed.Length; i++)
            {
                char ch = trimmed[i];
                if (ch < 0x20 || ch == '"' || ch == '\0')
                {
                    error = "路径包含非法控制字符或引号。";
                    return false;
                }
            }

            if (trimmed.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                error = "路径包含非法文件名字符。";
                return false;
            }

            if (trimmed.StartsWith("\\\\", StringComparison.Ordinal) ||
                trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                error = "不允许使用网络共享或设备命名空间路径。";
                return false;
            }

            if (trimmed.Length < 3 ||
                !((trimmed[0] >= 'a' && trimmed[0] <= 'z') || (trimmed[0] >= 'A' && trimmed[0] <= 'Z')) ||
                trimmed[1] != ':' ||
                (trimmed[2] != '\\' && trimmed[2] != '/'))
            {
                error = "路径必须是完整的本地盘符绝对路径。";
                return false;
            }

            if (trimmed.IndexOf(':', 2) >= 0)
            {
                error = "路径包含非法的冒号或 NTFS 备用数据流标识。";
                return false;
            }

            string[] segs = trimmed.Substring(3).Split(new char[] { '\\', '/' }, StringSplitOptions.None);
            for (int i = 0; i < segs.Length; i++)
            {
                string seg = segs[i].Trim();
                if (seg == "." || seg == "..")
                {
                    error = "路径不允许包含相对跳转段 (. 或 ..)。";
                    return false;
                }
            }

            try
            {
                canonicalPath = Path.GetFullPath(trimmed);
                return true;
            }
            catch (Exception ex)
            {
                error = "路径解析失败: " + ex.Message;
                return false;
            }
        }

        internal static bool TryValidateExplorerTarget(
            string rawPath,
            IEnumerable<string> allowedRoots,
            out string canonicalPath,
            out string explorerArgs,
            out int httpErrorCode,
            out string error)
        {
            canonicalPath = null;
            explorerArgs = null;
            httpErrorCode = 400;

            if (!TryValidateCanonicalLocalPath(rawPath, out canonicalPath, out error))
            {
                httpErrorCode = 400;
                return false;
            }

            bool underAllowed = false;
            if (allowedRoots != null)
            {
                string normTarget = canonicalPath.TrimEnd('\\', '/');
                foreach (string root in allowedRoots)
                {
                    if (string.IsNullOrWhiteSpace(root)) continue;
                    try
                    {
                        string normRoot = Path.GetFullPath(root).TrimEnd('\\', '/');
                        if (string.Equals(normTarget, normRoot, StringComparison.OrdinalIgnoreCase) ||
                            SessionLifecycleService.IsPathContainedUnder(normRoot, normTarget))
                        {
                            underAllowed = true;
                            break;
                        }
                    }
                    catch { }
                }
            }

            if (!underAllowed)
            {
                httpErrorCode = 403;
                error = "拒绝访问工作区允许范围之外的目录。";
                return false;
            }

            bool isFile = File.Exists(canonicalPath);
            if (isFile)
            {
                explorerArgs = "/select,\"" + canonicalPath + "\"";
            }
            else
            {
                string dirTarget = canonicalPath.TrimEnd('\\', '/');
                if (dirTarget.Length == 2 && dirTarget[1] == ':')
                    explorerArgs = dirTarget + "\\";
                else
                    explorerArgs = "\"" + dirTarget + "\"";
            }
            return true;
        }

        private List<string> CollectAllowedExplorerRoots()
        {
            List<string> roots = new List<string>();
            if (!string.IsNullOrEmpty(_host.GeminiPath))
            {
                roots.Add(_host.GeminiPath);
                roots.Add(Path.Combine(_host.GeminiPath, "companion-cold-archives"));
            }
            if (!string.IsNullOrEmpty(_host.RepoPath)) roots.Add(_host.RepoPath);
            if (_host.SnapshotService != null)
            {
                string snapDir = _host.SnapshotService.GetSettings().TargetDirectory;
                if (!string.IsNullOrWhiteSpace(snapDir)) roots.Add(snapDir);
            }

            lock (_host.StateLock)
            {
                if (!string.IsNullOrEmpty(_host.LoadedBackupPathUnsafe))
                {
                    string dir = Path.GetDirectoryName(_host.LoadedBackupPathUnsafe);
                    if (!string.IsNullOrEmpty(dir)) roots.Add(dir);
                }
                foreach (KeyValuePair<string, ProjectMetadata> kvp in _host.LocalProjectsUnsafe)
                {
                    ProjectMetadata pm = kvp.Value;
                    if (pm == null) continue;
                    if (!string.IsNullOrEmpty(pm.FolderUri))
                    {
                        string dp = WorkspaceService.UriToDisplayPath(pm.FolderUri);
                        if (!string.IsNullOrEmpty(dp)) roots.Add(dp);
                    }
                    if (pm.FolderUris != null)
                    {
                        for (int i = 0; i < pm.FolderUris.Count; i++)
                        {
                            string dp = WorkspaceService.UriToDisplayPath(pm.FolderUris[i]);
                            if (!string.IsNullOrEmpty(dp)) roots.Add(dp);
                        }
                    }
                }
            }
            return roots;
        }

        // ── IDE & Rename Execution Helpers ──

        private bool EnsureIdeClosedIfNeededResult(
            bool closeIde,
            bool forceKill,
            out int errHttpCode,
            out Dictionary<string, object> errPayload)
        {
            errHttpCode = 200;
            errPayload = null;

            int p, ls;
            if (_host.IdeRunningOverride != null)
            {
                if (!_host.IdeRunningOverride()) return true;
                p = 1;
                ls = 0;
            }
            else
            {
                ProcessGuardService.GetRunningAntigravityProcessCounts(out p, out ls);
                if (p == 0 && ls == 0) return true;
            }
            if (!closeIde)
            {
                errHttpCode = 409;
                errPayload = new Dictionary<string, object>();
                errPayload["ok"] = false;
                errPayload["error"] = string.Format("检测到 {0} 个 Antigravity 主进程与 {1} 个语言服务进程正在运行，请确认关闭 IDE 后重试。", p, ls);
                errPayload["code"] = "IDE_RUNNING";
                errPayload["parentCount"] = p;
                errPayload["lsCount"] = ls;
                return false;
            }
            string closeErr;
            // FIX P1-11 & P2-4: Graceful close only unless forceKill == true!
            if (!ProcessGuardService.CloseAntigravityProcesses(forceKill, delegate(string m) { _host.AppendLog("info", m); }, out closeErr))
            {
                errHttpCode = 409;
                errPayload = new Dictionary<string, object>();
                errPayload["ok"] = false;
                errPayload["error"] = closeErr ?? "无法关闭 Antigravity 进程。";
                return false;
            }
            return true;
        }

        private bool EnsureIdeClosedIfNeeded(bool closeIde, bool forceKill, NetworkStream stream)
        {
            int errCode;
            Dictionary<string, object> errPayload;
            if (EnsureIdeClosedIfNeededResult(closeIde, forceKill, out errCode, out errPayload))
                return true;
            HttpProtocolHelper.WriteJsonResponse(stream, errCode, errPayload);
            return false;
        }

        private bool ResolveStrategyExecution(string strategyStr, bool forceKill, out bool hotWriteOnly, out int errCode, out string errMsg)
        {
            hotWriteOnly = false;
            errCode = 0;
            errMsg = null;

            int p, ls;
            if (_host.IdeRunningOverride != null)
            {
                if (!_host.IdeRunningOverride()) return true;
                p = 1;
                ls = 0;
            }
            else
            {
                ProcessGuardService.GetRunningAntigravityProcessCounts(out p, out ls);
                if (p == 0 && ls == 0) return true;
            }

            if (string.Equals(strategyStr, "HotWritePersistentLayers", StringComparison.OrdinalIgnoreCase))
            {
                hotWriteOnly = true;
                return true;
            }
            if (string.Equals(strategyStr, "CloseAndFullWrite", StringComparison.OrdinalIgnoreCase))
            {
                string closeErr;
                // FIX P1-11 & P2-4: Graceful close only unless forceKill == true!
                if (!ProcessGuardService.CloseAntigravityProcesses(forceKill, delegate(string m) { _host.AppendLog("info", m); }, out closeErr))
                {
                    errCode = 409;
                    errMsg = closeErr ?? "无法关闭 Antigravity 进程。";
                    return false;
                }
                hotWriteOnly = false;
                return true;
            }

            errCode = 409;
            errMsg = string.Format("IDE_RUNNING:{0}:{1}", p, ls);
            return false;
        }

        private static OperationResult<Dictionary<string, object>> BuildStrategyOrErrorFailure(int errCode, string errMsg)
        {
            if (!string.IsNullOrEmpty(errMsg) && errMsg.StartsWith("IDE_RUNNING:", StringComparison.Ordinal))
            {
                string[] parts = errMsg.Split(':');
                int p = 0, ls = 0;
                if (parts.Length >= 3)
                {
                    int.TryParse(parts[1], out p);
                    int.TryParse(parts[2], out ls);
                }
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["ok"] = false;
                d["code"] = "IDE_RUNNING";
                d["parentCount"] = p;
                d["lsCount"] = ls;
                d["error"] = string.Format("检测到 {0} 个 Antigravity 主进程与 {1} 个语言服务进程正在运行，请选择写入模式。", p, ls);
                return FailCommandPayload(errCode > 0 ? errCode : 409, d, d["error"] as string);
            }
            if (errCode == 409 && !string.IsNullOrEmpty(errMsg) && errMsg.IndexOf("其他后台任务", StringComparison.Ordinal) >= 0)
            {
                return FailJobBusy(errMsg);
            }
            return FailCommand(errCode > 0 ? errCode : 400, errMsg);
        }

        private bool ExecuteRenameInternal(
            Dictionary<string, string> cidToNewTitle,
            string strategyStr,
            bool forceKill,
            out RenameBatchResult result,
            out int errCode,
            out string errMsg)
        {
            result = null;
            errCode = 0;
            errMsg = null;
            if (!_host.TryAcquireJob("rename_execute", "正在执行三层会话重命名与快照保护..."))
            {
                errCode = 409;
                errMsg = "当前有其他后台任务正在执行，请稍后再试。";
                return false;
            }
            try
            {
                bool hotOnly;
                if (!ResolveStrategyExecution(strategyStr, forceKill, out hotOnly, out errCode, out errMsg))
                    return false;

                OperationResult<RenameBatchResult> opRes;
                lock (_host.MutationLock)
                {
                    opRes = SessionRenameService.ApplyRenameBatch(_host.GeminiPath, cidToNewTitle, hotOnly);
                    if (opRes.Ok) _host.ReloadAllLocalStateLocked();
                }

                result = opRes.Data;
                if (!opRes.Ok || result == null)
                {
                    errCode = 400;
                    errMsg = !string.IsNullOrEmpty(opRes.ErrorMessage) ? opRes.ErrorMessage : "重命名未能更新任何会话，请检查会话状态或标题格式。";
                    return false;
                }

                _host.AppendLog("success", string.Format("会话重命名完成：请求 {0} 条，实际 {1} 条，跳过运行中 {2} 条（L1={3}, L2={4}, L3={5}）。",
                    cidToNewTitle.Count, result.RenamedCount, result.SkippedRunningIds.Count, result.Layer1Updated, result.Layer2Updated, result.Layer3Updated));
                return true;
            }
            finally
            {
                _host.ReleaseJob();
            }
        }

        // ── Shared Selection & Parsing Helpers ──

        private static List<CapabilityItem> SelectCapsByKeys(List<CapabilityItem> pool, List<string> keys)
        {
            List<CapabilityItem> res = new List<CapabilityItem>();
            if (pool == null || keys == null || keys.Count == 0) return res;
            HashSet<string> set = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && set.Contains(pool[i].UniqueKey))
                    res.Add(pool[i]);
            }
            return res;
        }

        private static List<ConversationSummaryInfo> SelectSessionsByIds(List<ConversationSummaryInfo> pool, List<string> cids)
        {
            List<ConversationSummaryInfo> res = new List<ConversationSummaryInfo>();
            if (pool == null || cids == null || cids.Count == 0) return res;
            HashSet<string> set = new HashSet<string>(cids, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && set.Contains(pool[i].ConversationId))
                    res.Add(pool[i]);
            }
            return res;
        }

        private static Dictionary<string, object> ParseJsonBody(string bodyText)
        {
            if (string.IsNullOrWhiteSpace(bodyText))
                return new Dictionary<string, object>(StringComparer.Ordinal);
            object parsed;
            if (UnifiedJson.TryParse(bodyText, out parsed))
            {
                Dictionary<string, object> dict = UnifiedJson.AsDict(parsed);
                if (dict != null) return dict;
            }
            return new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private static List<string> ReadStringArray(Dictionary<string, object> dict, string key)
        {
            List<string> list = new List<string>();
            if (dict == null || string.IsNullOrEmpty(key)) return list;
            object val;
            if (!dict.TryGetValue(key, out val))
            {
                foreach (KeyValuePair<string, object> kvp in dict)
                {
                    if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        val = kvp.Value;
                        break;
                    }
                }
            }
            List<object> arr = UnifiedJson.AsList(val);
            if (arr == null) return list;
            for (int i = 0; i < arr.Count; i++)
            {
                string s = arr[i] as string;
                if (!string.IsNullOrWhiteSpace(s)) list.Add(s.Trim());
            }
            return list;
        }

        private static string SanitizeFileName(string name)
        {
            string s = string.IsNullOrWhiteSpace(name) ? "session" : name.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                s = s.Replace(invalid[i], '_');
            return s.Length > 80 ? s.Substring(0, 80) : s;
        }

        private static double ReadDoubleParam(Dictionary<string, object> dict, string key, double def)
        {
            if (dict == null || string.IsNullOrEmpty(key)) return def;
            object val = null;
            if (!dict.TryGetValue(key, out val))
            {
                foreach (KeyValuePair<string, object> kvp in dict)
                {
                    if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        val = kvp.Value;
                        break;
                    }
                }
            }
            if (val == null) return def;
            if (val is double) return (double)val;
            if (val is long) return (double)(long)val;
            if (val is int) return (double)(int)val;
            string s = val as string;
            double parsed;
            if (s != null && double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                return parsed;
            return def;
        }
    }
}
