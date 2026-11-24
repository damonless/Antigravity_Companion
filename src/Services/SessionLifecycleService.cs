using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // SESSION LIFECYCLE SERVICE (F1: Delete & GC, F2: Rename/Move/Relocate, F3: Selective Export)
    // Hardened for P0-1, P0-3, P0-4, P2-3, P3. Strictly C# 5.0 (.NET Framework 4.x)
    // ══════════════════════════════════════════════════════════════════════

    public class DeletionResult
    {
        public int DeletedSessionCount;
        public int DeletedDbFilesCount;
        public int DeletedBrainDirsCount;
        public int DeletedAnnotationsCount;
        public int DeletedProjectJsonCount;
        public long FreedBytes;

        public double FreedMegabytes
        {
            get { return FreedBytes / (1024.0 * 1024.0); }
        }
    }

    public class OrphanScanResult
    {
        public List<string> OrphanDbFiles;
        public List<string> OrphanBrainDirs;
        public List<string> OrphanAnnotationFiles;
        public HashSet<string> OrphanSessionIds;
        public long TotalOrphanBytes;

        public OrphanScanResult()
        {
            OrphanDbFiles = new List<string>();
            OrphanBrainDirs = new List<string>();
            OrphanAnnotationFiles = new List<string>();
            OrphanSessionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            TotalOrphanBytes = 0;
        }

        public int TotalOrphanItems
        {
            get { return OrphanDbFiles.Count + OrphanBrainDirs.Count + OrphanAnnotationFiles.Count; }
        }

        public double TotalOrphanMegabytes
        {
            get { return TotalOrphanBytes / (1024.0 * 1024.0); }
        }
    }

    public class RenameBatchResult
    {
        public int RenamedCount;
        public List<string> SkippedRunningIds;
        public string BackupSnapshotDir;
        public bool Layer1Updated;
        public bool Layer2Updated;
        public bool Layer3Updated;
        public List<string> Warnings;

        public RenameBatchResult()
        {
            SkippedRunningIds = new List<string>();
            Warnings = new List<string>();
        }
    }

    public static class SessionLifecycleService
    {
        [ThreadStatic]
        private static string _lastWarning;

        [ThreadStatic]
        private static string _lastError;

        [ThreadStatic]
        private static List<string> _lastFailedSqliteCids;

        public static string LastWarning
        {
            get { return _lastWarning; }
            private set { _lastWarning = value; }
        }

        public static string LastError
        {
            get { return _lastError; }
            private set { _lastError = value; }
        }

        public static List<string> LastFailedSqliteCids
        {
            get
            {
                if (_lastFailedSqliteCids == null)
                    _lastFailedSqliteCids = new List<string>();
                return _lastFailedSqliteCids;
            }
            private set
            {
                _lastFailedSqliteCids = value;
            }
        }

        private static readonly string[] SessionDbDeleteExtensions = new string[]
        {
            ".db", ".db-wal", ".db-shm", ".db.bak", ".db-journal"
        };

        // ══════════════════════════════════════════════════════════════════
        // F1-A: 级联物理删除会话与工程 (IDE + CLI 全链支持, 保护 bin/)
        // ══════════════════════════════════════════════════════════════════
        public static OperationResult<DeletionResult> DeleteSessionsAndProjectsResult(
            string geminiPath,
            List<ConversationSummaryInfo> allLocalSummaries,
            Dictionary<string, ProjectMetadata> localProjects,
            HashSet<string> targetConvIds,
            HashSet<string> targetProjectNames,
            HashSet<string> targetProjectIds = null,
            HashSet<string> targetGhostProjectKeys = null)
        {
            try
            {
                DeletionResult res = DeleteSessionsAndProjects(
                    geminiPath, allLocalSummaries, localProjects,
                    targetConvIds, targetProjectNames, targetProjectIds, targetGhostProjectKeys);
                List<string> warns = string.IsNullOrEmpty(LastWarning) ? null : new List<string> { LastWarning };
                return OperationResult<DeletionResult>.Success(res, warns);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return OperationResult<DeletionResult>.Fail(ex.Message);
            }
        }

        public static DeletionResult DeleteSessionsAndProjects(
            string geminiPath,
            List<ConversationSummaryInfo> allLocalSummaries,
            Dictionary<string, ProjectMetadata> localProjects,
            HashSet<string> targetConvIds,
            HashSet<string> targetProjectNames)
        {
            return DeleteSessionsAndProjects(geminiPath, allLocalSummaries, localProjects, targetConvIds, targetProjectNames, null, null);
        }

        public static DeletionResult DeleteSessionsAndProjects(
            string geminiPath,
            List<ConversationSummaryInfo> allLocalSummaries,
            Dictionary<string, ProjectMetadata> localProjects,
            HashSet<string> targetConvIds,
            HashSet<string> targetProjectNames,
            HashSet<string> targetProjectIds,
            HashSet<string> targetGhostProjectKeys = null)
        {
            DeletionResult result = new DeletionResult();
            LastWarning = null;
            LastError = null;
            if (string.IsNullOrEmpty(geminiPath) || !Directory.Exists(geminiPath))
                return result;

            HashSet<string> finalConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (targetConvIds != null)
            {
                foreach (string cid in targetConvIds)
                {
                    if (SessionMetadataExtractor.IsLikelyConversationId(cid))
                        finalConvIds.Add(cid.Trim());
                }
            }

            HashSet<string> projNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (targetProjectNames != null)
            {
                foreach (string pn in targetProjectNames)
                {
                    if (!string.IsNullOrEmpty(pn)) projNames.Add(pn.Trim());
                }
            }

            HashSet<string> projIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (targetProjectIds != null)
            {
                foreach (string pid in targetProjectIds)
                {
                    if (!string.IsNullOrEmpty(pid)) projIds.Add(pid.Trim());
                }
            }

            HashSet<string> ghostKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (targetGhostProjectKeys != null)
            {
                foreach (string gk in targetGhostProjectKeys)
                {
                    if (!string.IsNullOrEmpty(gk)) ghostKeys.Add(gk.Trim());
                }
            }

            if (allLocalSummaries != null)
            {
                for (int i = 0; i < allLocalSummaries.Count; i++)
                {
                    ConversationSummaryInfo s = allLocalSummaries[i];
                    if (s == null || !SessionMetadataExtractor.IsLikelyConversationId(s.ConversationId)) continue;

                    if (DoesSummaryMatchProjectTargets(s, projIds, ghostKeys, projNames))
                    {
                        finalConvIds.Add(s.ConversationId.Trim());
                    }
                }

                bool addedAny = true;
                while (addedAny)
                {
                    addedAny = false;
                    for (int i = 0; i < allLocalSummaries.Count; i++)
                    {
                        ConversationSummaryInfo s = allLocalSummaries[i];
                        if (s == null || !SessionMetadataExtractor.IsLikelyConversationId(s.ConversationId)) continue;
                        string cidTrim = s.ConversationId.Trim();
                        string parentTrim = s.ParentConversationId != null ? s.ParentConversationId.Trim() : null;
                        if (!finalConvIds.Contains(cidTrim) &&
                            !string.IsNullOrEmpty(parentTrim) &&
                            finalConvIds.Contains(parentTrim))
                        {
                            finalConvIds.Add(cidTrim);
                            addedAny = true;
                        }
                    }
                }
            }

            HashSet<string> removedFromPbCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> survivingProjIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> survivingProjNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string[] dataRoots = new string[]
            {
                Path.Combine(geminiPath, "antigravity"),
                Path.Combine(geminiPath, "antigravity-cli")
            };

            for (int r = 0; r < dataRoots.Length; r++)
            {
                string pbPath = Path.Combine(dataRoots[r], "agyhub_summaries_proto.pb");
                RemoveTargetCidsFromProtoIndex(
                    pbPath, localProjects, finalConvIds, projIds, ghostKeys, projNames,
                    removedFromPbCids, survivingProjIds, survivingProjNames);
            }

            HashSet<string> actuallyDeletedSessions = new HashSet<string>(removedFromPbCids, StringComparer.OrdinalIgnoreCase);

            // Physical deletion across both antigravity/ and antigravity-cli/ (never touching bin/)
            for (int r = 0; r < dataRoots.Length; r++)
            {
                string convDir = Path.Combine(dataRoots[r], "conversations");
                string brainDir = Path.Combine(dataRoots[r], "brain");
                string annotationsDir = Path.Combine(dataRoots[r], "annotations");

                foreach (string cid in finalConvIds)
                {
                    if (!SessionMetadataExtractor.IsLikelyConversationId(cid)) continue;
                    if (string.Equals(cid, "bin", StringComparison.OrdinalIgnoreCase)) continue;

                    bool diskRemovedForCid = false;
                    for (int i = 0; i < SessionDbDeleteExtensions.Length; i++)
                    {
                        string dbFile = Path.Combine(convDir, cid + SessionDbDeleteExtensions[i]);
                        if (IsPathContainedUnder(convDir, dbFile) && File.Exists(dbFile))
                        {
                            long len = GetFileSizeSafe(dbFile);
                            if (DeleteFileSafe(dbFile))
                            {
                                result.DeletedDbFilesCount++;
                                result.FreedBytes += len;
                                diskRemovedForCid = true;
                            }
                        }
                    }

                    string bDir = Path.Combine(brainDir, cid);
                    if (IsPathContainedUnder(brainDir, bDir) && Directory.Exists(bDir))
                    {
                        long dirBytes = GetDirectorySizeSafe(bDir);
                        if (DeleteDirectorySafe(bDir))
                        {
                            result.DeletedBrainDirsCount++;
                            result.FreedBytes += dirBytes;
                            diskRemovedForCid = true;
                        }
                    }

                    string annotFile = Path.Combine(annotationsDir, cid + ".pbtxt");
                    if (IsPathContainedUnder(annotationsDir, annotFile) && File.Exists(annotFile))
                    {
                        long len = GetFileSizeSafe(annotFile);
                        if (DeleteFileSafe(annotFile))
                        {
                            result.DeletedAnnotationsCount++;
                            result.FreedBytes += len;
                            diskRemovedForCid = true;
                        }
                    }

                    if (diskRemovedForCid)
                    {
                        actuallyDeletedSessions.Add(cid);
                    }
                }
            }

            result.DeletedSessionCount = actuallyDeletedSessions.Count;

            // Cascade delete from antigravity/conversation_summaries.db (antigravity-cli/conversation_summaries.db is read-only legacy archive)
            string summaryDbPath = Path.Combine(geminiPath, "antigravity", "conversation_summaries.db");
            if (File.Exists(summaryDbPath) && finalConvIds.Count > 0)
            {
                List<string> dbWarnings;
                int dbDeleted = SummarySqliteBridge.DeleteSessionsFromSummaryDb(summaryDbPath, finalConvIds, out dbWarnings);
                if (dbDeleted > result.DeletedSessionCount)
                {
                    result.DeletedSessionCount = dbDeleted;
                }
                AppendWarningsToLastWarning(dbWarnings, SummarySqliteBridge.LastError);
            }

            // Cleanup unreferenced config/projects/<id>.json
            if (projIds.Count > 0 || (projIds.Count == 0 && ghostKeys.Count == 0 && projNames.Count > 0))
            {
                string projDir = Path.Combine(geminiPath, "config", "projects");
                if (Directory.Exists(projDir))
                {
                    string[] jsonFiles = Directory.GetFiles(projDir, "*.json");
                    for (int i = 0; i < jsonFiles.Length; i++)
                    {
                        try
                        {
                            string pId = Path.GetFileNameWithoutExtension(jsonFiles[i]);
                            bool targetMatch = false;
                            if (projIds.Count > 0)
                            {
                                targetMatch = projIds.Contains(pId) && !survivingProjIds.Contains(pId);
                            }
                            else if (ghostKeys.Count == 0 && projNames.Count > 0)
                            {
                                string json = File.ReadAllText(jsonFiles[i], Encoding.UTF8);
                                string name = DirectStreamArchiver.ExtractJsonValue(json, "name");
                                if (!string.IsNullOrEmpty(name) && projNames.Contains(name) &&
                                    !survivingProjIds.Contains(pId) && !survivingProjNames.Contains(name))
                                {
                                    targetMatch = true;
                                }
                            }

                            if (targetMatch)
                            {
                                long len = GetFileSizeSafe(jsonFiles[i]);
                                if (DeleteFileSafe(jsonFiles[i]))
                                {
                                    result.DeletedProjectJsonCount++;
                                    result.FreedBytes += len;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }

            return result;
        }

        private static void RemoveTargetCidsFromProtoIndex(
            string pbPath,
            Dictionary<string, ProjectMetadata> localProjects,
            HashSet<string> finalConvIds,
            HashSet<string> projIds,
            HashSet<string> ghostKeys,
            HashSet<string> projNames,
            HashSet<string> removedFromPbCids,
            HashSet<string> survivingProjIds,
            HashSet<string> survivingProjNames)
        {
            if (string.IsNullOrEmpty(pbPath) || !File.Exists(pbPath)) return;

            byte[] pbBytes = ReadAllBytesShared(pbPath);
            List<ProtoNode> rootNodes = ProtoCodec.Decode(pbBytes);
            List<ConversationSummaryInfo> parsedEntries = new List<ConversationSummaryInfo>(rootNodes.Count);

            for (int i = 0; i < rootNodes.Count; i++)
            {
                ProtoNode node = rootNodes[i];
                ConversationSummaryInfo parsed = null;
                if (node.FieldNumber == 1 && node.WireType == 2)
                {
                    parsed = ParseEntryWithProjects(node, localProjects);
                    if (parsed != null && SessionMetadataExtractor.IsLikelyConversationId(parsed.ConversationId))
                    {
                        string cidTrim = parsed.ConversationId.Trim();
                        if (finalConvIds.Contains(cidTrim) || DoesSummaryMatchProjectTargets(parsed, projIds, ghostKeys, projNames))
                        {
                            finalConvIds.Add(cidTrim);
                        }
                    }
                }
                parsedEntries.Add(parsed);
            }

            bool pbClosureAdded = true;
            while (pbClosureAdded)
            {
                pbClosureAdded = false;
                for (int i = 0; i < parsedEntries.Count; i++)
                {
                    ConversationSummaryInfo parsed = parsedEntries[i];
                    if (parsed == null || !SessionMetadataExtractor.IsLikelyConversationId(parsed.ConversationId)) continue;
                    string cidTrim = parsed.ConversationId.Trim();
                    string parentTrim = parsed.ParentConversationId != null ? parsed.ParentConversationId.Trim() : null;
                    if (!finalConvIds.Contains(cidTrim) &&
                        !string.IsNullOrEmpty(parentTrim) &&
                        finalConvIds.Contains(parentTrim))
                    {
                        finalConvIds.Add(cidTrim);
                        pbClosureAdded = true;
                    }
                }
            }

            List<ProtoNode> keptNodes = new List<ProtoNode>();
            bool modifiedPb = false;

            for (int i = 0; i < rootNodes.Count; i++)
            {
                ProtoNode node = rootNodes[i];
                ConversationSummaryInfo parsed = parsedEntries[i];
                bool shouldRemove = false;

                if (parsed != null && SessionMetadataExtractor.IsLikelyConversationId(parsed.ConversationId))
                {
                    string cidTrim = parsed.ConversationId.Trim();
                    if (finalConvIds.Contains(cidTrim))
                    {
                        shouldRemove = true;
                        removedFromPbCids.Add(cidTrim);
                    }
                }
                else if (node.FieldNumber == 1 && node.WireType == 2)
                {
                    ProtoCodec.DecodeSummaryEntryTree(node);
                    ProtoNode f1 = node.GetChild(1);
                    if (f1 != null)
                    {
                        string rawCid = f1.GetString() != null ? f1.GetString().Trim() : null;
                        if (!string.IsNullOrEmpty(rawCid) && finalConvIds.Contains(rawCid))
                        {
                            shouldRemove = true;
                            removedFromPbCids.Add(rawCid);
                        }
                    }
                }

                if (shouldRemove)
                {
                    modifiedPb = true;
                }
                else
                {
                    keptNodes.Add(node);
                    if (parsed != null)
                    {
                        if (!string.IsNullOrEmpty(parsed.ProjectId)) survivingProjIds.Add(parsed.ProjectId.Trim());
                        if (!string.IsNullOrEmpty(parsed.ProjectName)) survivingProjNames.Add(parsed.ProjectName.Trim());
                    }
                }
            }

            if (modifiedPb)
            {
                byte[] encoded = ProtoCodec.Encode(keptNodes);
                UnifiedJson.CreateTimestampedBackup(pbPath, 5);
                WriteBytesAtomicWithBackup(pbPath, encoded, null);
            }
        }

        private static bool DoesSummaryMatchProjectTargets(
            ConversationSummaryInfo s,
            HashSet<string> projIds,
            HashSet<string> ghostKeys,
            HashSet<string> projNames)
        {
            if (s == null) return false;
            if (projIds != null && projIds.Count > 0 && !string.IsNullOrEmpty(s.ProjectId))
            {
                if (projIds.Contains(s.ProjectId.Trim())) return true;
            }
            if (ghostKeys != null && ghostKeys.Count > 0)
            {
                string gk = BuildGhostProjectKey(s.ProjectName, s.FolderUri);
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

        public static string BuildGhostProjectKey(string projectName, string folderUri)
        {
            string pName = string.IsNullOrEmpty(projectName) ? "Outside of Project" : projectName.Trim();
            string normUri = DirectStreamArchiver.NormalizeUri(folderUri ?? string.Empty);
            return pName + "|" + normUri;
        }

        private static ConversationSummaryInfo ParseEntryWithProjects(ProtoNode node, Dictionary<string, ProjectMetadata> localProjects)
        {
            ConversationSummaryInfo parsed = SessionMetadataExtractor.ParseEntryNode(node);
            if (parsed == null) return null;

            if (localProjects != null && localProjects.Count > 0)
            {
                if (!string.IsNullOrEmpty(parsed.ProjectId) && localProjects.ContainsKey(parsed.ProjectId))
                {
                    parsed.ProjectName = localProjects[parsed.ProjectId].Name;
                    if (string.IsNullOrEmpty(parsed.FolderUri))
                        parsed.FolderUri = localProjects[parsed.ProjectId].FolderUri;
                }
                else if (!string.IsNullOrEmpty(parsed.FolderUri))
                {
                    string norm = DirectStreamArchiver.NormalizeUri(parsed.FolderUri);
                    foreach (KeyValuePair<string, ProjectMetadata> kvp in localProjects)
                    {
                        bool matched = string.Equals(norm, DirectStreamArchiver.NormalizeUri(kvp.Value.FolderUri), StringComparison.OrdinalIgnoreCase);
                        if (!matched && kvp.Value.FolderUris != null)
                        {
                            for (int u = 0; u < kvp.Value.FolderUris.Count; u++)
                            {
                                if (string.Equals(norm, DirectStreamArchiver.NormalizeUri(kvp.Value.FolderUris[u]), StringComparison.OrdinalIgnoreCase))
                                {
                                    matched = true;
                                    break;
                                }
                            }
                        }
                        if (matched)
                        {
                            parsed.ProjectName = kvp.Value.Name;
                            parsed.ProjectId = kvp.Value.Id;
                            break;
                        }
                    }
                }
            }
            return parsed;
        }

        internal static bool IsPathContainedUnder(string rootDir, string targetPath)
        {
            try
            {
                string fullRoot = Path.GetFullPath(rootDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string fullTarget = Path.GetFullPath(targetPath);
                return fullTarget.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string TryExtractSessionDbCid(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            if (fileName.EndsWith(".db-journal", StringComparison.OrdinalIgnoreCase))
                return fileName.Substring(0, fileName.Length - 11);
            if (fileName.EndsWith(".db-wal", StringComparison.OrdinalIgnoreCase))
                return fileName.Substring(0, fileName.Length - 7);
            if (fileName.EndsWith(".db-shm", StringComparison.OrdinalIgnoreCase))
                return fileName.Substring(0, fileName.Length - 7);
            if (fileName.EndsWith(".db.bak", StringComparison.OrdinalIgnoreCase))
                return fileName.Substring(0, fileName.Length - 7);
            if (fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                return fileName.Substring(0, fileName.Length - 3);
            return null;
        }

        // ══════════════════════════════════════════════════════════════════
        // F1-B: 孤儿数据库与残余脑区扫描及清理 (IDE + CLI, Fail-Closed, P3 sidecars)
        // ══════════════════════════════════════════════════════════════════
        public static OperationResult<OrphanScanResult> ScanOrphanedDataResult(string geminiPath)
        {
            try
            {
                return OperationResult<OrphanScanResult>.Success(ScanOrphanedData(geminiPath));
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return OperationResult<OrphanScanResult>.Fail(ex.Message);
            }
        }

        public static OrphanScanResult ScanOrphanedData(string geminiPath)
        {
            OrphanScanResult scan = new OrphanScanResult();
            if (string.IsNullOrEmpty(geminiPath) || !Directory.Exists(geminiPath))
                return scan;

            string ideRoot = Path.Combine(geminiPath, "antigravity");
            string cliRoot = Path.Combine(geminiPath, "antigravity-cli");
            string[] dataRoots = new string[] { ideRoot, cliRoot };

            bool hasAnyConvFiles = false;
            bool hasAnyBrainGuidDirs = false;
            bool hasAnyAnnotations = false;

            for (int r = 0; r < dataRoots.Length; r++)
            {
                string convDir = Path.Combine(dataRoots[r], "conversations");
                string brainDir = Path.Combine(dataRoots[r], "brain");
                string annotationsDir = Path.Combine(dataRoots[r], "annotations");

                if (Directory.Exists(convDir) && Directory.GetFiles(convDir).Length > 0)
                    hasAnyConvFiles = true;

                if (!hasAnyBrainGuidDirs && Directory.Exists(brainDir))
                {
                    string[] probeDirs = Directory.GetDirectories(brainDir);
                    for (int i = 0; i < probeDirs.Length; i++)
                    {
                        Guid pg;
                        if (Guid.TryParse(Path.GetFileName(probeDirs[i]), out pg))
                        {
                            hasAnyBrainGuidDirs = true;
                            break;
                        }
                    }
                }

                if (!hasAnyAnnotations && Directory.Exists(annotationsDir))
                {
                    string[] probeAnnots = Directory.GetFiles(annotationsDir, "*.pbtxt");
                    for (int i = 0; i < probeAnnots.Length; i++)
                    {
                        string stem = Path.GetFileNameWithoutExtension(probeAnnots[i]);
                        Guid ag;
                        if (!string.IsNullOrEmpty(stem) &&
                            Guid.TryParse(stem, out ag) &&
                            SessionMetadataExtractor.IsLikelyConversationId(stem))
                        {
                            hasAnyAnnotations = true;
                            break;
                        }
                    }
                }
            }

            string idePbPath = Path.Combine(ideRoot, "agyhub_summaries_proto.pb");
            string cliPbPath = Path.Combine(cliRoot, "agyhub_summaries_proto.pb");
            string ideDbPath = Path.Combine(ideRoot, "conversation_summaries.db");
            string cliDbPath = Path.Combine(cliRoot, "conversation_summaries.db");

            bool hasIdePb = File.Exists(idePbPath);
            bool hasCliPb = File.Exists(cliPbPath);
            bool hasIdeDb = File.Exists(ideDbPath) && GetFileSizeSafe(ideDbPath) > 0;
            bool hasCliDb = File.Exists(cliDbPath) && GetFileSizeSafe(cliDbPath) > 0;

            if (!hasIdePb && !hasCliPb && !hasIdeDb && !hasCliDb)
            {
                if (hasAnyConvFiles || hasAnyBrainGuidDirs || hasAnyAnnotations)
                {
                    throw new InvalidOperationException("未找到索引文件 agyhub_summaries_proto.pb，但磁盘存在会话数据；为防止误删全库，已中止孤儿扫描。");
                }
                return scan;
            }

            if (hasIdePb && new FileInfo(idePbPath).Length == 0 && !hasCliPb && !hasIdeDb && !hasCliDb)
            {
                if (hasAnyConvFiles || hasAnyBrainGuidDirs || hasAnyAnnotations)
                {
                    throw new InvalidOperationException("索引文件 agyhub_summaries_proto.pb 长度为 0 字节，已中止孤儿扫描以防误删全部会话。");
                }
                return scan;
            }

            HashSet<string> activeConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string[] pbPaths = new string[] { idePbPath, cliPbPath };
            for (int p = 0; p < pbPaths.Length; p++)
            {
                string pbPath = pbPaths[p];
                if (!File.Exists(pbPath) || GetFileSizeSafe(pbPath) == 0) continue;

                try
                {
                    byte[] pbBytes = ReadAllBytesShared(pbPath);
                    List<ProtoNode> rootNodes = ProtoCodec.Decode(pbBytes);
                    for (int i = 0; i < rootNodes.Count; i++)
                    {
                        ProtoNode node = rootNodes[i];
                        if (node.FieldNumber == 1 && node.WireType == 2)
                        {
                            ProtoCodec.DecodeSummaryEntryTree(node);
                            ProtoNode f1 = node.GetChild(1);
                            if (f1 != null)
                            {
                                string cid = f1.GetString();
                                if (SessionMetadataExtractor.IsLikelyConversationId(cid))
                                {
                                    activeConvIds.Add(cid.Trim());
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("无法安全读取或解码 " + Path.GetFileName(pbPath) + "（可能正被 Antigravity 占用或已损坏），已中止孤儿扫描: " + ex.Message, ex);
                }
            }

            if (hasIdeDb)
            {
                HashSet<string> dbActiveIds = SummarySqliteBridge.ReadAllConversationIds(ideDbPath);
                foreach (string dbCid in dbActiveIds)
                {
                    if (SessionMetadataExtractor.IsLikelyConversationId(dbCid))
                    {
                        activeConvIds.Add(dbCid.Trim());
                    }
                }
            }

            if (hasCliDb)
            {
                try
                {
                    HashSet<string> cliDbIds = SummarySqliteBridge.ReadAllConversationIds(cliDbPath);
                    foreach (string cliCid in cliDbIds)
                    {
                        if (SessionMetadataExtractor.IsLikelyConversationId(cliCid))
                        {
                            activeConvIds.Add(cliCid.Trim());
                        }
                    }
                }
                catch
                {
                    // Legacy CLI archive DB may use older schema; fail-safe check below protects against false full wipe
                }
            }

            if (activeConvIds.Count == 0 && (hasAnyConvFiles || hasAnyBrainGuidDirs || hasAnyAnnotations))
            {
                throw new InvalidOperationException("索引文件 agyhub_summaries_proto.pb 与 conversation_summaries.db 未包含任何有效会话 ID，但磁盘存在会话文件；已中止孤儿扫描以防全盘误删。");
            }

            for (int r = 0; r < dataRoots.Length; r++)
            {
                string convDir = Path.Combine(dataRoots[r], "conversations");
                string brainDir = Path.Combine(dataRoots[r], "brain");
                string annotationsDir = Path.Combine(dataRoots[r], "annotations");

                // 1. Scan conversations/*.db + sidecars (.db-wal, .db-shm, .db.bak, .db-journal)
                if (Directory.Exists(convDir))
                {
                    string[] files = Directory.GetFiles(convDir);
                    for (int i = 0; i < files.Length; i++)
                    {
                        string f = files[i];
                        string fn = Path.GetFileName(f);
                        string cid = TryExtractSessionDbCid(fn);

                        if (SessionMetadataExtractor.IsLikelyConversationId(cid) &&
                            !activeConvIds.Contains(cid.Trim()) &&
                            IsPathContainedUnder(convDir, f))
                        {
                            string trimmedCid = cid.Trim();
                            scan.OrphanDbFiles.Add(f);
                            scan.OrphanSessionIds.Add(trimmedCid);
                            scan.TotalOrphanBytes += GetFileSizeSafe(f);
                        }
                    }
                }

                // 2. Scan brain/<guid>/ (strictly GUID dirs, protecting bin/ and tempmediaStorage)
                if (Directory.Exists(brainDir))
                {
                    string[] dirs = Directory.GetDirectories(brainDir);
                    for (int i = 0; i < dirs.Length; i++)
                    {
                        string d = dirs[i];
                        string dirName = Path.GetFileName(d);
                        Guid dirGuid;
                        if (!string.IsNullOrEmpty(dirName) &&
                            !string.Equals(dirName, "bin", StringComparison.OrdinalIgnoreCase) &&
                            Guid.TryParse(dirName, out dirGuid) &&
                            SessionMetadataExtractor.IsLikelyConversationId(dirName) &&
                            !activeConvIds.Contains(dirName) &&
                            IsPathContainedUnder(brainDir, d))
                        {
                            scan.OrphanBrainDirs.Add(d);
                            scan.OrphanSessionIds.Add(dirName);
                            scan.TotalOrphanBytes += GetDirectorySizeSafe(d);
                        }
                    }
                }

                // 3. Scan annotations/<guid>.pbtxt
                if (Directory.Exists(annotationsDir))
                {
                    string[] annotFiles = Directory.GetFiles(annotationsDir, "*.pbtxt");
                    for (int i = 0; i < annotFiles.Length; i++)
                    {
                        string f = annotFiles[i];
                        string stem = Path.GetFileNameWithoutExtension(f);
                        Guid annotGuid;
                        if (!string.IsNullOrEmpty(stem) &&
                            Guid.TryParse(stem, out annotGuid) &&
                            SessionMetadataExtractor.IsLikelyConversationId(stem) &&
                            !activeConvIds.Contains(stem.Trim()) &&
                            IsPathContainedUnder(annotationsDir, f))
                        {
                            string trimmedCid = stem.Trim();
                            scan.OrphanAnnotationFiles.Add(f);
                            scan.OrphanSessionIds.Add(trimmedCid);
                            scan.TotalOrphanBytes += GetFileSizeSafe(f);
                        }
                    }
                }
            }

            return scan;
        }

        public static OperationResult<DeletionResult> CleanOrphanedDataResult(OrphanScanResult scan)
        {
            try
            {
                return OperationResult<DeletionResult>.Success(CleanOrphanedData(scan));
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return OperationResult<DeletionResult>.Fail(ex.Message);
            }
        }

        public static DeletionResult CleanOrphanedData(OrphanScanResult scan)
        {
            DeletionResult res = new DeletionResult();
            if (scan == null) return res;

            res.DeletedSessionCount = scan.OrphanSessionIds.Count;

            if (scan.OrphanDbFiles != null)
            {
                for (int i = 0; i < scan.OrphanDbFiles.Count; i++)
                {
                    string f = scan.OrphanDbFiles[i];
                    if (File.Exists(f))
                    {
                        long len = GetFileSizeSafe(f);
                        if (DeleteFileSafe(f))
                        {
                            res.DeletedDbFilesCount++;
                            res.FreedBytes += len;
                        }
                    }
                }
            }

            if (scan.OrphanBrainDirs != null)
            {
                for (int i = 0; i < scan.OrphanBrainDirs.Count; i++)
                {
                    string d = scan.OrphanBrainDirs[i];
                    string dirName = Path.GetFileName(d);
                    Guid g;
                    if (Directory.Exists(d) &&
                        !string.Equals(dirName, "bin", StringComparison.OrdinalIgnoreCase) &&
                        Guid.TryParse(dirName, out g) &&
                        SessionMetadataExtractor.IsLikelyConversationId(dirName))
                    {
                        long len = GetDirectorySizeSafe(d);
                        if (DeleteDirectorySafe(d))
                        {
                            res.DeletedBrainDirsCount++;
                            res.FreedBytes += len;
                        }
                    }
                }
            }

            if (scan.OrphanAnnotationFiles != null)
            {
                for (int i = 0; i < scan.OrphanAnnotationFiles.Count; i++)
                {
                    string f = scan.OrphanAnnotationFiles[i];
                    string stem = Path.GetFileNameWithoutExtension(f);
                    Guid g;
                    if (File.Exists(f) &&
                        !string.IsNullOrEmpty(stem) &&
                        Guid.TryParse(stem, out g) &&
                        SessionMetadataExtractor.IsLikelyConversationId(stem))
                    {
                        long len = GetFileSizeSafe(f);
                        if (DeleteFileSafe(f))
                        {
                            res.DeletedAnnotationsCount++;
                            res.FreedBytes += len;
                        }
                    }
                }
            }

            return res;
        }

        // ══════════════════════════════════════════════════════════════════
        // F2-A: 本地会话重命名 (三层持久化: .pbtxt / summary.sqlite / .pb)
        // ══════════════════════════════════════════════════════════════════
        public static bool RenameSession(string geminiPath, string convId, string newTitle)
        {
            if (string.IsNullOrEmpty(geminiPath) || !SessionMetadataExtractor.IsLikelyConversationId(convId) || string.IsNullOrWhiteSpace(newTitle))
                return false;

            Dictionary<string, string> batch = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            batch[convId.Trim()] = newTitle.Trim();

            RenameBatchResult result;
            bool ok = RenameSessionsBatch(geminiPath, batch, false, out result);
            return ok && result != null && result.RenamedCount > 0;
        }

        public static OperationResult<RenameBatchResult> RenameSessionsBatchResult(
            string geminiPath,
            Dictionary<string, string> cidToNewTitle,
            bool allowHotWriteSkipRunning)
        {
            RenameBatchResult res;
            bool ok = RenameSessionsBatch(geminiPath, cidToNewTitle, allowHotWriteSkipRunning, out res);
            if (!ok)
            {
                string err = (res != null && res.Warnings != null && res.Warnings.Count > 0)
                    ? string.Join("; ", res.Warnings.ToArray())
                    : (LastWarning ?? "重命名批处理失败。");
                return OperationResult<RenameBatchResult>.Fail(err, res != null ? res.Warnings : null);
            }
            return OperationResult<RenameBatchResult>.Success(res, res != null ? res.Warnings : null);
        }

        public static bool RenameSessionsBatch(
            string geminiPath,
            Dictionary<string, string> cidToNewTitle,
            bool allowHotWriteSkipRunning,
            out RenameBatchResult result)
        {
            result = new RenameBatchResult();
            LastWarning = null;
            LastError = null;

            if (string.IsNullOrEmpty(geminiPath) || !Directory.Exists(geminiPath))
                return false;
            if (cidToNewTitle == null || cidToNewTitle.Count == 0)
                return false;

            Dictionary<string, string> validatedTargets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> kvp in cidToNewTitle)
            {
                if (!SessionMetadataExtractor.IsLikelyConversationId(kvp.Key))
                {
                    result.Warnings.Add("跳过非法会话 ID: " + (kvp.Key ?? string.Empty));
                    continue;
                }
                if (string.IsNullOrWhiteSpace(kvp.Value))
                {
                    result.Warnings.Add("跳过空标题 (" + kvp.Key.Trim() + ")。");
                    continue;
                }
                string cleanTitle = kvp.Value.Trim();
                if (IncrementalProtoShardMerger.IsSyntheticPlaceholderTitle(cleanTitle))
                {
                    result.Warnings.Add("拒绝写入合成占位标题 (" + kvp.Key.Trim() + "): " + cleanTitle);
                    continue;
                }
                validatedTargets[kvp.Key.Trim()] = cleanTitle;
            }

            if (validatedTargets.Count == 0)
            {
                AppendWarningsToLastWarning(result.Warnings, null);
                return false;
            }

            string antigravityDir = Path.Combine(geminiPath, "antigravity");
            string annotationsDir = Path.Combine(antigravityDir, "annotations");
            string dbPath = Path.Combine(antigravityDir, "conversation_summaries.db");
            string pbPath = Path.Combine(antigravityDir, "agyhub_summaries_proto.pb");

            bool dbExists = File.Exists(dbPath) && GetFileSizeSafe(dbPath) > 0;
            Dictionary<string, SummaryDbRowInfo> dbSnapshot = dbExists
                ? SummarySqliteBridge.ReadSummaryDbSnapshot(dbPath)
                : new Dictionary<string, SummaryDbRowInfo>(StringComparer.OrdinalIgnoreCase);
            if (dbExists && !string.IsNullOrEmpty(SummarySqliteBridge.LastError))
            {
                result.Warnings.Add("读取 conversation_summaries.db 快照告警: " + SummarySqliteBridge.LastError);
            }

            bool pbExists = File.Exists(pbPath) && GetFileSizeSafe(pbPath) > 0;
            List<ProtoNode> pbRootNodes = null;
            Dictionary<string, List<ProtoNode>> pbEntriesByCid = new Dictionary<string, List<ProtoNode>>(StringComparer.OrdinalIgnoreCase);
            if (pbExists)
            {
                try
                {
                    byte[] pbBytes = ReadAllBytesShared(pbPath);
                    pbRootNodes = ProtoCodec.Decode(pbBytes);
                    for (int i = 0; i < pbRootNodes.Count; i++)
                    {
                        ProtoNode entry = pbRootNodes[i];
                        if (entry.FieldNumber == 1 && entry.WireType == 2)
                        {
                            ProtoCodec.DecodeSummaryEntryTree(entry);
                            ProtoNode f1 = entry.GetChild(1);
                            if (f1 != null)
                            {
                                string entryCid = f1.GetString();
                                if (SessionMetadataExtractor.IsLikelyConversationId(entryCid))
                                {
                                    string trimmedEntryCid = entryCid.Trim();
                                    List<ProtoNode> listForCid;
                                    if (!pbEntriesByCid.TryGetValue(trimmedEntryCid, out listForCid))
                                    {
                                        listForCid = new List<ProtoNode>(1);
                                        pbEntriesByCid[trimmedEntryCid] = listForCid;
                                    }
                                    listForCid.Add(entry);
                                }
                            }
                        }
                    }
                }
                catch (IOException ioEx)
                {
                    if (!allowHotWriteSkipRunning)
                    {
                        string msg = "读取 agyhub_summaries_proto.pb 失败: " + ioEx.Message;
                        result.Warnings.Add(msg);
                        AppendWarningsToLastWarning(result.Warnings, null);
                        return false;
                    }
                    result.Warnings.Add("热写入模式读取 .pb 失败（文件被占用，将跳过 .pb 更新）: " + ioEx.Message);
                    pbRootNodes = null;
                }
                catch (Exception ex)
                {
                    string msg = "解码 agyhub_summaries_proto.pb 失败: " + ex.Message;
                    result.Warnings.Add(msg);
                    AppendWarningsToLastWarning(result.Warnings, null);
                    return false;
                }
            }

            Dictionary<string, string> activeTargets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<RenameSnapshotItem> snapshotItems = new List<RenameSnapshotItem>();

            foreach (KeyValuePair<string, string> kvp in validatedTargets)
            {
                string cid = kvp.Key;
                string newTitle = kvp.Value;

                SummaryDbRowInfo dbRow = null;
                if (dbSnapshot != null)
                {
                    dbSnapshot.TryGetValue(cid, out dbRow);
                }

                // Preserve not_fully_idle lock guard during hot writes
                if (allowHotWriteSkipRunning && dbRow != null)
                {
                    bool isRunning = string.Equals(dbRow.Status, "CASCADE_RUN_STATUS_RUNNING", StringComparison.OrdinalIgnoreCase) ||
                                     dbRow.NotFullyIdle;
                    if (isRunning)
                    {
                        result.SkippedRunningIds.Add(cid);
                        result.Warnings.Add("热写入已跳过运行中会话: " + cid);
                        continue;
                    }
                }

                string pbtxtPath = Path.Combine(annotationsDir, cid + ".pbtxt");
                bool pbtxtSafe = IsPathContainedUnder(annotationsDir, pbtxtPath);
                bool existsInPbtxt = pbtxtSafe && File.Exists(pbtxtPath);

                List<ProtoNode> pbEntries = null;
                bool existsInPb = pbEntriesByCid.TryGetValue(cid, out pbEntries) && pbEntries != null && pbEntries.Count > 0;
                bool existsInDb = dbRow != null;

                if (!existsInPb && !existsInDb && !existsInPbtxt)
                {
                    result.Warnings.Add("未找到会话节点，已跳过: " + cid);
                    continue;
                }

                activeTargets[cid] = newTitle;

                RenameSnapshotItem snapItem = new RenameSnapshotItem();
                snapItem.ConversationId = cid;
                snapItem.NewTitle = newTitle;

                if (existsInPbtxt)
                {
                    try
                    {
                        snapItem.OldPbtxtRaw = ReadUtf8TextShared(pbtxtPath);
                        snapItem.HadPbtxt = true;
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add("读取现有 .pbtxt 快照失败 (" + cid + "): " + ex.Message);
                        snapItem.HadPbtxt = false;
                        snapItem.OldPbtxtRaw = null;
                    }
                }
                else
                {
                    snapItem.HadPbtxt = false;
                    snapItem.OldPbtxtRaw = null;
                }

                string pbDisplayTitle = null;
                if (existsInPb)
                {
                    ProtoNode primaryEntry = pbEntries[0];
                    ConversationSummaryInfo parsedPb = SessionMetadataExtractor.ParseEntryNode(primaryEntry);
                    if (parsedPb != null)
                    {
                        snapItem.ProjectName = parsedPb.ProjectName ?? string.Empty;
                        pbDisplayTitle = parsedPb.DisplayTitle;
                    }

                    ProtoNode meta = primaryEntry.GetChild(2);
                    if (meta != null)
                    {
                        ProtoNode mf1 = meta.GetChild(1);
                        snapItem.OldPbAutoTitle = mf1 != null ? (mf1.GetString() ?? string.Empty) : string.Empty;

                        ProtoNode mf15 = meta.GetChild(15);
                        if (mf15 != null)
                        {
                            snapItem.HadPbF15 = true;
                            if (mf15.HasChildren)
                            {
                                ProtoNode ct1 = mf15.GetChild(1);
                                snapItem.OldPbCustomTitle = ct1 != null ? (ct1.GetString() ?? string.Empty) : string.Empty;
                            }
                            else
                            {
                                snapItem.OldPbCustomTitle = mf15.GetString() ?? string.Empty;
                            }
                        }
                        else
                        {
                            snapItem.HadPbF15 = false;
                            snapItem.OldPbCustomTitle = string.Empty;
                        }
                    }
                }

                if (dbRow != null)
                {
                    snapItem.OldTitle = dbRow.Title ?? string.Empty;
                    snapItem.OldPreview = dbRow.Preview ?? string.Empty;
                    snapItem.SetOldRawSummaryBytes(dbRow.RawSummaryBytes);
                    if (string.IsNullOrEmpty(snapItem.ProjectName) && !string.IsNullOrEmpty(dbRow.ProjectId))
                    {
                        snapItem.ProjectName = dbRow.ProjectId;
                    }
                }
                else
                {
                    string pbtxtOld = snapItem.HadPbtxt ? PbtxtAnnotationCodec.ExtractTitle(snapItem.OldPbtxtRaw) : string.Empty;
                    snapItem.OldTitle = !string.IsNullOrEmpty(pbtxtOld) ? pbtxtOld : (pbDisplayTitle ?? string.Empty);
                    snapItem.OldPreview = string.Empty;
                    snapItem.SetOldRawSummaryBytes(null);
                }

                snapshotItems.Add(snapItem);
            }

            if (activeTargets.Count == 0)
            {
                AppendWarningsToLastWarning(result.Warnings, null);
                return false;
            }

            try
            {
                result.BackupSnapshotDir = RenameBackupManager.CreateSnapshot(geminiPath, snapshotItems);
            }
            catch (Exception ex)
            {
                result.Warnings.Add("创建重命名备份快照失败: " + ex.Message);
                AppendWarningsToLastWarning(result.Warnings, null);
                return false;
            }

            // Layer 1: .pbtxt
            try
            {
                for (int i = 0; i < snapshotItems.Count; i++)
                {
                    RenameSnapshotItem item = snapshotItems[i];
                    string pbtxtPath = Path.Combine(annotationsDir, item.ConversationId + ".pbtxt");
                    if (!IsPathContainedUnder(annotationsDir, pbtxtPath))
                    {
                        result.Warnings.Add("跳过越界 .pbtxt 路径: " + item.ConversationId);
                        continue;
                    }
                    string updatedPbtxt = PbtxtAnnotationCodec.UpsertTitle(
                        item.HadPbtxt ? item.OldPbtxtRaw : null,
                        item.NewTitle);
                    WriteUtf8TextAtomicWithBackup(pbtxtPath, updatedPbtxt, false, null);
                    result.Layer1Updated = true;
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add("写入 Layer 1 (.pbtxt) 失败: " + ex.Message);
                RollbackBatchLayers1And2(annotationsDir, dbPath, false, snapshotItems, result.Warnings);
                result.Layer1Updated = false;
                AppendWarningsToLastWarning(result.Warnings, null);
                return false;
            }

            // Layer 2: conversation_summaries.db
            if (dbExists)
            {
                Dictionary<string, SummaryDbRollbackEntry> dbRollback;
                List<string> dbWarnings;
                bool dbOk = SummarySqliteBridge.UpdateSummaryTitlesTransaction(dbPath, activeTargets, out dbRollback, out dbWarnings);
                if (dbWarnings != null && dbWarnings.Count > 0)
                {
                    result.Warnings.AddRange(dbWarnings);
                }
                if (!dbOk)
                {
                    result.Warnings.Add("写入 Layer 2 (conversation_summaries.db) 失败: " + (SummarySqliteBridge.LastError ?? "未知错误"));
                    RollbackBatchLayers1And2(annotationsDir, dbPath, false, snapshotItems, result.Warnings);
                    result.Layer1Updated = false;
                    result.Layer2Updated = false;
                    AppendWarningsToLastWarning(result.Warnings, null);
                    return false;
                }
                if (dbRollback != null && dbRollback.Count > 0)
                {
                    result.Layer2Updated = true;
                }
            }

            // Layer 3: agyhub_summaries_proto.pb
            if (pbRootNodes != null)
            {
                bool modifiedPb = false;
                foreach (KeyValuePair<string, string> kvp in activeTargets)
                {
                    List<ProtoNode> entries;
                    if (pbEntriesByCid.TryGetValue(kvp.Key, out entries) && entries != null)
                    {
                        for (int i = 0; i < entries.Count; i++)
                        {
                            ApplyTitleToEntryAst(entries[i], kvp.Value);
                            modifiedPb = true;
                        }
                    }
                }

                if (modifiedPb)
                {
                    try
                    {
                        byte[] updated = ProtoCodec.Encode(pbRootNodes);
                        UnifiedJson.CreateTimestampedBackup(pbPath, 5);
                        WriteBytesAtomicWithBackup(pbPath, updated, null);
                        result.Layer3Updated = true;
                    }
                    catch (IOException ioEx)
                    {
                        if (allowHotWriteSkipRunning)
                        {
                            result.Warnings.Add("热写入模式跳过 Layer 3 (.pb 被占用，L1/L2 已生效): " + ioEx.Message);
                        }
                        else
                        {
                            result.Warnings.Add("写入 Layer 3 (agyhub_summaries_proto.pb) 失败: " + ioEx.Message);
                            RollbackBatchLayers1And2(annotationsDir, dbPath, result.Layer2Updated, snapshotItems, result.Warnings);
                            result.Layer1Updated = false;
                            result.Layer2Updated = false;
                            result.Layer3Updated = false;
                            AppendWarningsToLastWarning(result.Warnings, null);
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add("写入 Layer 3 (agyhub_summaries_proto.pb) 异常: " + ex.Message);
                        RollbackBatchLayers1And2(annotationsDir, dbPath, result.Layer2Updated, snapshotItems, result.Warnings);
                        result.Layer1Updated = false;
                        result.Layer2Updated = false;
                        result.Layer3Updated = false;
                        AppendWarningsToLastWarning(result.Warnings, null);
                        return false;
                    }
                }
            }

            result.RenamedCount = activeTargets.Count;
            AppendWarningsToLastWarning(result.Warnings, null);
            return result.RenamedCount > 0;
        }

        private static void RollbackBatchLayers1And2(
            string annotationsDir,
            string dbPath,
            bool restoreDb,
            List<RenameSnapshotItem> snapshotItems,
            List<string> warnings)
        {
            if (snapshotItems == null || snapshotItems.Count == 0) return;

            for (int i = 0; i < snapshotItems.Count; i++)
            {
                RenameSnapshotItem item = snapshotItems[i];
                string pbtxtPath = Path.Combine(annotationsDir, item.ConversationId + ".pbtxt");
                if (!IsPathContainedUnder(annotationsDir, pbtxtPath)) continue;
                try
                {
                    if (item.HadPbtxt)
                    {
                        WriteUtf8TextAtomicWithBackup(pbtxtPath, item.OldPbtxtRaw ?? string.Empty, false, null);
                    }
                    else if (File.Exists(pbtxtPath))
                    {
                        DeleteFileSafe(pbtxtPath);
                    }
                }
                catch (Exception ex)
                {
                    if (warnings != null) warnings.Add("回滚 .pbtxt 失败 (" + item.ConversationId + "): " + ex.Message);
                }
            }

            if (restoreDb && !string.IsNullOrEmpty(dbPath) && File.Exists(dbPath))
            {
                Dictionary<string, SummaryDbRollbackEntry> restoreMap = new Dictionary<string, SummaryDbRollbackEntry>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < snapshotItems.Count; i++)
                {
                    RenameSnapshotItem item = snapshotItems[i];
                    SummaryDbRollbackEntry rb = new SummaryDbRollbackEntry();
                    rb.ConversationId = item.ConversationId;
                    rb.OldTitle = item.OldTitle ?? string.Empty;
                    rb.OldPreview = item.OldPreview ?? string.Empty;
                    rb.OldRawSummary = item.GetOldRawSummaryBytes();
                    restoreMap[item.ConversationId] = rb;
                }
                List<string> dbWarn;
                if (!SummarySqliteBridge.RestoreSummaryRowsTransaction(dbPath, restoreMap, out dbWarn))
                {
                    if (warnings != null) warnings.Add("回滚 SQLite Layer 2 失败: " + (SummarySqliteBridge.LastError ?? "未知错误"));
                }
                if (dbWarn != null && dbWarn.Count > 0 && warnings != null) warnings.AddRange(dbWarn);
            }
        }

        internal static byte[] ReadAllBytesShared(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (MemoryStream ms = new MemoryStream())
            {
                fs.CopyTo(ms);
                return ms.ToArray();
            }
        }

        internal static string ReadUtf8TextShared(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        private static void AppendWarningsToLastWarning(List<string> warnings, string fallbackError)
        {
            string msg = null;
            if (warnings != null && warnings.Count > 0)
            {
                List<string> nonEmpty = new List<string>(warnings.Count);
                for (int i = 0; i < warnings.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(warnings[i]))
                        nonEmpty.Add(warnings[i].Trim());
                }
                if (nonEmpty.Count > 0)
                    msg = string.Join("; ", nonEmpty.ToArray());
            }
            else if (!string.IsNullOrWhiteSpace(fallbackError))
            {
                msg = fallbackError.Trim();
            }

            if (!string.IsNullOrEmpty(msg))
            {
                LastWarning = string.IsNullOrEmpty(LastWarning) ? msg : (LastWarning + "; " + msg);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // F2-B: 会话跨工程迁移 / 归属重绑定
        // ══════════════════════════════════════════════════════════════════
        public static OperationResult<int> MoveSessionsToProjectResult(
            string geminiPath,
            List<string> convIds,
            string targetProjectName,
            string targetProjectId,
            string targetFolderUri)
        {
            try
            {
                int count = MoveSessionsToProject(geminiPath, convIds, targetProjectName, targetProjectId, targetFolderUri);
                List<string> warns = string.IsNullOrEmpty(LastWarning) ? null : new List<string> { LastWarning };
                return OperationResult<int>.Success(count, warns);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return OperationResult<int>.Fail(ex.Message);
            }
        }

        public static int MoveSessionsToProject(
            string geminiPath,
            List<string> convIds,
            string targetProjectName,
            string targetProjectId,
            string targetFolderUri)
        {
            LastWarning = null;
            LastError = null;
            LastFailedSqliteCids = new List<string>();

            if (string.IsNullOrEmpty(geminiPath) || convIds == null || convIds.Count == 0 || string.IsNullOrWhiteSpace(targetProjectName))
                return 0;

            string pbPath = Path.Combine(geminiPath, "antigravity", "agyhub_summaries_proto.pb");
            string summaryDbPath = Path.Combine(geminiPath, "antigravity", "conversation_summaries.db");
            bool hasPb = File.Exists(pbPath);
            bool hasSummaryDb = File.Exists(summaryDbPath);
            if (!hasPb && !hasSummaryDb) return 0;

            HashSet<string> targetSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < convIds.Count; i++)
            {
                if (SessionMetadataExtractor.IsLikelyConversationId(convIds[i]))
                    targetSet.Add(convIds[i].Trim());
            }
            if (targetSet.Count == 0) return 0;

            bool isOutside = string.Equals(targetProjectName.Trim(), "Outside of Project", StringComparison.OrdinalIgnoreCase);
            string normUri = isOutside ? string.Empty : ToFileUri(targetFolderUri);
            string normProjId = isOutside
                ? "outside-of-project"
                : (string.IsNullOrEmpty(targetProjectId) ? Guid.NewGuid().ToString() : targetProjectId.Trim());

            if (!isOutside)
            {
                string projDir = Path.Combine(geminiPath, "config", "projects");
                if (!Directory.Exists(projDir)) Directory.CreateDirectory(projDir);
                string projJsonPath = Path.Combine(projDir, normProjId + ".json");
                if (!File.Exists(projJsonPath))
                {
                    string jsonContent = WorkspaceService.GenerateStandardProjectJson(normProjId, targetProjectName.Trim(), normUri);
                    WriteUtf8TextAtomicWithBackup(projJsonPath, jsonContent, false, null);
                }
            }

            List<ProtoNode> rootNodes = null;
            if (hasPb)
            {
                byte[] pbBytes = File.ReadAllBytes(pbPath);
                rootNodes = ProtoCodec.Decode(pbBytes);

                List<ConversationSummaryInfo> parsedList = new List<ConversationSummaryInfo>(rootNodes.Count);
                for (int i = 0; i < rootNodes.Count; i++)
                {
                    ConversationSummaryInfo info = (rootNodes[i].FieldNumber == 1 && rootNodes[i].WireType == 2)
                        ? SessionMetadataExtractor.ParseEntryNode(rootNodes[i])
                        : null;
                    parsedList.Add(info);
                }

                bool addedChild = true;
                while (addedChild)
                {
                    addedChild = false;
                    for (int i = 0; i < parsedList.Count; i++)
                    {
                        ConversationSummaryInfo info = parsedList[i];
                        if (info == null || !SessionMetadataExtractor.IsLikelyConversationId(info.ConversationId)) continue;
                        string cid = info.ConversationId.Trim();
                        string parent = info.ParentConversationId != null ? info.ParentConversationId.Trim() : null;
                        if (!targetSet.Contains(cid) && !string.IsNullOrEmpty(parent) && targetSet.Contains(parent))
                        {
                            targetSet.Add(cid);
                            addedChild = true;
                        }
                    }
                }
            }

            if (hasSummaryDb)
            {
                Dictionary<string, SummaryDbRowInfo> dbSnap = SummarySqliteBridge.ReadSummaryDbSnapshot(summaryDbPath);
                if (dbSnap != null && dbSnap.Count > 0)
                {
                    bool addedDbChild = true;
                    while (addedDbChild)
                    {
                        addedDbChild = false;
                        foreach (KeyValuePair<string, SummaryDbRowInfo> kvp in dbSnap)
                        {
                            SummaryDbRowInfo row = kvp.Value;
                            if (row == null || !SessionMetadataExtractor.IsLikelyConversationId(row.ConversationId)) continue;
                            string cid = row.ConversationId.Trim();
                            string parent = row.ParentConversationId != null ? row.ParentConversationId.Trim() : null;
                            if (!targetSet.Contains(cid) && !string.IsNullOrEmpty(parent) && targetSet.Contains(parent))
                            {
                                targetSet.Add(cid);
                                addedDbChild = true;
                            }
                        }
                    }
                }
            }

            int movedCount = 0;
            if (rootNodes != null)
            {
                for (int i = 0; i < rootNodes.Count; i++)
                {
                    ProtoNode entry = rootNodes[i];
                    if (entry.FieldNumber == 1 && entry.WireType == 2)
                    {
                        ProtoCodec.DecodeSummaryEntryTree(entry);
                        ProtoNode f1 = entry.GetChild(1);
                        if (f1 == null) continue;
                        string cid = f1.GetString() != null ? f1.GetString().Trim() : null;
                        if (string.IsNullOrEmpty(cid)) continue;

                        if (targetSet.Contains(cid))
                        {
                            ApplyProjectBindingToEntryAst(entry, targetProjectName.Trim(), normProjId, normUri);
                            movedCount++;
                        }
                    }
                }

                if (movedCount > 0)
                {
                    byte[] updatedPb = ProtoCodec.Encode(rootNodes);
                    UnifiedJson.CreateTimestampedBackup(pbPath, 5);
                    WriteBytesAtomicWithBackup(pbPath, updatedPb, null);
                }
            }

            int summaryDbUpdated = 0;
            if (hasSummaryDb)
            {
                List<string> sumWarnings;
                summaryDbUpdated = SummarySqliteBridge.SyncProjectBindingInSummaryDb(
                    summaryDbPath,
                    targetSet,
                    targetProjectName.Trim(),
                    normProjId,
                    normUri,
                    out sumWarnings);
                AppendWarningsToLastWarning(sumWarnings, SummarySqliteBridge.LastError);
            }

            if (movedCount > 0 || summaryDbUpdated > 0)
            {
                string convDir = Path.Combine(geminiPath, "antigravity", "conversations");
                Dictionary<string, string> uriOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (!isOutside && !string.IsNullOrEmpty(normUri))
                {
                    uriOverrides[targetProjectName.Trim()] = normUri;
                }

                List<string> failedCids = LastFailedSqliteCids;
                foreach (string cid in targetSet)
                {
                    string dbFile = Path.Combine(convDir, cid + ".db");
                    if (File.Exists(dbFile))
                    {
                        bool ok = SqliteMetadataUpdater.RemapSqliteDbMetadata(
                            dbFile, null, null, uriOverrides, targetProjectName.Trim(), normProjId, null
                        );
                        if (!ok)
                        {
                            failedCids.Add(cid + (string.IsNullOrEmpty(SqliteMetadataUpdater.LastError) ? "" : " (" + SqliteMetadataUpdater.LastError + ")"));
                        }
                    }
                }

                if (failedCids.Count > 0)
                {
                    string perConvWarn = "已更新 .pb 索引，但有 " + failedCids.Count + " 个 SQLite 会话库未能同步写入: " + string.Join("; ", failedCids.ToArray());
                    LastWarning = string.IsNullOrEmpty(LastWarning) ? perConvWarn : (perConvWarn + "; " + LastWarning);
                }
            }

            return movedCount > 0 ? movedCount : summaryDbUpdated;
        }

        // ══════════════════════════════════════════════════════════════════
        // F2-C: 工程物理路径重定向与幽灵工程修复 (FIX P0-1: nested gitFolder.folderUri)
        // ══════════════════════════════════════════════════════════════════
        public static OperationResult<int> RelocateProjectPathResult(
            string geminiPath,
            string projectId,
            string projectName,
            string newPhysicalOrUriPath,
            List<ConversationSummaryInfo> allLocalSummaries)
        {
            try
            {
                int count = RelocateProjectPath(geminiPath, projectId, projectName, newPhysicalOrUriPath, allLocalSummaries);
                List<string> warns = string.IsNullOrEmpty(LastWarning) ? null : new List<string> { LastWarning };
                return OperationResult<int>.Success(count, warns);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return OperationResult<int>.Fail(ex.Message);
            }
        }

        public static int RelocateProjectPath(
            string geminiPath,
            string projectId,
            string projectName,
            string newPhysicalOrUriPath,
            List<ConversationSummaryInfo> allLocalSummaries)
        {
            LastWarning = null;
            LastError = null;
            if (string.IsNullOrEmpty(geminiPath) || string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(newPhysicalOrUriPath))
                return 0;

            string newFolderUri = ToFileUri(newPhysicalOrUriPath);
            string resolvedProjId = projectId;
            string oldNormFolderUri = null;
            string summaryDbPath = Path.Combine(geminiPath, "antigravity", "conversation_summaries.db");
            Dictionary<string, SummaryDbRowInfo> dbSnap = File.Exists(summaryDbPath)
                ? SummarySqliteBridge.ReadSummaryDbSnapshot(summaryDbPath)
                : null;

            List<ConversationSummaryInfo> effectiveSummaries = allLocalSummaries;
            if (effectiveSummaries == null || effectiveSummaries.Count == 0)
            {
                string pbPath = Path.Combine(geminiPath, "antigravity", "agyhub_summaries_proto.pb");
                if (File.Exists(pbPath))
                {
                    effectiveSummaries = SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(pbPath));
                }
            }

            if (string.IsNullOrEmpty(resolvedProjId) || string.Equals(resolvedProjId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
            {
                if (effectiveSummaries != null)
                {
                    for (int i = 0; i < effectiveSummaries.Count; i++)
                    {
                        ConversationSummaryInfo s = effectiveSummaries[i];
                        if (s != null && string.Equals(s.ProjectName, projectName, StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrEmpty(s.ProjectId) &&
                            !string.Equals(s.ProjectId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
                        {
                            resolvedProjId = s.ProjectId;
                            if (string.IsNullOrEmpty(oldNormFolderUri) && !string.IsNullOrEmpty(s.FolderUri))
                                oldNormFolderUri = DirectStreamArchiver.NormalizeUri(s.FolderUri);
                            break;
                        }
                    }
                }
                if (string.IsNullOrEmpty(resolvedProjId) || string.Equals(resolvedProjId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
                {
                    resolvedProjId = Guid.NewGuid().ToString();
                }
            }

            string projDir = Path.Combine(geminiPath, "config", "projects");
            if (!Directory.Exists(projDir)) Directory.CreateDirectory(projDir);
            string projJsonPath = Path.Combine(projDir, resolvedProjId + ".json");

            if (File.Exists(projJsonPath))
            {
                string existingJson = ReadUtf8TextShared(projJsonPath);
                string existingUri = DirectStreamArchiver.ExtractJsonValue(existingJson, "folderUri");
                if (string.IsNullOrEmpty(oldNormFolderUri) && !string.IsNullOrEmpty(existingUri))
                    oldNormFolderUri = DirectStreamArchiver.NormalizeUri(existingUri);

                string patchedJson = PatchExistingProjectJson(existingJson, projectName, newFolderUri);
                UnifiedJson.CreateTimestampedBackup(projJsonPath, 5);
                WriteUtf8TextAtomicWithBackup(projJsonPath, patchedJson, false, null);
            }
            else
            {
                string jsonContent = WorkspaceService.GenerateStandardProjectJson(resolvedProjId, projectName, newFolderUri);
                WriteUtf8TextAtomicWithBackup(projJsonPath, jsonContent, false, null);
            }

            List<string> projConvIds = new List<string>();
            HashSet<string> seenProjCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool hasExplicitProjId = !string.IsNullOrEmpty(projectId) && !string.Equals(projectId, "outside-of-project", StringComparison.OrdinalIgnoreCase);
            if (effectiveSummaries != null)
            {
                for (int i = 0; i < effectiveSummaries.Count; i++)
                {
                    ConversationSummaryInfo s = effectiveSummaries[i];
                    if (s == null || !SessionMetadataExtractor.IsLikelyConversationId(s.ConversationId)) continue;

                    bool belongs = false;
                    if (!string.IsNullOrEmpty(s.ProjectId))
                    {
                        if (string.Equals(s.ProjectId, resolvedProjId, StringComparison.OrdinalIgnoreCase))
                            belongs = true;
                        else if (hasExplicitProjId && !string.Equals(s.ProjectId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
                            belongs = false;
                    }
                    else if (!string.IsNullOrEmpty(oldNormFolderUri) && !string.IsNullOrEmpty(s.FolderUri))
                    {
                        belongs = string.Equals(DirectStreamArchiver.NormalizeUri(s.FolderUri), oldNormFolderUri, StringComparison.OrdinalIgnoreCase);
                    }
                    else if (string.Equals(s.ProjectName, projectName, StringComparison.OrdinalIgnoreCase))
                    {
                        belongs = true;
                    }

                    if (belongs)
                    {
                        string trimmedCid = s.ConversationId.Trim();
                        if (seenProjCids.Add(trimmedCid))
                        {
                            projConvIds.Add(trimmedCid);
                        }
                    }
                }
            }

            if (dbSnap != null && dbSnap.Count > 0)
            {
                foreach (KeyValuePair<string, SummaryDbRowInfo> kvp in dbSnap)
                {
                    SummaryDbRowInfo row = kvp.Value;
                    if (row == null || !SessionMetadataExtractor.IsLikelyConversationId(row.ConversationId)) continue;
                    string trimmedCid = row.ConversationId.Trim();
                    if (seenProjCids.Contains(trimmedCid)) continue;

                    if (!string.IsNullOrEmpty(row.ProjectId) &&
                        string.Equals(row.ProjectId.Trim(), resolvedProjId, StringComparison.OrdinalIgnoreCase))
                    {
                        seenProjCids.Add(trimmedCid);
                        projConvIds.Add(trimmedCid);
                    }
                }
            }

            return MoveSessionsToProject(geminiPath, projConvIds, projectName, resolvedProjId, newFolderUri);
        }

        public static string PatchExistingProjectJson(string existingJson, string projectName, string newFolderUri)
        {
            if (string.IsNullOrWhiteSpace(existingJson))
                return WorkspaceService.GenerateStandardProjectJson(Guid.NewGuid().ToString(), projectName, newFolderUri);

            try
            {
                string nowIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
                string uriLiteral = "\"" + UnifiedJson.EscapeString(newFolderUri ?? string.Empty) + "\"";
                string nowLiteral = "\"" + nowIso + "\"";

                // FIX P0-1: Patch projectResources.resources[*].gitFolder.folderUri & root updatedAt.
                // Do NOT insert a bogus root folderUri; only update root folderUri if a legacy root folderUri already exists.
                string result = existingJson;
                result = UnifiedJson.PatchNestedPathPreservingComments(
                    result,
                    new string[] { "projectResources", "resources[*]", "gitFolder", "folderUri" },
                    uriLiteral);

                object parsed;
                if (UnifiedJson.TryParse(result, out parsed))
                {
                    Dictionary<string, object> rootDict = UnifiedJson.AsDict(parsed);
                    if (rootDict != null)
                    {
                        string existingRootUriKey = null;
                        foreach (string k in rootDict.Keys)
                        {
                            if (string.Equals(k, "folderUri", StringComparison.OrdinalIgnoreCase))
                            {
                                existingRootUriKey = k;
                                break;
                            }
                        }
                        if (!string.IsNullOrEmpty(existingRootUriKey))
                        {
                            result = UnifiedJson.PatchObjectKeyPreservingComments(result, null, existingRootUriKey, uriLiteral);
                        }
                    }
                }

                result = UnifiedJson.PatchObjectKeyPreservingComments(result, null, "updatedAt", nowLiteral);
                return result;
            }
            catch
            {
                return existingJson;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // F3: 细粒度选择性轻量备份导出 (FIX P0-3 CLI support, P0-4 SqliteOnlineBackup)
        // ══════════════════════════════════════════════════════════════════
        public static OperationResult<int> ExportSelectiveBackupResult(
            string geminiPath,
            string homePath,
            string destZipPath,
            List<ConversationSummaryInfo> selectedSessions,
            Dictionary<string, ProjectMetadata> localProjects,
            bool includeGlobalConfigs,
            List<CapabilityItem> selectedCapabilities = null)
        {
            try
            {
                int count = ExportSelectiveBackup(
                    geminiPath, homePath, destZipPath, selectedSessions,
                    localProjects, includeGlobalConfigs, selectedCapabilities);
                List<string> warns = string.IsNullOrEmpty(LastWarning) ? null : new List<string> { LastWarning };
                return OperationResult<int>.Success(count, warns);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return OperationResult<int>.Fail(ex.Message);
            }
        }

        public static int ExportSelectiveBackup(
            string geminiPath,
            string homePath,
            string destZipPath,
            List<ConversationSummaryInfo> selectedSessions,
            Dictionary<string, ProjectMetadata> localProjects,
            bool includeGlobalConfigs)
        {
            return ExportSelectiveBackup(geminiPath, homePath, destZipPath, selectedSessions, localProjects, includeGlobalConfigs, null);
        }

        public static int ExportSelectiveBackup(
            string geminiPath,
            string homePath,
            string destZipPath,
            List<ConversationSummaryInfo> selectedSessions,
            Dictionary<string, ProjectMetadata> localProjects,
            bool includeGlobalConfigs,
            List<CapabilityItem> selectedCapabilities)
        {
            return ExportSelectiveBackup(geminiPath, homePath, destZipPath, selectedSessions, localProjects, includeGlobalConfigs, selectedCapabilities, BackupTier.FastDaily);
        }

        public static int ExportSelectiveBackup(
            string geminiPath,
            string homePath,
            string destZipPath,
            List<ConversationSummaryInfo> selectedSessions,
            Dictionary<string, ProjectMetadata> localProjects,
            bool includeGlobalConfigs,
            List<CapabilityItem> selectedCapabilities,
            BackupTier tier)
        {
            LastWarning = null;
            LastError = null;
            bool hasSessions = selectedSessions != null && selectedSessions.Count > 0;
            bool hasExplicitCaps = selectedCapabilities != null && selectedCapabilities.Count > 0;
            if (string.IsNullOrEmpty(geminiPath) || string.IsNullOrEmpty(destZipPath) || (!hasSessions && !includeGlobalConfigs && !hasExplicitCaps))
                return 0;

            string destDir = Path.GetDirectoryName(destZipPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            HashSet<string> selectedConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> cliConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> subagentConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> selectedProjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> selectedProjectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (hasSessions)
            {
                for (int i = 0; i < selectedSessions.Count; i++)
                {
                    ConversationSummaryInfo s = selectedSessions[i];
                    if (s == null || !SessionMetadataExtractor.IsLikelyConversationId(s.ConversationId)) continue;
                    string trimmedCid = s.ConversationId.Trim();
                    selectedConvIds.Add(trimmedCid);
                    if (s.IsCliSession) cliConvIds.Add(trimmedCid);
                    if (s.IsSubagent) subagentConvIds.Add(trimmedCid);
                    if (!string.IsNullOrEmpty(s.ProjectName)) selectedProjectNames.Add(s.ProjectName.Trim());
                    if (!string.IsNullOrEmpty(s.ProjectId)) selectedProjectIds.Add(s.ProjectId.Trim());
                }
            }

            int entriesWritten = 0;
            string safeHome = string.IsNullOrEmpty(homePath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : homePath;
            string tierName = tier == BackupTier.FullForensic ? "FullForensic" : "FastDaily";

            string tempZipPath = destZipPath + ".tmp." + Guid.NewGuid().ToString("N").Substring(0, 8);
            try
            {
                using (FileStream fs = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    ZipArchiveEntry metaEntry = archive.CreateEntry("metadata.json", CompressionLevel.Fastest);
                    using (StreamWriter sw = new StreamWriter(metaEntry.Open(), new UTF8Encoding(false)))
                    {
                        string meta = "{\r\n  \"source_home\": \"" + UnifiedJson.EscapeString(safeHome) + "\",\r\n" +
                                      "  \"source_user\": \"" + UnifiedJson.EscapeString(Environment.UserName) + "\",\r\n" +
                                      "  \"backup_time\": \"" + DateTime.Now.ToString("o") + "\",\r\n" +
                                      "  \"export_mode\": \"selective\",\r\n" +
                                      "  \"backup_tier\": \"" + tierName + "\",\r\n" +
                                      "  \"version\": \"1.0\"\r\n}";
                        sw.Write(meta);
                    }
                    entriesWritten++;

                    if (hasSessions)
                    {
                        string ideRoot = Path.Combine(geminiPath, "antigravity");
                        string cliRoot = Path.Combine(geminiPath, "antigravity-cli");
                        string idePbPath = Path.Combine(ideRoot, "agyhub_summaries_proto.pb");
                        string cliPbPath = Path.Combine(cliRoot, "agyhub_summaries_proto.pb");

                        List<ProtoNode> ideNodes = LoadProtoNodesIfPresent(idePbPath);
                        List<ProtoNode> cliNodes = LoadProtoNodesIfPresent(cliPbPath);

                        // Expand subagent closure across both IDE and CLI proto indexes
                        List<ConversationSummaryInfo> allParsed = new List<ConversationSummaryInfo>();
                        CollectParsedSummaries(ideNodes, false, allParsed, cliConvIds);
                        CollectParsedSummaries(cliNodes, true, allParsed, cliConvIds);

                        for (int i = 0; i < allParsed.Count; i++)
                        {
                            ConversationSummaryInfo info = allParsed[i];
                            if (info != null && info.IsSubagent && SessionMetadataExtractor.IsLikelyConversationId(info.ConversationId))
                                subagentConvIds.Add(info.ConversationId.Trim());
                        }

                        bool addedSub = true;
                        while (addedSub)
                        {
                            addedSub = false;
                            for (int i = 0; i < allParsed.Count; i++)
                            {
                                ConversationSummaryInfo info = allParsed[i];
                                if (info == null || !SessionMetadataExtractor.IsLikelyConversationId(info.ConversationId)) continue;
                                string cidTrim = info.ConversationId.Trim();
                                string parentTrim = info.ParentConversationId != null ? info.ParentConversationId.Trim() : null;
                                if (!selectedConvIds.Contains(cidTrim) &&
                                    !string.IsNullOrEmpty(parentTrim) &&
                                    selectedConvIds.Contains(parentTrim))
                                {
                                    selectedConvIds.Add(cidTrim);
                                    if (cliConvIds.Contains(parentTrim)) cliConvIds.Add(cidTrim);
                                    if (info.IsSubagent) subagentConvIds.Add(cidTrim);
                                    addedSub = true;
                                }
                            }
                        }

                        // Export per-session .db (via SqliteOnlineBackup), brain/<cid>/, annotations/<cid>.pbtxt for IDE & CLI
                        HashSet<string> excludedSubagentDbs = tier == BackupTier.FastDaily ? subagentConvIds : null;
                        entriesWritten += ExportSessionArtifactsForRoot(archive, ideRoot, "data/antigravity", selectedConvIds, excludedSubagentDbs);
                        entriesWritten += ExportSessionArtifactsForRoot(archive, cliRoot, "data/antigravity-cli", selectedConvIds, excludedSubagentDbs);

                        // Write sliced IDE .pb
                        List<ProtoNode> slicedIdeNodes = SliceNodesByConvIds(ideNodes, selectedConvIds);
                        if (ideNodes == null && selectedSessions != null)
                        {
                            for (int i = 0; i < selectedSessions.Count; i++)
                            {
                                if (selectedSessions[i] != null && !selectedSessions[i].IsCliSession && selectedSessions[i].RawProtoEntry != null)
                                    slicedIdeNodes.Add(selectedSessions[i].RawProtoEntry);
                            }
                        }
                        byte[] slicedIdeBytes = ProtoCodec.Encode(slicedIdeNodes);
                        ZipArchiveEntry pbEntry = archive.CreateEntry("data/antigravity/agyhub_summaries_proto.pb", CompressionLevel.Fastest);
                        using (Stream s = pbEntry.Open())
                        {
                            s.Write(slicedIdeBytes, 0, slicedIdeBytes.Length);
                        }
                        entriesWritten++;

                        // Write sliced CLI .pb if any CLI nodes matched
                        List<ProtoNode> slicedCliNodes = SliceNodesByConvIds(cliNodes, selectedConvIds);
                        if (cliNodes == null && selectedSessions != null)
                        {
                            for (int i = 0; i < selectedSessions.Count; i++)
                            {
                                if (selectedSessions[i] != null && selectedSessions[i].IsCliSession && selectedSessions[i].RawProtoEntry != null)
                                    slicedCliNodes.Add(selectedSessions[i].RawProtoEntry);
                            }
                        }
                        if (slicedCliNodes.Count > 0)
                        {
                            byte[] slicedCliBytes = ProtoCodec.Encode(slicedCliNodes);
                            ZipArchiveEntry cliPbEntry = archive.CreateEntry("data/antigravity-cli/agyhub_summaries_proto.pb", CompressionLevel.Fastest);
                            using (Stream s = cliPbEntry.Open())
                            {
                                s.Write(slicedCliBytes, 0, slicedCliBytes.Length);
                            }
                            entriesWritten++;
                        }

                        // Export config/projects/<id>.json
                        string projDir = Path.Combine(geminiPath, "config", "projects");
                        if (Directory.Exists(projDir))
                        {
                            string[] projFiles = Directory.GetFiles(projDir, "*.json");
                            for (int i = 0; i < projFiles.Length; i++)
                            {
                                string pf = projFiles[i];
                                string pId = Path.GetFileNameWithoutExtension(pf);
                                bool includeProj = selectedProjectIds.Contains(pId);
                                if (!includeProj && selectedProjectIds.Count == 0)
                                {
                                    try
                                    {
                                        string json = ReadUtf8TextShared(pf);
                                        string pName = DirectStreamArchiver.ExtractJsonValue(json, "name");
                                        if (!string.IsNullOrEmpty(pName) && selectedProjectNames.Contains(pName))
                                            includeProj = true;
                                    }
                                    catch { }
                                }

                                if (includeProj)
                                {
                                    entriesWritten += AddFileToZip(archive, pf, "data/config/projects/" + Path.GetFileName(pf));
                                }
                            }
                        }
                    }

                    if (includeGlobalConfigs)
                    {
                        string rootSettings = Path.Combine(geminiPath, "settings.json");
                        if (File.Exists(rootSettings))
                            entriesWritten += AddFileToZip(archive, rootSettings, "data/settings.json");

                        string cliSettings = Path.Combine(geminiPath, "antigravity-cli", "settings.json");
                        if (File.Exists(cliSettings))
                            entriesWritten += AddFileToZip(archive, cliSettings, "data/antigravity-cli/settings.json");
                    }

                    List<CapabilityItem> capsToPack = selectedCapabilities;
                    if ((capsToPack == null || capsToPack.Count == 0) && includeGlobalConfigs)
                    {
                        capsToPack = CapabilityVaultService.ScanLocalCapabilities(geminiPath, safeHome);
                    }

                    if (capsToPack != null && capsToPack.Count > 0)
                    {
                        entriesWritten += AddCapabilitiesToZip(archive, capsToPack, safeHome);
                    }
                }

                if (File.Exists(destZipPath))
                {
                    File.SetAttributes(destZipPath, FileAttributes.Normal);
                    File.Delete(destZipPath);
                }
                File.Move(tempZipPath, destZipPath);
                return entriesWritten;
            }
            catch
            {
                try
                {
                    if (File.Exists(tempZipPath)) File.Delete(tempZipPath);
                }
                catch { }
                throw;
            }
        }

        private static List<ProtoNode> LoadProtoNodesIfPresent(string pbPath)
        {
            if (string.IsNullOrEmpty(pbPath) || !File.Exists(pbPath)) return null;
            byte[] fullPb = ReadAllBytesShared(pbPath);
            return ProtoCodec.Decode(fullPb);
        }

        private static void CollectParsedSummaries(
            List<ProtoNode> nodes,
            bool isCli,
            List<ConversationSummaryInfo> allParsed,
            HashSet<string> cliConvIds)
        {
            if (nodes == null) return;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].FieldNumber == 1 && nodes[i].WireType == 2)
                {
                    ConversationSummaryInfo info = SessionMetadataExtractor.ParseEntryNode(nodes[i]);
                    if (info != null)
                    {
                        allParsed.Add(info);
                        if (isCli && !string.IsNullOrEmpty(info.ConversationId))
                            cliConvIds.Add(info.ConversationId.Trim());
                    }
                }
            }
        }

        private static List<ProtoNode> SliceNodesByConvIds(List<ProtoNode> nodes, HashSet<string> selectedConvIds)
        {
            List<ProtoNode> sliced = new List<ProtoNode>();
            if (nodes == null || selectedConvIds == null || selectedConvIds.Count == 0) return sliced;
            for (int i = 0; i < nodes.Count; i++)
            {
                ProtoNode node = nodes[i];
                if (node.FieldNumber == 1 && node.WireType == 2)
                {
                    ProtoCodec.DecodeSummaryEntryTree(node);
                    ProtoNode f1 = node.GetChild(1);
                    string rawCid = f1 != null ? (f1.GetString() ?? string.Empty).Trim() : null;
                    if (!string.IsNullOrEmpty(rawCid) && selectedConvIds.Contains(rawCid))
                    {
                        sliced.Add(node);
                    }
                }
            }
            return sliced;
        }

        private static int ExportSessionArtifactsForRoot(
            ZipArchive archive,
            string dataRootDir,
            string zipDataPrefix,
            HashSet<string> selectedConvIds,
            HashSet<string> excludedSubagentDbs)
        {
            if (!Directory.Exists(dataRootDir) || selectedConvIds == null || selectedConvIds.Count == 0)
                return 0;

            int count = 0;
            string convDir = Path.Combine(dataRootDir, "conversations");
            string brainDir = Path.Combine(dataRootDir, "brain");
            string annotationsDir = Path.Combine(dataRootDir, "annotations");

            foreach (string cid in selectedConvIds)
            {
                if (!SessionMetadataExtractor.IsLikelyConversationId(cid)) continue;
                if (string.Equals(cid, "bin", StringComparison.OrdinalIgnoreCase)) continue;

                bool skipDb = excludedSubagentDbs != null && excludedSubagentDbs.Contains(cid);
                string srcDbPath = Path.Combine(convDir, cid + ".db");
                if (!skipDb && IsPathContainedUnder(convDir, srcDbPath) && File.Exists(srcDbPath))
                {
                    count += AddSqliteDbSnapshotToZip(archive, srcDbPath, zipDataPrefix + "/conversations/" + cid + ".db");
                }

                string cidBrainDir = Path.Combine(brainDir, cid);
                if (IsPathContainedUnder(brainDir, cidBrainDir) && Directory.Exists(cidBrainDir))
                {
                    count += AddDirToZipExcludingBin(archive, cidBrainDir, zipDataPrefix + "/brain/" + cid);
                }

                string annotPath = Path.Combine(annotationsDir, cid + ".pbtxt");
                if (IsPathContainedUnder(annotationsDir, annotPath) && File.Exists(annotPath))
                {
                    count += AddFileToZip(archive, annotPath, zipDataPrefix + "/annotations/" + cid + ".pbtxt");
                }
            }
            return count;
        }

        internal static int AddSqliteDbSnapshotToZip(ZipArchive archive, string srcDbPath, string zipEntryName)
        {
            if (archive == null || string.IsNullOrEmpty(srcDbPath) || !File.Exists(srcDbPath))
                return 0;

            string tempSnap = Path.Combine(Path.GetTempPath(), "agy_db_snap_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".db");
            try
            {
                string backupErr;
                if (SqliteOnlineBackup.BackupDatabaseAtomic(srcDbPath, tempSnap, out backupErr))
                {
                    return AddFileToZip(archive, tempSnap, zipEntryName);
                }
                if (!string.IsNullOrEmpty(backupErr))
                {
                    AppendWarningsToLastWarning(null, "SQLite 在线快照失败 (" + Path.GetFileName(srcDbPath) + "): " + backupErr);
                }
                return 0;
            }
            finally
            {
                DeleteFileSafe(tempSnap);
                DeleteFileSafe(tempSnap + "-wal");
                DeleteFileSafe(tempSnap + "-shm");
                DeleteFileSafe(tempSnap + "-journal");
            }
        }

        private static int AddCapabilitiesToZip(ZipArchive archive, List<CapabilityItem> caps, string sourceHomePath)
        {
            int count = 0;
            string slicedConfigJson = "{}";
            bool hasPluginReg = false;
            string slicedMcpJson = "{}";
            bool hasMcp = false;
            string slicedHooksJson = "{}";
            bool hasHooks = false;
            HashSet<string> writtenZipEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < caps.Count; i++)
            {
                CapabilityItem item = caps[i];
                if (item == null) continue;

                switch (item.Kind)
                {
                    case CapabilityKind.Plugin:
                    {
                        string safePluginFolder = CapabilityVaultService.EncodeSafeCapabilityFolderName(item.Id);
                        if (item.FilesMap.Count == 0)
                        {
                            string dirPlaceholder = "data/config/plugins/" + safePluginFolder + "/";
                            if (writtenZipEntries.Add(dirPlaceholder))
                            {
                                archive.CreateEntry(dirPlaceholder);
                                count++;
                            }
                        }
                        else
                        {
                            foreach (KeyValuePair<string, byte[]> kvp in item.FilesMap)
                            {
                                string entryPath = "data/config/plugins/" + safePluginFolder + "/" + kvp.Key.Replace('\\', '/');
                                if (writtenZipEntries.Add(entryPath))
                                {
                                    count += AddBytesToZip(archive, item.GetFileBytes(kvp.Key), entryPath);
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(item.RawJsonFragment))
                        {
                            string expanded = CapabilityPathNormalizer.ExpandHomePlaceholder(item.RawJsonFragment, sourceHomePath, true).Trim();
                            slicedConfigJson = UnifiedJson.PatchObjectKeyPreservingComments(slicedConfigJson, "plugins", item.Id, expanded);
                            hasPluginReg = true;
                        }
                        break;
                    }
                    case CapabilityKind.Skill:
                    {
                        string safeSkillFolder = CapabilityVaultService.EncodeSafeCapabilityFolderName(item.Id);
                        if (item.FilesMap.Count == 0)
                        {
                            string dirPlaceholder = "data/config/skills/" + safeSkillFolder + "/";
                            if (writtenZipEntries.Add(dirPlaceholder))
                            {
                                archive.CreateEntry(dirPlaceholder);
                                count++;
                            }
                        }
                        else
                        {
                            foreach (KeyValuePair<string, byte[]> kvp in item.FilesMap)
                            {
                                string entryPath = "data/config/skills/" + safeSkillFolder + "/" + kvp.Key.Replace('\\', '/');
                                if (writtenZipEntries.Add(entryPath))
                                {
                                    count += AddBytesToZip(archive, item.GetFileBytes(kvp.Key), entryPath);
                                }
                            }
                        }
                        break;
                    }
                    case CapabilityKind.Rule:
                    {
                        foreach (KeyValuePair<string, byte[]> kvp in item.FilesMap)
                        {
                            string rel = kvp.Key.Replace('\\', '/');
                            string entryPath;
                            if (rel.StartsWith("_root/", StringComparison.OrdinalIgnoreCase))
                                entryPath = "data/" + rel.Substring("_root/".Length);
                            else if (rel.StartsWith("config/", StringComparison.OrdinalIgnoreCase))
                                entryPath = "data/config/rules/" + rel.Substring("config/".Length);
                            else
                                entryPath = "data/config/rules/" + rel;

                            if (writtenZipEntries.Add(entryPath))
                            {
                                count += AddBytesToZip(archive, item.GetFileBytes(kvp.Key), entryPath);
                            }
                        }
                        break;
                    }
                    case CapabilityKind.McpServer:
                    {
                        if (!string.IsNullOrEmpty(item.RawJsonFragment))
                        {
                            string expanded = CapabilityPathNormalizer.ExpandHomePlaceholder(item.RawJsonFragment, sourceHomePath, true).Trim();
                            slicedMcpJson = UnifiedJson.PatchObjectKeyPreservingComments(slicedMcpJson, "mcpServers", item.Id, expanded);
                            hasMcp = true;
                        }
                        foreach (KeyValuePair<string, byte[]> kvp in item.FilesMap)
                        {
                            string rel = kvp.Key.Replace('\\', '/');
                            if (rel.StartsWith("schemas/", StringComparison.OrdinalIgnoreCase))
                                rel = rel.Substring("schemas/".Length);
                            string entryPath = "data/antigravity/mcp/" + CapabilityVaultService.EncodeSafeCapabilityFolderName(item.Id) + "/" + rel;
                            if (writtenZipEntries.Add(entryPath))
                            {
                                count += AddBytesToZip(archive, item.GetFileBytes(kvp.Key), entryPath);
                            }
                        }
                        break;
                    }
                    case CapabilityKind.Hook:
                    {
                        if (!string.IsNullOrEmpty(item.RawJsonFragment))
                        {
                            string expanded = CapabilityPathNormalizer.ExpandHomePlaceholder(item.RawJsonFragment, sourceHomePath, true).Trim();
                            slicedHooksJson = UnifiedJson.PatchObjectKeyPreservingComments(slicedHooksJson, null, item.Id, expanded);
                            hasHooks = true;
                        }
                        foreach (KeyValuePair<string, byte[]> kvp in item.FilesMap)
                        {
                            string rel = kvp.Key.Replace('\\', '/');
                            if (rel.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase))
                                rel = rel.Substring("scripts/".Length);
                            string entryPath = "data/config/" + rel;
                            if (writtenZipEntries.Add(entryPath))
                            {
                                count += AddBytesToZip(archive, item.GetFileBytes(kvp.Key), entryPath);
                            }
                        }
                        break;
                    }
                }
            }

            if (hasPluginReg && writtenZipEntries.Add("data/config/config.json"))
                count += AddBytesToZip(archive, Encoding.UTF8.GetBytes(slicedConfigJson), "data/config/config.json");
            if (hasMcp && writtenZipEntries.Add("data/config/mcp_config.json"))
                count += AddBytesToZip(archive, Encoding.UTF8.GetBytes(slicedMcpJson), "data/config/mcp_config.json");
            if (hasHooks && writtenZipEntries.Add("data/config/hooks.json"))
                count += AddBytesToZip(archive, Encoding.UTF8.GetBytes(slicedHooksJson), "data/config/hooks.json");

            return count;
        }

        private static int AddBytesToZip(ZipArchive archive, byte[] data, string entryName)
        {
            if (data == null) return 0;
            ZipArchiveEntry entry = archive.CreateEntry(entryName.Replace('\\', '/'), CompressionLevel.Fastest);
            using (Stream s = entry.Open())
            {
                s.Write(data, 0, data.Length);
            }
            return 1;
        }

        // ══════════════════════════════════════════════════════════════════
        // 内部 AST 与文件系统辅助方法
        // ══════════════════════════════════════════════════════════════════
        public static string ToFileUri(string pathOrUri)
        {
            if (string.IsNullOrEmpty(pathOrUri)) return string.Empty;
            string s = pathOrUri.Trim();
            bool isUnc = false;

            if (s.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                s = DirectStreamArchiver.UnescapeUriString(s);
                if (s.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring(8);
                }
                else
                {
                    s = s.Substring(7);
                    isUnc = true;
                }
            }
            else if (s.StartsWith("\\\\") || s.StartsWith("//"))
            {
                isUnc = true;
                s = s.TrimStart('\\', '/');
            }

            string fwd = s.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrEmpty(fwd)) return string.Empty;

            if (!isUnc && fwd.Length >= 2 && fwd[1] == ':' && fwd[0] >= 'A' && fwd[0] <= 'Z')
            {
                fwd = char.ToLowerInvariant(fwd[0]) + fwd.Substring(1);
            }

            string encoded = DirectStreamArchiver.UrlEncodePath(fwd, false, false);
            return isUnc ? "file://" + encoded : "file:///" + encoded;
        }

        private static void EnsureEntryChildrenDecoded(ProtoNode entryNode)
        {
            ProtoCodec.DecodeSummaryEntryTree(entryNode);
        }

        private static void ApplyTitleToEntryAst(ProtoNode entryNode, string newTitle)
        {
            if (entryNode == null || string.IsNullOrEmpty(newTitle)) return;
            EnsureEntryChildrenDecoded(entryNode);
            ProtoNode meta = entryNode.GetChild(2);
            if (meta == null) return;
            if (meta.Children == null) meta.Children = new List<ProtoNode>();

            ProtoNode f1 = meta.GetChild(1);
            if (f1 != null)
            {
                f1.RawBytes = Encoding.UTF8.GetBytes(newTitle);
                f1.Children.Clear();
                f1.IsModified = true;
            }
            else
            {
                f1 = new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(newTitle), IsModified = true };
                meta.Children.Insert(0, f1);
            }

            ProtoNode f15 = meta.GetChild(15);
            long epoch = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

            if (f15 != null)
            {
                if ((f15.Children == null || f15.Children.Count == 0) && f15.RawBytes != null && f15.RawBytes.Length > 0)
                {
                    List<ProtoNode> sub15;
                    if (f15.RawBytes.Length >= 2 && (f15.RawBytes[0] == 0x0A || f15.RawBytes[0] == 0x3A) &&
                        ProtoCodec.TryDecodeStrict(f15.RawBytes, out sub15))
                        f15.Children = sub15;
                }

                if (f15.HasChildren)
                {
                    ProtoNode ct1 = f15.GetChild(1);
                    if (ct1 != null)
                    {
                        ct1.RawBytes = Encoding.UTF8.GetBytes(newTitle);
                        ct1.Children.Clear();
                        ct1.IsModified = true;
                    }
                    else
                    {
                        f15.Children.Insert(0, new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(newTitle), IsModified = true });
                    }
                }
                else
                {
                    f15.Children = new List<ProtoNode>();
                    f15.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(newTitle), IsModified = true });
                    ProtoNode ts = new ProtoNode(7, 2) { IsModified = true };
                    ts.Children.Add(new ProtoNode(1, 0) { VarintValue = (ulong)epoch, IsModified = true });
                    f15.Children.Add(ts);
                }
                f15.IsModified = true;
            }
            else
            {
                ProtoNode newF15 = new ProtoNode(15, 2) { IsModified = true };
                newF15.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(newTitle), IsModified = true });
                ProtoNode ts = new ProtoNode(7, 2) { IsModified = true };
                ts.Children.Add(new ProtoNode(1, 0) { VarintValue = (ulong)epoch, IsModified = true });
                newF15.Children.Add(ts);
                meta.Children.Add(newF15);
            }

            meta.IsModified = true;
            entryNode.IsModified = true;
        }

        private static void ApplyProjectBindingToEntryAst(ProtoNode entryNode, string targetProjectName, string targetProjectId, string targetFolderUri)
        {
            if (entryNode == null) return;
            EnsureEntryChildrenDecoded(entryNode);
            ProtoNode meta = entryNode.GetChild(2);
            if (meta == null) return;
            if (meta.Children == null) meta.Children = new List<ProtoNode>();

            ProtoNode f9 = meta.GetChild(9);
            if (!string.IsNullOrEmpty(targetFolderUri))
            {
                if (f9 == null)
                {
                    f9 = new ProtoNode(9, 2) { IsModified = true };
                    f9.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(targetFolderUri), IsModified = true });
                    meta.Children.Add(f9);
                }
                else
                {
                    if ((f9.Children == null || f9.Children.Count == 0) && f9.RawBytes != null && f9.RawBytes.Length > 0)
                    {
                        List<ProtoNode> sub9;
                        if (ProtoCodec.TryDecodeStrict(f9.RawBytes, out sub9))
                            f9.Children = sub9;
                    }

                    if (f9.HasChildren)
                    {
                        ProtoNode f9c1 = f9.GetChild(1);
                        if (f9c1 != null)
                        {
                            f9c1.RawBytes = Encoding.UTF8.GetBytes(targetFolderUri);
                            f9c1.Children.Clear();
                            f9c1.IsModified = true;
                        }
                        else
                        {
                            f9.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(targetFolderUri), IsModified = true });
                        }
                    }
                    else
                    {
                        f9.Children = new List<ProtoNode>();
                        f9.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(targetFolderUri), IsModified = true });
                    }
                    f9.IsModified = true;
                }
            }
            else if (f9 != null)
            {
                meta.Children.Remove(f9);
            }

            ProtoNode f17 = meta.GetChild(17);
            if (f17 == null)
            {
                f17 = new ProtoNode(17, 2) { IsModified = true };
                meta.Children.Add(f17);
            }
            else if ((f17.Children == null || f17.Children.Count == 0) && f17.RawBytes != null && f17.RawBytes.Length > 0)
            {
                List<ProtoNode> sub17;
                if (ProtoCodec.TryDecodeStrict(f17.RawBytes, out sub17))
                    f17.Children = sub17;
            }

            if (f17.Children == null) f17.Children = new List<ProtoNode>();

            SetOrAddStringChild(f17, 1, targetProjectName);

            if (!string.IsNullOrEmpty(targetFolderUri))
            {
                SetOrAddStringChild(f17, 7, targetFolderUri);
            }
            else
            {
                ProtoNode c7 = f17.GetChild(7);
                if (c7 != null) f17.Children.Remove(c7);
            }

            if (!string.IsNullOrEmpty(targetProjectId))
            {
                SetOrAddStringChild(f17, 18, targetProjectId);
            }

            f17.IsModified = true;
            meta.IsModified = true;
            entryNode.IsModified = true;
        }

        private static void SetOrAddStringChild(ProtoNode parent, int fieldNumber, string val)
        {
            if (parent == null) return;
            ProtoNode child = parent.GetChild(fieldNumber);
            if (child != null)
            {
                child.RawBytes = Encoding.UTF8.GetBytes(val ?? string.Empty);
                child.Children.Clear();
                child.IsModified = true;
            }
            else
            {
                parent.Children.Add(new ProtoNode(fieldNumber, 2)
                {
                    RawBytes = Encoding.UTF8.GetBytes(val ?? string.Empty),
                    IsModified = true
                });
            }
        }

        public static void WriteBytesAtomicWithBackup(string targetPath, byte[] data, string backupPath = null)
        {
            if (string.IsNullOrEmpty(targetPath) || data == null) return;
            string dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string tmpPath = targetPath + ".tmp." + Guid.NewGuid().ToString("N").Substring(0, 8);
            try
            {
                using (FileStream fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.Write(data, 0, data.Length);
                    fs.Flush(true);
                }

                if (File.Exists(targetPath))
                {
                    try { File.SetAttributes(targetPath, FileAttributes.Normal); } catch { }
                    if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
                    {
                        try { File.SetAttributes(backupPath, FileAttributes.Normal); } catch { }
                    }
                    File.Replace(tmpPath, targetPath, backupPath, true);
                }
                else
                {
                    File.Move(tmpPath, targetPath);
                }
            }
            catch
            {
                try
                {
                    if (File.Exists(tmpPath)) File.Delete(tmpPath);
                }
                catch { }
                throw;
            }
        }

        public static void WriteUtf8TextAtomicWithBackup(string targetPath, string text, string backupPath)
        {
            WriteUtf8TextAtomicWithBackup(targetPath, text, false, backupPath);
        }

        public static void WriteUtf8TextAtomicWithBackup(string targetPath, string text, bool withBom = false, string backupPath = null)
        {
            UTF8Encoding enc = new UTF8Encoding(withBom);
            byte[] textBytes = enc.GetBytes(text ?? string.Empty);
            if (withBom)
            {
                byte[] preamble = enc.GetPreamble();
                byte[] combined = new byte[preamble.Length + textBytes.Length];
                Buffer.BlockCopy(preamble, 0, combined, 0, preamble.Length);
                Buffer.BlockCopy(textBytes, 0, combined, preamble.Length, textBytes.Length);
                WriteBytesAtomicWithBackup(targetPath, combined, backupPath);
            }
            else
            {
                WriteBytesAtomicWithBackup(targetPath, textBytes, backupPath);
            }
        }

        private static long GetFileSizeSafe(string path)
        {
            try
            {
                if (File.Exists(path)) return new FileInfo(path).Length;
            }
            catch { }
            return 0;
        }

        private static long GetDirectorySizeSafe(string dirPath)
        {
            long sum = 0;
            try
            {
                if (!Directory.Exists(dirPath)) return 0;
                string[] files = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    sum += GetFileSizeSafe(files[i]);
                }
            }
            catch { }
            return sum;
        }

        private static bool DeleteFileSafe(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool DeleteDirectorySafe(string dirPath)
        {
            try
            {
                if (!Directory.Exists(dirPath)) return false;
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

        private static int AddFileToZip(ZipArchive archive, string filePath, string entryName)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return 0;
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                ZipArchiveEntry entry = archive.CreateEntry(entryName.Replace('\\', '/'), CompressionLevel.Fastest);
                using (Stream s = entry.Open())
                {
                    fs.CopyTo(s);
                }
            }
            return 1;
        }

        private static int AddDirToZipExcludingBin(ZipArchive archive, string dirPath, string baseEntryDir)
        {
            if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath)) return 0;
            int count = 0;
            string[] files = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string f = files[i];
                string rel = f.Substring(dirPath.Length).TrimStart('\\', '/').Replace('\\', '/');
                if (HasExcludedBinSegment(rel)) continue;
                string entryName = baseEntryDir.TrimEnd('/') + "/" + rel;
                count += AddFileToZip(archive, f, entryName);
            }
            return count;
        }

        internal static bool HasExcludedBinSegment(string relPath)
        {
            if (string.IsNullOrEmpty(relPath)) return false;
            string norm = relPath.Replace('\\', '/').Trim('/');
            string[] segs = norm.Split('/');
            for (int i = 0; i < segs.Length; i++)
            {
                if (string.Equals(segs[i], "bin", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Backwards-compatibility facade forwarding to SessionLifecycleService.
    /// </summary>
    public static class LocalWorkspaceManager
    {
        public static string LastWarning
        {
            get { return SessionLifecycleService.LastWarning; }
        }

        public static List<string> LastFailedSqliteCids
        {
            get { return SessionLifecycleService.LastFailedSqliteCids; }
        }

        public static DeletionResult DeleteSessionsAndProjects(
            string geminiPath,
            List<ConversationSummaryInfo> allLocalSummaries,
            Dictionary<string, ProjectMetadata> localProjects,
            HashSet<string> targetConvIds,
            HashSet<string> targetProjectNames)
        {
            return SessionLifecycleService.DeleteSessionsAndProjects(
                geminiPath, allLocalSummaries, localProjects, targetConvIds, targetProjectNames);
        }

        public static DeletionResult DeleteSessionsAndProjects(
            string geminiPath,
            List<ConversationSummaryInfo> allLocalSummaries,
            Dictionary<string, ProjectMetadata> localProjects,
            HashSet<string> targetConvIds,
            HashSet<string> targetProjectNames,
            HashSet<string> targetProjectIds,
            HashSet<string> targetGhostProjectKeys = null)
        {
            return SessionLifecycleService.DeleteSessionsAndProjects(
                geminiPath, allLocalSummaries, localProjects,
                targetConvIds, targetProjectNames, targetProjectIds, targetGhostProjectKeys);
        }

        public static string BuildGhostProjectKey(string projectName, string folderUri)
        {
            return SessionLifecycleService.BuildGhostProjectKey(projectName, folderUri);
        }

        public static OrphanScanResult ScanOrphanedData(string geminiPath)
        {
            return SessionLifecycleService.ScanOrphanedData(geminiPath);
        }

        public static DeletionResult CleanOrphanedData(OrphanScanResult scan)
        {
            return SessionLifecycleService.CleanOrphanedData(scan);
        }

        public static bool RenameSession(string geminiPath, string convId, string newTitle)
        {
            return SessionLifecycleService.RenameSession(geminiPath, convId, newTitle);
        }

        public static bool RenameSessionsBatch(
            string geminiPath,
            Dictionary<string, string> cidToNewTitle,
            bool allowHotWriteSkipRunning,
            out RenameBatchResult result)
        {
            return SessionLifecycleService.RenameSessionsBatch(
                geminiPath, cidToNewTitle, allowHotWriteSkipRunning, out result);
        }

        public static int MoveSessionsToProject(
            string geminiPath,
            List<string> convIds,
            string targetProjectName,
            string targetProjectId,
            string targetFolderUri)
        {
            return SessionLifecycleService.MoveSessionsToProject(
                geminiPath, convIds, targetProjectName, targetProjectId, targetFolderUri);
        }

        public static int RelocateProjectPath(
            string geminiPath,
            string projectId,
            string projectName,
            string newPhysicalOrUriPath,
            List<ConversationSummaryInfo> allLocalSummaries)
        {
            return SessionLifecycleService.RelocateProjectPath(
                geminiPath, projectId, projectName, newPhysicalOrUriPath, allLocalSummaries);
        }

        public static int ExportSelectiveBackup(
            string geminiPath,
            string homePath,
            string destZipPath,
            List<ConversationSummaryInfo> selectedSessions,
            Dictionary<string, ProjectMetadata> localProjects,
            bool includeGlobalConfigs)
        {
            return SessionLifecycleService.ExportSelectiveBackup(
                geminiPath, homePath, destZipPath, selectedSessions, localProjects, includeGlobalConfigs);
        }

        public static int ExportSelectiveBackup(
            string geminiPath,
            string homePath,
            string destZipPath,
            List<ConversationSummaryInfo> selectedSessions,
            Dictionary<string, ProjectMetadata> localProjects,
            bool includeGlobalConfigs,
            List<CapabilityItem> selectedCapabilities)
        {
            return SessionLifecycleService.ExportSelectiveBackup(
                geminiPath, homePath, destZipPath, selectedSessions, localProjects, includeGlobalConfigs, selectedCapabilities);
        }

        public static int ExportSelectiveBackup(
            string geminiPath,
            string homePath,
            string destZipPath,
            List<ConversationSummaryInfo> selectedSessions,
            Dictionary<string, ProjectMetadata> localProjects,
            bool includeGlobalConfigs,
            List<CapabilityItem> selectedCapabilities,
            BackupTier tier)
        {
            return SessionLifecycleService.ExportSelectiveBackup(
                geminiPath, homePath, destZipPath, selectedSessions, localProjects, includeGlobalConfigs, selectedCapabilities, tier);
        }

        public static string ToFileUri(string pathOrUri)
        {
            return SessionLifecycleService.ToFileUri(pathOrUri);
        }

        public static void WriteBytesAtomicWithBackup(string targetPath, byte[] data, string backupPath = null)
        {
            SessionLifecycleService.WriteBytesAtomicWithBackup(targetPath, data, backupPath);
        }

        public static void WriteUtf8TextAtomicWithBackup(string targetPath, string text, string backupPath)
        {
            SessionLifecycleService.WriteUtf8TextAtomicWithBackup(targetPath, text, backupPath);
        }

        public static void WriteUtf8TextAtomicWithBackup(string targetPath, string text, bool withBom = false, string backupPath = null)
        {
            SessionLifecycleService.WriteUtf8TextAtomicWithBackup(targetPath, text, withBom, backupPath);
        }

        internal static byte[] ReadAllBytesShared(string filePath)
        {
            return SessionLifecycleService.ReadAllBytesShared(filePath);
        }

        internal static string ReadUtf8TextShared(string filePath)
        {
            return SessionLifecycleService.ReadUtf8TextShared(filePath);
        }
    }
}
