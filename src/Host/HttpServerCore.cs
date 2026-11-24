using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace AntigravityCompanion
{
    internal sealed class ParsedHttpRequest
    {
        public string Method;
        public string RawTarget;
        public string Path;
        public Dictionary<string, string> Query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Cookies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public byte[] BodyBytes = new byte[0];
        public string BodyText = string.Empty;
    }

    internal static class HttpProtocolHelper
    {
        public static string GetAsciiStatusReason(int code)
        {
            switch (code)
            {
                case 200: return "OK";
                case 202: return "Accepted";
                case 204: return "No Content";
                case 400: return "Bad Request";
                case 403: return "Forbidden";
                case 404: return "Not Found";
                case 409: return "Conflict";
                case 413: return "Payload Too Large";
                case 431: return "Request Header Fields Too Large";
                case 500: return "Internal Server Error";
                case 501: return "Not Implemented";
                default: return "Error";
            }
        }

        public static string SanitizeHeaderValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            StringBuilder sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\r' || c == '\n' || c == '\0') continue;
                if ((c >= 0x20 && c <= 0x7E) || c == '\t')
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public static string SanitizeReasonPhrase(int code, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return GetAsciiStatusReason(code);

            StringBuilder sb = new StringBuilder(reason.Length);
            for (int i = 0; i < reason.Length; i++)
            {
                char c = reason[i];
                if (c == '\r' || c == '\n' || c == '\0') continue;
                if (c >= 0x20 && c <= 0x7E)
                {
                    sb.Append(c);
                }
            }
            string clean = sb.ToString().Trim();
            return string.IsNullOrEmpty(clean) ? GetAsciiStatusReason(code) : clean;
        }

        public static void WriteOkJson(NetworkStream stream, Dictionary<string, object> extra)
        {
            Dictionary<string, object> d = extra ?? new Dictionary<string, object>();
            d["ok"] = true;
            WriteJsonResponse(stream, 200, d);
        }

        public static void WriteStrategyOrError(NetworkStream stream, int code, string errorMsg)
        {
            if (!string.IsNullOrEmpty(errorMsg) && errorMsg.StartsWith("IDE_RUNNING:", StringComparison.Ordinal))
            {
                string[] parts = errorMsg.Split(':');
                int p = 0, ls = 0;
                if (parts.Length >= 3)
                {
                    int.TryParse(parts[1], out p);
                    int.TryParse(parts[2], out ls);
                }
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["ok"] = false;
                d["code"] = "IDE_RUNNING";
                d["parentCount"] = p;
                d["lsCount"] = ls;
                d["error"] = string.Format("检测到 {0} 个 Antigravity 主进程与 {1} 个语言服务进程正在运行，请选择写入模式。", p, ls);
                WriteJsonResponse(stream, code, d);
                return;
            }
            WriteJsonError(stream, code, errorMsg);
        }

        public static void WriteJsonError(NetworkStream stream, int code, string errorMsg)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["ok"] = false;
            d["error"] = errorMsg ?? "请求失败";
            WriteJsonResponse(stream, code, d);
        }

        public static void WriteJsonResponse(NetworkStream stream, int code, object payload)
        {
            string json = UnifiedJson.Serialize(payload, false);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            WriteRawResponse(stream, code, GetAsciiStatusReason(code), "application/json; charset=utf-8", bytes, null);
        }

        public static void WriteErrorResponse(NetworkStream stream, int code, string msg)
        {
            // FIX P2-5: Always emit strict ASCII HTTP status reason phrase on the status line; UTF-8 detail goes in body
            byte[] bytes = Encoding.UTF8.GetBytes(msg ?? GetAsciiStatusReason(code));
            WriteRawResponse(stream, code, GetAsciiStatusReason(code), "text/plain; charset=utf-8", bytes, null);
        }

        public static void WriteRawResponse(NetworkStream stream, int code, string reason, string contentType, byte[] body, string setCookieHeader)
        {
            byte[] safeBody = body ?? new byte[0];
            string safeReason = SanitizeReasonPhrase(code, reason);
            string safeContentType = SanitizeHeaderValue(contentType);

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "HTTP/1.1 {0} {1}\r\n", code, safeReason);
            sb.AppendFormat(CultureInfo.InvariantCulture, "Content-Type: {0}\r\n", safeContentType);
            sb.AppendFormat(CultureInfo.InvariantCulture, "Content-Length: {0}\r\n", safeBody.Length);
            sb.Append("Cache-Control: no-store\r\n");
            sb.Append("X-Content-Type-Options: nosniff\r\n");
            sb.Append("Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; frame-ancestors 'none'\r\n");
            if (!string.IsNullOrEmpty(setCookieHeader))
            {
                string safeCookie = SanitizeHeaderValue(setCookieHeader);
                if (!string.IsNullOrEmpty(safeCookie))
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, "Set-Cookie: {0}\r\n", safeCookie);
                }
            }
            sb.Append("Connection: close\r\n\r\n");

            byte[] hdrBytes = Encoding.ASCII.GetBytes(sb.ToString());
            stream.Write(hdrBytes, 0, hdrBytes.Length);
            if (safeBody.Length > 0) stream.Write(safeBody, 0, safeBody.Length);
            stream.Flush();
        }
    }

    /// <summary>
    /// Pure user-mode local HTTP/1.1 + SSE host (no HttpListener / no UAC).
    /// Coordinates TCP listener, authentication, port-scoped cookies (P2-1), SseHub (P2-2),
    /// OverviewBuilder, and ApiRouter.
    /// Strictly C# 5.0 (.NET Framework 4.x).
    /// </summary>
    public sealed class LocalWebHost : IDisposable
    {
        private const int MaxHeaderBytes = 64 * 1024;
        private const int MaxBodyBytes = 32 * 1024 * 1024;
        private const int MaxActivityLogEntries = 200;

        private readonly string _homePath;
        private readonly string _geminiPath;
        private string _repoPath;
        private readonly string _baselinePath;
        private readonly string _sessionToken;
        private readonly int _requestedPort;
        private readonly bool _isDefaultHome;
        private readonly ScheduledSnapshotService _snapshotService;
        private int _settingsVersion;
        private volatile bool _hotkeyRegistered;
        private volatile string _hotkeyError = string.Empty;
        private Action<int> _traySyncHandler;

        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _running;
        private volatile bool _shutdownRequested;
        private volatile bool _hasEverConnected;
        private long _lastActivityTicksUtc;
        private int _jobBusyFlag;

        private readonly object _stateLock = new object();
        private readonly object _mutationLock = new object();
        private readonly SseHub _sseHub;
        private readonly ApiRouter _apiRouter;

        private Dictionary<string, ProjectMetadata> _localProjects = new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
        private List<ConversationSummaryInfo> _localSummaries = new List<ConversationSummaryInfo>();
        private ProjectGroupsSnapshot _projectGroups = new ProjectGroupsSnapshot();
        private List<CapabilityItem> _localCaps = new List<CapabilityItem>();
        private List<CapabilityItem> _repoCaps = new List<CapabilityItem>();
        private List<CapabilityDiffNode> _capDiffs = new List<CapabilityDiffNode>();
        private Dictionary<string, string> _capBaseline = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private string _loadedBackupPath;
        private BackupArchiveIndex _backupIndex;
        private List<ProjectDiffNode> _backupSessionDiffs = new List<ProjectDiffNode>();
        private List<CapabilityDiffNode> _backupCapDiffs = new List<CapabilityDiffNode>();
        private OrphanScanResult _lastOrphanScan;

        private string _currentJobType;
        private string _currentJobMessage;
        private int _currentJobPercent;
        private readonly List<Dictionary<string, object>> _activityLogs = new List<Dictionary<string, object>>();

        public LocalWebHost(string homePath, string geminiPath, string repoPath, int requestedPort, string explicitToken)
        {
            CapabilityVaultService.EnsureZipCapabilityHookRegistered();

            _homePath = string.IsNullOrWhiteSpace(homePath) ? WorkspaceService.ResolveDefaultHomePath() : homePath.Trim();
            _geminiPath = string.IsNullOrWhiteSpace(geminiPath) ? Path.Combine(_homePath, ".gemini") : geminiPath.Trim();
            _repoPath = string.IsNullOrWhiteSpace(repoPath)
                ? CapabilityVaultService.ResolveDefaultVaultPath(null, _homePath)
                : repoPath.Trim();
            _isDefaultHome = string.Equals(_homePath, WorkspaceService.ResolveDefaultHomePath(), StringComparison.OrdinalIgnoreCase);
            _baselinePath = _isDefaultHome
                ? CapabilitySyncBaselineStore.GetDefaultStorePath()
                : Path.Combine(_geminiPath, ".companion_cap_sync_state.json");
            string snapshotSettingsPath = _isDefaultHome
                ? ScheduledSnapshotService.GetDefaultSettingsPath()
                : Path.Combine(_geminiPath, ".companion_settings.json");
            _requestedPort = requestedPort > 0 ? requestedPort : 0;
            _sessionToken = !string.IsNullOrWhiteSpace(explicitToken) ? explicitToken.Trim() : GenerateRandomToken();
            _lastActivityTicksUtc = DateTime.UtcNow.Ticks;

            _sseHub = new SseHub(TouchActivityTimestampOnly);
            _snapshotService = new ScheduledSnapshotService(_homePath, _geminiPath, snapshotSettingsPath, TryAcquireJob, ReleaseJob, AppendLog);
            _apiRouter = new ApiRouter(this);
        }

        public int Port { get; private set; }
        public string SessionToken { get { return _sessionToken; } }
        public bool ShutdownRequested { get { return _shutdownRequested; } }
        public bool HasEverConnected { get { return _hasEverConnected; } }
        public int ActiveSseCount { get { return _sseHub.ActiveCount; } }
        public DateTime LastClientActivityUtc { get { return new DateTime(Interlocked.Read(ref _lastActivityTicksUtc), DateTimeKind.Utc); } }
        public string SessionCookieName { get { return "agy_sid_" + Port.ToString(CultureInfo.InvariantCulture); } }
        public ScheduledSnapshotService SnapshotService { get { return _snapshotService; } }
        public bool IsDefaultHome { get { return _isDefaultHome; } }
        public int SettingsVersion { get { return Interlocked.CompareExchange(ref _settingsVersion, 0, 0); } }
        public bool HotkeyRegistered { get { return _hotkeyRegistered; } }
        public string HotkeyError { get { return _hotkeyError ?? string.Empty; } }

        internal string HomePath { get { return _homePath; } }
        internal string GeminiPath { get { return _geminiPath; } }
        internal string RepoPath { get { lock (_stateLock) { return _repoPath; } } }
        internal string BaselinePath { get { return _baselinePath; } }
        internal object StateLock { get { return _stateLock; } }
        internal object MutationLock { get { return _mutationLock; } }
        internal CompanionCommandDispatcher Dispatcher { get { return _apiRouter.Dispatcher; } }

        internal Dictionary<string, ProjectMetadata> LocalProjectsUnsafe { get { return _localProjects; } }
        internal List<ConversationSummaryInfo> LocalSummariesUnsafe { get { return _localSummaries; } }
        internal ProjectGroupsSnapshot ProjectGroupsUnsafe { get { return _projectGroups; } }
        internal List<CapabilityItem> LocalCapsUnsafe { get { return _localCaps; } }
        internal List<CapabilityItem> RepoCapsUnsafe { get { return _repoCaps; } }
        internal List<CapabilityDiffNode> CapDiffsUnsafe { get { return _capDiffs; } }
        internal Dictionary<string, string> CapBaselineUnsafe { get { return _capBaseline; } }
        internal string LoadedBackupPathUnsafe { get { return _loadedBackupPath; } }
        internal BackupArchiveIndex BackupIndexUnsafe { get { return _backupIndex; } }
        internal List<ProjectDiffNode> BackupSessionDiffsUnsafe { get { return _backupSessionDiffs; } }
        internal List<CapabilityDiffNode> BackupCapDiffsUnsafe { get { return _backupCapDiffs; } }
        internal OrphanScanResult LastOrphanScanUnsafe
        {
            get { return _lastOrphanScan; }
            set { _lastOrphanScan = value; }
        }

        public void Start()
        {
            ReloadAllLocalStateLocked();

            _listener = new TcpListener(IPAddress.Loopback, _requestedPort);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _running = true;

            _sseHub.Start();
            _snapshotService.Start();

            _acceptThread = new Thread(AcceptLoop);
            _acceptThread.IsBackground = true;
            _acceptThread.Name = "AntigravityCompanion_HttpAccept";
            _acceptThread.Start();
        }

        public void Stop()
        {
            _running = false;
            _shutdownRequested = true;
            try { _snapshotService.Stop(); } catch { }
            try { if (_listener != null) _listener.Stop(); } catch { }
            _sseHub.Stop();
        }

        public void Dispose()
        {
            Stop();
            try { _snapshotService.Dispose(); } catch { }
            _sseHub.Dispose();
        }

        internal void NotifySettingsChanged()
        {
            Interlocked.Increment(ref _settingsVersion);
            Action<int> handler = _traySyncHandler;
            if (handler != null)
            {
                try { handler(300); } catch { }
            }
        }

        internal void SetTraySyncHandler(Action<int> handler)
        {
            _traySyncHandler = handler;
        }

        internal void UpdateHotkeyRuntimeStatus(bool registered, string error)
        {
            _hotkeyRegistered = registered;
            _hotkeyError = error ?? string.Empty;
        }

        internal void UpdateRepoPath(string newVaultPath)
        {
            if (string.IsNullOrWhiteSpace(newVaultPath)) return;
            lock (_stateLock)
            {
                _repoPath = newVaultPath.Trim();
            }
        }

        private static string GenerateRandomToken()
        {
            byte[] buf = new byte[32];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(buf);
            }
            StringBuilder sb = new StringBuilder(64);
            for (int i = 0; i < buf.Length; i++)
                sb.Append(buf[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        internal void TouchActivity()
        {
            _hasEverConnected = true;
            Interlocked.Exchange(ref _lastActivityTicksUtc, DateTime.UtcNow.Ticks);
        }

        private void TouchActivityTimestampOnly()
        {
            Interlocked.Exchange(ref _lastActivityTicksUtc, DateTime.UtcNow.Ticks);
        }

        internal void RequestShutdown()
        {
            _shutdownRequested = true;
        }

        internal void AppendLog(string level, string message)
        {
            Dictionary<string, object> item = new Dictionary<string, object>();
            item["time"] = DateTime.Now.ToString("HH:mm:ss");
            item["level"] = level ?? "info";
            item["message"] = message ?? string.Empty;

            lock (_stateLock)
            {
                _activityLogs.Add(item);
                if (_activityLogs.Count > MaxActivityLogEntries)
                    _activityLogs.RemoveAt(0);
            }
            BroadcastSse("log", item);
        }

        internal void BroadcastSse(string eventName, object payload)
        {
            _sseHub.Broadcast(eventName, payload);
        }

        internal List<object> BuildRecentLogsListLocked()
        {
            List<object> list = new List<object>(_activityLogs.Count);
            for (int i = 0; i < _activityLogs.Count; i++) list.Add(_activityLogs[i]);
            return list;
        }

        internal Dictionary<string, object> GetCurrentJobStatusLocked()
        {
            return OverviewBuilder.BuildJobStatusDict(
                Interlocked.CompareExchange(ref _jobBusyFlag, 0, 0) != 0,
                _currentJobType,
                _currentJobMessage,
                _currentJobPercent);
        }

        internal void ReloadAllLocalStateLocked()
        {
            Dictionary<string, ProjectMetadata> projs;
            List<string> warnings;
            List<ConversationSummaryInfo> sums = WorkspaceService.LoadLocalThreeLayerSummaries(_geminiPath, out projs, out warnings);
            ProjectGroupsSnapshot groups = WorkspaceService.BuildProjectDisplayGroups(sums, projs, _homePath);

            List<CapabilityItem> lCaps, rCaps;
            List<CapabilityDiffNode> diffs;
            Dictionary<string, string> baseMap;
            WorkspaceService.LoadCapabilityMatrix(_geminiPath, _homePath, _repoPath, _baselinePath, out lCaps, out rCaps, out diffs, out baseMap);

            lock (_stateLock)
            {
                _localProjects = projs ?? new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
                _localSummaries = sums ?? new List<ConversationSummaryInfo>();
                _projectGroups = groups ?? new ProjectGroupsSnapshot();
                _localCaps = lCaps ?? new List<CapabilityItem>();
                _repoCaps = rCaps ?? new List<CapabilityItem>();
                _capDiffs = diffs ?? new List<CapabilityDiffNode>();
                _capBaseline = baseMap ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            if (warnings != null)
            {
                for (int i = 0; i < warnings.Count; i++)
                    AppendLog("warn", warnings[i]);
            }
        }

        internal void ReloadLoadedBackupLocked(string archivePath)
        {
            BackupArchiveIndex idx = StreamingZipIndexReader.ReadIndex(archivePath, _homePath, _geminiPath);

            List<ConversationSummaryInfo> localSumsSnap;
            List<CapabilityItem> localCapsSnap;
            lock (_stateLock)
            {
                localSumsSnap = new List<ConversationSummaryInfo>(_localSummaries);
                localCapsSnap = new List<CapabilityItem>(_localCaps);
            }

            List<ProjectDiffNode> sessDiffs = VisualDiffEngine.ComputeDiff(localSumsSnap, idx.Conversations, idx.SourceHome, _homePath);
            // FIX P0-8: Pass null as baseline for .agy archive capability diff
            List<CapabilityDiffNode> capDiffs = VisualDiffEngine.ComputeCapabilityDiff(localCapsSnap, idx.Capabilities, null);

            lock (_stateLock)
            {
                _loadedBackupPath = archivePath;
                _backupIndex = idx;
                _backupSessionDiffs = sessDiffs ?? new List<ProjectDiffNode>();
                _backupCapDiffs = capDiffs ?? new List<CapabilityDiffNode>();
            }
        }

        internal void UpdateBaselineForCapabilities(List<CapabilityItem> alignedCaps)
        {
            if (alignedCaps == null || alignedCaps.Count == 0) return;
            Dictionary<string, string> baselineCopy;
            lock (_stateLock)
            {
                for (int i = 0; i < alignedCaps.Count; i++)
                {
                    CapabilityItem c = alignedCaps[i];
                    if (c != null && !string.IsNullOrEmpty(c.UniqueKey) && !string.IsNullOrEmpty(c.ContentSignature))
                        _capBaseline[c.UniqueKey] = c.ContentSignature;
                }
                baselineCopy = new Dictionary<string, string>(_capBaseline, StringComparer.OrdinalIgnoreCase);
            }
            CapabilitySyncBaselineStore.Save(_baselinePath, baselineCopy);
        }

        internal bool TryAcquireJob(string jobType, string message)
        {
            if (Interlocked.CompareExchange(ref _jobBusyFlag, 1, 0) != 0) return false;
            lock (_stateLock)
            {
                _currentJobType = jobType;
                _currentJobMessage = message;
                _currentJobPercent = 5;
            }
            BroadcastSse("progress", OverviewBuilder.BuildJobStatusDict(true, jobType, message, 5));
            return true;
        }

        internal void UpdateJobProgress(string jobType, string message, int pct)
        {
            lock (_stateLock)
            {
                _currentJobMessage = message;
                _currentJobPercent = pct;
            }
            BroadcastSse("progress", OverviewBuilder.BuildJobStatusDict(true, jobType, message, pct));
        }

        internal void ReleaseJob()
        {
            lock (_stateLock)
            {
                _currentJobType = null;
                _currentJobMessage = null;
                _currentJobPercent = 0;
            }
            Interlocked.Exchange(ref _jobBusyFlag, 0);
        }

        internal void StartBackgroundJob(string jobType, Func<Dictionary<string, object>> work)
        {
            Thread t = new Thread(delegate()
            {
                Dictionary<string, object> donePayload = new Dictionary<string, object>();
                donePayload["jobType"] = jobType;
                try
                {
                    Dictionary<string, object> res = work();
                    string errMsg = res != null ? UnifiedJson.GetString(res, "errorMessage", string.Empty) : string.Empty;
                    if (string.IsNullOrEmpty(errMsg) && res != null)
                    {
                        errMsg = UnifiedJson.GetString(res, "error", string.Empty);
                    }

                    bool hasExplicitOk = res != null && res.ContainsKey("ok") && res["ok"] is bool;
                    bool explicitOk = hasExplicitOk ? (bool)res["ok"] : true;
                    // FIX P1-10: ok = string.IsNullOrEmpty(errorMessage) (and explicit ok if false)
                    bool ok = string.IsNullOrEmpty(errMsg) && explicitOk;
                    if (!ok && string.IsNullOrEmpty(errMsg))
                    {
                        errMsg = "后台任务返回失败状态。";
                    }

                    object warningsObj = (res != null && res.ContainsKey("warnings") && res["warnings"] != null)
                        ? res["warnings"]
                        : new List<object>();

                    donePayload["ok"] = ok;
                    donePayload["errorMessage"] = errMsg ?? string.Empty;
                    if (!ok) donePayload["error"] = errMsg ?? string.Empty;
                    donePayload["warnings"] = warningsObj;
                    donePayload["result"] = res;
                }
                catch (Exception ex)
                {
                    donePayload["ok"] = false;
                    donePayload["error"] = ex.Message;
                    donePayload["errorMessage"] = ex.Message;
                    donePayload["warnings"] = new List<object>();
                    AppendLog("error", "后台任务 [" + jobType + "] 失败: " + ex.Message);
                }
                finally
                {
                    ReleaseJob();
                    BroadcastSse("job_done", donePayload);
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        // ── TCP Listener & HTTP/1.1 Parser ──

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
                catch
                {
                    if (!_running) break;
                }
            }
        }

        private void HandleClient(object state)
        {
            TcpClient client = state as TcpClient;
            if (client == null) return;
            NetworkStream stream = null;
            bool keepOpenForSse = false;
            try
            {
                client.NoDelay = true;
                client.ReceiveTimeout = 30000;
                client.SendTimeout = 30000;
                stream = client.GetStream();

                int statusCode;
                string errReason;
                ParsedHttpRequest req = ReadHttpRequest(stream, out statusCode, out errReason);
                if (req == null)
                {
                    HttpProtocolHelper.WriteErrorResponse(stream, statusCode > 0 ? statusCode : 400, errReason ?? "Bad Request");
                    return;
                }

                if (!IsValidHostHeader(req))
                {
                    HttpProtocolHelper.WriteErrorResponse(stream, 403, "Invalid Host Header");
                    return;
                }

                keepOpenForSse = DispatchRequest(client, stream, req);
            }
            catch
            {
            }
            finally
            {
                if (!keepOpenForSse)
                {
                    if (stream != null) try { stream.Dispose(); } catch { }
                    try { client.Close(); } catch { }
                }
            }
        }

        private ParsedHttpRequest ReadHttpRequest(NetworkStream stream, out int errCode, out string errReason)
        {
            errCode = 0;
            errReason = null;

            byte[] chunk = new byte[8192];
            using (MemoryStream headerBuf = new MemoryStream())
            {
                int headerEndIdx = -1;
                while (headerBuf.Length < MaxHeaderBytes)
                {
                    int read = stream.Read(chunk, 0, chunk.Length);
                    if (read <= 0) break;
                    headerBuf.Write(chunk, 0, read);
                    byte[] arr = headerBuf.ToArray();
                    headerEndIdx = FindHeaderEnd(arr);
                    if (headerEndIdx >= 0)
                    {
                        int bodyStart = headerEndIdx + 4;
                        string headerStr = Encoding.UTF8.GetString(arr, 0, headerEndIdx);
                        ParsedHttpRequest req = ParseHeadersText(headerStr);
                        if (req == null)
                        {
                            errCode = 400;
                            errReason = "Malformed HTTP Request Line";
                            return null;
                        }

                        string te;
                        if (req.Headers.TryGetValue("transfer-encoding", out te) &&
                            te.IndexOf("chunked", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            errCode = 501;
                            errReason = "Chunked Transfer-Encoding Not Supported";
                            return null;
                        }

                        int contentLen = 0;
                        string clStr;
                        if (req.Headers.TryGetValue("content-length", out clStr))
                        {
                            if (!int.TryParse(clStr.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out contentLen) ||
                                contentLen < 0 || contentLen > MaxBodyBytes)
                            {
                                errCode = 413;
                                errReason = "Invalid or Oversized Content-Length";
                                return null;
                            }
                        }

                        string expectVal;
                        if (req.Headers.TryGetValue("expect", out expectVal) &&
                            expectVal.IndexOf("100-continue", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            byte[] contBytes = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
                            stream.Write(contBytes, 0, contBytes.Length);
                            stream.Flush();
                        }

                        if (contentLen > 0)
                        {
                            byte[] body = new byte[contentLen];
                            int already = arr.Length - bodyStart;
                            int copied = Math.Min(Math.Max(0, already), contentLen);
                            if (copied > 0) Buffer.BlockCopy(arr, bodyStart, body, 0, copied);
                            while (copied < contentLen)
                            {
                                int r2 = stream.Read(body, copied, contentLen - copied);
                                if (r2 <= 0)
                                {
                                    errCode = 400;
                                    errReason = "Incomplete HTTP Request Body";
                                    return null;
                                }
                                copied += r2;
                            }
                            req.BodyBytes = body;
                            req.BodyText = Encoding.UTF8.GetString(body, 0, contentLen);
                        }

                        return req;
                    }
                }

                errCode = headerBuf.Length >= MaxHeaderBytes ? 431 : 400;
                errReason = headerBuf.Length >= MaxHeaderBytes ? "Request Header Fields Too Large" : "Empty or Incomplete Request";
                return null;
            }
        }

        private static int FindHeaderEnd(byte[] arr)
        {
            if (arr == null || arr.Length < 4) return -1;
            for (int i = 0; i <= arr.Length - 4; i++)
            {
                if (arr[i] == 13 && arr[i + 1] == 10 && arr[i + 2] == 13 && arr[i + 3] == 10)
                    return i;
            }
            return -1;
        }

        private static ParsedHttpRequest ParseHeadersText(string headerStr)
        {
            if (string.IsNullOrEmpty(headerStr)) return null;
            string[] lines = headerStr.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0) return null;

            string[] reqLine = lines[0].Split(' ');
            if (reqLine.Length < 2) return null;

            ParsedHttpRequest req = new ParsedHttpRequest();
            req.Method = reqLine[0].Trim().ToUpperInvariant();
            req.RawTarget = reqLine[1].Trim();

            int qIdx = req.RawTarget.IndexOf('?');
            if (qIdx >= 0)
            {
                req.Path = req.RawTarget.Substring(0, qIdx);
                string qs = req.RawTarget.Substring(qIdx + 1);
                string[] pairs = qs.Split('&');
                for (int i = 0; i < pairs.Length; i++)
                {
                    if (string.IsNullOrEmpty(pairs[i])) continue;
                    int eq = pairs[i].IndexOf('=');
                    string k = eq >= 0 ? pairs[i].Substring(0, eq) : pairs[i];
                    string v = eq >= 0 ? pairs[i].Substring(eq + 1) : string.Empty;
                    try
                    {
                        k = Uri.UnescapeDataString(k.Replace('+', ' '));
                        v = Uri.UnescapeDataString(v.Replace('+', ' '));
                    }
                    catch { }
                    if (!string.IsNullOrEmpty(k)) req.Query[k] = v;
                }
            }
            else
            {
                req.Path = req.RawTarget;
            }

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string hKey = line.Substring(0, colon).Trim();
                string hVal = line.Substring(colon + 1).Trim();
                req.Headers[hKey] = hVal;
            }

            string cookieHdr;
            if (req.Headers.TryGetValue("cookie", out cookieHdr) && !string.IsNullOrEmpty(cookieHdr))
            {
                string[] parts = cookieHdr.Split(';');
                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i].Trim();
                    int eq = part.IndexOf('=');
                    if (eq <= 0) continue;
                    string ck = part.Substring(0, eq).Trim();
                    string cv = part.Substring(eq + 1).Trim();
                    req.Cookies[ck] = cv;
                }
            }

            return req;
        }

        private bool IsValidHostHeader(ParsedHttpRequest req)
        {
            string host;
            if (!req.Headers.TryGetValue("host", out host) || string.IsNullOrWhiteSpace(host))
                return false;
            string expected1 = "127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture);
            string expected2 = "localhost:" + Port.ToString(CultureInfo.InvariantCulture);
            return string.Equals(host.Trim(), expected1, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(host.Trim(), expected2, StringComparison.OrdinalIgnoreCase);
        }

        private bool HasValidQueryToken(ParsedHttpRequest req)
        {
            string qTok;
            return req.Query.TryGetValue("t", out qTok) && FixedTimeEquals(qTok, _sessionToken);
        }

        private bool HasValidCookieToken(ParsedHttpRequest req)
        {
            string cTok;
            // FIX P2-1: Port-scoped session cookie agy_sid_<port> prevents cross-instance cookie clobbering
            return req.Cookies.TryGetValue(SessionCookieName, out cTok) && FixedTimeEquals(cTok, _sessionToken);
        }

        private bool HasValidHeaderToken(ParsedHttpRequest req)
        {
            string hTok;
            return req.Headers.TryGetValue("x-agy-token", out hTok) && FixedTimeEquals(hTok, _sessionToken);
        }

        private bool IsSameOriginHeaderValid(ParsedHttpRequest req)
        {
            string origin;
            if (!req.Headers.TryGetValue("origin", out origin) || string.IsNullOrWhiteSpace(origin))
                return false;
            string exp1 = "http://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture);
            string exp2 = "http://localhost:" + Port.ToString(CultureInfo.InvariantCulture);
            return string.Equals(origin.Trim(), exp1, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(origin.Trim(), exp2, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsAuthorizedApiRequest(ParsedHttpRequest req)
        {
            bool hasToken = HasValidHeaderToken(req);
            bool hasCookie = HasValidCookieToken(req);
            if (!hasToken && !hasCookie) return false;

            if (string.Equals(req.Method, "POST", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(req.Method, "PUT", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(req.Method, "DELETE", StringComparison.OrdinalIgnoreCase))
            {
                if (hasToken) return true;
                return hasCookie && IsSameOriginHeaderValid(req);
            }

            return true;
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= (a[i] ^ b[i]);
            return diff == 0;
        }

        private static readonly Dictionary<string, string[]> StaticAssetRoutes = BuildStaticAssetRoutes();

        private static Dictionary<string, string[]> BuildStaticAssetRoutes()
        {
            Dictionary<string, string[]> map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            map["/styles.css"] = new string[] { "styles.css", "text/css; charset=utf-8" };
            map["/app.js"] = new string[] { "app.js", "application/javascript; charset=utf-8" };
            map["/js/api.js"] = new string[] { "js/api.js", "application/javascript; charset=utf-8" };
            map["/js/state.js"] = new string[] { "js/state.js", "application/javascript; charset=utf-8" };
            map["/js/components.js"] = new string[] { "js/components.js", "application/javascript; charset=utf-8" };
            map["/js/views/overview.js"] = new string[] { "js/views/overview.js", "application/javascript; charset=utf-8" };
            map["/js/views/cockpit.js"] = new string[] { "js/views/cockpit.js", "application/javascript; charset=utf-8" };
            map["/js/views/sessions.js"] = new string[] { "js/views/sessions.js", "application/javascript; charset=utf-8" };
            map["/js/views/storage.js"] = new string[] { "js/views/storage.js", "application/javascript; charset=utf-8" };
            map["/js/views/vault.js"] = new string[] { "js/views/vault.js", "application/javascript; charset=utf-8" };
            map["/js/views/backup.js"] = new string[] { "js/views/backup.js", "application/javascript; charset=utf-8" };
            return map;
        }

        private bool DispatchRequest(TcpClient client, NetworkStream stream, ParsedHttpRequest req)
        {
            string path = (req.Path ?? "/").Trim();
            if (string.IsNullOrEmpty(path)) path = "/";

            if (string.Equals(req.Method, "GET", StringComparison.OrdinalIgnoreCase) && path == "/favicon.ico")
            {
                HttpProtocolHelper.WriteRawResponse(stream, 204, "No Content", "image/x-icon", new byte[0], null);
                return false;
            }

            // 1. Embedded UI static resources
            if (string.Equals(req.Method, "GET", StringComparison.OrdinalIgnoreCase) &&
                (path == "/" || path == "/index.html"))
            {
                bool queryOk = HasValidQueryToken(req);
                bool cookieOk = HasValidCookieToken(req);
                if (!queryOk && !cookieOk)
                {
                    HttpProtocolHelper.WriteErrorResponse(stream, 403, "Forbidden: Missing or invalid session token");
                    return false;
                }
                TouchActivity();
                string setCookie = queryOk
                    ? (SessionCookieName + "=" + _sessionToken + "; Path=/; HttpOnly; SameSite=Strict")
                    : null;
                byte[] htmlBytes = LoadStaticAssetBytes("index.html");
                HttpProtocolHelper.WriteRawResponse(stream, 200, "OK", "text/html; charset=utf-8", htmlBytes, setCookie);
                return false;
            }

            string[] staticEntry;
            if (string.Equals(req.Method, "GET", StringComparison.OrdinalIgnoreCase) &&
                StaticAssetRoutes.TryGetValue(path, out staticEntry))
            {
                if (!HasValidCookieToken(req) && !HasValidQueryToken(req) && !HasValidHeaderToken(req))
                {
                    HttpProtocolHelper.WriteErrorResponse(stream, 403, "Forbidden");
                    return false;
                }
                HttpProtocolHelper.WriteRawResponse(stream, 200, "OK", staticEntry[1], LoadStaticAssetBytes(staticEntry[0]), null);
                return false;
            }

            // 2. Session token recovery endpoint for F5 reload
            if (string.Equals(req.Method, "GET", StringComparison.OrdinalIgnoreCase) && path == "/api/auth/session")
            {
                if (!HasValidCookieToken(req) && !HasValidHeaderToken(req) && !HasValidQueryToken(req))
                {
                    HttpProtocolHelper.WriteErrorResponse(stream, 403, "Forbidden");
                    return false;
                }
                TouchActivity();
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["token"] = _sessionToken;
                HttpProtocolHelper.WriteJsonResponse(stream, 200, d);
                return false;
            }

            // 3. SSE event stream (non-blocking handoff to SseHub pump thread -> P2-2)
            if (string.Equals(req.Method, "GET", StringComparison.OrdinalIgnoreCase) && path == "/api/events")
            {
                if (!HasValidCookieToken(req) && !HasValidQueryToken(req) && !HasValidHeaderToken(req))
                {
                    HttpProtocolHelper.WriteErrorResponse(stream, 403, "Forbidden");
                    return false;
                }
                TouchActivity();
                string headers =
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: text/event-stream; charset=utf-8\r\n" +
                    "Cache-Control: no-store\r\n" +
                    "Connection: close\r\n" +
                    "X-Content-Type-Options: nosniff\r\n\r\n" +
                    "event: connected\ndata: {\"ok\":true}\n\n";
                byte[] initBytes = Encoding.UTF8.GetBytes(headers);
                stream.Write(initBytes, 0, initBytes.Length);
                stream.Flush();

                _sseHub.RegisterClient(client, stream);
                return true;
            }

            // 4. Protected API routes
            if (!IsAuthorizedApiRequest(req))
            {
                HttpProtocolHelper.WriteJsonError(stream, 403, "未经授权的本地 API 访问（无效或缺失访问令牌）。");
                return false;
            }
            TouchActivity();

            try
            {
                _apiRouter.RouteApi(stream, req, path);
            }
            catch (Exception ex)
            {
                AppendLog("error", "API 异常 [" + path + "]: " + ex.Message);
                HttpProtocolHelper.WriteJsonError(stream, 500, ex.Message);
            }
            return false;
        }

        private static byte[] LoadStaticAssetBytes(string relAssetPath)
        {
            string normRel = (relAssetPath ?? string.Empty).Replace('\\', '/').TrimStart('/');
            string resName = "AntigravityCompanion.ui." + normRel.Replace('/', '.');
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream(resName))
                {
                    if (s != null)
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            s.CopyTo(ms);
                            return ms.ToArray();
                        }
                    }
                }
            }
            catch { }

            string diskRel = normRel.Replace('/', Path.DirectorySeparatorChar);
            string[] candidates = new string[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui", diskRel),
                Path.Combine(Directory.GetCurrentDirectory(), "ui", diskRel)
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                try
                {
                    if (File.Exists(candidates[i]))
                        return File.ReadAllBytes(candidates[i]);
                }
                catch { }
            }

            return Encoding.UTF8.GetBytes("/* Missing UI asset: " + normRel + " */");
        }
    }
}
