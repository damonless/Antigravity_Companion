using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // LOCAL WORKSPACE MANAGER (F1: Delete & GC, F2: Rename/Move/Relocate, F3: Selective Export)
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

    public static class LocalWorkspaceManager
    {
        public static string LastWarning { get; private set; }
        public static List<string> LastFailedSqliteCids { get; private set; }

        static LocalWorkspaceManager()
        {
            LastFailedSqliteCids = new List<string>();
        }

        // ══════════════════════════════════════════════════════════════════
        // F1-A: 级联物理删除会话与工程 (含隐藏子代理会话、.pb 索引、.db*、brain/、project.json)
        // ══════════════════════════════════════════════════════════════════
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

            // 1. 将目标工程（按 ProjectId / GhostKey 唯一归属；仅旧调用方传入裸工程名时按名称匹配）下的所有会话纳入待删除集
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

                // 2. 级联纳入所有父会话在 finalConvIds 中的 Subagent 子会话 (不动点闭包展开)
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

            // 3. 从 agyhub_summaries_proto.pb 中按不动点闭包移除全部选中节点与逆序多级子代理（原子替换保存 .bak）
            string pbPath = Path.Combine(geminiPath, "antigravity", "agyhub_summaries_proto.pb");
            HashSet<string> removedFromPbCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> survivingProjIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> survivingProjNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(pbPath))
            {
                byte[] pbBytes = File.ReadAllBytes(pbPath);
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

                // 在真实 .pb AST 全集上再执行一轮父子不动点闭包，防止逆序出现的子代理漏删
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
                    WriteBytesAtomicWithBackup(pbPath, encoded, pbPath + ".bak");
                }
            }

            // 4. 物理删除 conversations/<cid>.db, .db-wal, .db-shm、brain/<cid>/ 与 annotations/<cid>.pbtxt (严控路径安全边界)
            string convDir = Path.Combine(geminiPath, "antigravity", "conversations");
            string brainDir = Path.Combine(geminiPath, "antigravity", "brain");
            string annotationsDir = Path.Combine(geminiPath, "antigravity", "annotations");
            HashSet<string> actuallyDeletedSessions = new HashSet<string>(removedFromPbCids, StringComparer.OrdinalIgnoreCase);

            foreach (string cid in finalConvIds)
            {
                if (!SessionMetadataExtractor.IsLikelyConversationId(cid)) continue;

                bool diskRemovedForCid = false;
                string[] dbExts = new string[] { ".db", ".db-wal", ".db-shm" };
                for (int i = 0; i < dbExts.Length; i++)
                {
                    string dbFile = Path.Combine(convDir, cid + dbExts[i]);
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

            result.DeletedSessionCount = actuallyDeletedSessions.Count;

            // 4b. 若存在 conversation_summaries.db，级联清除摘要、FTS 与父子关系记录
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

            // 5. 清理关联 config/projects/<id>.json（仅删除已无任何存活会话引用的目标工程配置）
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
                        catch {}
                    }
                }
            }

            return result;
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

        // ══════════════════════════════════════════════════════════════════
        // F1-B: 孤儿数据库与残余脑区扫描及一键清理 (Orphan Garbage Collector, Fail-Closed)
        // ══════════════════════════════════════════════════════════════════
        public static OrphanScanResult ScanOrphanedData(string geminiPath)
        {
            OrphanScanResult scan = new OrphanScanResult();
            if (string.IsNullOrEmpty(geminiPath) || !Directory.Exists(geminiPath))
                return scan;

            string convDir = Path.Combine(geminiPath, "antigravity", "conversations");
            string brainDir = Path.Combine(geminiPath, "antigravity", "brain");
            string annotationsDir = Path.Combine(geminiPath, "antigravity", "annotations");
            string pbPath = Path.Combine(geminiPath, "antigravity", "agyhub_summaries_proto.pb");
            string summaryDbPath = Path.Combine(geminiPath, "antigravity", "conversation_summaries.db");

            bool hasAnyConvFiles = Directory.Exists(convDir) && Directory.GetFiles(convDir).Length > 0;
            bool hasAnyBrainGuidDirs = false;
            if (Directory.Exists(brainDir))
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

            bool hasAnyAnnotations = false;
            if (Directory.Exists(annotationsDir))
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

            bool hasPb = File.Exists(pbPath);
            bool hasNonEmptyDb = File.Exists(summaryDbPath) && GetFileSizeSafe(summaryDbPath) > 0;

            if (!hasPb && !hasNonEmptyDb)
            {
                if (hasAnyConvFiles || hasAnyBrainGuidDirs || hasAnyAnnotations)
                {
                    throw new InvalidOperationException("未找到索引文件 agyhub_summaries_proto.pb，但磁盘存在会话数据；为防止误删全库，已中止孤儿扫描。");
                }
                return scan;
            }

            if (hasPb)
            {
                FileInfo pbFi = new FileInfo(pbPath);
                if (pbFi.Length == 0)
                {
                    if (hasAnyConvFiles || hasAnyBrainGuidDirs || hasAnyAnnotations)
                    {
                        throw new InvalidOperationException("索引文件 agyhub_summaries_proto.pb 长度为 0 字节，已中止孤儿扫描以防误删全部会话。");
                    }
                    if (!hasNonEmptyDb) return scan;
                }
            }

            HashSet<string> activeConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (hasPb && new FileInfo(pbPath).Length > 0)
            {
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
                    throw new InvalidOperationException("无法安全读取或解码 agyhub_summaries_proto.pb（可能正被 Antigravity 占用或已损坏），已中止孤儿扫描: " + ex.Message, ex);
                }
            }

            // 联合 conversation_summaries.db 活跃会话 ID（非空库损坏或不可读时自动 fail-closed 抛异常）
            HashSet<string> dbActiveIds = SummarySqliteBridge.ReadAllConversationIds(summaryDbPath);
            foreach (string dbCid in dbActiveIds)
            {
                if (SessionMetadataExtractor.IsLikelyConversationId(dbCid))
                {
                    activeConvIds.Add(dbCid.Trim());
                }
            }

            if (activeConvIds.Count == 0 && (hasAnyConvFiles || hasAnyBrainGuidDirs || hasAnyAnnotations))
            {
                throw new InvalidOperationException("索引文件 agyhub_summaries_proto.pb 与 conversation_summaries.db 未包含任何有效会话 ID，但磁盘存在会话文件；已中止孤儿扫描以防全盘误删。");
            }

            // 1. 扫描 antigravity/conversations/ 下的孤儿 .db / .db-wal / .db-shm
            if (Directory.Exists(convDir))
            {
                string[] files = Directory.GetFiles(convDir);
                for (int i = 0; i < files.Length; i++)
                {
                    string f = files[i];
                    string fn = Path.GetFileName(f);
                    string cid = null;

                    if (fn.EndsWith(".db-wal", StringComparison.OrdinalIgnoreCase))
                        cid = fn.Substring(0, fn.Length - 7);
                    else if (fn.EndsWith(".db-shm", StringComparison.OrdinalIgnoreCase))
                        cid = fn.Substring(0, fn.Length - 7);
                    else if (fn.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                        cid = fn.Substring(0, fn.Length - 3);

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

            // 2. 扫描 antigravity/brain/ 下的孤儿脑区目录（严格仅限标准 UUID 会话目录，保护 tempmediaStorage 等系统文件夹）
            if (Directory.Exists(brainDir))
            {
                string[] dirs = Directory.GetDirectories(brainDir);
                for (int i = 0; i < dirs.Length; i++)
                {
                    string d = dirs[i];
                    string dirName = Path.GetFileName(d);
                    Guid dirGuid;
                    if (!string.IsNullOrEmpty(dirName) &&
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

            // 3. 扫描 antigravity/annotations/*.pbtxt 下的孤儿标注文件（严格仅限标准 UUID）
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

            return scan;
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
                    if (Directory.Exists(d) && Guid.TryParse(dirName, out g) && SessionMetadataExtractor.IsLikelyConversationId(dirName))
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

        public static bool RenameSessionsBatch(
            string geminiPath,
            Dictionary<string, string> cidToNewTitle,
            bool allowHotWriteSkipRunning,
            out RenameBatchResult result)
        {
            result = new RenameBatchResult();
            LastWarning = null;

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

            // Layer 1: 写入或无损更新 antigravity/annotations/<cid>.pbtxt
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

            // Layer 2: 事务级更新 conversation_summaries.db
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

            // Layer 3: 更新 agyhub_summaries_proto.pb (热写入模式遇 IOException 保留 L1/L2 并记入警告)
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
                        WriteBytesAtomicWithBackup(pbPath, updated, pbPath + ".bak");
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
        // F2-B: 会话跨工程迁移 / 归属重绑定 (同步更新 .pb Field 9/17 与 SQLite .db)
        // ══════════════════════════════════════════════════════════════════
        public static int MoveSessionsToProject(
            string geminiPath,
            List<string> convIds,
            string targetProjectName,
            string targetProjectId,
            string targetFolderUri)
        {
            LastWarning = null;
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

            // 若目标工程不是 Outside of Project，仅在不存在时安全创建 config/projects/<id>.json（严禁覆写已有配置）
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

                // 解析所有条目并在不动点闭包循环下纳入全部逆序/多级子代理
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
                    WriteBytesAtomicWithBackup(pbPath, updatedPb, pbPath + ".bak");
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
                            LastFailedSqliteCids.Add(cid + (string.IsNullOrEmpty(SqliteMetadataUpdater.LastError) ? "" : " (" + SqliteMetadataUpdater.LastError + ")"));
                        }
                    }
                }

                if (LastFailedSqliteCids.Count > 0)
                {
                    string perConvWarn = "已更新 .pb 索引，但有 " + LastFailedSqliteCids.Count + " 个 SQLite 会话库未能同步写入: " + string.Join("; ", LastFailedSqliteCids.ToArray());
                    LastWarning = string.IsNullOrEmpty(LastWarning) ? perConvWarn : (perConvWarn + "; " + LastWarning);
                }
            }

            return movedCount > 0 ? movedCount : summaryDbUpdated;
        }

        // ══════════════════════════════════════════════════════════════════
        // F2-C: 工程物理路径重定向与幽灵工程修复 (保留原 project.json 安全策略，按 ProjectId 隔离)
        // ══════════════════════════════════════════════════════════════════
        public static int RelocateProjectPath(
            string geminiPath,
            string projectId,
            string projectName,
            string newPhysicalOrUriPath,
            List<ConversationSummaryInfo> allLocalSummaries)
        {
            LastWarning = null;
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

            // 1. 写入或原位补丁更新 config/projects/<resolvedProjId>.json（保留 settings / 多工作区等已有配置）
            string projDir = Path.Combine(geminiPath, "config", "projects");
            if (!Directory.Exists(projDir)) Directory.CreateDirectory(projDir);
            string projJsonPath = Path.Combine(projDir, resolvedProjId + ".json");

            if (File.Exists(projJsonPath))
            {
                string existingJson = File.ReadAllText(projJsonPath, Encoding.UTF8);
                string existingUri = DirectStreamArchiver.ExtractJsonValue(existingJson, "folderUri");
                if (string.IsNullOrEmpty(oldNormFolderUri) && !string.IsNullOrEmpty(existingUri))
                    oldNormFolderUri = DirectStreamArchiver.NormalizeUri(existingUri);

                string patchedJson = PatchExistingProjectJson(existingJson, projectName, newFolderUri);
                WriteUtf8TextAtomicWithBackup(projJsonPath, patchedJson, false, projJsonPath + ".bak");
            }
            else
            {
                string jsonContent = WorkspaceService.GenerateStandardProjectJson(resolvedProjId, projectName, newFolderUri);
                WriteUtf8TextAtomicWithBackup(projJsonPath, jsonContent, false, null);
            }

            // 2. 严格按 ProjectId（无 ID 幽灵工程按旧 FolderUri）收集归属本工程的会话 ID，绝不劫持同名异路径工程
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

            // 3. 批量更新归属会话在 .pb、summary.sqlite 和 per-session .db 中的路径与 ProjectId
            return MoveSessionsToProject(geminiPath, projConvIds, projectName, resolvedProjId, newFolderUri);
        }

        private static string PatchExistingProjectJson(string existingJson, string projectName, string newFolderUri)
        {
            if (string.IsNullOrWhiteSpace(existingJson))
                return WorkspaceService.GenerateStandardProjectJson(Guid.NewGuid().ToString(), projectName, newFolderUri);

            try
            {
                string nowIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                string result = existingJson;
                result = JsonKeyManipulator.UpsertObjectEntry(result, null, "folderUri", "\"" + JsonKeyManipulator.EscapeJsonString(newFolderUri) + "\"");
                result = JsonKeyManipulator.UpsertObjectEntry(result, null, "updatedAt", "\"" + nowIso + "\"");
                return result;
            }
            catch
            {
                return existingJson;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // F3: 细粒度选择性轻量备份导出 (Selective Lightweight .agy Export)
        // 支持仅导出会话、仅导出所选能力、或会话+单品能力混合打包
        // ══════════════════════════════════════════════════════════════════
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
            bool hasSessions = selectedSessions != null && selectedSessions.Count > 0;
            bool hasExplicitCaps = selectedCapabilities != null && selectedCapabilities.Count > 0;
            if (string.IsNullOrEmpty(geminiPath) || string.IsNullOrEmpty(destZipPath) || (!hasSessions && !includeGlobalConfigs && !hasExplicitCaps))
                return 0;

            string destDir = Path.GetDirectoryName(destZipPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            HashSet<string> selectedConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> selectedProjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> selectedProjectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (hasSessions)
            {
                for (int i = 0; i < selectedSessions.Count; i++)
                {
                    ConversationSummaryInfo s = selectedSessions[i];
                    if (s == null || !SessionMetadataExtractor.IsLikelyConversationId(s.ConversationId)) continue;
                    selectedConvIds.Add(s.ConversationId.Trim());
                    if (!string.IsNullOrEmpty(s.ProjectName)) selectedProjectNames.Add(s.ProjectName.Trim());
                    if (!string.IsNullOrEmpty(s.ProjectId)) selectedProjectIds.Add(s.ProjectId.Trim());
                }
            }

            int entriesWritten = 0;
            string safeHome = string.IsNullOrEmpty(homePath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : homePath;

            string tempZipPath = destZipPath + ".tmp." + Guid.NewGuid().ToString("N").Substring(0, 8);
            try
            {
                using (FileStream fs = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    // 1. metadata.json（使用完整 JSON 转义）
                    ZipArchiveEntry metaEntry = archive.CreateEntry("metadata.json", CompressionLevel.Fastest);
                    using (StreamWriter sw = new StreamWriter(metaEntry.Open(), new UTF8Encoding(false)))
                    {
                        string meta = "{\r\n  \"source_home\": \"" + JsonKeyManipulator.EscapeJsonString(safeHome) + "\",\r\n" +
                                      "  \"source_user\": \"" + JsonKeyManipulator.EscapeJsonString(Environment.UserName) + "\",\r\n" +
                                      "  \"backup_time\": \"" + DateTime.Now.ToString("o") + "\",\r\n" +
                                      "  \"export_mode\": \"selective\",\r\n" +
                                      "  \"version\": \"0.7\"\r\n}";
                        sw.Write(meta);
                    }
                    entriesWritten++;

                    if (hasSessions)
                    {
                        string pbPath = Path.Combine(geminiPath, "antigravity", "agyhub_summaries_proto.pb");
                        List<ProtoNode> allNodes = null;
                        if (File.Exists(pbPath))
                        {
                            byte[] fullPb = File.ReadAllBytes(pbPath);
                            allNodes = ProtoCodec.Decode(fullPb);

                            // 按 .pb 中的父子引用闭包自动纳入全部关联子代理会话（包括 UI 被过滤隐藏的）
                            List<ConversationSummaryInfo> allParsed = new List<ConversationSummaryInfo>(allNodes.Count);
                            for (int i = 0; i < allNodes.Count; i++)
                            {
                                ConversationSummaryInfo info = (allNodes[i].FieldNumber == 1 && allNodes[i].WireType == 2)
                                    ? SessionMetadataExtractor.ParseEntryNode(allNodes[i])
                                    : null;
                                allParsed.Add(info);
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
                                        addedSub = true;
                                    }
                                }
                            }
                        }

                        // 2. 打包所选会话及其全部子代理的 .db / .db-wal、brain/<cid>/** 与 annotations/<cid>.pbtxt（绝不打包跨机无效的 .db-shm）
                        string convDir = Path.Combine(geminiPath, "antigravity", "conversations");
                        string brainDir = Path.Combine(geminiPath, "antigravity", "brain");
                        string annotationsDir = Path.Combine(geminiPath, "antigravity", "annotations");

                        foreach (string cid in selectedConvIds)
                        {
                            if (!SessionMetadataExtractor.IsLikelyConversationId(cid)) continue;
                            entriesWritten += AddFileToZip(archive, Path.Combine(convDir, cid + ".db"), "data/antigravity/conversations/" + cid + ".db");
                            entriesWritten += AddFileToZip(archive, Path.Combine(convDir, cid + ".db-wal"), "data/antigravity/conversations/" + cid + ".db-wal");
                            entriesWritten += AddDirToZip(archive, Path.Combine(brainDir, cid), "data/antigravity/brain/" + cid);
                            string annotPath = Path.Combine(annotationsDir, cid + ".pbtxt");
                            if (IsPathContainedUnder(annotationsDir, annotPath))
                            {
                                entriesWritten += AddFileToZip(archive, annotPath, "data/antigravity/annotations/" + cid + ".pbtxt");
                            }
                        }

                        // 3. 裁剪生成包含所选会话与子代理的 agyhub_summaries_proto.pb 分片
                        List<ProtoNode> slicedNodes = new List<ProtoNode>();
                        if (allNodes != null)
                        {
                            for (int i = 0; i < allNodes.Count; i++)
                            {
                                ProtoNode node = allNodes[i];
                                if (node.FieldNumber == 1 && node.WireType == 2)
                                {
                                    ProtoCodec.DecodeSummaryEntryTree(node);
                                    ProtoNode f1 = node.GetChild(1);
                                    string rawCid = f1 != null ? (f1.GetString() ?? string.Empty).Trim() : null;
                                    if (!string.IsNullOrEmpty(rawCid) && selectedConvIds.Contains(rawCid))
                                    {
                                        slicedNodes.Add(node);
                                    }
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < selectedSessions.Count; i++)
                            {
                                if (selectedSessions[i].RawProtoEntry != null)
                                    slicedNodes.Add(selectedSessions[i].RawProtoEntry);
                            }
                        }

                        byte[] slicedPbBytes = ProtoCodec.Encode(slicedNodes);
                        ZipArchiveEntry pbEntry = archive.CreateEntry("data/antigravity/agyhub_summaries_proto.pb", CompressionLevel.Fastest);
                        using (Stream s = pbEntry.Open())
                        {
                            s.Write(slicedPbBytes, 0, slicedPbBytes.Length);
                        }
                        entriesWritten++;

                        // 4. 打包所选会话关联的工程配置 config/projects/<id>.json
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
                                        string json = File.ReadAllText(pf, Encoding.UTF8);
                                        string pName = DirectStreamArchiver.ExtractJsonValue(json, "name");
                                        if (!string.IsNullOrEmpty(pName) && selectedProjectNames.Contains(pName))
                                            includeProj = true;
                                    }
                                    catch {}
                                }

                                if (includeProj)
                                {
                                    entriesWritten += AddFileToZip(archive, pf, "data/config/projects/" + Path.GetFileName(pf));
                                }
                            }
                        }
                    }

                    // 5. 打包选中的单品能力配置（或全量全局能力配置）
                    List<CapabilityItem> capsToPack = selectedCapabilities;
                    if ((capsToPack == null || capsToPack.Count == 0) && includeGlobalConfigs)
                    {
                        capsToPack = CapabilityRepositoryManager.ScanLocalCapabilities(geminiPath, safeHome);
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
                catch {}
                throw;
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
                        string safePluginFolder = CapabilityRepositoryManager.EncodeSafeCapabilityFolderName(item.Id);
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
                            string expanded = CapabilityPathNormalizer.ExpandHomePlaceholder(item.RawJsonFragment, sourceHomePath, true);
                            slicedConfigJson = JsonKeyManipulator.UpsertObjectEntry(slicedConfigJson, "plugins", item.Id, expanded);
                            hasPluginReg = true;
                        }
                        break;
                    }
                    case CapabilityKind.Skill:
                    {
                        string safeSkillFolder = CapabilityRepositoryManager.EncodeSafeCapabilityFolderName(item.Id);
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
                            string expanded = CapabilityPathNormalizer.ExpandHomePlaceholder(item.RawJsonFragment, sourceHomePath, true);
                            slicedMcpJson = JsonKeyManipulator.UpsertObjectEntry(slicedMcpJson, "mcpServers", item.Id, expanded);
                            hasMcp = true;
                        }
                        foreach (KeyValuePair<string, byte[]> kvp in item.FilesMap)
                        {
                            string rel = kvp.Key.Replace('\\', '/');
                            if (rel.StartsWith("schemas/", StringComparison.OrdinalIgnoreCase))
                                rel = rel.Substring("schemas/".Length);
                            string entryPath = "data/antigravity/mcp/" + CapabilityRepositoryManager.EncodeSafeCapabilityFolderName(item.Id) + "/" + rel;
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
                            string expanded = CapabilityPathNormalizer.ExpandHomePlaceholder(item.RawJsonFragment, sourceHomePath, true);
                            slicedHooksJson = JsonKeyManipulator.UpsertObjectEntry(slicedHooksJson, null, item.Id, expanded);
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

            // 1. 更新 Field 1 (Primary Title)
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

            // 2. 更新或创建 Field 15 (Custom Title Metadata -> Field 1)
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

            // 1. 更新 Field 9 (WorkspaceInfo -> Field 1 = FolderUri)
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

            // 2. 更新 Field 17 (CascadeInfo -> Field 1 = ProjectName, Field 7 = FolderUri, Field 18 = ProjectId)
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

            // Field 17 -> Child 1 (ProjectName)
            SetOrAddStringChild(f17, 1, targetProjectName);

            // Field 17 -> Child 7 (FolderUri)
            if (!string.IsNullOrEmpty(targetFolderUri))
            {
                SetOrAddStringChild(f17, 7, targetFolderUri);
            }
            else
            {
                ProtoNode c7 = f17.GetChild(7);
                if (c7 != null) f17.Children.Remove(c7);
            }

            // Field 17 -> Child 18 (ProjectId)
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
                    try { File.SetAttributes(targetPath, FileAttributes.Normal); } catch {}
                    if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
                    {
                        try { File.SetAttributes(backupPath, FileAttributes.Normal); } catch {}
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
                catch {}
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
            catch {}
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
            catch {}
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
                    try { File.SetAttributes(files[i], FileAttributes.Normal); } catch {}
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
            // 先确保源文件可读取，严禁先 CreateEntry 导致锁异常留下 0 字节损坏条目
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

        private static int AddDirToZip(ZipArchive archive, string dirPath, string baseEntryDir)
        {
            if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath)) return 0;
            int count = 0;
            string[] files = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string f = files[i];
                string rel = f.Substring(dirPath.Length).TrimStart('\\', '/');
                string entryName = baseEntryDir.TrimEnd('/') + "/" + rel.Replace('\\', '/');
                count += AddFileToZip(archive, f, entryName);
            }
            return count;
        }
    }
}
