using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // STORAGE SLIM SERVICE (Decision 5C Liposuction, Mode 5A Cold Archive Prune,
    // Pre-Flight Sandbox Compatibility, Complete Orphan GC, Legacy CLI Prune)
    // Strictly C# 5.0 (.NET Framework 4.x)
    // ══════════════════════════════════════════════════════════════════════

    public enum StorageSlimMode
    {
        Liposuction,
        ColdArchivePrune,
        OrphanGcOnly
    }

    public class ProjectStorageBreakdown
    {
        public string ProjectId;
        public string ProjectName;
        public long RootDbsBytes;
        public long SubagentDbsBytes;
        public long BrainDirsBytes;
        public long AnnotationsBytes;
        public long CliDbsBytes;
        public long TotalBytes;
        public int SessionCount;
        public int SubagentCount;

        public ProjectStorageBreakdown()
        {
            ProjectId = string.Empty;
            ProjectName = string.Empty;
        }
    }

    public class DbOffenderInfo
    {
        public string ConversationId;
        public string Title;
        public string ProjectId;
        public string ProjectName;
        public bool IsSubagent;
        public bool IsCli;
        public string Status;
        public bool NotFullyIdle;
        public double AgeDays;
        public long DbBytes;
        public long WalBytes;
        public long TotalBytes;
        public string DbPath;
        public bool HasTableBreakdown;
        public bool HasExecutorMetadataTable;
        public long ExecutorMetadataRows;
        public long ExecutorMetadataBytes;
        public long StepsRows;
        public long StepsBytes;
        public bool EligibleForSlim;
        public string IneligibleReason;

        public DbOffenderInfo()
        {
            ConversationId = string.Empty;
            Title = string.Empty;
            ProjectId = string.Empty;
            ProjectName = string.Empty;
            Status = string.Empty;
            DbPath = string.Empty;
            IneligibleReason = string.Empty;
        }
    }

    public class OrphanGcInventory
    {
        public List<string> OrphanDbFiles;
        public List<string> OrphanDbBakFiles;
        public List<string> OrphanDbJournalFiles;
        public List<string> OrphanBrainDirs;
        public List<string> OrphanAnnotationFiles;
        public long TotalOrphanBytes;

        public OrphanGcInventory()
        {
            OrphanDbFiles = new List<string>();
            OrphanDbBakFiles = new List<string>();
            OrphanDbJournalFiles = new List<string>();
            OrphanBrainDirs = new List<string>();
            OrphanAnnotationFiles = new List<string>();
        }

        public int TotalOrphanItems
        {
            get
            {
                return OrphanDbFiles.Count +
                       OrphanDbBakFiles.Count +
                       OrphanDbJournalFiles.Count +
                       OrphanBrainDirs.Count +
                       OrphanAnnotationFiles.Count;
            }
        }
    }

    public class LegacyCliArchiveSummary
    {
        public bool Exists;
        public string DbPath;
        public long TotalBytes;
        public int EntryCount;
        public List<LegacyCliArchiveEntry> Entries;

        public LegacyCliArchiveSummary()
        {
            DbPath = string.Empty;
            Entries = new List<LegacyCliArchiveEntry>();
        }
    }

    public class StorageAnalysisReport
    {
        public string HomePath;
        public string GeminiPath;
        public long TotalBytes;
        public long RootDbsBytes;
        public long SubagentDbsBytes;
        public long BrainDirsBytes;
        public long AnnotationsBytes;
        public long CliDbsBytes;
        public long OrphansBytes;
        public long LegacyCliSummaryDbBytes;
        public List<ProjectStorageBreakdown> Projects;
        public List<DbOffenderInfo> TopOffenders;
        public OrphanGcInventory Orphans;
        public LegacyCliArchiveSummary LegacyCliArchive;
        public List<string> Warnings;

        public StorageAnalysisReport()
        {
            HomePath = string.Empty;
            GeminiPath = string.Empty;
            Projects = new List<ProjectStorageBreakdown>();
            TopOffenders = new List<DbOffenderInfo>();
            Orphans = new OrphanGcInventory();
            LegacyCliArchive = new LegacyCliArchiveSummary();
            Warnings = new List<string>();
        }
    }

    public class StorageSlimRequest
    {
        public StorageSlimMode Mode;
        public List<string> TargetCids;
        public double MinAgeDays;
        public double MinSizeMb;
        public bool RunPreFlight;
        public bool CleanOrphans;
        public bool PruneLegacyCliArchive;

        public StorageSlimRequest()
        {
            Mode = StorageSlimMode.Liposuction;
            TargetCids = new List<string>();
            MinAgeDays = 7.0;
            MinSizeMb = 5.0;
            RunPreFlight = true;
            CleanOrphans = false;
            PruneLegacyCliArchive = false;
        }
    }

    public class StorageSlimItemResult
    {
        public string ConversationId;
        public string DbPath;
        public long BytesBefore;
        public long BytesAfter;
        public long SavedBytes;
        public bool Succeeded;
        public string SkippedReason;
        public string IntegrityCheck;

        public StorageSlimItemResult()
        {
            ConversationId = string.Empty;
            DbPath = string.Empty;
            SkippedReason = string.Empty;
            IntegrityCheck = string.Empty;
        }
    }

    public class StorageSlimResult
    {
        public bool Ok;
        public StorageSlimMode Mode;
        public string ColdArchivePath;
        public int ProcessedCount;
        public int SkippedCount;
        public long BytesBefore;
        public long BytesAfter;
        public long FreedBytes;
        public bool PreFlightPassed;
        public string PreFlightReport;
        public int DeletedOrphanCount;
        public long FreedOrphanBytes;
        public bool LegacyCliPruned;
        public string LegacyCliArchivePath;
        public List<StorageSlimItemResult> Items;
        public List<string> Warnings;
        public string ErrorMessage;

        public double FreedMegabytes
        {
            get { return FreedBytes / (1024.0 * 1024.0); }
        }

        public StorageSlimResult()
        {
            ColdArchivePath = string.Empty;
            PreFlightReport = string.Empty;
            LegacyCliArchivePath = string.Empty;
            Items = new List<StorageSlimItemResult>();
            Warnings = new List<string>();
            ErrorMessage = string.Empty;
        }
    }

    public static class StorageSlimService
    {
        public const long DefaultTableBreakdownThresholdBytes = 2L * 1024L * 1024L;
        public const int DefaultTopOffendersLimit = 20;
        public const double DefaultMinAgeDays = 7.0;
        public const double DefaultMinSizeMb = 5.0;

        private const int SQLITE_OK = 0;
        private const int SQLITE_ROW = 100;
        private const int SQLITE_DONE = 101;
        private const int SQLITE_NULL = 5;
        private const int SQLITE_OPEN_READONLY = 0x00000001;
        private const int BusyTimeoutMs = 5000;

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, IntPtr zVfs);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close(IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_busy_timeout", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_busy_timeout(IntPtr db, int ms);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_prepare_v2(IntPtr db, byte[] zSql, int nByte, out IntPtr ppStmt, IntPtr pzTail);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_step(IntPtr pStmt);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_type", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_type(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_text(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_bytes", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_bytes(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_int64", CallingConvention = CallingConvention.Cdecl)]
        private static extern long sqlite3_column_int64(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr pStmt);

        // ══════════════════════════════════════════════════════════════════
        // 1. STORAGE ANALYSIS & TABLE BREAKDOWN
        // ══════════════════════════════════════════════════════════════════

        public static StorageAnalysisReport AnalyzeStorage(string homePath, string geminiPath)
        {
            return AnalyzeStorage(homePath, geminiPath, DefaultTopOffendersLimit, DefaultMinAgeDays, DefaultMinSizeMb);
        }

        public static StorageAnalysisReport AnalyzeStorage(
            string homePath,
            string geminiPath,
            int topN,
            double minAgeDays,
            double minSizeMb)
        {
            StorageAnalysisReport report = new StorageAnalysisReport();
            report.HomePath = !string.IsNullOrWhiteSpace(homePath) ? homePath.Trim() : WorkspaceService.ResolveDefaultHomePath();
            report.GeminiPath = !string.IsNullOrWhiteSpace(geminiPath) ? geminiPath.Trim() : Path.Combine(report.HomePath, ".gemini");

            if (string.IsNullOrEmpty(report.GeminiPath) || !Directory.Exists(report.GeminiPath))
            {
                report.Warnings.Add("未找到有效的 .gemini 目录: " + report.GeminiPath);
                return report;
            }

            if (topN <= 0) topN = DefaultTopOffendersLimit;

            Dictionary<string, ProjectMetadata> localProjects;
            List<string> loadWarnings;
            List<ConversationSummaryInfo> summaries = WorkspaceService.LoadLocalThreeLayerSummaries(
                report.GeminiPath, out localProjects, out loadWarnings);
            if (loadWarnings != null && loadWarnings.Count > 0)
            {
                report.Warnings.AddRange(loadWarnings);
            }

            Dictionary<string, ConversationSummaryInfo> byCid = new Dictionary<string, ConversationSummaryInfo>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < summaries.Count; i++)
            {
                ConversationSummaryInfo s = summaries[i];
                if (s == null || !SessionMetadataExtractor.IsLikelyConversationId(s.ConversationId)) continue;
                string cid = s.ConversationId.Trim();
                if (!byCid.ContainsKey(cid))
                {
                    byCid[cid] = s;
                }
            }

            Dictionary<string, ProjectStorageBreakdown> projMap = new Dictionary<string, ProjectStorageBreakdown>(StringComparer.OrdinalIgnoreCase);
            List<DbOffenderInfo> allDbItems = new List<DbOffenderInfo>();
            HashSet<string> countedDbPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> countedBrainDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> countedAnnotPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string ideRoot = Path.Combine(report.GeminiPath, "antigravity");
            string cliRoot = Path.Combine(report.GeminiPath, "antigravity-cli");

            foreach (KeyValuePair<string, ConversationSummaryInfo> kvp in byCid)
            {
                string cid = kvp.Key;
                ConversationSummaryInfo s = kvp.Value;
                if (s is LegacyCliArchiveEntry) continue;

                string pKey;
                string pId;
                string pName;
                ResolveProjectBucket(s, localProjects, out pKey, out pId, out pName);

                ProjectStorageBreakdown pb;
                if (!projMap.TryGetValue(pKey, out pb))
                {
                    pb = new ProjectStorageBreakdown();
                    pb.ProjectId = pId;
                    pb.ProjectName = pName;
                    projMap[pKey] = pb;
                }
                pb.SessionCount++;
                if (s.IsSubagent) pb.SubagentCount++;

                string primaryRoot = s.IsCliSession ? cliRoot : ideRoot;
                string secondaryRoot = s.IsCliSession ? ideRoot : cliRoot;

                string dbPath = LocateSessionFile(primaryRoot, secondaryRoot, "conversations", cid + ".db");
                if (!string.IsNullOrEmpty(dbPath) && countedDbPaths.Add(dbPath))
                {
                    long dbBytes = GetFileSizeSafe(dbPath);
                    long walBytes = GetFileSizeSafe(dbPath + "-wal");
                    long shmBytes = GetFileSizeSafe(dbPath + "-shm");
                    long sidecarBytes = walBytes + shmBytes;
                    long totalItemBytes = dbBytes + sidecarBytes;

                    if (s.IsCliSession)
                    {
                        pb.CliDbsBytes += totalItemBytes;
                        report.CliDbsBytes += totalItemBytes;
                    }
                    else if (s.IsSubagent)
                    {
                        pb.SubagentDbsBytes += totalItemBytes;
                        report.SubagentDbsBytes += totalItemBytes;
                    }
                    else
                    {
                        pb.RootDbsBytes += totalItemBytes;
                        report.RootDbsBytes += totalItemBytes;
                    }

                    DbOffenderInfo off = new DbOffenderInfo();
                    off.ConversationId = cid;
                    off.Title = !string.IsNullOrEmpty(s.DisplayTitle) ? s.DisplayTitle : cid;
                    off.ProjectId = pId;
                    off.ProjectName = pName;
                    off.IsSubagent = s.IsSubagent;
                    off.IsCli = s.IsCliSession;
                    off.Status = s.RunStatus ?? string.Empty;
                    off.NotFullyIdle = s.NotFullyIdle;
                    off.AgeDays = ComputeAgeDays(s, dbPath);
                    off.DbBytes = dbBytes;
                    off.WalBytes = walBytes;
                    off.TotalBytes = totalItemBytes;
                    off.DbPath = dbPath;

                    string inelig;
                    off.EligibleForSlim = EvaluateSlimEligibility(s, totalItemBytes, off.AgeDays, minAgeDays, minSizeMb, out inelig);
                    off.IneligibleReason = inelig;

                    allDbItems.Add(off);
                }

                string brainDir = LocateSessionDir(primaryRoot, secondaryRoot, "brain", cid);
                if (!string.IsNullOrEmpty(brainDir) && countedBrainDirs.Add(brainDir))
                {
                    long bBytes = GetDirectorySizeSafe(brainDir);
                    pb.BrainDirsBytes += bBytes;
                    report.BrainDirsBytes += bBytes;
                }

                string annotPath = LocateSessionFile(primaryRoot, secondaryRoot, "annotations", cid + ".pbtxt");
                if (!string.IsNullOrEmpty(annotPath) && countedAnnotPaths.Add(annotPath))
                {
                    long aBytes = GetFileSizeSafe(annotPath);
                    pb.AnnotationsBytes += aBytes;
                    report.AnnotationsBytes += aBytes;
                }
            }

            // Complete Orphan GC Inventory (computed before unindexed CLI scan to prevent double-counting)
            report.Orphans = ScanCompleteOrphans(report.GeminiPath, summaries, report.Warnings);
            report.OrphansBytes = report.Orphans.TotalOrphanBytes;
            HashSet<string> orphanFileSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < report.Orphans.OrphanDbFiles.Count; i++) orphanFileSet.Add(report.Orphans.OrphanDbFiles[i]);
            for (int i = 0; i < report.Orphans.OrphanDbBakFiles.Count; i++) orphanFileSet.Add(report.Orphans.OrphanDbBakFiles[i]);
            for (int i = 0; i < report.Orphans.OrphanDbJournalFiles.Count; i++) orphanFileSet.Add(report.Orphans.OrphanDbJournalFiles[i]);

            // Also scan any unindexed non-orphan valid CLI .db files in antigravity-cli/conversations/
            string cliConvDir = Path.Combine(cliRoot, "conversations");
            if (Directory.Exists(cliConvDir))
            {
                string[] cliFiles = Directory.GetFiles(cliConvDir, "*.db");
                for (int i = 0; i < cliFiles.Length; i++)
                {
                    string f = cliFiles[i];
                    if (countedDbPaths.Contains(f) || orphanFileSet.Contains(f)) continue;
                    string stem = Path.GetFileNameWithoutExtension(f);
                    if (!SessionMetadataExtractor.IsLikelyConversationId(stem) ||
                        string.Equals(stem, "bin", StringComparison.OrdinalIgnoreCase))
                        continue;

                    long dbBytes = GetFileSizeSafe(f);
                    long walBytes = GetFileSizeSafe(f + "-wal");
                    long shmBytes = GetFileSizeSafe(f + "-shm");
                    long totalItemBytes = dbBytes + walBytes + shmBytes;

                    string cliKey = "CLI:default-cli-project";
                    ProjectStorageBreakdown cliPb;
                    if (!projMap.TryGetValue(cliKey, out cliPb))
                    {
                        cliPb = new ProjectStorageBreakdown();
                        cliPb.ProjectId = "default-cli-project";
                        cliPb.ProjectName = "CLI 命令行会话";
                        projMap[cliKey] = cliPb;
                    }
                    cliPb.SessionCount++;
                    cliPb.CliDbsBytes += totalItemBytes;
                    report.CliDbsBytes += totalItemBytes;
                    countedDbPaths.Add(f);

                    DbOffenderInfo off = new DbOffenderInfo();
                    off.ConversationId = stem;
                    off.Title = stem;
                    off.ProjectId = "default-cli-project";
                    off.ProjectName = "CLI 命令行会话";
                    off.IsCli = true;
                    off.DbBytes = dbBytes;
                    off.WalBytes = walBytes;
                    off.TotalBytes = totalItemBytes;
                    off.DbPath = f;
                    off.AgeDays = ComputeAgeDays(null, f);
                    off.EligibleForSlim = false;
                    off.IneligibleReason = "CLI session";
                    allDbItems.Add(off);
                }
            }

            // Sort DB items descending by TotalBytes
            allDbItems.Sort(delegate(DbOffenderInfo a, DbOffenderInfo b)
            {
                int cmp = b.TotalBytes.CompareTo(a.TotalBytes);
                if (cmp != 0) return cmp;
                return string.Compare(a.ConversationId, b.ConversationId, StringComparison.OrdinalIgnoreCase);
            });

            int topCount = Math.Min(topN, allDbItems.Count);
            HashSet<string> topOffenderCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < topCount; i++)
            {
                report.TopOffenders.Add(allDbItems[i]);
                topOffenderCids.Add(allDbItems[i].ConversationId);
            }

            // Query read-only SQLite table breakdown (executor_metadata vs steps) for DBs > 2 MB OR top-N offenders
            for (int i = 0; i < allDbItems.Count; i++)
            {
                DbOffenderInfo item = allDbItems[i];
                if (item.TotalBytes > DefaultTableBreakdownThresholdBytes ||
                    topOffenderCids.Contains(item.ConversationId))
                {
                    PopulateTableBreakdown(item, report.Warnings);
                }
            }

            // Legacy antigravity-cli/conversation_summaries.db ghost entries
            string cliSumDb = Path.Combine(cliRoot, "conversation_summaries.db");
            if (File.Exists(cliSumDb))
            {
                report.LegacyCliArchive.Exists = true;
                report.LegacyCliArchive.DbPath = cliSumDb;
                long legacyBytes = GetFileSizeSafe(cliSumDb) +
                                   GetFileSizeSafe(cliSumDb + "-wal") +
                                   GetFileSizeSafe(cliSumDb + "-shm");
                report.LegacyCliArchive.TotalBytes = legacyBytes;
                report.LegacyCliSummaryDbBytes = legacyBytes;

                List<string> cliArchiveWarns;
                List<LegacyCliArchiveEntry> cliEntries = WorkspaceService.LoadLegacyCliArchiveEntries(report.GeminiPath, out cliArchiveWarns);
                if (cliArchiveWarns != null && cliArchiveWarns.Count > 0)
                    report.Warnings.AddRange(cliArchiveWarns);
                if (cliEntries != null)
                {
                    report.LegacyCliArchive.Entries = cliEntries;
                    report.LegacyCliArchive.EntryCount = cliEntries.Count;
                }
            }

            // Finalize project totals & sort
            List<ProjectStorageBreakdown> projList = new List<ProjectStorageBreakdown>(projMap.Values);
            for (int i = 0; i < projList.Count; i++)
            {
                ProjectStorageBreakdown p = projList[i];
                p.TotalBytes = p.RootDbsBytes + p.SubagentDbsBytes + p.BrainDirsBytes + p.AnnotationsBytes + p.CliDbsBytes;
            }
            projList.Sort(delegate(ProjectStorageBreakdown a, ProjectStorageBreakdown b)
            {
                return b.TotalBytes.CompareTo(a.TotalBytes);
            });
            report.Projects = projList;

            report.TotalBytes = report.RootDbsBytes +
                                report.SubagentDbsBytes +
                                report.BrainDirsBytes +
                                report.AnnotationsBytes +
                                report.CliDbsBytes +
                                report.OrphansBytes +
                                report.LegacyCliSummaryDbBytes;

            return report;
        }

        // ══════════════════════════════════════════════════════════════════
        // 2. PRE-FLIGHT SANDBOX COMPATIBILITY VERIFICATION
        // ══════════════════════════════════════════════════════════════════

        public static bool VerifyLiposuctionCompatibility(string sampleSubagentDbPath, out string report)
        {
            report = string.Empty;
            if (string.IsNullOrEmpty(sampleSubagentDbPath) || !File.Exists(sampleSubagentDbPath))
            {
                report = "Sample DB does not exist: " + (sampleSubagentDbPath ?? string.Empty);
                return false;
            }

            string sandboxDir = Path.Combine(Path.GetTempPath(), "agy_liposuction_preflight_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            StringBuilder sb = new StringBuilder();
            try
            {
                string convDir = Path.Combine(sandboxDir, "antigravity", "conversations");
                Directory.CreateDirectory(convDir);

                string stem = Path.GetFileNameWithoutExtension(sampleSubagentDbPath);
                string cid = SessionMetadataExtractor.IsLikelyConversationId(stem)
                    ? stem.Trim()
                    : Guid.NewGuid().ToString();

                string tempDb = Path.Combine(convDir, cid + ".db");

                // Clone safely via SqliteOnlineBackup (never touching original DB)
                string backupErr;
                if (!SqliteOnlineBackup.BackupDatabaseAtomic(sampleSubagentDbPath, tempDb, out backupErr))
                {
                    report = "Failed to clone sample DB to %TEMP% via SqliteOnlineBackup: " + backupErr;
                    return false;
                }

                long beforeBytes = GetFileSizeSafe(tempDb);
                sb.AppendLine("Sample DB: " + sampleSubagentDbPath);
                sb.AppendLine("Cloned Temp DB: " + tempDb + " (" + beforeBytes + " bytes)");

                // Step 1: Inspect sqlite_master for FK/triggers referencing executor_metadata
                string fkTriggerErr;
                if (!CheckNoForeignKeysOrTriggersOnExecutorMetadata(tempDb, out fkTriggerErr))
                {
                    sb.AppendLine("[FAIL] Schema dependency check: " + fkTriggerErr);
                    report = sb.ToString();
                    return false;
                }
                sb.AppendLine("[PASS] No foreign keys or triggers reference executor_metadata.");

                // Check executor_metadata presence
                if (!HasTable(tempDb, "executor_metadata"))
                {
                    sb.AppendLine("[FAIL] Table 'executor_metadata' does not exist in sample DB.");
                    report = sb.ToString();
                    return false;
                }

                // Record before row counts for critical tables
                string[] trackedTables = new string[]
                {
                    "steps",
                    "trajectory_meta",
                    "gen_metadata",
                    "parent_references",
                    "trajectory_metadata_blob"
                };
                Dictionary<string, long> beforeCounts = ReadTableRowCounts(tempDb, trackedTables);
                long execBefore = ReadSingleTableRowCount(tempDb, "executor_metadata");
                sb.AppendLine("Before executor_metadata rows: " + execBefore);

                // Render Markdown BEFORE liposuction
                ConversationSummaryInfo session = BuildSummaryForTempDb(tempDb, cid);
                bool dummyTrunc;
                string mdBefore = TranscriptMarkdownExporter.PreviewLocalSessionMarkdown(session, sandboxDir, 4 * 1024 * 1024, out dummyTrunc);

                // Step 2: Execute DELETE FROM executor_metadata; VACUUM;
                string execErr;
                if (!SqliteOnlineBackup.ExecuteNonQuery(tempDb, "DELETE FROM executor_metadata; VACUUM;", out execErr))
                {
                    sb.AppendLine("[FAIL] DELETE FROM executor_metadata; VACUUM; failed: " + execErr);
                    report = sb.ToString();
                    return false;
                }

                long afterBytes = GetFileSizeSafe(tempDb);
                long execAfter = ReadSingleTableRowCount(tempDb, "executor_metadata");
                sb.AppendLine("After size: " + afterBytes + " bytes (saved " + (beforeBytes - afterBytes) + " bytes), executor_metadata rows: " + execAfter);

                // Step 3: PRAGMA integrity_check == "ok"
                string integrityDetail;
                if (!SqliteOnlineBackup.RunIntegrityCheck(tempDb, out integrityDetail))
                {
                    sb.AppendLine("[FAIL] PRAGMA integrity_check failed: " + integrityDetail);
                    report = sb.ToString();
                    return false;
                }
                sb.AppendLine("[PASS] PRAGMA integrity_check == " + integrityDetail);

                // Step 4: Verify critical table row counts are 100% unchanged
                Dictionary<string, long> afterCounts = ReadTableRowCounts(tempDb, trackedTables);
                for (int i = 0; i < trackedTables.Length; i++)
                {
                    string tbl = trackedTables[i];
                    long bCnt = beforeCounts[tbl];
                    long aCnt = afterCounts[tbl];
                    if (bCnt != aCnt)
                    {
                        sb.AppendLine("[FAIL] Table '" + tbl + "' row count changed: before=" + bCnt + ", after=" + aCnt);
                        report = sb.ToString();
                        return false;
                    }
                    sb.AppendLine("  - " + tbl + " rows: " + aCnt + " (unchanged)");
                }

                // Step 5: Verify identical TranscriptMarkdownExporter output
                string mdAfter = TranscriptMarkdownExporter.PreviewLocalSessionMarkdown(session, sandboxDir, 4 * 1024 * 1024, out dummyTrunc);
                if (!string.Equals(mdBefore, mdAfter, StringComparison.Ordinal))
                {
                    sb.AppendLine("[FAIL] TranscriptMarkdownExporter output differed before vs after liposuction.");
                    report = sb.ToString();
                    return false;
                }
                sb.AppendLine("[PASS] TranscriptMarkdownExporter rendered identical Markdown output (" + (mdAfter != null ? mdAfter.Length : 0) + " chars).");

                report = sb.ToString();
                return true;
            }
            catch (Exception ex)
            {
                sb.AppendLine("[FAIL] Exception during pre-flight verification: " + ex.Message);
                report = sb.ToString();
                return false;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(sandboxDir)) Directory.Delete(sandboxDir, true);
                }
                catch { }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 3. EXECUTE STORAGE SLIM (5C LIPOSUCTION / 5A COLD ARCHIVE PRUNE / GC)
        // ══════════════════════════════════════════════════════════════════

        public static StorageSlimResult ExecuteStorageSlim(string homePath, string geminiPath, StorageSlimRequest request)
        {
            StorageSlimResult result = new StorageSlimResult();
            StorageSlimRequest req = request ?? new StorageSlimRequest();
            result.Mode = req.Mode;

            string safeHome = !string.IsNullOrWhiteSpace(homePath) ? homePath.Trim() : WorkspaceService.ResolveDefaultHomePath();
            string safeGemini = !string.IsNullOrWhiteSpace(geminiPath) ? geminiPath.Trim() : Path.Combine(safeHome, ".gemini");

            if (string.IsNullOrEmpty(safeGemini) || !Directory.Exists(safeGemini))
            {
                result.Ok = false;
                result.ErrorMessage = "未找到有效的 .gemini 目录: " + safeGemini;
                return result;
            }

            Dictionary<string, ProjectMetadata> localProjects;
            List<string> loadWarns;
            List<ConversationSummaryInfo> summaries = WorkspaceService.LoadLocalThreeLayerSummaries(safeGemini, out localProjects, out loadWarns);
            if (loadWarns != null && loadWarns.Count > 0)
                result.Warnings.AddRange(loadWarns);

            // Execute optional Orphan GC & Legacy CLI archive prune if requested
            if (req.CleanOrphans || req.Mode == StorageSlimMode.OrphanGcOnly)
            {
                OrphanGcInventory inv = ScanCompleteOrphans(safeGemini, summaries, result.Warnings);
                DeletionResult gcRes = CleanOrphanedInventory(inv);
                result.DeletedOrphanCount = gcRes.DeletedDbFilesCount + gcRes.DeletedBrainDirsCount + gcRes.DeletedAnnotationsCount;
                result.FreedOrphanBytes = gcRes.FreedBytes;
                result.FreedBytes += gcRes.FreedBytes;
            }

            if (req.PruneLegacyCliArchive)
            {
                string legacyArchive;
                long legacyFreed;
                string legacyErr;
                if (ArchiveAndPruneLegacyCliDb(safeGemini, out legacyArchive, out legacyFreed, out legacyErr))
                {
                    result.LegacyCliPruned = true;
                    result.LegacyCliArchivePath = legacyArchive ?? string.Empty;
                    result.FreedBytes += legacyFreed;
                }
                else if (!string.IsNullOrEmpty(legacyErr))
                {
                    result.Warnings.Add("Legacy CLI 归档裁剪提示: " + legacyErr);
                }
            }

            if (req.Mode == StorageSlimMode.OrphanGcOnly)
            {
                result.Ok = true;
                return result;
            }

            HashSet<string> targetCidSet = null;
            if (req.TargetCids != null && req.TargetCids.Count > 0)
            {
                targetCidSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < req.TargetCids.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(req.TargetCids[i]))
                        targetCidSet.Add(req.TargetCids[i].Trim());
                }
            }

            string ideRoot = Path.Combine(safeGemini, "antigravity");
            string cliRoot = Path.Combine(safeGemini, "antigravity-cli");

            List<DbOffenderInfo> eligibleTargets = new List<DbOffenderInfo>();
            HashSet<string> seenCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < summaries.Count; i++)
            {
                ConversationSummaryInfo s = summaries[i];
                if (s == null || s is LegacyCliArchiveEntry) continue;
                if (!SessionMetadataExtractor.IsLikelyConversationId(s.ConversationId)) continue;
                string cid = s.ConversationId.Trim();
                if (!seenCids.Add(cid)) continue;

                if (targetCidSet != null && !targetCidSet.Contains(cid))
                    continue;

                string primaryRoot = s.IsCliSession ? cliRoot : ideRoot;
                string secondaryRoot = s.IsCliSession ? ideRoot : cliRoot;
                string dbPath = LocateSessionFile(primaryRoot, secondaryRoot, "conversations", cid + ".db");
                if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
                    continue;

                long dbBytes = GetFileSizeSafe(dbPath);
                long walBytes = GetFileSizeSafe(dbPath + "-wal");
                long totalBytes = dbBytes + walBytes + GetFileSizeSafe(dbPath + "-shm");
                double ageDays = ComputeAgeDays(s, dbPath);

                string ineligReason;
                if (!EvaluateSlimEligibility(s, totalBytes, ageDays, req.MinAgeDays, req.MinSizeMb, out ineligReason))
                {
                    if (targetCidSet != null)
                    {
                        StorageSlimItemResult skipped = new StorageSlimItemResult();
                        skipped.ConversationId = cid;
                        skipped.DbPath = dbPath;
                        skipped.BytesBefore = totalBytes;
                        skipped.BytesAfter = totalBytes;
                        skipped.Succeeded = false;
                        skipped.SkippedReason = ineligReason;
                        result.Items.Add(skipped);
                        result.SkippedCount++;
                    }
                    continue;
                }

                DbOffenderInfo target = new DbOffenderInfo();
                target.ConversationId = cid;
                target.Title = s.DisplayTitle ?? cid;
                target.IsCli = s.IsCliSession;
                target.IsSubagent = s.IsSubagent;
                target.Status = s.RunStatus ?? string.Empty;
                target.NotFullyIdle = s.NotFullyIdle;
                target.AgeDays = ageDays;
                target.DbBytes = dbBytes;
                target.WalBytes = walBytes;
                target.TotalBytes = totalBytes;
                target.DbPath = dbPath;
                eligibleTargets.Add(target);
            }

            if (eligibleTargets.Count == 0)
            {
                result.Ok = true;
                return result;
            }

            // Pre-flight verification on first eligible DB in Liposuction mode (never touches original DB)
            if (req.Mode == StorageSlimMode.Liposuction && req.RunPreFlight)
            {
                string preReport;
                bool preOk = VerifyLiposuctionCompatibility(eligibleTargets[0].DbPath, out preReport);
                result.PreFlightPassed = preOk;
                result.PreFlightReport = preReport ?? string.Empty;
                if (!preOk)
                {
                    result.Ok = false;
                    result.ErrorMessage = "预检兼容性验证未通过，已中止瘦身操作以保护数据安全: " + preReport;
                    return result;
                }
            }
            else
            {
                result.PreFlightPassed = true;
            }

            // MANDATORY COLD ARCHIVE FIRST: stream untouched original .db files via SqliteOnlineBackup
            string archiveDir = Path.Combine(safeGemini, "companion-cold-archives");
            if (!Directory.Exists(archiveDir))
                Directory.CreateDirectory(archiveDir);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            string coldArchivePath = Path.Combine(archiveDir, "subagents_" + timestamp + ".agy");
            string coldErr;
            if (!CreateSubagentsColdArchive(coldArchivePath, req.Mode, eligibleTargets, out coldErr))
            {
                result.Ok = false;
                result.ErrorMessage = "强制冷归档备份失败，未对本地数据库执行任何修改: " + coldErr;
                return result;
            }

            // Verify cold archive ZIP is readable before touching any local file
            string verifyErr;
            if (!VerifyArchiveEntriesReadable(coldArchivePath, eligibleTargets, out verifyErr))
            {
                result.Ok = false;
                result.ErrorMessage = "冷归档 ZIP 可读性校验失败，已中止本地操作: " + verifyErr;
                return result;
            }
            result.ColdArchivePath = coldArchivePath;

            // Process each eligible subagent DB
            for (int i = 0; i < eligibleTargets.Count; i++)
            {
                DbOffenderInfo item = eligibleTargets[i];
                StorageSlimItemResult itemRes = new StorageSlimItemResult();
                itemRes.ConversationId = item.ConversationId;
                itemRes.DbPath = item.DbPath;
                itemRes.BytesBefore = item.TotalBytes;

                if (req.Mode == StorageSlimMode.Liposuction)
                {
                    long afterBytes;
                    string integrity;
                    string slimErr;
                    if (ExecuteSingleLiposuction(item.DbPath, out afterBytes, out integrity, out slimErr))
                    {
                        itemRes.Succeeded = true;
                        itemRes.BytesAfter = afterBytes;
                        itemRes.SavedBytes = Math.Max(0L, item.TotalBytes - afterBytes);
                        itemRes.IntegrityCheck = integrity;
                        result.ProcessedCount++;
                        result.BytesBefore += item.TotalBytes;
                        result.BytesAfter += afterBytes;
                        result.FreedBytes += itemRes.SavedBytes;
                    }
                    else
                    {
                        itemRes.Succeeded = false;
                        itemRes.BytesAfter = item.TotalBytes;
                        itemRes.SkippedReason = slimErr;
                        itemRes.IntegrityCheck = integrity ?? string.Empty;
                        result.SkippedCount++;
                        result.Warnings.Add(string.Format("会话 {0} 瘦身跳过: {1}", item.ConversationId, slimErr));
                    }
                }
                else if (req.Mode == StorageSlimMode.ColdArchivePrune)
                {
                    string pruneErr;
                    if (DeleteLocalSubagentDbOnly(item.DbPath, out pruneErr))
                    {
                        itemRes.Succeeded = true;
                        itemRes.BytesAfter = 0L;
                        itemRes.SavedBytes = item.TotalBytes;
                        itemRes.IntegrityCheck = "archived_pruned";
                        result.ProcessedCount++;
                        result.BytesBefore += item.TotalBytes;
                        result.FreedBytes += item.TotalBytes;
                    }
                    else
                    {
                        itemRes.Succeeded = false;
                        itemRes.BytesAfter = item.TotalBytes;
                        itemRes.SkippedReason = pruneErr;
                        result.SkippedCount++;
                        result.Warnings.Add(string.Format("会话 {0} 归档后裁剪失败: {1}", item.ConversationId, pruneErr));
                    }
                }

                result.Items.Add(itemRes);
            }

            result.Ok = true;
            return result;
        }

        private static bool CreateSubagentsColdArchive(
            string archivePath,
            StorageSlimMode mode,
            List<DbOffenderInfo> targets,
            out string error)
        {
            error = string.Empty;
            string tmpArchive = archivePath + ".tmp." + Guid.NewGuid().ToString("N").Substring(0, 8);
            try
            {
                List<object> manifestItems = new List<object>(targets.Count);
                using (FileStream fs = new FileStream(tmpArchive, FileMode.Create, FileAccess.Write, FileShare.None))
                using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    for (int i = 0; i < targets.Count; i++)
                    {
                        DbOffenderInfo t = targets[i];
                        string prefix = t.IsCli ? "data/antigravity-cli/conversations/" : "data/antigravity/conversations/";
                        string entryName = prefix + t.ConversationId + ".db";

                        string tempSnap = Path.Combine(Path.GetTempPath(), "agy_cold_snap_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".db");
                        try
                        {
                            string bkErr;
                            if (!SqliteOnlineBackup.BackupDatabaseAtomic(t.DbPath, tempSnap, out bkErr))
                            {
                                error = "SqliteOnlineBackup failed for " + t.ConversationId + ": " + bkErr;
                                return false;
                            }

                            using (FileStream dbFs = new FileStream(tempSnap, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                ZipArchiveEntry entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
                                using (Stream es = entry.Open())
                                {
                                    dbFs.CopyTo(es);
                                }
                            }

                            Dictionary<string, object> mItem = new Dictionary<string, object>(StringComparer.Ordinal);
                            mItem["cid"] = t.ConversationId;
                            mItem["title"] = t.Title ?? string.Empty;
                            mItem["isCli"] = t.IsCli;
                            mItem["originalPath"] = t.DbPath;
                            mItem["entryName"] = entryName;
                            mItem["originalBytes"] = t.TotalBytes;
                            mItem["snapshotBytes"] = GetFileSizeSafe(tempSnap);
                            manifestItems.Add(mItem);
                        }
                        finally
                        {
                            DeleteFileQuiet(tempSnap);
                            DeleteFileQuiet(tempSnap + "-wal");
                            DeleteFileQuiet(tempSnap + "-shm");
                            DeleteFileQuiet(tempSnap + "-journal");
                        }
                    }

                    Dictionary<string, object> manifest = new Dictionary<string, object>(StringComparer.Ordinal);
                    manifest["archive_type"] = "companion-cold-archive";
                    manifest["created_at"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                    manifest["mode"] = mode.ToString();
                    manifest["count"] = targets.Count;
                    manifest["items"] = manifestItems;

                    ZipArchiveEntry manEntry = zip.CreateEntry("manifest.json", CompressionLevel.Fastest);
                    using (StreamWriter sw = new StreamWriter(manEntry.Open(), new UTF8Encoding(false)))
                    {
                        sw.Write(UnifiedJson.Serialize(manifest, true));
                    }
                }

                if (File.Exists(archivePath))
                {
                    File.SetAttributes(archivePath, FileAttributes.Normal);
                    File.Delete(archivePath);
                }
                File.Move(tmpArchive, archivePath);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                DeleteFileQuiet(tmpArchive);
                return false;
            }
        }

        private static bool VerifyArchiveEntriesReadable(string archivePath, List<DbOffenderInfo> targets, out string error)
        {
            error = string.Empty;
            if (!File.Exists(archivePath))
            {
                error = "Archive file not found: " + archivePath;
                return false;
            }

            try
            {
                using (ZipArchive zip = ZipFile.OpenRead(archivePath))
                {
                    ZipArchiveEntry man = zip.GetEntry("manifest.json");
                    if (man == null || man.Length <= 0)
                    {
                        error = "manifest.json missing or empty in cold archive.";
                        return false;
                    }
                    using (StreamReader sr = new StreamReader(man.Open(), Encoding.UTF8))
                    {
                        string text = sr.ReadToEnd();
                        object parsed;
                        if (!UnifiedJson.TryParse(text, out parsed))
                        {
                            error = "manifest.json is invalid JSON.";
                            return false;
                        }
                    }

                    for (int i = 0; i < targets.Count; i++)
                    {
                        DbOffenderInfo t = targets[i];
                        string prefix = t.IsCli ? "data/antigravity-cli/conversations/" : "data/antigravity/conversations/";
                        string entryName = prefix + t.ConversationId + ".db";
                        ZipArchiveEntry e = zip.GetEntry(entryName);
                        if (e == null || e.Length <= 0)
                        {
                            error = "Cold archive entry missing or empty: " + entryName;
                            return false;
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool ExecuteSingleLiposuction(
            string originalDbPath,
            out long afterBytes,
            out string integrityDetail,
            out string error)
        {
            afterBytes = GetFileSizeSafe(originalDbPath);
            integrityDetail = string.Empty;
            error = string.Empty;

            string tempWorkDb = Path.Combine(Path.GetTempPath(), "agy_slim_work_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".db");
            try
            {
                string bkErr;
                if (!SqliteOnlineBackup.BackupDatabaseAtomic(originalDbPath, tempWorkDb, out bkErr))
                {
                    error = "Clone to temp DB failed: " + bkErr;
                    return false;
                }

                string fkErr;
                if (!CheckNoForeignKeysOrTriggersOnExecutorMetadata(tempWorkDb, out fkErr))
                {
                    error = "Schema dependency guard blocked liposuction: " + fkErr;
                    return false;
                }

                if (!HasTable(tempWorkDb, "executor_metadata"))
                {
                    error = "Table executor_metadata does not exist.";
                    return false;
                }

                long stepsBefore = ReadSingleTableRowCount(tempWorkDb, "steps");

                string execErr;
                if (!SqliteOnlineBackup.ExecuteNonQuery(tempWorkDb, "DELETE FROM executor_metadata; VACUUM;", out execErr))
                {
                    error = "DELETE/VACUUM failed: " + execErr;
                    return false;
                }

                if (!SqliteOnlineBackup.RunIntegrityCheck(tempWorkDb, out integrityDetail))
                {
                    error = "PRAGMA integrity_check failed: " + integrityDetail;
                    return false;
                }

                long stepsAfter = ReadSingleTableRowCount(tempWorkDb, "steps");
                if (stepsBefore != stepsAfter)
                {
                    error = "steps row count mismatch (before=" + stepsBefore + ", after=" + stepsAfter + ")";
                    return false;
                }

                string sameDirStage = originalDbPath + ".slimtmp." + Guid.NewGuid().ToString("N").Substring(0, 8);
                try
                {
                    // Guard against locked -wal / -shm sidecars before swapping a vacuumed DB
                    if (!CanAcquireExclusiveSidecarAccess(originalDbPath + "-wal") ||
                        !CanAcquireExclusiveSidecarAccess(originalDbPath + "-shm"))
                    {
                        error = "Database sidecar (-wal/-shm) is locked by another process.";
                        return false;
                    }

                    // Stage into the same directory so File.Replace never fails across volumes (ERROR_NOT_SAME_DEVICE)
                    File.Copy(tempWorkDb, sameDirStage, true);
                    if (File.Exists(originalDbPath))
                    {
                        try { File.SetAttributes(originalDbPath, FileAttributes.Normal); } catch { }
                        File.Replace(sameDirStage, originalDbPath, null, true);
                    }
                    else
                    {
                        File.Move(sameDirStage, originalDbPath);
                    }

                    // Clean obsolete -wal / -shm / -journal sidecars after atomic swap
                    DeleteFileQuiet(originalDbPath + "-wal");
                    DeleteFileQuiet(originalDbPath + "-shm");
                    DeleteFileQuiet(originalDbPath + "-journal");

                    afterBytes = GetFileSizeSafe(originalDbPath);
                    return true;
                }
                finally
                {
                    DeleteFileQuiet(sameDirStage);
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                DeleteFileQuiet(tempWorkDb);
                DeleteFileQuiet(tempWorkDb + "-wal");
                DeleteFileQuiet(tempWorkDb + "-shm");
                DeleteFileQuiet(tempWorkDb + "-journal");
            }
        }

        private static bool DeleteLocalSubagentDbOnly(string dbPath, out string error)
        {
            error = string.Empty;
            try
            {
                if (File.Exists(dbPath))
                {
                    File.SetAttributes(dbPath, FileAttributes.Normal);
                    File.Delete(dbPath);
                }
                DeleteFileQuiet(dbPath + "-wal");
                DeleteFileQuiet(dbPath + "-shm");
                DeleteFileQuiet(dbPath + "-journal");
                DeleteFileQuiet(dbPath + ".bak");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 4. COMPLETE ORPHAN GC & LEGACY CLI ARCHIVE PRUNE
        // ══════════════════════════════════════════════════════════════════

        public static OrphanGcInventory ScanCompleteOrphans(
            string geminiPath,
            List<ConversationSummaryInfo> loadedSummaries,
            List<string> warnings)
        {
            OrphanGcInventory inv = new OrphanGcInventory();
            if (string.IsNullOrEmpty(geminiPath) || !Directory.Exists(geminiPath))
                return inv;

            HashSet<string> runningCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (loadedSummaries != null)
            {
                for (int i = 0; i < loadedSummaries.Count; i++)
                {
                    ConversationSummaryInfo s = loadedSummaries[i];
                    if (s == null || string.IsNullOrEmpty(s.ConversationId)) continue;
                    if (s.NotFullyIdle || (!string.IsNullOrEmpty(s.RunStatus) && s.RunStatus.IndexOf("RUNNING", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        runningCids.Add(s.ConversationId.Trim());
                    }
                }
            }

            OrphanScanResult baseOrphans = null;
            try
            {
                baseOrphans = SessionLifecycleService.ScanOrphanedData(geminiPath);
            }
            catch (Exception ex)
            {
                if (warnings != null) warnings.Add("基础孤儿扫描受保护跳过: " + ex.Message);
            }

            HashSet<string> seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (baseOrphans != null)
            {
                for (int i = 0; i < baseOrphans.OrphanDbFiles.Count; i++)
                {
                    string f = baseOrphans.OrphanDbFiles[i];
                    if (!seenFiles.Add(f)) continue;
                    if (f.EndsWith(".db.bak", StringComparison.OrdinalIgnoreCase))
                        inv.OrphanDbBakFiles.Add(f);
                    else if (f.EndsWith(".db-journal", StringComparison.OrdinalIgnoreCase))
                        inv.OrphanDbJournalFiles.Add(f);
                    else
                        inv.OrphanDbFiles.Add(f);
                    inv.TotalOrphanBytes += GetFileSizeSafe(f);
                }

                for (int i = 0; i < baseOrphans.OrphanBrainDirs.Count; i++)
                {
                    string d = baseOrphans.OrphanBrainDirs[i];
                    inv.OrphanBrainDirs.Add(d);
                    inv.TotalOrphanBytes += GetDirectorySizeSafe(d);
                }

                for (int i = 0; i < baseOrphans.OrphanAnnotationFiles.Count; i++)
                {
                    string f = baseOrphans.OrphanAnnotationFiles[i];
                    inv.OrphanAnnotationFiles.Add(f);
                    inv.TotalOrphanBytes += GetFileSizeSafe(f);
                }
            }

            // Scan leftover *.db.bak (including active-CID leftovers) & stale *.db-journal across IDE & CLI conversations/
            string[] roots = new string[]
            {
                Path.Combine(geminiPath, "antigravity"),
                Path.Combine(geminiPath, "antigravity-cli")
            };

            for (int r = 0; r < roots.Length; r++)
            {
                string convDir = Path.Combine(roots[r], "conversations");
                if (!Directory.Exists(convDir)) continue;

                string[] files = Directory.GetFiles(convDir);
                for (int i = 0; i < files.Length; i++)
                {
                    string f = files[i];
                    if (seenFiles.Contains(f)) continue;
                    string fn = Path.GetFileName(f);
                    if (string.IsNullOrEmpty(fn)) continue;

                    if (fn.EndsWith(".db.bak", StringComparison.OrdinalIgnoreCase) ||
                        fn.IndexOf(".db.bak.", StringComparison.OrdinalIgnoreCase) > 0)
                    {
                        seenFiles.Add(f);
                        inv.OrphanDbBakFiles.Add(f);
                        inv.TotalOrphanBytes += GetFileSizeSafe(f);
                    }
                    else if (fn.EndsWith(".db-journal", StringComparison.OrdinalIgnoreCase))
                    {
                        string cid = fn.Substring(0, fn.Length - ".db-journal".Length);
                        if (!runningCids.Contains(cid))
                        {
                            seenFiles.Add(f);
                            inv.OrphanDbJournalFiles.Add(f);
                            inv.TotalOrphanBytes += GetFileSizeSafe(f);
                        }
                    }
                }
            }

            return inv;
        }

        public static DeletionResult CleanOrphanedInventory(OrphanGcInventory inv)
        {
            DeletionResult res = new DeletionResult();
            if (inv == null) return res;

            List<string> allFiles = new List<string>();
            allFiles.AddRange(inv.OrphanDbFiles);
            allFiles.AddRange(inv.OrphanDbBakFiles);
            allFiles.AddRange(inv.OrphanDbJournalFiles);

            for (int i = 0; i < allFiles.Count; i++)
            {
                string f = allFiles[i];
                if (!File.Exists(f)) continue;
                long len = GetFileSizeSafe(f);
                if (DeleteFileQuiet(f))
                {
                    res.DeletedDbFilesCount++;
                    res.FreedBytes += len;
                }
            }

            for (int i = 0; i < inv.OrphanBrainDirs.Count; i++)
            {
                string d = inv.OrphanBrainDirs[i];
                string dirName = Path.GetFileName(d);
                Guid g;
                if (Directory.Exists(d) &&
                    !string.Equals(dirName, "bin", StringComparison.OrdinalIgnoreCase) &&
                    Guid.TryParse(dirName, out g) &&
                    SessionMetadataExtractor.IsLikelyConversationId(dirName))
                {
                    long len = GetDirectorySizeSafe(d);
                    if (DeleteDirectoryQuiet(d))
                    {
                        res.DeletedBrainDirsCount++;
                        res.FreedBytes += len;
                    }
                }
            }

            for (int i = 0; i < inv.OrphanAnnotationFiles.Count; i++)
            {
                string f = inv.OrphanAnnotationFiles[i];
                if (!File.Exists(f)) continue;
                long len = GetFileSizeSafe(f);
                if (DeleteFileQuiet(f))
                {
                    res.DeletedAnnotationsCount++;
                    res.FreedBytes += len;
                }
            }

            return res;
        }

        public static bool ArchiveAndPruneLegacyCliDb(
            string geminiPath,
            out string archivePath,
            out long freedBytes,
            out string error)
        {
            archivePath = string.Empty;
            freedBytes = 0;
            error = string.Empty;

            if (string.IsNullOrEmpty(geminiPath))
            {
                error = "Gemini path is empty.";
                return false;
            }

            string cliSummaryDb = Path.Combine(geminiPath, "antigravity-cli", "conversation_summaries.db");
            if (!File.Exists(cliSummaryDb))
            {
                return false;
            }

            long dbBytes = GetFileSizeSafe(cliSummaryDb);
            long walBytes = GetFileSizeSafe(cliSummaryDb + "-wal");
            long shmBytes = GetFileSizeSafe(cliSummaryDb + "-shm");
            long totalBytes = dbBytes + walBytes + shmBytes;

            string archiveDir = Path.Combine(geminiPath, "companion-cold-archives");
            if (!Directory.Exists(archiveDir))
                Directory.CreateDirectory(archiveDir);

            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            string destArchive = Path.Combine(archiveDir, "legacy_cli_summaries_" + ts + ".agy");
            string tmpArchive = destArchive + ".tmp." + Guid.NewGuid().ToString("N").Substring(0, 8);
            string tempSnap = Path.Combine(Path.GetTempPath(), "agy_legacy_cli_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".db");

            try
            {
                string bkErr;
                if (!SqliteOnlineBackup.BackupDatabaseAtomic(cliSummaryDb, tempSnap, out bkErr))
                {
                    error = "Failed to snapshot legacy CLI summary DB: " + bkErr;
                    return false;
                }

                using (FileStream fs = new FileStream(tmpArchive, FileMode.Create, FileAccess.Write, FileShare.None))
                using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    using (FileStream dbFs = new FileStream(tempSnap, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        ZipArchiveEntry e = zip.CreateEntry("data/antigravity-cli/conversation_summaries.db", CompressionLevel.Fastest);
                        using (Stream es = e.Open())
                        {
                            dbFs.CopyTo(es);
                        }
                    }

                    Dictionary<string, object> man = new Dictionary<string, object>(StringComparer.Ordinal);
                    man["archive_type"] = "legacy-cli-summary-archive";
                    man["created_at"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                    man["originalPath"] = cliSummaryDb;
                    man["originalBytes"] = totalBytes;
                    ZipArchiveEntry manEntry = zip.CreateEntry("manifest.json", CompressionLevel.Fastest);
                    using (StreamWriter sw = new StreamWriter(manEntry.Open(), new UTF8Encoding(false)))
                    {
                        sw.Write(UnifiedJson.Serialize(man, true));
                    }
                }

                // Verify readable before deleting local legacy DB
                using (ZipArchive verifyZip = ZipFile.OpenRead(tmpArchive))
                {
                    ZipArchiveEntry dbEntry = verifyZip.GetEntry("data/antigravity-cli/conversation_summaries.db");
                    if (dbEntry == null || dbEntry.Length <= 0)
                    {
                        error = "Legacy CLI archive verification failed.";
                        return false;
                    }
                }

                File.Move(tmpArchive, destArchive);
                archivePath = destArchive;

                if (DeleteFileQuiet(cliSummaryDb))
                {
                    DeleteFileQuiet(cliSummaryDb + "-wal");
                    DeleteFileQuiet(cliSummaryDb + "-shm");
                    DeleteFileQuiet(cliSummaryDb + "-journal");
                    freedBytes = totalBytes;
                    return true;
                }

                error = "Cold archive created at " + destArchive + ", but failed to delete local legacy CLI DB (file locked).";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                DeleteFileQuiet(tmpArchive);
                DeleteFileQuiet(tempSnap);
                DeleteFileQuiet(tempSnap + "-wal");
                DeleteFileQuiet(tempSnap + "-shm");
                DeleteFileQuiet(tempSnap + "-journal");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 5. INTERNAL SQLITE & ELIGIBILITY HELPERS
        // ══════════════════════════════════════════════════════════════════

        internal static bool IsIdleRunStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            string s = status.Trim();
            return string.Equals(s, "IDLE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(s, "CASCADE_RUN_STATUS_IDLE", StringComparison.OrdinalIgnoreCase);
        }

        private static bool EvaluateSlimEligibility(
            ConversationSummaryInfo s,
            long totalDbBytes,
            double ageDays,
            double minAgeDays,
            double minSizeMb,
            out string reason)
        {
            reason = string.Empty;
            if (s == null)
            {
                reason = "Missing summary metadata";
                return false;
            }
            if (!s.IsSubagent)
            {
                reason = "Not a subagent session";
                return false;
            }
            if (s.NotFullyIdle)
            {
                reason = "not_fully_idle != 0";
                return false;
            }
            if (!IsIdleRunStatus(s.RunStatus))
            {
                reason = "Status is not IDLE (" + (string.IsNullOrEmpty(s.RunStatus) ? "unknown" : s.RunStatus) + ")";
                return false;
            }
            if (ageDays < minAgeDays)
            {
                reason = string.Format(CultureInfo.InvariantCulture, "Age {0:F1}d < {1:F1}d", ageDays, minAgeDays);
                return false;
            }
            long minBytes = (long)(minSizeMb * 1024.0 * 1024.0);
            if (totalDbBytes < minBytes)
            {
                reason = string.Format(CultureInfo.InvariantCulture, "Size {0:F2}MB < {1:F2}MB", totalDbBytes / (1024.0 * 1024.0), minSizeMb);
                return false;
            }
            return true;
        }

        private static double ComputeAgeDays(ConversationSummaryInfo s, string dbPath)
        {
            DateTime utcRef = DateTime.MinValue;
            if (s != null && s.UpdatedTime > DateTime.MinValue)
            {
                utcRef = s.UpdatedTime.Kind == DateTimeKind.Utc
                    ? s.UpdatedTime
                    : s.UpdatedTime.ToUniversalTime();
            }
            else if (!string.IsNullOrEmpty(dbPath) && File.Exists(dbPath))
            {
                try { utcRef = File.GetLastWriteTimeUtc(dbPath); } catch { }
            }

            if (utcRef == DateTime.MinValue) return 0.0;
            double days = (DateTime.UtcNow - utcRef).TotalDays;
            return days < 0.0 ? 0.0 : days;
        }

        private static void PopulateTableBreakdown(DbOffenderInfo item, List<string> warnings)
        {
            if (item == null || string.IsNullOrEmpty(item.DbPath) || !File.Exists(item.DbPath))
                return;

            string queryPath = item.DbPath;
            string tempSnap = null;
            bool needSnapshot = File.Exists(item.DbPath + "-wal") && GetFileSizeSafe(item.DbPath + "-wal") > 0;

            try
            {
                IntPtr db = IntPtr.Zero;
                bool openedDirect = false;
                if (!needSnapshot)
                {
                    openedDirect = TryOpenReadOnly(item.DbPath, out db);
                }

                if (!openedDirect)
                {
                    tempSnap = Path.Combine(Path.GetTempPath(), "agy_stat_snap_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".db");
                    string bkErr;
                    if (!SqliteOnlineBackup.BackupDatabaseAtomic(item.DbPath, tempSnap, out bkErr))
                    {
                        if (warnings != null) warnings.Add("无法读取 SQLite 表占比 (" + item.ConversationId + "): " + bkErr);
                        return;
                    }
                    queryPath = tempSnap;
                    if (!TryOpenReadOnly(queryPath, out db))
                    {
                        if (warnings != null) warnings.Add("无法只读打开快照 DB (" + item.ConversationId + ")");
                        return;
                    }
                }

                try
                {
                    bool allOk = true;
                    if (HasTableHandle(db, "executor_metadata"))
                    {
                        item.HasExecutorMetadataTable = true;
                        long execRows, execBytes;
                        if (QueryTableRowsAndPayloadBytes(db, "executor_metadata", out execRows, out execBytes))
                        {
                            item.ExecutorMetadataRows = execRows;
                            item.ExecutorMetadataBytes = execBytes;
                        }
                        else
                        {
                            allOk = false;
                        }
                    }

                    string stepTable = ResolveStepTableName(db);
                    if (!string.IsNullOrEmpty(stepTable))
                    {
                        long stepRows, stepBytes;
                        if (QueryTableRowsAndPayloadBytes(db, stepTable, out stepRows, out stepBytes))
                        {
                            item.StepsRows = stepRows;
                            item.StepsBytes = stepBytes;
                        }
                        else
                        {
                            allOk = false;
                        }
                    }

                    item.HasTableBreakdown = allOk;
                }
                finally
                {
                    if (db != IntPtr.Zero)
                    {
                        try { sqlite3_close(db); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                if (warnings != null) warnings.Add("SQLite 表占比查询异常 (" + item.ConversationId + "): " + ex.Message);
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempSnap))
                {
                    DeleteFileQuiet(tempSnap);
                    DeleteFileQuiet(tempSnap + "-wal");
                    DeleteFileQuiet(tempSnap + "-shm");
                    DeleteFileQuiet(tempSnap + "-journal");
                }
            }
        }

        private static string ResolveStepTableName(IntPtr db)
        {
            string[] candidates = new string[] { "steps", "trajectory_steps", "conversation_steps", "messages" };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (HasTableHandle(db, candidates[i])) return candidates[i];
            }
            return null;
        }

        private static bool QueryTableRowsAndPayloadBytes(IntPtr db, string tableName, out long rowCount, out long payloadBytes)
        {
            rowCount = 0;
            payloadBytes = 0;
            if (db == IntPtr.Zero || !IsSafeIdentifier(tableName)) return false;

            List<string> byteCols = new List<string>();
            IntPtr infoStmt = IntPtr.Zero;
            if (sqlite3_prepare_v2(db, ToUtf8Z("PRAGMA table_info(\"" + tableName + "\");"), -1, out infoStmt, IntPtr.Zero) == SQLITE_OK && infoStmt != IntPtr.Zero)
            {
                try
                {
                    while (sqlite3_step(infoStmt) == SQLITE_ROW)
                    {
                        string colName = ReadColumnText(infoStmt, 1);
                        string colType = ReadColumnText(infoStmt, 2);
                        int pk = (int)sqlite3_column_int64(infoStmt, 5);
                        if (string.IsNullOrEmpty(colName) || !IsSafeIdentifier(colName)) continue;

                        bool isPayloadType = !string.IsNullOrEmpty(colType) &&
                            (colType.IndexOf("BLOB", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             colType.IndexOf("TEXT", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             colType.IndexOf("CHAR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             colType.IndexOf("CLOB", StringComparison.OrdinalIgnoreCase) >= 0);

                        if (isPayloadType || (pk == 0 && (string.Equals(colName, "data", StringComparison.OrdinalIgnoreCase) ||
                                                          string.Equals(colName, "content", StringComparison.OrdinalIgnoreCase) ||
                                                          string.Equals(colName, "step_payload", StringComparison.OrdinalIgnoreCase) ||
                                                          string.Equals(colName, "metadata", StringComparison.OrdinalIgnoreCase))))
                        {
                            byteCols.Add(colName);
                        }
                    }
                }
                finally
                {
                    sqlite3_finalize(infoStmt);
                }
            }

            string sumExpr = "0";
            if (byteCols.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < byteCols.Count; i++)
                {
                    if (i > 0) sb.Append(" + ");
                    sb.Append("COALESCE(LENGTH(CAST(\"").Append(byteCols[i]).Append("\" AS BLOB)), 0)");
                }
                sumExpr = "COALESCE(SUM(" + sb.ToString() + "), 0)";
            }

            string sql = "SELECT COUNT(*), " + sumExpr + " FROM \"" + tableName + "\";";
            IntPtr stmt = IntPtr.Zero;
            if (sqlite3_prepare_v2(db, ToUtf8Z(sql), -1, out stmt, IntPtr.Zero) == SQLITE_OK && stmt != IntPtr.Zero)
            {
                try
                {
                    if (sqlite3_step(stmt) == SQLITE_ROW)
                    {
                        rowCount = sqlite3_column_int64(stmt, 0);
                        payloadBytes = sqlite3_column_int64(stmt, 1);
                        return true;
                    }
                }
                finally
                {
                    sqlite3_finalize(stmt);
                }
            }
            return false;
        }

        private static bool CanAcquireExclusiveSidecarAccess(string sidecarPath)
        {
            if (string.IsNullOrEmpty(sidecarPath) || !File.Exists(sidecarPath))
                return true;
            try
            {
                using (FileStream fs = new FileStream(sidecarPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckNoForeignKeysOrTriggersOnExecutorMetadata(string dbPath, out string error)
        {
            error = string.Empty;
            IntPtr db = IntPtr.Zero;
            if (!TryOpenReadOnly(dbPath, out db))
            {
                error = "Failed to open DB read-only for sqlite_master inspection.";
                return false;
            }

            try
            {
                List<string> tableNames = new List<string>();
                IntPtr stmt = IntPtr.Zero;
                if (sqlite3_prepare_v2(db, ToUtf8Z("SELECT type, name, tbl_name, sql FROM sqlite_master;"), -1, out stmt, IntPtr.Zero) != SQLITE_OK || stmt == IntPtr.Zero)
                {
                    error = "Failed to query sqlite_master.";
                    return false;
                }

                try
                {
                    while (sqlite3_step(stmt) == SQLITE_ROW)
                    {
                        string objType = ReadColumnText(stmt, 0) ?? string.Empty;
                        string objName = ReadColumnText(stmt, 1) ?? string.Empty;
                        string tblName = ReadColumnText(stmt, 2) ?? string.Empty;
                        string sqlText = ReadColumnText(stmt, 3) ?? string.Empty;

                        if (string.Equals(objType, "table", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrEmpty(objName) && IsSafeIdentifier(objName))
                                tableNames.Add(objName);

                            if (!string.Equals(objName, "executor_metadata", StringComparison.OrdinalIgnoreCase) &&
                                sqlText.IndexOf("executor_metadata", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                error = "Table '" + objName + "' references executor_metadata in DDL.";
                                return false;
                            }
                        }
                        else if (string.Equals(objType, "trigger", StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.Equals(tblName, "executor_metadata", StringComparison.OrdinalIgnoreCase) ||
                                sqlText.IndexOf("executor_metadata", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                error = "Trigger '" + objName + "' references executor_metadata.";
                                return false;
                            }
                        }
                    }
                }
                finally
                {
                    sqlite3_finalize(stmt);
                }

                for (int i = 0; i < tableNames.Count; i++)
                {
                    string tbl = tableNames[i];
                    if (string.Equals(tbl, "executor_metadata", StringComparison.OrdinalIgnoreCase)) continue;
                    IntPtr fkStmt = IntPtr.Zero;
                    if (sqlite3_prepare_v2(db, ToUtf8Z("PRAGMA foreign_key_list(\"" + tbl + "\");"), -1, out fkStmt, IntPtr.Zero) == SQLITE_OK && fkStmt != IntPtr.Zero)
                    {
                        try
                        {
                            while (sqlite3_step(fkStmt) == SQLITE_ROW)
                            {
                                string refTable = ReadColumnText(fkStmt, 2) ?? string.Empty;
                                if (string.Equals(refTable, "executor_metadata", StringComparison.OrdinalIgnoreCase))
                                {
                                    error = "Table '" + tbl + "' has FOREIGN KEY referencing executor_metadata.";
                                    return false;
                                }
                            }
                        }
                        finally
                        {
                            sqlite3_finalize(fkStmt);
                        }
                    }
                }

                return true;
            }
            finally
            {
                if (db != IntPtr.Zero)
                {
                    try { sqlite3_close(db); } catch { }
                }
            }
        }

        private static Dictionary<string, long> ReadTableRowCounts(string dbPath, string[] tableNames)
        {
            Dictionary<string, long> counts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < tableNames.Length; i++)
                counts[tableNames[i]] = -1;

            IntPtr db = IntPtr.Zero;
            if (!TryOpenReadOnly(dbPath, out db)) return counts;
            try
            {
                for (int i = 0; i < tableNames.Length; i++)
                {
                    string tbl = tableNames[i];
                    if (!HasTableHandle(db, tbl)) continue;
                    counts[tbl] = ReadTableRowCountHandle(db, tbl);
                }
            }
            finally
            {
                if (db != IntPtr.Zero)
                {
                    try { sqlite3_close(db); } catch { }
                }
            }
            return counts;
        }

        private static long ReadSingleTableRowCount(string dbPath, string tableName)
        {
            IntPtr db = IntPtr.Zero;
            if (!TryOpenReadOnly(dbPath, out db)) return -1;
            try
            {
                if (!HasTableHandle(db, tableName)) return -1;
                return ReadTableRowCountHandle(db, tableName);
            }
            finally
            {
                if (db != IntPtr.Zero)
                {
                    try { sqlite3_close(db); } catch { }
                }
            }
        }

        private static long ReadTableRowCountHandle(IntPtr db, string tableName)
        {
            if (db == IntPtr.Zero || !IsSafeIdentifier(tableName)) return -1;
            IntPtr stmt = IntPtr.Zero;
            if (sqlite3_prepare_v2(db, ToUtf8Z("SELECT COUNT(*) FROM \"" + tableName + "\";"), -1, out stmt, IntPtr.Zero) != SQLITE_OK || stmt == IntPtr.Zero)
                return -1;
            try
            {
                if (sqlite3_step(stmt) == SQLITE_ROW)
                    return sqlite3_column_int64(stmt, 0);
                return 0;
            }
            finally
            {
                sqlite3_finalize(stmt);
            }
        }

        private static bool HasTable(string dbPath, string tableName)
        {
            IntPtr db = IntPtr.Zero;
            if (!TryOpenReadOnly(dbPath, out db)) return false;
            try
            {
                return HasTableHandle(db, tableName);
            }
            finally
            {
                if (db != IntPtr.Zero)
                {
                    try { sqlite3_close(db); } catch { }
                }
            }
        }

        private static bool HasTableHandle(IntPtr db, string tableName)
        {
            if (db == IntPtr.Zero || string.IsNullOrEmpty(tableName)) return false;
            IntPtr stmt = IntPtr.Zero;
            if (sqlite3_prepare_v2(db, ToUtf8Z("SELECT 1 FROM sqlite_master WHERE type='table' AND name='" + tableName.Replace("'", "''") + "' LIMIT 1;"), -1, out stmt, IntPtr.Zero) != SQLITE_OK || stmt == IntPtr.Zero)
                return false;
            try
            {
                return sqlite3_step(stmt) == SQLITE_ROW;
            }
            finally
            {
                sqlite3_finalize(stmt);
            }
        }

        private static ConversationSummaryInfo BuildSummaryForTempDb(string dbPath, string cid)
        {
            string metaErr;
            SessionDbMetadata meta = SqliteSessionStore.ReadSessionMetadata(dbPath, out metaErr);
            ConversationSummaryInfo s = new ConversationSummaryInfo();
            s.ConversationId = cid;
            s.DisplayTitle = (meta != null && !string.IsNullOrEmpty(meta.DisplayTitle)) ? meta.DisplayTitle : cid;
            s.ProjectId = meta != null ? meta.ProjectId : string.Empty;
            s.FolderUri = meta != null ? meta.WorkspaceUri : string.Empty;
            s.StepCount = meta != null ? meta.StepCount : 0;
            s.IsSubagent = true;
            return s;
        }

        private static bool TryOpenReadOnly(string dbPath, out IntPtr db)
        {
            db = IntPtr.Zero;
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath)) return false;
            int rc = sqlite3_open_v2(ToUtf8Z(dbPath), out db, SQLITE_OPEN_READONLY, IntPtr.Zero);
            if (rc != SQLITE_OK || db == IntPtr.Zero)
            {
                if (db != IntPtr.Zero)
                {
                    try { sqlite3_close(db); } catch { }
                    db = IntPtr.Zero;
                }
                return false;
            }
            sqlite3_busy_timeout(db, BusyTimeoutMs);
            return true;
        }

        private static byte[] ToUtf8Z(string s)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(s ?? string.Empty);
            byte[] z = new byte[utf8.Length + 1];
            Buffer.BlockCopy(utf8, 0, z, 0, utf8.Length);
            return z;
        }

        private static string ReadColumnText(IntPtr stmt, int col)
        {
            if (sqlite3_column_type(stmt, col) == SQLITE_NULL) return null;
            IntPtr ptr = sqlite3_column_text(stmt, col);
            if (ptr == IntPtr.Zero) return string.Empty;
            int len = sqlite3_column_bytes(stmt, col);
            if (len <= 0) return string.Empty;
            byte[] buf = new byte[len];
            Marshal.Copy(ptr, buf, 0, len);
            return Encoding.UTF8.GetString(buf);
        }

        private static bool IsSafeIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok) return false;
            }
            return true;
        }

        private static void ResolveProjectBucket(
            ConversationSummaryInfo s,
            Dictionary<string, ProjectMetadata> localProjects,
            out string bucketKey,
            out string projectId,
            out string projectName)
        {
            if (s.IsCliSession || string.Equals(s.ProjectId, "default-cli-project", StringComparison.OrdinalIgnoreCase))
            {
                bucketKey = "CLI:default-cli-project";
                projectId = "default-cli-project";
                projectName = "CLI 命令行会话";
                return;
            }

            if (VisualDiffEngine.IsOutsideProject(s.ProjectName) ||
                string.Equals(s.ProjectId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
            {
                bucketKey = "OUTSIDE:outside-of-project";
                projectId = "outside-of-project";
                projectName = "Outside of Project";
                return;
            }

            projectId = !string.IsNullOrEmpty(s.ProjectId) ? s.ProjectId.Trim() : string.Empty;
            projectName = !string.IsNullOrWhiteSpace(s.ProjectName) ? s.ProjectName.Trim() : "未命名工程";
            if (!string.IsNullOrEmpty(projectId) && localProjects != null && localProjects.ContainsKey(projectId))
            {
                if (!string.IsNullOrWhiteSpace(localProjects[projectId].Name))
                    projectName = localProjects[projectId].Name.Trim();
            }

            bucketKey = !string.IsNullOrEmpty(projectId)
                ? ("ID:" + projectId)
                : ("NAME:" + projectName + "|" + (s.FolderUri ?? string.Empty));
        }

        private static string LocateSessionFile(string primaryRoot, string secondaryRoot, string subDir, string fileName)
        {
            string p1 = Path.Combine(primaryRoot, subDir, fileName);
            if (File.Exists(p1)) return p1;
            string p2 = Path.Combine(secondaryRoot, subDir, fileName);
            if (File.Exists(p2)) return p2;
            return null;
        }

        private static string LocateSessionDir(string primaryRoot, string secondaryRoot, string subDir, string dirName)
        {
            string d1 = Path.Combine(primaryRoot, subDir, dirName);
            if (Directory.Exists(d1)) return d1;
            string d2 = Path.Combine(secondaryRoot, subDir, dirName);
            if (Directory.Exists(d2)) return d2;
            return null;
        }

        private static long GetFileSizeSafe(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return new FileInfo(path).Length;
            }
            catch { }
            return 0;
        }

        private static long GetDirectorySizeSafe(string dirPath)
        {
            long sum = 0;
            try
            {
                if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath)) return 0;
                string[] files = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    sum += GetFileSizeSafe(files[i]);
                }
            }
            catch { }
            return sum;
        }

        private static bool DeleteFileQuiet(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool DeleteDirectoryQuiet(string dirPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(dirPath) && Directory.Exists(dirPath))
                {
                    string[] files = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
                    for (int i = 0; i < files.Length; i++)
                    {
                        try { File.SetAttributes(files[i], FileAttributes.Normal); } catch { }
                    }
                    Directory.Delete(dirPath, true);
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}
