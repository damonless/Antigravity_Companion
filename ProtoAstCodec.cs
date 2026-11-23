using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // PROTOBUF WIRE TYPES & AST CODEC (Milestone 1 — F1, F2, F4)
    // ══════════════════════════════════════════════════════════════════════

    public enum ProtoWireType
    {
        Varint = 0,
        Fixed64 = 1,
        LengthDelimited = 2,
        StartGroup = 3,
        EndGroup = 4,
        Fixed32 = 5
    }

    public class ProtoNode
    {
        public int FieldNumber;
        public int WireType;
        public ulong VarintValue;
        public byte[] RawBytes;
        public List<ProtoNode> Children;
        public bool IsModified;

        public bool HasChildren { get { return Children != null && Children.Count > 0; } }

        public bool IsAnyModified
        {
            get
            {
                if (IsModified) return true;
                if (Children != null)
                {
                    for (int i = 0; i < Children.Count; i++)
                    {
                        if (Children[i].IsAnyModified) return true;
                    }
                }
                return false;
            }
        }

        public ProtoNode()
        {
            Children = new List<ProtoNode>();
        }

        public ProtoNode(int fieldNumber, int wireType)
        {
            FieldNumber = fieldNumber;
            WireType = wireType;
            Children = new List<ProtoNode>();
        }

        public ProtoNode GetChild(int fieldNumber)
        {
            if (Children == null) return null;
            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i].FieldNumber == fieldNumber) return Children[i];
            }
            return null;
        }

        public List<ProtoNode> GetChildren(int fieldNumber)
        {
            List<ProtoNode> res = new List<ProtoNode>();
            if (Children == null) return res;
            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i].FieldNumber == fieldNumber) res.Add(Children[i]);
            }
            return res;
        }

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public string GetString()
        {
            if (RawBytes == null || RawBytes.Length == 0) return string.Empty;
            return Encoding.UTF8.GetString(RawBytes);
        }

        public bool TryGetStrictUtf8String(out string text)
        {
            text = string.Empty;
            if (RawBytes == null || RawBytes.Length == 0) return true;
            try
            {
                text = StrictUtf8.GetString(RawBytes);
                return true;
            }
            catch (DecoderFallbackException)
            {
                text = null;
                return false;
            }
        }

        public void SetString(string value)
        {
            Children.Clear();
            RawBytes = string.IsNullOrEmpty(value) ? new byte[0] : Encoding.UTF8.GetBytes(value);
            IsModified = true;
        }

        public void SetBytes(byte[] bytes)
        {
            Children.Clear();
            RawBytes = bytes ?? new byte[0];
            IsModified = true;
        }

        public int GetInt32()
        {
            return (int)VarintValue;
        }

        public long GetInt64()
        {
            return (long)VarintValue;
        }
    }

    public static class ProtoCodec
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static List<ProtoNode> Decode(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0) return new List<ProtoNode>();
            return Decode(buffer, 0, buffer.Length);
        }

        public static List<ProtoNode> Decode(byte[] buffer, int offset, int length)
        {
            List<ProtoNode> nodes = new List<ProtoNode>();
            if (buffer == null || length == 0) return nodes;
            if (offset < 0 || length < 0 || offset > buffer.Length || length > buffer.Length - offset)
                throw new ArgumentOutOfRangeException("length", "Protobuf 切片范围越界。");

            int pos = offset;
            int end = offset + length;

            while (pos < end)
            {
                ulong tag;
                int tagBytes = ReadVarint(buffer, pos, end, out tag);
                if (tagBytes <= 0)
                    throw new InvalidDataException("Protobuf 字段 Tag Varint 截断或溢出。");
                pos += tagBytes;

                ulong rawFieldNumber = tag >> 3;
                if (rawFieldNumber == 0 || rawFieldNumber > 536870911UL)
                    throw new InvalidDataException("Protobuf 非法字段编号: " + rawFieldNumber);

                int fieldNumber = (int)rawFieldNumber;
                int wireType = (int)(tag & 0x07UL);

                ProtoNode node = new ProtoNode(fieldNumber, wireType);

                switch (wireType)
                {
                    case 0: // Varint
                        ulong v;
                        int vBytes = ReadVarint(buffer, pos, end, out v);
                        if (vBytes <= 0)
                            throw new InvalidDataException("Protobuf Varint 值截断或溢出，Field=" + fieldNumber);
                        pos += vBytes;
                        node.VarintValue = v;
                        break;

                    case 1: // Fixed64
                        if (end - pos < 8)
                            throw new InvalidDataException("Protobuf Fixed64 截断，Field=" + fieldNumber);
                        node.RawBytes = new byte[8];
                        Buffer.BlockCopy(buffer, pos, node.RawBytes, 0, 8);
                        pos += 8;
                        break;

                    case 2: // Length-delimited
                        ulong len;
                        int lBytes = ReadVarint(buffer, pos, end, out len);
                        if (lBytes <= 0)
                            throw new InvalidDataException("Protobuf Length Varint 截断或溢出，Field=" + fieldNumber);
                        pos += lBytes;

                        if (len > (ulong)(end - pos))
                            throw new InvalidDataException("Protobuf Length 越界或溢出，Field=" + fieldNumber + ", len=" + len);

                        int byteCount = (int)len;
                        node.RawBytes = new byte[byteCount];
                        if (byteCount > 0)
                        {
                            Buffer.BlockCopy(buffer, pos, node.RawBytes, 0, byteCount);
                        }
                        pos += byteCount;
                        break;

                    case 5: // Fixed32
                        if (end - pos < 4)
                            throw new InvalidDataException("Protobuf Fixed32 截断，Field=" + fieldNumber);
                        node.RawBytes = new byte[4];
                        Buffer.BlockCopy(buffer, pos, node.RawBytes, 0, 4);
                        pos += 4;
                        break;

                    default:
                        throw new InvalidDataException("Protobuf 遇到未支持或损坏的 WireType=" + wireType + ", Field=" + fieldNumber);
                }

                nodes.Add(node);
            }

            if (pos != end)
                throw new InvalidDataException("Protobuf 数据未完整消费。");

            return nodes;
        }

        public static bool TryDecodeStrict(byte[] buffer, out List<ProtoNode> nodes)
        {
            nodes = null;
            if (buffer == null || buffer.Length == 0) return false;
            try
            {
                nodes = Decode(buffer, 0, buffer.Length);
                return nodes != null && nodes.Count > 0;
            }
            catch
            {
                nodes = null;
                return false;
            }
        }

        /// <summary>
        /// 按已知 Antigravity .pb 条目结构按需解码容器节点，绝不对普通字符串做启发式拆解
        /// </summary>
        public static void DecodeSummaryEntryTree(ProtoNode entryNode)
        {
            if (entryNode == null) return;
            if (!entryNode.HasChildren && entryNode.WireType == 2 && entryNode.RawBytes != null && entryNode.RawBytes.Length > 0)
            {
                List<ProtoNode> sub;
                if (TryDecodeStrict(entryNode.RawBytes, out sub))
                {
                    entryNode.Children = sub;
                }
            }

            ProtoNode meta = entryNode.GetChild(2);
            if (meta != null && meta.WireType == 2)
            {
                DecodeMetaNodeContainers(meta);
            }
        }

        public static void DecodeMetaNodeContainers(ProtoNode metaNode)
        {
            if (metaNode == null || metaNode.WireType != 2) return;
            if (!metaNode.HasChildren && metaNode.RawBytes != null && metaNode.RawBytes.Length > 0)
            {
                List<ProtoNode> sub;
                if (TryDecodeStrict(metaNode.RawBytes, out sub))
                {
                    metaNode.Children = sub;
                }
            }
            if (metaNode.HasChildren)
            {
                DecodeMetaContainerList(metaNode.Children);
            }
        }

        public static void DecodeMetaContainerList(List<ProtoNode> metaChildren)
        {
            if (metaChildren == null) return;
            for (int i = 0; i < metaChildren.Count; i++)
            {
                ProtoNode child = metaChildren[i];
                if (child == null || child.WireType != 2 || child.RawBytes == null || child.RawBytes.Length == 0)
                    continue;

                if (child.FieldNumber == 3 || child.FieldNumber == 7 || child.FieldNumber == 10)
                {
                    if (!child.HasChildren)
                    {
                        List<ProtoNode> tsSub;
                        if (TryDecodeStrict(child.RawBytes, out tsSub) && IsValidTimestampContainer(tsSub))
                        {
                            child.Children = tsSub;
                        }
                    }
                }
                else if (child.FieldNumber == 9)
                {
                    // WorkspaceInfo 容器（兼容早期纯字符串或嵌套条目）
                    if (!child.HasChildren && !StartsLikePlainUriOrPath(child.RawBytes))
                    {
                        List<ProtoNode> wsSub;
                        if (TryDecodeStrict(child.RawBytes, out wsSub))
                        {
                            child.Children = wsSub;
                        }
                    }
                    if (child.HasChildren)
                    {
                        for (int j = 0; j < child.Children.Count; j++)
                        {
                            ProtoNode wsItem = child.Children[j];
                            if (wsItem.WireType == 2 && !wsItem.HasChildren && wsItem.RawBytes != null && wsItem.RawBytes.Length > 0 &&
                                !StartsLikePlainUriOrPath(wsItem.RawBytes))
                            {
                                List<ProtoNode> nestedWs;
                                if (TryDecodeStrict(wsItem.RawBytes, out nestedWs) && ContainsFieldWithWireType(nestedWs, 1, 2))
                                {
                                    wsItem.Children = nestedWs;
                                }
                            }
                        }
                    }
                }
                else if (child.FieldNumber == 15)
                {
                    // CustomTitle 可为子消息 {Field 1: title, optional Field 7: timestamp} 或早期裸 UTF-8 字符串
                    if (!child.HasChildren && IsLikelyCustomTitleContainerBytes(child.RawBytes))
                    {
                        List<ProtoNode> ctSub;
                        if (TryDecodeStrict(child.RawBytes, out ctSub) && IsValidCustomTitleContainer(ctSub))
                        {
                            child.Children = ctSub;
                        }
                    }
                }
                else if (child.FieldNumber == 17)
                {
                    // CascadeInfo 容器（内部 Field 1/4/7/11/18 均保持叶节点）
                    if (!child.HasChildren)
                    {
                        List<ProtoNode> casSub;
                        if (TryDecodeStrict(child.RawBytes, out casSub) && IsValidCascadeContainer(casSub))
                        {
                            child.Children = casSub;
                        }
                    }
                }
            }
        }

        private static bool StartsLikePlainUriOrPath(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return false;
            if (bytes.Length >= 5 && (bytes[0] == 'f' || bytes[0] == 'F') && bytes[1] == 'i' && bytes[2] == 'l' && bytes[3] == 'e' && bytes[4] == ':')
                return true;
            if (bytes.Length >= 3 && ((bytes[0] >= 'A' && bytes[0] <= 'Z') || (bytes[0] >= 'a' && bytes[0] <= 'z')) && bytes[1] == ':' && (bytes[2] == '\\' || bytes[2] == '/'))
                return true;
            if (bytes[0] == '/' || bytes[0] == '\\')
                return true;
            return false;
        }

        private static bool ContainsFieldWithWireType(List<ProtoNode> nodes, int fieldNumber, int wireType)
        {
            if (nodes == null) return false;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].FieldNumber == fieldNumber && nodes[i].WireType == wireType) return true;
            }
            return false;
        }

        private static bool IsValidTimestampContainer(List<ProtoNode> nodes)
        {
            if (nodes == null || nodes.Count == 0) return false;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].FieldNumber != 1 && nodes[i].FieldNumber != 2) return false;
                if (nodes[i].WireType != 0) return false;
            }
            return ContainsFieldWithWireType(nodes, 1, 0);
        }

        private static bool IsLikelyCustomTitleContainerBytes(byte[] raw)
        {
            if (raw == null || raw.Length < 2) return false;
            return raw[0] == 0x0A || raw[0] == 0x3A;
        }

        private static bool IsValidCustomTitleContainer(List<ProtoNode> nodes)
        {
            if (nodes == null || nodes.Count == 0) return false;
            bool hasTitleField = false;
            for (int i = 0; i < nodes.Count; i++)
            {
                ProtoNode n = nodes[i];
                if (n.FieldNumber == 1)
                {
                    if (n.WireType != 2) return false;
                    string s;
                    if (!n.TryGetStrictUtf8String(out s)) return false;
                    hasTitleField = true;
                }
                else if (n.FieldNumber == 7)
                {
                    if (n.WireType == 2)
                    {
                        List<ProtoNode> tsSub;
                        if (!TryDecodeStrict(n.RawBytes, out tsSub) || !IsValidTimestampContainer(tsSub)) return false;
                    }
                    else if (n.WireType != 0)
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            return hasTitleField;
        }

        private static bool IsValidCascadeContainer(List<ProtoNode> nodes)
        {
            if (nodes == null || nodes.Count == 0) return false;
            for (int i = 0; i < nodes.Count; i++)
            {
                ProtoNode n = nodes[i];
                if (n.FieldNumber <= 0 || n.FieldNumber > 128) return false;
                // Field 17->1 在新版 Antigravity .pb 中为嵌套子消息（>127B 时 Varint 含高位字节），不能按 UTF-8 拒解
                if ((n.FieldNumber == 4 || n.FieldNumber == 7 || n.FieldNumber == 11 || n.FieldNumber == 18) && n.WireType == 2)
                {
                    string s;
                    if (!n.TryGetStrictUtf8String(out s)) return false;
                }
            }
            return true;
        }

        public static byte[] Encode(List<ProtoNode> nodes)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                EncodeTo(nodes, ms);
                return ms.ToArray();
            }
        }

        public static void EncodeTo(List<ProtoNode> nodes, Stream stream)
        {
            if (nodes == null || nodes.Count == 0) return;

            for (int i = 0; i < nodes.Count; i++)
            {
                ProtoNode node = nodes[i];
                ulong tag = ((ulong)(uint)node.FieldNumber << 3) | ((uint)node.WireType & 0x07U);
                WriteVarint(stream, tag);

                switch (node.WireType)
                {
                    case 0: // Varint
                        WriteVarint(stream, node.VarintValue);
                        break;

                    case 1: // Fixed64
                        if (node.RawBytes != null && node.RawBytes.Length == 8)
                            stream.Write(node.RawBytes, 0, 8);
                        else
                            stream.Write(new byte[8], 0, 8);
                        break;

                    case 2: // Length-delimited
                        byte[] payload;
                        if (node.HasChildren && (node.IsAnyModified || node.RawBytes == null || node.RawBytes.Length == 0))
                        {
                            payload = Encode(node.Children);
                        }
                        else
                        {
                            payload = node.RawBytes ?? new byte[0];
                        }
                        WriteVarint(stream, (ulong)payload.Length);
                        if (payload.Length > 0)
                        {
                            stream.Write(payload, 0, payload.Length);
                        }
                        break;

                    case 5: // Fixed32
                        if (node.RawBytes != null && node.RawBytes.Length == 4)
                            stream.Write(node.RawBytes, 0, 4);
                        else
                            stream.Write(new byte[4], 0, 4);
                        break;
                }
            }
        }

        public static int ReadVarint(byte[] buffer, int offset, out ulong value)
        {
            return ReadVarint(buffer, offset, buffer != null ? buffer.Length : 0, out value);
        }

        public static int ReadVarint(byte[] buffer, int offset, int end, out ulong value)
        {
            value = 0;
            if (buffer == null || offset < 0) return 0;
            int limit = Math.Min(end, buffer.Length);
            int shift = 0;
            int count = 0;

            for (int i = offset; i < limit && count < 10; i++)
            {
                byte b = buffer[i];
                if (count == 9 && (b & 0xFE) != 0)
                {
                    value = 0;
                    return 0; // 64-bit unsigned overflow
                }
                count++;
                value |= ((ulong)(b & 0x7F)) << shift;
                if ((b & 0x80) == 0)
                {
                    return count;
                }
                shift += 7;
            }

            value = 0;
            return 0; // 截断或超过 10 字节溢出
        }

        public static void WriteVarint(Stream stream, ulong value)
        {
            while (value >= 0x80)
            {
                stream.WriteByte((byte)((value & 0x7F) | 0x80));
                value >>= 7;
            }
            stream.WriteByte((byte)(value & 0x7F));
        }
    }

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

        internal static bool IsSyntheticPlaceholderTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return false;
            return title.StartsWith("未命名会话 (", StringComparison.Ordinal) && title.EndsWith(")", StringComparison.Ordinal);
        }

        internal static string ExtractConversationId(ProtoNode entryNode)
        {
            if (entryNode == null) return null;
            ProtoCodec.DecodeSummaryEntryTree(entryNode);
            ProtoNode f1 = entryNode.GetChild(1);
            if (f1 != null)
            {
                string id = f1.GetString();
                if (!string.IsNullOrWhiteSpace(id)) return id.Trim();
            }
            return null;
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
            if (entryLevelNodes != null)
            {
                for (int i = 0; i < entryLevelNodes.Count; i++)
                {
                    if (entryLevelNodes[i].FieldNumber == 18 && entryLevelNodes[i].WireType == 2)
                    {
                        string pid = entryLevelNodes[i].GetString();
                        if (!string.IsNullOrWhiteSpace(pid)) return pid.Trim();
                    }
                }
            }
            if (metaChildren != null)
            {
                for (int i = 0; i < metaChildren.Count; i++)
                {
                    ProtoNode c = metaChildren[i];
                    if (c.FieldNumber == 17 && c.WireType == 2 && c.HasChildren)
                    {
                        ProtoNode f18 = c.GetChild(18);
                        if (f18 != null && f18.WireType == 2)
                        {
                            string pid = f18.GetString();
                            if (!string.IsNullOrWhiteSpace(pid)) return pid.Trim();
                        }
                    }
                }
            }
            return null;
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
            if (metaChildren == null) return null;
            for (int i = 0; i < metaChildren.Count; i++)
            {
                ProtoNode c = metaChildren[i];
                if (c.FieldNumber == 9 && c.WireType == 2)
                {
                    if (c.HasChildren)
                    {
                        ProtoNode f1 = c.GetChild(1);
                        if (f1 != null && f1.WireType == 2)
                        {
                            if (f1.HasChildren && f1.GetChild(1) != null)
                            {
                                string s = f1.GetChild(1).GetString();
                                if (!string.IsNullOrEmpty(s)) return s;
                            }
                            string direct = f1.GetString();
                            if (!string.IsNullOrEmpty(direct)) return direct;
                        }
                    }
                    else
                    {
                        string raw = c.GetString();
                        if (!string.IsNullOrEmpty(raw)) return raw;
                    }
                }
                else if (c.FieldNumber == 17 && c.WireType == 2 && c.HasChildren)
                {
                    ProtoNode f7 = c.GetChild(7);
                    if (f7 != null && f7.WireType == 2)
                    {
                        string s = f7.GetString();
                        if (!string.IsNullOrEmpty(s)) return s;
                    }
                }
            }
            return null;
        }

        private static string ExtractWorkspaceUriFromEntry(ProtoNode entryNode)
        {
            if (entryNode == null) return null;
            ProtoNode meta = entryNode.GetChild(2);
            if (meta != null && meta.HasChildren)
            {
                return ExtractWorkspaceUriFromMetaChildren(meta.Children);
            }
            return null;
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
            if (metaChildren == null || string.IsNullOrWhiteSpace(displayTitle) || IsSyntheticPlaceholderTitle(displayTitle))
                return false;

            bool modified = false;
            ProtoNode f1 = null;
            for (int i = 0; i < metaChildren.Count; i++)
            {
                if (metaChildren[i].FieldNumber == 1)
                {
                    f1 = metaChildren[i];
                    break;
                }
            }

            if (f1 != null && f1.WireType == 2)
            {
                f1.SetString(displayTitle);
                modified = true;
            }
            else if (createPrimaryIfMissing)
            {
                f1 = new ProtoNode { FieldNumber = 1, WireType = 2 };
                f1.SetString(displayTitle);
                metaChildren.Insert(0, f1);
                modified = true;
            }

            ProtoNode f15 = null;
            for (int i = 0; i < metaChildren.Count; i++)
            {
                if (metaChildren[i].FieldNumber == 15)
                {
                    f15 = metaChildren[i];
                    break;
                }
            }

            if (hasCustomTitle || f15 != null)
            {
                if (f15 != null && f15.WireType == 2)
                {
                    if (f15.HasChildren)
                    {
                        ProtoNode ctF1 = f15.GetChild(1);
                        if (ctF1 != null && ctF1.WireType == 2)
                        {
                            ctF1.SetString(displayTitle);
                        }
                        else
                        {
                            ProtoNode newCtF1 = new ProtoNode { FieldNumber = 1, WireType = 2 };
                            newCtF1.SetString(displayTitle);
                            f15.Children.Insert(0, newCtF1);
                        }
                        f15.IsModified = true;
                    }
                    else
                    {
                        f15.SetString(displayTitle);
                    }
                    modified = true;
                }
                else if (hasCustomTitle)
                {
                    ProtoNode newF15 = new ProtoNode { FieldNumber = 15, WireType = 2, Children = new List<ProtoNode>(), IsModified = true };
                    ProtoNode ct1 = new ProtoNode { FieldNumber = 1, WireType = 2 };
                    ct1.SetString(displayTitle);
                    newF15.Children.Add(ct1);

                    long epoch = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                    ProtoNode ts = new ProtoNode { FieldNumber = 7, WireType = 2, Children = new List<ProtoNode>(), IsModified = true };
                    ts.Children.Add(new ProtoNode { FieldNumber = 1, WireType = 0, VarintValue = (ulong)epoch, IsModified = true });
                    newF15.Children.Add(ts);

                    metaChildren.Add(newF15);
                    modified = true;
                }
            }

            return modified;
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
            if (metaChildren == null || string.IsNullOrEmpty(targetProjId)) return false;

            ProtoNode cascade = null;
            for (int i = 0; i < metaChildren.Count; i++)
            {
                if (metaChildren[i].FieldNumber == 17 && metaChildren[i].WireType == 2)
                {
                    cascade = metaChildren[i];
                    break;
                }
            }

            if (cascade == null)
            {
                if (!createCascadeIfMissing) return false;
                cascade = new ProtoNode { FieldNumber = 17, WireType = 2, Children = new List<ProtoNode>(), IsModified = true };
                metaChildren.Add(cascade);
            }

            if (!cascade.HasChildren && cascade.RawBytes != null && cascade.RawBytes.Length > 0)
            {
                List<ProtoNode> casSub;
                if (ProtoCodec.TryDecodeStrict(cascade.RawBytes, out casSub))
                {
                    cascade.Children = casSub;
                }
            }
            if (cascade.Children == null) cascade.Children = new List<ProtoNode>();

            ProtoNode pIdNode = cascade.GetChild(18);
            if (pIdNode != null && pIdNode.WireType == 2)
            {
                pIdNode.SetString(targetProjId);
            }
            else
            {
                ProtoNode newPId = new ProtoNode { FieldNumber = 18, WireType = 2 };
                newPId.SetString(targetProjId);
                cascade.Children.Add(newPId);
            }
            cascade.IsModified = true;
            return true;
        }

        internal static void ClearWorkspaceFieldsInMetaChildren(List<ProtoNode> metaChildren)
        {
            if (metaChildren == null) return;
            for (int i = metaChildren.Count - 1; i >= 0; i--)
            {
                ProtoNode child = metaChildren[i];
                if (child.FieldNumber == 9)
                {
                    metaChildren.RemoveAt(i);
                }
                else if (child.FieldNumber == 17 && child.WireType == 2 && child.HasChildren)
                {
                    for (int j = child.Children.Count - 1; j >= 0; j--)
                    {
                        if (child.Children[j].FieldNumber == 7)
                        {
                            child.Children.RemoveAt(j);
                            child.IsModified = true;
                        }
                    }
                }
            }
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
                   decoded.IndexOf("/antigravity/brain/", StringComparison.OrdinalIgnoreCase) >= 0;
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

    /// <summary>
    /// SQLite 数据库 trajectory_metadata_blob 跨机路径重映射与 ProjectId 修正工具
    /// </summary>
    public static class SqliteMetadataUpdater
    {
        private const int SQLITE_OK = 0;
        private const int SQLITE_ROW = 100;
        private const int SQLITE_DONE = 101;
        private const int SQLITE_OPEN_READWRITE = 0x00000002;
        private static readonly IntPtr SQLITE_TRANSIENT = new IntPtr(-1);

        public static string LastError { get; private set; }

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, IntPtr zVfs);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_close(IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_busy_timeout", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_busy_timeout(IntPtr db, int ms);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_prepare_v2(IntPtr db, byte[] zSql, int nByte, out IntPtr ppStmt, IntPtr pzTail);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_step(IntPtr pStmt);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_blob", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr sqlite3_column_blob(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_bytes", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_column_bytes(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_blob", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_bind_blob(IntPtr pStmt, int iCol, IntPtr zData, int nData, IntPtr xDel);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_finalize(IntPtr pStmt);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_changes", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_changes(IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_errmsg", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr sqlite3_errmsg(IntPtr db);

        internal static byte[] ToUtf8NullTerminated(string text)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(text ?? string.Empty);
            byte[] z = new byte[utf8.Length + 1];
            Buffer.BlockCopy(utf8, 0, z, 0, utf8.Length);
            z[utf8.Length] = 0;
            return z;
        }

        private static string GetSqliteErrorMessage(IntPtr db)
        {
            if (db == IntPtr.Zero) return "db is null";
            try
            {
                IntPtr ptr = sqlite3_errmsg(db);
                if (ptr == IntPtr.Zero) return "unknown sqlite error";
                int len = 0;
                while (len < 1024 && Marshal.ReadByte(ptr, len) != 0) len++;
                byte[] buf = new byte[len];
                Marshal.Copy(ptr, buf, 0, len);
                return Encoding.UTF8.GetString(buf);
            }
            catch
            {
                return "unknown sqlite error";
            }
        }

        public static bool RemapSqliteDbMetadata(string dbPath, string srcHome, string curHome,
                                                 Dictionary<string, string> uriOverrides, string projName,
                                                 string targetProjId = null, string displayTitle = null)
        {
            LastError = null;
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                LastError = "数据库文件不存在: " + dbPath;
                return false;
            }

            IntPtr db = IntPtr.Zero;
            try
            {
                byte[] pathBytes = ToUtf8NullTerminated(dbPath);
                int openRc = sqlite3_open_v2(pathBytes, out db, SQLITE_OPEN_READWRITE, IntPtr.Zero);
                if (openRc != SQLITE_OK || db == IntPtr.Zero)
                {
                    LastError = "sqlite3_open_v2 失败 (" + openRc + "): " + GetSqliteErrorMessage(db);
                    return false;
                }
                sqlite3_busy_timeout(db, 5000);

                byte[] blob = null;
                IntPtr selectStmt = IntPtr.Zero;
                byte[] selectSql = ToUtf8NullTerminated("SELECT data FROM trajectory_metadata_blob WHERE id='main';");
                try
                {
                    int prepRc = sqlite3_prepare_v2(db, selectSql, -1, out selectStmt, IntPtr.Zero);
                    if (prepRc != SQLITE_OK)
                    {
                        LastError = "SELECT prepare 失败 (" + prepRc + "): " + GetSqliteErrorMessage(db);
                        return false;
                    }
                    int stepRc = sqlite3_step(selectStmt);
                    if (stepRc == SQLITE_ROW)
                    {
                        int len = sqlite3_column_bytes(selectStmt, 0);
                        IntPtr blobPtr = sqlite3_column_blob(selectStmt, 0);
                        if (len > 0 && blobPtr != IntPtr.Zero)
                        {
                            blob = new byte[len];
                            Marshal.Copy(blobPtr, blob, 0, len);
                        }
                    }
                }
                finally
                {
                    if (selectStmt != IntPtr.Zero) sqlite3_finalize(selectStmt);
                }

                if (blob == null || blob.Length == 0)
                {
                    LastError = "trajectory_metadata_blob 未找到 id='main' 或数据为空。";
                    return false;
                }

                List<ProtoNode> nodes = ProtoCodec.Decode(blob);
                if (nodes == null || nodes.Count == 0)
                {
                    LastError = "trajectory_metadata_blob Protobuf 解码为空。";
                    return false;
                }

                // 兼容三种容器形态:
                // 1) entryNode 包装形态: Field 1 { Field 1 cid, Field 2 meta }
                // 2) entryChildren 顶层形态: { Field 1 cid, Field 2 meta }
                // 3) 顶层即 meta 形态: { Field 1 title, Field 9 ws, ... }
                ProtoNode wrappedEntry = null;
                ProtoNode wrappedMeta = null;
                if (nodes.Count == 1 && nodes[0].FieldNumber == 1 && nodes[0].WireType == 2 && !StartsLikePlainString(nodes[0]))
                {
                    List<ProtoNode> entrySub;
                    if (ProtoCodec.TryDecodeStrict(nodes[0].RawBytes, out entrySub) && entrySub.Count >= 2)
                    {
                        ProtoNode innerCid = null;
                        ProtoNode innerMeta = null;
                        for (int j = 0; j < entrySub.Count; j++)
                        {
                            if (entrySub[j].FieldNumber == 1 && entrySub[j].WireType == 2) innerCid = entrySub[j];
                            else if (entrySub[j].FieldNumber == 2 && entrySub[j].WireType == 2) innerMeta = entrySub[j];
                        }
                        List<ProtoNode> metaCheck;
                        if (innerCid != null && SessionMetadataExtractor.IsLikelyConversationId(innerCid.GetString()) &&
                            innerMeta != null && !StartsLikePlainString(innerMeta) &&
                            ProtoCodec.TryDecodeStrict(innerMeta.RawBytes, out metaCheck) && metaCheck.Count > 0)
                        {
                            nodes[0].Children = entrySub;
                            ProtoCodec.DecodeSummaryEntryTree(nodes[0]);
                            wrappedEntry = nodes[0];
                            wrappedMeta = nodes[0].GetChild(2);
                        }
                    }
                }

                if (wrappedMeta == null)
                {
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        if (nodes[i].FieldNumber == 2 && nodes[i].WireType == 2 && !StartsLikePlainString(nodes[i]))
                        {
                            List<ProtoNode> metaSub;
                            if (ProtoCodec.TryDecodeStrict(nodes[i].RawBytes, out metaSub) && metaSub.Count > 0)
                            {
                                nodes[i].Children = metaSub;
                                ProtoCodec.DecodeMetaContainerList(nodes[i].Children);
                                wrappedMeta = nodes[i];
                                break;
                            }
                        }
                    }
                    ProtoCodec.DecodeMetaContainerList(nodes);
                }

                List<ProtoNode> entryLevelNodes = (wrappedEntry != null && wrappedEntry.HasChildren)
                    ? wrappedEntry.Children
                    : nodes;

                List<ProtoNode> targetContainerNodes = (wrappedMeta != null && wrappedMeta.HasChildren)
                    ? wrappedMeta.Children
                    : nodes;

                string origProjId = IncrementalProtoShardMerger.ExtractProjectIdFromNodes(entryLevelNodes, targetContainerNodes);
                string sqlConvId = null;
                if (wrappedEntry != null)
                {
                    sqlConvId = IncrementalProtoShardMerger.ExtractConversationId(wrappedEntry);
                }
                else
                {
                    for (int i = 0; i < entryLevelNodes.Count; i++)
                    {
                        if (entryLevelNodes[i].FieldNumber == 1 && entryLevelNodes[i].WireType == 2 &&
                            SessionMetadataExtractor.IsLikelyConversationId(entryLevelNodes[i].GetString()))
                        {
                            sqlConvId = entryLevelNodes[i].GetString().Trim();
                            break;
                        }
                    }
                }

                string oldWorkspaceUri = IncrementalProtoShardMerger.ExtractWorkspaceUriFromMetaChildren(targetContainerNodes);
                bool isOutside = string.Equals(targetProjId, "outside-of-project", StringComparison.OrdinalIgnoreCase);
                string newUri = isOutside
                    ? null
                    : IncrementalProtoShardMerger.ResolveUriOverride(uriOverrides, sqlConvId, targetProjId, origProjId, projName);

                if (isOutside)
                {
                    IncrementalProtoShardMerger.ClearWorkspaceFieldsInMetaChildren(targetContainerNodes);
                    if (wrappedMeta != null) wrappedMeta.IsModified = true;
                    if (wrappedEntry != null) wrappedEntry.IsModified = true;
                }

                IncrementalProtoShardMerger.RemapContainerNodeListPaths(
                    targetContainerNodes,
                    wrappedMeta != null ? 2 : 0,
                    srcHome,
                    curHome,
                    newUri,
                    oldWorkspaceUri,
                    isOutside
                );
                if (wrappedMeta != null && wrappedMeta.IsAnyModified)
                {
                    wrappedMeta.IsModified = true;
                }
                if (wrappedEntry != null && wrappedEntry.IsAnyModified)
                {
                    wrappedEntry.IsModified = true;
                }

                if (!string.IsNullOrEmpty(targetProjId))
                {
                    bool updatedTop18 = false;
                    for (int i = 0; i < entryLevelNodes.Count; i++)
                    {
                        if (entryLevelNodes[i].FieldNumber == 18 && entryLevelNodes[i].WireType == 2)
                        {
                            entryLevelNodes[i].SetString(targetProjId);
                            updatedTop18 = true;
                            if (wrappedEntry != null) wrappedEntry.IsModified = true;
                        }
                    }
                    // 同步更新或创建嵌套 Field 17 -> Field 18
                    bool createCascade = !updatedTop18;
                    if (IncrementalProtoShardMerger.SyncProjectIdInMetaChildren(targetContainerNodes, targetProjId, createCascade))
                    {
                        if (wrappedMeta != null) wrappedMeta.IsModified = true;
                        if (wrappedEntry != null) wrappedEntry.IsModified = true;
                    }
                }

                if (!string.IsNullOrWhiteSpace(displayTitle) && !IncrementalProtoShardMerger.IsSyntheticPlaceholderTitle(displayTitle))
                {
                    if (IncrementalProtoShardMerger.SyncTitlesInMetaChildren(targetContainerNodes, displayTitle.Trim(), false, false))
                    {
                        if (wrappedMeta != null) wrappedMeta.IsModified = true;
                        if (wrappedEntry != null) wrappedEntry.IsModified = true;
                    }
                }

                byte[] updatedBlob = ProtoCodec.Encode(nodes);

                IntPtr updateStmt = IntPtr.Zero;
                GCHandle pinHandle = GCHandle.Alloc(updatedBlob, GCHandleType.Pinned);
                byte[] updateSql = ToUtf8NullTerminated("UPDATE trajectory_metadata_blob SET data = ? WHERE id = 'main';");
                try
                {
                    int prepRc = sqlite3_prepare_v2(db, updateSql, -1, out updateStmt, IntPtr.Zero);
                    if (prepRc != SQLITE_OK)
                    {
                        LastError = "UPDATE prepare 失败 (" + prepRc + "): " + GetSqliteErrorMessage(db);
                        return false;
                    }

                    int bindRc = sqlite3_bind_blob(updateStmt, 1, pinHandle.AddrOfPinnedObject(), updatedBlob.Length, SQLITE_TRANSIENT);
                    if (bindRc != SQLITE_OK)
                    {
                        LastError = "sqlite3_bind_blob 失败 (" + bindRc + "): " + GetSqliteErrorMessage(db);
                        return false;
                    }

                    int stepRc = sqlite3_step(updateStmt);
                    if (stepRc != SQLITE_DONE)
                    {
                        LastError = "UPDATE step 失败 (" + stepRc + "): " + GetSqliteErrorMessage(db);
                        return false;
                    }

                    int changedRows = sqlite3_changes(db);
                    if (changedRows <= 0)
                    {
                        LastError = "UPDATE 未命中任何 'main' 行 (sqlite3_changes=" + changedRows + ")";
                        return false;
                    }
                    return true;
                }
                finally
                {
                    if (updateStmt != IntPtr.Zero) sqlite3_finalize(updateStmt);
                    if (pinHandle.IsAllocated) pinHandle.Free();
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
            finally
            {
                if (db != IntPtr.Zero) sqlite3_close(db);
            }
        }

        private static bool StartsLikePlainString(ProtoNode node)
        {
            if (node == null || node.RawBytes == null || node.RawBytes.Length == 0) return true;
            byte first = node.RawBytes[0];
            return first >= 0x20 && first <= 0x7E && (first & 0x07) != 0 && (first & 0x07) != 2;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // SUMMARY.SQLITE BRIDGE (Read, Search, Transactional Update/Rollback/GC/Upsert/Backup)
    // ══════════════════════════════════════════════════════════════════════

    public class SummaryDbRowInfo
    {
        public string ConversationId;
        public string Title;
        public string Preview;
        public int StepCount;
        public string LastModifiedTime;
        public string WorkspaceUris;
        public string Status;
        public string ProjectId;
        public string AgentName;
        public string ParentConversationId;
        public int NestingDepth;
        public bool NotFullyIdle;
        public string FirstPromptSnippet;
        public string GitBranchHint;
        public List<KeyValuePair<string, string>> RepoBranchHints;
        public string AppDataDir;
        public string Source;
        public byte[] RawSummaryBytes;
    }

    public class SummaryDbRollbackEntry
    {
        public string ConversationId;
        public string OldTitle;
        public string OldPreview;
        public byte[] OldRawSummary;
    }

    public class SummaryDbUpsertItem
    {
        public string ConversationId;
        public string Title;
        public string Preview;
        public int StepCount;
        public string LastModifiedTimeIso;
        public string LastUserInputTimeIso;
        public string WorkspaceUrisJson;
        public string ProjectId;
        public string ParentConversationId;
        public int NestingDepth;
        public byte[] RawSummaryBytes;
        public string ProjectName;
        public string FtsWorkspaceToken;
    }

    /// <summary>
    /// Antigravity summary.sqlite 读写桥接器（C# 5.0 / winsqlite3.dll 原生 P/Invoke）
    /// </summary>
    public static class SummarySqliteBridge
    {
        private const int SQLITE_OK = 0;
        private const int SQLITE_ROW = 100;
        private const int SQLITE_DONE = 101;
        private const int SQLITE_NULL = 5;

        private const int SQLITE_OPEN_READONLY = 0x00000001;
        private const int SQLITE_OPEN_READWRITE = 0x00000002;
        private const int SQLITE_OPEN_CREATE = 0x00000004;

        private const int BusyTimeoutMs = 5000;
        private const int MaxFirstPromptSnippetChars = 1200;
        private const int MaxSearchTerms = 64;

        private static readonly IntPtr SQLITE_TRANSIENT = new IntPtr(-1);
        private static readonly byte[] EmptyPinSentinel = new byte[1] { 0 };

        public static string LastError { get; private set; }

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, IntPtr zVfs);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_close(IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_busy_timeout", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_busy_timeout(IntPtr db, int ms);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_prepare_v2(IntPtr db, byte[] zSql, int nByte, out IntPtr ppStmt, IntPtr pzTail);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_step(IntPtr pStmt);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_finalize(IntPtr pStmt);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_changes", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_changes(IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_errmsg", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr sqlite3_errmsg(IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_exec", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_exec(IntPtr db, byte[] sql, IntPtr callback, IntPtr arg, IntPtr errMsg);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_text", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_bind_text(IntPtr pStmt, int iCol, IntPtr zData, int nByte, IntPtr xDel);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_blob", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_bind_blob(IntPtr pStmt, int iCol, IntPtr zData, int nData, IntPtr xDel);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_int", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_bind_int(IntPtr pStmt, int iCol, int value);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_int64", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_bind_int64(IntPtr pStmt, int iCol, long value);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_null", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_bind_null(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_type", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_column_type(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr sqlite3_column_text(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_bytes", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_column_bytes(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_blob", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr sqlite3_column_blob(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_int", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_column_int(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_int64", CallingConvention = CallingConvention.Winapi)]
        private static extern long sqlite3_column_int64(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_backup_init", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr sqlite3_backup_init(IntPtr pDest, byte[] zDestName, IntPtr pSource, byte[] zSourceName);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_backup_step", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_backup_step(IntPtr pBackup, int nPage);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_backup_finish", CallingConvention = CallingConvention.Winapi)]
        private static extern int sqlite3_backup_finish(IntPtr pBackup);

        private delegate int StmtBinder(IntPtr stmt);
        private delegate bool WriteTxnBody(IntPtr db);

        private static string GetSqliteErrorMessage(IntPtr db)
        {
            if (db == IntPtr.Zero) return "db is null";
            try
            {
                IntPtr ptr = sqlite3_errmsg(db);
                if (ptr == IntPtr.Zero) return "unknown sqlite error";
                int len = 0;
                while (len < 1024 && Marshal.ReadByte(ptr, len) != 0) len++;
                byte[] buf = new byte[len];
                Marshal.Copy(ptr, buf, 0, len);
                return Encoding.UTF8.GetString(buf);
            }
            catch
            {
                return "unknown sqlite error";
            }
        }

        private static bool OpenSqliteDb(string dbPath, int flags, out IntPtr db, out string err)
        {
            db = IntPtr.Zero;
            err = null;
            try
            {
                byte[] pathBytes = SqliteMetadataUpdater.ToUtf8NullTerminated(dbPath);
                int openRc = sqlite3_open_v2(pathBytes, out db, flags, IntPtr.Zero);
                if (openRc != SQLITE_OK || db == IntPtr.Zero)
                {
                    err = "sqlite3_open_v2 失败 (" + openRc + "): " + GetSqliteErrorMessage(db);
                    if (db != IntPtr.Zero)
                    {
                        try { sqlite3_close(db); } catch {}
                        db = IntPtr.Zero;
                    }
                    return false;
                }
                sqlite3_busy_timeout(db, BusyTimeoutMs);
                return true;
            }
            catch (DllNotFoundException ex)
            {
                err = "未找到 winsqlite3.dll: " + ex.Message;
                db = IntPtr.Zero;
                return false;
            }
            catch (Exception ex)
            {
                err = ex.Message;
                if (db != IntPtr.Zero)
                {
                    try { sqlite3_close(db); } catch {}
                    db = IntPtr.Zero;
                }
                return false;
            }
        }

        private static bool ExecSql(IntPtr db, string sql, out string err)
        {
            err = null;
            byte[] sqlBytes = SqliteMetadataUpdater.ToUtf8NullTerminated(sql);
            int rc = sqlite3_exec(db, sqlBytes, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (rc != SQLITE_OK)
            {
                err = "SQL 执行失败 (" + rc + "): " + GetSqliteErrorMessage(db);
                return false;
            }
            return true;
        }

        private static bool PrepareStmt(IntPtr db, string sql, out IntPtr stmt, out string err)
        {
            stmt = IntPtr.Zero;
            err = null;
            byte[] sqlBytes = SqliteMetadataUpdater.ToUtf8NullTerminated(sql);
            int rc = sqlite3_prepare_v2(db, sqlBytes, -1, out stmt, IntPtr.Zero);
            if (rc != SQLITE_OK || stmt == IntPtr.Zero)
            {
                err = "SQL prepare 失败 (" + rc + "): " + GetSqliteErrorMessage(db);
                if (stmt != IntPtr.Zero)
                {
                    sqlite3_finalize(stmt);
                    stmt = IntPtr.Zero;
                }
                return false;
            }
            return true;
        }

        private static int BindText(IntPtr stmt, int col, string val)
        {
            if (val == null)
            {
                return sqlite3_bind_null(stmt, col);
            }
            byte[] utf8 = Encoding.UTF8.GetBytes(val);
            byte[] pinnedArr = utf8.Length > 0 ? utf8 : EmptyPinSentinel;
            GCHandle pin = GCHandle.Alloc(pinnedArr, GCHandleType.Pinned);
            try
            {
                return sqlite3_bind_text(stmt, col, pin.AddrOfPinnedObject(), utf8.Length, SQLITE_TRANSIENT);
            }
            finally
            {
                if (pin.IsAllocated) pin.Free();
            }
        }

        private static int BindBlob(IntPtr stmt, int col, byte[] data)
        {
            if (data == null)
            {
                return sqlite3_bind_null(stmt, col);
            }
            byte[] pinnedArr = data.Length > 0 ? data : EmptyPinSentinel;
            GCHandle pin = GCHandle.Alloc(pinnedArr, GCHandleType.Pinned);
            try
            {
                return sqlite3_bind_blob(stmt, col, pin.AddrOfPinnedObject(), data.Length, SQLITE_TRANSIENT);
            }
            finally
            {
                if (pin.IsAllocated) pin.Free();
            }
        }

        private static string ReadColumnString(IntPtr stmt, int col)
        {
            if (sqlite3_column_type(stmt, col) == SQLITE_NULL) return null;
            IntPtr ptr = sqlite3_column_text(stmt, col);
            if (ptr == IntPtr.Zero) return string.Empty;
            int len = sqlite3_column_bytes(stmt, col);
            if (len <= 0) return string.Empty;
            byte[] buf = new byte[len];
            Marshal.Copy(ptr, buf, 0, len);
            return Encoding.UTF8.GetString(buf);
        }

        private static byte[] ReadColumnBlob(IntPtr stmt, int col)
        {
            if (sqlite3_column_type(stmt, col) == SQLITE_NULL) return null;
            IntPtr ptr = sqlite3_column_blob(stmt, col);
            int len = sqlite3_column_bytes(stmt, col);
            if (len <= 0 || ptr == IntPtr.Zero) return new byte[0];
            byte[] buf = new byte[len];
            Marshal.Copy(ptr, buf, 0, len);
            return buf;
        }

        private static int ReadColumnInt(IntPtr stmt, int col, int defaultValue)
        {
            if (sqlite3_column_type(stmt, col) == SQLITE_NULL) return defaultValue;
            return sqlite3_column_int(stmt, col);
        }

        private static bool ExecuteNonQuery(IntPtr db, string sql, StmtBinder binder, out int changes, out string err)
        {
            changes = 0;
            err = null;
            IntPtr stmt = IntPtr.Zero;
            if (!PrepareStmt(db, sql, out stmt, out err)) return false;
            try
            {
                if (binder != null)
                {
                    int bindRc = binder(stmt);
                    if (bindRc != SQLITE_OK)
                    {
                        err = "SQL bind 失败 (" + bindRc + "): " + GetSqliteErrorMessage(db);
                        return false;
                    }
                }
                int stepRc = sqlite3_step(stmt);
                if (stepRc != SQLITE_DONE)
                {
                    err = "SQL step 失败 (" + stepRc + "): " + GetSqliteErrorMessage(db);
                    return false;
                }
                changes = sqlite3_changes(db);
                return true;
            }
            finally
            {
                if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
            }
        }

        private static bool TableExists(IntPtr db, string tableName)
        {
            if (db == IntPtr.Zero || string.IsNullOrEmpty(tableName)) return false;
            IntPtr stmt = IntPtr.Zero;
            string err;
            if (!PrepareStmt(db, "SELECT 1 FROM sqlite_master WHERE type IN ('table', 'view') AND name = ? LIMIT 1;", out stmt, out err))
                return false;
            try
            {
                if (BindText(stmt, 1, tableName) != SQLITE_OK) return false;
                return sqlite3_step(stmt) == SQLITE_ROW;
            }
            finally
            {
                if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
            }
        }

        private static bool IsSafeIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok) return false;
            }
            return true;
        }

        private static HashSet<string> GetTableColumns(IntPtr db, string tableName)
        {
            HashSet<string> cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (db == IntPtr.Zero || !IsSafeIdentifier(tableName)) return cols;
            IntPtr stmt = IntPtr.Zero;
            string err;
            if (!PrepareStmt(db, "PRAGMA table_info(" + tableName + ");", out stmt, out err))
                return cols;
            try
            {
                while (sqlite3_step(stmt) == SQLITE_ROW)
                {
                    string colName = ReadColumnString(stmt, 1);
                    if (!string.IsNullOrEmpty(colName)) cols.Add(colName.Trim());
                }
            }
            finally
            {
                if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
            }
            return cols;
        }

        private static void RollbackQuietly(IntPtr db)
        {
            if (db == IntPtr.Zero) return;
            try
            {
                string dummy;
                ExecSql(db, "ROLLBACK;", out dummy);
            }
            catch {}
        }

        private static bool ExecuteWriteTransaction(string dbPath, WriteTxnBody body)
        {
            LastError = null;
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                LastError = "数据库文件不存在: " + dbPath;
                return false;
            }

            IntPtr db = IntPtr.Zero;
            bool inTxn = false;
            try
            {
                string err;
                if (!OpenSqliteDb(dbPath, SQLITE_OPEN_READWRITE, out db, out err))
                {
                    LastError = err;
                    return false;
                }

                if (!ExecSql(db, "BEGIN IMMEDIATE;", out err))
                {
                    LastError = err;
                    return false;
                }
                inTxn = true;

                if (!body(db))
                {
                    RollbackQuietly(db);
                    inTxn = false;
                    return false;
                }

                if (!ExecSql(db, "COMMIT;", out err))
                {
                    LastError = err;
                    RollbackQuietly(db);
                    inTxn = false;
                    return false;
                }
                inTxn = false;
                return true;
            }
            catch (DllNotFoundException ex)
            {
                LastError = "未找到 winsqlite3.dll: " + ex.Message;
                if (inTxn) RollbackQuietly(db);
                return false;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                if (inTxn) RollbackQuietly(db);
                return false;
            }
            finally
            {
                if (db != IntPtr.Zero)
                {
                    try { sqlite3_close(db); } catch {}
                }
            }
        }

        private static List<string> CollectDistinctCids(IEnumerable<string> cids)
        {
            List<string> list = new List<string>();
            if (cids == null) return list;
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in cids)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                string trimmed = raw.Trim();
                if (seen.Add(trimmed)) list.Add(trimmed);
            }
            return list;
        }

        public static List<KeyValuePair<string, string>> ExtractGitBranchCandidatesFromRawSummary(byte[] rawSummary)
        {
            List<KeyValuePair<string, string>> results = new List<KeyValuePair<string, string>>();
            if (rawSummary == null || rawSummary.Length == 0) return results;
            try
            {
                List<ProtoNode> nodes = ProtoCodec.Decode(rawSummary);
                ProtoCodec.DecodeMetaContainerList(nodes);
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < nodes.Count; i++)
                {
                    ProtoNode f9 = nodes[i];
                    if (f9.FieldNumber != 9 || f9.WireType != 2) continue;
                    if (!f9.HasChildren && f9.RawBytes != null && f9.RawBytes.Length > 0)
                    {
                        List<ProtoNode> sub9;
                        if (ProtoCodec.TryDecodeStrict(f9.RawBytes, out sub9)) f9.Children = sub9;
                    }
                    if (!f9.HasChildren) continue;

                    string repoPath = string.Empty;
                    string directBranch = null;
                    string legacyNestedBranch = null;

                    for (int j = 0; j < f9.Children.Count; j++)
                    {
                        ProtoNode ch = f9.Children[j];
                        if (ch.FieldNumber == 4 && ch.WireType == 2 && directBranch == null)
                        {
                            string b4;
                            if (ch.TryGetStrictUtf8String(out b4) && !string.IsNullOrWhiteSpace(b4))
                            {
                                directBranch = b4.Trim();
                            }
                        }
                        else if (ch.FieldNumber == 3 && ch.WireType == 2)
                        {
                            List<ProtoNode> sub3 = ch.HasChildren ? ch.Children : null;
                            if (sub3 == null && ch.RawBytes != null && ch.RawBytes.Length > 0)
                            {
                                ProtoCodec.TryDecodeStrict(ch.RawBytes, out sub3);
                            }
                            if (sub3 != null)
                            {
                                for (int k = 0; k < sub3.Count; k++)
                                {
                                    ProtoNode inner = sub3[k];
                                    if (inner.FieldNumber == 1 && inner.WireType == 2 && string.IsNullOrEmpty(repoPath))
                                    {
                                        string rp;
                                        if (inner.TryGetStrictUtf8String(out rp) && !string.IsNullOrWhiteSpace(rp))
                                            repoPath = rp.Trim();
                                    }
                                    else if (inner.FieldNumber == 4 && inner.WireType == 2 && legacyNestedBranch == null)
                                    {
                                        string b34;
                                        if (inner.TryGetStrictUtf8String(out b34) && !string.IsNullOrWhiteSpace(b34))
                                            legacyNestedBranch = b34.Trim();
                                    }
                                }
                            }
                        }
                    }

                    string effectiveBranch = !string.IsNullOrEmpty(directBranch) ? directBranch : legacyNestedBranch;
                    if (!string.IsNullOrEmpty(effectiveBranch))
                    {
                        string dedup = (repoPath ?? string.Empty) + "|" + effectiveBranch;
                        if (seen.Add(dedup))
                        {
                            results.Add(new KeyValuePair<string, string>(repoPath ?? string.Empty, effectiveBranch));
                        }
                    }
                }
            }
            catch {}
            return results;
        }

        private static string ExtractGitBranchHintFromRawSummary(byte[] rawSummary)
        {
            List<KeyValuePair<string, string>> candidates = ExtractGitBranchCandidatesFromRawSummary(rawSummary);
            if (candidates.Count == 0) return null;

            for (int i = 0; i < candidates.Count; i++)
            {
                string b = candidates[i].Value;
                if (!string.IsNullOrEmpty(b) &&
                    !string.Equals(b, "main", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(b, "master", StringComparison.OrdinalIgnoreCase))
                {
                    return b;
                }
            }
            return candidates[0].Value;
        }

        /// <summary>
        /// 只读读取 summary.sqlite 的 conversation_summaries 快照（附带 GitBranchHint 与 FTS 首条提示词摘要）
        /// </summary>
        public static Dictionary<string, SummaryDbRowInfo> ReadSummaryDbSnapshot(string dbPath)
        {
            LastError = null;
            Dictionary<string, SummaryDbRowInfo> snapshot = new Dictionary<string, SummaryDbRowInfo>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                LastError = "数据库文件不存在: " + dbPath;
                return snapshot;
            }
            try
            {
                if (new FileInfo(dbPath).Length == 0) return snapshot;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return snapshot;
            }

            IntPtr db = IntPtr.Zero;
            try
            {
                string err;
                if (!OpenSqliteDb(dbPath, SQLITE_OPEN_READONLY, out db, out err))
                {
                    LastError = err;
                    return snapshot;
                }

                HashSet<string> tableCols = GetTableColumns(db, "conversation_summaries");
                if (tableCols.Count == 0 || !tableCols.Contains("conversation_id"))
                {
                    LastError = "conversation_summaries 表不存在或缺少 conversation_id 列。";
                    return snapshot;
                }

                string[] knownCols = new string[]
                {
                    "conversation_id", "title", "preview", "step_count", "last_modified_time",
                    "workspace_uris", "status", "project_id", "agent_name", "parent_conversation_id",
                    "nesting_depth", "not_fully_idle", "raw_summary", "app_data_dir", "source"
                };
                int[] colPos = new int[knownCols.Length];
                List<string> selectedCols = new List<string>(knownCols.Length);
                for (int i = 0; i < knownCols.Length; i++)
                {
                    if (tableCols.Contains(knownCols[i]))
                    {
                        colPos[i] = selectedCols.Count;
                        selectedCols.Add(knownCols[i]);
                    }
                    else
                    {
                        colPos[i] = -1;
                    }
                }

                IntPtr stmt = IntPtr.Zero;
                string sql = "SELECT " + string.Join(", ", selectedCols.ToArray()) + " FROM conversation_summaries;";
                if (!PrepareStmt(db, sql, out stmt, out err))
                {
                    LastError = err;
                    return snapshot;
                }

                try
                {
                    int stepRc;
                    while ((stepRc = sqlite3_step(stmt)) == SQLITE_ROW)
                    {
                        string cid = ReadColumnString(stmt, colPos[0]);
                        if (string.IsNullOrWhiteSpace(cid)) continue;
                        cid = cid.Trim();

                        SummaryDbRowInfo row = new SummaryDbRowInfo();
                        row.ConversationId = cid;
                        if (colPos[1] >= 0) row.Title = ReadColumnString(stmt, colPos[1]);
                        if (colPos[2] >= 0) row.Preview = ReadColumnString(stmt, colPos[2]);
                        if (colPos[3] >= 0) row.StepCount = ReadColumnInt(stmt, colPos[3], 0);
                        if (colPos[4] >= 0) row.LastModifiedTime = ReadColumnString(stmt, colPos[4]);
                        if (colPos[5] >= 0) row.WorkspaceUris = ReadColumnString(stmt, colPos[5]);
                        if (colPos[6] >= 0) row.Status = ReadColumnString(stmt, colPos[6]);
                        if (colPos[7] >= 0) row.ProjectId = ReadColumnString(stmt, colPos[7]);
                        if (colPos[8] >= 0) row.AgentName = ReadColumnString(stmt, colPos[8]);
                        if (colPos[9] >= 0) row.ParentConversationId = ReadColumnString(stmt, colPos[9]);
                        if (colPos[10] >= 0) row.NestingDepth = ReadColumnInt(stmt, colPos[10], 0);
                        if (colPos[11] >= 0) row.NotFullyIdle = ReadColumnInt(stmt, colPos[11], 0) != 0;
                        if (colPos[12] >= 0) row.RawSummaryBytes = ReadColumnBlob(stmt, colPos[12]);
                        if (colPos[13] >= 0) row.AppDataDir = ReadColumnString(stmt, colPos[13]);
                        if (colPos[14] >= 0) row.Source = ReadColumnString(stmt, colPos[14]);

                        row.RepoBranchHints = ExtractGitBranchCandidatesFromRawSummary(row.RawSummaryBytes);
                        if (row.RepoBranchHints.Count > 0)
                        {
                            string nonMain = null;
                            for (int bi = 0; bi < row.RepoBranchHints.Count; bi++)
                            {
                                string cand = row.RepoBranchHints[bi].Value;
                                if (!string.IsNullOrEmpty(cand) &&
                                    !string.Equals(cand, "main", StringComparison.OrdinalIgnoreCase) &&
                                    !string.Equals(cand, "master", StringComparison.OrdinalIgnoreCase))
                                {
                                    nonMain = cand;
                                    break;
                                }
                            }
                            row.GitBranchHint = nonMain ?? row.RepoBranchHints[0].Value;
                        }

                        snapshot[cid] = row;
                    }
                    if (stepRc != SQLITE_DONE)
                    {
                        LastError = "读取 conversation_summaries 中断 (" + stepRc + "): " + GetSqliteErrorMessage(db);
                    }
                }
                finally
                {
                    if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
                }

                if (TableExists(db, "conversations_fts"))
                {
                    IntPtr ftsStmt = IntPtr.Zero;
                    string ftsErr;
                    if (PrepareStmt(db, "SELECT conversation_id, user_body FROM conversations_fts WHERE chunk_index = 0;", out ftsStmt, out ftsErr))
                    {
                        try
                        {
                            while (sqlite3_step(ftsStmt) == SQLITE_ROW)
                            {
                                string cid = ReadColumnString(ftsStmt, 0);
                                if (string.IsNullOrWhiteSpace(cid)) continue;
                                SummaryDbRowInfo row;
                                if (!snapshot.TryGetValue(cid.Trim(), out row) || !string.IsNullOrEmpty(row.FirstPromptSnippet))
                                    continue;
                                string body = ReadColumnString(ftsStmt, 1);
                                if (!string.IsNullOrEmpty(body))
                                {
                                    row.FirstPromptSnippet = body.Length > MaxFirstPromptSnippetChars
                                        ? body.Substring(0, MaxFirstPromptSnippetChars)
                                        : body;
                                }
                            }
                        }
                        finally
                        {
                            if (ftsStmt != IntPtr.Zero) sqlite3_finalize(ftsStmt);
                        }
                    }
                }

                return snapshot;
            }
            catch (DllNotFoundException ex)
            {
                LastError = "未找到 winsqlite3.dll: " + ex.Message;
                return snapshot;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return snapshot;
            }
            finally
            {
                if (db != IntPtr.Zero)
                {
                    try { sqlite3_close(db); } catch {}
                }
            }
        }

        /// <summary>
        /// 读取全量活跃会话 ID（失败封闭：若非空库且打开/查询失败则抛 InvalidOperationException 防止孤儿误删）
        /// </summary>
        public static HashSet<string> ReadAllConversationIds(string dbPath)
        {
            LastError = null;
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
                return ids;

            FileInfo fi = new FileInfo(dbPath);
            if (fi.Length == 0)
                return ids;

            IntPtr db = IntPtr.Zero;
            IntPtr stmt = IntPtr.Zero;
            try
            {
                string err;
                if (!OpenSqliteDb(dbPath, SQLITE_OPEN_READONLY, out db, out err))
                {
                    LastError = err;
                    throw new InvalidOperationException("无法只读打开 summary.sqlite: " + err);
                }

                if (!PrepareStmt(db, "SELECT conversation_id FROM conversation_summaries;", out stmt, out err))
                {
                    LastError = err;
                    throw new InvalidOperationException("查询 conversation_summaries 失败: " + err);
                }

                int stepRc;
                while ((stepRc = sqlite3_step(stmt)) == SQLITE_ROW)
                {
                    string cid = ReadColumnString(stmt, 0);
                    if (!string.IsNullOrWhiteSpace(cid))
                    {
                        ids.Add(cid.Trim());
                    }
                }
                if (stepRc != SQLITE_DONE)
                {
                    LastError = "sqlite3_step 未正常完成 (" + stepRc + "): " + GetSqliteErrorMessage(db);
                    throw new InvalidOperationException("遍历 conversation_summaries 失败: " + LastError);
                }
                return ids;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (DllNotFoundException ex)
            {
                LastError = "未找到 winsqlite3.dll: " + ex.Message;
                throw new InvalidOperationException(LastError, ex);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                throw new InvalidOperationException("读取 summary.sqlite 会话 ID 失败: " + ex.Message, ex);
            }
            finally
            {
                if (stmt != IntPtr.Zero) try { sqlite3_finalize(stmt); } catch {}
                if (db != IntPtr.Zero) try { sqlite3_close(db); } catch {}
            }
        }

        /// <summary>
        /// 事务级批量更新标题与 raw_summary AST (Field 1 + Field 15->1) 及 FTS 标题，并产出可精确回滚快照
        /// </summary>
        public static bool UpdateSummaryTitlesTransaction(
            string dbPath,
            Dictionary<string, string> cidToNewTitle,
            out Dictionary<string, SummaryDbRollbackEntry> rollbackEntries,
            out List<string> warnings)
        {
            Dictionary<string, SummaryDbRollbackEntry> rbMap = new Dictionary<string, SummaryDbRollbackEntry>(StringComparer.OrdinalIgnoreCase);
            List<string> warnList = new List<string>();
            rollbackEntries = rbMap;
            warnings = warnList;
            LastError = null;

            if (cidToNewTitle == null || cidToNewTitle.Count == 0)
                return true;

            bool ok = ExecuteWriteTransaction(dbPath, delegate(IntPtr db)
            {
                bool hasFts = TableExists(db, "conversations_fts");
                HashSet<string> ftsCols = hasFts ? GetTableColumns(db, "conversations_fts") : null;
                foreach (KeyValuePair<string, string> kvp in cidToNewTitle)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value))
                    {
                        warnList.Add("跳过空 CID 或空标题。");
                        continue;
                    }
                    string cid = kvp.Key.Trim();
                    string newTitle = kvp.Value.Trim();

                    string oldTitle = null;
                    string oldPreview = null;
                    byte[] oldRaw = null;
                    bool foundRow = false;

                    IntPtr selStmt = IntPtr.Zero;
                    string err;
                    if (!PrepareStmt(db, "SELECT title, preview, raw_summary FROM conversation_summaries WHERE conversation_id = ? LIMIT 1;", out selStmt, out err))
                    {
                        LastError = err;
                        return false;
                    }
                    try
                    {
                        if (BindText(selStmt, 1, cid) != SQLITE_OK)
                        {
                            LastError = "SELECT title 参数绑定失败: " + GetSqliteErrorMessage(db);
                            return false;
                        }
                        int stepRc = sqlite3_step(selStmt);
                        if (stepRc == SQLITE_ROW)
                        {
                            foundRow = true;
                            oldTitle = ReadColumnString(selStmt, 0);
                            oldPreview = ReadColumnString(selStmt, 1);
                            oldRaw = ReadColumnBlob(selStmt, 2);
                        }
                        else if (stepRc != SQLITE_DONE)
                        {
                            LastError = "SELECT title 读取失败 (" + stepRc + "): " + GetSqliteErrorMessage(db);
                            return false;
                        }
                    }
                    finally
                    {
                        if (selStmt != IntPtr.Zero) sqlite3_finalize(selStmt);
                    }

                    if (!foundRow)
                    {
                        warnList.Add("conversation_summaries 未找到会话: " + cid);
                        continue;
                    }

                    SummaryDbRollbackEntry rb = new SummaryDbRollbackEntry();
                    rb.ConversationId = cid;
                    rb.OldTitle = oldTitle;
                    rb.OldPreview = oldPreview;
                    rb.OldRawSummary = oldRaw;
                    rbMap[cid] = rb;

                    byte[] newRaw = oldRaw;
                    if (oldRaw != null && oldRaw.Length > 0)
                    {
                        try
                        {
                            List<ProtoNode> nodes = ProtoCodec.Decode(oldRaw);
                            ProtoCodec.DecodeMetaContainerList(nodes);
                            IncrementalProtoShardMerger.SyncTitlesInMetaChildren(nodes, newTitle, true, true);
                            newRaw = ProtoCodec.Encode(nodes);
                        }
                        catch (Exception ex)
                        {
                            warnList.Add("raw_summary 标题 AST 同步失败 (" + cid + "): " + ex.Message);
                            newRaw = oldRaw;
                        }
                    }

                    int changes;
                    if (!ExecuteNonQuery(
                        db,
                        "UPDATE conversation_summaries SET title = ?, preview = ?, raw_summary = ? WHERE conversation_id = ?;",
                        delegate(IntPtr uStmt)
                        {
                            int rc = BindText(uStmt, 1, newTitle);
                            if (rc != SQLITE_OK) return rc;
                            rc = BindText(uStmt, 2, newTitle);
                            if (rc != SQLITE_OK) return rc;
                            rc = BindBlob(uStmt, 3, newRaw);
                            if (rc != SQLITE_OK) return rc;
                            return BindText(uStmt, 4, cid);
                        },
                        out changes,
                        out err))
                    {
                        LastError = err;
                        return false;
                    }

                    if (hasFts && ftsCols != null)
                    {
                        string ftsErr;
                        if (!UpdateFtsBindingMetadata(db, ftsCols, cid, newTitle, null, null, true, false, out ftsErr))
                        {
                            LastError = ftsErr;
                            return false;
                        }
                    }
                }
                return true;
            });

            if (!ok)
            {
                rbMap.Clear();
            }
            return ok;
        }

        /// <summary>
        /// 事务级精确还原标题、preview、raw_summary 字节及 FTS 标题
        /// </summary>
        public static bool RestoreSummaryRowsTransaction(
            string dbPath,
            Dictionary<string, SummaryDbRollbackEntry> restoreMap,
            out List<string> warnings)
        {
            List<string> warnList = new List<string>();
            warnings = warnList;
            LastError = null;

            if (restoreMap == null || restoreMap.Count == 0)
                return true;

            return ExecuteWriteTransaction(dbPath, delegate(IntPtr db)
            {
                bool hasFts = TableExists(db, "conversations_fts");
                HashSet<string> ftsCols = hasFts ? GetTableColumns(db, "conversations_fts") : null;
                foreach (KeyValuePair<string, SummaryDbRollbackEntry> kvp in restoreMap)
                {
                    SummaryDbRollbackEntry entry = kvp.Value;
                    if (entry == null) continue;
                    string cid = !string.IsNullOrEmpty(entry.ConversationId) ? entry.ConversationId.Trim() : kvp.Key;
                    if (string.IsNullOrWhiteSpace(cid)) continue;

                    int changes;
                    string err;
                    if (!ExecuteNonQuery(
                        db,
                        "UPDATE conversation_summaries SET title = ?, preview = ?, raw_summary = ? WHERE conversation_id = ?;",
                        delegate(IntPtr uStmt)
                        {
                            int rc = BindText(uStmt, 1, entry.OldTitle);
                            if (rc != SQLITE_OK) return rc;
                            rc = BindText(uStmt, 2, entry.OldPreview);
                            if (rc != SQLITE_OK) return rc;
                            rc = BindBlob(uStmt, 3, entry.OldRawSummary);
                            if (rc != SQLITE_OK) return rc;
                            return BindText(uStmt, 4, cid);
                        },
                        out changes,
                        out err))
                    {
                        LastError = err;
                        return false;
                    }

                    if (changes == 0)
                    {
                        warnList.Add("回滚未命中 conversation_summaries 行: " + cid);
                    }

                    if (hasFts && ftsCols != null)
                    {
                        string ftsErr;
                        if (!UpdateFtsBindingMetadata(db, ftsCols, cid, entry.OldTitle, null, null, true, false, out ftsErr))
                        {
                            LastError = ftsErr;
                            return false;
                        }
                    }
                }
                return true;
            });
        }

        private static bool CanUseFts5Match(List<string> tokens)
        {
            if (tokens == null || tokens.Count == 0) return false;
            for (int i = 0; i < tokens.Count; i++)
            {
                string tok = tokens[i];
                if (tok.Length < 2) return false;
                if (tok == "AND" || tok == "OR" || tok == "NOT" || tok == "NEAR") return false;
                bool hasAlnum = false;
                for (int j = 0; j < tok.Length; j++)
                {
                    char c = tok[j];
                    bool isAlnum = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                    if (isAlnum) hasAlnum = true;
                    else if (c != '_') return false;
                }
                if (!hasAlnum) return false;
            }
            return true;
        }

        private static string EscapeSqlLikePattern(string term)
        {
            StringBuilder sb = new StringBuilder(term.Length + 4);
            sb.Append('%');
            for (int i = 0; i < term.Length; i++)
            {
                char c = term[i];
                if (c == '\\' || c == '%' || c == '_')
                {
                    sb.Append('\\');
                }
                sb.Append(c);
            }
            sb.Append('%');
            return sb.ToString();
        }

        private static bool QueryLikeAcrossColumns(
            IntPtr db,
            string tableName,
            bool distinct,
            List<string> searchCols,
            List<string> terms,
            HashSet<string> hits,
            out string err)
        {
            err = null;
            if (searchCols == null || searchCols.Count == 0 || terms == null || terms.Count == 0)
                return true;

            StringBuilder sql = new StringBuilder();
            sql.Append(distinct ? "SELECT DISTINCT conversation_id FROM " : "SELECT conversation_id FROM ");
            sql.Append(tableName);
            sql.Append(" WHERE ");

            List<string> patterns = new List<string>(terms.Count);
            for (int t = 0; t < terms.Count; t++)
            {
                patterns.Add(EscapeSqlLikePattern(terms[t]));
                if (t > 0) sql.Append(" AND ");
                sql.Append("(");
                for (int c = 0; c < searchCols.Count; c++)
                {
                    if (c > 0) sql.Append(" OR ");
                    sql.Append(searchCols[c]);
                    sql.Append(" LIKE ? ESCAPE '\\'");
                }
                sql.Append(")");
            }
            sql.Append(";");

            IntPtr stmt = IntPtr.Zero;
            if (!PrepareStmt(db, sql.ToString(), out stmt, out err))
                return false;

            try
            {
                int bindIdx = 1;
                for (int t = 0; t < patterns.Count; t++)
                {
                    for (int c = 0; c < searchCols.Count; c++)
                    {
                        int rc = BindText(stmt, bindIdx++, patterns[t]);
                        if (rc != SQLITE_OK)
                        {
                            err = "LIKE 参数绑定失败 (" + rc + "): " + GetSqliteErrorMessage(db);
                            return false;
                        }
                    }
                }

                int stepRc;
                while ((stepRc = sqlite3_step(stmt)) == SQLITE_ROW)
                {
                    string cid = ReadColumnString(stmt, 0);
                    if (!string.IsNullOrWhiteSpace(cid)) hits.Add(cid.Trim());
                }
                if (stepRc != SQLITE_DONE)
                {
                    err = "LIKE 查询中断 (" + stepRc + "): " + GetSqliteErrorMessage(db);
                    return false;
                }
                return true;
            }
            finally
            {
                if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
            }
        }

        /// <summary>
        /// 混合路由全文检索：安全 ASCII 标识符走 FTS5 MATCH，其余走参数化 LIKE ESCAPE '\'，并联合 conversation_summaries 兜底
        /// </summary>
        public static HashSet<string> SearchConversationsFullText(string dbPath, string rawQuery)
        {
            LastError = null;
            HashSet<string> hits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(rawQuery) || string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
                return hits;

            try
            {
                if (new FileInfo(dbPath).Length == 0) return hits;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return hits;
            }

            string[] split = rawQuery.Trim().Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length == 0) return hits;

            List<string> terms = new List<string>(Math.Min(split.Length, MaxSearchTerms));
            for (int i = 0; i < split.Length && terms.Count < MaxSearchTerms; i++)
            {
                if (!string.IsNullOrEmpty(split[i])) terms.Add(split[i]);
            }
            if (terms.Count == 0) return hits;

            bool useFts5Match = CanUseFts5Match(terms);

            IntPtr db = IntPtr.Zero;
            try
            {
                string err;
                if (!OpenSqliteDb(dbPath, SQLITE_OPEN_READONLY, out db, out err))
                {
                    LastError = err;
                    return hits;
                }

                if (TableExists(db, "conversations_fts"))
                {
                    HashSet<string> ftsCols = GetTableColumns(db, "conversations_fts");
                    bool ftsMatched = false;

                    if (useFts5Match)
                    {
                        StringBuilder mq = new StringBuilder();
                        for (int i = 0; i < terms.Count; i++)
                        {
                            if (i > 0) mq.Append(" AND ");
                            mq.Append('"').Append(terms[i]).Append("\"*");
                        }

                        IntPtr ftsStmt = IntPtr.Zero;
                        string matchErr;
                        if (PrepareStmt(db, "SELECT DISTINCT conversation_id FROM conversations_fts WHERE conversations_fts MATCH ?;", out ftsStmt, out matchErr))
                        {
                            try
                            {
                                if (BindText(ftsStmt, 1, mq.ToString()) == SQLITE_OK)
                                {
                                    int stepRc;
                                    while ((stepRc = sqlite3_step(ftsStmt)) == SQLITE_ROW)
                                    {
                                        string cid = ReadColumnString(ftsStmt, 0);
                                        if (!string.IsNullOrWhiteSpace(cid)) hits.Add(cid.Trim());
                                    }
                                    if (stepRc == SQLITE_DONE)
                                    {
                                        ftsMatched = true;
                                    }
                                    else
                                    {
                                        LastError = "FTS5 MATCH 查询失败 (" + stepRc + "): " + GetSqliteErrorMessage(db);
                                    }
                                }
                            }
                            finally
                            {
                                if (ftsStmt != IntPtr.Zero) sqlite3_finalize(ftsStmt);
                            }
                        }
                        else
                        {
                            LastError = matchErr;
                        }
                    }

                    if (!ftsMatched && ftsCols.Contains("conversation_id"))
                    {
                        List<string> searchFtsCols = new List<string>(3);
                        if (ftsCols.Contains("title")) searchFtsCols.Add("title");
                        if (ftsCols.Contains("user_body")) searchFtsCols.Add("user_body");
                        if (ftsCols.Contains("agent_body")) searchFtsCols.Add("agent_body");

                        string likeErr;
                        if (!QueryLikeAcrossColumns(db, "conversations_fts", true, searchFtsCols, terms, hits, out likeErr))
                        {
                            LastError = likeErr;
                        }
                    }
                }

                if (TableExists(db, "conversation_summaries"))
                {
                    HashSet<string> sumCols = GetTableColumns(db, "conversation_summaries");
                    if (sumCols.Contains("conversation_id"))
                    {
                        List<string> searchSumCols = new List<string>(2);
                        if (sumCols.Contains("title")) searchSumCols.Add("title");
                        if (sumCols.Contains("preview")) searchSumCols.Add("preview");

                        string sumErr;
                        if (!QueryLikeAcrossColumns(db, "conversation_summaries", false, searchSumCols, terms, hits, out sumErr))
                        {
                            LastError = sumErr;
                        }
                    }
                }

                return hits;
            }
            catch (DllNotFoundException ex)
            {
                LastError = "未找到 winsqlite3.dll: " + ex.Message;
                return hits;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return hits;
            }
            finally
            {
                if (db != IntPtr.Zero)
                {
                    try { sqlite3_close(db); } catch {}
                }
            }
        }

        /// <summary>
        /// 事务级从 summary.sqlite 级联删除会话摘要、FTS 索引及父子关系记录
        /// </summary>
        public static int DeleteSessionsFromSummaryDb(string dbPath, IEnumerable<string> cids, out List<string> warnings)
        {
            List<string> warnList = new List<string>();
            warnings = warnList;
            LastError = null;

            List<string> targetCids = CollectDistinctCids(cids);
            if (targetCids.Count == 0) return 0;
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath)) return 0;

            int deletedCount = 0;
            bool ok = ExecuteWriteTransaction(dbPath, delegate(IntPtr db)
            {
                bool hasSummaries = TableExists(db, "conversation_summaries");
                bool hasFts = TableExists(db, "conversations_fts");
                bool hasParents = TableExists(db, "conversation_parents");

                for (int i = 0; i < targetCids.Count; i++)
                {
                    string cid = targetCids[i];
                    string err;
                    int changes;

                    if (hasSummaries)
                    {
                        if (!ExecuteNonQuery(
                            db,
                            "DELETE FROM conversation_summaries WHERE conversation_id = ?;",
                            delegate(IntPtr s) { return BindText(s, 1, cid); },
                            out changes,
                            out err))
                        {
                            LastError = err;
                            return false;
                        }
                        deletedCount += changes;
                    }

                    if (hasFts)
                    {
                        if (!ExecuteNonQuery(
                            db,
                            "DELETE FROM conversations_fts WHERE conversation_id = ?;",
                            delegate(IntPtr s) { return BindText(s, 1, cid); },
                            out changes,
                            out err))
                        {
                            LastError = err;
                            return false;
                        }
                    }

                    if (hasParents)
                    {
                        if (!ExecuteNonQuery(
                            db,
                            "DELETE FROM conversation_parents WHERE conversation_id = ? OR parent_id = ?;",
                            delegate(IntPtr s)
                            {
                                int rc = BindText(s, 1, cid);
                                if (rc != SQLITE_OK) return rc;
                                return BindText(s, 2, cid);
                            },
                            out changes,
                            out err))
                        {
                            LastError = err;
                            return false;
                        }
                    }
                }
                return true;
            });

            if (!ok)
            {
                if (!string.IsNullOrEmpty(LastError)) warnList.Add(LastError);
                return 0;
            }
            return deletedCount;
        }

        private static void SetOrAddUtf8Leaf(List<ProtoNode> children, int fieldNumber, string value)
        {
            if (children == null) return;
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i].FieldNumber == fieldNumber && children[i].WireType == 2)
                {
                    children[i].SetString(value);
                    return;
                }
            }
            ProtoNode added = new ProtoNode(fieldNumber, 2);
            added.SetString(value);
            children.Add(added);
        }

        private static byte[] ApplyProjectBindingToRawSummary(
            byte[] oldRaw,
            string effectiveProjName,
            string effectiveProjId,
            string effectiveFolderUri,
            bool isOutside,
            List<string> warnings,
            string cid)
        {
            if (oldRaw == null || oldRaw.Length == 0) return oldRaw;
            try
            {
                List<ProtoNode> nodes = ProtoCodec.Decode(oldRaw);
                ProtoCodec.DecodeMetaContainerList(nodes);

                if (isOutside)
                {
                    IncrementalProtoShardMerger.ClearWorkspaceFieldsInMetaChildren(nodes);
                    IncrementalProtoShardMerger.SyncProjectIdInMetaChildren(nodes, "outside-of-project", true);
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        if (nodes[i].FieldNumber == 17 && nodes[i].WireType == 2)
                        {
                            if (nodes[i].Children == null) nodes[i].Children = new List<ProtoNode>();
                            SetOrAddUtf8Leaf(nodes[i].Children, 1, string.IsNullOrEmpty(effectiveProjName) ? "Outside of Project" : effectiveProjName);
                            nodes[i].IsModified = true;
                            break;
                        }
                    }
                    return ProtoCodec.Encode(nodes);
                }

                if (!string.IsNullOrEmpty(effectiveProjId))
                {
                    IncrementalProtoShardMerger.SyncProjectIdInMetaChildren(nodes, effectiveProjId, true);
                }

                if (!string.IsNullOrEmpty(effectiveFolderUri))
                {
                    ProtoNode primaryF9 = null;
                    for (int i = nodes.Count - 1; i >= 0; i--)
                    {
                        if (nodes[i].FieldNumber == 9 && nodes[i].WireType == 2)
                        {
                            if (primaryF9 == null)
                            {
                                primaryF9 = nodes[i];
                            }
                            else
                            {
                                nodes.RemoveAt(i);
                            }
                        }
                    }

                    if (primaryF9 == null)
                    {
                        primaryF9 = new ProtoNode(9, 2) { IsModified = true };
                        SetOrAddUtf8Leaf(primaryF9.Children, 1, effectiveFolderUri);
                        nodes.Add(primaryF9);
                    }
                    else
                    {
                        if (!primaryF9.HasChildren && primaryF9.RawBytes != null && primaryF9.RawBytes.Length > 0)
                        {
                            List<ProtoNode> sub9;
                            if (ProtoCodec.TryDecodeStrict(primaryF9.RawBytes, out sub9))
                            {
                                primaryF9.Children = sub9;
                            }
                        }
                        if (primaryF9.Children == null) primaryF9.Children = new List<ProtoNode>();

                        ProtoNode f9c1 = primaryF9.GetChild(1);
                        if (f9c1 != null && f9c1.WireType == 2 && f9c1.HasChildren && f9c1.GetChild(1) != null)
                        {
                            f9c1.GetChild(1).SetString(effectiveFolderUri);
                            f9c1.IsModified = true;
                        }
                        else
                        {
                            SetOrAddUtf8Leaf(primaryF9.Children, 1, effectiveFolderUri);
                        }
                        primaryF9.IsModified = true;
                    }
                }

                if (!string.IsNullOrEmpty(effectiveProjName) || !string.IsNullOrEmpty(effectiveFolderUri))
                {
                    ProtoNode f17 = null;
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        if (nodes[i].FieldNumber == 17 && nodes[i].WireType == 2)
                        {
                            f17 = nodes[i];
                            break;
                        }
                    }
                    if (f17 == null)
                    {
                        f17 = new ProtoNode(17, 2) { IsModified = true };
                        nodes.Add(f17);
                    }
                    else if (!f17.HasChildren && f17.RawBytes != null && f17.RawBytes.Length > 0)
                    {
                        List<ProtoNode> sub17;
                        if (ProtoCodec.TryDecodeStrict(f17.RawBytes, out sub17))
                        {
                            f17.Children = sub17;
                        }
                    }
                    if (f17.Children == null) f17.Children = new List<ProtoNode>();

                    if (!string.IsNullOrEmpty(effectiveProjName))
                    {
                        SetOrAddUtf8Leaf(f17.Children, 1, effectiveProjName);
                    }
                    if (!string.IsNullOrEmpty(effectiveFolderUri))
                    {
                        SetOrAddUtf8Leaf(f17.Children, 7, effectiveFolderUri);
                    }
                    f17.IsModified = true;
                }

                return ProtoCodec.Encode(nodes);
            }
            catch (Exception ex)
            {
                warnings.Add("raw_summary 工程绑定 AST 更新失败 (" + cid + "): " + ex.Message);
                return oldRaw;
            }
        }

        private static bool UpdateFtsBindingMetadata(
            IntPtr db,
            HashSet<string> ftsCols,
            string cid,
            string title,
            string workspaceVal,
            string projectVal,
            bool updateTitle,
            bool updateWorkspaceAndProject,
            out string err)
        {
            err = null;
            List<string> assignments = new List<string>(3);
            List<string> values = new List<string>(3);

            if (updateTitle && ftsCols.Contains("title"))
            {
                assignments.Add("title = ?");
                values.Add(title);
            }
            if (updateWorkspaceAndProject)
            {
                if (ftsCols.Contains("workspace"))
                {
                    assignments.Add("workspace = ?");
                    values.Add(workspaceVal ?? string.Empty);
                }
                if (ftsCols.Contains("project"))
                {
                    assignments.Add("project = ?");
                    values.Add(projectVal ?? string.Empty);
                }
            }

            if (assignments.Count == 0) return true;

            string sql = "UPDATE conversations_fts SET " + string.Join(", ", assignments.ToArray()) +
                         " WHERE conversation_id = ? AND chunk_index = -1;";
            int changes;
            return ExecuteNonQuery(
                db,
                sql,
                delegate(IntPtr stmt)
                {
                    int idx = 1;
                    for (int i = 0; i < values.Count; i++)
                    {
                        int rc = BindText(stmt, idx++, values[i]);
                        if (rc != SQLITE_OK) return rc;
                    }
                    return BindText(stmt, idx, cid);
                },
                out changes,
                out err);
        }

        /// <summary>
        /// 事务级同步工程归属、workspace_uris JSON、raw_summary (Field 9 & Field 17) 与 FTS 工程/工作区字段
        /// </summary>
        public static int SyncProjectBindingInSummaryDb(
            string dbPath,
            IEnumerable<string> cids,
            string targetProjName,
            string targetProjId,
            string targetFolderUri,
            out List<string> warnings)
        {
            List<string> warnList = new List<string>();
            warnings = warnList;
            LastError = null;

            List<string> targetCids = CollectDistinctCids(cids);
            if (targetCids.Count == 0) return 0;
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath)) return 0;

            string trimmedProjId = targetProjId != null ? targetProjId.Trim() : string.Empty;
            string trimmedProjName = targetProjName != null ? targetProjName.Trim() : string.Empty;
            string trimmedFolderUri = targetFolderUri != null ? targetFolderUri.Trim() : string.Empty;

            bool isOutside = string.Equals(trimmedProjId, "outside-of-project", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(trimmedProjName, "Outside of Project", StringComparison.OrdinalIgnoreCase);

            string effectiveProjId = isOutside ? "outside-of-project" : trimmedProjId;
            string effectiveProjName = isOutside
                ? (string.IsNullOrEmpty(trimmedProjName) ? "Outside of Project" : trimmedProjName)
                : trimmedProjName;
            string effectiveFolderUri = isOutside ? string.Empty : trimmedFolderUri;
            string workspaceUrisVal = (isOutside || string.IsNullOrEmpty(effectiveFolderUri))
                ? string.Empty
                : "[\"" + JsonKeyManipulator.EscapeJsonString(effectiveFolderUri) + "\"]";

            string ftsWorkspace = isOutside ? string.Empty : effectiveFolderUri;
            string ftsProject = isOutside ? string.Empty : effectiveProjName;

            int updatedCount = 0;
            bool ok = ExecuteWriteTransaction(dbPath, delegate(IntPtr db)
            {
                bool hasFts = TableExists(db, "conversations_fts");
                HashSet<string> ftsCols = hasFts ? GetTableColumns(db, "conversations_fts") : null;

                for (int i = 0; i < targetCids.Count; i++)
                {
                    string cid = targetCids[i];
                    byte[] oldRaw = null;
                    bool foundRow = false;

                    IntPtr selStmt = IntPtr.Zero;
                    string err;
                    if (!PrepareStmt(db, "SELECT raw_summary FROM conversation_summaries WHERE conversation_id = ? LIMIT 1;", out selStmt, out err))
                    {
                        LastError = err;
                        return false;
                    }
                    try
                    {
                        if (BindText(selStmt, 1, cid) != SQLITE_OK)
                        {
                            LastError = "SELECT raw_summary 绑定失败: " + GetSqliteErrorMessage(db);
                            return false;
                        }
                        int stepRc = sqlite3_step(selStmt);
                        if (stepRc == SQLITE_ROW)
                        {
                            foundRow = true;
                            oldRaw = ReadColumnBlob(selStmt, 0);
                        }
                        else if (stepRc != SQLITE_DONE)
                        {
                            LastError = "SELECT raw_summary 失败 (" + stepRc + "): " + GetSqliteErrorMessage(db);
                            return false;
                        }
                    }
                    finally
                    {
                        if (selStmt != IntPtr.Zero) sqlite3_finalize(selStmt);
                    }

                    if (!foundRow)
                    {
                        warnList.Add("conversation_summaries 未找到会话: " + cid);
                        continue;
                    }

                    byte[] newRaw = ApplyProjectBindingToRawSummary(
                        oldRaw, effectiveProjName, effectiveProjId, effectiveFolderUri, isOutside, warnList, cid);

                    int changes;
                    if (!ExecuteNonQuery(
                        db,
                        "UPDATE conversation_summaries SET project_id = ?, workspace_uris = ?, raw_summary = ? WHERE conversation_id = ?;",
                        delegate(IntPtr uStmt)
                        {
                            int rc = BindText(uStmt, 1, effectiveProjId);
                            if (rc != SQLITE_OK) return rc;
                            rc = BindText(uStmt, 2, workspaceUrisVal);
                            if (rc != SQLITE_OK) return rc;
                            rc = BindBlob(uStmt, 3, newRaw);
                            if (rc != SQLITE_OK) return rc;
                            return BindText(uStmt, 4, cid);
                        },
                        out changes,
                        out err))
                    {
                        LastError = err;
                        return false;
                    }
                    if (changes > 0) updatedCount++;

                    if (hasFts && ftsCols != null)
                    {
                        string ftsErr;
                        if (!UpdateFtsBindingMetadata(db, ftsCols, cid, null, ftsWorkspace, ftsProject, false, true, out ftsErr))
                        {
                            LastError = ftsErr;
                            return false;
                        }
                    }
                }
                return true;
            });

            if (!ok)
            {
                if (!string.IsNullOrEmpty(LastError)) warnList.Add(LastError);
                return 0;
            }
            return updatedCount;
        }

        private static bool RowExistsByCid(IntPtr db, string tableName, string cid, bool requireNegOneChunk, out bool exists, out string err)
        {
            exists = false;
            err = null;
            string sql = requireNegOneChunk
                ? "SELECT 1 FROM " + tableName + " WHERE conversation_id = ? AND chunk_index = -1 LIMIT 1;"
                : "SELECT 1 FROM " + tableName + " WHERE conversation_id = ? LIMIT 1;";
            IntPtr stmt = IntPtr.Zero;
            if (!PrepareStmt(db, sql, out stmt, out err)) return false;
            try
            {
                int bindRc = BindText(stmt, 1, cid);
                if (bindRc != SQLITE_OK)
                {
                    err = "存在性查询绑定失败 (" + bindRc + "): " + GetSqliteErrorMessage(db);
                    return false;
                }
                int stepRc = sqlite3_step(stmt);
                if (stepRc == SQLITE_ROW)
                {
                    exists = true;
                    return true;
                }
                if (stepRc == SQLITE_DONE)
                {
                    exists = false;
                    return true;
                }
                err = "存在性查询失败 (" + stepRc + "): " + GetSqliteErrorMessage(db);
                return false;
            }
            finally
            {
                if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
            }
        }

        /// <summary>
        /// 事务级 Upsert 会话摘要：存在则增量更新标题/预览/工程/工作区/raw_summary，不存在则补全全量 NOT NULL 默认列插入
        /// </summary>
        public static int UpsertSessionsInSummaryDb(string dbPath, List<SummaryDbUpsertItem> items, out List<string> warnings)
        {
            List<string> warnList = new List<string>();
            warnings = warnList;
            LastError = null;

            if (items == null || items.Count == 0) return 0;
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                LastError = "数据库文件不存在: " + dbPath;
                return 0;
            }

            int upsertedCount = 0;
            bool ok = ExecuteWriteTransaction(dbPath, delegate(IntPtr db)
            {
                HashSet<string> sumCols = GetTableColumns(db, "conversation_summaries");
                if (sumCols.Count == 0 || !sumCols.Contains("conversation_id"))
                {
                    LastError = "conversation_summaries 表不存在或缺少 conversation_id 列。";
                    return false;
                }

                bool hasFts = TableExists(db, "conversations_fts");
                HashSet<string> ftsCols = hasFts ? GetTableColumns(db, "conversations_fts") : null;

                for (int i = 0; i < items.Count; i++)
                {
                    SummaryDbUpsertItem item = items[i];
                    if (item == null || string.IsNullOrWhiteSpace(item.ConversationId))
                    {
                        warnList.Add("跳过无效的 UpsertItem (空 CID)。");
                        continue;
                    }

                    string cid = item.ConversationId.Trim();
                    string titleVal = item.Title ?? string.Empty;
                    string previewVal = item.Preview ?? string.Empty;
                    string projectIdVal = item.ProjectId ?? string.Empty;
                    string workspaceUrisVal = item.WorkspaceUrisJson ?? string.Empty;
                    string parentCidVal = item.ParentConversationId ?? string.Empty;
                    string nowIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                    string modIso = string.IsNullOrEmpty(item.LastModifiedTimeIso) ? nowIso : item.LastModifiedTimeIso;
                    string inputIso = string.IsNullOrEmpty(item.LastUserInputTimeIso) ? modIso : item.LastUserInputTimeIso;

                    bool exists;
                    string err;
                    if (!RowExistsByCid(db, "conversation_summaries", cid, false, out exists, out err))
                    {
                        LastError = err;
                        return false;
                    }

                    int changes;
                    if (exists)
                    {
                        if (!ExecuteNonQuery(
                            db,
                            "UPDATE conversation_summaries SET title = ?, preview = ?, project_id = ?, workspace_uris = ?, raw_summary = COALESCE(?, raw_summary) WHERE conversation_id = ?;",
                            delegate(IntPtr uStmt)
                            {
                                int rc = BindText(uStmt, 1, titleVal);
                                if (rc != SQLITE_OK) return rc;
                                rc = BindText(uStmt, 2, previewVal);
                                if (rc != SQLITE_OK) return rc;
                                rc = BindText(uStmt, 3, projectIdVal);
                                if (rc != SQLITE_OK) return rc;
                                rc = BindText(uStmt, 4, workspaceUrisVal);
                                if (rc != SQLITE_OK) return rc;
                                rc = BindBlob(uStmt, 5, item.RawSummaryBytes);
                                if (rc != SQLITE_OK) return rc;
                                return BindText(uStmt, 6, cid);
                            },
                            out changes,
                            out err))
                        {
                            LastError = err;
                            return false;
                        }
                        upsertedCount++;
                    }
                    else
                    {
                        byte[] insertRaw = item.RawSummaryBytes ?? new byte[0];
                        string[] allCols = new string[]
                        {
                            "conversation_id", "title", "preview", "step_count", "last_modified_time",
                            "workspace_uris", "status", "source", "project_id", "agent_name",
                            "parent_conversation_id", "nesting_depth", "battle_id", "winning_conversation_id",
                            "not_fully_idle", "killed", "last_user_input_time", "last_user_input_step_index",
                            "app_data_dir", "raw_summary", "group_id"
                        };
                        string[] allExprs = new string[]
                        {
                            "?", "?", "?", "?", "?",
                            "?", "'CASCADE_RUN_STATUS_IDLE'", "''", "?", "''",
                            "?", "?", "''", "''",
                            "0", "0", "?", "-1",
                            "'antigravity'", "?", "''"
                        };

                        List<string> insCols = new List<string>(allCols.Length);
                        List<string> insExprs = new List<string>(allCols.Length);
                        List<string> activeDynamicCols = new List<string>(11);

                        for (int c = 0; c < allCols.Length; c++)
                        {
                            if (!sumCols.Contains(allCols[c])) continue;
                            insCols.Add(allCols[c]);
                            insExprs.Add(allExprs[c]);
                            if (allExprs[c] == "?") activeDynamicCols.Add(allCols[c]);
                        }

                        string insSql = "INSERT INTO conversation_summaries (" + string.Join(", ", insCols.ToArray()) +
                                        ") VALUES (" + string.Join(", ", insExprs.ToArray()) + ");";

                        if (!ExecuteNonQuery(
                            db,
                            insSql,
                            delegate(IntPtr iStmt)
                            {
                                for (int k = 0; k < activeDynamicCols.Count; k++)
                                {
                                    int pIdx = k + 1;
                                    string colName = activeDynamicCols[k];
                                    int rc = SQLITE_OK;
                                    if (colName == "conversation_id") rc = BindText(iStmt, pIdx, cid);
                                    else if (colName == "title") rc = BindText(iStmt, pIdx, titleVal);
                                    else if (colName == "preview") rc = BindText(iStmt, pIdx, previewVal);
                                    else if (colName == "step_count") rc = sqlite3_bind_int(iStmt, pIdx, item.StepCount);
                                    else if (colName == "last_modified_time") rc = BindText(iStmt, pIdx, modIso);
                                    else if (colName == "workspace_uris") rc = BindText(iStmt, pIdx, workspaceUrisVal);
                                    else if (colName == "project_id") rc = BindText(iStmt, pIdx, projectIdVal);
                                    else if (colName == "parent_conversation_id") rc = BindText(iStmt, pIdx, parentCidVal);
                                    else if (colName == "nesting_depth") rc = sqlite3_bind_int(iStmt, pIdx, item.NestingDepth);
                                    else if (colName == "last_user_input_time") rc = BindText(iStmt, pIdx, inputIso);
                                    else if (colName == "raw_summary") rc = BindBlob(iStmt, pIdx, insertRaw);
                                    if (rc != SQLITE_OK) return rc;
                                }
                                return SQLITE_OK;
                            },
                            out changes,
                            out err))
                        {
                            LastError = err;
                            return false;
                        }
                        upsertedCount++;
                    }

                    if (hasFts && ftsCols != null && ftsCols.Contains("conversation_id") && ftsCols.Contains("chunk_index"))
                    {
                        bool ftsExists;
                        string ftsErr;
                        if (!RowExistsByCid(db, "conversations_fts", cid, true, out ftsExists, out ftsErr))
                        {
                            LastError = ftsErr;
                            return false;
                        }
                        if (ftsExists)
                        {
                            if (!UpdateFtsBindingMetadata(
                                db,
                                ftsCols,
                                cid,
                                titleVal,
                                item.FtsWorkspaceToken ?? string.Empty,
                                item.ProjectName ?? string.Empty,
                                true,
                                true,
                                out ftsErr))
                            {
                                LastError = ftsErr;
                                return false;
                            }
                        }
                    }
                }
                return true;
            });

            if (!ok)
            {
                if (!string.IsNullOrEmpty(LastError)) warnList.Add(LastError);
                return 0;
            }
            return upsertedCount;
        }

        private static void DeleteTempSqliteFiles(string tmpPath)
        {
            if (string.IsNullOrEmpty(tmpPath)) return;
            string[] candidates = new string[] { tmpPath, tmpPath + "-wal", tmpPath + "-shm", tmpPath + "-journal" };
            for (int i = 0; i < candidates.Length; i++)
            {
                try
                {
                    if (File.Exists(candidates[i])) File.Delete(candidates[i]);
                }
                catch {}
            }
        }

        /// <summary>
        /// 使用 SQLite 在线备份 API (sqlite3_backup_init/step/finish) 安全拷贝数据库到临时文件并原子替换目标文件
        /// </summary>
        public static bool BackupDatabaseToFile(string srcDbPath, string destDbPath)
        {
            LastError = null;
            if (string.IsNullOrEmpty(srcDbPath) || !File.Exists(srcDbPath))
            {
                LastError = "源数据库文件不存在: " + srcDbPath;
                return false;
            }
            if (string.IsNullOrEmpty(destDbPath))
            {
                LastError = "目标备份路径为空。";
                return false;
            }

            string destDir = Path.GetDirectoryName(destDbPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            string tmpPath = destDbPath + ".tmp." + Guid.NewGuid().ToString("N").Substring(0, 8);
            IntPtr srcDb = IntPtr.Zero;
            IntPtr destDb = IntPtr.Zero;
            IntPtr bk = IntPtr.Zero;
            bool moved = false;

            try
            {
                string err;
                if (!OpenSqliteDb(srcDbPath, SQLITE_OPEN_READONLY, out srcDb, out err))
                {
                    LastError = err;
                    return false;
                }
                if (!OpenSqliteDb(tmpPath, SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE, out destDb, out err))
                {
                    LastError = err;
                    return false;
                }

                byte[] mainName = SqliteMetadataUpdater.ToUtf8NullTerminated("main");
                bk = sqlite3_backup_init(destDb, mainName, srcDb, mainName);
                if (bk == IntPtr.Zero)
                {
                    LastError = "sqlite3_backup_init 失败: " + GetSqliteErrorMessage(destDb);
                    return false;
                }

                int stepRc = sqlite3_backup_step(bk, -1);
                int finishRc = sqlite3_backup_finish(bk);
                bk = IntPtr.Zero;

                if (stepRc != SQLITE_DONE || finishRc != SQLITE_OK)
                {
                    LastError = "sqlite3_backup 失败 (step=" + stepRc + ", finish=" + finishRc + "): " + GetSqliteErrorMessage(destDb);
                    return false;
                }

                sqlite3_close(destDb);
                destDb = IntPtr.Zero;
                sqlite3_close(srcDb);
                srcDb = IntPtr.Zero;

                if (File.Exists(destDbPath))
                {
                    try { File.SetAttributes(destDbPath, FileAttributes.Normal); } catch {}
                    File.Replace(tmpPath, destDbPath, null, true);
                }
                else
                {
                    File.Move(tmpPath, destDbPath);
                }
                moved = true;
                return true;
            }
            catch (DllNotFoundException ex)
            {
                LastError = "未找到 winsqlite3.dll: " + ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
            finally
            {
                if (bk != IntPtr.Zero) try { sqlite3_backup_finish(bk); } catch {}
                if (destDb != IntPtr.Zero) try { sqlite3_close(destDb); } catch {}
                if (srcDb != IntPtr.Zero) try { sqlite3_close(srcDb); } catch {}
                if (!moved) DeleteTempSqliteFiles(tmpPath);
            }
        }
    }
}
