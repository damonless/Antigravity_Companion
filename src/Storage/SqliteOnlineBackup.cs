using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace AntigravityCompanion
{
    /// <summary>
    /// Thread-safe, WAL-safe SQLite online backup, integrity verification, and command helper
    /// backed by Windows built-in winsqlite3.dll (C# 5.0 / .NET Framework 4.x).
    /// </summary>
    public static class SqliteOnlineBackup
    {
        public const int SQLITE_OK = 0;
        public const int SQLITE_BUSY = 5;
        public const int SQLITE_LOCKED = 6;
        public const int SQLITE_ROW = 100;
        public const int SQLITE_DONE = 101;

        public const int SQLITE_OPEN_READONLY = 0x00000001;
        public const int SQLITE_OPEN_READWRITE = 0x00000002;
        public const int SQLITE_OPEN_CREATE = 0x00000004;

        private const int DefaultBusyTimeoutMs = 5000;

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_open_v2(byte[] filename, out IntPtr ppDb, int flags, IntPtr zVfs);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close(IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_busy_timeout", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_busy_timeout(IntPtr db, int ms);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_exec", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_exec(IntPtr db, byte[] sql, IntPtr callback, IntPtr arg, out IntPtr errmsg);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_free", CallingConvention = CallingConvention.Cdecl)]
        private static extern void sqlite3_free(IntPtr ptr);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_backup_init", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_backup_init(IntPtr pDest, byte[] zDestName, IntPtr pSource, byte[] zSourceName);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_backup_step", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_backup_step(IntPtr pBackup, int nPage);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_backup_finish", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_backup_finish(IntPtr pBackup);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_errmsg", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_errmsg(IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_prepare_v2(IntPtr db, byte[] zSql, int nByte, out IntPtr ppStmt, IntPtr pzTail);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_step(IntPtr pStmt);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_text(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_bytes", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_bytes(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr pStmt);

        /// <summary>
        /// Safely snapshots a live SQLite database (including uncommitted WAL frames) into destDbPath
        /// using sqlite3_backup_init/step/finish and atomic file replacement, without closing Antigravity IDE.
        /// </summary>
        public static bool BackupDatabaseAtomic(string srcDbPath, string destDbPath, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(srcDbPath) || !File.Exists(srcDbPath))
            {
                error = "Source SQLite database does not exist: " + (srcDbPath ?? string.Empty);
                return false;
            }
            if (string.IsNullOrEmpty(destDbPath))
            {
                error = "Destination SQLite database path is empty.";
                return false;
            }

            try
            {
                string fullSrc = Path.GetFullPath(srcDbPath);
                string fullDest = Path.GetFullPath(destDbPath);
                if (string.Equals(fullSrc, fullDest, StringComparison.OrdinalIgnoreCase))
                {
                    error = "Source and destination database paths must be different.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = "Invalid database path: " + ex.Message;
                return false;
            }

            string destDir = Path.GetDirectoryName(destDbPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            string tmpPath = destDbPath + ".tmp." + Guid.NewGuid().ToString("N").Substring(0, 8);
            IntPtr srcDb = IntPtr.Zero;
            IntPtr destDb = IntPtr.Zero;
            IntPtr bk = IntPtr.Zero;
            bool committed = false;

            try
            {
                if (!OpenDatabase(srcDbPath, SQLITE_OPEN_READONLY, out srcDb, out error))
                {
                    return false;
                }

                if (!OpenDatabase(tmpPath, SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE, out destDb, out error))
                {
                    return false;
                }

                byte[] mainName = ToUtf8NullTerminated("main");
                bk = sqlite3_backup_init(destDb, mainName, srcDb, mainName);
                if (bk == IntPtr.Zero)
                {
                    error = "sqlite3_backup_init failed: " + ReadErrorMessage(destDb);
                    return false;
                }

                int stepRc;
                Stopwatch sw = Stopwatch.StartNew();
                while (true)
                {
                    stepRc = sqlite3_backup_step(bk, -1);
                    if (stepRc == SQLITE_OK || stepRc == SQLITE_BUSY || stepRc == SQLITE_LOCKED)
                    {
                        if (sw.ElapsedMilliseconds >= DefaultBusyTimeoutMs)
                            break;
                        Thread.Sleep(25);
                        continue;
                    }
                    break;
                }
                int finishRc = sqlite3_backup_finish(bk);
                bk = IntPtr.Zero;

                if (stepRc != SQLITE_DONE || finishRc != SQLITE_OK)
                {
                    error = "sqlite3_backup failed (step=" + stepRc + ", finish=" + finishRc + "): " + ReadErrorMessage(destDb);
                    return false;
                }

                sqlite3_close(destDb);
                destDb = IntPtr.Zero;
                sqlite3_close(srcDb);
                srcDb = IntPtr.Zero;

                if (File.Exists(destDbPath))
                {
                    try { File.SetAttributes(destDbPath, FileAttributes.Normal); } catch { }
                    File.Replace(tmpPath, destDbPath, null, true);
                }
                else
                {
                    File.Move(tmpPath, destDbPath);
                }

                TryDeleteSidecarFiles(destDbPath);
                committed = true;
                return true;
            }
            catch (DllNotFoundException ex)
            {
                error = "winsqlite3.dll not found: " + ex.Message;
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                error = "winsqlite3.dll entry point missing: " + ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (bk != IntPtr.Zero)
                {
                    try { sqlite3_backup_finish(bk); } catch { }
                }
                if (destDb != IntPtr.Zero)
                {
                    try { sqlite3_close(destDb); } catch { }
                }
                if (srcDb != IntPtr.Zero)
                {
                    try { sqlite3_close(srcDb); } catch { }
                }
                if (!committed)
                {
                    TryDeleteFileAndSidecars(tmpPath);
                }
                else
                {
                    TryDeleteSidecarFiles(tmpPath);
                }
            }
        }

        /// <summary>
        /// Runs PRAGMA integrity_check; on dbPath and returns true iff the single result row is "ok".
        /// </summary>
        public static bool RunIntegrityCheck(string dbPath, out string detail)
        {
            detail = string.Empty;
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                detail = "SQLite database does not exist: " + (dbPath ?? string.Empty);
                return false;
            }

            IntPtr db = IntPtr.Zero;
            IntPtr stmt = IntPtr.Zero;

            try
            {
                string openErr;
                if (!OpenDatabase(dbPath, SQLITE_OPEN_READONLY, out db, out openErr))
                {
                    detail = openErr;
                    return false;
                }

                byte[] sqlBytes = ToUtf8NullTerminated("PRAGMA integrity_check;");
                int prepRc = sqlite3_prepare_v2(db, sqlBytes, -1, out stmt, IntPtr.Zero);
                if (prepRc != SQLITE_OK || stmt == IntPtr.Zero)
                {
                    detail = "sqlite3_prepare_v2 failed (" + prepRc + "): " + ReadErrorMessage(db);
                    return false;
                }

                List<string> rows = new List<string>();
                while (true)
                {
                    int stepRc = sqlite3_step(stmt);
                    if (stepRc == SQLITE_ROW)
                    {
                        string val = ReadColumnUtf8Text(stmt, 0);
                        rows.Add(val);
                    }
                    else if (stepRc == SQLITE_DONE)
                    {
                        break;
                    }
                    else
                    {
                        detail = "sqlite3_step failed (" + stepRc + "): " + ReadErrorMessage(db);
                        return false;
                    }
                }

                if (rows.Count == 1 && string.Equals(rows[0], "ok", StringComparison.OrdinalIgnoreCase))
                {
                    detail = "ok";
                    return true;
                }

                detail = rows.Count > 0 ? string.Join("\n", rows.ToArray()) : "PRAGMA integrity_check returned no rows.";
                return false;
            }
            catch (DllNotFoundException ex)
            {
                detail = "winsqlite3.dll not found: " + ex.Message;
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                detail = "winsqlite3.dll entry point missing: " + ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                detail = ex.Message;
                return false;
            }
            finally
            {
                if (stmt != IntPtr.Zero)
                {
                    try { sqlite3_finalize(stmt); } catch { }
                }
                if (db != IntPtr.Zero)
                {
                    try { sqlite3_close(db); } catch { }
                }
            }
        }

        /// <summary>
        /// Executes a non-query SQL statement (e.g. VACUUM, DELETE) against an existing SQLite database.
        /// </summary>
        public static bool ExecuteNonQuery(string dbPath, string sql, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                error = "SQLite database does not exist: " + (dbPath ?? string.Empty);
                return false;
            }
            if (string.IsNullOrWhiteSpace(sql))
            {
                error = "SQL statement cannot be empty.";
                return false;
            }

            IntPtr db = IntPtr.Zero;
            IntPtr errMsgPtr = IntPtr.Zero;

            try
            {
                if (!OpenDatabase(dbPath, SQLITE_OPEN_READWRITE, out db, out error))
                {
                    return false;
                }

                byte[] sqlBytes = ToUtf8NullTerminated(sql);
                int rc = sqlite3_exec(db, sqlBytes, IntPtr.Zero, IntPtr.Zero, out errMsgPtr);
                if (rc != SQLITE_OK)
                {
                    string execErr = ReadNullTerminatedUtf8(errMsgPtr);
                    if (string.IsNullOrEmpty(execErr))
                    {
                        execErr = ReadErrorMessage(db);
                    }
                    error = "sqlite3_exec failed (" + rc + "): " + execErr;
                    return false;
                }

                return true;
            }
            catch (DllNotFoundException ex)
            {
                error = "winsqlite3.dll not found: " + ex.Message;
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                error = "winsqlite3.dll entry point missing: " + ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (errMsgPtr != IntPtr.Zero)
                {
                    try { sqlite3_free(errMsgPtr); } catch { }
                }
                if (db != IntPtr.Zero)
                {
                    try { sqlite3_close(db); } catch { }
                }
            }
        }

        private static bool OpenDatabase(string dbPath, int flags, out IntPtr db, out string error)
        {
            db = IntPtr.Zero;
            error = string.Empty;
            byte[] pathBytes = ToUtf8NullTerminated(dbPath);
            int openRc = sqlite3_open_v2(pathBytes, out db, flags, IntPtr.Zero);
            if (openRc != SQLITE_OK || db == IntPtr.Zero)
            {
                string detail = db != IntPtr.Zero ? ReadErrorMessage(db) : "open returned null handle";
                if (db != IntPtr.Zero)
                {
                    try { sqlite3_close(db); } catch { }
                    db = IntPtr.Zero;
                }
                error = "sqlite3_open_v2 failed (" + openRc + "): " + detail;
                return false;
            }

            sqlite3_busy_timeout(db, DefaultBusyTimeoutMs);
            return true;
        }

        private static byte[] ToUtf8NullTerminated(string text)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(text ?? string.Empty);
            byte[] z = new byte[utf8.Length + 1];
            Buffer.BlockCopy(utf8, 0, z, 0, utf8.Length);
            z[utf8.Length] = 0;
            return z;
        }

        private static string ReadErrorMessage(IntPtr db)
        {
            if (db == IntPtr.Zero) return "db is null";
            try
            {
                IntPtr ptr = sqlite3_errmsg(db);
                string msg = ReadNullTerminatedUtf8(ptr);
                return string.IsNullOrEmpty(msg) ? "unknown sqlite error" : msg;
            }
            catch
            {
                return "unknown sqlite error";
            }
        }

        private static string ReadNullTerminatedUtf8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return string.Empty;
            int len = 0;
            while (len < 4096 && Marshal.ReadByte(ptr, len) != 0)
            {
                len++;
            }
            if (len == 0) return string.Empty;
            byte[] buf = new byte[len];
            Marshal.Copy(ptr, buf, 0, len);
            return Encoding.UTF8.GetString(buf);
        }

        private static string ReadColumnUtf8Text(IntPtr stmt, int col)
        {
            IntPtr ptr = sqlite3_column_text(stmt, col);
            if (ptr == IntPtr.Zero) return string.Empty;
            int len = sqlite3_column_bytes(stmt, col);
            if (len <= 0) return string.Empty;
            byte[] buf = new byte[len];
            Marshal.Copy(ptr, buf, 0, len);
            return Encoding.UTF8.GetString(buf);
        }

        private static void TryDeleteSidecarFiles(string dbPath)
        {
            TryDeleteSingleFile(dbPath + "-wal");
            TryDeleteSingleFile(dbPath + "-shm");
            TryDeleteSingleFile(dbPath + "-journal");
        }

        private static void TryDeleteFileAndSidecars(string dbPath)
        {
            TryDeleteSingleFile(dbPath);
            TryDeleteSidecarFiles(dbPath);
        }

        private static void TryDeleteSingleFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }
}
