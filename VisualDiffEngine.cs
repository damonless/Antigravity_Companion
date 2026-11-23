using System;
using System.Collections.Generic;
using System.IO;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // VISUAL DIFF ENGINE & CONFLICT RESOLUTION (Milestone 2 — F6, F7, F11)
    // ══════════════════════════════════════════════════════════════════════

    public enum DiffStatus
    {
        NewInBackup,    // [新增工程 / 含新增会话]
        Aligned,        // [已对齐]
        LocalOnly,      // [本地独有]
        Modified        // [已修改 / 内容有更新 / 已改名]
    }

    public enum ItemCategory
    {
        Project,
        Conversation,
        CapabilityCategoryGroup,
        Capability
    }

    public class DiffItemNodeTag
    {
        public ItemCategory Category;
        public string Id;                   // UUID or Config Key
        public string DisplayName;          // Title or Name
        public string ProjectName;          // Project Name
        public string SourcePath;           // Origin path
        public string TargetPath;           // Local mapped target path
        public DateTime? Timestamp;         // Last modified timestamp
        public DiffStatus Status;           // Visual diff status
        public bool IsTemporaryOrJunk;      // Ephemeral / Junk / Subagent
        public ConversationSummaryInfo SummaryInfo;
        public ProjectDiffNode ProjectNode;
        public CapabilityKind? CapKind;
        public CapabilityItem CapabilityInfo;
        public CapabilityDiffNode CapabilityDiff;
        public object TagData;              // Additional metadata
    }

    public enum CapabilityDiffDirection
    {
        Aligned,
        RemoteOnly,
        LocalOnly,
        LocalNewer,
        RemoteNewer,
        BothModified
    }

    public class TextDiffLine
    {
        public string Type; // "equal", "delete", "insert"
        public int OldLine;
        public int NewLine;
        public string Text;
    }

    public class CapabilityDiffNode
    {
        public CapabilityKind Kind;
        public string Id;
        public string UniqueKey;
        public CapabilityItem LocalItem;
        public CapabilityItem RemoteItem;
        public DiffStatus Status;
        public CapabilityDiffDirection Direction;
        public string DirectionReason;

        public CapabilityItem PrimaryItem
        {
            get { return RemoteItem ?? LocalItem; }
        }

        public string GetStatusBadge()
        {
            bool remoteMiss = RemoteItem != null && RemoteItem.HasMissingPaths;
            bool localMiss = LocalItem != null && LocalItem.HasMissingPaths;
            string warn = string.Empty;
            if (remoteMiss && localMiss) warn = " [⚠️双侧路径缺失]";
            else if (remoteMiss) warn = " [⚠️路径缺失]";
            else if (localMiss) warn = " [⚠️本机路径缺失]";

            switch (Status)
            {
                case DiffStatus.NewInBackup: return "[新增]" + warn;
                case DiffStatus.Modified: return "[内容有更新]" + warn;
                case DiffStatus.Aligned: return "[已对齐]" + warn;
                case DiffStatus.LocalOnly: return "[本机独有]" + warn;
                default: return warn.Trim();
            }
        }

        public string GetDirectionalBadge()
        {
            switch (Direction)
            {
                case CapabilityDiffDirection.Aligned: return "已一致";
                case CapabilityDiffDirection.RemoteOnly: return "仅仓库";
                case CapabilityDiffDirection.LocalOnly: return "仅本机";
                case CapabilityDiffDirection.RemoteNewer: return "仓库较新";
                case CapabilityDiffDirection.LocalNewer: return "本机较新";
                case CapabilityDiffDirection.BothModified: return "双方差异";
                default: return "未知";
            }
        }
    }

    public class ConversationDiffDetail
    {
        public bool IsModified;
        public bool TitleDiff;
        public bool StepDiff;
        public bool TimeDiff;
        public bool ProjectDiff;
    }

    public class ProjectDiffNode
    {
        public string ProjectName;
        public string LocalFolderUri;
        public string BackupFolderUri;
        public DiffStatus Status;
        public int NewSessionCount;
        public int ModifiedSessionCount;
        public int LocalOnlySessionCount;
        public string RenamedFromOrTo;
        public bool IsTemporaryOrJunk;
        public List<ConversationSummaryInfo> LocalConversations;
        public List<ConversationSummaryInfo> BackupConversations;

        public ProjectDiffNode(string name)
        {
            ProjectName = name;
            LocalConversations = new List<ConversationSummaryInfo>();
            BackupConversations = new List<ConversationSummaryInfo>();
            IsTemporaryOrJunk = SmartProjectFilter.IsJunkOrEphemeral(name);
            NewSessionCount = 0;
            ModifiedSessionCount = 0;
            LocalOnlySessionCount = 0;
        }

        public string GetStatusBadge()
        {
            switch (Status)
            {
                case DiffStatus.NewInBackup:
                    if (LocalConversations.Count == 0 && string.IsNullOrEmpty(RenamedFromOrTo))
                        return "[新增工程]";
                    if (!string.IsNullOrEmpty(RenamedFromOrTo))
                    {
                        if (ModifiedSessionCount > 0)
                            return string.Format("[工程已改名: 本地为 '{0}', 含 {1} 新增, {2} 更新]", RenamedFromOrTo, NewSessionCount, ModifiedSessionCount);
                        return string.Format("[工程已改名: 本地为 '{0}', 含 {1} 条新增]", RenamedFromOrTo, NewSessionCount);
                    }
                    if (ModifiedSessionCount > 0)
                        return string.Format("[含 {0} 新增, {1} 更新]", NewSessionCount, ModifiedSessionCount);
                    return string.Format("[含 {0} 条新增]", NewSessionCount);
                case DiffStatus.Modified:
                    if (!string.IsNullOrEmpty(RenamedFromOrTo))
                    {
                        if (ModifiedSessionCount > 0)
                            return string.Format("[工程已改名: 本地为 '{0}', 含 {1} 更新]", RenamedFromOrTo, ModifiedSessionCount);
                        return string.Format("[工程已改名: 本地为 '{0}']", RenamedFromOrTo);
                    }
                    return string.Format("[含 {0} 条已修改/更新]", ModifiedSessionCount);
                case DiffStatus.Aligned:
                    if (LocalOnlySessionCount > 0)
                        return string.Format("[已对齐 · 本机另含 {0} 条独有]", LocalOnlySessionCount);
                    return "[已对齐]";
                case DiffStatus.LocalOnly:
                    return "[本地独有]";
                default:
                    return string.Empty;
            }
        }
    }

    public static class VisualDiffEngine
    {
        private class ProjectBucket
        {
            public string ProjectName;
            public string RawFolderUri;
            public string NormalizedFolderUri;
            public HashSet<string> ProjectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> ConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public List<ConversationSummaryInfo> Conversations = new List<ConversationSummaryInfo>();
            public bool Matched;
        }

        private class BucketPair
        {
            public ProjectBucket Backup;
            public ProjectBucket Local;
            public bool IsRenamed;
        }

        private static string NormalizeProjectNameOrOutside(string projectName)
        {
            return string.IsNullOrWhiteSpace(projectName) ? "Outside of Project" : projectName.Trim();
        }

        public static bool IsOutsideProject(string projectName)
        {
            return string.Equals(NormalizeProjectNameOrOutside(projectName), "Outside of Project", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRealProjectId(string projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId)) return false;
            string trimmed = projectId.Trim();
            if (string.Equals(trimmed, "outside-of-project", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(trimmed, "outside_of_project", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        public static string RemapAndNormalizeBackupUri(string rawUri, string sourceHomePath, string currentHomePath)
        {
            if (string.IsNullOrEmpty(rawUri)) return string.Empty;
            string candidate = rawUri;
            if (!string.IsNullOrEmpty(sourceHomePath) && !string.IsNullOrEmpty(currentHomePath))
            {
                string srcTrim = sourceHomePath.TrimEnd('\\', '/');
                string curTrim = currentHomePath.TrimEnd('\\', '/');
                if (!string.Equals(srcTrim, curTrim, StringComparison.OrdinalIgnoreCase))
                {
                    candidate = DirectStreamArchiver.RemapAllPathFormats(candidate, sourceHomePath, currentHomePath);
                }
            }
            return DirectStreamArchiver.NormalizeUri(candidate);
        }

        /// <summary>
        /// 单一事实比对函数：判定备份会话与本地会话之间是否存在实质差异，同时正确排除“父工程已合并改名”导致的伪 projDiff
        /// </summary>
        public static ConversationDiffDetail EvaluateConversationDiff(
            ConversationSummaryInfo backupConv,
            ConversationSummaryInfo localConv,
            ProjectDiffNode parentProjectNode)
        {
            ConversationDiffDetail detail = new ConversationDiffDetail();
            if (backupConv == null || localConv == null) return detail;

            detail.TitleDiff = !string.Equals(backupConv.DisplayTitle ?? string.Empty, localConv.DisplayTitle ?? string.Empty, StringComparison.Ordinal);
            detail.StepDiff = backupConv.StepCount != localConv.StepCount || backupConv.UserInteractions != localConv.UserInteractions;
            detail.TimeDiff = (Math.Abs((backupConv.UpdatedTime - localConv.UpdatedTime).TotalSeconds) > 2 &&
                              backupConv.UpdatedTime > DateTime.MinValue &&
                              localConv.UpdatedTime > DateTime.MinValue) ||
                              (backupConv.UpdatedTime > DateTime.MinValue && localConv.UpdatedTime == DateTime.MinValue) ||
                              (localConv.UpdatedTime > DateTime.MinValue && backupConv.UpdatedTime == DateTime.MinValue);

            string bProj = NormalizeProjectNameOrOutside(backupConv.ProjectName);
            string lProj = NormalizeProjectNameOrOutside(localConv.ProjectName);
            bool projNameDiff = !string.Equals(bProj, lProj, StringComparison.OrdinalIgnoreCase);

            if (projNameDiff && parentProjectNode != null && !string.IsNullOrEmpty(parentProjectNode.RenamedFromOrTo))
            {
                if (string.Equals(bProj, NormalizeProjectNameOrOutside(parentProjectNode.ProjectName), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(lProj, NormalizeProjectNameOrOutside(parentProjectNode.RenamedFromOrTo), StringComparison.OrdinalIgnoreCase))
                {
                    projNameDiff = false;
                }
            }

            detail.ProjectDiff = projNameDiff;
            detail.IsModified = detail.TitleDiff || detail.StepDiff || detail.TimeDiff || detail.ProjectDiff;
            return detail;
        }

        public static List<ProjectDiffNode> ComputeDiff(
            List<ConversationSummaryInfo> localSummaries,
            List<ConversationSummaryInfo> backupSummaries)
        {
            return ComputeDiff(localSummaries, backupSummaries, null, null);
        }

        public static List<ProjectDiffNode> ComputeDiff(
            List<ConversationSummaryInfo> localSummaries,
            List<ConversationSummaryInfo> backupSummaries,
            string sourceHomePath,
            string currentHomePath)
        {
            Dictionary<string, ProjectBucket> localBuckets = new Dictionary<string, ProjectBucket>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ProjectBucket> backupBuckets = new Dictionary<string, ProjectBucket>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ConversationSummaryInfo> localConvMap = new Dictionary<string, ConversationSummaryInfo>(StringComparer.OrdinalIgnoreCase);

            if (localSummaries != null)
            {
                for (int i = 0; i < localSummaries.Count; i++)
                {
                    ConversationSummaryInfo s = localSummaries[i];
                    if (s == null) continue;
                    string p = NormalizeProjectNameOrOutside(s.ProjectName);

                    ProjectBucket bucket;
                    if (!localBuckets.TryGetValue(p, out bucket))
                    {
                        bucket = new ProjectBucket { ProjectName = p };
                        localBuckets[p] = bucket;
                    }
                    bucket.Conversations.Add(s);
                    if (string.IsNullOrEmpty(bucket.RawFolderUri) && !string.IsNullOrEmpty(s.FolderUri))
                    {
                        bucket.RawFolderUri = s.FolderUri;
                        bucket.NormalizedFolderUri = DirectStreamArchiver.NormalizeUri(s.FolderUri);
                    }
                    if (IsRealProjectId(s.ProjectId))
                        bucket.ProjectIds.Add(s.ProjectId.Trim());
                    if (!string.IsNullOrEmpty(s.ConversationId))
                    {
                        string cid = s.ConversationId.Trim();
                        bucket.ConvIds.Add(cid);
                        localConvMap[cid] = s;
                    }
                }
            }

            if (backupSummaries != null)
            {
                for (int i = 0; i < backupSummaries.Count; i++)
                {
                    ConversationSummaryInfo s = backupSummaries[i];
                    if (s == null) continue;
                    string p = NormalizeProjectNameOrOutside(s.ProjectName);

                    ProjectBucket bucket;
                    if (!backupBuckets.TryGetValue(p, out bucket))
                    {
                        bucket = new ProjectBucket { ProjectName = p };
                        backupBuckets[p] = bucket;
                    }
                    bucket.Conversations.Add(s);
                    if (string.IsNullOrEmpty(bucket.RawFolderUri) && !string.IsNullOrEmpty(s.FolderUri))
                    {
                        bucket.RawFolderUri = s.FolderUri;
                        bucket.NormalizedFolderUri = RemapAndNormalizeBackupUri(s.FolderUri, sourceHomePath, currentHomePath);
                    }
                    if (IsRealProjectId(s.ProjectId))
                        bucket.ProjectIds.Add(s.ProjectId.Trim());
                    if (!string.IsNullOrEmpty(s.ConversationId))
                        bucket.ConvIds.Add(s.ConversationId.Trim());
                }
            }

            List<BucketPair> pairs = new List<BucketPair>();

            // 第 1 阶段：同名工程精确配对（含 Outside of Project；排除已配对工程进入改名候选池）
            foreach (KeyValuePair<string, ProjectBucket> kvp in backupBuckets)
            {
                ProjectBucket b = kvp.Value;
                ProjectBucket l;
                if (localBuckets.TryGetValue(b.ProjectName, out l))
                {
                    b.Matched = true;
                    l.Matched = true;
                    pairs.Add(new BucketPair { Backup = b, Local = l, IsRenamed = false });
                }
            }

            // 构造尚未配对的非 Outside 本地候选工程列表
            List<ProjectBucket> unmatchedLocal = new List<ProjectBucket>();
            foreach (KeyValuePair<string, ProjectBucket> kvp in localBuckets)
            {
                ProjectBucket l = kvp.Value;
                if (!l.Matched && !IsOutsideProject(l.ProjectName))
                {
                    unmatchedLocal.Add(l);
                }
            }

            // 第 2 阶段：未配对非 Outside 备份工程按 3 级优先顺序识别本地改名工程
            //   Tier 1: ProjectId 精确一致
            //   Tier 2: 跨机重映射后的 NormalizedFolderUri 一致
            //   Tier 3: 共享 ConversationId UUID 重叠最多
            foreach (KeyValuePair<string, ProjectBucket> kvp in backupBuckets)
            {
                ProjectBucket b = kvp.Value;
                if (b.Matched || IsOutsideProject(b.ProjectName)) continue;

                ProjectBucket matchedLocal = FindBestRenamedLocalMatch(b, unmatchedLocal);
                if (matchedLocal != null)
                {
                    b.Matched = true;
                    matchedLocal.Matched = true;
                    pairs.Add(new BucketPair { Backup = b, Local = matchedLocal, IsRenamed = true });
                }
            }

            // 第 3 阶段：剩余未配对的纯新增备份工程与纯本地独有工程
            foreach (KeyValuePair<string, ProjectBucket> kvp in backupBuckets)
            {
                if (!kvp.Value.Matched)
                    pairs.Add(new BucketPair { Backup = kvp.Value, Local = null, IsRenamed = false });
            }

            foreach (KeyValuePair<string, ProjectBucket> kvp in localBuckets)
            {
                if (!kvp.Value.Matched)
                    pairs.Add(new BucketPair { Backup = null, Local = kvp.Value, IsRenamed = false });
            }

            // 组装合并后的 ProjectDiffNode 列表
            List<ProjectDiffNode> results = new List<ProjectDiffNode>();
            for (int i = 0; i < pairs.Count; i++)
            {
                BucketPair pair = pairs[i];
                ProjectBucket bBucket = pair.Backup;
                ProjectBucket lBucket = pair.Local;

                string primaryName = bBucket != null ? bBucket.ProjectName : lBucket.ProjectName;
                ProjectDiffNode node = new ProjectDiffNode(primaryName);
                if (bBucket != null)
                {
                    node.BackupConversations.AddRange(bBucket.Conversations);
                    node.BackupFolderUri = bBucket.RawFolderUri;
                }
                if (lBucket != null)
                {
                    node.LocalConversations.AddRange(lBucket.Conversations);
                    node.LocalFolderUri = lBucket.RawFolderUri;
                }
                if (pair.IsRenamed && lBucket != null)
                {
                    node.RenamedFromOrTo = lBucket.ProjectName;
                }

                int newConvs = 0;
                int modifiedConvs = 0;

                for (int b = 0; b < node.BackupConversations.Count; b++)
                {
                    ConversationSummaryInfo bConv = node.BackupConversations[b];
                    if (bConv == null) continue;
                    string bCid = bConv.ConversationId;
                    if (string.IsNullOrEmpty(bCid)) continue;

                    ConversationSummaryInfo lConv;
                    if (!localConvMap.TryGetValue(bCid.Trim(), out lConv))
                    {
                        newConvs++;
                    }
                    else
                    {
                        ConversationDiffDetail d = EvaluateConversationDiff(bConv, lConv, node);
                        if (d.IsModified)
                            modifiedConvs++;
                    }
                }

                int localOnlyConvs = 0;
                for (int l = 0; l < node.LocalConversations.Count; l++)
                {
                    ConversationSummaryInfo lConv = node.LocalConversations[l];
                    if (lConv == null) continue;
                    string lCid = lConv.ConversationId != null ? lConv.ConversationId.Trim() : string.Empty;
                    if (string.IsNullOrEmpty(lCid) || bBucket == null || !bBucket.ConvIds.Contains(lCid))
                    {
                        localOnlyConvs++;
                    }
                }

                node.NewSessionCount = newConvs;
                node.ModifiedSessionCount = modifiedConvs;
                node.LocalOnlySessionCount = localOnlyConvs;

                bool hasLocal = lBucket != null && node.LocalConversations.Count > 0;
                bool hasBackup = bBucket != null && node.BackupConversations.Count > 0;

                if (!hasLocal && hasBackup)
                {
                    node.Status = DiffStatus.NewInBackup;
                }
                else if (hasLocal && !hasBackup)
                {
                    node.Status = DiffStatus.LocalOnly;
                }
                else
                {
                    if (newConvs > 0)
                        node.Status = DiffStatus.NewInBackup;
                    else if (modifiedConvs > 0 || pair.IsRenamed)
                        node.Status = DiffStatus.Modified;
                    else
                        node.Status = DiffStatus.Aligned;
                }

                results.Add(node);
            }

            results.Sort((a, b) => string.Compare(a.ProjectName, b.ProjectName, StringComparison.OrdinalIgnoreCase));
            return results;
        }

        private static ProjectBucket FindBestRenamedLocalMatch(ProjectBucket backupBucket, List<ProjectBucket> unmatchedLocal)
        {
            if (backupBucket == null || unmatchedLocal == null || unmatchedLocal.Count == 0)
                return null;

            // Tier 1: ProjectId
            if (backupBucket.ProjectIds.Count > 0)
            {
                for (int i = 0; i < unmatchedLocal.Count; i++)
                {
                    ProjectBucket cand = unmatchedLocal[i];
                    if (cand.Matched) continue;
                    foreach (string pid in backupBucket.ProjectIds)
                    {
                        if (cand.ProjectIds.Contains(pid))
                            return cand;
                    }
                }
            }

            // Tier 2: Remapped NormalizedFolderUri
            if (!string.IsNullOrEmpty(backupBucket.NormalizedFolderUri))
            {
                for (int i = 0; i < unmatchedLocal.Count; i++)
                {
                    ProjectBucket cand = unmatchedLocal[i];
                    if (cand.Matched || string.IsNullOrEmpty(cand.NormalizedFolderUri)) continue;
                    if (string.Equals(backupBucket.NormalizedFolderUri, cand.NormalizedFolderUri, StringComparison.OrdinalIgnoreCase))
                        return cand;
                }
            }

            // Tier 3: Shared ConversationId overlap
            if (backupBucket.ConvIds.Count > 0)
            {
                ProjectBucket best = null;
                int bestOverlap = 0;
                for (int i = 0; i < unmatchedLocal.Count; i++)
                {
                    ProjectBucket cand = unmatchedLocal[i];
                    if (cand.Matched || cand.ConvIds.Count == 0) continue;

                    int overlap = 0;
                    foreach (string cid in backupBucket.ConvIds)
                    {
                        if (cand.ConvIds.Contains(cid))
                            overlap++;
                    }
                    if (overlap > bestOverlap)
                    {
                        bestOverlap = overlap;
                        best = cand;
                    }
                }
                if (best != null && bestOverlap > 0)
                    return best;
            }

            return null;
        }

        public static List<CapabilityDiffNode> ComputeCapabilityDiff(
            List<CapabilityItem> localCaps,
            List<CapabilityItem> remoteCaps)
        {
            return ComputeCapabilityDiff(localCaps, remoteCaps, null);
        }

        public static List<CapabilityDiffNode> ComputeCapabilityDiff(
            List<CapabilityItem> localCaps,
            List<CapabilityItem> remoteCaps,
            Dictionary<string, string> baselineSignatures)
        {
            Dictionary<string, CapabilityDiffNode> map = new Dictionary<string, CapabilityDiffNode>(StringComparer.OrdinalIgnoreCase);

            if (localCaps != null)
            {
                for (int i = 0; i < localCaps.Count; i++)
                {
                    CapabilityItem lc = localCaps[i];
                    if (lc == null) continue;
                    string key = lc.UniqueKey;
                    CapabilityDiffNode node;
                    if (!map.TryGetValue(key, out node))
                    {
                        node = new CapabilityDiffNode { Kind = lc.Kind, Id = lc.Id, UniqueKey = key };
                        map[key] = node;
                    }
                    node.LocalItem = lc;
                }
            }

            if (remoteCaps != null)
            {
                for (int i = 0; i < remoteCaps.Count; i++)
                {
                    CapabilityItem rc = remoteCaps[i];
                    if (rc == null) continue;
                    string key = rc.UniqueKey;
                    CapabilityDiffNode node;
                    if (!map.TryGetValue(key, out node))
                    {
                        node = new CapabilityDiffNode { Kind = rc.Kind, Id = rc.Id, UniqueKey = key };
                        map[key] = node;
                    }
                    node.RemoteItem = rc;
                }
            }

            List<CapabilityDiffNode> results = new List<CapabilityDiffNode>();
            foreach (KeyValuePair<string, CapabilityDiffNode> kvp in map)
            {
                CapabilityDiffNode n = kvp.Value;
                if (n.LocalItem == null && n.RemoteItem != null)
                {
                    n.Status = DiffStatus.NewInBackup;
                    n.Direction = CapabilityDiffDirection.RemoteOnly;
                    n.DirectionReason = "presence";
                }
                else if (n.LocalItem != null && n.RemoteItem == null)
                {
                    n.Status = DiffStatus.LocalOnly;
                    n.Direction = CapabilityDiffDirection.LocalOnly;
                    n.DirectionReason = "presence";
                }
                else
                {
                    if (string.IsNullOrEmpty(n.LocalItem.ContentSignature))
                        CapabilityRepositoryManager.EnsureContentSignature(n.LocalItem, null, null);
                    if (string.IsNullOrEmpty(n.RemoteItem.ContentSignature))
                        CapabilityRepositoryManager.EnsureContentSignature(n.RemoteItem, null, null);

                    string localSig = n.LocalItem.ContentSignature ?? string.Empty;
                    string remoteSig = n.RemoteItem.ContentSignature ?? string.Empty;
                    bool sigMatch = localSig.Length > 0 && remoteSig.Length > 0 &&
                                    string.Equals(localSig, remoteSig, StringComparison.OrdinalIgnoreCase);

                    if (sigMatch)
                    {
                        n.Status = DiffStatus.Aligned;
                        n.Direction = CapabilityDiffDirection.Aligned;
                        n.DirectionReason = "signature";
                    }
                    else
                    {
                        n.Status = DiffStatus.Modified;
                        ResolveModifiedDirection(n, localSig, remoteSig, baselineSignatures);
                    }
                }
                results.Add(n);
            }

            results.Sort((a, b) =>
            {
                int cmpKind = a.Kind.CompareTo(b.Kind);
                if (cmpKind != 0) return cmpKind;
                return string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
            });
            return results;
        }

        private static DateTime ToUtcSafe(DateTime dt)
        {
            if (dt == DateTime.MinValue || dt.Year < 1980) return DateTime.MinValue;
            if (dt.Kind == DateTimeKind.Utc) return dt;
            return DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime();
        }

        private static void ResolveModifiedDirection(
            CapabilityDiffNode n,
            string localSig,
            string remoteSig,
            Dictionary<string, string> baselineSignatures)
        {
            string baseSig;
            if (baselineSignatures != null && baselineSignatures.TryGetValue(n.UniqueKey, out baseSig) &&
                !string.IsNullOrEmpty(baseSig))
            {
                bool localSameBase = string.Equals(localSig, baseSig, StringComparison.OrdinalIgnoreCase);
                bool remoteSameBase = string.Equals(remoteSig, baseSig, StringComparison.OrdinalIgnoreCase);
                if (localSameBase && !remoteSameBase)
                {
                    n.Direction = CapabilityDiffDirection.RemoteNewer;
                    n.DirectionReason = "baseline";
                    return;
                }
                if (remoteSameBase && !localSameBase)
                {
                    n.Direction = CapabilityDiffDirection.LocalNewer;
                    n.DirectionReason = "baseline";
                    return;
                }
                n.Direction = CapabilityDiffDirection.BothModified;
                n.DirectionReason = "baseline";
                return;
            }

            DateTime localUtc = ToUtcSafe(n.LocalItem.LastModified);
            DateTime remoteUtc = ToUtcSafe(n.RemoteItem.LastModified);
            if (localUtc != DateTime.MinValue && remoteUtc != DateTime.MinValue)
            {
                double diffSec = (remoteUtc - localUtc).TotalSeconds;
                if (diffSec > 3.0)
                {
                    n.Direction = CapabilityDiffDirection.RemoteNewer;
                    n.DirectionReason = "mtime";
                    return;
                }
                if (diffSec < -3.0)
                {
                    n.Direction = CapabilityDiffDirection.LocalNewer;
                    n.DirectionReason = "mtime";
                    return;
                }
                n.Direction = CapabilityDiffDirection.BothModified;
                n.DirectionReason = "mtime";
                return;
            }

            n.Direction = CapabilityDiffDirection.BothModified;
            n.DirectionReason = "signature";
        }

        public static List<TextDiffLine> ComputeLineDiff(string leftText, string rightText, out bool truncated)
        {
            truncated = false;
            string[] leftLines = SplitNormalizedLines(leftText);
            string[] rightLines = SplitNormalizedLines(rightText);

            int prefix = 0;
            while (prefix < leftLines.Length && prefix < rightLines.Length &&
                   string.Equals(leftLines[prefix], rightLines[prefix], StringComparison.Ordinal))
            {
                prefix++;
            }

            int suffix = 0;
            while (suffix < leftLines.Length - prefix && suffix < rightLines.Length - prefix &&
                   string.Equals(leftLines[leftLines.Length - 1 - suffix], rightLines[rightLines.Length - 1 - suffix], StringComparison.Ordinal))
            {
                suffix++;
            }

            List<TextDiffLine> result = new List<TextDiffLine>();
            for (int i = 0; i < prefix; i++)
            {
                result.Add(new TextDiffLine { Type = "equal", OldLine = i + 1, NewLine = i + 1, Text = leftLines[i] });
            }

            int midLeftLen = leftLines.Length - prefix - suffix;
            int midRightLen = rightLines.Length - prefix - suffix;

            if (midLeftLen > 0 || midRightLen > 0)
            {
                if (midLeftLen > 1000 || midRightLen > 1000 || ((long)midLeftLen * (long)midRightLen) > 450000L)
                {
                    truncated = true;
                    int emitLeft = Math.Min(midLeftLen, 400);
                    int emitRight = Math.Min(midRightLen, 400);
                    for (int i = 0; i < emitLeft; i++)
                    {
                        result.Add(new TextDiffLine { Type = "delete", OldLine = prefix + i + 1, NewLine = 0, Text = leftLines[prefix + i] });
                    }
                    if (emitLeft < midLeftLen)
                    {
                        result.Add(new TextDiffLine { Type = "delete", OldLine = prefix + emitLeft + 1, NewLine = 0, Text = "... (" + (midLeftLen - emitLeft) + " more removed lines omitted)" });
                    }
                    for (int j = 0; j < emitRight; j++)
                    {
                        result.Add(new TextDiffLine { Type = "insert", OldLine = 0, NewLine = prefix + j + 1, Text = rightLines[prefix + j] });
                    }
                    if (emitRight < midRightLen)
                    {
                        result.Add(new TextDiffLine { Type = "insert", OldLine = 0, NewLine = prefix + emitRight + 1, Text = "... (" + (midRightLen - emitRight) + " more added lines omitted)" });
                    }
                }
                else
                {
                    int[,] dp = new int[midLeftLen + 1, midRightLen + 1];
                    for (int i = midLeftLen - 1; i >= 0; i--)
                    {
                        for (int j = midRightLen - 1; j >= 0; j--)
                        {
                            if (string.Equals(leftLines[prefix + i], rightLines[prefix + j], StringComparison.Ordinal))
                                dp[i, j] = dp[i + 1, j + 1] + 1;
                            else
                                dp[i, j] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
                        }
                    }

                    int a = 0;
                    int b = 0;
                    while (a < midLeftLen && b < midRightLen)
                    {
                        if (string.Equals(leftLines[prefix + a], rightLines[prefix + b], StringComparison.Ordinal))
                        {
                            result.Add(new TextDiffLine { Type = "equal", OldLine = prefix + a + 1, NewLine = prefix + b + 1, Text = leftLines[prefix + a] });
                            a++;
                            b++;
                        }
                        else if (dp[a + 1, b] >= dp[a, b + 1])
                        {
                            result.Add(new TextDiffLine { Type = "delete", OldLine = prefix + a + 1, NewLine = 0, Text = leftLines[prefix + a] });
                            a++;
                        }
                        else
                        {
                            result.Add(new TextDiffLine { Type = "insert", OldLine = 0, NewLine = prefix + b + 1, Text = rightLines[prefix + b] });
                            b++;
                        }
                    }
                    while (a < midLeftLen)
                    {
                        result.Add(new TextDiffLine { Type = "delete", OldLine = prefix + a + 1, NewLine = 0, Text = leftLines[prefix + a] });
                        a++;
                    }
                    while (b < midRightLen)
                    {
                        result.Add(new TextDiffLine { Type = "insert", OldLine = 0, NewLine = prefix + b + 1, Text = rightLines[prefix + b] });
                        b++;
                    }
                }
            }

            for (int k = 0; k < suffix; k++)
            {
                int oldIdx = leftLines.Length - suffix + k;
                int newIdx = rightLines.Length - suffix + k;
                result.Add(new TextDiffLine { Type = "equal", OldLine = oldIdx + 1, NewLine = newIdx + 1, Text = leftLines[oldIdx] });
            }

            return result;
        }

        private static string[] SplitNormalizedLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return new string[0];
            return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }
    }

    public class ImportHighlightTracker
    {
        private readonly HashSet<string> _importedConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _importedProjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _importedCapabilityKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void AddImportedConversation(string convId)
        {
            if (!string.IsNullOrEmpty(convId)) _importedConvIds.Add(convId);
        }

        public void AddImportedProject(string projectName)
        {
            if (!string.IsNullOrEmpty(projectName)) _importedProjectNames.Add(projectName);
        }

        public void AddImportedCapability(string uniqueKey)
        {
            if (!string.IsNullOrEmpty(uniqueKey)) _importedCapabilityKeys.Add(uniqueKey);
        }

        public bool IsConversationHighlighted(string convId)
        {
            return !string.IsNullOrEmpty(convId) && _importedConvIds.Contains(convId);
        }

        public bool IsProjectHighlighted(string projectName)
        {
            return !string.IsNullOrEmpty(projectName) && _importedProjectNames.Contains(projectName);
        }

        public bool IsCapabilityHighlighted(string uniqueKey)
        {
            return !string.IsNullOrEmpty(uniqueKey) && _importedCapabilityKeys.Contains(uniqueKey);
        }

        public void Clear()
        {
            _importedConvIds.Clear();
            _importedProjectNames.Clear();
            _importedCapabilityKeys.Clear();
        }
    }
}
