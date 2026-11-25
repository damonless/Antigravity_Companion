using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;

namespace AntigravityCompanion
{
    /// <summary>
    /// Sessions, Projects, Markdown Export, and Rename/AI/CLI partial of ApiRouter.
    /// Strictly C# 5.0 (.NET Framework 4.x).
    /// </summary>
    internal sealed partial class ApiRouter
    {
        private bool TryRouteSessions(NetworkStream stream, ParsedHttpRequest req, string path, Dictionary<string, object> body)
        {
            if (path == "/api/sessions" && req.Method == "GET")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("sessions.list", null);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return true; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return true;
            }
            if (path == "/api/sessions/search" && req.Method == "POST")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("sessions.search", body);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return true; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return true;
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
                    return true;
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
                return true;
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
                    return true;
                }
                ConversationSummaryInfo info = FindSession(cid, fromLocal);
                ConversationSummaryInfo safeInfo = info ?? new ConversationSummaryInfo { ConversationId = cid };
                if (string.IsNullOrWhiteSpace(outPath))
                {
                    string safeTitle = SanitizeFileName(safeInfo.DisplayTitle ?? cid);
                    bool dlgBusy;
                    outPath = NativeDialogBridge.PickSaveFile("导出会话记录为 Markdown", "Markdown 文件 (*.md)|*.md|所有文件 (*.*)|*.*", safeTitle + ".md", out dlgBusy);
                    if (dlgBusy) { HttpProtocolHelper.WriteJsonError(stream, 409, "已有文件选择窗口正在打开。"); return true; }
                    if (string.IsNullOrEmpty(outPath))
                    {
                        Dictionary<string, object> cancelled = new Dictionary<string, object>();
                        cancelled["cancelled"] = true;
                        HttpProtocolHelper.WriteJsonResponse(stream, 200, cancelled);
                        return true;
                    }
                }
                else
                {
                    string pathErr;
                    if (!TryValidateCanonicalLocalPath(outPath, out outPath, out pathErr))
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 400, pathErr);
                        return true;
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
                return true;
            }
            if (path == "/api/sessions/rename-single" && req.Method == "POST")
            {
                string cid = UnifiedJson.GetString(body, "cid", string.Empty).Trim();
                string newTitle = UnifiedJson.GetString(body, "newTitle", string.Empty).Trim();
                string strategyStr = UnifiedJson.GetString(body, "strategy", "auto");
                bool preferHotWrite = UnifiedJson.GetBool(body, "preferHotWrite", false);
                bool forceKill = UnifiedJson.GetBool(body, "forceKill", false);
                if (!SessionMetadataExtractor.IsLikelyConversationId(cid) || string.IsNullOrWhiteSpace(newTitle))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "对话 ID 或新标题不能为空。");
                    return true;
                }
                Dictionary<string, object> itemRow = new Dictionary<string, object>();
                itemRow["cid"] = cid;
                itemRow["newTitle"] = newTitle;
                Dictionary<string, object> cmdArgs = new Dictionary<string, object>();
                cmdArgs["strategy"] = strategyStr;
                cmdArgs["preferHotWrite"] = preferHotWrite;
                cmdArgs["forceKill"] = forceKill;
                cmdArgs["items"] = new List<object> { itemRow };
                WriteDispatcherResult(stream, _dispatcher.Execute("rename.commit", cmdArgs));
                return true;
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
                    return true;
                }

                // FIX P1-11: Acquire job BEFORE closing IDE
                if (!_host.TryAcquireJob("sessions_move", "正在迁移会话所属工程..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行，请稍后再试。");
                    return true;
                }
                try
                {
                    if (!EnsureIdeClosedIfNeeded(closeIde, forceKill, stream)) return true;

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
                        return true;
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
                return true;
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
                    return true;
                }

                // FIX P1-11: Acquire job BEFORE closing IDE
                if (!_host.TryAcquireJob("sessions_delete", "正在物理删除选中会话或工程..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行，请稍后再试。");
                    return true;
                }
                try
                {
                    if (!EnsureIdeClosedIfNeeded(closeIde, forceKill, stream)) return true;

                    OperationResult<DeletionResult> delOp;
                    string trashId = string.Empty;
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
                        delOp = TrashAndBackupService.SoftDeleteSessionsAndProjects(
                            _host.GeminiPath, sumsSnap, projsSnap, cidSet, null, pIdSet, ghostSet, out trashId);
                        if (delOp.Ok) _host.ReloadAllLocalStateLocked();
                    }
                    if (!delOp.Ok)
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 500, delOp.ErrorMessage);
                        return true;
                    }
                    DeletionResult delRes = delOp.Data;
                    string warn = (delOp.Warnings != null && delOp.Warnings.Count > 0)
                        ? string.Join("; ", delOp.Warnings.ToArray())
                        : string.Empty;
                    _host.AppendLog("success", string.Format("已将 {0} 条对话（含后台子任务）移入回收站，释放 {1:F2} MB。", delRes.DeletedSessionCount, delRes.FreedMegabytes));
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["deletedSessions"] = delRes.DeletedSessionCount;
                    resp["freedMegabytes"] = Math.Round(delRes.FreedMegabytes, 2);
                    resp["trashId"] = trashId ?? string.Empty;
                    resp["warning"] = warn;
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return true;
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
                    return true;
                }

                // FIX P1-11: Acquire job BEFORE closing IDE
                if (!_host.TryAcquireJob("projects_relocate", "正在重定位工程物理目录..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行，请稍后再试。");
                    return true;
                }
                try
                {
                    if (!EnsureIdeClosedIfNeeded(closeIde, forceKill, stream)) return true;

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
                        return true;
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
                return true;
            }

            if (path == "/api/rename/candidates" && req.Method == "GET")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("rename.candidates", null);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return true; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return true;
            }
            if (path == "/api/rename/execute" && req.Method == "POST")
            {
                WriteDispatcherResult(stream, _dispatcher.Execute("rename.commit", body));
                return true;
            }
            if (path == "/api/rename/flush-pending" && req.Method == "POST")
            {
                WriteDispatcherResult(stream, _dispatcher.Execute("rename.flush_pending", body));
                return true;
            }
            if (path == "/api/rename/ai-prompt" && req.Method == "POST")
            {
                List<string> cids = ReadStringArray(body, "cids");
                List<SessionRenameCandidate> selectedCands = BuildSelectedCandidatesForAi(cids);
                if (selectedCands.Count == 0)
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "请先勾选至少 1 条会话再生成 AI 润色提示词。");
                    return true;
                }
                string prompt = AiPolishPromptExchange.BuildPolishPrompt(selectedCands);
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["count"] = selectedCands.Count;
                resp["prompt"] = prompt;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                return true;
            }
            if (path == "/api/rename/ai-save-prompt" && req.Method == "POST")
            {
                List<string> cids = ReadStringArray(body, "cids");
                string outPath = UnifiedJson.GetString(body, "outputPath", string.Empty).Trim();
                List<SessionRenameCandidate> selectedCands = BuildSelectedCandidatesForAi(cids);
                if (selectedCands.Count == 0)
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "请先勾选至少 1 条会话再导出 AI 提示词。");
                    return true;
                }
                if (string.IsNullOrEmpty(outPath))
                {
                    bool dlgBusy;
                    outPath = NativeDialogBridge.PickSaveFile("保存 AI 润色提示词 (.txt)", "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*", "ai_polish_prompt.txt", out dlgBusy);
                    if (dlgBusy) { HttpProtocolHelper.WriteJsonError(stream, 409, "已有文件选择窗口正在打开。"); return true; }
                    if (string.IsNullOrEmpty(outPath))
                    {
                        Dictionary<string, object> cancelled = new Dictionary<string, object>();
                        cancelled["cancelled"] = true;
                        HttpProtocolHelper.WriteJsonResponse(stream, 200, cancelled);
                        return true;
                    }
                }
                else
                {
                    string pathErr;
                    if (!TryValidateCanonicalLocalPath(outPath, out outPath, out pathErr))
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 400, pathErr);
                        return true;
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
                return true;
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
                    return true;
                }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, BuildAiParseResponsePayload(parsedMap, allCands, null));
                return true;
            }
            if (path == "/api/rename/ai-load-json" && req.Method == "POST")
            {
                string inPath = UnifiedJson.GetString(body, "inputPath", string.Empty).Trim();
                List<string> cids = ReadStringArray(body, "cids");
                if (string.IsNullOrEmpty(inPath))
                {
                    bool dlgBusy;
                    inPath = NativeDialogBridge.PickOpenFile("导入 AI 润色结果 JSON", "JSON / 文本文件 (*.json;*.txt)|*.json;*.txt|所有文件 (*.*)|*.*", out dlgBusy);
                    if (dlgBusy) { HttpProtocolHelper.WriteJsonError(stream, 409, "已有文件选择窗口正在打开。"); return true; }
                    if (string.IsNullOrEmpty(inPath))
                    {
                        Dictionary<string, object> cancelled = new Dictionary<string, object>();
                        cancelled["cancelled"] = true;
                        HttpProtocolHelper.WriteJsonResponse(stream, 200, cancelled);
                        return true;
                    }
                }
                else
                {
                    string pathErr;
                    if (!TryValidateCanonicalLocalPath(inPath, out inPath, out pathErr))
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 400, pathErr);
                        return true;
                    }
                }
                if (!File.Exists(inPath))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 404, "未找到指定的 AI 结果文件: " + inPath);
                    return true;
                }
                string rawText = SessionLifecycleService.ReadUtf8TextShared(inPath);
                List<SessionRenameCandidate> allCands = BuildScopedAiCandidates(cids);
                Dictionary<string, string> parsedMap;
                string err;
                if (!AiPolishPromptExchange.ParseAiClipboardJson(rawText, allCands, out parsedMap, out err))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, err ?? "解析 AI JSON 文件失败。");
                    return true;
                }
                _host.AppendLog("info", string.Format("已从文件导入并解析 {0} 条 AI 润色标题：{1}", parsedMap.Count, Path.GetFileName(inPath)));
                HttpProtocolHelper.WriteJsonResponse(stream, 200, BuildAiParseResponsePayload(parsedMap, allCands, inPath));
                return true;
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
                return true;
            }
            if (path == "/api/rename/rules" && req.Method == "POST")
            {
                if (!_host.TryAcquireJob("rename_rules", "正在保存会话命名规则..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行，请稍后再试。");
                    return true;
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
                            return true;
                        }
                        finalJson = AutoTitleSynthesizer.SerializeRulesToJson(parsed);
                    }
                    else
                    {
                        object objVal;
                        if (body == null || !body.TryGetValue("rulesObj", out objVal) || UnifiedJson.AsDict(objVal) == null)
                        {
                            HttpProtocolHelper.WriteJsonError(stream, 400, "缺少 rulesObj 或 rawJson 参数。");
                            return true;
                        }
                        finalJson = UnifiedJson.Serialize(objVal, true);
                        if (!AutoTitleSynthesizer.TryParseRulesFromJson(finalJson, out parsed, out err) || parsed == null)
                        {
                            HttpProtocolHelper.WriteJsonError(stream, 400, err ?? "表单规则校验失败。");
                            return true;
                        }
                        finalJson = AutoTitleSynthesizer.SerializeRulesToJson(parsed);
                    }

                    lock (_host.MutationLock)
                    {
                        string saveErr;
                        if (!AutoTitleSynthesizer.ValidateAndSaveRules(_host.RepoPath, finalJson, out saveErr))
                        {
                            HttpProtocolHelper.WriteJsonError(stream, 500, saveErr ?? "保存 rename_rules.json 失败。");
                            return true;
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
                return true;
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
                        return true;
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
                            return true;
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
                return true;
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
                return true;
            }
            if (path == "/api/rename/rollback" && req.Method == "POST")
            {
                string snapshotDir = UnifiedJson.GetString(body, "snapshotDir", string.Empty).Trim();
                string strategyStr = UnifiedJson.GetString(body, "strategy", "auto");
                bool preferHotWrite = UnifiedJson.GetBool(body, "preferHotWrite", false);
                if (preferHotWrite && string.Equals(strategyStr, "auto", StringComparison.OrdinalIgnoreCase))
                    strategyStr = "SmartAuto";
                bool forceKill = UnifiedJson.GetBool(body, "forceKill", false);
                if (string.IsNullOrEmpty(snapshotDir) || !Directory.Exists(snapshotDir))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "未找到指定的重命名快照目录。");
                    return true;
                }
                string backupRoot = RenameBackupManager.GetBackupRoot(_host.GeminiPath);
                if (!SessionLifecycleService.IsPathContainedUnder(backupRoot, snapshotDir))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 403, "拒绝访问越界的快照路径。");
                    return true;
                }

                // FIX P1-11: Acquire job BEFORE closing IDE
                if (!_host.TryAcquireJob("rename_rollback", "正在执行三层快照回滚..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台写入任务正在执行。");
                    return true;
                }
                try
                {
                    bool hotOnly;
                    int errCode;
                    string errMsg;
                    if (!ResolveStrategyExecution(strategyStr, forceKill, out hotOnly, out errCode, out errMsg))
                    {
                        HttpProtocolHelper.WriteStrategyOrError(stream, errCode, errMsg);
                        return true;
                    }

                    bool deferPbRollback = hotOnly && _host.IsIdeRunning();
                    RenameRollbackResult rbRes;
                    bool ok;
                    lock (_host.MutationLock)
                    {
                        ok = RenameBackupManager.RollbackSnapshot(_host.GeminiPath, snapshotDir, hotOnly, deferPbRollback, out rbRes);
                        if (ok)
                        {
                            try
                            {
                                List<RenameSnapshotInfo> snaps = RenameBackupManager.ListSnapshots(_host.GeminiPath);
                                RenameSnapshotInfo matchedSnap = null;
                                for (int si = 0; si < snaps.Count; si++)
                                {
                                    if (string.Equals(snaps[si].SnapshotDir, snapshotDir, StringComparison.OrdinalIgnoreCase))
                                    {
                                        matchedSnap = snaps[si];
                                        break;
                                    }
                                }
                                if (matchedSnap != null && matchedSnap.Items != null && matchedSnap.Items.Count > 0)
                                {
                                    if (rbRes.PbSkippedDueToLock)
                                    {
                                        Dictionary<string, string> pendingRollback = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                        for (int ri = 0; ri < matchedSnap.Items.Count; ri++)
                                        {
                                            RenameSnapshotItem it = matchedSnap.Items[ri];
                                            if (it != null && !string.IsNullOrEmpty(it.ConversationId) && !string.IsNullOrEmpty(it.OldTitle))
                                                pendingRollback[it.ConversationId] = it.OldTitle;
                                        }
                                        if (pendingRollback.Count > 0)
                                            TrashAndBackupService.EnqueuePendingPbSync(_host.GeminiPath, pendingRollback);
                                    }
                                    else
                                    {
                                        List<string> clearedCids = new List<string>();
                                        for (int ri = 0; ri < matchedSnap.Items.Count; ri++)
                                        {
                                            RenameSnapshotItem it = matchedSnap.Items[ri];
                                            if (it != null && !string.IsNullOrEmpty(it.ConversationId))
                                                clearedCids.Add(it.ConversationId);
                                        }
                                        if (clearedCids.Count > 0)
                                            TrashAndBackupService.RemovePendingPbSync(_host.GeminiPath, clearedCids);
                                    }
                                }
                            }
                            catch { }
                            _host.ReloadAllLocalStateLocked();
                        }
                    }
                    if (!ok)
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 500, rbRes != null ? rbRes.ErrorMessage : "回滚失败。");
                        return true;
                    }
                    int pendingCntAfterRb = TrashAndBackupService.GetPendingPbSyncCount(_host.GeminiPath);
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
                    resp["deferredToPbFlush"] = rbRes.PbSkippedDueToLock && pendingCntAfterRb > 0;
                    resp["pendingPbSyncCount"] = pendingCntAfterRb;
                    resp["layer2Restored"] = rbRes.RestoredDbCount > 0;
                    resp["layer3Restored"] = rbRes.RestoredPbCount > 0;
                    resp["warnings"] = new List<object>(OverviewBuilder.ToObjList(rbRes.Warnings));
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return true;
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
                return true;
            }
            if (path == "/api/rename/cli-suggest" && req.Method == "POST")
            {
                if (!_host.TryAcquireJob("rename_cli_suggest", "正在调用本地 agy CLI 生成重命名建议..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行，请稍后再试。");
                    return true;
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
                        return true;
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
                return true;
            }

            return false;
        }

        // ── Dispatcher Write Command: rename.commit ──

        private OperationResult<Dictionary<string, object>> ExecuteRenameCommitCommand(Dictionary<string, object> args)
        {
            string strategyStr = UnifiedJson.GetString(args, "strategy", "auto");
            bool preferHotWrite = UnifiedJson.GetBool(args, "preferHotWrite", false);
            if (preferHotWrite && string.Equals(strategyStr, "auto", StringComparison.OrdinalIgnoreCase))
            {
                strategyStr = "SmartAuto";
            }
            bool forceKill = UnifiedJson.GetBool(args, "forceKill", false);
            Dictionary<string, string> cidMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            object itemsObj;
            if (args != null && args.TryGetValue("items", out itemsObj))
            {
                List<object> list = UnifiedJson.AsList(itemsObj);
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        Dictionary<string, object> row = UnifiedJson.AsDict(list[i]);
                        if (row == null) continue;
                        string cid = UnifiedJson.GetString(row, "cid", string.Empty).Trim();
                        if (string.IsNullOrEmpty(cid)) cid = UnifiedJson.GetString(row, "id", string.Empty).Trim();
                        string newTitle = UnifiedJson.GetString(row, "newTitle", string.Empty).Trim();
                        if (!string.IsNullOrEmpty(cid) && !string.IsNullOrEmpty(newTitle))
                            cidMap[cid] = newTitle;
                    }
                }
            }

            if (cidMap.Count == 0 && args != null)
            {
                string singleCid = UnifiedJson.GetString(args, "cid", string.Empty).Trim();
                string singleTitle = UnifiedJson.GetString(args, "newTitle", string.Empty).Trim();
                if (!string.IsNullOrEmpty(singleCid) && !string.IsNullOrEmpty(singleTitle))
                {
                    cidMap[singleCid] = singleTitle;
                }
            }

            if (cidMap.Count == 0)
            {
                return FailCommand(400, "未提交有效的重命名条目。");
            }

            RenameBatchResult res;
            int errCode;
            string errMsg;
            if (!ExecuteRenameInternal(cidMap, strategyStr, forceKill, out res, out errCode, out errMsg))
            {
                return BuildStrategyOrErrorFailure(errCode, errMsg);
            }

            Dictionary<string, object> resp = SerializeBatchRenameResult(res);
            int pendingCnt = TrashAndBackupService.GetPendingPbSyncCount(_host.GeminiPath);
            resp["deferredToPbFlush"] = res != null && !res.Layer3Updated && pendingCnt > 0;
            resp["pendingPbSyncCount"] = pendingCnt;
            return OperationResult<Dictionary<string, object>>.Success(resp);
        }

        private OperationResult<Dictionary<string, object>> ExecuteRenameFlushPendingCommand(Dictionary<string, object> args)
        {
            bool closeIde = UnifiedJson.GetBool(args, "closeIdeIfRunning", false);
            bool forceKill = UnifiedJson.GetBool(args, "forceKill", false);

            if (!_host.TryAcquireJob("rename_flush_pending", "正在将待补刷的改名记录同步到侧边栏索引..."))
            {
                return FailJobBusy("当前有其他后台任务正在执行，请稍后再试。");
            }
            try
            {
                int errHttpCode;
                Dictionary<string, object> errPayload;
                if (!EnsureIdeClosedIfNeededResult(closeIde, forceKill, out errHttpCode, out errPayload))
                {
                    return FailCommandPayload(errHttpCode, errPayload, UnifiedJson.GetString(errPayload, "error", "Antigravity 主窗口正在运行。"));
                }

                int flushed = 0;
                List<string> warns = null;
                bool ok;
                lock (_host.MutationLock)
                {
                    ok = TrashAndBackupService.TryFlushPendingPbSync(_host.GeminiPath, out flushed, out warns);
                    if (ok && flushed > 0) _host.ReloadAllLocalStateLocked();
                }

                if (!ok)
                {
                    string err = (warns != null && warns.Count > 0) ? string.Join("; ", warns.ToArray()) : "同步侧边栏索引失败。";
                    return FailCommand(500, err);
                }

                if (flushed > 0)
                    _host.AppendLog("success", string.Format("已将 {0} 条待补刷改名记录同步到侧边栏索引。", flushed));

                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = true;
                resp["flushedCount"] = flushed;
                resp["remainingCount"] = TrashAndBackupService.GetPendingPbSyncCount(_host.GeminiPath);
                resp["warnings"] = new List<object>(OverviewBuilder.ToObjList(warns));
                return OperationResult<Dictionary<string, object>>.Success(resp);
            }
            finally
            {
                _host.ReleaseJob();
            }
        }

        // ── Session & Rename Lookup/Serialization Helpers ──

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
    }
}
