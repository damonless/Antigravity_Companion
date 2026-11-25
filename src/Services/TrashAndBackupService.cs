using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // UNIFIED SOFT-DELETE TRASH (.gemini/companion-trash/),
    // .bak.* CONFIG VERSION RESTORE & PENDING LAYER-3 .pb AUTO-FLUSH QUEUE
    // Strictly C# 5.0 (.NET Framework 4.x)
    // ══════════════════════════════════════════════════════════════════════

    public sealed class TrashRestoreResult
    {
        public bool Ok;
        public string TrashId;
        public string Kind;
        public int RestoredFilesCount;
        public int RestoredProtoEntriesCount;
        public int RestoredDbRowsCount;
        public string ErrorMessage;
        public List<string> Warnings;

        public TrashRestoreResult()
        {
            TrashId = string.Empty;
            Kind = string.Empty;
            ErrorMessage = string.Empty;
            Warnings = new List<string>();
        }
    }

    public sealed class ConfigBackupRestoreResult
    {
        public bool Ok;
        public int HttpStatusCode;
        public string OriginalPath;
        public string RestoredFromPath;
        public string SafetyBackupPath;
        public string ErrorMessage;

        public ConfigBackupRestoreResult()
        {
            HttpStatusCode = 200;
            OriginalPath = string.Empty;
            RestoredFromPath = string.Empty;
            SafetyBackupPath = string.Empty;
            ErrorMessage = string.Empty;
        }
    }

    public static class TrashAndBackupService
    {
        public const int DefaultRetentionDays = 30;

        public static string GetTrashRoot(string geminiPath)
        {
            string safeGemini = string.IsNullOrWhiteSpace(geminiPath)
                ? Path.Combine(WorkspaceService.ResolveDefaultHomePath(), ".gemini")
                : geminiPath.Trim();
            return Path.Combine(safeGemini, "companion-trash");
        }

        // ══════════════════════════════════════════════════════════════════
        // 1. SOFT-DELETE TO TRASH QUARANTINE
        // ══════════════════════════════════════════════════════════════════

        public static OperationResult<DeletionResult> SoftDeleteSessionsAndProjects(
            string geminiPath,
            List<ConversationSummaryInfo> allLocalSummaries,
            Dictionary<string, ProjectMetadata> localProjects,
            HashSet<string> targetConvIds,
            HashSet<string> targetProjectNames,
            HashSet<string> targetProjectIds,
            HashSet<string> targetGhostKeys,
            out string trashId)
        {
            trashId = string.Empty;
            if (string.IsNullOrEmpty(geminiPath) || !Directory.Exists(geminiPath))
                return OperationResult<DeletionResult>.Fail("未找到有效的 .gemini 目录。");

            HashSet<string> finalConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (targetConvIds != null)
            {
                foreach (string cid in targetConvIds)
                {
                    if (SessionMetadataExtractor.IsLikelyConversationId(cid))
                        finalConvIds.Add(cid.Trim());
                }
            }

            HashSet<string> projIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (targetProjectIds != null)
            {
                foreach (string pid in targetProjectIds)
                    if (!string.IsNullOrWhiteSpace(pid)) projIds.Add(pid.Trim());
            }

            HashSet<string> projNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (targetProjectNames != null)
            {
                foreach (string pn in targetProjectNames)
                    if (!string.IsNullOrWhiteSpace(pn)) projNames.Add(pn.Trim());
            }

            HashSet<string> ghostKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (targetGhostKeys != null)
            {
                foreach (string gk in targetGhostKeys)
                    if (!string.IsNullOrWhiteSpace(gk)) ghostKeys.Add(gk.Trim());
            }

            Dictionary<string, ConversationSummaryInfo> byCid = new Dictionary<string, ConversationSummaryInfo>(StringComparer.OrdinalIgnoreCase);
            if (allLocalSummaries != null)
            {
                for (int i = 0; i < allLocalSummaries.Count; i++)
                {
                    ConversationSummaryInfo s = allLocalSummaries[i];
                    if (s == null || !SessionMetadataExtractor.IsLikelyConversationId(s.ConversationId)) continue;
                    string c = s.ConversationId.Trim();
                    byCid[c] = s;
                    if (MatchesProjectSelection(s, projIds, ghostKeys, projNames))
                        finalConvIds.Add(c);
                }

                // Expand child subagent closure
                bool added = true;
                while (added)
                {
                    added = false;
                    for (int i = 0; i < allLocalSummaries.Count; i++)
                    {
                        ConversationSummaryInfo s = allLocalSummaries[i];
                        if (s == null || !SessionMetadataExtractor.IsLikelyConversationId(s.ConversationId)) continue;
                        string c = s.ConversationId.Trim();
                        string p = s.ParentConversationId != null ? s.ParentConversationId.Trim() : string.Empty;
                        if (!finalConvIds.Contains(c) && !string.IsNullOrEmpty(p) && finalConvIds.Contains(p))
                        {
                            finalConvIds.Add(c);
                            added = true;
                        }
                    }
                }
            }

            string kind = (projIds.Count > 0 || projNames.Count > 0 || ghostKeys.Count > 0) ? "project" : "session";
            string summaryLabel = BuildSessionDeleteSummary(kind, finalConvIds, projIds, projNames, localProjects, byCid);

            string trashFolder = CreateTrashBatchFolder(geminiPath, out trashId);
            string payloadDir = Path.Combine(trashFolder, "payload");
            Directory.CreateDirectory(payloadDir);

            List<object> manifestEntries = new List<object>();
            long totalArchivedBytes = 0;
            int fileSeq = 0;

            string ideRoot = Path.Combine(geminiPath, "antigravity");
            string cliRoot = Path.Combine(geminiPath, "antigravity-cli");
            string[] dataRoots = new string[] { ideRoot, cliRoot };

            // 1. Archive matching .pb entries (IDE & CLI)
            string idePbRel = string.Empty;
            string cliPbRel = string.Empty;
            byte[] idePbNodes = SessionLifecycleService.ExportProtoEntriesForCids(
                Path.Combine(ideRoot, "agyhub_summaries_proto.pb"), finalConvIds);
            if (idePbNodes != null && idePbNodes.Length > 0)
            {
                idePbRel = "ide_pb_entries.pb";
                File.WriteAllBytes(Path.Combine(trashFolder, idePbRel), idePbNodes);
                totalArchivedBytes += idePbNodes.Length;
            }
            byte[] cliPbNodes = SessionLifecycleService.ExportProtoEntriesForCids(
                Path.Combine(cliRoot, "agyhub_summaries_proto.pb"), finalConvIds);
            if (cliPbNodes != null && cliPbNodes.Length > 0)
            {
                cliPbRel = "cli_pb_entries.pb";
                File.WriteAllBytes(Path.Combine(trashFolder, cliPbRel), cliPbNodes);
                totalArchivedBytes += cliPbNodes.Length;
            }

            // 2. Archive matching conversation_summaries.db rows
            string summaryRowsRel = string.Empty;
            string summaryDbPath = Path.Combine(ideRoot, "conversation_summaries.db");
            if (File.Exists(summaryDbPath) && finalConvIds.Count > 0)
            {
                try
                {
                    Dictionary<string, SummaryDbRowInfo> snap = SummarySqliteBridge.ReadSummaryDbSnapshot(summaryDbPath);
                    List<object> savedRows = new List<object>();
                    foreach (string cid in finalConvIds)
                    {
                        SummaryDbRowInfo row;
                        if (snap.TryGetValue(cid, out row) && row != null)
                        {
                            ConversationSummaryInfo memInfo;
                            byCid.TryGetValue(cid, out memInfo);
                            Dictionary<string, object> rd = new Dictionary<string, object>();
                            rd["cid"] = row.ConversationId ?? cid;
                            rd["title"] = row.Title ?? string.Empty;
                            rd["preview"] = row.Preview ?? string.Empty;
                            rd["stepCount"] = row.StepCount;
                            rd["lastModifiedTime"] = row.LastModifiedTime ?? string.Empty;
                            rd["workspaceUris"] = row.WorkspaceUris ?? string.Empty;
                            rd["projectId"] = row.ProjectId ?? string.Empty;
                            rd["parentConversationId"] = row.ParentConversationId ?? string.Empty;
                            rd["nestingDepth"] = row.NestingDepth;
                            rd["projectName"] = memInfo != null ? (memInfo.ProjectName ?? string.Empty) : string.Empty;
                            rd["rawSummaryBase64"] = (row.RawSummaryBytes != null && row.RawSummaryBytes.Length > 0)
                                ? Convert.ToBase64String(row.RawSummaryBytes)
                                : string.Empty;
                            savedRows.Add(rd);
                        }
                    }
                    if (savedRows.Count > 0)
                    {
                        summaryRowsRel = "summary_rows.json";
                        string rowsJson = UnifiedJson.Serialize(savedRows, false);
                        File.WriteAllText(Path.Combine(trashFolder, summaryRowsRel), rowsJson, new UTF8Encoding(false));
                        totalArchivedBytes += Encoding.UTF8.GetByteCount(rowsJson);
                    }
                }
                catch { }
            }

            // 3. Archive per-session DBs, brain dirs, and annotations
            for (int r = 0; r < dataRoots.Length; r++)
            {
                string convDir = Path.Combine(dataRoots[r], "conversations");
                string brainDir = Path.Combine(dataRoots[r], "brain");
                string annotDir = Path.Combine(dataRoots[r], "annotations");

                foreach (string cid in finalConvIds)
                {
                    if (!SessionMetadataExtractor.IsLikelyConversationId(cid) ||
                        string.Equals(cid, "bin", StringComparison.OrdinalIgnoreCase)) continue;

                    for (int e = 0; e < SessionLifecycleService.SessionDbDeleteExtensions.Length; e++)
                    {
                        string dbFile = Path.Combine(convDir, cid + SessionLifecycleService.SessionDbDeleteExtensions[e]);
                        if (File.Exists(dbFile) && SessionLifecycleService.IsPathContainedUnder(convDir, dbFile))
                        {
                            long sz;
                            if (ArchiveSingleFileToPayload(dbFile, payloadDir, ref fileSeq, manifestEntries, "db", out sz))
                                totalArchivedBytes += sz;
                        }
                    }

                    string bDir = Path.Combine(brainDir, cid);
                    if (Directory.Exists(bDir) && SessionLifecycleService.IsPathContainedUnder(brainDir, bDir))
                    {
                        long sz;
                        if (ArchiveDirectoryToPayload(bDir, payloadDir, ref fileSeq, manifestEntries, "brain", out sz))
                            totalArchivedBytes += sz;
                    }

                    string aFile = Path.Combine(annotDir, cid + ".pbtxt");
                    if (File.Exists(aFile) && SessionLifecycleService.IsPathContainedUnder(annotDir, aFile))
                    {
                        long sz;
                        if (ArchiveSingleFileToPayload(aFile, payloadDir, ref fileSeq, manifestEntries, "annotation", out sz))
                            totalArchivedBytes += sz;
                    }
                }
            }

            // 4. Archive matching project JSON files
            if (projIds.Count > 0 || (projIds.Count == 0 && ghostKeys.Count == 0 && projNames.Count > 0))
            {
                string projDir = Path.Combine(geminiPath, "config", "projects");
                if (Directory.Exists(projDir))
                {
                    string[] projFiles = Directory.GetFiles(projDir, "*.json");
                    for (int i = 0; i < projFiles.Length; i++)
                    {
                        string pf = projFiles[i];
                        string pId = Path.GetFileNameWithoutExtension(pf);
                        bool match = projIds.Contains(pId);
                        if (!match && projIds.Count == 0 && ghostKeys.Count == 0 && projNames.Count > 0)
                        {
                            try
                            {
                                string nm = DirectStreamArchiver.ExtractJsonValue(SessionLifecycleService.ReadUtf8TextShared(pf), "name");
                                if (!string.IsNullOrEmpty(nm) && projNames.Contains(nm)) match = true;
                            }
                            catch { }
                        }
                        if (match)
                        {
                            long sz;
                            if (ArchiveSingleFileToPayload(pf, payloadDir, ref fileSeq, manifestEntries, "project_json", out sz))
                                totalArchivedBytes += sz;
                        }
                    }
                }
            }

            WriteTrashManifest(trashFolder, trashId, kind, summaryLabel, finalConvIds.Count > 0 ? finalConvIds.Count : manifestEntries.Count,
                totalArchivedBytes, idePbRel, cliPbRel, summaryRowsRel, manifestEntries, null);

            // Execute clean deletion from active workspace
            OperationResult<DeletionResult> delOp = SessionLifecycleService.DeleteSessionsAndProjectsResult(
                geminiPath, allLocalSummaries, localProjects, targetConvIds, targetProjectNames, targetProjectIds, targetGhostKeys);

            if (!delOp.Ok)
            {
                // Roll back unused trash batch if delete failed
                DeleteDirQuiet(trashFolder);
                trashId = string.Empty;
                return delOp;
            }

            if (manifestEntries.Count == 0 && string.IsNullOrEmpty(idePbRel) && string.IsNullOrEmpty(cliPbRel) && string.IsNullOrEmpty(summaryRowsRel))
            {
                DeleteDirQuiet(trashFolder);
                trashId = string.Empty;
            }
            else
            {
                PruneExpiredTrash(geminiPath, DefaultRetentionDays);
            }

            return delOp;
        }

        public static DeletionResult SoftDeleteOrphans(string geminiPath, OrphanGcInventory inv, out string trashId)
        {
            trashId = string.Empty;
            DeletionResult empty = new DeletionResult();
            if (string.IsNullOrEmpty(geminiPath) || inv == null || inv.TotalOrphanItems == 0)
                return empty;

            string trashFolder = CreateTrashBatchFolder(geminiPath, out trashId);
            string payloadDir = Path.Combine(trashFolder, "payload");
            Directory.CreateDirectory(payloadDir);

            List<object> entries = new List<object>();
            long archivedBytes = 0;
            int seq = 0;

            List<string> allFiles = new List<string>();
            allFiles.AddRange(inv.OrphanDbFiles);
            allFiles.AddRange(inv.OrphanDbBakFiles);
            allFiles.AddRange(inv.OrphanDbJournalFiles);
            allFiles.AddRange(inv.OrphanAnnotationFiles);

            for (int i = 0; i < allFiles.Count; i++)
            {
                if (File.Exists(allFiles[i]))
                {
                    long sz;
                    if (ArchiveSingleFileToPayload(allFiles[i], payloadDir, ref seq, entries, "orphan_file", out sz))
                        archivedBytes += sz;
                }
            }

            for (int i = 0; i < inv.OrphanBrainDirs.Count; i++)
            {
                if (Directory.Exists(inv.OrphanBrainDirs[i]))
                {
                    long sz;
                    if (ArchiveDirectoryToPayload(inv.OrphanBrainDirs[i], payloadDir, ref seq, entries, "orphan_brain", out sz))
                        archivedBytes += sz;
                }
            }

            string summary = string.Format("残留垃圾文件清理（{0} 项，{1:F2} MB）", entries.Count, archivedBytes / (1024.0 * 1024.0));
            WriteTrashManifest(trashFolder, trashId, "orphan", summary, entries.Count, archivedBytes,
                string.Empty, string.Empty, string.Empty, entries, null);

            DeletionResult cleanRes = StorageSlimService.CleanOrphanedInventory(inv);
            if (entries.Count == 0)
            {
                DeleteDirQuiet(trashFolder);
                trashId = string.Empty;
            }
            else
            {
                PruneExpiredTrash(geminiPath, DefaultRetentionDays);
            }
            return cleanRes;
        }

        public static int SoftDeleteLocalCapabilities(string geminiPath, List<CapabilityItem> items, out string trashId)
        {
            trashId = string.Empty;
            if (string.IsNullOrEmpty(geminiPath) || items == null || items.Count == 0)
                return 0;

            string trashFolder = CreateTrashBatchFolder(geminiPath, out trashId);
            string payloadDir = Path.Combine(trashFolder, "payload");
            Directory.CreateDirectory(payloadDir);

            List<object> entries = new List<object>();
            List<object> jsonRestores = new List<object>();
            long archivedBytes = 0;
            int seq = 0;
            string configDir = Path.Combine(geminiPath, "config");

            for (int i = 0; i < items.Count; i++)
            {
                CapabilityItem item = items[i];
                if (item == null || string.IsNullOrEmpty(item.Id)) continue;
                string safeFolder = CapabilityVaultService.EncodeSafeCapabilityFolderName(item.Id);

                if (item.Kind == CapabilityKind.Plugin)
                {
                    string pDir = ResolveExistingDir(Path.Combine(configDir, "plugins"), safeFolder, item.Id);
                    long sz;
                    if (!string.IsNullOrEmpty(pDir) && ArchiveDirectoryToPayload(pDir, payloadDir, ref seq, entries, "plugin_dir", out sz))
                        archivedBytes += sz;
                    CaptureJsonKeyRestore(Path.Combine(configDir, "config.json"), "plugins", item.Id, jsonRestores);
                }
                else if (item.Kind == CapabilityKind.Skill)
                {
                    string sDir = ResolveExistingDir(Path.Combine(configDir, "skills"), safeFolder, item.Id);
                    long sz;
                    if (!string.IsNullOrEmpty(sDir) && ArchiveDirectoryToPayload(sDir, payloadDir, ref seq, entries, "skill_dir", out sz))
                        archivedBytes += sz;
                }
                else if (item.Kind == CapabilityKind.Rule)
                {
                    string rPath = CapabilityVaultService.ResolveLocalRulePath(geminiPath, item.Id);
                    long sz;
                    if (File.Exists(rPath) && ArchiveSingleFileToPayload(rPath, payloadDir, ref seq, entries, "rule_file", out sz))
                        archivedBytes += sz;
                }
                else if (item.Kind == CapabilityKind.McpServer)
                {
                    CaptureJsonKeyRestore(Path.Combine(configDir, "mcp_config.json"), "mcpServers", item.Id, jsonRestores);
                    string schemaDir = ResolveExistingDir(Path.Combine(geminiPath, "antigravity", "mcp"), safeFolder, item.Id);
                    long sz;
                    if (!string.IsNullOrEmpty(schemaDir) && ArchiveDirectoryToPayload(schemaDir, payloadDir, ref seq, entries, "mcp_schema", out sz))
                        archivedBytes += sz;
                }
                else if (item.Kind == CapabilityKind.Hook)
                {
                    CaptureJsonKeyRestore(Path.Combine(configDir, "hooks.json"), null, item.Id, jsonRestores);
                    if (item.CompanionScriptNames != null)
                    {
                        for (int s = 0; s < item.CompanionScriptNames.Count; s++)
                        {
                            string sn = item.CompanionScriptNames[s];
                            if (string.IsNullOrEmpty(sn)) continue;
                            try
                            {
                                string sp = CapabilityVaultService.CombineAndEnsureUnderRoot(configDir, sn);
                                long sz;
                                if (File.Exists(sp) && ArchiveSingleFileToPayload(sp, payloadDir, ref seq, entries, "hook_script", out sz))
                                    archivedBytes += sz;
                            }
                            catch { }
                        }
                    }
                }
            }

            string summary = BuildCapDeleteSummary("本机扩展与配置", items);
            WriteTrashManifest(trashFolder, trashId, "capability_local", summary, items.Count, archivedBytes,
                string.Empty, string.Empty, string.Empty, entries, jsonRestores);

            int deleted = CapabilityVaultService.DeleteCapabilitiesFromLocal(geminiPath, items);
            if (deleted == 0 || (entries.Count == 0 && jsonRestores.Count == 0))
            {
                DeleteDirQuiet(trashFolder);
                trashId = string.Empty;
            }
            else
            {
                PruneExpiredTrash(geminiPath, DefaultRetentionDays);
            }
            return deleted;
        }

        public static int SoftDeleteRepoCapabilities(string repoPath, string geminiPath, List<CapabilityItem> items, out string trashId)
        {
            trashId = string.Empty;
            if (string.IsNullOrEmpty(repoPath) || string.IsNullOrEmpty(geminiPath) || items == null || items.Count == 0)
                return 0;

            string trashFolder = CreateTrashBatchFolder(geminiPath, out trashId);
            string payloadDir = Path.Combine(trashFolder, "payload");
            Directory.CreateDirectory(payloadDir);

            List<object> entries = new List<object>();
            long archivedBytes = 0;
            int seq = 0;

            for (int i = 0; i < items.Count; i++)
            {
                CapabilityItem item = items[i];
                if (item == null || string.IsNullOrEmpty(item.Id)) continue;
                string safeFolder = CapabilityVaultService.EncodeSafeCapabilityFolderName(item.Id);
                long sz;

                if (item.Kind == CapabilityKind.Plugin)
                {
                    string d = CapabilityVaultService.CombineAndEnsureUnderRoot(Path.Combine(repoPath, "plugins"), safeFolder);
                    if (Directory.Exists(d) && ArchiveDirectoryToPayload(d, payloadDir, ref seq, entries, "repo_plugin", out sz)) archivedBytes += sz;
                }
                else if (item.Kind == CapabilityKind.Skill)
                {
                    string d = CapabilityVaultService.CombineAndEnsureUnderRoot(Path.Combine(repoPath, "skills"), safeFolder);
                    if (Directory.Exists(d) && ArchiveDirectoryToPayload(d, payloadDir, ref seq, entries, "repo_skill", out sz)) archivedBytes += sz;
                }
                else if (item.Kind == CapabilityKind.Rule)
                {
                    string f = CapabilityVaultService.ResolveRepoRulePath(repoPath, item.Id);
                    if (File.Exists(f) && ArchiveSingleFileToPayload(f, payloadDir, ref seq, entries, "repo_rule", out sz)) archivedBytes += sz;
                }
                else if (item.Kind == CapabilityKind.McpServer)
                {
                    string d = CapabilityVaultService.CombineAndEnsureUnderRoot(Path.Combine(repoPath, "mcp"), safeFolder);
                    if (Directory.Exists(d) && ArchiveDirectoryToPayload(d, payloadDir, ref seq, entries, "repo_mcp", out sz)) archivedBytes += sz;
                }
                else if (item.Kind == CapabilityKind.Hook)
                {
                    string d = CapabilityVaultService.CombineAndEnsureUnderRoot(Path.Combine(repoPath, "hooks"), safeFolder);
                    if (Directory.Exists(d) && ArchiveDirectoryToPayload(d, payloadDir, ref seq, entries, "repo_hook", out sz)) archivedBytes += sz;
                }
            }

            string summary = BuildCapDeleteSummary("配置同步库", items);
            WriteTrashManifest(trashFolder, trashId, "capability_repo", summary, items.Count, archivedBytes,
                string.Empty, string.Empty, string.Empty, entries, null);

            int deleted = CapabilityVaultService.DeleteCapabilitiesFromRepository(repoPath, items);
            if (deleted == 0 || entries.Count == 0)
            {
                DeleteDirQuiet(trashFolder);
                trashId = string.Empty;
            }
            else
            {
                PruneExpiredTrash(geminiPath, DefaultRetentionDays);
            }
            return deleted;
        }

        // ══════════════════════════════════════════════════════════════════
        // 2. LIST, RESTORE & PURGE TRASH
        // ══════════════════════════════════════════════════════════════════

        public static Dictionary<string, object> ListTrashPayload(string geminiPath)
        {
            string trashRoot = GetTrashRoot(geminiPath);
            PruneExpiredTrash(geminiPath, DefaultRetentionDays);

            List<object> items = new List<object>();
            long totalBytes = 0;

            if (Directory.Exists(trashRoot))
            {
                string[] dirs = Directory.GetDirectories(trashRoot, "trash_*", SearchOption.TopDirectoryOnly);
                List<Dictionary<string, object>> parsedList = new List<Dictionary<string, object>>();

                for (int i = 0; i < dirs.Length; i++)
                {
                    string d = dirs[i];
                    string manPath = Path.Combine(d, "manifest.json");
                    if (!File.Exists(manPath)) continue;
                    try
                    {
                        object parsed;
                        if (!UnifiedJson.TryParse(SessionLifecycleService.ReadUtf8TextShared(manPath), out parsed)) continue;
                        Dictionary<string, object> dict = UnifiedJson.AsDict(parsed);
                        if (dict == null) continue;

                        long bytes = ReadLong(dict, "totalBytes", 0L);
                        totalBytes += bytes;

                        Dictionary<string, object> row = new Dictionary<string, object>();
                        row["id"] = UnifiedJson.GetString(dict, "id", Path.GetFileName(d));
                        row["kind"] = UnifiedJson.GetString(dict, "kind", "unknown");
                        row["summary"] = UnifiedJson.GetString(dict, "summary", Path.GetFileName(d));
                        row["createdAtUtc"] = UnifiedJson.GetString(dict, "createdAtUtc", string.Empty);
                        row["itemCount"] = UnifiedJson.GetInt(dict, "itemCount", 0);
                        row["totalBytes"] = bytes;
                        row["sizeMegabytes"] = Math.Round(bytes / (1024.0 * 1024.0), 2);
                        row["requiresIdeClosed"] = (row["kind"] as string) == "session" || (row["kind"] as string) == "project";
                        parsedList.Add(row);
                    }
                    catch { }
                }

                parsedList.Sort(delegate(Dictionary<string, object> a, Dictionary<string, object> b)
                {
                    return string.Compare(
                        UnifiedJson.GetString(b, "createdAtUtc", string.Empty),
                        UnifiedJson.GetString(a, "createdAtUtc", string.Empty),
                        StringComparison.OrdinalIgnoreCase);
                });

                for (int i = 0; i < parsedList.Count; i++) items.Add(parsedList[i]);
            }

            Dictionary<string, object> resp = new Dictionary<string, object>();
            resp["trashRoot"] = trashRoot;
            resp["retentionDays"] = DefaultRetentionDays;
            resp["totalCount"] = items.Count;
            resp["totalBytes"] = totalBytes;
            resp["totalMegabytes"] = Math.Round(totalBytes / (1024.0 * 1024.0), 2);
            resp["items"] = items;
            return resp;
        }

        public static bool TrashItemRequiresIdeClosed(string geminiPath, string trashId)
        {
            string trashDir;
            if (!TryResolveTrashDir(geminiPath, trashId, out trashDir)) return false;
            string manPath = Path.Combine(trashDir, "manifest.json");
            if (!File.Exists(manPath)) return false;
            try
            {
                object parsed;
                if (UnifiedJson.TryParse(SessionLifecycleService.ReadUtf8TextShared(manPath), out parsed))
                {
                    Dictionary<string, object> dict = UnifiedJson.AsDict(parsed);
                    string kind = UnifiedJson.GetString(dict, "kind", string.Empty);
                    return kind == "session" || kind == "project";
                }
            }
            catch { }
            return false;
        }

        public static TrashRestoreResult RestoreTrashItem(string geminiPath, string repoPath, string trashId)
        {
            TrashRestoreResult res = new TrashRestoreResult();
            res.TrashId = trashId ?? string.Empty;

            string trashDir;
            if (!TryResolveTrashDir(geminiPath, trashId, out trashDir))
            {
                res.Ok = false;
                res.ErrorMessage = "未找到指定的回收站记录，或路径不合法。";
                return res;
            }

            string manPath = Path.Combine(trashDir, "manifest.json");
            if (!File.Exists(manPath))
            {
                res.Ok = false;
                res.ErrorMessage = "回收站清单文件损坏或缺失。";
                return res;
            }

            object parsed;
            if (!UnifiedJson.TryParse(SessionLifecycleService.ReadUtf8TextShared(manPath), out parsed))
            {
                res.Ok = false;
                res.ErrorMessage = "回收站清单 JSON 解析失败。";
                return res;
            }
            Dictionary<string, object> manifest = UnifiedJson.AsDict(parsed);
            if (manifest == null)
            {
                res.Ok = false;
                res.ErrorMessage = "回收站清单格式无效。";
                return res;
            }

            res.Kind = UnifiedJson.GetString(manifest, "kind", string.Empty);
            string payloadDir = Path.Combine(trashDir, "payload");

            try
            {
                // 1. Restore archived files & directories
                object entriesObj;
                if (manifest.TryGetValue("entries", out entriesObj))
                {
                    List<object> entries = UnifiedJson.AsList(entriesObj);
                    if (entries != null)
                    {
                        for (int i = 0; i < entries.Count; i++)
                        {
                            Dictionary<string, object> ed = UnifiedJson.AsDict(entries[i]);
                            if (ed == null) continue;
                            string origPath = UnifiedJson.GetString(ed, "originalPath", string.Empty);
                            string relPath = UnifiedJson.GetString(ed, "trashRelPath", string.Empty);
                            bool isDir = UnifiedJson.GetBool(ed, "isDirectory", false);
                            if (string.IsNullOrEmpty(origPath) || string.IsNullOrEmpty(relPath)) continue;

                            string srcPath = CapabilityVaultService.CombineAndEnsureUnderRoot(payloadDir, relPath);
                            if (!IsAllowedTrashRestoreDestination(origPath, geminiPath, repoPath))
                            {
                                res.Warnings.Add("跳过越界恢复路径: " + origPath);
                                continue;
                            }

                            if (isDir)
                            {
                                if (Directory.Exists(srcPath))
                                {
                                    CopyDirectoryOverwrite(srcPath, origPath);
                                    res.RestoredFilesCount++;
                                }
                            }
                            else
                            {
                                if (File.Exists(srcPath))
                                {
                                    string parent = Path.GetDirectoryName(origPath);
                                    if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
                                        Directory.CreateDirectory(parent);
                                    if (File.Exists(origPath))
                                        UnifiedJson.CreateTimestampedBackup(origPath, 5);
                                    File.Copy(srcPath, origPath, true);
                                    res.RestoredFilesCount++;
                                }
                            }
                        }
                    }
                }

                // 2. Restore JSON key-level deletions (Plugin / MCP / Hook)
                object jsonRestoresObj;
                if (manifest.TryGetValue("jsonRestores", out jsonRestoresObj))
                {
                    List<object> jrList = UnifiedJson.AsList(jsonRestoresObj);
                    if (jrList != null)
                    {
                        for (int i = 0; i < jrList.Count; i++)
                        {
                            Dictionary<string, object> jr = UnifiedJson.AsDict(jrList[i]);
                            if (jr == null) continue;
                            string filePath = UnifiedJson.GetString(jr, "filePath", string.Empty);
                            string section = UnifiedJson.GetString(jr, "parentSection", string.Empty);
                            string key = UnifiedJson.GetString(jr, "key", string.Empty);
                            string rawVal = UnifiedJson.GetString(jr, "valueJson", string.Empty);
                            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(rawVal)) continue;
                            if (!IsAllowedTrashRestoreDestination(filePath, geminiPath, repoPath)) continue;

                            string existing = File.Exists(filePath)
                                ? SessionLifecycleService.ReadUtf8TextShared(filePath)
                                : (string.IsNullOrEmpty(section) ? "{}" : ("{\"" + UnifiedJson.EscapeString(section) + "\":{}}"));
                            string patched = UnifiedJson.PatchObjectKeyPreservingComments(
                                existing, string.IsNullOrEmpty(section) ? null : section, key, rawVal);
                            if (File.Exists(filePath)) UnifiedJson.CreateTimestampedBackup(filePath, 5);
                            SessionLifecycleService.WriteUtf8TextAtomicWithBackup(filePath, patched, false, null);
                            res.RestoredFilesCount++;
                        }
                    }
                }

                // 3. Re-merge IDE & CLI .pb entries
                string idePbRel = UnifiedJson.GetString(manifest, "idePbRelPath", string.Empty);
                if (!string.IsNullOrEmpty(idePbRel))
                {
                    string srcPb = CapabilityVaultService.CombineAndEnsureUnderRoot(trashDir, idePbRel);
                    if (File.Exists(srcPb))
                    {
                        string destPb = Path.Combine(geminiPath, "antigravity", "agyhub_summaries_proto.pb");
                        res.RestoredProtoEntriesCount += SessionLifecycleService.MergeArchivedProtoEntriesIntoPb(destPb, File.ReadAllBytes(srcPb));
                    }
                }
                string cliPbRel = UnifiedJson.GetString(manifest, "cliPbRelPath", string.Empty);
                if (!string.IsNullOrEmpty(cliPbRel))
                {
                    string srcCliPb = CapabilityVaultService.CombineAndEnsureUnderRoot(trashDir, cliPbRel);
                    if (File.Exists(srcCliPb))
                    {
                        string destCliPb = Path.Combine(geminiPath, "antigravity-cli", "agyhub_summaries_proto.pb");
                        res.RestoredProtoEntriesCount += SessionLifecycleService.MergeArchivedProtoEntriesIntoPb(destCliPb, File.ReadAllBytes(srcCliPb));
                    }
                }

                // 4. Re-upsert conversation_summaries.db rows
                string sumRowsRel = UnifiedJson.GetString(manifest, "summaryRowsRelPath", string.Empty);
                string sumDbPath = Path.Combine(geminiPath, "antigravity", "conversation_summaries.db");
                if (!string.IsNullOrEmpty(sumRowsRel) && File.Exists(sumDbPath))
                {
                    string rowsFile = CapabilityVaultService.CombineAndEnsureUnderRoot(trashDir, sumRowsRel);
                    if (File.Exists(rowsFile))
                    {
                        object rParsed;
                        if (UnifiedJson.TryParse(SessionLifecycleService.ReadUtf8TextShared(rowsFile), out rParsed))
                        {
                            List<object> rowsArr = UnifiedJson.AsList(rParsed);
                            if (rowsArr != null && rowsArr.Count > 0)
                            {
                                List<SummaryDbUpsertItem> upserts = new List<SummaryDbUpsertItem>(rowsArr.Count);
                                for (int i = 0; i < rowsArr.Count; i++)
                                {
                                    Dictionary<string, object> rd = UnifiedJson.AsDict(rowsArr[i]);
                                    if (rd == null) continue;
                                    SummaryDbUpsertItem u = new SummaryDbUpsertItem();
                                    u.ConversationId = UnifiedJson.GetString(rd, "cid", string.Empty);
                                    u.Title = UnifiedJson.GetString(rd, "title", string.Empty);
                                    u.Preview = UnifiedJson.GetString(rd, "preview", string.Empty);
                                    u.StepCount = UnifiedJson.GetInt(rd, "stepCount", 0);
                                    string lm = UnifiedJson.GetString(rd, "lastModifiedTime", string.Empty);
                                    u.LastModifiedTimeIso = string.IsNullOrEmpty(lm) ? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) : lm;
                                    u.LastUserInputTimeIso = u.LastModifiedTimeIso;
                                    u.WorkspaceUrisJson = UnifiedJson.GetString(rd, "workspaceUris", string.Empty);
                                    u.ProjectId = UnifiedJson.GetString(rd, "projectId", string.Empty);
                                    u.ParentConversationId = UnifiedJson.GetString(rd, "parentConversationId", string.Empty);
                                    u.NestingDepth = UnifiedJson.GetInt(rd, "nestingDepth", 0);
                                    u.ProjectName = UnifiedJson.GetString(rd, "projectName", string.Empty);
                                    string b64 = UnifiedJson.GetString(rd, "rawSummaryBase64", string.Empty);
                                    if (!string.IsNullOrEmpty(b64))
                                    {
                                        try { u.RawSummaryBytes = Convert.FromBase64String(b64); } catch { }
                                    }
                                    if (!string.IsNullOrEmpty(u.ConversationId)) upserts.Add(u);
                                }
                                if (upserts.Count > 0)
                                {
                                    List<string> dbWarns;
                                    res.RestoredDbRowsCount = SummarySqliteBridge.UpsertSessionsInSummaryDb(sumDbPath, upserts, out dbWarns);
                                    if (dbWarns != null && dbWarns.Count > 0) res.Warnings.AddRange(dbWarns);
                                }
                            }
                        }
                    }
                }

                if (res.Kind == "capability_repo" && !string.IsNullOrEmpty(repoPath))
                {
                    try { CapabilityVaultService.WriteRepositoryManifest(repoPath); } catch { }
                }

                DeleteDirQuiet(trashDir);
                res.Ok = true;
                return res;
            }
            catch (Exception ex)
            {
                res.Ok = false;
                res.ErrorMessage = "从回收站还原失败: " + ex.Message;
                return res;
            }
        }

        public static bool PurgeTrashItem(string geminiPath, string trashId, out string error)
        {
            error = string.Empty;
            string trashDir;
            if (!TryResolveTrashDir(geminiPath, trashId, out trashDir))
            {
                error = "未找到指定的回收站记录，或路径不合法。";
                return false;
            }
            if (!DeleteDirQuiet(trashDir))
            {
                error = "彻底删除回收站记录失败（文件可能被占用）。";
                return false;
            }
            return true;
        }

        public static int EmptyTrash(string geminiPath)
        {
            string trashRoot = GetTrashRoot(geminiPath);
            if (!Directory.Exists(trashRoot)) return 0;
            int removed = 0;
            string[] dirs = Directory.GetDirectories(trashRoot, "trash_*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < dirs.Length; i++)
            {
                if (DeleteDirQuiet(dirs[i])) removed++;
            }
            return removed;
        }

        public static int PruneExpiredTrash(string geminiPath, int retentionDays)
        {
            if (retentionDays <= 0) retentionDays = DefaultRetentionDays;
            string trashRoot = GetTrashRoot(geminiPath);
            if (!Directory.Exists(trashRoot)) return 0;

            int pruned = 0;
            DateTime cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            try
            {
                string[] dirs = Directory.GetDirectories(trashRoot, "trash_*", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < dirs.Length; i++)
                {
                    try
                    {
                        DirectoryInfo di = new DirectoryInfo(dirs[i]);
                        if (di.LastWriteTimeUtc < cutoff)
                        {
                            if (DeleteDirQuiet(dirs[i])) pruned++;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return pruned;
        }

        // ══════════════════════════════════════════════════════════════════
        // 3. .bak.* CONFIG VERSION LIST & ONE-CLICK RESTORE
        // ══════════════════════════════════════════════════════════════════

        public static List<Dictionary<string, object>> ListConfigBackups(
            string geminiPath,
            string homePath,
            string repoPath,
            string filterOriginalPath)
        {
            List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string normFilter = string.Empty;
            if (!string.IsNullOrWhiteSpace(filterOriginalPath))
            {
                string canon;
                string err;
                if (ApiRouter.TryValidateCanonicalLocalPath(filterOriginalPath, out canon, out err))
                {
                    normFilter = canon;
                }
            }

            if (!string.IsNullOrEmpty(normFilter))
            {
                string dir = Path.GetDirectoryName(normFilter);
                string fn = Path.GetFileName(normFilter);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && !string.IsNullOrEmpty(fn))
                {
                    ScanBakFilesInDirectory(dir, fn + ".bak.*", false, geminiPath, homePath, repoPath, normFilter, seen, results);
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(geminiPath) && Directory.Exists(geminiPath))
                {
                    ScanBakFilesInDirectory(geminiPath, "*.bak.*", false, geminiPath, homePath, repoPath, null, seen, results);
                    string cfgDir = Path.Combine(geminiPath, "config");
                    if (Directory.Exists(cfgDir))
                        ScanBakFilesInDirectory(cfgDir, "*.bak.*", true, geminiPath, homePath, repoPath, null, seen, results);
                }
                if (!string.IsNullOrEmpty(repoPath) && Directory.Exists(repoPath))
                {
                    ScanBakFilesInDirectory(repoPath, "*.bak.*", false, geminiPath, homePath, repoPath, null, seen, results);
                }
                // Also scan registered project workspace roots
                try
                {
                    string projDir = Path.Combine(geminiPath ?? string.Empty, "config", "projects");
                    if (Directory.Exists(projDir))
                    {
                        string[] pFiles = Directory.GetFiles(projDir, "*.json");
                        for (int i = 0; i < pFiles.Length; i++)
                        {
                            string raw = SessionLifecycleService.ReadUtf8TextShared(pFiles[i]);
                            List<string> uris = DirectStreamArchiver.ExtractAllProjectFolderUris(raw);
                            for (int u = 0; u < uris.Count; u++)
                            {
                                string dp = WorkspaceService.UriToDisplayPath(uris[u]);
                                if (string.IsNullOrEmpty(dp) || !Directory.Exists(dp)) continue;
                                ScanBakFilesInDirectory(dp, "*.bak.*", false, geminiPath, homePath, repoPath, null, seen, results);
                                string agDir = Path.Combine(dp, ".agents");
                                if (Directory.Exists(agDir))
                                    ScanBakFilesInDirectory(agDir, "*.bak.*", true, geminiPath, homePath, repoPath, null, seen, results);
                            }
                        }
                    }
                }
                catch { }
            }

            results.Sort(delegate(Dictionary<string, object> a, Dictionary<string, object> b)
            {
                return string.Compare(
                    UnifiedJson.GetString(b, "modifiedUtc", string.Empty),
                    UnifiedJson.GetString(a, "modifiedUtc", string.Empty),
                    StringComparison.OrdinalIgnoreCase);
            });
            return results;
        }

        public static ConfigBackupRestoreResult RestoreConfigBackup(
            string geminiPath,
            string homePath,
            string repoPath,
            string rawBackupPath)
        {
            ConfigBackupRestoreResult res = new ConfigBackupRestoreResult();
            string canonBak;
            string pathErr;
            if (!ApiRouter.TryValidateCanonicalLocalPath(rawBackupPath, out canonBak, out pathErr))
            {
                res.Ok = false;
                res.HttpStatusCode = 400;
                res.ErrorMessage = pathErr;
                return res;
            }
            if (!File.Exists(canonBak))
            {
                res.Ok = false;
                res.HttpStatusCode = 404;
                res.ErrorMessage = "未找到指定的历史备份文件。";
                return res;
            }

            string origPath;
            string stamp;
            if (!TryParseBackupOriginalPath(canonBak, out origPath, out stamp))
            {
                res.Ok = false;
                res.HttpStatusCode = 400;
                res.ErrorMessage = "目标文件不是有效的 .bak.* 历史版本文件。";
                return res;
            }

            string canonOrig;
            int errCode;
            string errMsg;
            if (!AntigravityConfigService.TryValidateEditorFilePath(
                geminiPath, homePath, repoPath, origPath, true, out canonOrig, out errCode, out errMsg))
            {
                res.Ok = false;
                res.HttpStatusCode = errCode > 0 ? errCode : 403;
                res.ErrorMessage = errMsg ?? "拒绝还原允许目录之外的配置文件。";
                return res;
            }

            string content = SessionLifecycleService.ReadUtf8TextShared(canonBak);
            string ext = Path.GetExtension(canonOrig).ToLowerInvariant();
            if (ext == ".json" || ext == ".jsonc")
            {
                try { UnifiedJson.Parse(content); }
                catch (Exception ex)
                {
                    res.Ok = false;
                    res.HttpStatusCode = 400;
                    res.ErrorMessage = "该历史版本 JSON 语法校验未通过，已拒绝还原: " + ex.Message;
                    return res;
                }
            }

            string safetyBak = File.Exists(canonOrig) ? UnifiedJson.CreateTimestampedBackup(canonOrig, 5) : string.Empty;
            SessionLifecycleService.WriteUtf8TextAtomicWithBackup(canonOrig, content, false, null);

            res.Ok = true;
            res.HttpStatusCode = 200;
            res.OriginalPath = canonOrig;
            res.RestoredFromPath = canonBak;
            res.SafetyBackupPath = safetyBak ?? string.Empty;
            return res;
        }

        private static void ScanBakFilesInDirectory(
            string dir,
            string pattern,
            bool recursive,
            string geminiPath,
            string homePath,
            string repoPath,
            string filterOrigPath,
            HashSet<string> seen,
            List<Dictionary<string, object>> results)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            try
            {
                SearchOption opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                string[] files = Directory.GetFiles(dir, pattern, opt);
                for (int i = 0; i < files.Length; i++)
                {
                    string bf = files[i];
                    if (!seen.Add(bf)) continue;

                    string origPath, stamp;
                    if (!TryParseBackupOriginalPath(bf, out origPath, out stamp)) continue;
                    if (!string.IsNullOrEmpty(filterOrigPath) &&
                        !string.Equals(origPath, filterOrigPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string canonOrig;
                    int ec;
                    string em;
                    if (!AntigravityConfigService.TryValidateEditorFilePath(
                        geminiPath, homePath, repoPath, origPath, true, out canonOrig, out ec, out em))
                        continue;

                    FileInfo fi = new FileInfo(bf);
                    string preview = string.Empty;
                    try
                    {
                        string raw = SessionLifecycleService.ReadUtf8TextShared(bf);
                        preview = raw.Length > 240 ? raw.Substring(0, 240) + "..." : raw;
                    }
                    catch { }

                    Dictionary<string, object> row = new Dictionary<string, object>();
                    row["backupPath"] = bf;
                    row["backupFileName"] = fi.Name;
                    row["originalPath"] = canonOrig;
                    row["originalFileName"] = Path.GetFileName(canonOrig);
                    row["timestampLabel"] = stamp;
                    row["modifiedUtc"] = fi.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture);
                    row["modifiedLocal"] = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    row["sizeBytes"] = fi.Length;
                    row["preview"] = preview;
                    results.Add(row);
                }
            }
            catch { }
        }

        public static bool TryParseBackupOriginalPath(string backupPath, out string originalPath, out string stamp)
        {
            originalPath = null;
            stamp = null;
            if (string.IsNullOrWhiteSpace(backupPath)) return false;
            string fn = Path.GetFileName(backupPath);
            if (string.IsNullOrEmpty(fn)) return false;
            int idx = fn.LastIndexOf(".bak.", StringComparison.OrdinalIgnoreCase);
            if (idx <= 0 || idx + 5 >= fn.Length) return false;
            string origName = fn.Substring(0, idx);
            stamp = fn.Substring(idx + 5);
            string dir = Path.GetDirectoryName(backupPath);
            originalPath = string.IsNullOrEmpty(dir) ? origName : Path.Combine(dir, origName);
            return true;
        }

        // ══════════════════════════════════════════════════════════════════
        // 4. PENDING LAYER-3 (.pb) AUTO-FLUSH QUEUE
        // ══════════════════════════════════════════════════════════════════

        public static string GetPendingPbSyncPath(string geminiPath)
        {
            string safeGemini = string.IsNullOrWhiteSpace(geminiPath)
                ? Path.Combine(WorkspaceService.ResolveDefaultHomePath(), ".gemini")
                : geminiPath.Trim();
            return Path.Combine(safeGemini, ".companion_pending_pb_sync.json");
        }

        public static Dictionary<string, string> LoadPendingPbSync(string geminiPath)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string path = GetPendingPbSyncPath(geminiPath);
            if (!File.Exists(path)) return map;
            try
            {
                object parsed;
                if (!UnifiedJson.TryParse(SessionLifecycleService.ReadUtf8TextShared(path), out parsed)) return map;
                Dictionary<string, object> root = UnifiedJson.AsDict(parsed);
                if (root == null) return map;
                object itemsObj;
                if (!root.TryGetValue("pendingTitles", out itemsObj)) return map;
                Dictionary<string, object> items = UnifiedJson.AsDict(itemsObj);
                if (items == null) return map;
                foreach (KeyValuePair<string, object> kvp in items)
                {
                    string val = kvp.Value as string;
                    if (!string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(val))
                        map[kvp.Key.Trim()] = val.Trim();
                }
            }
            catch { }
            return map;
        }

        public static int GetPendingPbSyncCount(string geminiPath)
        {
            return LoadPendingPbSync(geminiPath).Count;
        }

        public static void EnqueuePendingPbSync(string geminiPath, Dictionary<string, string> cidToTitle)
        {
            if (string.IsNullOrEmpty(geminiPath) || cidToTitle == null || cidToTitle.Count == 0) return;
            Dictionary<string, string> cur = LoadPendingPbSync(geminiPath);
            foreach (KeyValuePair<string, string> kvp in cidToTitle)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                    cur[kvp.Key.Trim()] = kvp.Value.Trim();
            }
            SavePendingPbSync(geminiPath, cur);
        }

        public static void RemovePendingPbSync(string geminiPath, IEnumerable<string> cids)
        {
            if (string.IsNullOrEmpty(geminiPath) || cids == null) return;
            Dictionary<string, string> cur = LoadPendingPbSync(geminiPath);
            if (cur.Count == 0) return;
            bool changed = false;
            foreach (string c in cids)
            {
                if (!string.IsNullOrWhiteSpace(c) && cur.Remove(c.Trim()))
                    changed = true;
            }
            if (changed) SavePendingPbSync(geminiPath, cur);
        }

        public static bool TryFlushPendingPbSync(string geminiPath, out int flushedCount, out List<string> warnings)
        {
            flushedCount = 0;
            warnings = new List<string>();
            Dictionary<string, string> pending = LoadPendingPbSync(geminiPath);
            if (pending.Count == 0) return true;

            int updated;
            List<string> pbWarns;
            if (!SessionLifecycleService.FlushPendingTitlesToPbOnly(geminiPath, pending, out updated, out pbWarns))
            {
                if (pbWarns != null) warnings.AddRange(pbWarns);
                return false;
            }
            if (pbWarns != null) warnings.AddRange(pbWarns);
            flushedCount = updated;
            SavePendingPbSync(geminiPath, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            return true;
        }

        private static void SavePendingPbSync(string geminiPath, Dictionary<string, string> map)
        {
            string path = GetPendingPbSyncPath(geminiPath);
            try
            {
                if (map == null || map.Count == 0)
                {
                    if (File.Exists(path))
                    {
                        File.SetAttributes(path, FileAttributes.Normal);
                        File.Delete(path);
                    }
                    return;
                }
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                Dictionary<string, object> pendingObj = new Dictionary<string, object>();
                foreach (KeyValuePair<string, string> kvp in map) pendingObj[kvp.Key] = kvp.Value;
                Dictionary<string, object> root = new Dictionary<string, object>();
                root["version"] = 1;
                root["updatedAtUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                root["pendingTitles"] = pendingObj;
                SessionLifecycleService.WriteUtf8TextAtomicWithBackup(path, UnifiedJson.Serialize(root, true), false, null);
            }
            catch { }
        }

        // ══════════════════════════════════════════════════════════════════
        // INTERNAL HELPERS
        // ══════════════════════════════════════════════════════════════════

        private static bool MatchesProjectSelection(
            ConversationSummaryInfo s,
            HashSet<string> projIds,
            HashSet<string> ghostKeys,
            HashSet<string> projNames)
        {
            if (s == null) return false;
            if (projIds != null && projIds.Count > 0 && !string.IsNullOrEmpty(s.ProjectId) && projIds.Contains(s.ProjectId.Trim()))
                return true;
            if (ghostKeys != null && ghostKeys.Count > 0)
            {
                string gk = SessionLifecycleService.BuildGhostProjectKey(s.ProjectName, s.FolderUri);
                if (ghostKeys.Contains(gk)) return true;
            }
            if ((projIds == null || projIds.Count == 0) &&
                (ghostKeys == null || ghostKeys.Count == 0) &&
                projNames != null && projNames.Count > 0)
            {
                string pName = string.IsNullOrEmpty(s.ProjectName) ? "Outside of Project" : s.ProjectName.Trim();
                if (projNames.Contains(pName)) return true;
            }
            return false;
        }

        private static string CreateTrashBatchFolder(string geminiPath, out string trashId)
        {
            string root = GetTrashRoot(geminiPath);
            if (!Directory.Exists(root)) Directory.CreateDirectory(root);
            trashId = "trash_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + "_" +
                      Guid.NewGuid().ToString("N").Substring(0, 6);
            string folder = Path.Combine(root, trashId);
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static bool TryResolveTrashDir(string geminiPath, string trashId, out string trashDir)
        {
            trashDir = null;
            if (string.IsNullOrWhiteSpace(geminiPath) || string.IsNullOrWhiteSpace(trashId)) return false;
            string cleanId = trashId.Trim();
            if (cleanId.IndexOf('\\') >= 0 || cleanId.IndexOf('/') >= 0 || cleanId.Contains("..")) return false;
            string root = GetTrashRoot(geminiPath);
            string candidate = Path.Combine(root, cleanId);
            if (!SessionLifecycleService.IsPathContainedUnder(root, candidate) || !Directory.Exists(candidate))
                return false;
            trashDir = candidate;
            return true;
        }

        private static bool IsAllowedTrashRestoreDestination(string targetPath, string geminiPath, string repoPath)
        {
            string canon;
            string err;
            if (!ApiRouter.TryValidateCanonicalLocalPath(targetPath, out canon, out err)) return false;
            if (!string.IsNullOrEmpty(geminiPath) && SessionLifecycleService.IsPathContainedUnder(geminiPath, canon))
                return true;
            if (!string.IsNullOrEmpty(repoPath) && SessionLifecycleService.IsPathContainedUnder(repoPath, canon))
                return true;
            return false;
        }

        private static bool ArchiveSingleFileToPayload(
            string srcFile,
            string payloadDir,
            ref int seq,
            List<object> entries,
            string label,
            out long bytes)
        {
            bytes = 0;
            try
            {
                if (!File.Exists(srcFile)) return false;
                seq++;
                string relName = seq.ToString("D4", CultureInfo.InvariantCulture) + "_" + Path.GetFileName(srcFile);
                string dst = Path.Combine(payloadDir, relName);
                File.Copy(srcFile, dst, true);
                FileInfo fi = new FileInfo(dst);
                bytes = fi.Exists ? fi.Length : 0L;

                Dictionary<string, object> row = new Dictionary<string, object>();
                row["originalPath"] = Path.GetFullPath(srcFile);
                row["trashRelPath"] = relName;
                row["isDirectory"] = false;
                row["sizeBytes"] = bytes;
                row["label"] = label ?? string.Empty;
                entries.Add(row);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool ArchiveDirectoryToPayload(
            string srcDir,
            string payloadDir,
            ref int seq,
            List<object> entries,
            string label,
            out long bytes)
        {
            bytes = 0;
            try
            {
                if (!Directory.Exists(srcDir)) return false;
                seq++;
                string relName = seq.ToString("D4", CultureInfo.InvariantCulture) + "_" + Path.GetFileName(srcDir.TrimEnd('\\', '/'));
                string dst = Path.Combine(payloadDir, relName);
                bytes = CopyDirectoryOverwrite(srcDir, dst);

                Dictionary<string, object> row = new Dictionary<string, object>();
                row["originalPath"] = Path.GetFullPath(srcDir);
                row["trashRelPath"] = relName;
                row["isDirectory"] = true;
                row["sizeBytes"] = bytes;
                row["label"] = label ?? string.Empty;
                entries.Add(row);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static long CopyDirectoryOverwrite(string srcDir, string dstDir)
        {
            long total = 0;
            if (!Directory.Exists(dstDir)) Directory.CreateDirectory(dstDir);
            string[] files = Directory.GetFiles(srcDir, "*", SearchOption.AllDirectories);
            string normSrc = Path.GetFullPath(srcDir).TrimEnd('\\', '/');
            for (int i = 0; i < files.Length; i++)
            {
                string f = files[i];
                string rel = Path.GetFullPath(f).Substring(normSrc.Length).TrimStart('\\', '/');
                string dstFile = Path.Combine(dstDir, rel);
                string parent = Path.GetDirectoryName(dstFile);
                if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent)) Directory.CreateDirectory(parent);
                File.Copy(f, dstFile, true);
                try { total += new FileInfo(dstFile).Length; } catch { }
            }
            return total;
        }

        private static string ResolveExistingDir(string parentDir, string safeFolder, string rawId)
        {
            string p1 = CapabilityVaultService.CombineAndEnsureUnderRoot(parentDir, safeFolder);
            if (Directory.Exists(p1)) return p1;
            if (!string.Equals(safeFolder, rawId, StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(rawId) && rawId.IndexOf("..") < 0 && rawId.IndexOf('\\') < 0 && rawId.IndexOf('/') < 0)
            {
                string p2 = CapabilityVaultService.CombineAndEnsureUnderRoot(parentDir, rawId);
                if (Directory.Exists(p2)) return p2;
            }
            return null;
        }

        private static void CaptureJsonKeyRestore(string filePath, string parentSection, string key, List<object> jsonRestores)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath) || string.IsNullOrEmpty(key)) return;
            try
            {
                string raw = SessionLifecycleService.ReadUtf8TextShared(filePath);
                object parsed;
                if (!UnifiedJson.TryParse(raw, out parsed)) return;
                Dictionary<string, object> root = UnifiedJson.AsDict(parsed);
                if (root == null) return;
                Dictionary<string, object> targetObj = root;
                if (!string.IsNullOrEmpty(parentSection))
                {
                    object secObj;
                    if (!root.TryGetValue(parentSection, out secObj)) return;
                    targetObj = UnifiedJson.AsDict(secObj);
                    if (targetObj == null) return;
                }
                object valObj;
                if (!targetObj.TryGetValue(key, out valObj)) return;
                Dictionary<string, object> rec = new Dictionary<string, object>();
                rec["filePath"] = Path.GetFullPath(filePath);
                rec["parentSection"] = parentSection ?? string.Empty;
                rec["key"] = key;
                rec["valueJson"] = UnifiedJson.Serialize(valObj, true);
                jsonRestores.Add(rec);
            }
            catch { }
        }

        private static void WriteTrashManifest(
            string trashFolder,
            string trashId,
            string kind,
            string summary,
            int itemCount,
            long totalBytes,
            string idePbRel,
            string cliPbRel,
            string summaryRowsRel,
            List<object> entries,
            List<object> jsonRestores)
        {
            Dictionary<string, object> man = new Dictionary<string, object>();
            man["version"] = 1;
            man["id"] = trashId;
            man["kind"] = kind;
            man["summary"] = summary;
            man["createdAtUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            man["itemCount"] = itemCount;
            man["totalBytes"] = totalBytes;
            man["idePbRelPath"] = idePbRel ?? string.Empty;
            man["cliPbRelPath"] = cliPbRel ?? string.Empty;
            man["summaryRowsRelPath"] = summaryRowsRel ?? string.Empty;
            man["entries"] = entries ?? new List<object>();
            if (jsonRestores != null && jsonRestores.Count > 0)
                man["jsonRestores"] = jsonRestores;

            SessionLifecycleService.WriteUtf8TextAtomicWithBackup(
                Path.Combine(trashFolder, "manifest.json"),
                UnifiedJson.Serialize(man, true), false, null);
        }

        private static string BuildSessionDeleteSummary(
            string kind,
            HashSet<string> finalCids,
            HashSet<string> projIds,
            HashSet<string> projNames,
            Dictionary<string, ProjectMetadata> localProjects,
            Dictionary<string, ConversationSummaryInfo> byCid)
        {
            if (kind == "project")
            {
                List<string> names = new List<string>();
                foreach (string pid in projIds)
                {
                    ProjectMetadata pm;
                    if (localProjects != null && localProjects.TryGetValue(pid, out pm) && pm != null && !string.IsNullOrEmpty(pm.Name))
                        names.Add(pm.Name);
                    else
                        names.Add(pid);
                }
                foreach (string pn in projNames) names.Add(pn);
                string label = names.Count > 0 ? string.Join("、", names.ToArray()) : "项目记录";
                return string.Format("项目「{0}」（含 {1} 条对话/子任务）", label, finalCids.Count);
            }
            string firstTitle = string.Empty;
            foreach (string cid in finalCids)
            {
                ConversationSummaryInfo info;
                if (byCid != null && byCid.TryGetValue(cid, out info) && info != null && !string.IsNullOrEmpty(info.DisplayTitle))
                {
                    firstTitle = info.DisplayTitle;
                    break;
                }
            }
            if (finalCids.Count == 1 && !string.IsNullOrEmpty(firstTitle))
                return "对话「" + firstTitle + "」";
            return string.Format("{0} 条对话{1}", finalCids.Count,
                string.IsNullOrEmpty(firstTitle) ? string.Empty : ("（含「" + firstTitle + "」等）"));
        }

        private static string BuildCapDeleteSummary(string scopeLabel, List<CapabilityItem> items)
        {
            if (items == null || items.Count == 0) return scopeLabel;
            string first = items[0].DisplayName ?? items[0].Id ?? string.Empty;
            if (items.Count == 1)
                return scopeLabel + "：" + first;
            return string.Format("{0}：{1} 等 {2} 项", scopeLabel, first, items.Count);
        }

        private static long ReadLong(Dictionary<string, object> dict, string key, long def)
        {
            if (dict == null || string.IsNullOrEmpty(key)) return def;
            object val;
            if (!dict.TryGetValue(key, out val) || val == null) return def;
            if (val is long) return (long)val;
            if (val is int) return (long)(int)val;
            if (val is double) return (long)(double)val;
            long parsed;
            if (long.TryParse(Convert.ToString(val, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return parsed;
            return def;
        }

        private static bool DeleteDirQuiet(string dirPath)
        {
            try
            {
                if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath)) return true;
                string[] files = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    try { File.SetAttributes(files[i], FileAttributes.Normal); } catch { }
                }
                Directory.Delete(dirPath, true);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
