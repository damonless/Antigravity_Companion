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
                        bool canSub = false;
                        try { canSub = (byteCount > 0 && LooksLikeValidSubMessage(node.RawBytes)); } catch {}
                        if (canSub)
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

            if (count == 10)
            {
                return 10;
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
            try
            {
                int pos = 0;
                int validCount = 0;

                while (pos < data.Length)
                {
                    ulong tag;
                    int tb = ReadVarint(data, pos, out tag);
                    if (tb <= 0) return false;
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
                            if (vb <= 0) return false;
                            pos += vb;
                            break;
                        case 1:
                            if (pos + 8 > data.Length) return false;
                            pos += 8;
                            break;
                        case 2:
                            ulong len;
                            int lb = ReadVarint(data, pos, out len);
                            if (lb <= 0) return false;
                            if (len > (ulong)(data.Length - pos - lb)) return false; // 严防整型溢出与越界
                            pos += lb + (int)len;
                            break;
                        case 5:
                            if (pos + 4 > data.Length) return false;
                            pos += 4;
                            break;
                    }

                    if (pos < 0 || pos > data.Length) return false;
                    validCount++;
                }

                return pos == data.Length && validCount > 0;
            }
            catch
            {
                return false;
            }
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

                    string targetProjId = null;
                    if (projectNewIds != null && !string.IsNullOrEmpty(s.ProjectName) && projectNewIds.ContainsKey(s.ProjectName))
                    {
                        targetProjId = projectNewIds[s.ProjectName];
                    }

                    ProtoNode clonedNode = CloneAndRemapNode(
                        s.RawProtoEntry, sourceHome, currentHome, projectUriOverrides, 
                        s.ProjectName, s.DisplayTitle, s.HasCustomTitle, targetProjId
                    );
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
                                                   Dictionary<string, string> uriOverrides, string projName,
                                                   string displayTitle = null, bool hasCustomTitle = false,
                                                   string targetProjId = null)
        {
            byte[] encoded = ProtoCodec.Encode(new List<ProtoNode> { sourceNode });
            List<ProtoNode> decodedList = ProtoCodec.Decode(encoded);
            if (decodedList == null || decodedList.Count == 0) return sourceNode;

            ProtoNode node = decodedList[0];
            RemapPathsInAst(node, srcHome, curHome, uriOverrides, projName);

            // 同步标题与自定义重命名元数据 (防止 IDE 点击会话后自动重置回自动识别名)
            SyncTitlesInAst(node, displayTitle, hasCustomTitle);

            // 同步 ProjectId (确保关联新机器上的 Project JSON)
            if (!string.IsNullOrEmpty(targetProjId))
            {
                SyncProjectIdInAst(node, targetProjId);
            }

            return node;
        }

        private static void SyncTitlesInAst(ProtoNode entryNode, string displayTitle, bool hasCustomTitle)
        {
            if (entryNode == null || string.IsNullOrEmpty(displayTitle)) return;
            ProtoNode meta = entryNode.GetChild(2);
            if (meta == null || !meta.HasChildren) return;

            // 1. 同步 Primary Title (Field 1)
            ProtoNode f1 = meta.GetChild(1);
            if (f1 != null)
            {
                f1.RawBytes = Encoding.UTF8.GetBytes(displayTitle);
                f1.IsModified = true;
            }
            else
            {
                f1 = new ProtoNode { FieldNumber = 1, WireType = 2, RawBytes = Encoding.UTF8.GetBytes(displayTitle), IsModified = true };
                meta.Children.Insert(0, f1);
            }

            // 2. 如果包含自定义标题，同步 Custom Title Node (Field 15)
            if (hasCustomTitle)
            {
                ProtoNode f15 = meta.GetChild(15);
                if (f15 != null)
                {
                    if (f15.HasChildren)
                    {
                        ProtoNode ctF1 = f15.GetChild(1);
                        if (ctF1 != null)
                        {
                            ctF1.RawBytes = Encoding.UTF8.GetBytes(displayTitle);
                            ctF1.IsModified = true;
                        }
                        else
                        {
                            f15.Children.Insert(0, new ProtoNode { FieldNumber = 1, WireType = 2, RawBytes = Encoding.UTF8.GetBytes(displayTitle), IsModified = true });
                        }
                    }
                    else
                    {
                        f15.RawBytes = Encoding.UTF8.GetBytes(displayTitle);
                    }
                    f15.IsModified = true;
                }
                else
                {
                    // 创建标准 Field 15 自定义标题节点
                    ProtoNode newF15 = new ProtoNode { FieldNumber = 15, WireType = 2, Children = new List<ProtoNode>(), IsModified = true };
                    newF15.Children.Add(new ProtoNode { FieldNumber = 1, WireType = 2, RawBytes = Encoding.UTF8.GetBytes(displayTitle), IsModified = true });
                    
                    // 添加时间戳 (Field 7)
                    long epoch = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                    ProtoNode ts = new ProtoNode { FieldNumber = 7, WireType = 2, Children = new List<ProtoNode>(), IsModified = true };
                    ts.Children.Add(new ProtoNode { FieldNumber = 1, WireType = 0, VarintValue = (ulong)epoch, IsModified = true });
                    newF15.Children.Add(ts);

                    meta.Children.Add(newF15);
                }
            }

            meta.IsModified = true;
            entryNode.IsModified = true;
        }

        private static void SyncProjectIdInAst(ProtoNode entryNode, string targetProjId)
        {
            if (entryNode == null || string.IsNullOrEmpty(targetProjId)) return;
            ProtoNode meta = entryNode.GetChild(2);
            if (meta == null || !meta.HasChildren) return;

            ProtoNode cascade = meta.GetChild(17);
            if (cascade != null && cascade.HasChildren)
            {
                ProtoNode pIdNode = cascade.GetChild(18);
                if (pIdNode != null)
                {
                    pIdNode.RawBytes = Encoding.UTF8.GetBytes(targetProjId);
                    pIdNode.IsModified = true;
                }
                else
                {
                    cascade.Children.Add(new ProtoNode { FieldNumber = 18, WireType = 2, RawBytes = Encoding.UTF8.GetBytes(targetProjId), IsModified = true });
                }
                cascade.IsModified = true;
                meta.IsModified = true;
                entryNode.IsModified = true;
            }
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
                        node.IsModified = true;
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

    /// <summary>
    /// SQLite 数据库 trajectory_metadata_blob 跨机路径重映射与 ProjectId 修正工具
    /// </summary>
    public static class SqliteMetadataUpdater
    {
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_open(string filename, out IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close(IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_prepare_v2(IntPtr db, string zSql, int nByte, out IntPtr ppStmt, IntPtr pzTail);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_step(IntPtr pStmt);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_blob", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_blob(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_bytes", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_bytes(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_blob", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_blob(IntPtr pStmt, int iCol, byte[] zData, int nData, IntPtr xDel);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr pStmt);

        public static bool RemapSqliteDbMetadata(string dbPath, string srcHome, string curHome, 
                                                 Dictionary<string, string> uriOverrides, string projName,
                                                 string targetProjId = null, string displayTitle = null)
        {
            if (!File.Exists(dbPath)) return false;
            try
            {
                IntPtr db;
                if (sqlite3_open(dbPath, out db) != 0) return false;

                try
                {
                    byte[] blob = null;
                    IntPtr stmt;
                    string sql = "SELECT data FROM trajectory_metadata_blob WHERE id='main';";
                    if (sqlite3_prepare_v2(db, sql, -1, out stmt, IntPtr.Zero) == 0)
                    {
                        if (sqlite3_step(stmt) == 100) // SQLITE_ROW
                        {
                            int len = sqlite3_column_bytes(stmt, 0);
                            if (len > 0)
                            {
                                blob = new byte[len];
                                Marshal.Copy(sqlite3_column_blob(stmt, 0), blob, 0, len);
                            }
                        }
                        sqlite3_finalize(stmt);
                    }

                    if (blob == null || blob.Length == 0) return false;

                    List<ProtoNode> nodes = ProtoCodec.Decode(blob);
                    if (nodes == null || nodes.Count == 0) return false;

                    // 重映射 AST 内部所有工作区路径与 ProjectId
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        ProtoNode n = nodes[i];
                        RemapAstNodePaths(n, srcHome, curHome, uriOverrides, projName);
                        if (!string.IsNullOrEmpty(targetProjId) && n.FieldNumber == 18 && n.WireType == 2)
                        {
                            n.RawBytes = Encoding.UTF8.GetBytes(targetProjId);
                            n.IsModified = true;
                        }
                    }

                    byte[] updatedBlob = ProtoCodec.Encode(nodes);

                    // 写回 SQLite
                    string updateSql = "INSERT OR REPLACE INTO trajectory_metadata_blob (id, data) VALUES ('main', ?);";
                    if (sqlite3_prepare_v2(db, updateSql, -1, out stmt, IntPtr.Zero) == 0)
                    {
                        sqlite3_bind_blob(stmt, 1, updatedBlob, updatedBlob.Length, IntPtr.Zero);
                        int res = sqlite3_step(stmt);
                        sqlite3_finalize(stmt);
                        return res == 101; // SQLITE_DONE
                    }
                    return false;
                }
                finally
                {
                    sqlite3_close(db);
                }
            }
            catch
            {
                return false;
            }
        }

        private static void RemapAstNodePaths(ProtoNode node, string srcHome, string curHome, 
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
                        node.IsModified = true;
                    }
                }
            }

            if (node.Children != null)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    RemapAstNodePaths(node.Children[i], srcHome, curHome, uriOverrides, projName);
                }
            }
        }
    }
}
