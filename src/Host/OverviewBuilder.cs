using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AntigravityCompanion
{
    /// <summary>
    /// Builds JSON-serializable payloads for /api/overview, /api/sessions, /api/rename/candidates,
    /// /api/capabilities, /api/capabilities/diff, and /api/backup/current.
    /// Hardened for P0-7 (subagent grouping), P1-1 (not_fully_idle separation), P1-9 (VisualDiffEngine),
    /// P2-3 (Win32 process enumeration outside _stateLock), and P3 (accurate local vs vault missing paths).
    /// Strictly C# 5.0 (.NET Framework 4.x).
    /// </summary>
    internal static class OverviewBuilder
    {
        public static Dictionary<string, object> BuildIdeStatusDict()
        {
            int p, ls;
            ProcessGuardService.GetRunningAntigravityProcessCounts(out p, out ls);
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["running"] = (p > 0 || ls > 0);
            d["parentCount"] = p;
            d["lsCount"] = ls;
            return d;
        }

        public static Dictionary<string, object> BuildJobStatusDict(bool busy, string jobType, string msg, int pct)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["busy"] = busy;
            d["jobType"] = jobType ?? string.Empty;
            d["message"] = msg ?? string.Empty;
            d["percent"] = pct;
            return d;
        }

        public static Dictionary<string, object> SerializeOrphanScan(OrphanScanResult scan)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            if (scan == null) return d;
            d["totalItems"] = scan.TotalOrphanItems;
            d["sessionCount"] = scan.OrphanSessionIds.Count;
            d["dbFilesCount"] = scan.OrphanDbFiles.Count;
            d["brainDirsCount"] = scan.OrphanBrainDirs.Count;
            d["annotationFilesCount"] = scan.OrphanAnnotationFiles.Count;
            d["totalMegabytes"] = Math.Round(scan.TotalOrphanMegabytes, 2);
            return d;
        }

        public static bool IsSessionRunning(ConversationSummaryInfo s)
        {
            if (s == null) return false;
            return s.NotFullyIdle ||
                   (!string.IsNullOrEmpty(s.RunStatus) &&
                    s.RunStatus.IndexOf("RUNNING", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static Dictionary<string, object> BuildOverviewPayload(LocalWebHost host)
        {
            // FIX P2-3: Enumerate Win32 IDE processes strictly OUTSIDE _stateLock
            Dictionary<string, object> ideStatus = BuildIdeStatusDict();

            Dictionary<string, object> d = new Dictionary<string, object>();
            lock (host.StateLock)
            {
                ProjectGroupsSnapshot groups = host.ProjectGroupsUnsafe;
                List<ConversationSummaryInfo> localSums = host.LocalSummariesUnsafe;
                List<CapabilityItem> localCaps = host.LocalCapsUnsafe;
                List<CapabilityItem> repoCaps = host.RepoCapsUnsafe;
                List<CapabilityDiffNode> capDiffs = host.CapDiffsUnsafe;

                Dictionary<string, object> counts = new Dictionary<string, object>();
                counts["formalProjectsCount"] = groups.FormalProjectsCount;
                counts["totalRawSessions"] = groups.TotalRawSessions;
                counts["formalPrimarySessions"] = groups.FormalPrimarySessions;
                counts["cliSessions"] = groups.CliSessions;
                counts["outsideSessions"] = groups.OutsideSessions;
                counts["subagentSessions"] = groups.SubagentSessions;
                counts["ephemeralSessions"] = groups.EphemeralSessions;
                counts["localCapabilitiesCount"] = localCaps.Count;
                counts["repoCapabilitiesCount"] = repoCaps.Count;

                // FIX P1-1: Separate actionable untagged sessions from locked/active (not_fully_idle / RUNNING) sessions
                int untaggedActionableCount = 0;
                int untaggedLockedActiveCount = 0;
                List<object> lockedSessions = new List<object>();

                for (int i = 0; i < localSums.Count; i++)
                {
                    ConversationSummaryInfo s = localSums[i];
                    if (s == null) continue;

                    bool running = IsSessionRunning(s);
                    bool isTagged = AutoTitleSynthesizer.HasStandardBracketedPrefix(s.DisplayTitle);
                    bool isFormalPrimary = !s.IsSubagent && !s.IsCliSession &&
                                           !VisualDiffEngine.IsOutsideProject(s.ProjectName) &&
                                           !SmartProjectFilter.IsJunkOrEphemeral(s.ProjectName);

                    if (running)
                    {
                        Dictionary<string, object> lsItem = new Dictionary<string, object>();
                        lsItem["cid"] = s.ConversationId ?? string.Empty;
                        lsItem["title"] = s.DisplayTitle ?? string.Empty;
                        lsItem["projectName"] = s.ProjectName ?? string.Empty;
                        lsItem["runStatus"] = !string.IsNullOrEmpty(s.RunStatus) ? s.RunStatus : (s.NotFullyIdle ? "NOT_FULLY_IDLE" : "RUNNING");
                        lsItem["notFullyIdle"] = s.NotFullyIdle;
                        lsItem["isTagged"] = isTagged;
                        lsItem["isSubagent"] = s.IsSubagent;
                        lsItem["isCli"] = s.IsCliSession;
                        lockedSessions.Add(lsItem);
                    }

                    if (isFormalPrimary && !isTagged)
                    {
                        if (running) untaggedLockedActiveCount++;
                        else untaggedActionableCount++;
                    }
                }

                counts["untaggedFormalSessions"] = untaggedActionableCount;
                counts["untaggedActionableCount"] = untaggedActionableCount;
                counts["untaggedLockedActiveCount"] = untaggedLockedActiveCount;
                counts["lockedSessionsCount"] = lockedSessions.Count;

                // FIX P3: Separate local missing paths vs vault missing paths accurately
                int aligned = 0, remoteOnly = 0, localOnly = 0, remoteNewer = 0, localNewer = 0, bothMod = 0, conflict = 0;
                int localMissingPaths = 0;
                int vaultMissingPaths = 0;

                for (int i = 0; i < capDiffs.Count; i++)
                {
                    CapabilityDiffNode cdn = capDiffs[i];
                    if (cdn == null) continue;

                    if (cdn.Direction == CapabilityDiffDirection.Aligned) aligned++;
                    else if (cdn.Direction == CapabilityDiffDirection.RemoteOnly) remoteOnly++;
                    else if (cdn.Direction == CapabilityDiffDirection.LocalOnly) localOnly++;
                    else if (cdn.Direction == CapabilityDiffDirection.RemoteNewer) remoteNewer++;
                    else if (cdn.Direction == CapabilityDiffDirection.LocalNewer) localNewer++;
                    else if (cdn.Direction == CapabilityDiffDirection.Conflict || cdn.Direction == CapabilityDiffDirection.NeedsReview) conflict++;
                    else bothMod++;

                    if (cdn.LocalItem != null && cdn.LocalItem.HasMissingPaths)
                        localMissingPaths++;
                    if (cdn.RemoteItem != null && cdn.RemoteItem.HasMissingPaths)
                        vaultMissingPaths++;
                }

                Dictionary<string, object> capSummary = new Dictionary<string, object>();
                capSummary["aligned"] = aligned;
                capSummary["remoteOnly"] = remoteOnly;
                capSummary["localOnly"] = localOnly;
                capSummary["remoteNewer"] = remoteNewer;
                capSummary["localNewer"] = localNewer;
                capSummary["bothModified"] = bothMod;
                capSummary["conflict"] = conflict;
                capSummary["localMissingPaths"] = localMissingPaths;
                capSummary["vaultMissingPaths"] = vaultMissingPaths;
                capSummary["missingPaths"] = localMissingPaths;
                counts["capabilitySummary"] = capSummary;

                d["counts"] = counts;
                d["lockedSessions"] = lockedSessions;
                d["ide"] = ideStatus;
                d["lastOrphanScan"] = host.LastOrphanScanUnsafe != null ? SerializeOrphanScan(host.LastOrphanScanUnsafe) : null;

                Dictionary<string, object> paths = new Dictionary<string, object>();
                paths["home"] = host.HomePath;
                paths["gemini"] = host.GeminiPath;
                paths["repository"] = host.RepoPath;
                paths["vault"] = host.RepoPath;
                d["paths"] = paths;

                d["logs"] = host.BuildRecentLogsListLocked();
                d["job"] = host.GetCurrentJobStatusLocked();
            }
            return d;
        }

        public static Dictionary<string, object> BuildSessionsPayload(LocalWebHost host)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            lock (host.StateLock)
            {
                ProjectGroupsSnapshot groups = host.ProjectGroupsUnsafe;
                d["formalProjectsCount"] = groups.FormalProjectsCount;
                d["totalRawSessions"] = groups.TotalRawSessions;
                d["formalPrimarySessions"] = groups.FormalPrimarySessions;
                d["cliSessions"] = groups.CliSessions;
                d["outsideSessions"] = groups.OutsideSessions;
                d["subagentSessions"] = groups.SubagentSessions;
                d["ephemeralSessions"] = groups.EphemeralSessions;

                List<object> grpArr = new List<object>();
                for (int i = 0; i < groups.Groups.Count; i++)
                {
                    ProjectDisplayGroup g = groups.Groups[i];
                    Dictionary<string, object> gd = new Dictionary<string, object>();
                    gd["bucketKey"] = g.BucketKey;
                    gd["groupKind"] = g.GroupKind;
                    gd["projectName"] = g.ProjectName ?? string.Empty;
                    gd["projectId"] = g.ProjectId ?? string.Empty;
                    gd["fullPath"] = g.FullPath ?? string.Empty;
                    gd["shortPath"] = g.ShortPath ?? string.Empty;
                    gd["distinguishingLabel"] = g.DistinguishingLabel ?? string.Empty;
                    gd["isMultiRoot"] = g.IsMultiRoot;
                    gd["isDuplicateBasename"] = g.IsDuplicateBasename;
                    gd["rootPaths"] = new List<object>(ToObjList(g.RootPaths));
                    gd["primaryCount"] = g.PrimaryCount;
                    gd["totalCount"] = g.TotalCount;

                    List<object> convArr = new List<object>(g.Conversations.Count);
                    for (int k = 0; k < g.Conversations.Count; k++)
                    {
                        convArr.Add(SerializeSessionSummary(g.Conversations[k], host.HomePath));
                    }
                    gd["sessions"] = convArr;
                    grpArr.Add(gd);
                }
                d["groups"] = grpArr;
            }
            return d;
        }

        private static Dictionary<string, object> SerializeSessionSummary(ConversationSummaryInfo s, string homePath)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["cid"] = s.ConversationId ?? string.Empty;
            d["title"] = s.DisplayTitle ?? string.Empty;
            d["projectName"] = s.ProjectName ?? string.Empty;
            d["projectId"] = s.ProjectId ?? string.Empty;
            d["fullPath"] = WorkspaceService.UriToDisplayPath(s.FolderUri);
            d["shortPath"] = WorkspaceService.ShortenHomeDisplayPath(WorkspaceService.UriToDisplayPath(s.FolderUri), homePath);
            d["stepCount"] = s.StepCount;
            d["updatedTime"] = s.UpdatedTime > DateTime.MinValue ? s.UpdatedTime.ToString("yyyy-MM-dd HH:mm") : "-";
            d["updatedIso"] = s.UpdatedTime > DateTime.MinValue ? s.UpdatedTime.ToString("yyyy-MM-ddTHH:mm:ss") : string.Empty;
            d["hasCustomTitle"] = s.HasCustomTitle;
            d["isTagged"] = AutoTitleSynthesizer.HasStandardBracketedPrefix(s.DisplayTitle);
            d["isSubagent"] = s.IsSubagent;
            d["parentConversationId"] = s.ParentConversationId ?? string.Empty;
            d["agentName"] = s.AgentName ?? string.Empty;
            d["isCli"] = s.IsCliSession;
            d["isLegacyCli"] = s is LegacyCliArchiveEntry;
            d["isRunning"] = IsSessionRunning(s);
            d["notFullyIdle"] = s.NotFullyIdle;
            d["firstPromptSnippet"] = s.FirstPromptSnippet ?? string.Empty;
            d["gitBranchHint"] = s.GitBranchHint ?? string.Empty;
            return d;
        }

        public static List<SessionRenameCandidate> BuildAllCandidateObjects(LocalWebHost host)
        {
            string ruleErr;
            RenameRulesConfig rules = AutoTitleSynthesizer.LoadRules(host.RepoPath, out ruleErr);
            List<ConversationSummaryInfo> snap;
            lock (host.StateLock)
            {
                snap = new List<ConversationSummaryInfo>(host.LocalSummariesUnsafe);
            }

            List<SessionRenameCandidate> list = new List<SessionRenameCandidate>(snap.Count);
            for (int i = 0; i < snap.Count; i++)
            {
                ConversationSummaryInfo s = snap[i];
                if (s == null || string.IsNullOrWhiteSpace(s.ConversationId)) continue;

                SessionRenameCandidate c = new SessionRenameCandidate
                {
                    ConversationId = s.ConversationId.Trim(),
                    ProjectName = s.ProjectName ?? string.Empty,
                    ProjectId = s.ProjectId ?? string.Empty,
                    FolderUri = s.FolderUri ?? string.Empty,
                    OldTitle = s.DisplayTitle ?? string.Empty,
                    OldAutoTitle = s.AutoSummaryTitle ?? string.Empty,
                    OldCustomTitle = s.CustomTitle ?? string.Empty,
                    HasCustomTitle = s.HasCustomTitle,
                    FirstPromptSnippet = s.FirstPromptSnippet ?? string.Empty,
                    GitBranchHint = s.GitBranchHint ?? string.Empty,
                    UpdatedTime = s.UpdatedTime,
                    StepCount = s.StepCount,
                    IsCli = s.IsCliSession,
                    Summary = s
                };
                AutoTitleSynthesizer.SynthesizeCandidate(c, rules);

                bool isRunning = IsSessionRunning(s);
                bool isTagged = AutoTitleSynthesizer.HasStandardBracketedPrefix(c.OldTitle);
                bool isJunk = SmartProjectFilter.IsJunkOrEphemeral(c.ProjectName) && !s.IsCliSession;
                bool isOut = VisualDiffEngine.IsOutsideProject(c.ProjectName);
                c.Selected = !s.IsSubagent && !isJunk && !isOut && !s.IsCliSession && !isTagged && !c.IsLowValue && !isRunning;
                list.Add(c);
            }

            list.Sort(delegate(SessionRenameCandidate a, SessionRenameCandidate b)
            {
                return DateTime.Compare(b.UpdatedTime, a.UpdatedTime);
            });
            return list;
        }

        public static Dictionary<string, object> BuildRenameCandidatesPayload(LocalWebHost host)
        {
            Dictionary<string, object> ideStatus = BuildIdeStatusDict();
            List<SessionRenameCandidate> cands = BuildAllCandidateObjects(host);
            List<RenameSnapshotInfo> snaps = RenameBackupManager.ListSnapshots(host.GeminiPath);

            List<object> items = new List<object>(cands.Count);
            List<object> lockedList = new List<object>();
            HashSet<string> projectSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int actionableCount = 0;
            int lockedActiveCount = 0;

            for (int i = 0; i < cands.Count; i++)
            {
                SessionRenameCandidate c = cands[i];
                bool isSub = c.Summary != null && c.Summary.IsSubagent;
                bool isCli = c.Summary != null && c.Summary.IsCliSession;
                bool isOut = VisualDiffEngine.IsOutsideProject(c.ProjectName);
                bool isJunk = SmartProjectFilter.IsJunkOrEphemeral(c.ProjectName) && !isCli;
                bool isTagged = AutoTitleSynthesizer.HasStandardBracketedPrefix(c.OldTitle);
                bool isLocked = IsSessionRunning(c.Summary);
                bool notFullyIdle = c.Summary != null && c.Summary.NotFullyIdle;

                if (!isSub && !isCli && !isOut && !isJunk && !string.IsNullOrEmpty(c.ProjectName))
                {
                    projectSet.Add(c.ProjectName);
                    if (!isTagged)
                    {
                        if (isLocked) lockedActiveCount++;
                        else if (!c.IsLowValue) actionableCount++;
                    }
                }

                if (isLocked)
                {
                    Dictionary<string, object> lk = new Dictionary<string, object>();
                    lk["cid"] = c.ConversationId;
                    lk["title"] = c.OldTitle ?? string.Empty;
                    lk["projectName"] = c.ProjectName ?? string.Empty;
                    lk["runStatus"] = (c.Summary != null && !string.IsNullOrEmpty(c.Summary.RunStatus)) ? c.Summary.RunStatus : "NOT_FULLY_IDLE";
                    lk["notFullyIdle"] = notFullyIdle;
                    lockedList.Add(lk);
                }

                Dictionary<string, object> row = new Dictionary<string, object>();
                row["cid"] = c.ConversationId;
                row["projectName"] = c.ProjectName ?? string.Empty;
                row["projectId"] = c.ProjectId ?? string.Empty;
                row["fullPath"] = WorkspaceService.UriToDisplayPath(c.FolderUri);
                row["shortPath"] = WorkspaceService.ShortenHomeDisplayPath(WorkspaceService.UriToDisplayPath(c.FolderUri), host.HomePath);
                row["oldTitle"] = c.OldTitle ?? string.Empty;
                row["suggestedPrefix"] = c.SuggestedPrefix ?? string.Empty;
                row["suggestedTitle"] = c.SuggestedTitle ?? string.Empty;
                row["firstPromptSnippet"] = c.FirstPromptSnippet ?? string.Empty;
                row["cleanedPrompt"] = c.CleanedPrompt ?? string.Empty;
                row["gitBranchHint"] = c.GitBranchHint ?? string.Empty;
                row["runStatus"] = (c.Summary != null && !string.IsNullOrEmpty(c.Summary.RunStatus)) ? c.Summary.RunStatus : "IDLE";
                row["stepCount"] = c.StepCount;
                row["updatedTime"] = c.UpdatedTime > DateTime.MinValue ? c.UpdatedTime.ToString("yyyy-MM-dd HH:mm") : "-";
                row["selected"] = c.Selected;
                row["isLowValue"] = c.IsLowValue;
                row["isTagged"] = isTagged;
                row["isRunning"] = isLocked;
                row["notFullyIdle"] = notFullyIdle;
                row["isLockedActive"] = isLocked;
                row["isCli"] = isCli;
                row["isOutside"] = isOut;
                row["isSubagent"] = isSub;
                row["isJunk"] = isJunk;
                items.Add(row);
            }

            List<string> projList = new List<string>(projectSet);
            projList.Sort(StringComparer.OrdinalIgnoreCase);

            Dictionary<string, object> resp = new Dictionary<string, object>();
            resp["candidates"] = items;
            resp["formalProjects"] = new List<object>(ToObjList(projList));
            resp["snapshotCount"] = snaps.Count;
            resp["actionableCount"] = actionableCount;
            resp["lockedActiveCount"] = lockedActiveCount;
            resp["lockedSessions"] = lockedList;
            resp["ide"] = ideStatus;
            return resp;
        }

        public static Dictionary<string, object> BuildCapabilitiesPayload(LocalWebHost host, bool fromBackup)
        {
            List<CapabilityDiffNode> diffs;
            lock (host.StateLock)
            {
                diffs = new List<CapabilityDiffNode>(fromBackup ? host.BackupCapDiffsUnsafe : host.CapDiffsUnsafe);
            }
            return SerializeCapabilitiesFromDiffs(diffs, host.RepoPath);
        }

        private static Dictionary<string, object> SerializeCapabilitiesFromDiffs(List<CapabilityDiffNode> diffs, string repoPath)
        {
            List<object> items = new List<object>(diffs.Count);
            for (int i = 0; i < diffs.Count; i++)
            {
                CapabilityDiffNode cdn = diffs[i];
                CapabilityItem pri = cdn.PrimaryItem;
                if (pri == null) continue;

                Dictionary<string, object> row = new Dictionary<string, object>();
                row["key"] = cdn.UniqueKey;
                row["kind"] = CapabilityItem.GetKindShortTag(cdn.Kind);
                row["kindEnum"] = cdn.Kind.ToString();
                row["id"] = cdn.Id ?? string.Empty;
                row["displayName"] = pri.DisplayName ?? cdn.Id ?? string.Empty;
                row["description"] = pri.Description ?? string.Empty;
                row["direction"] = cdn.Direction.ToString();
                row["directionBadge"] = cdn.GetDirectionalBadge();
                row["directionReason"] = cdn.DirectionReason ?? "signature";
                row["legacyStatus"] = cdn.Status.ToString();
                row["localExists"] = cdn.LocalItem != null;
                row["remoteExists"] = cdn.RemoteItem != null;
                row["localTime"] = FormatUtcTime(cdn.LocalItem != null ? cdn.LocalItem.LastModified : DateTime.MinValue);
                row["remoteTime"] = FormatUtcTime(cdn.RemoteItem != null ? cdn.RemoteItem.LastModified : DateTime.MinValue);
                row["hasMissingLocalPaths"] = cdn.LocalItem != null && cdn.LocalItem.HasMissingPaths;
                row["hasMissingRemotePaths"] = cdn.RemoteItem != null && cdn.RemoteItem.HasMissingPaths;
                row["missingLocalPaths"] = cdn.LocalItem != null ? new List<object>(ToObjList(cdn.LocalItem.MissingLocalPaths)) : new List<object>();
                row["missingRemotePaths"] = cdn.RemoteItem != null ? new List<object>(ToObjList(cdn.RemoteItem.MissingLocalPaths)) : new List<object>();
                row["localFilesCount"] = cdn.LocalItem != null && cdn.LocalItem.FilesMap != null ? cdn.LocalItem.FilesMap.Count : 0;
                row["remoteFilesCount"] = cdn.RemoteItem != null && cdn.RemoteItem.FilesMap != null ? cdn.RemoteItem.FilesMap.Count : 0;
                items.Add(row);
            }

            Dictionary<string, object> resp = new Dictionary<string, object>();
            resp["items"] = items;
            resp["repositoryPath"] = repoPath ?? string.Empty;
            resp["vaultPath"] = repoPath ?? string.Empty;
            return resp;
        }

        public static Dictionary<string, object> BuildCapabilityLineDiffPayload(CapabilityDiffNode cdn)
        {
            Dictionary<string, string> localFiles = CollectCapabilityReadableFiles(cdn.LocalItem);
            Dictionary<string, string> remoteFiles = CollectCapabilityReadableFiles(cdn.RemoteItem);

            HashSet<string> allNames = new HashSet<string>(localFiles.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (string rk in remoteFiles.Keys) allNames.Add(rk);
            List<string> sortedNames = new List<string>(allNames);
            sortedNames.Sort(StringComparer.OrdinalIgnoreCase);

            List<object> filesPayload = new List<object>(sortedNames.Count);
            for (int i = 0; i < sortedNames.Count; i++)
            {
                string fn = sortedNames[i];
                string leftText;
                localFiles.TryGetValue(fn, out leftText);
                string rightText;
                remoteFiles.TryGetValue(fn, out rightText);

                bool isBinary = (leftText == "[BINARY]") || (rightText == "[BINARY]");
                List<object> lineObjs = new List<object>();
                bool truncated = false;
                string fileStatus = "equal";

                if (leftText == null && rightText != null) fileStatus = "remote_only";
                else if (leftText != null && rightText == null) fileStatus = "local_only";
                else if (!string.Equals(leftText ?? string.Empty, rightText ?? string.Empty, StringComparison.Ordinal)) fileStatus = "modified";

                if (!isBinary)
                {
                    List<TextDiffLine> lines = VisualDiffEngine.ComputeLineDiff(leftText ?? string.Empty, rightText ?? string.Empty, out truncated);
                    for (int l = 0; l < lines.Count; l++)
                    {
                        Dictionary<string, object> lo = new Dictionary<string, object>();
                        lo["type"] = lines[l].Type;
                        lo["oldLine"] = lines[l].OldLine;
                        lo["newLine"] = lines[l].NewLine;
                        lo["text"] = lines[l].Text ?? string.Empty;
                        lineObjs.Add(lo);
                    }
                }

                Dictionary<string, object> fp = new Dictionary<string, object>();
                fp["path"] = fn;
                fp["status"] = fileStatus;
                fp["isBinary"] = isBinary;
                fp["truncated"] = truncated;
                fp["lines"] = lineObjs;
                filesPayload.Add(fp);
            }

            CapabilityItem pri = cdn.PrimaryItem;
            Dictionary<string, object> resp = new Dictionary<string, object>();
            resp["key"] = cdn.UniqueKey;
            resp["id"] = cdn.Id ?? string.Empty;
            resp["kind"] = CapabilityItem.GetKindShortTag(cdn.Kind);
            resp["direction"] = cdn.Direction.ToString();
            resp["directionBadge"] = cdn.GetDirectionalBadge();
            resp["directionReason"] = cdn.DirectionReason ?? "signature";
            resp["localExists"] = cdn.LocalItem != null;
            resp["remoteExists"] = cdn.RemoteItem != null;
            resp["files"] = filesPayload;
            resp["missingLocalPaths"] = cdn.LocalItem != null ? new List<object>(ToObjList(cdn.LocalItem.MissingLocalPaths)) : new List<object>();
            resp["missingRemotePaths"] = cdn.RemoteItem != null ? new List<object>(ToObjList(cdn.RemoteItem.MissingLocalPaths)) : new List<object>();
            resp["referencedPaths"] = (pri != null && pri.ReferencedLocalPaths != null) ? new List<object>(ToObjList(pri.ReferencedLocalPaths)) : new List<object>();
            return resp;
        }

        public static Dictionary<string, object> BuildBackupPayload(LocalWebHost host)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            lock (host.StateLock)
            {
                BackupArchiveIndex backupIndex = host.BackupIndexUnsafe;
                string loadedBackupPath = host.LoadedBackupPathUnsafe;
                bool loaded = backupIndex != null && !string.IsNullOrEmpty(loadedBackupPath);
                d["loaded"] = loaded;
                if (!loaded) return d;

                d["archivePath"] = loadedBackupPath;
                d["fileName"] = Path.GetFileName(loadedBackupPath);
                d["sourceHome"] = backupIndex.SourceHome ?? string.Empty;
                d["sourceUser"] = backupIndex.SourceUser ?? string.Empty;
                d["backupTime"] = backupIndex.BackupTime ?? string.Empty;
                d["version"] = backupIndex.Version ?? string.Empty;
                d["backupTier"] = backupIndex.BackupTier ?? string.Empty;
                d["sizeMegabytes"] = Math.Round(backupIndex.TotalZipSizeBytes / (1024.0 * 1024.0), 2);
                d["capabilitiesCount"] = backupIndex.Capabilities != null ? backupIndex.Capabilities.Count : 0;
                d["projectsCount"] = backupIndex.Projects != null ? backupIndex.Projects.Count : 0;
                d["conversationsCount"] = backupIndex.Conversations != null ? backupIndex.Conversations.Count : 0;
                d["capabilityDiffs"] = SerializeCapabilitiesFromDiffs(host.BackupCapDiffsUnsafe, host.RepoPath)["items"];

                List<ConversationSummaryInfo> localSums = host.LocalSummariesUnsafe;
                Dictionary<string, ConversationSummaryInfo> localMap = new Dictionary<string, ConversationSummaryInfo>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < localSums.Count; i++)
                {
                    if (localSums[i] != null && !string.IsNullOrEmpty(localSums[i].ConversationId))
                        localMap[localSums[i].ConversationId.Trim()] = localSums[i];
                }

                // Build global backup session map for P0-7 parent-subagent grouping & collapse
                Dictionary<string, ConversationSummaryInfo> allBackupByCid = new Dictionary<string, ConversationSummaryInfo>(StringComparer.OrdinalIgnoreCase);
                if (backupIndex.Conversations != null)
                {
                    for (int i = 0; i < backupIndex.Conversations.Count; i++)
                    {
                        ConversationSummaryInfo c = backupIndex.Conversations[i];
                        if (c != null && !string.IsNullOrEmpty(c.ConversationId))
                            allBackupByCid[c.ConversationId.Trim()] = c;
                    }
                }

                Dictionary<string, List<ConversationSummaryInfo>> childrenByRootParent = new Dictionary<string, List<ConversationSummaryInfo>>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> collapsedChildCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (backupIndex.Conversations != null)
                {
                    for (int i = 0; i < backupIndex.Conversations.Count; i++)
                    {
                        ConversationSummaryInfo c = backupIndex.Conversations[i];
                        if (c == null || string.IsNullOrEmpty(c.ConversationId)) continue;
                        string cid = c.ConversationId.Trim();
                        string rootCid = ResolveRootParentCid(cid, allBackupByCid);
                        if (!string.IsNullOrEmpty(rootCid) && !string.Equals(rootCid, cid, StringComparison.OrdinalIgnoreCase))
                        {
                            collapsedChildCids.Add(cid);
                            List<ConversationSummaryInfo> list;
                            if (!childrenByRootParent.TryGetValue(rootCid, out list))
                            {
                                list = new List<ConversationSummaryInfo>();
                                childrenByRootParent[rootCid] = list;
                            }
                            list.Add(c);
                        }
                    }
                }

                List<ProjectDiffNode> backupSessionDiffs = host.BackupSessionDiffsUnsafe;
                List<object> projArr = new List<object>();
                for (int p = 0; p < backupSessionDiffs.Count; p++)
                {
                    ProjectDiffNode pd = backupSessionDiffs[p];
                    if (pd == null || pd.BackupConversations == null || pd.BackupConversations.Count == 0) continue;

                    List<object> convs = new List<object>();
                    for (int k = 0; k < pd.BackupConversations.Count; k++)
                    {
                        ConversationSummaryInfo bc = pd.BackupConversations[k];
                        if (bc == null) continue;
                        string bcid = bc.ConversationId != null ? bc.ConversationId.Trim() : string.Empty;

                        // P0-7: Collapse subagents whose parent exists in the backup index under their parent session
                        if (!string.IsNullOrEmpty(bcid) && collapsedChildCids.Contains(bcid))
                            continue;

                        ConversationSummaryInfo lc = null;
                        if (!string.IsNullOrEmpty(bcid))
                            localMap.TryGetValue(bcid, out lc);

                        Dictionary<string, object> cd = SerializeBackupSessionEntry(bc, lc, pd);

                        List<ConversationSummaryInfo> childList;
                        if (!string.IsNullOrEmpty(bcid) && childrenByRootParent.TryGetValue(bcid, out childList) && childList != null)
                        {
                            cd["childCount"] = childList.Count;
                            List<object> subCids = new List<object>(childList.Count);
                            List<object> subObjs = new List<object>(childList.Count);
                            for (int ci = 0; ci < childList.Count; ci++)
                            {
                                ConversationSummaryInfo sub = childList[ci];
                                string subCid = sub.ConversationId != null ? sub.ConversationId.Trim() : string.Empty;
                                subCids.Add(subCid);
                                ConversationSummaryInfo subLocal = null;
                                if (!string.IsNullOrEmpty(subCid))
                                    localMap.TryGetValue(subCid, out subLocal);
                                subObjs.Add(SerializeBackupSessionEntry(sub, subLocal, pd));
                            }
                            cd["subagentCids"] = subCids;
                            cd["children"] = subObjs;
                        }
                        else
                        {
                            cd["childCount"] = 0;
                            cd["subagentCids"] = new List<object>();
                            cd["children"] = new List<object>();
                        }

                        convs.Add(cd);
                    }

                    if (convs.Count == 0) continue;

                    Dictionary<string, object> projObj = new Dictionary<string, object>();
                    projObj["projectName"] = pd.ProjectName ?? string.Empty;
                    projObj["status"] = pd.Status.ToString();
                    projObj["shortPath"] = WorkspaceService.ShortenHomeDisplayPath(WorkspaceService.UriToDisplayPath(pd.BackupFolderUri), backupIndex.SourceHome);
                    projObj["totalCount"] = pd.BackupConversations.Count;
                    projObj["topLevelCount"] = convs.Count;
                    projObj["sessions"] = convs;
                    projArr.Add(projObj);
                }
                d["sessionDiffGroups"] = projArr;
            }
            return d;
        }

        private static string ResolveRootParentCid(string cid, Dictionary<string, ConversationSummaryInfo> byCid)
        {
            if (string.IsNullOrEmpty(cid) || byCid == null) return null;
            string current = cid;
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            visited.Add(current);

            while (true)
            {
                ConversationSummaryInfo info;
                if (!byCid.TryGetValue(current, out info) || info == null) break;
                string parent = info.ParentConversationId != null ? info.ParentConversationId.Trim() : string.Empty;
                if (string.IsNullOrEmpty(parent) || !byCid.ContainsKey(parent) || !visited.Add(parent))
                    break;
                current = parent;
            }

            return string.Equals(current, cid, StringComparison.OrdinalIgnoreCase) ? null : current;
        }

        private static Dictionary<string, object> SerializeBackupSessionEntry(
            ConversationSummaryInfo bc,
            ConversationSummaryInfo lc,
            ProjectDiffNode pd)
        {
            // FIX P1-9: Use VisualDiffEngine.EvaluateConversationDiff as single source of truth
            ConversationDiffDetail diffDetail = VisualDiffEngine.EvaluateConversationDiff(bc, lc, pd);
            string statusLabel;
            switch (diffDetail.Direction)
            {
                case SessionDiffDirection.OnlyInBackup: statusLabel = "New"; break;
                case SessionDiffDirection.BackupNewer: statusLabel = "Newer"; break;
                case SessionDiffDirection.LocalNewer: statusLabel = "LocalNewer"; break;
                case SessionDiffDirection.Modified: statusLabel = "Modified"; break;
                case SessionDiffDirection.OnlyInLocal: statusLabel = "LocalOnly"; break;
                default: statusLabel = "Aligned"; break;
            }

            Dictionary<string, object> cd = new Dictionary<string, object>();
            cd["cid"] = bc.ConversationId ?? string.Empty;
            cd["title"] = bc.DisplayTitle ?? string.Empty;
            cd["projectName"] = (pd != null ? pd.ProjectName : bc.ProjectName) ?? string.Empty;
            cd["stepCount"] = bc.StepCount;
            cd["localStepCount"] = lc != null ? lc.StepCount : -1;
            cd["updatedTime"] = bc.UpdatedTime > DateTime.MinValue ? bc.UpdatedTime.ToString("yyyy-MM-dd HH:mm") : "-";
            cd["status"] = statusLabel;
            cd["direction"] = diffDetail.Direction.ToString();
            cd["directionReason"] = diffDetail.DirectionReason ?? string.Empty;
            cd["autoSelected"] = BackupSelectionPolicy.ShouldAutoSelectBackupSession(bc, lc, pd);
            cd["isSubagent"] = bc.IsSubagent;
            cd["parentConversationId"] = bc.ParentConversationId ?? string.Empty;
            return cd;
        }

        private static Dictionary<string, string> CollectCapabilityReadableFiles(CapabilityItem item)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (item == null) return dict;

            if (!string.IsNullOrEmpty(item.RawJsonFragment))
            {
                dict["_config_registration.json"] = item.RawJsonFragment;
            }
            if (item.FilesMap != null)
            {
                foreach (string relKey in item.FilesMap.Keys)
                {
                    byte[] bytes = item.GetFileBytes(relKey);
                    if (bytes == null)
                    {
                        dict[relKey] = string.Empty;
                        continue;
                    }
                    if (bytes.Length > 512 * 1024)
                    {
                        dict[relKey] = string.Format("[文件过大 ({0:F1} KB)，请打开源码目录查看]", bytes.Length / 1024.0);
                        continue;
                    }
                    if (LooksBinary(bytes))
                    {
                        dict[relKey] = "[BINARY]";
                        continue;
                    }
                    string text = Encoding.UTF8.GetString(bytes);
                    if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
                    dict[relKey] = text;
                }
            }
            return dict;
        }

        private static bool LooksBinary(byte[] bytes)
        {
            int checkLen = Math.Min(bytes.Length, 4096);
            for (int i = 0; i < checkLen; i++)
            {
                if (bytes[i] == 0) return true;
            }
            return false;
        }

        internal static List<object> ToObjList(IEnumerable<string> seq)
        {
            List<object> list = new List<object>();
            if (seq == null) return list;
            foreach (string s in seq) list.Add(s ?? string.Empty);
            return list;
        }

        private static string FormatUtcTime(DateTime dt)
        {
            if (dt == DateTime.MinValue || dt.Year < 1980) return "-";
            DateTime local = dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
            return local.ToString("yyyy-MM-dd HH:mm");
        }
    }
}
