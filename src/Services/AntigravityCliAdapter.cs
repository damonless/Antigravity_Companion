using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // ANTIGRAVITY CLI ADAPTER (Decision 3C: Zero-Key `agy -p --output-format json`)
    // Strictly C# 5.0 (.NET Framework 4.x)
    // ══════════════════════════════════════════════════════════════════════

    public enum CliSuggestStatus
    {
        Success,
        CliNotFound,
        Timeout,
        InvalidInput,
        CliError,
        ParseError
    }

    public class CliAvailabilityResult
    {
        public bool Available;
        public string ResolvedPath;
        public string VersionOrHint;

        public CliAvailabilityResult()
        {
            ResolvedPath = string.Empty;
            VersionOrHint = string.Empty;
        }
    }

    public class CliSessionSuggestion
    {
        public string Cid;
        public string Title;
        public string Summary;

        public CliSessionSuggestion()
        {
            Cid = string.Empty;
            Title = string.Empty;
            Summary = string.Empty;
        }
    }

    public class CliBatchSuggestionResult
    {
        public bool Ok;
        public CliSuggestStatus Status;
        public string ResolvedCliPath;
        public string ErrorOrHint;
        public string RawOutput;
        public List<CliSessionSuggestion> Suggestions;

        public CliBatchSuggestionResult()
        {
            ResolvedCliPath = string.Empty;
            ErrorOrHint = string.Empty;
            RawOutput = string.Empty;
            Suggestions = new List<CliSessionSuggestion>();
        }
    }

    public static class AntigravityCliAdapter
    {
        public const int DefaultTimeoutSeconds = 60;
        private const int VersionCheckTimeoutMs = 4000;

        private static readonly string[] CliFileNames = new string[]
        {
            "agy.exe",
            "agy.cmd",
            "agy.bat"
        };

        private static readonly string[] WrapperKeys = new string[]
        {
            "result",
            "response",
            "content",
            "output",
            "text",
            "message",
            "data"
        };

        private static readonly string[] ArrayContainerKeys = new string[]
        {
            "suggestions",
            "items",
            "results",
            "data"
        };

        private static readonly Regex MarkdownFenceRegex =
            new Regex("```(?:json|[A-Za-z0-9_-]+)?\\s*([\\s\\S]*?)```", RegexOptions.IgnoreCase);

        // ══════════════════════════════════════════════════════════════════
        // 1. CLI AUTO-DISCOVERY & AVAILABILITY
        // ══════════════════════════════════════════════════════════════════

        public static CliAvailabilityResult CheckCliAvailability(string customCliPath)
        {
            return CheckCliAvailability(customCliPath, null, null, null);
        }

        internal static CliAvailabilityResult CheckCliAvailability(
            string customCliPath,
            string pathEnvOverride,
            string localAppDataOverride,
            string userProfileOverride)
        {
            CliAvailabilityResult res = new CliAvailabilityResult();
            string resolved = ResolveAgyCliPath(customCliPath, pathEnvOverride, localAppDataOverride, userProfileOverride);
            if (string.IsNullOrEmpty(resolved))
            {
                res.Available = false;
                res.ResolvedPath = string.Empty;
                res.VersionOrHint = "未检测到 agy.exe / agy.cmd。请确认已安装 Antigravity CLI 并加入 PATH，或指定自定义 CLI 路径。";
                return res;
            }

            res.Available = true;
            res.ResolvedPath = resolved;

            string verText = TryProbeVersion(resolved);
            res.VersionOrHint = !string.IsNullOrEmpty(verText)
                ? verText
                : ("CLI ready: " + Path.GetFileName(resolved));
            return res;
        }

        public static string ResolveAgyCliPath(string customCliPath)
        {
            return ResolveAgyCliPath(customCliPath, null, null, null);
        }

        internal static string ResolveAgyCliPath(
            string customCliPath,
            string pathEnvOverride,
            string localAppDataOverride,
            string userProfileOverride)
        {
            // 1. Custom configured path (file or directory)
            if (!string.IsNullOrWhiteSpace(customCliPath))
            {
                string trimmed = customCliPath.Trim().Trim('"');
                try
                {
                    if (File.Exists(trimmed))
                    {
                        string ext = Path.GetExtension(trimmed);
                        if (string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ext, ".cmd", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ext, ".bat", StringComparison.OrdinalIgnoreCase))
                        {
                            return Path.GetFullPath(trimmed);
                        }
                        string parentDir = Path.GetDirectoryName(trimmed);
                        string siblingHit = FindCliInDirectory(parentDir);
                        if (!string.IsNullOrEmpty(siblingHit)) return siblingHit;
                    }
                    else if (Directory.Exists(trimmed))
                    {
                        string hit = FindCliInDirectory(trimmed);
                        if (!string.IsNullOrEmpty(hit)) return hit;
                    }
                }
                catch { }
            }

            // 2. PATH environment variable
            string pathEnv = pathEnvOverride != null ? pathEnvOverride : Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrWhiteSpace(pathEnv))
            {
                string[] dirs = pathEnv.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < dirs.Length; i++)
                {
                    string dir = dirs[i].Trim().Trim('"');
                    if (string.IsNullOrEmpty(dir)) continue;
                    string hit = FindCliInDirectory(dir);
                    if (!string.IsNullOrEmpty(hit)) return hit;
                }
            }

            // 3. Standard Antigravity install dirs (skipped if explicit empty overrides passed in isolation tests)
            if (pathEnvOverride != null && localAppDataOverride != null && userProfileOverride != null &&
                pathEnvOverride.Length == 0 && localAppDataOverride.Length == 0 && userProfileOverride.Length == 0)
            {
                return string.Empty;
            }

            List<string> standardDirs = CollectStandardInstallDirs(localAppDataOverride, userProfileOverride);
            for (int i = 0; i < standardDirs.Count; i++)
            {
                string hit = FindCliInDirectory(standardDirs[i]);
                if (!string.IsNullOrEmpty(hit)) return hit;
            }

            return string.Empty;
        }

        private static List<string> CollectStandardInstallDirs(string localAppDataOverride, string userProfileOverride)
        {
            List<string> dirs = new List<string>();
            string localApp = localAppDataOverride != null ? localAppDataOverride : Environment.GetEnvironmentVariable("LOCALAPPDATA");
            string appData = localAppDataOverride != null ? string.Empty : Environment.GetEnvironmentVariable("APPDATA");
            string userProfile = userProfileOverride != null ? userProfileOverride : WorkspaceService.ResolveDefaultHomePath();
            string progFiles = localAppDataOverride != null ? string.Empty : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            if (!string.IsNullOrWhiteSpace(localApp))
            {
                dirs.Add(Path.Combine(localApp, "Programs", "Antigravity", "bin"));
                dirs.Add(Path.Combine(localApp, "Programs", "Antigravity"));
                dirs.Add(Path.Combine(localApp, "Programs", "Antigravity IDE", "bin"));
                dirs.Add(Path.Combine(localApp, "Programs", "Antigravity IDE"));
                dirs.Add(Path.Combine(localApp, "Programs", "antigravity", "bin"));
                dirs.Add(Path.Combine(localApp, "Programs", "antigravity"));
            }
            if (!string.IsNullOrWhiteSpace(appData))
            {
                dirs.Add(Path.Combine(appData, "Antigravity", "bin"));
                dirs.Add(Path.Combine(appData, "npm"));
            }
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                dirs.Add(Path.Combine(userProfile, ".gemini", "antigravity-ide", "bin"));
                dirs.Add(Path.Combine(userProfile, ".gemini", "antigravity-cli", "bin"));
                dirs.Add(Path.Combine(userProfile, ".gemini", "antigravity-cli"));
            }
            if (!string.IsNullOrWhiteSpace(progFiles))
            {
                dirs.Add(Path.Combine(progFiles, "Antigravity", "bin"));
                dirs.Add(Path.Combine(progFiles, "Antigravity IDE", "bin"));
            }
            return dirs;
        }

        private static string FindCliInDirectory(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir)) return null;
            try
            {
                if (!Directory.Exists(dir)) return null;
                for (int i = 0; i < CliFileNames.Length; i++)
                {
                    string candidate = Path.Combine(dir, CliFileNames[i]);
                    if (File.Exists(candidate))
                        return Path.GetFullPath(candidate);
                }
            }
            catch { }
            return null;
        }

        private static string TryProbeVersion(string exePath)
        {
            Process proc = null;
            ManualResetEventSlim outEof = new ManualResetEventSlim(false);
            ManualResetEventSlim errEof = new ManualResetEventSlim(false);
            StringBuilder outSb = new StringBuilder();
            object ioLock = new object();

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = exePath;
                psi.Arguments = "--version";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;

                proc = Process.Start(psi);
                if (proc == null) return string.Empty;

                proc.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data == null) { outEof.Set(); return; }
                    lock (ioLock)
                    {
                        if (outSb.Length < 512) outSb.AppendLine(e.Data);
                    }
                };
                proc.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data == null) { errEof.Set(); return; }
                };

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                try { proc.StandardInput.Close(); } catch { }

                if (!proc.WaitForExit(VersionCheckTimeoutMs))
                {
                    EnsureProcessTreeTerminated(proc);
                    return string.Empty;
                }

                try { outEof.Wait(300); } catch { }
                try { errEof.Wait(200); } catch { }

                if (proc.ExitCode == 0)
                {
                    string raw;
                    lock (ioLock) { raw = outSb.ToString().Trim(); }
                    if (!string.IsNullOrEmpty(raw))
                    {
                        int nl = raw.IndexOfAny(new char[] { '\r', '\n' });
                        return nl > 0 ? raw.Substring(0, nl).Trim() : raw;
                    }
                }
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                if (proc != null)
                {
                    EnsureProcessTreeTerminated(proc);
                    try { proc.Dispose(); } catch { }
                }
                outEof.Dispose();
                errEof.Dispose();
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 2. ZERO-KEY BATCH RENAME SUGGESTIONS VIA `agy -p --output-format json`
        // ══════════════════════════════════════════════════════════════════

        public static CliBatchSuggestionResult GenerateBatchSuggestionsViaCli(
            List<SessionRenameCandidate> batch,
            string customCliPath,
            int timeoutSec)
        {
            return GenerateBatchSuggestionsViaCli(batch, customCliPath, timeoutSec, null, null, null);
        }

        internal static CliBatchSuggestionResult GenerateBatchSuggestionsViaCli(
            List<SessionRenameCandidate> batch,
            string customCliPath,
            int timeoutSec,
            string pathEnvOverride,
            string localAppDataOverride,
            string userProfileOverride)
        {
            CliBatchSuggestionResult res = new CliBatchSuggestionResult();

            if (batch == null || batch.Count == 0)
            {
                res.Ok = false;
                res.Status = CliSuggestStatus.InvalidInput;
                res.ErrorOrHint = "未提供候选会话批次。";
                return res;
            }

            string resolvedPath = ResolveAgyCliPath(customCliPath, pathEnvOverride, localAppDataOverride, userProfileOverride);
            if (string.IsNullOrEmpty(resolvedPath))
            {
                res.Ok = false;
                res.Status = CliSuggestStatus.CliNotFound;
                res.ResolvedCliPath = string.Empty;
                res.ErrorOrHint = "未找到 agy.exe / agy.cmd。请安装 Antigravity CLI 并添加到 PATH，或在界面中配置自定义 CLI 路径。";
                return res;
            }

            res.ResolvedCliPath = resolvedPath;
            int effectiveTimeoutSec = timeoutSec > 0 ? timeoutSec : DefaultTimeoutSeconds;
            int timeoutMs = effectiveTimeoutSec * 1000;

            string prompt = BuildCompactCliPrompt(batch);

            Process proc = null;
            ManualResetEventSlim outEof = new ManualResetEventSlim(false);
            ManualResetEventSlim errEof = new ManualResetEventSlim(false);
            StringBuilder outSb = new StringBuilder();
            StringBuilder errSb = new StringBuilder();
            object ioLock = new object();

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = resolvedPath;
                psi.Arguments = "-p --output-format json";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;

                proc = Process.Start(psi);
                if (proc == null)
                {
                    res.Ok = false;
                    res.Status = CliSuggestStatus.CliError;
                    res.ErrorOrHint = "无法启动 Antigravity CLI 进程: " + resolvedPath;
                    return res;
                }

                proc.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data == null) { outEof.Set(); return; }
                    lock (ioLock)
                    {
                        if (outSb.Length < 4 * 1024 * 1024)
                            outSb.AppendLine(e.Data);
                    }
                };
                proc.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data == null) { errEof.Set(); return; }
                    lock (ioLock)
                    {
                        if (errSb.Length < 64 * 1024)
                            errSb.AppendLine(e.Data);
                    }
                };

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                try
                {
                    byte[] promptBytes = Encoding.UTF8.GetBytes(prompt);
                    proc.StandardInput.BaseStream.Write(promptBytes, 0, promptBytes.Length);
                    proc.StandardInput.BaseStream.Flush();
                    proc.StandardInput.Close();
                }
                catch { }

                if (!proc.WaitForExit(timeoutMs))
                {
                    EnsureProcessTreeTerminated(proc);
                    res.Ok = false;
                    res.Status = CliSuggestStatus.Timeout;
                    res.ErrorOrHint = string.Format(CultureInfo.InvariantCulture, "agy CLI 调用超时（>{0}s），已终止子进程。", effectiveTimeoutSec);
                    lock (ioLock) { res.RawOutput = outSb.ToString().Trim(); }
                    return res;
                }

                try { outEof.Wait(1000); } catch { }
                try { errEof.Wait(500); } catch { }

                string stdoutText;
                string stderrText;
                lock (ioLock)
                {
                    stdoutText = outSb.ToString().Trim();
                    stderrText = errSb.ToString().Trim();
                }
                res.RawOutput = stdoutText;

                int exitCode = 0;
                try { exitCode = proc.ExitCode; } catch { exitCode = -1; }

                if (string.IsNullOrWhiteSpace(stdoutText))
                {
                    res.Ok = false;
                    res.Status = CliSuggestStatus.CliError;
                    res.ErrorOrHint = !string.IsNullOrEmpty(stderrText)
                        ? ("agy CLI 返回错误: " + stderrText)
                        : string.Format(CultureInfo.InvariantCulture, "agy CLI 未输出任何内容（ExitCode={0}）。", exitCode);
                    return res;
                }

                List<CliSessionSuggestion> parsed;
                string parseErr;
                if (!ParseCliSuggestionsOutput(stdoutText, batch, out parsed, out parseErr))
                {
                    res.Ok = false;
                    res.Status = exitCode != 0 ? CliSuggestStatus.CliError : CliSuggestStatus.ParseError;
                    res.ErrorOrHint = !string.IsNullOrEmpty(stderrText) && exitCode != 0
                        ? (stderrText + " | " + parseErr)
                        : parseErr;
                    return res;
                }

                res.Ok = true;
                res.Status = CliSuggestStatus.Success;
                res.Suggestions = parsed;
                return res;
            }
            catch (Exception ex)
            {
                res.Ok = false;
                res.Status = CliSuggestStatus.CliError;
                res.ErrorOrHint = "调用 agy CLI 异常: " + ex.Message;
                return res;
            }
            finally
            {
                if (proc != null)
                {
                    EnsureProcessTreeTerminated(proc);
                    try { proc.Dispose(); } catch { }
                }
                outEof.Dispose();
                errEof.Dispose();
            }
        }

        internal static string BuildCompactCliPrompt(IList<SessionRenameCandidate> batch)
        {
            List<object> rows = new List<object>();
            if (batch != null)
            {
                for (int i = 0; i < batch.Count; i++)
                {
                    SessionRenameCandidate c = batch[i];
                    if (c == null || string.IsNullOrWhiteSpace(c.ConversationId)) continue;

                    string snippet = !string.IsNullOrWhiteSpace(c.CleanedPrompt) ? c.CleanedPrompt : (c.FirstPromptSnippet ?? string.Empty);
                    snippet = Regex.Replace(snippet, "[\\r\\n\\t]+", " ").Trim();
                    if (snippet.Length > 240) snippet = snippet.Substring(0, 240);

                    Dictionary<string, object> row = new Dictionary<string, object>(StringComparer.Ordinal);
                    row["cid"] = c.ConversationId.Trim();
                    row["project"] = c.ProjectName ?? string.Empty;
                    row["prefix"] = c.SuggestedPrefix ?? string.Empty;
                    row["old_title"] = c.OldTitle ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(c.GitBranchHint))
                        row["branch"] = c.GitBranchHint.Trim();
                    row["prompt"] = snippet;
                    rows.Add(row);
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Generate concise, accurate session titles and 1-sentence summaries for the following sessions.");
            sb.AppendLine("Rules:");
            sb.AppendLine("1. Preserve each item's `prefix` (if non-empty) at the start of `title`, followed by a concise topic (no newlines).");
            sb.AppendLine("2. Keep every `cid` unchanged.");
            sb.AppendLine("3. Respond STRICTLY with a JSON array ONLY in the format: [{\"cid\":\"...\",\"title\":\"...\",\"summary\":\"...\"}]");
            sb.AppendLine();
            sb.AppendLine(UnifiedJson.Serialize(rows, false));
            return sb.ToString();
        }

        internal static bool ParseCliSuggestionsOutput(
            string rawOutput,
            IList<SessionRenameCandidate> batch,
            out List<CliSessionSuggestion> suggestions,
            out string error)
        {
            suggestions = new List<CliSessionSuggestion>();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(rawOutput))
            {
                error = "CLI 输出为空。";
                return false;
            }

            List<string> candidateCids = new List<string>();
            if (batch != null)
            {
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < batch.Count; i++)
                {
                    if (batch[i] != null && !string.IsNullOrWhiteSpace(batch[i].ConversationId))
                    {
                        string trimmed = batch[i].ConversationId.Trim();
                        if (seen.Add(trimmed)) candidateCids.Add(trimmed);
                    }
                }
            }

            List<object> itemList = TryExtractArrayFromFlexibleOutput(rawOutput, 0);
            if (itemList == null || itemList.Count == 0)
            {
                string wrapperErr = TryExtractCliWrapperError(rawOutput);
                error = !string.IsNullOrEmpty(wrapperErr)
                    ? ("agy CLI 返回错误: " + wrapperErr)
                    : "未能从 agy CLI 输出中解析出有效的 JSON 建议数组。";
                return false;
            }

            HashSet<string> matchedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < itemList.Count; i++)
            {
                Dictionary<string, object> d = UnifiedJson.AsDict(itemList[i]);
                if (d == null) continue;

                string rawCid = GetFirstNonEmptyString(d, new string[] { "cid", "id", "conversation_id", "conversationId" });
                string rawTitle = GetFirstNonEmptyString(d, new string[] { "title", "new_title", "newTitle", "suggestedTitle" });
                string rawSummary = GetFirstNonEmptyString(d, new string[] { "summary", "reason", "description" });

                if (string.IsNullOrWhiteSpace(rawCid) || string.IsNullOrWhiteSpace(rawTitle))
                    continue;

                string cleanTitle = Regex.Replace(rawTitle, "[\\r\\n]+", " ").Trim();
                if (string.IsNullOrEmpty(cleanTitle)) continue;

                string resolvedCid = ResolveCidAgainstBatch(rawCid.Trim(), candidateCids);
                if (string.IsNullOrEmpty(resolvedCid))
                    continue;

                if (!matchedSet.Add(resolvedCid))
                    continue;

                CliSessionSuggestion item = new CliSessionSuggestion();
                item.Cid = resolvedCid;
                item.Title = cleanTitle;
                item.Summary = rawSummary != null ? Regex.Replace(rawSummary, "[\\r\\n]+", " ").Trim() : string.Empty;
                suggestions.Add(item);
            }

            if (suggestions.Count == 0)
            {
                error = "JSON 数组未包含任何匹配当前候选会话的有效 {cid, title} 建议。";
                return false;
            }

            return true;
        }

        private static List<object> TryExtractArrayFromFlexibleOutput(string text, int depth)
        {
            if (string.IsNullOrWhiteSpace(text) || depth > 4) return null;
            string trimmed = text.Trim();

            // 1. Try direct JSON parse
            object parsed;
            if (UnifiedJson.TryParse(trimmed, out parsed))
            {
                List<object> arr = ExtractArrayFromParsedNode(parsed, depth);
                if (arr != null) return arr;
            }

            // 2. Try ```json ... ``` markdown fence extraction (all matches)
            MatchCollection fences = MarkdownFenceRegex.Matches(trimmed);
            for (int i = 0; i < fences.Count; i++)
            {
                string inner = fences[i].Groups[1].Value;
                List<object> arr = TryExtractArrayFromFlexibleOutput(inner, depth + 1);
                if (arr != null) return arr;
            }

            // 3. Try scanning all balanced [...] array slices and {...} wrapper objects in mixed prose/log/NDJSON output
            List<string> arraySlices = ExtractAllBalancedJsonContainers(trimmed, '[', ']');
            for (int i = 0; i < arraySlices.Count; i++)
            {
                object sliceParsed;
                if (UnifiedJson.TryParse(arraySlices[i], out sliceParsed))
                {
                    List<object> arr = UnifiedJson.AsList(sliceParsed);
                    if (arr != null && arr.Count > 0) return arr;
                }
            }

            List<string> objSlices = ExtractAllBalancedJsonContainers(trimmed, '{', '}');
            for (int i = 0; i < objSlices.Count; i++)
            {
                object objParsed;
                if (UnifiedJson.TryParse(objSlices[i], out objParsed))
                {
                    List<object> arr = ExtractArrayFromParsedNode(objParsed, depth + 1);
                    if (arr != null && arr.Count > 0) return arr;
                }
            }

            return null;
        }

        private static string TryExtractCliWrapperError(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            object parsed;
            if (UnifiedJson.TryParse(text.Trim(), out parsed))
            {
                Dictionary<string, object> d = UnifiedJson.AsDict(parsed);
                if (d != null)
                {
                    bool isErr = UnifiedJson.GetBool(d, "is_error", false);
                    string errText = GetFirstNonEmptyString(d, new string[] { "error", "errorMessage", "message" });
                    if (!string.IsNullOrEmpty(errText)) return errText;
                    if (isErr)
                    {
                        string resText = GetFirstNonEmptyString(d, new string[] { "result", "output", "text" });
                        if (!string.IsNullOrEmpty(resText)) return resText;
                    }
                }
            }
            return null;
        }

        private static List<object> ExtractArrayFromParsedNode(object parsed, int depth)
        {
            if (parsed == null || depth > 4) return null;

            List<object> directList = UnifiedJson.AsList(parsed);
            if (directList != null) return directList;

            string asStr = parsed as string;
            if (!string.IsNullOrWhiteSpace(asStr))
            {
                return TryExtractArrayFromFlexibleOutput(asStr, depth + 1);
            }

            Dictionary<string, object> dict = UnifiedJson.AsDict(parsed);
            if (dict == null) return null;

            // Check direct array containers first ("suggestions", "items", "results", "data")
            for (int i = 0; i < ArrayContainerKeys.Length; i++)
            {
                object val;
                if (TryGetIgnoreCase(dict, ArrayContainerKeys[i], out val) && val != null)
                {
                    List<object> innerList = UnifiedJson.AsList(val);
                    if (innerList != null) return innerList;
                }
            }

            // Check string/object wrapper fields ("result", "response", "content", "output", "text", "message", "data")
            for (int i = 0; i < WrapperKeys.Length; i++)
            {
                object val;
                if (TryGetIgnoreCase(dict, WrapperKeys[i], out val) && val != null)
                {
                    List<object> nested = ExtractArrayFromParsedNode(val, depth + 1);
                    if (nested != null) return nested;
                }
            }

            return null;
        }

        private static List<string> ExtractAllBalancedJsonContainers(string s, char openCh, char closeCh)
        {
            List<string> results = new List<string>();
            if (string.IsNullOrEmpty(s)) return results;
            int start = s.IndexOf(openCh);
            while (start >= 0)
            {
                int depth = 0;
                bool inString = false;
                bool escape = false;
                int matchedEnd = -1;
                for (int i = start; i < s.Length; i++)
                {
                    char c = s[i];
                    if (inString)
                    {
                        if (escape) { escape = false; continue; }
                        if (c == '\\') { escape = true; continue; }
                        if (c == '"') { inString = false; continue; }
                    }
                    else
                    {
                        if (c == '"') { inString = true; continue; }
                        if (c == openCh) depth++;
                        else if (c == closeCh)
                        {
                            depth--;
                            if (depth == 0)
                            {
                                string candidate = s.Substring(start, i - start + 1);
                                object dummy;
                                if (UnifiedJson.TryParse(candidate, out dummy))
                                {
                                    results.Add(candidate);
                                    matchedEnd = i;
                                }
                                break;
                            }
                        }
                    }
                }
                int nextSearch = matchedEnd >= 0 ? matchedEnd + 1 : start + 1;
                start = nextSearch < s.Length ? s.IndexOf(openCh, nextSearch) : -1;
            }
            return results;
        }

        private static string ResolveCidAgainstBatch(string rawCid, List<string> candidateCids)
        {
            if (candidateCids == null || candidateCids.Count == 0)
                return SessionMetadataExtractor.IsLikelyConversationId(rawCid) ? rawCid : null;

            for (int i = 0; i < candidateCids.Count; i++)
            {
                if (string.Equals(candidateCids[i], rawCid, StringComparison.OrdinalIgnoreCase))
                    return candidateCids[i];
            }

            int hexCount = 0;
            for (int i = 0; i < rawCid.Length; i++)
            {
                char c = rawCid[i];
                bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (isHex) hexCount++;
                else if (c != '-') return null;
            }
            if (hexCount < 8) return null;

            List<string> matches = new List<string>();
            for (int i = 0; i < candidateCids.Count; i++)
            {
                if (candidateCids[i].StartsWith(rawCid, StringComparison.OrdinalIgnoreCase))
                    matches.Add(candidateCids[i]);
            }
            return matches.Count == 1 ? matches[0] : null;
        }

        private static string GetFirstNonEmptyString(Dictionary<string, object> dict, string[] keys)
        {
            if (dict == null || keys == null) return null;
            for (int i = 0; i < keys.Length; i++)
            {
                object val;
                if (TryGetIgnoreCase(dict, keys[i], out val) && val is string)
                {
                    string s = (string)val;
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }
            }
            return null;
        }

        private static bool TryGetIgnoreCase(Dictionary<string, object> dict, string key, out object val)
        {
            val = null;
            if (dict == null || string.IsNullOrEmpty(key)) return false;
            if (dict.TryGetValue(key, out val)) return true;
            foreach (KeyValuePair<string, object> kvp in dict)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    val = kvp.Value;
                    return true;
                }
            }
            return false;
        }

        private static void EnsureProcessTreeTerminated(Process proc)
        {
            if (proc == null) return;
            try
            {
                if (proc.HasExited) return;
                int pid = proc.Id;
                try
                {
                    ProcessStartInfo tk = new ProcessStartInfo();
                    tk.FileName = "taskkill";
                    tk.Arguments = "/F /T /PID " + pid.ToString(CultureInfo.InvariantCulture);
                    tk.UseShellExecute = false;
                    tk.CreateNoWindow = true;
                    tk.RedirectStandardOutput = true;
                    tk.RedirectStandardError = true;
                    using (Process tkProc = Process.Start(tk))
                    {
                        if (tkProc != null) tkProc.WaitForExit(1500);
                    }
                }
                catch { }

                if (!proc.HasExited)
                {
                    try { proc.Kill(); } catch { }
                }
                try { proc.WaitForExit(1000); } catch { }
            }
            catch { }
        }
    }
}
