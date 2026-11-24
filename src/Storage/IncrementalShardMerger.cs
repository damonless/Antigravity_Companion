using System;
using System.Collections.Generic;
using System.Text;

namespace AntigravityCompanion
{
    /// <summary>
    /// 增量 Protobuf 分片合流器 (Milestone 1 — F4)
    /// </summary>
    public static class IncrementalProtoShardMerger
    {
        public static byte[] MergeShards(byte[] localPbBytes, List<ConversationSummaryInfo> selectedBackups,
                                        string sourceHome, string currentHome,
                                        Dictionary<string, string> projectUriOverrides = null,
                                        Dictionary<string, string> projectNewIds = null)
        {
            List<ProtoNode> localRootNodes = (localPbBytes != null && localPbBytes.Length > 0)
                ? ProtoCodec.Decode(localPbBytes)
                : new List<ProtoNode>();

            // 先按 UUID 去重保留已有节点索引，确保同一 CID 不残留重复脏项
            Dictionary<string, int> existingIndexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            List<ProtoNode> deduplicated = new List<ProtoNode>(localRootNodes.Count);
            for (int i = 0; i < localRootNodes.Count; i++)
            {
                ProtoNode n = localRootNodes[i];
                if (n.FieldNumber == 1 && n.WireType == 2)
                {
                    ProtoCodec.DecodeSummaryEntryTree(n);
                    string cid = ExtractConversationId(n);
                    if (!string.IsNullOrEmpty(cid))
                    {
                        cid = cid.Trim();
                        int prevIdx;
                        if (existingIndexMap.TryGetValue(cid, out prevIdx))
                        {
                            deduplicated[prevIdx] = n;
                        }
                        else
                        {
                            existingIndexMap[cid] = deduplicated.Count;
                            deduplicated.Add(n);
                        }
                        continue;
                    }
                }
                deduplicated.Add(n);
            }
            localRootNodes = deduplicated;

            if (selectedBackups != null)
            {
                for (int i = 0; i < selectedBackups.Count; i++)
                {
                    ConversationSummaryInfo s = selectedBackups[i];
                    if (s == null || s.RawProtoEntry == null) continue;

                    string origCid = !string.IsNullOrEmpty(s.ConversationId) ? s.ConversationId.Trim() : ExtractConversationId(s.RawProtoEntry);
                    string targetProjId = ResolveNewProjectId(projectNewIds, origCid, s.ProjectId, s.ProjectName);

                    string realTitle = null;
                    bool realCustomTitle = false;
                    if (!string.IsNullOrWhiteSpace(s.CustomTitle))
                    {
                        realTitle = s.CustomTitle.Trim();
                        realCustomTitle = true;
                    }
                    else if (!string.IsNullOrWhiteSpace(s.AutoSummaryTitle))
                    {
                        realTitle = s.AutoSummaryTitle.Trim();
                    }
                    else if (!string.IsNullOrWhiteSpace(s.DisplayTitle) && !IsSyntheticPlaceholderTitle(s.DisplayTitle))
                    {
                        realTitle = s.DisplayTitle.Trim();
                        realCustomTitle = s.HasCustomTitle;
                    }

                    string targetCid = !string.IsNullOrEmpty(s.ConversationId) ? s.ConversationId.Trim() : origCid;

                    ProtoNode clonedNode = CloneAndRemapNode(
                        s.RawProtoEntry, sourceHome, currentHome, projectUriOverrides,
                        s.ProjectName, realTitle, realCustomTitle, targetProjId, s.FolderUri,
                        s.ProjectId, targetCid
                    );

                    if (string.IsNullOrEmpty(targetCid))
                    {
                        targetCid = ExtractConversationId(clonedNode);
                    }
                    if (!SessionMetadataExtractor.IsLikelyConversationId(targetCid))
                    {
                        continue;
                    }
                    targetCid = targetCid.Trim();

                    ProtoNode f1 = clonedNode.GetChild(1);
                    if (f1 != null && !string.Equals(f1.GetString(), targetCid, StringComparison.Ordinal))
                    {
                        f1.SetString(targetCid);
                        clonedNode.IsModified = true;
                    }

                    int existIdx;
                    if (existingIndexMap.TryGetValue(targetCid, out existIdx))
                    {
                        localRootNodes[existIdx] = clonedNode;
                    }
                    else
                    {
                        existingIndexMap[targetCid] = localRootNodes.Count;
                        localRootNodes.Add(clonedNode);
                    }
                }
            }

            return ProtoCodec.Encode(localRootNodes);
        }

