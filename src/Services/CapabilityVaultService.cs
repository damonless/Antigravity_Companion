using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // CAPABILITY VAULT SERVICE & PORTABLE VAULT RESOLUTION (P0-5 & Decision 4B)
    // Strictly C# 5.0 (.NET Framework 4.x)
    // ══════════════════════════════════════════════════════════════════════

    public enum CapabilityKind
    {
        Plugin,
        Skill,
        Rule,
        McpServer,
        Hook
    }

    public enum ViewFilterMode
    {
        All,
        CapabilitiesOnly,
        SessionsOnly
    }

    public enum RightPaneSourceMode
    {
        BuiltInRepository,
        BackupArchive
    }

    public partial class BackupArchiveIndex
    {
        public List<CapabilityItem> Capabilities = new List<CapabilityItem>();
    }

    public class CapabilityItem
    {
        public CapabilityKind Kind;
        public string Id;
        public string DisplayName;
        public string Description;
        public string ContentSignature;
        public string RawJsonFragment; // Normalized with {{HOME}}
        public Dictionary<string, byte[]> FilesMap; // Relative path -> bytes (large files >1MB may be null + mapped in SourceFilePaths)
        public Dictionary<string, string> SourceFilePaths; // Relative path -> disk source path (for lazy streaming of >1MB assets)
        public List<string> CompanionScriptNames;
        public List<string> ReferencedLocalPaths;
        public List<string> MissingLocalPaths;
        public DateTime LastModified;
        public string PreviewText;

        public CapabilityItem()
        {
            FilesMap = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            SourceFilePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            CompanionScriptNames = new List<string>();
            ReferencedLocalPaths = new List<string>();
            MissingLocalPaths = new List<string>();
            LastModified = DateTime.MinValue;
            Description = string.Empty;
            RawJsonFragment = string.Empty;
            PreviewText = string.Empty;
            ContentSignature = string.Empty;
        }

        public string UniqueKey
        {
            get { return Kind.ToString() + ":" + (Id ?? string.Empty); }
        }

        public bool HasMissingPaths
        {
            get { return MissingLocalPaths != null && MissingLocalPaths.Count > 0; }
        }

        public byte[] GetFileBytes(string relKey)
        {
            if (string.IsNullOrEmpty(relKey)) return null;

            byte[] cached = null;
            bool hasKey = FilesMap != null && FilesMap.TryGetValue(relKey, out cached);
            if (hasKey && cached != null)
                return cached;

            string srcPath;
            if (SourceFilePaths != null && SourceFilePaths.TryGetValue(relKey, out srcPath) &&
                !string.IsNullOrEmpty(srcPath) && File.Exists(srcPath))
            {
                using (FileStream fs = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (MemoryStream ms = new MemoryStream())
                {
                    fs.CopyTo(ms);
                    return ms.ToArray();
                }
            }

            return cached;
        }

        public bool TryGetDiskSourcePath(string relKey, out string srcPath)
        {
            srcPath = null;
            return SourceFilePaths != null &&
                   !string.IsNullOrEmpty(relKey) &&
                   SourceFilePaths.TryGetValue(relKey, out srcPath) &&
                   !string.IsNullOrEmpty(srcPath) &&
                   File.Exists(srcPath);
        }

        public static string GetCategoryTitle(CapabilityKind kind)
        {
            switch (kind)
            {
                case CapabilityKind.Plugin: return "[插件包 Plugins]";
                case CapabilityKind.Skill: return "[独立技能 Skills]";
                case CapabilityKind.Rule: return "[全局与目录规则 Rules]";
                case CapabilityKind.McpServer: return "[MCP 服务节点 MCP Servers]";
                case CapabilityKind.Hook: return "[生命周期钩子 Hooks]";
                default: return "[其他能力]";
            }
        }

        public static string GetKindShortTag(CapabilityKind kind)
        {
            switch (kind)
            {
                case CapabilityKind.Plugin: return "Plugin";
                case CapabilityKind.Skill: return "Skill";
                case CapabilityKind.Rule: return "Rule";
                case CapabilityKind.McpServer: return "MCP";
                case CapabilityKind.Hook: return "Hook";
                default: return "Cap";
            }
        }
    }

    public static class CapabilityPathNormalizer
    {
        public const string HomePlaceholder = "{{HOME}}";

        public static string NormalizeToHomePlaceholder(string text, string homePath)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(homePath)) return text ?? string.Empty;
            string cleanHome = homePath.TrimEnd('\\', '/');
            if (string.IsNullOrEmpty(cleanHome)) return text;

            return DirectStreamArchiver.RemapAllPathFormats(text, cleanHome, HomePlaceholder);
        }

        public static string ExpandHomePlaceholder(string text, string targetHomePath)
        {
            return ExpandHomePlaceholder(text, targetHomePath, false);
        }

        public static string ExpandHomePlaceholder(string text, string targetHomePath, bool isJsonContent)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(targetHomePath)) return text ?? string.Empty;
            if (text.IndexOf(HomePlaceholder, StringComparison.OrdinalIgnoreCase) < 0) return text;

            string cleanHome = targetHomePath.TrimEnd('\\', '/');
            string winSingle = cleanHome.Replace('/', '\\');
            string fwd = cleanHome.Replace('\\', '/');
            string uriEncoded = DirectStreamArchiver.UrlEncodePath(fwd, true, false);

            string winForJson = UnifiedJson.EscapeString(winSingle);
            string fwdForJson = UnifiedJson.EscapeString(fwd);

            string res = text;
            // 1. file:///{{HOME}}
            res = ReplaceIgnoreCase(res, "file:///" + HomePlaceholder, "file:///" + uriEncoded);
            // 2. {{HOME}}\\ (escaped JSON backslash)
            res = ReplaceIgnoreCase(res, HomePlaceholder + "\\\\", winForJson + "\\\\");
            // 3. {{HOME}}/ (forward slash)
            res = ReplaceIgnoreCase(res, HomePlaceholder + "/", (isJsonContent ? fwdForJson : fwd) + "/");
            // 4. {{HOME}}\ (single backslash: inside JSON must escape backslashes so "\t"/"\n" don't corrupt JSON)
            res = ReplaceIgnoreCase(
                res,
                HomePlaceholder + "\\",
                isJsonContent ? (winForJson + "\\\\") : (winSingle + "\\")
            );
            // 5. Standalone {{HOME}}
            res = ReplaceIgnoreCase(res, HomePlaceholder, isJsonContent ? fwdForJson : fwd);

            return res;
        }

        public static void DetectReferencedAndMissingPaths(
            string rawNormalizedJson,
            string currentHomePath,
            string localGeminiPath,
            List<string> bundledCompanionScripts,
            out List<string> referencedPaths,
            out List<string> missingPaths)
        {
            referencedPaths = new List<string>();
            missingPaths = new List<string>();
            if (string.IsNullOrEmpty(rawNormalizedJson)) return;

            string expanded = ExpandHomePlaceholder(rawNormalizedJson, currentHomePath, true);
            List<string> literals = CapabilityVaultService.ExtractAllStringLiterals(expanded);
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < literals.Count; i++)
            {
                string rawLit = literals[i].Trim();
                if (string.IsNullOrEmpty(rawLit)) continue;

                string lit = ExpandEnvAndTildeInLiteral(rawLit, currentHomePath);

                if (LooksLikeExplicitLocalPath(lit))
                {
                    AddAndCheckPath(lit, localGeminiPath, currentHomePath, bundledCompanionScripts, seen, referencedPaths, missingPaths);
                }
                else
                {
                    MatchCollection dQuotedMatches = Regex.Matches(lit, "\"([a-zA-Z]:(?:\\\\|/)[^\"]+)\"");
                    for (int m = 0; m < dQuotedMatches.Count; m++)
                    {
                        string p = dQuotedMatches[m].Groups[1].Value;
                        if (LooksLikeExplicitLocalPath(p))
                        {
                            AddAndCheckPath(p, localGeminiPath, currentHomePath, bundledCompanionScripts, seen, referencedPaths, missingPaths);
                        }
                    }

                    MatchCollection sQuotedMatches = Regex.Matches(lit, "'([a-zA-Z]:(?:\\\\|/)[^']+)'");
                    for (int m = 0; m < sQuotedMatches.Count; m++)
                    {
                        string p = sQuotedMatches[m].Groups[1].Value;
                        if (LooksLikeExplicitLocalPath(p))
                        {
                            AddAndCheckPath(p, localGeminiPath, currentHomePath, bundledCompanionScripts, seen, referencedPaths, missingPaths);
                        }
                    }

                    MatchCollection unquotedMatches = Regex.Matches(lit, "(?:^|[\\s=])([a-zA-Z]:(?:\\\\|/)[^\\s\"']+)");
                    for (int m = 0; m < unquotedMatches.Count; m++)
                    {
                        string p = unquotedMatches[m].Groups[1].Value;
                        if (LooksLikeExplicitLocalPath(p))
                        {
                            AddAndCheckPath(p, localGeminiPath, currentHomePath, bundledCompanionScripts, seen, referencedPaths, missingPaths);
                        }
                    }
                }
            }
        }

        private static string ExpandEnvAndTildeInLiteral(string lit, string currentHomePath)
        {
            if (string.IsNullOrEmpty(lit)) return lit ?? string.Empty;
            string result = lit;

            if (!string.IsNullOrEmpty(currentHomePath))
            {
                string cleanHome = currentHomePath.TrimEnd('\\', '/').Replace('/', '\\');
                result = Regex.Replace(
                    result,
                    "(?<=^|[\\s\"'=])~(?=[\\\\/])",
                    cleanHome.Replace("$", "$$")
                );
            }

            if (result.IndexOf('%') >= 0)
            {
                result = Regex.Replace(result, "%([A-Za-z0-9_().-]+)%", delegate(Match m)
                {
                    string val = Environment.GetEnvironmentVariable(m.Groups[1].Value);
                    return !string.IsNullOrEmpty(val) ? val : m.Value;
                });
            }

            return result;
        }

        private static bool LooksLikeExplicitLocalPath(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 4) return false;
            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return false;

            if (s.IndexOf('*') >= 0 || s.IndexOf('?') >= 0) return false;
            if (Regex.IsMatch(s, "%[A-Za-z0-9_().-]+%")) return false;

            char c0 = s[0];
            if (!((c0 >= 'a' && c0 <= 'z') || (c0 >= 'A' && c0 <= 'Z'))) return false;
            if (s[1] != ':') return false;
            if (s[2] != '\\' && s[2] != '/') return false;

            if (s.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return false;
            return true;
        }

        private static void AddAndCheckPath(
            string rawPath,
            string localGeminiPath,
            string currentHomePath,
            List<string> bundledScripts,
            HashSet<string> seen,
            List<string> referencedPaths,
            List<string> missingPaths)
        {
            string normWin = rawPath.Replace('/', '\\').Trim();
            if (!seen.Add(normWin)) return;

            referencedPaths.Add(normWin);

            if (bundledScripts != null && bundledScripts.Count > 0)
            {
                string effGemini = !string.IsNullOrEmpty(localGeminiPath)
                    ? localGeminiPath
                    : (!string.IsNullOrEmpty(currentHomePath) ? Path.Combine(currentHomePath, ".gemini") : null);

                if (!string.IsNullOrEmpty(effGemini))
                {
                    try
                    {
                        string expectedConfigDir = Path.GetFullPath(Path.Combine(effGemini, "config")).TrimEnd('\\', '/') + "\\";
                        string normFull = Path.GetFullPath(normWin);
                        if (normFull.StartsWith(expectedConfigDir, StringComparison.OrdinalIgnoreCase))
                        {
                            string fn = Path.GetFileName(normWin);
                            for (int i = 0; i < bundledScripts.Count; i++)
                            {
                                if (string.Equals(fn, bundledScripts[i], StringComparison.OrdinalIgnoreCase))
                                    return;
                            }
                        }
                    }
                    catch { }
                }
            }

            if (!File.Exists(normWin) && !Directory.Exists(normWin))
            {
                missingPaths.Add(normWin);
            }
        }

        public static string ReplaceIgnoreCase(string input, string oldVal, string newVal)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(oldVal) ||
                string.Equals(oldVal, newVal, StringComparison.OrdinalIgnoreCase))
                return input;

            int idx = input.IndexOf(oldVal, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return input;

            StringBuilder sb = new StringBuilder(input.Length);
            int lastIdx = 0;
            while (idx >= 0)
            {
                sb.Append(input, lastIdx, idx - lastIdx);
                sb.Append(newVal);
                lastIdx = idx + oldVal.Length;
                idx = input.IndexOf(oldVal, lastIdx, StringComparison.OrdinalIgnoreCase);
            }
            sb.Append(input, lastIdx, input.Length - lastIdx);
            return sb.ToString();
        }
    }

    /// <summary>
    /// 模块化能力资产扫描、安全编码、Portable Vault 解析、Comment-Preserving 合流与维护服务
    /// </summary>
    public static class CapabilityVaultService
    {
        private const int LazyLargeFileThresholdBytes = 1024 * 1024;
        private const int MaxDirectoryWalkDepth = 20;
        private const int MaxTimestampedBackups = 5;
        private const string LegacyMigrationMarkerFile = ".legacy_repo_migrated";

        private static readonly HashSet<string> WindowsReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        private static readonly HashSet<string> ExcludedSubdirNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "node_modules", ".venv", "venv", "__pycache__", ".git", ".pytest_cache", ".mypy_cache", ".ruff_cache"
        };

        private static readonly HashSet<string> AllowedRootRuleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GEMINI.md", "AGENTS.md", ".geminirules"
        };

        private const string DefaultCleanManifestJson =
            "{\r\n" +
            "  \"schemaVersion\": 2,\r\n" +
            "  \"vaultType\": \"portable\",\r\n" +
            "  \"description\": \"Clean generic Antigravity Companion Vault template\"\r\n" +
            "}\r\n";

        private const string DefaultCleanRenameRulesJson =
            "{\r\n" +
            "  \"version\": 2,\r\n" +
            "  \"defaultFormat\": \"【{project}】{topic}\",\r\n" +
            "  \"preserveExistingTag\": true,\r\n" +
            "  \"includeGitBranch\": true,\r\n" +
            "  \"multiRepoRules\": [],\r\n" +
            "  \"projectAliases\": {}\r\n" +
            "}\r\n";

        static CapabilityVaultService()
        {
            EnsureZipCapabilityHookRegistered();
        }

        public static void EnsureZipCapabilityHookRegistered()
        {
            if (StreamingZipIndexReader.CapabilityScanHook == null)
            {
                StreamingZipIndexReader.CapabilityScanHook = delegate(
                    ZipArchive archive,
                    BackupArchiveIndex index,
                    string currentHomePath,
                    string vaultPath)
                {
                    if (index == null) return;
                    index.Capabilities = ScanZipArchiveCapabilities(archive, index.SourceHome, currentHomePath, vaultPath);
                };
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // Decision 4B: Portable Vault Resolution & Legacy Repository Migration
        // ══════════════════════════════════════════════════════════════════

        private static string ResolveEffectiveAppBaseDir()
        {
            string baseDir = (AppDomain.CurrentDomain.BaseDirectory ?? string.Empty).TrimEnd('\\', '/');
            if (string.IsNullOrEmpty(baseDir)) return Directory.GetCurrentDirectory();
            string dirName = Path.GetFileName(baseDir);
            if (string.Equals(dirName, "tests", StringComparison.OrdinalIgnoreCase))
            {
                string parent = Path.GetDirectoryName(baseDir);
                if (!string.IsNullOrEmpty(parent)) baseDir = parent;
            }
            return baseDir;
        }

        public static string ResolveDefaultVaultPath(string explicitPath, string homePath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                string trimmed = explicitPath.Trim();
                try
                {
                    return Path.GetFullPath(trimmed);
                }
                catch
                {
                    return trimmed;
                }
            }

            string baseDir = ResolveEffectiveAppBaseDir();
            string adjacentVault = Path.Combine(baseDir, "Vault");
            if (Directory.Exists(adjacentVault))
            {
                return adjacentVault;
            }

            string effectiveHome = !string.IsNullOrWhiteSpace(homePath)
                ? homePath.Trim()
                : WorkspaceService.ResolveDefaultHomePath();
            string userVault = Path.Combine(effectiveHome, ".gemini", "companion-vault");
            EnsurePortableVaultInitialized(userVault, baseDir);
            return userVault;
        }

        public static string ResolveDefaultRepositoryPath()
        {
            return ResolveDefaultVaultPath(null, WorkspaceService.ResolveDefaultHomePath());
        }

        private static void EnsurePortableVaultInitialized(string vaultDir, string appBaseDir)
        {
            if (string.IsNullOrEmpty(vaultDir)) return;
            try
            {
                if (!Directory.Exists(vaultDir))
                    Directory.CreateDirectory(vaultDir);

                string manifestPath = Path.Combine(vaultDir, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    string templateManifest = ResolveTemplateFileContent(appBaseDir, "manifest.json", DefaultCleanManifestJson);
                    SessionLifecycleService.WriteUtf8TextAtomicWithBackup(manifestPath, templateManifest, false, null);
                }

                string rulesPath = Path.Combine(vaultDir, "rename_rules.json");
                if (!File.Exists(rulesPath))
                {
                    string templateRules = ResolveTemplateFileContent(appBaseDir, "rename_rules.json", DefaultCleanRenameRulesJson);
                    SessionLifecycleService.WriteUtf8TextAtomicWithBackup(rulesPath, templateRules, false, null);
                }
            }
            catch
            {
                // Best-effort vault bootstrap
            }
        }

        private static string ResolveTemplateFileContent(string appBaseDir, string fileName, string fallbackContent)
        {
            if (!string.IsNullOrEmpty(appBaseDir))
            {
                string tplPath = Path.Combine(appBaseDir, "templates", "vault-template", fileName);
                if (File.Exists(tplPath))
                {
                    try
                    {
                        string text = ReadAllTextShared(tplPath);
                        if (!string.IsNullOrWhiteSpace(text)) return text;
                    }
                    catch { }
                }
            }
            return fallbackContent;
        }

        public static bool DetectLegacyRepositoryMigrationNeeded(string baseDir, string vaultDir)
        {
            if (string.IsNullOrEmpty(vaultDir)) return false;

            string effectiveBase = !string.IsNullOrWhiteSpace(baseDir) ? baseDir.Trim() : ResolveEffectiveAppBaseDir();
            string legacyRepoDir = Path.Combine(effectiveBase, "Repository");
            if (!Directory.Exists(legacyRepoDir)) return false;

            try
            {
                string fullLegacy = Path.GetFullPath(legacyRepoDir).TrimEnd('\\', '/');
                string fullVault = Path.GetFullPath(vaultDir).TrimEnd('\\', '/');
                if (string.Equals(fullLegacy, fullVault, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            catch { }

            if (File.Exists(Path.Combine(vaultDir, LegacyMigrationMarkerFile)))
                return false;

            bool legacyHasContent = DirectoryHasAnyFiles(Path.Combine(legacyRepoDir, "plugins")) ||
                                    DirectoryHasAnyFiles(Path.Combine(legacyRepoDir, "skills")) ||
                                    DirectoryHasAnyFiles(Path.Combine(legacyRepoDir, "rules")) ||
                                    DirectoryHasAnyFiles(Path.Combine(legacyRepoDir, "mcp")) ||
                                    DirectoryHasAnyFiles(Path.Combine(legacyRepoDir, "hooks")) ||
                                    File.Exists(Path.Combine(legacyRepoDir, "rename_rules.json"));
            if (!legacyHasContent) return false;

            bool vaultAlreadyPopulated = DirectoryHasAnyFiles(Path.Combine(vaultDir, "plugins")) ||
                                         DirectoryHasAnyFiles(Path.Combine(vaultDir, "skills")) ||
                                         DirectoryHasAnyFiles(Path.Combine(vaultDir, "rules")) ||
                                         DirectoryHasAnyFiles(Path.Combine(vaultDir, "mcp")) ||
                                         DirectoryHasAnyFiles(Path.Combine(vaultDir, "hooks"));
            return !vaultAlreadyPopulated;
        }

        public static int MigrateLegacyRepositoryToVault(string legacyRepoDir, string vaultDir)
        {
            if (string.IsNullOrEmpty(legacyRepoDir) || !Directory.Exists(legacyRepoDir) || string.IsNullOrEmpty(vaultDir))
                return 0;

            if (!Directory.Exists(vaultDir))
                Directory.CreateDirectory(vaultDir);

            int migratedFiles = 0;
            string[] subBuckets = new string[] { "plugins", "skills", "rules", "mcp", "hooks" };
            for (int b = 0; b < subBuckets.Length; b++)
            {
                string srcBucket = Path.Combine(legacyRepoDir, subBuckets[b]);
                if (!Directory.Exists(srcBucket)) continue;

                List<string> files = new List<string>();
                CollectSafeFilesRecursive(srcBucket, srcBucket, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase), files);
                for (int i = 0; i < files.Count; i++)
                {
                    string srcFile = files[i];
                    string rel = srcFile.Substring(srcBucket.Length).TrimStart('\\', '/').Replace('\\', '/');
                    if (!IsSafeRelativePath(rel) || IsExcludedFileOrPath(rel)) continue;

                    string dstBucket = Path.Combine(vaultDir, subBuckets[b]);
                    string dstFile = CombineAndEnsureUnderRoot(dstBucket, rel);
                    string dstDir = Path.GetDirectoryName(dstFile);
                    if (!string.IsNullOrEmpty(dstDir) && !Directory.Exists(dstDir))
                        Directory.CreateDirectory(dstDir);

                    if (File.Exists(dstFile))
                    {
                        UnifiedJson.CreateTimestampedBackup(dstFile, MaxTimestampedBackups);
                    }
                    byte[] data = ReadFileBytesShared(srcFile);
                    SessionLifecycleService.WriteBytesAtomicWithBackup(dstFile, data, null);
                    migratedFiles++;
                }
            }

            string legacyRules = Path.Combine(legacyRepoDir, "rename_rules.json");
            if (File.Exists(legacyRules))
            {
                string dstRules = Path.Combine(vaultDir, "rename_rules.json");
                if (File.Exists(dstRules))
                {
                    UnifiedJson.CreateTimestampedBackup(dstRules, MaxTimestampedBackups);
                }
                byte[] rulesBytes = ReadFileBytesShared(legacyRules);
                SessionLifecycleService.WriteBytesAtomicWithBackup(dstRules, rulesBytes, null);
                migratedFiles++;
            }

            string manifestPath = Path.Combine(vaultDir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                SessionLifecycleService.WriteUtf8TextAtomicWithBackup(manifestPath, DefaultCleanManifestJson, false, null);
            }

            string markerPath = Path.Combine(vaultDir, LegacyMigrationMarkerFile);
            string markerJson = "{\r\n  \"migratedFrom\": \"" + UnifiedJson.EscapeString(legacyRepoDir) +
                                "\",\r\n  \"migratedAt\": \"" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) +
                                "\",\r\n  \"filesCopied\": " + migratedFiles.ToString(CultureInfo.InvariantCulture) + "\r\n}\r\n";
            SessionLifecycleService.WriteUtf8TextAtomicWithBackup(markerPath, markerJson, false, null);
            return migratedFiles;
        }

        private static bool DirectoryHasAnyFiles(string dirPath)
        {
            if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath)) return false;
            try
            {
                return Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 0. UnifiedJson-Backed JSONC Fragment Helpers (replaces JsonKeyManipulator)
        // ══════════════════════════════════════════════════════════════════

        internal static Dictionary<string, string> ExtractObjectEntries(string json, string parentKey)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<KeyValuePair<string, string>> ordered = ExtractObjectEntriesOrdered(json, parentKey);
            for (int i = 0; i < ordered.Count; i++)
            {
                map[ordered[i].Key] = ordered[i].Value;
            }
            return map;
        }

        internal static List<KeyValuePair<string, string>> ExtractObjectEntriesOrdered(string json, string parentKey)
        {
            List<KeyValuePair<string, string>> result = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrWhiteSpace(json)) return result;

            object parsed;
            if (!UnifiedJson.TryParse(json, out parsed)) return result;

            Dictionary<string, object> rootDict = UnifiedJson.AsDict(parsed);
            if (rootDict == null) return result;

            Dictionary<string, object> targetDict = rootDict;
            if (!string.IsNullOrEmpty(parentKey))
            {
                object childObj = null;
                if (!rootDict.TryGetValue(parentKey, out childObj))
                {
                    foreach (KeyValuePair<string, object> kvp in rootDict)
                    {
                        if (string.Equals(kvp.Key, parentKey, StringComparison.OrdinalIgnoreCase))
                        {
                            childObj = kvp.Value;
                            break;
                        }
                    }
                }
                targetDict = UnifiedJson.AsDict(childObj);
                if (targetDict == null) return result;
            }

            foreach (KeyValuePair<string, object> kvp in targetDict)
            {
                if (string.IsNullOrEmpty(kvp.Key)) continue;
                string serializedVal = UnifiedJson.Serialize(kvp.Value, true);
                result.Add(new KeyValuePair<string, string>(kvp.Key, serializedVal));
            }
            return result;
        }

        internal static List<string> ExtractAllStringLiterals(string json)
        {
            List<string> results = new List<string>();
            if (string.IsNullOrWhiteSpace(json)) return results;

            object parsed;
            if (UnifiedJson.TryParse(json, out parsed))
            {
                CollectStringValuesRecursive(parsed, results, 0);
                return results;
            }

            // Fallback literal scan when fragment is incomplete
            MatchCollection mc = Regex.Matches(json, "\"((?:\\\\.|[^\"\\\\])*)\"");
            for (int i = 0; i < mc.Count; i++)
            {
                string raw = mc[i].Groups[1].Value;
                object unescaped;
                if (UnifiedJson.TryParse("\"" + raw + "\"", out unescaped) && unescaped is string)
                    results.Add((string)unescaped);
                else
                    results.Add(raw);
            }
            return results;
        }

        private static void CollectStringValuesRecursive(object node, List<string> results, int depth)
        {
            if (node == null || depth > 128) return;
            string s = node as string;
            if (s != null)
            {
                results.Add(s);
                return;
            }

            Dictionary<string, object> dict = UnifiedJson.AsDict(node);
            if (dict != null)
            {
                foreach (KeyValuePair<string, object> kvp in dict)
                {
                    CollectStringValuesRecursive(kvp.Value, results, depth + 1);
                }
                return;
            }

            List<object> list = UnifiedJson.AsList(node);
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    CollectStringValuesRecursive(list[i], results, depth + 1);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 0b. 路径穿越防护 (Zip-Slip Guard) 与危险 ID 无损编码
        // ══════════════════════════════════════════════════════════════════

        public static string EncodeSafeCapabilityFolderName(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return "_enc_";

            bool plainSafe = itemId.Length >= 1 && itemId.Length <= 80 &&
                             !itemId.StartsWith("_enc_", StringComparison.Ordinal) &&
                             !itemId.StartsWith(".", StringComparison.Ordinal) &&
                             !itemId.EndsWith(".", StringComparison.Ordinal) &&
                             !WindowsReservedNames.Contains(itemId);

            if (plainSafe)
            {
                for (int i = 0; i < itemId.Length; i++)
                {
                    char c = itemId[i];
                    bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ||
                              c == '-' || c == '_' || c == '.';
                    if (!ok)
                    {
                        plainSafe = false;
                        break;
                    }
                }
            }

            if (plainSafe) return itemId;

            byte[] utf8 = Encoding.UTF8.GetBytes(itemId);
            string b64 = Convert.ToBase64String(utf8).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return "_enc_" + b64;
        }

        public static string DecodeSafeCapabilityFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName) || !folderName.StartsWith("_enc_", StringComparison.Ordinal))
                return folderName ?? string.Empty;

            string b64 = folderName.Substring(5).Replace('-', '+').Replace('_', '/');
            int mod = b64.Length % 4;
            if (mod == 2) b64 += "==";
            else if (mod == 3) b64 += "=";
            else if (mod == 1) return folderName;

            try
            {
                byte[] raw = Convert.FromBase64String(b64);
                string decoded = Encoding.UTF8.GetString(raw);
                return string.IsNullOrEmpty(decoded) ? folderName : decoded;
            }
            catch
            {
                return folderName;
            }
        }

        public static bool IsSafeRelativePath(string relPath)
        {
            if (string.IsNullOrWhiteSpace(relPath)) return false;
            string norm = relPath.Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrEmpty(norm)) return false;
            if (norm.Length >= 2 && norm[1] == ':') return false;

            string[] segs = norm.Split('/');
            char[] invalidChars = Path.GetInvalidFileNameChars();
            for (int i = 0; i < segs.Length; i++)
            {
                string seg = segs[i];
                if (string.IsNullOrEmpty(seg)) return false;
                if (seg == "." || seg == "..") return false;
                if (seg.IndexOfAny(invalidChars) >= 0) return false;
                if (WindowsReservedNames.Contains(seg)) return false;
            }
            return true;
        }

        public static string CombineAndEnsureUnderRoot(string rootDir, string relPath)
        {
            if (string.IsNullOrEmpty(rootDir) || !IsSafeRelativePath(relPath))
                throw new InvalidOperationException("非法或包含越界路径段的目标相对路径: " + relPath);

            string cleanRel = relPath.Replace('\\', '/').TrimStart('/').Replace('/', '\\');
            string fullRoot = Path.GetFullPath(rootDir).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            string combined = Path.GetFullPath(Path.Combine(rootDir, cleanRel));
            if (!combined.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("路径越界 (Zip-Slip 防护): " + combined);
            return combined;
        }

        public static bool IsExcludedSubdirName(string dirName)
        {
            return !string.IsNullOrEmpty(dirName) && ExcludedSubdirNames.Contains(dirName);
        }

        public static bool IsExcludedFileOrPath(string relPath)
        {
            if (string.IsNullOrEmpty(relPath)) return true;
            string norm = relPath.Replace('\\', '/').TrimStart('/');
            string[] segs = norm.Split('/');
            for (int i = 0; i < segs.Length - 1; i++)
            {
                if (ExcludedSubdirNames.Contains(segs[i]))
                    return true;
            }

            string fileName = Path.GetFileName(norm);
            if (string.IsNullOrEmpty(fileName)) return true;

            if (string.Equals(fileName, "auth.json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "credentials.json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, ".env", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith(".env.", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (ext == ".pyc" || ext == ".pyo" || ext == ".pem" || ext == ".key" || ext == ".pfx")
                return true;

            if ((ext == ".json" || ext == ".yaml" || ext == ".yml" || ext == ".ini" || ext == ".conf" || ext == ".txt") &&
                (fileName.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 fileName.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            return false;
        }

        private static byte[] ReadFileBytesShared(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (MemoryStream ms = new MemoryStream())
            {
                fs.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private static string ReadAllTextShared(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 1. 扫描本地 .gemini 环境中的五大类能力资产 (含单品容错隔离)
        // ══════════════════════════════════════════════════════════════════
        public static List<CapabilityItem> ScanLocalCapabilities(string geminiPath, string homePath)
        {
            List<CapabilityItem> list = new List<CapabilityItem>();
            if (string.IsNullOrEmpty(geminiPath) || !Directory.Exists(geminiPath))
                return list;

            // 1.1 Plugins: config/plugins/<id>/ + config/config.json ("plugins": { "<id>": ... })
            string configJsonPath = Path.Combine(geminiPath, "config", "config.json");
            Dictionary<string, string> pluginRegMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(configJsonPath))
            {
                try
                {
                    string cfgText = ReadAllTextShared(configJsonPath);
                    pluginRegMap = ExtractObjectEntries(cfgText, "plugins");
                }
                catch { }
            }

            string pluginsDir = Path.Combine(geminiPath, "config", "plugins");
            if (Directory.Exists(pluginsDir))
            {
                string[] pDirs = Directory.GetDirectories(pluginsDir);
                Array.Sort(pDirs, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < pDirs.Length; i++)
                {
                    try
                    {
                        string pDir = pDirs[i];
                        string rawDirName = Path.GetFileName(pDir);
                        if (string.IsNullOrEmpty(rawDirName)) continue;
                        string pId = DecodeSafeCapabilityFolderName(rawDirName);

                        CapabilityItem item = new CapabilityItem
                        {
                            Kind = CapabilityKind.Plugin,
                            Id = pId,
                            DisplayName = pId,
                            LastModified = DateTime.MinValue
                        };

                        string regFrag = null;
                        if (!pluginRegMap.TryGetValue(pId, out regFrag) &&
                            !string.Equals(rawDirName, pId, StringComparison.OrdinalIgnoreCase))
                        {
                            pluginRegMap.TryGetValue(rawDirName, out regFrag);
                        }
                        if (!string.IsNullOrEmpty(regFrag))
                        {
                            item.RawJsonFragment = CapabilityPathNormalizer.NormalizeToHomePlaceholder(regFrag, homePath);
                        }

                        PopulateFilesFromDirectory(pDir, item);
                        if (item.LastModified.Year < 1980)
                        {
                            UpdateMaxLastModified(item, Directory.GetLastWriteTimeUtc(pDir));
                        }
                        ParsePluginMetadata(item);
                        FinalizeCapabilitySignatureAndPreview(item, homePath, geminiPath);
                        list.Add(item);
                    }
                    catch { }
                }
            }

            // 1.2 Standalone Skills: config/skills/<safeId>/
            string skillsDir = Path.Combine(geminiPath, "config", "skills");
            if (Directory.Exists(skillsDir))
            {
                string[] sDirs = Directory.GetDirectories(skillsDir);
                Array.Sort(sDirs, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < sDirs.Length; i++)
                {
                    try
                    {
                        string sDir = sDirs[i];
                        string rawDirName = Path.GetFileName(sDir);
                        if (string.IsNullOrEmpty(rawDirName)) continue;
                        string sId = DecodeSafeCapabilityFolderName(rawDirName);

                        CapabilityItem item = new CapabilityItem
                        {
                            Kind = CapabilityKind.Skill,
                            Id = sId,
                            DisplayName = sId,
                            LastModified = DateTime.MinValue
                        };

                        PopulateFilesFromDirectory(sDir, item);
                        if (item.LastModified.Year < 1980)
                        {
                            UpdateMaxLastModified(item, Directory.GetLastWriteTimeUtc(sDir));
                        }
                        ParseSkillMetadata(item);
                        FinalizeCapabilitySignatureAndPreview(item, homePath, geminiPath);
                        list.Add(item);
                    }
                    catch { }
                }
            }

            // 1.3 Rules: Root rules (GEMINI.md, AGENTS.md, .geminirules) & config/rules/*.md
            string[] rootRuleNames = new string[] { "GEMINI.md", "AGENTS.md", ".geminirules" };
            for (int i = 0; i < rootRuleNames.Length; i++)
            {
                string rf = Path.Combine(geminiPath, rootRuleNames[i]);
                if (File.Exists(rf))
                {
                    CapabilityItem item = CreateRuleItemFromFile(rf, rootRuleNames[i], "_root/" + rootRuleNames[i], homePath, geminiPath);
                    if (item != null) list.Add(item);
                }
            }

            string rulesDir = Path.Combine(geminiPath, "config", "rules");
            if (Directory.Exists(rulesDir))
            {
                try
                {
                    List<string> rFiles = new List<string>();
                    CollectSafeFilesRecursive(rulesDir, rulesDir, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase), rFiles);
                    rFiles.Sort(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < rFiles.Count; i++)
                    {
                        string rel = rFiles[i].Substring(rulesDir.Length).TrimStart('\\', '/').Replace('\\', '/');
                        if (!IsSafeRelativePath(rel) || IsExcludedFileOrPath(rel)) continue;
                        CapabilityItem item = CreateRuleItemFromFile(rFiles[i], "rules/" + rel, "config/" + rel, homePath, geminiPath);
                        if (item != null) list.Add(item);
                    }
                }
                catch { }
            }

            // 1.4 MCP Servers: config/mcp_config.json ("mcpServers": { "<id>": ... }) + antigravity/mcp/<id>/
            string mcpConfigPath = Path.Combine(geminiPath, "config", "mcp_config.json");
            if (File.Exists(mcpConfigPath))
            {
                try
                {
                    string mcpJson = ReadAllTextShared(mcpConfigPath);
                    DateTime mcpTime = File.GetLastWriteTimeUtc(mcpConfigPath);
                    List<KeyValuePair<string, string>> servers = ExtractObjectEntriesOrdered(mcpJson, "mcpServers");
                    for (int i = 0; i < servers.Count; i++)
                    {
                        try
                        {
                            string sId = servers[i].Key;
                            string rawFragment = CapabilityPathNormalizer.NormalizeToHomePlaceholder(servers[i].Value, homePath);

                            CapabilityItem item = new CapabilityItem
                            {
                                Kind = CapabilityKind.McpServer,
                                Id = sId,
                                DisplayName = sId,
                                RawJsonFragment = rawFragment,
                                LastModified = mcpTime
                            };

                            string safeSchemaFolder = EncodeSafeCapabilityFolderName(sId);
                            string schemaDir = CombineAndEnsureUnderRoot(Path.Combine(geminiPath, "antigravity", "mcp"), safeSchemaFolder);
                            if (!Directory.Exists(schemaDir) && !string.Equals(safeSchemaFolder, sId, StringComparison.Ordinal) && IsSafeRelativePath(sId))
                            {
                                string legacySchemaDir = CombineAndEnsureUnderRoot(Path.Combine(geminiPath, "antigravity", "mcp"), sId);
                                if (Directory.Exists(legacySchemaDir)) schemaDir = legacySchemaDir;
                            }
                            if (Directory.Exists(schemaDir))
                            {
                                PopulateFilesFromDirectory(schemaDir, item, "schemas/");
                            }

                            ParseMcpDescription(item);
                            FinalizeCapabilitySignatureAndPreview(item, homePath, geminiPath);
                            list.Add(item);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            // 1.5 Hooks: config/hooks.json + companion scripts in config/
            string hooksConfigPath = Path.Combine(geminiPath, "config", "hooks.json");
            if (File.Exists(hooksConfigPath))
            {
                try
                {
                    string hooksJson = ReadAllTextShared(hooksConfigPath);
                    DateTime hooksTime = File.GetLastWriteTimeUtc(hooksConfigPath);
                    List<KeyValuePair<string, string>> hookEntries = ExtractObjectEntriesOrdered(hooksJson, null);
                    for (int i = 0; i < hookEntries.Count; i++)
                    {
                        try
                        {
                            string hId = hookEntries[i].Key;
                            string rawFragment = CapabilityPathNormalizer.NormalizeToHomePlaceholder(hookEntries[i].Value, homePath);

                            CapabilityItem item = new CapabilityItem
                            {
                                Kind = CapabilityKind.Hook,
                                Id = hId,
                                DisplayName = hId,
                                RawJsonFragment = rawFragment,
                                LastModified = hooksTime
                            };

                            DiscoverAndBundleHookScripts(geminiPath, hookEntries[i].Value, item);
                            ParseHookDescription(item);
                            FinalizeCapabilitySignatureAndPreview(item, homePath, geminiPath);
                            list.Add(item);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return list;
        }

        // ══════════════════════════════════════════════════════════════════
        // 2. 扫描 Vault / Repository 中的五大类能力资产
        // ══════════════════════════════════════════════════════════════════
        public static List<CapabilityItem> ScanRepositoryCapabilities(string repoPath, string currentHomePath, string localGeminiPath = null)
        {
            List<CapabilityItem> list = new List<CapabilityItem>();
            if (string.IsNullOrEmpty(repoPath) || !Directory.Exists(repoPath))
                return list;

            // 2.1 Plugins: Vault/plugins/<safeId>/
            string pluginsRoot = Path.Combine(repoPath, "plugins");
            if (Directory.Exists(pluginsRoot))
            {
                string[] dirs = Directory.GetDirectories(pluginsRoot);
                Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < dirs.Length; i++)
                {
                    try
                    {
                        string pDir = dirs[i];
                        string pId = DecodeSafeCapabilityFolderName(Path.GetFileName(pDir));
                        CapabilityItem item = new CapabilityItem
                        {
                            Kind = CapabilityKind.Plugin,
                            Id = pId,
                            DisplayName = pId,
                            LastModified = DateTime.MinValue
                        };

                        PopulateFilesFromDirectory(pDir, item);
                        if (item.LastModified.Year < 1980)
                        {
                            UpdateMaxLastModified(item, Directory.GetLastWriteTimeUtc(pDir));
                        }
                        byte[] regBytes = item.GetFileBytes("_plugin_reg.json");
                        if (regBytes != null)
                        {
                            item.RawJsonFragment = StripUtf8Bom(Encoding.UTF8.GetString(regBytes)).Trim();
                            item.FilesMap.Remove("_plugin_reg.json");
                            item.SourceFilePaths.Remove("_plugin_reg.json");
                        }

                        ParsePluginMetadata(item);
                        FinalizeCapabilitySignatureAndPreview(item, currentHomePath, localGeminiPath);
                        list.Add(item);
                    }
                    catch { }
                }
            }

            // 2.2 Skills: Vault/skills/<safeId>/
            string skillsRoot = Path.Combine(repoPath, "skills");
            if (Directory.Exists(skillsRoot))
            {
                string[] dirs = Directory.GetDirectories(skillsRoot);
                Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < dirs.Length; i++)
                {
                    try
                    {
                        string sDir = dirs[i];
                        string sId = DecodeSafeCapabilityFolderName(Path.GetFileName(sDir));
                        CapabilityItem item = new CapabilityItem
                        {
                            Kind = CapabilityKind.Skill,
                            Id = sId,
                            DisplayName = sId,
                            LastModified = DateTime.MinValue
                        };

                        PopulateFilesFromDirectory(sDir, item);
                        if (item.LastModified.Year < 1980)
                        {
                            UpdateMaxLastModified(item, Directory.GetLastWriteTimeUtc(sDir));
                        }
                        ParseSkillMetadata(item);
                        FinalizeCapabilitySignatureAndPreview(item, currentHomePath, localGeminiPath);
                        list.Add(item);
                    }
                    catch { }
                }
            }

            // 2.3 Rules: Vault/rules/_root/* and Vault/rules/config/*
            string rulesRoot = Path.Combine(repoPath, "rules", "_root");
            if (Directory.Exists(rulesRoot))
            {
                string[] rFiles = Directory.GetFiles(rulesRoot);
                Array.Sort(rFiles, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < rFiles.Length; i++)
                {
                    string fn = Path.GetFileName(rFiles[i]);
                    if (!AllowedRootRuleNames.Contains(fn)) continue;
                    CapabilityItem item = CreateRuleItemFromFile(rFiles[i], fn, "_root/" + fn, currentHomePath, localGeminiPath);
                    if (item != null) list.Add(item);
                }
            }

            string rulesConfig = Path.Combine(repoPath, "rules", "config");
            if (Directory.Exists(rulesConfig))
            {
                try
                {
                    List<string> rFiles = new List<string>();
                    CollectSafeFilesRecursive(rulesConfig, rulesConfig, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase), rFiles);
                    rFiles.Sort(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < rFiles.Count; i++)
                    {
                        string rel = rFiles[i].Substring(rulesConfig.Length).TrimStart('\\', '/').Replace('\\', '/');
                        if (!IsSafeRelativePath(rel) || IsExcludedFileOrPath(rel)) continue;
                        CapabilityItem item = CreateRuleItemFromFile(rFiles[i], "rules/" + rel, "config/" + rel, currentHomePath, localGeminiPath);
                        if (item != null) list.Add(item);
                    }
                }
                catch { }
            }

            // 2.4 MCP Servers: Vault/mcp/<safeId>/server_entry.json + schemas/
            string mcpRoot = Path.Combine(repoPath, "mcp");
            if (Directory.Exists(mcpRoot))
            {
                string[] dirs = Directory.GetDirectories(mcpRoot);
                Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < dirs.Length; i++)
                {
                    try
                    {
                        string mDir = dirs[i];
                        string mId = DecodeSafeCapabilityFolderName(Path.GetFileName(mDir));
                        string entryFile = Path.Combine(mDir, "server_entry.json");
                        if (!File.Exists(entryFile)) continue;

                        CapabilityItem item = new CapabilityItem
                        {
                            Kind = CapabilityKind.McpServer,
                            Id = mId,
                            DisplayName = mId,
                            RawJsonFragment = CapabilityPathNormalizer.NormalizeToHomePlaceholder(ReadAllTextShared(entryFile).Trim(), currentHomePath),
                            LastModified = File.GetLastWriteTimeUtc(entryFile)
                        };

                        string schemaDir = Path.Combine(mDir, "schemas");
                        if (Directory.Exists(schemaDir))
                        {
                            PopulateFilesFromDirectory(schemaDir, item, "schemas/");
                        }

                        ParseMcpDescription(item);
                        FinalizeCapabilitySignatureAndPreview(item, currentHomePath, localGeminiPath);
                        list.Add(item);
                    }
                    catch { }
                }
            }

            // 2.5 Hooks: Vault/hooks/<safeId>/hook_entry.json + scripts/
            string hooksRoot = Path.Combine(repoPath, "hooks");
            if (Directory.Exists(hooksRoot))
            {
                string[] dirs = Directory.GetDirectories(hooksRoot);
                Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < dirs.Length; i++)
                {
                    try
                    {
                        string hDir = dirs[i];
                        string hId = DecodeSafeCapabilityFolderName(Path.GetFileName(hDir));
                        string entryFile = Path.Combine(hDir, "hook_entry.json");
                        if (!File.Exists(entryFile)) continue;

                        CapabilityItem item = new CapabilityItem
                        {
                            Kind = CapabilityKind.Hook,
                            Id = hId,
                            DisplayName = hId,
                            RawJsonFragment = CapabilityPathNormalizer.NormalizeToHomePlaceholder(ReadAllTextShared(entryFile).Trim(), currentHomePath),
                            LastModified = File.GetLastWriteTimeUtc(entryFile)
                        };

                        string scriptsDir = Path.Combine(hDir, "scripts");
                        if (Directory.Exists(scriptsDir))
                        {
                            PopulateFilesFromDirectory(scriptsDir, item, "scripts/");
                            foreach (string key in item.FilesMap.Keys)
                            {
                                if (key.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase))
                                {
                                    item.CompanionScriptNames.Add(Path.GetFileName(key));
                                }
                            }
                        }

                        ParseHookDescription(item);
                        FinalizeCapabilitySignatureAndPreview(item, currentHomePath, localGeminiPath);
                        list.Add(item);
                    }
                    catch { }
                }
            }

            return list;
        }

        // ══════════════════════════════════════════════════════════════════
        // 3. 从 .agy 归档包流式解析五大类单品能力 (含 Zip-Slip 与单品异常隔离)
        // ══════════════════════════════════════════════════════════════════
        public static List<CapabilityItem> ScanZipArchiveCapabilities(
            ZipArchive archive,
            string sourceHome,
            string currentHomePath,
            string localGeminiPath = null)
        {
            List<CapabilityItem> list = new List<CapabilityItem>();
            if (archive == null) return list;

            string effectiveSourceHome = string.IsNullOrEmpty(sourceHome) ? currentHomePath : sourceHome;

            Dictionary<string, CapabilityItem> plugins = new Dictionary<string, CapabilityItem>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, CapabilityItem> skills = new Dictionary<string, CapabilityItem>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, byte[]> mcpSchemaFiles = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, DateTime> mcpSchemaTimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, byte[]> configRootFiles = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, DateTime> configRootTimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

            string configJsonText = null;
            string mcpConfigJsonText = null;
            string hooksJsonText = null;
            DateTime mcpConfigTime = DateTime.MinValue;
            DateTime hooksJsonTime = DateTime.MinValue;

            for (int i = 0; i < archive.Entries.Count; i++)
            {
                ZipArchiveEntry entry = archive.Entries[i];
                if (string.IsNullOrEmpty(entry.Name)) continue;

                string norm = entry.FullName.Replace('\\', '/').TrimStart('/');
                if (norm.StartsWith("data/", StringComparison.OrdinalIgnoreCase))
                    norm = norm.Substring(5);

                if (norm.StartsWith("antigravity/builtin/", StringComparison.OrdinalIgnoreCase) ||
                    norm.IndexOf("/builtin/skills", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                try
                {
                    if (AllowedRootRuleNames.Contains(norm))
                    {
                        byte[] bytes = ReadZipEntryBytes(entry);
                        CapabilityItem ruleItem = CreateRuleItemFromBytes(norm, "_root/" + norm, bytes, ConvertZipTimeToUtc(entry.LastWriteTime), effectiveSourceHome, currentHomePath, localGeminiPath);
                        if (ruleItem != null) list.Add(ruleItem);
                    }
                    else if (norm.StartsWith("config/rules/", StringComparison.OrdinalIgnoreCase))
                    {
                        string rel = norm.Substring("config/rules/".Length);
                        if (IsSafeRelativePath(rel) && !IsExcludedFileOrPath(rel))
                        {
                            byte[] bytes = ReadZipEntryBytes(entry);
                            CapabilityItem ruleItem = CreateRuleItemFromBytes("rules/" + rel, "config/" + rel, bytes, ConvertZipTimeToUtc(entry.LastWriteTime), effectiveSourceHome, currentHomePath, localGeminiPath);
                            if (ruleItem != null) list.Add(ruleItem);
                        }
                    }
                    else if (norm.StartsWith("config/plugins/", StringComparison.OrdinalIgnoreCase))
                    {
                        string sub = norm.Substring("config/plugins/".Length);
                        int slash = sub.IndexOf('/');
                        if (slash > 0)
                        {
                            string pId = DecodeSafeCapabilityFolderName(sub.Substring(0, slash));
                            string rel = sub.Substring(slash + 1);
                            if (IsSafeRelativePath(rel) && !IsExcludedFileOrPath(rel) && !pId.Contains(".."))
                            {
                                CapabilityItem pItem;
                                if (!plugins.TryGetValue(pId, out pItem))
                                {
                                    pItem = new CapabilityItem { Kind = CapabilityKind.Plugin, Id = pId, DisplayName = pId, LastModified = DateTime.MinValue };
                                    plugins[pId] = pItem;
                                }
                                pItem.FilesMap[rel] = ReadZipEntryBytes(entry);
                                UpdateMaxLastModified(pItem, ConvertZipTimeToUtc(entry.LastWriteTime));
                            }
                        }
                    }
                    else if (norm.StartsWith("config/skills/", StringComparison.OrdinalIgnoreCase))
                    {
                        string sub = norm.Substring("config/skills/".Length);
                        int slash = sub.IndexOf('/');
                        if (slash > 0)
                        {
                            string sId = DecodeSafeCapabilityFolderName(sub.Substring(0, slash));
                            string rel = sub.Substring(slash + 1);
                            if (IsSafeRelativePath(rel) && !IsExcludedFileOrPath(rel) && !sId.Contains(".."))
                            {
                                CapabilityItem sItem;
                                if (!skills.TryGetValue(sId, out sItem))
                                {
                                    sItem = new CapabilityItem { Kind = CapabilityKind.Skill, Id = sId, DisplayName = sId, LastModified = DateTime.MinValue };
                                    skills[sId] = sItem;
                                }
                                sItem.FilesMap[rel] = ReadZipEntryBytes(entry);
                                UpdateMaxLastModified(sItem, ConvertZipTimeToUtc(entry.LastWriteTime));
                            }
                        }
                    }
                    else if (string.Equals(norm, "config/config.json", StringComparison.OrdinalIgnoreCase))
                    {
                        configJsonText = StripUtf8Bom(Encoding.UTF8.GetString(ReadZipEntryBytes(entry)));
                    }
                    else if (string.Equals(norm, "config/mcp_config.json", StringComparison.OrdinalIgnoreCase))
                    {
                        mcpConfigJsonText = StripUtf8Bom(Encoding.UTF8.GetString(ReadZipEntryBytes(entry)));
                        DateTime utcMcp = ConvertZipTimeToUtc(entry.LastWriteTime);
                        if (utcMcp.Year >= 1980) mcpConfigTime = utcMcp;
                    }
                    else if (string.Equals(norm, "config/hooks.json", StringComparison.OrdinalIgnoreCase))
                    {
                        hooksJsonText = StripUtf8Bom(Encoding.UTF8.GetString(ReadZipEntryBytes(entry)));
                        DateTime utcHooks = ConvertZipTimeToUtc(entry.LastWriteTime);
                        if (utcHooks.Year >= 1980) hooksJsonTime = utcHooks;
                    }
                    else if (norm.StartsWith("config/", StringComparison.OrdinalIgnoreCase) && norm.IndexOf('/', "config/".Length) < 0)
                    {
                        string fn = norm.Substring("config/".Length);
                        if (IsSafeRelativePath(fn) && !IsExcludedFileOrPath(fn))
                        {
                            configRootFiles[fn] = ReadZipEntryBytes(entry);
                            configRootTimes[fn] = ConvertZipTimeToUtc(entry.LastWriteTime);
                        }
                    }
                    else if (norm.StartsWith("antigravity/mcp/", StringComparison.OrdinalIgnoreCase))
                    {
                        string sub = norm.Substring("antigravity/mcp/".Length);
                        if (IsSafeRelativePath(sub) && !IsExcludedFileOrPath(sub))
                        {
                            mcpSchemaFiles[sub] = ReadZipEntryBytes(entry);
                            mcpSchemaTimes[sub] = ConvertZipTimeToUtc(entry.LastWriteTime);
                        }
                    }
                }
                catch { }
            }

            Dictionary<string, string> pluginRegMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(configJsonText))
            {
                try { pluginRegMap = ExtractObjectEntries(configJsonText, "plugins"); }
                catch { }
            }

            foreach (KeyValuePair<string, CapabilityItem> kvp in plugins)
            {
                try
                {
                    CapabilityItem pItem = kvp.Value;
                    if (pluginRegMap.ContainsKey(kvp.Key))
                    {
                        pItem.RawJsonFragment = CapabilityPathNormalizer.NormalizeToHomePlaceholder(pluginRegMap[kvp.Key], effectiveSourceHome);
                    }
                    ParsePluginMetadata(pItem);
                    FinalizeCapabilitySignatureAndPreview(pItem, currentHomePath, localGeminiPath);
                    list.Add(pItem);
                }
                catch { }
            }

            foreach (KeyValuePair<string, CapabilityItem> kvp in skills)
            {
                try
                {
                    CapabilityItem sItem = kvp.Value;
                    ParseSkillMetadata(sItem);
                    FinalizeCapabilitySignatureAndPreview(sItem, currentHomePath, localGeminiPath);
                    list.Add(sItem);
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(mcpConfigJsonText))
            {
                try
                {
                    List<KeyValuePair<string, string>> servers = ExtractObjectEntriesOrdered(mcpConfigJsonText, "mcpServers");
                    for (int i = 0; i < servers.Count; i++)
                    {
                        try
                        {
                            string sId = servers[i].Key;
                            string rawFrag = CapabilityPathNormalizer.NormalizeToHomePlaceholder(servers[i].Value, effectiveSourceHome);
                            CapabilityItem mItem = new CapabilityItem
                            {
                                Kind = CapabilityKind.McpServer,
                                Id = sId,
                                DisplayName = sId,
                                RawJsonFragment = rawFrag,
                                LastModified = mcpConfigTime.Year >= 1980 ? mcpConfigTime : DateTime.MinValue
                            };

                            string safeFolderPrefix = EncodeSafeCapabilityFolderName(sId) + "/";
                            string rawPrefix = sId + "/";
                            foreach (KeyValuePair<string, byte[]> skvp in mcpSchemaFiles)
                            {
                                string relSchema = null;
                                if (skvp.Key.StartsWith(rawPrefix, StringComparison.OrdinalIgnoreCase))
                                    relSchema = skvp.Key.Substring(rawPrefix.Length);
                                else if (skvp.Key.StartsWith(safeFolderPrefix, StringComparison.OrdinalIgnoreCase))
                                    relSchema = skvp.Key.Substring(safeFolderPrefix.Length);

                                if (!string.IsNullOrEmpty(relSchema) && IsSafeRelativePath(relSchema))
                                {
                                    mItem.FilesMap["schemas/" + relSchema] = skvp.Value;
                                    DateTime schemaTime;
                                    if (mcpSchemaTimes.TryGetValue(skvp.Key, out schemaTime))
                                    {
                                        UpdateMaxLastModified(mItem, schemaTime);
                                    }
                                }
                            }

                            ParseMcpDescription(mItem);
                            FinalizeCapabilitySignatureAndPreview(mItem, currentHomePath, localGeminiPath);
                            list.Add(mItem);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(hooksJsonText))
            {
                try
                {
                    List<KeyValuePair<string, string>> hookEntries = ExtractObjectEntriesOrdered(hooksJsonText, null);
                    for (int i = 0; i < hookEntries.Count; i++)
                    {
                        try
                        {
                            string hId = hookEntries[i].Key;
                            string rawFrag = CapabilityPathNormalizer.NormalizeToHomePlaceholder(hookEntries[i].Value, effectiveSourceHome);
                            CapabilityItem hItem = new CapabilityItem
                            {
                                Kind = CapabilityKind.Hook,
                                Id = hId,
                                DisplayName = hId,
                                RawJsonFragment = rawFrag,
                                LastModified = hooksJsonTime.Year >= 1980 ? hooksJsonTime : DateTime.MinValue
                            };

                            foreach (KeyValuePair<string, byte[]> ckvp in configRootFiles)
                            {
                                if (IsExcludedConfigRootFile(ckvp.Key)) continue;
                                if (HookFragmentReferencesScript(hookEntries[i].Value, ckvp.Key))
                                {
                                    hItem.FilesMap["scripts/" + ckvp.Key] = ckvp.Value;
                                    hItem.CompanionScriptNames.Add(ckvp.Key);
                                    DateTime scriptTime;
                                    if (configRootTimes.TryGetValue(ckvp.Key, out scriptTime))
                                    {
                                        UpdateMaxLastModified(hItem, scriptTime);
                                    }
                                }
                            }

                            ParseHookDescription(hItem);
                            FinalizeCapabilitySignatureAndPreview(hItem, currentHomePath, localGeminiPath);
                            list.Add(hItem);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return list;
        }

        // ══════════════════════════════════════════════════════════════════
        // 4. 推入能力资产至 Vault (Local -> Vault/)
        // ══════════════════════════════════════════════════════════════════
        public static int PushCapabilitiesToRepository(
            string repoPath,
            string sourceHomePath,
            List<CapabilityItem> selectedItems)
        {
            if (string.IsNullOrEmpty(repoPath) || selectedItems == null || selectedItems.Count == 0)
                return 0;

            if (!Directory.Exists(repoPath)) Directory.CreateDirectory(repoPath);
            int writtenCount = 0;

            for (int i = 0; i < selectedItems.Count; i++)
            {
                CapabilityItem item = selectedItems[i];
                if (item == null || string.IsNullOrEmpty(item.Id)) continue;

                string safeFolder = EncodeSafeCapabilityFolderName(item.Id);

                switch (item.Kind)
                {
                    case CapabilityKind.Plugin:
                    {
                        string targetDir = CombineAndEnsureUnderRoot(Path.Combine(repoPath, "plugins"), safeFolder);
                        if (Directory.Exists(targetDir)) DeleteDirSafe(targetDir);
                        Directory.CreateDirectory(targetDir);

                        writtenCount += WriteCapabilityFilesToDirectory(item, targetDir, null, sourceHomePath, true, false);
                        if (!string.IsNullOrEmpty(item.RawJsonFragment))
                        {
                            string regNorm = CapabilityPathNormalizer.NormalizeToHomePlaceholder(item.RawJsonFragment, sourceHomePath);
                            SessionLifecycleService.WriteUtf8TextAtomicWithBackup(
                                Path.Combine(targetDir, "_plugin_reg.json"), regNorm, false, null
                            );
                            writtenCount++;
                        }
                        break;
                    }
                    case CapabilityKind.Skill:
                    {
                        string targetDir = CombineAndEnsureUnderRoot(Path.Combine(repoPath, "skills"), safeFolder);
                        if (Directory.Exists(targetDir)) DeleteDirSafe(targetDir);
                        Directory.CreateDirectory(targetDir);

                        writtenCount += WriteCapabilityFilesToDirectory(item, targetDir, null, sourceHomePath, true, false);
                        break;
                    }
                    case CapabilityKind.Rule:
                    {
                        string rulesBase = Path.Combine(repoPath, "rules");
                        writtenCount += WriteCapabilityFilesToDirectory(item, rulesBase, null, sourceHomePath, true, false);
                        break;
                    }
                    case CapabilityKind.McpServer:
                    {
                        string targetDir = CombineAndEnsureUnderRoot(Path.Combine(repoPath, "mcp"), safeFolder);
                        if (Directory.Exists(targetDir)) DeleteDirSafe(targetDir);
                        Directory.CreateDirectory(targetDir);

                        string normFrag = CapabilityPathNormalizer.NormalizeToHomePlaceholder(item.RawJsonFragment, sourceHomePath);
                        SessionLifecycleService.WriteUtf8TextAtomicWithBackup(
                            Path.Combine(targetDir, "server_entry.json"), normFrag, false, null
                        );
                        writtenCount++;

                        writtenCount += WriteCapabilityFilesToDirectory(item, targetDir, null, sourceHomePath, true, false);
                        break;
                    }
                    case CapabilityKind.Hook:
                    {
                        string targetDir = CombineAndEnsureUnderRoot(Path.Combine(repoPath, "hooks"), safeFolder);
                        if (Directory.Exists(targetDir)) DeleteDirSafe(targetDir);
                        Directory.CreateDirectory(targetDir);

                        string normFrag = CapabilityPathNormalizer.NormalizeToHomePlaceholder(item.RawJsonFragment, sourceHomePath);
                        SessionLifecycleService.WriteUtf8TextAtomicWithBackup(
                            Path.Combine(targetDir, "hook_entry.json"), normFrag, false, null
                        );
                        writtenCount++;

                        writtenCount += WriteCapabilityFilesToDirectory(item, targetDir, null, sourceHomePath, true, false);
                        break;
                    }
                }
            }

            WriteRepositoryManifest(repoPath);
            return writtenCount;
        }

        // ══════════════════════════════════════════════════════════════════
        // 5. 从 Vault 或备份包细粒度合流能力至本机 (FIX P0-5: JSONC + single timestamped backup)
        // ══════════════════════════════════════════════════════════════════
        public static int ImportCapabilitiesToLocal(
            string geminiPath,
            string targetHomePath,
            List<CapabilityItem> selectedItems)
        {
            if (string.IsNullOrEmpty(geminiPath) || selectedItems == null || selectedItems.Count == 0)
                return 0;

            if (!Directory.Exists(geminiPath)) Directory.CreateDirectory(geminiPath);
            string configDir = Path.Combine(geminiPath, "config");
            if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);

            int writtenCount = 0;
            HashSet<string> backedUpFilesThisBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < selectedItems.Count; i++)
            {
                CapabilityItem item = selectedItems[i];
                if (item == null || string.IsNullOrEmpty(item.Id)) continue;

                switch (item.Kind)
                {
                    case CapabilityKind.Plugin:
                    {
                        string pluginsBase = Path.Combine(configDir, "plugins");
                        string safeFolder = EncodeSafeCapabilityFolderName(item.Id);
                        string targetDir = CombineAndEnsureUnderRoot(pluginsBase, safeFolder);
                        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                        CleanupStaleFilesInDirectory(targetDir, CollectIncomingRelPaths(item, null));
                        writtenCount += WriteCapabilityFilesToDirectory(item, targetDir, null, targetHomePath, false, true);

                        if (!string.IsNullOrEmpty(item.RawJsonFragment))
                        {
                            string configJsonFile = Path.Combine(configDir, "config.json");
                            string expandedFrag = CapabilityPathNormalizer.ExpandHomePlaceholder(item.RawJsonFragment, targetHomePath, true).Trim();
                            PatchJsoncFilePreservingCommentsWithBatchBackup(
                                configJsonFile, "plugins", item.Id, expandedFrag, backedUpFilesThisBatch);
                            writtenCount++;
                        }
                        break;
                    }
                    case CapabilityKind.Skill:
                    {
                        string skillsBase = Path.Combine(configDir, "skills");
                        string safeFolder = EncodeSafeCapabilityFolderName(item.Id);
                        string targetDir = CombineAndEnsureUnderRoot(skillsBase, safeFolder);
                        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                        CleanupStaleFilesInDirectory(targetDir, CollectIncomingRelPaths(item, null));
                        writtenCount += WriteCapabilityFilesToDirectory(item, targetDir, null, targetHomePath, false, true);
                        break;
                    }
                    case CapabilityKind.Rule:
                    {
                        foreach (string key in item.FilesMap.Keys)
                        {
                            string rel = key.Replace('\\', '/');
                            string destPath;
                            if (rel.StartsWith("_root/", StringComparison.OrdinalIgnoreCase))
                            {
                                string rootName = rel.Substring("_root/".Length);
                                if (!AllowedRootRuleNames.Contains(rootName))
                                    throw new InvalidOperationException("不允许的非标准根规则文件名: " + rootName);
                                destPath = CombineAndEnsureUnderRoot(geminiPath, rootName);
                            }
                            else if (rel.StartsWith("config/", StringComparison.OrdinalIgnoreCase))
                            {
                                string subRel = rel.Substring("config/".Length);
                                destPath = CombineAndEnsureUnderRoot(Path.Combine(geminiPath, "config", "rules"), subRel);
                            }
                            else
                            {
                                destPath = CombineAndEnsureUnderRoot(Path.Combine(geminiPath, "config", "rules"), rel);
                            }

                            WriteCapabilitySingleFile(item, key, destPath, targetHomePath, false);
                            writtenCount++;
                        }
                        break;
                    }
                    case CapabilityKind.McpServer:
                    {
                        string mcpConfigFile = Path.Combine(configDir, "mcp_config.json");
                        string expandedFrag = CapabilityPathNormalizer.ExpandHomePlaceholder(item.RawJsonFragment, targetHomePath, true).Trim();
                        if (!string.IsNullOrWhiteSpace(expandedFrag))
                        {
                            PatchJsoncFilePreservingCommentsWithBatchBackup(
                                mcpConfigFile, "mcpServers", item.Id, expandedFrag, backedUpFilesThisBatch);
                            writtenCount++;
                        }

                        if (item.FilesMap.Count > 0)
                        {
                            string mcpSchemasBase = Path.Combine(geminiPath, "antigravity", "mcp");
                            string safeFolder = EncodeSafeCapabilityFolderName(item.Id);
                            string schemaTargetDir = CombineAndEnsureUnderRoot(mcpSchemasBase, safeFolder);
                            CleanupStaleFilesInDirectory(schemaTargetDir, CollectIncomingRelPaths(item, "schemas/"));
                            writtenCount += WriteCapabilityFilesToDirectory(item, schemaTargetDir, "schemas/", targetHomePath, false, false);
                        }
                        break;
                    }
                    case CapabilityKind.Hook:
                    {
                        string hooksConfigFile = Path.Combine(configDir, "hooks.json");
                        string expandedFrag = CapabilityPathNormalizer.ExpandHomePlaceholder(item.RawJsonFragment, targetHomePath, true).Trim();
                        if (!string.IsNullOrWhiteSpace(expandedFrag))
                        {
                            PatchJsoncFilePreservingCommentsWithBatchBackup(
                                hooksConfigFile, null, item.Id, expandedFrag, backedUpFilesThisBatch);
                            writtenCount++;
                        }

                        if (item.FilesMap.Count > 0)
                        {
                            writtenCount += WriteCapabilityFilesToDirectory(item, configDir, "scripts/", targetHomePath, false, false);
                        }
                        break;
                    }
                }
            }

            return writtenCount;
        }

        private static void PatchJsoncFilePreservingCommentsWithBatchBackup(
            string jsonFilePath,
            string parentSectionOrNull,
            string key,
            string rawJsonValueLiteral,
            HashSet<string> backedUpFilesThisBatch)
        {
            string existing = File.Exists(jsonFilePath) ? ReadAllTextShared(jsonFilePath) : "{}";
            string merged = UnifiedJson.PatchObjectKeyPreservingComments(existing, parentSectionOrNull, key, rawJsonValueLiteral);

            if (File.Exists(jsonFilePath) && backedUpFilesThisBatch != null && backedUpFilesThisBatch.Add(jsonFilePath))
            {
                UnifiedJson.CreateTimestampedBackup(jsonFilePath, MaxTimestampedBackups);
            }
            SessionLifecycleService.WriteUtf8TextAtomicWithBackup(jsonFilePath, merged, false, null);
        }

        // ══════════════════════════════════════════════════════════════════
        // 6. 单品物理删除 (顺序约束、Hook 引用计数与 Zip-Slip 隔离)
        // ══════════════════════════════════════════════════════════════════
        public static int DeleteCapabilitiesFromLocal(string geminiPath, List<CapabilityItem> itemsToDelete)
        {
            if (string.IsNullOrEmpty(geminiPath) || itemsToDelete == null || itemsToDelete.Count == 0)
                return 0;

            int deletedCount = 0;
            string configDir = Path.Combine(geminiPath, "config");
            HashSet<string> backedUpFilesThisBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < itemsToDelete.Count; i++)
            {
                CapabilityItem item = itemsToDelete[i];
                if (item == null || string.IsNullOrEmpty(item.Id)) continue;

                switch (item.Kind)
                {
                    case CapabilityKind.Plugin:
                    {
                        string safePluginFolder = EncodeSafeCapabilityFolderName(item.Id);
                        string pDir = CombineAndEnsureUnderRoot(Path.Combine(configDir, "plugins"), safePluginFolder);
                        if (!Directory.Exists(pDir) && !string.Equals(safePluginFolder, item.Id, StringComparison.Ordinal) && IsSafeRelativePath(item.Id))
                        {
                            string legacyDir = CombineAndEnsureUnderRoot(Path.Combine(configDir, "plugins"), item.Id);
                            if (Directory.Exists(legacyDir)) pDir = legacyDir;
                        }
                        bool dirDeleted = false;
                        if (Directory.Exists(pDir))
                        {
                            dirDeleted = DeleteDirSafe(pDir);
                            if (!dirDeleted)
                                continue;
                        }

                        string cfgFile = Path.Combine(configDir, "config.json");
                        bool regRemoved = false;
                        if (File.Exists(cfgFile))
                        {
                            string existing = ReadAllTextShared(cfgFile);
                            string updated = UnifiedJson.DeleteObjectKeyPreservingComments(existing, "plugins", item.Id);
                            if (!string.Equals(existing, updated, StringComparison.Ordinal))
                            {
                                if (backedUpFilesThisBatch.Add(cfgFile))
                                    UnifiedJson.CreateTimestampedBackup(cfgFile, MaxTimestampedBackups);
                                SessionLifecycleService.WriteUtf8TextAtomicWithBackup(cfgFile, updated, false, null);
                                regRemoved = true;
                            }
                        }

                        if (dirDeleted || regRemoved) deletedCount++;
                        break;
                    }
                    case CapabilityKind.Skill:
                    {
                        string safeSkillFolder = EncodeSafeCapabilityFolderName(item.Id);
                        string sDir = CombineAndEnsureUnderRoot(Path.Combine(configDir, "skills"), safeSkillFolder);
                        if (!Directory.Exists(sDir) && !string.Equals(safeSkillFolder, item.Id, StringComparison.Ordinal) && IsSafeRelativePath(item.Id))
                        {
                            string legacyDir = CombineAndEnsureUnderRoot(Path.Combine(configDir, "skills"), item.Id);
                            if (Directory.Exists(legacyDir)) sDir = legacyDir;
                        }
                        if (Directory.Exists(sDir) && DeleteDirSafe(sDir)) deletedCount++;
                        break;
                    }
                    case CapabilityKind.Rule:
                    {
                        string targetFile = ResolveLocalRulePath(geminiPath, item.Id);
                        if (File.Exists(targetFile) && DeleteFileSafe(targetFile)) deletedCount++;
                        break;
                    }
                    case CapabilityKind.McpServer:
                    {
                        string mcpFile = Path.Combine(configDir, "mcp_config.json");
                        bool mcpRemoved = false;
                        if (File.Exists(mcpFile))
                        {
                            string existing = ReadAllTextShared(mcpFile);
                            string updated = UnifiedJson.DeleteObjectKeyPreservingComments(existing, "mcpServers", item.Id);
                            if (!string.Equals(existing, updated, StringComparison.Ordinal))
                            {
                                if (backedUpFilesThisBatch.Add(mcpFile))
                                    UnifiedJson.CreateTimestampedBackup(mcpFile, MaxTimestampedBackups);
                                SessionLifecycleService.WriteUtf8TextAtomicWithBackup(mcpFile, updated, false, null);
                                mcpRemoved = true;
                            }
                        }
                        string safeFolder = EncodeSafeCapabilityFolderName(item.Id);
                        string schemaDir = CombineAndEnsureUnderRoot(Path.Combine(geminiPath, "antigravity", "mcp"), safeFolder);
                        if (!Directory.Exists(schemaDir) && !string.Equals(safeFolder, item.Id, StringComparison.Ordinal) && IsSafeRelativePath(item.Id))
                        {
                            string legacySchema = CombineAndEnsureUnderRoot(Path.Combine(geminiPath, "antigravity", "mcp"), item.Id);
                            if (Directory.Exists(legacySchema)) schemaDir = legacySchema;
                        }
                        bool schemaRemoved = Directory.Exists(schemaDir) && DeleteDirSafe(schemaDir);
                        if (mcpRemoved || schemaRemoved) deletedCount++;
                        break;
                    }
                    case CapabilityKind.Hook:
                    {
                        string hooksFile = Path.Combine(configDir, "hooks.json");
                        string updatedHooksJson = null;
                        bool hookRemoved = false;
                        if (File.Exists(hooksFile))
                        {
                            string existing = ReadAllTextShared(hooksFile);
                            updatedHooksJson = UnifiedJson.DeleteObjectKeyPreservingComments(existing, null, item.Id);
                            if (!string.Equals(existing, updatedHooksJson, StringComparison.Ordinal))
                            {
                                if (backedUpFilesThisBatch.Add(hooksFile))
                                    UnifiedJson.CreateTimestampedBackup(hooksFile, MaxTimestampedBackups);
                                SessionLifecycleService.WriteUtf8TextAtomicWithBackup(hooksFile, updatedHooksJson, false, null);
                                hookRemoved = true;
                            }
                        }

                        bool scriptRemoved = false;
                        HashSet<string> stillReferenced = CollectReferencedScriptsFromHooksJson(updatedHooksJson);
                        for (int s = 0; s < item.CompanionScriptNames.Count; s++)
                        {
                            string scriptName = item.CompanionScriptNames[s];
                            if (string.IsNullOrEmpty(scriptName) || !IsSafeRelativePath(scriptName)) continue;
                            if (stillReferenced.Contains(scriptName)) continue;

                            string scriptPath = CombineAndEnsureUnderRoot(configDir, scriptName);
                            if (File.Exists(scriptPath) && DeleteFileSafe(scriptPath))
                                scriptRemoved = true;
                        }
                        if (hookRemoved || scriptRemoved) deletedCount++;
                        break;
                    }
                }
            }

            return deletedCount;
        }

        public static int DeleteCapabilitiesFromRepository(string repoPath, List<CapabilityItem> itemsToDelete)
        {
            if (string.IsNullOrEmpty(repoPath) || itemsToDelete == null || itemsToDelete.Count == 0)
                return 0;

            int deletedCount = 0;
            for (int i = 0; i < itemsToDelete.Count; i++)
            {
                CapabilityItem item = itemsToDelete[i];
                if (item == null || string.IsNullOrEmpty(item.Id)) continue;

                string safeFolder = EncodeSafeCapabilityFolderName(item.Id);
                switch (item.Kind)
                {
                    case CapabilityKind.Plugin:
                    {
                        string pDir = CombineAndEnsureUnderRoot(Path.Combine(repoPath, "plugins"), safeFolder);
                        if (DeleteDirSafe(pDir)) deletedCount++;
                        break;
                    }
                    case CapabilityKind.Skill:
                    {
                        string sDir = CombineAndEnsureUnderRoot(Path.Combine(repoPath, "skills"), safeFolder);
                        if (DeleteDirSafe(sDir)) deletedCount++;
                        break;
                    }
                    case CapabilityKind.Rule:
                    {
                        string rPath = ResolveRepoRulePath(repoPath, item.Id);
                        if (DeleteFileSafe(rPath)) deletedCount++;
                        break;
                    }
                    case CapabilityKind.McpServer:
                    {
                        string mDir = CombineAndEnsureUnderRoot(Path.Combine(repoPath, "mcp"), safeFolder);
                        if (DeleteDirSafe(mDir)) deletedCount++;
                        break;
                    }
                    case CapabilityKind.Hook:
                    {
                        string hDir = CombineAndEnsureUnderRoot(Path.Combine(repoPath, "hooks"), safeFolder);
                        if (DeleteDirSafe(hDir)) deletedCount++;
                        break;
                    }
                }
            }

            WriteRepositoryManifest(repoPath);
            return deletedCount;
        }

        // ══════════════════════════════════════════════════════════════════
        // 7. 重定向 MCP / Hook 脚本路径 (FIX P0-5: JSONC + timestamped backup)
        // ══════════════════════════════════════════════════════════════════
        public static bool RelocateCapabilityPath(
            string geminiPath,
            string repoPath,
            bool isLocalTarget,
            string homePath,
            CapabilityItem item,
            string oldPath,
            string newPath)
        {
            if (item == null || string.IsNullOrEmpty(item.Id) || string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath))
                return false;

            string oldFwd = oldPath.Replace('\\', '/');
            string newFwd = newPath.Replace('\\', '/');
            string oldWin = oldPath.Replace('/', '\\');
            string newWin = newPath.Replace('/', '\\');
            string oldEsc = UnifiedJson.EscapeString(oldWin);
            string newEsc = UnifiedJson.EscapeString(newWin);
            string newFwdJson = UnifiedJson.EscapeString(newFwd);

            if (isLocalTarget)
            {
                if (item.Kind == CapabilityKind.McpServer)
                {
                    string mcpFile = Path.Combine(geminiPath, "config", "mcp_config.json");
                    if (!File.Exists(mcpFile)) return false;
                    string text = ReadAllTextShared(mcpFile);
                    Dictionary<string, string> servers = ExtractObjectEntries(text, "mcpServers");
                    string origFrag;
                    if (!servers.TryGetValue(item.Id, out origFrag)) return false;

                    string updatedFrag = ReplacePathVariantsInJson(origFrag, oldFwd, newFwdJson, oldWin, oldEsc, newEsc);
                    if (string.Equals(origFrag, updatedFrag, StringComparison.Ordinal))
                        return false;

                    string merged = UnifiedJson.PatchObjectKeyPreservingComments(text, "mcpServers", item.Id, updatedFrag);
                    UnifiedJson.CreateTimestampedBackup(mcpFile, MaxTimestampedBackups);
                    SessionLifecycleService.WriteUtf8TextAtomicWithBackup(mcpFile, merged, false, null);
                    return true;
                }
                else if (item.Kind == CapabilityKind.Hook)
                {
                    string hooksFile = Path.Combine(geminiPath, "config", "hooks.json");
                    if (!File.Exists(hooksFile)) return false;
                    string text = ReadAllTextShared(hooksFile);
                    Dictionary<string, string> hooks = ExtractObjectEntries(text, null);
                    string origFrag;
                    if (!hooks.TryGetValue(item.Id, out origFrag)) return false;

                    string updatedFrag = ReplacePathVariantsInJson(origFrag, oldFwd, newFwdJson, oldWin, oldEsc, newEsc);
                    if (string.Equals(origFrag, updatedFrag, StringComparison.Ordinal))
                        return false;

                    string merged = UnifiedJson.PatchObjectKeyPreservingComments(text, null, item.Id, updatedFrag);
                    UnifiedJson.CreateTimestampedBackup(hooksFile, MaxTimestampedBackups);
                    SessionLifecycleService.WriteUtf8TextAtomicWithBackup(hooksFile, merged, false, null);
                    return true;
                }
            }
            else
            {
                string safeFolder = EncodeSafeCapabilityFolderName(item.Id);
                string entryFile;
                if (item.Kind == CapabilityKind.McpServer)
                    entryFile = CombineAndEnsureUnderRoot(Path.Combine(repoPath, "mcp"), safeFolder + "/server_entry.json");
                else if (item.Kind == CapabilityKind.Hook)
                    entryFile = CombineAndEnsureUnderRoot(Path.Combine(repoPath, "hooks"), safeFolder + "/hook_entry.json");
                else
                    return false;

                if (!File.Exists(entryFile)) return false;
                string raw = ReadAllTextShared(entryFile);
                string expanded = CapabilityPathNormalizer.ExpandHomePlaceholder(raw, homePath, true);
                string updated = ReplacePathVariantsInJson(expanded, oldFwd, newFwdJson, oldWin, oldEsc, newEsc);
                if (string.Equals(expanded, updated, StringComparison.Ordinal))
                    return false;

                string reNormalized = CapabilityPathNormalizer.NormalizeToHomePlaceholder(updated, homePath);
                if (string.Equals(raw, reNormalized, StringComparison.Ordinal))
                    return false;

                UnifiedJson.CreateTimestampedBackup(entryFile, MaxTimestampedBackups);
                SessionLifecycleService.WriteUtf8TextAtomicWithBackup(entryFile, reNormalized, false, null);
                return true;
            }

            return false;
        }

        private static string ReplacePathVariantsInJson(string text, string oldFwd, string newFwdJson, string oldWin, string oldEsc, string newEsc)
        {
            string res = text;
            res = CapabilityPathNormalizer.ReplaceIgnoreCase(res, oldEsc, newEsc);
            res = CapabilityPathNormalizer.ReplaceIgnoreCase(res, oldFwd, newFwdJson);
            if (!string.Equals(oldWin, oldEsc, StringComparison.Ordinal))
            {
                res = CapabilityPathNormalizer.ReplaceIgnoreCase(res, oldWin, newEsc);
            }
            return res;
        }

        // ══════════════════════════════════════════════════════════════════
        // 8. 内部扫描、Hook 脚本字面量匹配、签名与原子目录同步辅助
        // ══════════════════════════════════════════════════════════════════

        private static void CollectSafeFilesRecursive(
            string rootDir,
            string currentDir,
            int depth,
            HashSet<string> visitedFullDirs,
            List<string> results)
        {
            if (depth > MaxDirectoryWalkDepth || string.IsNullOrEmpty(currentDir) || !Directory.Exists(currentDir))
                return;

            string fullCur = Path.GetFullPath(currentDir);
            if (!visitedFullDirs.Add(fullCur))
                return;

            string[] files = Directory.GetFiles(currentDir, "*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                results.Add(files[i]);
            }

            string[] subDirs = Directory.GetDirectories(currentDir);
            for (int i = 0; i < subDirs.Length; i++)
            {
                string subName = Path.GetFileName(subDirs[i]);
                if (string.IsNullOrEmpty(subName) || ExcludedSubdirNames.Contains(subName))
                    continue;
                CollectSafeFilesRecursive(rootDir, subDirs[i], depth + 1, visitedFullDirs, results);
            }
        }

        private static void PopulateFilesFromDirectory(
            string rootDir,
            CapabilityItem item,
            string keyPrefix = "")
        {
            if (!Directory.Exists(rootDir) || item == null) return;

            List<string> files = new List<string>();
            CollectSafeFilesRecursive(rootDir, rootDir, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase), files);
            files.Sort(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Count; i++)
            {
                string f = files[i];
                string rel = f.Substring(rootDir.Length).TrimStart('\\', '/').Replace('\\', '/');
                if (!IsSafeRelativePath(rel) || IsExcludedFileOrPath(rel))
                    continue;

                string mapKey = keyPrefix + rel;
                try
                {
                    long len = new FileInfo(f).Length;
                    if (len > LazyLargeFileThresholdBytes)
                    {
                        item.FilesMap[mapKey] = null;
                        item.SourceFilePaths[mapKey] = f;
                    }
                    else
                    {
                        item.FilesMap[mapKey] = ReadFileBytesShared(f);
                    }

                    DateTime lw = File.GetLastWriteTimeUtc(f);
                    UpdateMaxLastModified(item, lw);
                }
                catch { }
            }
        }

        private static CapabilityItem CreateRuleItemFromFile(string fullPath, string ruleId, string mapKey, string homePath, string localGeminiPath)
        {
            try
            {
                byte[] bytes = ReadFileBytesShared(fullPath);
                DateTime lastMod = File.GetLastWriteTimeUtc(fullPath);
                return CreateRuleItemFromBytes(ruleId, mapKey, bytes, lastMod, homePath, homePath, localGeminiPath);
            }
            catch
            {
                return null;
            }
        }

        private static CapabilityItem CreateRuleItemFromBytes(
            string ruleId,
            string mapKey,
            byte[] bytes,
            DateTime lastMod,
            string sourceHome,
            string currentHome,
            string localGeminiPath)
        {
            CapabilityItem item = new CapabilityItem
            {
                Kind = CapabilityKind.Rule,
                Id = ruleId,
                DisplayName = ruleId,
                LastModified = lastMod
            };

            string text;
            bool hadBom;
            if (TryDecodeStrictUtf8(bytes, out text, out hadBom))
            {
                string normalizedText = CapabilityPathNormalizer.NormalizeToHomePlaceholder(text, sourceHome);
                item.FilesMap[mapKey] = EncodeUtf8WithOptionalBom(normalizedText, hadBom);
                item.Description = ExtractFirstHeadingOrLine(StripUtf8Bom(normalizedText));
            }
            else
            {
                item.FilesMap[mapKey] = bytes ?? new byte[0];
                string fallbackText = bytes != null ? Encoding.Default.GetString(bytes) : string.Empty;
                item.Description = ExtractFirstHeadingOrLine(StripUtf8Bom(fallbackText));
            }
            FinalizeCapabilitySignatureAndPreview(item, currentHome, localGeminiPath);
            return item;
        }

        private static bool IsExcludedConfigRootFile(string fn)
        {
            return string.IsNullOrEmpty(fn) ||
                   string.Equals(fn, "config.json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fn, "mcp_config.json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fn, "hooks.json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fn, "mcp_config.json.backup", StringComparison.OrdinalIgnoreCase) ||
                   fn.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ||
                   fn.IndexOf(".bak.", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   fn.EndsWith(".backup", StringComparison.OrdinalIgnoreCase) ||
                   fn.StartsWith(".", StringComparison.OrdinalIgnoreCase) ||
                   IsExcludedFileOrPath(fn);
        }

        private static bool HookFragmentReferencesScript(string hookFragmentJson, string scriptFileName)
        {
            if (string.IsNullOrEmpty(hookFragmentJson) || string.IsNullOrEmpty(scriptFileName))
                return false;

            List<string> literals = ExtractAllStringLiterals(hookFragmentJson);
            for (int i = 0; i < literals.Count; i++)
            {
                string lit = literals[i];
                if (string.IsNullOrEmpty(lit)) continue;

                List<string> tokens = TokenizeCommandString(lit);
                for (int t = 0; t < tokens.Count; t++)
                {
                    string tok = tokens[t];
                    if (string.IsNullOrEmpty(tok)) continue;

                    string normTok = tok.Replace('\\', '/');
                    string baseName = Path.GetFileName(normTok);
                    if (!string.Equals(baseName, scriptFileName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (normTok.IndexOf('/') < 0)
                        return true;

                    string parentDir = Path.GetDirectoryName(normTok.Replace('/', '\\'));
                    string parentLeaf = !string.IsNullOrEmpty(parentDir) ? Path.GetFileName(parentDir) : string.Empty;
                    if (string.Equals(parentLeaf, "config", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        private static List<string> TokenizeCommandString(string commandLine)
        {
            List<string> tokens = new List<string>();
            if (string.IsNullOrEmpty(commandLine)) return tokens;

            int i = 0;
            int len = commandLine.Length;
            while (i < len)
            {
                while (i < len && char.IsWhiteSpace(commandLine[i])) i++;
                if (i >= len) break;

                char quote = '\0';
                StringBuilder cur = new StringBuilder();
                while (i < len)
                {
                    char c = commandLine[i];
                    if (quote == '\0')
                    {
                        if (char.IsWhiteSpace(c)) break;
                        if (c == '"' || c == '\'') quote = c;
                        else cur.Append(c);
                    }
                    else
                    {
                        if (c == quote) quote = '\0';
                        else cur.Append(c);
                    }
                    i++;
                }

                if (cur.Length > 0) tokens.Add(cur.ToString());
            }
            return tokens;
        }

        private static HashSet<string> CollectReferencedScriptsFromHooksJson(string hooksJson)
        {
            HashSet<string> refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(hooksJson)) return refs;

            try
            {
                List<KeyValuePair<string, string>> remainingHooks = ExtractObjectEntriesOrdered(hooksJson, null);
                for (int i = 0; i < remainingHooks.Count; i++)
                {
                    List<string> literals = ExtractAllStringLiterals(remainingHooks[i].Value);
                    for (int l = 0; l < literals.Count; l++)
                    {
                        List<string> toks = TokenizeCommandString(literals[l]);
                        for (int t = 0; t < toks.Count; t++)
                        {
                            string fn = Path.GetFileName(toks[t].Replace('\\', '/'));
                            if (!string.IsNullOrEmpty(fn)) refs.Add(fn);
                        }
                    }
                }
            }
            catch { }

            return refs;
        }

        private static void DiscoverAndBundleHookScripts(string geminiPath, string hookRawJson, CapabilityItem hookItem)
        {
            string configDir = Path.Combine(geminiPath, "config");
            if (!Directory.Exists(configDir) || string.IsNullOrEmpty(hookRawJson)) return;

            string[] files = Directory.GetFiles(configDir);
            for (int i = 0; i < files.Length; i++)
            {
                string fn = Path.GetFileName(files[i]);
                if (IsExcludedConfigRootFile(fn)) continue;

                if (HookFragmentReferencesScript(hookRawJson, fn))
                {
                    hookItem.CompanionScriptNames.Add(fn);
                    try
                    {
                        long len = new FileInfo(files[i]).Length;
                        string mapKey = "scripts/" + fn;
                        if (len > LazyLargeFileThresholdBytes)
                        {
                            hookItem.FilesMap[mapKey] = null;
                            hookItem.SourceFilePaths[mapKey] = files[i];
                        }
                        else
                        {
                            hookItem.FilesMap[mapKey] = ReadFileBytesShared(files[i]);
                        }
                        UpdateMaxLastModified(hookItem, File.GetLastWriteTimeUtc(files[i]));
                    }
                    catch { }
                }
            }
        }

        private static void ParsePluginMetadata(CapabilityItem item)
        {
            byte[] pjBytes = item.GetFileBytes("plugin.json");
            if (pjBytes != null)
            {
                string json = Encoding.UTF8.GetString(pjBytes);
                string disp = DirectStreamArchiver.ExtractJsonValue(json, "displayName");
                string desc = DirectStreamArchiver.ExtractJsonValue(json, "description");
                if (!string.IsNullOrEmpty(disp)) item.DisplayName = string.Format("{0} ({1})", item.Id, disp);
                if (!string.IsNullOrEmpty(desc)) item.Description = desc;
            }

            List<string> subAssets = new List<string>();
            foreach (string k in item.FilesMap.Keys)
            {
                if (k.StartsWith("skills/", StringComparison.OrdinalIgnoreCase) && k.EndsWith("/SKILL.md", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = k.Split('/');
                    if (parts.Length >= 2) subAssets.Add("Skill:" + parts[1]);
                }
                else if (k.StartsWith("rules/", StringComparison.OrdinalIgnoreCase) && k.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    subAssets.Add("Rule:" + Path.GetFileName(k));
                }
                else if (string.Equals(k, "mcp_config.json", StringComparison.OrdinalIgnoreCase))
                {
                    subAssets.Add("MCP");
                }
                else if (string.Equals(k, "hooks.json", StringComparison.OrdinalIgnoreCase))
                {
                    subAssets.Add("Hook");
                }
            }

            if (subAssets.Count > 0)
            {
                string subSummary = "内含: " + string.Join(", ", subAssets.ToArray());
                item.Description = string.IsNullOrEmpty(item.Description) ? subSummary : (subSummary + " | " + item.Description);
            }
        }

        private static void ParseSkillMetadata(CapabilityItem item)
        {
            byte[] mdBytes = item.GetFileBytes("SKILL.md");
            if (mdBytes == null) return;

            string md;
            bool dummyBom;
            if (!TryDecodeStrictUtf8(mdBytes, out md, out dummyBom))
            {
                md = Encoding.Default.GetString(mdBytes);
            }
            md = StripUtf8Bom(md ?? string.Empty);

            string parsedDesc = ExtractYamlFrontmatterDescription(md);
            if (!string.IsNullOrEmpty(parsedDesc))
            {
                item.Description = parsedDesc;
            }
            else
            {
                item.Description = ExtractFirstHeadingOrLine(md);
            }
        }

        private static string ExtractYamlFrontmatterDescription(string md)
        {
            if (string.IsNullOrEmpty(md)) return string.Empty;
            string clean = StripUtf8Bom(md);
            Match fmMatch = Regex.Match(clean, "^\\s*---\\r?\\n([\\s\\S]*?)\\r?\\n---(?:\\r?\\n|$)");
            if (!fmMatch.Success) return string.Empty;

            string yaml = fmMatch.Groups[1].Value.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = yaml.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                Match km = Regex.Match(line, "^description\\s*:(.*)$", RegexOptions.IgnoreCase);
                if (!km.Success) continue;

                string rest = km.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(rest) || Regex.IsMatch(rest, "^[>|][+-]?\\d*$"))
                {
                    StringBuilder blockSb = new StringBuilder();
                    for (int j = i + 1; j < lines.Length; j++)
                    {
                        string sub = lines[j];
                        if (string.IsNullOrWhiteSpace(sub))
                        {
                            if (blockSb.Length > 0) continue;
                            continue;
                        }
                        if (sub[0] != ' ' && sub[0] != '\t')
                            break;
                        if (blockSb.Length > 0) blockSb.Append(' ');
                        blockSb.Append(sub.Trim());
                    }
                    return blockSb.ToString().Trim();
                }

                if ((rest.Length >= 2 && rest[0] == '"' && rest[rest.Length - 1] == '"') ||
                    (rest.Length >= 2 && rest[0] == '\'' && rest[rest.Length - 1] == '\''))
                {
                    return rest.Substring(1, rest.Length - 2).Trim();
                }
                return rest;
            }
            return string.Empty;
        }

        private static void ParseMcpDescription(CapabilityItem item)
        {
            if (string.IsNullOrEmpty(item.RawJsonFragment)) return;
            string cmd = DirectStreamArchiver.ExtractJsonValue(item.RawJsonFragment, "command");
            string url = DirectStreamArchiver.ExtractJsonValue(item.RawJsonFragment, "url");
            int schemaCount = item.FilesMap.Count;
            if (!string.IsNullOrEmpty(cmd))
                item.Description = string.Format("命令: {0} ({1} 个 Schema 缓存)", cmd, schemaCount);
            else if (!string.IsNullOrEmpty(url))
                item.Description = string.Format("远端端点: {0} ({1} 个 Schema 缓存)", url, schemaCount);
            else
                item.Description = string.Format("MCP 服务配置 ({0} 个 Schema 缓存)", schemaCount);
        }

        private static readonly HashSet<string> NonHookEventKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "enabled", "disabled", "description", "name", "timeout", "timeoutMs", "async", "blocking", "matcher", "env"
        };

        private static void ParseHookDescription(CapabilityItem item)
        {
            List<KeyValuePair<string, string>> events = ExtractObjectEntriesOrdered(item.RawJsonFragment, null);
            List<string> evNames = new List<string>();
            for (int i = 0; i < events.Count; i++)
            {
                string key = events[i].Key;
                if (!string.IsNullOrEmpty(key) && !NonHookEventKeys.Contains(key))
                    evNames.Add(key);
            }

            string evStr = evNames.Count > 0 ? ("触发点: " + string.Join(", ", evNames.ToArray())) : "生命周期钩子";
            if (item.CompanionScriptNames.Count > 0)
            {
                evStr += " | 伴随脚本: " + string.Join(", ", item.CompanionScriptNames.ToArray());
            }
            item.Description = evStr;
        }

        private static string ComputeSha256HexStream(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(fs);
                StringBuilder hex = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) hex.Append(hash[i].ToString("x2"));
                return hex.ToString();
            }
        }

        private static string ComputeSha256HexBytes(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(data ?? new byte[0]);
                StringBuilder hex = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) hex.Append(hash[i].ToString("x2"));
                return hex.ToString();
            }
        }

        private static readonly UTF8Encoding StrictUtf8NoBom = new UTF8Encoding(false, true);

        public static string EnsureContentSignature(CapabilityItem item, string currentHomePath, string localGeminiPath)
        {
            if (item == null) return string.Empty;
            if (string.IsNullOrEmpty(item.ContentSignature))
            {
                FinalizeCapabilitySignatureAndPreview(item, currentHomePath, localGeminiPath);
            }
            return item.ContentSignature ?? string.Empty;
        }

        private static DateTime ToUtcTimestamp(DateTime dt)
        {
            if (dt == DateTime.MinValue || dt.Year < 1980) return DateTime.MinValue;
            if (dt.Kind == DateTimeKind.Utc) return dt;
            return DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime();
        }

        private static DateTime ConvertZipTimeToUtc(DateTimeOffset dto)
        {
            if (dto.Year < 1980) return DateTime.MinValue;
            try
            {
                return dto.ToUniversalTime().UtcDateTime;
            }
            catch
            {
                return ToUtcTimestamp(dto.DateTime);
            }
        }

        private static void UpdateMaxLastModified(CapabilityItem item, DateTime candidate)
        {
            if (item == null) return;
            DateTime utc = ToUtcTimestamp(candidate);
            if (utc == DateTime.MinValue || utc.Year < 1980) return;
            DateTime curUtc = ToUtcTimestamp(item.LastModified);
            if (curUtc == DateTime.MinValue || curUtc.Year < 1980 || utc > curUtc)
            {
                item.LastModified = utc;
            }
        }

        private static bool TryDecodeStrictUtf8(byte[] data, out string text, out bool hadBom)
        {
            hadBom = false;
            if (data == null || data.Length == 0)
            {
                text = string.Empty;
                return true;
            }

            int offset = 0;
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            {
                hadBom = true;
                offset = 3;
            }

            try
            {
                text = StrictUtf8NoBom.GetString(data, offset, data.Length - offset);
                return true;
            }
            catch (DecoderFallbackException)
            {
                text = null;
                return false;
            }
            catch (ArgumentException)
            {
                text = null;
                return false;
            }
        }

        private static byte[] EncodeUtf8WithOptionalBom(string text, bool includeBom)
        {
            string clean = StripUtf8Bom(text ?? string.Empty);
            byte[] body = StrictUtf8NoBom.GetBytes(clean);
            if (!includeBom) return body;

            byte[] withBom = new byte[3 + body.Length];
            withBom[0] = 0xEF;
            withBom[1] = 0xBB;
            withBom[2] = 0xBF;
            Buffer.BlockCopy(body, 0, withBom, 3, body.Length);
            return withBom;
        }

        private static void FinalizeCapabilitySignatureAndPreview(CapabilityItem item, string currentHomePath, string localGeminiPath)
        {
            if (item.Kind == CapabilityKind.McpServer || item.Kind == CapabilityKind.Hook)
            {
                List<string> refPaths;
                List<string> missPaths;
                CapabilityPathNormalizer.DetectReferencedAndMissingPaths(
                    item.RawJsonFragment, currentHomePath, localGeminiPath, item.CompanionScriptNames,
                    out refPaths, out missPaths
                );
                item.ReferencedLocalPaths = refPaths;
                item.MissingLocalPaths = missPaths;
            }

            StringBuilder sigBuilder = new StringBuilder();
            sigBuilder.Append(item.Kind.ToString()).Append('|').Append((item.Id ?? string.Empty).ToLowerInvariant()).Append('\n');
            if (!string.IsNullOrEmpty(item.RawJsonFragment))
            {
                string normFrag = CapabilityPathNormalizer.NormalizeToHomePlaceholder(item.RawJsonFragment, currentHomePath);
                sigBuilder.Append("JSON:").Append(NormalizeLineEndingsAndWhitespace(normFrag)).Append('\n');
            }

            List<string> sortedKeys = new List<string>(item.FilesMap.Keys);
            sortedKeys.Sort(StringComparer.OrdinalIgnoreCase);

            StringBuilder previewBuilder = new StringBuilder();
            previewBuilder.AppendLine("══════════════════════════════════════════════════════════════");
            previewBuilder.AppendLine(string.Format("  [{0}] {1}", CapabilityItem.GetKindShortTag(item.Kind), item.DisplayName));
            if (!string.IsNullOrEmpty(item.Description))
                previewBuilder.AppendLine("  摘要: " + item.Description);
            if (item.ReferencedLocalPaths.Count > 0)
                previewBuilder.AppendLine("  关联本机路径: " + string.Join("; ", item.ReferencedLocalPaths.ToArray()));
            if (item.HasMissingPaths)
                previewBuilder.AppendLine("  ⚠️ 警告 - 当前机器缺失以下目标路径: " + string.Join("; ", item.MissingLocalPaths.ToArray()));
            previewBuilder.AppendLine("══════════════════════════════════════════════════════════════");

            if (!string.IsNullOrEmpty(item.RawJsonFragment))
            {
                previewBuilder.AppendLine("\r\n--- [JSON 配置节点片段] ---");
                previewBuilder.AppendLine(CapabilityPathNormalizer.ExpandHomePlaceholder(item.RawJsonFragment, currentHomePath, true));
            }

            for (int i = 0; i < sortedKeys.Count; i++)
            {
                string k = sortedKeys[i];
                sigBuilder.Append("FILE:").Append(k.ToLowerInvariant()).Append(':');

                string largeDiskPath;
                if (item.TryGetDiskSourcePath(k, out largeDiskPath))
                {
                    sigBuilder.Append("SHA256:").Append(ComputeSha256HexStream(largeDiskPath)).Append('\n');
                    if (previewBuilder.Length < 12000)
                    {
                        previewBuilder.AppendLine(string.Format("\r\n--- [文件: {0}] (流式大文件，共 {1} 字节) ---", k, new FileInfo(largeDiskPath).Length));
                    }
                    continue;
                }

                byte[] data = item.FilesMap[k] ?? new byte[0];
                string txt;
                bool hadBom;
                if (IsTextFile(k) && TryDecodeStrictUtf8(data, out txt, out hadBom))
                {
                    txt = StripUtf8Bom(txt);
                    string normTxt = CapabilityPathNormalizer.NormalizeToHomePlaceholder(txt, currentHomePath);
                    sigBuilder.Append(NormalizeLineEndingsAndWhitespace(normTxt)).Append('\n');

                    if (previewBuilder.Length < 12000)
                    {
                        previewBuilder.AppendLine(string.Format("\r\n--- [文件: {0}] ---", k));
                        previewBuilder.AppendLine(txt.Length > 4000 ? (txt.Substring(0, 4000) + "\r\n...(内容已截断)...") : txt);
                    }
                }
                else
                {
                    sigBuilder.Append("BIN:").Append(ComputeSha256HexBytes(data)).Append('\n');
                    if (IsTextFile(k) && previewBuilder.Length < 12000)
                    {
                        string fallbackTxt = StripUtf8Bom(Encoding.Default.GetString(data));
                        previewBuilder.AppendLine(string.Format("\r\n--- [文件: {0} (非 UTF-8 原始字节保留)] ---", k));
                        previewBuilder.AppendLine(fallbackTxt.Length > 4000 ? (fallbackTxt.Substring(0, 4000) + "\r\n...(内容已截断)...") : fallbackTxt);
                    }
                }
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sigBuilder.ToString()));
                StringBuilder hex = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) hex.Append(hash[i].ToString("x2"));
                item.ContentSignature = hex.ToString();
            }

            item.PreviewText = previewBuilder.ToString();
        }

        private static string NormalizeLineEndingsAndWhitespace(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return StripUtf8Bom(s).Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        }

        private static string StripUtf8Bom(string s)
        {
            if (!string.IsNullOrEmpty(s) && s[0] == '\uFEFF')
                return s.Substring(1);
            return s;
        }

        private static bool IsTextFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            string leaf = Path.GetFileName(fileName);
            if (!string.IsNullOrEmpty(leaf) && leaf.StartsWith(".env", StringComparison.OrdinalIgnoreCase))
                return true;

            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext == ".md" || ext == ".json" || ext == ".mjs" || ext == ".cjs" ||
                   ext == ".mts" || ext == ".cts" || ext == ".js" || ext == ".jsx" ||
                   ext == ".ts" || ext == ".tsx" || ext == ".py" || ext == ".sh" ||
                   ext == ".bash" || ext == ".zsh" || ext == ".ps1" || ext == ".ps1xml" ||
                   ext == ".psm1" || ext == ".psd1" || ext == ".bat" || ext == ".cmd" ||
                   ext == ".txt" || ext == ".yaml" || ext == ".yml" || ext == ".toml" ||
                   ext == ".ini" || ext == ".conf" || ext == ".cfg" || ext == ".xml" ||
                   ext == ".css" || ext == ".html" || ext == ".htm" ||
                   fileName.EndsWith(".geminirules", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsJsonFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            return string.Equals(Path.GetExtension(fileName), ".json", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractFirstHeadingOrLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string[] lines = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i].Trim();
                if (l == "---" || l.StartsWith("name:", StringComparison.OrdinalIgnoreCase)) continue;
                l = l.TrimStart('#', ' ', '*', '-');
                if (!string.IsNullOrEmpty(l))
                {
                    return l.Length > 65 ? l.Substring(0, 65) + "..." : l;
                }
            }
            return string.Empty;
        }

        private static HashSet<string> CollectIncomingRelPaths(CapabilityItem item, string stripPrefix)
        {
            HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (item == null || item.FilesMap == null) return set;

            foreach (string key in item.FilesMap.Keys)
            {
                string rel = key.Replace('\\', '/');
                if (!string.IsNullOrEmpty(stripPrefix))
                {
                    if (!rel.StartsWith(stripPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    rel = rel.Substring(stripPrefix.Length);
                }
                if (!string.IsNullOrEmpty(rel)) set.Add(rel);
            }
            return set;
        }

        private static void CleanupStaleFilesInDirectory(string targetDir, HashSet<string> incomingRelPaths)
        {
            if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir)) return;

            List<string> existingFiles = new List<string>();
            CollectSafeFilesRecursive(targetDir, targetDir, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase), existingFiles);
            for (int i = 0; i < existingFiles.Count; i++)
            {
                string f = existingFiles[i];
                string rel = f.Substring(targetDir.Length).TrimStart('\\', '/').Replace('\\', '/');
                if (IsExcludedFileOrPath(rel)) continue;
                if (!incomingRelPaths.Contains(rel))
                {
                    DeleteFileSafe(f);
                }
            }

            PruneEmptyDirectories(targetDir, true);
        }

        private static void PruneEmptyDirectories(string dirPath, bool isRoot)
        {
            if (!Directory.Exists(dirPath)) return;
            string[] subDirs = Directory.GetDirectories(dirPath);
            for (int i = 0; i < subDirs.Length; i++)
            {
                string subName = Path.GetFileName(subDirs[i]);
                if (ExcludedSubdirNames.Contains(subName)) continue;
                PruneEmptyDirectories(subDirs[i], false);
            }

            if (!isRoot &&
                Directory.GetFiles(dirPath).Length == 0 &&
                Directory.GetDirectories(dirPath).Length == 0)
            {
                try { Directory.Delete(dirPath, false); } catch { }
            }
        }

        private static int WriteCapabilityFilesToDirectory(
            CapabilityItem item,
            string targetBaseDir,
            string stripPrefix,
            string homePath,
            bool normalizeToPlaceholder,
            bool jsonTransformAwareness)
        {
            int count = 0;
            foreach (string key in item.FilesMap.Keys)
            {
                string rel = key.Replace('\\', '/');
                if (!string.IsNullOrEmpty(stripPrefix))
                {
                    if (!rel.StartsWith(stripPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    rel = rel.Substring(stripPrefix.Length);
                }
                if (string.IsNullOrEmpty(rel)) continue;

                string dest = CombineAndEnsureUnderRoot(targetBaseDir, rel);
                WriteCapabilitySingleFile(item, key, dest, homePath, normalizeToPlaceholder);
                count++;
            }
            return count;
        }

        private static void WriteCapabilitySingleFile(
            CapabilityItem item,
            string mapKey,
            string destPath,
            string homePath,
            bool normalizeToPlaceholder)
        {
            string dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string largeSrcPath;
            if (item.TryGetDiskSourcePath(mapKey, out largeSrcPath))
            {
                if (!IsTextFile(destPath) || string.IsNullOrEmpty(homePath))
                {
                    string tmpDest = destPath + ".tmp." + Guid.NewGuid().ToString("N").Substring(0, 8);
                    try
                    {
                        using (FileStream srcFs = new FileStream(largeSrcPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        using (FileStream dstFs = new FileStream(tmpDest, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            srcFs.CopyTo(dstFs);
                            dstFs.Flush(true);
                        }

                        if (File.Exists(destPath))
                        {
                            try { File.SetAttributes(destPath, FileAttributes.Normal); } catch { }
                            File.Replace(tmpDest, destPath, null, true);
                        }
                        else
                        {
                            File.Move(tmpDest, destPath);
                        }
                    }
                    finally
                    {
                        try { if (File.Exists(tmpDest)) File.Delete(tmpDest); } catch { }
                    }
                    return;
                }
            }

            byte[] rawBytes = item.GetFileBytes(mapKey);
            if (rawBytes != null && IsTextFile(destPath))
            {
                string txt;
                bool hadBom;
                if (TryDecodeStrictUtf8(rawBytes, out txt, out hadBom))
                {
                    string transformed = !string.IsNullOrEmpty(homePath)
                        ? (normalizeToPlaceholder
                            ? CapabilityPathNormalizer.NormalizeToHomePlaceholder(txt, homePath)
                            : CapabilityPathNormalizer.ExpandHomePlaceholder(txt, homePath, IsJsonFile(destPath)))
                        : txt;

                    bool writeBom = hadBom || string.Equals(Path.GetExtension(destPath), ".ps1", StringComparison.OrdinalIgnoreCase);
                    byte[] encoded = EncodeUtf8WithOptionalBom(transformed, writeBom);
                    SessionLifecycleService.WriteBytesAtomicWithBackup(destPath, encoded, null);
                    return;
                }
            }

            SessionLifecycleService.WriteBytesAtomicWithBackup(destPath, rawBytes ?? new byte[0], null);
        }

        internal static void WriteRepositoryManifest(string repoPath)
        {
            try
            {
                if (!Directory.Exists(repoPath)) return;
                string manifestPath = Path.Combine(repoPath, "manifest.json");
                string nowIso = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                string user = Environment.UserName ?? string.Empty;

                if (File.Exists(manifestPath))
                {
                    string existing = ReadAllTextShared(manifestPath);
                    object parsed;
                    if (UnifiedJson.TryParse(existing, out parsed) && UnifiedJson.AsDict(parsed) != null)
                    {
                        string patched = UnifiedJson.PatchObjectKeyPreservingComments(
                            existing, null, "updated_at", "\"" + UnifiedJson.EscapeString(nowIso) + "\"");
                        patched = UnifiedJson.PatchObjectKeyPreservingComments(
                            patched, null, "last_updated_by", "\"" + UnifiedJson.EscapeString(user) + "\"");
                        SessionLifecycleService.WriteUtf8TextAtomicWithBackup(manifestPath, patched, false, null);
                        return;
                    }
                }

                string json = string.Format(
                    "{{\r\n  \"schemaVersion\": 2,\r\n  \"vaultType\": \"portable\",\r\n  \"updated_at\": \"{0}\",\r\n  \"last_updated_by\": \"{1}\"\r\n}}\r\n",
                    UnifiedJson.EscapeString(nowIso),
                    UnifiedJson.EscapeString(user)
                );
                SessionLifecycleService.WriteUtf8TextAtomicWithBackup(manifestPath, json, false, null);
            }
            catch { }
        }

        internal static string ResolveLocalRulePath(string geminiPath, string ruleId)
        {
            if (AllowedRootRuleNames.Contains(ruleId))
                return CombineAndEnsureUnderRoot(geminiPath, ruleId);

            if (ruleId.StartsWith("rules/", StringComparison.OrdinalIgnoreCase))
            {
                string rel = ruleId.Substring("rules/".Length);
                return CombineAndEnsureUnderRoot(Path.Combine(geminiPath, "config", "rules"), rel);
            }

            throw new InvalidOperationException("非法的规则标识路径: " + ruleId);
        }

        internal static string ResolveRepoRulePath(string repoPath, string ruleId)
        {
            if (AllowedRootRuleNames.Contains(ruleId))
                return CombineAndEnsureUnderRoot(Path.Combine(repoPath, "rules", "_root"), ruleId);

            if (ruleId.StartsWith("rules/", StringComparison.OrdinalIgnoreCase))
            {
                string rel = ruleId.Substring("rules/".Length);
                return CombineAndEnsureUnderRoot(Path.Combine(repoPath, "rules", "config"), rel);
            }

            throw new InvalidOperationException("非法的仓库规则标识路径: " + ruleId);
        }

        private static byte[] ReadZipEntryBytes(ZipArchiveEntry entry)
        {
            using (MemoryStream ms = new MemoryStream())
            using (Stream s = entry.Open())
            {
                s.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private static bool DeleteFileSafe(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                return true;
            }
            catch { return false; }
        }

        private static bool DeleteDirSafe(string dirPath)
        {
            try
            {
                if (!Directory.Exists(dirPath)) return false;
                string[] files = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    try { File.SetAttributes(files[i], FileAttributes.Normal); } catch { }
                }
                Directory.Delete(dirPath, true);
                return true;
            }
            catch { return false; }
        }
    }

    /// <summary>
    /// Backwards-compatibility facade forwarding to CapabilityVaultService.
    /// </summary>
    public static class CapabilityRepositoryManager
    {
        static CapabilityRepositoryManager()
        {
            CapabilityVaultService.EnsureZipCapabilityHookRegistered();
        }

        public static string ResolveDefaultRepositoryPath()
        {
            return CapabilityVaultService.ResolveDefaultRepositoryPath();
        }

        public static string ResolveDefaultVaultPath(string explicitPath, string homePath)
        {
            return CapabilityVaultService.ResolveDefaultVaultPath(explicitPath, homePath);
        }

        public static string EncodeSafeCapabilityFolderName(string itemId)
        {
            return CapabilityVaultService.EncodeSafeCapabilityFolderName(itemId);
        }

        public static string DecodeSafeCapabilityFolderName(string folderName)
        {
            return CapabilityVaultService.DecodeSafeCapabilityFolderName(folderName);
        }

        public static bool IsSafeRelativePath(string relPath)
        {
            return CapabilityVaultService.IsSafeRelativePath(relPath);
        }

        public static string CombineAndEnsureUnderRoot(string rootDir, string relPath)
        {
            return CapabilityVaultService.CombineAndEnsureUnderRoot(rootDir, relPath);
        }

        public static bool IsExcludedSubdirName(string dirName)
        {
            return CapabilityVaultService.IsExcludedSubdirName(dirName);
        }

        public static bool IsExcludedFileOrPath(string relPath)
        {
            return CapabilityVaultService.IsExcludedFileOrPath(relPath);
        }

        public static List<CapabilityItem> ScanLocalCapabilities(string geminiPath, string homePath)
        {
            return CapabilityVaultService.ScanLocalCapabilities(geminiPath, homePath);
        }

        public static List<CapabilityItem> ScanRepositoryCapabilities(string repoPath, string currentHomePath, string localGeminiPath = null)
        {
            return CapabilityVaultService.ScanRepositoryCapabilities(repoPath, currentHomePath, localGeminiPath);
        }

        public static List<CapabilityItem> ScanZipArchiveCapabilities(
            ZipArchive archive,
            string sourceHome,
            string currentHomePath,
            string localGeminiPath = null)
        {
            return CapabilityVaultService.ScanZipArchiveCapabilities(archive, sourceHome, currentHomePath, localGeminiPath);
        }

        public static int PushCapabilitiesToRepository(string repoPath, string sourceHomePath, List<CapabilityItem> selectedItems)
        {
            return CapabilityVaultService.PushCapabilitiesToRepository(repoPath, sourceHomePath, selectedItems);
        }

        public static int ImportCapabilitiesToLocal(string geminiPath, string targetHomePath, List<CapabilityItem> selectedItems)
        {
            return CapabilityVaultService.ImportCapabilitiesToLocal(geminiPath, targetHomePath, selectedItems);
        }

        public static int DeleteCapabilitiesFromLocal(string geminiPath, List<CapabilityItem> itemsToDelete)
        {
            return CapabilityVaultService.DeleteCapabilitiesFromLocal(geminiPath, itemsToDelete);
        }

        public static int DeleteCapabilitiesFromRepository(string repoPath, List<CapabilityItem> itemsToDelete)
        {
            return CapabilityVaultService.DeleteCapabilitiesFromRepository(repoPath, itemsToDelete);
        }

        public static bool RelocateCapabilityPath(
            string geminiPath,
            string repoPath,
            bool isLocalTarget,
            string homePath,
            CapabilityItem item,
            string oldPath,
            string newPath)
        {
            return CapabilityVaultService.RelocateCapabilityPath(geminiPath, repoPath, isLocalTarget, homePath, item, oldPath, newPath);
        }

        public static string EnsureContentSignature(CapabilityItem item, string currentHomePath, string localGeminiPath)
        {
            return CapabilityVaultService.EnsureContentSignature(item, currentHomePath, localGeminiPath);
        }
    }

    /// <summary>
    /// 能力同步三向基线签名存储（记录上次已对齐时的 ContentSignature，用于精准判定仓库较新 vs 本机较新）
    /// </summary>
    public static class CapabilitySyncBaselineStore
    {
        public static string GetDefaultStorePath()
        {
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localApp))
            {
                localApp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? Path.GetTempPath(), "AppData", "Local");
            }
            return Path.Combine(localApp, "AntigravityCompanion", "cap_sync_state.json");
        }

        public static string GetVaultScopedStorePath(string baseStorePath, string vaultPath)
        {
            string safeBase = string.IsNullOrEmpty(baseStorePath) ? GetDefaultStorePath() : baseStorePath;
            if (string.IsNullOrWhiteSpace(vaultPath)) return safeBase;

            string normVault = vaultPath.Trim();
            try { normVault = Path.GetFullPath(normVault); } catch { }
            normVault = normVault.TrimEnd('\\', '/').ToLowerInvariant();

            string hashHex;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(normVault));
                StringBuilder sb = new StringBuilder(16);
                for (int i = 0; i < 6 && i < hash.Length; i++)
                    sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                hashHex = sb.ToString();
            }

            string dir = Path.GetDirectoryName(safeBase);
            string stem = Path.GetFileNameWithoutExtension(safeBase);
            string ext = Path.GetExtension(safeBase);
            if (string.IsNullOrEmpty(ext)) ext = ".json";
            string fileName = stem + "." + hashHex + ext;
            return string.IsNullOrEmpty(dir) ? fileName : Path.Combine(dir, fileName);
        }

        public static Dictionary<string, string> Load(string customPath)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string path = string.IsNullOrEmpty(customPath) ? GetDefaultStorePath() : customPath;
            if (!File.Exists(path)) return map;

            try
            {
                string raw;
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                {
                    raw = sr.ReadToEnd();
                }
                object parsed;
                if (!UnifiedJson.TryParse(raw, out parsed)) return map;
                Dictionary<string, object> root = UnifiedJson.AsDict(parsed);
                if (root == null) return map;

                object sigsObj;
                if (root.TryGetValue("alignedSignatures", out sigsObj))
                {
                    Dictionary<string, object> sigDict = UnifiedJson.AsDict(sigsObj);
                    if (sigDict != null)
                    {
                        foreach (KeyValuePair<string, object> kvp in sigDict)
                        {
                            string val = kvp.Value as string;
                            if (!string.IsNullOrEmpty(kvp.Key) && !string.IsNullOrEmpty(val))
                                map[kvp.Key] = val;
                        }
                    }
                }
            }
            catch { }
            return map;
        }

        public static void Save(string customPath, Dictionary<string, string> map)
        {
            if (map == null) return;
            string path = string.IsNullOrEmpty(customPath) ? GetDefaultStorePath() : customPath;
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                Dictionary<string, object> root = new Dictionary<string, object>();
                root["version"] = 1;
                root["updatedAt"] = DateTime.UtcNow.ToString("o");
                Dictionary<string, object> sigs = new Dictionary<string, object>();
                List<string> keys = new List<string>(map.Keys);
                keys.Sort(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < keys.Count; i++)
                {
                    if (!string.IsNullOrEmpty(map[keys[i]]))
                        sigs[keys[i]] = map[keys[i]];
                }
                root["alignedSignatures"] = sigs;

                string json = UnifiedJson.Serialize(root, true);
                SessionLifecycleService.WriteUtf8TextAtomicWithBackup(path, json, false, null);
            }
            catch { }
        }

        public static bool RecordAlignedFromDiffs(Dictionary<string, string> map, List<CapabilityDiffNode> diffs)
        {
            if (map == null || diffs == null) return false;
            bool changed = false;
            for (int i = 0; i < diffs.Count; i++)
            {
                CapabilityDiffNode d = diffs[i];
                if (d != null && d.Status == DiffStatus.Aligned && d.LocalItem != null && !string.IsNullOrEmpty(d.LocalItem.ContentSignature))
                {
                    string existing;
                    if (!map.TryGetValue(d.UniqueKey, out existing) ||
                        !string.Equals(existing, d.LocalItem.ContentSignature, StringComparison.OrdinalIgnoreCase))
                    {
                        map[d.UniqueKey] = d.LocalItem.ContentSignature;
                        changed = true;
                    }
                }
            }
            return changed;
        }
    }
}
