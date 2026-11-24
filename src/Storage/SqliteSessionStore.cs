using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace AntigravityCompanion
{
    /// <summary>
    /// Parsed metadata from a per-session SQLite database (trajectory_metadata_blob).
    /// </summary>
    public class SessionDbMetadata
    {
        public string ConversationId;
        public string WorkspaceUri;
        public string ProjectId;
        public string DisplayTitle;
        public int StepCount;
        public bool HasMetadataBlob;
        public byte[] RawMetadataBytes;
    }

    /// <summary>
    /// Step record extracted from a per-session SQLite database when step tables are present.
    /// </summary>
    public class SessionStepRecord
    {
        public int StepIndex;
        public string StepType;
        public string CreatedAt;
        public string Content;
        public string Thinking;
    }

    /// <summary>
    /// Per-session .db SQLite operations: reading/remapping workspace URIs &amp; project IDs, and step extraction.
    /// Strictly C# 5.0 / winsqlite3.dll P/Invoke.
    /// </summary>
    public static class SqliteSessionStore
    {
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

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_blob", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_blob(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_bytes", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_bytes(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_int", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_int(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr pStmt);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_text", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_text(IntPtr pStmt, int iCol, IntPtr zData, int nByte, IntPtr xDel);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_errmsg", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_errmsg(IntPtr db);

        private static readonly IntPtr SQLITE_TRANSIENT = new IntPtr(-1);
        private static readonly byte[] EmptyPinSentinel = new byte[1] { 0 };

        public static SessionDbMetadata ReadSessionMetadata(string dbPath, out string error)
        {
            error = null;
            SessionDbMetadata meta = new SessionDbMetadata();
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                error = "数据库文件不存在: " + (dbPath ?? string.Empty);
                return meta;
            }

            IntPtr db = IntPtr.Zero;
            try
            {
                byte[] pathBytes = SqliteMetadataUpdater.ToUtf8NullTerminated(dbPath);
                int openRc = sqlite3_open_v2(pathBytes, out db, SQLITE_OPEN_READONLY, IntPtr.Zero);
                if (openRc != SQLITE_OK || db == IntPtr.Zero)
                {
                    error = "sqlite3_open_v2 失败 (" + openRc + "): " + GetSqliteErrorMessage(db);
                    return meta;
                }
                sqlite3_busy_timeout(db, BusyTimeoutMs);

                if (TableExists(db, "trajectory_metadata_blob"))
                {
                    byte[] blob = ReadMetadataBlob(db, out error);
                    if (blob != null && blob.Length > 0)
                    {
                        meta.HasMetadataBlob = true;
                        meta.RawMetadataBytes = blob;
                        ParseMetadataBlobInto(blob, meta);
                    }
                }

                if (meta.StepCount <= 0)
                {
                    int tableSteps = ReadStepCountFromTables(db);
                    if (tableSteps > 0) meta.StepCount = tableSteps;
                }

                return meta;
            }
            catch (DllNotFoundException ex)
            {
                error = "未找到 winsqlite3.dll: " + ex.Message;
                return meta;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return meta;
            }
            finally
            {
                if (db != IntPtr.Zero)
                {
                    try { sqlite3_close(db); } catch {}
                }
            }
        }

        public static bool RemapSessionMetadata(
            string dbPath,
            string srcHome,
            string curHome,
            Dictionary<string, string> uriOverrides,
            string projName,
            string targetProjId,
            string displayTitle,
            out string error)
        {
            return SqliteMetadataUpdater.RemapSqliteDbMetadata(
                dbPath, srcHome, curHome, uriOverrides, projName, targetProjId, displayTitle, out error);
        }

        public static int ExtractStepCount(string dbPath, out string error)
        {
            SessionDbMetadata meta = ReadSessionMetadata(dbPath, out error);
            return meta != null ? meta.StepCount : 0;
        }

        public static List<SessionStepRecord> ExtractSteps(string dbPath, out string error)
        {
            error = null;
            List<SessionStepRecord> steps = new List<SessionStepRecord>();
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                error = "数据库文件不存在: " + (dbPath ?? string.Empty);
                return steps;
            }

            IntPtr db = IntPtr.Zero;
            try
            {
                byte[] pathBytes = SqliteMetadataUpdater.ToUtf8NullTerminated(dbPath);
                int openRc = sqlite3_open_v2(pathBytes, out db, SQLITE_OPEN_READONLY, IntPtr.Zero);
                if (openRc != SQLITE_OK || db == IntPtr.Zero)
                {
                    error = "sqlite3_open_v2 失败 (" + openRc + "): " + GetSqliteErrorMessage(db);
                    return steps;
                }
                sqlite3_busy_timeout(db, BusyTimeoutMs);

                string[] candidateTables = new string[] { "trajectory_steps", "steps", "conversation_steps", "messages" };
                for (int t = 0; t < candidateTables.Length; t++)
                {
                    string table = candidateTables[t];
                    if (!TableExists(db, table)) continue;

                    HashSet<string> cols = GetTableColumns(db, table);
                    if (cols.Count == 0) continue;

                    List<SessionStepRecord> extracted = QueryStepsFromTable(db, table, cols);
                    if (extracted.Count > 0)
                        return extracted;
                }

                return steps;
            }
            catch (DllNotFoundException ex)
            {
                error = "未找到 winsqlite3.dll: " + ex.Message;
                return steps;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return steps;
            }
            finally
            {
                if (db != IntPtr.Zero)
                {
                    try { sqlite3_close(db); } catch {}
                }
            }
        }

        private static byte[] ReadMetadataBlob(IntPtr db, out string error)
        {
            error = null;
            IntPtr stmt = IntPtr.Zero;
            byte[] sql = SqliteMetadataUpdater.ToUtf8NullTerminated("SELECT data FROM trajectory_metadata_blob WHERE id='main';");
            try
            {
                int prepRc = sqlite3_prepare_v2(db, sql, -1, out stmt, IntPtr.Zero);
                if (prepRc != SQLITE_OK)
                {
                    error = "SELECT prepare 失败 (" + prepRc + "): " + GetSqliteErrorMessage(db);
                    return null;
                }
                int stepRc = sqlite3_step(stmt);
                if (stepRc == SQLITE_ROW)
                {
                    int len = sqlite3_column_bytes(stmt, 0);
                    IntPtr blobPtr = sqlite3_column_blob(stmt, 0);
                    if (len > 0 && blobPtr != IntPtr.Zero)
                    {
                        byte[] blob = new byte[len];
                        Marshal.Copy(blobPtr, blob, 0, len);
                        return blob;
                    }
                }
                return null;
            }
            finally
            {
                if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
            }
        }

        private static void ParseMetadataBlobInto(byte[] blob, SessionDbMetadata meta)
        {
            try
            {
                List<ProtoNode> nodes = ProtoCodec.Decode(blob);
                if (nodes == null || nodes.Count == 0) return;

                List<ProtoNode> entryLevelNodes;
                List<ProtoNode> metaNodes;
                ProtoNode wrappedEntry;
                ProtoNode wrappedMeta;
                SqliteMetadataUpdater.ResolveMetadataContainers(nodes, out wrappedEntry, out wrappedMeta, out entryLevelNodes, out metaNodes);

                if (wrappedEntry != null)
                {
                    meta.ConversationId = ProtoAstCodec.ExtractConversationId(wrappedEntry);
                }
                else
                {
                    for (int i = 0; i < entryLevelNodes.Count; i++)
                    {
                        if (entryLevelNodes[i].FieldNumber == 1 && entryLevelNodes[i].WireType == 2 &&
                            SessionMetadataExtractor.IsLikelyConversationId(entryLevelNodes[i].GetString()))
                        {
                            meta.ConversationId = entryLevelNodes[i].GetString().Trim();
                            break;
                        }
                    }
                }

                meta.WorkspaceUri = ProtoAstCodec.ExtractWorkspaceUriFromMetaChildren(metaNodes);
                meta.ProjectId = ProtoAstCodec.ExtractProjectIdFromNodes(entryLevelNodes, metaNodes);

                string autoTitle = null;
                string customTitle = null;
                for (int i = 0; i < metaNodes.Count; i++)
                {
                    ProtoNode c = metaNodes[i];
                    if (c.FieldNumber == 1 && c.WireType == 2)
                    {
                        string t;
                        if (c.TryGetStrictUtf8String(out t) && !string.IsNullOrWhiteSpace(t))
                            autoTitle = t.Trim();
                    }
                    else if (c.FieldNumber == 2 && c.WireType == 0)
                    {
                        meta.StepCount = c.GetInt32();
                    }
                    else if (c.FieldNumber == 15 && c.WireType == 2)
                    {
                        if (c.HasChildren)
                        {
                            ProtoNode ct1 = c.GetChild(1);
                            if (ct1 != null)
                            {
                                string s = ct1.GetString();
                                if (!string.IsNullOrWhiteSpace(s)) customTitle = s.Trim();
                            }
                        }
                        else
                        {
                            string s = c.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) customTitle = s.Trim();
                        }
                    }
                }
                meta.DisplayTitle = !string.IsNullOrEmpty(customTitle) ? customTitle : autoTitle;
            }
            catch
            {
            }
        }

        private static int ReadStepCountFromTables(IntPtr db)
        {
            string[] tables = new string[] { "trajectory_steps", "steps", "conversation_steps", "messages" };
            for (int i = 0; i < tables.Length; i++)
            {
                if (!TableExists(db, tables[i])) continue;
                IntPtr stmt = IntPtr.Zero;
                byte[] sql = SqliteMetadataUpdater.ToUtf8NullTerminated("SELECT COUNT(*) FROM " + tables[i] + ";");
                try
                {
                    if (sqlite3_prepare_v2(db, sql, -1, out stmt, IntPtr.Zero) == SQLITE_OK && stmt != IntPtr.Zero)
                    {
                        if (sqlite3_step(stmt) == SQLITE_ROW)
                        {
                            int cnt = sqlite3_column_int(stmt, 0);
                            if (cnt > 0) return cnt;
                        }
                    }
                }
                finally
                {
                    if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
                }
            }
            return 0;
        }

        private static List<SessionStepRecord> QueryStepsFromTable(IntPtr db, string table, HashSet<string> cols)
        {
            List<SessionStepRecord> list = new List<SessionStepRecord>();

            string idxCol = cols.Contains("step_index") ? "step_index" : (cols.Contains("idx") ? "idx" : (cols.Contains("id") ? "id" : null));
            string typeCol = cols.Contains("type") ? "type" : (cols.Contains("step_type") ? "step_type" : (cols.Contains("role") ? "role" : (cols.Contains("sender") ? "sender" : null)));
            string contentCol = cols.Contains("content") ? "content" : (cols.Contains("text") ? "text" : (cols.Contains("body") ? "body" : (cols.Contains("message") ? "message" : null)));
            string thinkingCol = cols.Contains("thinking") ? "thinking" : (cols.Contains("thought") ? "thought" : (cols.Contains("reasoning") ? "reasoning" : null));
            string timeCol = cols.Contains("created_at") ? "created_at" : (cols.Contains("timestamp") ? "timestamp" : (cols.Contains("createdAt") ? "createdAt" : null));
            string jsonCol = cols.Contains("data") ? "data" : (cols.Contains("json") ? "json" : (cols.Contains("payload") ? "payload" : null));

            List<string> selectCols = new List<string>();
            if (idxCol != null) selectCols.Add(idxCol);
            if (typeCol != null) selectCols.Add(typeCol);
            if (contentCol != null) selectCols.Add(contentCol);
            if (thinkingCol != null) selectCols.Add(thinkingCol);
            if (timeCol != null) selectCols.Add(timeCol);
            if (jsonCol != null) selectCols.Add(jsonCol);
            if (selectCols.Count == 0) return list;

            string orderClause = idxCol != null ? (" ORDER BY " + idxCol + " ASC") : string.Empty;
            string sql = "SELECT " + string.Join(", ", selectCols.ToArray()) + " FROM " + table + orderClause + ";";
            IntPtr stmt = IntPtr.Zero;
            if (sqlite3_prepare_v2(db, SqliteMetadataUpdater.ToUtf8NullTerminated(sql), -1, out stmt, IntPtr.Zero) != SQLITE_OK || stmt == IntPtr.Zero)
                return list;

            try
            {
                int posIdx = idxCol != null ? selectCols.IndexOf(idxCol) : -1;
                int posType = typeCol != null ? selectCols.IndexOf(typeCol) : -1;
                int posContent = contentCol != null ? selectCols.IndexOf(contentCol) : -1;
                int posThinking = thinkingCol != null ? selectCols.IndexOf(thinkingCol) : -1;
                int posTime = timeCol != null ? selectCols.IndexOf(timeCol) : -1;
                int posJson = jsonCol != null ? selectCols.IndexOf(jsonCol) : -1;

                int autoIdx = 0;
                while (sqlite3_step(stmt) == SQLITE_ROW)
                {
                    SessionStepRecord rec = new SessionStepRecord();
                    rec.StepIndex = posIdx >= 0 && sqlite3_column_type(stmt, posIdx) != SQLITE_NULL
                        ? sqlite3_column_int(stmt, posIdx)
                        : autoIdx;
                    if (posType >= 0) rec.StepType = ReadColumnString(stmt, posType);
                    if (posContent >= 0) rec.Content = ReadColumnString(stmt, posContent);
                    if (posThinking >= 0) rec.Thinking = ReadColumnString(stmt, posThinking);
                    if (posTime >= 0) rec.CreatedAt = ReadColumnString(stmt, posTime);

                    if (posJson >= 0 && (string.IsNullOrEmpty(rec.Content) || string.IsNullOrEmpty(rec.StepType) ||
                        string.IsNullOrEmpty(rec.Thinking) || string.IsNullOrEmpty(rec.CreatedAt)))
                    {
                        string rawJson = ReadColumnString(stmt, posJson);
                        if (!string.IsNullOrEmpty(rawJson) && rawJson.TrimStart().StartsWith("{", StringComparison.Ordinal))
                        {
                            object parsed;
                            if (UnifiedJson.TryParse(rawJson, out parsed))
                            {
                                Dictionary<string, object> dict = UnifiedJson.AsDict(parsed);
                                if (dict != null)
                                {
                                    if (string.IsNullOrEmpty(rec.StepType))
                                    {
                                        rec.StepType = UnifiedJson.GetString(dict, "type", string.Empty);
                                        if (string.IsNullOrEmpty(rec.StepType)) rec.StepType = UnifiedJson.GetString(dict, "role", string.Empty);
                                    }
                                    if (string.IsNullOrEmpty(rec.Content))
                                    {
                                        rec.Content = UnifiedJson.GetString(dict, "content", string.Empty);
                                        if (string.IsNullOrEmpty(rec.Content)) rec.Content = UnifiedJson.GetString(dict, "text", string.Empty);
                                        if (string.IsNullOrEmpty(rec.Content)) rec.Content = UnifiedJson.GetString(dict, "message", string.Empty);
                                    }
                                    if (string.IsNullOrEmpty(rec.Thinking))
                                    {
                                        rec.Thinking = UnifiedJson.GetString(dict, "thinking", string.Empty);
                                        if (string.IsNullOrEmpty(rec.Thinking)) rec.Thinking = UnifiedJson.GetString(dict, "thought", string.Empty);
                                        if (string.IsNullOrEmpty(rec.Thinking)) rec.Thinking = UnifiedJson.GetString(dict, "reasoning", string.Empty);
                                    }
                                    if (string.IsNullOrEmpty(rec.CreatedAt))
                                    {
                                        rec.CreatedAt = UnifiedJson.GetString(dict, "created_at", string.Empty);
                                        if (string.IsNullOrEmpty(rec.CreatedAt)) rec.CreatedAt = UnifiedJson.GetString(dict, "createdAt", string.Empty);
                                        if (string.IsNullOrEmpty(rec.CreatedAt)) rec.CreatedAt = UnifiedJson.GetString(dict, "timestamp", string.Empty);
                                    }
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(rec.StepType) || !string.IsNullOrEmpty(rec.Content) || !string.IsNullOrEmpty(rec.Thinking))
                    {
                        list.Add(rec);
                    }
                    autoIdx++;
                }
            }
            finally
            {
                if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
            }

            return list;
        }

        private static bool TableExists(IntPtr db, string tableName)
        {
            if (db == IntPtr.Zero || string.IsNullOrEmpty(tableName)) return false;
            IntPtr stmt = IntPtr.Zero;
            byte[] sql = SqliteMetadataUpdater.ToUtf8NullTerminated("SELECT 1 FROM sqlite_master WHERE type IN ('table', 'view') AND name = ? LIMIT 1;");
            if (sqlite3_prepare_v2(db, sql, -1, out stmt, IntPtr.Zero) != SQLITE_OK || stmt == IntPtr.Zero)
                return false;
            try
            {
                byte[] utf8 = Encoding.UTF8.GetBytes(tableName);
                byte[] pinned = utf8.Length > 0 ? utf8 : EmptyPinSentinel;
                GCHandle h = GCHandle.Alloc(pinned, GCHandleType.Pinned);
                try
                {
                    if (sqlite3_bind_text(stmt, 1, h.AddrOfPinnedObject(), utf8.Length, SQLITE_TRANSIENT) != SQLITE_OK)
                        return false;
                }
                finally
                {
                    if (h.IsAllocated) h.Free();
                }
                return sqlite3_step(stmt) == SQLITE_ROW;
            }
            finally
            {
                if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
            }
        }

        private static HashSet<string> GetTableColumns(IntPtr db, string tableName)
        {
            HashSet<string> cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (db == IntPtr.Zero || string.IsNullOrEmpty(tableName)) return cols;
            IntPtr stmt = IntPtr.Zero;
            byte[] sql = SqliteMetadataUpdater.ToUtf8NullTerminated("PRAGMA table_info(" + tableName + ");");
            if (sqlite3_prepare_v2(db, sql, -1, out stmt, IntPtr.Zero) != SQLITE_OK || stmt == IntPtr.Zero)
                return cols;
            try
            {
                while (sqlite3_step(stmt) == SQLITE_ROW)
                {
                    string name = ReadColumnString(stmt, 1);
                    if (!string.IsNullOrEmpty(name)) cols.Add(name.Trim());
                }
            }
            finally
            {
                if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
            }
            return cols;
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
    }

    /// <summary>
    /// SQLite 数据库 trajectory_metadata_blob 跨机路径重映射与 ProjectId 修正工具
    /// </summary>
    public static class SqliteMetadataUpdater
    {
        private const int SQLITE_OK = 0;
        private const int SQLITE_ROW = 100;
        private const int SQLITE_DONE = 101;
        private const int SQLITE_OPEN_READWRITE = 0x00000002;
        private static readonly IntPtr SQLITE_TRANSIENT = new IntPtr(-1);

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

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_blob", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_blob(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_bytes", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_bytes(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_blob", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_blob(IntPtr pStmt, int iCol, IntPtr zData, int nData, IntPtr xDel);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr pStmt);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_changes", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_changes(IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_errmsg", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_errmsg(IntPtr db);

        internal static byte[] ToUtf8NullTerminated(string text)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(text ?? string.Empty);
            byte[] z = new byte[utf8.Length + 1];
            Buffer.BlockCopy(utf8, 0, z, 0, utf8.Length);
            z[utf8.Length] = 0;
            return z;
        }

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

        public static bool RemapSqliteDbMetadata(string dbPath, string srcHome, string curHome,
                                                 Dictionary<string, string> uriOverrides, string projName,
                                                 string targetProjId = null, string displayTitle = null)
        {
            string err;
            bool ok = RemapSqliteDbMetadata(dbPath, srcHome, curHome, uriOverrides, projName, targetProjId, displayTitle, out err);
            LastError = err;
            return ok;
        }

        public static bool RemapSqliteDbMetadata(string dbPath, string srcHome, string curHome,
                                                 Dictionary<string, string> uriOverrides, string projName,
                                                 string targetProjId, string displayTitle, out string error)
        {
            error = null;
            LastError = null;
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                error = "数据库文件不存在: " + dbPath;
                LastError = error;
                return false;
            }

            IntPtr db = IntPtr.Zero;
            try
            {
                byte[] pathBytes = ToUtf8NullTerminated(dbPath);
                int openRc = sqlite3_open_v2(pathBytes, out db, SQLITE_OPEN_READWRITE, IntPtr.Zero);
                if (openRc != SQLITE_OK || db == IntPtr.Zero)
                {
                    error = "sqlite3_open_v2 失败 (" + openRc + "): " + GetSqliteErrorMessage(db);
                    LastError = error;
                    return false;
                }
                sqlite3_busy_timeout(db, 5000);

                byte[] blob = null;
                IntPtr selectStmt = IntPtr.Zero;
                byte[] selectSql = ToUtf8NullTerminated("SELECT data FROM trajectory_metadata_blob WHERE id='main';");
                try
                {
                    int prepRc = sqlite3_prepare_v2(db, selectSql, -1, out selectStmt, IntPtr.Zero);
                    if (prepRc != SQLITE_OK)
                    {
                        error = "SELECT prepare 失败 (" + prepRc + "): " + GetSqliteErrorMessage(db);
                        LastError = error;
                        return false;
                    }
                    int stepRc = sqlite3_step(selectStmt);
                    if (stepRc == SQLITE_ROW)
                    {
                        int len = sqlite3_column_bytes(selectStmt, 0);
                        IntPtr blobPtr = sqlite3_column_blob(selectStmt, 0);
                        if (len > 0 && blobPtr != IntPtr.Zero)
                        {
                            blob = new byte[len];
                            Marshal.Copy(blobPtr, blob, 0, len);
                        }
                    }
                }
                finally
                {
                    if (selectStmt != IntPtr.Zero) sqlite3_finalize(selectStmt);
                }

                if (blob == null || blob.Length == 0)
                {
                    error = "trajectory_metadata_blob 未找到 id='main' 或数据为空。";
                    LastError = error;
                    return false;
                }

                List<ProtoNode> nodes = ProtoCodec.Decode(blob);
                if (nodes == null || nodes.Count == 0)
                {
                    error = "trajectory_metadata_blob Protobuf 解码为空。";
                    LastError = error;
                    return false;
                }

                ProtoNode wrappedEntry;
                ProtoNode wrappedMeta;
                List<ProtoNode> entryLevelNodes;
                List<ProtoNode> targetContainerNodes;
                ResolveMetadataContainers(nodes, out wrappedEntry, out wrappedMeta, out entryLevelNodes, out targetContainerNodes);

                string origProjId = IncrementalProtoShardMerger.ExtractProjectIdFromNodes(entryLevelNodes, targetContainerNodes);
                string sqlConvId = null;
                if (wrappedEntry != null)
                {
                    sqlConvId = IncrementalProtoShardMerger.ExtractConversationId(wrappedEntry);
                }
                else
                {
                    for (int i = 0; i < entryLevelNodes.Count; i++)
                    {
                        if (entryLevelNodes[i].FieldNumber == 1 && entryLevelNodes[i].WireType == 2 &&
                            SessionMetadataExtractor.IsLikelyConversationId(entryLevelNodes[i].GetString()))
                        {
                            sqlConvId = entryLevelNodes[i].GetString().Trim();
                            break;
                        }
                    }
                }

                string oldWorkspaceUri = IncrementalProtoShardMerger.ExtractWorkspaceUriFromMetaChildren(targetContainerNodes);
                bool isOutside = string.Equals(targetProjId, "outside-of-project", StringComparison.OrdinalIgnoreCase);
                string newUri = isOutside
                    ? null
                    : IncrementalProtoShardMerger.ResolveUriOverride(uriOverrides, sqlConvId, targetProjId, origProjId, projName);

                if (isOutside)
                {
                    IncrementalProtoShardMerger.ClearWorkspaceFieldsInMetaChildren(targetContainerNodes);
                    if (wrappedMeta != null) wrappedMeta.IsModified = true;
                    if (wrappedEntry != null) wrappedEntry.IsModified = true;
                }

                IncrementalProtoShardMerger.RemapContainerNodeListPaths(
                    targetContainerNodes,
                    wrappedMeta != null ? 2 : 0,
                    srcHome,
                    curHome,
                    newUri,
                    oldWorkspaceUri,
                    isOutside
                );
                if (wrappedMeta != null && wrappedMeta.IsAnyModified)
                {
                    wrappedMeta.IsModified = true;
                }
                if (wrappedEntry != null && wrappedEntry.IsAnyModified)
                {
                    wrappedEntry.IsModified = true;
                }

                if (!string.IsNullOrEmpty(targetProjId))
                {
                    bool updatedTop18 = false;
                    for (int i = 0; i < entryLevelNodes.Count; i++)
                    {
                        if (entryLevelNodes[i].FieldNumber == 18 && entryLevelNodes[i].WireType == 2)
                        {
                            entryLevelNodes[i].SetString(targetProjId);
                            updatedTop18 = true;
                            if (wrappedEntry != null) wrappedEntry.IsModified = true;
                        }
                    }
                    bool createCascade = !updatedTop18;
                    if (IncrementalProtoShardMerger.SyncProjectIdInMetaChildren(targetContainerNodes, targetProjId, createCascade))
                    {
                        if (wrappedMeta != null) wrappedMeta.IsModified = true;
                        if (wrappedEntry != null) wrappedEntry.IsModified = true;
                    }
                }

                if (!string.IsNullOrWhiteSpace(displayTitle) && !IncrementalProtoShardMerger.IsSyntheticPlaceholderTitle(displayTitle))
                {
                    if (IncrementalProtoShardMerger.SyncTitlesInMetaChildren(targetContainerNodes, displayTitle.Trim(), false, false))
                    {
                        if (wrappedMeta != null) wrappedMeta.IsModified = true;
                        if (wrappedEntry != null) wrappedEntry.IsModified = true;
                    }
                }

                byte[] updatedBlob = ProtoCodec.Encode(nodes);

                IntPtr updateStmt = IntPtr.Zero;
                GCHandle pinHandle = GCHandle.Alloc(updatedBlob, GCHandleType.Pinned);
                byte[] updateSql = ToUtf8NullTerminated("UPDATE trajectory_metadata_blob SET data = ? WHERE id = 'main';");
                try
                {
                    int prepRc = sqlite3_prepare_v2(db, updateSql, -1, out updateStmt, IntPtr.Zero);
                    if (prepRc != SQLITE_OK)
                    {
                        error = "UPDATE prepare 失败 (" + prepRc + "): " + GetSqliteErrorMessage(db);
                        LastError = error;
                        return false;
                    }

                    int bindRc = sqlite3_bind_blob(updateStmt, 1, pinHandle.AddrOfPinnedObject(), updatedBlob.Length, SQLITE_TRANSIENT);
                    if (bindRc != SQLITE_OK)
                    {
                        error = "sqlite3_bind_blob 失败 (" + bindRc + "): " + GetSqliteErrorMessage(db);
                        LastError = error;
                        return false;
                    }

                    int stepRc = sqlite3_step(updateStmt);
                    if (stepRc != SQLITE_DONE)
                    {
                        error = "UPDATE step 失败 (" + stepRc + "): " + GetSqliteErrorMessage(db);
                        LastError = error;
                        return false;
                    }

                    int changedRows = sqlite3_changes(db);
                    if (changedRows <= 0)
                    {
                        error = "UPDATE 未命中任何 'main' 行 (sqlite3_changes=" + changedRows + ")";
                        LastError = error;
                        return false;
                    }
                    return true;
                }
                finally
                {
                    if (updateStmt != IntPtr.Zero) sqlite3_finalize(updateStmt);
                    if (pinHandle.IsAllocated) pinHandle.Free();
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                LastError = error;
                return false;
            }
            finally
            {
                if (db != IntPtr.Zero) sqlite3_close(db);
            }
        }

        internal static void ResolveMetadataContainers(
            List<ProtoNode> nodes,
            out ProtoNode wrappedEntry,
            out ProtoNode wrappedMeta,
            out List<ProtoNode> entryLevelNodes,
            out List<ProtoNode> targetContainerNodes)
        {
            wrappedEntry = null;
            wrappedMeta = null;
            if (nodes.Count == 1 && nodes[0].FieldNumber == 1 && nodes[0].WireType == 2 && !StartsLikePlainString(nodes[0]))
            {
                List<ProtoNode> entrySub;
                if (ProtoCodec.TryDecodeStrict(nodes[0].RawBytes, out entrySub) && entrySub.Count >= 2)
                {
                    ProtoNode innerCid = null;
                    ProtoNode innerMeta = null;
                    for (int j = 0; j < entrySub.Count; j++)
                    {
                        if (entrySub[j].FieldNumber == 1 && entrySub[j].WireType == 2) innerCid = entrySub[j];
                        else if (entrySub[j].FieldNumber == 2 && entrySub[j].WireType == 2) innerMeta = entrySub[j];
                    }
                    List<ProtoNode> metaCheck;
                    if (innerCid != null && SessionMetadataExtractor.IsLikelyConversationId(innerCid.GetString()) &&
                        innerMeta != null && !StartsLikePlainString(innerMeta) &&
                        ProtoCodec.TryDecodeStrict(innerMeta.RawBytes, out metaCheck) && metaCheck.Count > 0)
                    {
                        nodes[0].Children = entrySub;
                        ProtoCodec.DecodeSummaryEntryTree(nodes[0]);
                        wrappedEntry = nodes[0];
                        wrappedMeta = nodes[0].GetChild(2);
                    }
                }
            }

            if (wrappedMeta == null)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i].FieldNumber == 2 && nodes[i].WireType == 2 && !StartsLikePlainString(nodes[i]))
                    {
                        List<ProtoNode> metaSub;
                        if (ProtoCodec.TryDecodeStrict(nodes[i].RawBytes, out metaSub) && metaSub.Count > 0)
                        {
                            nodes[i].Children = metaSub;
                            ProtoCodec.DecodeMetaContainerList(nodes[i].Children);
                            wrappedMeta = nodes[i];
                            break;
                        }
                    }
                }
                ProtoCodec.DecodeMetaContainerList(nodes);
            }

            entryLevelNodes = (wrappedEntry != null && wrappedEntry.HasChildren)
                ? wrappedEntry.Children
                : nodes;

            targetContainerNodes = (wrappedMeta != null && wrappedMeta.HasChildren)
                ? wrappedMeta.Children
                : nodes;
        }

        private static bool StartsLikePlainString(ProtoNode node)
        {
            if (node == null || node.RawBytes == null || node.RawBytes.Length == 0) return true;
            byte first = node.RawBytes[0];
            return first >= 0x20 && first <= 0x7E && (first & 0x07) != 0 && (first & 0x07) != 2;
        }
    }
}
