using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // ANTIGRAVITY COCKPIT CONFIG, FULL CUSTOMIZATION SCANNER, MCP DOCTOR,
    // STRICT INLINE EDITOR & TOKEN BUDGET AUDIT
    // Strictly C# 5.0 (.NET Framework 4.x)
    // ══════════════════════════════════════════════════════════════════════

    public class ConfigSettingsSnapshot
    {
        public string ConfigPath = string.Empty;
        public bool Exists;
        public string AutoExecutionPolicy = string.Empty;
        public string ArtifactReviewMode = string.Empty;
        public string BrowserJsExecutionPolicy = string.Empty;
        public string NonWorkspaceFileAccessPolicy = string.Empty;
        public bool EnableTerminalSandbox;
        public bool EnableSoundsForSpecialEvents;
        public bool UseAiCredits;
        public string ThemeMode = string.Empty;
        public string ConversationWidth = string.Empty;
        public Dictionary<string, object> RawSettings = new Dictionary<string, object>(StringComparer.Ordinal);
    }

    public class ScannedRuleInfo
    {
        public string Id = string.Empty;
        public string Scope = string.Empty;
        public string PluginId = string.Empty;
        public string SourcePath = string.Empty;
        public int CharCount;
        public int LineCount;
        public int ByteCount;
        public int EstimatedTokens;
        public bool OverBudget;
        public bool AlwaysOn;
        public bool Enabled = true;
    }

    public class ScannedSkillInfo
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public string Scope = string.Empty;
        public string PluginId = string.Empty;
        public string SourcePath = string.Empty;
        public bool Enabled = true;
        public string ManifestStatus = string.Empty;
    }

    public class ScannedMcpServerInfo
    {
        public string Id = string.Empty;
        public string Scope = string.Empty;
        public string PluginId = string.Empty;
        public string PluginRoot = string.Empty;
        public string SourcePath = string.Empty;
        public string Transport = "stdio";
        public string Type = string.Empty;
        public string Command = string.Empty;
        public List<string> Args = new List<string>();
        public Dictionary<string, string> Env = new Dictionary<string, string>(StringComparer.Ordinal);
        public string Url = string.Empty;
        public bool Disabled;
        public bool Enabled = true;
        public bool Duplicate;
        public List<string> DuplicateSources = new List<string>();
    }

    public class ScannedHookEventItem
    {
        public string EventName = string.Empty;
        public string Matcher = string.Empty;
        public string Type = string.Empty;
        public string Command = string.Empty;
        public int Timeout;
    }

    public class ScannedHookInfo
    {
        public string Id = string.Empty;
        public string Scope = string.Empty;
        public string PluginId = string.Empty;
        public string SourcePath = string.Empty;
        public bool Enabled = true;
        public List<ScannedHookEventItem> Events = new List<ScannedHookEventItem>();
    }

    public class ScannedPluginInfo
    {
        public string Id = string.Empty;
        public string DisplayName = string.Empty;
        public string Version = string.Empty;
        public string Description = string.Empty;
        public bool Enabled = true;
        public bool HasFolder;
        public bool InConfigJson;
        public string FolderPath = string.Empty;
        public string PluginJsonPath = string.Empty;
        public string ConfigPath = string.Empty;
        public Dictionary<string, object> InstalledFrom;
        public string ManifestStatus = string.Empty;
        public List<ScannedRuleInfo> Rules = new List<ScannedRuleInfo>();
        public List<ScannedSkillInfo> Skills = new List<ScannedSkillInfo>();
        public List<ScannedMcpServerInfo> McpServers = new List<ScannedMcpServerInfo>();
        public List<ScannedHookInfo> Hooks = new List<ScannedHookInfo>();
    }

    public class ScannedSidecarInfo
    {
        public string Id = string.Empty;
        public string Source = string.Empty;
        public string ProjectId = string.Empty;
        public int SchemaVersion;
        public string SourcePath = string.Empty;
        public Dictionary<string, object> RawData = new Dictionary<string, object>(StringComparer.Ordinal);
    }

    public class ManifestControlInfo
    {
        public string Kind = string.Empty;
        public string Scope = string.Empty;
        public string SourcePath = string.Empty;
        public object Inherits;
        public Dictionary<string, object> Entries = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public List<string> IncludeOnly = new List<string>();
        public List<string> Exclude = new List<string>();
    }

    public class ScannedWorkspaceInfo
    {
        public string ProjectId = string.Empty;
        public string ProjectName = string.Empty;
        public string FolderPath = string.Empty;
        public bool Exists;
        public bool HasAgentsDir;
        public string AgentsDirName = string.Empty;
        public bool HasGeminiMd;
        public bool HasAgentsMd;
        public bool HasSkillsJson;
        public string SkillsJsonPath = string.Empty;
        public bool HasPluginsJson;
        public string PluginsJsonPath = string.Empty;
        public bool HasHooksJson;
        public string HooksJsonPath = string.Empty;
        public List<ScannedRuleInfo> Rules = new List<ScannedRuleInfo>();
        public List<ScannedSkillInfo> Skills = new List<ScannedSkillInfo>();
        public List<ScannedHookInfo> Hooks = new List<ScannedHookInfo>();
        public List<ScannedMcpServerInfo> McpServers = new List<ScannedMcpServerInfo>();
    }

    public class McpConflictInfo
    {
        public string Id = string.Empty;
        public int Count;
        public List<string> Sources = new List<string>();
    }

    public class CockpitSnapshot
    {
        public ConfigSettingsSnapshot UserSettings = new ConfigSettingsSnapshot();
        public List<ScannedPluginInfo> Plugins = new List<ScannedPluginInfo>();
        public List<ScannedSidecarInfo> Sidecars = new List<ScannedSidecarInfo>();
        public List<ScannedSkillInfo> Skills = new List<ScannedSkillInfo>();
        public List<ScannedRuleInfo> Rules = new List<ScannedRuleInfo>();
        public List<ScannedMcpServerInfo> McpServers = new List<ScannedMcpServerInfo>();
        public List<McpConflictInfo> McpConflicts = new List<McpConflictInfo>();
        public List<ScannedHookInfo> Hooks = new List<ScannedHookInfo>();
        public List<ManifestControlInfo> Manifests = new List<ManifestControlInfo>();
        public List<ScannedWorkspaceInfo> Workspaces = new List<ScannedWorkspaceInfo>();
        public Dictionary<string, object> CliSettings = new Dictionary<string, object>(StringComparer.Ordinal);
        public int GlobalAlwaysOnTokens;
        public int EnabledPluginRulesTokens;
        public int ProjectRulesTokens;
        public int TotalAlwaysOnTokens;
        public int OverBudgetCount;
        public List<ScannedRuleInfo> OverBudgetFiles = new List<ScannedRuleInfo>();
    }

    public class McpHealthCheckResult
    {
        public string Id = string.Empty;
        public string Scope = string.Empty;
        public string SourcePath = string.Empty;
        public string Transport = "stdio";
        public bool Disabled;
        public bool Healthy;
        public bool Reachable;
        public string Status = string.Empty;
        public long LatencyMs;
        public int HttpStatusCode;
        public string ServerName = string.Empty;
        public string ServerVersion = string.Empty;
        public string ProtocolVersion = string.Empty;
        public string Stderr = string.Empty;
        public string Error = string.Empty;
    }

    public class EditorFileResult
    {
        public bool Ok;
        public int HttpStatusCode = 200;
        public string Error = string.Empty;
        public string CanonicalPath = string.Empty;
        public string Content = string.Empty;
        public int CharCount;
        public int EstimatedTokens;
        public bool OverBudget;
        public string BackupPath = string.Empty;
    }

    public static class AntigravityConfigService
    {
        public const int TokenBudgetThreshold = 1500;
        private const int MaxTimestampedBackups = 5;
        private const int DefaultMcpTimeoutMs = 3000;

        public static readonly string[] AutoExecutionPolicyValues = new string[]
        {
            "CASCADE_COMMANDS_AUTO_EXECUTION_OFF",
            "CASCADE_COMMANDS_AUTO_EXECUTION_AUTO",
            "CASCADE_COMMANDS_AUTO_EXECUTION_EAGER"
        };

        public static readonly string[] ArtifactReviewModeValues = new string[]
        {
            "ARTIFACT_REVIEW_MODE_ALWAYS",
            "ARTIFACT_REVIEW_MODE_AUTO",
            "ARTIFACT_REVIEW_MODE_TURBO"
        };

        public static readonly string[] BrowserJsExecutionPolicyValues = new string[]
        {
            "BROWSER_JS_EXECUTION_POLICY_DISABLED",
            "BROWSER_JS_EXECUTION_POLICY_ASK",
            "BROWSER_JS_EXECUTION_POLICY_TURBO"
        };

        public static readonly string[] NonWorkspaceFileAccessPolicyValues = new string[]
        {
            "AGENT_SETTING_POLICY_DENY",
            "AGENT_SETTING_POLICY_ASK",
            "AGENT_SETTING_POLICY_ALLOW"
        };

        public static readonly string[] ThemeModeValues = new string[]
        {
            "THEME_MODE_INHERIT",
            "THEME_MODE_LIGHT",
            "THEME_MODE_DARK"
        };

        private static readonly HashSet<string> KnownHookEventNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PreToolUse", "PostToolUse", "SessionStart", "SessionEnd",
            "PreInvocation", "PostInvocation", "Stop",
            "SubagentStart", "SubagentStop", "UserPromptSubmit", "Notification"
        };

        private static readonly HashSet<string> SensitiveAuthFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "oauth_creds.json",
            "google_accounts.json",
            "auth.json",
            "credentials.json",
            ".env"
        };

        private static readonly HashSet<string> AllowedEditorExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".md", ".json", ".jsonc"
        };

        private static readonly HashSet<string> ScriptArgExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".js", ".mjs", ".cjs", ".ts", ".py", ".ps1", ".sh", ".rb", ".jar", ".exe", ".cmd", ".bat"
        };

        public static int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int cjkChars = 0;
            int otherChars = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if ((c >= 0x4E00 && c <= 0x9FFF) ||
                    (c >= 0x3400 && c <= 0x4DBF) ||
                    (c >= 0x3000 && c <= 0x30FF) ||
                    (c >= 0xF900 && c <= 0xFAFF) ||
                    (c >= 0xAC00 && c <= 0xD7AF) ||
                    (c >= 0xFF00 && c <= 0xFFEF))
                {
                    cjkChars++;
                }
                else
                {
                    otherChars++;
                }
            }
            return (int)Math.Ceiling(cjkChars / 1.5 + otherChars / 4.0);
        }

        // ══════════════════════════════════════════════════════════════════
        // 1. FULL OFFICIAL CUSTOMIZATION SCANNER (P1-3 & P1-12)
        // ══════════════════════════════════════════════════════════════════

        public static CockpitSnapshot ScanCockpit(string geminiPath, string homePath, string repoPath)
        {
            return ScanCockpit(geminiPath, homePath, repoPath, 0);
        }

        public static CockpitSnapshot ScanCockpit(string geminiPath, string homePath, string repoPath, int referenceTokenThreshold)
        {
            CockpitSnapshot snap = new CockpitSnapshot();
            if (string.IsNullOrWhiteSpace(geminiPath))
                return snap;

            string configDir = Path.Combine(geminiPath, "config");
            string configJsonPath = Path.Combine(configDir, "config.json");
            snap.UserSettings.ConfigPath = configJsonPath;

            Dictionary<string, object> configRoot = null;
            if (File.Exists(configJsonPath))
            {
                snap.UserSettings.Exists = true;
                configRoot = TryReadJsonDict(configJsonPath);
            }

            if (configRoot != null)
            {
                LoadUserSettingsFromConfigRoot(configRoot, snap.UserSettings);
                LoadSidecarsFromConfigRoot(configRoot, configJsonPath, snap.Sidecars);
            }

            // Sidecars folder items
            string sidecarsDir = Path.Combine(configDir, "sidecars");
            if (Directory.Exists(sidecarsDir))
            {
                ScanSidecarsDirectory(sidecarsDir, snap.Sidecars);
            }

            // Global manifests: skills.json & plugins.json
            ManifestControlInfo globalSkillsManifest = TryParseManifestControl(Path.Combine(configDir, "skills.json"), "skills", "global");
            if (globalSkillsManifest != null) snap.Manifests.Add(globalSkillsManifest);

            ManifestControlInfo globalPluginsManifest = TryParseManifestControl(Path.Combine(configDir, "plugins.json"), "plugins", "global");
            if (globalPluginsManifest != null) snap.Manifests.Add(globalPluginsManifest);

            // Global rules: ~/.gemini/GEMINI.md, AGENTS.md, .geminirules, ~/.gemini/config/GEMINI.md, ~/.gemini/config/rules/**
            ScanGlobalRules(geminiPath, configDir, snap, referenceTokenThreshold);

            // Global standalone skills: ~/.gemini/config/skills/<id>/
            string skillsDir = Path.Combine(configDir, "skills");
            if (Directory.Exists(skillsDir))
            {
                ScanSkillsDirectory(skillsDir, "global", string.Empty, globalSkillsManifest, snap.Skills);
            }

            // Global MCP: ~/.gemini/config/mcp_config.json
            string globalMcpPath = Path.Combine(configDir, "mcp_config.json");
            if (File.Exists(globalMcpPath))
            {
                ScanMcpConfigFile(globalMcpPath, "global", string.Empty, string.Empty, snap.McpServers);
            }

            // Global Hooks: ~/.gemini/config/hooks.json
            string globalHooksPath = Path.Combine(configDir, "hooks.json");
            if (File.Exists(globalHooksPath))
            {
                ScanHooksFile(globalHooksPath, "global", string.Empty, snap.Hooks);
            }

            // Plugins: config.json plugins + config/plugins/<id>/ + plugins.json
            ScanPlugins(configRoot, configJsonPath, Path.Combine(configDir, "plugins"), globalPluginsManifest, snap, referenceTokenThreshold);

            // CLI configs: ~/.gemini/settings.json & ~/.gemini/antigravity-cli/settings.json
            ScanCliSettings(geminiPath, snap);

            // Registered Workspaces
            ScanRegisteredWorkspaces(geminiPath, snap, referenceTokenThreshold);

            // Apply global skills manifest visibility/status for skills referenced only in manifest
            if (globalSkillsManifest != null)
            {
                AppendManifestOnlySkills(globalSkillsManifest, "global", snap.Skills);
            }

            // Duplicate MCP server ID conflict detection (across global + plugin + CLI + project)
            DetectDuplicateMcpServers(snap);

            // Token budget audit
            ComputeTokenBudgetAudit(snap);

            return snap;
        }

        private static void LoadUserSettingsFromConfigRoot(Dictionary<string, object> configRoot, ConfigSettingsSnapshot target)
        {
            if (configRoot == null || target == null) return;
            object usObj;
            if (!TryGetDictValueIgnoreCase(configRoot, "userSettings", out usObj))
                return;

            Dictionary<string, object> us = UnifiedJson.AsDict(usObj);
            if (us == null) return;

            target.RawSettings = new Dictionary<string, object>(us, StringComparer.Ordinal);
            target.AutoExecutionPolicy = UnifiedJson.GetString(us, "autoExecutionPolicy", string.Empty);
            target.ArtifactReviewMode = UnifiedJson.GetString(us, "artifactReviewMode", string.Empty);
            target.BrowserJsExecutionPolicy = UnifiedJson.GetString(us, "browserJsExecutionPolicy", string.Empty);
            target.NonWorkspaceFileAccessPolicy = UnifiedJson.GetString(us, "nonWorkspaceFileAccessPolicy", string.Empty);
            target.EnableTerminalSandbox = UnifiedJson.GetBool(us, "enableTerminalSandbox", false);
            target.EnableSoundsForSpecialEvents = UnifiedJson.GetBool(us, "enableSoundsForSpecialEvents", false);
            target.UseAiCredits = UnifiedJson.GetBool(us, "useAiCredits", false);
            target.ThemeMode = UnifiedJson.GetString(us, "themeMode", string.Empty);
            target.ConversationWidth = UnifiedJson.GetString(us, "conversationWidth", string.Empty);
        }

        private static void LoadSidecarsFromConfigRoot(Dictionary<string, object> configRoot, string configJsonPath, List<ScannedSidecarInfo> list)
        {
            object scObj;
            if (!TryGetDictValueIgnoreCase(configRoot, "sidecars", out scObj))
                return;
            Dictionary<string, object> scDict = UnifiedJson.AsDict(scObj);
            if (scDict == null) return;

            foreach (KeyValuePair<string, object> kvp in scDict)
            {
                ScannedSidecarInfo item = new ScannedSidecarInfo();
                item.Id = kvp.Key;
                item.Source = "config.json";
                item.SourcePath = configJsonPath;
                Dictionary<string, object> valDict = UnifiedJson.AsDict(kvp.Value);
                if (valDict != null)
                {
                    item.RawData = valDict;
                    item.ProjectId = UnifiedJson.GetString(valDict, "projectId", string.Empty);
                    item.SchemaVersion = UnifiedJson.GetInt(valDict, "schemaVersion", 0);
                }
                list.Add(item);
            }
        }

        private static void ScanSidecarsDirectory(string sidecarsDir, List<ScannedSidecarInfo> list)
        {
            try
            {
                string[] entries = Directory.GetFileSystemEntries(sidecarsDir);
                Array.Sort(entries, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < entries.Length; i++)
                {
                    string entry = entries[i];
                    string name = Path.GetFileName(entry);
                    if (string.IsNullOrEmpty(name)) continue;
                    bool existsInConfig = false;
                    for (int j = 0; j < list.Count; j++)
                    {
                        if (string.Equals(list[j].Id, name, StringComparison.OrdinalIgnoreCase))
                        {
                            existsInConfig = true;
                            break;
                        }
                    }
                    if (!existsInConfig)
                    {
                        ScannedSidecarInfo item = new ScannedSidecarInfo();
                        item.Id = name;
                        item.Source = "sidecars_dir";
                        item.SourcePath = entry;
                        list.Add(item);
                    }
                }
            }
            catch { }
        }

        private static void ScanGlobalRules(string geminiPath, string configDir, CockpitSnapshot snap, int referenceTokenThreshold)
        {
            string[] rootFiles = new string[]
            {
                Path.Combine(geminiPath, "GEMINI.md"),
                Path.Combine(geminiPath, "AGENTS.md"),
                Path.Combine(geminiPath, ".geminirules"),
                Path.Combine(configDir, "GEMINI.md")
            };

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rootFiles.Length; i++)
            {
                string rf = rootFiles[i];
                if (!File.Exists(rf)) continue;
                try
                {
                    string full = Path.GetFullPath(rf);
                    if (!seen.Add(full)) continue;
                    string id = full.StartsWith(Path.GetFullPath(configDir), StringComparison.OrdinalIgnoreCase)
                        ? "config/" + Path.GetFileName(rf)
                        : Path.GetFileName(rf);
                    ScannedRuleInfo rule = BuildRuleInfo(id, "global", string.Empty, full, true, true, referenceTokenThreshold);
                    if (rule != null) snap.Rules.Add(rule);
                }
                catch { }
            }

            string rulesDir = Path.Combine(configDir, "rules");
            if (Directory.Exists(rulesDir))
            {
                List<string> files = CollectSafeMarkdownFiles(rulesDir);
                for (int i = 0; i < files.Count; i++)
                {
                    string full = Path.GetFullPath(files[i]);
                    if (!seen.Add(full)) continue;
                    string rel = files[i].Substring(rulesDir.Length).TrimStart('\\', '/').Replace('\\', '/');
                    ScannedRuleInfo rule = BuildRuleInfo("rules/" + rel, "global", string.Empty, full, true, true, referenceTokenThreshold);
                    if (rule != null) snap.Rules.Add(rule);
                }
            }
        }

        private static void ScanPlugins(
            Dictionary<string, object> configRoot,
            string configJsonPath,
            string pluginsDir,
            ManifestControlInfo pluginsManifest,
            CockpitSnapshot snap,
            int referenceTokenThreshold)
        {
            Dictionary<string, Dictionary<string, object>> configPluginEntries = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
            if (configRoot != null)
            {
                object pluginsObj;
                if (TryGetDictValueIgnoreCase(configRoot, "plugins", out pluginsObj))
                {
                    Dictionary<string, object> pDict = UnifiedJson.AsDict(pluginsObj);
                    if (pDict != null)
                    {
                        foreach (KeyValuePair<string, object> kvp in pDict)
                        {
                            Dictionary<string, object> entryDict = UnifiedJson.AsDict(kvp.Value);
                            configPluginEntries[kvp.Key] = entryDict ?? new Dictionary<string, object>(StringComparer.Ordinal);
                        }
                    }
                }
            }

            Dictionary<string, string> folderById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(pluginsDir))
            {
                string[] dirs = Directory.GetDirectories(pluginsDir);
                Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < dirs.Length; i++)
                {
                    string rawName = Path.GetFileName(dirs[i]);
                    if (string.IsNullOrEmpty(rawName)) continue;
                    string decodedId = CapabilityVaultService.DecodeSafeCapabilityFolderName(rawName);
                    folderById[decodedId] = dirs[i];
                }
            }

            HashSet<string> allIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string id in configPluginEntries.Keys) allIds.Add(id);
            foreach (string id in folderById.Keys) allIds.Add(id);
            if (pluginsManifest != null)
            {
                foreach (string id in pluginsManifest.Entries.Keys) allIds.Add(id);
            }

            List<string> sortedIds = new List<string>(allIds);
            sortedIds.Sort(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < sortedIds.Count; i++)
            {
                string id = sortedIds[i];
                ScannedPluginInfo pInfo = new ScannedPluginInfo();
                pInfo.Id = id;
                pInfo.DisplayName = id;
                pInfo.ConfigPath = configJsonPath;

                Dictionary<string, object> cfgEntry;
                if (configPluginEntries.TryGetValue(id, out cfgEntry))
                {
                    pInfo.InConfigJson = true;
                    pInfo.Enabled = UnifiedJson.GetBool(cfgEntry, "enabled", true);
                    object instObj;
                    if (TryGetDictValueIgnoreCase(cfgEntry, "installedFrom", out instObj))
                    {
                        pInfo.InstalledFrom = UnifiedJson.AsDict(instObj);
                        if (pInfo.InstalledFrom != null)
                        {
                            string v = UnifiedJson.GetString(pInfo.InstalledFrom, "version", string.Empty);
                            if (!string.IsNullOrEmpty(v)) pInfo.Version = v;
                        }
                    }
                }

                string folderPath;
                if (folderById.TryGetValue(id, out folderPath))
                {
                    pInfo.HasFolder = true;
                    pInfo.FolderPath = folderPath;
                    if (!pInfo.InConfigJson)
                    {
                        pInfo.Enabled = true;
                    }

                    string pluginJsonPath = Path.Combine(folderPath, "plugin.json");
                    if (File.Exists(pluginJsonPath))
                    {
                        pInfo.PluginJsonPath = pluginJsonPath;
                        Dictionary<string, object> pj = TryReadJsonDict(pluginJsonPath);
                        if (pj != null)
                        {
                            string dn = UnifiedJson.GetString(pj, "displayName", string.Empty);
                            if (string.IsNullOrEmpty(dn)) dn = UnifiedJson.GetString(pj, "name", string.Empty);
                            if (!string.IsNullOrEmpty(dn)) pInfo.DisplayName = dn;
                            string ver = UnifiedJson.GetString(pj, "version", string.Empty);
                            if (!string.IsNullOrEmpty(ver)) pInfo.Version = ver;
                            pInfo.Description = UnifiedJson.GetString(pj, "description", string.Empty);
                        }
                    }

                    // Apply plugins.json manifest filter status
                    string mStatus = EvaluateManifestEntryStatus(pluginsManifest, id);
                    if (!string.IsNullOrEmpty(mStatus))
                    {
                        pInfo.ManifestStatus = mStatus;
                        if (string.Equals(mStatus, "excluded", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(mStatus, "disabled", StringComparison.OrdinalIgnoreCase))
                        {
                            pInfo.Enabled = false;
                        }
                    }

                    // Plugin internals: rules/
                    string pRulesDir = Path.Combine(folderPath, "rules");
                    if (Directory.Exists(pRulesDir))
                    {
                        List<string> rFiles = CollectSafeMarkdownFiles(pRulesDir);
                        for (int r = 0; r < rFiles.Count; r++)
                        {
                            string rel = rFiles[r].Substring(pRulesDir.Length).TrimStart('\\', '/').Replace('\\', '/');
                            ScannedRuleInfo rInfo = BuildRuleInfo("plugin:" + id + "/" + rel, "plugin:" + id, id, rFiles[r], true, pInfo.Enabled, referenceTokenThreshold);
                            if (rInfo != null)
                            {
                                pInfo.Rules.Add(rInfo);
                                snap.Rules.Add(rInfo);
                            }
                        }
                    }

                    // Plugin internals: skills/
                    string pSkillsDir = Path.Combine(folderPath, "skills");
                    if (Directory.Exists(pSkillsDir))
                    {
                        ScanSkillsDirectory(pSkillsDir, "plugin:" + id, id, null, pInfo.Skills);
                        for (int s = 0; s < pInfo.Skills.Count; s++)
                        {
                            if (!pInfo.Enabled) pInfo.Skills[s].Enabled = false;
                            snap.Skills.Add(pInfo.Skills[s]);
                        }
                    }

                    // Plugin internals: mcp_config.json
                    string pMcpPath = Path.Combine(folderPath, "mcp_config.json");
                    if (File.Exists(pMcpPath))
                    {
                        ScanMcpConfigFile(pMcpPath, "plugin:" + id, id, folderPath, pInfo.McpServers);
                        for (int m = 0; m < pInfo.McpServers.Count; m++)
                        {
                            if (!pInfo.Enabled) pInfo.McpServers[m].Enabled = false;
                            snap.McpServers.Add(pInfo.McpServers[m]);
                        }
                    }

                    // Plugin internals: hooks.json
                    string pHooksPath = Path.Combine(folderPath, "hooks.json");
                    if (File.Exists(pHooksPath))
                    {
                        ScanHooksFile(pHooksPath, "plugin:" + id, id, pInfo.Hooks);
                        for (int h = 0; h < pInfo.Hooks.Count; h++)
                        {
                            if (!pInfo.Enabled) pInfo.Hooks[h].Enabled = false;
                            snap.Hooks.Add(pInfo.Hooks[h]);
                        }
                    }
                }
                else
                {
                    string mStatus = EvaluateManifestEntryStatus(pluginsManifest, id);
                    if (!string.IsNullOrEmpty(mStatus))
                    {
                        pInfo.ManifestStatus = mStatus;
                        if (string.Equals(mStatus, "excluded", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(mStatus, "disabled", StringComparison.OrdinalIgnoreCase))
                        {
                            pInfo.Enabled = false;
                        }
                    }
                }

                snap.Plugins.Add(pInfo);
            }
        }

        private static void ScanCliSettings(string geminiPath, CockpitSnapshot snap)
        {
            string cliGlobalPath = Path.Combine(geminiPath, "settings.json");
            if (File.Exists(cliGlobalPath))
            {
                Dictionary<string, object> dict = TryReadJsonDict(cliGlobalPath);
                if (dict != null)
                {
                    snap.CliSettings["settingsJson"] = dict;
                    ScanMcpFromParsedDict(dict, cliGlobalPath, "cli:settings", string.Empty, string.Empty, snap.McpServers);
                    if (dict.ContainsKey("hooks"))
                    {
                        ScanHooksFromParsedDict(UnifiedJson.AsDict(dict["hooks"]) ?? dict, cliGlobalPath, "cli:settings", string.Empty, snap.Hooks);
                    }
                }
            }

            string agyCliPath = Path.Combine(geminiPath, "antigravity-cli", "settings.json");
            if (File.Exists(agyCliPath))
            {
                Dictionary<string, object> dict = TryReadJsonDict(agyCliPath);
                if (dict != null)
                {
                    snap.CliSettings["antigravityCliSettingsJson"] = dict;
                    ScanMcpFromParsedDict(dict, agyCliPath, "cli:antigravity-cli", string.Empty, string.Empty, snap.McpServers);
                    if (dict.ContainsKey("hooks"))
                    {
                        ScanHooksFromParsedDict(UnifiedJson.AsDict(dict["hooks"]) ?? dict, agyCliPath, "cli:antigravity-cli", string.Empty, snap.Hooks);
                    }
                }
            }
        }

        private static void ScanRegisteredWorkspaces(string geminiPath, CockpitSnapshot snap, int referenceTokenThreshold)
        {
            Dictionary<string, ProjectMetadata> projects = WorkspaceService.LoadLocalProjects(geminiPath);
            if (projects == null || projects.Count == 0) return;

            HashSet<string> visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, ProjectMetadata> kvp in projects)
            {
                ProjectMetadata pm = kvp.Value;
                if (pm == null) continue;

                List<string> uris = new List<string>();
                if (!string.IsNullOrEmpty(pm.FolderUri)) uris.Add(pm.FolderUri);
                if (pm.FolderUris != null)
                {
                    for (int i = 0; i < pm.FolderUris.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(pm.FolderUris[i])) uris.Add(pm.FolderUris[i]);
                    }
                }

                for (int u = 0; u < uris.Count; u++)
                {
                    string folderPath = WorkspaceService.UriToDisplayPath(uris[u]);
                    if (string.IsNullOrWhiteSpace(folderPath)) continue;
                    string normFolder;
                    try { normFolder = Path.GetFullPath(folderPath).TrimEnd('\\', '/'); }
                    catch { continue; }

                    if (!visitedPaths.Add(normFolder)) continue;

                    ScannedWorkspaceInfo ws = new ScannedWorkspaceInfo();
                    ws.ProjectId = pm.Id ?? kvp.Key;
                    ws.ProjectName = pm.Name ?? Path.GetFileName(normFolder);
                    ws.FolderPath = normFolder;
                    ws.Exists = Directory.Exists(normFolder);
                    if (!ws.Exists)
                    {
                        snap.Workspaces.Add(ws);
                        continue;
                    }

                    string scope = "project:" + ws.ProjectName;
                    string geminiMd = Path.Combine(normFolder, "GEMINI.md");
                    if (File.Exists(geminiMd))
                    {
                        ws.HasGeminiMd = true;
                        ScannedRuleInfo r = BuildRuleInfo(ws.ProjectName + "/GEMINI.md", scope, string.Empty, geminiMd, true, true, referenceTokenThreshold);
                        if (r != null) { ws.Rules.Add(r); snap.Rules.Add(r); }
                    }

                    string agentsMd = Path.Combine(normFolder, "AGENTS.md");
                    if (File.Exists(agentsMd))
                    {
                        ws.HasAgentsMd = true;
                        ScannedRuleInfo r = BuildRuleInfo(ws.ProjectName + "/AGENTS.md", scope, string.Empty, agentsMd, true, true, referenceTokenThreshold);
                        if (r != null) { ws.Rules.Add(r); snap.Rules.Add(r); }
                    }

                    List<string> agentDirs = new List<string>();
                    string agentsDir = Path.Combine(normFolder, ".agents");
                    string agentDirLegacy = Path.Combine(normFolder, ".agent");
                    if (Directory.Exists(agentsDir)) agentDirs.Add(agentsDir);
                    if (Directory.Exists(agentDirLegacy)) agentDirs.Add(agentDirLegacy);
                    if (agentDirs.Count > 0)
                    {
                        ws.HasAgentsDir = true;
                        ws.AgentsDirName = Path.GetFileName(agentDirs[0]);
                    }
                    string primaryAgentsDir = agentDirs.Count > 0 ? agentDirs[0] : null;

                    // Project manifests: skills.json / plugins.json (check root and .agents/.agent/)
                    string projSkillsJson = File.Exists(Path.Combine(normFolder, "skills.json"))
                        ? Path.Combine(normFolder, "skills.json")
                        : (!string.IsNullOrEmpty(primaryAgentsDir) ? Path.Combine(primaryAgentsDir, "skills.json") : string.Empty);
                    ManifestControlInfo projSkillsManifest = TryParseManifestControl(projSkillsJson, "skills", scope);
                    if (projSkillsManifest != null)
                    {
                        ws.HasSkillsJson = true;
                        ws.SkillsJsonPath = projSkillsJson;
                        snap.Manifests.Add(projSkillsManifest);
                    }

                    string projPluginsJson = File.Exists(Path.Combine(normFolder, "plugins.json"))
                        ? Path.Combine(normFolder, "plugins.json")
                        : (!string.IsNullOrEmpty(primaryAgentsDir) ? Path.Combine(primaryAgentsDir, "plugins.json") : string.Empty);
                    ManifestControlInfo projPluginsManifest = TryParseManifestControl(projPluginsJson, "plugins", scope);
                    if (projPluginsManifest != null)
                    {
                        ws.HasPluginsJson = true;
                        ws.PluginsJsonPath = projPluginsJson;
                        snap.Manifests.Add(projPluginsManifest);
                    }

                    // Project hooks.json (check root and .agents/.agent/)
                    List<string> hookCandidates = new List<string>();
                    hookCandidates.Add(Path.Combine(normFolder, "hooks.json"));
                    for (int ad = 0; ad < agentDirs.Count; ad++)
                        hookCandidates.Add(Path.Combine(agentDirs[ad], "hooks.json"));
                    for (int hc = 0; hc < hookCandidates.Count; hc++)
                    {
                        if (File.Exists(hookCandidates[hc]))
                        {
                            ws.HasHooksJson = true;
                            if (string.IsNullOrEmpty(ws.HooksJsonPath))
                                ws.HooksJsonPath = hookCandidates[hc];
                            ScanHooksFile(hookCandidates[hc], scope, string.Empty, ws.Hooks);
                        }
                    }
                    for (int hi = 0; hi < ws.Hooks.Count; hi++) snap.Hooks.Add(ws.Hooks[hi]);

                    string rootMcpPath = Path.Combine(normFolder, "mcp_config.json");
                    if (File.Exists(rootMcpPath))
                    {
                        ScanMcpConfigFile(rootMcpPath, scope, string.Empty, normFolder, ws.McpServers);
                    }

                    for (int ad = 0; ad < agentDirs.Count; ad++)
                    {
                        string curAgentsDir = agentDirs[ad];
                        string curDirName = Path.GetFileName(curAgentsDir);

                        string pRulesDir = Path.Combine(curAgentsDir, "rules");
                        if (Directory.Exists(pRulesDir))
                        {
                            List<string> rFiles = CollectSafeMarkdownFiles(pRulesDir);
                            for (int r = 0; r < rFiles.Count; r++)
                            {
                                string rel = rFiles[r].Substring(pRulesDir.Length).TrimStart('\\', '/').Replace('\\', '/');
                                ScannedRuleInfo rInfo = BuildRuleInfo(ws.ProjectName + "/" + curDirName + "/rules/" + rel, scope, string.Empty, rFiles[r], true, true, referenceTokenThreshold);
                                if (rInfo != null) { ws.Rules.Add(rInfo); snap.Rules.Add(rInfo); }
                            }
                        }

                        string pSkillsDir = Path.Combine(curAgentsDir, "skills");
                        if (Directory.Exists(pSkillsDir))
                        {
                            ScanSkillsDirectory(pSkillsDir, scope, string.Empty, projSkillsManifest, ws.Skills);
                        }

                        string pMcpPath = Path.Combine(curAgentsDir, "mcp_config.json");
                        if (File.Exists(pMcpPath))
                        {
                            ScanMcpConfigFile(pMcpPath, scope, string.Empty, normFolder, ws.McpServers);
                        }
                    }

                    for (int s = 0; s < ws.Skills.Count; s++) snap.Skills.Add(ws.Skills[s]);
                    for (int m = 0; m < ws.McpServers.Count; m++) snap.McpServers.Add(ws.McpServers[m]);

                    if (projSkillsManifest != null)
                    {
                        AppendManifestOnlySkills(projSkillsManifest, scope, ws.Skills);
                        for (int s = 0; s < ws.Skills.Count; s++)
                        {
                            if (!snap.Skills.Contains(ws.Skills[s])) snap.Skills.Add(ws.Skills[s]);
                        }
                    }

                    snap.Workspaces.Add(ws);
                }
            }
        }

        private static void ScanSkillsDirectory(
            string skillsDir,
            string scope,
            string pluginId,
            ManifestControlInfo manifest,
            List<ScannedSkillInfo> target)
        {
            try
            {
                string[] dirs = Directory.GetDirectories(skillsDir);
                Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < dirs.Length; i++)
                {
                    string dir = dirs[i];
                    string rawName = Path.GetFileName(dir);
                    if (string.IsNullOrEmpty(rawName)) continue;
                    string id = CapabilityVaultService.DecodeSafeCapabilityFolderName(rawName);

                    ScannedSkillInfo sInfo = new ScannedSkillInfo();
                    sInfo.Id = id;
                    sInfo.Name = id;
                    sInfo.Scope = scope;
                    sInfo.PluginId = pluginId ?? string.Empty;

                    string skillMd = Path.Combine(dir, "SKILL.md");
                    if (File.Exists(skillMd))
                    {
                        sInfo.SourcePath = skillMd;
                        string text = ReadTextSharedSafe(skillMd);
                        string parsedName, parsedDesc;
                        ExtractSkillFrontmatter(text, out parsedName, out parsedDesc);
                        if (!string.IsNullOrEmpty(parsedName)) sInfo.Name = parsedName;
                        sInfo.Description = parsedDesc;
                    }
                    else
                    {
                        sInfo.SourcePath = dir;
                    }

                    string mStatus = EvaluateManifestEntryStatus(manifest, id);
                    if (!string.IsNullOrEmpty(mStatus))
                    {
                        sInfo.ManifestStatus = mStatus;
                        if (string.Equals(mStatus, "excluded", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(mStatus, "disabled", StringComparison.OrdinalIgnoreCase))
                        {
                            sInfo.Enabled = false;
                        }
                    }

                    target.Add(sInfo);
                }
            }
            catch { }
        }

        private static void AppendManifestOnlySkills(ManifestControlInfo manifest, string scope, List<ScannedSkillInfo> list)
        {
            if (manifest == null || manifest.Entries == null) return;
            foreach (KeyValuePair<string, object> kvp in manifest.Entries)
            {
                bool found = false;
                for (int i = 0; i < list.Count; i++)
                {
                    if (string.Equals(list[i].Id, kvp.Key, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(list[i].Scope, scope, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    ScannedSkillInfo sInfo = new ScannedSkillInfo();
                    sInfo.Id = kvp.Key;
                    sInfo.Name = kvp.Key;
                    sInfo.Scope = scope;
                    sInfo.SourcePath = manifest.SourcePath;
                    string mStatus = EvaluateManifestEntryStatus(manifest, kvp.Key);
                    sInfo.ManifestStatus = !string.IsNullOrEmpty(mStatus) ? mStatus : "manifest_only";
                    sInfo.Enabled = !string.Equals(mStatus, "excluded", StringComparison.OrdinalIgnoreCase) &&
                                    !string.Equals(mStatus, "disabled", StringComparison.OrdinalIgnoreCase);
                    list.Add(sInfo);
                }
            }
        }

        private static ManifestControlInfo TryParseManifestControl(string path, string kind, string scope)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            Dictionary<string, object> dict = TryReadJsonDict(path);
            if (dict == null) return null;

            ManifestControlInfo info = new ManifestControlInfo();
            info.Kind = kind;
            info.Scope = scope;
            info.SourcePath = path;

            object inh;
            if (TryGetDictValueIgnoreCase(dict, "inherits", out inh))
            {
                info.Inherits = inh;
            }

            object entriesObj;
            if (TryGetDictValueIgnoreCase(dict, "entries", out entriesObj))
            {
                Dictionary<string, object> eDict = UnifiedJson.AsDict(entriesObj);
                if (eDict != null)
                {
                    foreach (KeyValuePair<string, object> kvp in eDict)
                        info.Entries[kvp.Key] = kvp.Value;
                }
                else
                {
                    List<object> eList = UnifiedJson.AsList(entriesObj);
                    if (eList != null)
                    {
                        for (int i = 0; i < eList.Count; i++)
                        {
                            string s = eList[i] as string;
                            if (!string.IsNullOrWhiteSpace(s))
                                info.Entries[s.Trim()] = true;
                            else
                            {
                                Dictionary<string, object> row = UnifiedJson.AsDict(eList[i]);
                                string rid = row != null ? UnifiedJson.GetString(row, "id", string.Empty) : string.Empty;
                                if (!string.IsNullOrEmpty(rid)) info.Entries[rid] = row;
                            }
                        }
                    }
                }
            }

            info.IncludeOnly = ReadStringListFromDict(dict, "include_only");
            info.Exclude = ReadStringListFromDict(dict, "exclude");
            return info;
        }

        private static string EvaluateManifestEntryStatus(ManifestControlInfo manifest, string id)
        {
            if (manifest == null || string.IsNullOrEmpty(id)) return string.Empty;

            for (int i = 0; i < manifest.Exclude.Count; i++)
            {
                if (string.Equals(manifest.Exclude[i], id, StringComparison.OrdinalIgnoreCase))
                    return "excluded";
            }

            if (manifest.IncludeOnly != null && manifest.IncludeOnly.Count > 0)
            {
                bool inInclude = false;
                for (int i = 0; i < manifest.IncludeOnly.Count; i++)
                {
                    if (string.Equals(manifest.IncludeOnly[i], id, StringComparison.OrdinalIgnoreCase))
                    {
                        inInclude = true;
                        break;
                    }
                }
                if (!inInclude) return "excluded";
            }

            object entryVal;
            if (manifest.Entries.TryGetValue(id, out entryVal) && entryVal != null)
            {
                if (entryVal is bool)
                    return ((bool)entryVal) ? "enabled" : "disabled";
                Dictionary<string, object> eDict = UnifiedJson.AsDict(entryVal);
                if (eDict != null && eDict.ContainsKey("enabled"))
                {
                    return UnifiedJson.GetBool(eDict, "enabled", true) ? "enabled" : "disabled";
                }
                return "configured";
            }

            return manifest.IncludeOnly.Count > 0 ? "included" : string.Empty;
        }

        private static void ScanMcpConfigFile(
            string path,
            string scope,
            string pluginId,
            string pluginRoot,
            List<ScannedMcpServerInfo> target)
        {
            Dictionary<string, object> dict = TryReadJsonDict(path);
            if (dict == null) return;
            ScanMcpFromParsedDict(dict, path, scope, pluginId, pluginRoot, target);
        }

        private static void ScanMcpFromParsedDict(
            Dictionary<string, object> dict,
            string sourcePath,
            string scope,
            string pluginId,
            string pluginRoot,
            List<ScannedMcpServerInfo> target)
        {
            object serversObj;
            if (!TryGetDictValueIgnoreCase(dict, "mcpServers", out serversObj))
                return;
            Dictionary<string, object> servers = UnifiedJson.AsDict(serversObj);
            if (servers == null) return;

            foreach (KeyValuePair<string, object> kvp in servers)
            {
                Dictionary<string, object> srv = UnifiedJson.AsDict(kvp.Value);
                if (srv == null) continue;

                ScannedMcpServerInfo info = new ScannedMcpServerInfo();
                info.Id = kvp.Key;
                info.Scope = scope;
                info.PluginId = pluginId ?? string.Empty;
                info.PluginRoot = pluginRoot ?? string.Empty;
                info.SourcePath = sourcePath;
                info.Disabled = UnifiedJson.GetBool(srv, "disabled", false);
                info.Enabled = !info.Disabled;
                info.Type = UnifiedJson.GetString(srv, "type", string.Empty);
                info.Command = UnifiedJson.GetString(srv, "command", string.Empty);
                info.Url = UnifiedJson.GetString(srv, "url", string.Empty);
                if (string.IsNullOrEmpty(info.Url)) info.Url = UnifiedJson.GetString(srv, "serverUrl", string.Empty);
                if (string.IsNullOrEmpty(info.Url)) info.Url = UnifiedJson.GetString(srv, "httpUrl", string.Empty);

                info.Args = ReadStringListFromDict(srv, "args");

                object envObj;
                if (TryGetDictValueIgnoreCase(srv, "env", out envObj))
                {
                    Dictionary<string, object> envDict = UnifiedJson.AsDict(envObj);
                    if (envDict != null)
                    {
                        foreach (KeyValuePair<string, object> ekvp in envDict)
                        {
                            info.Env[ekvp.Key] = ekvp.Value != null ? Convert.ToString(ekvp.Value, CultureInfo.InvariantCulture) : string.Empty;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(info.Url) ||
                    string.Equals(info.Type, "http", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(info.Type, "streamable-http", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(info.Type, "sse", StringComparison.OrdinalIgnoreCase))
                {
                    info.Transport = "http";
                }
                else
                {
                    info.Transport = "stdio";
                }

                target.Add(info);
            }
        }

        private static void ScanHooksFile(string path, string scope, string pluginId, List<ScannedHookInfo> target)
        {
            Dictionary<string, object> root = TryReadJsonDict(path);
            if (root == null) return;
            ScanHooksFromParsedDict(root, path, scope, pluginId, target);
        }

        private static void ScanHooksFromParsedDict(
            Dictionary<string, object> root,
            string sourcePath,
            string scope,
            string pluginId,
            List<ScannedHookInfo> target)
        {
            if (root == null || root.Count == 0) return;

            // Check if root itself holds direct event arrays or "hooks": { ... } wrapper
            bool rootHasEventKeys = false;
            foreach (string key in root.Keys)
            {
                if (KnownHookEventNames.Contains(key))
                {
                    rootHasEventKeys = true;
                    break;
                }
            }

            if (rootHasEventKeys)
            {
                ScannedHookInfo rootGroup = new ScannedHookInfo();
                rootGroup.Id = !string.IsNullOrEmpty(pluginId) ? pluginId : "_root";
                rootGroup.Scope = scope;
                rootGroup.PluginId = pluginId ?? string.Empty;
                rootGroup.SourcePath = sourcePath;
                rootGroup.Enabled = UnifiedJson.GetBool(root, "enabled", true);
                ExtractHookEventsFromGroupDict(root, rootGroup.Events);
                target.Add(rootGroup);
                return;
            }

            object hooksWrapperObj;
            if (TryGetDictValueIgnoreCase(root, "hooks", out hooksWrapperObj))
            {
                Dictionary<string, object> wrapperDict = UnifiedJson.AsDict(hooksWrapperObj);
                if (wrapperDict != null)
                {
                    bool wrapperHasEvents = false;
                    foreach (string wk in wrapperDict.Keys)
                    {
                        if (KnownHookEventNames.Contains(wk)) { wrapperHasEvents = true; break; }
                    }
                    if (wrapperHasEvents)
                    {
                        ScannedHookInfo wGroup = new ScannedHookInfo();
                        wGroup.Id = !string.IsNullOrEmpty(pluginId) ? pluginId : "_root";
                        wGroup.Scope = scope;
                        wGroup.PluginId = pluginId ?? string.Empty;
                        wGroup.SourcePath = sourcePath;
                        wGroup.Enabled = UnifiedJson.GetBool(root, "enabled", true) && UnifiedJson.GetBool(wrapperDict, "enabled", true);
                        ExtractHookEventsFromGroupDict(wrapperDict, wGroup.Events);
                        target.Add(wGroup);
                        return;
                    }
                }
            }

            // Named hook groups at root level (e.g. "evo-evolution-reminder": { "enabled": true, "PreInvocation": [...] })
            foreach (KeyValuePair<string, object> kvp in root)
            {
                if (string.Equals(kvp.Key, "enabled", StringComparison.OrdinalIgnoreCase)) continue;
                Dictionary<string, object> grpDict = UnifiedJson.AsDict(kvp.Value);
                if (grpDict == null) continue;

                ScannedHookInfo hInfo = new ScannedHookInfo();
                hInfo.Id = kvp.Key;
                hInfo.Scope = scope;
                hInfo.PluginId = pluginId ?? string.Empty;
                hInfo.SourcePath = sourcePath;
                hInfo.Enabled = UnifiedJson.GetBool(grpDict, "enabled", true);

                ExtractHookEventsFromGroupDict(grpDict, hInfo.Events);

                // Fallback: legacy single-command hook object without event keys
                if (hInfo.Events.Count == 0 && grpDict.ContainsKey("command"))
                {
                    ScannedHookEventItem ev = new ScannedHookEventItem();
                    ev.EventName = UnifiedJson.GetString(grpDict, "event", "command");
                    ev.Matcher = UnifiedJson.GetString(grpDict, "matcher", string.Empty);
                    ev.Type = UnifiedJson.GetString(grpDict, "type", "command");
                    ev.Command = UnifiedJson.GetString(grpDict, "command", string.Empty);
                    ev.Timeout = UnifiedJson.GetInt(grpDict, "timeout", 0);
                    hInfo.Events.Add(ev);
                }

                target.Add(hInfo);
            }
        }

        private static void ExtractHookEventsFromGroupDict(Dictionary<string, object> grpDict, List<ScannedHookEventItem> outEvents)
        {
            if (grpDict == null || outEvents == null) return;
            foreach (KeyValuePair<string, object> kvp in grpDict)
            {
                if (string.Equals(kvp.Key, "enabled", StringComparison.OrdinalIgnoreCase)) continue;
                List<object> arr = UnifiedJson.AsList(kvp.Value);
                if (arr == null) continue;

                for (int i = 0; i < arr.Count; i++)
                {
                    Dictionary<string, object> itemDict = UnifiedJson.AsDict(arr[i]);
                    if (itemDict == null) continue;

                    string matcher = UnifiedJson.GetString(itemDict, "matcher", string.Empty);
                    object nestedHooksObj;
                    if (TryGetDictValueIgnoreCase(itemDict, "hooks", out nestedHooksObj))
                    {
                        List<object> nestedList = UnifiedJson.AsList(nestedHooksObj);
                        if (nestedList != null)
                        {
                            for (int j = 0; j < nestedList.Count; j++)
                            {
                                Dictionary<string, object> hd = UnifiedJson.AsDict(nestedList[j]);
                                if (hd == null) continue;
                                ScannedHookEventItem ev = new ScannedHookEventItem();
                                ev.EventName = kvp.Key;
                                ev.Matcher = matcher;
                                ev.Type = UnifiedJson.GetString(hd, "type", "command");
                                ev.Command = UnifiedJson.GetString(hd, "command", string.Empty);
                                ev.Timeout = UnifiedJson.GetInt(hd, "timeout", 0);
                                outEvents.Add(ev);
                            }
                            continue;
                        }
                    }

                    ScannedHookEventItem flatEv = new ScannedHookEventItem();
                    flatEv.EventName = kvp.Key;
                    flatEv.Matcher = matcher;
                    flatEv.Type = UnifiedJson.GetString(itemDict, "type", "command");
                    flatEv.Command = UnifiedJson.GetString(itemDict, "command", string.Empty);
                    flatEv.Timeout = UnifiedJson.GetInt(itemDict, "timeout", 0);
                    outEvents.Add(flatEv);
                }
            }
        }

        private static void DetectDuplicateMcpServers(CockpitSnapshot snap)
        {
            Dictionary<string, List<ScannedMcpServerInfo>> byId = new Dictionary<string, List<ScannedMcpServerInfo>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < snap.McpServers.Count; i++)
            {
                ScannedMcpServerInfo s = snap.McpServers[i];
                if (s == null || string.IsNullOrEmpty(s.Id)) continue;
                List<ScannedMcpServerInfo> bucket;
                if (!byId.TryGetValue(s.Id, out bucket))
                {
                    bucket = new List<ScannedMcpServerInfo>();
                    byId[s.Id] = bucket;
                }
                bucket.Add(s);
            }

            foreach (KeyValuePair<string, List<ScannedMcpServerInfo>> kvp in byId)
            {
                if (kvp.Value.Count <= 1) continue;
                McpConflictInfo conflict = new McpConflictInfo();
                conflict.Id = kvp.Key;
                conflict.Count = kvp.Value.Count;

                List<string> sources = new List<string>(kvp.Value.Count);
                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    sources.Add(kvp.Value[i].Scope + " (" + kvp.Value[i].SourcePath + ")");
                }
                conflict.Sources = sources;
                snap.McpConflicts.Add(conflict);

                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    kvp.Value[i].Duplicate = true;
                    kvp.Value[i].DuplicateSources = new List<string>(sources);
                }
            }
        }

        private static void ComputeTokenBudgetAudit(CockpitSnapshot snap)
        {
            int globalTokens = 0;
            int pluginTokens = 0;
            int projectTokens = 0;
            int overCount = 0;

            for (int i = 0; i < snap.Rules.Count; i++)
            {
                ScannedRuleInfo r = snap.Rules[i];
                if (r == null) continue;

                if (r.OverBudget)
                {
                    overCount++;
                    snap.OverBudgetFiles.Add(r);
                }

                if (string.Equals(r.Scope, "global", StringComparison.OrdinalIgnoreCase))
                {
                    globalTokens += r.EstimatedTokens;
                }
                else if (r.Scope.StartsWith("plugin:", StringComparison.OrdinalIgnoreCase))
                {
                    if (r.Enabled) pluginTokens += r.EstimatedTokens;
                }
                else if (r.Scope.StartsWith("project:", StringComparison.OrdinalIgnoreCase))
                {
                    projectTokens += r.EstimatedTokens;
                }
            }

            snap.GlobalAlwaysOnTokens = globalTokens;
            snap.EnabledPluginRulesTokens = pluginTokens;
            snap.ProjectRulesTokens = projectTokens;
            snap.TotalAlwaysOnTokens = globalTokens + pluginTokens;
            snap.OverBudgetCount = overCount;
        }

        private static ScannedRuleInfo BuildRuleInfo(string id, string scope, string pluginId, string path, bool alwaysOn, bool enabled, int referenceTokenThreshold)
        {
            try
            {
                string text = ReadTextSharedSafe(path);
                int tokens = EstimateTokenCount(text);
                ScannedRuleInfo r = new ScannedRuleInfo();
                r.Id = id;
                r.Scope = scope;
                r.PluginId = pluginId ?? string.Empty;
                r.SourcePath = path;
                r.CharCount = text.Length;
                r.LineCount = CountLines(text);
                r.ByteCount = Encoding.UTF8.GetByteCount(text);
                r.EstimatedTokens = tokens;
                r.OverBudget = (referenceTokenThreshold > 0 && tokens > referenceTokenThreshold);
                r.AlwaysOn = alwaysOn;
                r.Enabled = enabled;
                return r;
            }
            catch
            {
                return null;
            }
        }

        private static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int lines = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    if (i < text.Length - 1) lines++;
                }
                else if (text[i] == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n') continue;
                    if (i < text.Length - 1) lines++;
                }
            }
            return lines;
        }

        // ══════════════════════════════════════════════════════════════════
        // 2. SETTINGS PATCHING & UNIFIED TOGGLE
        // ══════════════════════════════════════════════════════════════════

        public static bool PatchUserSettings(
            string geminiPath,
            Dictionary<string, object> updates,
            out string backupPath,
            out string error)
        {
            backupPath = null;
            error = null;

            if (string.IsNullOrWhiteSpace(geminiPath))
            {
                error = "Gemini 路径不能为空。";
                return false;
            }
            if (updates == null || updates.Count == 0)
            {
                error = "未提供要修改的 userSettings 字段。";
                return false;
            }

            string configDir = Path.Combine(geminiPath, "config");
            if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);
            string configPath = Path.Combine(configDir, "config.json");

            string existing = File.Exists(configPath) ? ReadTextSharedSafe(configPath) : "{}";
            if (string.IsNullOrWhiteSpace(existing)) existing = "{}";

            // Validate incoming values before mutating
            Dictionary<string, string> literalMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> kvp in updates)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                {
                    error = "userSettings 包含空字段名。";
                    return false;
                }

                string key = kvp.Key.Trim();
                object val = kvp.Value;

                if (IsBooleanUserSettingKey(key))
                {
                    if (!(val is bool))
                    {
                        bool parsedBool;
                        if (val is string && bool.TryParse((string)val, out parsedBool))
                        {
                            val = parsedBool;
                        }
                        else
                        {
                            error = string.Format("字段 {0} 必须是布尔值 (true/false)。", key);
                            return false;
                        }
                    }
                    literalMap[key] = ((bool)val) ? "true" : "false";
                }
                else
                {
                    literalMap[key] = UnifiedJson.Serialize(val, false);
                }
            }

            string patched = existing;
            try
            {
                foreach (KeyValuePair<string, string> kvp in literalMap)
                {
                    patched = UnifiedJson.PatchNestedPathPreservingComments(
                        patched,
                        new string[] { "userSettings", kvp.Key },
                        kvp.Value);
                }
            }
            catch (Exception ex)
            {
                error = "补丁写入 config.json 失败: " + ex.Message;
                return false;
            }

            if (File.Exists(configPath))
            {
                backupPath = UnifiedJson.CreateTimestampedBackup(configPath, MaxTimestampedBackups);
            }
            SessionLifecycleService.WriteUtf8TextAtomicWithBackup(configPath, patched, false, null);
            return true;
        }

        private static bool IsBooleanUserSettingKey(string key)
        {
            return string.Equals(key, "enableTerminalSandbox", StringComparison.Ordinal) ||
                   string.Equals(key, "enableSoundsForSpecialEvents", StringComparison.Ordinal) ||
                   string.Equals(key, "useAiCredits", StringComparison.Ordinal);
        }

        public static bool ToggleCapability(
            string geminiPath,
            string kind,
            string id,
            bool? desiredEnabled,
            string sourcePathOverride,
            out bool resultingEnabled,
            out string targetFile,
            out string backupPath,
            out int httpCode,
            out string error)
        {
            resultingEnabled = false;
            targetFile = string.Empty;
            backupPath = null;
            httpCode = 400;
            error = null;

            if (string.IsNullOrWhiteSpace(geminiPath) || string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(id))
            {
                error = "kind 与 id 不能为空。";
                return false;
            }

            string normKind = kind.Trim().ToLowerInvariant();
            string cleanId = id.Trim();
            string configDir = Path.Combine(geminiPath, "config");

            if (normKind == "plugin")
            {
                targetFile = Path.Combine(configDir, "config.json");
                string existing = File.Exists(targetFile) ? ReadTextSharedSafe(targetFile) : "{}";
                if (string.IsNullOrWhiteSpace(existing)) existing = "{}";

                Dictionary<string, object> rootDict = TryParseDict(existing);
                if (rootDict == null)
                {
                    error = "config.json 格式损坏，无法解析。";
                    return false;
                }

                bool hasFolder = Directory.Exists(Path.Combine(configDir, "plugins", cleanId)) ||
                                 Directory.Exists(Path.Combine(configDir, "plugins", CapabilityVaultService.EncodeSafeCapabilityFolderName(cleanId)));

                string pluginsSectionKey = "plugins";
                Dictionary<string, object> pluginsDict = null;
                object pluginsObj;
                if (TryGetDictKeyAndValueIgnoreCase(rootDict, "plugins", out pluginsSectionKey, out pluginsObj))
                    pluginsDict = UnifiedJson.AsDict(pluginsObj);
                else
                    pluginsSectionKey = "plugins";

                string matchedPluginKey = cleanId;
                object pluginEntryObj = null;
                bool inConfig = pluginsDict != null && TryGetDictKeyAndValueIgnoreCase(pluginsDict, cleanId, out matchedPluginKey, out pluginEntryObj);
                if (!inConfig) matchedPluginKey = cleanId;

                ManifestControlInfo pluginsManifest = TryParseManifestControl(Path.Combine(configDir, "plugins.json"), "plugins", "global");
                bool inManifest = pluginsManifest != null &&
                    (pluginsManifest.Entries.ContainsKey(cleanId) ||
                     pluginsManifest.IncludeOnly.Exists(delegate(string x) { return string.Equals(x, cleanId, StringComparison.OrdinalIgnoreCase); }) ||
                     pluginsManifest.Exclude.Exists(delegate(string x) { return string.Equals(x, cleanId, StringComparison.OrdinalIgnoreCase); }));

                if (!hasFolder && !inConfig && !inManifest)
                {
                    httpCode = 404;
                    error = "未找到指定的插件: " + cleanId;
                    return false;
                }

                bool currentEnabled = true;
                if (inConfig)
                {
                    Dictionary<string, object> pEntry = UnifiedJson.AsDict(pluginEntryObj);
                    if (pEntry != null) currentEnabled = UnifiedJson.GetBool(pEntry, "enabled", true);
                }
                else if (inManifest)
                {
                    string mStatus = EvaluateManifestEntryStatus(pluginsManifest, cleanId);
                    if (string.Equals(mStatus, "excluded", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(mStatus, "disabled", StringComparison.OrdinalIgnoreCase))
                        currentEnabled = false;
                }
                resultingEnabled = desiredEnabled.HasValue ? desiredEnabled.Value : !currentEnabled;

                string patched = UnifiedJson.PatchNestedPathPreservingComments(
                    existing,
                    new string[] { pluginsSectionKey, matchedPluginKey, "enabled" },
                    resultingEnabled ? "true" : "false");

                if (File.Exists(targetFile))
                    backupPath = UnifiedJson.CreateTimestampedBackup(targetFile, MaxTimestampedBackups);
                else if (!Directory.Exists(configDir))
                    Directory.CreateDirectory(configDir);

                SessionLifecycleService.WriteUtf8TextAtomicWithBackup(targetFile, patched, false, null);
                httpCode = 200;
                return true;
            }
            else if (normKind == "mcp" || normKind == "mcpserver")
            {
                targetFile = string.IsNullOrWhiteSpace(sourcePathOverride)
                    ? Path.Combine(configDir, "mcp_config.json")
                    : sourcePathOverride.Trim();

                if (!IsPathUnderAllowedMcpRoot(geminiPath, targetFile))
                {
                    httpCode = 403;
                    error = "拒绝修改允许目录之外的 MCP 配置文件。";
                    return false;
                }
                if (!File.Exists(targetFile))
                {
                    httpCode = 404;
                    error = "MCP 配置文件不存在: " + targetFile;
                    return false;
                }

                string existing = ReadTextSharedSafe(targetFile);
                Dictionary<string, object> rootDict = TryParseDict(existing);
                string serversSectionKey = "mcpServers";
                Dictionary<string, object> serversDict = null;
                object serversObj;
                if (rootDict != null && TryGetDictKeyAndValueIgnoreCase(rootDict, "mcpServers", out serversSectionKey, out serversObj))
                    serversDict = UnifiedJson.AsDict(serversObj);

                string matchedSrvKey = cleanId;
                object srvObj = null;
                if (serversDict == null || !TryGetDictKeyAndValueIgnoreCase(serversDict, cleanId, out matchedSrvKey, out srvObj))
                {
                    httpCode = 404;
                    error = "未在配置文件中找到 MCP 服务: " + cleanId;
                    return false;
                }

                Dictionary<string, object> srvEntry = UnifiedJson.AsDict(srvObj);
                bool currentDisabled = srvEntry != null && UnifiedJson.GetBool(srvEntry, "disabled", false);
                resultingEnabled = desiredEnabled.HasValue ? desiredEnabled.Value : currentDisabled;
                bool targetDisabled = !resultingEnabled;

                string patched = UnifiedJson.PatchNestedPathPreservingComments(
                    existing,
                    new string[] { serversSectionKey, matchedSrvKey, "disabled" },
                    targetDisabled ? "true" : "false");

                backupPath = UnifiedJson.CreateTimestampedBackup(targetFile, MaxTimestampedBackups);
                SessionLifecycleService.WriteUtf8TextAtomicWithBackup(targetFile, patched, false, null);
                httpCode = 200;
                return true;
            }
            else if (normKind == "hook")
            {
                targetFile = string.IsNullOrWhiteSpace(sourcePathOverride)
                    ? Path.Combine(configDir, "hooks.json")
                    : sourcePathOverride.Trim();

                if (!IsPathUnderAllowedMcpRoot(geminiPath, targetFile))
                {
                    httpCode = 403;
                    error = "拒绝修改允许目录之外的 Hook 配置文件。";
                    return false;
                }
                if (!File.Exists(targetFile))
                {
                    httpCode = 404;
                    error = "Hook 配置文件不存在: " + targetFile;
                    return false;
                }

                string existing = ReadTextSharedSafe(targetFile);
                Dictionary<string, object> rootDict = TryParseDict(existing);
                if (rootDict == null)
                {
                    error = "hooks.json 格式损坏，无法解析。";
                    return false;
                }

                bool rootHasDirectEvents = false;
                foreach (string rk in rootDict.Keys)
                {
                    if (KnownHookEventNames.Contains(rk)) { rootHasDirectEvents = true; break; }
                }

                string wrapperKey = null;
                Dictionary<string, object> wrapperDict = null;
                object wrapperObj;
                if (!rootHasDirectEvents && TryGetDictKeyAndValueIgnoreCase(rootDict, "hooks", out wrapperKey, out wrapperObj))
                {
                    Dictionary<string, object> wd = UnifiedJson.AsDict(wrapperObj);
                    if (wd != null)
                    {
                        foreach (string wk in wd.Keys)
                        {
                            if (KnownHookEventNames.Contains(wk)) { wrapperDict = wd; break; }
                        }
                    }
                }

                string matchedHookKey = null;
                object matchedHookObj = null;
                bool foundNamedGroup = TryGetDictKeyAndValueIgnoreCase(rootDict, cleanId, out matchedHookKey, out matchedHookObj);

                string patched;
                if (foundNamedGroup)
                {
                    Dictionary<string, object> hDict = UnifiedJson.AsDict(matchedHookObj);
                    bool currentEnabled = hDict != null ? UnifiedJson.GetBool(hDict, "enabled", true) : true;
                    resultingEnabled = desiredEnabled.HasValue ? desiredEnabled.Value : !currentEnabled;
                    patched = UnifiedJson.PatchNestedPathPreservingComments(
                        existing,
                        new string[] { matchedHookKey, "enabled" },
                        resultingEnabled ? "true" : "false");
                }
                else if (rootHasDirectEvents)
                {
                    bool currentEnabled = UnifiedJson.GetBool(rootDict, "enabled", true);
                    resultingEnabled = desiredEnabled.HasValue ? desiredEnabled.Value : !currentEnabled;
                    patched = UnifiedJson.PatchNestedPathPreservingComments(
                        existing,
                        new string[] { "enabled" },
                        resultingEnabled ? "true" : "false");
                }
                else if (wrapperDict != null)
                {
                    bool currentEnabled = UnifiedJson.GetBool(rootDict, "enabled", true) && UnifiedJson.GetBool(wrapperDict, "enabled", true);
                    resultingEnabled = desiredEnabled.HasValue ? desiredEnabled.Value : !currentEnabled;
                    patched = UnifiedJson.PatchNestedPathPreservingComments(
                        existing,
                        new string[] { "enabled" },
                        resultingEnabled ? "true" : "false");
                    if (wrapperDict.ContainsKey("enabled") || wrapperDict.ContainsKey("Enabled"))
                    {
                        string wEnabledKey;
                        object dummy;
                        TryGetDictKeyAndValueIgnoreCase(wrapperDict, "enabled", out wEnabledKey, out dummy);
                        patched = UnifiedJson.PatchNestedPathPreservingComments(
                            patched,
                            new string[] { wrapperKey, wEnabledKey ?? "enabled" },
                            resultingEnabled ? "true" : "false");
                    }
                }
                else
                {
                    httpCode = 404;
                    error = "未在 hooks.json 中找到 Hook 项: " + cleanId;
                    return false;
                }

                backupPath = UnifiedJson.CreateTimestampedBackup(targetFile, MaxTimestampedBackups);
                SessionLifecycleService.WriteUtf8TextAtomicWithBackup(targetFile, patched, false, null);
                httpCode = 200;
                return true;
            }
            else
            {
                error = "不支持的能力类型: " + kind + "（仅支持 plugin / mcp / hook）。";
                return false;
            }
        }

        private static bool IsPathUnderAllowedMcpRoot(string geminiPath, string candidatePath)
        {
            string can;
            string err;
            if (!ApiRouter.TryValidateCanonicalLocalPath(candidatePath, out can, out err))
                return false;

            if (SessionLifecycleService.IsPathContainedUnder(geminiPath, can))
                return true;

            Dictionary<string, ProjectMetadata> projs = WorkspaceService.LoadLocalProjects(geminiPath);
            if (projs != null)
            {
                foreach (KeyValuePair<string, ProjectMetadata> kvp in projs)
                {
                    ProjectMetadata pm = kvp.Value;
                    if (pm == null) continue;
                    List<string> uris = new List<string>();
                    if (!string.IsNullOrEmpty(pm.FolderUri)) uris.Add(pm.FolderUri);
                    if (pm.FolderUris != null) uris.AddRange(pm.FolderUris);
                    for (int i = 0; i < uris.Count; i++)
                    {
                        string projRoot = WorkspaceService.UriToDisplayPath(uris[i]);
                        if (!string.IsNullOrEmpty(projRoot) && SessionLifecycleService.IsPathContainedUnder(projRoot, can))
                            return true;
                    }
                }
            }
            return false;
        }

        // ══════════════════════════════════════════════════════════════════
        // 3. MCP HEALTH DOCTOR (Stdio JSON-RPC initialize & HTTP Probe)
        // ══════════════════════════════════════════════════════════════════

        public static List<McpHealthCheckResult> RunMcpHealthCheck(
            string geminiPath,
            string homePath,
            string serverIdOrNull,
            string sourcePathOrNull,
            bool includeDisabled,
            int timeoutMs)
        {
            if (timeoutMs <= 0) timeoutMs = DefaultMcpTimeoutMs;
            if (timeoutMs > 10000) timeoutMs = 10000;

            CockpitSnapshot snap = ScanCockpit(geminiPath, homePath, null);
            List<McpHealthCheckResult> results = new List<McpHealthCheckResult>();

            for (int i = 0; i < snap.McpServers.Count; i++)
            {
                ScannedMcpServerInfo srv = snap.McpServers[i];
                if (srv == null) continue;

                if (!string.IsNullOrWhiteSpace(serverIdOrNull) &&
                    !string.Equals(srv.Id, serverIdOrNull.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(sourcePathOrNull) &&
                    !string.Equals(srv.SourcePath, sourcePathOrNull.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if ((srv.Disabled || !srv.Enabled) && string.IsNullOrWhiteSpace(serverIdOrNull) && !includeDisabled)
                {
                    McpHealthCheckResult skipped = new McpHealthCheckResult();
                    skipped.Id = srv.Id;
                    skipped.Scope = srv.Scope;
                    skipped.SourcePath = srv.SourcePath;
                    skipped.Transport = srv.Transport;
                    skipped.Disabled = true;
                    skipped.Healthy = false;
                    skipped.Status = "disabled";
                    results.Add(skipped);
                    continue;
                }

                if (string.Equals(srv.Transport, "http", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(ProbeHttpMcpServer(srv, timeoutMs));
                }
                else
                {
                    results.Add(ProbeStdioMcpServer(srv, geminiPath, homePath, timeoutMs));
                }
            }

            return results;
        }

        public static McpHealthCheckResult ProbeStdioMcpServer(
            ScannedMcpServerInfo srv,
            string geminiPath,
            string homePath,
            int timeoutMs)
        {
            McpHealthCheckResult res = new McpHealthCheckResult();
            res.Id = srv.Id;
            res.Scope = srv.Scope;
            res.SourcePath = srv.SourcePath;
            res.Transport = "stdio";
            res.Disabled = srv.Disabled;

            if (timeoutMs <= 0) timeoutMs = DefaultMcpTimeoutMs;

            string pluginRoot = srv.PluginRoot ?? string.Empty;
            string effectiveHome = !string.IsNullOrWhiteSpace(homePath) ? homePath : WorkspaceService.ResolveDefaultHomePath();

            string expandedCmd = ExpandMcpTokens(srv.Command, pluginRoot, effectiveHome);
            if (string.IsNullOrWhiteSpace(expandedCmd))
            {
                res.Status = "missing_command";
                res.Error = "MCP 配置缺少 command 字段。";
                return res;
            }

            string resolvedCmd = ResolveExecutableCommand(expandedCmd, pluginRoot);
            if (string.IsNullOrEmpty(resolvedCmd))
            {
                res.Status = "missing_command";
                res.Error = "未找到可执行命令: " + expandedCmd;
                return res;
            }

            List<string> expandedArgs = new List<string>(srv.Args.Count);
            bool nextIsInlineCode = false;
            for (int i = 0; i < srv.Args.Count; i++)
            {
                string rawArg = srv.Args[i] ?? string.Empty;
                string expArg = ExpandMcpTokens(rawArg, pluginRoot, effectiveHome);
                expandedArgs.Add(expArg);

                if (nextIsInlineCode)
                {
                    nextIsInlineCode = false;
                    continue;
                }
                if (string.Equals(rawArg, "-e", StringComparison.Ordinal) ||
                    string.Equals(rawArg, "--eval", StringComparison.Ordinal) ||
                    string.Equals(rawArg, "-c", StringComparison.Ordinal))
                {
                    nextIsInlineCode = true;
                    continue;
                }
                if (rawArg.StartsWith("-", StringComparison.Ordinal))
                {
                    continue;
                }

                string missingScript;
                if (LooksLikeScriptPathArg(expArg) && !ResolveScriptArgExists(expArg, pluginRoot, geminiPath, out missingScript))
                {
                    res.Status = "missing_script";
                    res.Error = "MCP 脚本参数指向的文件不存在: " + missingScript;
                    return res;
                }
            }

            Process proc = null;
            ManualResetEventSlim gotResponseEvent = new ManualResetEventSlim(false);
            ManualResetEventSlim exitedEvent = new ManualResetEventSlim(false);
            ManualResetEventSlim stdoutEofEvent = new ManualResetEventSlim(false);
            ManualResetEventSlim stderrEofEvent = new ManualResetEventSlim(false);
            StringBuilder stderrSb = new StringBuilder();
            StringBuilder stdoutSb = new StringBuilder();
            object ioLock = new object();
            string validJsonResponseLine = null;
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                string launchFile = resolvedCmd;
                string joinedArgs = BuildWindowsCommandLineArgs(expandedArgs);
                if (resolvedCmd.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                    resolvedCmd.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
                {
                    launchFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                    if (!File.Exists(launchFile)) launchFile = "cmd.exe";
                    joinedArgs = string.IsNullOrEmpty(joinedArgs)
                        ? ("/c \"" + resolvedCmd + "\"")
                        : ("/c \"\"" + resolvedCmd + "\" " + joinedArgs + "\"");
                }

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = launchFile;
                psi.Arguments = joinedArgs;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;

                if (!string.IsNullOrEmpty(pluginRoot) && Directory.Exists(pluginRoot))
                {
                    psi.WorkingDirectory = pluginRoot;
                    psi.EnvironmentVariables["PLUGIN_ROOT"] = pluginRoot;
                    psi.EnvironmentVariables["CLAUDE_PLUGIN_ROOT"] = pluginRoot;
                }

                if (srv.Env != null)
                {
                    foreach (KeyValuePair<string, string> kvp in srv.Env)
                    {
                        if (!string.IsNullOrEmpty(kvp.Key))
                        {
                            psi.EnvironmentVariables[kvp.Key] = ExpandMcpTokens(kvp.Value ?? string.Empty, pluginRoot, effectiveHome);
                        }
                    }
                }

                proc = new Process();
                proc.StartInfo = psi;
                proc.EnableRaisingEvents = true;
                proc.Exited += delegate(object sender, EventArgs e)
                {
                    exitedEvent.Set();
                };
                proc.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null)
                    {
                        stdoutEofEvent.Set();
                        return;
                    }
                    string line = e.Data.Trim().TrimStart('\uFEFF');
                    lock (ioLock)
                    {
                        if (stdoutSb.Length < 8192) stdoutSb.AppendLine(line);
                        if (validJsonResponseLine == null && LooksLikeJsonRpcInitializeResponse(line))
                        {
                            validJsonResponseLine = line;
                            gotResponseEvent.Set();
                        }
                    }
                };
                proc.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null)
                    {
                        stderrEofEvent.Set();
                        return;
                    }
                    lock (ioLock)
                    {
                        if (stderrSb.Length < 8192) stderrSb.AppendLine(e.Data);
                    }
                };

                if (!proc.Start())
                {
                    res.Status = "spawn_failed";
                    res.Error = "无法启动 MCP 进程: " + resolvedCmd;
                    return res;
                }

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                string initReq = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"antigravity-companion\",\"version\":\"1.0\"}}}\n";
                try
                {
                    byte[] reqBytes = Encoding.UTF8.GetBytes(initReq);
                    proc.StandardInput.BaseStream.Write(reqBytes, 0, reqBytes.Length);
                    proc.StandardInput.BaseStream.Flush();
                }
                catch { }

                if (proc.HasExited)
                {
                    exitedEvent.Set();
                }

                int signaled = WaitHandle.WaitAny(
                    new WaitHandle[] { gotResponseEvent.WaitHandle, exitedEvent.WaitHandle },
                    timeoutMs);

                sw.Stop();
                res.LatencyMs = sw.ElapsedMilliseconds;

                if (signaled == 1)
                {
                    // Wait deterministically for async stdout/stderr EOF callbacks (unless child spawned a lingering grandchild holding handles)
                    try { stdoutEofEvent.Wait(300); } catch { }
                    try { stderrEofEvent.Wait(300); } catch { }
                }

                string respLineSnap;
                string stderrText;
                lock (ioLock)
                {
                    respLineSnap = validJsonResponseLine;
                    stderrText = stderrSb.ToString().Trim();
                }
                res.Stderr = stderrText;

                if (!string.IsNullOrEmpty(respLineSnap))
                {
                    ParseJsonRpcInitializePayload(respLineSnap, res);
                    return res;
                }

                if (signaled == WaitHandle.WaitTimeout)
                {
                    res.Healthy = false;
                    res.Status = "timeout";
                    res.Error = string.Format("MCP initialize 响应超时（>{0}ms）。", timeoutMs);
                    return res;
                }

                int exitCode = 0;
                try { exitCode = proc.HasExited ? proc.ExitCode : -1; } catch { }
                res.Healthy = false;
                res.Status = "crashed";
                res.Error = !string.IsNullOrEmpty(stderrText)
                    ? stderrText
                    : string.Format("MCP 进程未返回 initialize 响应即退出（ExitCode={0}）。", exitCode);
                return res;
            }
            catch (Exception ex)
            {
                sw.Stop();
                res.LatencyMs = sw.ElapsedMilliseconds;
                res.Healthy = false;
                res.Status = "error";
                res.Error = ex.Message;
                return res;
            }
            finally
            {
                if (proc != null)
                {
                    EnsureProcessTerminated(proc);
                    try { proc.Dispose(); } catch { }
                }
                gotResponseEvent.Dispose();
                exitedEvent.Dispose();
                stdoutEofEvent.Dispose();
                stderrEofEvent.Dispose();
            }
        }

        private static bool LooksLikeJsonRpcInitializeResponse(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            string clean = line.Trim().TrimStart('\uFEFF');
            if (clean.Length == 0 || clean[0] != '{') return false;
            object parsed;
            if (!UnifiedJson.TryParse(clean, out parsed)) return false;
            Dictionary<string, object> d = UnifiedJson.AsDict(parsed);
            if (d == null) return false;
            // Ignore JSON-RPC notifications (which have "method" and no "result"/"error")
            return d.ContainsKey("result") || d.ContainsKey("error");
        }

        private static void ParseJsonRpcInitializePayload(string jsonLine, McpHealthCheckResult res)
        {
            object parsed;
            if (!UnifiedJson.TryParse(jsonLine, out parsed))
            {
                res.Healthy = false;
                res.Status = "invalid_response";
                res.Error = "无法解析 MCP JSON-RPC 响应。";
                return;
            }

            Dictionary<string, object> d = UnifiedJson.AsDict(parsed);
            if (d == null)
            {
                res.Healthy = false;
                res.Status = "invalid_response";
                return;
            }

            object errObj;
            if (d.TryGetValue("error", out errObj) && errObj != null)
            {
                Dictionary<string, object> ed = UnifiedJson.AsDict(errObj);
                res.Healthy = false;
                res.Status = "rpc_error";
                res.Error = ed != null ? UnifiedJson.GetString(ed, "message", "JSON-RPC error") : Convert.ToString(errObj, CultureInfo.InvariantCulture);
                return;
            }

            object resultObj;
            if (d.TryGetValue("result", out resultObj) && resultObj != null)
            {
                Dictionary<string, object> rd = UnifiedJson.AsDict(resultObj);
                res.Healthy = true;
                res.Reachable = true;
                res.Status = "healthy";
                if (rd != null)
                {
                    res.ProtocolVersion = UnifiedJson.GetString(rd, "protocolVersion", string.Empty);
                    object siObj;
                    if (rd.TryGetValue("serverInfo", out siObj))
                    {
                        Dictionary<string, object> si = UnifiedJson.AsDict(siObj);
                        if (si != null)
                        {
                            res.ServerName = UnifiedJson.GetString(si, "name", string.Empty);
                            res.ServerVersion = UnifiedJson.GetString(si, "version", string.Empty);
                        }
                    }
                }
                return;
            }

            res.Healthy = false;
            res.Status = "invalid_response";
            res.Error = "JSON-RPC 响应缺少 result 字段。";
        }

        private static void EnsureProcessTerminated(Process proc)
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

        public static McpHealthCheckResult ProbeHttpMcpServer(ScannedMcpServerInfo srv, int timeoutMs)
        {
            McpHealthCheckResult res = new McpHealthCheckResult();
            res.Id = srv.Id;
            res.Scope = srv.Scope;
            res.SourcePath = srv.SourcePath;
            res.Transport = "http";
            res.Disabled = srv.Disabled;

            if (timeoutMs <= 0) timeoutMs = DefaultMcpTimeoutMs;

            string rawUrl = (srv.Url ?? string.Empty).Trim();
            Uri uri;
            if (string.IsNullOrEmpty(rawUrl) ||
                !Uri.TryCreate(rawUrl, UriKind.Absolute, out uri) ||
                (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                res.Healthy = false;
                res.Reachable = false;
                res.Status = "invalid_url";
                res.Error = "无效的 HTTP/HTTPS MCP URL: " + rawUrl;
                return res;
            }

            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(uri);
                req.Method = "POST";
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.KeepAlive = false;
                req.ServicePoint.Expect100Continue = false;
                req.ContentType = "application/json";
                req.Accept = "application/json, text/event-stream";

                byte[] payload = Encoding.UTF8.GetBytes(
                    "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"antigravity-companion\",\"version\":\"1.0\"}}}");
                req.ContentLength = payload.Length;
                using (Stream reqStream = req.GetRequestStream())
                {
                    reqStream.Write(payload, 0, payload.Length);
                }

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    sw.Stop();
                    res.LatencyMs = sw.ElapsedMilliseconds;
                    res.HttpStatusCode = (int)resp.StatusCode;
                    res.Reachable = true;
                    res.Healthy = res.HttpStatusCode >= 200 && res.HttpStatusCode < 400;
                    res.Status = res.Healthy ? "healthy" : ("http_" + res.HttpStatusCode.ToString(CultureInfo.InvariantCulture));
                    if (res.Healthy)
                    {
                        try
                        {
                            using (Stream respStream = resp.GetResponseStream())
                            {
                                if (respStream != null)
                                {
                                    using (StreamReader sr = new StreamReader(respStream, Encoding.UTF8))
                                    {
                                        char[] buf = new char[16384];
                                        int n = sr.Read(buf, 0, buf.Length);
                                        if (n > 0)
                                        {
                                            string bodyText = new string(buf, 0, n).Trim();
                                            if (LooksLikeJsonRpcInitializeResponse(bodyText))
                                            {
                                                McpHealthCheckResult parsed = new McpHealthCheckResult();
                                                ParseJsonRpcInitializePayload(bodyText, parsed);
                                                if (!string.IsNullOrEmpty(parsed.ServerName)) res.ServerName = parsed.ServerName;
                                                if (!string.IsNullOrEmpty(parsed.ServerVersion)) res.ServerVersion = parsed.ServerVersion;
                                                if (!string.IsNullOrEmpty(parsed.ProtocolVersion)) res.ProtocolVersion = parsed.ProtocolVersion;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                    return res;
                }
            }
            catch (WebException wex)
            {
                sw.Stop();
                res.LatencyMs = sw.ElapsedMilliseconds;
                HttpWebResponse errResp = wex.Response as HttpWebResponse;
                if (errResp != null)
                {
                    res.HttpStatusCode = (int)errResp.StatusCode;
                    res.Reachable = true;
                    res.Healthy = false;
                    res.Status = "http_" + res.HttpStatusCode.ToString(CultureInfo.InvariantCulture);
                    res.Error = "HTTP " + res.HttpStatusCode.ToString(CultureInfo.InvariantCulture) + " " + errResp.StatusDescription;
                    try { errResp.Close(); } catch { }
                }
                else
                {
                    res.Reachable = false;
                    res.Healthy = false;
                    res.Status = wex.Status == WebExceptionStatus.Timeout ? "timeout" : "unreachable";
                    res.Error = wex.Message;
                }
                return res;
            }
            catch (Exception ex)
            {
                sw.Stop();
                res.LatencyMs = sw.ElapsedMilliseconds;
                res.Reachable = false;
                res.Healthy = false;
                res.Status = "error";
                res.Error = ex.Message;
                return res;
            }
        }

        private static string ExpandMcpTokens(string input, string pluginRoot, string homePath)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            string s = input;
            if (!string.IsNullOrEmpty(pluginRoot))
            {
                s = CapabilityPathNormalizer.ReplaceIgnoreCase(s, "${PLUGIN_ROOT}", pluginRoot);
                s = CapabilityPathNormalizer.ReplaceIgnoreCase(s, "${CLAUDE_PLUGIN_ROOT}", pluginRoot);
                s = CapabilityPathNormalizer.ReplaceIgnoreCase(s, "{{PLUGIN_ROOT}}", pluginRoot);
                s = CapabilityPathNormalizer.ReplaceIgnoreCase(s, "$PLUGIN_ROOT", pluginRoot);
            }
            if (!string.IsNullOrEmpty(homePath))
            {
                string cleanHome = homePath.TrimEnd('\\', '/');
                if (s.StartsWith("~/", StringComparison.Ordinal) || s.StartsWith("~\\", StringComparison.Ordinal))
                {
                    s = cleanHome + s.Substring(1);
                }
            }
            if (s.IndexOf('%') >= 0)
            {
                s = Regex.Replace(s, "%([A-Za-z0-9_().-]+)%", delegate(Match m)
                {
                    string v = Environment.GetEnvironmentVariable(m.Groups[1].Value);
                    return !string.IsNullOrEmpty(v) ? v : m.Value;
                });
            }
            return s;
        }

        private static string ResolveExecutableCommand(string cmd, string pluginRoot)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return null;
            string trimmed = cmd.Trim().Trim('"');
            bool hasSeparator = trimmed.IndexOf('\\') >= 0 || trimmed.IndexOf('/') >= 0 || (trimmed.Length >= 2 && trimmed[1] == ':');
            bool hasExt = !string.IsNullOrEmpty(Path.GetExtension(trimmed));

            // On Windows, prefer executable extensions (.exe/.cmd/.bat/.com) before extensionless files
            // so POSIX shim scripts (e.g. %APPDATA%\npm\npx alongside npx.cmd) do not shadow .cmd/.exe!
            string[] exts = hasExt
                ? new string[] { "" }
                : new string[] { ".exe", ".com", ".cmd", ".bat", "" };

            if (hasSeparator)
            {
                string candidateBase = trimmed;
                if (!Path.IsPathRooted(candidateBase) && !string.IsNullOrEmpty(pluginRoot))
                {
                    try { candidateBase = Path.GetFullPath(Path.Combine(pluginRoot, candidateBase)); }
                    catch { }
                }
                for (int i = 0; i < exts.Length; i++)
                {
                    string c = exts[i].Length == 0
                        ? candidateBase
                        : candidateBase + exts[i];
                    if (File.Exists(c)) return c;
                }
                return null;
            }

            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            string[] dirs = pathEnv.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int d = 0; d < dirs.Length; d++)
            {
                string dir = dirs[d].Trim().Trim('"');
                if (string.IsNullOrEmpty(dir)) continue;
                for (int i = 0; i < exts.Length; i++)
                {
                    try
                    {
                        string p = Path.Combine(dir, trimmed + exts[i]);
                        if (File.Exists(p)) return p;
                    }
                    catch { }
                }
            }
            return null;
        }

        private static bool LooksLikeScriptPathArg(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg)) return false;
            string t = arg.Trim().Trim('"');
            if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return false;

            // npm scoped packages (@org/pkg or @org/pkg@version) and package scheme specifiers (npm:pkg, uvx:pkg) are NOT local script paths
            if (t.StartsWith("@", StringComparison.Ordinal) ||
                t.StartsWith("npm:", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("npx:", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("uvx:", StringComparison.OrdinalIgnoreCase))
                return false;

            if (t.IndexOf('\\') >= 0 || t.IndexOf('/') >= 0 || (t.Length >= 3 && t[1] == ':'))
                return true;

            string ext = Path.GetExtension(t);
            return !string.IsNullOrEmpty(ext) && ScriptArgExtensions.Contains(ext);
        }

        private static bool ResolveScriptArgExists(string arg, string pluginRoot, string geminiPath, out string checkedPath)
        {
            string t = arg.Trim().Trim('"');
            checkedPath = t;
            try
            {
                if (Path.IsPathRooted(t))
                {
                    checkedPath = Path.GetFullPath(t);
                    return File.Exists(checkedPath) || Directory.Exists(checkedPath);
                }

                if (!string.IsNullOrEmpty(pluginRoot))
                {
                    string p1 = Path.GetFullPath(Path.Combine(pluginRoot, t));
                    checkedPath = p1;
                    if (File.Exists(p1) || Directory.Exists(p1)) return true;
                }

                if (!string.IsNullOrEmpty(geminiPath))
                {
                    string p2 = Path.GetFullPath(Path.Combine(geminiPath, "config", t));
                    if (File.Exists(p2) || Directory.Exists(p2)) { checkedPath = p2; return true; }
                }

                return File.Exists(t) || Directory.Exists(t);
            }
            catch
            {
                return false;
            }
        }

        private static string BuildWindowsCommandLineArgs(List<string> args)
        {
            if (args == null || args.Count == 0) return string.Empty;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < args.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(QuoteArg(args[i] ?? string.Empty));
            }
            return sb.ToString();
        }

        private static string QuoteArg(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return "\"\"";
            bool needsQuotes = false;
            for (int i = 0; i < arg.Length; i++)
            {
                char c = arg[i];
                if (char.IsWhiteSpace(c) || c == '"') { needsQuotes = true; break; }
            }
            if (!needsQuotes) return arg;

            StringBuilder sb = new StringBuilder();
            sb.Append('"');
            int backslashes = 0;
            for (int i = 0; i < arg.Length; i++)
            {
                char c = arg[i];
                if (c == '\\')
                {
                    backslashes++;
                }
                else if (c == '"')
                {
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                    backslashes = 0;
                }
                else
                {
                    if (backslashes > 0)
                    {
                        sb.Append('\\', backslashes);
                        backslashes = 0;
                    }
                    sb.Append(c);
                }
            }
            if (backslashes > 0) sb.Append('\\', backslashes * 2);
            sb.Append('"');
            return sb.ToString();
        }

        // ══════════════════════════════════════════════════════════════════
        // 4. INLINE CUSTOMIZATION FILE EDITOR (STRICT ALLOWLIST)
        // ══════════════════════════════════════════════════════════════════

        public static EditorFileResult ReadAllowedCustomizationFile(
            string geminiPath,
            string homePath,
            string repoPath,
            string rawPath)
        {
            return ReadAllowedCustomizationFile(geminiPath, homePath, repoPath, rawPath, 0);
        }

        public static EditorFileResult ReadAllowedCustomizationFile(
            string geminiPath,
            string homePath,
            string repoPath,
            string rawPath,
            int referenceTokenThreshold)
        {
            EditorFileResult res = new EditorFileResult();
            string canonicalPath;
            int errCode;
            string errMsg;
            if (!TryValidateEditorFilePath(geminiPath, homePath, repoPath, rawPath, false, out canonicalPath, out errCode, out errMsg))
            {
                res.Ok = false;
                res.HttpStatusCode = errCode;
                res.Error = errMsg;
                return res;
            }

            if (!File.Exists(canonicalPath))
            {
                res.Ok = false;
                res.HttpStatusCode = 404;
                res.Error = "未找到目标配置文件: " + canonicalPath;
                return res;
            }

            string text = ReadTextSharedSafe(canonicalPath);
            int tokens = EstimateTokenCount(text);
            res.Ok = true;
            res.HttpStatusCode = 200;
            res.CanonicalPath = canonicalPath;
            res.Content = text;
            res.CharCount = text.Length;
            res.EstimatedTokens = tokens;
            res.OverBudget = (referenceTokenThreshold > 0 && tokens > referenceTokenThreshold);
            return res;
        }

        public static EditorFileResult SaveAllowedCustomizationFile(
            string geminiPath,
            string homePath,
            string repoPath,
            string rawPath,
            string content)
        {
            return SaveAllowedCustomizationFile(geminiPath, homePath, repoPath, rawPath, content, 0);
        }

        public static EditorFileResult SaveAllowedCustomizationFile(
            string geminiPath,
            string homePath,
            string repoPath,
            string rawPath,
            string content,
            int referenceTokenThreshold)
        {
            EditorFileResult res = new EditorFileResult();
            string canonicalPath;
            int errCode;
            string errMsg;
            if (!TryValidateEditorFilePath(geminiPath, homePath, repoPath, rawPath, true, out canonicalPath, out errCode, out errMsg))
            {
                res.Ok = false;
                res.HttpStatusCode = errCode;
                res.Error = errMsg;
                return res;
            }

            string text = content ?? string.Empty;
            string ext = Path.GetExtension(canonicalPath).ToLowerInvariant();
            if (ext == ".json" || ext == ".jsonc")
            {
                try
                {
                    UnifiedJson.Parse(text);
                }
                catch (Exception ex)
                {
                    res.Ok = false;
                    res.HttpStatusCode = 400;
                    res.Error = "JSON/JSONC 语法校验失败，已拒绝保存: " + ex.Message;
                    return res;
                }
            }

            string parentDir = Path.GetDirectoryName(canonicalPath);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                Directory.CreateDirectory(parentDir);

            string bakPath = null;
            if (File.Exists(canonicalPath))
            {
                bakPath = UnifiedJson.CreateTimestampedBackup(canonicalPath, MaxTimestampedBackups);
            }
            SessionLifecycleService.WriteUtf8TextAtomicWithBackup(canonicalPath, text, false, null);

            int tokens = EstimateTokenCount(text);
            res.Ok = true;
            res.HttpStatusCode = 200;
            res.CanonicalPath = canonicalPath;
            res.Content = text;
            res.CharCount = text.Length;
            res.EstimatedTokens = tokens;
            res.OverBudget = (referenceTokenThreshold > 0 && tokens > referenceTokenThreshold);
            res.BackupPath = bakPath ?? string.Empty;
            return res;
        }

        public static bool TryValidateEditorFilePath(
            string geminiPath,
            string homePath,
            string repoPath,
            string rawPath,
            bool allowCreate,
            out string canonicalPath,
            out int httpErrorCode,
            out string error)
        {
            canonicalPath = null;
            httpErrorCode = 400;
            error = null;

            string pathErr;
            if (!ApiRouter.TryValidateCanonicalLocalPath(rawPath, out canonicalPath, out pathErr))
            {
                httpErrorCode = 400;
                error = pathErr;
                return false;
            }

            string ext = Path.GetExtension(canonicalPath);
            if (string.IsNullOrEmpty(ext) || !AllowedEditorExtensions.Contains(ext))
            {
                httpErrorCode = 403;
                error = "仅允许编辑 .md / .json / .jsonc 定制配置文件。";
                return false;
            }

            string fileName = Path.GetFileName(canonicalPath) ?? string.Empty;
            if (IsSensitiveAuthFileName(fileName))
            {
                httpErrorCode = 403;
                error = "安全策略禁止读取或修改敏感认证/密钥文件: " + fileName;
                return false;
            }

            // Check allowed roots:
            // 1. Inside ~/.gemini/ (excluding antigravity-ide junction)
            // 2. Inside active Vault (repoPath)
            // 3. Registered project: .agents/, .agent/, GEMINI.md, AGENTS.md, skills.json, plugins.json, hooks.json
            string matchedRoot = null;
            bool allowed = false;

            if (!string.IsNullOrWhiteSpace(geminiPath))
            {
                string fullGemini = Path.GetFullPath(geminiPath).TrimEnd('\\', '/');
                if (SessionLifecycleService.IsPathContainedUnder(fullGemini, canonicalPath))
                {
                    string ideLinkDir = Path.Combine(fullGemini, "antigravity-ide");
                    if (SessionLifecycleService.IsPathContainedUnder(ideLinkDir, canonicalPath))
                    {
                        httpErrorCode = 403;
                        error = "不允许读写 antigravity-ide 安装目录文件。";
                        return false;
                    }
                    allowed = true;
                    matchedRoot = fullGemini;
                }
            }

            if (!allowed && !string.IsNullOrWhiteSpace(repoPath))
            {
                string fullRepo = Path.GetFullPath(repoPath).TrimEnd('\\', '/');
                if (SessionLifecycleService.IsPathContainedUnder(fullRepo, canonicalPath))
                {
                    allowed = true;
                    matchedRoot = fullRepo;
                }
            }

            if (!allowed && !string.IsNullOrWhiteSpace(geminiPath))
            {
                Dictionary<string, ProjectMetadata> projs = WorkspaceService.LoadLocalProjects(geminiPath);
                if (projs != null)
                {
                    foreach (KeyValuePair<string, ProjectMetadata> kvp in projs)
                    {
                        ProjectMetadata pm = kvp.Value;
                        if (pm == null) continue;
                        List<string> uris = new List<string>();
                        if (!string.IsNullOrEmpty(pm.FolderUri)) uris.Add(pm.FolderUri);
                        if (pm.FolderUris != null) uris.AddRange(pm.FolderUris);
                        for (int i = 0; i < uris.Count; i++)
                        {
                            string projDir = WorkspaceService.UriToDisplayPath(uris[i]);
                            if (string.IsNullOrWhiteSpace(projDir)) continue;
                            try
                            {
                                string fullProj = Path.GetFullPath(projDir).TrimEnd('\\', '/');
                                if (!SessionLifecycleService.IsPathContainedUnder(fullProj, canonicalPath))
                                    continue;

                                string agentsDir = Path.Combine(fullProj, ".agents");
                                string agentDir = Path.Combine(fullProj, ".agent");
                                string geminiMd = Path.Combine(fullProj, "GEMINI.md");
                                string agentsMd = Path.Combine(fullProj, "AGENTS.md");
                                string skillsJson = Path.Combine(fullProj, "skills.json");
                                string pluginsJson = Path.Combine(fullProj, "plugins.json");
                                string hooksJson = Path.Combine(fullProj, "hooks.json");

                                if (SessionLifecycleService.IsPathContainedUnder(agentsDir, canonicalPath) ||
                                    SessionLifecycleService.IsPathContainedUnder(agentDir, canonicalPath) ||
                                    string.Equals(canonicalPath, geminiMd, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(canonicalPath, agentsMd, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(canonicalPath, skillsJson, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(canonicalPath, pluginsJson, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(canonicalPath, hooksJson, StringComparison.OrdinalIgnoreCase))
                                {
                                    allowed = true;
                                    matchedRoot = fullProj;
                                    break;
                                }
                            }
                            catch { }
                        }
                        if (allowed) break;
                    }
                }
            }

            if (!allowed)
            {
                httpErrorCode = 403;
                error = "拒绝访问白名单范围之外的文件路径。";
                return false;
            }

            // Symlink / junction escape defense from matchedRoot down to canonicalPath
            if (HasReparsePointBetween(matchedRoot, canonicalPath))
            {
                httpErrorCode = 403;
                error = "安全策略拒绝通过符号链接或目录联接访问文件。";
                return false;
            }

            return true;
        }

        private static bool IsSensitiveAuthFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return true;
            if (SensitiveAuthFileNames.Contains(fileName)) return true;
            if (fileName.StartsWith(".env.", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (fileName.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (fileName.EndsWith(".key", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".pem", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".p12", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        private static bool HasReparsePointBetween(string rootDir, string targetPath)
        {
            try
            {
                if ((File.Exists(targetPath) || Directory.Exists(targetPath)) &&
                    (File.GetAttributes(targetPath) & FileAttributes.ReparsePoint) != 0)
                {
                    return true;
                }

                string normRoot = Path.GetFullPath(rootDir).TrimEnd('\\', '/');
                string cur = Path.GetDirectoryName(targetPath);
                while (!string.IsNullOrEmpty(cur) && cur.Length > normRoot.Length)
                {
                    if (Directory.Exists(cur) && (File.GetAttributes(cur) & FileAttributes.ReparsePoint) != 0)
                        return true;
                    cur = Path.GetDirectoryName(cur);
                }
            }
            catch
            {
                return true;
            }
            return false;
        }

        // ══════════════════════════════════════════════════════════════════
        // 5. SERIALIZATION FOR /api/cockpit/summary & /api/mcp/doctor
        // ══════════════════════════════════════════════════════════════════

        public static Dictionary<string, object> SerializeCockpitSummary(CockpitSnapshot snap, string geminiPath)
        {
            return SerializeCockpitSummary(snap, geminiPath, 0);
        }

        public static Dictionary<string, object> SerializeCockpitSummary(CockpitSnapshot snap, string geminiPath, int referenceTokenThreshold)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            if (snap == null) return d;

            Dictionary<string, object> us = new Dictionary<string, object>();
            us["configPath"] = snap.UserSettings.ConfigPath ?? string.Empty;
            us["exists"] = snap.UserSettings.Exists;
            us["autoExecutionPolicy"] = snap.UserSettings.AutoExecutionPolicy ?? string.Empty;
            us["artifactReviewMode"] = snap.UserSettings.ArtifactReviewMode ?? string.Empty;
            us["browserJsExecutionPolicy"] = snap.UserSettings.BrowserJsExecutionPolicy ?? string.Empty;
            us["nonWorkspaceFileAccessPolicy"] = snap.UserSettings.NonWorkspaceFileAccessPolicy ?? string.Empty;
            us["enableTerminalSandbox"] = snap.UserSettings.EnableTerminalSandbox;
            us["enableSoundsForSpecialEvents"] = snap.UserSettings.EnableSoundsForSpecialEvents;
            us["useAiCredits"] = snap.UserSettings.UseAiCredits;
            us["themeMode"] = snap.UserSettings.ThemeMode ?? string.Empty;
            us["conversationWidth"] = snap.UserSettings.ConversationWidth ?? string.Empty;
            us["raw"] = snap.UserSettings.RawSettings;
            d["userSettings"] = us;

            List<object> pluginsArr = new List<object>(snap.Plugins.Count);
            for (int i = 0; i < snap.Plugins.Count; i++)
            {
                ScannedPluginInfo p = snap.Plugins[i];
                Dictionary<string, object> pd = new Dictionary<string, object>();
                pd["id"] = p.Id;
                pd["displayName"] = p.DisplayName;
                pd["version"] = p.Version;
                pd["description"] = p.Description;
                pd["enabled"] = p.Enabled;
                pd["hasFolder"] = p.HasFolder;
                pd["inConfigJson"] = p.InConfigJson;
                pd["folderPath"] = p.FolderPath;
                pd["pluginJsonPath"] = p.PluginJsonPath;
                pd["configPath"] = p.ConfigPath;
                pd["installedFrom"] = p.InstalledFrom;
                pd["manifestStatus"] = p.ManifestStatus;
                pd["rulesCount"] = p.Rules.Count;
                pd["skillsCount"] = p.Skills.Count;
                pd["mcpCount"] = p.McpServers.Count;
                pd["hooksCount"] = p.Hooks.Count;
                pluginsArr.Add(pd);
            }
            d["plugins"] = pluginsArr;

            List<object> sidecarsArr = new List<object>(snap.Sidecars.Count);
            for (int i = 0; i < snap.Sidecars.Count; i++)
            {
                ScannedSidecarInfo sc = snap.Sidecars[i];
                Dictionary<string, object> sd = new Dictionary<string, object>();
                sd["id"] = sc.Id;
                sd["source"] = sc.Source;
                sd["projectId"] = sc.ProjectId;
                sd["schemaVersion"] = sc.SchemaVersion;
                sd["sourcePath"] = sc.SourcePath;
                sd["raw"] = sc.RawData;
                sidecarsArr.Add(sd);
            }
            d["sidecars"] = sidecarsArr;

            List<object> skillsArr = new List<object>(snap.Skills.Count);
            for (int i = 0; i < snap.Skills.Count; i++)
            {
                ScannedSkillInfo sk = snap.Skills[i];
                Dictionary<string, object> skd = new Dictionary<string, object>();
                skd["id"] = sk.Id;
                skd["name"] = sk.Name;
                skd["description"] = sk.Description;
                skd["scope"] = sk.Scope;
                skd["pluginId"] = sk.PluginId;
                skd["sourcePath"] = sk.SourcePath;
                skd["enabled"] = sk.Enabled;
                skd["manifestStatus"] = sk.ManifestStatus;
                skillsArr.Add(skd);
            }
            d["skills"] = skillsArr;

            List<object> rulesArr = new List<object>(snap.Rules.Count);
            for (int i = 0; i < snap.Rules.Count; i++)
            {
                rulesArr.Add(SerializeRule(snap.Rules[i]));
            }
            d["rules"] = rulesArr;

            List<object> mcpArr = new List<object>(snap.McpServers.Count);
            for (int i = 0; i < snap.McpServers.Count; i++)
            {
                ScannedMcpServerInfo m = snap.McpServers[i];
                Dictionary<string, object> md = new Dictionary<string, object>();
                md["id"] = m.Id;
                md["scope"] = m.Scope;
                md["pluginId"] = m.PluginId;
                md["sourcePath"] = m.SourcePath;
                md["transport"] = m.Transport;
                md["type"] = m.Type;
                md["command"] = m.Command;
                md["args"] = new List<object>(OverviewBuilder.ToObjList(m.Args));
                md["env"] = m.Env;
                md["url"] = m.Url;
                md["disabled"] = m.Disabled;
                md["enabled"] = m.Enabled;
                md["duplicate"] = m.Duplicate;
                md["duplicateSources"] = new List<object>(OverviewBuilder.ToObjList(m.DuplicateSources));
                mcpArr.Add(md);
            }
            d["mcpServers"] = mcpArr;

            List<object> confArr = new List<object>(snap.McpConflicts.Count);
            for (int i = 0; i < snap.McpConflicts.Count; i++)
            {
                Dictionary<string, object> cd = new Dictionary<string, object>();
                cd["id"] = snap.McpConflicts[i].Id;
                cd["count"] = snap.McpConflicts[i].Count;
                cd["sources"] = new List<object>(OverviewBuilder.ToObjList(snap.McpConflicts[i].Sources));
                confArr.Add(cd);
            }
            d["mcpConflicts"] = confArr;

            List<object> hooksArr = new List<object>(snap.Hooks.Count);
            for (int i = 0; i < snap.Hooks.Count; i++)
            {
                ScannedHookInfo h = snap.Hooks[i];
                Dictionary<string, object> hd = new Dictionary<string, object>();
                hd["id"] = h.Id;
                hd["scope"] = h.Scope;
                hd["pluginId"] = h.PluginId;
                hd["sourcePath"] = h.SourcePath;
                hd["enabled"] = h.Enabled;
                List<object> evArr = new List<object>(h.Events.Count);
                for (int e = 0; e < h.Events.Count; e++)
                {
                    ScannedHookEventItem ev = h.Events[e];
                    Dictionary<string, object> evd = new Dictionary<string, object>();
                    evd["eventName"] = ev.EventName;
                    evd["matcher"] = ev.Matcher;
                    evd["type"] = ev.Type;
                    evd["command"] = ev.Command;
                    evd["timeout"] = ev.Timeout;
                    evArr.Add(evd);
                }
                hd["events"] = evArr;
                hooksArr.Add(hd);
            }
            d["hooks"] = hooksArr;

            List<object> manArr = new List<object>(snap.Manifests.Count);
            for (int i = 0; i < snap.Manifests.Count; i++)
            {
                ManifestControlInfo mf = snap.Manifests[i];
                Dictionary<string, object> mfd = new Dictionary<string, object>();
                mfd["kind"] = mf.Kind;
                mfd["scope"] = mf.Scope;
                mfd["sourcePath"] = mf.SourcePath;
                mfd["inherits"] = mf.Inherits;
                mfd["entries"] = mf.Entries;
                mfd["include_only"] = new List<object>(OverviewBuilder.ToObjList(mf.IncludeOnly));
                mfd["exclude"] = new List<object>(OverviewBuilder.ToObjList(mf.Exclude));
                manArr.Add(mfd);
            }
            d["manifests"] = manArr;

            List<object> wsArr = new List<object>(snap.Workspaces.Count);
            for (int i = 0; i < snap.Workspaces.Count; i++)
            {
                ScannedWorkspaceInfo ws = snap.Workspaces[i];
                Dictionary<string, object> wsd = new Dictionary<string, object>();
                wsd["projectId"] = ws.ProjectId;
                wsd["projectName"] = ws.ProjectName;
                wsd["folderPath"] = ws.FolderPath;
                wsd["exists"] = ws.Exists;
                wsd["hasAgentsDir"] = ws.HasAgentsDir;
                wsd["agentsDirName"] = ws.AgentsDirName;
                wsd["hasGeminiMd"] = ws.HasGeminiMd;
                wsd["hasAgentsMd"] = ws.HasAgentsMd;
                wsd["hasSkillsJson"] = ws.HasSkillsJson;
                wsd["skillsJsonPath"] = ws.SkillsJsonPath;
                wsd["hasPluginsJson"] = ws.HasPluginsJson;
                wsd["pluginsJsonPath"] = ws.PluginsJsonPath;
                wsd["hasHooksJson"] = ws.HasHooksJson;
                wsd["hooksJsonPath"] = ws.HooksJsonPath;
                wsd["rulesCount"] = ws.Rules.Count;
                wsd["skillsCount"] = ws.Skills.Count;
                wsd["hooksCount"] = ws.Hooks.Count;
                wsd["mcpCount"] = ws.McpServers.Count;
                wsArr.Add(wsd);
            }
            d["workspaces"] = wsArr;

            d["cliSettings"] = snap.CliSettings;

            List<object> overArr = new List<object>(snap.OverBudgetFiles.Count);
            for (int i = 0; i < snap.OverBudgetFiles.Count; i++)
            {
                overArr.Add(SerializeRule(snap.OverBudgetFiles[i]));
            }

            Dictionary<string, object> tb = new Dictionary<string, object>();
            tb["threshold"] = referenceTokenThreshold > 0 ? referenceTokenThreshold : 0;
            tb["globalAlwaysOnTokens"] = snap.GlobalAlwaysOnTokens;
            tb["enabledPluginRulesTokens"] = snap.EnabledPluginRulesTokens;
            tb["projectRulesTokens"] = snap.ProjectRulesTokens;
            tb["totalAlwaysOnTokens"] = snap.TotalAlwaysOnTokens;
            tb["overBudgetCount"] = snap.OverBudgetCount;
            tb["overBudgetFiles"] = overArr;
            d["tokenBudget"] = tb;

            d["tools"] = WorkspaceLauncherService.ResolveLaunchToolsDict(geminiPath);
            return d;
        }

        private static Dictionary<string, object> SerializeRule(ScannedRuleInfo r)
        {
            Dictionary<string, object> rd = new Dictionary<string, object>();
            rd["id"] = r.Id;
            rd["scope"] = r.Scope;
            rd["pluginId"] = r.PluginId;
            rd["sourcePath"] = r.SourcePath;
            rd["charCount"] = r.CharCount;
            rd["lineCount"] = r.LineCount;
            rd["byteCount"] = r.ByteCount;
            rd["estimatedTokens"] = r.EstimatedTokens;
            rd["overBudget"] = r.OverBudget;
            rd["alwaysOn"] = r.AlwaysOn;
            rd["enabled"] = r.Enabled;
            return rd;
        }

        public static Dictionary<string, object> SerializeMcpHealthResults(List<McpHealthCheckResult> results)
        {
            List<object> list = new List<object>(results != null ? results.Count : 0);
            int healthy = 0;
            if (results != null)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    McpHealthCheckResult r = results[i];
                    if (r.Healthy) healthy++;
                    Dictionary<string, object> d = new Dictionary<string, object>();
                    d["id"] = r.Id;
                    d["scope"] = r.Scope;
                    d["sourcePath"] = r.SourcePath;
                    d["transport"] = r.Transport;
                    d["disabled"] = r.Disabled;
                    d["healthy"] = r.Healthy;
                    d["reachable"] = r.Reachable;
                    d["status"] = r.Status;
                    d["latencyMs"] = r.LatencyMs;
                    d["httpStatusCode"] = r.HttpStatusCode;
                    d["serverName"] = r.ServerName;
                    d["serverVersion"] = r.ServerVersion;
                    d["protocolVersion"] = r.ProtocolVersion;
                    d["stderr"] = r.Stderr;
                    d["error"] = r.Error;
                    list.Add(d);
                }
            }
            Dictionary<string, object> resp = new Dictionary<string, object>();
            resp["ok"] = true;
            resp["checkedCount"] = list.Count;
            resp["healthyCount"] = healthy;
            resp["results"] = list;
            return resp;
        }

        // ══════════════════════════════════════════════════════════════════
        // 6. INTERNAL HELPERS
        // ══════════════════════════════════════════════════════════════════

        private static Dictionary<string, object> TryReadJsonDict(string path)
        {
            try
            {
                string text = ReadTextSharedSafe(path);
                return TryParseDict(text);
            }
            catch
            {
                return null;
            }
        }

        private static Dictionary<string, object> TryParseDict(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText)) return null;
            object parsed;
            if (!UnifiedJson.TryParse(jsonText, out parsed)) return null;
            return UnifiedJson.AsDict(parsed);
        }

        private static string ReadTextSharedSafe(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        private static bool TryGetDictValueIgnoreCase(Dictionary<string, object> dict, string key, out object value)
        {
            string actualKey;
            return TryGetDictKeyAndValueIgnoreCase(dict, key, out actualKey, out value);
        }

        private static bool TryGetDictKeyAndValueIgnoreCase(Dictionary<string, object> dict, string key, out string actualKey, out object value)
        {
            actualKey = null;
            value = null;
            if (dict == null || string.IsNullOrEmpty(key)) return false;
            if (dict.TryGetValue(key, out value))
            {
                actualKey = key;
                return true;
            }
            foreach (KeyValuePair<string, object> kvp in dict)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    actualKey = kvp.Key;
                    value = kvp.Value;
                    return true;
                }
            }
            return false;
        }

        private static List<string> ReadStringListFromDict(Dictionary<string, object> dict, string key)
        {
            List<string> list = new List<string>();
            object obj;
            if (!TryGetDictValueIgnoreCase(dict, key, out obj)) return list;
            List<object> arr = UnifiedJson.AsList(obj);
            if (arr == null) return list;
            for (int i = 0; i < arr.Count; i++)
            {
                string s = arr[i] as string;
                if (s != null) list.Add(s);
            }
            return list;
        }

        private static List<string> CollectSafeMarkdownFiles(string rootDir)
        {
            List<string> results = new List<string>();
            if (string.IsNullOrEmpty(rootDir) || !Directory.Exists(rootDir)) return results;
            CollectMarkdownRecursive(rootDir, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase), results);
            results.Sort(StringComparer.OrdinalIgnoreCase);
            return results;
        }

        private static void CollectMarkdownRecursive(string currentDir, int depth, HashSet<string> visited, List<string> results)
        {
            if (depth > 16 || string.IsNullOrEmpty(currentDir) || !Directory.Exists(currentDir)) return;
            try
            {
                string full = Path.GetFullPath(currentDir);
                if (!visited.Add(full)) return;
                string[] files = Directory.GetFiles(currentDir, "*.md", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < files.Length; i++) results.Add(files[i]);
                string[] subDirs = Directory.GetDirectories(currentDir);
                for (int i = 0; i < subDirs.Length; i++)
                {
                    string name = Path.GetFileName(subDirs[i]);
                    if (string.IsNullOrEmpty(name) || CapabilityVaultService.IsExcludedSubdirName(name)) continue;
                    CollectMarkdownRecursive(subDirs[i], depth + 1, visited, results);
                }
            }
            catch { }
        }

        private static void ExtractSkillFrontmatter(string text, out string name, out string desc)
        {
            name = string.Empty;
            desc = string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return;

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            if (lines.Length > 0 && lines[0].Trim() == "---")
            {
                for (int i = 1; i < lines.Length && i < 30; i++)
                {
                    string line = lines[i].Trim();
                    if (line == "---") break;
                    if (line.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                    {
                        name = line.Substring(5).Trim().Trim('"', '\'');
                    }
                    else if (line.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
                    {
                        desc = line.Substring(12).Trim().Trim('"', '\'');
                    }
                }
            }

            if (string.IsNullOrEmpty(desc))
            {
                for (int i = 0; i < lines.Length && i < 20; i++)
                {
                    string t = lines[i].Trim();
                    if (string.IsNullOrEmpty(t) || t == "---" || t.StartsWith("#", StringComparison.Ordinal)) continue;
                    desc = t.Length > 140 ? t.Substring(0, 140) + "..." : t;
                    break;
                }
            }
        }
    }
}
