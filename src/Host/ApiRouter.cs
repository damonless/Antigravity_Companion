using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;

namespace AntigravityCompanion
{
    /// <summary>
    /// Routes authenticated HTTP API requests through CompanionCommandDispatcher &amp; domain services.
    /// Hardened for P0-7, P0-8, P1-4, P1-10, P1-11, P2-3, P2-4, P2-5.
    /// Strictly C# 5.0 (.NET Framework 4.x).
    /// </summary>
    internal sealed class ApiRouter
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
                CockpitSnapshot snap = AntigravityConfigService.ScanCockpit(_host.GeminiPath, _host.HomePath, _host.RepoPath);
                return OperationResult<Dictionary<string, object>>.Success(
                    AntigravityConfigService.SerializeCockpitSummary(snap, _host.GeminiPath));
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
                EditorFileResult ef = AntigravityConfigService.ReadAllowedCustomizationFile(
                    _host.GeminiPath, _host.HomePath, _host.RepoPath, filePath);
                if (!ef.Ok)
                {
                    Dictionary<string, object> errDict = new Dictionary<string, object>();
                    errDict["ok"] = false;
                    errDict["httpStatusCode"] = ef.HttpStatusCode;
                    errDict["error"] = ef.Error;
                    return OperationResult<Dictionary<string, object>>.Success(errDict);
                }
                Dictionary<string, object> okDict = new Dictionary<string, object>();
                okDict["ok"] = true;
                okDict["httpStatusCode"] = 200;
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
        }

        public void RouteApi(NetworkStream stream, ParsedHttpRequest req, string path)
        {
            Dictionary<string, object> body = ParseJsonBody(req.BodyText);

            if (path == "/api/app/heartbeat" && req.Method == "POST")
            {
                _host.TouchActivity();
                HttpProtocolHelper.WriteOkJson(stream, null);
                return;
            }
            if (path == "/api/app/shutdown" && req.Method == "POST")
            {
                HttpProtocolHelper.WriteOkJson(stream, null);
                _host.RequestShutdown();
                return;
            }

            if (path == "/api/overview" && req.Method == "GET")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("overview.get", null);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return;
            }
            if (path == "/api/jobs/current" && req.Method == "GET")
            {
                lock (_host.StateLock)
                {
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, _host.GetCurrentJobStatusLocked());
                }
                return;
            }
            if (path == "/api/refresh" && req.Method == "POST")
            {
                lock (_host.MutationLock)
                {
                    _host.ReloadAllLocalStateLocked();
                    string loadedPathSnap;
                    lock (_host.StateLock)
                    {
                        loadedPathSnap = _host.LoadedBackupPathUnsafe;
                    }
                    if (!string.IsNullOrEmpty(loadedPathSnap) && File.Exists(loadedPathSnap))
                    {
                        _host.ReloadLoadedBackupLocked(loadedPathSnap);
                    }
                }
                _host.AppendLog("info", "已刷新本机三层数据与内置能力仓库状态。");
                HttpProtocolHelper.WriteJsonResponse(stream, 200, OverviewBuilder.BuildOverviewPayload(_host));
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

            if (path == "/api/gc/scan" && req.Method == "POST")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("gc.scan", body);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return;
            }
            if (path == "/api/gc/clean" && req.Method == "POST")
            {
                if (!_host.TryAcquireJob("gc_clean", "正在执行两阶段安全孤儿清理..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台写入任务正在执行，请稍后再试。");
                    return;
                }
                try
                {
                    bool closeIde = UnifiedJson.GetBool(body, "closeIdeIfRunning", false);
                    bool forceKill = UnifiedJson.GetBool(body, "forceKill", false);
                    DeletionResult delRes;
                    string err;
                    lock (_host.MutationLock)
                    {
                        bool ok = WorkspaceService.CleanOrphansSafe(_host.GeminiPath, closeIde, forceKill,
                            delegate(string m) { _host.AppendLog("info", m); }, out delRes, out err);
                        if (!ok)
                        {
                            HttpProtocolHelper.WriteJsonError(stream, 409, err ?? "孤儿清理未执行。");
                            return;
                        }
                        OrphanScanResult rescanned = WorkspaceService.ScanOrphansReadOnly(_host.GeminiPath);
                        lock (_host.StateLock)
                        {
                            _host.LastOrphanScanUnsafe = rescanned;
                        }
                        _host.ReloadAllLocalStateLocked();
                    }
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["deletedSessions"] = delRes.DeletedSessionCount;
                    resp["deletedDbFiles"] = delRes.DeletedDbFilesCount;
                    resp["deletedBrainDirs"] = delRes.DeletedBrainDirsCount;
                    resp["deletedAnnotations"] = delRes.DeletedAnnotationsCount;
                    resp["freedMegabytes"] = Math.Round(delRes.FreedMegabytes, 2);
                    _host.AppendLog("success", string.Format("孤儿清理完成：清理数据库 {0} 个、脑区 {1} 个、标注 {2} 个，释放 {3:F2} MB。",
                        delRes.DeletedDbFilesCount, delRes.DeletedBrainDirsCount, delRes.DeletedAnnotationsCount, delRes.FreedMegabytes));
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return;
            }

            if (path == "/api/sessions" && req.Method == "GET")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("sessions.list", null);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return;
            }
            if (path == "/api/sessions/search" && req.Method == "POST")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("sessions.search", body);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return;
            }
            if (path == "/api/sessions/markdown" && req.Method == "GET")
            {
                string cid;
                req.Query.TryGetValue("cid", out cid);
                string source;
                req.Query.TryGetValue("source", out source);
                bool fromLocal = !string.Equals(source, "backup", StringComparison.OrdinalIgnoreCase);

                if (!SessionMetadataExtractor.IsLikelyConversationId(cid))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "无效的会话 ID。");
                    return;
                }

                ConversationSummaryInfo info = FindSession(cid, fromLocal);
                ConversationSummaryInfo safeInfo = info ?? new ConversationSummaryInfo { ConversationId = cid.Trim() };
                string loadedBackupSnap;
                lock (_host.StateLock)
                {
                    loadedBackupSnap = _host.LoadedBackupPathUnsafe;
                }
                bool truncated;
                string md = fromLocal
                    ? TranscriptMarkdownExporter.PreviewLocalSessionMarkdown(safeInfo, _host.GeminiPath, 260000, out truncated)
                    : TranscriptMarkdownExporter.PreviewBackupSessionMarkdown(safeInfo, loadedBackupSnap, 260000, out truncated);

                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["cid"] = cid.Trim();
                resp["title"] = info != null ? (info.DisplayTitle ?? cid.Trim()) : cid.Trim();
                resp["projectName"] = info != null ? (info.ProjectName ?? string.Empty) : string.Empty;
                resp["stepCount"] = info != null ? info.StepCount : 0;
                resp["updatedTime"] = (info != null && info.UpdatedTime > DateTime.MinValue) ? info.UpdatedTime.ToString("yyyy-MM-dd HH:mm:ss") : "-";
                resp["isCli"] = info != null && info.IsCliSession;
                resp["truncated"] = truncated;
                resp["markdown"] = md ?? string.Empty;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/sessions/export-md" && req.Method == "POST")
            {
                string cid = UnifiedJson.GetString(body, "cid", string.Empty).Trim();
                string source = UnifiedJson.GetString(body, "source", "local");
                string outPath = UnifiedJson.GetString(body, "outputPath", string.Empty).Trim();
                bool fromLocal = !string.Equals(source, "backup", StringComparison.OrdinalIgnoreCase);
                if (!SessionMetadataExtractor.IsLikelyConversationId(cid))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "无效的会话 ID。");
                    return;
                }
                ConversationSummaryInfo info = FindSession(cid, fromLocal);
                ConversationSummaryInfo safeInfo = info ?? new ConversationSummaryInfo { ConversationId = cid };
                if (string.IsNullOrWhiteSpace(outPath))
                {
                    string safeTitle = SanitizeFileName(safeInfo.DisplayTitle ?? cid);
                    bool dlgBusy;
                    outPath = NativeDialogBridge.PickSaveFile("导出会话记录为 Markdown", "Markdown 文件 (*.md)|*.md|所有文件 (*.*)|*.*", safeTitle + ".md", out dlgBusy);
                    if (dlgBusy) { HttpProtocolHelper.WriteJsonError(stream, 409, "已有文件选择窗口正在打开。"); return; }
                    if (string.IsNullOrEmpty(outPath))
                    {
                        Dictionary<string, object> cancelled = new Dictionary<string, object>();
                        cancelled["cancelled"] = true;
                        HttpProtocolHelper.WriteJsonResponse(stream, 200, cancelled);
                        return;
                    }
                }
                else
                {
                    string pathErr;
                    if (!TryValidateCanonicalLocalPath(outPath, out outPath, out pathErr))
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 400, pathErr);
                        return;
                    }
                }

                string loadedBackupSnap;
                lock (_host.StateLock)
                {
                    loadedBackupSnap = _host.LoadedBackupPathUnsafe;
                }
                if (fromLocal)
                    TranscriptMarkdownExporter.ExportLocalSessionToMarkdown(safeInfo, _host.GeminiPath, outPath);
                else
                    TranscriptMarkdownExporter.ExportBackupSessionToMarkdown(safeInfo, loadedBackupSnap, outPath);
                _host.AppendLog("success", "已导出 Markdown 对话记录：" + outPath);
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = true;
                resp["outputPath"] = outPath;
                resp["truncated"] = false;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/sessions/rename-single" && req.Method == "POST")
            {
                string cid = UnifiedJson.GetString(body, "cid", string.Empty).Trim();
                string newTitle = UnifiedJson.GetString(body, "newTitle", string.Empty).Trim();
                string strategyStr = UnifiedJson.GetString(body, "strategy", "auto");
                bool forceKill = UnifiedJson.GetBool(body, "forceKill", false);
                if (!SessionMetadataExtractor.IsLikelyConversationId(cid) || string.IsNullOrWhiteSpace(newTitle))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "会话 ID 或新标题不能为空。");
                    return;
                }
                Dictionary<string, string> cidMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                cidMap[cid] = newTitle;
                RenameBatchResult res;
                int errCode;
                string errMsg;
                if (!ExecuteRenameInternal(cidMap, strategyStr, forceKill, out res, out errCode, out errMsg))
                {
                    HttpProtocolHelper.WriteStrategyOrError(stream, errCode, errMsg);
                    return;
                }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, SerializeBatchRenameResult(res));
                return;
            }
            if (path == "/api/sessions/move" && req.Method == "POST")
            {
                List<string> cids = ReadStringArray(body, "cids");
                string targetProjId = UnifiedJson.GetString(body, "targetProjectId", string.Empty).Trim();
                string targetProjName = UnifiedJson.GetString(body, "targetProjectName", string.Empty).Trim();
                string targetFolderPath = UnifiedJson.GetString(body, "targetFolderPath", string.Empty).Trim();
                bool closeIde = UnifiedJson.GetBool(body, "closeIdeIfRunning", false);
                bool forceKill = UnifiedJson.GetBool(body, "forceKill", false);

                if (cids.Count == 0 || string.IsNullOrWhiteSpace(targetProjName))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "请选择待移动的会话及目标工程名称。");
                    return;
                }

                // FIX P1-11: Acquire job BEFORE closing IDE
                if (!_host.TryAcquireJob("sessions_move", "正在迁移会话所属工程..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行，请稍后再试。");
                    return;
                }
                try
                {
                    if (!EnsureIdeClosedIfNeeded(closeIde, forceKill, stream)) return;

                    bool isOut = VisualDiffEngine.IsOutsideProject(targetProjName);
                    string normProjId = isOut ? "outside-of-project" : (string.IsNullOrEmpty(targetProjId) ? Guid.NewGuid().ToString() : targetProjId);
                    string normUri = isOut ? string.Empty : SessionLifecycleService.ToFileUri(targetFolderPath);

                    OperationResult<int> moveRes;
                    lock (_host.MutationLock)
                    {
                        moveRes = SessionLifecycleService.MoveSessionsToProjectResult(_host.GeminiPath, cids, targetProjName, normProjId, normUri);
                        if (moveRes.Ok) _host.ReloadAllLocalStateLocked();
                    }
                    if (!moveRes.Ok)
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 500, moveRes.ErrorMessage);
                        return;
                    }
                    string warn = (moveRes.Warnings != null && moveRes.Warnings.Count > 0)
                        ? string.Join("; ", moveRes.Warnings.ToArray())
                        : string.Empty;
                    _host.AppendLog("success", string.Format("已迁移 {0} 条会话至工程 [{1}]。", moveRes.Data, targetProjName));
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["moved"] = moveRes.Data;
                    resp["warning"] = warn;
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return;
            }
            if (path == "/api/sessions/delete" && req.Method == "POST")
            {
                List<string> cidArr = ReadStringArray(body, "cids");
                List<string> projIdArr = ReadStringArray(body, "projectIds");
                List<string> ghostArr = ReadStringArray(body, "ghostKeys");
                bool closeIde = UnifiedJson.GetBool(body, "closeIdeIfRunning", false);
                bool forceKill = UnifiedJson.GetBool(body, "forceKill", false);

                if (cidArr.Count == 0 && projIdArr.Count == 0 && ghostArr.Count == 0)
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "请先选择要删除的会话或工程。");
                    return;
                }

                // FIX P1-11: Acquire job BEFORE closing IDE
                if (!_host.TryAcquireJob("sessions_delete", "正在物理删除选中会话或工程..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行，请稍后再试。");
                    return;
                }
                try
                {
                    if (!EnsureIdeClosedIfNeeded(closeIde, forceKill, stream)) return;

                    OperationResult<DeletionResult> delOp;
                    lock (_host.MutationLock)
                    {
                        List<ConversationSummaryInfo> sumsSnap;
                        Dictionary<string, ProjectMetadata> projsSnap;
                        lock (_host.StateLock)
                        {
                            sumsSnap = new List<ConversationSummaryInfo>(_host.LocalSummariesUnsafe);
                            projsSnap = new Dictionary<string, ProjectMetadata>(_host.LocalProjectsUnsafe, StringComparer.OrdinalIgnoreCase);
                        }
                        HashSet<string> cidSet = new HashSet<string>(cidArr, StringComparer.OrdinalIgnoreCase);
                        HashSet<string> pIdSet = new HashSet<string>(projIdArr, StringComparer.OrdinalIgnoreCase);
                        HashSet<string> ghostSet = new HashSet<string>(ghostArr, StringComparer.OrdinalIgnoreCase);
                        delOp = SessionLifecycleService.DeleteSessionsAndProjectsResult(
                            _host.GeminiPath, sumsSnap, projsSnap, cidSet, null, pIdSet, ghostSet);
                        if (delOp.Ok) _host.ReloadAllLocalStateLocked();
                    }
                    if (!delOp.Ok)
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 500, delOp.ErrorMessage);
                        return;
                    }
                    DeletionResult delRes = delOp.Data;
                    string warn = (delOp.Warnings != null && delOp.Warnings.Count > 0)
                        ? string.Join("; ", delOp.Warnings.ToArray())
                        : string.Empty;
                    _host.AppendLog("success", string.Format("已物理删除 {0} 条会话（含子任务），释放 {1:F2} MB。", delRes.DeletedSessionCount, delRes.FreedMegabytes));
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["deletedSessions"] = delRes.DeletedSessionCount;
                    resp["freedMegabytes"] = Math.Round(delRes.FreedMegabytes, 2);
                    resp["warning"] = warn;
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return;
            }
            if (path == "/api/projects/relocate" && req.Method == "POST")
            {
                string projId = UnifiedJson.GetString(body, "projectId", string.Empty).Trim();
                string projName = UnifiedJson.GetString(body, "projectName", string.Empty).Trim();
                string newFolderPath = UnifiedJson.GetString(body, "newFolderPath", string.Empty).Trim();
                bool closeIde = UnifiedJson.GetBool(body, "closeIdeIfRunning", false);
                bool forceKill = UnifiedJson.GetBool(body, "forceKill", false);

                if (string.IsNullOrEmpty(projName) || string.IsNullOrEmpty(newFolderPath))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "工程名称与新物理目录不能为空。");
                    return;
                }

                // FIX P1-11: Acquire job BEFORE closing IDE
                if (!_host.TryAcquireJob("projects_relocate", "正在重定位工程物理目录..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行，请稍后再试。");
                    return;
                }
                try
                {
                    if (!EnsureIdeClosedIfNeeded(closeIde, forceKill, stream)) return;

                    OperationResult<int> relocOp;
                    lock (_host.MutationLock)
                    {
                        List<ConversationSummaryInfo> sumsSnap;
                        lock (_host.StateLock)
                        {
                            sumsSnap = new List<ConversationSummaryInfo>(_host.LocalSummariesUnsafe);
                        }
                        relocOp = SessionLifecycleService.RelocateProjectPathResult(_host.GeminiPath, projId, projName, newFolderPath, sumsSnap);
                        if (relocOp.Ok) _host.ReloadAllLocalStateLocked();
                    }
                    if (!relocOp.Ok)
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 500, relocOp.ErrorMessage);
                        return;
                    }
                    string warn = (relocOp.Warnings != null && relocOp.Warnings.Count > 0)
                        ? string.Join("; ", relocOp.Warnings.ToArray())
                        : string.Empty;
                    _host.AppendLog("success", string.Format("工程 [{0}] 物理路径已重定向至 {1}（同步 {2} 条会话）。", projName, newFolderPath, relocOp.Data));
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["updated"] = relocOp.Data;
                    resp["warning"] = warn;
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return;
            }

            if (path == "/api/rename/candidates" && req.Method == "GET")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("rename.candidates", null);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return;
            }
            if (path == "/api/rename/execute" && req.Method == "POST")
            {
                string strategyStr = UnifiedJson.GetString(body, "strategy", "auto");
                bool forceKill = UnifiedJson.GetBool(body, "forceKill", false);
                Dictionary<string, string> cidMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                object itemsObj;
                if (body != null && body.TryGetValue("items", out itemsObj))
                {
                    List<object> list = UnifiedJson.AsList(itemsObj);
                    if (list != null)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            Dictionary<string, object> row = UnifiedJson.AsDict(list[i]);
                            if (row == null) continue;
                            string cid = UnifiedJson.GetString(row, "cid", string.Empty).Trim();
                            string newTitle = UnifiedJson.GetString(row, "newTitle", string.Empty).Trim();
                            if (!string.IsNullOrEmpty(cid) && !string.IsNullOrEmpty(newTitle))
                                cidMap[cid] = newTitle;
                        }
                    }
                }
                if (cidMap.Count == 0)
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "未提交有效的重命名条目。");
                    return;
                }

                RenameBatchResult res;
                int errCode;
                string errMsg;
                if (!ExecuteRenameInternal(cidMap, strategyStr, forceKill, out res, out errCode, out errMsg))
                {
                    HttpProtocolHelper.WriteStrategyOrError(stream, errCode, errMsg);
                    return;
                }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, SerializeBatchRenameResult(res));
                return;
            }
            if (path == "/api/rename/ai-prompt" && req.Method == "POST")
            {
                List<string> cids = ReadStringArray(body, "cids");
                List<SessionRenameCandidate> selectedCands = BuildSelectedCandidatesForAi(cids);
                if (selectedCands.Count == 0)
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "请先勾选至少 1 条会话再生成 AI 润色提示词。");
                    return;
                }
                string prompt = AiPolishPromptExchange.BuildPolishPrompt(selectedCands);
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["count"] = selectedCands.Count;
                resp["prompt"] = prompt;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/rename/ai-save-prompt" && req.Method == "POST")
            {
                List<string> cids = ReadStringArray(body, "cids");
                string outPath = UnifiedJson.GetString(body, "outputPath", string.Empty).Trim();
                List<SessionRenameCandidate> selectedCands = BuildSelectedCandidatesForAi(cids);
                if (selectedCands.Count == 0)
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "请先勾选至少 1 条会话再导出 AI 提示词。");
                    return;
                }
                if (string.IsNullOrEmpty(outPath))
                {
                    bool dlgBusy;
                    outPath = NativeDialogBridge.PickSaveFile("保存 AI 润色提示词 (.txt)", "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*", "ai_polish_prompt.txt", out dlgBusy);
                    if (dlgBusy) { HttpProtocolHelper.WriteJsonError(stream, 409, "已有文件选择窗口正在打开。"); return; }
                    if (string.IsNullOrEmpty(outPath))
                    {
                        Dictionary<string, object> cancelled = new Dictionary<string, object>();
                        cancelled["cancelled"] = true;
                        HttpProtocolHelper.WriteJsonResponse(stream, 200, cancelled);
                        return;
                    }
                }
                else
                {
                    string pathErr;
                    if (!TryValidateCanonicalLocalPath(outPath, out outPath, out pathErr))
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 400, pathErr);
                        return;
                    }
                }
                string prompt = AiPolishPromptExchange.BuildPolishPrompt(selectedCands);
                SessionLifecycleService.WriteUtf8TextAtomicWithBackup(outPath, prompt ?? string.Empty, false, null);
                _host.AppendLog("success", "已导出 AI 润色提示词：" + outPath);
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = true;
                resp["count"] = selectedCands.Count;
                resp["outputPath"] = outPath;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/rename/ai-parse" && req.Method == "POST")
            {
                string rawText = UnifiedJson.GetString(body, "rawText", string.Empty);
                List<string> cids = ReadStringArray(body, "cids");
                List<SessionRenameCandidate> allCands = BuildScopedAiCandidates(cids);
                Dictionary<string, string> parsedMap;
                string err;
                if (!AiPolishPromptExchange.ParseAiClipboardJson(rawText, allCands, out parsedMap, out err))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, err ?? "解析 AI JSON 失败。");
                    return;
                }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, BuildAiParseResponsePayload(parsedMap, allCands, null));
                return;
            }
            if (path == "/api/rename/ai-load-json" && req.Method == "POST")
            {
                string inPath = UnifiedJson.GetString(body, "inputPath", string.Empty).Trim();
                List<string> cids = ReadStringArray(body, "cids");
                if (string.IsNullOrEmpty(inPath))
                {
                    bool dlgBusy;
                    inPath = NativeDialogBridge.PickOpenFile("导入 AI 润色结果 JSON", "JSON / 文本文件 (*.json;*.txt)|*.json;*.txt|所有文件 (*.*)|*.*", out dlgBusy);
                    if (dlgBusy) { HttpProtocolHelper.WriteJsonError(stream, 409, "已有文件选择窗口正在打开。"); return; }
                    if (string.IsNullOrEmpty(inPath))
                    {
                        Dictionary<string, object> cancelled = new Dictionary<string, object>();
                        cancelled["cancelled"] = true;
                        HttpProtocolHelper.WriteJsonResponse(stream, 200, cancelled);
                        return;
                    }
                }
                else
                {
                    string pathErr;
                    if (!TryValidateCanonicalLocalPath(inPath, out inPath, out pathErr))
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 400, pathErr);
                        return;
                    }
                }
                if (!File.Exists(inPath))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 404, "未找到指定的 AI 结果文件: " + inPath);
                    return;
                }
                string rawText = SessionLifecycleService.ReadUtf8TextShared(inPath);
                List<SessionRenameCandidate> allCands = BuildScopedAiCandidates(cids);
                Dictionary<string, string> parsedMap;
                string err;
                if (!AiPolishPromptExchange.ParseAiClipboardJson(rawText, allCands, out parsedMap, out err))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, err ?? "解析 AI JSON 文件失败。");
                    return;
                }
                _host.AppendLog("info", string.Format("已从文件导入并解析 {0} 条 AI 润色标题：{1}", parsedMap.Count, Path.GetFileName(inPath)));
                HttpProtocolHelper.WriteJsonResponse(stream, 200, BuildAiParseResponsePayload(parsedMap, allCands, inPath));
                return;
            }
            if (path == "/api/rename/rules" && req.Method == "GET")
            {
                string err;
                RenameRulesConfig cfg = AutoTitleSynthesizer.LoadRules(_host.RepoPath, out err);
                string rulesFile = Path.Combine(_host.RepoPath, "rename_rules.json");
                string rawJson = File.Exists(rulesFile) ? SessionLifecycleService.ReadUtf8TextShared(rulesFile) : AutoTitleSynthesizer.SerializeRulesToJson(cfg);
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["rulesPath"] = rulesFile;
                resp["rawJson"] = rawJson;
                resp["rulesObj"] = SerializeRulesConfigObj(cfg);
                resp["warning"] = err ?? string.Empty;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/rename/rules" && req.Method == "POST")
            {
                if (!_host.TryAcquireJob("rename_rules", "正在保存会话命名规则..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行，请稍后再试。");
                    return;
                }
                try
                {
                    string rawJson = UnifiedJson.GetString(body, "rawJson", null);
                    RenameRulesConfig parsed;
                    string err;
                    string finalJson;

                    if (!string.IsNullOrWhiteSpace(rawJson))
                    {
                        if (!AutoTitleSynthesizer.TryParseRulesFromJson(rawJson, out parsed, out err) || parsed == null)
                        {
                            HttpProtocolHelper.WriteJsonError(stream, 400, err ?? "规则 JSON 校验失败。");
                            return;
                        }
                        finalJson = AutoTitleSynthesizer.SerializeRulesToJson(parsed);
                    }
                    else
                    {
                        object objVal;
                        if (body == null || !body.TryGetValue("rulesObj", out objVal) || UnifiedJson.AsDict(objVal) == null)
                        {
                            HttpProtocolHelper.WriteJsonError(stream, 400, "缺少 rulesObj 或 rawJson 参数。");
                            return;
                        }
                        finalJson = UnifiedJson.Serialize(objVal, true);
                        if (!AutoTitleSynthesizer.TryParseRulesFromJson(finalJson, out parsed, out err) || parsed == null)
                        {
                            HttpProtocolHelper.WriteJsonError(stream, 400, err ?? "表单规则校验失败。");
                            return;
                        }
                        finalJson = AutoTitleSynthesizer.SerializeRulesToJson(parsed);
                    }

                    lock (_host.MutationLock)
                    {
                        string saveErr;
                        if (!AutoTitleSynthesizer.ValidateAndSaveRules(_host.RepoPath, finalJson, out saveErr))
                        {
                            HttpProtocolHelper.WriteJsonError(stream, 500, saveErr ?? "保存 rename_rules.json 失败。");
                            return;
                        }
                    }
                    _host.AppendLog("success", "已保存并应用会话自动命名规则。");
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["rawJson"] = finalJson;
                    resp["rulesObj"] = SerializeRulesConfigObj(parsed);
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return;
            }
            if (path == "/api/rename/rules/test" && req.Method == "POST")
            {
                string rawJson = UnifiedJson.GetString(body, "rawJson", null);
                RenameRulesConfig cfg;
                string err;
                if (!string.IsNullOrWhiteSpace(rawJson))
                {
                    if (!AutoTitleSynthesizer.TryParseRulesFromJson(rawJson, out cfg, out err) || cfg == null)
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 400, err ?? "规则 JSON 无效。");
                        return;
                    }
                }
                else
                {
                    object objVal;
                    if (body != null && body.TryGetValue("rulesObj", out objVal) && UnifiedJson.AsDict(objVal) != null)
                    {
                        string tmp = UnifiedJson.Serialize(objVal, false);
                        if (!AutoTitleSynthesizer.TryParseRulesFromJson(tmp, out cfg, out err) || cfg == null)
                        {
                            HttpProtocolHelper.WriteJsonError(stream, 400, err ?? "表单规则无效。");
                            return;
                        }
                    }
                    else
                    {
                        cfg = AutoTitleSynthesizer.LoadRules(_host.RepoPath, out err);
                    }
                }

                int sampleLimit = 20;
                List<ConversationSummaryInfo> sumsSnap;
                lock (_host.StateLock)
                {
                    sumsSnap = new List<ConversationSummaryInfo>(_host.LocalSummariesUnsafe);
                }
                List<RuleSandboxItem> samples = AutoTitleSynthesizer.PreviewRulesOnSessions(cfg, sumsSnap, sampleLimit);
                List<object> outList = new List<object>();
                for (int i = 0; i < samples.Count; i++)
                {
                    RuleSandboxItem s = samples[i];
                    Dictionary<string, object> item = new Dictionary<string, object>();
                    item["cid"] = s.ConversationId;
                    item["projectName"] = s.ProjectName;
                    item["oldTitle"] = s.OldTitle;
                    item["suggestedPrefix"] = s.SuggestedPrefix;
                    item["suggestedTitle"] = s.SuggestedTitle;
                    item["isLowValue"] = s.IsLowValue;
                    outList.Add(item);
                }
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["samples"] = outList;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/rename/history" && req.Method == "GET")
            {
                List<RenameSnapshotInfo> snaps = RenameBackupManager.ListSnapshots(_host.GeminiPath);
                List<object> arr = new List<object>();
                for (int i = 0; i < snaps.Count; i++)
                {
                    RenameSnapshotInfo s = snaps[i];
                    Dictionary<string, object> d = new Dictionary<string, object>();
                    d["snapshotDir"] = s.SnapshotDir;
                    d["folderName"] = !string.IsNullOrEmpty(s.SnapshotName) ? s.SnapshotName : Path.GetFileName(s.SnapshotDir);
                    d["createdAtIso"] = s.CreatedAt ?? string.Empty;
                    d["itemCount"] = s.ItemCount;
                    d["hasPbBackup"] = s.HasPbBackup;

                    List<RenameSnapshotItem> items = s.Items ?? new List<RenameSnapshotItem>();
                    string previewSummary = string.Empty;
                    if (items.Count > 0 && items[0] != null)
                        previewSummary = (items[0].OldTitle ?? string.Empty) + " -> " + (items[0].NewTitle ?? string.Empty);
                    d["previewSummary"] = previewSummary;

                    List<object> itemArr = new List<object>();
                    for (int k = 0; k < items.Count; k++)
                    {
                        Dictionary<string, object> it = new Dictionary<string, object>();
                        it["cid"] = items[k].ConversationId;
                        it["projectName"] = items[k].ProjectName ?? string.Empty;
                        it["oldTitle"] = items[k].OldTitle ?? string.Empty;
                        it["newTitle"] = items[k].NewTitle ?? string.Empty;
                        itemArr.Add(it);
                    }
                    d["items"] = itemArr;
                    arr.Add(d);
                }
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["snapshots"] = arr;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/rename/rollback" && req.Method == "POST")
            {
                string snapshotDir = UnifiedJson.GetString(body, "snapshotDir", string.Empty).Trim();
                string strategyStr = UnifiedJson.GetString(body, "strategy", "auto");
                bool forceKill = UnifiedJson.GetBool(body, "forceKill", false);
                if (string.IsNullOrEmpty(snapshotDir) || !Directory.Exists(snapshotDir))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "未找到指定的重命名快照目录。");
                    return;
                }
                string backupRoot = RenameBackupManager.GetBackupRoot(_host.GeminiPath);
                if (!SessionLifecycleService.IsPathContainedUnder(backupRoot, snapshotDir))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 403, "拒绝访问越界的快照路径。");
                    return;
                }

                // FIX P1-11: Acquire job BEFORE closing IDE
                if (!_host.TryAcquireJob("rename_rollback", "正在执行三层快照回滚..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台写入任务正在执行。");
                    return;
                }
                try
                {
                    bool hotOnly;
                    int errCode;
                    string errMsg;
                    if (!ResolveStrategyExecution(strategyStr, forceKill, out hotOnly, out errCode, out errMsg))
                    {
                        HttpProtocolHelper.WriteStrategyOrError(stream, errCode, errMsg);
                        return;
                    }

                    RenameRollbackResult rbRes;
                    bool ok;
                    lock (_host.MutationLock)
                    {
                        ok = RenameBackupManager.RollbackSnapshot(_host.GeminiPath, snapshotDir, hotOnly, out rbRes);
                        if (ok) _host.ReloadAllLocalStateLocked();
                    }
                    if (!ok)
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 500, rbRes != null ? rbRes.ErrorMessage : "回滚失败。");
                        return;
                    }
                    _host.AppendLog("success", string.Format("回滚完成：恢复 .pbtxt {0} 条、删除新建 .pbtxt {1} 条，Layer 2 DB {2} 条，Layer 3 PB {3} 条。",
                        rbRes.RestoredPbtxtCount, rbRes.DeletedPbtxtCount, rbRes.RestoredDbCount, rbRes.RestoredPbCount));
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["restoredPbtxt"] = rbRes.RestoredPbtxtCount;
                    resp["deletedPbtxt"] = rbRes.DeletedPbtxtCount;
                    resp["skippedOrphanPbtxt"] = rbRes.SkippedOrphanPbtxtCount;
                    resp["restoredDb"] = rbRes.RestoredDbCount;
                    resp["restoredPb"] = rbRes.RestoredPbCount;
                    resp["pbSkippedDueToLock"] = rbRes.PbSkippedDueToLock;
                    resp["layer2Restored"] = rbRes.RestoredDbCount > 0;
                    resp["layer3Restored"] = rbRes.RestoredPbCount > 0;
                    resp["warnings"] = new List<object>(OverviewBuilder.ToObjList(rbRes.Warnings));
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return;
            }

            if (path == "/api/capabilities" && req.Method == "GET")
            {
                string src;
                req.Query.TryGetValue("source", out src);
                Dictionary<string, object> cmdArgs = new Dictionary<string, object>();
                cmdArgs["fromBackup"] = string.Equals(src, "backup", StringComparison.OrdinalIgnoreCase);
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("capabilities.list", cmdArgs);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return;
            }
            if (path == "/api/capabilities/diff" && req.Method == "GET")
            {
                string key, src;
                req.Query.TryGetValue("key", out key);
                req.Query.TryGetValue("source", out src);
                bool fromBackup = string.Equals(src, "backup", StringComparison.OrdinalIgnoreCase);

                CapabilityDiffNode node = FindCapDiffNode(key, fromBackup);
                if (node == null)
                {
                    HttpProtocolHelper.WriteJsonError(stream, 404, "未找到指定的插件/技能/规则/MCP/Hook 项。");
                    return;
                }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, OverviewBuilder.BuildCapabilityLineDiffPayload(node));
                return;
            }
            if (path == "/api/capabilities/push" && req.Method == "POST")
            {
                List<string> keys = ReadStringArray(body, "keys");
                if (keys.Count == 0) { HttpProtocolHelper.WriteJsonError(stream, 400, "请选择要存入内置仓库的能力项。"); return; }

                // FIX P1-11: Acquire job lock
                if (!_host.TryAcquireJob("capabilities_push", "正在将本地能力写入内置仓库..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行，请稍后再试。");
                    return;
                }
                try
                {
                    int written = 0;
                    lock (_host.MutationLock)
                    {
                        List<CapabilityItem> localSnap;
                        lock (_host.StateLock)
                        {
                            localSnap = new List<CapabilityItem>(_host.LocalCapsUnsafe);
                        }
                        List<CapabilityItem> selected = SelectCapsByKeys(localSnap, keys);
                        if (selected.Count == 0) { HttpProtocolHelper.WriteJsonError(stream, 404, "本机未找到所选能力项。"); return; }
                        written = CapabilityVaultService.PushCapabilitiesToRepository(_host.RepoPath, _host.HomePath, selected);
                        _host.UpdateBaselineForCapabilities(selected);
                        _host.ReloadAllLocalStateLocked();
                    }
                    _host.AppendLog("success", string.Format("已将 {0} 项本地能力写入内置仓库 Vault。", keys.Count));
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["written"] = written;
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return;
            }
            if (path == "/api/capabilities/import" && req.Method == "POST")
            {
                List<string> keys = ReadStringArray(body, "keys");
                string source = UnifiedJson.GetString(body, "source", "repo");
                bool fromBackup = string.Equals(source, "backup", StringComparison.OrdinalIgnoreCase);
                if (keys.Count == 0) { HttpProtocolHelper.WriteJsonError(stream, 400, "请选择要导入本地的能力项。"); return; }

                // FIX P1-11: Acquire job lock
                if (!_host.TryAcquireJob("capabilities_import", "正在按 Key 级无损导入能力配置..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行，请稍后再试。");
                    return;
                }
                try
                {
                    int written = 0;
                    lock (_host.MutationLock)
                    {
                        List<CapabilityItem> poolSnap;
                        lock (_host.StateLock)
                        {
                            if (fromBackup && _host.BackupIndexUnsafe != null && _host.BackupIndexUnsafe.Capabilities != null)
                                poolSnap = new List<CapabilityItem>(_host.BackupIndexUnsafe.Capabilities);
                            else if (!fromBackup)
                                poolSnap = new List<CapabilityItem>(_host.RepoCapsUnsafe);
                            else
                                poolSnap = new List<CapabilityItem>();
                        }
                        List<CapabilityItem> selected = SelectCapsByKeys(poolSnap, keys);
                        if (selected.Count == 0) { HttpProtocolHelper.WriteJsonError(stream, 404, "目标来源未找到所选能力项。"); return; }
                        written = CapabilityVaultService.ImportCapabilitiesToLocal(_host.GeminiPath, _host.HomePath, selected);
                        if (!fromBackup) _host.UpdateBaselineForCapabilities(selected);
                        _host.ReloadAllLocalStateLocked();
                    }
                    _host.AppendLog("success", string.Format("已按 Key 级无损导入 {0} 项能力至本机 .gemini 目录。", keys.Count));
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["written"] = written;
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return;
            }
            if (path == "/api/capabilities/delete-local" && req.Method == "POST")
            {
                List<string> keys = ReadStringArray(body, "keys");
                if (keys.Count == 0) { HttpProtocolHelper.WriteJsonError(stream, 400, "请选择要删除的本机能力项。"); return; }

                // FIX P1-11: Acquire job lock
                if (!_host.TryAcquireJob("capabilities_delete_local", "正在删除本机能力配置..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行，请稍后再试。");
                    return;
                }
                try
                {
                    int deleted = 0;
                    lock (_host.MutationLock)
                    {
                        List<CapabilityItem> localSnap;
                        lock (_host.StateLock)
                        {
                            localSnap = new List<CapabilityItem>(_host.LocalCapsUnsafe);
                        }
                        List<CapabilityItem> selected = SelectCapsByKeys(localSnap, keys);
                        deleted = CapabilityVaultService.DeleteCapabilitiesFromLocal(_host.GeminiPath, selected);
                        _host.ReloadAllLocalStateLocked();
                    }
                    _host.AppendLog("warn", string.Format("已从本机删除 {0} 项能力配置（已生成 .bak.*）。", deleted));
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["deleted"] = deleted;
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return;
            }
            if (path == "/api/capabilities/delete-repo" && req.Method == "POST")
            {
                List<string> keys = ReadStringArray(body, "keys");
                if (keys.Count == 0) { HttpProtocolHelper.WriteJsonError(stream, 400, "请选择要从仓库移除的能力项。"); return; }

                // FIX P1-11: Acquire job lock
                if (!_host.TryAcquireJob("capabilities_delete_repo", "正在从内置仓库移除能力快照..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行，请稍后再试。");
                    return;
                }
                try
                {
                    int deleted = 0;
                    lock (_host.MutationLock)
                    {
                        List<CapabilityItem> repoSnap;
                        lock (_host.StateLock)
                        {
                            repoSnap = new List<CapabilityItem>(_host.RepoCapsUnsafe);
                        }
                        List<CapabilityItem> selected = SelectCapsByKeys(repoSnap, keys);
                        deleted = CapabilityVaultService.DeleteCapabilitiesFromRepository(_host.RepoPath, selected);
                        _host.ReloadAllLocalStateLocked();
                    }
                    _host.AppendLog("warn", string.Format("已从内置仓库移除 {0} 项能力快照。", deleted));
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["deleted"] = deleted;
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return;
            }
            if (path == "/api/capabilities/relocate" && req.Method == "POST")
            {
                string key = UnifiedJson.GetString(body, "key", string.Empty).Trim();
                string scope = UnifiedJson.GetString(body, "scope", string.Empty);
                if (string.IsNullOrWhiteSpace(scope))
                    scope = UnifiedJson.GetString(body, "target", "local");
                string oldPath = UnifiedJson.GetString(body, "oldPath", string.Empty).Trim();
                string newPath = UnifiedJson.GetString(body, "newPath", string.Empty).Trim();
                bool isLocalTarget = !string.Equals(scope, "repo", StringComparison.OrdinalIgnoreCase);

                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "能力标识、原路径及新目标路径均不能为空。");
                    return;
                }

                // FIX P1-11: Acquire job lock
                if (!_host.TryAcquireJob("capabilities_relocate", "正在重定位能力依赖路径..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行，请稍后再试。");
                    return;
                }
                try
                {
                    bool ok;
                    lock (_host.MutationLock)
                    {
                        List<CapabilityItem> poolSnap;
                        lock (_host.StateLock)
                        {
                            poolSnap = new List<CapabilityItem>(isLocalTarget ? _host.LocalCapsUnsafe : _host.RepoCapsUnsafe);
                        }
                        CapabilityItem cap = poolSnap.Find(delegate(CapabilityItem c) { return string.Equals(c.UniqueKey, key, StringComparison.OrdinalIgnoreCase); });
                        if (cap == null)
                        {
                            HttpProtocolHelper.WriteJsonError(stream, 404, "未找到目标 MCP/Hook 项。");
                            return;
                        }
                        ok = CapabilityVaultService.RelocateCapabilityPath(_host.GeminiPath, _host.RepoPath, isLocalTarget, _host.HomePath, cap, oldPath, newPath);
                        if (ok) _host.ReloadAllLocalStateLocked();
                    }
                    // FIX P1-4: Return HTTP 400 {ok:false, error:...} on relocate failure
                    if (!ok)
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 400, "未在目标配置中匹配到原路径，或新旧路径一致。");
                        return;
                    }
                    _host.AppendLog("success", string.Format("已重定向能力路径: {0} -> {1}", oldPath, newPath));
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return;
            }

            if (path == "/api/backup/current" && req.Method == "GET")
            {
                HttpProtocolHelper.WriteJsonResponse(stream, 200, OverviewBuilder.BuildBackupPayload(_host));
                return;
            }
            if (path == "/api/backup/open" && req.Method == "POST")
            {
                string agyPath = UnifiedJson.GetString(body, "archivePath", string.Empty).Trim();
                if (string.IsNullOrEmpty(agyPath))
                {
                    bool dlgBusy;
                    agyPath = NativeDialogBridge.PickOpenFile("打开 Antigravity 备份归档包 (.agy)", "Antigravity 备份包 (*.agy)|*.agy|ZIP 压缩包 (*.zip)|*.zip|所有文件 (*.*)|*.*", out dlgBusy);
                    if (dlgBusy) { HttpProtocolHelper.WriteJsonError(stream, 409, "已有文件选择窗口正在打开。"); return; }
                    if (string.IsNullOrEmpty(agyPath))
                    {
                        Dictionary<string, object> cancelled = new Dictionary<string, object>();
                        cancelled["cancelled"] = true;
                        HttpProtocolHelper.WriteJsonResponse(stream, 200, cancelled);
                        return;
                    }
                }
                else
                {
                    string pathErr;
                    if (!TryValidateCanonicalLocalPath(agyPath, out agyPath, out pathErr))
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 400, pathErr);
                        return;
                    }
                }
                if (!File.Exists(agyPath)) { HttpProtocolHelper.WriteJsonError(stream, 404, "归档文件不存在: " + agyPath); return; }

                lock (_host.MutationLock)
                {
                    _host.ReloadLoadedBackupLocked(agyPath);
                }
                _host.AppendLog("info", "已打开归档包：" + Path.GetFileName(agyPath));
                HttpProtocolHelper.WriteJsonResponse(stream, 200, OverviewBuilder.BuildBackupPayload(_host));
                return;
            }
            if (path == "/api/backup/full" && req.Method == "POST")
            {
                string outPath = UnifiedJson.GetString(body, "outputPath", string.Empty).Trim();
                bool closeIde = UnifiedJson.GetBool(body, "closeIdeIfRunning", false);
                bool forceKill = UnifiedJson.GetBool(body, "forceKill", false);
                string tierStr = UnifiedJson.GetString(body, "tier", "fast").Trim();
                BackupTier backupTier = (string.Equals(tierStr, "full", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(tierStr, "FullForensic", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(tierStr, "full_forensic", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(tierStr, "full-forensic", StringComparison.OrdinalIgnoreCase))
                    ? BackupTier.FullForensic
                    : BackupTier.FastDaily;

                if (string.IsNullOrEmpty(outPath))
                {
                    string defName = string.Format("Antigravity_FullBackup_{0}.agy", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                    bool dlgBusy;
                    outPath = NativeDialogBridge.PickSaveFile("保存全量备份 (.agy)", "Antigravity 备份包 (*.agy)|*.agy", defName, out dlgBusy);
                    if (dlgBusy) { HttpProtocolHelper.WriteJsonError(stream, 409, "已有文件选择窗口正在打开。"); return; }
                    if (string.IsNullOrEmpty(outPath))
                    {
                        Dictionary<string, object> cancelled = new Dictionary<string, object>();
                        cancelled["cancelled"] = true;
                        HttpProtocolHelper.WriteJsonResponse(stream, 200, cancelled);
                        return;
                    }
                }
                else
                {
                    string pathErr;
                    if (!TryValidateCanonicalLocalPath(outPath, out outPath, out pathErr))
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 400, pathErr);
                        return;
                    }
                }

                // FIX P1-11: Acquire job BEFORE closing IDE!
                if (!_host.TryAcquireJob("full_backup", "正在创建全量备份归档 (.agy)..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行。");
                    return;
                }
                if (!EnsureIdeClosedIfNeeded(closeIde, forceKill, stream))
                {
                    _host.ReleaseJob();
                    return;
                }

                string destSnap = outPath;
                List<ConversationSummaryInfo> sumSnap;
                lock (_host.StateLock)
                {
                    sumSnap = new List<ConversationSummaryInfo>(_host.LocalSummariesUnsafe);
                }
                _host.StartBackgroundJob("full_backup", delegate()
                {
                    int fileCount;
                    List<string> warnings;
                    lock (_host.MutationLock)
                    {
                        ArchiveTransferService.CreateFullBackup(_host.GeminiPath, _host.HomePath, sumSnap, destSnap, backupTier,
                            delegate(string msg, int pct) { _host.UpdateJobProgress("full_backup", msg, pct); },
                            out fileCount, out warnings);
                    }
                    Dictionary<string, object> res = new Dictionary<string, object>();
                    res["ok"] = true;
                    res["fileCount"] = fileCount;
                    res["outputPath"] = destSnap;
                    res["tier"] = backupTier == BackupTier.FullForensic ? "full" : "fast";
                    res["backupTier"] = backupTier.ToString();
                    res["warnings"] = new List<object>(OverviewBuilder.ToObjList(warnings));
                    _host.AppendLog("success", string.Format("全量备份完成（{0}，打包 {1} 文件）：{2}",
                        backupTier.ToString(), fileCount, destSnap));
                    return res;
                });

                Dictionary<string, object> ack = new Dictionary<string, object>();
                ack["accepted"] = true;
                ack["jobType"] = "full_backup";
                ack["outputPath"] = destSnap;
                ack["tier"] = backupTier == BackupTier.FullForensic ? "full" : "fast";
                HttpProtocolHelper.WriteJsonResponse(stream, 202, ack);
                return;
            }
            if (path == "/api/backup/selective-export" && req.Method == "POST")
            {
                List<string> cids = ReadStringArray(body, "cids");
                List<string> capKeys = ReadStringArray(body, "capKeys");
                bool includeGlobal = UnifiedJson.GetBool(body, "includeGlobalCaps", false);
                bool closeIde = UnifiedJson.GetBool(body, "closeIdeIfRunning", false);
                bool forceKill = UnifiedJson.GetBool(body, "forceKill", false);
                string outPath = UnifiedJson.GetString(body, "outputPath", string.Empty).Trim();
                string tierStr = UnifiedJson.GetString(body, "tier", "fast").Trim();
                BackupTier selTier = (string.Equals(tierStr, "full", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(tierStr, "FullForensic", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(tierStr, "full_forensic", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(tierStr, "full-forensic", StringComparison.OrdinalIgnoreCase))
                    ? BackupTier.FullForensic
                    : BackupTier.FastDaily;

                if (cids.Count == 0 && capKeys.Count == 0 && !includeGlobal)
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "请先选择要导出的会话或能力项。");
                    return;
                }
                if (string.IsNullOrEmpty(outPath))
                {
                    string defName = string.Format("Antigravity_Selective_{0}.agy", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                    bool dlgBusy;
                    outPath = NativeDialogBridge.PickSaveFile("导出所选至轻量归档 (.agy)", "Antigravity 备份包 (*.agy)|*.agy", defName, out dlgBusy);
                    if (dlgBusy) { HttpProtocolHelper.WriteJsonError(stream, 409, "已有文件选择窗口正在打开。"); return; }
                    if (string.IsNullOrEmpty(outPath))
                    {
                        Dictionary<string, object> cancelled = new Dictionary<string, object>();
                        cancelled["cancelled"] = true;
                        HttpProtocolHelper.WriteJsonResponse(stream, 200, cancelled);
                        return;
                    }
                }
                else
                {
                    string pathErr;
                    if (!TryValidateCanonicalLocalPath(outPath, out outPath, out pathErr))
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 400, pathErr);
                        return;
                    }
                }

                // FIX P1-11: Acquire job BEFORE closing IDE!
                if (!_host.TryAcquireJob("selective_export", "正在导出轻量归档 (.agy)..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行。");
                    return;
                }
                if (cids.Count > 0 && !EnsureIdeClosedIfNeeded(closeIde, forceKill, stream))
                {
                    _host.ReleaseJob();
                    return;
                }

                List<ConversationSummaryInfo> selConvs;
                List<CapabilityItem> selCaps;
                Dictionary<string, ProjectMetadata> projSnap;
                lock (_host.StateLock)
                {
                    selConvs = SelectSessionsByIds(_host.LocalSummariesUnsafe, cids);
                    selCaps = SelectCapsByKeys(_host.LocalCapsUnsafe, capKeys);
                    projSnap = new Dictionary<string, ProjectMetadata>(_host.LocalProjectsUnsafe, StringComparer.OrdinalIgnoreCase);
                }

                string destSnap = outPath;
                _host.StartBackgroundJob("selective_export", delegate()
                {
                    int written;
                    lock (_host.MutationLock)
                    {
                        _host.UpdateJobProgress("selective_export", "正在增量切片与压缩选择性 .agy 归档...", 40);
                        written = SessionLifecycleService.ExportSelectiveBackup(
                            _host.GeminiPath, _host.HomePath, destSnap, selConvs, projSnap, includeGlobal, selCaps, selTier);
                    }
                    Dictionary<string, object> res = new Dictionary<string, object>();
                    res["ok"] = true;
                    res["entriesWritten"] = written;
                    res["outputPath"] = destSnap;
                    res["tier"] = selTier == BackupTier.FullForensic ? "full" : "fast";
                    _host.AppendLog("success", string.Format("选择性 .agy 导出完成（写入 {0} 条目）：{1}", written, destSnap));
                    return res;
                });

                Dictionary<string, object> ack = new Dictionary<string, object>();
                ack["accepted"] = true;
                ack["jobType"] = "selective_export";
                ack["outputPath"] = destSnap;
                HttpProtocolHelper.WriteJsonResponse(stream, 202, ack);
                return;
            }
            if (path == "/api/backup/import" && req.Method == "POST")
            {
                List<string> cids = ReadStringArray(body, "cids");
                List<string> capKeys = ReadStringArray(body, "capKeys");
                bool closeIde = UnifiedJson.GetBool(body, "closeIdeIfRunning", false);
                bool forceKill = UnifiedJson.GetBool(body, "forceKill", false);
                bool includeUserSettings = UnifiedJson.GetBool(body, "includeUserSettings", false);
                bool restoreUserSettings = UnifiedJson.GetBool(body, "restoreUserSettings", false);

                if (cids.Count == 0 && capKeys.Count == 0 && !includeUserSettings && !restoreUserSettings)
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "请先选择要从归档包导入的能力或会话。");
                    return;
                }

                // Check backup loaded under _stateLock before acquiring job
                BackupArchiveIndex backupCheck;
                string backupPathCheck;
                lock (_host.StateLock)
                {
                    backupCheck = _host.BackupIndexUnsafe;
                    backupPathCheck = _host.LoadedBackupPathUnsafe;
                }
                if (backupCheck == null || string.IsNullOrEmpty(backupPathCheck))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "当前未打开任何 .agy 备份归档包。");
                    return;
                }

                // FIX P1-11: Acquire job BEFORE closing IDE!
                if (!_host.TryAcquireJob("selective_import", "正在执行三层选择性合流导入..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行。");
                    return;
                }
                if (cids.Count > 0 || includeUserSettings || restoreUserSettings)
                {
                    if (!EnsureIdeClosedIfNeeded(closeIde, forceKill, stream))
                    {
                        _host.ReleaseJob();
                        return;
                    }
                }

                SelectiveImportRequest importReq = new SelectiveImportRequest();
                lock (_host.StateLock)
                {
                    if (_host.BackupIndexUnsafe == null || string.IsNullOrEmpty(_host.LoadedBackupPathUnsafe))
                    {
                        _host.ReleaseJob();
                        HttpProtocolHelper.WriteJsonError(stream, 400, "当前未打开任何 .agy 备份归档包。");
                        return;
                    }
                    importReq.GeminiPath = _host.GeminiPath;
                    importReq.HomePath = _host.HomePath;
                    importReq.SourceMode = RightPaneSourceMode.BackupArchive;
                    importReq.LoadedBackupPath = _host.LoadedBackupPathUnsafe;
                    importReq.BackupIndex = _host.BackupIndexUnsafe;
                    importReq.LocalProjects = new Dictionary<string, ProjectMetadata>(_host.LocalProjectsUnsafe, StringComparer.OrdinalIgnoreCase);
                    importReq.CurrentDiffNodes = new List<ProjectDiffNode>(_host.BackupSessionDiffsUnsafe);
                    importReq.SelectedCapabilities = SelectCapsByKeys(_host.BackupIndexUnsafe.Capabilities, capKeys);
                    importReq.SelectedConversations = SelectSessionsByIds(_host.BackupIndexUnsafe.Conversations, cids);
                    importReq.ConvProjDiffMap = new Dictionary<string, ProjectDiffNode>(StringComparer.OrdinalIgnoreCase);
                    importReq.IncludeUserSettings = includeUserSettings;
                    importReq.RestoreUserSettings = restoreUserSettings;
                }

                _host.StartBackgroundJob("selective_import", delegate()
                {
                    OperationResult<SelectiveImportResult> impOp;
                    lock (_host.MutationLock)
                    {
                        impOp = ArchiveTransferService.PerformSelectiveImportResult(importReq,
                            delegate(string m, int pct) { _host.UpdateJobProgress("selective_import", m, pct); });
                        _host.ReloadAllLocalStateLocked();
                        if (!string.IsNullOrEmpty(importReq.LoadedBackupPath) && File.Exists(importReq.LoadedBackupPath))
                        {
                            _host.ReloadLoadedBackupLocked(importReq.LoadedBackupPath);
                        }
                    }

                    SelectiveImportResult impRes = impOp.Data;
                    string errMsg = !impOp.Ok ? impOp.ErrorMessage : (impRes != null ? (impRes.ErrorMessage ?? string.Empty) : string.Empty);
                    bool ok = impOp.Ok && string.IsNullOrEmpty(errMsg);
                    List<string> warnings = impOp.Warnings ?? (impRes != null ? impRes.Warnings : new List<string>());

                    Dictionary<string, object> res = new Dictionary<string, object>();
                    res["ok"] = ok;
                    res["filesImported"] = impRes != null ? impRes.FilesImported : 0;
                    res["capabilitiesImported"] = impRes != null ? impRes.CapabilitiesImported : 0;
                    res["conversationsImported"] = impRes != null ? impRes.ConversationsImported : 0;
                    res["warnings"] = new List<object>(OverviewBuilder.ToObjList(warnings));
                    res["errorMessage"] = errMsg ?? string.Empty;

                    if (ok)
                    {
                        _host.AppendLog("success", string.Format("备份包导入完成：能力 {0} 项、会话 {1} 条（写入 {2} 项）。",
                            res["capabilitiesImported"], res["conversationsImported"], res["filesImported"]));
                    }
                    else
                    {
                        _host.AppendLog("error", "备份包导入失败: " + errMsg);
                    }
                    return res;
                });

                Dictionary<string, object> ack = new Dictionary<string, object>();
                ack["accepted"] = true;
                ack["jobType"] = "selective_import";
                HttpProtocolHelper.WriteJsonResponse(stream, 202, ack);
                return;
            }

            if (path == "/api/native/pick-open-file" && req.Method == "POST")
            {
                string title = UnifiedJson.GetString(body, "title", "选择文件");
                string filter = UnifiedJson.GetString(body, "filter", "所有文件 (*.*)|*.*");
                bool busy;
                string selected = NativeDialogBridge.PickOpenFile(title, filter, out busy);
                if (busy) { HttpProtocolHelper.WriteJsonError(stream, 409, "已有文件选择弹窗处于打开状态。"); return; }
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["cancelled"] = string.IsNullOrEmpty(selected);
                resp["path"] = selected ?? string.Empty;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/native/pick-save-file" && req.Method == "POST")
            {
                string title = UnifiedJson.GetString(body, "title", "保存文件");
                string filter = UnifiedJson.GetString(body, "filter", "所有文件 (*.*)|*.*");
                string defName = UnifiedJson.GetString(body, "defaultFileName", string.Empty);
                bool busy;
                string selected = NativeDialogBridge.PickSaveFile(title, filter, defName, out busy);
                if (busy) { HttpProtocolHelper.WriteJsonError(stream, 409, "已有文件选择弹窗处于打开状态。"); return; }
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["cancelled"] = string.IsNullOrEmpty(selected);
                resp["path"] = selected ?? string.Empty;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/native/pick-folder" && req.Method == "POST")
            {
                string title = UnifiedJson.GetString(body, "title", "选择文件夹");
                string initialPath = UnifiedJson.GetString(body, "initialPath", _host.HomePath);
                bool busy;
                string selected = NativeDialogBridge.PickFolder(title, initialPath, out busy);
                if (busy) { HttpProtocolHelper.WriteJsonError(stream, 409, "已有目录选择弹窗处于打开状态。"); return; }
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["cancelled"] = string.IsNullOrEmpty(selected);
                resp["path"] = selected ?? string.Empty;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/native/open-in-explorer" && req.Method == "POST")
            {
                // FIX P2-5: Validate canonical path, reject traversal/unsafe inputs, escape explorer /select, argument
                string target = UnifiedJson.GetString(body, "path", string.Empty).Trim();
                List<string> allowedRoots = CollectAllowedExplorerRoots();
                string canonicalPath;
                string explorerArgs;
                int errHttpCode;
                string errMsg;
                if (!TryValidateExplorerTarget(target, allowedRoots, out canonicalPath, out explorerArgs, out errHttpCode, out errMsg))
                {
                    HttpProtocolHelper.WriteJsonError(stream, errHttpCode, errMsg);
                    return;
                }
                if (!File.Exists(canonicalPath) && !Directory.Exists(canonicalPath))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 404, "路径不存在: " + canonicalPath);
                    return;
                }
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "explorer.exe";
                psi.Arguments = explorerArgs;
                psi.UseShellExecute = false;
                using (Process.Start(psi)) { }
                HttpProtocolHelper.WriteOkJson(stream, null);
                return;
            }

            // ── Stage 2: Daily Cockpit, MCP Doctor, Strict Inline Editor & Workspace Launcher ──

            if (path == "/api/cockpit/summary" && req.Method == "GET")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("cockpit.summary", null);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return;
            }
            if (path == "/api/cockpit/settings" && req.Method == "POST")
            {
                if (!_host.TryAcquireJob("cockpit_settings", "正在保存 Antigravity 全局配置..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台写入任务正在执行，请稍后再试。");
                    return;
                }
                try
                {
                    Dictionary<string, object> updates = null;
                    object usObj;
                    if (body != null && body.TryGetValue("userSettings", out usObj))
                    {
                        updates = UnifiedJson.AsDict(usObj);
                        if (updates == null)
                        {
                            HttpProtocolHelper.WriteJsonError(stream, 400, "userSettings 必须是 JSON 对象。");
                            return;
                        }
                    }
                    else
                    {
                        updates = body;
                    }

                    if (updates == null || updates.Count == 0)
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 400, "未提交任何 userSettings 更新字段。");
                        return;
                    }

                    string bakPath;
                    string patchErr;
                    CockpitSnapshot afterSnap;
                    lock (_host.MutationLock)
                    {
                        if (!AntigravityConfigService.PatchUserSettings(_host.GeminiPath, updates, out bakPath, out patchErr))
                        {
                            HttpProtocolHelper.WriteJsonError(stream, 400, patchErr ?? "保存 userSettings 失败。");
                            return;
                        }
                        _host.ReloadAllLocalStateLocked();
                        afterSnap = AntigravityConfigService.ScanCockpit(_host.GeminiPath, _host.HomePath, _host.RepoPath);
                    }
                    _host.AppendLog("success", "已无损更新 config.json userSettings 配置。");
                    Dictionary<string, object> serialized = AntigravityConfigService.SerializeCockpitSummary(afterSnap, _host.GeminiPath);
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["backupPath"] = bakPath ?? string.Empty;
                    resp["userSettings"] = serialized["userSettings"];
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return;
            }
            if (path == "/api/cockpit/toggle" && req.Method == "POST")
            {
                string kind = UnifiedJson.GetString(body, "kind", string.Empty).Trim();
                string id = UnifiedJson.GetString(body, "id", string.Empty).Trim();
                string sourcePath = UnifiedJson.GetString(body, "sourcePath", string.Empty).Trim();
                if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(id))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "kind 与 id 不能为空。");
                    return;
                }

                bool? desiredEnabled = null;
                if (body != null && body.ContainsKey("enabled") && body["enabled"] != null)
                {
                    desiredEnabled = UnifiedJson.GetBool(body, "enabled", true);
                }

                if (!_host.TryAcquireJob("cockpit_toggle", "正在切换能力启用状态..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台写入任务正在执行，请稍后再试。");
                    return;
                }
                try
                {
                    bool resultingEnabled;
                    string targetFile;
                    string bakPath;
                    int httpCode;
                    string errMsg;
                    lock (_host.MutationLock)
                    {
                        if (!AntigravityConfigService.ToggleCapability(
                            _host.GeminiPath, kind, id, desiredEnabled, sourcePath,
                            out resultingEnabled, out targetFile, out bakPath, out httpCode, out errMsg))
                        {
                            HttpProtocolHelper.WriteJsonError(stream, httpCode > 0 ? httpCode : 400, errMsg ?? "切换启用状态失败。");
                            return;
                        }
                        _host.ReloadAllLocalStateLocked();
                    }
                    _host.AppendLog("success", string.Format("已切换 [{0}] {1} -> {2}", kind, id, resultingEnabled ? "启用" : "禁用"));
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["kind"] = kind;
                    resp["id"] = id;
                    resp["enabled"] = resultingEnabled;
                    resp["targetFile"] = targetFile ?? string.Empty;
                    resp["backupPath"] = bakPath ?? string.Empty;
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return;
            }
            if (path == "/api/mcp/doctor" && req.Method == "POST")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("mcp.doctor", body);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return;
            }
            if (path == "/api/editor/file" && req.Method == "GET")
            {
                string filePath;
                req.Query.TryGetValue("path", out filePath);
                Dictionary<string, object> args = new Dictionary<string, object>();
                args["path"] = filePath ?? string.Empty;
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("editor.read", args);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return; }

                int code = UnifiedJson.GetInt(res.Data, "httpStatusCode", 200);
                if (!UnifiedJson.GetBool(res.Data, "ok", false))
                {
                    HttpProtocolHelper.WriteJsonError(stream, code > 0 ? code : 400, UnifiedJson.GetString(res.Data, "error", "读取失败。"));
                    return;
                }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return;
            }
            if (path == "/api/editor/save" && req.Method == "POST")
            {
                string filePath = UnifiedJson.GetString(body, "path", string.Empty).Trim();
                if (string.IsNullOrEmpty(filePath) || body == null || !body.ContainsKey("content"))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "path 与 content 参数不能为空。");
                    return;
                }
                string content = body["content"] as string;
                if (content == null)
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "content 必须是非空类型的字符串。");
                    return;
                }

                if (!_host.TryAcquireJob("editor_save", "正在保存定制配置文件..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台写入任务正在执行，请稍后再试。");
                    return;
                }
                try
                {
                    EditorFileResult saveRes;
                    lock (_host.MutationLock)
                    {
                        saveRes = AntigravityConfigService.SaveAllowedCustomizationFile(
                            _host.GeminiPath, _host.HomePath, _host.RepoPath, filePath, content ?? string.Empty);
                        if (saveRes.Ok) _host.ReloadAllLocalStateLocked();
                    }
                    if (!saveRes.Ok)
                    {
                        HttpProtocolHelper.WriteJsonError(stream, saveRes.HttpStatusCode > 0 ? saveRes.HttpStatusCode : 400, saveRes.Error ?? "保存失败。");
                        return;
                    }
                    _host.AppendLog("success", string.Format("已保存配置文件：{0}（约 {1} tokens）", Path.GetFileName(saveRes.CanonicalPath), saveRes.EstimatedTokens));
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["path"] = saveRes.CanonicalPath;
                    resp["charCount"] = saveRes.CharCount;
                    resp["estimatedTokens"] = saveRes.EstimatedTokens;
                    resp["overBudget"] = saveRes.OverBudget;
                    resp["backupPath"] = saveRes.BackupPath ?? string.Empty;
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return;
            }
            if (path == "/api/workspace/launch" && req.Method == "POST")
            {
                string targetDir = UnifiedJson.GetString(body, "path", string.Empty).Trim();
                if (string.IsNullOrEmpty(targetDir)) targetDir = UnifiedJson.GetString(body, "folderPath", string.Empty).Trim();
                string toolMode = UnifiedJson.GetString(body, "tool", string.Empty).Trim();
                if (string.IsNullOrEmpty(toolMode)) toolMode = UnifiedJson.GetString(body, "mode", "ide").Trim();

                if (string.IsNullOrEmpty(targetDir))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "工作区目录 path 不能为空。");
                    return;
                }

                if (!_host.TryAcquireJob("workspace_launch", "正在启动工作区工具..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行，请稍后再试。");
                    return;
                }
                try
                {
                    WorkspaceLaunchResult launchRes = WorkspaceLauncherService.LaunchWorkspace(
                        _host.GeminiPath, _host.HomePath, _host.RepoPath, targetDir, toolMode);
                    if (!launchRes.Ok)
                    {
                        HttpProtocolHelper.WriteJsonError(stream, launchRes.HttpStatusCode > 0 ? launchRes.HttpStatusCode : 400, launchRes.Error ?? "启动失败。");
                        return;
                    }
                    _host.AppendLog("info", string.Format("已使用 [{0}] 打开工作区：{1}", launchRes.Tool, launchRes.LaunchedPath));
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["tool"] = launchRes.Tool;
                    resp["executable"] = launchRes.Executable;
                    resp["launchedPath"] = launchRes.LaunchedPath;
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return;
            }

            // ── Stage 3: Storage Slimming (5C / 5A / Complete GC) & Zero-Key `agy` CLI Adapter (3C) ──

            if (path == "/api/storage/analyze" && (req.Method == "GET" || req.Method == "POST"))
            {
                int topN = UnifiedJson.GetInt(body, "topN", StorageSlimService.DefaultTopOffendersLimit);
                double minAgeDays = ReadDoubleParam(body, "minAgeDays", StorageSlimService.DefaultMinAgeDays);
                double minSizeMb = ReadDoubleParam(body, "minSizeMb", StorageSlimService.DefaultMinSizeMb);

                if (req.Method == "GET" && req.Query != null)
                {
                    string qTop;
                    int parsedTop;
                    if (req.Query.TryGetValue("topN", out qTop) && int.TryParse(qTop, out parsedTop) && parsedTop > 0)
                        topN = parsedTop;
                    string qAge;
                    double parsedAge;
                    if (req.Query.TryGetValue("minAgeDays", out qAge) && double.TryParse(qAge, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedAge))
                        minAgeDays = parsedAge;
                    string qSize;
                    double parsedSize;
                    if (req.Query.TryGetValue("minSizeMb", out qSize) && double.TryParse(qSize, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedSize))
                        minSizeMb = parsedSize;
                }

                StorageAnalysisReport report = StorageSlimService.AnalyzeStorage(_host.HomePath, _host.GeminiPath, topN, minAgeDays, minSizeMb);
                HttpProtocolHelper.WriteJsonResponse(stream, 200, SerializeStorageAnalysisReport(report));
                return;
            }
            if (path == "/api/storage/slim" && req.Method == "POST")
            {
                if (!_host.TryAcquireJob("storage_slim", "正在执行存储瘦身与冷归档..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台写入任务正在执行，请稍后再试。");
                    return;
                }
                try
                {
                    bool closeIde = UnifiedJson.GetBool(body, "closeIdeIfRunning", false);
                    bool forceKill = UnifiedJson.GetBool(body, "forceKill", false);
                    if (closeIde && !EnsureIdeClosedIfNeeded(true, forceKill, stream))
                    {
                        return;
                    }

                    string modeStr = UnifiedJson.GetString(body, "mode", "liposuction").Trim();
                    StorageSlimMode slimMode = StorageSlimMode.Liposuction;
                    if (string.Equals(modeStr, "coldArchivePrune", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(modeStr, "cold_archive_prune", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(modeStr, "5a", StringComparison.OrdinalIgnoreCase))
                    {
                        slimMode = StorageSlimMode.ColdArchivePrune;
                    }
                    else if (string.Equals(modeStr, "orphanGcOnly", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(modeStr, "orphan_gc_only", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(modeStr, "gc", StringComparison.OrdinalIgnoreCase))
                    {
                        slimMode = StorageSlimMode.OrphanGcOnly;
                    }

                    StorageSlimRequest slimReq = new StorageSlimRequest();
                    slimReq.Mode = slimMode;
                    slimReq.TargetCids = ReadStringArray(body, "cids");
                    slimReq.MinAgeDays = ReadDoubleParam(body, "minAgeDays", StorageSlimService.DefaultMinAgeDays);
                    slimReq.MinSizeMb = ReadDoubleParam(body, "minSizeMb", StorageSlimService.DefaultMinSizeMb);
                    slimReq.RunPreFlight = body != null && body.ContainsKey("verifyPreFlight")
                        ? UnifiedJson.GetBool(body, "verifyPreFlight", true)
                        : UnifiedJson.GetBool(body, "runPreFlight", true);
                    slimReq.CleanOrphans = UnifiedJson.GetBool(body, "cleanOrphans", false);
                    slimReq.PruneLegacyCliArchive = UnifiedJson.GetBool(body, "pruneLegacyCliArchive", false);

                    StorageSlimResult slimRes;
                    lock (_host.MutationLock)
                    {
                        slimRes = StorageSlimService.ExecuteStorageSlim(_host.HomePath, _host.GeminiPath, slimReq);
                        if (slimRes.ProcessedCount > 0 || slimRes.DeletedOrphanCount > 0 || slimRes.LegacyCliPruned)
                        {
                            _host.ReloadAllLocalStateLocked();
                        }
                    }

                    if (!slimRes.Ok)
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 400, !string.IsNullOrEmpty(slimRes.ErrorMessage) ? slimRes.ErrorMessage : "存储瘦身执行失败。");
                        return;
                    }

                    _host.AppendLog("success", string.Format("存储瘦身完成（模式={0}，处理={1}，释放={2:F2} MB）。",
                        slimRes.Mode.ToString(), slimRes.ProcessedCount, slimRes.FreedMegabytes));
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, SerializeStorageSlimResult(slimRes));
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return;
            }
            if (path == "/api/rename/cli-status" && req.Method == "GET")
            {
                string customCliPath = string.Empty;
                if (req.Query != null)
                {
                    if (!req.Query.TryGetValue("customCliPath", out customCliPath))
                        req.Query.TryGetValue("path", out customCliPath);
                }
                CliAvailabilityResult avail = AntigravityCliAdapter.CheckCliAvailability(customCliPath);
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["available"] = avail.Available;
                resp["resolvedPath"] = avail.ResolvedPath ?? string.Empty;
                resp["versionOrHint"] = avail.VersionOrHint ?? string.Empty;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/rename/cli-suggest" && req.Method == "POST")
            {
                if (!_host.TryAcquireJob("rename_cli_suggest", "正在调用本地 agy CLI 生成重命名建议..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行，请稍后再试。");
                    return;
                }
                try
                {
                    List<string> cids = ReadStringArray(body, "cids");
                    string customCliPath = UnifiedJson.GetString(body, "customCliPath", string.Empty).Trim();
                    int timeoutSec = UnifiedJson.GetInt(body, "timeoutSec", AntigravityCliAdapter.DefaultTimeoutSeconds);

                    List<SessionRenameCandidate> batch;
                    if (cids.Count > 0)
                    {
                        batch = BuildSelectedCandidatesForAi(cids);
                    }
                    else
                    {
                        List<SessionRenameCandidate> all = OverviewBuilder.BuildAllCandidateObjects(_host);
                        batch = new List<SessionRenameCandidate>();
                        for (int i = 0; i < all.Count; i++)
                        {
                            if (all[i] != null && all[i].Selected)
                                batch.Add(all[i]);
                        }
                    }

                    if (batch.Count == 0)
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 400, "未找到可用的候选会话用于 CLI 智能建议。");
                        return;
                    }

                    CliBatchSuggestionResult cliRes = AntigravityCliAdapter.GenerateBatchSuggestionsViaCli(batch, customCliPath, timeoutSec);

                    Dictionary<string, string> oldTitleMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < batch.Count; i++)
                    {
                        if (batch[i] != null && !string.IsNullOrEmpty(batch[i].ConversationId))
                            oldTitleMap[batch[i].ConversationId] = batch[i].OldTitle ?? string.Empty;
                    }

                    List<object> sugList = new List<object>();
                    if (cliRes.Suggestions != null)
                    {
                        for (int i = 0; i < cliRes.Suggestions.Count; i++)
                        {
                            CliSessionSuggestion s = cliRes.Suggestions[i];
                            if (s == null) continue;
                            string oldT;
                            oldTitleMap.TryGetValue(s.Cid, out oldT);
                            Dictionary<string, object> row = new Dictionary<string, object>();
                            row["cid"] = s.Cid;
                            row["oldTitle"] = oldT ?? string.Empty;
                            row["title"] = s.Title;
                            row["newTitle"] = s.Title;
                            row["summary"] = s.Summary ?? string.Empty;
                            sugList.Add(row);
                        }
                    }

                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = cliRes.Ok;
                    resp["status"] = cliRes.Status.ToString();
                    resp["resolvedCliPath"] = cliRes.ResolvedCliPath ?? string.Empty;
                    resp["errorOrHint"] = cliRes.ErrorOrHint ?? string.Empty;
                    resp["count"] = sugList.Count;
                    resp["suggestions"] = sugList;
                    if (cliRes.Ok)
                        _host.AppendLog("success", string.Format("agy CLI 已生成 {0} 条会话命名建议。", sugList.Count));
                    else
                        _host.AppendLog("warn", "agy CLI 命名建议未成功: " + (cliRes.ErrorOrHint ?? cliRes.Status.ToString()));
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return;
            }

            // ── Stage 4: Companion Settings, Scheduled Snapshots & Portable Vault Switching ──

            if (path == "/api/settings/companion" && req.Method == "GET")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("settings.companion.get", null);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return;
            }
            if (path == "/api/settings/companion" && req.Method == "POST")
            {
                CompanionSettings prev = _host.SnapshotService.GetSettings();
                CompanionSettings next = prev.Clone();

                if (body.ContainsKey("enabled"))
                    next.Enabled = UnifiedJson.GetBool(body, "enabled", prev.Enabled);
                if (body.ContainsKey("intervalHours"))
                {
                    int hrs = UnifiedJson.GetInt(body, "intervalHours", prev.IntervalHours);
                    if (hrs < 1 || hrs > 720)
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 400, "快照间隔 intervalHours 必须在 1 到 720 小时之间。");
                        return;
                    }
                    next.IntervalHours = hrs;
                }
                if (body.ContainsKey("retainCount"))
                {
                    int ret = UnifiedJson.GetInt(body, "retainCount", prev.RetainCount);
                    if (ret < 1 || ret > 100)
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 400, "保留份数 retainCount 必须在 1 到 100 之间。");
                        return;
                    }
                    next.RetainCount = ret;
                }
                if (body.ContainsKey("targetDirectory"))
                {
                    string rawDir = UnifiedJson.GetString(body, "targetDirectory", string.Empty).Trim();
                    if (string.IsNullOrEmpty(rawDir))
                    {
                        next.TargetDirectory = ScheduledSnapshotService.GetDefaultSnapshotDirectory(_host.GeminiPath);
                    }
                    else
                    {
                        string canonDir;
                        string dirErr;
                        if (!TryValidateCanonicalLocalPath(rawDir, out canonDir, out dirErr))
                        {
                            HttpProtocolHelper.WriteJsonError(stream, 400, "快照目标目录无效: " + dirErr);
                            return;
                        }
                        next.TargetDirectory = canonDir;
                    }
                }
                if (body.ContainsKey("tier"))
                {
                    string t = UnifiedJson.GetString(body, "tier", "fast").Trim().ToLowerInvariant();
                    if (t != "fast" && t != "full")
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 400, "tier 仅支持 \"fast\" 或 \"full\"。");
                        return;
                    }
                    next.Tier = t;
                }
                if (body.ContainsKey("minimizeToTray"))
                    next.MinimizeToTray = UnifiedJson.GetBool(body, "minimizeToTray", prev.MinimizeToTray);
                if (body.ContainsKey("globalHotkey"))
                {
                    string hk = UnifiedJson.GetString(body, "globalHotkey", string.Empty).Trim();
                    if (string.IsNullOrEmpty(hk)) hk = "Ctrl+Alt+A";
                    uint mods, vk;
                    if (!TrayHost.TryParseHotkey(hk, out mods, out vk))
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 400, "全局热键格式无效，示例: Ctrl+Alt+A / Ctrl+Shift+K。");
                        return;
                    }
                    next.GlobalHotkey = hk;
                }
                if (body.ContainsKey("customAgyCliPath"))
                    next.CustomAgyCliPath = UnifiedJson.GetString(body, "customAgyCliPath", string.Empty).Trim();
                if (body.ContainsKey("vaultPath"))
                {
                    string vp = UnifiedJson.GetString(body, "vaultPath", string.Empty).Trim();
                    if (!string.IsNullOrEmpty(vp))
                    {
                        string canonVp;
                        string vpErr;
                        if (!TryValidateCanonicalLocalPath(vp, out canonVp, out vpErr))
                        {
                            HttpProtocolHelper.WriteJsonError(stream, 400, "Vault 路径无效: " + vpErr);
                            return;
                        }
                        next.VaultPath = canonVp;
                    }
                    else
                    {
                        next.VaultPath = string.Empty;
                    }
                }
                if (body.ContainsKey("startWithWindows"))
                    next.StartWithWindows = UnifiedJson.GetBool(body, "startWithWindows", prev.StartWithWindows);

                bool startupChanged = next.StartWithWindows != prev.StartWithWindows;
                bool hotkeyChanged = !string.Equals(next.GlobalHotkey, prev.GlobalHotkey, StringComparison.OrdinalIgnoreCase);

                CompanionSettings saved = _host.SnapshotService.UpdateSettings(next);
                if (startupChanged && _host.IsDefaultHome)
                {
                    ScheduledSnapshotService.SetStartWithWindows(saved.StartWithWindows, System.Windows.Forms.Application.ExecutablePath);
                }
                if (startupChanged || hotkeyChanged)
                {
                    _host.NotifySettingsChanged();
                }

                _host.AppendLog("success", "已保存 Companion 伴侣运行与快照设置。");
                Dictionary<string, object> resp = BuildCompanionSettingsPayload();
                resp["ok"] = true;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/snapshots/status" && req.Method == "GET")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("snapshots.status", null);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return;
            }
            if (path == "/api/snapshots/run-now" && req.Method == "POST")
            {
                SnapshotRunResult runRes = _host.SnapshotService.RunSnapshotNow();
                if (runRes.SkippedBusy)
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, runRes.ErrorMessage ?? "当前有其他后台任务正在执行，请稍后再试。");
                    return;
                }
                if (!runRes.Ok)
                {
                    HttpProtocolHelper.WriteJsonError(stream, 500, runRes.ErrorMessage ?? "立即快照失败。");
                    return;
                }

                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = true;
                resp["filePath"] = runRes.FilePath;
                resp["sizeBytes"] = runRes.SizeBytes;
                resp["sizeMegabytes"] = Math.Round(runRes.SizeBytes / (1024.0 * 1024.0), 2);
                resp["fileCount"] = runRes.FileCount;
                resp["deletedOldCount"] = runRes.DeletedOldCount;
                resp["runUtc"] = runRes.RunUtc;
                resp["warnings"] = new List<object>(OverviewBuilder.ToObjList(runRes.Warnings));
                HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/vault/status" && req.Method == "GET")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("vault.status", null);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return;
            }
            if (path == "/api/vault/select" && req.Method == "POST")
            {
                string rawVault = UnifiedJson.GetString(body, "vaultPath", string.Empty).Trim();
                if (string.IsNullOrEmpty(rawVault))
                {
                    bool dlgBusy;
                    rawVault = NativeDialogBridge.PickFolder("选择 Portable Vault 能力仓库目录", _host.RepoPath, out dlgBusy);
                    if (dlgBusy) { HttpProtocolHelper.WriteJsonError(stream, 409, "已有目录选择窗口正在打开。"); return; }
                    if (string.IsNullOrEmpty(rawVault))
                    {
                        Dictionary<string, object> cancelled = new Dictionary<string, object>();
                        cancelled["cancelled"] = true;
                        HttpProtocolHelper.WriteJsonResponse(stream, 200, cancelled);
                        return;
                    }
                }

                string canonVault;
                string vErr;
                if (!TryValidateCanonicalLocalPath(rawVault, out canonVault, out vErr))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "Vault 路径无效: " + vErr);
                    return;
                }

                lock (_host.MutationLock)
                {
                    string resolved = CapabilityVaultService.ResolveDefaultVaultPath(canonVault, _host.HomePath);
                    if (!Directory.Exists(resolved))
                        Directory.CreateDirectory(resolved);
                    _host.UpdateRepoPath(resolved);
                    CompanionSettings curCfg = _host.SnapshotService.GetSettings();
                    curCfg.VaultPath = resolved;
                    _host.SnapshotService.UpdateSettings(curCfg);
                    _host.ReloadAllLocalStateLocked();
                }

                _host.AppendLog("success", "已切换 Portable Vault 目录至：" + _host.RepoPath);
                Dictionary<string, object> resp = BuildVaultStatusPayload();
                resp["ok"] = true;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/vault/migrate-legacy" && req.Method == "POST")
            {
                string baseDir = ResolveEffectiveAppBaseDirForApi();
                string legacyRepoDir = Path.Combine(baseDir, "Repository");
                if (!Directory.Exists(legacyRepoDir))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 404, "未找到旧版 ./Repository 目录: " + legacyRepoDir);
                    return;
                }

                int migrated = 0;
                lock (_host.MutationLock)
                {
                    migrated = CapabilityVaultService.MigrateLegacyRepositoryToVault(legacyRepoDir, _host.RepoPath);
                    _host.ReloadAllLocalStateLocked();
                }
                _host.AppendLog("success", string.Format("已从旧版 ./Repository 迁移 {0} 个文件到当前 Vault。", migrated));
                Dictionary<string, object> resp = BuildVaultStatusPayload();
                resp["ok"] = true;
                resp["migratedFiles"] = migrated;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                return;
            }

            HttpProtocolHelper.WriteJsonError(stream, 404, "未知接口路由: " + path);
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
            if (!string.IsNullOrEmpty(_host.HomePath)) roots.Add(_host.HomePath);
            if (!string.IsNullOrEmpty(_host.GeminiPath)) roots.Add(_host.GeminiPath);
            if (!string.IsNullOrEmpty(_host.RepoPath)) roots.Add(_host.RepoPath);

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

        private bool EnsureIdeClosedIfNeeded(bool closeIde, bool forceKill, NetworkStream stream)
        {
            int p, ls;
            ProcessGuardService.GetRunningAntigravityProcessCounts(out p, out ls);
            if (p == 0 && ls == 0) return true;
            if (!closeIde)
            {
                Dictionary<string, object> errObj = new Dictionary<string, object>();
                errObj["error"] = string.Format("检测到 {0} 个 Antigravity 主进程与 {1} 个语言服务进程正在运行，请确认关闭 IDE 后重试。", p, ls);
                errObj["code"] = "IDE_RUNNING";
                errObj["parentCount"] = p;
                errObj["lsCount"] = ls;
                HttpProtocolHelper.WriteJsonResponse(stream, 409, errObj);
                return false;
            }
            string closeErr;
            // FIX P1-11 & P2-4: Graceful close only unless forceKill == true!
            if (!ProcessGuardService.CloseAntigravityProcesses(forceKill, delegate(string m) { _host.AppendLog("info", m); }, out closeErr))
            {
                HttpProtocolHelper.WriteJsonError(stream, 409, closeErr ?? "无法关闭 Antigravity 进程。");
                return false;
            }
            return true;
        }

        private bool ResolveStrategyExecution(string strategyStr, bool forceKill, out bool hotWriteOnly, out int errCode, out string errMsg)
        {
            hotWriteOnly = false;
            errCode = 0;
            errMsg = null;

            int p, ls;
            ProcessGuardService.GetRunningAntigravityProcessCounts(out p, out ls);
            if (p == 0 && ls == 0) return true;

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

        // ── Lookup & Serialization Helpers ──

        private List<SessionRenameCandidate> BuildScopedAiCandidates(List<string> cids)
        {
            List<SessionRenameCandidate> allCands = OverviewBuilder.BuildAllCandidateObjects(_host);
            if (cids != null && cids.Count > 0)
            {
                HashSet<string> set = new HashSet<string>(cids, StringComparer.OrdinalIgnoreCase);
                List<SessionRenameCandidate> scoped = new List<SessionRenameCandidate>();
                for (int i = 0; i < allCands.Count; i++)
                {
                    if (set.Contains(allCands[i].ConversationId))
                        scoped.Add(allCands[i]);
                }
                return scoped;
            }
            return allCands;
        }

        private List<SessionRenameCandidate> BuildSelectedCandidatesForAi(List<string> cids)
        {
            List<SessionRenameCandidate> all = OverviewBuilder.BuildAllCandidateObjects(_host);
            if (cids == null || cids.Count == 0) return new List<SessionRenameCandidate>();
            HashSet<string> set = new HashSet<string>(cids, StringComparer.OrdinalIgnoreCase);
            List<SessionRenameCandidate> res = new List<SessionRenameCandidate>();
            for (int i = 0; i < all.Count; i++)
            {
                if (set.Contains(all[i].ConversationId))
                    res.Add(all[i]);
            }
            return res;
        }

        private static Dictionary<string, object> BuildAiParseResponsePayload(
            Dictionary<string, string> parsedMap,
            List<SessionRenameCandidate> allCands,
            string inputPath)
        {
            List<object> changes = new List<object>();
            foreach (KeyValuePair<string, string> kvp in parsedMap)
            {
                SessionRenameCandidate cand = allCands.Find(delegate(SessionRenameCandidate x)
                {
                    return string.Equals(x.ConversationId, kvp.Key, StringComparison.OrdinalIgnoreCase);
                });
                Dictionary<string, object> ch = new Dictionary<string, object>();
                ch["cid"] = kvp.Key;
                ch["oldTitle"] = cand != null ? cand.OldTitle : string.Empty;
                ch["newTitle"] = kvp.Value;
                changes.Add(ch);
            }

            Dictionary<string, object> resp = new Dictionary<string, object>();
            resp["ok"] = true;
            resp["matchedCount"] = parsedMap.Count;
            resp["skippedCount"] = 0;
            resp["unmatchedIds"] = new List<object>();
            resp["changes"] = changes;
            if (!string.IsNullOrEmpty(inputPath)) resp["inputPath"] = inputPath;
            return resp;
        }

        private static Dictionary<string, object> SerializeRulesConfigObj(RenameRulesConfig cfg)
        {
            Dictionary<string, object> root = new Dictionary<string, object>();
            root["version"] = cfg.Version > 0 ? cfg.Version : 1;
            root["keepExistingBracketedPrefix"] = cfg.KeepExistingBracketedPrefix;
            Dictionary<string, object> aliases = new Dictionary<string, object>();
            if (cfg.ProjectAliases != null)
            {
                foreach (KeyValuePair<string, string> kvp in cfg.ProjectAliases)
                    aliases[kvp.Key] = kvp.Value ?? string.Empty;
            }
            root["projectAliases"] = aliases;

            List<object> multiList = new List<object>();
            if (cfg.MultiRepoRules != null)
            {
                for (int i = 0; i < cfg.MultiRepoRules.Count; i++)
                {
                    MultiRepoRule mr = cfg.MultiRepoRules[i];
                    Dictionary<string, object> mrObj = new Dictionary<string, object>();
                    mrObj["projectName"] = mr.ProjectName ?? string.Empty;
                    mrObj["defaultModel"] = mr.DefaultModel ?? string.Empty;
                    mrObj["branchRegexes"] = new List<object>(OverviewBuilder.ToObjList(mr.BranchRegexes));
                    List<object> mmList = new List<object>();
                    if (mr.ModelMatchers != null)
                    {
                        for (int k = 0; k < mr.ModelMatchers.Count; k++)
                        {
                            Dictionary<string, object> mm = new Dictionary<string, object>();
                            mm["model"] = mr.ModelMatchers[k].Model ?? string.Empty;
                            mm["keywords"] = new List<object>(OverviewBuilder.ToObjList(mr.ModelMatchers[k].Keywords));
                            mmList.Add(mm);
                        }
                    }
                    mrObj["modelMatchers"] = mmList;
                    multiList.Add(mrObj);
                }
            }
            root["multiRepoRules"] = multiList;
            return root;
        }

        private static Dictionary<string, object> SerializeBatchRenameResult(RenameBatchResult res)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["ok"] = res != null && res.RenamedCount > 0;
            if (res == null) return d;
            d["renamedCount"] = res.RenamedCount;
            d["layer1Updated"] = res.Layer1Updated;
            d["layer2Updated"] = res.Layer2Updated;
            d["layer3Updated"] = res.Layer3Updated;
            d["snapshotDir"] = res.BackupSnapshotDir ?? string.Empty;
            d["skippedRunning"] = new List<object>(OverviewBuilder.ToObjList(res.SkippedRunningIds));
            d["warnings"] = new List<object>(OverviewBuilder.ToObjList(res.Warnings));
            return d;
        }

        private ConversationSummaryInfo FindSession(string cid, bool fromLocal)
        {
            string trimmed = cid.Trim();
            lock (_host.StateLock)
            {
                List<ConversationSummaryInfo> list = fromLocal
                    ? _host.LocalSummariesUnsafe
                    : (_host.BackupIndexUnsafe != null ? _host.BackupIndexUnsafe.Conversations : null);
                if (list == null) return null;
                for (int i = 0; i < list.Count; i++)
                {
                    if (string.Equals(list[i].ConversationId, trimmed, StringComparison.OrdinalIgnoreCase))
                        return list[i];
                }
            }
            return null;
        }

        private CapabilityDiffNode FindCapDiffNode(string key, bool fromBackup)
        {
            if (string.IsNullOrEmpty(key)) return null;
            lock (_host.StateLock)
            {
                List<CapabilityDiffNode> list = fromBackup ? _host.BackupCapDiffsUnsafe : _host.CapDiffsUnsafe;
                for (int i = 0; i < list.Count; i++)
                {
                    if (string.Equals(list[i].UniqueKey, key.Trim(), StringComparison.OrdinalIgnoreCase))
                        return list[i];
                }
            }
            return null;
        }

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

        private static Dictionary<string, object> SerializeStorageAnalysisReport(StorageAnalysisReport r)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            if (r == null) return d;

            d["homePath"] = r.HomePath ?? string.Empty;
            d["geminiPath"] = r.GeminiPath ?? string.Empty;
            d["totalBytes"] = r.TotalBytes;
            d["totalMegabytes"] = Math.Round(r.TotalBytes / (1024.0 * 1024.0), 2);
            d["rootDbsBytes"] = r.RootDbsBytes;
            d["subagentDbsBytes"] = r.SubagentDbsBytes;
            d["brainDirsBytes"] = r.BrainDirsBytes;
            d["annotationsBytes"] = r.AnnotationsBytes;
            d["cliDbsBytes"] = r.CliDbsBytes;
            d["orphansBytes"] = r.OrphansBytes;
            d["legacyCliSummaryDbBytes"] = r.LegacyCliSummaryDbBytes;

            List<object> projs = new List<object>();
            if (r.Projects != null)
            {
                for (int i = 0; i < r.Projects.Count; i++)
                {
                    ProjectStorageBreakdown p = r.Projects[i];
                    Dictionary<string, object> pd = new Dictionary<string, object>();
                    pd["projectId"] = p.ProjectId ?? string.Empty;
                    pd["projectName"] = p.ProjectName ?? string.Empty;
                    pd["rootDbsBytes"] = p.RootDbsBytes;
                    pd["subagentDbsBytes"] = p.SubagentDbsBytes;
                    pd["brainDirsBytes"] = p.BrainDirsBytes;
                    pd["annotationsBytes"] = p.AnnotationsBytes;
                    pd["cliDbsBytes"] = p.CliDbsBytes;
                    pd["totalBytes"] = p.TotalBytes;
                    pd["sessionCount"] = p.SessionCount;
                    pd["subagentCount"] = p.SubagentCount;
                    projs.Add(pd);
                }
            }
            d["projects"] = projs;

            List<object> offs = new List<object>();
            if (r.TopOffenders != null)
            {
                for (int i = 0; i < r.TopOffenders.Count; i++)
                {
                    DbOffenderInfo o = r.TopOffenders[i];
                    Dictionary<string, object> od = new Dictionary<string, object>();
                    od["cid"] = o.ConversationId ?? string.Empty;
                    od["title"] = o.Title ?? string.Empty;
                    od["projectId"] = o.ProjectId ?? string.Empty;
                    od["projectName"] = o.ProjectName ?? string.Empty;
                    od["isSubagent"] = o.IsSubagent;
                    od["isCli"] = o.IsCli;
                    od["status"] = o.Status ?? string.Empty;
                    od["notFullyIdle"] = o.NotFullyIdle;
                    od["ageDays"] = Math.Round(o.AgeDays, 2);
                    od["dbBytes"] = o.DbBytes;
                    od["walBytes"] = o.WalBytes;
                    od["totalBytes"] = o.TotalBytes;
                    od["dbPath"] = o.DbPath ?? string.Empty;
                    od["hasTableBreakdown"] = o.HasTableBreakdown;
                    od["hasExecutorMetadataTable"] = o.HasExecutorMetadataTable;
                    od["executorMetadataRows"] = o.ExecutorMetadataRows;
                    od["executorMetadataBytes"] = o.ExecutorMetadataBytes;
                    od["stepsRows"] = o.StepsRows;
                    od["stepsBytes"] = o.StepsBytes;
                    od["eligibleForSlim"] = o.EligibleForSlim;
                    od["ineligibleReason"] = o.IneligibleReason ?? string.Empty;
                    offs.Add(od);
                }
            }
            d["topOffenders"] = offs;

            Dictionary<string, object> orph = new Dictionary<string, object>();
            if (r.Orphans != null)
            {
                orph["totalItems"] = r.Orphans.TotalOrphanItems;
                orph["totalBytes"] = r.Orphans.TotalOrphanBytes;
                orph["orphanDbFilesCount"] = r.Orphans.OrphanDbFiles.Count;
                orph["orphanDbBakCount"] = r.Orphans.OrphanDbBakFiles.Count;
                orph["orphanDbJournalCount"] = r.Orphans.OrphanDbJournalFiles.Count;
                orph["orphanBrainDirsCount"] = r.Orphans.OrphanBrainDirs.Count;
                orph["orphanAnnotationsCount"] = r.Orphans.OrphanAnnotationFiles.Count;
            }
            d["orphans"] = orph;

            Dictionary<string, object> leg = new Dictionary<string, object>();
            if (r.LegacyCliArchive != null)
            {
                leg["exists"] = r.LegacyCliArchive.Exists;
                leg["dbPath"] = r.LegacyCliArchive.DbPath ?? string.Empty;
                leg["totalBytes"] = r.LegacyCliArchive.TotalBytes;
                leg["entryCount"] = r.LegacyCliArchive.EntryCount;
            }
            d["legacyCliArchive"] = leg;
            d["warnings"] = new List<object>(OverviewBuilder.ToObjList(r.Warnings));
            return d;
        }

        private static Dictionary<string, object> SerializeStorageSlimResult(StorageSlimResult r)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            if (r == null) return d;

            d["ok"] = r.Ok;
            d["mode"] = r.Mode.ToString();
            d["coldArchivePath"] = r.ColdArchivePath ?? string.Empty;
            d["processedCount"] = r.ProcessedCount;
            d["skippedCount"] = r.SkippedCount;
            d["bytesBefore"] = r.BytesBefore;
            d["bytesAfter"] = r.BytesAfter;
            d["freedBytes"] = r.FreedBytes;
            d["freedMegabytes"] = Math.Round(r.FreedMegabytes, 2);
            d["preFlightPassed"] = r.PreFlightPassed;
            d["preFlightReport"] = r.PreFlightReport ?? string.Empty;
            d["deletedOrphanCount"] = r.DeletedOrphanCount;
            d["freedOrphanBytes"] = r.FreedOrphanBytes;
            d["legacyCliPruned"] = r.LegacyCliPruned;
            d["legacyCliArchivePath"] = r.LegacyCliArchivePath ?? string.Empty;
            d["errorMessage"] = r.ErrorMessage ?? string.Empty;
            d["warnings"] = new List<object>(OverviewBuilder.ToObjList(r.Warnings));

            List<object> items = new List<object>();
            if (r.Items != null)
            {
                for (int i = 0; i < r.Items.Count; i++)
                {
                    StorageSlimItemResult it = r.Items[i];
                    Dictionary<string, object> id = new Dictionary<string, object>();
                    id["cid"] = it.ConversationId ?? string.Empty;
                    id["dbPath"] = it.DbPath ?? string.Empty;
                    id["bytesBefore"] = it.BytesBefore;
                    id["bytesAfter"] = it.BytesAfter;
                    id["savedBytes"] = it.SavedBytes;
                    id["succeeded"] = it.Succeeded;
                    id["skippedReason"] = it.SkippedReason ?? string.Empty;
                    id["integrityCheck"] = it.IntegrityCheck ?? string.Empty;
                    items.Add(id);
                }
            }
            d["items"] = items;
            return d;
        }

        private Dictionary<string, object> BuildCompanionSettingsPayload()
        {
            CompanionSettings cfg = _host.SnapshotService.GetSettings();
            Dictionary<string, object> d = ScheduledSnapshotService.ToDictionary(cfg);
            d["settingsPath"] = _host.SnapshotService.SettingsFilePath ?? string.Empty;
            d["hotkeyRegistered"] = _host.HotkeyRegistered;
            d["hotkeyError"] = _host.HotkeyError ?? string.Empty;
            return d;
        }

        private Dictionary<string, object> BuildSnapshotStatusPayload()
        {
            SnapshotStatusReport r = _host.SnapshotService.GetStatus();
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["enabled"] = r.Enabled;
            d["intervalHours"] = r.IntervalHours;
            d["targetDirectory"] = r.TargetDirectory ?? string.Empty;
            d["retainCount"] = r.RetainCount;
            d["tier"] = r.Tier ?? "fast";
            d["lastRunUtc"] = r.LastRunUtc ?? string.Empty;
            d["lastFilePath"] = r.LastFilePath ?? string.Empty;
            d["lastSizeBytes"] = r.LastSizeBytes;
            d["lastSizeMegabytes"] = Math.Round(r.LastSizeBytes / (1024.0 * 1024.0), 2);
            d["lastError"] = r.LastError ?? string.Empty;
            d["isRunning"] = r.IsRunning;
            d["existingCount"] = r.ExistingCount;
            List<object> list = new List<object>(r.ExistingSnapshots.Count);
            for (int i = 0; i < r.ExistingSnapshots.Count; i++)
            {
                list.Add(r.ExistingSnapshots[i]);
            }
            d["existingSnapshots"] = list;
            return d;
        }

        private Dictionary<string, object> BuildVaultStatusPayload()
        {
            string baseDir = ResolveEffectiveAppBaseDirForApi();
            string legacyRepo = Path.Combine(baseDir, "Repository");
            string curVault = _host.RepoPath;
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["vaultPath"] = curVault ?? string.Empty;
            d["legacyRepoPath"] = legacyRepo;
            d["legacyRepoExists"] = Directory.Exists(legacyRepo);
            d["legacyMigrationNeeded"] = CapabilityVaultService.DetectLegacyRepositoryMigrationNeeded(baseDir, curVault);
            return d;
        }

        private static string ResolveEffectiveAppBaseDirForApi()
        {
            string baseDir = (AppDomain.CurrentDomain.BaseDirectory ?? string.Empty).TrimEnd('\\', '/');
            if (string.IsNullOrEmpty(baseDir)) return Directory.GetCurrentDirectory();
            string dirName = Path.GetFileName(baseDir);
            if (string.Equals(dirName, "tests", StringComparison.OrdinalIgnoreCase))
            {
                string parent = Path.GetDirectoryName(baseDir);
                if (!string.IsNullOrEmpty(parent)) baseDir = parent;
            }
            return baseDir;
        }
    }
}
