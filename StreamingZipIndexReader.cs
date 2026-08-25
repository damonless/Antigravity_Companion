using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // STREAMING ZIP INDEX READER & ARCHIVING PIPELINE (Milestone 1 — F3, F10)
    // ══════════════════════════════════════════════════════════════════════

    public static class StreamingZipIndexReader
    {
        public static BackupArchiveIndex ReadIndex(string zipPath)
        {
            if (!File.Exists(zipPath))
                throw new FileNotFoundException("备份归档文件不存在: " + zipPath);

            Stopwatch sw = Stopwatch.StartNew();
            BackupArchiveIndex index = new BackupArchiveIndex();
            FileInfo fi = new FileInfo(zipPath);
            index.TotalZipSizeBytes = fi.Length;

            byte[] pbData = null;
            Dictionary<string, string> projectJsons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using (FileStream fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                for (int i = 0; i < archive.Entries.Count; i++)
                {
                    ZipArchiveEntry entry = archive.Entries[i];
                    string name = entry.FullName.Replace('\\', '/');

                    if (name.Equals("metadata.json", StringComparison.OrdinalIgnoreCase) || 
                        name.EndsWith("/metadata.json", StringComparison.OrdinalIgnoreCase))
                    {
                        using (StreamReader sr = new StreamReader(entry.Open(), Encoding.UTF8))
                        {
                            string json = sr.ReadToEnd();
                            index.SourceHome = DirectStreamArchiver.ExtractJsonValue(json, "source_home");
                            index.SourceUser = DirectStreamArchiver.ExtractJsonValue(json, "source_user");
                            index.BackupTime = DirectStreamArchiver.ExtractJsonValue(json, "backup_time");
                            index.Version = DirectStreamArchiver.ExtractJsonValue(json, "version");
                        }
                    }
                    else if (name.Contains("config/projects/") && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        string pId = Path.GetFileNameWithoutExtension(name);
                        using (StreamReader sr = new StreamReader(entry.Open(), Encoding.UTF8))
                        {
                            projectJsons[pId] = sr.ReadToEnd();
                        }
                    }
                    else if (name.EndsWith("agyhub_summaries_proto.pb", StringComparison.OrdinalIgnoreCase))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            using (Stream s = entry.Open())
                            {
                                s.CopyTo(ms);
                            }
                            pbData = ms.ToArray();
                        }
                    }
                    else if (name.Contains("antigravity/conversations/") && name.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                    {
                        string cid = Path.GetFileNameWithoutExtension(name);
                        index.ExistingConvoDbFiles.Add(cid);
                    }
                    else if (name.Contains("antigravity/brain/"))
                    {
                        int idx = name.IndexOf("antigravity/brain/");
                        if (idx >= 0)
                        {
                            string sub = name.Substring(idx + "antigravity/brain/".Length);
                            int slash = sub.IndexOf('/');
                            string brainCid = slash >= 0 ? sub.Substring(0, slash) : sub;
                            if (!string.IsNullOrEmpty(brainCid)) index.ExistingBrainDirs.Add(brainCid);
                        }
                    }
                    else if (name.Contains("rules/") || name.EndsWith(".geminirules") || name.EndsWith("GEMINI.md"))
                    {
                        index.HasRules = true;
                    }
                    else if (name.Contains("skills/"))
                    {
                        index.HasSkills = true;
                    }
                    else if (name.Contains("mcp/") || name.EndsWith("mcp_config.json"))
                    {
                        index.HasMcp = true;
                    }
                }

                // 解析项目配置
                foreach (KeyValuePair<string, string> kvp in projectJsons)
                {
                    string id = kvp.Key;
                    string json = kvp.Value;
                    string pName = DirectStreamArchiver.ExtractJsonValue(json, "name");
                    string pUri = DirectStreamArchiver.ExtractJsonValue(json, "folderUri");
                    if (!string.IsNullOrEmpty(pName))
                    {
                        index.Projects[id] = new ProjectMetadata { Id = id, Name = pName, FolderUri = pUri };
                    }
                }

                // 提取会话元数据
                if (pbData != null && pbData.Length > 0)
                {
                    index.ExtractedMetadataSizeBytes = pbData.Length;
                    index.Conversations = SessionMetadataExtractor.ExtractSummaries(pbData, index.Projects);
                }
            }

            sw.Stop();
            index.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            return index;
        }
    }

    /// <summary>
    /// 流式直接归档与路径转码工具类
    /// </summary>
    public static class DirectStreamArchiver
    {
        public static string ExtractJsonValue(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return string.Empty;
            string pattern = "\"" + key + "\"\\s*:\\s*\"([^\"]+)\"";
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(json, pattern);
            return m.Success ? m.Groups[1].Value : string.Empty;
        }

        public static string NormalizeUri(string uriStr)
        {
            if (string.IsNullOrEmpty(uriStr)) return string.Empty;
            try
            {
                string s = UnescapeUriString(uriStr);
                s = s.Replace('\\', '/');
                if (s.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                    s = s.Substring(8);
                else if (s.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                    s = s.Substring(7);
                s = s.TrimEnd('/');
                return s.ToLowerInvariant();
            }
            catch
            {
                return uriStr.Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
            }
        }

        public static string UnescapeUriString(string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            try
            {
                string s = Uri.UnescapeDataString(str);
                s = s.Replace("%3A", ":").Replace("%3a", ":")
                     .Replace("%2F", "/").Replace("%2f", "/")
                     .Replace("%5C", "\\").Replace("%5c", "\\");
                return s;
            }
            catch
            {
                return str;
            }
        }

        public static string RemapAllPathFormats(string text, string srcHome, string curHome)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(srcHome) || string.IsNullOrEmpty(curHome))
                return text;

            string sWin = srcHome;
            string dWin = curHome;

            string sFwd = srcHome.Replace('\\', '/');
            string dFwd = curHome.Replace('\\', '/');

            string sEsc = srcHome.Replace("\\", "\\\\");
            string dEsc = curHome.Replace("\\", "\\\\");

            string sUrl1 = UrlEncodePath(sFwd, true);
            string dUrl1 = UrlEncodePath(dFwd, true);

            string sUrl2 = UrlEncodePath(sFwd, false);
            string dUrl2 = UrlEncodePath(dFwd, false);

            string result = text;
            result = ReplaceExact(result, sUrl1, dUrl1);
            result = ReplaceExact(result, sUrl2, dUrl2);
            result = ReplaceExact(result, sEsc, dEsc);
            result = ReplaceExact(result, sWin, dWin);
            result = ReplaceExact(result, sFwd, dFwd);

            return result;
        }

        private static string ReplaceExact(string input, string oldVal, string newVal)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(oldVal) || oldVal == newVal)
                return input;
            return input.Replace(oldVal, newVal);
        }

        public static string UrlEncodePath(string path, bool lowerCase)
        {
            if (string.IsNullOrEmpty(path)) return path;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < path.Length; i++)
            {
                char c = path[i];
                if (c == ':') sb.Append(lowerCase ? "%3a" : "%3A");
                else if (c == '/') sb.Append(lowerCase ? "%2f" : "%2F");
                else if (c == '\\') sb.Append(lowerCase ? "%5c" : "%5C");
                else if (c == ' ') sb.Append("%20");
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
