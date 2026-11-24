using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using Microsoft.Win32;

namespace AntigravityCompanion
{
    public sealed class CompanionSettings
    {
        public bool Enabled;
        public int IntervalHours;
        public string TargetDirectory;
        public int RetainCount;
        public string Tier;
        public bool MinimizeToTray;
        public string GlobalHotkey;
        public string CustomAgyCliPath;
        public string VaultPath;
        public bool StartWithWindows;
        public int RuleTokenReferenceThreshold;
        public string LastRunUtc;
        public string LastFilePath;
        public long LastSizeBytes;
        public string LastError;

        public CompanionSettings()
        {
            Enabled = false;
            IntervalHours = 12;
            TargetDirectory = string.Empty;
            RetainCount = 5;
            Tier = "fast";
            MinimizeToTray = false;
            GlobalHotkey = string.Empty;
            CustomAgyCliPath = string.Empty;
            VaultPath = string.Empty;
            StartWithWindows = false;
            RuleTokenReferenceThreshold = 0;
            LastRunUtc = string.Empty;
            LastFilePath = string.Empty;
            LastSizeBytes = 0;
            LastError = string.Empty;
        }

        public CompanionSettings Clone()
        {
            CompanionSettings copy = new CompanionSettings();
            copy.Enabled = Enabled;
            copy.IntervalHours = IntervalHours;
            copy.TargetDirectory = TargetDirectory;
            copy.RetainCount = RetainCount;
            copy.Tier = Tier;
            copy.MinimizeToTray = MinimizeToTray;
            copy.GlobalHotkey = GlobalHotkey;
            copy.CustomAgyCliPath = CustomAgyCliPath;
            copy.VaultPath = VaultPath;
            copy.StartWithWindows = StartWithWindows;
            copy.RuleTokenReferenceThreshold = RuleTokenReferenceThreshold;
            copy.LastRunUtc = LastRunUtc;
            copy.LastFilePath = LastFilePath;
            copy.LastSizeBytes = LastSizeBytes;
            copy.LastError = LastError;
            return copy;
        }
    }

    public sealed class SnapshotRunResult
    {
        public bool Ok;
        public bool SkippedBusy;
        public string FilePath;
        public long SizeBytes;
        public int FileCount;
        public int DeletedOldCount;
        public string RunUtc;
        public string ErrorMessage;
        public List<string> Warnings;

        public SnapshotRunResult()
        {
            FilePath = string.Empty;
            RunUtc = string.Empty;
            ErrorMessage = string.Empty;
            Warnings = new List<string>();
        }
    }

    public sealed class SnapshotStatusReport
    {
        public bool Enabled;
        public int IntervalHours;
        public string TargetDirectory;
        public int RetainCount;
        public string Tier;
        public string LastRunUtc;
        public string LastFilePath;
        public long LastSizeBytes;
        public string LastError;
        public bool IsRunning;
        public int ExistingCount;
        public List<Dictionary<string, object>> ExistingSnapshots;

        public SnapshotStatusReport()
        {
            TargetDirectory = string.Empty;
            Tier = "fast";
            LastRunUtc = string.Empty;
            LastFilePath = string.Empty;
            LastError = string.Empty;
            ExistingSnapshots = new List<Dictionary<string, object>>();
        }
    }

    /// <summary>
    /// Companion settings store (%LOCALAPPDATA%\AntigravityCompanion\companion_settings.json),
    /// Windows Run-key startup management, and background scheduled silent .agy snapshot service.
    /// Strictly C# 5.0 (.NET Framework 4.x).
    /// </summary>
    public sealed class ScheduledSnapshotService : IDisposable
    {
        private const string RunRegistrySubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunRegistryValueName = "AntigravityCompanion";

        private readonly string _homePath;
        private readonly string _geminiPath;
        private readonly string _settingsFilePath;
        private readonly Func<string, string, bool> _tryAcquireJob;
        private readonly Action _releaseJob;
        private readonly Action<string, string> _onLog;
        private readonly object _mutationLock;
        private readonly Func<bool> _isIdeRunning;
        private readonly object _sync = new object();

        private CompanionSettings _settings;
        private System.Threading.Timer _timer;
        private volatile bool _disposed;
        private int _snapshotRunningFlag;