        public static bool IsSyntheticPlaceholderTitle(string title)
        {
            return ProtoAstCodec.IsSyntheticPlaceholderTitle(title);
        }

        internal static string ExtractConversationId(ProtoNode entryNode)
        {
            return ProtoAstCodec.ExtractConversationId(entryNode);
        }

        internal static string ResolveNewProjectId(
            Dictionary<string, string> projectNewIds,
            string convId,
            string origProjId,
            string projName)
        {
            if (projectNewIds == null) return null;

            string val;
            if (!string.IsNullOrEmpty(convId) && projectNewIds.TryGetValue("CID:" + convId.Trim(), out val))
                return val;

            if (!string.IsNullOrEmpty(origProjId) &&
                !string.Equals(origProjId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
            {
                if (projectNewIds.TryGetValue("ID:" + origProjId, out val)) return val;
                if (projectNewIds.TryGetValue(origProjId, out val)) return val;
            }

            if (!string.IsNullOrEmpty(projName) && projectNewIds.TryGetValue(projName, out val))
                return val;

            return null;
        }

        internal static string ResolveUriOverride(
            Dictionary<string, string> uriOverrides,
            string convId,
            string targetProjId,
            string origProjId,
            string projName)
        {
            if (uriOverrides == null) return null;

            string val;
            if (!string.IsNullOrEmpty(convId) && uriOverrides.TryGetValue("CID:" + convId.Trim(), out val))
                return val;

            if (!string.IsNullOrEmpty(targetProjId) &&
                !string.Equals(targetProjId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
            {
                if (uriOverrides.TryGetValue("ID:" + targetProjId, out val)) return val;
                if (uriOverrides.TryGetValue(targetProjId, out val)) return val;
            }

            if (!string.IsNullOrEmpty(origProjId) &&
                !string.Equals(origProjId, "outside-of-project", StringComparison.OrdinalIgnoreCase))
            {
                if (uriOverrides.TryGetValue("ID:" + origProjId, out val)) return val;
                if (uriOverrides.TryGetValue(origProjId, out val)) return val;
            }

            if (!string.IsNullOrEmpty(projName) && uriOverrides.TryGetValue(projName, out val))
                return val;

            return null;
        }

        internal static string ExtractProjectIdFromNodes(List<ProtoNode> entryLevelNodes, List<ProtoNode> metaChildren)
        {
            return ProtoAstCodec.ExtractProjectIdFromNodes(entryLevelNodes, metaChildren);
        }

        private static ProtoNode CloneAndRemapNode(ProtoNode sourceNode, string srcHome, string curHome,
                                                   Dictionary<string, string> uriOverrides, string projName,
                                                   string displayTitle = null, bool hasCustomTitle = false,
                                                   string targetProjId = null, string oldWorkspaceUri = null,
                                                   string origProjId = null, string convId = null)
        {
            byte[] encoded = ProtoCodec.Encode(new List<ProtoNode> { sourceNode });
            List<ProtoNode> decodedList = ProtoCodec.Decode(encoded);
            if (decodedList == null || decodedList.Count == 0) return sourceNode;

            ProtoNode node = decodedList[0];
            ProtoCodec.DecodeSummaryEntryTree(node);

            if (string.IsNullOrEmpty(oldWorkspaceUri))
            {
                oldWorkspaceUri = ExtractWorkspaceUriFromEntry(node);
            }

            RemapPathsInAst(node, srcHome, curHome, uriOverrides, projName, oldWorkspaceUri, targetProjId, origProjId, convId);

            // 仅同步真实标题，严禁把 UI 合成占位串写入 .pb
            if (!string.IsNullOrWhiteSpace(displayTitle) && !IsSyntheticPlaceholderTitle(displayTitle))
            {
                SyncTitlesInAst(node, displayTitle, hasCustomTitle);
            }

            if (!string.IsNullOrEmpty(targetProjId))
            {
                SyncProjectIdInAst(node, targetProjId);
            }

            return node;
        }

        internal static string ExtractWorkspaceUriFromMetaChildren(List<ProtoNode> metaChildren)
        {
            return ProtoAstCodec.ExtractWorkspaceUriFromMetaChildren(metaChildren);
        }

        private static string ExtractWorkspaceUriFromEntry(ProtoNode entryNode)
        {
            return ProtoAstCodec.ExtractWorkspaceUriFromEntry(entryNode);
        }

        private static void SyncTitlesInAst(ProtoNode entryNode, string displayTitle, bool hasCustomTitle)
        {
            if (entryNode == null || string.IsNullOrWhiteSpace(displayTitle) || IsSyntheticPlaceholderTitle(displayTitle)) return;
            ProtoCodec.DecodeSummaryEntryTree(entryNode);
            ProtoNode meta = entryNode.GetChild(2);
            if (meta == null || !meta.HasChildren) return;

            SyncTitlesInMetaChildren(meta.Children, displayTitle, hasCustomTitle, true);
            meta.IsModified = true;
            entryNode.IsModified = true;
        }

        internal static bool SyncTitlesInMetaChildren(List<ProtoNode> metaChildren, string displayTitle, bool hasCustomTitle, bool createPrimaryIfMissing)
        {
            return ProtoAstCodec.SyncTitlesInMetaChildren(metaChildren, displayTitle, hasCustomTitle, createPrimaryIfMissing);
        }

        private static void SyncProjectIdInAst(ProtoNode entryNode, string targetProjId)
        {
            if (entryNode == null || string.IsNullOrEmpty(targetProjId)) return;
            ProtoCodec.DecodeSummaryEntryTree(entryNode);
            ProtoNode meta = entryNode.GetChild(2);
            if (meta == null) return;
            if (meta.Children == null) meta.Children = new List<ProtoNode>();

            if (SyncProjectIdInMetaChildren(meta.Children, targetProjId, true))
            {
                meta.IsModified = true;
                entryNode.IsModified = true;
            }
        }

        internal static bool SyncProjectIdInMetaChildren(List<ProtoNode> metaChildren, string targetProjId, bool createCascadeIfMissing)
        {
            return ProtoAstCodec.SyncProjectIdInMetaChildren(metaChildren, targetProjId, createCascadeIfMissing);
        }

        internal static void ClearWorkspaceFieldsInMetaChildren(List<ProtoNode> metaChildren)
        {
            ProtoAstCodec.ClearWorkspaceFieldsInMetaChildren(metaChildren);
        }

        private static void RemapPathsInAst(ProtoNode node, string srcHome, string curHome,
                                            Dictionary<string, string> uriOverrides, string projName,
                                            string oldWorkspaceUri = null, string targetProjId = null,
                                            string origProjId = null, string convId = null)
        {
            if (node == null) return;
            string effectiveConvId = !string.IsNullOrEmpty(convId) ? convId : ExtractConversationId(node);
            string newUri = ResolveUriOverride(uriOverrides, effectiveConvId, targetProjId, origProjId, projName);
            bool isOutside = string.Equals(targetProjId, "outside-of-project", StringComparison.OrdinalIgnoreCase);

            ProtoNode meta = (node.FieldNumber == 1 && node.WireType == 2) ? node.GetChild(2) : null;
            if (meta != null && meta.HasChildren)
            {
                if (isOutside)
                {
                    ClearWorkspaceFieldsInMetaChildren(meta.Children);
                    meta.IsModified = true;
                    node.IsModified = true;
                }
                RemapContainerNodeListPaths(meta.Children, 2, srcHome, curHome, newUri, oldWorkspaceUri, isOutside);
                if (meta.IsAnyModified) node.IsModified = true;
            }
            else
            {
                RemapSingleAstNodePaths(node, 0, srcHome, curHome, newUri, oldWorkspaceUri, isOutside);
            }
        }

        internal static void RemapContainerNodeListPaths(List<ProtoNode> nodes, int parentFieldNumber,
                                                        string srcHome, string curHome,
                                                        string newWorkspaceUri, string oldWorkspaceUri,
                                                        bool isOutsideOfProject)
        {
            if (nodes == null) return;
            if (isOutsideOfProject)
            {
                ClearWorkspaceFieldsInMetaChildren(nodes);
            }
            for (int i = 0; i < nodes.Count; i++)
            {
                RemapSingleAstNodePaths(nodes[i], parentFieldNumber, srcHome, curHome, newWorkspaceUri, oldWorkspaceUri, isOutsideOfProject);
            }
        }

        private static void RemapSingleAstNodePaths(ProtoNode node, int parentFieldNumber,
                                                    string srcHome, string curHome,
                                                    string newWorkspaceUri, string oldWorkspaceUri,
                                                    bool isOutsideOfProject)
        {
            if (node == null) return;

            if (node.WireType == 2 && !node.HasChildren)
            {
                if (node.RawBytes != null && node.RawBytes.Length > 0)
                {
                    string original;
                    if (!node.TryGetStrictUtf8String(out original) || string.IsNullOrEmpty(original))
                        return;

                    bool isExplicitWorkspaceUriLeaf =
                        (parentFieldNumber == 9 && (node.FieldNumber == 1 || node.FieldNumber == 9)) ||
                        (parentFieldNumber == 17 && node.FieldNumber == 7);

                    string updated = original;

                    if (!isOutsideOfProject && !string.IsNullOrEmpty(newWorkspaceUri))
                    {
                        if (isExplicitWorkspaceUriLeaf &&
                            original.StartsWith("file://", StringComparison.OrdinalIgnoreCase) &&
                            !IsBrainArtifactUri(original))
                        {
                            updated = newWorkspaceUri;
                        }
                        else if (updated.IndexOf("file:///", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            updated = ReplaceWorkspaceUriPrefix(updated, oldWorkspaceUri, newWorkspaceUri);
                        }
                    }

                    if (!string.IsNullOrEmpty(srcHome) && !string.IsNullOrEmpty(curHome))
                    {
                        updated = DirectStreamArchiver.RemapAllPathFormats(updated, srcHome, curHome);
                    }

                    if (!string.Equals(updated, original, StringComparison.Ordinal))
                    {
                        node.SetString(updated);
                    }
                }
                return;
            }

            if (node.Children != null)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    RemapSingleAstNodePaths(node.Children[i], node.FieldNumber, srcHome, curHome, newWorkspaceUri, oldWorkspaceUri, isOutsideOfProject);
                }
            }
        }

        internal static bool IsBrainArtifactUri(string uriToken)
        {
            if (string.IsNullOrEmpty(uriToken)) return false;
            string decoded = DirectStreamArchiver.UnescapeUriString(uriToken).Replace('\\', '/');
            return decoded.IndexOf("/.gemini/antigravity/brain/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   decoded.IndexOf("/antigravity/brain/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   decoded.IndexOf("/.gemini/antigravity-cli/brain/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   decoded.IndexOf("/antigravity-cli/brain/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static string ReplaceWorkspaceUriPrefix(string text, string oldWorkspaceUri, string newWorkspaceUri)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(newWorkspaceUri)) return text;

            string normOld = string.IsNullOrEmpty(oldWorkspaceUri)
                ? null
                : DirectStreamArchiver.NormalizeUri(oldWorkspaceUri);

            StringBuilder sb = new StringBuilder(text.Length);
            int pos = 0;
            bool changed = false;

            while (pos < text.Length)
            {
                int fileIdx = text.IndexOf("file:///", pos, StringComparison.OrdinalIgnoreCase);
                if (fileIdx < 0)
                {
                    sb.Append(text, pos, text.Length - pos);
                    break;
                }

                int tokenEnd = text.IndexOfAny(new char[] { ' ', '\t', '\r', '\n', '\0', '\"', '\'', ')', ']', '>', ',', ';' }, fileIdx);
                if (tokenEnd < 0) tokenEnd = text.Length;

                string token = text.Substring(fileIdx, tokenEnd - fileIdx);
                sb.Append(text, pos, fileIdx - pos);

                string replacement = token;
                if (!IsBrainArtifactUri(token) && !string.IsNullOrEmpty(normOld))
                {
                    string normToken = DirectStreamArchiver.NormalizeUri(token);
                    if (string.Equals(normToken, normOld, StringComparison.OrdinalIgnoreCase))
                    {
                        replacement = newWorkspaceUri;
                        changed = true;
                    }
                    else if (normToken.StartsWith(normOld + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        string suffix = normToken.Substring(normOld.Length); // starts with '/'
                        // 尽量从原 token 中保留大小写相对路径
                        string rawSuffix = ExtractRawUriRelativeSuffix(token, normOld);
                        replacement = newWorkspaceUri.TrimEnd('/') + (rawSuffix ?? suffix);
                        changed = true;
                    }
                }

                sb.Append(replacement);
                pos = tokenEnd;
            }

            return changed ? sb.ToString() : text;
        }

        private static string ExtractRawUriRelativeSuffix(string rawToken, string normOldRoot)
        {
            try
            {
                string decoded = DirectStreamArchiver.UnescapeUriString(rawToken).Replace('\\', '/');
                if (decoded.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                    decoded = decoded.Substring(8);
                if (decoded.Length > normOldRoot.Length &&
                    decoded.StartsWith(normOldRoot + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return decoded.Substring(normOldRoot.Length);
                }
            }
            catch {}
            return null;
        }
    }
}
