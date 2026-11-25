using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;

namespace AntigravityCompanion
{
    /// <summary>
    /// Capabilities Vault, Backup/Restore, Companion Settings, Snapshots, Native Dialogs &amp; App Lifecycle partial of ApiRouter.
    /// Strictly C# 5.0 (.NET Framework 4.x).
    /// </summary>
    internal sealed partial class ApiRouter
    {
        private bool TryRouteVaultBackup(NetworkStream stream, ParsedHttpRequest req, string path, Dictionary<string, object> body)
        {
            if (path == "/api/app/heartbeat" && req.Method == "POST")
            {
                _host.TouchActivity();
                HttpProtocolHelper.WriteOkJson(stream, null);
                return true;
            }
            if (path == "/api/app/shutdown" && req.Method == "POST")
            {
                HttpProtocolHelper.WriteOkJson(stream, null);
                _host.RequestShutdown();
                return true;
            }
            if (path == "/api/jobs/current" && req.Method == "GET")
            {
                lock (_host.StateLock)
                {
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, _host.GetCurrentJobStatusLocked());
                }
                return true;
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
                return true;
            }

            if (path == "/api/capabilities" && req.Method == "GET")
            {
                string src;
                req.Query.TryGetValue("source", out src);
                Dictionary<string, object> cmdArgs = new Dictionary<string, object>();
                cmdArgs["fromBackup"] = string.Equals(src, "backup", StringComparison.OrdinalIgnoreCase);
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("capabilities.list", cmdArgs);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return true; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return true;
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
                    return true;
                }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, OverviewBuilder.BuildCapabilityLineDiffPayload(node));
                return true;
            }
            if (path == "/api/capabilities/push" && req.Method == "POST")
            {
                WriteDispatcherResult(stream, _dispatcher.Execute("vault.push", body));
                return true;
            }
            if (path == "/api/capabilities/import" && req.Method == "POST")
            {
                WriteDispatcherResult(stream, _dispatcher.Execute("vault.import", body));
                return true;
            }
            if (path == "/api/capabilities/delete-local" && req.Method == "POST")
            {
                List<string> keys = ReadStringArray(body, "keys");
                if (keys.Count == 0) { HttpProtocolHelper.WriteJsonError(stream, 400, "请选择要删除的本机能力项。"); return true; }

                // FIX P1-11: Acquire job lock
                if (!_host.TryAcquireJob("capabilities_delete_local", "正在删除本机能力配置..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行，请稍后再试。");
                    return true;
                }
                try
                {
                    int deleted = 0;
                    string trashId = string.Empty;
                    lock (_host.MutationLock)
                    {
                        List<CapabilityItem> localSnap;
                        lock (_host.StateLock)
                        {
                            localSnap = new List<CapabilityItem>(_host.LocalCapsUnsafe);
                        }
                        List<CapabilityItem> selected = SelectCapsByKeys(localSnap, keys);
                        deleted = TrashAndBackupService.SoftDeleteLocalCapabilities(_host.GeminiPath, selected, out trashId);
                        _host.ReloadAllLocalStateLocked();
                    }
                    _host.AppendLog("warn", string.Format("已从本机移除 {0} 项扩展配置（已移入回收站，可随时还原）。", deleted));
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["deleted"] = deleted;
                    resp["trashId"] = trashId ?? string.Empty;
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return true;
            }
            if (path == "/api/capabilities/delete-repo" && req.Method == "POST")
            {
                List<string> keys = ReadStringArray(body, "keys");
                if (keys.Count == 0) { HttpProtocolHelper.WriteJsonError(stream, 400, "请选择要从仓库移除的能力项。"); return true; }

                // FIX P1-11: Acquire job lock
                if (!_host.TryAcquireJob("capabilities_delete_repo", "正在从内置仓库移除能力快照..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行，请稍后再试。");
                    return true;
                }
                try
                {
                    int deleted = 0;
                    string trashId = string.Empty;
                    lock (_host.MutationLock)
                    {
                        List<CapabilityItem> repoSnap;
                        lock (_host.StateLock)
                        {
                            repoSnap = new List<CapabilityItem>(_host.RepoCapsUnsafe);
                        }
                        List<CapabilityItem> selected = SelectCapsByKeys(repoSnap, keys);
                        deleted = TrashAndBackupService.SoftDeleteRepoCapabilities(_host.RepoPath, _host.GeminiPath, selected, out trashId);
                        _host.ReloadAllLocalStateLocked();
                    }
                    _host.AppendLog("warn", string.Format("已从配置同步库移除 {0} 项配置（已移入回收站，可随时还原）。", deleted));
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["deleted"] = deleted;
                    resp["trashId"] = trashId ?? string.Empty;
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return true;
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
                    return true;
                }

                // FIX P1-11: Acquire job lock
                if (!_host.TryAcquireJob("capabilities_relocate", "正在重定位能力依赖路径..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行，请稍后再试。");
                    return true;
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
                            return true;
                        }
                        ok = CapabilityVaultService.RelocateCapabilityPath(_host.GeminiPath, _host.RepoPath, isLocalTarget, _host.HomePath, cap, oldPath, newPath);
                        if (ok) _host.ReloadAllLocalStateLocked();
                    }
                    // FIX P1-4: Return HTTP 400 {ok:false, error:...} on relocate failure
                    if (!ok)
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 400, "未在目标配置中匹配到原路径，或新旧路径一致。");
                        return true;
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
                return true;
            }

            if (path == "/api/backup/current" && req.Method == "GET")
            {
                HttpProtocolHelper.WriteJsonResponse(stream, 200, OverviewBuilder.BuildBackupPayload(_host));
                return true;
            }
            if (path == "/api/backup/open" && req.Method == "POST")
            {
                string agyPath = UnifiedJson.GetString(body, "archivePath", string.Empty).Trim();
                if (string.IsNullOrEmpty(agyPath))
                    agyPath = UnifiedJson.GetString(body, "path", string.Empty).Trim();
                if (string.IsNullOrEmpty(agyPath))
                {
                    bool dlgBusy;
                    agyPath = NativeDialogBridge.PickOpenFile("打开 Antigravity 备份归档包 (.agy)", "Antigravity 备份包 (*.agy)|*.agy|ZIP 压缩包 (*.zip)|*.zip|所有文件 (*.*)|*.*", out dlgBusy);
                    if (dlgBusy) { HttpProtocolHelper.WriteJsonError(stream, 409, "已有文件选择窗口正在打开。"); return true; }
                    if (string.IsNullOrEmpty(agyPath))
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
                    if (!TryValidateCanonicalLocalPath(agyPath, out agyPath, out pathErr))
                    {
                        HttpProtocolHelper.WriteJsonError(stream, 400, pathErr);
                        return true;
                    }
                }
                if (!File.Exists(agyPath)) { HttpProtocolHelper.WriteJsonError(stream, 404, "归档文件不存在: " + agyPath); return true; }

                lock (_host.MutationLock)
                {
                    _host.ReloadLoadedBackupLocked(agyPath);
                }
                _host.AppendLog("info", "已打开归档包：" + Path.GetFileName(agyPath));
                HttpProtocolHelper.WriteJsonResponse(stream, 200, OverviewBuilder.BuildBackupPayload(_host));
                return true;
            }
            if (path == "/api/backup/full" && req.Method == "POST")
            {
                Dictionary<string, object> cmdArgs = body != null
                    ? new Dictionary<string, object>(body, StringComparer.Ordinal)
                    : new Dictionary<string, object>(StringComparer.Ordinal);
                string outPath = UnifiedJson.GetString(cmdArgs, "outputPath", string.Empty).Trim();
                if (string.IsNullOrEmpty(outPath))
                {
                    string defName = string.Format("Antigravity_FullBackup_{0}.agy", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                    bool dlgBusy;
                    outPath = NativeDialogBridge.PickSaveFile("保存全量备份 (.agy)", "Antigravity 备份包 (*.agy)|*.agy", defName, out dlgBusy);
                    if (dlgBusy) { HttpProtocolHelper.WriteJsonError(stream, 409, "已有文件选择窗口正在打开。"); return true; }
                    if (string.IsNullOrEmpty(outPath))
                    {
                        Dictionary<string, object> cancelled = new Dictionary<string, object>();
                        cancelled["cancelled"] = true;
                        HttpProtocolHelper.WriteJsonResponse(stream, 200, cancelled);
                        return true;
                    }
                    cmdArgs["outputPath"] = outPath;
                }
                WriteDispatcherResult(stream, _dispatcher.Execute("backup.full", cmdArgs));
                return true;
            }
            if (path == "/api/backup/selective-export" && req.Method == "POST")
            {
                List<string> cids = ReadStringArray(body, "cids");
                List<string> capKeys = ReadStringArray(body, "capKeys");
                bool includeGlobal = UnifiedJson.GetBool(body, "includeGlobalCaps", false);
                bool closeIde = UnifiedJson.GetBool(body, "closeIdeIfRunning", false);
                bool forceKill = UnifiedJson.GetBool(body, "forceKill", false);
                bool onlineHotBackup = UnifiedJson.GetBool(body, "onlineHotBackup", false);
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
                    return true;
                }
                if (string.IsNullOrEmpty(outPath))
                {
                    string defName = string.Format("Antigravity_Selective_{0}.agy", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                    bool dlgBusy;
                    outPath = NativeDialogBridge.PickSaveFile("导出所选至轻量归档 (.agy)", "Antigravity 备份包 (*.agy)|*.agy", defName, out dlgBusy);
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

                // FIX P1-11 + Phase 2: Read-only export supports onlineHotBackup without closing IDE (WAL-safe SqliteOnlineBackup)
                if (!_host.TryAcquireJob("selective_export", "正在导出备份文件 (.agy)..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行。");
                    return true;
                }
                if (cids.Count > 0 && !onlineHotBackup && !EnsureIdeClosedIfNeeded(closeIde, forceKill, stream))
                {
                    _host.ReleaseJob();
                    return true;
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
                return true;
            }
            if (path == "/api/backup/import" && req.Method == "POST")
            {
                WriteDispatcherResult(stream, _dispatcher.Execute("backup.import", body));
                return true;
            }

            if (path == "/api/settings/companion" && req.Method == "GET")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("settings.companion.get", null);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return true; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return true;
            }
            if (path == "/api/settings/companion" && req.Method == "POST")
            {
                WriteDispatcherResult(stream, _dispatcher.Execute("settings.companion.update", body));
                return true;
            }
            if (path == "/api/snapshots/status" && req.Method == "GET")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("snapshots.status", null);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return true; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return true;
            }
            if (path == "/api/snapshots/run-now" && req.Method == "POST")
            {
                SnapshotRunResult runRes = _host.SnapshotService.RunSnapshotNow();
                if (runRes.SkippedBusy)
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, runRes.ErrorMessage ?? "当前有其他后台任务正在执行，请稍后再试。");
                    return true;
                }
                if (!runRes.Ok)
                {
                    HttpProtocolHelper.WriteJsonError(stream, 500, runRes.ErrorMessage ?? "立即快照失败。");
                    return true;
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
                return true;
            }
            if (path == "/api/vault/status" && req.Method == "GET")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("vault.status", null);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return true; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return true;
            }
            if (path == "/api/vault/select" && req.Method == "POST")
            {
                string rawVault = UnifiedJson.GetString(body, "vaultPath", string.Empty).Trim();
                if (string.IsNullOrEmpty(rawVault))
                {
                    bool dlgBusy;
                    rawVault = NativeDialogBridge.PickFolder("选择 Portable Vault 能力仓库目录", _host.RepoPath, out dlgBusy);
                    if (dlgBusy) { HttpProtocolHelper.WriteJsonError(stream, 409, "已有目录选择窗口正在打开。"); return true; }
                    if (string.IsNullOrEmpty(rawVault))
                    {
                        Dictionary<string, object> cancelled = new Dictionary<string, object>();
                        cancelled["cancelled"] = true;
                        HttpProtocolHelper.WriteJsonResponse(stream, 200, cancelled);
                        return true;
                    }
                }

                string canonVault;
                string vErr;
                if (!TryValidateCanonicalLocalPath(rawVault, out canonVault, out vErr))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "Vault 路径无效: " + vErr);
                    return true;
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
                return true;
            }
            if (path == "/api/vault/migrate-legacy" && req.Method == "POST")
            {
                string baseDir = ResolveEffectiveAppBaseDirForApi();
                string legacyRepoDir = Path.Combine(baseDir, "Repository");
                if (!Directory.Exists(legacyRepoDir))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 404, "未找到旧版 ./Repository 目录: " + legacyRepoDir);
                    return true;
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
                return true;
            }

            if (path == "/api/native/pick-open-file" && req.Method == "POST")
            {
                string title = UnifiedJson.GetString(body, "title", "选择文件");
                string filter = UnifiedJson.GetString(body, "filter", "所有文件 (*.*)|*.*");
                bool busy;
                string selected = NativeDialogBridge.PickOpenFile(title, filter, out busy);
                if (busy) { HttpProtocolHelper.WriteJsonError(stream, 409, "已有文件选择弹窗处于打开状态。"); return true; }
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["cancelled"] = string.IsNullOrEmpty(selected);
                resp["path"] = selected ?? string.Empty;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                return true;
            }
            if (path == "/api/native/pick-save-file" && req.Method == "POST")
            {
                string title = UnifiedJson.GetString(body, "title", "保存文件");
                string filter = UnifiedJson.GetString(body, "filter", "所有文件 (*.*)|*.*");
                string defName = UnifiedJson.GetString(body, "defaultFileName", string.Empty);
                bool busy;
                string selected = NativeDialogBridge.PickSaveFile(title, filter, defName, out busy);
                if (busy) { HttpProtocolHelper.WriteJsonError(stream, 409, "已有文件选择弹窗处于打开状态。"); return true; }
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["cancelled"] = string.IsNullOrEmpty(selected);
                resp["path"] = selected ?? string.Empty;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                return true;
            }
            if (path == "/api/native/pick-folder" && req.Method == "POST")
            {
                string title = UnifiedJson.GetString(body, "title", "选择文件夹");
                string initialPath = UnifiedJson.GetString(body, "initialPath", _host.HomePath);
                bool busy;
                string selected = NativeDialogBridge.PickFolder(title, initialPath, out busy);
                if (busy) { HttpProtocolHelper.WriteJsonError(stream, 409, "已有目录选择弹窗处于打开状态。"); return true; }
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["cancelled"] = string.IsNullOrEmpty(selected);
                resp["path"] = selected ?? string.Empty;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                return true;
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
                    return true;
                }
                if (!File.Exists(canonicalPath) && !Directory.Exists(canonicalPath))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 404, "路径不存在: " + canonicalPath);
                    return true;
                }
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "explorer.exe";
                psi.Arguments = explorerArgs;
                psi.UseShellExecute = false;
                using (Process.Start(psi)) { }
                HttpProtocolHelper.WriteOkJson(stream, null);
                return true;
            }

            if (path == "/api/trash/list" && req.Method == "GET")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("trash.list", null);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return true; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return true;
            }
            if (path == "/api/trash/restore" && req.Method == "POST")
            {
                WriteDispatcherResult(stream, _dispatcher.Execute("trash.restore", body));
                return true;
            }
            if (path == "/api/trash/purge" && req.Method == "POST")
            {
                WriteDispatcherResult(stream, _dispatcher.Execute("trash.purge", body));
                return true;
            }
            if (path == "/api/config-backups/list" && req.Method == "GET")
            {
                string filterPath = string.Empty;
                if (req.Query != null) req.Query.TryGetValue("path", out filterPath);
                Dictionary<string, object> cmdArgs = new Dictionary<string, object>();
                cmdArgs["path"] = filterPath ?? string.Empty;
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("config.backups.list", cmdArgs);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return true; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return true;
            }
            if (path == "/api/config-backups/restore" && req.Method == "POST")
            {
                WriteDispatcherResult(stream, _dispatcher.Execute("config.backups.restore", body));
                return true;
            }

            return false;
        }

        // ── Dispatcher Write Commands: vault.push, vault.import, backup.full, backup.import, settings.companion.update ──

        private OperationResult<Dictionary<string, object>> ExecuteVaultPushCommand(Dictionary<string, object> args)
        {
            List<string> keys = ReadStringArray(args, "keys");
            if (keys.Count == 0)
            {
                return FailCommand(400, "请选择要存入内置仓库的能力项。");
            }

            // FIX P1-11: Acquire job lock
            if (!_host.TryAcquireJob("capabilities_push", "正在将本地能力写入内置仓库..."))
            {
                return FailJobBusy("当前有其他后台任务正在执行，请稍后再试。");
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
                    if (selected.Count == 0)
                    {
                        return FailCommand(404, "本机未找到所选能力项。");
                    }
                    written = CapabilityVaultService.PushCapabilitiesToRepository(_host.RepoPath, _host.HomePath, selected);
                    _host.UpdateBaselineForCapabilities(selected);
                    _host.ReloadAllLocalStateLocked();
                }
                _host.AppendLog("success", string.Format("已将 {0} 项本地能力写入内置仓库 Vault。", keys.Count));
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = true;
                resp["written"] = written;
                return OperationResult<Dictionary<string, object>>.Success(resp);
            }
            finally
            {
                _host.ReleaseJob();
            }
        }

        private OperationResult<Dictionary<string, object>> ExecuteVaultImportCommand(Dictionary<string, object> args)
        {
            List<string> keys = ReadStringArray(args, "keys");
            string source = UnifiedJson.GetString(args, "source", "repo");
            bool fromBackup = string.Equals(source, "backup", StringComparison.OrdinalIgnoreCase);
            if (keys.Count == 0)
            {
                return FailCommand(400, "请选择要导入本地的能力项。");
            }

            // FIX P1-11: Acquire job lock
            if (!_host.TryAcquireJob("capabilities_import", "正在按 Key 级无损导入能力配置..."))
            {
                return FailJobBusy("当前有其他后台任务正在执行，请稍后再试。");
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
                    if (selected.Count == 0)
                    {
                        return FailCommand(404, "目标来源未找到所选能力项。");
                    }
                    written = CapabilityVaultService.ImportCapabilitiesToLocal(_host.GeminiPath, _host.HomePath, selected);
                    if (!fromBackup) _host.UpdateBaselineForCapabilities(selected);
                    _host.ReloadAllLocalStateLocked();
                }
                _host.AppendLog("success", string.Format("已按 Key 级无损导入 {0} 项能力至本机 .gemini 目录。", keys.Count));
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = true;
                resp["written"] = written;
                return OperationResult<Dictionary<string, object>>.Success(resp);
            }
            finally
            {
                _host.ReleaseJob();
            }
        }

        private OperationResult<Dictionary<string, object>> ExecuteBackupFullCommand(Dictionary<string, object> args)
        {
            string outPath = UnifiedJson.GetString(args, "outputPath", string.Empty).Trim();
            bool closeIde = UnifiedJson.GetBool(args, "closeIdeIfRunning", false);
            bool forceKill = UnifiedJson.GetBool(args, "forceKill", false);
            bool onlineHotBackup = UnifiedJson.GetBool(args, "onlineHotBackup", false);
            string tierStr = UnifiedJson.GetString(args, "tier", "fast").Trim();
            BackupTier backupTier = (string.Equals(tierStr, "full", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(tierStr, "FullForensic", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(tierStr, "full_forensic", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(tierStr, "full-forensic", StringComparison.OrdinalIgnoreCase))
                ? BackupTier.FullForensic
                : BackupTier.FastDaily;

            if (string.IsNullOrEmpty(outPath))
            {
                return FailCommand(400, "请提供 outputPath 备份输出路径。");
            }

            string pathErr;
            if (!TryValidateCanonicalLocalPath(outPath, out outPath, out pathErr))
            {
                return FailCommand(400, pathErr);
            }

            // FIX P1-11: Acquire job BEFORE closing IDE!
            if (!_host.TryAcquireJob("full_backup", "正在创建全量备份归档 (.agy)..."))
            {
                return FailJobBusy("当前有其他后台任务正在执行。");
            }

            bool handedOff = false;
            try
            {
                if (!onlineHotBackup)
                {
                    int errHttpCode;
                    Dictionary<string, object> errPayload;
                    if (!EnsureIdeClosedIfNeededResult(closeIde, forceKill, out errHttpCode, out errPayload))
                    {
                        return FailCommandPayload(errHttpCode, errPayload, UnifiedJson.GetString(errPayload, "error", "IDE 正在运行。"));
                    }
                }

                string destSnap = outPath;
                List<ConversationSummaryInfo> sumSnap;
                lock (_host.StateLock)
                {
                    sumSnap = new List<ConversationSummaryInfo>(_host.LocalSummariesUnsafe);
                }

                handedOff = true;
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
                ack["onlineHotBackup"] = onlineHotBackup;
                ack["httpStatusCode"] = 202;
                return OperationResult<Dictionary<string, object>>.Success(ack);
            }
            finally
            {
                if (!handedOff) _host.ReleaseJob();
            }
        }

        private OperationResult<Dictionary<string, object>> ExecuteBackupImportCommand(Dictionary<string, object> args)
        {
            List<string> cids = ReadStringArray(args, "cids");
            List<string> capKeys = ReadStringArray(args, "capKeys");
            bool closeIde = UnifiedJson.GetBool(args, "closeIdeIfRunning", false);
            bool forceKill = UnifiedJson.GetBool(args, "forceKill", false);
            bool includeUserSettings = UnifiedJson.GetBool(args, "includeUserSettings", false);
            bool restoreUserSettings = UnifiedJson.GetBool(args, "restoreUserSettings", false);
            bool restoreLegacyCliArchive = UnifiedJson.GetBool(args, "restoreLegacyCliArchive", false);

            if (cids.Count == 0 && capKeys.Count == 0 && !includeUserSettings && !restoreUserSettings && !restoreLegacyCliArchive)
            {
                return FailCommand(400, "请先选择要从备份文件导入的配置或对话。");
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
                return FailCommand(400, "当前未打开任何 .agy 备份归档包。");
            }

            // FIX P1-11: Acquire job BEFORE closing IDE!
            if (!_host.TryAcquireJob("selective_import", "正在执行三层选择性合流导入..."))
            {
                return FailJobBusy("当前有其他后台任务正在执行。");
            }

            bool handedOff = false;
            try
            {
                if (cids.Count > 0 || includeUserSettings || restoreUserSettings || restoreLegacyCliArchive)
                {
                    int errHttpCode;
                    Dictionary<string, object> errPayload;
                    if (!EnsureIdeClosedIfNeededResult(closeIde, forceKill, out errHttpCode, out errPayload))
                    {
                        return FailCommandPayload(errHttpCode, errPayload, UnifiedJson.GetString(errPayload, "error", "IDE 正在运行。"));
                    }
                }

                SelectiveImportRequest importReq = new SelectiveImportRequest();
                lock (_host.StateLock)
                {
                    if (_host.BackupIndexUnsafe == null || string.IsNullOrEmpty(_host.LoadedBackupPathUnsafe))
                    {
                        return FailCommand(400, "当前未打开任何 .agy 备份文件。");
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
                    importReq.RestoreLegacyCliArchive = restoreLegacyCliArchive;
                }

                handedOff = true;
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
                ack["httpStatusCode"] = 202;
                return OperationResult<Dictionary<string, object>>.Success(ack);
            }
            finally
            {
                if (!handedOff) _host.ReleaseJob();
            }
        }

        private OperationResult<Dictionary<string, object>> ExecuteSettingsCompanionUpdateCommand(Dictionary<string, object> args)
        {
            Dictionary<string, object> safeArgs = args ?? new Dictionary<string, object>(StringComparer.Ordinal);

            bool hasEnabled = safeArgs.ContainsKey("enabled");
            bool reqEnabled = hasEnabled ? UnifiedJson.GetBool(safeArgs, "enabled", false) : false;

            bool hasInterval = safeArgs.ContainsKey("intervalHours");
            int reqInterval = 0;
            if (hasInterval)
            {
                reqInterval = UnifiedJson.GetInt(safeArgs, "intervalHours", 12);
                if (reqInterval < 1 || reqInterval > 720)
                {
                    return FailCommand(400, "快照间隔 intervalHours 必须在 1 到 720 小时之间。");
                }
            }

            bool hasRetain = safeArgs.ContainsKey("retainCount");
            int reqRetain = 0;
            if (hasRetain)
            {
                reqRetain = UnifiedJson.GetInt(safeArgs, "retainCount", 5);
                if (reqRetain < 1 || reqRetain > 100)
                {
                    return FailCommand(400, "保留份数 retainCount 必须在 1 到 100 之间。");
                }
            }

            bool hasTargetDir = safeArgs.ContainsKey("targetDirectory");
            string reqTargetDir = string.Empty;
            if (hasTargetDir)
            {
                string rawDir = UnifiedJson.GetString(safeArgs, "targetDirectory", string.Empty).Trim();
                if (string.IsNullOrEmpty(rawDir))
                {
                    reqTargetDir = ScheduledSnapshotService.GetDefaultSnapshotDirectory(_host.GeminiPath);
                }
                else
                {
                    string canonDir;
                    string dirErr;
                    if (!TryValidateCanonicalLocalPath(rawDir, out canonDir, out dirErr))
                    {
                        return FailCommand(400, "快照目标目录无效: " + dirErr);
                    }
                    reqTargetDir = canonDir;
                }
            }

            bool hasTier = safeArgs.ContainsKey("tier");
            string reqTier = "fast";
            if (hasTier)
            {
                reqTier = UnifiedJson.GetString(safeArgs, "tier", "fast").Trim().ToLowerInvariant();
                if (reqTier != "fast" && reqTier != "full")
                {
                    return FailCommand(400, "tier 仅支持 \"fast\" 或 \"full\"。");
                }
            }

            bool hasMinimize = safeArgs.ContainsKey("minimizeToTray");
            bool reqMinimize = hasMinimize ? UnifiedJson.GetBool(safeArgs, "minimizeToTray", false) : false;

            bool hasHotkey = safeArgs.ContainsKey("globalHotkey");
            string reqHotkey = string.Empty;
            if (hasHotkey)
            {
                string hk = UnifiedJson.GetString(safeArgs, "globalHotkey", string.Empty).Trim();
                if (!string.IsNullOrEmpty(hk))
                {
                    uint mods, vk;
                    if (!TrayHost.TryParseHotkey(hk, out mods, out vk))
                    {
                        return FailCommand(400, "全局热键格式无效，示例: Ctrl+Alt+A / Ctrl+Shift+K。");
                    }
                }
                reqHotkey = hk;
            }

            bool hasCliPath = safeArgs.ContainsKey("customAgyCliPath");
            string reqCliPath = hasCliPath ? UnifiedJson.GetString(safeArgs, "customAgyCliPath", string.Empty).Trim() : string.Empty;

            bool hasVaultPath = safeArgs.ContainsKey("vaultPath");
            string reqVaultPath = string.Empty;
            if (hasVaultPath)
            {
                string vp = UnifiedJson.GetString(safeArgs, "vaultPath", string.Empty).Trim();
                if (!string.IsNullOrEmpty(vp))
                {
                    string canonVp;
                    string vpErr;
                    if (!TryValidateCanonicalLocalPath(vp, out canonVp, out vpErr))
                    {
                        return FailCommand(400, "Vault 路径无效: " + vpErr);
                    }
                    reqVaultPath = canonVp;
                }
            }

            bool hasStartWithWindows = safeArgs.ContainsKey("startWithWindows");
            bool reqStartWithWindows = hasStartWithWindows ? UnifiedJson.GetBool(safeArgs, "startWithWindows", false) : false;

            bool hasThreshold = safeArgs.ContainsKey("ruleTokenReferenceThreshold");
            int reqThreshold = 0;
            if (hasThreshold)
            {
                reqThreshold = UnifiedJson.GetInt(safeArgs, "ruleTokenReferenceThreshold", 0);
                if (reqThreshold < 0 || reqThreshold > 100000)
                {
                    return FailCommand(400, "规则参考阈值 ruleTokenReferenceThreshold 必须在 0 到 100000 之间（0 表示不标红）。");
                }
            }

            bool startupRollback = false;
            bool finalStartupChanged = false;
            bool hotkeyChanged = false;

            lock (_host.MutationLock)
            {
                CompanionSettings prev = _host.SnapshotService.GetSettings();
                CompanionSettings next = prev.Clone();

                if (hasEnabled) next.Enabled = reqEnabled;
                if (hasInterval) next.IntervalHours = reqInterval;
                if (hasRetain) next.RetainCount = reqRetain;
                if (hasTargetDir) next.TargetDirectory = reqTargetDir;
                if (hasTier) next.Tier = reqTier;
                if (hasMinimize) next.MinimizeToTray = reqMinimize;
                if (hasHotkey) next.GlobalHotkey = reqHotkey;
                if (hasCliPath) next.CustomAgyCliPath = reqCliPath;
                if (hasVaultPath) next.VaultPath = reqVaultPath;
                if (hasStartWithWindows) next.StartWithWindows = reqStartWithWindows;
                if (hasThreshold) next.RuleTokenReferenceThreshold = reqThreshold;

                bool startupChanged = next.StartWithWindows != prev.StartWithWindows;
                hotkeyChanged = !string.Equals(next.GlobalHotkey, prev.GlobalHotkey, StringComparison.OrdinalIgnoreCase);

                CompanionSettings saved = _host.SnapshotService.UpdateSettings(next);
                if (startupChanged)
                {
                    bool regOk = _host.TryApplyStartWithWindows(saved.StartWithWindows, System.Windows.Forms.Application.ExecutablePath);
                    if (!regOk)
                    {
                        saved.StartWithWindows = prev.StartWithWindows;
                        saved = _host.SnapshotService.UpdateSettings(saved);
                        startupRollback = true;
                    }
                }
                finalStartupChanged = saved.StartWithWindows != prev.StartWithWindows;
            }

            if (startupRollback)
            {
                _host.AppendLog("warn", "无法写入 Windows 开机自启注册表，已自动回滚该开关。");
            }
            if (finalStartupChanged || hotkeyChanged)
            {
                _host.NotifySettingsChanged();
            }

            _host.AppendLog("success", "已保存 Companion 伴侣运行与快照设置。");
            Dictionary<string, object> resp = BuildCompanionSettingsPayload();
            resp["ok"] = true;
            if (startupRollback)
            {
                resp["startupRollback"] = true;
                resp["startupError"] = "无法写入 Windows 开机自启注册表，已自动回滚该开关。";
            }
            return OperationResult<Dictionary<string, object>>.Success(resp);
        }

        // ── Trash & Config Backup Write Commands ──

        private OperationResult<Dictionary<string, object>> ExecuteTrashRestoreCommand(Dictionary<string, object> args)
        {
            string trashId = UnifiedJson.GetString(args, "trashId", string.Empty).Trim();
            if (string.IsNullOrEmpty(trashId)) trashId = UnifiedJson.GetString(args, "id", string.Empty).Trim();
            if (string.IsNullOrEmpty(trashId))
                return FailCommand(400, "请指定要还原的回收站记录 ID。");

            bool closeIde = UnifiedJson.GetBool(args, "closeIdeIfRunning", false);
            bool forceKill = UnifiedJson.GetBool(args, "forceKill", false);

            if (!_host.TryAcquireJob("trash_restore", "正在从回收站还原数据..."))
                return FailJobBusy("当前有其他后台写入任务正在执行，请稍后再试。");

            try
            {
                if (TrashAndBackupService.TrashItemRequiresIdeClosed(_host.GeminiPath, trashId))
                {
                    int errHttpCode;
                    Dictionary<string, object> errPayload;
                    if (!EnsureIdeClosedIfNeededResult(closeIde, forceKill, out errHttpCode, out errPayload))
                        return FailCommandPayload(errHttpCode, errPayload, UnifiedJson.GetString(errPayload, "error", "Antigravity 主窗口正在运行。"));
                }

                TrashRestoreResult restoreRes;
                lock (_host.MutationLock)
                {
                    restoreRes = TrashAndBackupService.RestoreTrashItem(_host.GeminiPath, _host.RepoPath, trashId);
                    if (restoreRes.Ok) _host.ReloadAllLocalStateLocked();
                }

                if (!restoreRes.Ok)
                    return FailCommand(400, restoreRes.ErrorMessage ?? "还原失败。");

                _host.AppendLog("success", string.Format("已从回收站还原记录 {0}（恢复文件 {1} 项）。", trashId, restoreRes.RestoredFilesCount));
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = true;
                resp["trashId"] = restoreRes.TrashId;
                resp["kind"] = restoreRes.Kind;
                resp["restoredFiles"] = restoreRes.RestoredFilesCount;
                resp["restoredProtoEntries"] = restoreRes.RestoredProtoEntriesCount;
                resp["restoredDbRows"] = restoreRes.RestoredDbRowsCount;
                resp["warnings"] = new List<object>(OverviewBuilder.ToObjList(restoreRes.Warnings));
                return OperationResult<Dictionary<string, object>>.Success(resp);
            }
            finally
            {
                _host.ReleaseJob();
            }
        }

        private OperationResult<Dictionary<string, object>> ExecuteTrashPurgeCommand(Dictionary<string, object> args)
        {
            bool emptyAll = UnifiedJson.GetBool(args, "all", false);
            string trashId = UnifiedJson.GetString(args, "trashId", string.Empty).Trim();
            if (string.IsNullOrEmpty(trashId)) trashId = UnifiedJson.GetString(args, "id", string.Empty).Trim();

            if (!emptyAll && string.IsNullOrEmpty(trashId))
                return FailCommand(400, "请指定要彻底删除的记录 ID 或传入 all=true。");

            if (!_host.TryAcquireJob("trash_purge", emptyAll ? "正在清空回收站..." : "正在彻底删除回收站记录..."))
                return FailJobBusy("当前有其他后台写入任务正在执行，请稍后再试。");

            try
            {
                if (emptyAll)
                {
                    int removed = 0;
                    lock (_host.MutationLock)
                    {
                        removed = TrashAndBackupService.EmptyTrash(_host.GeminiPath);
                    }
                    _host.AppendLog("warn", string.Format("已清空回收站，彻底删除 {0} 条历史记录。", removed));
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["purgedCount"] = removed;
                    return OperationResult<Dictionary<string, object>>.Success(resp);
                }

                string err;
                bool ok;
                lock (_host.MutationLock)
                {
                    ok = TrashAndBackupService.PurgeTrashItem(_host.GeminiPath, trashId, out err);
                }
                if (!ok) return FailCommand(400, err ?? "彻底删除失败。");
                _host.AppendLog("warn", "已从回收站彻底删除记录: " + trashId);
                Dictionary<string, object> singleResp = new Dictionary<string, object>();
                singleResp["ok"] = true;
                singleResp["purgedCount"] = 1;
                return OperationResult<Dictionary<string, object>>.Success(singleResp);
            }
            finally
            {
                _host.ReleaseJob();
            }
        }

        private OperationResult<Dictionary<string, object>> ExecuteConfigBackupRestoreCommand(Dictionary<string, object> args)
        {
            string backupPath = UnifiedJson.GetString(args, "backupPath", string.Empty).Trim();
            if (string.IsNullOrEmpty(backupPath))
                return FailCommand(400, "backupPath 不能为空。");

            if (!_host.TryAcquireJob("config_backup_restore", "正在还原配置文件历史版本..."))
                return FailJobBusy("当前有其他后台写入任务正在执行，请稍后再试。");

            try
            {
                ConfigBackupRestoreResult res;
                lock (_host.MutationLock)
                {
                    res = TrashAndBackupService.RestoreConfigBackup(_host.GeminiPath, _host.HomePath, _host.RepoPath, backupPath);
                    if (res.Ok) _host.ReloadAllLocalStateLocked();
                }

                if (!res.Ok)
                    return FailCommand(res.HttpStatusCode > 0 ? res.HttpStatusCode : 400, res.ErrorMessage ?? "还原配置版本失败。");

                _host.AppendLog("success", "已还原配置文件历史版本：" + Path.GetFileName(res.OriginalPath));
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = true;
                resp["originalPath"] = res.OriginalPath;
                resp["restoredFromPath"] = res.RestoredFromPath;
                resp["safetyBackupPath"] = res.SafetyBackupPath;
                return OperationResult<Dictionary<string, object>>.Success(resp);
            }
            finally
            {
                _host.ReleaseJob();
            }
        }

        // ── Vault, Backup & Settings Payload Helpers ──

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

            List<Dictionary<string, object>> coldRaw = StorageSlimService.ListColdArchives(_host.GeminiPath);
            List<object> coldObj = new List<object>(coldRaw.Count);
            for (int i = 0; i < coldRaw.Count; i++) coldObj.Add(coldRaw[i]);
            d["coldArchives"] = coldObj;
            d["coldArchivesCount"] = coldRaw.Count;
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
