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
    // Strictly C# 5.0 (.NET Framework 4.x)
    // ══════════════════════════════════════════════════════════════════════

    public static class StreamingZipIndexReader
    {
        public delegate void ZipArchiveCapabilityScanHook(
            ZipArchive archive,
            BackupArchiveIndex index,
            string currentHomePath,
            string vaultPath);

        /// <summary>
        /// Optional hook registered by the Capabilities/Services layer to populate
        /// fine-grained CapabilityItem entries during ReadIndex without coupling Storage to Capabilities.
        /// </summary>
        public static ZipArchiveCapabilityScanHook CapabilityScanHook;

        private static readonly HashSet<string> WindowsReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>
        /// Validates that a zip archive entry path is safe against ZipSlip traversal,
        /// rooted/drive paths, control characters, and Windows reserved device names.
        /// </summary>
        public static bool IsSafeZipEntryPath(string rawEntryName)
        {
            if (string.IsNullOrWhiteSpace(rawEntryName)) return false;
            if (rawEntryName.IndexOf('\0') >= 0) return false;

            string normalized = rawEntryName.Replace('\\', '/').Trim();
            if (normalized.Length == 0) return false;
            if (normalized.StartsWith("/", StringComparison.Ordinal)) return false;
            if (normalized.Length >= 2 && normalized[1] == ':') return false;

            string[] segments = normalized.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                string seg = segments[i];
                if (string.IsNullOrEmpty(seg)) continue;
                if (seg == "." || seg == "..") return false;
                if (seg.IndexOf(':') >= 0) return false;
                if (WindowsReservedNames.Contains(seg)) return false;
                int dotIdx = seg.IndexOf('.');
                if (dotIdx > 0 && WindowsReservedNames.Contains(seg.Substring(0, dotIdx))) return false;
                for (int c = 0; c < seg.Length; c++)
                {
                    if (seg[c] < 0x20) return false;
                }
            }
            return true;
        }

        public static BackupArchiveIndex ReadIndex(string zipPath)
        {
            return ReadIndex(zipPath, null, null);
        }

        public static BackupArchiveIndex ReadIndex(string zipPath, string homePath, string vaultPath)
        {
            if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
                throw new FileNotFoundException("备份归档文件不存在: " + zipPath);

            Stopwatch sw = Stopwatch.StartNew();
            BackupArchiveIndex index = new BackupArchiveIndex();
            FileInfo fi = new FileInfo(zipPath);
            index.TotalZipSizeBytes = fi.Length;

            byte[] pbData = null;
            byte[] cliPbData = null;
            Dictionary<string, string> projectJsons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> annotTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using (FileStream fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                for (int i = 0; i < archive.Entries.Count; i++)
                {
                    ZipArchiveEntry entry = archive.Entries[i];
                    if (entry == null || string.IsNullOrEmpty(entry.FullName)) continue;

                    // ZipSlip path traversal guard (validate raw entry.FullName before stripping leading slashes)
                    if (!IsSafeZipEntryPath(entry.FullName)) continue;

                    string rawName = entry.FullName.Replace('\\', '/').TrimStart('/');
                    if (string.IsNullOrEmpty(rawName) || rawName.EndsWith("/", StringComparison.Ordinal)) continue;

                    string logical = rawName.StartsWith("data/", StringComparison.OrdinalIgnoreCase)
                        ? rawName.Substring(5)
                        : rawName;

                    if (string.Equals(logical, "metadata.json", StringComparison.OrdinalIgnoreCase))
                    {
                        using (StreamReader sr = new StreamReader(entry.Open(), Encoding.UTF8))
                        {
                            string json = sr.ReadToEnd();
                            ParseManifestMetadata(json, index);
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
                    else if (IsUserSettingsLogicalPath(logical))
                    {
                        index.HasUserSettings = true;
                        index.UserSettingsEntries.Add(logical);
                    }
                    else if (string.Equals(logical, "antigravity/agyhub_summaries_proto.pb", StringComparison.OrdinalIgnoreCase))
                    {
                        pbData = ReadEntryBytes(entry);
                    }
                    else if (string.Equals(logical, "antigravity-cli/agyhub_summaries_proto.pb", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(logical, "cli/agyhub_summaries_proto.pb", StringComparison.OrdinalIgnoreCase))
                    {
                        index.HasCliSessions = true;
                        cliPbData = ReadEntryBytes(entry);
                    }
                    else if (TryExtractConversationDbCid(logical, "antigravity/conversations/", out rawName))
                    {
                        index.ExistingConvoDbFiles.Add(rawName);
                    }
                    else if (TryExtractConversationDbCid(logical, "antigravity-cli/conversations/", out rawName) ||
                             TryExtractConversationDbCid(logical, "cli/conversations/", out rawName))
                    {
                        index.HasCliSessions = true;
                        index.ExistingConvoDbFiles.Add(rawName);
                        index.CliConversationIds.Add(rawName);
                    }
                    else if (TryExtractBrainCid(logical, "antigravity/brain/", out rawName))
                    {
                        index.ExistingBrainDirs.Add(rawName);
                    }
                    else if (TryExtractBrainCid(logical, "antigravity-cli/brain/", out rawName) ||
                             TryExtractBrainCid(logical, "cli/brain/", out rawName))
                    {
                        index.HasCliSessions = true;
                        index.ExistingBrainDirs.Add(rawName);
                        index.CliConversationIds.Add(rawName);
                    }
                    else if (TryExtractAnnotationTitle(entry, logical, "antigravity/annotations/", annotTitles))
                    {
                        // Collected into annotTitles
                    }
                    else if (TryExtractAnnotationTitle(entry, logical, "antigravity-cli/annotations/", annotTitles) ||
                             TryExtractAnnotationTitle(entry, logical, "cli/annotations/", annotTitles))
                    {
                        index.HasCliSessions = true;
                    }
                    else if (logical.StartsWith("cli/", StringComparison.OrdinalIgnoreCase) ||
                             logical.StartsWith("antigravity-cli/", StringComparison.OrdinalIgnoreCase))
                    {
                        index.HasCliSessions = true;
                    }
                    else if (!logical.StartsWith("antigravity/", StringComparison.OrdinalIgnoreCase))
                    {
                        if (logical.StartsWith("config/rules/", StringComparison.OrdinalIgnoreCase) ||
                            logical.Equals("GEMINI.md", StringComparison.OrdinalIgnoreCase) ||
                            logical.Equals("AGENTS.md", StringComparison.OrdinalIgnoreCase) ||
                            logical.Equals("CLAUDE.md", StringComparison.OrdinalIgnoreCase) ||
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

                // 提取 IDE 与 CLI 会话元数据
                if (pbData != null && pbData.Length > 0)
                {
                    index.ExtractedMetadataSizeBytes += pbData.Length;
                    List<ConversationSummaryInfo> ideConvs = SessionMetadataExtractor.ExtractSummaries(pbData, index.Projects);
                    index.Conversations.AddRange(ideConvs);
                }

                if (cliPbData != null && cliPbData.Length > 0)
                {
                    index.ExtractedMetadataSizeBytes += cliPbData.Length;
                    List<ConversationSummaryInfo> cliConvs = SessionMetadataExtractor.ExtractSummaries(cliPbData, index.Projects);
                    for (int i = 0; i < cliConvs.Count; i++)
                    {
                        cliConvs[i].IsCliSession = true;
                        if (!string.IsNullOrEmpty(cliConvs[i].ConversationId))
                        {
                            index.CliConversationIds.Add(cliConvs[i].ConversationId.Trim());
                        }
                        index.Conversations.Add(cliConvs[i]);
                    }
                }

                if (index.CliConversationIds.Count > 0 && index.Conversations != null)
                {
                    for (int i = 0; i < index.Conversations.Count; i++)
                    {
                        ConversationSummaryInfo conv = index.Conversations[i];
                        if (conv != null && !string.IsNullOrEmpty(conv.ConversationId) &&
                            index.CliConversationIds.Contains(conv.ConversationId.Trim()))
                        {
                            conv.IsCliSession = true;
                        }
                    }
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

                // 解析运行时主目录与 Vault/Gemini 目录（FIX P1-7: 尊重显式传入的 homePath / vaultPath）
                string curHome = !string.IsNullOrWhiteSpace(homePath)
                    ? homePath
                    : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrEmpty(curHome)) curHome = index.SourceHome ?? string.Empty;

                string localGemini = !string.IsNullOrWhiteSpace(vaultPath)
                    ? vaultPath
                    : (!string.IsNullOrEmpty(curHome) ? Path.Combine(curHome, ".gemini") : null);

                if (CapabilityScanHook != null)
                {
                    CapabilityScanHook(archive, index, curHome, localGemini);
                }
            }

            sw.Stop();
            index.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            return index;
        }

        private static void ParseManifestMetadata(string json, BackupArchiveIndex index)
        {
            if (string.IsNullOrEmpty(json) || index == null) return;
            object parsed;
            if (!UnifiedJson.TryParse(json, out parsed)) return;

            Dictionary<string, object> rootDict = UnifiedJson.AsDict(parsed);
            index.SourceHome = ExtractStringFromNode(parsed, rootDict, "source_home");
            index.SourceUser = ExtractStringFromNode(parsed, rootDict, "source_user");
            index.BackupTime = ExtractStringFromNode(parsed, rootDict, "backup_time");
            index.Version = ExtractStringFromNode(parsed, rootDict, "version");

            string tier = ExtractStringFromNode(parsed, rootDict, "backup_tier");
            if (string.IsNullOrEmpty(tier)) tier = ExtractStringFromNode(parsed, rootDict, "backupTier");
            if (string.IsNullOrEmpty(tier)) tier = ExtractStringFromNode(parsed, rootDict, "tier");
            if (string.IsNullOrEmpty(tier) && rootDict != null)
            {
                object tierObj;
                if (TryGetScalarValueCaseInsensitive(rootDict, "backup_tier", out tierObj) ||
                    TryGetScalarValueCaseInsensitive(rootDict, "backupTier", out tierObj) ||
                    TryGetScalarValueCaseInsensitive(rootDict, "tier", out tierObj))
                {
                    tier = Convert.ToString(tierObj, System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            index.BackupTier = tier ?? string.Empty;

            if (rootDict != null)
            {
                if (UnifiedJson.GetBool(rootDict, "has_user_settings", false) ||
                    UnifiedJson.GetBool(rootDict, "hasUserSettings", false) ||
                    rootDict.ContainsKey("userSettings") ||
                    rootDict.ContainsKey("user_settings"))
                {
                    index.HasUserSettings = true;
                }
                if (UnifiedJson.GetBool(rootDict, "has_cli", false) ||
                    UnifiedJson.GetBool(rootDict, "hasCli", false) ||
                    UnifiedJson.GetBool(rootDict, "has_cli_sessions", false) ||
                    UnifiedJson.GetBool(rootDict, "hasCliSessions", false) ||
                    rootDict.ContainsKey("cli"))
                {
                    index.HasCliSessions = true;
                }
            }
        }

        private static string ExtractStringFromNode(object parsed, Dictionary<string, object> rootDict, string key)
        {
            if (rootDict != null)
            {
                string topHit;
                if (DirectStreamArchiver.TryGetStringValueCaseInsensitive(rootDict, key, out topHit))
                    return topHit;
            }
            return DirectStreamArchiver.FindFirstNestedStringValue(parsed, key, 0) ?? string.Empty;
        }

        private static bool TryGetScalarValueCaseInsensitive(Dictionary<string, object> dict, string key, out object scalarVal)
        {
            scalarVal = null;
            if (dict == null || key == null) return false;
            foreach (KeyValuePair<string, object> kvp in dict)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase) &&
                    kvp.Value != null &&
                    !(kvp.Value is Dictionary<string, object>) &&
                    !(kvp.Value is List<object>))
                {
                    scalarVal = kvp.Value;
                    return true;
                }
            }
            return false;
        }

        private static bool IsUserSettingsLogicalPath(string logical)
        {
            if (string.IsNullOrEmpty(logical)) return false;
            return string.Equals(logical, "userSettings.json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(logical, "user_settings.json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(logical, "antigravity/userSettings.json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(logical, "antigravity/user_settings.json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(logical, "config/settings.json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(logical, "config/userSettings.json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(logical, "config/user_settings.json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(logical, "config/keybindings.json", StringComparison.OrdinalIgnoreCase) ||
                   logical.StartsWith("userSettings/", StringComparison.OrdinalIgnoreCase) ||
                   logical.StartsWith("user_settings/", StringComparison.OrdinalIgnoreCase) ||
                   logical.StartsWith("antigravity/userSettings/", StringComparison.OrdinalIgnoreCase) ||
                   logical.StartsWith("antigravity/user_settings/", StringComparison.OrdinalIgnoreCase) ||
                   logical.StartsWith("config/userSettings/", StringComparison.OrdinalIgnoreCase) ||
                   logical.StartsWith("config/user_settings/", StringComparison.OrdinalIgnoreCase);
        }

        private static byte[] ReadEntryBytes(ZipArchiveEntry entry)
        {
            using (MemoryStream ms = new MemoryStream())
            using (Stream s = entry.Open())
            {
                s.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private static bool TryExtractConversationDbCid(string logical, string prefix, out string cid)
        {
            cid = null;
            if (!logical.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            if (!logical.EndsWith(".db", StringComparison.OrdinalIgnoreCase) &&
                !logical.EndsWith(".db-wal", StringComparison.OrdinalIgnoreCase))
                return false;

            string fn = Path.GetFileName(logical);
            string candidate = fn;
            if (candidate.EndsWith(".db-wal", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(0, candidate.Length - 7);
            else if (candidate.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(0, candidate.Length - 3);

            if (!SessionMetadataExtractor.IsLikelyConversationId(candidate)) return false;
            cid = candidate.Trim();
            return true;
        }

        private static bool TryExtractBrainCid(string logical, string prefix, out string cid)
        {
            cid = null;
            if (!logical.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            string sub = logical.Substring(prefix.Length);
            int slash = sub.IndexOf('/');
            string candidate = slash >= 0 ? sub.Substring(0, slash) : sub;
            if (!SessionMetadataExtractor.IsLikelyConversationId(candidate)) return false;
            cid = candidate.Trim();
            return true;
        }

        private static bool TryExtractAnnotationTitle(
            ZipArchiveEntry entry,
            string logical,
            string prefix,
            Dictionary<string, string> annotTitles)
        {
            if (!logical.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !logical.EndsWith(".pbtxt", StringComparison.OrdinalIgnoreCase))
                return false;

            string rel = logical.Substring(prefix.Length);
            if (rel.IndexOf('/') >= 0) return false;

            string cid = Path.GetFileNameWithoutExtension(rel);
            if (!SessionMetadataExtractor.IsLikelyConversationId(cid)) return false;

            using (StreamReader sr = new StreamReader(entry.Open(), Encoding.UTF8))
            {
                string rawPbtxt = sr.ReadToEnd();
                string extracted = PbtxtTitleExtractor.ExtractTitle(rawPbtxt);
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    string trimmedTitle = extracted.Trim();
                    if (!IncrementalProtoShardMerger.IsSyntheticPlaceholderTitle(trimmedTitle))
                    {
                        annotTitles[cid.Trim()] = trimmedTitle;
                    }
                }
            }
            return true;
        }
    }

    /// <summary>
    /// 流式直接归档与路径转码工具类
    /// </summary>
    public static class DirectStreamArchiver
    {
        public static string ExtractJsonValue(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
                return string.Empty;

            object parsed;
            if (!UnifiedJson.TryParse(json, out parsed))
                return string.Empty;

            Dictionary<string, object> rootDict = UnifiedJson.AsDict(parsed);
            if (rootDict != null)
            {
                string topHit;
                if (TryGetStringValueCaseInsensitive(rootDict, key, out topHit))
                    return topHit;
            }

            string nestedHit = FindFirstNestedStringValue(parsed, key, 0);
            return nestedHit ?? string.Empty;
        }

        public static List<string> ExtractAllJsonValues(string json, string key)
        {
            List<string> results = new List<string>();
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
                return results;

            object parsed;
            if (!UnifiedJson.TryParse(json, out parsed))
                return results;

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectAllJsonStringValues(parsed, key, results, seen, 0);
            return results;
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

        internal static bool TryGetStringValueCaseInsensitive(Dictionary<string, object> dict, string key, out string strVal)
        {
            strVal = null;
            if (dict == null || key == null) return false;

            object exactVal;
            if (dict.TryGetValue(key, out exactVal))
            {
                string s = exactVal as string;
                if (s != null)
                {
                    strVal = s;
                    return true;
                }
            }

            foreach (KeyValuePair<string, object> kvp in dict)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    string s = kvp.Value as string;
                    if (s != null)
                    {
                        strVal = s;
                        return true;
                    }
                }
            }
            return false;
        }

        internal static string FindFirstNestedStringValue(object node, string key, int depth)
        {
            if (node == null || depth > 128) return null;

            Dictionary<string, object> dict = UnifiedJson.AsDict(node);
            if (dict != null)
            {
                string direct;
                if (TryGetStringValueCaseInsensitive(dict, key, out direct))
                    return direct;

                foreach (KeyValuePair<string, object> kvp in dict)
                {
                    string sub = FindFirstNestedStringValue(kvp.Value, key, depth + 1);
                    if (sub != null) return sub;
                }
                return null;
            }

            List<object> list = UnifiedJson.AsList(node);
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    string sub = FindFirstNestedStringValue(list[i], key, depth + 1);
                    if (sub != null) return sub;
                }
            }
            return null;
        }

        private static void CollectAllJsonStringValues(object node, string key, List<string> results, HashSet<string> seen, int depth)
        {
            if (node == null || depth > 128) return;

            Dictionary<string, object> dict = UnifiedJson.AsDict(node);
            if (dict != null)
            {
                foreach (KeyValuePair<string, object> kvp in dict)
                {
                    if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        string str = kvp.Value as string;
                        if (str != null)
                        {
                            if (!string.IsNullOrWhiteSpace(str) && seen.Add(str))
                                results.Add(str);
                        }
                        else
                        {
                            List<object> arr = UnifiedJson.AsList(kvp.Value);
                            if (arr != null)
                            {
                                for (int i = 0; i < arr.Count; i++)
                                {
                                    string elem = arr[i] as string;
                                    if (!string.IsNullOrWhiteSpace(elem) && seen.Add(elem))
                                        results.Add(elem);
                                    else if (elem == null)
                                        CollectAllJsonStringValues(arr[i], key, results, seen, depth + 1);
                                }
                            }
                            else
                            {
                                CollectAllJsonStringValues(kvp.Value, key, results, seen, depth + 1);
                            }
                        }
                    }
                    else
                    {
                        CollectAllJsonStringValues(kvp.Value, key, results, seen, depth + 1);
                    }
                }
                return;
            }

            List<object> list = UnifiedJson.AsList(node);
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    CollectAllJsonStringValues(list[i], key, results, seen, depth + 1);
                }
            }
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
