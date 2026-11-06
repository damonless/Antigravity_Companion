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
    /// 会话明文对话流 (transcript.jsonl) 与脑区产出文档 (*.md) 的统一 Markdown 导出器
    /// 兼容 C# 5.0 (.NET Framework 4.0 csc.exe)
    /// </summary>
    public static class TranscriptMarkdownExporter
    {
        /// <summary>
        /// 将本地会话导出为 Markdown 文档（包含对话流明文与产出文档）
        /// </summary>
        /// <param name="session">会话元数据</param>
        /// <param name="geminiPath">.gemini 根目录路径</param>
        /// <param name="outputFilePath">输出 Markdown 文件路径（如果为 null 或空则不写文件）</param>
        /// <returns>生成的 Markdown 字符串</returns>
        public static string ExportLocalSessionToMarkdown(ConversationSummaryInfo session, string geminiPath, string outputFilePath)
        {
            if (session == null)
                throw new ArgumentNullException("session");

            string cid = !string.IsNullOrEmpty(session.ConversationId) ? session.ConversationId.Trim() : string.Empty;

            string brainDir = string.Empty;
            if (!string.IsNullOrEmpty(geminiPath))
            {
                string trimmedGemini = geminiPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (trimmedGemini.EndsWith("antigravity", StringComparison.OrdinalIgnoreCase))
                {
                    brainDir = Path.Combine(trimmedGemini, "brain", cid);
                }
                else
                {
                    brainDir = Path.Combine(trimmedGemini, "antigravity", "brain", cid);
                }
            }

            string transcriptJsonl = string.Empty;
            Dictionary<string, string> artifacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(brainDir) && Directory.Exists(brainDir))
            {
                string transcriptPath = Path.Combine(brainDir, ".system_generated", "logs", "transcript.jsonl");
                if (File.Exists(transcriptPath))
                {
                    try
                    {
                        using (FileStream fs = new FileStream(transcriptPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (StreamReader reader = new StreamReader(fs, Encoding.UTF8))
                        {
                            transcriptJsonl = reader.ReadToEnd();
                        }
                    }
                    catch
                    {
                        // 忽略并发或只读读取异常
                    }
                }

                try
                {
                    string[] mdFiles = Directory.GetFiles(brainDir, "*.md", SearchOption.TopDirectoryOnly);
                    for (int i = 0; i < mdFiles.Length; i++)
                    {
                        string file = mdFiles[i];
                        string fileName = Path.GetFileName(file);
                        try
                        {
                            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (StreamReader reader = new StreamReader(fs, Encoding.UTF8))
                            {
                                artifacts[fileName] = reader.ReadToEnd();
                            }
                        }
                        catch
                        {
                            // 忽略单个文件读取异常
                        }
                    }
                }
                catch
                {
                    // 忽略目录扫描异常
                }
            }

            string markdown = FormatSessionMarkdown(session, transcriptJsonl, artifacts);

            if (!string.IsNullOrEmpty(outputFilePath))
            {
                string dir = Path.GetDirectoryName(outputFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(outputFilePath, markdown, Encoding.UTF8);
            }

            return markdown;
        }

        /// <summary>
        /// 从备份压缩包（.agy / .zip）中解压并导出指定会话为 Markdown 文档
        /// </summary>
        /// <param name="session">会话元数据</param>
        /// <param name="zipFilePath">备份压缩包文件路径</param>
        /// <param name="outputFilePath">输出 Markdown 文件路径（如果为 null 或空则不写文件）</param>
        /// <returns>生成的 Markdown 字符串</returns>
        public static string ExportBackupSessionToMarkdown(ConversationSummaryInfo session, string zipFilePath, string outputFilePath)
        {
            if (session == null)
                throw new ArgumentNullException("session");
            if (!File.Exists(zipFilePath))
                throw new FileNotFoundException("备份归档文件不存在: " + zipFilePath);

            string cid = !string.IsNullOrEmpty(session.ConversationId) ? session.ConversationId.Trim() : string.Empty;
            string expectedTranscriptSuffix = "antigravity/brain/" + cid + "/.system_generated/logs/transcript.jsonl";
            string brainFolderPrefix = "antigravity/brain/" + cid + "/";

            string transcriptJsonl = string.Empty;
            Dictionary<string, string> artifacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using (FileStream fs = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                for (int i = 0; i < archive.Entries.Count; i++)
                {
                    ZipArchiveEntry entry = archive.Entries[i];
                    string normName = entry.FullName.Replace('\\', '/');

                    // 1. 匹配 transcript.jsonl
                    if (!string.IsNullOrEmpty(cid) &&
                        (normName.Equals(expectedTranscriptSuffix, StringComparison.OrdinalIgnoreCase) ||
                         normName.EndsWith("/" + expectedTranscriptSuffix, StringComparison.OrdinalIgnoreCase)))
                    {
                        using (Stream stream = entry.Open())
                        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            transcriptJsonl = reader.ReadToEnd();
                        }
                    }
                    // 2. 匹配脑区直属 top-level *.md 产出文档
                    else if (!string.IsNullOrEmpty(cid))
                    {
                        int idx = normName.IndexOf(brainFolderPrefix, StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0)
                        {
                            string relPath = normName.Substring(idx + brainFolderPrefix.Length);
                            if (!relPath.Contains("/") && relPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && relPath.Length > 3)
                            {
                                using (Stream stream = entry.Open())
                                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                                {
                                    artifacts[relPath] = reader.ReadToEnd();
                                }
                            }
                        }
                    }
                }
            }

            string markdown = FormatSessionMarkdown(session, transcriptJsonl, artifacts);

            if (!string.IsNullOrEmpty(outputFilePath))
            {
                string dir = Path.GetDirectoryName(outputFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(outputFilePath, markdown, Encoding.UTF8);
            }

            return markdown;
        }

        /// <summary>
        /// 格式化会话为结构化 Markdown 文档
        /// </summary>
        /// <param name="session">会话元数据</param>
        /// <param name="transcriptJsonlContent">transcript.jsonl 原始内容</param>
        /// <param name="artifactMdFiles">脑区产出文档字典 (文件名 -> 内容)</param>
        /// <returns>生成的 Markdown 字符串</returns>
        public static string FormatSessionMarkdown(ConversationSummaryInfo session, string transcriptJsonlContent, Dictionary<string, string> artifactMdFiles)
        {
            StringBuilder sb = new StringBuilder();

            // 1. 文档标题
            string displayTitle = session != null && !string.IsNullOrEmpty(session.DisplayTitle)
                ? session.DisplayTitle
                : "未命名会话";
            sb.AppendLine("# " + displayTitle);
            sb.AppendLine();

            // 2. 元数据列表
            sb.AppendLine(string.Format("- **会话 UUID**: {0}", session != null && !string.IsNullOrEmpty(session.ConversationId) ? session.ConversationId : "-"));
            sb.AppendLine(string.Format("- **所属工程**: {0}", session != null && !string.IsNullOrEmpty(session.ProjectName) ? session.ProjectName : "-"));
            sb.AppendLine(string.Format("- **工程路径**: {0}", session != null && !string.IsNullOrEmpty(session.FolderUri) ? session.FolderUri : "-"));
            sb.AppendLine(string.Format("- **更新时间**: {0}", session != null && session.UpdatedTime > DateTime.MinValue ? session.UpdatedTime.ToString("yyyy-MM-dd HH:mm:ss") : "-"));
            sb.AppendLine(string.Format("- **总步数 / 用户交互数**: {0} / {1}", session != null ? session.StepCount : 0, session != null ? session.UserInteractions : 0));
            sb.AppendLine();

            // 3. 对话记录
            sb.AppendLine("# 💬 对话记录 (Conversation Transcript)");
            sb.AppendLine();

            int dialogueCount = 0;
            if (!string.IsNullOrEmpty(transcriptJsonlContent))
            {
                string[] lines = transcriptJsonlContent.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    string type = ExtractJsonStringField(line, "type");
                    string createdAt = ExtractJsonStringField(line, "created_at");
                    string content = ExtractJsonStringField(line, "content");
                    string thinking = ExtractJsonStringField(line, "thinking");
                    int stepIdx = ExtractJsonIntField(line, "step_index", -1);

                    if (string.Equals(type, "USER_INPUT", StringComparison.OrdinalIgnoreCase))
                    {
                        string cleaned = CleanUserInputContent(content);
                        if (!string.IsNullOrEmpty(cleaned))
                        {
                            dialogueCount++;
                            string timeSuffix = !string.IsNullOrEmpty(createdAt) ? " - " + createdAt : string.Empty;
                            if (stepIdx >= 0)
                                sb.AppendLine(string.Format("## 👤 用户提问 (Step #{0}){1}", stepIdx, timeSuffix));
                            else
                                sb.AppendLine(!string.IsNullOrEmpty(createdAt) ? string.Format("## 👤 用户提问 - {0}", createdAt) : "## 👤 用户提问");

                            sb.AppendLine();
                            sb.AppendLine(cleaned);
                            sb.AppendLine();
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
                                sb.AppendLine(string.Format("## 🤖 AI 回复 (Step #{0}){1}", stepIdx, timeSuffix));
                            else
                                sb.AppendLine(!string.IsNullOrEmpty(createdAt) ? string.Format("## 🤖 AI 回复 - {0}", createdAt) : "## 🤖 AI 回复");

                            sb.AppendLine();

                            if (!string.IsNullOrEmpty(thinking))
                            {
                                sb.AppendLine("<details><summary>💭 思考过程 (Thinking)</summary>");
                                sb.AppendLine();
                                sb.AppendLine(thinking);
                                sb.AppendLine();
                                sb.AppendLine("</details>");
                                sb.AppendLine();
                            }

                            if (!string.IsNullOrEmpty(content))
                            {
                                sb.AppendLine(content);
                                sb.AppendLine();
                            }
                        }
                    }
                }
            }

            if (dialogueCount == 0)
            {
                sb.AppendLine("> *(未在该会话的脑区目录中检测到 transcript.jsonl 明文对话日志)*");
                sb.AppendLine();
            }

            // 4. 会话产出文档 (Brain Artifacts)
            if (artifactMdFiles != null && artifactMdFiles.Count > 0)
            {
                sb.AppendLine("# 📎 会话产出文档 (Brain Artifacts)");
                sb.AppendLine();

                List<string> keys = new List<string>(artifactMdFiles.Keys);
                keys.Sort(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < keys.Count; i++)
                {
                    string fileName = keys[i];
                    string mdContent = artifactMdFiles[fileName];

                    sb.AppendLine("## 📄 " + fileName);
                    sb.AppendLine();
                    if (!string.IsNullOrEmpty(mdContent))
                    {
                        sb.AppendLine(mdContent.Trim());
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 从单行 JSON 字符串中精准提取指定字符串字段值并完成反转义（支持 \", \\, \/, \n, \r, \t, \b, \f, \uXXXX）
        /// </summary>
        public static string ExtractJsonStringField(string jsonLine, string fieldName)
        {
            if (string.IsNullOrEmpty(jsonLine) || string.IsNullOrEmpty(fieldName))
                return string.Empty;

            string pattern = "\"" + Regex.Escape(fieldName) + "\"\\s*:\\s*\"";
            Match match = Regex.Match(jsonLine, pattern);
            while (match.Success)
            {
                int quotePos = match.Index;
                int backslashCount = 0;
                for (int b = quotePos - 1; b >= 0 && jsonLine[b] == '\\'; b--)
                {
                    backslashCount++;
                }
                if (backslashCount % 2 == 0)
                {
                    break;
                }
                match = match.NextMatch();
            }

            if (!match.Success)
                return string.Empty;

            int i = match.Index + match.Length;
            StringBuilder sb = new StringBuilder();

            while (i < jsonLine.Length)
            {
                char c = jsonLine[i];
                if (c == '\\')
                {
                    i++;
                    if (i >= jsonLine.Length)
                        break;

                    char esc = jsonLine[i];
                    if (esc == '"')
                    {
                        sb.Append('"');
                    }
                    else if (esc == '\\')
                    {
                        sb.Append('\\');
                    }
                    else if (esc == '/')
                    {
                        sb.Append('/');
                    }
                    else if (esc == 'n')
                    {
                        sb.Append('\n');
                    }
                    else if (esc == 'r')
                    {
                        sb.Append('\r');
                    }
                    else if (esc == 't')
                    {
                        sb.Append('\t');
                    }
                    else if (esc == 'b')
                    {
                        sb.Append('\b');
                    }
                    else if (esc == 'f')
                    {
                        sb.Append('\f');
                    }
                    else if (esc == 'u')
                    {
                        if (i + 4 < jsonLine.Length)
                        {
                            string hex = jsonLine.Substring(i + 1, 4);
                            try
                            {
                                int code = Convert.ToInt32(hex, 16);
                                sb.Append((char)code);
                                i += 4;
                            }
                            catch
                            {
                                sb.Append("\\u").Append(hex);
                                i += 4;
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
                }
                else if (c == '"')
                {
                    break;
                }
                else
                {
                    sb.Append(c);
                }
                i++;
            }

            return sb.ToString();
        }

        /// <summary>
        /// 从单行 JSON 字符串中精准提取指定整数字段值
        /// </summary>
        public static int ExtractJsonIntField(string jsonLine, string fieldName, int defaultValue)
        {
            if (string.IsNullOrEmpty(jsonLine) || string.IsNullOrEmpty(fieldName))
                return defaultValue;

            string pattern = "\"" + Regex.Escape(fieldName) + "\"\\s*:\\s*(-?\\d+)";
            Match match = Regex.Match(jsonLine, pattern);
            while (match.Success)
            {
                int quotePos = match.Index;
                int backslashCount = 0;
                for (int b = quotePos - 1; b >= 0 && jsonLine[b] == '\\'; b--)
                {
                    backslashCount++;
                }
                if (backslashCount % 2 == 0)
                {
                    int val;
                    if (int.TryParse(match.Groups[1].Value, out val))
                    {
                        return val;
                    }
                    break;
                }
                match = match.NextMatch();
            }

            return defaultValue;
        }

        /// <summary>
        /// 清理用户提问内容，去除系统附加元数据与配置指令
        /// </summary>
        public static string CleanUserInputContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            int startTag = content.IndexOf("<USER_REQUEST>", StringComparison.OrdinalIgnoreCase);
            if (startTag >= 0)
            {
                int start = startTag + "<USER_REQUEST>".Length;
                int endTag = content.IndexOf("</USER_REQUEST>", start, StringComparison.OrdinalIgnoreCase);
                if (endTag >= 0)
                {
                    return content.Substring(start, endTag - start).Trim();
                }
            }

            string cleaned = content;
            cleaned = Regex.Replace(cleaned, @"<ADDITIONAL_METADATA>[\s\S]*?</ADDITIONAL_METADATA>", string.Empty, RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"<USER_SETTINGS_CHANGE>[\s\S]*?</USER_SETTINGS_CHANGE>", string.Empty, RegexOptions.IgnoreCase);
            return cleaned.Trim();
        }
    }
}
