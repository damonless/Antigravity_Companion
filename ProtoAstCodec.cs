using System;
using System.Collections.Generic;
using System.IO;
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

        public bool HasChildren { get { return Children != null && Children.Count > 0; } }

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

        public string GetString()
        {
            if (RawBytes == null || RawBytes.Length == 0) return string.Empty;
            return Encoding.UTF8.GetString(RawBytes);
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
        public static List<ProtoNode> Decode(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0) return new List<ProtoNode>();
            return Decode(buffer, 0, buffer.Length);
        }

        public static List<ProtoNode> Decode(byte[] buffer, int offset, int length)
        {
            List<ProtoNode> nodes = new List<ProtoNode>();
            if (buffer == null || length <= 0) return nodes;

            int pos = offset;
            int end = offset + length;

            while (pos < end)
            {
                int tagOffset = pos;
                ulong tag;
                int tagBytes = ReadVarint(buffer, pos, out tag);
                if (tagBytes == 0) break;
                pos += tagBytes;

                int fieldNumber = (int)(tag >> 3);
                int wireType = (int)(tag & 0x07);

                if (fieldNumber <= 0) break;

                ProtoNode node = new ProtoNode(fieldNumber, wireType);

                switch (wireType)
                {
                    case 0: // Varint
                        ulong v;
                        int vBytes = ReadVarint(buffer, pos, out v);
                        if (vBytes == 0) return nodes;
                        pos += vBytes;
                        node.VarintValue = v;
                        break;

                    case 1: // Fixed64
                        if (pos + 8 > end) return nodes;
                        node.RawBytes = new byte[8];
                        Buffer.BlockCopy(buffer, pos, node.RawBytes, 0, 8);
                        pos += 8;
                        break;

                    case 2: // Length-delimited
                        ulong len;
                        int lBytes = ReadVarint(buffer, pos, out len);
                        if (lBytes == 0) return nodes;
                        pos += lBytes;

                        int byteCount = (int)len;
                        if (pos + byteCount > end || byteCount < 0) return nodes;

                        node.RawBytes = new byte[byteCount];
                        Buffer.BlockCopy(buffer, pos, node.RawBytes, 0, byteCount);

                        // 试探递归解析 sub-message
                        if (byteCount > 0 && LooksLikeValidSubMessage(node.RawBytes))
                        {
                            try
                            {
                                List<ProtoNode> children = Decode(node.RawBytes, 0, byteCount);
                                if (children != null && children.Count > 0)
                                {
                                    node.Children = children;
                                }
                            }
                            catch
                            {
                                // 保留 RawBytes 作为 fallback
                            }
                        }

                        pos += byteCount;
                        break;

                    case 5: // Fixed32
                        if (pos + 4 > end) return nodes;
                        node.RawBytes = new byte[4];
                        Buffer.BlockCopy(buffer, pos, node.RawBytes, 0, 4);
                        pos += 4;
                        break;

                    default:
                        // 未知 wireType 退出
                        return nodes;
                }

                nodes.Add(node);
            }

            return nodes;
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
                ulong tag = (ulong)((node.FieldNumber << 3) | (node.WireType & 0x07));
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
                        if (node.HasChildren)
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
            value = 0;
            int shift = 0;
            int count = 0;

            for (int i = offset; i < buffer.Length && count < 10; i++)
            {
                byte b = buffer[i];
                count++;
                value |= ((ulong)(b & 0x7F)) << shift;
                if ((b & 0x80) == 0)
                {
                    return count;
                }
                shift += 7;
            }

            value = 0;
            return 0; // 溢出或异常
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

        public static bool LooksLikeValidSubMessage(byte[] data)
        {
            if (data == null || data.Length == 0) return false;
            int pos = 0;
            int validCount = 0;

            while (pos < data.Length)
            {
                ulong tag;
                int tb = ReadVarint(data, pos, out tag);
                if (tb == 0) return false;
                pos += tb;

                int fn = (int)(tag >> 3);
                int wt = (int)(tag & 0x07);

                if (fn <= 0 || fn > 500000000) return false;
                if (wt != 0 && wt != 1 && wt != 2 && wt != 5) return false;

                switch (wt)
                {
                    case 0:
                        ulong v;
                        int vb = ReadVarint(data, pos, out v);
                        if (vb == 0) return false;
                        pos += vb;
                        break;
                    case 1:
                        pos += 8;
                        break;
                    case 2:
                        ulong len;
                        int lb = ReadVarint(data, pos, out len);
                        if (lb == 0) return false;
                        pos += lb + (int)len;
                        break;
                    case 5:
                        pos += 4;
                        break;
                }

                validCount++;
            }

            return pos == data.Length && validCount > 0;
        }
    }

    /// <summary>
    /// 增量 Protobuf 分片合流器 (Milestone 1 — F4)
    /// </summary>
    public static class IncrementalProtoShardMerger
    {
        public static byte[] MergeShards(byte[] localPbBytes, List<ConversationSummaryInfo> selectedBackups, 
                                        string sourceHome, string currentHome, 
                                        Dictionary<string, string> projectUriOverrides = null)
        {
            List<ProtoNode> localRootNodes = (localPbBytes != null && localPbBytes.Length > 0) 
                ? ProtoCodec.Decode(localPbBytes) 
                : new List<ProtoNode>();

            Dictionary<string, int> existingIndexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < localRootNodes.Count; i++)
            {
                ProtoNode n = localRootNodes[i];
                if (n.FieldNumber == 1 && n.WireType == 2)
                {
                    string cid = ExtractConversationId(n);
                    if (!string.IsNullOrEmpty(cid))
                    {
                        existingIndexMap[cid] = i;
                    }
                }
            }

            if (selectedBackups != null)
            {
                for (int i = 0; i < selectedBackups.Count; i++)
                {
                    ConversationSummaryInfo s = selectedBackups[i];
                    if (s.RawProtoEntry == null) continue;

                    ProtoNode clonedNode = CloneAndRemapNode(s.RawProtoEntry, sourceHome, currentHome, projectUriOverrides, s.ProjectName);
                    string targetCid = s.ConversationId;

                    if (existingIndexMap.ContainsKey(targetCid))
                    {
                        localRootNodes[existingIndexMap[targetCid]] = clonedNode;
                    }
                    else
                    {
                        localRootNodes.Add(clonedNode);
                        existingIndexMap[targetCid] = localRootNodes.Count - 1;
                    }
                }
            }

            return ProtoCodec.Encode(localRootNodes);
        }

        private static string ExtractConversationId(ProtoNode entryNode)
        {
            if (entryNode == null) return null;
            ProtoNode f1 = entryNode.GetChild(1);
            if (f1 != null && !string.IsNullOrEmpty(f1.GetString())) return f1.GetString();
            return null;
        }

        private static ProtoNode CloneAndRemapNode(ProtoNode sourceNode, string srcHome, string curHome, 
                                                   Dictionary<string, string> uriOverrides, string projName)
        {
            byte[] encoded = ProtoCodec.Encode(new List<ProtoNode> { sourceNode });
            List<ProtoNode> decodedList = ProtoCodec.Decode(encoded);
            if (decodedList == null || decodedList.Count == 0) return sourceNode;

            ProtoNode node = decodedList[0];
            RemapPathsInAst(node, srcHome, curHome, uriOverrides, projName);
            return node;
        }

        private static void RemapPathsInAst(ProtoNode node, string srcHome, string curHome, 
                                           Dictionary<string, string> uriOverrides, string projName)
        {
            if (node == null) return;

            if (node.WireType == 2 && (node.Children == null || node.Children.Count == 0))
            {
                if (node.RawBytes != null && node.RawBytes.Length > 0)
                {
                    string str = node.GetString();
                    if (!string.IsNullOrEmpty(str) && (str.Contains("file:///") || str.Contains(":\\") || str.Contains(":/")))
                    {
                        if (uriOverrides != null && !string.IsNullOrEmpty(projName) && uriOverrides.ContainsKey(projName))
                        {
                            string newUri = uriOverrides[projName];
                            int fileIdx = str.IndexOf("file:///");
                            if (fileIdx >= 0)
                            {
                                int end = str.IndexOfAny(new char[] { ' ', '\t', '\r', '\n', '\0', '\"', '\'' }, fileIdx);
                                string oldUriPart = end > fileIdx ? str.Substring(fileIdx, end - fileIdx) : str.Substring(fileIdx);
                                str = str.Replace(oldUriPart, newUri);
                            }
                        }

                        if (!string.IsNullOrEmpty(srcHome) && !string.IsNullOrEmpty(curHome))
                        {
                            str = DirectStreamArchiver.RemapAllPathFormats(str, srcHome, curHome);
                        }

                        node.RawBytes = Encoding.UTF8.GetBytes(str);
                    }
                }
            }

            if (node.Children != null)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    RemapPathsInAst(node.Children[i], srcHome, curHome, uriOverrides, projName);
                }
            }
        }
    }
}
