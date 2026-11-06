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
        public HashSet<string> OrphanSessionIds;
        public long TotalOrphanBytes;

        public OrphanScanResult()
        {
            OrphanDbFiles = new List<string>();
            OrphanBrainDirs = new List<string>();
            OrphanSessionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            TotalOrphanBytes = 0;
        }

        public int TotalOrphanItems
        {
            get { return OrphanDbFiles.Count + OrphanBrainDirs.Count; }
        }

        public double TotalOrphanMegabytes
        {
            get { return TotalOrphanBytes / (1024.0 * 1024.0); }
        }
    }

    public static class LocalWorkspaceManager
    {
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
            DeletionResult result = new DeletionResult();
            if (string.IsNullOrEmpty(geminiPath) || !Directory.Exists(geminiPath))
                return result;

            HashSet<string> finalConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (targetConvIds != null)
            {
                foreach (string cid in targetConvIds)
                {
                    if (!string.IsNullOrEmpty(cid)) finalConvIds.Add(cid.Trim());
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

            // 1. 将目标工程下的所有会话（含被过滤隐藏的 Subagent 会话）全部纳入待删除集合
            if (allLocalSummaries != null)
            {
                for (int i = 0; i < allLocalSummaries.Count; i++)
                {
                    ConversationSummaryInfo s = allLocalSummaries[i];
                    if (s == null || string.IsNullOrEmpty(s.ConversationId)) continue;
                    string pName = string.IsNullOrEmpty(s.ProjectName) ? "Outside of Project" : s.ProjectName;
                    if (projNames.Contains(pName))
                    {
                        finalConvIds.Add(s.ConversationId);
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
                        if (s == null || string.IsNullOrEmpty(s.ConversationId)) continue;
                        if (!finalConvIds.Contains(s.ConversationId) &&
                            !string.IsNullOrEmpty(s.ParentConversationId) &&
                            finalConvIds.Contains(s.ParentConversationId))
                        {
                            finalConvIds.Add(s.ConversationId);
                            addedAny = true;
                        }
                    }
                }
            }

            // 3. 从 agyhub_summaries_proto.pb 中移除对应 Field 1 节点（同时备份 .bak）
            string pbPath = Path.Combine(geminiPath, "antigravity", "agyhub_summaries_proto.pb");
            if (File.Exists(pbPath))
            {
                byte[] pbBytes = File.ReadAllBytes(pbPath);
                List<ProtoNode> rootNodes = ProtoCodec.Decode(pbBytes);
                List<ProtoNode> keptNodes = new List<ProtoNode>();
                bool modifiedPb = false;

                for (int i = 0; i < rootNodes.Count; i++)
                {
                    ProtoNode node = rootNodes[i];
                    bool shouldRemove = false;

                    if (node.FieldNumber == 1 && node.WireType == 2)
                    {
                        ConversationSummaryInfo parsed = SessionMetadataExtractor.ParseEntryNode(node);
                        if (parsed != null)
                        {
                            // 解析所属工程名以防 allLocalSummaries 未覆盖
                            if (localProjects != null && localProjects.Count > 0 &&
                                !string.IsNullOrEmpty(parsed.ProjectId) && localProjects.ContainsKey(parsed.ProjectId))
                            {
                                parsed.ProjectName = localProjects[parsed.ProjectId].Name;
                            }

                            if (!string.IsNullOrEmpty(parsed.ConversationId) && finalConvIds.Contains(parsed.ConversationId))
                            {
                                shouldRemove = true;
                            }
                            else if (!string.IsNullOrEmpty(parsed.ProjectName) && projNames.Contains(parsed.ProjectName))
                            {
                                shouldRemove = true;
                                if (!string.IsNullOrEmpty(parsed.ConversationId))
                                    finalConvIds.Add(parsed.ConversationId);
                            }
                            else if (!string.IsNullOrEmpty(parsed.ParentConversationId) && finalConvIds.Contains(parsed.ParentConversationId))
                            {
                                shouldRemove = true;
                                if (!string.IsNullOrEmpty(parsed.ConversationId))
                                    finalConvIds.Add(parsed.ConversationId);
                            }
                        }
                        else
                        {
                            ProtoNode f1 = node.GetChild(1);
                            if (f1 != null)
                            {
                                string rawCid = f1.GetString();
                                if (!string.IsNullOrEmpty(rawCid) && finalConvIds.Contains(rawCid))
                                {
                                    shouldRemove = true;
                                }
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
                    }
                }

                if (modifiedPb)
                {
                    BackupProtoFile(pbPath);
                    byte[] encoded = ProtoCodec.Encode(keptNodes);
                    File.WriteAllBytes(pbPath, encoded);
                }
            }

            result.DeletedSessionCount = finalConvIds.Count;

            // 4. 物理删除 conversations/<cid>.db, .db-wal, .db-shm 与 brain/<cid>/
            string convDir = Path.Combine(geminiPath, "antigravity", "conversations");
            string brainDir = Path.Combine(geminiPath, "antigravity", "brain");

            foreach (string cid in finalConvIds)
            {
                if (string.IsNullOrEmpty(cid)) continue;

                string[] dbExts = new string[] { ".db", ".db-wal", ".db-shm" };
                for (int i = 0; i < dbExts.Length; i++)
                {
                    string dbFile = Path.Combine(convDir, cid + dbExts[i]);
                    if (File.Exists(dbFile))
                    {
                        long len = GetFileSizeSafe(dbFile);
                        if (DeleteFileSafe(dbFile))
                        {
                            result.DeletedDbFilesCount++;
                            result.FreedBytes += len;
                        }
                    }
                }

                string bDir = Path.Combine(brainDir, cid);
                if (Directory.Exists(bDir))
                {
                    long dirBytes = GetDirectorySizeSafe(bDir);
                    if (DeleteDirectorySafe(bDir))
                    {
                        result.DeletedBrainDirsCount++;
                        result.FreedBytes += dirBytes;
                    }
                }
            }

            // 5. 若勾选了整工程删除，同步清理 config/projects/<id>.json
            if (projNames.Count > 0)
            {
                string projDir = Path.Combine(geminiPath, "config", "projects");
                if (Directory.Exists(projDir))
                {
                    string[] jsonFiles = Directory.GetFiles(projDir, "*.json");
                    for (int i = 0; i < jsonFiles.Length; i++)
                    {
                        try
                        {
                            string json = File.ReadAllText(jsonFiles[i], Encoding.UTF8);
                            string name = DirectStreamArchiver.ExtractJsonValue(json, "name");
                            if (!string.IsNullOrEmpty(name) && projNames.Contains(name))
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

        // ══════════════════════════════════════════════════════════════════
        // F1-B: 孤儿数据库与残余脑区扫描及一键清理 (Orphan Garbage Collector)
        // ══════════════════════════════════════════════════════════════════
        public static OrphanScanResult ScanOrphanedData(string geminiPath)
        {
            OrphanScanResult scan = new OrphanScanResult();
            if (string.IsNullOrEmpty(geminiPath) || !Directory.Exists(geminiPath))
                return scan;

            HashSet<string> activeConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string pbPath = Path.Combine(geminiPath, "antigravity", "agyhub_summaries_proto.pb");
            if (File.Exists(pbPath))
            {
                try
                {
                    byte[] pbBytes = File.ReadAllBytes(pbPath);
                    List<ProtoNode> rootNodes = ProtoCodec.Decode(pbBytes);
                    for (int i = 0; i < rootNodes.Count; i++)
                    {
                        ProtoNode node = rootNodes[i];
                        if (node.FieldNumber == 1 && node.WireType == 2)
                        {
                            ProtoNode f1 = node.GetChild(1);
                            if (f1 != null)
                            {
                                string cid = f1.GetString();
                                if (!string.IsNullOrEmpty(cid))
                                {
                                    activeConvIds.Add(cid.Trim());
                                }
                            }
                        }
                    }
                }
                catch {}
            }

            // 1. 扫描 antigravity/conversations/ 下的孤儿 .db / .db-wal / .db-shm
            string convDir = Path.Combine(geminiPath, "antigravity", "conversations");
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

                    if (!string.IsNullOrEmpty(cid) && !activeConvIds.Contains(cid))
                    {
                        scan.OrphanDbFiles.Add(f);
                        scan.OrphanSessionIds.Add(cid);
                        scan.TotalOrphanBytes += GetFileSizeSafe(f);
                    }
                }
            }

            // 2. 扫描 antigravity/brain/ 下的孤儿脑区目录
            string brainDir = Path.Combine(geminiPath, "antigravity", "brain");
            if (Directory.Exists(brainDir))
            {
                string[] dirs = Directory.GetDirectories(brainDir);
                for (int i = 0; i < dirs.Length; i++)
                {
                    string d = dirs[i];
                    string dirName = Path.GetFileName(d);
                    if (!string.IsNullOrEmpty(dirName) &&
                        SessionMetadataExtractor.IsLikelyConversationId(dirName) &&
                        !activeConvIds.Contains(dirName))
                    {
                        scan.OrphanBrainDirs.Add(d);
                        scan.OrphanSessionIds.Add(dirName);
                        scan.TotalOrphanBytes += GetDirectorySizeSafe(d);
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

            for (int i = 0; i < scan.OrphanBrainDirs.Count; i++)
            {
                string d = scan.OrphanBrainDirs[i];
                if (Directory.Exists(d))
                {
                    long len = GetDirectorySizeSafe(d);
                    if (DeleteDirectorySafe(d))
                    {
                        res.DeletedBrainDirsCount++;
                        res.FreedBytes += len;
                    }
                }
            }

            return res;
        }

        // ══════════════════════════════════════════════════════════════════
        // F2-A: 本地会话重命名 (双向固化 Field 1 与 Field 15 自定义标题)
        // ══════════════════════════════════════════════════════════════════
        public static bool RenameSession(string geminiPath, string convId, string newTitle)
        {
            if (string.IsNullOrEmpty(geminiPath) || string.IsNullOrEmpty(convId) || string.IsNullOrWhiteSpace(newTitle))
                return false;

            string trimmedTitle = newTitle.Trim();
            string pbPath = Path.Combine(geminiPath, "antigravity", "agyhub_summaries_proto.pb");
            if (!File.Exists(pbPath)) return false;

            byte[] pbBytes = File.ReadAllBytes(pbPath);
            List<ProtoNode> rootNodes = ProtoCodec.Decode(pbBytes);
            bool found = false;

            for (int i = 0; i < rootNodes.Count; i++)
            {
                ProtoNode entry = rootNodes[i];
                if (entry.FieldNumber == 1 && entry.WireType == 2)
                {
                    EnsureEntryChildrenDecoded(entry);
                    ProtoNode f1 = entry.GetChild(1);
                    if (f1 != null && string.Equals(f1.GetString(), convId, StringComparison.OrdinalIgnoreCase))
                    {
                        ApplyTitleToEntryAst(entry, trimmedTitle);
                        found = true;
                        break;
                    }
                }
            }

            if (found)
            {
                BackupProtoFile(pbPath);
                byte[] updated = ProtoCodec.Encode(rootNodes);
                File.WriteAllBytes(pbPath, updated);
            }

            return found;
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
            if (string.IsNullOrEmpty(geminiPath) || convIds == null || convIds.Count == 0 || string.IsNullOrEmpty(targetProjectName))
                return 0;

            string pbPath = Path.Combine(geminiPath, "antigravity", "agyhub_summaries_proto.pb");
            if (!File.Exists(pbPath)) return 0;

            HashSet<string> targetSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < convIds.Count; i++)
            {
                if (!string.IsNullOrEmpty(convIds[i])) targetSet.Add(convIds[i].Trim());
            }

            bool isOutside = string.Equals(targetProjectName, "Outside of Project", StringComparison.OrdinalIgnoreCase);
            string normUri = isOutside ? string.Empty : ToFileUri(targetFolderUri);
            string normProjId = isOutside
                ? "outside-of-project"
                : (string.IsNullOrEmpty(targetProjectId) ? Guid.NewGuid().ToString() : targetProjectId);

            // 若目标工程不是 Outside of Project，确保 config/projects/<id>.json 存在
            if (!isOutside)
            {
                string projDir = Path.Combine(geminiPath, "config", "projects");
                if (!Directory.Exists(projDir)) Directory.CreateDirectory(projDir);
                string projJsonPath = Path.Combine(projDir, normProjId + ".json");
                if (!File.Exists(projJsonPath))
                {
                    string jsonContent = MainForm.GenerateStandardProjectJson(normProjId, targetProjectName, normUri);
                    File.WriteAllText(projJsonPath, jsonContent, Encoding.UTF8);
                }
            }

            byte[] pbBytes = File.ReadAllBytes(pbPath);
            List<ProtoNode> rootNodes = ProtoCodec.Decode(pbBytes);
            int movedCount = 0;

            for (int i = 0; i < rootNodes.Count; i++)
            {
                ProtoNode entry = rootNodes[i];
                if (entry.FieldNumber == 1 && entry.WireType == 2)
                {
                    EnsureEntryChildrenDecoded(entry);
                    ProtoNode f1 = entry.GetChild(1);
                    if (f1 == null) continue;
                    string cid = f1.GetString();
                    if (string.IsNullOrEmpty(cid)) continue;

                    // 检查是否为目标会话或其直接子代理会话
                    bool match = targetSet.Contains(cid);
                    if (!match)
                    {
                        ConversationSummaryInfo parsed = SessionMetadataExtractor.ParseEntryNode(entry);
                        if (parsed != null && !string.IsNullOrEmpty(parsed.ParentConversationId) && targetSet.Contains(parsed.ParentConversationId))
                        {
                            match = true;
                            targetSet.Add(cid);
                        }
                    }

                    if (match)
                    {
                        ApplyProjectBindingToEntryAst(entry, targetProjectName, normProjId, normUri);
                        movedCount++;
                    }
                }
            }

            if (movedCount > 0)
            {
                BackupProtoFile(pbPath);
                byte[] updatedPb = ProtoCodec.Encode(rootNodes);
                File.WriteAllBytes(pbPath, updatedPb);

                // 同步更新每个会话的 SQLite 数据库 trajectory_metadata_blob
                string convDir = Path.Combine(geminiPath, "antigravity", "conversations");
                Dictionary<string, string> uriOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(normUri))
                {
                    uriOverrides[targetProjectName] = normUri;
                }

                foreach (string cid in targetSet)
                {
                    string dbFile = Path.Combine(convDir, cid + ".db");
                    if (File.Exists(dbFile))
                    {
                        SqliteMetadataUpdater.RemapSqliteDbMetadata(
                            dbFile, null, null, uriOverrides, targetProjectName, normProjId, null
                        );
                    }
                }
            }

            return movedCount;
        }

        // ══════════════════════════════════════════════════════════════════
        // F2-C: 工程物理路径重定向与幽灵工程修复 (同步更新 project.json、.pb 与 .db)
        // ══════════════════════════════════════════════════════════════════
        public static int RelocateProjectPath(
            string geminiPath,
            string projectId,
            string projectName,
            string newPhysicalOrUriPath,
            List<ConversationSummaryInfo> allLocalSummaries)
        {
            if (string.IsNullOrEmpty(geminiPath) || string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(newPhysicalOrUriPath))
                return 0;

            string newFolderUri = ToFileUri(newPhysicalOrUriPath);
            string resolvedProjId = projectId;

            // 若该工程尚无 projectId（如幽灵工程），尝试从已有会话提取或生成新 UUID
            if (string.IsNullOrEmpty(resolvedProjId) || string.Equals(resolvedProjId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
            {
                if (allLocalSummaries != null)
                {
                    for (int i = 0; i < allLocalSummaries.Count; i++)
                    {
                        ConversationSummaryInfo s = allLocalSummaries[i];
                        if (s != null && string.Equals(s.ProjectName, projectName, StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrEmpty(s.ProjectId) &&
                            !string.Equals(s.ProjectId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
                        {
                            resolvedProjId = s.ProjectId;
                            break;
                        }
                    }
                }
                if (string.IsNullOrEmpty(resolvedProjId) || string.Equals(resolvedProjId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
                {
                    resolvedProjId = Guid.NewGuid().ToString();
                }
            }

            // 1. 写入或更新 config/projects/<resolvedProjId>.json
            string projDir = Path.Combine(geminiPath, "config", "projects");
            if (!Directory.Exists(projDir)) Directory.CreateDirectory(projDir);
            string projJsonPath = Path.Combine(projDir, resolvedProjId + ".json");
            string jsonContent = MainForm.GenerateStandardProjectJson(resolvedProjId, projectName, newFolderUri);
            File.WriteAllText(projJsonPath, jsonContent, Encoding.UTF8);

            // 2. 收集该工程下的所有会话 ID
            List<string> projConvIds = new List<string>();
            if (allLocalSummaries != null)
            {
                for (int i = 0; i < allLocalSummaries.Count; i++)
                {
                    ConversationSummaryInfo s = allLocalSummaries[i];
                    if (s != null && !string.IsNullOrEmpty(s.ConversationId) &&
                        string.Equals(s.ProjectName, projectName, StringComparison.OrdinalIgnoreCase))
                    {
                        projConvIds.Add(s.ConversationId);
                    }
                }
            }

            // 3. 批量更新所有会话在 .pb 和 .db 中的路径与 ProjectId
            return MoveSessionsToProject(geminiPath, projConvIds, projectName, resolvedProjId, newFolderUri);
        }

        // ══════════════════════════════════════════════════════════════════
        // F3: 细粒度选择性轻量备份导出 (Selective Lightweight .agy Export)
        // ══════════════════════════════════════════════════════════════════
        public static int ExportSelectiveBackup(
            string geminiPath,
            string homePath,
            string destZipPath,
            List<ConversationSummaryInfo> selectedSessions,
            Dictionary<string, ProjectMetadata> localProjects,
            bool includeGlobalConfigs)
        {
            if (string.IsNullOrEmpty(geminiPath) || string.IsNullOrEmpty(destZipPath) || selectedSessions == null || selectedSessions.Count == 0)
                return 0;

            string destDir = Path.GetDirectoryName(destZipPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);
            if (File.Exists(destZipPath))
                File.Delete(destZipPath);

            HashSet<string> selectedConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> selectedProjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> selectedProjectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < selectedSessions.Count; i++)
            {
                ConversationSummaryInfo s = selectedSessions[i];
                if (s == null || string.IsNullOrEmpty(s.ConversationId)) continue;
                selectedConvIds.Add(s.ConversationId.Trim());
                if (!string.IsNullOrEmpty(s.ProjectName)) selectedProjectNames.Add(s.ProjectName.Trim());
                if (!string.IsNullOrEmpty(s.ProjectId)) selectedProjectIds.Add(s.ProjectId.Trim());
            }

            int entriesWritten = 0;
            string safeHome = string.IsNullOrEmpty(homePath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : homePath;

            using (FileStream fs = new FileStream(destZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                // 1. metadata.json
                ZipArchiveEntry metaEntry = archive.CreateEntry("metadata.json", CompressionLevel.Fastest);
                using (StreamWriter sw = new StreamWriter(metaEntry.Open(), Encoding.UTF8))
                {
                    string meta = "{\r\n  \"source_home\": \"" + safeHome.Replace("\\", "\\\\") + "\",\r\n" +
                                  "  \"source_user\": \"" + Environment.UserName + "\",\r\n" +
                                  "  \"backup_time\": \"" + DateTime.Now.ToString("o") + "\",\r\n" +
                                  "  \"export_mode\": \"selective\",\r\n" +
                                  "  \"version\": \"0.6\"\r\n}";
                    sw.Write(meta);
                }
                entriesWritten++;

                // 2. 仅打包被选中会话的 .db / .db-wal / .db-shm 与 brain/<cid>/**
                string convDir = Path.Combine(geminiPath, "antigravity", "conversations");
                string brainDir = Path.Combine(geminiPath, "antigravity", "brain");

                foreach (string cid in selectedConvIds)
                {
                    entriesWritten += AddFileToZip(archive, Path.Combine(convDir, cid + ".db"), "data/antigravity/conversations/" + cid + ".db");
                    entriesWritten += AddFileToZip(archive, Path.Combine(convDir, cid + ".db-wal"), "data/antigravity/conversations/" + cid + ".db-wal");
                    entriesWritten += AddFileToZip(archive, Path.Combine(convDir, cid + ".db-shm"), "data/antigravity/conversations/" + cid + ".db-shm");
                    entriesWritten += AddDirToZip(archive, Path.Combine(brainDir, cid), "data/antigravity/brain/" + cid);
                }

                // 3. 裁剪生成仅包含所选会话的 agyhub_summaries_proto.pb 分片
                List<ProtoNode> slicedNodes = new List<ProtoNode>();
                string pbPath = Path.Combine(geminiPath, "antigravity", "agyhub_summaries_proto.pb");
                if (File.Exists(pbPath))
                {
                    byte[] fullPb = File.ReadAllBytes(pbPath);
                    List<ProtoNode> allNodes = ProtoCodec.Decode(fullPb);
                    for (int i = 0; i < allNodes.Count; i++)
                    {
                        ProtoNode node = allNodes[i];
                        if (node.FieldNumber == 1 && node.WireType == 2)
                        {
                            ProtoNode f1 = node.GetChild(1);
                            if (f1 != null && selectedConvIds.Contains(f1.GetString()))
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

                // 4. 仅打包所选会话关联的工程配置 config/projects/<id>.json
                string projDir = Path.Combine(geminiPath, "config", "projects");
                if (Directory.Exists(projDir))
                {
                    string[] projFiles = Directory.GetFiles(projDir, "*.json");
                    for (int i = 0; i < projFiles.Length; i++)
                    {
                        string pf = projFiles[i];
                        string pId = Path.GetFileNameWithoutExtension(pf);
                        bool includeProj = selectedProjectIds.Contains(pId);
                        if (!includeProj)
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

                // 5. 可选附带用户全局规则、插件与 MCP 配置
                if (includeGlobalConfigs)
                {
                    entriesWritten += AddFileToZip(archive, Path.Combine(geminiPath, ".geminirules"), "data/.geminirules");
                    entriesWritten += AddFileToZip(archive, Path.Combine(geminiPath, "GEMINI.md"), "data/GEMINI.md");
                    entriesWritten += AddFileToZip(archive, Path.Combine(geminiPath, "AGENTS.md"), "data/AGENTS.md");
                    entriesWritten += AddDirToZip(archive, Path.Combine(geminiPath, "config", "plugins"), "data/config/plugins");
                    entriesWritten += AddDirToZip(archive, Path.Combine(geminiPath, "antigravity", "mcp"), "data/antigravity/mcp");
                }
            }

            return entriesWritten;
        }

        // ══════════════════════════════════════════════════════════════════
        // 内部 AST 与文件系统辅助方法
        // ══════════════════════════════════════════════════════════════════
        public static string ToFileUri(string pathOrUri)
        {
            if (string.IsNullOrEmpty(pathOrUri)) return string.Empty;
            string s = pathOrUri.Trim();
            if (s.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                return s;

            string fwd = s.Replace('\\', '/').TrimEnd('/');
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < fwd.Length; i++)
            {
                char c = fwd[i];
                if (c == ':') sb.Append("%3A");
                else if (c == ' ') sb.Append("%20");
                else sb.Append(c);
            }
            return "file:///" + sb.ToString();
        }

        private static void EnsureEntryChildrenDecoded(ProtoNode entryNode)
        {
            if (entryNode == null) return;
            if ((entryNode.Children == null || entryNode.Children.Count == 0) &&
                entryNode.RawBytes != null && entryNode.RawBytes.Length > 0)
            {
                entryNode.Children = ProtoCodec.Decode(entryNode.RawBytes);
            }
            ProtoNode meta = entryNode.GetChild(2);
            if (meta != null && (meta.Children == null || meta.Children.Count == 0) &&
                meta.RawBytes != null && meta.RawBytes.Length > 0)
            {
                meta.Children = ProtoCodec.Decode(meta.RawBytes);
            }
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
                    if (ProtoCodec.LooksLikeValidSubMessage(f15.RawBytes))
                        f15.Children = ProtoCodec.Decode(f15.RawBytes);
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
                    if ((f9.Children == null || f9.Children.Count == 0) && f9.RawBytes != null && ProtoCodec.LooksLikeValidSubMessage(f9.RawBytes))
                        f9.Children = ProtoCodec.Decode(f9.RawBytes);

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
            else if ((f17.Children == null || f17.Children.Count == 0) && f17.RawBytes != null && ProtoCodec.LooksLikeValidSubMessage(f17.RawBytes))
            {
                f17.Children = ProtoCodec.Decode(f17.RawBytes);
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

        private static void BackupProtoFile(string pbPath)
        {
            try
            {
                if (File.Exists(pbPath))
                {
                    string bak = pbPath + ".bak";
                    if (File.Exists(bak)) File.Delete(bak);
                    File.Copy(pbPath, bak);
                }
            }
            catch {}
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
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return 0;
                ZipArchiveEntry entry = archive.CreateEntry(entryName.Replace('\\', '/'), CompressionLevel.Fastest);
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Stream s = entry.Open())
                {
                    fs.CopyTo(s);
                }
                return 1;
            }
            catch
            {
                return 0;
            }
        }

        private static int AddDirToZip(ZipArchive archive, string dirPath, string baseEntryDir)
        {
            try
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
            catch
            {
                return 0;
            }
        }
    }
}
