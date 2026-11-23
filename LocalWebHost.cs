using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    // ══════════════════════════════════════════════════════════════════════
    // PURE USER-MODE LOCAL HTTP/1.1 + SSE HOST (No HttpListener / No UAC)
    // Strictly C# 5.0 (.NET Framework 4.0+ csc.exe)
    // ══════════════════════════════════════════════════════════════════════

    public sealed class LocalWebHost : IDisposable
    {
        private const int MaxHeaderBytes = 64 * 1024;
        private const int MaxBodyBytes = 32 * 1024 * 1024;
        private const int MaxActivityLogEntries = 200;

        private readonly string _homePath;
        private readonly string _geminiPath;
        private readonly string _repoPath;
        private readonly string _baselinePath;
        private readonly string _sessionToken;
        private readonly int _requestedPort;

        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _running;
        private volatile bool _shutdownRequested;
        private volatile bool _hasEverConnected;
        private int _activeSseCount;
        private long _lastActivityTicksUtc;
        private int _jobBusyFlag;

        private readonly object _stateLock = new object();
        private readonly object _mutationLock = new object();
        private readonly object _sseLock = new object();
        private readonly List<SseClient> _sseClients = new List<SseClient>();

        // 状态缓存
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

        private sealed class SseClient
        {
            public readonly object QueueLock = new object();
            public readonly Queue<string> Queue = new Queue<string>();
            public readonly AutoResetEvent Signal = new AutoResetEvent(false);
            public volatile bool Closed;
        }

        private sealed class ParsedHttpRequest
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

        public LocalWebHost(string homePath, string geminiPath, string repoPath, int requestedPort, string explicitToken)
        {
            _homePath = string.IsNullOrWhiteSpace(homePath) ? WorkspaceService.ResolveDefaultHomePath() : homePath.Trim();
            _geminiPath = string.IsNullOrWhiteSpace(geminiPath) ? Path.Combine(_homePath, ".gemini") : geminiPath.Trim();
            _repoPath = string.IsNullOrWhiteSpace(repoPath) ? CapabilityRepositoryManager.ResolveDefaultRepositoryPath() : repoPath.Trim();
            _baselinePath = string.Equals(_homePath, WorkspaceService.ResolveDefaultHomePath(), StringComparison.OrdinalIgnoreCase)
                ? CapabilitySyncBaselineStore.GetDefaultStorePath()
                : Path.Combine(_geminiPath, ".companion_cap_sync_state.json");
            _requestedPort = requestedPort > 0 ? requestedPort : 0;
            _sessionToken = !string.IsNullOrWhiteSpace(explicitToken) ? explicitToken.Trim() : GenerateRandomToken();
            _lastActivityTicksUtc = DateTime.UtcNow.Ticks;
        }

        public int Port { get; private set; }
        public string SessionToken { get { return _sessionToken; } }
        public bool ShutdownRequested { get { return _shutdownRequested; } }
        public bool HasEverConnected { get { return _hasEverConnected; } }
        public int ActiveSseCount { get { return Interlocked.CompareExchange(ref _activeSseCount, 0, 0); } }
        public DateTime LastClientActivityUtc { get { return new DateTime(Interlocked.Read(ref _lastActivityTicksUtc), DateTimeKind.Utc); } }

        public void Start()
        {
            ReloadAllLocalStateLocked();

            _listener = new TcpListener(IPAddress.Loopback, _requestedPort);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _running = true;

            _acceptThread = new Thread(AcceptLoop);
            _acceptThread.IsBackground = true;
            _acceptThread.Start();
        }

        public void Stop()
        {
            _running = false;
            _shutdownRequested = true;
            try { if (_listener != null) _listener.Stop(); } catch { }

            lock (_sseLock)
            {
                for (int i = 0; i < _sseClients.Count; i++)
                {
                    _sseClients[i].Closed = true;
                    try { _sseClients[i].Signal.Set(); } catch { }
                }
                _sseClients.Clear();
            }
        }

        public void Dispose()
        {
            Stop();
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

        private void TouchActivity()
        {
            _hasEverConnected = true;
            Interlocked.Exchange(ref _lastActivityTicksUtc, DateTime.UtcNow.Ticks);
        }

        private void AppendLog(string level, string message)
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

        private void BroadcastSse(string eventName, object payload)
        {
            Dictionary<string, object> envelope = new Dictionary<string, object>();
            envelope["event"] = eventName;
            envelope["payload"] = payload;
            string json = MiniJson.Serialize(envelope, false);
            string frame = "event: " + eventName + "\ndata: " + json + "\n\n";

            lock (_sseLock)
            {
                for (int i = 0; i < _sseClients.Count; i++)
                {
                    SseClient c = _sseClients[i];
                    if (c.Closed) continue;
                    lock (c.QueueLock)
                    {
                        c.Queue.Enqueue(frame);
                    }
                    try { c.Signal.Set(); } catch { }
                }
            }
        }

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
            try
            {
                client.NoDelay = true;
                client.ReceiveTimeout = 30000;
                client.SendTimeout = 30000;
                using (NetworkStream stream = client.GetStream())
                {
                    int statusCode;
                    string errReason;
                    ParsedHttpRequest req = ReadHttpRequest(stream, out statusCode, out errReason);
                    if (req == null)
                    {
                        WriteErrorResponse(stream, statusCode > 0 ? statusCode : 400, errReason ?? "Bad Request");
                        return;
                    }

                    if (!IsValidHostHeader(req))
                    {
                        WriteErrorResponse(stream, 403, "Invalid Host Header");
                        return;
                    }

                    DispatchRequest(stream, req);
                }
            }
            catch
            {
            }
            finally
            {
                try { client.Close(); } catch { }
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
            return req.Cookies.TryGetValue("agy_sid", out cTok) && FixedTimeEquals(cTok, _sessionToken);
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

        private void DispatchRequest(NetworkStream stream, ParsedHttpRequest req)
        {
            string path = (req.Path ?? "/").Trim();
            if (string.IsNullOrEmpty(path)) path = "/";

            // 1. 静态页面与脚本/样式资源
            if (string.Equals(req.Method, "GET", StringComparison.OrdinalIgnoreCase) &&
                (path == "/" || path == "/index.html"))
            {
                bool queryOk = HasValidQueryToken(req);
                bool cookieOk = HasValidCookieToken(req);
                if (!queryOk && !cookieOk)
                {
                    WriteErrorResponse(stream, 403, "Forbidden: Missing or invalid session token");
                    return;
                }
                TouchActivity();
                string setCookie = queryOk
                    ? ("agy_sid=" + _sessionToken + "; Path=/; HttpOnly; SameSite=Strict")
                    : null;
                byte[] htmlBytes = LoadStaticAssetBytes("index.html");
                WriteRawResponse(stream, 200, "OK", "text/html; charset=utf-8", htmlBytes, setCookie);
                return;
            }

            if (string.Equals(req.Method, "GET", StringComparison.OrdinalIgnoreCase) && path == "/styles.css")
            {
                if (!HasValidCookieToken(req) && !HasValidQueryToken(req) && !HasValidHeaderToken(req))
                {
                    WriteErrorResponse(stream, 403, "Forbidden");
                    return;
                }
                WriteRawResponse(stream, 200, "OK", "text/css; charset=utf-8", LoadStaticAssetBytes("styles.css"), null);
                return;
            }

            if (string.Equals(req.Method, "GET", StringComparison.OrdinalIgnoreCase) && path == "/app.js")
            {
                if (!HasValidCookieToken(req) && !HasValidQueryToken(req) && !HasValidHeaderToken(req))
                {
                    WriteErrorResponse(stream, 403, "Forbidden");
                    return;
                }
                WriteRawResponse(stream, 200, "OK", "application/javascript; charset=utf-8", LoadStaticAssetBytes("app.js"), null);
                return;
            }

            // 2. 会话令牌恢复接口（供 F5 刷新后免内联脚本恢复 X-AGY-Token）
            if (string.Equals(req.Method, "GET", StringComparison.OrdinalIgnoreCase) && path == "/api/auth/session")
            {
                if (!HasValidCookieToken(req) && !HasValidHeaderToken(req) && !HasValidQueryToken(req))
                {
                    WriteErrorResponse(stream, 403, "Forbidden");
                    return;
                }
                TouchActivity();
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["token"] = _sessionToken;
                WriteJsonResponse(stream, 200, d);
                return;
            }

            // 3. SSE 事件流
            if (string.Equals(req.Method, "GET", StringComparison.OrdinalIgnoreCase) && path == "/api/events")
            {
                if (!HasValidCookieToken(req) && !HasValidQueryToken(req) && !HasValidHeaderToken(req))
                {
                    WriteErrorResponse(stream, 403, "Forbidden");
                    return;
                }
                ServeSseStream(stream);
                return;
            }

            // 4. 其余 API 统一校验
            if (!IsAuthorizedApiRequest(req))
            {
                WriteJsonError(stream, 403, "未经授权的本地 API 访问（无效或缺失访问令牌）。");
                return;
            }
            TouchActivity();

            try
            {
                RouteApi(stream, req, path);
            }
            catch (Exception ex)
            {
                AppendLog("error", "API 异常 [" + path + "]: " + ex.Message);
                WriteJsonError(stream, 500, ex.Message);
            }
        }

        private void ServeSseStream(NetworkStream stream)
        {
            TouchActivity();
            SseClient client = new SseClient();
            lock (_sseLock) _sseClients.Add(client);
            Interlocked.Increment(ref _activeSseCount);

            try
            {
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

                while (_running && !client.Closed)
                {
                    client.Signal.WaitOne(10000);
                    if (!_running || client.Closed) break;

                    List<string> pending = null;
                    lock (client.QueueLock)
                    {
                        if (client.Queue.Count > 0)
                        {
                            pending = new List<string>(client.Queue.Count);
                            while (client.Queue.Count > 0)
                                pending.Add(client.Queue.Dequeue());
                        }
                    }

                    if (pending != null && pending.Count > 0)
                    {
                        for (int i = 0; i < pending.Count; i++)
                        {
                            byte[] fb = Encoding.UTF8.GetBytes(pending[i]);
                            stream.Write(fb, 0, fb.Length);
                        }
                        stream.Flush();
                    }
                    else
                    {
                        byte[] ping = Encoding.UTF8.GetBytes(": ping\n\n");
                        stream.Write(ping, 0, ping.Length);
                        stream.Flush();
                    }
                    Interlocked.Exchange(ref _lastActivityTicksUtc, DateTime.UtcNow.Ticks);
                }
            }
            catch
            {
            }
            finally
            {
                client.Closed = true;
                Interlocked.Decrement(ref _activeSseCount);
                lock (_sseLock) _sseClients.Remove(client);
                try { client.Signal.Dispose(); } catch { }
            }
        }

        private byte[] LoadStaticAssetBytes(string fileName)
        {
            string resName = "AntigravityCompanion.ui." + fileName;
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

            // 磁盘开发回退路径
            string[] candidates = new string[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui", fileName),
                Path.Combine(Directory.GetCurrentDirectory(), "ui", fileName)
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

            return Encoding.UTF8.GetBytes("/* Missing UI asset: " + fileName + " */");
        }

        private void RouteApi(NetworkStream stream, ParsedHttpRequest req, string path)
        {
            IDictionary<string, object> body = ParseJsonBody(req.BodyText);

            if (path == "/api/app/heartbeat" && req.Method == "POST")
            {
                TouchActivity();
                WriteOkJson(stream, null);
                return;
            }
            if (path == "/api/app/shutdown" && req.Method == "POST")
            {
                WriteOkJson(stream, null);
                _shutdownRequested = true;
                return;
            }

            if (path == "/api/overview" && req.Method == "GET")
            {
                WriteJsonResponse(stream, 200, BuildOverviewPayload());
                return;
            }
            if (path == "/api/jobs/current" && req.Method == "GET")
            {
                lock (_stateLock)
                {
                    WriteJsonResponse(stream, 200, BuildJobStatusDict(
                        Interlocked.CompareExchange(ref _jobBusyFlag, 0, 0) != 0,
                        _currentJobType, _currentJobMessage, _currentJobPercent));
                }
                return;
            }
            if (path == "/api/refresh" && req.Method == "POST")
            {
                lock (_mutationLock)
                {
                    ReloadAllLocalStateLocked();
                    if (!string.IsNullOrEmpty(_loadedBackupPath) && File.Exists(_loadedBackupPath))
                        ReloadLoadedBackupLocked(_loadedBackupPath);
                }
                AppendLog("info", "已刷新本机三层数据与内置能力仓库状态。");
                WriteJsonResponse(stream, 200, BuildOverviewPayload());
                return;
            }

            if (path == "/api/ide/status" && req.Method == "GET")
            {
                WriteJsonResponse(stream, 200, BuildIdeStatusDict());
                return;
            }
            if (path == "/api/ide/close" && req.Method == "POST")
            {
                string closeErr;
                bool ok = ProcessGuardService.CloseAntigravityProcesses(true,
                    delegate(string msg) { AppendLog("info", msg); }, out closeErr);
                if (!ok)
                {
                    WriteJsonError(stream, 409, closeErr ?? "无法完全关闭 Antigravity 进程。");
                    return;
                }
                AppendLog("success", "已安全关闭 Antigravity 相关进程。");
                Dictionary<string, object> d = BuildIdeStatusDict();
                d["ok"] = true;
                WriteJsonResponse(stream, 200, d);
                return;
            }

            if (path == "/api/gc/scan" && req.Method == "POST")
            {
                OrphanScanResult scan = WorkspaceService.ScanOrphansReadOnly(_geminiPath);
                lock (_stateLock) { _lastOrphanScan = scan; }
                AppendLog("info", string.Format("孤儿只读体检完成：发现 {0} 个可清理项（{1:F2} MB）。",
                    scan.TotalOrphanItems, scan.TotalOrphanMegabytes));
                WriteJsonResponse(stream, 200, SerializeOrphanScan(scan));
                return;
            }
            if (path == "/api/gc/clean" && req.Method == "POST")
            {
                if (!TryAcquireJob("gc_clean", "正在执行两阶段安全孤儿清理..."))
                {
                    WriteJsonError(stream, 409, "当前有其他后台写入任务正在执行，请稍后再试。");
                    return;
                }
                try
                {
                    bool closeIde = MiniJson.GetBool(body, "closeIdeIfRunning", false);
                    DeletionResult delRes;
                    string err;
                    lock (_mutationLock)
                    {
                        bool ok = WorkspaceService.CleanOrphansSafe(_geminiPath, closeIde,
                            delegate(string m) { AppendLog("info", m); }, out delRes, out err);
                        if (!ok)
                        {
                            WriteJsonError(stream, 409, err ?? "孤儿清理未执行。");
                            return;
                        }
                        _lastOrphanScan = WorkspaceService.ScanOrphansReadOnly(_geminiPath);
                        ReloadAllLocalStateLocked();
                    }
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["deletedSessions"] = delRes.DeletedSessionCount;
                    resp["deletedDbFiles"] = delRes.DeletedDbFilesCount;
                    resp["deletedBrainDirs"] = delRes.DeletedBrainDirsCount;
                    resp["deletedAnnotations"] = delRes.DeletedAnnotationsCount;
                    resp["freedMegabytes"] = Math.Round(delRes.FreedMegabytes, 2);
                    AppendLog("success", string.Format("孤儿清理完成：清理数据库 {0} 个、脑区 {1} 个、标注 {2} 个，释放 {3:F2} MB。",
                        delRes.DeletedDbFilesCount, delRes.DeletedBrainDirsCount, delRes.DeletedAnnotationsCount, delRes.FreedMegabytes));
                    WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    ReleaseJob();
                }
                return;
            }

            if (path == "/api/sessions" && req.Method == "GET")
            {
                WriteJsonResponse(stream, 200, BuildSessionsPayload());
                return;
            }
            if (path == "/api/sessions/search" && req.Method == "POST")
            {
                string q = MiniJson.GetString(body, "query", string.Empty);
                bool searchContent = MiniJson.GetBool(body, "searchContent", false);
                string err = null;
                HashSet<string> hitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string trimmedQ = (q ?? string.Empty).Trim();

                if (!string.IsNullOrEmpty(trimmedQ))
                {
                    if (searchContent)
                    {
                        string dbPath = Path.Combine(_geminiPath, "antigravity", "conversation_summaries.db");
                        if (File.Exists(dbPath))
                        {
                            HashSet<string> dbHits = SummarySqliteBridge.SearchConversationsFullText(dbPath, trimmedQ);
                            if (!string.IsNullOrEmpty(SummarySqliteBridge.LastError))
                                err = SummarySqliteBridge.LastError;
                            if (dbHits != null)
                            {
                                foreach (string id in dbHits) hitIds.Add(id);
                            }
                        }
                    }

                    lock (_stateLock)
                    {
                        for (int i = 0; i < _localSummaries.Count; i++)
                        {
                            ConversationSummaryInfo s = _localSummaries[i];
                            if (s == null || string.IsNullOrEmpty(s.ConversationId)) continue;
                            if ((s.DisplayTitle != null && s.DisplayTitle.IndexOf(trimmedQ, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (s.ProjectName != null && s.ProjectName.IndexOf(trimmedQ, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (searchContent && s.FirstPromptSnippet != null && s.FirstPromptSnippet.IndexOf(trimmedQ, StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                hitIds.Add(s.ConversationId);
                            }
                        }
                    }
                }

                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["matchedIds"] = new List<object>(ToObjList(hitIds));
                resp["error"] = err ?? string.Empty;
                resp["searchContent"] = searchContent;
                WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/sessions/markdown" && req.Method == "GET")
            {
                string cid;
                req.Query.TryGetValue("cid", out cid);
                string source;
                req.Query.TryGetValue("source", out source);
                bool fromLocal = !string.Equals(source, "backup", StringComparison.OrdinalIgnoreCase);

                if (!SessionMetadataExtractor.IsLikelyConversationId(cid))
                {
                    WriteJsonError(stream, 400, "无效的会话 ID。");
                    return;
                }

                ConversationSummaryInfo info = FindSession(cid, fromLocal);
                ConversationSummaryInfo safeInfo = info ?? new ConversationSummaryInfo { ConversationId = cid.Trim() };
                bool truncated;
                string md = fromLocal
                    ? TranscriptMarkdownExporter.PreviewLocalSessionMarkdown(safeInfo, _geminiPath, 260000, out truncated)
                    : TranscriptMarkdownExporter.PreviewBackupSessionMarkdown(safeInfo, _loadedBackupPath, 260000, out truncated);

                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["cid"] = cid.Trim();
                resp["title"] = info != null ? (info.DisplayTitle ?? cid.Trim()) : cid.Trim();
                resp["projectName"] = info != null ? (info.ProjectName ?? string.Empty) : string.Empty;
                resp["stepCount"] = info != null ? info.StepCount : 0;
                resp["updatedTime"] = (info != null && info.UpdatedTime > DateTime.MinValue) ? info.UpdatedTime.ToString("yyyy-MM-dd HH:mm:ss") : "-";
                resp["isCli"] = info != null && info.IsCliSession;
                resp["truncated"] = truncated;
                resp["markdown"] = md ?? string.Empty;
                WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/sessions/export-md" && req.Method == "POST")
            {
                string cid = MiniJson.GetString(body, "cid", string.Empty).Trim();
                string source = MiniJson.GetString(body, "source", "local");
                string outPath = MiniJson.GetString(body, "outputPath", string.Empty);
                bool fromLocal = !string.Equals(source, "backup", StringComparison.OrdinalIgnoreCase);
                if (!SessionMetadataExtractor.IsLikelyConversationId(cid))
                {
                    WriteJsonError(stream, 400, "无效的会话 ID。");
                    return;
                }
                ConversationSummaryInfo info = FindSession(cid, fromLocal);
                ConversationSummaryInfo safeInfo = info ?? new ConversationSummaryInfo { ConversationId = cid };
                if (string.IsNullOrWhiteSpace(outPath))
                {
                    string safeTitle = SanitizeFileName(safeInfo.DisplayTitle ?? cid);
                    bool dlgBusy;
                    outPath = NativeDialogBridge.PickSaveFile("导出会话记录为 Markdown", "Markdown 文件 (*.md)|*.md|所有文件 (*.*)|*.*", safeTitle + ".md", out dlgBusy);
                    if (dlgBusy) { WriteJsonError(stream, 409, "已有文件选择窗口正在打开。"); return; }
                    if (string.IsNullOrEmpty(outPath))
                    {
                        Dictionary<string, object> cancelled = new Dictionary<string, object>();
                        cancelled["cancelled"] = true;
                        WriteJsonResponse(stream, 200, cancelled);
                        return;
                    }
                }
                if (fromLocal)
                    TranscriptMarkdownExporter.ExportLocalSessionToMarkdown(safeInfo, _geminiPath, outPath);
                else
                    TranscriptMarkdownExporter.ExportBackupSessionToMarkdown(safeInfo, _loadedBackupPath, outPath);
                AppendLog("success", "已导出 Markdown 对话记录：" + outPath);
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = true;
                resp["outputPath"] = outPath;
                resp["truncated"] = false;
                WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/sessions/rename-single" && req.Method == "POST")
            {
                string cid = MiniJson.GetString(body, "cid", string.Empty).Trim();
                string newTitle = MiniJson.GetString(body, "newTitle", string.Empty).Trim();
                string strategyStr = MiniJson.GetString(body, "strategy", "auto");
                if (!SessionMetadataExtractor.IsLikelyConversationId(cid) || string.IsNullOrWhiteSpace(newTitle))
                {
                    WriteJsonError(stream, 400, "会话 ID 或新标题不能为空。");
                    return;
                }
                Dictionary<string, string> cidMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                cidMap[cid] = newTitle;
                RenameBatchResult res;
                int errCode;
                string errMsg;
                if (!ExecuteRenameInternal(cidMap, strategyStr, out res, out errCode, out errMsg))
                {
                    WriteStrategyOrError(stream, errCode, errMsg);
                    return;
                }
                WriteJsonResponse(stream, 200, SerializeBatchRenameResult(res));
                return;
            }
            if (path == "/api/sessions/move" && req.Method == "POST")
            {
                List<string> cids = ReadStringArray(body, "cids");
                string targetProjId = MiniJson.GetString(body, "targetProjectId", string.Empty).Trim();
                string targetProjName = MiniJson.GetString(body, "targetProjectName", string.Empty).Trim();
                string targetFolderPath = MiniJson.GetString(body, "targetFolderPath", string.Empty).Trim();
                bool closeIde = MiniJson.GetBool(body, "closeIdeIfRunning", false);

                if (cids.Count == 0 || string.IsNullOrWhiteSpace(targetProjName))
                {
                    WriteJsonError(stream, 400, "请选择待移动的会话及目标工程名称。");
                    return;
                }
                bool isOut = VisualDiffEngine.IsOutsideProject(targetProjName);
                string normProjId = isOut ? "outside-of-project" : (string.IsNullOrEmpty(targetProjId) ? Guid.NewGuid().ToString() : targetProjId);
                string normUri = isOut ? string.Empty : LocalWorkspaceManager.ToFileUri(targetFolderPath);

                if (!EnsureIdeClosedIfNeeded(closeIde, stream)) return;

                int moved;
                string warn;
                lock (_mutationLock)
                {
                    moved = LocalWorkspaceManager.MoveSessionsToProject(_geminiPath, cids, targetProjName, normProjId, normUri);
                    warn = LocalWorkspaceManager.LastWarning;
                    ReloadAllLocalStateLocked();
                }
                AppendLog("success", string.Format("已迁移 {0} 条会话至工程 [{1}]。", moved, targetProjName));
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = true;
                resp["moved"] = moved;
                resp["warning"] = warn ?? string.Empty;
                WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/sessions/delete" && req.Method == "POST")
            {
                List<string> cidArr = ReadStringArray(body, "cids");
                List<string> projIdArr = ReadStringArray(body, "projectIds");
                List<string> ghostArr = ReadStringArray(body, "ghostKeys");
                bool closeIde = MiniJson.GetBool(body, "closeIdeIfRunning", false);

                if (cidArr.Count == 0 && projIdArr.Count == 0 && ghostArr.Count == 0)
                {
                    WriteJsonError(stream, 400, "请先选择要删除的会话或工程。");
                    return;
                }
                if (!EnsureIdeClosedIfNeeded(closeIde, stream)) return;

                DeletionResult delRes;
                string warn;
                lock (_mutationLock)
                {
                    HashSet<string> cidSet = new HashSet<string>(cidArr, StringComparer.OrdinalIgnoreCase);
                    HashSet<string> pIdSet = new HashSet<string>(projIdArr, StringComparer.OrdinalIgnoreCase);
                    HashSet<string> ghostSet = new HashSet<string>(ghostArr, StringComparer.OrdinalIgnoreCase);
                    delRes = LocalWorkspaceManager.DeleteSessionsAndProjects(_geminiPath, _localSummaries, _localProjects, cidSet, null, pIdSet, ghostSet);
                    warn = LocalWorkspaceManager.LastWarning;
                    ReloadAllLocalStateLocked();
                }
                AppendLog("success", string.Format("已物理删除 {0} 条会话（含子任务），释放 {1:F2} MB。", delRes.DeletedSessionCount, delRes.FreedMegabytes));
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = true;
                resp["deletedSessions"] = delRes.DeletedSessionCount;
                resp["freedMegabytes"] = Math.Round(delRes.FreedMegabytes, 2);
                resp["warning"] = warn ?? string.Empty;
                WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/projects/relocate" && req.Method == "POST")
            {
                string projId = MiniJson.GetString(body, "projectId", string.Empty).Trim();
                string projName = MiniJson.GetString(body, "projectName", string.Empty).Trim();
                string newFolderPath = MiniJson.GetString(body, "newFolderPath", string.Empty).Trim();
                bool closeIde = MiniJson.GetBool(body, "closeIdeIfRunning", false);

                if (string.IsNullOrEmpty(projName) || string.IsNullOrEmpty(newFolderPath))
                {
                    WriteJsonError(stream, 400, "工程名称与新物理目录不能为空。");
                    return;
                }
                if (!EnsureIdeClosedIfNeeded(closeIde, stream)) return;

                int updated;
                string warn;
                lock (_mutationLock)
                {
                    updated = LocalWorkspaceManager.RelocateProjectPath(_geminiPath, projId, projName, newFolderPath, _localSummaries);
                    warn = LocalWorkspaceManager.LastWarning;
                    ReloadAllLocalStateLocked();
                }
                AppendLog("success", string.Format("工程 [{0}] 物理路径已重定向至 {1}（同步 {2} 条会话）。", projName, newFolderPath, updated));
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = true;
                resp["updated"] = updated;
                resp["warning"] = warn ?? string.Empty;
                WriteJsonResponse(stream, 200, resp);
                return;
            }

            if (path == "/api/rename/candidates" && req.Method == "GET")
            {
                WriteJsonResponse(stream, 200, BuildRenameCandidatesPayload());
                return;
            }
            if (path == "/api/rename/execute" && req.Method == "POST")
            {
                string strategyStr = MiniJson.GetString(body, "strategy", "auto");
                Dictionary<string, string> cidMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                object itemsObj;
                if (body != null && body.TryGetValue("items", out itemsObj))
                {
                    IList<object> list = itemsObj as IList<object>;
                    if (list != null)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            IDictionary<string, object> row = list[i] as IDictionary<string, object>;
                            if (row == null) continue;
                            string cid = MiniJson.GetString(row, "cid", string.Empty).Trim();
                            string newTitle = MiniJson.GetString(row, "newTitle", string.Empty).Trim();
                            if (!string.IsNullOrEmpty(cid) && !string.IsNullOrEmpty(newTitle))
                                cidMap[cid] = newTitle;
                        }
                    }
                }
                if (cidMap.Count == 0)
                {
                    WriteJsonError(stream, 400, "未提交有效的重命名条目。");
                    return;
                }

                RenameBatchResult res;
                int errCode;
                string errMsg;
                if (!ExecuteRenameInternal(cidMap, strategyStr, out res, out errCode, out errMsg))
                {
                    WriteStrategyOrError(stream, errCode, errMsg);
                    return;
                }
                WriteJsonResponse(stream, 200, SerializeBatchRenameResult(res));
                return;
            }
            if (path == "/api/rename/ai-prompt" && req.Method == "POST")
            {
                List<string> cids = ReadStringArray(body, "cids");
                List<SessionRenameCandidate> selectedCands = BuildSelectedCandidatesForAi(cids);
                if (selectedCands.Count == 0)
                {
                    WriteJsonError(stream, 400, "请先勾选至少 1 条会话再生成 AI 润色提示词。");
                    return;
                }
                string prompt = AiPolishPromptExchange.BuildPolishPrompt(selectedCands);
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["count"] = selectedCands.Count;
                resp["prompt"] = prompt;
                WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/rename/ai-save-prompt" && req.Method == "POST")
            {
                List<string> cids = ReadStringArray(body, "cids");
                string outPath = MiniJson.GetString(body, "outputPath", string.Empty).Trim();
                List<SessionRenameCandidate> selectedCands = BuildSelectedCandidatesForAi(cids);
                if (selectedCands.Count == 0)
                {
                    WriteJsonError(stream, 400, "请先勾选至少 1 条会话再导出 AI 提示词。");
                    return;
                }
                if (string.IsNullOrEmpty(outPath))
                {
                    bool dlgBusy;
                    outPath = NativeDialogBridge.PickSaveFile("保存 AI 润色提示词 (.txt)", "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*", "ai_polish_prompt.txt", out dlgBusy);
                    if (dlgBusy) { WriteJsonError(stream, 409, "已有文件选择窗口正在打开。"); return; }
                    if (string.IsNullOrEmpty(outPath))
                    {
                        Dictionary<string, object> cancelled = new Dictionary<string, object>();
                        cancelled["cancelled"] = true;
                        WriteJsonResponse(stream, 200, cancelled);
                        return;
                    }
                }
                string prompt = AiPolishPromptExchange.BuildPolishPrompt(selectedCands);
                LocalWorkspaceManager.WriteUtf8TextAtomicWithBackup(outPath, prompt ?? string.Empty, false, null);
                AppendLog("success", "已导出 AI 润色提示词：" + outPath);
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = true;
                resp["count"] = selectedCands.Count;
                resp["outputPath"] = outPath;
                WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/rename/ai-parse" && req.Method == "POST")
            {
                string rawText = MiniJson.GetString(body, "rawText", string.Empty);
                List<string> cids = ReadStringArray(body, "cids");
                List<SessionRenameCandidate> allCands = BuildScopedAiCandidates(cids);
                Dictionary<string, string> parsedMap;
                string err;
                if (!AiPolishPromptExchange.ParseAiClipboardJson(rawText, allCands, out parsedMap, out err))
                {
                    WriteJsonError(stream, 400, err ?? "解析 AI JSON 失败。");
                    return;
                }
                WriteJsonResponse(stream, 200, BuildAiParseResponsePayload(parsedMap, allCands, null));
                return;
            }
            if (path == "/api/rename/ai-load-json" && req.Method == "POST")
            {
                string inPath = MiniJson.GetString(body, "inputPath", string.Empty).Trim();
                List<string> cids = ReadStringArray(body, "cids");
                if (string.IsNullOrEmpty(inPath))
                {
                    bool dlgBusy;
                    inPath = NativeDialogBridge.PickOpenFile("导入 AI 润色结果 JSON", "JSON / 文本文件 (*.json;*.txt)|*.json;*.txt|所有文件 (*.*)|*.*", out dlgBusy);
                    if (dlgBusy) { WriteJsonError(stream, 409, "已有文件选择窗口正在打开。"); return; }
                    if (string.IsNullOrEmpty(inPath))
                    {
                        Dictionary<string, object> cancelled = new Dictionary<string, object>();
                        cancelled["cancelled"] = true;
                        WriteJsonResponse(stream, 200, cancelled);
                        return;
                    }
                }
                if (!File.Exists(inPath))
                {
                    WriteJsonError(stream, 404, "未找到指定的 AI 结果文件: " + inPath);
                    return;
                }
                string rawText = LocalWorkspaceManager.ReadUtf8TextShared(inPath);
                List<SessionRenameCandidate> allCands = BuildScopedAiCandidates(cids);
                Dictionary<string, string> parsedMap;
                string err;
                if (!AiPolishPromptExchange.ParseAiClipboardJson(rawText, allCands, out parsedMap, out err))
                {
                    WriteJsonError(stream, 400, err ?? "解析 AI JSON 文件失败。");
                    return;
                }
                AppendLog("info", string.Format("已从文件导入并解析 {0} 条 AI 润色标题：{1}", parsedMap.Count, Path.GetFileName(inPath)));
                WriteJsonResponse(stream, 200, BuildAiParseResponsePayload(parsedMap, allCands, inPath));
                return;
            }
            if (path == "/api/rename/rules" && req.Method == "GET")
            {
                string err;
                RenameRulesConfig cfg = AutoTitleSynthesizer.LoadRules(_repoPath, out err);
                string rulesFile = Path.Combine(_repoPath, "rename_rules.json");
                string rawJson = File.Exists(rulesFile) ? LocalWorkspaceManager.ReadUtf8TextShared(rulesFile) : AutoTitleSynthesizer.SerializeRulesToJson(cfg);
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["rulesPath"] = rulesFile;
                resp["rawJson"] = rawJson;
                resp["rulesObj"] = SerializeRulesConfigObj(cfg);
                resp["warning"] = err ?? string.Empty;
                WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/rename/rules" && req.Method == "POST")
            {
                string rawJson = MiniJson.GetString(body, "rawJson", null);
                RenameRulesConfig parsed;
                string err;
                string finalJson;

                if (!string.IsNullOrWhiteSpace(rawJson))
                {
                    if (!AutoTitleSynthesizer.TryParseRulesFromJson(rawJson, out parsed, out err) || parsed == null)
                    {
                        WriteJsonError(stream, 400, err ?? "规则 JSON 校验失败。");
                        return;
                    }
                    finalJson = AutoTitleSynthesizer.SerializeRulesToJson(parsed);
                }
                else
                {
                    object objVal;
                    if (body == null || !body.TryGetValue("rulesObj", out objVal) || !(objVal is IDictionary<string, object>))
                    {
                        WriteJsonError(stream, 400, "缺少 rulesObj 或 rawJson 参数。");
                        return;
                    }
                    finalJson = MiniJson.Serialize(objVal, true);
                    if (!AutoTitleSynthesizer.TryParseRulesFromJson(finalJson, out parsed, out err) || parsed == null)
                    {
                        WriteJsonError(stream, 400, err ?? "表单规则校验失败。");
                        return;
                    }
                    finalJson = AutoTitleSynthesizer.SerializeRulesToJson(parsed);
                }

                string rulesFile = Path.Combine(_repoPath, "rename_rules.json");
                LocalWorkspaceManager.WriteUtf8TextAtomicWithBackup(rulesFile, finalJson, false, rulesFile + ".bak");
                AppendLog("success", "已保存并应用会话自动命名规则。");
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = true;
                resp["rawJson"] = finalJson;
                resp["rulesObj"] = SerializeRulesConfigObj(parsed);
                WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/rename/rules/test" && req.Method == "POST")
            {
                string rawJson = MiniJson.GetString(body, "rawJson", null);
                RenameRulesConfig cfg;
                string err;
                if (!string.IsNullOrWhiteSpace(rawJson))
                {
                    if (!AutoTitleSynthesizer.TryParseRulesFromJson(rawJson, out cfg, out err) || cfg == null)
                    {
                        WriteJsonError(stream, 400, err ?? "规则 JSON 无效。");
                        return;
                    }
                }
                else
                {
                    object objVal;
                    if (body != null && body.TryGetValue("rulesObj", out objVal) && objVal is IDictionary<string, object>)
                    {
                        string tmp = MiniJson.Serialize(objVal, false);
                        if (!AutoTitleSynthesizer.TryParseRulesFromJson(tmp, out cfg, out err) || cfg == null)
                        {
                            WriteJsonError(stream, 400, err ?? "表单规则无效。");
                            return;
                        }
                    }
                    else
                    {
                        cfg = AutoTitleSynthesizer.LoadRules(_repoPath, out err);
                    }
                }

                int sampleLimit = 20;
                List<ConversationSummaryInfo> sumsSnap;
                lock (_stateLock) sumsSnap = new List<ConversationSummaryInfo>(_localSummaries);
                List<RuleSandboxItem> samples = AutoTitleSynthesizer.PreviewRulesOnSessions(cfg, sumsSnap, sampleLimit);
                List<object> outList = new List<object>();
                for (int i = 0; i < samples.Count; i++)
                {
                    RuleSandboxItem s = samples[i];
                    Dictionary<string, object> item = new Dictionary<string, object>();
                    item["cid"] = s.ConversationId;
                    item["projectName"] = s.ProjectName;
                    item["oldTitle"] = s.OldTitle;
                    item["suggestedPrefix"] = s.SuggestedPrefix;
                    item["suggestedTitle"] = s.SuggestedTitle;
                    item["isLowValue"] = s.IsLowValue;
                    outList.Add(item);
                }
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["samples"] = outList;
                WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/rename/history" && req.Method == "GET")
            {
                List<RenameSnapshotInfo> snaps = RenameBackupManager.ListSnapshots(_geminiPath);
                List<object> arr = new List<object>();
                for (int i = 0; i < snaps.Count; i++)
                {
                    RenameSnapshotInfo s = snaps[i];
                    Dictionary<string, object> d = new Dictionary<string, object>();
                    d["snapshotDir"] = s.SnapshotDir;
                    d["folderName"] = !string.IsNullOrEmpty(s.SnapshotName) ? s.SnapshotName : Path.GetFileName(s.SnapshotDir);
                    d["createdAtIso"] = s.CreatedAt ?? string.Empty;
                    d["itemCount"] = s.ItemCount;
                    d["hasPbBackup"] = s.HasPbBackup;

                    List<RenameSnapshotItem> items = s.Items ?? new List<RenameSnapshotItem>();
                    string previewSummary = string.Empty;
                    if (items.Count > 0 && items[0] != null)
                        previewSummary = (items[0].OldTitle ?? string.Empty) + " -> " + (items[0].NewTitle ?? string.Empty);
                    d["previewSummary"] = previewSummary;

                    List<object> itemArr = new List<object>();
                    for (int k = 0; k < items.Count; k++)
                    {
                        Dictionary<string, object> it = new Dictionary<string, object>();
                        it["cid"] = items[k].ConversationId;
                        it["projectName"] = items[k].ProjectName ?? string.Empty;
                        it["oldTitle"] = items[k].OldTitle ?? string.Empty;
                        it["newTitle"] = items[k].NewTitle ?? string.Empty;
                        itemArr.Add(it);
                    }
                    d["items"] = itemArr;
                    arr.Add(d);
                }
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["snapshots"] = arr;
                WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/rename/rollback" && req.Method == "POST")
            {
                string snapshotDir = MiniJson.GetString(body, "snapshotDir", string.Empty).Trim();
                string strategyStr = MiniJson.GetString(body, "strategy", "auto");
                if (string.IsNullOrEmpty(snapshotDir) || !Directory.Exists(snapshotDir))
                {
                    WriteJsonError(stream, 400, "未找到指定的重命名快照目录。");
                    return;
                }
                string backupRoot = RenameBackupManager.GetBackupRoot(_geminiPath);
                if (!LocalWorkspaceManager.IsPathContainedUnder(backupRoot, snapshotDir))
                {
                    WriteJsonError(stream, 403, "拒绝访问越界的快照路径。");
                    return;
                }

                if (!TryAcquireJob("rename_rollback", "正在执行三层快照回滚..."))
                {
                    WriteJsonError(stream, 409, "当前有其他后台写入任务正在执行。");
                    return;
                }
                try
                {
                    bool hotOnly;
                    int errCode;
                    string errMsg;
                    if (!ResolveStrategyExecution(strategyStr, out hotOnly, out errCode, out errMsg))
                    {
                        WriteStrategyOrError(stream, errCode, errMsg);
                        return;
                    }

                    RenameRollbackResult rbRes;
                    bool ok;
                    lock (_mutationLock)
                    {
                        ok = RenameBackupManager.RollbackSnapshot(_geminiPath, snapshotDir, hotOnly, out rbRes);
                        if (ok) ReloadAllLocalStateLocked();
                    }
                    if (!ok)
                    {
                        WriteJsonError(stream, 500, rbRes != null ? rbRes.ErrorMessage : "回滚失败。");
                        return;
                    }
                    AppendLog("success", string.Format("回滚完成：恢复 .pbtxt {0} 条、删除新建 .pbtxt {1} 条，Layer 2 DB {2} 条，Layer 3 PB {3} 条。",
                        rbRes.RestoredPbtxtCount, rbRes.DeletedPbtxtCount, rbRes.RestoredDbCount, rbRes.RestoredPbCount));
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["restoredPbtxt"] = rbRes.RestoredPbtxtCount;
                    resp["deletedPbtxt"] = rbRes.DeletedPbtxtCount;
                    resp["skippedOrphanPbtxt"] = rbRes.SkippedOrphanPbtxtCount;
                    resp["restoredDb"] = rbRes.RestoredDbCount;
                    resp["restoredPb"] = rbRes.RestoredPbCount;
                    resp["pbSkippedDueToLock"] = rbRes.PbSkippedDueToLock;
                    resp["layer2Restored"] = rbRes.RestoredDbCount > 0;
                    resp["layer3Restored"] = rbRes.RestoredPbCount > 0;
                    resp["warnings"] = new List<object>(ToObjList(rbRes.Warnings));
                    WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    ReleaseJob();
                }
                return;
            }

            if (path == "/api/capabilities" && req.Method == "GET")
            {
                WriteJsonResponse(stream, 200, BuildCapabilitiesPayload(false));
                return;
            }
            if (path == "/api/capabilities/diff" && req.Method == "GET")
            {
                string key, src;
                req.Query.TryGetValue("key", out key);
                req.Query.TryGetValue("source", out src);
                bool fromBackup = string.Equals(src, "backup", StringComparison.OrdinalIgnoreCase);

                CapabilityDiffNode node = FindCapDiffNode(key, fromBackup);
                if (node == null)
                {
                    WriteJsonError(stream, 404, "未找到指定的插件/技能/规则/MCP/Hook 项。");
                    return;
                }
                WriteJsonResponse(stream, 200, BuildCapabilityLineDiffPayload(node));
                return;
            }
            if (path == "/api/capabilities/push" && req.Method == "POST")
            {
                List<string> keys = ReadStringArray(body, "keys");
                if (keys.Count == 0) { WriteJsonError(stream, 400, "请选择要存入内置仓库的能力项。"); return; }
                int written = 0;
                lock (_mutationLock)
                {
                    List<CapabilityItem> selected = SelectCapsByKeys(_localCaps, keys);
                    if (selected.Count == 0) { WriteJsonError(stream, 404, "本机未找到所选能力项。"); return; }
                    written = CapabilityRepositoryManager.PushCapabilitiesToRepository(_repoPath, _homePath, selected);
                    UpdateBaselineForCapabilities(selected);
                    ReloadAllLocalStateLocked();
                }
                AppendLog("success", string.Format("已将 {0} 项本地能力写入内置仓库 Repository/。", keys.Count));
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = true;
                resp["written"] = written;
                WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/capabilities/import" && req.Method == "POST")
            {
                List<string> keys = ReadStringArray(body, "keys");
                string source = MiniJson.GetString(body, "source", "repo");
                bool fromBackup = string.Equals(source, "backup", StringComparison.OrdinalIgnoreCase);
                if (keys.Count == 0) { WriteJsonError(stream, 400, "请选择要导入本地的能力项。"); return; }
                int written = 0;
                lock (_mutationLock)
                {
                    List<CapabilityItem> pool = fromBackup && _backupIndex != null ? _backupIndex.Capabilities : _repoCaps;
                    List<CapabilityItem> selected = SelectCapsByKeys(pool, keys);
                    if (selected.Count == 0) { WriteJsonError(stream, 404, "目标来源未找到所选能力项。"); return; }
                    written = CapabilityRepositoryManager.ImportCapabilitiesToLocal(_geminiPath, _homePath, selected);
                    if (!fromBackup) UpdateBaselineForCapabilities(selected);
                    ReloadAllLocalStateLocked();
                }
                AppendLog("success", string.Format("已按 Key 级无损导入 {0} 项能力至本机 .gemini 目录。", keys.Count));
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = true;
                resp["written"] = written;
                WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/capabilities/delete-local" && req.Method == "POST")
            {
                List<string> keys = ReadStringArray(body, "keys");
                if (keys.Count == 0) { WriteJsonError(stream, 400, "请选择要删除的本机能力项。"); return; }
                int deleted = 0;
                lock (_mutationLock)
                {
                    List<CapabilityItem> selected = SelectCapsByKeys(_localCaps, keys);
                    deleted = CapabilityRepositoryManager.DeleteCapabilitiesFromLocal(_geminiPath, selected);
                    ReloadAllLocalStateLocked();
                }
                AppendLog("warn", string.Format("已从本机删除 {0} 项能力配置（已生成 .bak）。", deleted));
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = true;
                resp["deleted"] = deleted;
                WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/capabilities/delete-repo" && req.Method == "POST")
            {
                List<string> keys = ReadStringArray(body, "keys");
                if (keys.Count == 0) { WriteJsonError(stream, 400, "请选择要从仓库移除的能力项。"); return; }
                int deleted = 0;
                lock (_mutationLock)
                {
                    List<CapabilityItem> selected = SelectCapsByKeys(_repoCaps, keys);
                    deleted = CapabilityRepositoryManager.DeleteCapabilitiesFromRepository(_repoPath, selected);
                    ReloadAllLocalStateLocked();
                }
                AppendLog("warn", string.Format("已从内置仓库移除 {0} 项能力快照。", deleted));
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = true;
                resp["deleted"] = deleted;
                WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/capabilities/relocate" && req.Method == "POST")
            {
                string key = MiniJson.GetString(body, "key", string.Empty).Trim();
                string scope = MiniJson.GetString(body, "scope", "local");
                string oldPath = MiniJson.GetString(body, "oldPath", string.Empty).Trim();
                string newPath = MiniJson.GetString(body, "newPath", string.Empty).Trim();
                bool isLocalTarget = !string.Equals(scope, "repo", StringComparison.OrdinalIgnoreCase);

                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath))
                {
                    WriteJsonError(stream, 400, "能力标识、原路径及新目标路径均不能为空。");
                    return;
                }
                bool ok;
                lock (_mutationLock)
                {
                    List<CapabilityItem> pool = isLocalTarget ? _localCaps : _repoCaps;
                    CapabilityItem cap = pool.Find(delegate(CapabilityItem c) { return string.Equals(c.UniqueKey, key, StringComparison.OrdinalIgnoreCase); });
                    if (cap == null) { WriteJsonError(stream, 404, "未找到目标 MCP/Hook 项。"); return; }
                    ok = CapabilityRepositoryManager.RelocateCapabilityPath(_geminiPath, _repoPath, isLocalTarget, _homePath, cap, oldPath, newPath);
                    if (ok) ReloadAllLocalStateLocked();
                }
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = ok;
                if (!ok) resp["message"] = "未在目标配置中匹配到原路径，或新旧路径一致。";
                else AppendLog("success", string.Format("已重定向能力路径: {0} -> {1}", oldPath, newPath));
                WriteJsonResponse(stream, 200, resp);
                return;
            }

            if (path == "/api/backup/current" && req.Method == "GET")
            {
                WriteJsonResponse(stream, 200, BuildBackupPayload());
                return;
            }
            if (path == "/api/backup/open" && req.Method == "POST")
            {
                string agyPath = MiniJson.GetString(body, "archivePath", string.Empty).Trim();
                if (string.IsNullOrEmpty(agyPath))
                {
                    bool dlgBusy;
                    agyPath = NativeDialogBridge.PickOpenFile("打开 Antigravity 备份归档包 (.agy)", "Antigravity 备份包 (*.agy)|*.agy|ZIP 压缩包 (*.zip)|*.zip|所有文件 (*.*)|*.*", out dlgBusy);
                    if (dlgBusy) { WriteJsonError(stream, 409, "已有文件选择窗口正在打开。"); return; }
                    if (string.IsNullOrEmpty(agyPath))
                    {
                        Dictionary<string, object> cancelled = new Dictionary<string, object>();
                        cancelled["cancelled"] = true;
                        WriteJsonResponse(stream, 200, cancelled);
                        return;
                    }
                }
                if (!File.Exists(agyPath)) { WriteJsonError(stream, 404, "归档文件不存在: " + agyPath); return; }

                lock (_mutationLock)
                {
                    ReloadLoadedBackupLocked(agyPath);
                }
                AppendLog("info", "已打开归档包：" + Path.GetFileName(agyPath));
                WriteJsonResponse(stream, 200, BuildBackupPayload());
                return;
            }
            if (path == "/api/backup/full" && req.Method == "POST")
            {
                string outPath = MiniJson.GetString(body, "outputPath", string.Empty).Trim();
                bool closeIde = MiniJson.GetBool(body, "closeIdeIfRunning", false);
                if (string.IsNullOrEmpty(outPath))
                {
                    string defName = string.Format("Antigravity_FullBackup_{0}.agy", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                    bool dlgBusy;
                    outPath = NativeDialogBridge.PickSaveFile("保存全量备份 (.agy)", "Antigravity 备份包 (*.agy)|*.agy", defName, out dlgBusy);
                    if (dlgBusy) { WriteJsonError(stream, 409, "已有文件选择窗口正在打开。"); return; }
                    if (string.IsNullOrEmpty(outPath))
                    {
                        Dictionary<string, object> cancelled = new Dictionary<string, object>();
                        cancelled["cancelled"] = true;
                        WriteJsonResponse(stream, 200, cancelled);
                        return;
                    }
                }
                if (!EnsureIdeClosedIfNeeded(closeIde, stream)) return;
                if (!TryAcquireJob("full_backup", "正在创建全量备份归档 (.agy)..."))
                {
                    WriteJsonError(stream, 409, "当前有其他后台任务正在执行。");
                    return;
                }

                string destSnap = outPath;
                List<ConversationSummaryInfo> sumSnap;
                lock (_stateLock) sumSnap = new List<ConversationSummaryInfo>(_localSummaries);
                StartBackgroundJob("full_backup", delegate()
                {
                    int fileCount;
                    List<string> warnings;
                    lock (_mutationLock)
                    {
                        ArchiveTransferService.CreateFullBackup(_geminiPath, _homePath, sumSnap, destSnap,
                            delegate(string msg, int pct) { UpdateJobProgress("full_backup", msg, pct); },
                            out fileCount, out warnings);
                    }
                    Dictionary<string, object> res = new Dictionary<string, object>();
                    res["ok"] = true;
                    res["fileCount"] = fileCount;
                    res["outputPath"] = destSnap;
                    res["warnings"] = new List<object>(ToObjList(warnings));
                    AppendLog("success", string.Format("全量备份完成（打包 {0} 文件）：{1}", fileCount, destSnap));
                    return res;
                });

                Dictionary<string, object> ack = new Dictionary<string, object>();
                ack["accepted"] = true;
                ack["jobType"] = "full_backup";
                ack["outputPath"] = destSnap;
                WriteJsonResponse(stream, 202, ack);
                return;
            }
            if (path == "/api/backup/selective-export" && req.Method == "POST")
            {
                List<string> cids = ReadStringArray(body, "cids");
                List<string> capKeys = ReadStringArray(body, "capKeys");
                bool includeGlobal = MiniJson.GetBool(body, "includeGlobalCaps", false);
                bool closeIde = MiniJson.GetBool(body, "closeIdeIfRunning", false);
                string outPath = MiniJson.GetString(body, "outputPath", string.Empty).Trim();

                if (cids.Count == 0 && capKeys.Count == 0 && !includeGlobal)
                {
                    WriteJsonError(stream, 400, "请先选择要导出的会话或能力项。");
                    return;
                }
                if (string.IsNullOrEmpty(outPath))
                {
                    string defName = string.Format("Antigravity_Selective_{0}.agy", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                    bool dlgBusy;
                    outPath = NativeDialogBridge.PickSaveFile("导出所选至轻量归档 (.agy)", "Antigravity 备份包 (*.agy)|*.agy", defName, out dlgBusy);
                    if (dlgBusy) { WriteJsonError(stream, 409, "已有文件选择窗口正在打开。"); return; }
                    if (string.IsNullOrEmpty(outPath))
                    {
                        Dictionary<string, object> cancelled = new Dictionary<string, object>();
                        cancelled["cancelled"] = true;
                        WriteJsonResponse(stream, 200, cancelled);
                        return;
                    }
                }
                if (cids.Count > 0 && !EnsureIdeClosedIfNeeded(closeIde, stream)) return;
                if (!TryAcquireJob("selective_export", "正在导出轻量归档 (.agy)..."))
                {
                    WriteJsonError(stream, 409, "当前有其他后台任务正在执行。");
                    return;
                }

                List<ConversationSummaryInfo> selConvs = SelectSessionsByIds(_localSummaries, cids);
                List<CapabilityItem> selCaps = SelectCapsByKeys(_localCaps, capKeys);
                Dictionary<string, ProjectMetadata> projSnap;
                lock (_stateLock) projSnap = new Dictionary<string, ProjectMetadata>(_localProjects, StringComparer.OrdinalIgnoreCase);

                string destSnap = outPath;
                StartBackgroundJob("selective_export", delegate()
                {
                    UpdateJobProgress("selective_export", "正在增量切片与压缩选择性 .agy 归档...", 40);
                    int written;
                    lock (_mutationLock)
                    {
                        written = LocalWorkspaceManager.ExportSelectiveBackup(
                            _geminiPath, _homePath, destSnap, selConvs, projSnap, includeGlobal, selCaps);
                    }
                    Dictionary<string, object> res = new Dictionary<string, object>();
                    res["ok"] = true;
                    res["entriesWritten"] = written;
                    res["outputPath"] = destSnap;
                    AppendLog("success", string.Format("选择性 .agy 导出完成（写入 {0} 条目）：{1}", written, destSnap));
                    return res;
                });

                Dictionary<string, object> ack = new Dictionary<string, object>();
                ack["accepted"] = true;
                ack["jobType"] = "selective_export";
                ack["outputPath"] = destSnap;
                WriteJsonResponse(stream, 202, ack);
                return;
            }
            if (path == "/api/backup/import" && req.Method == "POST")
            {
                List<string> cids = ReadStringArray(body, "cids");
                List<string> capKeys = ReadStringArray(body, "capKeys");
                bool closeIde = MiniJson.GetBool(body, "closeIdeIfRunning", false);

                if (cids.Count == 0 && capKeys.Count == 0)
                {
                    WriteJsonError(stream, 400, "请先选择要从归档包导入的能力或会话。");
                    return;
                }
                if (_backupIndex == null || string.IsNullOrEmpty(_loadedBackupPath))
                {
                    WriteJsonError(stream, 400, "当前未打开任何 .agy 备份归档包。");
                    return;
                }
                if (!EnsureIdeClosedIfNeeded(closeIde, stream)) return;
                if (!TryAcquireJob("selective_import", "正在执行三层选择性合流导入..."))
                {
                    WriteJsonError(stream, 409, "当前有其他后台任务正在执行。");
                    return;
                }

                SelectiveImportRequest importReq = new SelectiveImportRequest();
                lock (_stateLock)
                {
                    importReq.GeminiPath = _geminiPath;
                    importReq.HomePath = _homePath;
                    importReq.SourceMode = RightPaneSourceMode.BackupArchive;
                    importReq.LoadedBackupPath = _loadedBackupPath;
                    importReq.BackupIndex = _backupIndex;
                    importReq.LocalProjects = _localProjects;
                    importReq.CurrentDiffNodes = _backupSessionDiffs;
                    importReq.SelectedCapabilities = SelectCapsByKeys(_backupIndex.Capabilities, capKeys);
                    importReq.SelectedConversations = SelectSessionsByIds(_backupIndex.Conversations, cids);
                    importReq.ConvProjDiffMap = new Dictionary<string, ProjectDiffNode>(StringComparer.OrdinalIgnoreCase);
                }

                StartBackgroundJob("selective_import", delegate()
                {
                    SelectiveImportResult impRes;
                    lock (_mutationLock)
                    {
                        impRes = ArchiveTransferService.PerformSelectiveImport(importReq,
                            delegate(string m, int pct) { UpdateJobProgress("selective_import", m, pct); });
                        ReloadAllLocalStateLocked();
                        ReloadLoadedBackupLocked(_loadedBackupPath);
                    }
                    Dictionary<string, object> res = new Dictionary<string, object>();
                    res["ok"] = string.IsNullOrEmpty(impRes.ErrorMessage);
                    res["filesImported"] = impRes.FilesImported;
                    res["capabilitiesImported"] = impRes.CapabilitiesImported;
                    res["conversationsImported"] = impRes.ConversationsImported;
                    res["warnings"] = new List<object>(ToObjList(impRes.Warnings));
                    res["errorMessage"] = impRes.ErrorMessage ?? string.Empty;
                    AppendLog("success", string.Format("备份包导入完成：能力 {0} 项、会话 {1} 条（写入 {2} 项）。",
                        impRes.CapabilitiesImported, impRes.ConversationsImported, impRes.FilesImported));
                    return res;
                });

                Dictionary<string, object> ack = new Dictionary<string, object>();
                ack["accepted"] = true;
                ack["jobType"] = "selective_import";
                WriteJsonResponse(stream, 202, ack);
                return;
            }

            if (path == "/api/native/pick-open-file" && req.Method == "POST")
            {
                string title = MiniJson.GetString(body, "title", "选择文件");
                string filter = MiniJson.GetString(body, "filter", "所有文件 (*.*)|*.*");
                bool busy;
                string selected = NativeDialogBridge.PickOpenFile(title, filter, out busy);
                if (busy) { WriteJsonError(stream, 409, "已有文件选择弹窗处于打开状态。"); return; }
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["cancelled"] = string.IsNullOrEmpty(selected);
                resp["path"] = selected ?? string.Empty;
                WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/native/pick-save-file" && req.Method == "POST")
            {
                string title = MiniJson.GetString(body, "title", "保存文件");
                string filter = MiniJson.GetString(body, "filter", "所有文件 (*.*)|*.*");
                string defName = MiniJson.GetString(body, "defaultFileName", string.Empty);
                bool busy;
                string selected = NativeDialogBridge.PickSaveFile(title, filter, defName, out busy);
                if (busy) { WriteJsonError(stream, 409, "已有文件选择弹窗处于打开状态。"); return; }
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["cancelled"] = string.IsNullOrEmpty(selected);
                resp["path"] = selected ?? string.Empty;
                WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/native/pick-folder" && req.Method == "POST")
            {
                string title = MiniJson.GetString(body, "title", "选择文件夹");
                string initialPath = MiniJson.GetString(body, "initialPath", _homePath);
                bool busy;
                string selected = NativeDialogBridge.PickFolder(title, initialPath, out busy);
                if (busy) { WriteJsonError(stream, 409, "已有目录选择弹窗处于打开状态。"); return; }
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["cancelled"] = string.IsNullOrEmpty(selected);
                resp["path"] = selected ?? string.Empty;
                WriteJsonResponse(stream, 200, resp);
                return;
            }
            if (path == "/api/native/open-in-explorer" && req.Method == "POST")
            {
                string target = MiniJson.GetString(body, "path", string.Empty).Trim();
                if (string.IsNullOrEmpty(target) || (!File.Exists(target) && !Directory.Exists(target)))
                {
                    WriteJsonError(stream, 404, "路径不存在: " + target);
                    return;
                }
                string openArgs = File.Exists(target) ? ("/select,\"" + target + "\"") : ("\"" + target + "\"");
                using (Process.Start("explorer.exe", openArgs)) { }
                WriteOkJson(stream, null);
                return;
            }

            WriteJsonError(stream, 404, "未知接口路由: " + path);
        }

        // ── 内部状态管理与重命名/任务编排辅助 ──

        private void ReloadAllLocalStateLocked()
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

        private void ReloadLoadedBackupLocked(string archivePath)
        {
            BackupArchiveIndex idx = StreamingZipIndexReader.ReadIndex(archivePath);
            List<ProjectDiffNode> sessDiffs = VisualDiffEngine.ComputeDiff(_localSummaries, idx.Conversations, idx.SourceHome, _homePath);
            List<CapabilityDiffNode> capDiffs = VisualDiffEngine.ComputeCapabilityDiff(_localCaps, idx.Capabilities, _capBaseline);
            lock (_stateLock)
            {
                _loadedBackupPath = archivePath;
                _backupIndex = idx;
                _backupSessionDiffs = sessDiffs ?? new List<ProjectDiffNode>();
                _backupCapDiffs = capDiffs ?? new List<CapabilityDiffNode>();
            }
        }

        private bool TryAcquireJob(string jobType, string message)
        {
            if (Interlocked.CompareExchange(ref _jobBusyFlag, 1, 0) != 0) return false;
            lock (_stateLock)
            {
                _currentJobType = jobType;
                _currentJobMessage = message;
                _currentJobPercent = 5;
            }
            BroadcastSse("progress", BuildJobStatusDict(true, jobType, message, 5));
            return true;
        }

        private void UpdateJobProgress(string jobType, string message, int pct)
        {
            lock (_stateLock)
            {
                _currentJobMessage = message;
                _currentJobPercent = pct;
            }
            BroadcastSse("progress", BuildJobStatusDict(true, jobType, message, pct));
        }

        private void ReleaseJob()
        {
            lock (_stateLock)
            {
                _currentJobType = null;
                _currentJobMessage = null;
                _currentJobPercent = 0;
            }
            Interlocked.Exchange(ref _jobBusyFlag, 0);
        }

        private void StartBackgroundJob(string jobType, Func<Dictionary<string, object>> work)
        {
            Thread t = new Thread(delegate()
            {
                Dictionary<string, object> donePayload = new Dictionary<string, object>();
                donePayload["jobType"] = jobType;
                try
                {
                    Dictionary<string, object> res = work();
                    donePayload["ok"] = true;
                    donePayload["result"] = res;
                }
                catch (Exception ex)
                {
                    donePayload["ok"] = false;
                    donePayload["error"] = ex.Message;
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

        private bool EnsureIdeClosedIfNeeded(bool closeIde, NetworkStream stream)
        {
            int p, ls;
            ProcessGuardService.GetRunningAntigravityProcessCounts(out p, out ls);
            if (p == 0 && ls == 0) return true;
            if (!closeIde)
            {
                Dictionary<string, object> errObj = new Dictionary<string, object>();
                errObj["error"] = string.Format("检测到 {0} 个 Antigravity 主进程与 {1} 个语言服务进程正在运行，请确认关闭 IDE 后重试。", p, ls);
                errObj["code"] = "IDE_RUNNING";
                errObj["parentCount"] = p;
                errObj["lsCount"] = ls;
                WriteJsonResponse(stream, 409, errObj);
                return false;
            }
            string closeErr;
            if (!ProcessGuardService.CloseAntigravityProcesses(true, delegate(string m) { AppendLog("info", m); }, out closeErr))
            {
                WriteJsonError(stream, 409, closeErr ?? "无法关闭 Antigravity 进程。");
                return false;
            }
            return true;
        }

        private bool ResolveStrategyExecution(string strategyStr, out bool hotWriteOnly, out int errCode, out string errMsg)
        {
            hotWriteOnly = false;
            errCode = 0;
            errMsg = null;

            int p, ls;
            ProcessGuardService.GetRunningAntigravityProcessCounts(out p, out ls);
            if (p == 0 && ls == 0) return true;

            if (string.Equals(strategyStr, "HotWritePersistentLayers", StringComparison.OrdinalIgnoreCase))
            {
                hotWriteOnly = true;
                return true;
            }
            if (string.Equals(strategyStr, "CloseAndFullWrite", StringComparison.OrdinalIgnoreCase))
            {
                string closeErr;
                if (!ProcessGuardService.CloseAntigravityProcesses(true, delegate(string m) { AppendLog("info", m); }, out closeErr))
                {
                    errCode = 409;
                    errMsg = closeErr ?? "无法关闭 Antigravity 进程。";
                    return false;
                }
                hotWriteOnly = false;
                return true;
            }

            errCode = 409;
            errMsg = string.Format("IDE_RUNNING:{0}:{1}", p, ls);
            return false;
        }

        private List<SessionRenameCandidate> BuildScopedAiCandidates(List<string> cids)
        {
            List<SessionRenameCandidate> allCands = BuildAllCandidateObjects();
            if (cids != null && cids.Count > 0)
            {
                HashSet<string> set = new HashSet<string>(cids, StringComparer.OrdinalIgnoreCase);
                List<SessionRenameCandidate> scoped = new List<SessionRenameCandidate>();
                for (int i = 0; i < allCands.Count; i++)
                {
                    if (set.Contains(allCands[i].ConversationId))
                        scoped.Add(allCands[i]);
                }
                return scoped;
            }
            return allCands;
        }

        private static Dictionary<string, object> BuildAiParseResponsePayload(
            Dictionary<string, string> parsedMap,
            List<SessionRenameCandidate> allCands,
            string inputPath)
        {
            List<object> changes = new List<object>();
            foreach (KeyValuePair<string, string> kvp in parsedMap)
            {
                SessionRenameCandidate cand = allCands.Find(delegate(SessionRenameCandidate x)
                {
                    return string.Equals(x.ConversationId, kvp.Key, StringComparison.OrdinalIgnoreCase);
                });
                Dictionary<string, object> ch = new Dictionary<string, object>();
                ch["cid"] = kvp.Key;
                ch["oldTitle"] = cand != null ? cand.OldTitle : string.Empty;
                ch["newTitle"] = kvp.Value;
                changes.Add(ch);
            }

            Dictionary<string, object> resp = new Dictionary<string, object>();
            resp["ok"] = true;
            resp["matchedCount"] = parsedMap.Count;
            resp["skippedCount"] = 0;
            resp["unmatchedIds"] = new List<object>();
            resp["changes"] = changes;
            if (!string.IsNullOrEmpty(inputPath)) resp["inputPath"] = inputPath;
            return resp;
        }

        private bool ExecuteRenameInternal(
            Dictionary<string, string> cidToNewTitle,
            string strategyStr,
            out RenameBatchResult result,
            out int errCode,
            out string errMsg)
        {
            result = null;
            errCode = 0;
            errMsg = null;
            if (!TryAcquireJob("rename_execute", "正在执行三层会话重命名与快照保护..."))
            {
                errCode = 409;
                errMsg = "当前有其他后台任务正在执行，请稍后再试。";
                return false;
            }
            try
            {
                bool hotOnly;
                if (!ResolveStrategyExecution(strategyStr, out hotOnly, out errCode, out errMsg))
                    return false;

                bool renameOk;
                string batchWarn;
                lock (_mutationLock)
                {
                    renameOk = LocalWorkspaceManager.RenameSessionsBatch(_geminiPath, cidToNewTitle, hotOnly, out result);
                    batchWarn = LocalWorkspaceManager.LastWarning;
                    if (renameOk) ReloadAllLocalStateLocked();
                }

                if (!renameOk)
                {
                    errCode = 400;
                    errMsg = !string.IsNullOrEmpty(batchWarn) ? batchWarn : "重命名未能更新任何会话，请检查会话状态或标题格式。";
                    return false;
                }

                AppendLog("success", string.Format("会话重命名完成：请求 {0} 条，实际 {1} 条，跳过运行中 {2} 条（L1={3}, L2={4}, L3={5}）。",
                    cidToNewTitle.Count, result.RenamedCount, result.SkippedRunningIds.Count, result.Layer1Updated, result.Layer2Updated, result.Layer3Updated));
                return true;
            }
            finally
            {
                ReleaseJob();
            }
        }

        private void UpdateBaselineForCapabilities(List<CapabilityItem> alignedCaps)
        {
            if (alignedCaps == null || alignedCaps.Count == 0) return;
            for (int i = 0; i < alignedCaps.Count; i++)
            {
                CapabilityItem c = alignedCaps[i];
                if (c != null && !string.IsNullOrEmpty(c.UniqueKey) && !string.IsNullOrEmpty(c.ContentSignature))
                    _capBaseline[c.UniqueKey] = c.ContentSignature;
            }
            CapabilitySyncBaselineStore.Save(_baselinePath, _capBaseline);
        }

        // ── 载荷构造器 ──

        private Dictionary<string, object> BuildOverviewPayload()
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            lock (_stateLock)
            {
                Dictionary<string, object> counts = new Dictionary<string, object>();
                counts["formalProjectsCount"] = _projectGroups.FormalProjectsCount;
                counts["totalRawSessions"] = _projectGroups.TotalRawSessions;
                counts["formalPrimarySessions"] = _projectGroups.FormalPrimarySessions;
                counts["cliSessions"] = _projectGroups.CliSessions;
                counts["outsideSessions"] = _projectGroups.OutsideSessions;
                counts["subagentSessions"] = _projectGroups.SubagentSessions;
                counts["ephemeralSessions"] = _projectGroups.EphemeralSessions;
                counts["localCapabilitiesCount"] = _localCaps.Count;
                counts["repoCapabilitiesCount"] = _repoCaps.Count;

                int untaggedFormal = 0;
                for (int i = 0; i < _localSummaries.Count; i++)
                {
                    ConversationSummaryInfo s = _localSummaries[i];
                    if (s == null || s.IsSubagent || s.IsCliSession) continue;
                    if (VisualDiffEngine.IsOutsideProject(s.ProjectName) || SmartProjectFilter.IsJunkOrEphemeral(s.ProjectName)) continue;
                    if (!AutoTitleSynthesizer.HasStandardBracketedPrefix(s.DisplayTitle))
                        untaggedFormal++;
                }
                counts["untaggedFormalSessions"] = untaggedFormal;

                int aligned = 0, remoteOnly = 0, localOnly = 0, remoteNewer = 0, localNewer = 0, bothMod = 0, missingPaths = 0;
                for (int i = 0; i < _capDiffs.Count; i++)
                {
                    CapabilityDiffNode cdn = _capDiffs[i];
                    if (cdn.Direction == CapabilityDiffDirection.Aligned) aligned++;
                    else if (cdn.Direction == CapabilityDiffDirection.RemoteOnly) remoteOnly++;
                    else if (cdn.Direction == CapabilityDiffDirection.LocalOnly) localOnly++;
                    else if (cdn.Direction == CapabilityDiffDirection.RemoteNewer) remoteNewer++;
                    else if (cdn.Direction == CapabilityDiffDirection.LocalNewer) localNewer++;
                    else bothMod++;

                    if ((cdn.LocalItem != null && cdn.LocalItem.HasMissingPaths) ||
                        (cdn.RemoteItem != null && cdn.RemoteItem.HasMissingPaths))
                    {
                        missingPaths++;
                    }
                }
                Dictionary<string, object> capSummary = new Dictionary<string, object>();
                capSummary["aligned"] = aligned;
                capSummary["remoteOnly"] = remoteOnly;
                capSummary["localOnly"] = localOnly;
                capSummary["remoteNewer"] = remoteNewer;
                capSummary["localNewer"] = localNewer;
                capSummary["bothModified"] = bothMod;
                capSummary["missingPaths"] = missingPaths;
                counts["capabilitySummary"] = capSummary;

                d["counts"] = counts;
                d["ide"] = BuildIdeStatusDict();
                d["lastOrphanScan"] = _lastOrphanScan != null ? SerializeOrphanScan(_lastOrphanScan) : null;
                d["paths"] = BuildPathsDict();
                d["logs"] = BuildRecentLogsList();
                d["job"] = BuildJobStatusDict(Interlocked.CompareExchange(ref _jobBusyFlag, 0, 0) != 0, _currentJobType, _currentJobMessage, _currentJobPercent);
            }
            return d;
        }

        private Dictionary<string, object> BuildPathsDict()
        {
            Dictionary<string, object> p = new Dictionary<string, object>();
            p["home"] = _homePath;
            p["gemini"] = _geminiPath;
            p["repository"] = _repoPath;
            return p;
        }

        private List<object> BuildRecentLogsList()
        {
            List<object> list = new List<object>(_activityLogs.Count);
            for (int i = 0; i < _activityLogs.Count; i++) list.Add(_activityLogs[i]);
            return list;
        }

        private static Dictionary<string, object> BuildJobStatusDict(bool busy, string jobType, string msg, int pct)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["busy"] = busy;
            d["jobType"] = jobType ?? string.Empty;
            d["message"] = msg ?? string.Empty;
            d["percent"] = pct;
            return d;
        }

        private Dictionary<string, object> BuildIdeStatusDict()
        {
            int p, ls;
            ProcessGuardService.GetRunningAntigravityProcessCounts(out p, out ls);
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["running"] = (p > 0 || ls > 0);
            d["parentCount"] = p;
            d["lsCount"] = ls;
            return d;
        }

        private static Dictionary<string, object> SerializeOrphanScan(OrphanScanResult scan)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            if (scan == null) return d;
            d["totalItems"] = scan.TotalOrphanItems;
            d["sessionCount"] = scan.OrphanSessionIds.Count;
            d["dbFilesCount"] = scan.OrphanDbFiles.Count;
            d["brainDirsCount"] = scan.OrphanBrainDirs.Count;
            d["annotationFilesCount"] = scan.OrphanAnnotationFiles.Count;
            d["totalMegabytes"] = Math.Round(scan.TotalOrphanMegabytes, 2);
            return d;
        }

        private Dictionary<string, object> BuildSessionsPayload()
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            lock (_stateLock)
            {
                d["formalProjectsCount"] = _projectGroups.FormalProjectsCount;
                d["totalRawSessions"] = _projectGroups.TotalRawSessions;
                d["formalPrimarySessions"] = _projectGroups.FormalPrimarySessions;
                d["cliSessions"] = _projectGroups.CliSessions;
                d["outsideSessions"] = _projectGroups.OutsideSessions;
                d["subagentSessions"] = _projectGroups.SubagentSessions;
                d["ephemeralSessions"] = _projectGroups.EphemeralSessions;

                List<object> grpArr = new List<object>();
                for (int i = 0; i < _projectGroups.Groups.Count; i++)
                {
                    ProjectDisplayGroup g = _projectGroups.Groups[i];
                    Dictionary<string, object> gd = new Dictionary<string, object>();
                    gd["bucketKey"] = g.BucketKey;
                    gd["groupKind"] = g.GroupKind;
                    gd["projectName"] = g.ProjectName ?? string.Empty;
                    gd["projectId"] = g.ProjectId ?? string.Empty;
                    gd["fullPath"] = g.FullPath ?? string.Empty;
                    gd["shortPath"] = g.ShortPath ?? string.Empty;
                    gd["distinguishingLabel"] = g.DistinguishingLabel ?? string.Empty;
                    gd["isMultiRoot"] = g.IsMultiRoot;
                    gd["isDuplicateBasename"] = g.IsDuplicateBasename;
                    gd["rootPaths"] = new List<object>(ToObjList(g.RootPaths));
                    gd["primaryCount"] = g.PrimaryCount;
                    gd["totalCount"] = g.TotalCount;

                    List<object> convArr = new List<object>(g.Conversations.Count);
                    for (int k = 0; k < g.Conversations.Count; k++)
                        convArr.Add(SerializeSessionSummary(g.Conversations[k]));
                    gd["sessions"] = convArr;
                    grpArr.Add(gd);
                }
                d["groups"] = grpArr;
            }
            return d;
        }

        private Dictionary<string, object> SerializeSessionSummary(ConversationSummaryInfo s)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["cid"] = s.ConversationId ?? string.Empty;
            d["title"] = s.DisplayTitle ?? string.Empty;
            d["projectName"] = s.ProjectName ?? string.Empty;
            d["projectId"] = s.ProjectId ?? string.Empty;
            d["fullPath"] = WorkspaceService.UriToDisplayPath(s.FolderUri);
            d["shortPath"] = WorkspaceService.ShortenHomeDisplayPath(WorkspaceService.UriToDisplayPath(s.FolderUri), _homePath);
            d["stepCount"] = s.StepCount;
            d["updatedTime"] = s.UpdatedTime > DateTime.MinValue ? s.UpdatedTime.ToString("yyyy-MM-dd HH:mm") : "-";
            d["updatedIso"] = s.UpdatedTime > DateTime.MinValue ? s.UpdatedTime.ToString("yyyy-MM-ddTHH:mm:ss") : string.Empty;
            d["hasCustomTitle"] = s.HasCustomTitle;
            d["isTagged"] = AutoTitleSynthesizer.HasStandardBracketedPrefix(s.DisplayTitle);
            d["isSubagent"] = s.IsSubagent;
            d["agentName"] = s.AgentName ?? string.Empty;
            d["isCli"] = s.IsCliSession;
            d["isRunning"] = IsSessionRunning(s);
            d["firstPromptSnippet"] = s.FirstPromptSnippet ?? string.Empty;
            d["gitBranchHint"] = s.GitBranchHint ?? string.Empty;
            return d;
        }

        private static bool IsSessionRunning(ConversationSummaryInfo s)
        {
            if (s == null) return false;
            return s.NotFullyIdle ||
                   (!string.IsNullOrEmpty(s.RunStatus) &&
                    s.RunStatus.IndexOf("RUNNING", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private List<SessionRenameCandidate> BuildAllCandidateObjects()
        {
            string ruleErr;
            RenameRulesConfig rules = AutoTitleSynthesizer.LoadRules(_repoPath, out ruleErr);
            List<ConversationSummaryInfo> snap;
            lock (_stateLock) snap = new List<ConversationSummaryInfo>(_localSummaries);

            List<SessionRenameCandidate> list = new List<SessionRenameCandidate>(snap.Count);
            for (int i = 0; i < snap.Count; i++)
            {
                ConversationSummaryInfo s = snap[i];
                if (s == null || string.IsNullOrWhiteSpace(s.ConversationId)) continue;

                SessionRenameCandidate c = new SessionRenameCandidate
                {
                    ConversationId = s.ConversationId.Trim(),
                    ProjectName = s.ProjectName ?? string.Empty,
                    ProjectId = s.ProjectId ?? string.Empty,
                    FolderUri = s.FolderUri ?? string.Empty,
                    OldTitle = s.DisplayTitle ?? string.Empty,
                    OldAutoTitle = s.AutoSummaryTitle ?? string.Empty,
                    OldCustomTitle = s.CustomTitle ?? string.Empty,
                    HasCustomTitle = s.HasCustomTitle,
                    FirstPromptSnippet = s.FirstPromptSnippet ?? string.Empty,
                    GitBranchHint = s.GitBranchHint ?? string.Empty,
                    UpdatedTime = s.UpdatedTime,
                    StepCount = s.StepCount,
                    Summary = s
                };
                AutoTitleSynthesizer.SynthesizeCandidate(c, rules);

                bool isRunning = IsSessionRunning(s);
                bool isTagged = AutoTitleSynthesizer.HasStandardBracketedPrefix(c.OldTitle);
                bool isJunk = SmartProjectFilter.IsJunkOrEphemeral(c.ProjectName) && !s.IsCliSession;
                bool isOut = VisualDiffEngine.IsOutsideProject(c.ProjectName);
                c.Selected = !s.IsSubagent && !isJunk && !isOut && !s.IsCliSession && !isTagged && !c.IsLowValue && !isRunning;
                list.Add(c);
            }

            list.Sort(delegate(SessionRenameCandidate a, SessionRenameCandidate b)
            {
                return DateTime.Compare(b.UpdatedTime, a.UpdatedTime);
            });
            return list;
        }

        private List<SessionRenameCandidate> BuildSelectedCandidatesForAi(List<string> cids)
        {
            List<SessionRenameCandidate> all = BuildAllCandidateObjects();
            if (cids == null || cids.Count == 0) return new List<SessionRenameCandidate>();
            HashSet<string> set = new HashSet<string>(cids, StringComparer.OrdinalIgnoreCase);
            List<SessionRenameCandidate> res = new List<SessionRenameCandidate>();
            for (int i = 0; i < all.Count; i++)
            {
                if (set.Contains(all[i].ConversationId))
                    res.Add(all[i]);
            }
            return res;
        }

        private Dictionary<string, object> BuildRenameCandidatesPayload()
        {
            List<SessionRenameCandidate> cands = BuildAllCandidateObjects();
            List<RenameSnapshotInfo> snaps = RenameBackupManager.ListSnapshots(_geminiPath);

            List<object> items = new List<object>(cands.Count);
            HashSet<string> projectSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < cands.Count; i++)
            {
                SessionRenameCandidate c = cands[i];
                bool isSub = c.Summary != null && c.Summary.IsSubagent;
                bool isCli = c.Summary != null && c.Summary.IsCliSession;
                bool isOut = VisualDiffEngine.IsOutsideProject(c.ProjectName);
                bool isJunk = SmartProjectFilter.IsJunkOrEphemeral(c.ProjectName) && !isCli;
                if (!isSub && !isCli && !isOut && !isJunk && !string.IsNullOrEmpty(c.ProjectName))
                    projectSet.Add(c.ProjectName);

                Dictionary<string, object> row = new Dictionary<string, object>();
                row["cid"] = c.ConversationId;
                row["projectName"] = c.ProjectName ?? string.Empty;
                row["projectId"] = c.ProjectId ?? string.Empty;
                row["fullPath"] = WorkspaceService.UriToDisplayPath(c.FolderUri);
                row["shortPath"] = WorkspaceService.ShortenHomeDisplayPath(WorkspaceService.UriToDisplayPath(c.FolderUri), _homePath);
                row["oldTitle"] = c.OldTitle ?? string.Empty;
                row["suggestedPrefix"] = c.SuggestedPrefix ?? string.Empty;
                row["suggestedTitle"] = c.SuggestedTitle ?? string.Empty;
                row["firstPromptSnippet"] = c.FirstPromptSnippet ?? string.Empty;
                row["cleanedPrompt"] = c.CleanedPrompt ?? string.Empty;
                row["gitBranchHint"] = c.GitBranchHint ?? string.Empty;
                row["runStatus"] = (c.Summary != null && !string.IsNullOrEmpty(c.Summary.RunStatus)) ? c.Summary.RunStatus : "IDLE";
                row["stepCount"] = c.StepCount;
                row["updatedTime"] = c.UpdatedTime > DateTime.MinValue ? c.UpdatedTime.ToString("yyyy-MM-dd HH:mm") : "-";
                row["selected"] = c.Selected;
                row["isLowValue"] = c.IsLowValue;
                row["isTagged"] = AutoTitleSynthesizer.HasStandardBracketedPrefix(c.OldTitle);
                row["isRunning"] = IsSessionRunning(c.Summary);
                row["isCli"] = isCli;
                row["isOutside"] = isOut;
                row["isSubagent"] = isSub;
                row["isJunk"] = isJunk;
                items.Add(row);
            }

            List<string> projList = new List<string>(projectSet);
            projList.Sort(StringComparer.OrdinalIgnoreCase);

            Dictionary<string, object> resp = new Dictionary<string, object>();
            resp["candidates"] = items;
            resp["formalProjects"] = new List<object>(ToObjList(projList));
            resp["snapshotCount"] = snaps.Count;
            resp["ide"] = BuildIdeStatusDict();
            return resp;
        }

        private Dictionary<string, object> BuildCapabilitiesPayload(bool fromBackup)
        {
            List<CapabilityDiffNode> diffs;
            lock (_stateLock)
            {
                diffs = new List<CapabilityDiffNode>(fromBackup ? _backupCapDiffs : _capDiffs);
            }
            List<object> items = new List<object>(diffs.Count);
            for (int i = 0; i < diffs.Count; i++)
            {
                CapabilityDiffNode cdn = diffs[i];
                CapabilityItem pri = cdn.PrimaryItem;
                if (pri == null) continue;

                Dictionary<string, object> row = new Dictionary<string, object>();
                row["key"] = cdn.UniqueKey;
                row["kind"] = CapabilityItem.GetKindShortTag(cdn.Kind);
                row["kindEnum"] = cdn.Kind.ToString();
                row["id"] = cdn.Id ?? string.Empty;
                row["displayName"] = pri.DisplayName ?? cdn.Id ?? string.Empty;
                row["description"] = pri.Description ?? string.Empty;
                row["direction"] = cdn.Direction.ToString();
                row["directionBadge"] = cdn.GetDirectionalBadge();
                row["directionReason"] = cdn.DirectionReason ?? "signature";
                row["legacyStatus"] = cdn.Status.ToString();
                row["localExists"] = cdn.LocalItem != null;
                row["remoteExists"] = cdn.RemoteItem != null;
                row["localTime"] = FormatUtcTime(cdn.LocalItem != null ? cdn.LocalItem.LastModified : DateTime.MinValue);
                row["remoteTime"] = FormatUtcTime(cdn.RemoteItem != null ? cdn.RemoteItem.LastModified : DateTime.MinValue);
                row["hasMissingLocalPaths"] = cdn.LocalItem != null && cdn.LocalItem.HasMissingPaths;
                row["hasMissingRemotePaths"] = cdn.RemoteItem != null && cdn.RemoteItem.HasMissingPaths;
                row["missingLocalPaths"] = cdn.LocalItem != null ? new List<object>(ToObjList(cdn.LocalItem.MissingLocalPaths)) : new List<object>();
                row["missingRemotePaths"] = cdn.RemoteItem != null ? new List<object>(ToObjList(cdn.RemoteItem.MissingLocalPaths)) : new List<object>();
                row["localFilesCount"] = cdn.LocalItem != null && cdn.LocalItem.FilesMap != null ? cdn.LocalItem.FilesMap.Count : 0;
                row["remoteFilesCount"] = cdn.RemoteItem != null && cdn.RemoteItem.FilesMap != null ? cdn.RemoteItem.FilesMap.Count : 0;
                items.Add(row);
            }

            Dictionary<string, object> resp = new Dictionary<string, object>();
            resp["items"] = items;
            resp["repositoryPath"] = _repoPath;
            return resp;
        }

        private Dictionary<string, object> BuildCapabilityLineDiffPayload(CapabilityDiffNode cdn)
        {
            Dictionary<string, string> localFiles = CollectCapabilityReadableFiles(cdn.LocalItem);
            Dictionary<string, string> remoteFiles = CollectCapabilityReadableFiles(cdn.RemoteItem);

            HashSet<string> allNames = new HashSet<string>(localFiles.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (string rk in remoteFiles.Keys) allNames.Add(rk);
            List<string> sortedNames = new List<string>(allNames);
            sortedNames.Sort(StringComparer.OrdinalIgnoreCase);

            List<object> filesPayload = new List<object>(sortedNames.Count);
            for (int i = 0; i < sortedNames.Count; i++)
            {
                string fn = sortedNames[i];
                string leftText;
                localFiles.TryGetValue(fn, out leftText);
                string rightText;
                remoteFiles.TryGetValue(fn, out rightText);

                bool isBinary = (leftText == "[BINARY]") || (rightText == "[BINARY]");
                List<object> lineObjs = new List<object>();
                bool truncated = false;
                string fileStatus = "equal";

                if (leftText == null && rightText != null) fileStatus = "remote_only";
                else if (leftText != null && rightText == null) fileStatus = "local_only";
                else if (!string.Equals(leftText ?? string.Empty, rightText ?? string.Empty, StringComparison.Ordinal)) fileStatus = "modified";

                if (!isBinary)
                {
                    List<TextDiffLine> lines = VisualDiffEngine.ComputeLineDiff(leftText ?? string.Empty, rightText ?? string.Empty, out truncated);
                    for (int l = 0; l < lines.Count; l++)
                    {
                        Dictionary<string, object> lo = new Dictionary<string, object>();
                        lo["type"] = lines[l].Type;
                        lo["oldLine"] = lines[l].OldLine;
                        lo["newLine"] = lines[l].NewLine;
                        lo["text"] = lines[l].Text ?? string.Empty;
                        lineObjs.Add(lo);
                    }
                }

                Dictionary<string, object> fp = new Dictionary<string, object>();
                fp["path"] = fn;
                fp["status"] = fileStatus;
                fp["isBinary"] = isBinary;
                fp["truncated"] = truncated;
                fp["lines"] = lineObjs;
                filesPayload.Add(fp);
            }

            CapabilityItem pri = cdn.PrimaryItem;
            Dictionary<string, object> resp = new Dictionary<string, object>();
            resp["key"] = cdn.UniqueKey;
            resp["id"] = cdn.Id ?? string.Empty;
            resp["kind"] = CapabilityItem.GetKindShortTag(cdn.Kind);
            resp["direction"] = cdn.Direction.ToString();
            resp["directionBadge"] = cdn.GetDirectionalBadge();
            resp["directionReason"] = cdn.DirectionReason ?? "signature";
            resp["localExists"] = cdn.LocalItem != null;
            resp["remoteExists"] = cdn.RemoteItem != null;
            resp["files"] = filesPayload;
            resp["missingLocalPaths"] = cdn.LocalItem != null ? new List<object>(ToObjList(cdn.LocalItem.MissingLocalPaths)) : new List<object>();
            resp["missingRemotePaths"] = cdn.RemoteItem != null ? new List<object>(ToObjList(cdn.RemoteItem.MissingLocalPaths)) : new List<object>();
            resp["referencedPaths"] = (pri != null && pri.ReferencedLocalPaths != null) ? new List<object>(ToObjList(pri.ReferencedLocalPaths)) : new List<object>();
            return resp;
        }

        private static Dictionary<string, string> CollectCapabilityReadableFiles(CapabilityItem item)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (item == null) return dict;

            if (!string.IsNullOrEmpty(item.RawJsonFragment))
            {
                dict["_config_registration.json"] = item.RawJsonFragment;
            }
            if (item.FilesMap != null)
            {
                foreach (string relKey in item.FilesMap.Keys)
                {
                    byte[] bytes = item.GetFileBytes(relKey);
                    if (bytes == null)
                    {
                        dict[relKey] = string.Empty;
                        continue;
                    }
                    if (bytes.Length > 512 * 1024)
                    {
                        dict[relKey] = string.Format("[文件过大 ({0:F1} KB)，请打开源码目录查看]", bytes.Length / 1024.0);
                        continue;
                    }
                    if (LooksBinary(bytes))
                    {
                        dict[relKey] = "[BINARY]";
                        continue;
                    }
                    string text = Encoding.UTF8.GetString(bytes);
                    if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
                    dict[relKey] = text;
                }
            }
            return dict;
        }

        private static bool LooksBinary(byte[] bytes)
        {
            int checkLen = Math.Min(bytes.Length, 4096);
            for (int i = 0; i < checkLen; i++)
            {
                if (bytes[i] == 0) return true;
            }
            return false;
        }

        private Dictionary<string, object> BuildBackupPayload()
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            lock (_stateLock)
            {
                bool loaded = _backupIndex != null && !string.IsNullOrEmpty(_loadedBackupPath);
                d["loaded"] = loaded;
                if (!loaded) return d;

                d["archivePath"] = _loadedBackupPath;
                d["fileName"] = Path.GetFileName(_loadedBackupPath);
                d["sourceHome"] = _backupIndex.SourceHome ?? string.Empty;
                d["sourceUser"] = _backupIndex.SourceUser ?? string.Empty;
                d["backupTime"] = _backupIndex.BackupTime ?? string.Empty;
                d["version"] = _backupIndex.Version ?? string.Empty;
                d["sizeMegabytes"] = Math.Round(_backupIndex.TotalZipSizeBytes / (1024.0 * 1024.0), 2);
                d["capabilitiesCount"] = _backupIndex.Capabilities != null ? _backupIndex.Capabilities.Count : 0;
                d["projectsCount"] = _backupIndex.Projects != null ? _backupIndex.Projects.Count : 0;
                d["conversationsCount"] = _backupIndex.Conversations != null ? _backupIndex.Conversations.Count : 0;
                d["capabilityDiffs"] = BuildCapabilitiesPayload(true)["items"];

                Dictionary<string, ConversationSummaryInfo> localMap = new Dictionary<string, ConversationSummaryInfo>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < _localSummaries.Count; i++)
                {
                    if (!string.IsNullOrEmpty(_localSummaries[i].ConversationId))
                        localMap[_localSummaries[i].ConversationId] = _localSummaries[i];
                }

                List<object> projArr = new List<object>();
                for (int p = 0; p < _backupSessionDiffs.Count; p++)
                {
                    ProjectDiffNode pd = _backupSessionDiffs[p];
                    if (pd == null || pd.BackupConversations == null || pd.BackupConversations.Count == 0) continue;

                    Dictionary<string, object> projObj = new Dictionary<string, object>();
                    projObj["projectName"] = pd.ProjectName ?? string.Empty;
                    projObj["status"] = pd.Status.ToString();
                    projObj["shortPath"] = WorkspaceService.ShortenHomeDisplayPath(WorkspaceService.UriToDisplayPath(pd.BackupFolderUri), _backupIndex.SourceHome);

                    List<object> convs = new List<object>();
                    for (int k = 0; k < pd.BackupConversations.Count; k++)
                    {
                        ConversationSummaryInfo bc = pd.BackupConversations[k];
                        ConversationSummaryInfo lc = null;
                        if (!string.IsNullOrEmpty(bc.ConversationId))
                            localMap.TryGetValue(bc.ConversationId, out lc);

                        Dictionary<string, object> cd = new Dictionary<string, object>();
                        cd["cid"] = bc.ConversationId ?? string.Empty;
                        cd["title"] = bc.DisplayTitle ?? string.Empty;
                        cd["projectName"] = pd.ProjectName ?? string.Empty;
                        cd["stepCount"] = bc.StepCount;
                        cd["localStepCount"] = lc != null ? lc.StepCount : -1;
                        cd["updatedTime"] = bc.UpdatedTime > DateTime.MinValue ? bc.UpdatedTime.ToString("yyyy-MM-dd HH:mm") : "-";
                        cd["status"] = lc == null ? "New" : (bc.StepCount > lc.StepCount ? "Newer" : (bc.DisplayTitle != lc.DisplayTitle ? "Modified" : "Aligned"));
                        cd["autoSelected"] = MainForm.ShouldAutoSelectBackupSession(bc, lc, pd);
                        cd["isSubagent"] = bc.IsSubagent;
                        convs.Add(cd);
                    }
                    projObj["sessions"] = convs;
                    projArr.Add(projObj);
                }
                d["sessionDiffGroups"] = projArr;
            }
            return d;
        }

        private static Dictionary<string, object> SerializeRulesConfigObj(RenameRulesConfig cfg)
        {
            Dictionary<string, object> root = new Dictionary<string, object>();
            root["version"] = cfg.Version > 0 ? cfg.Version : 1;
            root["keepExistingBracketedPrefix"] = cfg.KeepExistingBracketedPrefix;
            Dictionary<string, object> aliases = new Dictionary<string, object>();
            if (cfg.ProjectAliases != null)
            {
                foreach (KeyValuePair<string, string> kvp in cfg.ProjectAliases)
                    aliases[kvp.Key] = kvp.Value ?? string.Empty;
            }
            root["projectAliases"] = aliases;

            List<object> multiList = new List<object>();
            if (cfg.MultiRepoRules != null)
            {
                for (int i = 0; i < cfg.MultiRepoRules.Count; i++)
                {
                    MultiRepoRule mr = cfg.MultiRepoRules[i];
                    Dictionary<string, object> mrObj = new Dictionary<string, object>();
                    mrObj["projectName"] = mr.ProjectName ?? string.Empty;
                    mrObj["defaultModel"] = mr.DefaultModel ?? string.Empty;
                    mrObj["branchRegexes"] = new List<object>(ToObjList(mr.BranchRegexes));
                    List<object> mmList = new List<object>();
                    if (mr.ModelMatchers != null)
                    {
                        for (int k = 0; k < mr.ModelMatchers.Count; k++)
                        {
                            Dictionary<string, object> mm = new Dictionary<string, object>();
                            mm["model"] = mr.ModelMatchers[k].Model ?? string.Empty;
                            mm["keywords"] = new List<object>(ToObjList(mr.ModelMatchers[k].Keywords));
                            mmList.Add(mm);
                        }
                    }
                    mrObj["modelMatchers"] = mmList;
                    multiList.Add(mrObj);
                }
            }
            root["multiRepoRules"] = multiList;
            return root;
        }

        private static Dictionary<string, object> SerializeBatchRenameResult(RenameBatchResult res)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["ok"] = res != null && res.RenamedCount > 0;
            if (res == null) return d;
            d["renamedCount"] = res.RenamedCount;
            d["layer1Updated"] = res.Layer1Updated;
            d["layer2Updated"] = res.Layer2Updated;
            d["layer3Updated"] = res.Layer3Updated;
            d["snapshotDir"] = res.BackupSnapshotDir ?? string.Empty;
            d["skippedRunning"] = new List<object>(ToObjList(res.SkippedRunningIds));
            d["warnings"] = new List<object>(ToObjList(res.Warnings));
            return d;
        }

        private ConversationSummaryInfo FindSession(string cid, bool fromLocal)
        {
            string trimmed = cid.Trim();
            lock (_stateLock)
            {
                List<ConversationSummaryInfo> list = fromLocal
                    ? _localSummaries
                    : (_backupIndex != null ? _backupIndex.Conversations : null);
                if (list == null) return null;
                for (int i = 0; i < list.Count; i++)
                {
                    if (string.Equals(list[i].ConversationId, trimmed, StringComparison.OrdinalIgnoreCase))
                        return list[i];
                }
            }
            return null;
        }

        private CapabilityDiffNode FindCapDiffNode(string key, bool fromBackup)
        {
            if (string.IsNullOrEmpty(key)) return null;
            lock (_stateLock)
            {
                List<CapabilityDiffNode> list = fromBackup ? _backupCapDiffs : _capDiffs;
                for (int i = 0; i < list.Count; i++)
                {
                    if (string.Equals(list[i].UniqueKey, key.Trim(), StringComparison.OrdinalIgnoreCase))
                        return list[i];
                }
            }
            return null;
        }

        private static List<CapabilityItem> SelectCapsByKeys(List<CapabilityItem> pool, List<string> keys)
        {
            List<CapabilityItem> res = new List<CapabilityItem>();
            if (pool == null || keys == null || keys.Count == 0) return res;
            HashSet<string> set = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && set.Contains(pool[i].UniqueKey))
                    res.Add(pool[i]);
            }
            return res;
        }

        private static List<ConversationSummaryInfo> SelectSessionsByIds(List<ConversationSummaryInfo> pool, List<string> cids)
        {
            List<ConversationSummaryInfo> res = new List<ConversationSummaryInfo>();
            if (pool == null || cids == null || cids.Count == 0) return res;
            HashSet<string> set = new HashSet<string>(cids, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && set.Contains(pool[i].ConversationId))
                    res.Add(pool[i]);
            }
            return res;
        }

        private static IDictionary<string, object> ParseJsonBody(string bodyText)
        {
            if (string.IsNullOrWhiteSpace(bodyText)) return new Dictionary<string, object>();
            try
            {
                return (MiniJson.Parse(bodyText) as IDictionary<string, object>) ?? new Dictionary<string, object>();
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        private static List<string> ReadStringArray(IDictionary<string, object> dict, string key)
        {
            List<string> list = new List<string>();
            if (dict == null) return list;
            object val;
            if (!dict.TryGetValue(key, out val)) return list;
            IList<object> arr = val as IList<object>;
            if (arr == null) return list;
            for (int i = 0; i < arr.Count; i++)
            {
                string s = arr[i] as string;
                if (!string.IsNullOrWhiteSpace(s)) list.Add(s.Trim());
            }
            return list;
        }

        private static List<object> ToObjList(IEnumerable<string> seq)
        {
            List<object> list = new List<object>();
            if (seq == null) return list;
            foreach (string s in seq) list.Add(s ?? string.Empty);
            return list;
        }

        private static string FormatUtcTime(DateTime dt)
        {
            if (dt == DateTime.MinValue || dt.Year < 1980) return "-";
            DateTime local = dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
            return local.ToString("yyyy-MM-dd HH:mm");
        }

        private static string SanitizeFileName(string name)
        {
            string s = string.IsNullOrWhiteSpace(name) ? "session" : name.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                s = s.Replace(invalid[i], '_');
            return s.Length > 80 ? s.Substring(0, 80) : s;
        }

        // ── HTTP 原始写入工具（强制使用 UTF-8 字节长度作为 Content-Length）──

        private static void WriteOkJson(NetworkStream stream, Dictionary<string, object> extra)
        {
            Dictionary<string, object> d = extra ?? new Dictionary<string, object>();
            d["ok"] = true;
            WriteJsonResponse(stream, 200, d);
        }

        private static void WriteStrategyOrError(NetworkStream stream, int code, string errorMsg)
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

        private static void WriteJsonError(NetworkStream stream, int code, string errorMsg)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["ok"] = false;
            d["error"] = errorMsg ?? "请求失败";
            WriteJsonResponse(stream, code, d);
        }

        private static void WriteJsonResponse(NetworkStream stream, int code, object payload)
        {
            string json = MiniJson.Serialize(payload, false);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            string reason = code == 200 ? "OK" : (code == 202 ? "Accepted" : (code == 400 ? "Bad Request" : (code == 403 ? "Forbidden" : (code == 404 ? "Not Found" : (code == 409 ? "Conflict" : "Error")))));
            WriteRawResponse(stream, code, reason, "application/json; charset=utf-8", bytes, null);
        }

        private static void WriteErrorResponse(NetworkStream stream, int code, string msg)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(msg ?? "Error");
            WriteRawResponse(stream, code, msg ?? "Error", "text/plain; charset=utf-8", bytes, null);
        }

        private static void WriteRawResponse(NetworkStream stream, int code, string reason, string contentType, byte[] body, string setCookieHeader)
        {
            byte[] safeBody = body ?? new byte[0];
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("HTTP/1.1 {0} {1}\r\n", code, reason);
            sb.AppendFormat("Content-Type: {0}\r\n", contentType);
            sb.AppendFormat("Content-Length: {0}\r\n", safeBody.Length);
            sb.Append("Cache-Control: no-store\r\n");
            sb.Append("X-Content-Type-Options: nosniff\r\n");
            sb.Append("Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; frame-ancestors 'none'\r\n");
            if (!string.IsNullOrEmpty(setCookieHeader))
            {
                sb.AppendFormat("Set-Cookie: {0}\r\n", setCookieHeader);
            }
            sb.Append("Connection: close\r\n\r\n");

            byte[] hdrBytes = Encoding.ASCII.GetBytes(sb.ToString());
            stream.Write(hdrBytes, 0, hdrBytes.Length);
            if (safeBody.Length > 0) stream.Write(safeBody, 0, safeBody.Length);
            stream.Flush();
        }
    }
}
