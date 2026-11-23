using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // TRANSCRIPT & BRAIN ARTIFACTS MARKDOWN EXPORTER (Milestone 2 — F4)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 会话明文对话流 (transcript_full.jsonl / transcript.jsonl) 与脑区产出文档 (*.md) 的流式 Markdown 导出器
    /// 兼容 C# 5.0 (.NET Framework 4.0+ csc.exe)
    /// </summary>
    public static class TranscriptMarkdownExporter
    {
        private const int MaxInMemoryReturnChars = 4 * 1024 * 1024;
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private struct TranscriptLineFields
        {
            public string Type;
            public string CreatedAt;
            public string Content;
            public string Thinking;
            public int StepIndex;
            public bool HasStepIndex;
        }

        private static bool IsPathContainedUnder(string baseDir, string targetPath)
        {
            if (string.IsNullOrEmpty(baseDir) || string.IsNullOrEmpty(targetPath))
                return false;

            string fullBase = Path.GetFullPath(baseDir)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullTarget = Path.GetFullPath(targetPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return fullTarget.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveBrainDirectory(string geminiPath, string cid)
        {
            if (string.IsNullOrEmpty(geminiPath) || string.IsNullOrEmpty(cid))
                return string.Empty;

            if (!SessionMetadataExtractor.IsLikelyConversationId(cid))
                throw new ArgumentException("非法的会话 ID（包含不允许的路径字符或保留名称）: " + cid, "session");

            string trimmedGemini = geminiPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            bool hasExplicitAppDir = trimmedGemini.EndsWith("antigravity", StringComparison.OrdinalIgnoreCase) ||
                                     trimmedGemini.EndsWith("antigravity-cli", StringComparison.OrdinalIgnoreCase);
            string brainRoot = hasExplicitAppDir
                ? Path.Combine(trimmedGemini, "brain")
                : Path.Combine(trimmedGemini, "antigravity", "brain");

            string candidate = Path.Combine(brainRoot, cid);
            if (!IsPathContainedUnder(brainRoot, candidate))
                throw new InvalidOperationException("拒绝访问越界的脑区目录: " + candidate);

            if (!Directory.Exists(candidate) && !hasExplicitAppDir)
            {
                string cliBrainRoot = Path.Combine(trimmedGemini, "antigravity-cli", "brain");
                string cliCandidate = Path.Combine(cliBrainRoot, cid);
                if (!IsPathContainedUnder(cliBrainRoot, cliCandidate))
                    throw new InvalidOperationException("拒绝访问越界的 CLI 脑区目录: " + cliCandidate);
                if (Directory.Exists(cliCandidate))
                    return cliCandidate;
            }

            return candidate;
        }

        private static string SelectPreferredTranscriptPath(string brainDir)
        {
            if (string.IsNullOrEmpty(brainDir) || !Directory.Exists(brainDir))
                return string.Empty;

            string logsDir = Path.Combine(brainDir, ".system_generated", "logs");
            string fullPath = Path.Combine(logsDir, "transcript_full.jsonl");
            string compactPath = Path.Combine(logsDir, "transcript.jsonl");

            try
            {
                if (File.Exists(fullPath) && new FileInfo(fullPath).Length > 0)
                    return fullPath;
            }
            catch
            {
                if (File.Exists(fullPath))
                    return fullPath;
            }

            if (File.Exists(compactPath))
                return compactPath;

            if (File.Exists(fullPath))
                return fullPath;

            return string.Empty;
        }

        private static bool TryMatchZipSubPath(string normalizedEntryName, string expectedSubPath, out string tail)
        {
            tail = string.Empty;
            if (string.IsNullOrEmpty(normalizedEntryName) || string.IsNullOrEmpty(expectedSubPath))
                return false;

            string clean = normalizedEntryName.TrimStart('/');
            if (clean.StartsWith(expectedSubPath, StringComparison.OrdinalIgnoreCase))
            {
                tail = clean.Substring(expectedSubPath.Length);
                return true;
            }

            string slashPrefixed = "/" + expectedSubPath;
            int idx = clean.IndexOf(slashPrefixed, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                tail = clean.Substring(idx + slashPrefixed.Length);
                return true;
            }

            return false;
        }

        private static void WriteAtomicMarkdownFile(string outputFilePath, ConversationSummaryInfo session, TextReader transcriptReader, Dictionary<string, string> artifacts, StringBuilder optionalMirror)
        {
            string dir = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string tempPath = outputFilePath + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
            try
            {
                using (FileStream outFs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (StreamWriter sw = new StreamWriter(outFs, Utf8NoBom))
                {
                    FormatSessionMarkdownStream(session, transcriptReader, artifacts, sw, optionalMirror);
                    sw.Flush();
                }

                if (File.Exists(outputFilePath))
                {
                    FileAttributes attrs = File.GetAttributes(outputFilePath);
                    if ((attrs & FileAttributes.ReadOnly) != 0)
                    {
                        File.SetAttributes(outputFilePath, attrs & ~FileAttributes.ReadOnly);
                    }
                    File.Replace(tempPath, outputFilePath, null, true);
                }
                else
                {
                    File.Move(tempPath, outputFilePath);
                }
            }
            catch
            {
                try
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch
                {
                }
                throw;
            }
        }

        private sealed class BoundedStringWriter : TextWriter
        {
            private readonly StringBuilder _sb = new StringBuilder();
            private readonly int _maxChars;
            public bool Truncated { get; private set; }

            public override Encoding Encoding
            {
                get { return Encoding.UTF8; }
            }

            public BoundedStringWriter(int maxChars)
            {
                _maxChars = Math.Max(1024, maxChars);
            }

            public override void Write(char value)
            {
                if (_sb.Length >= _maxChars) { Truncated = true; return; }
                _sb.Append(value);
            }

            public override void Write(string value)
            {
                if (string.IsNullOrEmpty(value)) return;
                if (_sb.Length >= _maxChars) { Truncated = true; return; }
                int remain = _maxChars - _sb.Length;
                if (value.Length <= remain)
                {
                    _sb.Append(value);
                }
                else
                {
                    _sb.Append(value, 0, remain);
                    Truncated = true;
                }
            }

            public override string ToString()
            {
                return _sb.ToString();
            }
        }

        private static string RenderStreamToOutputOrPreview(
            string outputFilePath,
            ConversationSummaryInfo session,
            TextReader transcriptReader,
            Dictionary<string, string> artifacts,
            int maxPreviewChars,
            out bool isTruncated)
        {
            isTruncated = false;
            if (!string.IsNullOrEmpty(outputFilePath))
            {
                StringBuilder mirror = new StringBuilder();
                WriteAtomicMarkdownFile(outputFilePath, session, transcriptReader, artifacts, mirror);
                return mirror.ToString();
            }

            if (maxPreviewChars > 0)
            {
                using (BoundedStringWriter bsw = new BoundedStringWriter(maxPreviewChars))
                {
                    FormatSessionMarkdownStream(session, transcriptReader, artifacts, bsw, null);
                    isTruncated = bsw.Truncated;
                    return bsw.ToString();
                }
            }

            StringBuilder sb = new StringBuilder();
            using (StringWriter sw = new StringWriter(sb))
            {
                FormatSessionMarkdownStream(session, transcriptReader, artifacts, sw, null);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 预览本地会话为有界 Markdown 文本（在达到 maxPreviewChars 后提前终止读取，避免大文件 OOM）
        /// </summary>
        public static string PreviewLocalSessionMarkdown(ConversationSummaryInfo session, string geminiPath, int maxPreviewChars, out bool isTruncated)
        {
            return ExportOrPreviewLocalSession(session, geminiPath, null, maxPreviewChars, out isTruncated);
        }

        /// <summary>
        /// 从备份压缩包（.agy / .zip）预览会话为有界 Markdown 文本
        /// </summary>
        public static string PreviewBackupSessionMarkdown(ConversationSummaryInfo session, string zipFilePath, int maxPreviewChars, out bool isTruncated)
        {
            return ExportOrPreviewBackupSession(session, zipFilePath, null, maxPreviewChars, out isTruncated);
        }

        /// <summary>
        /// 将本地会话导出为 Markdown 文档（流式读取 transcript_full.jsonl / transcript.jsonl 与产出文档）
        /// </summary>
        public static string ExportLocalSessionToMarkdown(ConversationSummaryInfo session, string geminiPath, string outputFilePath)
        {
            bool dummy;
            return ExportOrPreviewLocalSession(session, geminiPath, outputFilePath, 0, out dummy);
        }

        private static string ExportOrPreviewLocalSession(
            ConversationSummaryInfo session,
            string geminiPath,
            string outputFilePath,
            int maxPreviewChars,
            out bool isTruncated)
        {
            if (session == null)
                throw new ArgumentNullException("session");

            string cid = !string.IsNullOrEmpty(session.ConversationId) ? session.ConversationId.Trim() : string.Empty;
            string brainDir = ResolveBrainDirectory(geminiPath, cid);

            string transcriptPath = SelectPreferredTranscriptPath(brainDir);
            Dictionary<string, string> artifacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(brainDir) && Directory.Exists(brainDir))
            {
                try
                {
                    string[] mdFiles = Directory.GetFiles(brainDir, "*.md", SearchOption.TopDirectoryOnly);
                    for (int i = 0; i < mdFiles.Length; i++)
                    {
                        string file = mdFiles[i];
                        if (!IsPathContainedUnder(brainDir, file))
                            continue;

                        string fileName = Path.GetFileName(file);
                        try
                        {
                            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                            using (StreamReader reader = new StreamReader(fs, Encoding.UTF8))
                            {
                                artifacts[fileName] = reader.ReadToEnd();
                            }
                        }
                        catch
                        {
                            // 忽略单个文件并发只读异常
                        }
                    }
                }
                catch
                {
                    // 忽略目录枚举异常
                }
            }

            FileStream transcriptFs = null;
            StreamReader transcriptReader = null;
            try
            {
                if (!string.IsNullOrEmpty(transcriptPath) && File.Exists(transcriptPath))
                {
                    try
                    {
                        transcriptFs = new FileStream(transcriptPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        transcriptReader = new StreamReader(transcriptFs, Encoding.UTF8);
                    }
                    catch
                    {
                        if (transcriptReader != null) { transcriptReader.Dispose(); transcriptReader = null; }
                        if (transcriptFs != null) { transcriptFs.Dispose(); transcriptFs = null; }
                    }
                }

                return RenderStreamToOutputOrPreview(outputFilePath, session, transcriptReader, artifacts, maxPreviewChars, out isTruncated);
            }
            finally
            {
                if (transcriptReader != null) transcriptReader.Dispose();
                if (transcriptFs != null) transcriptFs.Dispose();
            }
        }

        /// <summary>
        /// 从备份压缩包（.agy / .zip）中流式解压并导出指定会话为 Markdown 文档
        /// </summary>
        public static string ExportBackupSessionToMarkdown(ConversationSummaryInfo session, string zipFilePath, string outputFilePath)
        {
            bool dummy;
            return ExportOrPreviewBackupSession(session, zipFilePath, outputFilePath, 0, out dummy);
        }

        private static string ExportOrPreviewBackupSession(
            ConversationSummaryInfo session,
            string zipFilePath,
            string outputFilePath,
            int maxPreviewChars,
            out bool isTruncated)
        {
            if (session == null)
                throw new ArgumentNullException("session");
            if (string.IsNullOrEmpty(zipFilePath) || !File.Exists(zipFilePath))
                throw new FileNotFoundException("备份归档文件不存在: " + zipFilePath);

            string cid = !string.IsNullOrEmpty(session.ConversationId) ? session.ConversationId.Trim() : string.Empty;
            if (!string.IsNullOrEmpty(cid) && !SessionMetadataExtractor.IsLikelyConversationId(cid))
                throw new ArgumentException("非法的会话 ID（包含不允许的路径字符或保留名称）: " + cid, "session");

            string expectedFullSuffix = "antigravity/brain/" + cid + "/.system_generated/logs/transcript_full.jsonl";
            string expectedCompactSuffix = "antigravity/brain/" + cid + "/.system_generated/logs/transcript.jsonl";
            string brainFolderPrefix = "antigravity/brain/" + cid + "/";

            Dictionary<string, string> artifacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using (FileStream fs = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                ZipArchiveEntry fullEntry = null;
                ZipArchiveEntry compactEntry = null;

                if (!string.IsNullOrEmpty(cid))
                {
                    for (int i = 0; i < archive.Entries.Count; i++)
                    {
                        ZipArchiveEntry entry = archive.Entries[i];
                        string normName = entry.FullName.Replace('\\', '/');
                        string tail;

                        if (TryMatchZipSubPath(normName, expectedFullSuffix, out tail) && tail.Length == 0)
                        {
                            fullEntry = entry;
                        }
                        else if (TryMatchZipSubPath(normName, expectedCompactSuffix, out tail) && tail.Length == 0)
                        {
                            compactEntry = entry;
                        }
                        else if (TryMatchZipSubPath(normName, brainFolderPrefix, out tail))
                        {
                            if (!string.IsNullOrEmpty(tail) &&
                                !tail.Contains("/") &&
                                !tail.Contains("..") &&
                                tail.EndsWith(".md", StringComparison.OrdinalIgnoreCase) &&
                                tail.Length > 3)
                            {
                                using (Stream stream = entry.Open())
                                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                                {
                                    artifacts[tail] = reader.ReadToEnd();
                                }
                            }
                        }
                    }
                }

                ZipArchiveEntry targetEntry = null;
                if (fullEntry != null && (fullEntry.Length > 0 || compactEntry == null))
                    targetEntry = fullEntry;
                else
                    targetEntry = compactEntry;

                Stream entryStream = null;
                StreamReader entryReader = null;
                try
                {
                    if (targetEntry != null)
                    {
                        entryStream = targetEntry.Open();
                        entryReader = new StreamReader(entryStream, Encoding.UTF8);
                    }

                    return RenderStreamToOutputOrPreview(outputFilePath, session, entryReader, artifacts, maxPreviewChars, out isTruncated);
                }
                finally
                {
                    if (entryReader != null) entryReader.Dispose();
                    if (entryStream != null) entryStream.Dispose();
                }
            }
        }

        /// <summary>
        /// 格式化会话为结构化 Markdown 文档（保持向后兼容签名）
        /// </summary>
        public static string FormatSessionMarkdown(ConversationSummaryInfo session, string transcriptJsonlContent, Dictionary<string, string> artifactMdFiles)
        {
            StringBuilder sb = new StringBuilder();
            using (StringReader sr = new StringReader(transcriptJsonlContent ?? string.Empty))
            using (StringWriter sw = new StringWriter(sb))
            {
                FormatSessionMarkdownStream(session, sr, artifactMdFiles, sw, null);
            }
            return sb.ToString();
        }

        private static void WriteLineWithMirror(TextWriter writer, StringBuilder mirror, string text)
        {
            if (text != null)
                writer.WriteLine(text);
            else
                writer.WriteLine();

            if (mirror != null && mirror.Length < MaxInMemoryReturnChars)
            {
                if (text != null) mirror.AppendLine(text);
                else mirror.AppendLine();
            }
        }

        private static void FormatSessionMarkdownStream(
            ConversationSummaryInfo session,
            TextReader transcriptReader,
            Dictionary<string, string> artifactMdFiles,
            TextWriter writer,
            StringBuilder mirror)
        {
            // 1. 文档标题
            string displayTitle = session != null && !string.IsNullOrEmpty(session.DisplayTitle)
                ? session.DisplayTitle
                : "未命名会话";
            WriteLineWithMirror(writer, mirror, "# " + displayTitle);
            WriteLineWithMirror(writer, mirror, null);

            // 2. 元数据列表
            WriteLineWithMirror(writer, mirror, string.Format("- **会话 UUID**: {0}", session != null && !string.IsNullOrEmpty(session.ConversationId) ? session.ConversationId : "-"));
            WriteLineWithMirror(writer, mirror, string.Format("- **所属工程**: {0}", session != null && !string.IsNullOrEmpty(session.ProjectName) ? session.ProjectName : "-"));
            WriteLineWithMirror(writer, mirror, string.Format("- **工程路径**: {0}", session != null && !string.IsNullOrEmpty(session.FolderUri) ? session.FolderUri : "-"));
            WriteLineWithMirror(writer, mirror, string.Format("- **更新时间**: {0}", session != null && session.UpdatedTime > DateTime.MinValue ? session.UpdatedTime.ToString("yyyy-MM-dd HH:mm:ss") : "-"));
            WriteLineWithMirror(writer, mirror, string.Format("- **总步数 / 用户交互数**: {0} / {1}", session != null ? session.StepCount : 0, session != null ? session.UserInteractions : 0));
            WriteLineWithMirror(writer, mirror, null);

            // 3. 对话记录（严格流式逐行读取）
            WriteLineWithMirror(writer, mirror, "# 💬 对话记录 (Conversation Transcript)");
            WriteLineWithMirror(writer, mirror, null);

            int dialogueCount = 0;
            if (transcriptReader != null)
            {
                BoundedStringWriter boundedWriter = writer as BoundedStringWriter;
                string rawLine;
                while ((rawLine = transcriptReader.ReadLine()) != null)
                {
                    if (boundedWriter != null && boundedWriter.Truncated)
                    {
                        break;
                    }
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    TranscriptLineFields parsed = ParseTranscriptJsonLine(line);
                    string type = parsed.Type;
                    string createdAt = parsed.CreatedAt;
                    string content = parsed.Content;
                    string thinking = parsed.Thinking;
                    int stepIdx = parsed.HasStepIndex ? parsed.StepIndex : -1;

                    if (string.Equals(type, "USER_INPUT", StringComparison.OrdinalIgnoreCase))
                    {
                        string cleaned = CleanUserInputContent(content);
                        if (!string.IsNullOrEmpty(cleaned))
                        {
                            dialogueCount++;
                            string timeSuffix = !string.IsNullOrEmpty(createdAt) ? " - " + createdAt : string.Empty;
                            if (stepIdx >= 0)
                                WriteLineWithMirror(writer, mirror, string.Format("## 👤 用户提问 (Step #{0}){1}", stepIdx, timeSuffix));
                            else
                                WriteLineWithMirror(writer, mirror, !string.IsNullOrEmpty(createdAt) ? string.Format("## 👤 用户提问 - {0}", createdAt) : "## 👤 用户提问");

                            WriteLineWithMirror(writer, mirror, null);
                            WriteLineWithMirror(writer, mirror, cleaned);
                            WriteLineWithMirror(writer, mirror, null);
                        }
                    }
                    else if (string.Equals(type, "PLANNER_RESPONSE", StringComparison.OrdinalIgnoreCase))
                    {
                        thinking = thinking != null ? thinking.Trim() : string.Empty;
                        content = content != null ? content.Trim() : string.Empty;

                        if (!string.IsNullOrEmpty(thinking) || !string.IsNullOrEmpty(content))
                        {
                            dialogueCount++;
                            string timeSuffix = !string.IsNullOrEmpty(createdAt) ? " - " + createdAt : string.Empty;
                            if (stepIdx >= 0)
                                WriteLineWithMirror(writer, mirror, string.Format("## 🤖 AI 回复 (Step #{0}){1}", stepIdx, timeSuffix));
                            else
                                WriteLineWithMirror(writer, mirror, !string.IsNullOrEmpty(createdAt) ? string.Format("## 🤖 AI 回复 - {0}", createdAt) : "## 🤖 AI 回复");

                            WriteLineWithMirror(writer, mirror, null);

                            if (!string.IsNullOrEmpty(thinking))
                            {
                                WriteLineWithMirror(writer, mirror, "<details><summary>💭 思考过程 (Thinking)</summary>");
                                WriteLineWithMirror(writer, mirror, null);
                                WriteLineWithMirror(writer, mirror, EscapeDetailsTags(thinking));
                                WriteLineWithMirror(writer, mirror, null);
                                WriteLineWithMirror(writer, mirror, "</details>");
                                WriteLineWithMirror(writer, mirror, null);
                            }

                            if (!string.IsNullOrEmpty(content))
                            {
                                WriteLineWithMirror(writer, mirror, content);
                                WriteLineWithMirror(writer, mirror, null);
                            }
                        }
                    }
                }
            }

            if (dialogueCount == 0)
            {
                WriteLineWithMirror(writer, mirror, "> *(未在该会话的脑区目录中检测到 transcript_full.jsonl 或 transcript.jsonl 明文对话日志)*");
                WriteLineWithMirror(writer, mirror, null);
            }

            // 4. 会话产出文档 (Brain Artifacts)
            if (artifactMdFiles != null && artifactMdFiles.Count > 0)
            {
                WriteLineWithMirror(writer, mirror, "# 📎 会话产出文档 (Brain Artifacts)");
                WriteLineWithMirror(writer, mirror, null);

                List<string> keys = new List<string>(artifactMdFiles.Keys);
                keys.Sort(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < keys.Count; i++)
                {
                    string fileName = keys[i];
                    string mdContent = artifactMdFiles[fileName];

                    WriteLineWithMirror(writer, mirror, "## 📄 " + fileName);
                    WriteLineWithMirror(writer, mirror, null);
                    if (!string.IsNullOrEmpty(mdContent))
                    {
                        WriteLineWithMirror(writer, mirror, mdContent.Trim());
                    }
                    WriteLineWithMirror(writer, mirror, null);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 单遍线性 JSON 词法扫描器 (仅提取 depth == 1 顶层字段，杜绝 nested 误命中与 Regex O(L^2))
        // ══════════════════════════════════════════════════════════════════

        private static TranscriptLineFields ParseTranscriptJsonLine(string jsonLine)
        {
            TranscriptLineFields res = new TranscriptLineFields();
            if (string.IsNullOrEmpty(jsonLine)) return res;

            int pos = 0;
            int len = jsonLine.Length;
            SkipWs(jsonLine, ref pos, len);
            if (pos >= len || jsonLine[pos] != '{') return res;
            pos++;

            while (pos < len)
            {
                SkipWs(jsonLine, ref pos, len);
                if (pos >= len) break;
                char ch = jsonLine[pos];
                if (ch == '}') break;
                if (ch == ',')
                {
                    pos++;
                    continue;
                }
                if (ch != '"') break;

                string key;
                if (!ReadJsonStringToken(jsonLine, ref pos, len, out key)) break;

                SkipWs(jsonLine, ref pos, len);
                if (pos >= len || jsonLine[pos] != ':') break;
                pos++;
                SkipWs(jsonLine, ref pos, len);
                if (pos >= len) break;

                bool isTargetStr =
                    string.Equals(key, "type", StringComparison.Ordinal) ||
                    string.Equals(key, "created_at", StringComparison.Ordinal) ||
                    string.Equals(key, "content", StringComparison.Ordinal) ||
                    string.Equals(key, "thinking", StringComparison.Ordinal);

                bool isStepIdx = string.Equals(key, "step_index", StringComparison.Ordinal);

                char valChar = jsonLine[pos];
                if (isTargetStr && valChar == '"')
                {
                    string val;
                    if (!ReadJsonStringToken(jsonLine, ref pos, len, out val)) break;
                    if (string.Equals(key, "type", StringComparison.Ordinal) && res.Type == null) res.Type = val;
                    else if (string.Equals(key, "created_at", StringComparison.Ordinal) && res.CreatedAt == null) res.CreatedAt = val;
                    else if (string.Equals(key, "content", StringComparison.Ordinal) && res.Content == null) res.Content = val;
                    else if (string.Equals(key, "thinking", StringComparison.Ordinal) && res.Thinking == null) res.Thinking = val;
                }
                else if (isStepIdx && (valChar == '-' || (valChar >= '0' && valChar <= '9')))
                {
                    int numStart = pos;
                    if (jsonLine[pos] == '-') pos++;
                    while (pos < len && jsonLine[pos] >= '0' && jsonLine[pos] <= '9') pos++;
                    int parsed;
                    if (!res.HasStepIndex && int.TryParse(jsonLine.Substring(numStart, pos - numStart), out parsed))
                    {
                        res.StepIndex = parsed;
                        res.HasStepIndex = true;
                    }
                    // 若存在小数或指数后缀，继续跳过完整数字字面量
                    while (pos < len && (jsonLine[pos] == '.' || jsonLine[pos] == 'e' || jsonLine[pos] == 'E' || jsonLine[pos] == '+' || jsonLine[pos] == '-' || (jsonLine[pos] >= '0' && jsonLine[pos] <= '9')))
                    {
                        pos++;
                    }
                }
                else
                {
                    if (!SkipJsonValue(jsonLine, ref pos, len)) break;
                }
            }

            return res;
        }

        private static void SkipWs(string s, ref int pos, int len)
        {
            while (pos < len)
            {
                char c = s[pos];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') pos++;
                else break;
            }
        }

        private static bool ReadJsonStringToken(string s, ref int pos, int len, out string decoded)
        {
            decoded = string.Empty;
            if (pos >= len || s[pos] != '"') return false;
            pos++;

            StringBuilder sb = null;
            int literalStart = pos;

            while (pos < len)
            {
                char c = s[pos];
                if (c == '"')
                {
                    if (sb == null)
                    {
                        decoded = s.Substring(literalStart, pos - literalStart);
                    }
                    else
                    {
                        if (pos > literalStart) sb.Append(s, literalStart, pos - literalStart);
                        decoded = sb.ToString();
                    }
                    pos++;
                    return true;
                }
                if (c == '\\')
                {
                    if (sb == null) sb = new StringBuilder();
                    if (pos > literalStart) sb.Append(s, literalStart, pos - literalStart);

                    pos++;
                    if (pos >= len)
                    {
                        decoded = sb.ToString();
                        return false;
                    }

                    char esc = s[pos];
                    if (esc == '"') sb.Append('"');
                    else if (esc == '\\') sb.Append('\\');
                    else if (esc == '/') sb.Append('/');
                    else if (esc == 'n') sb.Append('\n');
                    else if (esc == 'r') sb.Append('\r');
                    else if (esc == 't') sb.Append('\t');
                    else if (esc == 'b') sb.Append('\b');
                    else if (esc == 'f') sb.Append('\f');
                    else if (esc == 'u')
                    {
                        if (pos + 4 < len)
                        {
                            string hex = s.Substring(pos + 1, 4);
                            int code;
                            if (TryParseHex4(hex, out code))
                            {
                                sb.Append((char)code);
                                pos += 4;
                            }
                            else
                            {
                                sb.Append("\\u").Append(hex);
                                pos += 4;
                            }
                        }
                        else
                        {
                            sb.Append("\\u");
                        }
                    }
                    else
                    {
                        sb.Append('\\').Append(esc);
                    }

                    pos++;
                    literalStart = pos;
                }
                else
                {
                    pos++;
                }
            }

            if (sb == null) decoded = s.Substring(literalStart, len - literalStart);
            else
            {
                if (len > literalStart) sb.Append(s, literalStart, len - literalStart);
                decoded = sb.ToString();
            }
            return false;
        }

        private static bool TryParseHex4(string hex, out int code)
        {
            code = 0;
            if (hex == null || hex.Length != 4) return false;
            for (int i = 0; i < 4; i++)
            {
                char c = hex[i];
                int v;
                if (c >= '0' && c <= '9') v = c - '0';
                else if (c >= 'a' && c <= 'f') v = 10 + (c - 'a');
                else if (c >= 'A' && c <= 'F') v = 10 + (c - 'A');
                else return false;
                code = (code << 4) | v;
            }
            return true;
        }

        private static bool SkipJsonValue(string s, ref int pos, int len)
        {
            SkipWs(s, ref pos, len);
            if (pos >= len) return false;

            char first = s[pos];
            if (first == '"')
            {
                return SkipJsonStringRaw(s, ref pos, len);
            }
            if (first == '{' || first == '[')
            {
                int depth = 0;
                while (pos < len)
                {
                    char c = s[pos];
                    if (c == '"')
                    {
                        if (!SkipJsonStringRaw(s, ref pos, len)) return false;
                        continue;
                    }
                    if (c == '{' || c == '[') depth++;
                    else if (c == '}' || c == ']')
                    {
                        depth--;
                        if (depth <= 0)
                        {
                            pos++;
                            return true;
                        }
                    }
                    pos++;
                }
                return false;
            }

            while (pos < len)
            {
                char c = s[pos];
                if (c == ',' || c == '}' || c == ']' || c == ' ' || c == '\t' || c == '\r' || c == '\n')
                    return true;
                pos++;
            }
            return true;
        }

        private static bool SkipJsonStringRaw(string s, ref int pos, int len)
        {
            if (pos >= len || s[pos] != '"') return false;
            pos++;
            while (pos < len)
            {
                char c = s[pos];
                if (c == '\\')
                {
                    pos += 2;
                    continue;
                }
                if (c == '"')
                {
                    pos++;
                    return true;
                }
                pos++;
            }
            return false;
        }

        /// <summary>
        /// 从单行顶层 JSON 对象中精准提取指定字符串字段值并完成反转义（仅扫描 depth == 1 顶层键）
        /// </summary>
        public static string ExtractJsonStringField(string jsonLine, string fieldName)
        {
            if (string.IsNullOrEmpty(jsonLine) || string.IsNullOrEmpty(fieldName))
                return string.Empty;

            int pos = 0;
            int len = jsonLine.Length;
            SkipWs(jsonLine, ref pos, len);
            if (pos >= len || jsonLine[pos] != '{') return string.Empty;
            pos++;

            while (pos < len)
            {
                SkipWs(jsonLine, ref pos, len);
                if (pos >= len || jsonLine[pos] == '}') break;
                if (jsonLine[pos] == ',') { pos++; continue; }
                if (jsonLine[pos] != '"') break;

                string key;
                if (!ReadJsonStringToken(jsonLine, ref pos, len, out key)) break;

                SkipWs(jsonLine, ref pos, len);
                if (pos >= len || jsonLine[pos] != ':') break;
                pos++;
                SkipWs(jsonLine, ref pos, len);
                if (pos >= len) break;

                if (string.Equals(key, fieldName, StringComparison.Ordinal))
                {
                    if (jsonLine[pos] == '"')
                    {
                        string val;
                        ReadJsonStringToken(jsonLine, ref pos, len, out val);
                        return val;
                    }
                    return string.Empty;
                }

                if (!SkipJsonValue(jsonLine, ref pos, len)) break;
            }

            return string.Empty;
        }

        /// <summary>
        /// 从单行顶层 JSON 对象中精准提取指定整数字段值（仅扫描 depth == 1 顶层键）
        /// </summary>
        public static int ExtractJsonIntField(string jsonLine, string fieldName, int defaultValue)
        {
            if (string.IsNullOrEmpty(jsonLine) || string.IsNullOrEmpty(fieldName))
                return defaultValue;

            int pos = 0;
            int len = jsonLine.Length;
            SkipWs(jsonLine, ref pos, len);
            if (pos >= len || jsonLine[pos] != '{') return defaultValue;
            pos++;

            while (pos < len)
            {
                SkipWs(jsonLine, ref pos, len);
                if (pos >= len || jsonLine[pos] == '}') break;
                if (jsonLine[pos] == ',') { pos++; continue; }
                if (jsonLine[pos] != '"') break;

                string key;
                if (!ReadJsonStringToken(jsonLine, ref pos, len, out key)) break;

                SkipWs(jsonLine, ref pos, len);
                if (pos >= len || jsonLine[pos] != ':') break;
                pos++;
                SkipWs(jsonLine, ref pos, len);
                if (pos >= len) break;

                if (string.Equals(key, fieldName, StringComparison.Ordinal))
                {
                    int start = pos;
                    if (pos < len && jsonLine[pos] == '-') pos++;
                    int digitStart = pos;
                    while (pos < len && jsonLine[pos] >= '0' && jsonLine[pos] <= '9') pos++;
                    if (pos > digitStart)
                    {
                        int parsed;
                        if (int.TryParse(jsonLine.Substring(start, pos - start), out parsed))
                            return parsed;
                    }
                    return defaultValue;
                }

                if (!SkipJsonValue(jsonLine, ref pos, len)) break;
            }

            return defaultValue;
        }

        private static string EscapeDetailsTags(string thinking)
        {
            if (string.IsNullOrEmpty(thinking)) return string.Empty;
            return Regex.Replace(
                thinking,
                @"<(/?)\s*details\b([^>]*)>",
                delegate(Match m)
                {
                    return "&lt;" + m.Groups[1].Value + "details" + m.Groups[2].Value + "&gt;";
                },
                RegexOptions.IgnoreCase
            );
        }

        /// <summary>
        /// 清理用户提问内容：精确匹配最外层 &lt;USER_REQUEST&gt;...&lt;/USER_REQUEST&gt;（LastIndexOf 防正文误截断），并移除系统附加元数据与孤立标签
        /// </summary>
        public static string CleanUserInputContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            int startTag = content.IndexOf("<USER_REQUEST>", StringComparison.OrdinalIgnoreCase);
            if (startTag >= 0)
            {
                int start = startTag + "<USER_REQUEST>".Length;
                int endTag = content.LastIndexOf("</USER_REQUEST>", StringComparison.OrdinalIgnoreCase);
                string inner = (endTag >= start)
                    ? content.Substring(start, endTag - start)
                    : content.Substring(start);
                return inner.Trim();
            }

            string cleaned = content;
            cleaned = Regex.Replace(cleaned, @"<ADDITIONAL_METADATA>[\s\S]*?</ADDITIONAL_METADATA>", string.Empty, RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"<USER_SETTINGS_CHANGE>[\s\S]*?</USER_SETTINGS_CHANGE>", string.Empty, RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"</?USER_REQUEST>", string.Empty, RegexOptions.IgnoreCase);
            return cleaned.Trim();
        }
    }
}
