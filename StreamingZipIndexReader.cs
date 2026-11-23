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
            Dictionary<string, string> annotTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using (FileStream fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                for (int i = 0; i < archive.Entries.Count; i++)
                {
                    ZipArchiveEntry entry = archive.Entries[i];
                    string rawName = entry.FullName.Replace('\\', '/').TrimStart('/');
                    if (string.IsNullOrEmpty(rawName) || rawName.EndsWith("/")) continue;

                    string logical = rawName.StartsWith("data/", StringComparison.OrdinalIgnoreCase)
                        ? rawName.Substring(5)
                        : rawName;

                    if (string.Equals(logical, "metadata.json", StringComparison.OrdinalIgnoreCase))
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
                    else if (logical.StartsWith("config/projects/", StringComparison.OrdinalIgnoreCase) &&
                             logical.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        string rel = logical.Substring("config/projects/".Length);
                        if (rel.IndexOf('/') < 0)
                        {
                            string pId = Path.GetFileNameWithoutExtension(rel);
                            if (SessionMetadataExtractor.IsLikelyConversationId(pId))
                            {
                                using (StreamReader sr = new StreamReader(entry.Open(), Encoding.UTF8))
                                {
                                    projectJsons[pId] = sr.ReadToEnd();
                                }
                            }
                        }
                    }
                    else if (string.Equals(logical, "antigravity/agyhub_summaries_proto.pb", StringComparison.OrdinalIgnoreCase))
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
                    else if (logical.StartsWith("antigravity/conversations/", StringComparison.OrdinalIgnoreCase) &&
                            (logical.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
                             logical.EndsWith(".db-wal", StringComparison.OrdinalIgnoreCase)))
                    {
                        string fn = Path.GetFileName(logical);
                        string cid = fn;
                        if (cid.EndsWith(".db-wal", StringComparison.OrdinalIgnoreCase)) cid = cid.Substring(0, cid.Length - 7);
                        else if (cid.EndsWith(".db", StringComparison.OrdinalIgnoreCase)) cid = cid.Substring(0, cid.Length - 3);

                        if (SessionMetadataExtractor.IsLikelyConversationId(cid))
                            index.ExistingConvoDbFiles.Add(cid.Trim());
                    }
                    else if (logical.StartsWith("antigravity/brain/", StringComparison.OrdinalIgnoreCase))
                    {
                        string sub = logical.Substring("antigravity/brain/".Length);
                        int slash = sub.IndexOf('/');
                        string brainCid = slash >= 0 ? sub.Substring(0, slash) : sub;
                        if (SessionMetadataExtractor.IsLikelyConversationId(brainCid))
                            index.ExistingBrainDirs.Add(brainCid.Trim());
                    }
                    else if (logical.StartsWith("antigravity/annotations/", StringComparison.OrdinalIgnoreCase) &&
                             logical.EndsWith(".pbtxt", StringComparison.OrdinalIgnoreCase))
                    {
                        string rel = logical.Substring("antigravity/annotations/".Length);
                        if (rel.IndexOf('/') < 0)
                        {
                            string cid = Path.GetFileNameWithoutExtension(rel);
                            if (SessionMetadataExtractor.IsLikelyConversationId(cid))
                            {
                                using (StreamReader sr = new StreamReader(entry.Open(), Encoding.UTF8))
                                {
                                    string rawPbtxt = sr.ReadToEnd();
                                    string extracted = PbtxtAnnotationCodec.ExtractTitle(rawPbtxt);
                                    if (!string.IsNullOrWhiteSpace(extracted))
                                    {
                                        string trimmedTitle = extracted.Trim();
                                        if (!IncrementalProtoShardMerger.IsSyntheticPlaceholderTitle(trimmedTitle))
                                        {
                                            annotTitles[cid.Trim()] = trimmedTitle;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (!logical.StartsWith("antigravity/", StringComparison.OrdinalIgnoreCase))
                    {
                        if (logical.StartsWith("config/rules/", StringComparison.OrdinalIgnoreCase) ||
                            logical.Equals("GEMINI.md", StringComparison.OrdinalIgnoreCase) ||
                            logical.EndsWith("/GEMINI.md", StringComparison.OrdinalIgnoreCase) ||
                            logical.EndsWith(".geminirules", StringComparison.OrdinalIgnoreCase))
                        {
                            index.HasRules = true;
                        }
                        else if (logical.StartsWith("config/skills/", StringComparison.OrdinalIgnoreCase) ||
                                 logical.StartsWith("config/plugins/", StringComparison.OrdinalIgnoreCase))
                        {
                            index.HasSkills = true;
                        }
                        else if (logical.StartsWith("config/mcp/", StringComparison.OrdinalIgnoreCase) ||
                                 logical.Equals("config/mcp_config.json", StringComparison.OrdinalIgnoreCase) ||
                                 logical.Equals("config/hooks.json", StringComparison.OrdinalIgnoreCase))
                        {
                            index.HasMcp = true;
                        }
                    }
                }

                // 解析项目配置
                foreach (KeyValuePair<string, string> kvp in projectJsons)
                {
                    string id = kvp.Key;
                    string json = kvp.Value;
                    string pName = DirectStreamArchiver.ExtractJsonValue(json, "name");
                    List<string> pUris = DirectStreamArchiver.ExtractAllProjectFolderUris(json);
                    string pUri = pUris.Count > 0 ? pUris[0] : string.Empty;
                    if (!string.IsNullOrEmpty(pName))
                    {
                        ProjectMetadata pm = new ProjectMetadata { Id = id, Name = pName, FolderUri = pUri };
                        if (pUris.Count > 0) pm.FolderUris.AddRange(pUris);
                        index.Projects[id] = pm;
                    }
                }

                // 提取会话元数据
                if (pbData != null && pbData.Length > 0)
                {
                    index.ExtractedMetadataSizeBytes = pbData.Length;
                    index.Conversations = SessionMetadataExtractor.ExtractSummaries(pbData, index.Projects);
                }

                // 叠加备份包内 annotations/<cid>.pbtxt 真实自定义标题 (Layer 1 优先级最高)
                if (annotTitles.Count > 0 && index.Conversations != null)
                {
                    for (int i = 0; i < index.Conversations.Count; i++)
                    {
                        ConversationSummaryInfo conv = index.Conversations[i];
                        if (conv == null || string.IsNullOrWhiteSpace(conv.ConversationId)) continue;
                        string annotTitle;
                        if (annotTitles.TryGetValue(conv.ConversationId.Trim(), out annotTitle) &&
                            !string.IsNullOrWhiteSpace(annotTitle))
                        {
                            conv.CustomTitle = annotTitle;
                            conv.HasCustomTitle = true;
                            conv.DisplayTitle = annotTitle;
                        }
                    }
                }

                // 提取细粒度能力与配置资产 (Plugins, Skills, Rules, MCP, Hooks)
                string curHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrEmpty(curHome)) curHome = index.SourceHome ?? string.Empty;
                string localGemini = !string.IsNullOrEmpty(curHome) ? Path.Combine(curHome, ".gemini") : null;
                index.Capabilities = CapabilityRepositoryManager.ScanZipArchiveCapabilities(archive, index.SourceHome, curHome, localGemini);
                for (int c = 0; c < index.Capabilities.Count; c++)
                {
                    CapabilityKind k = index.Capabilities[c].Kind;
                    if (k == CapabilityKind.Rule) index.HasRules = true;
                    else if (k == CapabilityKind.Skill || k == CapabilityKind.Plugin) index.HasSkills = true;
                    else if (k == CapabilityKind.McpServer || k == CapabilityKind.Hook) index.HasMcp = true;
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
            string topOrFirst;
            ScanJsonValues(json, key, true, out topOrFirst);
            return topOrFirst;
        }

        public static List<string> ExtractAllJsonValues(string json, string key)
        {
            string unused;
            return ScanJsonValues(json, key, false, out unused);
        }

        public static List<string> ExtractAllProjectFolderUris(string json)
        {
            List<string> combined = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] keys = new string[] { "folderUri", "folderUris" };
            for (int k = 0; k < keys.Length; k++)
            {
                List<string> vals = ExtractAllJsonValues(json, keys[k]);
                for (int i = 0; i < vals.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(vals[i]) && seen.Add(vals[i]))
                        combined.Add(vals[i]);
                }
            }
            if (combined.Count == 0)
            {
                string single = ExtractJsonValue(json, "folderUri");
                if (!string.IsNullOrWhiteSpace(single)) combined.Add(single);
            }
            return combined;
        }

        private static List<string> ScanJsonValues(string json, string key, bool preferTopLevelSingle, out string topOrFirst)
        {
            topOrFirst = string.Empty;
            List<string> all = new List<string>();
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return all;

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int idx = 0;
            int depth = 0;
            string nestedMatch = null;

            while (idx < json.Length)
            {
                char c = json[idx];

                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    idx++;
                    continue;
                }
                if (c == '/' && idx + 1 < json.Length)
                {
                    if (json[idx + 1] == '/')
                    {
                        idx += 2;
                        while (idx < json.Length && json[idx] != '\n' && json[idx] != '\r') idx++;
                        continue;
                    }
                    if (json[idx + 1] == '*')
                    {
                        idx += 2;
                        while (idx + 1 < json.Length && !(json[idx] == '*' && json[idx + 1] == '/')) idx++;
                        idx = (idx + 1 < json.Length) ? idx + 2 : json.Length;
                        continue;
                    }
                }

                if (c == '{' || c == '[')
                {
                    depth++;
                    idx++;
                    continue;
                }
                if (c == '}' || c == ']')
                {
                    if (depth > 0) depth--;
                    idx++;
                    continue;
                }

                if (c == '"')
                {
                    string propName = ReadJsonStringSinglePass(json, ref idx);
                    int probe = SkipWsAndComments(json, idx);
                    if (probe < json.Length && json[probe] == ':')
                    {
                        probe = SkipWsAndComments(json, probe + 1);
                        if (string.Equals(propName, key, StringComparison.OrdinalIgnoreCase))
                        {
                            if (probe < json.Length && json[probe] == '"')
                            {
                                string val = ReadJsonStringSinglePass(json, ref probe);
                                if (preferTopLevelSingle)
                                {
                                    if (depth == 1)
                                    {
                                        topOrFirst = val;
                                        return all;
                                    }
                                    if (nestedMatch == null) nestedMatch = val;
                                }
                                else if (!string.IsNullOrWhiteSpace(val) && seen.Add(val))
                                {
                                    all.Add(val);
                                }
                                idx = probe;
                                continue;
                            }
                            if (!preferTopLevelSingle && probe < json.Length && json[probe] == '[')
                            {
                                probe++;
                                while (probe < json.Length)
                                {
                                    probe = SkipWsAndComments(json, probe);
                                    if (probe >= json.Length) break;
                                    if (json[probe] == ']') { probe++; break; }
                                    if (json[probe] == ',') { probe++; continue; }
                                    if (json[probe] == '"')
                                    {
                                        string elem = ReadJsonStringSinglePass(json, ref probe);
                                        if (!string.IsNullOrWhiteSpace(elem) && seen.Add(elem))
                                            all.Add(elem);
                                    }
                                    else
                                    {
                                        probe++;
                                    }
                                }
                                idx = probe;
                                continue;
                            }
                        }
                    }
                    continue;
                }

                idx++;
            }

            if (preferTopLevelSingle)
            {
                topOrFirst = nestedMatch ?? string.Empty;
            }
            return all;
        }

        private static int SkipWsAndComments(string s, int idx)
        {
            while (idx < s.Length)
            {
                char c = s[idx];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    idx++;
                    continue;
                }
                if (c == '/' && idx + 1 < s.Length)
                {
                    if (s[idx + 1] == '/')
                    {
                        idx += 2;
                        while (idx < s.Length && s[idx] != '\n' && s[idx] != '\r') idx++;
                        continue;
                    }
                    if (s[idx + 1] == '*')
                    {
                        idx += 2;
                        while (idx + 1 < s.Length && !(s[idx] == '*' && s[idx + 1] == '/')) idx++;
                        idx = (idx + 1 < s.Length) ? idx + 2 : s.Length;
                        continue;
                    }
                }
                break;
            }
            return idx;
        }

        internal static string ReadJsonStringSinglePass(string json, ref int idx)
        {
            if (idx >= json.Length || json[idx] != '"') return string.Empty;
            idx++; // skip '"'
            StringBuilder sb = new StringBuilder();
            while (idx < json.Length)
            {
                char ch = json[idx++];
                if (ch == '"')
                {
                    return sb.ToString();
                }
                if (ch == '\\' && idx < json.Length)
                {
                    char esc = json[idx++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (idx + 4 <= json.Length)
                            {
                                int code;
                                if (int.TryParse(json.Substring(idx, 4), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out code))
                                {
                                    sb.Append((char)code);
                                    idx += 4;
                                    break;
                                }
                            }
                            sb.Append('u');
                            break;
                        default:
                            sb.Append(esc);
                            break;
                    }
                }
                else
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
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

            string sWin = srcHome.TrimEnd('\\', '/');
            string dWin = curHome.TrimEnd('\\', '/');
            if (string.IsNullOrEmpty(sWin) || string.IsNullOrEmpty(dWin))
                return text;

            string sFwd = sWin.Replace('\\', '/');
            string dFwd = dWin.Replace('\\', '/');

            string sEsc = sWin.Replace("\\", "\\\\");
            string dEsc = dWin.Replace("\\", "\\\\");

            string sFwdLowerDrive = LowerDrivePrefix(sFwd);
            string dFwdLowerDrive = LowerDrivePrefix(dFwd);

            string result = text;
            // 1. 最严格编码：全部斜杠与冒号百分号编码 (%2f / %2F)
            result = ReplacePathPrefixIgnoreCase(result, UrlEncodePath(sFwd, true, true), UrlEncodePath(dFwd, true, true));
            result = ReplacePathPrefixIgnoreCase(result, UrlEncodePath(sWin, true, true), UrlEncodePath(dWin, true, true));

            // 2. VS Code / Antigravity 标准 file:/// 形态 (保留 '/'，驱动器字母小写、冒号/空格/中文 UTF-8 采用大写 %3A 编码)
            result = ReplacePathPrefixIgnoreCase(result, UrlEncodePath(sFwdLowerDrive, false, false), UrlEncodePath(dFwdLowerDrive, false, false));
            result = ReplacePathPrefixIgnoreCase(result, UrlEncodePath(sFwd, false, false), UrlEncodePath(dFwdLowerDrive, false, false));

            // 3. 仅编码冒号但未编码中文/空格的半编码形式
            result = ReplacePathPrefixIgnoreCase(result, sFwd.Replace(":", "%3A"), dFwd.Replace(":", "%3A"));
            result = ReplacePathPrefixIgnoreCase(result, sFwd.Replace(":", "%3a"), dFwd.Replace(":", "%3a"));

            // 4. 仅编码空格/非 ASCII 但保留原始冒号与 '/'
            result = ReplacePathPrefixIgnoreCase(result, UrlEncodePathSpacesAndUtf8Only(sFwd, false), UrlEncodePathSpacesAndUtf8Only(dFwd, false));

            // 5. JSON 双反斜杠转义与原生 Windows / POSIX 路径
            result = ReplacePathPrefixIgnoreCase(result, sEsc, dEsc);
            result = ReplacePathPrefixIgnoreCase(result, sWin, dWin);
            result = ReplacePathPrefixIgnoreCase(result, sFwd, dFwd);

            return result;
        }

        private static string LowerDrivePrefix(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length < 2) return path;
            if (path[1] == ':' && path[0] >= 'A' && path[0] <= 'Z')
                return char.ToLowerInvariant(path[0]) + path.Substring(1);
            return path;
        }

        public static string ReplacePathPrefixIgnoreCase(string input, string oldVal, string newVal)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(oldVal) ||
                string.Equals(oldVal, newVal, StringComparison.OrdinalIgnoreCase))
                return input;

            int idx = input.IndexOf(oldVal, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return input;

            bool endsWithSep = oldVal.EndsWith("/", StringComparison.Ordinal) ||
                               oldVal.EndsWith("\\", StringComparison.Ordinal) ||
                               oldVal.EndsWith("%2f", StringComparison.OrdinalIgnoreCase) ||
                               oldVal.EndsWith("%5c", StringComparison.OrdinalIgnoreCase);

            StringBuilder sb = new StringBuilder(input.Length);
            int lastIdx = 0;
            bool changed = false;

            while (idx >= 0)
            {
                bool validBefore = IsValidPathPrefixStartBoundary(input, idx, oldVal);
                int afterIdx = idx + oldVal.Length;
                bool validAfter = endsWithSep || IsValidPathPrefixEndBoundary(input, afterIdx);

                if (validBefore && validAfter)
                {
                    sb.Append(input, lastIdx, idx - lastIdx);
                    sb.Append(newVal);
                    lastIdx = afterIdx;
                    changed = true;
                    idx = input.IndexOf(oldVal, lastIdx, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    idx = input.IndexOf(oldVal, idx + 1, StringComparison.OrdinalIgnoreCase);
                }
            }

            if (!changed) return input;
            sb.Append(input, lastIdx, input.Length - lastIdx);
            return sb.ToString();
        }

        private static bool IsValidPathPrefixStartBoundary(string input, int idx, string oldVal)
        {
            if (idx <= 0) return true;
            char prev = input[idx - 1];
            // file:/// 前缀或路径分隔/引号/空白/等号/括号均视为有效起始边界
            if (prev == '/' || prev == '\\' || prev == '"' || prev == '\'' ||
                prev == ' ' || prev == '\t' || prev == '\r' || prev == '\n' ||
                prev == '(' || prev == '[' || prev == '<' || prev == '=' ||
                prev == ',' || prev == ';' || prev == '?')
                return true;
            if (oldVal.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) return true;
            return !char.IsLetterOrDigit(prev) && prev != '_';
        }

        private static bool IsValidPathPrefixEndBoundary(string input, int afterIdx)
        {
            if (afterIdx >= input.Length) return true;
            char next = input[afterIdx];
            if (next == '/' || next == '\\' || next == '"' || next == '\'' ||
                next == ' ' || next == '\t' || next == '\r' || next == '\n' ||
                next == ')' || next == ']' || next == '>' || next == ',' ||
                next == ';' || next == '?' || next == '#' || next == '\0')
                return true;
            if (afterIdx + 3 <= input.Length)
            {
                string tri = input.Substring(afterIdx, 3);
                if (string.Equals(tri, "%2F", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(tri, "%5C", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static string UrlEncodePath(string path, bool lowerCase)
        {
            return UrlEncodePath(path, lowerCase, true);
        }

        public static string UrlEncodePath(string path, bool lowerCase, bool encodeSlash)
        {
            return UrlEncodePathInternal(path, lowerCase, true, encodeSlash);
        }

        private static string UrlEncodePathSpacesAndUtf8Only(string path, bool lowerCase)
        {
            return UrlEncodePathInternal(path, lowerCase, false, false);
        }

        private static string UrlEncodePathInternal(string path, bool lowerCase, bool encodeColon, bool encodeSlash)
        {
            if (string.IsNullOrEmpty(path)) return path;
            byte[] utf8 = Encoding.UTF8.GetBytes(path);
            StringBuilder sb = new StringBuilder(utf8.Length * 2);
            string hexFmt = lowerCase ? "x2" : "X2";
            for (int i = 0; i < utf8.Length; i++)
            {
                byte b = utf8[i];
                if (b == ':')
                {
                    sb.Append(encodeColon ? (lowerCase ? "%3a" : "%3A") : ":");
                }
                else if (b == '/')
                {
                    sb.Append(encodeSlash ? (lowerCase ? "%2f" : "%2F") : "/");
                }
                else if (b == '\\')
                {
                    sb.Append(encodeSlash ? (lowerCase ? "%5c" : "%5C") : "\\");
                }
                else if (b == ' ' || b == '%' || b == '#' || b == '?' || b < 0x20 || b >= 0x7F)
                {
                    sb.Append('%');
                    sb.Append(b.ToString(hexFmt));
                }
                else
                {
                    sb.Append((char)b);
                }
            }
            return sb.ToString();
        }
    }
}