        public ScheduledSnapshotService(
            string homePath,
            string geminiPath,
            string explicitSettingsPath)
            : this(homePath, geminiPath, explicitSettingsPath, null, null, null, null, null)
        {
        }

        public ScheduledSnapshotService(
            string homePath,
            string geminiPath,
            string explicitSettingsPath,
            Func<string, string, bool> tryAcquireJob,
            Action releaseJob,
            Action<string, string> onLog)
            : this(homePath, geminiPath, explicitSettingsPath, tryAcquireJob, releaseJob, onLog, null, null)
        {
        }

        public ScheduledSnapshotService(
            string homePath,
            string geminiPath,
            string explicitSettingsPath,
            Func<string, string, bool> tryAcquireJob,
            Action releaseJob,
            Action<string, string> onLog,
            object mutationLock,
            Func<bool> isIdeRunning)
        {
            _homePath = string.IsNullOrWhiteSpace(homePath) ? WorkspaceService.ResolveDefaultHomePath() : homePath.Trim();
            _geminiPath = string.IsNullOrWhiteSpace(geminiPath) ? Path.Combine(_homePath, ".gemini") : geminiPath.Trim();
            _settingsFilePath = !string.IsNullOrWhiteSpace(explicitSettingsPath)
                ? explicitSettingsPath.Trim()
                : GetDefaultSettingsPath();
            _tryAcquireJob = tryAcquireJob;
            _releaseJob = releaseJob;
            _onLog = onLog;
            _mutationLock = mutationLock;
            _isIdeRunning = isIdeRunning;
            _settings = LoadSettingsInternal();
        }

        public string SettingsFilePath
        {
            get { return _settingsFilePath; }
        }

