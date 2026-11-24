using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // PROTOBUF WIRE TYPES, AST CODEC & SUMMARY MODELS (C# 5.0)
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

    /// <summary>
    /// 会话摘要与元数据模型
    /// </summary>
    public class ConversationSummaryInfo
    {
        public string ConversationId;      // UUID (Field 1)
        public string DisplayTitle;        // 最终呈现标题 (Field 15 用户自定义标题 优先于 Field 1 自动摘要)
        public string CustomTitle;         // Field 15 -> Field 1 (用户重命名真实标题)
        public bool HasCustomTitle;        // 是否包含有效的用户自定义重命名标题
        public string AutoSummaryTitle;    // Field 1 (系统自动生成摘要)
        public string FolderUri;           // Field 9 -> Field 1 或 Field 17 -> Field 7
        public string ProjectName;         // 所属工程名称
        public string ProjectId;           // Field 17 -> Field 18 (工程 UUID 或 "outside-of-project")
        public DateTime UpdatedTime;       // 最近更新时间
        public DateTime CreatedTime;       // 创建时间
        public int StepCount;              // 对话步数/消息数
        public int UserInteractions;       // 用户交互步数 (Field 16)
        public int? Field23;               // Field 23 标志位 (0 为 Subagent / Ephemeral)
        public bool IsSubagent;            // 是否为临时子代理会话
        public string SubagentRole;        // 子代理角色
        public string ParentConversationId;// 父会话 UUID
        public string AgentName;           // 子代理名称 (来自 summary.sqlite agent_name)
        public string FirstPromptSnippet;  // 首条用户提示词摘要 (来自 conversations_fts user_body)
        public string GitBranchHint;       // Git 分支提示 (来自 raw_summary Field 9->4 / 兼容 9->3->4)
        public List<KeyValuePair<string, string>> RepoBranchHints; // 多仓 (repoPath, branch) 列表
        public bool IsCliSession;          // 是否为 antigravity-cli 会话 (default-cli-project)
        public string RunStatus;           // 运行状态 (来自 summary.sqlite status)
        public bool NotFullyIdle;          // 未完全空闲标志 (来自 summary.sqlite not_fully_idle)
        public ProtoNode RawProtoEntry;    // AST 原始节点
        public byte[] RawBytes;            // 原始二进制切片

        public override string ToString()
        {
            return string.Format("{0} [{1}] ({2})", DisplayTitle, ConversationId, ProjectName);
        }
    }

    /// <summary>
    /// 工程配置元数据
    /// </summary>
    public class ProjectMetadata
    {
        public string Id;
        public string Name;
        public string FolderUri;
        public List<string> FolderUris = new List<string>();
    }

    public static class ProtoCodec
    {
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
    /// Core summary .pb parsing and AST patching helpers shared across storage modules.
    /// </summary>
    public static class ProtoAstCodec
    {
        public static bool IsSyntheticPlaceholderTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return false;
            return title.StartsWith("未命名会话 (", StringComparison.Ordinal) && title.EndsWith(")", StringComparison.Ordinal);
        }

        public static string ExtractConversationId(ProtoNode entryNode)
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

        public static string ExtractProjectIdFromNodes(List<ProtoNode> entryLevelNodes, List<ProtoNode> metaChildren)
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

        public static string ExtractWorkspaceUriFromMetaChildren(List<ProtoNode> metaChildren)
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

        public static string ExtractWorkspaceUriFromEntry(ProtoNode entryNode)
        {
            if (entryNode == null) return null;
            ProtoNode meta = entryNode.GetChild(2);
            if (meta != null && meta.HasChildren)
            {
                return ExtractWorkspaceUriFromMetaChildren(meta.Children);
            }
            return null;
        }

        public static void SetOrAddUtf8Leaf(List<ProtoNode> children, int fieldNumber, string value)
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

        public static bool SyncTitlesInMetaChildren(List<ProtoNode> metaChildren, string displayTitle, bool hasCustomTitle, bool createPrimaryIfMissing)
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

        public static bool SyncProjectIdInMetaChildren(List<ProtoNode> metaChildren, string targetProjId, bool createCascadeIfMissing)
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

        public static void ClearWorkspaceFieldsInMetaChildren(List<ProtoNode> metaChildren)
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
    }

    /// <summary>
    /// Internal .pbtxt top-level title extractor (supports C/Protobuf octal UTF-8 sequences,
    /// hex escapes, \uXXXX surrogate pairs, and # comments) without depending on Host/Services.
    /// </summary>
    internal static class PbtxtTitleExtractor
    {
        public static string ExtractTitle(string content)
        {
            if (string.IsNullOrEmpty(content)) return string.Empty;
            int len = content.Length;
            int pos = 0;
            if (len > 0 && content[0] == '\uFEFF') pos = 1;

            int braceDepth = 0;
            while (pos < len)
            {
                SkipWsAndComments(content, ref pos, len);
                if (pos >= len) break;

                char c = content[pos];
                if (c == '{')
                {
                    braceDepth++;
                    pos++;
                    continue;
                }
                if (c == '}')
                {
                    if (braceDepth > 0) braceDepth--;
                    pos++;
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    SkipOrDecodeQuotedLiteral(content, ref pos, len, null, null);
                    continue;
                }

                if (braceDepth == 0 && IsIdentStart(c))
                {
                    int identStart = pos;
                    while (pos < len && IsIdentPart(content[pos])) pos++;
                    string ident = content.Substring(identStart, pos - identStart);

                    if (string.Equals(ident, "title", StringComparison.Ordinal))
                    {
                        int probe = pos;
                        SkipWsAndComments(content, ref probe, len);
                        if (probe < len && content[probe] == ':')
                        {
                            probe++;
                            SkipWsAndComments(content, ref probe, len);
                            if (probe < len && (content[probe] == '"' || content[probe] == '\''))
                            {
                                StringBuilder decodedSb = new StringBuilder();
                                List<byte> pendingBytes = new List<byte>();

                                while (probe < len && (content[probe] == '"' || content[probe] == '\''))
                                {
                                    SkipOrDecodeQuotedLiteral(content, ref probe, len, decodedSb, pendingBytes);
                                    int nextQuoteProbe = probe;
                                    SkipWsAndComments(content, ref nextQuoteProbe, len);
                                    if (nextQuoteProbe < len && (content[nextQuoteProbe] == '"' || content[nextQuoteProbe] == '\''))
                                    {
                                        probe = nextQuoteProbe;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }

                                FlushPendingUtf8Bytes(pendingBytes, decodedSb);
                                return decodedSb.ToString();
                            }
                        }
                    }
                    continue;
                }

                pos++;
            }

            return string.Empty;
        }

        private static void SkipWsAndComments(string s, ref int pos, int len)
        {
            while (pos < len)
            {
                char c = s[pos];
                if (char.IsWhiteSpace(c))
                {
                    pos++;
                    continue;
                }
                if (c == '#')
                {
                    pos++;
                    while (pos < len && s[pos] != '\n' && s[pos] != '\r') pos++;
                    continue;
                }
                break;
            }
        }

        private static bool IsIdentStart(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_';
        }

        private static bool IsIdentPart(char c)
        {
            return IsIdentStart(c) || (c >= '0' && c <= '9');
        }

        private static int HexDigitValue(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return 10 + (c - 'a');
            if (c >= 'A' && c <= 'F') return 10 + (c - 'A');
            return -1;
        }

        private static bool TryParseHex4(string s, int offset, out int code)
        {
            code = 0;
            if (s == null || offset < 0 || offset + 4 > s.Length) return false;
            for (int i = 0; i < 4; i++)
            {
                int d = HexDigitValue(s[offset + i]);
                if (d < 0) return false;
                code = (code << 4) | d;
            }
            return true;
        }

        private static void FlushPendingUtf8Bytes(List<byte> pendingBytes, StringBuilder sb)
        {
            if (pendingBytes == null || pendingBytes.Count == 0) return;
            sb.Append(Encoding.UTF8.GetString(pendingBytes.ToArray()));
            pendingBytes.Clear();
        }

        private static void SkipOrDecodeQuotedLiteral(
            string s,
            ref int pos,
            int len,
            StringBuilder sb,
            List<byte> pendingBytes)
        {
            if (pos >= len) return;
            char quote = s[pos];
            if (quote != '"' && quote != '\'') return;
            pos++;

            bool decode = sb != null;
            while (pos < len)
            {
                char c = s[pos++];
                if (c == quote)
                {
                    if (decode) FlushPendingUtf8Bytes(pendingBytes, sb);
                    return;
                }

                if (c == '\\')
                {
                    if (pos >= len) return;
                    char esc = s[pos++];

                    if (esc >= '0' && esc <= '7')
                    {
                        int octVal = esc - '0';
                        for (int k = 0; k < 2 && pos < len; k++)
                        {
                            char next = s[pos];
                            if (next < '0' || next > '7') break;
                            octVal = (octVal << 3) | (next - '0');
                            pos++;
                        }
                        if (decode) pendingBytes.Add((byte)(octVal & 0xFF));
                        continue;
                    }

                    if (esc == 'x' || esc == 'X')
                    {
                        int hexVal = 0;
                        int hexDigits = 0;
                        while (hexDigits < 2 && pos < len)
                        {
                            int d = HexDigitValue(s[pos]);
                            if (d < 0) break;
                            hexVal = (hexVal << 4) | d;
                            pos++;
                            hexDigits++;
                        }
                        if (decode)
                        {
                            if (hexDigits > 0)
                            {
                                pendingBytes.Add((byte)hexVal);
                            }
                            else
                            {
                                FlushPendingUtf8Bytes(pendingBytes, sb);
                                sb.Append(esc);
                            }
                        }
                        continue;
                    }

                    if (decode) FlushPendingUtf8Bytes(pendingBytes, sb);

                    switch (esc)
                    {
                        case '\\': if (decode) sb.Append('\\'); break;
                        case '"': if (decode) sb.Append('"'); break;
                        case '\'': if (decode) sb.Append('\''); break;
                        case 'n': if (decode) sb.Append('\n'); break;
                        case 'r': if (decode) sb.Append('\r'); break;
                        case 't': if (decode) sb.Append('\t'); break;
                        case 'a': if (decode) sb.Append('\a'); break;
                        case 'b': if (decode) sb.Append('\b'); break;
                        case 'f': if (decode) sb.Append('\f'); break;
                        case 'v': if (decode) sb.Append('\v'); break;
                        case '?': if (decode) sb.Append('?'); break;
                        case 'u':
                            if (pos + 4 <= len)
                            {
                                int code;
                                if (TryParseHex4(s, pos, out code))
                                {
                                    pos += 4;
                                    if (decode)
                                    {
                                        if (code >= 0xD800 && code <= 0xDBFF &&
                                            pos + 6 <= len && s[pos] == '\\' && s[pos + 1] == 'u')
                                        {
                                            int low;
                                            if (TryParseHex4(s, pos + 2, out low) &&
                                                low >= 0xDC00 && low <= 0xDFFF)
                                            {
                                                int cp = 0x10000 + ((code - 0xD800) << 10) + (low - 0xDC00);
                                                sb.Append(char.ConvertFromUtf32(cp));
                                                pos += 6;
                                                break;
                                            }
                                        }
                                        sb.Append((char)code);
                                    }
                                    break;
                                }
                            }
                            if (decode) sb.Append('u');
                            break;
                        default:
                            if (decode) sb.Append(esc);
                            break;
                    }
                }
                else
                {
                    if (decode)
                    {
                        FlushPendingUtf8Bytes(pendingBytes, sb);
                        sb.Append(c);
                    }
                }
            }

            if (decode) FlushPendingUtf8Bytes(pendingBytes, sb);
        }
    }
}
