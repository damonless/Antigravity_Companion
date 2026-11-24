using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // SUMMARY.SQLITE BRIDGE (Read, Search, Transactional Update/Rollback/GC/Upsert/Backup)
    // Strictly C# 5.0 / winsqlite3.dll P/Invoke
    // ══════════════════════════════════════════════════════════════════════

    public class SummaryDbRowInfo
    {
        public string ConversationId;
        public string Title;
        public string Preview;
        public int StepCount;
        public string LastModifiedTime;
        public string WorkspaceUris;
        public string Status;
        public string ProjectId;
        public string AgentName;
        public string ParentConversationId;
        public int NestingDepth;
        public bool NotFullyIdle;
        public string FirstPromptSnippet;
        public string GitBranchHint;
        public List<KeyValuePair<string, string>> RepoBranchHints;
        public string AppDataDir;
        public string Source;
        public byte[] RawSummaryBytes;
    }

    public class SummaryDbRollbackEntry
    {
        public string ConversationId;
        public string OldTitle;
        public string OldPreview;
        public byte[] OldRawSummary;
    }

    public class SummaryDbUpsertItem
    {
        public string ConversationId;
        public string Title;
        public string Preview;
        public int StepCount;
        public string LastModifiedTimeIso;
        public string LastUserInputTimeIso;
        public string WorkspaceUrisJson;
        public string ProjectId;
        public string ParentConversationId;
        public int NestingDepth;
        public byte[] RawSummaryBytes;
        public string ProjectName;
        public string FtsWorkspaceToken;
    }

    /// <summary>
    /// Antigravity summary.sqlite 读写桥接器（C# 5.0 / winsqlite3.dll 原生 P/Invoke）
    /// </summary>
    public static class SummarySqliteBridge
    {
        private const int SQLITE_OK = 0;
        private const int SQLITE_ROW = 100;
        private const int SQLITE_DONE = 101;
        private const int SQLITE_NULL = 5;

        private const int SQLITE_OPEN_READONLY = 0x00000001;
        private const int SQLITE_OPEN_READWRITE = 0x00000002;
        private const int SQLITE_OPEN_CREATE = 0x00000004;

        private const int BusyTimeoutMs = 5000;
        private const int MaxFirstPromptSnippetChars = 1200;
        private const int MaxSearchTerms = 64;

        private static readonly IntPtr SQLITE_TRANSIENT = new IntPtr(-1);
        private static readonly byte[] EmptyPinSentinel = new byte[1] { 0 };

        [ThreadStatic]
        private static string _lastError;

        public static string LastError
        {
            get { return _lastError; }
            private set { _lastError = value; }
        }

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

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr pStmt);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_changes", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_changes(IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_errmsg", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_errmsg(IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_exec", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_exec(IntPtr db, byte[] sql, IntPtr callback, IntPtr arg, IntPtr errMsg);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_text", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_text(IntPtr pStmt, int iCol, IntPtr zData, int nByte, IntPtr xDel);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_blob", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_blob(IntPtr pStmt, int iCol, IntPtr zData, int nData, IntPtr xDel);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_int", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_int(IntPtr pStmt, int iCol, int value);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_null", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_null(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_type", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_type(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_text(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_bytes", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_bytes(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_blob", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_blob(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_int", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_int(IntPtr pStmt, int iCol);

        private delegate int StmtBinder(IntPtr stmt);
        private delegate bool WriteTxnBody(IntPtr db, out string bodyErr);

        private static string GetSqliteErrorMessage(IntPtr db)
        {
            if (db == IntPtr.Zero) return "db is null";
            try
            {
                IntPtr ptr = sqlite3_errmsg(db);
                if (ptr == IntPtr.Zero) return "unknown sqlite error";
                int len = 0;
                while (len < 1024 && Marshal.ReadByte(ptr, len) != 0) len++;
                byte[] buf = new byte[len];
                Marshal.Copy(ptr, buf, 0, len);
                return Encoding.UTF8.GetString(buf);
            }
            catch
            {
                return "unknown sqlite error";
            }
        }

        private static bool OpenSqliteDb(string dbPath, int flags, out IntPtr db, out string err)
        {
            db = IntPtr.Zero;
            err = null;
            try
            {
                byte[] pathBytes = SqliteMetadataUpdater.ToUtf8NullTerminated(dbPath);
                int openRc = sqlite3_open_v2(pathBytes, out db, flags, IntPtr.Zero);
                if (openRc != SQLITE_OK || db == IntPtr.Zero)
                {
                    err = "sqlite3_open_v2 失败 (" + openRc + "): " + GetSqliteErrorMessage(db);
                    if (db != IntPtr.Zero)
                    {
                        try { sqlite3_close(db); } catch {}
                        db = IntPtr.Zero;
                    }
                    return false;
                }
                sqlite3_busy_timeout(db, BusyTimeoutMs);
                return true;
            }
            catch (DllNotFoundException ex)
            {
                err = "未找到 winsqlite3.dll: " + ex.Message;
                db = IntPtr.Zero;
                return false;
            }
            catch (Exception ex)
            {
                err = ex.Message;
                if (db != IntPtr.Zero)
                {
                    try { sqlite3_close(db); } catch {}
                    db = IntPtr.Zero;
                }
                return false;
            }
        }

        private static bool ExecSql(IntPtr db, string sql, out string err)
        {
            err = null;
            byte[] sqlBytes = SqliteMetadataUpdater.ToUtf8NullTerminated(sql);
            int rc = sqlite3_exec(db, sqlBytes, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (rc != SQLITE_OK)
            {
                err = "SQL 执行失败 (" + rc + "): " + GetSqliteErrorMessage(db);
                return false;
            }
            return true;
        }

        private static bool PrepareStmt(IntPtr db, string sql, out IntPtr stmt, out string err)
        {
            stmt = IntPtr.Zero;
            err = null;
            byte[] sqlBytes = SqliteMetadataUpdater.ToUtf8NullTerminated(sql);
            int rc = sqlite3_prepare_v2(db, sqlBytes, -1, out stmt, IntPtr.Zero);
            if (rc != SQLITE_OK || stmt == IntPtr.Zero)
            {
                err = "SQL prepare 失败 (" + rc + "): " + GetSqliteErrorMessage(db);
                if (stmt != IntPtr.Zero)
                {
                    sqlite3_finalize(stmt);
                    stmt = IntPtr.Zero;
                }
                return false;
            }
            return true;
        }

        private static int BindText(IntPtr stmt, int col, string val)
        {
            if (val == null)
            {
                return sqlite3_bind_null(stmt, col);
            }
            byte[] utf8 = Encoding.UTF8.GetBytes(val);
            byte[] pinnedArr = utf8.Length > 0 ? utf8 : EmptyPinSentinel;
            GCHandle pin = GCHandle.Alloc(pinnedArr, GCHandleType.Pinned);
            try
            {
                return sqlite3_bind_text(stmt, col, pin.AddrOfPinnedObject(), utf8.Length, SQLITE_TRANSIENT);
            }
            finally
            {
                if (pin.IsAllocated) pin.Free();
            }
        }

        private static int BindBlob(IntPtr stmt, int col, byte[] data)
        {
            if (data == null)
            {
                return sqlite3_bind_null(stmt, col);
            }
            byte[] pinnedArr = data.Length > 0 ? data : EmptyPinSentinel;
            GCHandle pin = GCHandle.Alloc(pinnedArr, GCHandleType.Pinned);
            try
            {
                return sqlite3_bind_blob(stmt, col, pin.AddrOfPinnedObject(), data.Length, SQLITE_TRANSIENT);
            }
            finally
            {
                if (pin.IsAllocated) pin.Free();
            }
        }

        private static string ReadColumnString(IntPtr stmt, int col)
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

        private static byte[] ReadColumnBlob(IntPtr stmt, int col)
        {
            if (sqlite3_column_type(stmt, col) == SQLITE_NULL) return null;
            IntPtr ptr = sqlite3_column_blob(stmt, col);
            int len = sqlite3_column_bytes(stmt, col);
            if (len <= 0 || ptr == IntPtr.Zero) return new byte[0];
            byte[] buf = new byte[len];
            Marshal.Copy(ptr, buf, 0, len);
            return buf;
        }

        private static int ReadColumnInt(IntPtr stmt, int col, int defaultValue)
        {
            if (sqlite3_column_type(stmt, col) == SQLITE_NULL) return defaultValue;
            return sqlite3_column_int(stmt, col);
        }

        private static bool ExecuteNonQuery(IntPtr db, string sql, StmtBinder binder, out int changes, out string err)
        {
            changes = 0;
            err = null;
            IntPtr stmt = IntPtr.Zero;
            if (!PrepareStmt(db, sql, out stmt, out err)) return false;
            try
            {
                if (binder != null)
                {
                    int bindRc = binder(stmt);
                    if (bindRc != SQLITE_OK)
                    {
                        err = "SQL bind 失败 (" + bindRc + "): " + GetSqliteErrorMessage(db);
                        return false;
                    }
                }
                int stepRc = sqlite3_step(stmt);
                if (stepRc != SQLITE_DONE)
                {
                    err = "SQL step 失败 (" + stepRc + "): " + GetSqliteErrorMessage(db);
                    return false;
                }
                changes = sqlite3_changes(db);
                return true;
            }
            finally
            {
                if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
            }
        }

        private static bool TableExists(IntPtr db, string tableName)
        {
            if (db == IntPtr.Zero || string.IsNullOrEmpty(tableName)) return false;
            IntPtr stmt = IntPtr.Zero;
            string err;
            if (!PrepareStmt(db, "SELECT 1 FROM sqlite_master WHERE type IN ('table', 'view') AND name = ? LIMIT 1;", out stmt, out err))
                return false;
            try
            {
                if (BindText(stmt, 1, tableName) != SQLITE_OK) return false;
                return sqlite3_step(stmt) == SQLITE_ROW;
            }
            finally
            {
                if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
            }
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

        private static HashSet<string> GetTableColumns(IntPtr db, string tableName)
        {
            HashSet<string> cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (db == IntPtr.Zero || !IsSafeIdentifier(tableName)) return cols;
            IntPtr stmt = IntPtr.Zero;
            string err;
            if (!PrepareStmt(db, "PRAGMA table_info(" + tableName + ");", out stmt, out err))
                return cols;
            try
            {
                while (sqlite3_step(stmt) == SQLITE_ROW)
                {
                    string colName = ReadColumnString(stmt, 1);
                    if (!string.IsNullOrEmpty(colName)) cols.Add(colName.Trim());
                }
            }
            finally
            {
                if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
            }
            return cols;
        }

        private static void RollbackQuietly(IntPtr db)
        {
            if (db == IntPtr.Zero) return;
            try
            {
                string dummy;
                ExecSql(db, "ROLLBACK;", out dummy);
            }
            catch {}
        }

        private static bool ExecuteWriteTransaction(string dbPath, WriteTxnBody body, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                error = "数据库文件不存在: " + dbPath;
                return false;
            }

            IntPtr db = IntPtr.Zero;
            bool inTxn = false;
            try
            {
                string err;
                if (!OpenSqliteDb(dbPath, SQLITE_OPEN_READWRITE, out db, out err))
                {
                    error = err;
                    return false;
                }

                if (!ExecSql(db, "BEGIN IMMEDIATE;", out err))
                {
                    error = err;
                    return false;
                }
                inTxn = true;

                string bodyErr;
                if (!body(db, out bodyErr))
                {
                    error = bodyErr;
                    RollbackQuietly(db);
                    inTxn = false;
                    return false;
                }

                if (!ExecSql(db, "COMMIT;", out err))
                {
                    error = err;
                    RollbackQuietly(db);
                    inTxn = false;
                    return false;
                }
                inTxn = false;
                return true;
            }
            catch (DllNotFoundException ex)
            {
                error = "未找到 winsqlite3.dll: " + ex.Message;
                if (inTxn) RollbackQuietly(db);
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                if (inTxn) RollbackQuietly(db);
                return false;
            }
            finally
            {
                if (db != IntPtr.Zero)
                {
                    try { sqlite3_close(db); } catch {}
                }
            }
        }

        private static List<string> CollectDistinctCids(IEnumerable<string> cids)
        {
            List<string> list = new List<string>();
            if (cids == null) return list;
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in cids)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                string trimmed = raw.Trim();
                if (seen.Add(trimmed)) list.Add(trimmed);
            }
            return list;
        }

        public static List<KeyValuePair<string, string>> ExtractGitBranchCandidatesFromRawSummary(byte[] rawSummary)
        {
            List<KeyValuePair<string, string>> results = new List<KeyValuePair<string, string>>();
            if (rawSummary == null || rawSummary.Length == 0) return results;
            try
            {
                List<ProtoNode> nodes = ProtoCodec.Decode(rawSummary);
                ProtoCodec.DecodeMetaContainerList(nodes);
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < nodes.Count; i++)
                {
                    ProtoNode f9 = nodes[i];
                    if (f9.FieldNumber != 9 || f9.WireType != 2) continue;
                    if (!f9.HasChildren && f9.RawBytes != null && f9.RawBytes.Length > 0)
                    {
                        List<ProtoNode> sub9;
                        if (ProtoCodec.TryDecodeStrict(f9.RawBytes, out sub9)) f9.Children = sub9;
                    }
                    if (!f9.HasChildren) continue;

                    string repoPath = string.Empty;
                    string directBranch = null;
                    string legacyNestedBranch = null;

                    for (int j = 0; j < f9.Children.Count; j++)
                    {
                        ProtoNode ch = f9.Children[j];
                        if (ch.FieldNumber == 4 && ch.WireType == 2 && directBranch == null)
                        {
                            string b4;
                            if (ch.TryGetStrictUtf8String(out b4) && !string.IsNullOrWhiteSpace(b4))
                            {
                                directBranch = b4.Trim();
                            }
                        }
                        else if (ch.FieldNumber == 3 && ch.WireType == 2)
                        {
                            List<ProtoNode> sub3 = ch.HasChildren ? ch.Children : null;
                            if (sub3 == null && ch.RawBytes != null && ch.RawBytes.Length > 0)
                            {
                                ProtoCodec.TryDecodeStrict(ch.RawBytes, out sub3);
                            }
                            if (sub3 != null)
                            {
                                for (int k = 0; k < sub3.Count; k++)
                                {
                                    ProtoNode inner = sub3[k];
                                    if (inner.FieldNumber == 1 && inner.WireType == 2 && string.IsNullOrEmpty(repoPath))
                                    {
                                        string rp;
                                        if (inner.TryGetStrictUtf8String(out rp) && !string.IsNullOrWhiteSpace(rp))
                                            repoPath = rp.Trim();
                                    }
                                    else if (inner.FieldNumber == 4 && inner.WireType == 2 && legacyNestedBranch == null)
                                    {
                                        string b34;
                                        if (inner.TryGetStrictUtf8String(out b34) && !string.IsNullOrWhiteSpace(b34))
                                            legacyNestedBranch = b34.Trim();
                                    }
                                }
                            }
                        }
                    }

                    string effectiveBranch = !string.IsNullOrEmpty(directBranch) ? directBranch : legacyNestedBranch;
                    if (!string.IsNullOrEmpty(effectiveBranch))
                    {
                        string dedup = (repoPath ?? string.Empty) + "|" + effectiveBranch;
                        if (seen.Add(dedup))
                        {
                            results.Add(new KeyValuePair<string, string>(repoPath ?? string.Empty, effectiveBranch));
                        }
                    }
                }
            }
            catch {}
            return results;
        }

        public static Dictionary<string, SummaryDbRowInfo> ReadSummaryDbSnapshot(string dbPath)
        {
            string err;
            Dictionary<string, SummaryDbRowInfo> res = ReadSummaryDbSnapshot(dbPath, out err);
            LastError = err;
            return res;
        }

        /// <summary>
        /// 只读读取 summary.sqlite 的 conversation_summaries 快照（附带 GitBranchHint 与 FTS 首条提示词摘要）
        /// </summary>
        public static Dictionary<string, SummaryDbRowInfo> ReadSummaryDbSnapshot(string dbPath, out string error)
        {
            error = null;
            LastError = null;
            Dictionary<string, SummaryDbRowInfo> snapshot = new Dictionary<string, SummaryDbRowInfo>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                error = "数据库文件不存在: " + dbPath;
                LastError = error;
                return snapshot;
            }
            try
            {
                if (new FileInfo(dbPath).Length == 0) return snapshot;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                LastError = error;
                return snapshot;
            }

            IntPtr db = IntPtr.Zero;
            try
            {
                string err;
                if (!OpenSqliteDb(dbPath, SQLITE_OPEN_READONLY, out db, out err))
                {
                    error = err;
                    LastError = error;
                    return snapshot;
                }

                HashSet<string> tableCols = GetTableColumns(db, "conversation_summaries");
                if (tableCols.Count == 0 || !tableCols.Contains("conversation_id"))
                {
                    error = "conversation_summaries 表不存在或缺少 conversation_id 列。";
                    LastError = error;
                    return snapshot;
                }

                string[] knownCols = new string[]
                {
                    "conversation_id", "title", "preview", "step_count", "last_modified_time",
                    "workspace_uris", "status", "project_id", "agent_name", "parent_conversation_id",
                    "nesting_depth", "not_fully_idle", "raw_summary", "app_data_dir", "source"
                };
                int[] colPos = new int[knownCols.Length];
                List<string> selectedCols = new List<string>(knownCols.Length);
                for (int i = 0; i < knownCols.Length; i++)
                {
                    if (tableCols.Contains(knownCols[i]))
                    {
                        colPos[i] = selectedCols.Count;
                        selectedCols.Add(knownCols[i]);
                    }
                    else
                    {
                        colPos[i] = -1;
                    }
                }

                IntPtr stmt = IntPtr.Zero;
                string sql = "SELECT " + string.Join(", ", selectedCols.ToArray()) + " FROM conversation_summaries;";
                if (!PrepareStmt(db, sql, out stmt, out err))
                {
                    error = err;
                    LastError = error;
                    return snapshot;
                }

                try
                {
                    int stepRc;
                    while ((stepRc = sqlite3_step(stmt)) == SQLITE_ROW)
                    {
                        string cid = ReadColumnString(stmt, colPos[0]);
                        if (string.IsNullOrWhiteSpace(cid)) continue;
                        cid = cid.Trim();

                        SummaryDbRowInfo row = new SummaryDbRowInfo();
                        row.ConversationId = cid;
                        if (colPos[1] >= 0) row.Title = ReadColumnString(stmt, colPos[1]);
                        if (colPos[2] >= 0) row.Preview = ReadColumnString(stmt, colPos[2]);
                        if (colPos[3] >= 0) row.StepCount = ReadColumnInt(stmt, colPos[3], 0);
                        if (colPos[4] >= 0) row.LastModifiedTime = ReadColumnString(stmt, colPos[4]);
                        if (colPos[5] >= 0) row.WorkspaceUris = ReadColumnString(stmt, colPos[5]);
                        if (colPos[6] >= 0) row.Status = ReadColumnString(stmt, colPos[6]);
                        if (colPos[7] >= 0) row.ProjectId = ReadColumnString(stmt, colPos[7]);
                        if (colPos[8] >= 0) row.AgentName = ReadColumnString(stmt, colPos[8]);
                        if (colPos[9] >= 0) row.ParentConversationId = ReadColumnString(stmt, colPos[9]);
                        if (colPos[10] >= 0) row.NestingDepth = ReadColumnInt(stmt, colPos[10], 0);
                        if (colPos[11] >= 0) row.NotFullyIdle = ReadColumnInt(stmt, colPos[11], 0) != 0;
                        if (colPos[12] >= 0) row.RawSummaryBytes = ReadColumnBlob(stmt, colPos[12]);
                        if (colPos[13] >= 0) row.AppDataDir = ReadColumnString(stmt, colPos[13]);
                        if (colPos[14] >= 0) row.Source = ReadColumnString(stmt, colPos[14]);

                        row.RepoBranchHints = ExtractGitBranchCandidatesFromRawSummary(row.RawSummaryBytes);
                        if (row.RepoBranchHints.Count > 0)
                        {
                            string nonMain = null;
                            for (int bi = 0; bi < row.RepoBranchHints.Count; bi++)
                            {
                                string cand = row.RepoBranchHints[bi].Value;
                                if (!string.IsNullOrEmpty(cand) &&
                                    !string.Equals(cand, "main", StringComparison.OrdinalIgnoreCase) &&
                                    !string.Equals(cand, "master", StringComparison.OrdinalIgnoreCase))
                                {
                                    nonMain = cand;
                                    break;
                                }
                            }
                            row.GitBranchHint = nonMain ?? row.RepoBranchHints[0].Value;
                        }

                        snapshot[cid] = row;
                    }
                    if (stepRc != SQLITE_DONE)
                    {
                        error = "读取 conversation_summaries 中断 (" + stepRc + "): " + GetSqliteErrorMessage(db);
                        LastError = error;
                    }
                }
                finally
                {
                    if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
                }

                if (TableExists(db, "conversations_fts"))
                {
                    IntPtr ftsStmt = IntPtr.Zero;
                    string ftsErr;
                    if (PrepareStmt(db, "SELECT conversation_id, user_body FROM conversations_fts WHERE chunk_index = 0;", out ftsStmt, out ftsErr))
                    {
                        try
                        {
                            while (sqlite3_step(ftsStmt) == SQLITE_ROW)
                            {
                                string cid = ReadColumnString(ftsStmt, 0);
                                if (string.IsNullOrWhiteSpace(cid)) continue;
                                SummaryDbRowInfo row;
                                if (!snapshot.TryGetValue(cid.Trim(), out row) || !string.IsNullOrEmpty(row.FirstPromptSnippet))
                                    continue;
                                string body = ReadColumnString(ftsStmt, 1);
                                if (!string.IsNullOrEmpty(body))
                                {
                                    row.FirstPromptSnippet = body.Length > MaxFirstPromptSnippetChars
                                        ? body.Substring(0, MaxFirstPromptSnippetChars)
                                        : body;
                                }
                            }
                        }
                        finally
                        {
                            if (ftsStmt != IntPtr.Zero) sqlite3_finalize(ftsStmt);
                        }
                    }
                }

                return snapshot;
            }
            catch (DllNotFoundException ex)
            {
                error = "未找到 winsqlite3.dll: " + ex.Message;
                LastError = error;
                return snapshot;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                LastError = error;
                return snapshot;
            }
            finally
            {
                if (db != IntPtr.Zero)
                {
                    try { sqlite3_close(db); } catch {}
                }
            }
        }

        public static HashSet<string> ReadAllConversationIds(string dbPath)
        {
            string err;
            HashSet<string> ids = ReadAllConversationIds(dbPath, out err);
            LastError = err;
            return ids;
        }

        /// <summary>
        /// 读取全量活跃会话 ID（失败封闭：若非空库且打开/查询失败则抛 InvalidOperationException 防止孤儿误删）
        /// </summary>
        public static HashSet<string> ReadAllConversationIds(string dbPath, out string error)
        {
            error = null;
            LastError = null;
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
                return ids;

            FileInfo fi = new FileInfo(dbPath);
            if (fi.Length == 0)
                return ids;

            IntPtr db = IntPtr.Zero;
            IntPtr stmt = IntPtr.Zero;
            try
            {
                string err;
                if (!OpenSqliteDb(dbPath, SQLITE_OPEN_READONLY, out db, out err))
                {
                    error = err;
                    LastError = error;
                    throw new InvalidOperationException("无法只读打开 summary.sqlite: " + err);
                }

                if (!PrepareStmt(db, "SELECT conversation_id FROM conversation_summaries;", out stmt, out err))
                {
                    error = err;
                    LastError = error;
                    throw new InvalidOperationException("查询 conversation_summaries 失败: " + err);
                }

                int stepRc;
                while ((stepRc = sqlite3_step(stmt)) == SQLITE_ROW)
                {
                    string cid = ReadColumnString(stmt, 0);
                    if (!string.IsNullOrWhiteSpace(cid))
                    {
                        ids.Add(cid.Trim());
                    }
                }
                if (stepRc != SQLITE_DONE)
                {
                    error = "sqlite3_step 未正常完成 (" + stepRc + "): " + GetSqliteErrorMessage(db);
                    LastError = error;
                    throw new InvalidOperationException("遍历 conversation_summaries 失败: " + error);
                }
                return ids;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (DllNotFoundException ex)
            {
                error = "未找到 winsqlite3.dll: " + ex.Message;
                LastError = error;
                throw new InvalidOperationException(error, ex);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                LastError = error;
                throw new InvalidOperationException("读取 summary.sqlite 会话 ID 失败: " + ex.Message, ex);
            }
            finally
            {
                if (stmt != IntPtr.Zero) try { sqlite3_finalize(stmt); } catch {}
                if (db != IntPtr.Zero) try { sqlite3_close(db); } catch {}
            }
        }

        public static bool UpdateSummaryTitlesTransaction(
            string dbPath,
            Dictionary<string, string> cidToNewTitle,
            out Dictionary<string, SummaryDbRollbackEntry> rollbackEntries,
            out List<string> warnings)
        {
            string err;
            bool ok = UpdateSummaryTitlesTransaction(dbPath, cidToNewTitle, out rollbackEntries, out warnings, out err);
            LastError = err;
            return ok;
        }

        /// <summary>
        /// 事务级批量更新标题与 raw_summary AST (Field 1 + Field 15->1) 及 FTS 标题，并产出可精确回滚快照
        /// </summary>
        public static bool UpdateSummaryTitlesTransaction(
            string dbPath,
            Dictionary<string, string> cidToNewTitle,
            out Dictionary<string, SummaryDbRollbackEntry> rollbackEntries,
            out List<string> warnings,
            out string error)
        {
            Dictionary<string, SummaryDbRollbackEntry> rbMap = new Dictionary<string, SummaryDbRollbackEntry>(StringComparer.OrdinalIgnoreCase);
            List<string> warnList = new List<string>();
            rollbackEntries = rbMap;
            warnings = warnList;
            error = null;
            LastError = null;

            if (cidToNewTitle == null || cidToNewTitle.Count == 0)
                return true;

            bool ok = ExecuteWriteTransaction(dbPath, delegate(IntPtr db, out string txnErr)
            {
                txnErr = null;
                bool hasFts = TableExists(db, "conversations_fts");
                HashSet<string> ftsCols = hasFts ? GetTableColumns(db, "conversations_fts") : null;
                foreach (KeyValuePair<string, string> kvp in cidToNewTitle)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value))
                    {
                        warnList.Add("跳过空 CID 或空标题。");
                        continue;
                    }
                    string cid = kvp.Key.Trim();
                    string newTitle = kvp.Value.Trim();

                    string oldTitle = null;
                    string oldPreview = null;
                    byte[] oldRaw = null;
                    bool foundRow = false;

                    IntPtr selStmt = IntPtr.Zero;
                    string err;
                    if (!PrepareStmt(db, "SELECT title, preview, raw_summary FROM conversation_summaries WHERE conversation_id = ? LIMIT 1;", out selStmt, out err))
                    {
                        txnErr = err;
                        return false;
                    }
                    try
                    {
                        if (BindText(selStmt, 1, cid) != SQLITE_OK)
                        {
                            txnErr = "SELECT title 参数绑定失败: " + GetSqliteErrorMessage(db);
                            return false;
                        }
                        int stepRc = sqlite3_step(selStmt);
                        if (stepRc == SQLITE_ROW)
                        {
                            foundRow = true;
                            oldTitle = ReadColumnString(selStmt, 0);
                            oldPreview = ReadColumnString(selStmt, 1);
                            oldRaw = ReadColumnBlob(selStmt, 2);
                        }
                        else if (stepRc != SQLITE_DONE)
                        {
                            txnErr = "SELECT title 读取失败 (" + stepRc + "): " + GetSqliteErrorMessage(db);
                            return false;
                        }
                    }
                    finally
                    {
                        if (selStmt != IntPtr.Zero) sqlite3_finalize(selStmt);
                    }

                    if (!foundRow)
                    {
                        warnList.Add("conversation_summaries 未找到会话: " + cid);
                        continue;
                    }

                    SummaryDbRollbackEntry rb = new SummaryDbRollbackEntry();
                    rb.ConversationId = cid;
                    rb.OldTitle = oldTitle;
                    rb.OldPreview = oldPreview;
                    rb.OldRawSummary = oldRaw;
                    rbMap[cid] = rb;

                    byte[] newRaw = oldRaw;
                    if (oldRaw != null && oldRaw.Length > 0)
                    {
                        try
                        {
                            List<ProtoNode> nodes = ProtoCodec.Decode(oldRaw);
                            ProtoCodec.DecodeMetaContainerList(nodes);
                            ProtoAstCodec.SyncTitlesInMetaChildren(nodes, newTitle, true, true);
                            newRaw = ProtoCodec.Encode(nodes);
                        }
                        catch (Exception ex)
                        {
                            warnList.Add("raw_summary 标题 AST 同步失败 (" + cid + "): " + ex.Message);
                            newRaw = oldRaw;
                        }
                    }

                    int changes;
                    if (!ExecuteNonQuery(
                        db,
                        "UPDATE conversation_summaries SET title = ?, preview = ?, raw_summary = ? WHERE conversation_id = ?;",
                        delegate(IntPtr uStmt)
                        {
                            int rc = BindText(uStmt, 1, newTitle);
                            if (rc != SQLITE_OK) return rc;
                            rc = BindText(uStmt, 2, newTitle);
                            if (rc != SQLITE_OK) return rc;
                            rc = BindBlob(uStmt, 3, newRaw);
                            if (rc != SQLITE_OK) return rc;
                            return BindText(uStmt, 4, cid);
                        },
                        out changes,
                        out err))
                    {
                        txnErr = err;
                        return false;
                    }

                    if (hasFts && ftsCols != null)
                    {
                        string ftsErr;
                        if (!UpdateFtsBindingMetadata(db, ftsCols, cid, newTitle, null, null, true, false, out ftsErr))
                        {
                            txnErr = ftsErr;
                            return false;
                        }
                    }
                }
                return true;
            }, out error);

            LastError = error;
            if (!ok)
            {
                rbMap.Clear();
            }
            return ok;
        }

        public static bool RestoreSummaryRowsTransaction(
            string dbPath,
            Dictionary<string, SummaryDbRollbackEntry> restoreMap,
            out List<string> warnings)
        {
            string err;
            bool ok = RestoreSummaryRowsTransaction(dbPath, restoreMap, out warnings, out err);
            LastError = err;
            return ok;
        }

        /// <summary>
        /// 事务级精确还原标题、preview、raw_summary 字节及 FTS 标题
        /// </summary>
        public static bool RestoreSummaryRowsTransaction(
            string dbPath,
            Dictionary<string, SummaryDbRollbackEntry> restoreMap,
            out List<string> warnings,
            out string error)
        {
            List<string> warnList = new List<string>();
            warnings = warnList;
            error = null;
            LastError = null;

            if (restoreMap == null || restoreMap.Count == 0)
                return true;

            bool ok = ExecuteWriteTransaction(dbPath, delegate(IntPtr db, out string txnErr)
            {
                txnErr = null;
                bool hasFts = TableExists(db, "conversations_fts");
                HashSet<string> ftsCols = hasFts ? GetTableColumns(db, "conversations_fts") : null;
                foreach (KeyValuePair<string, SummaryDbRollbackEntry> kvp in restoreMap)
                {
                    SummaryDbRollbackEntry entry = kvp.Value;
                    if (entry == null) continue;
                    string cid = !string.IsNullOrEmpty(entry.ConversationId) ? entry.ConversationId.Trim() : kvp.Key;
                    if (string.IsNullOrWhiteSpace(cid)) continue;

                    int changes;
                    string err;
                    if (!ExecuteNonQuery(
                        db,
                        "UPDATE conversation_summaries SET title = ?, preview = ?, raw_summary = ? WHERE conversation_id = ?;",
                        delegate(IntPtr uStmt)
                        {
                            int rc = BindText(uStmt, 1, entry.OldTitle);
                            if (rc != SQLITE_OK) return rc;
                            rc = BindText(uStmt, 2, entry.OldPreview);
                            if (rc != SQLITE_OK) return rc;
                            rc = BindBlob(uStmt, 3, entry.OldRawSummary);
                            if (rc != SQLITE_OK) return rc;
                            return BindText(uStmt, 4, cid);
                        },
                        out changes,
                        out err))
                    {
                        txnErr = err;
                        return false;
                    }

                    if (changes == 0)
                    {
                        warnList.Add("回滚未命中 conversation_summaries 行: " + cid);
                    }

                    if (hasFts && ftsCols != null)
                    {
                        string ftsErr;
                        if (!UpdateFtsBindingMetadata(db, ftsCols, cid, entry.OldTitle, null, null, true, false, out ftsErr))
                        {
                            txnErr = ftsErr;
                            return false;
                        }
                    }
                }
                return true;
            }, out error);

            LastError = error;
            return ok;
        }

        private static bool CanUseFts5Match(List<string> tokens)
        {
            if (tokens == null || tokens.Count == 0) return false;
            for (int i = 0; i < tokens.Count; i++)
            {
                string tok = tokens[i];
                if (tok.Length < 2) return false;
                if (tok == "AND" || tok == "OR" || tok == "NOT" || tok == "NEAR") return false;
                bool hasAlnum = false;
                for (int j = 0; j < tok.Length; j++)
                {
                    char c = tok[j];
                    bool isAlnum = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                    if (isAlnum) hasAlnum = true;
                    else if (c != '_') return false;
                }
                if (!hasAlnum) return false;
            }
            return true;
        }

        private static string EscapeSqlLikePattern(string term)
        {
            StringBuilder sb = new StringBuilder(term.Length + 4);
            sb.Append('%');
            for (int i = 0; i < term.Length; i++)
            {
                char c = term[i];
                if (c == '\\' || c == '%' || c == '_')
                {
                    sb.Append('\\');
                }
                sb.Append(c);
            }
            sb.Append('%');
            return sb.ToString();
        }

        private static bool QueryLikeAcrossColumns(
            IntPtr db,
            string tableName,
            bool distinct,
            List<string> searchCols,
            List<string> terms,
            HashSet<string> hits,
            out string err)
        {
            err = null;
            if (searchCols == null || searchCols.Count == 0 || terms == null || terms.Count == 0)
                return true;

            StringBuilder sql = new StringBuilder();
            sql.Append(distinct ? "SELECT DISTINCT conversation_id FROM " : "SELECT conversation_id FROM ");
            sql.Append(tableName);
            sql.Append(" WHERE ");

            List<string> patterns = new List<string>(terms.Count);
            for (int t = 0; t < terms.Count; t++)
            {
                patterns.Add(EscapeSqlLikePattern(terms[t]));
                if (t > 0) sql.Append(" AND ");
                sql.Append("(");
                for (int c = 0; c < searchCols.Count; c++)
                {
                    if (c > 0) sql.Append(" OR ");
                    sql.Append(searchCols[c]);
                    sql.Append(" LIKE ? ESCAPE '\\'");
                }
                sql.Append(")");
            }
            sql.Append(";");

            IntPtr stmt = IntPtr.Zero;
            if (!PrepareStmt(db, sql.ToString(), out stmt, out err))
                return false;

            try
            {
                int bindIdx = 1;
                for (int t = 0; t < patterns.Count; t++)
                {
                    for (int c = 0; c < searchCols.Count; c++)
                    {
                        int rc = BindText(stmt, bindIdx++, patterns[t]);
                        if (rc != SQLITE_OK)
                        {
                            err = "LIKE 参数绑定失败 (" + rc + "): " + GetSqliteErrorMessage(db);
                            return false;
                        }
                    }
                }

                int stepRc;
                while ((stepRc = sqlite3_step(stmt)) == SQLITE_ROW)
                {
                    string cid = ReadColumnString(stmt, 0);
                    if (!string.IsNullOrWhiteSpace(cid)) hits.Add(cid.Trim());
                }
                if (stepRc != SQLITE_DONE)
                {
                    err = "LIKE 查询中断 (" + stepRc + "): " + GetSqliteErrorMessage(db);
                    return false;
                }
                return true;
            }
            finally
            {
                if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
            }
        }

        public static HashSet<string> SearchConversationsFullText(string dbPath, string rawQuery)
        {
            string err;
            HashSet<string> hits = SearchConversationsFullText(dbPath, rawQuery, out err);
            LastError = err;
            return hits;
        }

        /// <summary>
        /// 混合路由全文检索：安全 ASCII 标识符优先 FTS5 MATCH；若 FTS5 命中数为 0（如子串检索 "Graph" 命中 "CodeGraph"）
        /// 或不可用时自动降级为参数化 LIKE '%term%' ESCAPE '\' 子串检索，并联合 conversation_summaries 兜底。
        /// </summary>
        public static HashSet<string> SearchConversationsFullText(string dbPath, string rawQuery, out string error)
        {
            error = null;
            LastError = null;
            HashSet<string> hits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(rawQuery) || string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
                return hits;

            try
            {
                if (new FileInfo(dbPath).Length == 0) return hits;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                LastError = error;
                return hits;
            }

            string[] split = rawQuery.Trim().Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length == 0) return hits;

            List<string> terms = new List<string>(Math.Min(split.Length, MaxSearchTerms));
            for (int i = 0; i < split.Length && terms.Count < MaxSearchTerms; i++)
            {
                if (!string.IsNullOrEmpty(split[i])) terms.Add(split[i]);
            }
            if (terms.Count == 0) return hits;

            bool useFts5Match = CanUseFts5Match(terms);

            IntPtr db = IntPtr.Zero;
            try
            {
                string err;
                if (!OpenSqliteDb(dbPath, SQLITE_OPEN_READONLY, out db, out err))
                {
                    error = err;
                    LastError = error;
                    return hits;
                }

                if (TableExists(db, "conversations_fts"))
                {
                    HashSet<string> ftsCols = GetTableColumns(db, "conversations_fts");
                    bool ftsHasHits = false;

                    if (useFts5Match)
                    {
                        StringBuilder mq = new StringBuilder();
                        for (int i = 0; i < terms.Count; i++)
                        {
                            if (i > 0) mq.Append(" AND ");
                            mq.Append('"').Append(terms[i]).Append("\"*");
                        }

                        IntPtr ftsStmt = IntPtr.Zero;
                        string matchErr;
                        if (PrepareStmt(db, "SELECT DISTINCT conversation_id FROM conversations_fts WHERE conversations_fts MATCH ?;", out ftsStmt, out matchErr))
                        {
                            try
                            {
                                if (BindText(ftsStmt, 1, mq.ToString()) == SQLITE_OK)
                                {
                                    int ftsHitCount = 0;
                                    int stepRc;
                                    while ((stepRc = sqlite3_step(ftsStmt)) == SQLITE_ROW)
                                    {
                                        string cid = ReadColumnString(ftsStmt, 0);
                                        if (!string.IsNullOrWhiteSpace(cid))
                                        {
                                            hits.Add(cid.Trim());
                                            ftsHitCount++;
                                        }
                                    }
                                    if (stepRc == SQLITE_DONE)
                                    {
                                        // FIX P1-6: Only skip LIKE fallback if FTS5 actually returned >= 1 hit.
                                        ftsHasHits = ftsHitCount > 0;
                                    }
                                    else
                                    {
                                        error = "FTS5 MATCH 查询失败 (" + stepRc + "): " + GetSqliteErrorMessage(db);
                                        LastError = error;
                                    }
                                }
                            }
                            finally
                            {
                                if (ftsStmt != IntPtr.Zero) sqlite3_finalize(ftsStmt);
                            }
                        }
                        else
                        {
                            error = matchErr;
                            LastError = error;
                        }
                    }

                    if (!ftsHasHits && ftsCols.Contains("conversation_id"))
                    {
                        List<string> searchFtsCols = new List<string>(3);
                        if (ftsCols.Contains("title")) searchFtsCols.Add("title");
                        if (ftsCols.Contains("user_body")) searchFtsCols.Add("user_body");
                        if (ftsCols.Contains("agent_body")) searchFtsCols.Add("agent_body");

                        string likeErr;
                        if (QueryLikeAcrossColumns(db, "conversations_fts", true, searchFtsCols, terms, hits, out likeErr))
                        {
                            // If FTS5 MATCH threw a transient error (e.g. non-FTS5 table) and LIKE fallback succeeded, clear stale error.
                            error = null;
                            LastError = null;
                        }
                        else
                        {
                            error = likeErr;
                            LastError = error;
                        }
                    }
                }

                if (TableExists(db, "conversation_summaries"))
                {
                    HashSet<string> sumCols = GetTableColumns(db, "conversation_summaries");
                    if (sumCols.Contains("conversation_id"))
                    {
                        List<string> searchSumCols = new List<string>(2);
                        if (sumCols.Contains("title")) searchSumCols.Add("title");
                        if (sumCols.Contains("preview")) searchSumCols.Add("preview");

                        string sumErr;
                        if (!QueryLikeAcrossColumns(db, "conversation_summaries", false, searchSumCols, terms, hits, out sumErr))
                        {
                            error = sumErr;
                            LastError = error;
                        }
                    }
                }

                return hits;
            }
            catch (DllNotFoundException ex)
            {
                error = "未找到 winsqlite3.dll: " + ex.Message;
                LastError = error;
                return hits;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                LastError = error;
                return hits;
            }
            finally
            {
                if (db != IntPtr.Zero)
                {
                    try { sqlite3_close(db); } catch {}
                }
            }
        }

        public static int DeleteSessionsFromSummaryDb(string dbPath, IEnumerable<string> cids, out List<string> warnings)
        {
            string err;
            int cnt = DeleteSessionsFromSummaryDb(dbPath, cids, out warnings, out err);
            LastError = err;
            return cnt;
        }

        /// <summary>
        /// 事务级从 summary.sqlite 级联删除会话摘要、FTS 索引及父子关系记录
        /// </summary>
        public static int DeleteSessionsFromSummaryDb(string dbPath, IEnumerable<string> cids, out List<string> warnings, out string error)
        {
            List<string> warnList = new List<string>();
            warnings = warnList;
            error = null;
            LastError = null;

            List<string> targetCids = CollectDistinctCids(cids);
            if (targetCids.Count == 0) return 0;
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath)) return 0;

            int deletedCount = 0;
            bool ok = ExecuteWriteTransaction(dbPath, delegate(IntPtr db, out string txnErr)
            {
                txnErr = null;
                bool hasSummaries = TableExists(db, "conversation_summaries");
                bool hasFts = TableExists(db, "conversations_fts");
                bool hasParents = TableExists(db, "conversation_parents");

                for (int i = 0; i < targetCids.Count; i++)
                {
                    string cid = targetCids[i];
                    string err;
                    int changes;

                    if (hasSummaries)
                    {
                        if (!ExecuteNonQuery(
                            db,
                            "DELETE FROM conversation_summaries WHERE conversation_id = ?;",
                            delegate(IntPtr s) { return BindText(s, 1, cid); },
                            out changes,
                            out err))
                        {
                            txnErr = err;
                            return false;
                        }
                        deletedCount += changes;
                    }

                    if (hasFts)
                    {
                        if (!ExecuteNonQuery(
                            db,
                            "DELETE FROM conversations_fts WHERE conversation_id = ?;",
                            delegate(IntPtr s) { return BindText(s, 1, cid); },
                            out changes,
                            out err))
                        {
                            txnErr = err;
                            return false;
                        }
                    }

                    if (hasParents)
                    {
                        if (!ExecuteNonQuery(
                            db,
                            "DELETE FROM conversation_parents WHERE conversation_id = ? OR parent_id = ?;",
                            delegate(IntPtr s)
                            {
                                int rc = BindText(s, 1, cid);
                                if (rc != SQLITE_OK) return rc;
                                return BindText(s, 2, cid);
                            },
                            out changes,
                            out err))
                        {
                            txnErr = err;
                            return false;
                        }
                    }
                }
                return true;
            }, out error);

            LastError = error;
            if (!ok)
            {
                if (!string.IsNullOrEmpty(error)) warnList.Add(error);
                return 0;
            }
            return deletedCount;
        }

        private static byte[] ApplyProjectBindingToRawSummary(
            byte[] oldRaw,
            string effectiveProjName,
            string effectiveProjId,
            string effectiveFolderUri,
            bool isOutside,
            List<string> warnings,
            string cid)
        {
            if (oldRaw == null || oldRaw.Length == 0) return oldRaw;
            try
            {
                List<ProtoNode> nodes = ProtoCodec.Decode(oldRaw);
                ProtoCodec.DecodeMetaContainerList(nodes);

                if (isOutside)
                {
                    ProtoAstCodec.ClearWorkspaceFieldsInMetaChildren(nodes);
                    ProtoAstCodec.SyncProjectIdInMetaChildren(nodes, "outside-of-project", true);
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        if (nodes[i].FieldNumber == 17 && nodes[i].WireType == 2)
                        {
                            if (nodes[i].Children == null) nodes[i].Children = new List<ProtoNode>();
                            ProtoAstCodec.SetOrAddUtf8Leaf(nodes[i].Children, 1, string.IsNullOrEmpty(effectiveProjName) ? "Outside of Project" : effectiveProjName);
                            nodes[i].IsModified = true;
                            break;
                        }
                    }
                    return ProtoCodec.Encode(nodes);
                }

                if (!string.IsNullOrEmpty(effectiveProjId))
                {
                    ProtoAstCodec.SyncProjectIdInMetaChildren(nodes, effectiveProjId, true);
                }

                if (!string.IsNullOrEmpty(effectiveFolderUri))
                {
                    ProtoNode primaryF9 = null;
                    for (int i = nodes.Count - 1; i >= 0; i--)
                    {
                        if (nodes[i].FieldNumber == 9 && nodes[i].WireType == 2)
                        {
                            if (primaryF9 == null)
                            {
                                primaryF9 = nodes[i];
                            }
                            else
                            {
                                nodes.RemoveAt(i);
                            }
                        }
                    }

                    if (primaryF9 == null)
                    {
                        primaryF9 = new ProtoNode(9, 2) { IsModified = true };
                        ProtoAstCodec.SetOrAddUtf8Leaf(primaryF9.Children, 1, effectiveFolderUri);
                        nodes.Add(primaryF9);
                    }
                    else
                    {
                        if (!primaryF9.HasChildren && primaryF9.RawBytes != null && primaryF9.RawBytes.Length > 0)
                        {
                            List<ProtoNode> sub9;
                            if (ProtoCodec.TryDecodeStrict(primaryF9.RawBytes, out sub9))
                            {
                                primaryF9.Children = sub9;
                            }
                        }
                        if (primaryF9.Children == null) primaryF9.Children = new List<ProtoNode>();

                        ProtoNode f9c1 = primaryF9.GetChild(1);
                        if (f9c1 != null && f9c1.WireType == 2 && f9c1.HasChildren && f9c1.GetChild(1) != null)
                        {
                            f9c1.GetChild(1).SetString(effectiveFolderUri);
                            f9c1.IsModified = true;
                        }
                        else
                        {
                            ProtoAstCodec.SetOrAddUtf8Leaf(primaryF9.Children, 1, effectiveFolderUri);
                        }
                        primaryF9.IsModified = true;
                    }
                }

                if (!string.IsNullOrEmpty(effectiveProjName) || !string.IsNullOrEmpty(effectiveFolderUri))
                {
                    ProtoNode f17 = null;
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        if (nodes[i].FieldNumber == 17 && nodes[i].WireType == 2)
                        {
                            f17 = nodes[i];
                            break;
                        }
                    }
                    if (f17 == null)
                    {
                        f17 = new ProtoNode(17, 2) { IsModified = true };
                        nodes.Add(f17);
                    }
                    else if (!f17.HasChildren && f17.RawBytes != null && f17.RawBytes.Length > 0)
                    {
                        List<ProtoNode> sub17;
                        if (ProtoCodec.TryDecodeStrict(f17.RawBytes, out sub17))
                        {
                            f17.Children = sub17;
                        }
                    }
                    if (f17.Children == null) f17.Children = new List<ProtoNode>();

                    if (!string.IsNullOrEmpty(effectiveProjName))
                    {
                        ProtoAstCodec.SetOrAddUtf8Leaf(f17.Children, 1, effectiveProjName);
                    }
                    if (!string.IsNullOrEmpty(effectiveFolderUri))
                    {
                        ProtoAstCodec.SetOrAddUtf8Leaf(f17.Children, 7, effectiveFolderUri);
                    }
                    f17.IsModified = true;
                }

                return ProtoCodec.Encode(nodes);
            }
            catch (Exception ex)
            {
                warnings.Add("raw_summary 工程绑定 AST 更新失败 (" + cid + "): " + ex.Message);
                return oldRaw;
            }
        }

        private static bool UpdateFtsBindingMetadata(
            IntPtr db,
            HashSet<string> ftsCols,
            string cid,
            string title,
            string workspaceVal,
            string projectVal,
            bool updateTitle,
            bool updateWorkspaceAndProject,
            out string err)
        {
            err = null;
            List<string> assignments = new List<string>(3);
            List<string> values = new List<string>(3);

            if (updateTitle && ftsCols.Contains("title"))
            {
                assignments.Add("title = ?");
                values.Add(title);
            }
            if (updateWorkspaceAndProject)
            {
                if (ftsCols.Contains("workspace"))
                {
                    assignments.Add("workspace = ?");
                    values.Add(workspaceVal ?? string.Empty);
                }
                if (ftsCols.Contains("project"))
                {
                    assignments.Add("project = ?");
                    values.Add(projectVal ?? string.Empty);
                }
            }

            if (assignments.Count == 0) return true;

            string sql = "UPDATE conversations_fts SET " + string.Join(", ", assignments.ToArray()) +
                         " WHERE conversation_id = ? AND chunk_index = -1;";
            int changes;
            return ExecuteNonQuery(
                db,
                sql,
                delegate(IntPtr stmt)
                {
                    int idx = 1;
                    for (int i = 0; i < values.Count; i++)
                    {
                        int rc = BindText(stmt, idx++, values[i]);
                        if (rc != SQLITE_OK) return rc;
                    }
                    return BindText(stmt, idx, cid);
                },
                out changes,
                out err);
        }

        public static int SyncProjectBindingInSummaryDb(
            string dbPath,
            IEnumerable<string> cids,
            string targetProjName,
            string targetProjId,
            string targetFolderUri,
            out List<string> warnings)
        {
            string err;
            int cnt = SyncProjectBindingInSummaryDb(dbPath, cids, targetProjName, targetProjId, targetFolderUri, out warnings, out err);
            LastError = err;
            return cnt;
        }

        /// <summary>
        /// 事务级同步工程归属、workspace_uris JSON、raw_summary (Field 9 &amp; Field 17) 与 FTS 工程/工作区字段
        /// </summary>
        public static int SyncProjectBindingInSummaryDb(
            string dbPath,
            IEnumerable<string> cids,
            string targetProjName,
            string targetProjId,
            string targetFolderUri,
            out List<string> warnings,
            out string error)
        {
            List<string> warnList = new List<string>();
            warnings = warnList;
            error = null;
            LastError = null;

            List<string> targetCids = CollectDistinctCids(cids);
            if (targetCids.Count == 0) return 0;
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath)) return 0;

            string trimmedProjId = targetProjId != null ? targetProjId.Trim() : string.Empty;
            string trimmedProjName = targetProjName != null ? targetProjName.Trim() : string.Empty;
            string trimmedFolderUri = targetFolderUri != null ? targetFolderUri.Trim() : string.Empty;

            bool isOutside = string.Equals(trimmedProjId, "outside-of-project", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(trimmedProjName, "Outside of Project", StringComparison.OrdinalIgnoreCase);

            string effectiveProjId = isOutside ? "outside-of-project" : trimmedProjId;
            string effectiveProjName = isOutside
                ? (string.IsNullOrEmpty(trimmedProjName) ? "Outside of Project" : trimmedProjName)
                : trimmedProjName;
            string effectiveFolderUri = isOutside ? string.Empty : trimmedFolderUri;
            string workspaceUrisVal = (isOutside || string.IsNullOrEmpty(effectiveFolderUri))
                ? string.Empty
                : "[\"" + UnifiedJson.EscapeString(effectiveFolderUri) + "\"]";

            string ftsWorkspace = isOutside ? string.Empty : effectiveFolderUri;
            string ftsProject = isOutside ? string.Empty : effectiveProjName;

            int updatedCount = 0;
            bool ok = ExecuteWriteTransaction(dbPath, delegate(IntPtr db, out string txnErr)
            {
                txnErr = null;
                bool hasFts = TableExists(db, "conversations_fts");
                HashSet<string> ftsCols = hasFts ? GetTableColumns(db, "conversations_fts") : null;

                for (int i = 0; i < targetCids.Count; i++)
                {
                    string cid = targetCids[i];
                    byte[] oldRaw = null;
                    bool foundRow = false;

                    IntPtr selStmt = IntPtr.Zero;
                    string err;
                    if (!PrepareStmt(db, "SELECT raw_summary FROM conversation_summaries WHERE conversation_id = ? LIMIT 1;", out selStmt, out err))
                    {
                        txnErr = err;
                        return false;
                    }
                    try
                    {
                        if (BindText(selStmt, 1, cid) != SQLITE_OK)
                        {
                            txnErr = "SELECT raw_summary 绑定失败: " + GetSqliteErrorMessage(db);
                            return false;
                        }
                        int stepRc = sqlite3_step(selStmt);
                        if (stepRc == SQLITE_ROW)
                        {
                            foundRow = true;
                            oldRaw = ReadColumnBlob(selStmt, 0);
                        }
                        else if (stepRc != SQLITE_DONE)
                        {
                            txnErr = "SELECT raw_summary 失败 (" + stepRc + "): " + GetSqliteErrorMessage(db);
                            return false;
                        }
                    }
                    finally
                    {
                        if (selStmt != IntPtr.Zero) sqlite3_finalize(selStmt);
                    }

                    if (!foundRow)
                    {
                        warnList.Add("conversation_summaries 未找到会话: " + cid);
                        continue;
                    }

                    byte[] newRaw = ApplyProjectBindingToRawSummary(
                        oldRaw, effectiveProjName, effectiveProjId, effectiveFolderUri, isOutside, warnList, cid);

                    int changes;
                    if (!ExecuteNonQuery(
                        db,
                        "UPDATE conversation_summaries SET project_id = ?, workspace_uris = ?, raw_summary = ? WHERE conversation_id = ?;",
                        delegate(IntPtr uStmt)
                        {
                            int rc = BindText(uStmt, 1, effectiveProjId);
                            if (rc != SQLITE_OK) return rc;
                            rc = BindText(uStmt, 2, workspaceUrisVal);
                            if (rc != SQLITE_OK) return rc;
                            rc = BindBlob(uStmt, 3, newRaw);
                            if (rc != SQLITE_OK) return rc;
                            return BindText(uStmt, 4, cid);
                        },
                        out changes,
                        out err))
                    {
                        txnErr = err;
                        return false;
                    }
                    if (changes > 0) updatedCount++;

                    if (hasFts && ftsCols != null)
                    {
                        string ftsErr;
                        if (!UpdateFtsBindingMetadata(db, ftsCols, cid, null, ftsWorkspace, ftsProject, false, true, out ftsErr))
                        {
                            txnErr = ftsErr;
                            return false;
                        }
                    }
                }
                return true;
            }, out error);

            LastError = error;
            if (!ok)
            {
                if (!string.IsNullOrEmpty(error)) warnList.Add(error);
                return 0;
            }
            return updatedCount;
        }

        private static bool RowExistsByCid(IntPtr db, string tableName, string cid, bool requireNegOneChunk, out bool exists, out string err)
        {
            exists = false;
            err = null;
            string sql = requireNegOneChunk
                ? "SELECT 1 FROM " + tableName + " WHERE conversation_id = ? AND chunk_index = -1 LIMIT 1;"
                : "SELECT 1 FROM " + tableName + " WHERE conversation_id = ? LIMIT 1;";
            IntPtr stmt = IntPtr.Zero;
            if (!PrepareStmt(db, sql, out stmt, out err)) return false;
            try
            {
                int bindRc = BindText(stmt, 1, cid);
                if (bindRc != SQLITE_OK)
                {
                    err = "存在性查询绑定失败 (" + bindRc + "): " + GetSqliteErrorMessage(db);
                    return false;
                }
                int stepRc = sqlite3_step(stmt);
                if (stepRc == SQLITE_ROW)
                {
                    exists = true;
                    return true;
                }
                if (stepRc == SQLITE_DONE)
                {
                    exists = false;
                    return true;
                }
                err = "存在性查询失败 (" + stepRc + "): " + GetSqliteErrorMessage(db);
                return false;
            }
            finally
            {
                if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
            }
        }

        public static int UpsertSessionsInSummaryDb(string dbPath, List<SummaryDbUpsertItem> items, out List<string> warnings)
        {
            string err;
            int cnt = UpsertSessionsInSummaryDb(dbPath, items, out warnings, out err);
            LastError = err;
            return cnt;
        }

        /// <summary>
        /// 事务级 Upsert 会话摘要：存在则增量更新标题/预览/工程/工作区/parent_conversation_id/raw_summary，
        /// 不存在则补全全量 NOT NULL 默认列插入。
        /// </summary>
        public static int UpsertSessionsInSummaryDb(string dbPath, List<SummaryDbUpsertItem> items, out List<string> warnings, out string error)
        {
            List<string> warnList = new List<string>();
            warnings = warnList;
            error = null;
            LastError = null;

            if (items == null || items.Count == 0) return 0;
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                error = "数据库文件不存在: " + dbPath;
                LastError = error;
                return 0;
            }

            int upsertedCount = 0;
            bool ok = ExecuteWriteTransaction(dbPath, delegate(IntPtr db, out string txnErr)
            {
                txnErr = null;
                HashSet<string> sumCols = GetTableColumns(db, "conversation_summaries");
                if (sumCols.Count == 0 || !sumCols.Contains("conversation_id"))
                {
                    txnErr = "conversation_summaries 表不存在或缺少 conversation_id 列。";
                    return false;
                }

                bool hasFts = TableExists(db, "conversations_fts");
                HashSet<string> ftsCols = hasFts ? GetTableColumns(db, "conversations_fts") : null;

                for (int i = 0; i < items.Count; i++)
                {
                    SummaryDbUpsertItem item = items[i];
                    if (item == null || string.IsNullOrWhiteSpace(item.ConversationId))
                    {
                        warnList.Add("跳过无效的 UpsertItem (空 CID)。");
                        continue;
                    }

                    string cid = item.ConversationId.Trim();
                    string titleVal = item.Title ?? string.Empty;
                    string previewVal = item.Preview ?? string.Empty;
                    string projectIdVal = item.ProjectId ?? string.Empty;
                    string workspaceUrisVal = item.WorkspaceUrisJson ?? string.Empty;
                    string parentCidVal = item.ParentConversationId != null ? item.ParentConversationId.Trim() : string.Empty;
                    string nowIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                    string modIso = string.IsNullOrEmpty(item.LastModifiedTimeIso) ? nowIso : item.LastModifiedTimeIso;
                    string inputIso = string.IsNullOrEmpty(item.LastUserInputTimeIso) ? modIso : item.LastUserInputTimeIso;

                    bool exists;
                    string err;
                    if (!RowExistsByCid(db, "conversation_summaries", cid, false, out exists, out err))
                    {
                        txnErr = err;
                        return false;
                    }

                    int changes;
                    if (exists)
                    {
                        bool updateParent = !string.IsNullOrEmpty(parentCidVal) && sumCols.Contains("parent_conversation_id");
                        bool updateDepth = item.NestingDepth > 0 && sumCols.Contains("nesting_depth");
                        StringBuilder updSql = new StringBuilder();
                        updSql.Append("UPDATE conversation_summaries SET title = ?, preview = ?, project_id = ?, workspace_uris = ?, raw_summary = COALESCE(?, raw_summary)");
                        if (updateParent) updSql.Append(", parent_conversation_id = ?");
                        if (updateDepth) updSql.Append(", nesting_depth = ?");
                        updSql.Append(" WHERE conversation_id = ?;");

                        if (!ExecuteNonQuery(
                            db,
                            updSql.ToString(),
                            delegate(IntPtr uStmt)
                            {
                                int pIdx = 1;
                                int rc = BindText(uStmt, pIdx++, titleVal);
                                if (rc != SQLITE_OK) return rc;
                                rc = BindText(uStmt, pIdx++, previewVal);
                                if (rc != SQLITE_OK) return rc;
                                rc = BindText(uStmt, pIdx++, projectIdVal);
                                if (rc != SQLITE_OK) return rc;
                                rc = BindText(uStmt, pIdx++, workspaceUrisVal);
                                if (rc != SQLITE_OK) return rc;
                                rc = BindBlob(uStmt, pIdx++, item.RawSummaryBytes);
                                if (rc != SQLITE_OK) return rc;
                                if (updateParent)
                                {
                                    rc = BindText(uStmt, pIdx++, parentCidVal);
                                    if (rc != SQLITE_OK) return rc;
                                }
                                if (updateDepth)
                                {
                                    rc = sqlite3_bind_int(uStmt, pIdx++, item.NestingDepth);
                                    if (rc != SQLITE_OK) return rc;
                                }
                                return BindText(uStmt, pIdx, cid);
                            },
                            out changes,
                            out err))
                        {
                            txnErr = err;
                            return false;
                        }
                        upsertedCount++;
                    }
                    else
                    {
                        byte[] insertRaw = item.RawSummaryBytes ?? new byte[0];
                        string[] allCols = new string[]
                        {
                            "conversation_id", "title", "preview", "step_count", "last_modified_time",
                            "workspace_uris", "status", "source", "project_id", "agent_name",
                            "parent_conversation_id", "nesting_depth", "battle_id", "winning_conversation_id",
                            "not_fully_idle", "killed", "last_user_input_time", "last_user_input_step_index",
                            "app_data_dir", "raw_summary", "group_id"
                        };
                        string[] allExprs = new string[]
                        {
                            "?", "?", "?", "?", "?",
                            "?", "'CASCADE_RUN_STATUS_IDLE'", "''", "?", "''",
                            "?", "?", "''", "''",
                            "0", "0", "?", "-1",
                            "'antigravity'", "?", "''"
                        };

                        List<string> insCols = new List<string>(allCols.Length);
                        List<string> insExprs = new List<string>(allCols.Length);
                        List<string> activeDynamicCols = new List<string>(11);

                        for (int c = 0; c < allCols.Length; c++)
                        {
                            if (!sumCols.Contains(allCols[c])) continue;
                            insCols.Add(allCols[c]);
                            insExprs.Add(allExprs[c]);
                            if (allExprs[c] == "?") activeDynamicCols.Add(allCols[c]);
                        }

                        string insSql = "INSERT INTO conversation_summaries (" + string.Join(", ", insCols.ToArray()) +
                                        ") VALUES (" + string.Join(", ", insExprs.ToArray()) + ");";

                        if (!ExecuteNonQuery(
                            db,
                            insSql,
                            delegate(IntPtr iStmt)
                            {
                                for (int k = 0; k < activeDynamicCols.Count; k++)
                                {
                                    int pIdx = k + 1;
                                    string colName = activeDynamicCols[k];
                                    int rc = SQLITE_OK;
                                    if (colName == "conversation_id") rc = BindText(iStmt, pIdx, cid);
                                    else if (colName == "title") rc = BindText(iStmt, pIdx, titleVal);
                                    else if (colName == "preview") rc = BindText(iStmt, pIdx, previewVal);
                                    else if (colName == "step_count") rc = sqlite3_bind_int(iStmt, pIdx, item.StepCount);
                                    else if (colName == "last_modified_time") rc = BindText(iStmt, pIdx, modIso);
                                    else if (colName == "workspace_uris") rc = BindText(iStmt, pIdx, workspaceUrisVal);
                                    else if (colName == "project_id") rc = BindText(iStmt, pIdx, projectIdVal);
                                    else if (colName == "parent_conversation_id") rc = BindText(iStmt, pIdx, parentCidVal);
                                    else if (colName == "nesting_depth") rc = sqlite3_bind_int(iStmt, pIdx, item.NestingDepth);
                                    else if (colName == "last_user_input_time") rc = BindText(iStmt, pIdx, inputIso);
                                    else if (colName == "raw_summary") rc = BindBlob(iStmt, pIdx, insertRaw);
                                    if (rc != SQLITE_OK) return rc;
                                }
                                return SQLITE_OK;
                            },
                            out changes,
                            out err))
                        {
                            txnErr = err;
                            return false;
                        }
                        upsertedCount++;
                    }

                    if (hasFts && ftsCols != null && ftsCols.Contains("conversation_id") && ftsCols.Contains("chunk_index"))
                    {
                        bool ftsExists;
                        string ftsErr;
                        if (!RowExistsByCid(db, "conversations_fts", cid, true, out ftsExists, out ftsErr))
                        {
                            txnErr = ftsErr;
                            return false;
                        }
                        if (ftsExists)
                        {
                            if (!UpdateFtsBindingMetadata(
                                db,
                                ftsCols,
                                cid,
                                titleVal,
                                item.FtsWorkspaceToken ?? string.Empty,
                                item.ProjectName ?? string.Empty,
                                true,
                                true,
                                out ftsErr))
                            {
                                txnErr = ftsErr;
                                return false;
                            }
                        }
                    }
                }
                return true;
            }, out error);

            LastError = error;
            if (!ok)
            {
                if (!string.IsNullOrEmpty(error)) warnList.Add(error);
                return 0;
            }
            return upsertedCount;
        }

        public static bool BackupDatabaseToFile(string srcDbPath, string destDbPath)
        {
            string err;
            bool ok = BackupDatabaseToFile(srcDbPath, destDbPath, out err);
            LastError = string.IsNullOrEmpty(err) ? null : err;
            return ok;
        }

        /// <summary>
        /// 使用 SQLite 在线备份 API (sqlite3_backup_init/step/finish) 安全拷贝数据库到临时文件并原子替换目标文件
        /// </summary>
        public static bool BackupDatabaseToFile(string srcDbPath, string destDbPath, out string error)
        {
            bool ok = SqliteOnlineBackup.BackupDatabaseAtomic(srcDbPath, destDbPath, out error);
            if (ok) error = null;
            LastError = error;
            return ok;
        }
    }
}
