using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Sockets;

namespace AntigravityCompanion
{
    /// <summary>
    /// Storage Analysis, Liposuction/Cold-Archive Slimming &amp; Orphan GC partial of ApiRouter.
    /// Strictly C# 5.0 (.NET Framework 4.x).
    /// </summary>
    internal sealed partial class ApiRouter
    {
        private bool TryRouteStorage(NetworkStream stream, ParsedHttpRequest req, string path, Dictionary<string, object> body)
        {
            if (path == "/api/storage/analyze" && (req.Method == "GET" || req.Method == "POST"))
            {
                Dictionary<string, object> cmdArgs = body != null
                    ? new Dictionary<string, object>(body, StringComparer.Ordinal)
                    : new Dictionary<string, object>(StringComparer.Ordinal);

                if (req.Method == "GET" && req.Query != null)
                {
                    string qTop;
                    int parsedTop;
                    if (req.Query.TryGetValue("topN", out qTop) && int.TryParse(qTop, out parsedTop) && parsedTop > 0)
                        cmdArgs["topN"] = parsedTop;
                    string qAge;
                    double parsedAge;
                    if (req.Query.TryGetValue("minAgeDays", out qAge) && double.TryParse(qAge, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedAge))
                        cmdArgs["minAgeDays"] = parsedAge;
                    string qSize;
                    double parsedSize;
                    if (req.Query.TryGetValue("minSizeMb", out qSize) && double.TryParse(qSize, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedSize))
                        cmdArgs["minSizeMb"] = parsedSize;
                }

                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("storage.analyze", cmdArgs);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return true; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return true;
            }
            if (path == "/api/storage/cold-archives" && req.Method == "GET")
            {
                List<Dictionary<string, object>> list = StorageSlimService.ListColdArchives(_host.GeminiPath);
                List<object> objList = new List<object>(list.Count);
                for (int i = 0; i < list.Count; i++) objList.Add(list[i]);
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["items"] = objList;
                resp["count"] = list.Count;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                return true;
            }
            if (path == "/api/storage/slim" && req.Method == "POST")
            {
                WriteDispatcherResult(stream, _dispatcher.Execute("storage.slim", body));
                return true;
            }
            if (path == "/api/gc/scan" && req.Method == "POST")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("gc.scan", body);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return true; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return true;
            }
            if (path == "/api/gc/clean" && req.Method == "POST")
            {
                if (!_host.TryAcquireJob("gc_clean", "正在将残留垃圾文件安全移入回收站..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台写入任务正在执行，请稍后再试。");
                    return true;
                }
                try
                {
                    bool closeIde = UnifiedJson.GetBool(body, "closeIdeIfRunning", false);
                    bool forceKill = UnifiedJson.GetBool(body, "forceKill", false);
                    if (!EnsureIdeClosedIfNeeded(closeIde, forceKill, stream)) return true;

                    DeletionResult delRes;
                    string trashId = string.Empty;
                    lock (_host.MutationLock)
                    {
                        List<ConversationSummaryInfo> sumsSnap;
                        lock (_host.StateLock)
                        {
                            sumsSnap = new List<ConversationSummaryInfo>(_host.LocalSummariesUnsafe);
                        }
                        List<string> warns = new List<string>();
                        OrphanGcInventory inv = StorageSlimService.ScanCompleteOrphans(_host.GeminiPath, sumsSnap, warns);
                        if (inv.TotalOrphanItems == 0)
                        {
                            delRes = new DeletionResult();
                        }
                        else
                        {
                            delRes = TrashAndBackupService.SoftDeleteOrphans(_host.GeminiPath, inv, out trashId);
                        }

                        OrphanGcInventory afterInv = StorageSlimService.ScanCompleteOrphans(_host.GeminiPath, sumsSnap, null);
                        OrphanScanResult rescanned = ConvertInventoryToOrphanScan(afterInv);
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
                    resp["trashId"] = trashId ?? string.Empty;
                    _host.AppendLog("success", string.Format("残留垃圾文件已移入回收站：数据库 {0} 个、产物目录 {1} 个、标题标注 {2} 个，释放 {3:F2} MB。",
                        delRes.DeletedDbFilesCount, delRes.DeletedBrainDirsCount, delRes.DeletedAnnotationsCount, delRes.FreedMegabytes));
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

        // ── Dispatcher Write Command: storage.slim ──

        private OperationResult<Dictionary<string, object>> ExecuteStorageSlimCommand(Dictionary<string, object> args)
        {
            if (!_host.TryAcquireJob("storage_slim", "正在执行空间清理与清理前安全备份..."))
            {
                return FailJobBusy("当前有其他后台写入任务正在执行，请稍后再试。");
            }
            try
            {
                bool closeIde = UnifiedJson.GetBool(args, "closeIdeIfRunning", false);
                bool forceKill = UnifiedJson.GetBool(args, "forceKill", false);
                int errHttpCode;
                Dictionary<string, object> errPayload;
                if (!EnsureIdeClosedIfNeededResult(closeIde, forceKill, out errHttpCode, out errPayload))
                {
                    return FailCommandPayload(errHttpCode, errPayload, UnifiedJson.GetString(errPayload, "error", "Antigravity 主窗口正在运行。"));
                }

                string modeStr = UnifiedJson.GetString(args, "mode", "liposuction").Trim();
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
                slimReq.TargetCids = ReadStringArray(args, "cids");
                slimReq.MinAgeDays = ReadDoubleParam(args, "minAgeDays", StorageSlimService.DefaultMinAgeDays);
                slimReq.MinSizeMb = ReadDoubleParam(args, "minSizeMb", StorageSlimService.DefaultMinSizeMb);
                slimReq.RunPreFlight = args != null && args.ContainsKey("verifyPreFlight")
                    ? UnifiedJson.GetBool(args, "verifyPreFlight", true)
                    : UnifiedJson.GetBool(args, "runPreFlight", true);
                slimReq.CleanOrphans = UnifiedJson.GetBool(args, "cleanOrphans", false);
                slimReq.PruneLegacyCliArchive = UnifiedJson.GetBool(args, "pruneLegacyCliArchive", false);
                slimReq.MoveOrphansToTrash = !UnifiedJson.GetBool(args, "skipTrash", false);

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
                    return FailCommand(400, !string.IsNullOrEmpty(slimRes.ErrorMessage) ? slimRes.ErrorMessage : "存储瘦身执行失败。");
                }

                _host.AppendLog("success", string.Format("存储瘦身完成（模式={0}，处理={1}，释放={2:F2} MB）。",
                    slimRes.Mode.ToString(), slimRes.ProcessedCount, slimRes.FreedMegabytes));
                return OperationResult<Dictionary<string, object>>.Success(SerializeStorageSlimResult(slimRes));
            }
            finally
            {
                _host.ReleaseJob();
            }
        }

        // ── Storage Serialization Helpers ──

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
            d["trashId"] = r.TrashId ?? string.Empty;
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
    }
}
