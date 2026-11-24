using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // TRANSCRIPT & BRAIN ARTIFACTS MARKDOWN EXPORTER (Milestone 2 — F4)
    // Strictly C# 5.0 (.NET Framework 4.x)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 会话明文对话流 (transcript_full.jsonl / transcript.jsonl)、会话数据库步骤 (*.db) 与脑区产出文档 (*.md) 的流式 Markdown 导出器
    /// 支持 IDE (antigravity) 与 CLI (antigravity-cli) 会话。
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

        private static string ResolveBrainDirectory(string geminiPath, string cid, bool preferCli)
        {
            if (string.IsNullOrEmpty(geminiPath) || string.IsNullOrEmpty(cid))
                return string.Empty;

            if (!SessionMetadataExtractor.IsLikelyConversationId(cid))
                throw new ArgumentException("非法的会话 ID（包含不允许的路径字符或保留名称）: " + cid, "session");

            string trimmedGemini = geminiPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            bool hasExplicitAppDir = trimmedGemini.EndsWith("antigravity", StringComparison.OrdinalIgnoreCase) ||
                                     trimmedGemini.EndsWith("antigravity-cli", StringComparison.OrdinalIgnoreCase);
            if (hasExplicitAppDir)
            {
                string explicitRoot = Path.Combine(trimmedGemini, "brain");
                string explicitCandidate = Path.Combine(explicitRoot, cid);
                if (!IsPathContainedUnder(explicitRoot, explicitCandidate))
                    throw new InvalidOperationException("拒绝访问越界的脑区目录: " + explicitCandidate);
                return explicitCandidate;
            }

            string primaryApp = preferCli ? "antigravity-cli" : "antigravity";
            string fallbackApp = preferCli ? "antigravity" : "antigravity-cli";

            string primaryRoot = Path.Combine(trimmedGemini, primaryApp, "brain");
            string primaryCandidate = Path.Combine(primaryRoot, cid);
            if (!IsPathContainedUnder(primaryRoot, primaryCandidate))
                throw new InvalidOperationException("拒绝访问越界的脑区目录: " + primaryCandidate);
            if (Directory.Exists(primaryCandidate))
                return primaryCandidate;

            string fallbackRoot = Path.Combine(trimmedGemini, fallbackApp, "brain");
            string fallbackCandidate = Path.Combine(fallbackRoot, cid);
            if (!IsPathContainedUnder(fallbackRoot, fallbackCandidate))
                throw new InvalidOperationException("拒绝访问越界的脑区目录: " + fallbackCandidate);
            if (Directory.Exists(fallbackCandidate))
                return fallbackCandidate;

            return primaryCandidate;
        }

        private static string ResolveConversationDbPath(string geminiPath, string cid, bool preferCli)
        {
            if (string.IsNullOrEmpty(geminiPath) || string.IsNullOrEmpty(cid))
                return string.Empty;
            if (!SessionMetadataExtractor.IsLikelyConversationId(cid))
                return string.Empty;

            string trimmedGemini = geminiPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            bool hasExplicitAppDir = trimmedGemini.EndsWith("antigravity", StringComparison.OrdinalIgnoreCase) ||
                                     trimmedGemini.EndsWith("antigravity-cli", StringComparison.OrdinalIgnoreCase);
            if (hasExplicitAppDir)
            {
                string convRoot = Path.Combine(trimmedGemini, "conversations");
                string cand = Path.Combine(convRoot, cid + ".db");
                if (IsPathContainedUnder(convRoot, cand) && File.Exists(cand))
                    return cand;
                return string.Empty;
            }

            string[] appDirs = preferCli
                ? new string[] { "antigravity-cli", "antigravity" }
                : new string[] { "antigravity", "antigravity-cli" };

            for (int i = 0; i < appDirs.Length; i++)
            {
                string convRoot = Path.Combine(trimmedGemini, appDirs[i], "conversations");
                string cand = Path.Combine(convRoot, cid + ".db");
                if (IsPathContainedUnder(convRoot, cand) && File.Exists(cand))
                    return cand;
            }
            return string.Empty;
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

        private static void WriteAtomicMarkdownFile(
            string outputFilePath,
            ConversationSummaryInfo session,
            TextReader transcriptReader,
            List<SessionStepRecord> dbSteps,
            Dictionary<string, string> artifacts,
            StringBuilder optionalMirror)
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
                    FormatSessionMarkdownStream(session, transcriptReader, dbSteps, artifacts, sw, optionalMirror);
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
            List<SessionStepRecord> dbSteps,
            Dictionary<string, string> artifacts,
            int maxPreviewChars,
            out bool isTruncated)
        {
            isTruncated = false;
            if (!string.IsNullOrEmpty(outputFilePath))
            {
                StringBuilder mirror = new StringBuilder();
                WriteAtomicMarkdownFile(outputFilePath, session, transcriptReader, dbSteps, artifacts, mirror);
                return mirror.ToString();
            }

            if (maxPreviewChars > 0)
            {
                using (BoundedStringWriter bsw = new BoundedStringWriter(maxPreviewChars))
                {
                    FormatSessionMarkdownStream(session, transcriptReader, dbSteps, artifacts, bsw, null);
                    isTruncated = bsw.Truncated;
                    return bsw.ToString();
                }
            }

            StringBuilder sb = new StringBuilder();
            using (StringWriter sw = new StringWriter(sb))
            {
                FormatSessionMarkdownStream(session, transcriptReader, dbSteps, artifacts, sw, null);
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
        /// 将本地会话导出为 Markdown 文档（流式读取 transcript_full.jsonl / transcript.jsonl 与产出文档，支持 IDE 与 CLI）
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
            bool preferCli = session.IsCliSession ||
                             string.Equals(session.ProjectId, "default-cli-project", StringComparison.OrdinalIgnoreCase);
            string brainDir = ResolveBrainDirectory(geminiPath, cid, preferCli);

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
                        }
                    }
                }
                catch
                {
                }
            }

            FileStream transcriptFs = null;
            StreamReader transcriptReader = null;
            List<SessionStepRecord> dbSteps = null;
            try
            {
                bool hasNonEmptyTranscript = false;
                if (!string.IsNullOrEmpty(transcriptPath) && File.Exists(transcriptPath))
                {
                    try
                    {
                        if (new FileInfo(transcriptPath).Length > 0)
                        {
                            transcriptFs = new FileStream(transcriptPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                            transcriptReader = new StreamReader(transcriptFs, Encoding.UTF8);
                            hasNonEmptyTranscript = true;
                        }
                    }
                    catch
                    {
                        if (transcriptReader != null) { transcriptReader.Dispose(); transcriptReader = null; }
                        if (transcriptFs != null) { transcriptFs.Dispose(); transcriptFs = null; }
                        hasNonEmptyTranscript = false;
                    }
                }

                if (!hasNonEmptyTranscript && !string.IsNullOrEmpty(cid))
                {
                    string dbPath = ResolveConversationDbPath(geminiPath, cid, preferCli);
                    if (!string.IsNullOrEmpty(dbPath))
                    {
                        string dbErr;
                        dbSteps = SqliteSessionStore.ExtractSteps(dbPath, out dbErr);
                    }
                }

                return RenderStreamToOutputOrPreview(outputFilePath, session, transcriptReader, dbSteps, artifacts, maxPreviewChars, out isTruncated);
            }
            finally
            {
                if (transcriptReader != null) transcriptReader.Dispose();
                if (transcriptFs != null) transcriptFs.Dispose();
            }
        }

        /// <summary>
        /// 从备份压缩包（.agy / .zip）中流式解压并导出指定会话为 Markdown 文档（支持 IDE 与 CLI）
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

            bool preferCli = session.IsCliSession ||
                             string.Equals(session.ProjectId, "default-cli-project", StringComparison.OrdinalIgnoreCase);

            string ideFullSuffix = "antigravity/brain/" + cid + "/.system_generated/logs/transcript_full.jsonl";
            string ideCompactSuffix = "antigravity/brain/" + cid + "/.system_generated/logs/transcript.jsonl";
            string ideBrainPrefix = "antigravity/brain/" + cid + "/";
            string ideDbSuffix = "antigravity/conversations/" + cid + ".db";

            string cliFullSuffix = "antigravity-cli/brain/" + cid + "/.system_generated/logs/transcript_full.jsonl";
            string cliCompactSuffix = "antigravity-cli/brain/" + cid + "/.system_generated/logs/transcript.jsonl";
            string cliBrainPrefix = "antigravity-cli/brain/" + cid + "/";
            string cliDbSuffix = "antigravity-cli/conversations/" + cid + ".db";

            string shortCliFullSuffix = "cli/brain/" + cid + "/.system_generated/logs/transcript_full.jsonl";
            string shortCliCompactSuffix = "cli/brain/" + cid + "/.system_generated/logs/transcript.jsonl";
            string shortCliBrainPrefix = "cli/brain/" + cid + "/";
            string shortCliDbSuffix = "cli/conversations/" + cid + ".db";

            Dictionary<string, string> artifacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using (FileStream fs = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                ZipArchiveEntry ideFullEntry = null;
                ZipArchiveEntry ideCompactEntry = null;
                ZipArchiveEntry cliFullEntry = null;
                ZipArchiveEntry cliCompactEntry = null;
                ZipArchiveEntry ideDbEntry = null;
                ZipArchiveEntry cliDbEntry = null;

                if (!string.IsNullOrEmpty(cid))
                {
                    for (int i = 0; i < archive.Entries.Count; i++)
                    {
                        ZipArchiveEntry entry = archive.Entries[i];
                        if (entry == null || string.IsNullOrEmpty(entry.FullName)) continue;

                        if (!StreamingZipIndexReader.IsSafeZipEntryPath(entry.FullName)) continue;
                        string normName = entry.FullName.Replace('\\', '/');

                        string tail;
                        if (TryMatchZipSubPath(normName, ideFullSuffix, out tail) && tail.Length == 0)
                        {
                            ideFullEntry = entry;
                        }
                        else if (TryMatchZipSubPath(normName, ideCompactSuffix, out tail) && tail.Length == 0)
                        {
                            ideCompactEntry = entry;
                        }
                        else if ((TryMatchZipSubPath(normName, cliFullSuffix, out tail) || TryMatchZipSubPath(normName, shortCliFullSuffix, out tail)) && tail.Length == 0)
                        {
                            cliFullEntry = entry;
                        }
                        else if ((TryMatchZipSubPath(normName, cliCompactSuffix, out tail) || TryMatchZipSubPath(normName, shortCliCompactSuffix, out tail)) && tail.Length == 0)
                        {
                            cliCompactEntry = entry;
                        }
                        else if (TryMatchZipSubPath(normName, ideDbSuffix, out tail) && tail.Length == 0)
                        {
                            ideDbEntry = entry;
                        }
                        else if ((TryMatchZipSubPath(normName, cliDbSuffix, out tail) || TryMatchZipSubPath(normName, shortCliDbSuffix, out tail)) && tail.Length == 0)
                        {
                            cliDbEntry = entry;
                        }
                        else if (TryMatchZipSubPath(normName, ideBrainPrefix, out tail) ||
                                 TryMatchZipSubPath(normName, cliBrainPrefix, out tail) ||
                                 TryMatchZipSubPath(normName, shortCliBrainPrefix, out tail))
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

                ZipArchiveEntry primaryFull = preferCli ? (cliFullEntry ?? ideFullEntry) : (ideFullEntry ?? cliFullEntry);
                ZipArchiveEntry primaryCompact = preferCli ? (cliCompactEntry ?? ideCompactEntry) : (ideCompactEntry ?? cliCompactEntry);

                ZipArchiveEntry targetEntry = null;
                if (primaryFull != null && primaryFull.Length > 0)
                    targetEntry = primaryFull;
                else if (primaryCompact != null && primaryCompact.Length > 0)
                    targetEntry = primaryCompact;

                Stream entryStream = null;
                StreamReader entryReader = null;
                List<SessionStepRecord> dbSteps = null;
                string tempDbPath = null;
                try
                {
                    if (targetEntry != null)
                    {
                        entryStream = targetEntry.Open();
                        entryReader = new StreamReader(entryStream, Encoding.UTF8);
                    }
                    else
                    {
                        ZipArchiveEntry targetDbEntry = preferCli ? (cliDbEntry ?? ideDbEntry) : (ideDbEntry ?? cliDbEntry);
                        if (targetDbEntry != null)
                        {
                            tempDbPath = Path.Combine(Path.GetTempPath(), "agy_exp_" + Guid.NewGuid().ToString("N") + ".db");
                            using (Stream dbIn = targetDbEntry.Open())
                            using (FileStream dbOut = new FileStream(tempDbPath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                dbIn.CopyTo(dbOut);
                            }
                            string dbErr;
                            dbSteps = SqliteSessionStore.ExtractSteps(tempDbPath, out dbErr);
                        }
                    }

                    return RenderStreamToOutputOrPreview(outputFilePath, session, entryReader, dbSteps, artifacts, maxPreviewChars, out isTruncated);
                }
                finally
                {
                    if (entryReader != null) entryReader.Dispose();
                    if (entryStream != null) entryStream.Dispose();
                    if (!string.IsNullOrEmpty(tempDbPath))
                    {
                        try { if (File.Exists(tempDbPath)) File.Delete(tempDbPath); } catch {}
                    }
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
                FormatSessionMarkdownStream(session, sr, null, artifactMdFiles, sw, null);
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

        private static void EmitTranscriptTurn(
            TextWriter writer,
            StringBuilder mirror,
            string type,
            string createdAt,
            string content,
            string thinking,
            int stepIdx,
            ref int dialogueCount)
        {
            if (string.Equals(type, "USER_INPUT", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "USER", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "HUMAN", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "CORTEX_STEP_TYPE_USER_INPUT", StringComparison.OrdinalIgnoreCase))
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
            else if (string.Equals(type, "PLANNER_RESPONSE", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(type, "ASSISTANT", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(type, "MODEL", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(type, "CORTEX_STEP_TYPE_PLANNER_RESPONSE", StringComparison.OrdinalIgnoreCase))
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

        private static void FormatSessionMarkdownStream(
            ConversationSummaryInfo session,
            TextReader transcriptReader,
            List<SessionStepRecord> dbSteps,
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

            // 3. 对话记录（严格流式逐行读取，支持 JSONL 与 .db 步表降级）
            WriteLineWithMirror(writer, mirror, "# 💬 对话记录 (Conversation Transcript)");
            WriteLineWithMirror(writer, mirror, null);

            int dialogueCount = 0;
            BoundedStringWriter boundedWriter = writer as BoundedStringWriter;
            if (transcriptReader != null)
            {
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
                    EmitTranscriptTurn(
                        writer, mirror,
                        parsed.Type, parsed.CreatedAt, parsed.Content, parsed.Thinking,
                        parsed.HasStepIndex ? parsed.StepIndex : -1,
                        ref dialogueCount);
                }
            }
            else if (dbSteps != null && dbSteps.Count > 0)
            {
                for (int i = 0; i < dbSteps.Count; i++)
                {
                    if (boundedWriter != null && boundedWriter.Truncated) break;
                    SessionStepRecord rec = dbSteps[i];
                    if (rec == null) continue;
                    EmitTranscriptTurn(
                        writer, mirror,
                        rec.StepType, rec.CreatedAt, rec.Content, rec.Thinking,
                        rec.StepIndex,
                        ref dialogueCount);
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
        // JSONL 行解析（基于 UnifiedJson）
        // ══════════════════════════════════════════════════════════════════

        private static TranscriptLineFields ParseTranscriptJsonLine(string jsonLine)
        {
            TranscriptLineFields res = new TranscriptLineFields();
            if (string.IsNullOrEmpty(jsonLine)) return res;

            object parsed;
            if (!UnifiedJson.TryParse(jsonLine, out parsed))
                return res;

            Dictionary<string, object> dict = UnifiedJson.AsDict(parsed);
            if (dict == null)
                return res;

            object val;
            if (dict.TryGetValue("type", out val) && val is string) res.Type = (string)val;
            if (dict.TryGetValue("created_at", out val) && val is string) res.CreatedAt = (string)val;
            if (dict.TryGetValue("content", out val) && val is string) res.Content = (string)val;
            if (dict.TryGetValue("thinking", out val) && val is string) res.Thinking = (string)val;
            if (dict.TryGetValue("step_index", out val))
            {
                if (val is long)
                {
                    long lv = (long)val;
                    if (lv >= int.MinValue && lv <= int.MaxValue)
                    {
                        res.StepIndex = (int)lv;
                        res.HasStepIndex = true;
                    }
                }
                else if (val is int)
                {
                    res.StepIndex = (int)val;
                    res.HasStepIndex = true;
                }
                else if (val is double)
                {
                    double dv = (double)val;
                    if (dv >= int.MinValue && dv <= int.MaxValue)
                    {
                        res.StepIndex = (int)dv;
                        res.HasStepIndex = true;
                    }
                }
            }
            return res;
        }

        /// <summary>
        /// 从单行顶层 JSON 对象中精准提取指定字符串字段值并完成反转义（基于 UnifiedJson）
        /// </summary>
        public static string ExtractJsonStringField(string jsonLine, string fieldName)
        {
            if (string.IsNullOrEmpty(jsonLine) || string.IsNullOrEmpty(fieldName))
                return string.Empty;

            object parsed;
            if (!UnifiedJson.TryParse(jsonLine, out parsed))
                return string.Empty;

            Dictionary<string, object> dict = UnifiedJson.AsDict(parsed);
            if (dict == null) return string.Empty;

            object val;
            if (dict.TryGetValue(fieldName, out val) && val is string)
                return (string)val;

            return string.Empty;
        }

        /// <summary>
        /// 从单行顶层 JSON 对象中精准提取指定整数字段值（基于 UnifiedJson）
        /// </summary>
        public static int ExtractJsonIntField(string jsonLine, string fieldName, int defaultValue)
        {
            if (string.IsNullOrEmpty(jsonLine) || string.IsNullOrEmpty(fieldName))
                return defaultValue;

            object parsed;
            if (!UnifiedJson.TryParse(jsonLine, out parsed))
                return defaultValue;

            Dictionary<string, object> dict = UnifiedJson.AsDict(parsed);
            if (dict == null) return defaultValue;

            object val;
            if (!dict.TryGetValue(fieldName, out val) || val == null)
                return defaultValue;

            if (val is int) return (int)val;
            if (val is long)
            {
                long lv = (long)val;
                if (lv >= int.MinValue && lv <= int.MaxValue) return (int)lv;
            }
            if (val is double)
            {
                double dv = (double)val;
                if (dv >= int.MinValue && dv <= int.MaxValue) return (int)dv;
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