        public static string GetDefaultSettingsPath()
        {
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localApp))
                localApp = Path.GetTempPath();
            return Path.Combine(localApp, "AntigravityCompanion", "companion_settings.json");
        }

        public static string GetDefaultSnapshotDirectory(string geminiPath)
        {
            string safeGemini = string.IsNullOrWhiteSpace(geminiPath)
                ? Path.Combine(WorkspaceService.ResolveDefaultHomePath(), ".gemini")
                : geminiPath.Trim();
            return Path.Combine(safeGemini, "companion-snapshots");
        }

        public void Start()
        {
            lock (_sync)
            {
                if (_disposed) return;
                RescheduleTimerLocked();
            }
        }

        public void Stop()
        {
            lock (_sync)
            {
                if (_timer != null)
                {
                    try { _timer.Dispose(); } catch { }
                    _timer = null;
                }
            }
        }

        public void Dispose()
        {
            _disposed = true;
            Stop();
        }

        public CompanionSettings GetSettings()
        {
            lock (_sync)
            {
                return _settings.Clone();
            }
        }

        public CompanionSettings UpdateSettings(CompanionSettings updated)
        {
            if (updated == null) throw new ArgumentNullException("updated");

            CompanionSettings normalized = NormalizeSettings(updated, _geminiPath);
            lock (_sync)
            {
                // Preserve runtime snapshot status fields if caller didn't supply them
                if (string.IsNullOrEmpty(normalized.LastRunUtc) && !string.IsNullOrEmpty(_settings.LastRunUtc))
                    normalized.LastRunUtc = _settings.LastRunUtc;
                if (string.IsNullOrEmpty(normalized.LastFilePath) && !string.IsNullOrEmpty(_settings.LastFilePath))
                    normalized.LastFilePath = _settings.LastFilePath;
                if (normalized.LastSizeBytes <= 0 && _settings.LastSizeBytes > 0)
                    normalized.LastSizeBytes = _settings.LastSizeBytes;
                if (normalized.LastError == null)
                    normalized.LastError = _settings.LastError ?? string.Empty;

                _settings = normalized;
                SaveSettingsLocked(_settings);
                RescheduleTimerLocked();
                return _settings.Clone();
            }
        }

        public SnapshotStatusReport GetStatus()
        {
            CompanionSettings snap;
            lock (_sync)
            {
                snap = _settings.Clone();
            }

            SnapshotStatusReport report = new SnapshotStatusReport();
            report.Enabled = snap.Enabled;
            report.IntervalHours = snap.IntervalHours;
            report.TargetDirectory = snap.TargetDirectory;
            report.RetainCount = snap.RetainCount;
            report.Tier = snap.Tier;
            report.LastRunUtc = snap.LastRunUtc ?? string.Empty;
            report.LastFilePath = snap.LastFilePath ?? string.Empty;
            report.LastSizeBytes = snap.LastSizeBytes;
            report.LastError = snap.LastError ?? string.Empty;
            report.IsRunning = Interlocked.CompareExchange(ref _snapshotRunningFlag, 0, 0) != 0;

            List<FileInfo> files = ListSnapshotFilesOrdered(snap.TargetDirectory);
            report.ExistingCount = files.Count;
            int limit = Math.Min(files.Count, 20);
            for (int i = 0; i < limit; i++)
            {
                FileInfo fi = files[i];
                Dictionary<string, object> item = new Dictionary<string, object>();
                item["fileName"] = fi.Name;
                item["filePath"] = fi.FullName;
                item["sizeBytes"] = fi.Length;
                item["sizeMegabytes"] = Math.Round(fi.Length / (1024.0 * 1024.0), 2);
                item["createdUtc"] = fi.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture);
                report.ExistingSnapshots.Add(item);
            }
            return report;
        }

        public SnapshotRunResult RunSnapshotNow()
        {
            return ExecuteSnapshotInternal(false);
        }

        private void OnTimerTick(object state)
        {
            if (_disposed) return;
            CompanionSettings snap = GetSettings();
            if (!snap.Enabled) return;
            ExecuteSnapshotInternal(true);
        }

        internal SnapshotRunResult ExecuteSnapshotInternal(bool isScheduled)
        {
            SnapshotRunResult res = new SnapshotRunResult();
            if (Interlocked.CompareExchange(ref _snapshotRunningFlag, 1, 0) != 0)
            {
                res.Ok = false;
                res.SkippedBusy = true;
                res.ErrorMessage = "已有快照任务正在运行中。";
                return res;
            }

            bool acquiredHostJob = false;
            try
            {
                if (_tryAcquireJob != null)
                {
                    if (!_tryAcquireJob("scheduled_snapshot", isScheduled ? "正在后台执行定时静默快照..." : "正在创建极速快照 (.agy)..."))
                    {
                        res.Ok = false;
                        res.SkippedBusy = true;
                        res.ErrorMessage = "当前有其他后台任务正在执行，本次快照已安全跳过。";
                        if (isScheduled)
                            EmitLog("info", "定时静默快照跳过：工作台正在执行其他写入任务。");
                        return res;
                    }
                    acquiredHostJob = true;
                }

                CompanionSettings cfg = GetSettings();
                BackupTier tier = string.Equals(cfg.Tier, "full", StringComparison.OrdinalIgnoreCase)
                    ? BackupTier.FullForensic
                    : BackupTier.FastDaily;

                if (isScheduled && tier == BackupTier.FullForensic && _isIdeRunning != null && _isIdeRunning())
                {
                    res.Ok = false;
                    res.SkippedBusy = true;
                    res.ErrorMessage = "检测到 IDE 正在运行，定时全量法证快照已安全跳过（建议使用 FastDaily 极速只读档）。";
                    EmitLog("warn", res.ErrorMessage);
                    return res;
                }

                string targetDir = string.IsNullOrWhiteSpace(cfg.TargetDirectory)
                    ? GetDefaultSnapshotDirectory(_geminiPath)
                    : cfg.TargetDirectory.Trim();
                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                if (!Directory.Exists(_geminiPath))
                {
                    throw new DirectoryNotFoundException("未找到 .gemini 目录: " + _geminiPath);
                }

                string fileName = "snapshot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + ".agy";
                string destPath = Path.Combine(targetDir, fileName);

                int fileCount = 0;
                List<string> bkWarnings = null;
                bool lockTaken = false;
                try
                {
                    if (_mutationLock != null)
                    {
                        Monitor.Enter(_mutationLock, ref lockTaken);
                    }
                    Dictionary<string, ProjectMetadata> projs;
                    List<string> loadWarns;
                    List<ConversationSummaryInfo> sums = WorkspaceService.LoadLocalThreeLayerSummaries(_geminiPath, out projs, out loadWarns);
                    if (loadWarns != null && loadWarns.Count > 0)
                        res.Warnings.AddRange(loadWarns);

                    ArchiveTransferService.CreateFullBackup(
                        _geminiPath,
                        _homePath,
                        sums,
                        destPath,
                        tier,
                        null,
                        out fileCount,
                        out bkWarnings);
                }
                finally
                {
                    if (lockTaken)
                    {
                        Monitor.Exit(_mutationLock);
                    }
                }
                if (bkWarnings != null && bkWarnings.Count > 0)
                    res.Warnings.AddRange(bkWarnings);

                FileInfo fi = new FileInfo(destPath);
                long sizeBytes = fi.Exists ? fi.Length : 0L;
                int retain = cfg.RetainCount > 0 ? cfg.RetainCount : 5;
                int deletedOld = PruneOldSnapshots(targetDir, retain);

                string runUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

                lock (_sync)
                {
                    _settings.LastRunUtc = runUtc;
                    _settings.LastFilePath = destPath;
                    _settings.LastSizeBytes = sizeBytes;
                    _settings.LastError = string.Empty;
                    SaveSettingsLocked(_settings);
                }

                res.Ok = true;
                res.FilePath = destPath;
                res.SizeBytes = sizeBytes;
                res.FileCount = fileCount;
                res.DeletedOldCount = deletedOld;
                res.RunUtc = runUtc;

                EmitLog("success", string.Format("静默快照完成（{0}，{1} 文件，{2:F2} MB，清理旧快照 {3} 份）：{4}",
                    tier.ToString(), fileCount, sizeBytes / (1024.0 * 1024.0), deletedOld, fileName));
                return res;
            }
            catch (Exception ex)
            {
                string runUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                lock (_sync)
                {
                    _settings.LastRunUtc = runUtc;
                    _settings.LastError = ex.Message ?? "快照创建失败";
                    SaveSettingsLocked(_settings);
                }
                res.Ok = false;
                res.RunUtc = runUtc;
                res.ErrorMessage = ex.Message ?? "快照创建失败";
                EmitLog("error", "静默快照失败: " + res.ErrorMessage);
                return res;
            }
            finally
            {
                if (acquiredHostJob && _releaseJob != null)
                {
                    try { _releaseJob(); } catch { }
                }
                Interlocked.Exchange(ref _snapshotRunningFlag, 0);
            }
        }

        public static int PruneOldSnapshots(string targetDirectory, int retainCount)
        {
            if (string.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory))
                return 0;

            int effectiveRetain = retainCount > 0 ? retainCount : 1;
            List<FileInfo> files = ListSnapshotFilesOrdered(targetDirectory);
            int deleted = 0;
            for (int i = effectiveRetain; i < files.Count; i++)
            {
                try
                {
                    FileInfo fi = files[i];
                    if (fi.Exists)
                    {
                        File.SetAttributes(fi.FullName, FileAttributes.Normal);
                        fi.Delete();
                        deleted++;
                    }
                }
                catch { }
            }
            return deleted;
        }

        private static List<FileInfo> ListSnapshotFilesOrdered(string targetDirectory)
        {
            List<FileInfo> list = new List<FileInfo>();
            if (string.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory))
                return list;

            try
            {
                string[] paths = Directory.GetFiles(targetDirectory, "snapshot_*.agy", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < paths.Length; i++)
                {
                    try
                    {
                        list.Add(new FileInfo(paths[i]));
                    }
                    catch { }
                }
                list.Sort(delegate(FileInfo a, FileInfo b)
                {
                    int cmp = DateTime.Compare(b.LastWriteTimeUtc, a.LastWriteTimeUtc);
                    if (cmp != 0) return cmp;
                    return string.Compare(b.Name, a.Name, StringComparison.OrdinalIgnoreCase);
                });
            }
            catch { }
            return list;
        }

        public static bool SetStartWithWindows(bool enable, string exePath)
        {
            return SetStartWithWindowsInKey(Registry.CurrentUser, RunRegistrySubKey, RunRegistryValueName, enable, exePath);
        }

        public static bool IsStartWithWindowsEnabled()
        {
            return IsStartWithWindowsEnabledInKey(Registry.CurrentUser, RunRegistrySubKey, RunRegistryValueName);
        }

        internal static bool SetStartWithWindowsInKey(
            RegistryKey rootKey,
            string subKeyPath,
            string valueName,
            bool enable,
            string exePath)
        {
            if (rootKey == null || string.IsNullOrEmpty(subKeyPath) || string.IsNullOrEmpty(valueName))
                return false;

            try
            {
                using (RegistryKey key = rootKey.OpenSubKey(subKeyPath, true) ?? rootKey.CreateSubKey(subKeyPath))
                {
                    if (key == null) return false;
                    if (enable)
                    {
                        string resolvedExe = !string.IsNullOrWhiteSpace(exePath)
                            ? exePath.Trim().Trim('"')
                            : System.Windows.Forms.Application.ExecutablePath;
                        if (string.IsNullOrWhiteSpace(resolvedExe)) return false;
                        string cmd = "\"" + resolvedExe + "\" --tray-only";
                        key.SetValue(valueName, cmd, RegistryValueKind.String);
                    }
                    else
                    {
                        key.DeleteValue(valueName, false);
                    }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsStartWithWindowsEnabledInKey(
            RegistryKey rootKey,
            string subKeyPath,
            string valueName)
        {
            if (rootKey == null || string.IsNullOrEmpty(subKeyPath) || string.IsNullOrEmpty(valueName))
                return false;

            try
            {
                using (RegistryKey key = rootKey.OpenSubKey(subKeyPath, false))
                {
                    if (key == null) return false;
                    object val = key.GetValue(valueName);
                    string str = val as string;
                    return !string.IsNullOrWhiteSpace(str);
                }
            }
            catch
            {
                return false;
            }
        }

        private void RescheduleTimerLocked()
        {
            if (_timer != null)
            {
                try { _timer.Dispose(); } catch { }
                _timer = null;
            }

            if (_disposed || !_settings.Enabled)
                return;

            int hours = _settings.IntervalHours > 0 ? _settings.IntervalHours : 12;
            long intervalMsLong = (long)hours * 3600L * 1000L;
            if (intervalMsLong > int.MaxValue - 1) intervalMsLong = int.MaxValue - 1;
            int intervalMs = (int)intervalMsLong;

            long dueMsLong = intervalMsLong;
            if (!string.IsNullOrEmpty(_settings.LastRunUtc))
            {
                DateTime lastRun;
                if (DateTime.TryParse(_settings.LastRunUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out lastRun))
                {
                    double elapsedMs = (DateTime.UtcNow - lastRun.ToUniversalTime()).TotalMilliseconds;
                    double remainingMs = intervalMsLong - elapsedMs;
                    if (remainingMs <= 15000.0)
                        dueMsLong = 15000L;
                    else if (remainingMs < intervalMsLong)
                        dueMsLong = (long)remainingMs;
                }
            }
            if (dueMsLong > int.MaxValue - 1) dueMsLong = int.MaxValue - 1;

            _timer = new System.Threading.Timer(OnTimerTick, null, (int)dueMsLong, intervalMs);
        }

        private CompanionSettings LoadSettingsInternal()
        {
            CompanionSettings def = NormalizeSettings(new CompanionSettings(), _geminiPath);
            if (string.IsNullOrEmpty(_settingsFilePath) || !File.Exists(_settingsFilePath))
                return def;

            try
            {
                string raw = SessionLifecycleService.ReadUtf8TextShared(_settingsFilePath);
                object parsed;
                if (!UnifiedJson.TryParse(raw, out parsed))
                    return def;

                Dictionary<string, object> dict = UnifiedJson.AsDict(parsed);
                if (dict == null)
                    return def;

                CompanionSettings s = FromDictionary(dict, _geminiPath);
                return s;
            }
            catch
            {
                return def;
            }
        }

        private void SaveSettingsLocked(CompanionSettings s)
        {
            if (string.IsNullOrEmpty(_settingsFilePath)) return;
            try
            {
                string dir = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                string json = UnifiedJson.Serialize(ToDictionary(s), true);
                SessionLifecycleService.WriteUtf8TextAtomicWithBackup(_settingsFilePath, json, false, null);
            }
            catch { }
        }

        public static CompanionSettings NormalizeSettings(CompanionSettings s, string geminiPath)
        {
            CompanionSettings n = (s != null) ? s.Clone() : new CompanionSettings();
            if (n.IntervalHours <= 0) n.IntervalHours = 12;
            if (n.IntervalHours > 720) n.IntervalHours = 720;
            if (n.RetainCount <= 0) n.RetainCount = 5;
            if (n.RetainCount > 100) n.RetainCount = 100;
            if (string.IsNullOrWhiteSpace(n.TargetDirectory))
                n.TargetDirectory = GetDefaultSnapshotDirectory(geminiPath);
            else
                n.TargetDirectory = n.TargetDirectory.Trim();

            n.Tier = string.Equals((n.Tier ?? string.Empty).Trim(), "full", StringComparison.OrdinalIgnoreCase)
                ? "full"
                : "fast";
            n.GlobalHotkey = string.IsNullOrWhiteSpace(n.GlobalHotkey) ? string.Empty : n.GlobalHotkey.Trim();
            n.CustomAgyCliPath = (n.CustomAgyCliPath ?? string.Empty).Trim();
            n.VaultPath = (n.VaultPath ?? string.Empty).Trim();
            if (n.RuleTokenReferenceThreshold < 0) n.RuleTokenReferenceThreshold = 0;
            if (n.RuleTokenReferenceThreshold > 100000) n.RuleTokenReferenceThreshold = 100000;
            n.LastRunUtc = n.LastRunUtc ?? string.Empty;
            n.LastFilePath = n.LastFilePath ?? string.Empty;
            n.LastError = n.LastError ?? string.Empty;
            return n;
        }

        public static Dictionary<string, object> ToDictionary(CompanionSettings s)
        {
            CompanionSettings safe = s ?? new CompanionSettings();
            Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.Ordinal);
            d["enabled"] = safe.Enabled;
            d["intervalHours"] = safe.IntervalHours;
            d["targetDirectory"] = safe.TargetDirectory ?? string.Empty;
            d["retainCount"] = safe.RetainCount;
            d["tier"] = safe.Tier ?? "fast";
            d["minimizeToTray"] = safe.MinimizeToTray;
            d["globalHotkey"] = safe.GlobalHotkey ?? string.Empty;
            d["customAgyCliPath"] = safe.CustomAgyCliPath ?? string.Empty;
            d["vaultPath"] = safe.VaultPath ?? string.Empty;
            d["startWithWindows"] = safe.StartWithWindows;
            d["ruleTokenReferenceThreshold"] = safe.RuleTokenReferenceThreshold;
            d["lastRunUtc"] = safe.LastRunUtc ?? string.Empty;
            d["lastFilePath"] = safe.LastFilePath ?? string.Empty;
            d["lastSizeBytes"] = safe.LastSizeBytes;
            d["lastError"] = safe.LastError ?? string.Empty;
            return d;
        }

        public static CompanionSettings FromDictionary(Dictionary<string, object> dict, string geminiPath)
        {
            CompanionSettings s = new CompanionSettings();
            if (dict == null) return NormalizeSettings(s, geminiPath);

            s.Enabled = UnifiedJson.GetBool(dict, "enabled", false);
            s.IntervalHours = UnifiedJson.GetInt(dict, "intervalHours", 12);
            s.TargetDirectory = UnifiedJson.GetString(dict, "targetDirectory", string.Empty);
            s.RetainCount = UnifiedJson.GetInt(dict, "retainCount", 5);
            s.Tier = UnifiedJson.GetString(dict, "tier", "fast");
            s.MinimizeToTray = UnifiedJson.GetBool(dict, "minimizeToTray", false);
            s.GlobalHotkey = UnifiedJson.GetString(dict, "globalHotkey", string.Empty);
            s.CustomAgyCliPath = UnifiedJson.GetString(dict, "customAgyCliPath", string.Empty);
            s.VaultPath = UnifiedJson.GetString(dict, "vaultPath", string.Empty);
            s.StartWithWindows = UnifiedJson.GetBool(dict, "startWithWindows", false);
            s.RuleTokenReferenceThreshold = UnifiedJson.GetInt(dict, "ruleTokenReferenceThreshold", 0);
            s.LastRunUtc = UnifiedJson.GetString(dict, "lastRunUtc", string.Empty);
            s.LastFilePath = UnifiedJson.GetString(dict, "lastFilePath", string.Empty);
            s.LastSizeBytes = ReadLong(dict, "lastSizeBytes", 0L);
            s.LastError = UnifiedJson.GetString(dict, "lastError", string.Empty);
            return NormalizeSettings(s, geminiPath);
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

        private void EmitLog(string level, string msg)
        {
            if (_onLog != null)
            {
                try { _onLog(level, msg); } catch { }
            }
        }
    }
}
