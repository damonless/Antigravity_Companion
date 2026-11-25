using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // ARCHIVE TRANSFER SERVICE (Selective Import & Full Backup)
    // Hardened for P0-3, P0-4, P0-6, P0-7, P1-8. Strictly C# 5.0 (.NET Framework 4.x)
    // ══════════════════════════════════════════════════════════════════════

    public enum BackupTier
    {
        FastDaily,
        FullForensic
    }

    public sealed class ResolvedImportProjectInfo
    {
        public string TargetId;
        public string BackupId;
        public string ProjectName;
        public string FolderUri;
    }

    public sealed class SelectiveImportRequest
    {
        public string GeminiPath;
        public string HomePath;
        public RightPaneSourceMode SourceMode;
        public string LoadedBackupPath;
        public BackupArchiveIndex BackupIndex;
        public Dictionary<string, ProjectMetadata> LocalProjects;
        public List<ProjectDiffNode> CurrentDiffNodes;
        public List<CapabilityItem> SelectedCapabilities;
        public List<ConversationSummaryInfo> SelectedConversations;
        public Dictionary<string, ProjectDiffNode> ConvProjDiffMap;
        public bool IncludeUserSettings;
        public bool RestoreUserSettings;
        public bool RestoreLegacyCliArchive;
    }

    public sealed class SelectiveImportResult
    {
        public int FilesImported;
        public int CapabilitiesImported;
        public int ConversationsImported;
        public List<string> ImportedCapKeys = new List<string>();
        public List<string> ImportedConvIds = new List<string>();
        public List<string> ImportedProjNames = new List<string>();
        public List<string> Warnings = new List<string>();
        public string ErrorMessage;
    }

    public static class ArchiveTransferService
    {
        public static OperationResult<SelectiveImportResult> PerformSelectiveImportResult(
            SelectiveImportRequest req,
            Action<string, int> onProgress)
        {
            SelectiveImportResult res = PerformSelectiveImport(req, onProgress);
            if (!string.IsNullOrEmpty(res.ErrorMessage))
                return OperationResult<SelectiveImportResult>.Fail(res.ErrorMessage, res.Warnings);
            return OperationResult<SelectiveImportResult>.Success(res, res.Warnings);
        }

        public static SelectiveImportResult PerformSelectiveImport(
            SelectiveImportRequest req,
            Action<string, int> onProgress)
        {
            SelectiveImportResult workRes = new SelectiveImportResult();
            if (req == null || string.IsNullOrEmpty(req.GeminiPath))
            {
                workRes.ErrorMessage = "无效的导入请求参数。";
                return workRes;
            }

            List<CapabilityItem> selectedCaps = req.SelectedCapabilities ?? new List<CapabilityItem>();
            List<ConversationSummaryInfo> initialSelectedConvs = req.SelectedConversations ?? new List<ConversationSummaryInfo>();
            bool isColdArchive = req.BackupIndex != null && req.BackupIndex.IsColdArchive;
            if (selectedCaps.Count == 0 && initialSelectedConvs.Count == 0 && !req.IncludeUserSettings && !req.RestoreUserSettings && !req.RestoreLegacyCliArchive)
            {
                workRes.ErrorMessage = "未选择任何能力配置或会话。";
                return workRes;
            }

            if (isColdArchive &&
                req.SourceMode == RightPaneSourceMode.BackupArchive &&
                !string.IsNullOrEmpty(req.LoadedBackupPath) &&
                File.Exists(req.LoadedBackupPath))
            {
                return PerformColdArchiveRestore(req, initialSelectedConvs, onProgress);
            }

            string geminiSnap = req.GeminiPath;
            string curHomeSnap = req.HomePath ?? WorkspaceService.ResolveDefaultHomePath();
            string srcHomeSnap = (req.BackupIndex != null && !string.IsNullOrEmpty(req.BackupIndex.SourceHome)) ? req.BackupIndex.SourceHome : string.Empty;
            Dictionary<string, ProjectMetadata> localProjectsSnap = req.LocalProjects != null
                ? new Dictionary<string, ProjectMetadata>(req.LocalProjects, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ProjectMetadata> backupProjectsSnap = (req.BackupIndex != null && req.BackupIndex.Projects != null)
                ? new Dictionary<string, ProjectMetadata>(req.BackupIndex.Projects, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
            List<ProjectDiffNode> diffNodesSnap = req.CurrentDiffNodes ?? new List<ProjectDiffNode>();
            Dictionary<string, ProjectDiffNode> convProjDiffMap = req.ConvProjDiffMap ?? new Dictionary<string, ProjectDiffNode>(StringComparer.OrdinalIgnoreCase);

            // FIX P0-7: Expand dependent child subagent closures for any selected parent session CID
            List<ConversationSummaryInfo> effectiveSelectedConvs = new List<ConversationSummaryInfo>();
            HashSet<string> convIdsToExtract = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ConversationSummaryInfo> convMap = new Dictionary<string, ConversationSummaryInfo>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < initialSelectedConvs.Count; i++)
            {
                ConversationSummaryInfo sc = initialSelectedConvs[i];
                if (sc == null || !SessionMetadataExtractor.IsLikelyConversationId(sc.ConversationId)) continue;
                string cidTrim = sc.ConversationId.Trim();
                if (convIdsToExtract.Add(cidTrim))
                {
                    effectiveSelectedConvs.Add(sc);
                    convMap[cidTrim] = sc;
                }
            }

            if (req.BackupIndex != null && req.BackupIndex.Conversations != null && convIdsToExtract.Count > 0)
            {
                bool addedChild = true;
                while (addedChild)
                {
                    addedChild = false;
                    for (int i = 0; i < req.BackupIndex.Conversations.Count; i++)
                    {
                        ConversationSummaryInfo cand = req.BackupIndex.Conversations[i];
                        if (cand == null || !SessionMetadataExtractor.IsLikelyConversationId(cand.ConversationId)) continue;
                        string cidTrim = cand.ConversationId.Trim();
                        string parentTrim = cand.ParentConversationId != null ? cand.ParentConversationId.Trim() : null;
                        if (!convIdsToExtract.Contains(cidTrim) &&
                            !string.IsNullOrEmpty(parentTrim) &&
                            convIdsToExtract.Contains(parentTrim))
                        {
                            convIdsToExtract.Add(cidTrim);
                            effectiveSelectedConvs.Add(cand);
                            convMap[cidTrim] = cand;
                            addedChild = true;
                        }
                    }
                }
            }

            Dictionary<string, string> projectUriOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> projectNewIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> seenNameNewId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> ambiguousNewIdNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> seenNameUri = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> ambiguousUriNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> generatedProjIdsByUriOrName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ResolvedImportProjectInfo> neededProjectsByTargetId = new Dictionary<string, ResolvedImportProjectInfo>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> convResolvedProjId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> convResolvedProjName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int c = 0; c < selectedCaps.Count; c++)
            {
                if (selectedCaps[c] != null && !string.IsNullOrEmpty(selectedCaps[c].UniqueKey))
                    workRes.ImportedCapKeys.Add(selectedCaps[c].UniqueKey);
            }

            HashSet<string> initProjSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < effectiveSelectedConvs.Count; i++)
            {
                ConversationSummaryInfo sc = effectiveSelectedConvs[i];
                if (sc == null || string.IsNullOrEmpty(sc.ConversationId)) continue;
                string cidTrim = sc.ConversationId.Trim();
                workRes.ImportedConvIds.Add(cidTrim);

                ProjectDiffNode pDiff;
                if (!convProjDiffMap.TryGetValue(cidTrim, out pDiff) || pDiff == null)
                    pDiff = FindProjectDiffForBackupConv(diffNodesSnap, cidTrim);

                bool isOutside = VisualDiffEngine.IsOutsideProject(sc.ProjectName) ||
                                 string.Equals(sc.ProjectId, "outside-of-project", StringComparison.OrdinalIgnoreCase);
                if (isOutside)
                {
                    convResolvedProjId[cidTrim] = "outside-of-project";
                    convResolvedProjName[cidTrim] = "Outside of Project";
                    if (initProjSeen.Add("Outside of Project")) workRes.ImportedProjNames.Add("Outside of Project");
                    continue;
                }

                string localProjId = null;
                string localProjName = null;
                string localFolderUri = pDiff != null ? pDiff.LocalFolderUri : null;

                if (pDiff != null && pDiff.LocalConversations != null)
                {
                    for (int li = 0; li < pDiff.LocalConversations.Count; li++)
                    {
                        ConversationSummaryInfo lc = pDiff.LocalConversations[li];
                        if (lc != null && !string.IsNullOrEmpty(lc.ProjectId) && localProjectsSnap.ContainsKey(lc.ProjectId))
                        {
                            localProjId = lc.ProjectId;
                            localProjName = localProjectsSnap[lc.ProjectId].Name;
                            if (string.IsNullOrEmpty(localFolderUri))
                                localFolderUri = localProjectsSnap[lc.ProjectId].FolderUri;
                            break;
                        }
                    }
                }

                string remappedBackupUri = !string.IsNullOrEmpty(sc.FolderUri)
                    ? DirectStreamArchiver.RemapAllPathFormats(sc.FolderUri, srcHomeSnap, curHomeSnap)
                    : null;

                if (string.IsNullOrEmpty(localProjId))
                {
                    string lookupUri = !string.IsNullOrEmpty(localFolderUri) ? localFolderUri : remappedBackupUri;
                    if (!string.IsNullOrEmpty(lookupUri))
                    {
                        foreach (KeyValuePair<string, ProjectMetadata> kvp in localProjectsSnap)
                        {
                            if (DoesProjectMetadataMatchUri(kvp.Value, lookupUri))
                            {
                                localProjId = kvp.Key;
                                localProjName = kvp.Value.Name;
                                localFolderUri = kvp.Value.FolderUri;
                                break;
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(localProjId) && !string.IsNullOrEmpty(sc.ProjectName) && !SmartProjectFilter.IsJunkOrEphemeral(sc.ProjectName))
                {
                    foreach (KeyValuePair<string, ProjectMetadata> kvp in localProjectsSnap)
                    {
                        if (!string.Equals(kvp.Value.Name, sc.ProjectName, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!string.IsNullOrEmpty(remappedBackupUri) && !string.IsNullOrEmpty(kvp.Value.FolderUri) &&
                            !DoesProjectMetadataMatchUri(kvp.Value, remappedBackupUri))
                            continue;

                        localProjId = kvp.Key;
                        localProjName = kvp.Value.Name;
                        if (string.IsNullOrEmpty(localFolderUri)) localFolderUri = kvp.Value.FolderUri;
                        break;
                    }
                }

                string backupProjId = !string.IsNullOrEmpty(sc.ProjectId) ? sc.ProjectId : null;
                if (string.IsNullOrEmpty(backupProjId))
                {
                    if (!string.IsNullOrEmpty(sc.FolderUri))
                    {
                        foreach (KeyValuePair<string, ProjectMetadata> bKvp in backupProjectsSnap)
                        {
                            if (DoesProjectMetadataMatchUri(bKvp.Value, sc.FolderUri))
                            {
                                backupProjId = bKvp.Key;
                                break;
                            }
                        }
                    }
                    if (string.IsNullOrEmpty(backupProjId) && !string.IsNullOrEmpty(sc.ProjectName))
                    {
                        foreach (KeyValuePair<string, ProjectMetadata> bKvp in backupProjectsSnap)
                        {
                            if (!string.Equals(bKvp.Value.Name, sc.ProjectName, StringComparison.OrdinalIgnoreCase)) continue;
                            if (!string.IsNullOrEmpty(sc.FolderUri) && !string.IsNullOrEmpty(bKvp.Value.FolderUri) &&
                                !DoesProjectMetadataMatchUri(bKvp.Value, sc.FolderUri))
                                continue;
                            backupProjId = bKvp.Key;
                            break;
                        }
                    }
                }

                string effectiveProjName = !string.IsNullOrEmpty(localProjName) ? localProjName : sc.ProjectName;
                string effectiveUri = localFolderUri;
                if (string.IsNullOrEmpty(effectiveUri) && !string.IsNullOrEmpty(remappedBackupUri))
                    effectiveUri = remappedBackupUri;
                if (string.IsNullOrEmpty(effectiveUri) && !string.IsNullOrEmpty(effectiveProjName))
                    effectiveUri = SessionLifecycleService.ToFileUri(Path.Combine(curHomeSnap, effectiveProjName));
                if (!string.IsNullOrEmpty(effectiveUri))
                    effectiveUri = SessionLifecycleService.ToFileUri(effectiveUri);

                string effectiveProjId = localProjId;
                if (string.IsNullOrEmpty(effectiveProjId))
                {
                    if (!string.IsNullOrEmpty(backupProjId) && !localProjectsSnap.ContainsKey(backupProjId))
                    {
                        effectiveProjId = backupProjId;
                    }
                    else
                    {
                        string genKey = !string.IsNullOrEmpty(effectiveUri)
                            ? ("URI:" + WorkspaceService.UriToDisplayPath(effectiveUri).TrimEnd('\\', '/'))
                            : ("NAME:" + (effectiveProjName ?? cidTrim));
                        if (!generatedProjIdsByUriOrName.TryGetValue(genKey, out effectiveProjId))
                        {
                            effectiveProjId = Guid.NewGuid().ToString();
                            generatedProjIdsByUriOrName[genKey] = effectiveProjId;
                        }
                    }
                }

                projectNewIds["CID:" + cidTrim] = effectiveProjId;
                if (!string.IsNullOrEmpty(sc.ProjectId) &&
                    !string.Equals(sc.ProjectId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
                {
                    projectNewIds["ID:" + sc.ProjectId] = effectiveProjId;
                    SetUnambiguousMapping(projectNewIds, seenNameNewId, ambiguousNewIdNames, sc.ProjectId, effectiveProjId);
                }
                SetUnambiguousMapping(projectNewIds, seenNameNewId, ambiguousNewIdNames, sc.ProjectName, effectiveProjId);
                SetUnambiguousMapping(projectNewIds, seenNameNewId, ambiguousNewIdNames, effectiveProjName, effectiveProjId);

                if (!string.IsNullOrEmpty(effectiveUri))
                {
                    projectUriOverrides["CID:" + cidTrim] = effectiveUri;
                    if (!string.IsNullOrEmpty(sc.ProjectId) &&
                        !string.Equals(sc.ProjectId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
                    {
                        projectUriOverrides["ID:" + sc.ProjectId] = effectiveUri;
                        SetUnambiguousMapping(projectUriOverrides, seenNameUri, ambiguousUriNames, sc.ProjectId, effectiveUri);
                    }
                    if (!string.IsNullOrEmpty(effectiveProjId) &&
                        !string.Equals(effectiveProjId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
                    {
                        projectUriOverrides["ID:" + effectiveProjId] = effectiveUri;
                        SetUnambiguousMapping(projectUriOverrides, seenNameUri, ambiguousUriNames, effectiveProjId, effectiveUri);
                    }
                    SetUnambiguousMapping(projectUriOverrides, seenNameUri, ambiguousUriNames, sc.ProjectName, effectiveUri);
                    SetUnambiguousMapping(projectUriOverrides, seenNameUri, ambiguousUriNames, effectiveProjName, effectiveUri);
                }

                convResolvedProjId[cidTrim] = effectiveProjId;
                convResolvedProjName[cidTrim] = effectiveProjName;
                if (!string.IsNullOrEmpty(effectiveProjName) && initProjSeen.Add(effectiveProjName))
                    workRes.ImportedProjNames.Add(effectiveProjName);

                if (!string.IsNullOrEmpty(effectiveProjName) &&
                    !SmartProjectFilter.IsJunkOrEphemeral(effectiveProjName) &&
                    !neededProjectsByTargetId.ContainsKey(effectiveProjId))
                {
                    neededProjectsByTargetId[effectiveProjId] = new ResolvedImportProjectInfo
                    {
                        TargetId = effectiveProjId,
                        BackupId = backupProjId,
                        ProjectName = effectiveProjName,
                        FolderUri = effectiveUri
                    };
                }
            }

            if (selectedCaps.Count > 0)
            {
                if (onProgress != null) onProgress("正在按键值级无损合流所选能力配置 (Skills / Rules / MCP / Plugins / Hooks)...", 20);
                workRes.FilesImported += CapabilityVaultService.ImportCapabilitiesToLocal(geminiSnap, curHomeSnap, selectedCaps);
                workRes.CapabilitiesImported = selectedCaps.Count;
            }

            bool needArchiveRead = (effectiveSelectedConvs.Count > 0 || req.IncludeUserSettings || req.RestoreUserSettings) &&
                                   req.SourceMode == RightPaneSourceMode.BackupArchive &&
                                   !string.IsNullOrEmpty(req.LoadedBackupPath) &&
                                   File.Exists(req.LoadedBackupPath);

            if (needArchiveRead)
            {
                string ideConvDir = Path.Combine(geminiSnap, "antigravity", "conversations");
                string ideBrainDir = Path.Combine(geminiSnap, "antigravity", "brain");
                string ideAnnotationsDir = Path.Combine(geminiSnap, "antigravity", "annotations");
                string cliConvDir = Path.Combine(geminiSnap, "antigravity-cli", "conversations");
                string cliBrainDir = Path.Combine(geminiSnap, "antigravity-cli", "brain");
                string cliAnnotationsDir = Path.Combine(geminiSnap, "antigravity-cli", "annotations");
                string projDir = Path.Combine(geminiSnap, "config", "projects");

                if (effectiveSelectedConvs.Count > 0)
                {
                    if (!Directory.Exists(ideConvDir)) Directory.CreateDirectory(ideConvDir);
                    if (!Directory.Exists(ideBrainDir)) Directory.CreateDirectory(ideBrainDir);
                    if (!Directory.Exists(ideAnnotationsDir)) Directory.CreateDirectory(ideAnnotationsDir);
                    if (!Directory.Exists(projDir)) Directory.CreateDirectory(projDir);
                }

                HashSet<string> cleanedConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> extractedAnnotationCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, string> extractedDbPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                Dictionary<string, ResolvedImportProjectInfo> byBackupProjId = new Dictionary<string, ResolvedImportProjectInfo>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, ResolvedImportProjectInfo> byProjName = new Dictionary<string, ResolvedImportProjectInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, ResolvedImportProjectInfo> kvp in neededProjectsByTargetId)
                {
                    ResolvedImportProjectInfo pInfo = kvp.Value;
                    if (!string.IsNullOrEmpty(pInfo.BackupId) && !byBackupProjId.ContainsKey(pInfo.BackupId))
                        byBackupProjId[pInfo.BackupId] = pInfo;
                    if (!string.IsNullOrEmpty(pInfo.ProjectName) &&
                        !ambiguousUriNames.Contains(pInfo.ProjectName) &&
                        !byProjName.ContainsKey(pInfo.ProjectName))
                        byProjName[pInfo.ProjectName] = pInfo;
                }

                using (FileStream fs = new FileStream(req.LoadedBackupPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    if (onProgress != null) onProgress("正在安全提取工程定义、对话数据库、标注与脑区文件...", 45);
                    for (int i = 0; i < archive.Entries.Count; i++)
                    {
                        ZipArchiveEntry entry = archive.Entries[i];
                        if (!StreamingZipIndexReader.IsSafeZipEntryPath(entry.FullName)) continue;

                        string rawNorm = (entry.FullName ?? string.Empty).Replace('\\', '/').TrimStart('/');
                        if (string.IsNullOrEmpty(rawNorm) || rawNorm.EndsWith("/")) continue;
                        string norm = rawNorm.StartsWith("data/", StringComparison.OrdinalIgnoreCase) ? rawNorm.Substring(5) : rawNorm;

                        // FIX P0-3: Strictly exclude antigravity/bin/ and antigravity-cli/bin/
                        if (norm.StartsWith("antigravity/bin/", StringComparison.OrdinalIgnoreCase) ||
                            norm.StartsWith("antigravity-cli/bin/", StringComparison.OrdinalIgnoreCase) ||
                            norm.StartsWith("cli/bin/", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (req.IncludeUserSettings &&
                            string.Equals(norm, "settings.json", StringComparison.OrdinalIgnoreCase))
                        {
                            string destSettings = Path.Combine(geminiSnap, "settings.json");
                            if (File.Exists(destSettings))
                                UnifiedJson.CreateTimestampedBackup(destSettings, 5);
                            ExtractZipEntrySafe(entry, destSettings);
                            workRes.FilesImported++;
                            continue;
                        }

                        if (req.IncludeUserSettings &&
                            string.Equals(norm, "antigravity-cli/settings.json", StringComparison.OrdinalIgnoreCase))
                        {
                            string destCliSettings = Path.Combine(geminiSnap, "antigravity-cli", "settings.json");
                            if (File.Exists(destCliSettings))
                                UnifiedJson.CreateTimestampedBackup(destCliSettings, 5);
                            ExtractZipEntrySafe(entry, destCliSettings);
                            workRes.FilesImported++;
                            continue;
                        }

                        if (req.RestoreUserSettings &&
                            string.Equals(norm, "config/config.json", StringComparison.OrdinalIgnoreCase))
                        {
                            string archiveCfgText;
                            using (StreamReader cfgSr = new StreamReader(entry.Open(), Encoding.UTF8))
                            {
                                archiveCfgText = cfgSr.ReadToEnd();
                            }
                            string mergeWarn;
                            if (MergeUserSettingsAndPluginFlagsFromConfigJson(geminiSnap, archiveCfgText, out mergeWarn))
                            {
                                workRes.FilesImported++;
                            }
                            if (!string.IsNullOrEmpty(mergeWarn))
                            {
                                workRes.Warnings.Add(mergeWarn);
                            }
                            continue;
                        }

                        if (effectiveSelectedConvs.Count == 0)
                            continue;

                        string convPrefix = null;
                        string targetConvDir = null;
                        string convKeyPrefix = null;
                        if (norm.StartsWith("antigravity/conversations/", StringComparison.OrdinalIgnoreCase))
                        {
                            convPrefix = "antigravity/conversations/";
                            targetConvDir = ideConvDir;
                            convKeyPrefix = "IDE:";
                        }
                        else if (norm.StartsWith("antigravity-cli/conversations/", StringComparison.OrdinalIgnoreCase))
                        {
                            convPrefix = "antigravity-cli/conversations/";
                            targetConvDir = cliConvDir;
                            convKeyPrefix = "CLI:";
                        }
                        else if (norm.StartsWith("cli/conversations/", StringComparison.OrdinalIgnoreCase))
                        {
                            convPrefix = "cli/conversations/";
                            targetConvDir = cliConvDir;
                            convKeyPrefix = "CLI:";
                        }

                        if (convPrefix != null)
                        {
                            string fn = norm.Substring(convPrefix.Length);
                            if (string.IsNullOrEmpty(fn) || fn.IndexOf('/') >= 0) continue;
                            if (fn.EndsWith(".db-shm", StringComparison.OrdinalIgnoreCase)) continue;

                            string cid = null;
                            if (fn.EndsWith(".db-wal", StringComparison.OrdinalIgnoreCase)) cid = fn.Substring(0, fn.Length - 7);
                            else if (fn.EndsWith(".db", StringComparison.OrdinalIgnoreCase)) cid = fn.Substring(0, fn.Length - 3);
                            else continue;

                            if (!SessionMetadataExtractor.IsLikelyConversationId(cid) || !convIdsToExtract.Contains(cid))
                                continue;

                            if (!Directory.Exists(targetConvDir)) Directory.CreateDirectory(targetConvDir);

                            string cleanKey = convKeyPrefix + cid;
                            if (cleanedConvIds.Add(cleanKey))
                            {
                                string existingDbPath = Path.Combine(targetConvDir, cid + ".db");
                                if (File.Exists(existingDbPath))
                                {
                                    UnifiedJson.CreateTimestampedBackup(existingDbPath, 5);
                                }
                                DeleteFileIfExistsQuiet(existingDbPath);
                                DeleteFileIfExistsQuiet(Path.Combine(targetConvDir, cid + ".db-wal"));
                                DeleteFileIfExistsQuiet(Path.Combine(targetConvDir, cid + ".db-shm"));
                            }

                            string dest = CapabilityVaultService.CombineAndEnsureUnderRoot(targetConvDir, fn);
                            ExtractZipEntrySafe(entry, dest);
                            workRes.FilesImported++;

                            if (fn.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                                extractedDbPaths[cleanKey] = dest;
                            continue;
                        }

                        string annotPrefix = null;
                        string targetAnnotDir = null;
                        if (norm.StartsWith("antigravity/annotations/", StringComparison.OrdinalIgnoreCase))
                        {
                            annotPrefix = "antigravity/annotations/";
                            targetAnnotDir = ideAnnotationsDir;
                        }
                        else if (norm.StartsWith("antigravity-cli/annotations/", StringComparison.OrdinalIgnoreCase))
                        {
                            annotPrefix = "antigravity-cli/annotations/";
                            targetAnnotDir = cliAnnotationsDir;
                        }
                        else if (norm.StartsWith("cli/annotations/", StringComparison.OrdinalIgnoreCase))
                        {
                            annotPrefix = "cli/annotations/";
                            targetAnnotDir = cliAnnotationsDir;
                        }

                        if (annotPrefix != null && norm.EndsWith(".pbtxt", StringComparison.OrdinalIgnoreCase))
                        {
                            string filePart = norm.Substring(annotPrefix.Length);
                            if (filePart.IndexOf('/') >= 0) continue;

                            string cid = Path.GetFileNameWithoutExtension(filePart);
                            // P1-8: Ignore orphan .pbtxt annotations not in selected/closure set
                            if (!SessionMetadataExtractor.IsLikelyConversationId(cid) || !convIdsToExtract.Contains(cid))
                                continue;

                            if (!Directory.Exists(targetAnnotDir)) Directory.CreateDirectory(targetAnnotDir);
                            string trimmedCid = cid.Trim();
                            string dest = CapabilityVaultService.CombineAndEnsureUnderRoot(targetAnnotDir, trimmedCid + ".pbtxt");
                            ExtractZipEntrySafe(entry, dest);
                            extractedAnnotationCids.Add(trimmedCid);
                            workRes.FilesImported++;
                            continue;
                        }

                        string brainPrefix = null;
                        string targetBrainDir = null;
                        if (norm.StartsWith("antigravity/brain/", StringComparison.OrdinalIgnoreCase))
                        {
                            brainPrefix = "antigravity/brain/";
                            targetBrainDir = ideBrainDir;
                        }
                        else if (norm.StartsWith("antigravity-cli/brain/", StringComparison.OrdinalIgnoreCase))
                        {
                            brainPrefix = "antigravity-cli/brain/";
                            targetBrainDir = cliBrainDir;
                        }
                        else if (norm.StartsWith("cli/brain/", StringComparison.OrdinalIgnoreCase))
                        {
                            brainPrefix = "cli/brain/";
                            targetBrainDir = cliBrainDir;
                        }

                        if (brainPrefix != null)
                        {
                            string rest = norm.Substring(brainPrefix.Length);
                            int slashIdx = rest.IndexOf('/');
                            if (slashIdx <= 0 || slashIdx >= rest.Length - 1) continue;

                            string cid = rest.Substring(0, slashIdx);
                            string relSubPath = rest.Substring(slashIdx + 1);
                            if (!SessionMetadataExtractor.IsLikelyConversationId(cid) || !convIdsToExtract.Contains(cid))
                                continue;
                            if (SessionLifecycleService.HasExcludedBinSegment(relSubPath))
                                continue;

                            if (!Directory.Exists(targetBrainDir)) Directory.CreateDirectory(targetBrainDir);
                            string cidBrainRoot = CapabilityVaultService.CombineAndEnsureUnderRoot(targetBrainDir, cid);
                            string dest = CapabilityVaultService.CombineAndEnsureUnderRoot(cidBrainRoot, relSubPath);
                            ExtractZipEntrySafe(entry, dest);
                            workRes.FilesImported++;
                            continue;
                        }

                        if (norm.StartsWith("config/projects/", StringComparison.OrdinalIgnoreCase) &&
                            norm.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        {
                            string filePart = norm.Substring("config/projects/".Length);
                            if (filePart.IndexOf('/') >= 0) continue;

                            string archiveProjId = Path.GetFileNameWithoutExtension(filePart);
                            if (string.Equals(archiveProjId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
                                continue;

                            string rawJson;
                            using (StreamReader sr = new StreamReader(entry.Open(), Encoding.UTF8))
                            {
                                rawJson = sr.ReadToEnd();
                            }

                            string pNameInJson = DirectStreamArchiver.ExtractJsonValue(rawJson, "name");
                            if (VisualDiffEngine.IsOutsideProject(pNameInJson) || SmartProjectFilter.IsJunkOrEphemeral(pNameInJson))
                                continue;

                            ResolvedImportProjectInfo matchedInfo = null;
                            if (!byBackupProjId.TryGetValue(archiveProjId, out matchedInfo))
                            {
                                if (!string.IsNullOrEmpty(pNameInJson))
                                    byProjName.TryGetValue(pNameInJson, out matchedInfo);
                            }

                            if (matchedInfo != null && !string.IsNullOrEmpty(matchedInfo.TargetId))
                            {
                                string dest = CapabilityVaultService.CombineAndEnsureUnderRoot(projDir, matchedInfo.TargetId + ".json");
                                if (!File.Exists(dest))
                                {
                                    // FIX P1-8: Rewrite internal "id" to TargetId & apply custom FolderUri override
                                    string remappedJson = DirectStreamArchiver.RemapAllPathFormats(rawJson, srcHomeSnap, curHomeSnap);
                                    remappedJson = PatchImportedProjectJson(remappedJson, matchedInfo);
                                    SessionLifecycleService.WriteUtf8TextAtomicWithBackup(dest, remappedJson, false, null);
                                    workRes.FilesImported++;
                                }
                            }
                        }
                    }

                    if (effectiveSelectedConvs.Count > 0)
                    {
                        // Phase 2a-bis: synthesize Layer 1 .pbtxt annotations for legacy archives
                        for (int i = 0; i < effectiveSelectedConvs.Count; i++)
                        {
                            ConversationSummaryInfo sc = effectiveSelectedConvs[i];
                            if (sc == null || string.IsNullOrEmpty(sc.ConversationId)) continue;
                            string cid = sc.ConversationId.Trim();
                            if (!SessionMetadataExtractor.IsLikelyConversationId(cid)) continue;
                            if (extractedAnnotationCids.Contains(cid)) continue;

                            string synthTitle = null;
                            if (sc.HasCustomTitle && !string.IsNullOrWhiteSpace(sc.CustomTitle))
                                synthTitle = sc.CustomTitle.Trim();
                            else if (!string.IsNullOrWhiteSpace(sc.DisplayTitle))
                                synthTitle = sc.DisplayTitle.Trim();

                            if (!string.IsNullOrWhiteSpace(synthTitle) &&
                                !IncrementalProtoShardMerger.IsSyntheticPlaceholderTitle(synthTitle) &&
                                (sc.HasCustomTitle || !IncrementalProtoShardMerger.IsSyntheticPlaceholderTitle(sc.DisplayTitle)))
                            {
                                try
                                {
                                    string targetAnnotBase = sc.IsCliSession ? cliAnnotationsDir : ideAnnotationsDir;
                                    if (!Directory.Exists(targetAnnotBase)) Directory.CreateDirectory(targetAnnotBase);
                                    string pbtxtDest = CapabilityVaultService.CombineAndEnsureUnderRoot(targetAnnotBase, cid + ".pbtxt");
                                    string existingRaw = File.Exists(pbtxtDest) ? SessionLifecycleService.ReadUtf8TextShared(pbtxtDest) : null;
                                    string synthesized = PbtxtAnnotationCodec.UpsertTitle(existingRaw, synthTitle);
                                    SessionLifecycleService.WriteUtf8TextAtomicWithBackup(pbtxtDest, synthesized, false, null);
                                    workRes.FilesImported++;
                                }
                                catch (Exception ex)
                                {
                                    workRes.Warnings.Add(string.Format("合成会话 {0} 的 .pbtxt 标题标注失败: {1}", cid, ex.Message));
                                }
                            }
                        }

                        // Phase 2b: create missing project JSONs from standard template
                        foreach (KeyValuePair<string, ResolvedImportProjectInfo> kvp in neededProjectsByTargetId)
                        {
                            ResolvedImportProjectInfo pInfo = kvp.Value;
                            string dest = CapabilityVaultService.CombineAndEnsureUnderRoot(projDir, pInfo.TargetId + ".json");
                            if (!File.Exists(dest))
                            {
                                string fallbackJson = WorkspaceService.GenerateStandardProjectJson(pInfo.TargetId, pInfo.ProjectName, pInfo.FolderUri);
                                SessionLifecycleService.WriteUtf8TextAtomicWithBackup(dest, fallbackJson, false, null);
                                workRes.FilesImported++;
                            }
                        }

                        // Phase 2c: remap per-session SQLite metadata
                        if (onProgress != null) onProgress("正在同步重映射 SQLite 会话数据库元数据与摘要索引 (.pb / summary.sqlite)...", 75);
                        foreach (KeyValuePair<string, string> dbKvp in extractedDbPaths)
                        {
                            string key = dbKvp.Key;
                            int colonIdx = key.IndexOf(':');
                            string cid = colonIdx >= 0 ? key.Substring(colonIdx + 1) : key;
                            string dbDest = dbKvp.Value;
                            ConversationSummaryInfo sc = convMap.ContainsKey(cid) ? convMap[cid] : null;
                            string pName = (sc != null && !string.IsNullOrEmpty(sc.ProjectName))
                                ? sc.ProjectName
                                : (convResolvedProjName.ContainsKey(cid) ? convResolvedProjName[cid] : null);
                            string pId = convResolvedProjId.ContainsKey(cid) ? convResolvedProjId[cid] : null;
                            string dTitle = sc != null ? sc.DisplayTitle : null;

                            bool sqlOk = SqliteMetadataUpdater.RemapSqliteDbMetadata(
                                dbDest, srcHomeSnap, curHomeSnap, projectUriOverrides, pName, pId, dTitle
                            );
                            if (!sqlOk)
                            {
                                workRes.Warnings.Add(string.Format("会话 {0} 的 SQLite 元数据重映射失败: {1}",
                                    cid, SqliteMetadataUpdater.LastError ?? "未知错误"));
                            }
                        }

                        // Merge IDE and CLI protobuf shards separately
                        List<ConversationSummaryInfo> ideConvs = new List<ConversationSummaryInfo>();
                        List<ConversationSummaryInfo> cliConvs = new List<ConversationSummaryInfo>();
                        for (int i = 0; i < effectiveSelectedConvs.Count; i++)
                        {
                            ConversationSummaryInfo sc = effectiveSelectedConvs[i];
                            if (sc == null) continue;
                            if (sc.IsCliSession) cliConvs.Add(sc);
                            else ideConvs.Add(sc);
                        }

                        byte[] mergedIdePb = null;
                        if (ideConvs.Count > 0)
                        {
                            string localPb = Path.Combine(geminiSnap, "antigravity", "agyhub_summaries_proto.pb");
                            byte[] localPbBytes = File.Exists(localPb) ? SessionLifecycleService.ReadAllBytesShared(localPb) : new byte[0];
                            mergedIdePb = IncrementalProtoShardMerger.MergeShards(
                                localPbBytes, ideConvs, srcHomeSnap, curHomeSnap, projectUriOverrides, projectNewIds);
                            if (File.Exists(localPb))
                                UnifiedJson.CreateTimestampedBackup(localPb, 5);
                            SessionLifecycleService.WriteBytesAtomicWithBackup(localPb, mergedIdePb, null);
                            workRes.FilesImported++;
                        }

                        if (cliConvs.Count > 0)
                        {
                            string localCliPb = Path.Combine(geminiSnap, "antigravity-cli", "agyhub_summaries_proto.pb");
                            byte[] localCliPbBytes = File.Exists(localCliPb) ? SessionLifecycleService.ReadAllBytesShared(localCliPb) : new byte[0];
                            byte[] mergedCliPb = IncrementalProtoShardMerger.MergeShards(
                                localCliPbBytes, cliConvs, srcHomeSnap, curHomeSnap, projectUriOverrides, projectNewIds);
                            if (File.Exists(localCliPb))
                                UnifiedJson.CreateTimestampedBackup(localCliPb, 5);
                            SessionLifecycleService.WriteBytesAtomicWithBackup(localCliPb, mergedCliPb, null);
                            workRes.FilesImported++;
                        }

                        // Phase 2d: Upsert into antigravity/conversation_summaries.db (including ParentConversationId per P0-6!)
                        string localSummaryDb = Path.Combine(geminiSnap, "antigravity", "conversation_summaries.db");
                        if (File.Exists(localSummaryDb) && ideConvs.Count > 0)
                        {
                            Dictionary<string, byte[]> remappedMetaByCid = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                            if (mergedIdePb != null && mergedIdePb.Length > 0)
                            {
                                try
                                {
                                    List<ProtoNode> mergedNodes = ProtoCodec.Decode(mergedIdePb);
                                    for (int i = 0; i < mergedNodes.Count; i++)
                                    {
                                        ProtoNode entryNode = mergedNodes[i];
                                        if (entryNode.FieldNumber != 1 || entryNode.WireType != 2) continue;
                                        ProtoCodec.DecodeSummaryEntryTree(entryNode);
                                        ProtoNode f1Node = entryNode.GetChild(1);
                                        string mCid = f1Node != null ? (f1Node.GetString() ?? string.Empty).Trim() : string.Empty;
                                        if (string.IsNullOrEmpty(mCid) || !convIdsToExtract.Contains(mCid)) continue;
                                        ProtoNode metaNode = entryNode.GetChild(2);
                                        if (metaNode != null)
                                        {
                                            byte[] metaBytes = metaNode.HasChildren
                                                ? ProtoCodec.Encode(metaNode.Children)
                                                : (metaNode.RawBytes ?? new byte[0]);
                                            remappedMetaByCid[mCid] = metaBytes;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    workRes.Warnings.Add("解析合流后 .pb 摘要字节以同步 conversation_summaries.db 告警: " + ex.Message);
                                }
                            }

                            List<SummaryDbUpsertItem> upsertItems = new List<SummaryDbUpsertItem>(ideConvs.Count);
                            for (int i = 0; i < ideConvs.Count; i++)
                            {
                                ConversationSummaryInfo sc = ideConvs[i];
                                if (sc == null || string.IsNullOrEmpty(sc.ConversationId)) continue;
                                string cid = sc.ConversationId.Trim();
                                if (!SessionMetadataExtractor.IsLikelyConversationId(cid)) continue;

                                string pId = convResolvedProjId.ContainsKey(cid) ? convResolvedProjId[cid] : (sc.ProjectId ?? string.Empty);
                                string pName = convResolvedProjName.ContainsKey(cid) ? convResolvedProjName[cid] : (sc.ProjectName ?? string.Empty);
                                bool isOutside = string.Equals(pId, "outside-of-project", StringComparison.OrdinalIgnoreCase) ||
                                                 VisualDiffEngine.IsOutsideProject(pName);

                                string resolvedUri = null;
                                if (!isOutside)
                                {
                                    projectUriOverrides.TryGetValue("CID:" + cid, out resolvedUri);
                                    if (string.IsNullOrEmpty(resolvedUri) && !string.IsNullOrEmpty(pId))
                                        projectUriOverrides.TryGetValue("ID:" + pId, out resolvedUri);
                                    if (string.IsNullOrEmpty(resolvedUri) && !string.IsNullOrEmpty(sc.FolderUri))
                                        resolvedUri = DirectStreamArchiver.RemapAllPathFormats(sc.FolderUri, srcHomeSnap, curHomeSnap);
                                }

                                string titleVal = string.Empty;
                                if (sc.HasCustomTitle && !string.IsNullOrWhiteSpace(sc.CustomTitle))
                                    titleVal = sc.CustomTitle.Trim();
                                else if (!string.IsNullOrWhiteSpace(sc.DisplayTitle) && !IncrementalProtoShardMerger.IsSyntheticPlaceholderTitle(sc.DisplayTitle))
                                    titleVal = sc.DisplayTitle.Trim();
                                else if (!string.IsNullOrWhiteSpace(sc.AutoSummaryTitle))
                                    titleVal = sc.AutoSummaryTitle.Trim();

                                DateTime utcTime = sc.UpdatedTime > DateTime.MinValue ? sc.UpdatedTime.ToUniversalTime() : DateTime.UtcNow;
                                string isoTime = utcTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

                                SummaryDbUpsertItem upsertItem = new SummaryDbUpsertItem();
                                upsertItem.ConversationId = cid;
                                upsertItem.Title = titleVal;
                                upsertItem.Preview = titleVal;
                                upsertItem.StepCount = sc.StepCount;
                                upsertItem.LastModifiedTimeIso = isoTime;
                                upsertItem.LastUserInputTimeIso = isoTime;
                                upsertItem.ProjectId = isOutside ? "outside-of-project" : (pId ?? string.Empty);
                                upsertItem.WorkspaceUrisJson = (isOutside || string.IsNullOrEmpty(resolvedUri))
                                    ? string.Empty
                                    : "[\"" + UnifiedJson.EscapeString(resolvedUri) + "\"]";

                                // FIX P0-6: Assign ParentConversationId alongside NestingDepth!
                                upsertItem.ParentConversationId = sc.ParentConversationId != null ? sc.ParentConversationId.Trim() : string.Empty;
                                upsertItem.NestingDepth = !string.IsNullOrEmpty(upsertItem.ParentConversationId) ? 1 : 0;

                                byte[] rawMeta;
                                if (remappedMetaByCid.TryGetValue(cid, out rawMeta))
                                    upsertItem.RawSummaryBytes = rawMeta;
                                upsertItem.ProjectName = isOutside ? string.Empty : (pName ?? string.Empty);
                                upsertItem.FtsWorkspaceToken = isOutside ? string.Empty : (resolvedUri ?? string.Empty);
                                upsertItems.Add(upsertItem);
                            }

                            if (upsertItems.Count > 0)
                            {
                                List<string> sumDbWarnings;
                                int upserted = SummarySqliteBridge.UpsertSessionsInSummaryDb(localSummaryDb, upsertItems, out sumDbWarnings);
                                if (sumDbWarnings != null && sumDbWarnings.Count > 0)
                                    workRes.Warnings.AddRange(sumDbWarnings);
                                if (upserted == 0 && (sumDbWarnings == null || sumDbWarnings.Count == 0) && !string.IsNullOrEmpty(SummarySqliteBridge.LastError))
                                    workRes.Warnings.Add("同步写入 conversation_summaries.db 失败: " + SummarySqliteBridge.LastError);
                            }
                        }
                    }
                }
                workRes.ConversationsImported = effectiveSelectedConvs.Count;
            }

            if (onProgress != null) onProgress("选择性导入已完成。", 100);
            return workRes;
        }

        private static SelectiveImportResult PerformColdArchiveRestore(
            SelectiveImportRequest req,
            List<ConversationSummaryInfo> selectedConvs,
            Action<string, int> onProgress)
        {
            SelectiveImportResult res = new SelectiveImportResult();
            string geminiSnap = req.GeminiPath;
            string ideConvDir = Path.Combine(geminiSnap, "antigravity", "conversations");
            string cliConvDir = Path.Combine(geminiSnap, "antigravity-cli", "conversations");

            HashSet<string> targetCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (selectedConvs != null)
            {
                for (int i = 0; i < selectedConvs.Count; i++)
                {
                    if (selectedConvs[i] != null && SessionMetadataExtractor.IsLikelyConversationId(selectedConvs[i].ConversationId))
                        targetCids.Add(selectedConvs[i].ConversationId.Trim());
                }
            }

            bool restoreLegacyCli = req.RestoreLegacyCliArchive ||
                (req.BackupIndex != null && string.Equals(req.BackupIndex.ColdArchiveType, "legacy-cli-summary-archive", StringComparison.OrdinalIgnoreCase));

            if (targetCids.Count == 0 && !restoreLegacyCli)
            {
                res.ErrorMessage = "请先选择要从安全备份中恢复的后台子任务数据库。";
                return res;
            }

            if (onProgress != null) onProgress("正在从清理前安全备份恢复数据库文件...", 30);

            HashSet<string> restoredCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (FileStream fs = new FileStream(req.LoadedBackupPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                for (int i = 0; i < archive.Entries.Count; i++)
                {
                    ZipArchiveEntry entry = archive.Entries[i];
                    if (!StreamingZipIndexReader.IsSafeZipEntryPath(entry.FullName)) continue;

                    string rawNorm = (entry.FullName ?? string.Empty).Replace('\\', '/').TrimStart('/');
                    if (string.IsNullOrEmpty(rawNorm) || rawNorm.EndsWith("/", StringComparison.Ordinal)) continue;
                    string norm = rawNorm.StartsWith("data/", StringComparison.OrdinalIgnoreCase) ? rawNorm.Substring(5) : rawNorm;

                    if (restoreLegacyCli && string.Equals(norm, "antigravity-cli/conversation_summaries.db", StringComparison.OrdinalIgnoreCase))
                    {
                        string cliDir = Path.Combine(geminiSnap, "antigravity-cli");
                        if (!Directory.Exists(cliDir)) Directory.CreateDirectory(cliDir);
                        string destLegacy = Path.Combine(cliDir, "conversation_summaries.db");
                        if (File.Exists(destLegacy)) UnifiedJson.CreateTimestampedBackup(destLegacy, 5);
                        DeleteFileIfExistsQuiet(destLegacy + "-wal");
                        DeleteFileIfExistsQuiet(destLegacy + "-shm");
                        ExtractZipEntrySafe(entry, destLegacy);
                        res.FilesImported++;
                        continue;
                    }

                    string targetDir = null;
                    string fn = null;
                    if (norm.StartsWith("antigravity/conversations/", StringComparison.OrdinalIgnoreCase))
                    {
                        targetDir = ideConvDir;
                        fn = norm.Substring("antigravity/conversations/".Length);
                    }
                    else if (norm.StartsWith("antigravity-cli/conversations/", StringComparison.OrdinalIgnoreCase))
                    {
                        targetDir = cliConvDir;
                        fn = norm.Substring("antigravity-cli/conversations/".Length);
                    }

                    if (targetDir == null || string.IsNullOrEmpty(fn) || fn.IndexOf('/') >= 0) continue;
                    if (!fn.EndsWith(".db", StringComparison.OrdinalIgnoreCase)) continue;

                    string cid = fn.Substring(0, fn.Length - 3).Trim();
                    if (!SessionMetadataExtractor.IsLikelyConversationId(cid) || !targetCids.Contains(cid)) continue;

                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                    string destDb = CapabilityVaultService.CombineAndEnsureUnderRoot(targetDir, cid + ".db");
                    if (File.Exists(destDb)) UnifiedJson.CreateTimestampedBackup(destDb, 5);
                    DeleteFileIfExistsQuiet(destDb + "-wal");
                    DeleteFileIfExistsQuiet(destDb + "-shm");
                    DeleteFileIfExistsQuiet(destDb + "-journal");
                    ExtractZipEntrySafe(entry, destDb);

                    res.FilesImported++;
                    if (restoredCids.Add(cid))
                        res.ImportedConvIds.Add(cid);
                }
            }

            res.ConversationsImported = restoredCids.Count;
            if (res.FilesImported == 0)
            {
                res.ErrorMessage = "安全备份包中未找到匹配的数据库文件。";
                return res;
            }

            if (onProgress != null) onProgress("清理前安全备份恢复完成。", 100);
            return res;
        }

        private static string PatchImportedProjectJson(string remappedJson, ResolvedImportProjectInfo matchedInfo)
        {
            if (string.IsNullOrWhiteSpace(remappedJson) || matchedInfo == null)
                return remappedJson;

            try
            {
                string result = remappedJson;
                if (!string.IsNullOrEmpty(matchedInfo.TargetId))
                {
                    result = UnifiedJson.PatchObjectKeyPreservingComments(
                        result, null, "id", "\"" + UnifiedJson.EscapeString(matchedInfo.TargetId) + "\"");
                }

                if (!string.IsNullOrEmpty(matchedInfo.FolderUri))
                {
                    string normalizedOverrideUri = SessionLifecycleService.ToFileUri(matchedInfo.FolderUri);
                    string uriLiteral = "\"" + UnifiedJson.EscapeString(normalizedOverrideUri) + "\"";
                    result = UnifiedJson.PatchNestedPathPreservingComments(
                        result,
                        new string[] { "projectResources", "resources[*]", "gitFolder", "folderUri" },
                        uriLiteral);

                    object parsed;
                    if (UnifiedJson.TryParse(result, out parsed))
                    {
                        Dictionary<string, object> rootDict = UnifiedJson.AsDict(parsed);
                        if (rootDict != null)
                        {
                            string existingRootUriKey = null;
                            foreach (string k in rootDict.Keys)
                            {
                                if (string.Equals(k, "folderUri", StringComparison.OrdinalIgnoreCase))
                                {
                                    existingRootUriKey = k;
                                    break;
                                }
                            }
                            if (!string.IsNullOrEmpty(existingRootUriKey))
                            {
                                result = UnifiedJson.PatchObjectKeyPreservingComments(result, null, existingRootUriKey, uriLiteral);
                            }
                        }
                    }
                }

                string nowIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
                result = UnifiedJson.PatchObjectKeyPreservingComments(
                    result, null, "updatedAt", "\"" + nowIso + "\"");
                return result;
            }
            catch
            {
                return remappedJson;
            }
        }

        public static void CreateFullBackup(
            string geminiPath,
            string homePath,
            List<ConversationSummaryInfo> localSummaries,
            string destPath,
            Action<string, int> onProgress,
            out int fileCount,
            out List<string> warnings)
        {
            CreateFullBackup(geminiPath, homePath, localSummaries, destPath, BackupTier.FastDaily, onProgress, out fileCount, out warnings);
        }

        public static void CreateFullBackup(
            string geminiPath,
            string homePath,
            List<ConversationSummaryInfo> localSummaries,
            string destPath,
            BackupTier tier,
            Action<string, int> onProgress,
            out int fileCount,
            out List<string> warnings)
        {
            fileCount = 0;
            warnings = new List<string>();
            if (string.IsNullOrEmpty(geminiPath) || !Directory.Exists(geminiPath))
                throw new DirectoryNotFoundException("未找到 .gemini 目录: " + geminiPath);

            string dDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dDir) && !Directory.Exists(dDir)) Directory.CreateDirectory(dDir);

            string homeSnap = homePath ?? WorkspaceService.ResolveDefaultHomePath();
            string userSnap = Environment.UserName;
            string tierName = tier == BackupTier.FullForensic ? "FullForensic" : "FastDaily";
            string tmpDest = destPath + ".tmp." + Guid.NewGuid().ToString("N").Substring(0, 8);
            int n = 0;
            try
            {
                HashSet<string> writtenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (FileStream fs = new FileStream(tmpDest, FileMode.Create, FileAccess.Write, FileShare.None))
                using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    if (writtenEntries.Add("metadata.json"))
                    {
                        ZipArchiveEntry metaEntry = archive.CreateEntry("metadata.json", CompressionLevel.Fastest);
                        using (StreamWriter sw = new StreamWriter(metaEntry.Open(), new UTF8Encoding(false)))
                        {
                            string meta = "{\r\n" +
                                          "  \"source_home\": \"" + UnifiedJson.EscapeString(homeSnap) + "\",\r\n" +
                                          "  \"source_user\": \"" + UnifiedJson.EscapeString(userSnap) + "\",\r\n" +
                                          "  \"backup_time\": \"" + UnifiedJson.EscapeString(DateTime.Now.ToString("o")) + "\",\r\n" +
                                          "  \"backup_tier\": \"" + tierName + "\",\r\n" +
                                          "  \"version\": \"1.0\"\r\n}";
                            sw.Write(meta);
                        }
                        n++;
                    }

                    if (onProgress != null) onProgress("正在归档全部有效会话、标题标注与脑区索引...", 25);
                    HashSet<string> validCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    HashSet<string> subagentCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (localSummaries != null)
                    {
                        for (int i = 0; i < localSummaries.Count; i++)
                        {
                            ConversationSummaryInfo s = localSummaries[i];
                            if (s != null && !string.IsNullOrEmpty(s.ConversationId) && SessionMetadataExtractor.IsLikelyConversationId(s.ConversationId))
                            {
                                string cidTrim = s.ConversationId.Trim();
                                validCids.Add(cidTrim);
                                if (s.IsSubagent) subagentCids.Add(cidTrim);
                            }
                        }
                    }

                    string ideRoot = Path.Combine(geminiPath, "antigravity");
                    string cliRoot = Path.Combine(geminiPath, "antigravity-cli");

                    // Supplement subagent detection from local .pb / summary.db if localSummaries was empty/partial
                    CollectSubagentCidsFromDisk(ideRoot, subagentCids);
                    CollectSubagentCidsFromDisk(cliRoot, subagentCids);

                    string ideConvDir = Path.Combine(ideRoot, "conversations");
                    string ideBrainDir = Path.Combine(ideRoot, "brain");
                    string cliConvDir = Path.Combine(cliRoot, "conversations");
                    string cliBrainDir = Path.Combine(cliRoot, "brain");

                    // FIX P0-3 & P0-4: Snapshot per-session .db via SqliteOnlineBackup for both IDE and CLI
                    // Tier policy: FastDaily excludes subagent DBs (IsSubagent == true); FullForensic includes them
                    foreach (string cid in validCids)
                    {
                        if (string.Equals(cid, "bin", StringComparison.OrdinalIgnoreCase)) continue;
                        bool skipSubagentDb = (tier == BackupTier.FastDaily) && subagentCids.Contains(cid);

                        string ideDb = Path.Combine(ideConvDir, cid + ".db");
                        if (!skipSubagentDb && File.Exists(ideDb))
                        {
                            n += AddSqliteSnapshotToArchive(archive, writtenEntries, ideDb, "data/antigravity/conversations/" + cid + ".db", warnings);
                        }
                        n += AddDirToArchive(archive, writtenEntries, Path.Combine(ideBrainDir, cid), "data/antigravity/brain/" + cid, false);

                        string cliDb = Path.Combine(cliConvDir, cid + ".db");
                        if (!skipSubagentDb && File.Exists(cliDb))
                        {
                            n += AddSqliteSnapshotToArchive(archive, writtenEntries, cliDb, "data/antigravity-cli/conversations/" + cid + ".db", warnings);
                        }
                        n += AddDirToArchive(archive, writtenEntries, Path.Combine(cliBrainDir, cid), "data/antigravity-cli/brain/" + cid, false);
                    }

                    // Also include any valid antigravity-cli/conversations/*.db & brain/<cid>/ not already added
                    if (Directory.Exists(cliConvDir))
                    {
                        string[] cliDbFiles = Directory.GetFiles(cliConvDir, "*.db");
                        for (int i = 0; i < cliDbFiles.Length; i++)
                        {
                            string stem = Path.GetFileNameWithoutExtension(cliDbFiles[i]);
                            if (!SessionMetadataExtractor.IsLikelyConversationId(stem)) continue;
                            if (string.Equals(stem, "bin", StringComparison.OrdinalIgnoreCase)) continue;
                            string trimmedCid = stem.Trim();
                            validCids.Add(trimmedCid);
                            bool skipSubagentDb = (tier == BackupTier.FastDaily) && subagentCids.Contains(trimmedCid);
                            string entryName = "data/antigravity-cli/conversations/" + trimmedCid + ".db";
                            if (!skipSubagentDb && !writtenEntries.Contains(entryName))
                            {
                                n += AddSqliteSnapshotToArchive(archive, writtenEntries, cliDbFiles[i], entryName, warnings);
                            }
                            string cliBrainSub = Path.Combine(cliBrainDir, trimmedCid);
                            if (Directory.Exists(cliBrainSub))
                            {
                                n += AddDirToArchive(archive, writtenEntries, cliBrainSub, "data/antigravity-cli/brain/" + trimmedCid, false);
                            }
                        }
                    }

                    // P1-8: Strictly exclude orphan .pbtxt annotations (only pack annotations belonging to validCids)
                    n += AddValidAnnotationsToArchive(archive, writtenEntries, Path.Combine(ideRoot, "annotations"), "data/antigravity/annotations", validCids);
                    n += AddValidAnnotationsToArchive(archive, writtenEntries, Path.Combine(cliRoot, "annotations"), "data/antigravity-cli/annotations", validCids);

                    n += AddFileToArchive(archive, writtenEntries,
                        Path.Combine(ideRoot, "agyhub_summaries_proto.pb"),
                        "data/antigravity/agyhub_summaries_proto.pb");

                    n += AddFileToArchive(archive, writtenEntries,
                        Path.Combine(cliRoot, "agyhub_summaries_proto.pb"),
                        "data/antigravity-cli/agyhub_summaries_proto.pb");

                    string summaryDbPath = Path.Combine(ideRoot, "conversation_summaries.db");
                    if (File.Exists(summaryDbPath))
                    {
                        if (onProgress != null) onProgress("正在安全快照 conversation_summaries.db...", 65);
                        n += AddSqliteSnapshotToArchive(archive, writtenEntries, summaryDbPath, "data/antigravity/conversation_summaries.db", warnings);
                    }

                    if (onProgress != null) onProgress("正在归档项目配置、用户设置、规则与 MCP 配置...", 85);
                    n += AddFileToArchive(archive, writtenEntries, Path.Combine(geminiPath, "settings.json"), "data/settings.json");
                    n += AddFileToArchive(archive, writtenEntries, Path.Combine(cliRoot, "settings.json"), "data/antigravity-cli/settings.json");
                    n += AddFileToArchive(archive, writtenEntries, Path.Combine(geminiPath, ".geminirules"), "data/.geminirules");
                    n += AddFileToArchive(archive, writtenEntries, Path.Combine(geminiPath, "GEMINI.md"), "data/GEMINI.md");
                    n += AddFileToArchive(archive, writtenEntries, Path.Combine(geminiPath, "AGENTS.md"), "data/AGENTS.md");
                    n += AddDirToArchive(archive, writtenEntries, Path.Combine(geminiPath, "config"), "data/config", true);
                    n += AddDirToArchive(archive, writtenEntries, Path.Combine(ideRoot, "mcp"), "data/antigravity/mcp", false);
                }

                if (File.Exists(destPath))
                {
                    File.SetAttributes(destPath, FileAttributes.Normal);
                    File.Delete(destPath);
                }
                File.Move(tmpDest, destPath);
                if (onProgress != null) onProgress("全量备份归档完成。", 100);
            }
            catch
            {
                try { if (File.Exists(tmpDest)) File.Delete(tmpDest); } catch { }
                throw;
            }
            fileCount = n;
        }

        private static int AddValidAnnotationsToArchive(
            ZipArchive archive,
            HashSet<string> writtenEntries,
            string annotationsDir,
            string zipPrefix,
            HashSet<string> validCids)
        {
            if (!Directory.Exists(annotationsDir) || validCids == null || validCids.Count == 0)
                return 0;

            int count = 0;
            string[] pbtxtFiles = Directory.GetFiles(annotationsDir, "*.pbtxt");
            for (int i = 0; i < pbtxtFiles.Length; i++)
            {
                string pf = pbtxtFiles[i];
                string fn = Path.GetFileName(pf);
                string stem = Path.GetFileNameWithoutExtension(pf);
                if (!string.IsNullOrEmpty(fn) &&
                    SessionMetadataExtractor.IsLikelyConversationId(stem) &&
                    validCids.Contains(stem.Trim()))
                {
                    count += AddFileToArchive(archive, writtenEntries, pf, zipPrefix + "/" + fn);
                }
            }
            return count;
        }

        private static int AddSqliteSnapshotToArchive(
            ZipArchive archive,
            HashSet<string> writtenEntries,
            string srcDbPath,
            string entryName,
            List<string> warnings)
        {
            if (archive == null || string.IsNullOrEmpty(srcDbPath) || !File.Exists(srcDbPath))
                return 0;
            if (writtenEntries != null && writtenEntries.Contains(entryName))
                return 0;

            string tempDb = Path.Combine(Path.GetTempPath(), "agy_full_bk_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".db");
            try
            {
                string err;
                if (SqliteOnlineBackup.BackupDatabaseAtomic(srcDbPath, tempDb, out err))
                {
                    return AddFileToArchive(archive, writtenEntries, tempDb, entryName);
                }
                if (warnings != null)
                    warnings.Add("在线快照 SQLite 失败（已跳过 " + Path.GetFileName(srcDbPath) + "）: " + (err ?? "未知错误"));
                return 0;
            }
            finally
            {
                DeleteFileIfExistsQuiet(tempDb);
                DeleteFileIfExistsQuiet(tempDb + "-wal");
                DeleteFileIfExistsQuiet(tempDb + "-shm");
                DeleteFileIfExistsQuiet(tempDb + "-journal");
            }
        }

        private static bool DoesProjectMetadataMatchUri(ProjectMetadata pm, string uri)
        {
            if (pm == null || string.IsNullOrEmpty(uri)) return false;
            if (UriEqualsNormalized(pm.FolderUri, uri)) return true;
            if (pm.FolderUris != null)
            {
                for (int i = 0; i < pm.FolderUris.Count; i++)
                {
                    if (UriEqualsNormalized(pm.FolderUris[i], uri)) return true;
                }
            }
            return false;
        }

        private static ProjectDiffNode FindProjectDiffForBackupConv(List<ProjectDiffNode> diffNodes, string cid)
        {
            if (diffNodes == null || string.IsNullOrEmpty(cid)) return null;
            for (int i = 0; i < diffNodes.Count; i++)
            {
                ProjectDiffNode pDiff = diffNodes[i];
                if (pDiff == null || pDiff.BackupConversations == null) continue;
                for (int j = 0; j < pDiff.BackupConversations.Count; j++)
                {
                    if (string.Equals(pDiff.BackupConversations[j].ConversationId, cid, StringComparison.OrdinalIgnoreCase))
                        return pDiff;
                }
            }
            return null;
        }

        private static void SetUnambiguousMapping(
            Dictionary<string, string> dict,
            Dictionary<string, string> seenFirst,
            HashSet<string> ambiguousKeys,
            string key,
            string value)
        {
            if (dict == null || seenFirst == null || ambiguousKeys == null ||
                string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                return;

            if (ambiguousKeys.Contains(key)) return;

            string prev;
            if (seenFirst.TryGetValue(key, out prev))
            {
                if (!string.Equals(prev, value, StringComparison.OrdinalIgnoreCase))
                {
                    dict.Remove(key);
                    ambiguousKeys.Add(key);
                }
            }
            else
            {
                seenFirst[key] = value;
                dict[key] = value;
            }
        }

        private static bool UriEqualsNormalized(string uriA, string uriB)
        {
            if (string.IsNullOrWhiteSpace(uriA) || string.IsNullOrWhiteSpace(uriB)) return false;
            return string.Equals(
                WorkspaceService.UriToDisplayPath(uriA).TrimEnd('\\', '/'),
                WorkspaceService.UriToDisplayPath(uriB).TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
        }

        private static void DeleteFileIfExistsQuiet(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }
            }
            catch { }
        }

        private static void ExtractZipEntrySafe(ZipArchiveEntry entry, string destPath)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Name)) return;
            string dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string tmpDest = destPath + ".tmp." + Guid.NewGuid().ToString("N").Substring(0, 8);
            try
            {
                using (Stream s = entry.Open())
                using (FileStream fs = new FileStream(tmpDest, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    s.CopyTo(fs);
                    fs.Flush(true);
                }

                if (File.Exists(destPath))
                {
                    try { File.SetAttributes(destPath, FileAttributes.Normal); } catch { }
                    File.Delete(destPath);
                }
                File.Move(tmpDest, destPath);
            }
            catch
            {
                try { if (File.Exists(tmpDest)) File.Delete(tmpDest); } catch { }
                throw;
            }
        }

        private static int AddFileToArchive(ZipArchive archive, HashSet<string> writtenEntries, string filePath, string entryName)
        {
            if (archive == null || string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return 0;

            string normEntry = (entryName ?? string.Empty).Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrEmpty(normEntry) || (writtenEntries != null && !writtenEntries.Add(normEntry)))
                return 0;

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                ZipArchiveEntry entry = archive.CreateEntry(normEntry, CompressionLevel.Fastest);
                using (Stream s = entry.Open())
                {
                    fs.CopyTo(s);
                }
            }
            return 1;
        }

        private sealed class DirWalkItem
        {
            public string FullDir;
            public string RelDir;
            public int Depth;
        }

        private static int AddDirToArchive(ZipArchive archive, HashSet<string> writtenEntries, string dirPath, string baseEntryDir, bool isConfigRoot)
        {
            if (archive == null || string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath)) return 0;

            int count = 0;
            string cleanBase = (baseEntryDir ?? string.Empty).Replace('\\', '/').Trim('/');
            Stack<DirWalkItem> stack = new Stack<DirWalkItem>();
            stack.Push(new DirWalkItem { FullDir = dirPath, RelDir = string.Empty, Depth = 0 });

            while (stack.Count > 0)
            {
                DirWalkItem cur = stack.Pop();
                if (cur.Depth > 32) continue;

                string[] files = Directory.GetFiles(cur.FullDir);
                for (int i = 0; i < files.Length; i++)
                {
                    string f = files[i];
                    string fn = Path.GetFileName(f);
                    string rel = string.IsNullOrEmpty(cur.RelDir) ? fn : (cur.RelDir + "/" + fn);
                    if (string.Equals(fn, "installation_id", StringComparison.OrdinalIgnoreCase)) continue;
                    if (SessionLifecycleService.HasExcludedBinSegment(rel)) continue;
                    if (CapabilityVaultService.IsExcludedFileOrPath(rel)) continue;

                    string entryName = string.IsNullOrEmpty(cleanBase) ? rel : (cleanBase + "/" + rel);
                    count += AddFileToArchive(archive, writtenEntries, f, entryName);
                }

                string[] subDirs = Directory.GetDirectories(cur.FullDir);
                for (int i = 0; i < subDirs.Length; i++)
                {
                    string sub = subDirs[i];
                    string subName = Path.GetFileName(sub);
                    if (string.Equals(subName, "bin", StringComparison.OrdinalIgnoreCase)) continue;
                    if (CapabilityVaultService.IsExcludedSubdirName(subName)) continue;
                    if (isConfigRoot && cur.Depth == 0 && string.Equals(subName, "builtin", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string nextRel = string.IsNullOrEmpty(cur.RelDir) ? subName : (cur.RelDir + "/" + subName);
                    stack.Push(new DirWalkItem { FullDir = sub, RelDir = nextRel, Depth = cur.Depth + 1 });
                }
            }
            return count;
        }

        private static void CollectSubagentCidsFromDisk(string dataRoot, HashSet<string> subagentCids)
        {
            if (string.IsNullOrEmpty(dataRoot) || subagentCids == null) return;
            try
            {
                string pbPath = Path.Combine(dataRoot, "agyhub_summaries_proto.pb");
                if (File.Exists(pbPath))
                {
                    byte[] pbBytes = SessionLifecycleService.ReadAllBytesShared(pbPath);
                    List<ConversationSummaryInfo> sums = SessionMetadataExtractor.ExtractSummariesFromProto(pbBytes);
                    for (int i = 0; i < sums.Count; i++)
                    {
                        ConversationSummaryInfo s = sums[i];
                        if (s != null && s.IsSubagent && SessionMetadataExtractor.IsLikelyConversationId(s.ConversationId))
                            subagentCids.Add(s.ConversationId.Trim());
                    }
                }
            }
            catch { }

            try
            {
                string sumDbPath = Path.Combine(dataRoot, "conversation_summaries.db");
                if (File.Exists(sumDbPath))
                {
                    Dictionary<string, SummaryDbRowInfo> rows = SummarySqliteBridge.ReadSummaryDbSnapshot(sumDbPath);
                    if (rows != null)
                    {
                        foreach (KeyValuePair<string, SummaryDbRowInfo> kvp in rows)
                        {
                            SummaryDbRowInfo r = kvp.Value;
                            if (r == null) continue;
                            if (!string.IsNullOrWhiteSpace(r.ParentConversationId) || !string.IsNullOrWhiteSpace(r.AgentName))
                            {
                                if (SessionMetadataExtractor.IsLikelyConversationId(kvp.Key))
                                    subagentCids.Add(kvp.Key.Trim());
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private static bool MergeUserSettingsAndPluginFlagsFromConfigJson(
            string geminiPath,
            string archiveConfigJson,
            out string warning)
        {
            warning = null;
            if (string.IsNullOrWhiteSpace(archiveConfigJson))
                return false;

            object parsedArchive;
            if (!UnifiedJson.TryParse(archiveConfigJson, out parsedArchive))
            {
                warning = "归档中的 config/config.json 语法无效，已跳过 RestoreUserSettings 合并。";
                return false;
            }
            Dictionary<string, object> archiveRoot = UnifiedJson.AsDict(parsedArchive);
            if (archiveRoot == null)
                return false;

            Dictionary<string, object> archiveUserSettings = FindSubDictIgnoreCase(archiveRoot, "userSettings");
            Dictionary<string, object> archivePlugins = FindSubDictIgnoreCase(archiveRoot, "plugins");
            if ((archiveUserSettings == null || archiveUserSettings.Count == 0) &&
                (archivePlugins == null || archivePlugins.Count == 0))
            {
                return false;
            }

            string cfgDir = Path.Combine(geminiPath, "config");
            if (!Directory.Exists(cfgDir)) Directory.CreateDirectory(cfgDir);
            string localConfigPath = Path.Combine(cfgDir, "config.json");

            string localText = File.Exists(localConfigPath) ? SessionLifecycleService.ReadUtf8TextShared(localConfigPath) : "{}";
            if (string.IsNullOrWhiteSpace(localText)) localText = "{}";

            Dictionary<string, object> localRoot = null;
            object parsedLocal;
            if (UnifiedJson.TryParse(localText, out parsedLocal))
            {
                localRoot = UnifiedJson.AsDict(parsedLocal);
            }
            if (localRoot == null)
            {
                warning = "本地 config/config.json 格式损坏，已跳过 RestoreUserSettings 合并。";
                return false;
            }

            string patched = localText;
            bool anyChange = false;

            if (archiveUserSettings != null && archiveUserSettings.Count > 0)
            {
                string localUsKey = FindExactKeyIgnoreCase(localRoot, "userSettings") ?? "userSettings";
                Dictionary<string, object> localUsDict = FindSubDictIgnoreCase(localRoot, "userSettings");
                if (localUsDict == null && FindExactKeyIgnoreCase(localRoot, "userSettings") != null)
                {
                    patched = UnifiedJson.PatchObjectKeyPreservingComments(patched, null, localUsKey, "{}");
                }
                foreach (KeyValuePair<string, object> kvp in archiveUserSettings)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Key)) continue;
                    string targetKey = FindExactKeyIgnoreCase(localUsDict, kvp.Key) ?? kvp.Key;
                    string literal = UnifiedJson.Serialize(kvp.Value, false);
                    patched = UnifiedJson.PatchNestedPathPreservingComments(
                        patched,
                        new string[] { localUsKey, targetKey },
                        literal);
                    anyChange = true;
                }
            }

            if (archivePlugins != null && archivePlugins.Count > 0)
            {
                string localPluginsKey = FindExactKeyIgnoreCase(localRoot, "plugins") ?? "plugins";
                Dictionary<string, object> localPluginsDict = FindSubDictIgnoreCase(localRoot, "plugins");
                if (localPluginsDict == null && FindExactKeyIgnoreCase(localRoot, "plugins") != null)
                {
                    patched = UnifiedJson.PatchObjectKeyPreservingComments(patched, null, localPluginsKey, "{}");
                }
                foreach (KeyValuePair<string, object> kvp in archivePlugins)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Key)) continue;
                    Dictionary<string, object> pEntry = UnifiedJson.AsDict(kvp.Value);
                    if (pEntry == null) continue;

                    string enabledKey = FindExactKeyIgnoreCase(pEntry, "enabled");
                    if (string.IsNullOrEmpty(enabledKey)) continue;

                    bool enabledVal = UnifiedJson.GetBool(pEntry, enabledKey, true);
                    string targetPlugKey = FindExactKeyIgnoreCase(localPluginsDict, kvp.Key) ?? kvp.Key;
                    Dictionary<string, object> localPlugEntry = FindSubDictIgnoreCase(localPluginsDict, targetPlugKey);
                    if (localPlugEntry == null && FindExactKeyIgnoreCase(localPluginsDict, targetPlugKey) != null)
                    {
                        patched = UnifiedJson.PatchNestedPathPreservingComments(
                            patched,
                            new string[] { localPluginsKey, targetPlugKey },
                            "{}");
                    }
                    string localEnabledKey = FindExactKeyIgnoreCase(localPlugEntry, "enabled") ?? "enabled";

                    patched = UnifiedJson.PatchNestedPathPreservingComments(
                        patched,
                        new string[] { localPluginsKey, targetPlugKey, localEnabledKey },
                        enabledVal ? "true" : "false");
                    anyChange = true;
                }
            }

            if (!anyChange) return false;

            if (File.Exists(localConfigPath))
            {
                UnifiedJson.CreateTimestampedBackup(localConfigPath, 5);
            }
            SessionLifecycleService.WriteUtf8TextAtomicWithBackup(localConfigPath, patched, false, null);
            return true;
        }

        private static string FindExactKeyIgnoreCase(Dictionary<string, object> dict, string key)
        {
            if (dict == null || string.IsNullOrEmpty(key)) return null;
            foreach (string k in dict.Keys)
            {
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                    return k;
            }
            return null;
        }

        private static Dictionary<string, object> FindSubDictIgnoreCase(Dictionary<string, object> dict, string key)
        {
            if (dict == null || string.IsNullOrEmpty(key)) return null;
            foreach (KeyValuePair<string, object> kvp in dict)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                    return UnifiedJson.AsDict(kvp.Value);
            }
            return null;
        }
    }
}
