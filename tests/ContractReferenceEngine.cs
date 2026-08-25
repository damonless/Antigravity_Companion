using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace AntigravityCompanion.Tests
{
    // =========================================================================
    // 1. PROTOBUF AST & WIRE-FORMAT CODEC
    // =========================================================================
    public class ProtoNode
    {
        public int FieldNumber;
        public int WireType;
        public ulong VarintValue;
        public ulong Fixed64Value;
        public uint Fixed32Value;
        public byte[] LengthDelimitedValue;
        public List<ProtoNode> Children;

        public ProtoNode()
        {
            Children = new List<ProtoNode>();
        }

        public string GetString()
        {
            if (LengthDelimitedValue == null || LengthDelimitedValue.Length == 0)
                return string.Empty;
            try
            {
                return Encoding.UTF8.GetString(LengthDelimitedValue);
            }
            catch
            {
                return string.Empty;
            }
        }

        public void SetString(string text)
        {
            WireType = 2;
            LengthDelimitedValue = text != null ? Encoding.UTF8.GetBytes(text) : new byte[0];
            Children = null;
        }

        public ProtoNode FindChild(int fieldNumber)
        {
            if (Children == null) return null;
            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i].FieldNumber == fieldNumber)
                    return Children[i];
            }
            return null;
        }

        public List<ProtoNode> FindAllChildren(int fieldNumber)
        {
            List<ProtoNode> result = new List<ProtoNode>();
            if (Children != null)
            {
                for (int i = 0; i < Children.Count; i++)
                {
                    if (Children[i].FieldNumber == fieldNumber)
                        result.Add(Children[i]);
                }
            }
            return result;
        }
    }

    public static class ProtoAstCodec
    {
        public static List<ProtoNode> Decode(byte[] buffer)
        {
            List<ProtoNode> nodes = new List<ProtoNode>();
            if (buffer == null || buffer.Length == 0)
                return nodes;

            int offset = 0;
            while (offset < buffer.Length)
            {
                ulong tag;
                int tagBytes = ReadVarint(buffer, offset, out tag);
                if (tagBytes <= 0) break;
                offset += tagBytes;

                int wireType = (int)(tag & 0x07);
                int fieldNumber = (int)(tag >> 3);

                ProtoNode node = new ProtoNode();
                node.FieldNumber = fieldNumber;
                node.WireType = wireType;

                if (wireType == 0) // Varint
                {
                    ulong val;
                    int valBytes = ReadVarint(buffer, offset, out val);
                    if (valBytes <= 0) break;
                    offset += valBytes;
                    node.VarintValue = val;
                }
                else if (wireType == 1) // 64-bit
                {
                    if (offset + 8 > buffer.Length) break;
                    node.Fixed64Value = BitConverter.ToUInt64(buffer, offset);
                    offset += 8;
                }
                else if (wireType == 2) // Length-delimited
                {
                    ulong len;
                    int lenBytes = ReadVarint(buffer, offset, out len);
                    if (lenBytes <= 0) break;
                    offset += lenBytes;

                    int length = (int)len;
                    if (offset + length > buffer.Length)
                    {
                        // Truncated payload
                        int remaining = buffer.Length - offset;
                        node.LengthDelimitedValue = new byte[remaining];
                        Array.Copy(buffer, offset, node.LengthDelimitedValue, 0, remaining);
                        offset = buffer.Length;
                        nodes.Add(node);
                        break;
                    }

                    byte[] data = new byte[length];
                    Array.Copy(buffer, offset, data, 0, length);
                    offset += length;
                    node.LengthDelimitedValue = data;

                    // Attempt recursive message parsing if valid
                    try
                    {
                        if (data.Length > 0 && LooksLikeValidSubMessage(data))
                        {
                            node.Children = Decode(data);
                        }
                    }
                    catch
                    {
                        node.Children = null;
                    }
                }
                else if (wireType == 5) // 32-bit
                {
                    if (offset + 4 > buffer.Length) break;
                    node.Fixed32Value = BitConverter.ToUInt32(buffer, offset);
                    offset += 4;
                }
                else
                {
                    // Unknown or unsupported wire type
                    break;
                }

                nodes.Add(node);
            }

            return nodes;
        }

        public static byte[] Encode(List<ProtoNode> nodes)
        {
            if (nodes == null || nodes.Count == 0)
                return new byte[0];

            using (MemoryStream ms = new MemoryStream())
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    ProtoNode node = nodes[i];
                    ulong tag = ((ulong)node.FieldNumber << 3) | ((ulong)node.WireType & 0x07);
                    WriteVarint(ms, tag);

                    if (node.WireType == 0)
                    {
                        WriteVarint(ms, node.VarintValue);
                    }
                    else if (node.WireType == 1)
                    {
                        byte[] b64 = BitConverter.GetBytes(node.Fixed64Value);
                        ms.Write(b64, 0, 8);
                    }
                    else if (node.WireType == 2)
                    {
                        byte[] payload;
                        if (node.Children != null && node.Children.Count > 0)
                        {
                            payload = Encode(node.Children);
                        }
                        else
                        {
                            payload = node.LengthDelimitedValue ?? new byte[0];
                        }
                        WriteVarint(ms, (ulong)payload.Length);
                        if (payload.Length > 0)
                        {
                            ms.Write(payload, 0, payload.Length);
                        }
                    }
                    else if (node.WireType == 5)
                    {
                        byte[] b32 = BitConverter.GetBytes(node.Fixed32Value);
                        ms.Write(b32, 0, 4);
                    }
                }
                return ms.ToArray();
            }
        }

        public static int ReadVarint(byte[] buffer, int offset, out ulong value)
        {
            value = 0;
            int shift = 0;
            int count = 0;
            while (offset + count < buffer.Length && count < 10)
            {
                byte b = buffer[offset + count];
                value |= ((ulong)(b & 0x7F)) << shift;
                count++;
                if ((b & 0x80) == 0)
                    return count;
                shift += 7;
            }
            return 0; // Invalid / incomplete varint
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

        private static bool LooksLikeValidSubMessage(byte[] data)
        {
            if (data == null || data.Length == 0) return false;
            int offset = 0;
            int validNodes = 0;
            while (offset < data.Length)
            {
                ulong tag;
                int tagBytes = ReadVarint(data, offset, out tag);
                if (tagBytes <= 0) return false;
                offset += tagBytes;

                int wireType = (int)(tag & 0x07);
                int fieldNumber = (int)(tag >> 3);
                if (fieldNumber <= 0 || fieldNumber > 536870911) return false;

                if (wireType == 0)
                {
                    ulong v;
                    int vb = ReadVarint(data, offset, out v);
                    if (vb <= 0) return false;
                    offset += vb;
                }
                else if (wireType == 1)
                {
                    if (offset + 8 > data.Length) return false;
                    offset += 8;
                }
                else if (wireType == 2)
                {
                    ulong len;
                    int lb = ReadVarint(data, offset, out len);
                    if (lb <= 0 || offset + lb + (int)len > data.Length) return false;
                    offset += lb + (int)len;
                }
                else if (wireType == 5)
                {
                    if (offset + 4 > data.Length) return false;
                    offset += 4;
                }
                else
                {
                    return false;
                }
                validNodes++;
            }
            return validNodes > 0 && offset == data.Length;
        }
    }

    // =========================================================================
    // 2. DATA LAYER & SESSION METADATA EXTRACTOR (F1, F2, F3, F4)
    // =========================================================================
    public class ConversationSummaryInfo
    {
        public string ConversationId;      // UUID
        public string DisplayTitle;        // Field 15 Custom Title if present, else Field 1 Auto Summary
        public string AutoSummaryTitle;    // Field 1
        public string CustomTitle;         // Field 15 -> Field 1
        public bool HasCustomTitle;
        public string FolderUri;           // Field 9 -> Field 1 or Field 17
        public string ProjectName;
        public DateTime UpdatedTime;
        public DateTime CreatedTime;
        public int StepCount;
        public int UserInteractions;
        public ProtoNode RawProtoEntry;    // Preserved for lossless merge

        public override string ToString()
        {
            return string.Format("{0} [{1}] ({2})", DisplayTitle, ConversationId, ProjectName);
        }
    }

    public static class SessionMetadataExtractor
    {
        public static List<ConversationSummaryInfo> ExtractSummariesFromProto(byte[] pbBytes)
        {
            List<ConversationSummaryInfo> results = new List<ConversationSummaryInfo>();
            if (pbBytes == null || pbBytes.Length == 0)
                return results;

            List<ProtoNode> rootNodes = ProtoAstCodec.Decode(pbBytes);
            for (int i = 0; i < rootNodes.Count; i++)
            {
                ProtoNode node = rootNodes[i];
                if (node.FieldNumber == 1 && node.WireType == 2)
                {
                    ConversationSummaryInfo info = ParseEntryNode(node);
                    if (info != null)
                    {
                        results.Add(info);
                    }
                }
            }

            return results;
        }

        public static ConversationSummaryInfo ParseEntryNode(ProtoNode entryNode)
        {
            if (entryNode == null) return null;

            ConversationSummaryInfo info = new ConversationSummaryInfo();
            info.RawProtoEntry = entryNode;

            if (entryNode.Children == null || entryNode.Children.Count == 0)
            {
                if (entryNode.LengthDelimitedValue != null && entryNode.LengthDelimitedValue.Length > 0)
                {
                    List<ProtoNode> sub = ProtoAstCodec.Decode(entryNode.LengthDelimitedValue);
                    if (sub != null && sub.Count > 0) entryNode.Children = sub;
                }
            }

            if (entryNode.Children == null)
            {
                entryNode.Children = new List<ProtoNode>();
            }

            ProtoNode metaSubmessage = entryNode.FindChild(2);
            bool isRealProtoLayout = metaSubmessage != null && metaSubmessage.Children != null && metaSubmessage.Children.Count > 0;

            if (isRealProtoLayout)
            {
                ProtoNode f1 = entryNode.FindChild(1);
                if (f1 != null) info.ConversationId = f1.GetString();

                for (int i = 0; i < metaSubmessage.Children.Count; i++)
                {
                    ProtoNode child = metaSubmessage.Children[i];
                    if (child.FieldNumber == 1) // Auto Summary Title
                    {
                        info.AutoSummaryTitle = child.GetString();
                    }
                    else if (child.FieldNumber == 2) // Step count
                    {
                        info.StepCount = (int)child.VarintValue;
                    }
                    else if (child.FieldNumber == 3) // Updated At
                    {
                        DateTime t = ParseTimestampNode(child);
                        if (t > info.UpdatedTime) info.UpdatedTime = t;
                    }
                    else if (child.FieldNumber == 7) // Created At
                    {
                        info.CreatedTime = ParseTimestampNode(child);
                    }
                    else if (child.FieldNumber == 9) // WorkspaceInfo
                    {
                        ParseWorkspaceNode(child, info);
                    }
                    else if (child.FieldNumber == 10) // Last Activity Time
                    {
                        DateTime t = ParseTimestampNode(child);
                        if (t > info.UpdatedTime) info.UpdatedTime = t;
                    }
                    else if (child.FieldNumber == 15) // Custom Title Metadata ★
                    {
                        ParseCustomTitleNode(child, info);
                    }
                    else if (child.FieldNumber == 17) // CascadeInfo
                    {
                        ParseCascadeNode(child, info);
                    }
                }
            }
            else
            {
                for (int i = 0; i < entryNode.Children.Count; i++)
                {
                    ProtoNode child = entryNode.Children[i];
                    if (child.FieldNumber == 1)
                    {
                        string s = child.GetString();
                        if (entryNode.FindChild(2) != null)
                        {
                            info.AutoSummaryTitle = s;
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(info.ConversationId)) info.ConversationId = s;
                            else info.AutoSummaryTitle = s;
                        }
                    }
                    else if (child.FieldNumber == 2)
                    {
                        if (child.Children != null && child.Children.Count > 0)
                        {
                            for (int c = 0; c < child.Children.Count; c++)
                            {
                                ProtoNode sc = child.Children[c];
                                if (sc.FieldNumber == 15) ParseCustomTitleNode(sc, info);
                                else if (sc.FieldNumber == 1) info.AutoSummaryTitle = sc.GetString();
                            }
                        }
                        else
                        {
                            info.ConversationId = child.GetString();
                        }
                    }
                    else if (child.FieldNumber == 3)
                    {
                        if (child.WireType == 0) info.CreatedTime = ConvertEpochToDateTime(child.VarintValue);
                        else info.CreatedTime = ParseTimestampNode(child);
                    }
                    else if (child.FieldNumber == 4)
                    {
                        if (child.WireType == 0) info.UpdatedTime = ConvertEpochToDateTime(child.VarintValue);
                        else info.UpdatedTime = ParseTimestampNode(child);
                    }
                    else if (child.FieldNumber == 5)
                    {
                        if (child.WireType == 0) info.StepCount = (int)child.VarintValue;
                    }
                    else if (child.FieldNumber == 7)
                    {
                        info.CreatedTime = ParseTimestampNode(child);
                    }
                    else if (child.FieldNumber == 9)
                    {
                        ParseWorkspaceNode(child, info);
                    }
                    else if (child.FieldNumber == 10)
                    {
                        DateTime t = ParseTimestampNode(child);
                        if (t > info.UpdatedTime) info.UpdatedTime = t;
                    }
                    else if (child.FieldNumber == 15)
                    {
                        ParseCustomTitleNode(child, info);
                    }
                    else if (child.FieldNumber == 17)
                    {
                        ParseCascadeNode(child, info);
                    }
                }
            }

            // 100% Priority Custom Title Resolution
            if (!string.IsNullOrEmpty(info.CustomTitle) && !string.IsNullOrWhiteSpace(info.CustomTitle))
            {
                info.HasCustomTitle = true;
                info.DisplayTitle = info.CustomTitle.Trim();
            }
            else if (!string.IsNullOrEmpty(info.AutoSummaryTitle) && !string.IsNullOrWhiteSpace(info.AutoSummaryTitle))
            {
                info.HasCustomTitle = false;
                info.DisplayTitle = info.AutoSummaryTitle.Trim();
            }
            else
            {
                info.HasCustomTitle = false;
                string id = info.ConversationId;
                if (string.IsNullOrEmpty(id)) id = "session";
                info.DisplayTitle = "未命名会话 (" + (id.Length >= 8 ? id.Substring(0, 8) : id) + ")";
            }

            if (string.IsNullOrEmpty(info.ConversationId))
            {
                info.ConversationId = Guid.NewGuid().ToString();
            }

            if (string.IsNullOrEmpty(info.ProjectName) && !string.IsNullOrEmpty(info.FolderUri))
            {
                info.ProjectName = ExtractProjectNameFromUri(info.FolderUri);
            }
            if (string.IsNullOrEmpty(info.ProjectName))
            {
                info.ProjectName = "Default Project";
            }

            if (info.CreatedTime == DateTime.MinValue && info.UpdatedTime != DateTime.MinValue)
                info.CreatedTime = info.UpdatedTime;
            if (info.UpdatedTime == DateTime.MinValue && info.CreatedTime != DateTime.MinValue)
                info.UpdatedTime = info.CreatedTime;

            return info;
        }

        private static void ParseCustomTitleNode(ProtoNode node, ConversationSummaryInfo info)
        {
            if (node == null) return;
            if (node.Children != null && node.Children.Count > 0)
            {
                ProtoNode ct = node.FindChild(1);
                if (ct != null)
                {
                    string s = ct.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        info.CustomTitle = s;
                        info.HasCustomTitle = true;
                    }
                }
            }
            else
            {
                string s = node.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    info.CustomTitle = s;
                    info.HasCustomTitle = true;
                }
            }
        }

        private static void ParseWorkspaceNode(ProtoNode node, ConversationSummaryInfo info)
        {
            if (node == null) return;
            if (node.Children != null && node.Children.Count > 0)
            {
                ProtoNode furi = node.FindChild(1);
                if (furi != null && string.IsNullOrEmpty(info.FolderUri))
                {
                    info.FolderUri = furi.GetString();
                }
            }
            else
            {
                if (string.IsNullOrEmpty(info.FolderUri))
                {
                    info.FolderUri = node.GetString();
                }
            }
        }

        private static void ParseCascadeNode(ProtoNode node, ConversationSummaryInfo info)
        {
            if (node == null || node.Children == null) return;
            ProtoNode nameNode = node.FindChild(1);
            if (nameNode != null && string.IsNullOrEmpty(info.ProjectName))
            {
                info.ProjectName = nameNode.GetString();
            }
            ProtoNode uriNode = node.FindChild(7);
            if (uriNode != null && string.IsNullOrEmpty(info.FolderUri))
            {
                info.FolderUri = uriNode.GetString();
            }
        }

        private static DateTime ParseTimestampNode(ProtoNode node)
        {
            if (node == null) return DateTime.MinValue;
            if (node.WireType == 0 && node.VarintValue > 0)
            {
                return ConvertEpochToDateTime(node.VarintValue);
            }
            if (node.Children != null)
            {
                ProtoNode secNode = node.FindChild(1);
                if (secNode != null && secNode.VarintValue > 0)
                {
                    return ConvertEpochToDateTime(secNode.VarintValue);
                }
            }
            return DateTime.MinValue;
        }

        public static string ExtractProjectNameFromUri(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return "Unknown";
            string clean = uri.Replace("file:///", "").Replace("file://", "").Replace('/', '\\').TrimEnd('\\');
            int lastSlash = clean.LastIndexOf('\\');
            if (lastSlash >= 0 && lastSlash < clean.Length - 1)
            {
                return clean.Substring(lastSlash + 1);
            }
            return clean;
        }

        private static bool IsUuid(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 32) return false;
            Guid g;
            return Guid.TryParse(s, out g);
        }

        private static DateTime ConvertEpochToDateTime(ulong epoch)
        {
            try
            {
                DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                if (epoch > 1000000000000UL) // Milliseconds
                {
                    return origin.AddMilliseconds((double)epoch).ToLocalTime();
                }
                else // Seconds
                {
                    return origin.AddSeconds((double)epoch).ToLocalTime();
                }
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }
    }

    // =========================================================================
    // 3. STREAMING ZIP INDEX READER (F3)
    // =========================================================================
    public class ZipMetadataPackage
    {
        public byte[] SummaryProtoBytes;
        public Dictionary<string, string> ProjectConfigs; // relative path -> json string
        public string MetadataJson;
        public List<string> ConversationDbNames;
        public List<string> BrainDirectoryNames;
        public bool HasGlobalRules;
        public bool HasGlobalSkills;
        public bool HasGlobalMcp;

        public ZipMetadataPackage()
        {
            ProjectConfigs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ConversationDbNames = new List<string>();
            BrainDirectoryNames = new List<string>();
        }
    }

    public static class StreamingZipIndexReader
    {
        public static ZipMetadataPackage ReadZipMetadata(string zipFilePath)
        {
            if (!File.Exists(zipFilePath))
                throw new FileNotFoundException("Zip file not found", zipFilePath);

            using (FileStream fs = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                return ReadZipMetadata(archive);
            }
        }

        public static ZipMetadataPackage ReadZipMetadata(ZipArchive archive)
        {
            ZipMetadataPackage package = new ZipMetadataPackage();
            HashSet<string> brainSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < archive.Entries.Count; i++)
            {
                ZipArchiveEntry entry = archive.Entries[i];
                string name = entry.FullName.Replace('\\', '/');

                if (name.EndsWith("agyhub_summaries_proto.pb", StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream es = entry.Open())
                    using (MemoryStream ms = new MemoryStream())
                    {
                        es.CopyTo(ms);
                        package.SummaryProtoBytes = ms.ToArray();
                    }
                }
                else if (name.StartsWith("config/projects/", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream es = entry.Open())
                    using (StreamReader reader = new StreamReader(es, Encoding.UTF8))
                    {
                        package.ProjectConfigs[name] = reader.ReadToEnd();
                    }
                }
                else if (name.Equals("metadata.json", StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream es = entry.Open())
                    using (StreamReader reader = new StreamReader(es, Encoding.UTF8))
                    {
                        package.MetadataJson = reader.ReadToEnd();
                    }
                }
                else if (name.StartsWith("conversations/", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = Path.GetFileName(name);
                    if (!string.IsNullOrEmpty(fileName))
                        package.ConversationDbNames.Add(fileName);
                }
                else if (name.StartsWith("brain/", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = name.Split('/');
                    if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
                    {
                        brainSet.Add(parts[1]);
                    }
                }
                else if (name.StartsWith("rules/", StringComparison.OrdinalIgnoreCase) || name.StartsWith("config/rules/", StringComparison.OrdinalIgnoreCase))
                {
                    package.HasGlobalRules = true;
                }
                else if (name.StartsWith("skills/", StringComparison.OrdinalIgnoreCase) || name.StartsWith("config/skills/", StringComparison.OrdinalIgnoreCase))
                {
                    package.HasGlobalSkills = true;
                }
                else if (name.StartsWith("mcp/", StringComparison.OrdinalIgnoreCase) || name.StartsWith("config/mcp/", StringComparison.OrdinalIgnoreCase))
                {
                    package.HasGlobalMcp = true;
                }
            }

            package.BrainDirectoryNames.AddRange(brainSet);
            return package;
        }
    }

    // =========================================================================
    // 4. SMART PROJECT FILTER & DIFF ENGINE (F5, F6)
    // =========================================================================
    public enum DiffStatus
    {
        NewInBackup,    // [新增工程 / 含新增会话]
        Aligned,        // [已对齐]
        Conflicted,     // [重名/路径差异]
        LocalOnly,      // [本地独有]
        Modified        // [已修改 / 内容有更新 / 已改名]
    }

    public static class SmartProjectFilter
    {
        public static bool IsJunkOrEphemeral(string projectName)
        {
            if (string.IsNullOrEmpty(projectName))
                return true;

            string lower = projectName.Trim().ToLowerInvariant();

            if (lower.StartsWith("subagent-") || lower.StartsWith("subagent_"))
                return true;
            if (lower.StartsWith("temp-") || lower.StartsWith("temp_"))
                return true;
            if (lower.StartsWith("recovered project") || lower.StartsWith("recovered_project"))
                return true;
            if (lower == "outside-of-project" || lower == "outside_of_project")
                return true;
            if (lower == "default-cli-project" || lower == "default_cli_project")
                return true;

            return false;
        }
    }

    public class ProjectDiffNode
    {
        public string ProjectName;
        public string LocalFolderUri;
        public string BackupFolderUri;
        public DiffStatus Status;
        public int NewSessionCount;
        public int ModifiedSessionCount;
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
        }

        public string GetStatusBadge()
        {
            switch (Status)
            {
                case DiffStatus.NewInBackup: return "[★ 新增]";
                case DiffStatus.Modified:
                    if (!string.IsNullOrEmpty(RenamedFromOrTo))
                        return string.Format("[工程已改名: 本地为 '{0}']", RenamedFromOrTo);
                    return string.Format("[含 {0} 条已修改/更新]", ModifiedSessionCount);
                case DiffStatus.Aligned: return "[已对齐]";
                case DiffStatus.Conflicted: return "[重名/路径差异]";
                case DiffStatus.LocalOnly: return "[本地独有]";
                default: return "";
            }
        }
    }

    public static class VisualDiffEngine
    {
        public static List<ProjectDiffNode> ComputeDiff(
            List<ConversationSummaryInfo> localSummaries,
            List<ConversationSummaryInfo> backupSummaries)
        {
            Dictionary<string, ProjectDiffNode> map = new Dictionary<string, ProjectDiffNode>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ConversationSummaryInfo> localConvMap = new Dictionary<string, ConversationSummaryInfo>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> localUriToProjectNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Populate Local
            if (localSummaries != null)
            {
                for (int i = 0; i < localSummaries.Count; i++)
                {
                    ConversationSummaryInfo info = localSummaries[i];
                    string pName = info.ProjectName ?? "Default Project";
                    ProjectDiffNode node;
                    if (!map.TryGetValue(pName, out node))
                    {
                        node = new ProjectDiffNode(pName);
                        map[pName] = node;
                    }
                    node.LocalConversations.Add(info);
                    if (string.IsNullOrEmpty(node.LocalFolderUri) && !string.IsNullOrEmpty(info.FolderUri))
                        node.LocalFolderUri = info.FolderUri;

                    if (!string.IsNullOrEmpty(info.ConversationId))
                        localConvMap[info.ConversationId] = info;

                    if (!string.IsNullOrEmpty(info.FolderUri))
                    {
                        string norm = NormalizeUri(info.FolderUri);
                        if (!string.IsNullOrEmpty(norm) && !localUriToProjectNameMap.ContainsKey(norm) && !string.Equals(pName, "Outside of Project", StringComparison.OrdinalIgnoreCase))
                        {
                            localUriToProjectNameMap[norm] = pName;
                        }
                    }
                }
            }

            // Populate Backup
            if (backupSummaries != null)
            {
                for (int i = 0; i < backupSummaries.Count; i++)
                {
                    ConversationSummaryInfo info = backupSummaries[i];
                    string pName = info.ProjectName ?? "Default Project";
                    ProjectDiffNode node;
                    if (!map.TryGetValue(pName, out node))
                    {
                        node = new ProjectDiffNode(pName);
                        map[pName] = node;
                    }
                    node.BackupConversations.Add(info);
                    if (string.IsNullOrEmpty(node.BackupFolderUri) && !string.IsNullOrEmpty(info.FolderUri))
                        node.BackupFolderUri = info.FolderUri;
                }
            }

            // Compute Status
            List<ProjectDiffNode> result = new List<ProjectDiffNode>();
            foreach (KeyValuePair<string, ProjectDiffNode> kvp in map)
            {
                ProjectDiffNode node = kvp.Value;
                bool hasLocal = node.LocalConversations.Count > 0;
                bool hasBackup = node.BackupConversations.Count > 0;

                int newConvs = 0;
                int modifiedConvs = 0;
                int alignedConvs = 0;

                for (int b = 0; b < node.BackupConversations.Count; b++)
                {
                    ConversationSummaryInfo bConv = node.BackupConversations[b];
                    string bCid = bConv.ConversationId;
                    if (string.IsNullOrEmpty(bCid)) continue;

                    ConversationSummaryInfo lConv;
                    if (!localConvMap.TryGetValue(bCid, out lConv))
                    {
                        newConvs++;
                    }
                    else
                    {
                        bool titleDiff = !string.Equals(bConv.DisplayTitle, lConv.DisplayTitle, StringComparison.Ordinal);
                        bool stepDiff = bConv.StepCount != lConv.StepCount || bConv.UserInteractions != lConv.UserInteractions;
                        bool timeDiff = Math.Abs((bConv.UpdatedTime - lConv.UpdatedTime).TotalSeconds) > 2 && bConv.UpdatedTime > DateTime.MinValue && lConv.UpdatedTime > DateTime.MinValue;
                        bool projDiff = !string.Equals(bConv.ProjectName, lConv.ProjectName, StringComparison.OrdinalIgnoreCase);

                        if (titleDiff || stepDiff || timeDiff || projDiff)
                        {
                            modifiedConvs++;
                        }
                        else
                        {
                            alignedConvs++;
                        }
                    }
                }
                node.NewSessionCount = newConvs;
                node.ModifiedSessionCount = modifiedConvs;

                if (!hasLocal && hasBackup)
                {
                    string normBackupUri = NormalizeUri(node.BackupFolderUri);
                    string localProjName;
                    if (!string.IsNullOrEmpty(normBackupUri) && localUriToProjectNameMap.TryGetValue(normBackupUri, out localProjName) && !string.Equals(localProjName, node.ProjectName, StringComparison.OrdinalIgnoreCase))
                    {
                        node.Status = DiffStatus.Modified;
                        node.RenamedFromOrTo = localProjName;
                    }
                    else
                    {
                        node.Status = DiffStatus.NewInBackup;
                    }
                }
                else if (hasLocal && !hasBackup)
                {
                    node.Status = DiffStatus.LocalOnly;
                }
                else
                {
                    // Exists on both sides: Check path/URI match
                    if (NormalizeUri(node.LocalFolderUri) != NormalizeUri(node.BackupFolderUri))
                    {
                        node.Status = DiffStatus.Conflicted;
                    }
                    else if (newConvs > 0)
                    {
                        node.Status = DiffStatus.NewInBackup;
                    }
                    else if (modifiedConvs > 0)
                    {
                        node.Status = DiffStatus.Modified;
                    }
                    else
                    {
                        node.Status = DiffStatus.Aligned;
                    }
                }
                result.Add(node);
            }

            return result;
        }

        private static string NormalizeUri(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return "";
            return uri.Replace("file:///", "").Replace("file://", "").Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();
        }
    }

    // =========================================================================
    // 5. CONFLICT RESOLUTION WIZARD (F7)
    public enum ConflictResolutionStrategy
    {
        Merge,
        Rename,
        Overwrite,
        Skip
    }

    public class ConflictResolutionPlan
    {
        public Dictionary<string, ConflictResolutionStrategy> Strategies;
        public Dictionary<string, string> RenamedProjects;

        public ConflictResolutionPlan()
        {
            Strategies = new Dictionary<string, ConflictResolutionStrategy>(StringComparer.OrdinalIgnoreCase);
            RenamedProjects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    // =========================================================================
    // 6. CASCADING CHECKBOX MODEL (F9)
    // =========================================================================
    public class CascadingSelectionModel
    {
        private bool _isUpdating = false;

        public void ToggleProjectSelection(bool checkState, List<string> childIds, HashSet<string> selectedIds)
        {
            if (_isUpdating) return;
            try
            {
                _isUpdating = true;
                if (childIds == null) return;
                for (int i = 0; i < childIds.Count; i++)
                {
                    if (checkState)
                        selectedIds.Add(childIds[i]);
                    else
                        selectedIds.Remove(childIds[i]);
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        public void SelectAll(List<ConversationSummaryInfo> allConversations, HashSet<string> selectedIds)
        {
            selectedIds.Clear();
            if (allConversations == null) return;
            for (int i = 0; i < allConversations.Count; i++)
            {
                if (!string.IsNullOrEmpty(allConversations[i].ConversationId))
                    selectedIds.Add(allConversations[i].ConversationId);
            }
        }

        public void SelectNone(HashSet<string> selectedIds)
        {
            selectedIds.Clear();
        }

        public void SelectNewOnly(List<ProjectDiffNode> diffNodes, HashSet<string> selectedIds)
        {
            selectedIds.Clear();
            if (diffNodes == null) return;
            Dictionary<string, ConversationSummaryInfo> localMap = new Dictionary<string, ConversationSummaryInfo>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < diffNodes.Count; i++)
            {
                if (diffNodes[i].LocalConversations != null)
                {
                    for (int j = 0; j < diffNodes[i].LocalConversations.Count; j++)
                    {
                        var c = diffNodes[i].LocalConversations[j];
                        if (c != null && !string.IsNullOrEmpty(c.ConversationId)) localMap[c.ConversationId] = c;
                    }
                }
            }

            for (int i = 0; i < diffNodes.Count; i++)
            {
                if (diffNodes[i].Status == DiffStatus.Aligned && diffNodes[i].ModifiedSessionCount == 0 && diffNodes[i].NewSessionCount == 0 && localMap.Count == 0)
                {
                    continue;
                }

                if (diffNodes[i].BackupConversations != null)
                {
                    for (int j = 0; j < diffNodes[i].BackupConversations.Count; j++)
                    {
                        var c = diffNodes[i].BackupConversations[j];
                        if (c != null && !string.IsNullOrEmpty(c.ConversationId))
                        {
                            ConversationSummaryInfo loc;
                            bool exists = localMap.TryGetValue(c.ConversationId, out loc);
                            if (!exists)
                            {
                                if (diffNodes[i].Status != DiffStatus.Aligned)
                                {
                                    selectedIds.Add(c.ConversationId);
                                }
                            }
                            else if (loc != null)
                            {
                                bool titleDiff = !string.Equals(c.DisplayTitle, loc.DisplayTitle, StringComparison.Ordinal);
                                bool stepDiff = c.StepCount != loc.StepCount || c.UserInteractions != loc.UserInteractions;
                                bool timeDiff = Math.Abs((c.UpdatedTime - loc.UpdatedTime).TotalSeconds) > 2 && c.UpdatedTime > DateTime.MinValue && loc.UpdatedTime > DateTime.MinValue;
                                bool projDiff = !string.Equals(c.ProjectName, loc.ProjectName, StringComparison.OrdinalIgnoreCase);

                                if (titleDiff || stepDiff || timeDiff || projDiff)
                                {
                                    selectedIds.Add(c.ConversationId);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // =========================================================================
    // 7. SELECTIVE CASCADING IMPORTER & SHARD MERGER (F4, F10)
    // =========================================================================
    public class SelectiveImportRequest
    {
        public string BackupFilePath;
        public string TargetGeminiPath;
        public List<string> SelectedConversationIds;
        public List<string> SelectedProjectNames;
        public Dictionary<string, ConflictResolutionStrategy> ProjectStrategies;
        public Dictionary<string, string> ProjectRenames;
        public bool ImportGlobalRules;
        public bool ImportGlobalSkills;
        public bool ImportGlobalMcp;

        public SelectiveImportRequest()
        {
            SelectedConversationIds = new List<string>();
            SelectedProjectNames = new List<string>();
            ProjectStrategies = new Dictionary<string, ConflictResolutionStrategy>(StringComparer.OrdinalIgnoreCase);
            ProjectRenames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public class SelectiveImportResult
    {
        public bool Success;
        public int ImportedConversationsCount;
        public int ImportedBrainDirsCount;
        public int MergedProtoSummariesCount;
        public List<string> ImportedConversationIds;
        public List<string> ImportedProjectNames;
        public List<string> Errors;

        public SelectiveImportResult()
        {
            ImportedConversationIds = new List<string>();
            ImportedProjectNames = new List<string>();
            Errors = new List<string>();
        }
    }

    public static class SelectiveCascadingImporter
    {
        public static SelectiveImportResult ExecuteImport(SelectiveImportRequest request)
        {
            SelectiveImportResult result = new SelectiveImportResult();

            if (request == null || string.IsNullOrEmpty(request.BackupFilePath) || string.IsNullOrEmpty(request.TargetGeminiPath))
            {
                result.Success = false;
                result.Errors.Add("Invalid request parameters");
                return result;
            }

            if (!File.Exists(request.BackupFilePath))
            {
                result.Success = false;
                result.Errors.Add("Backup file not found: " + request.BackupFilePath);
                return result;
            }

            try
            {
                string antigravityDir = Path.Combine(request.TargetGeminiPath, "antigravity");
                string convDestDir = Path.Combine(antigravityDir, "conversations");
                string brainDestDir = Path.Combine(antigravityDir, "brain");
                string localPbPath = Path.Combine(antigravityDir, "agyhub_summaries_proto.pb");

                Directory.CreateDirectory(convDestDir);
                Directory.CreateDirectory(brainDestDir);

                HashSet<string> targetIdSet = new HashSet<string>(request.SelectedConversationIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

                using (FileStream fs = new FileStream(request.BackupFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    // 1. Read Backup Proto
                    byte[] backupPbBytes = null;
                    for (int i = 0; i < archive.Entries.Count; i++)
                    {
                        if (archive.Entries[i].FullName.Replace('\\', '/').EndsWith("agyhub_summaries_proto.pb", StringComparison.OrdinalIgnoreCase))
                        {
                            using (Stream es = archive.Entries[i].Open())
                            using (MemoryStream ms = new MemoryStream())
                            {
                                es.CopyTo(ms);
                                backupPbBytes = ms.ToArray();
                            }
                            break;
                        }
                    }

                    // 2. Extract selected .db files and brain folders
                    for (int i = 0; i < archive.Entries.Count; i++)
                    {
                        ZipArchiveEntry entry = archive.Entries[i];
                        string normName = entry.FullName.Replace('\\', '/');

                        // Conversation db
                        if (normName.StartsWith("conversations/", StringComparison.OrdinalIgnoreCase))
                        {
                            string fileName = Path.GetFileName(normName);
                            string uuid = ExtractUuidFromFileName(fileName);
                            if (!string.IsNullOrEmpty(uuid) && targetIdSet.Contains(uuid))
                            {
                                string destFile = Path.Combine(convDestDir, fileName);
                                entry.ExtractToFile(destFile, true);
                                if (fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!result.ImportedConversationIds.Contains(uuid))
                                        result.ImportedConversationIds.Add(uuid);
                                    result.ImportedConversationsCount++;
                                }
                            }
                        }
                        // Brain memory folder
                        else if (normName.StartsWith("brain/", StringComparison.OrdinalIgnoreCase))
                        {
                            string[] parts = normName.Split('/');
                            if (parts.Length > 1)
                            {
                                string brainId = parts[1];
                                if (targetIdSet.Contains(brainId))
                                {
                                    string relative = normName.Substring("brain/".Length);
                                    string destPath = Path.Combine(brainDestDir, relative.Replace('/', '\\'));
                                    if (string.IsNullOrEmpty(Path.GetFileName(destPath)))
                                    {
                                        Directory.CreateDirectory(destPath);
                                    }
                                    else
                                    {
                                        Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                                        entry.ExtractToFile(destPath, true);
                                    }
                                }
                            }
                        }
                    }

                    // Count imported brain dirs
                    HashSet<string> importedBrainSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < result.ImportedConversationIds.Count; i++)
                    {
                        string bDir = Path.Combine(brainDestDir, result.ImportedConversationIds[i]);
                        if (Directory.Exists(bDir))
                        {
                            importedBrainSet.Add(result.ImportedConversationIds[i]);
                        }
                    }
                    result.ImportedBrainDirsCount = importedBrainSet.Count;

                    // 3. Shard Merge into local agyhub_summaries_proto.pb
                    if (backupPbBytes != null && backupPbBytes.Length > 0 && targetIdSet.Count > 0)
                    {
                        byte[] localPbBytes = File.Exists(localPbPath) ? File.ReadAllBytes(localPbPath) : new byte[0];
                        byte[] mergedPbBytes = MergeProtoShards(localPbBytes, backupPbBytes, targetIdSet, request.ProjectRenames, request.ProjectStrategies);
                        File.WriteAllBytes(localPbPath, mergedPbBytes);
                        result.MergedProtoSummariesCount = targetIdSet.Count;
                    }
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add(ex.ToString());
            }

            return result;
        }

        public static byte[] MergeProtoShards(
            byte[] localPbBytes,
            byte[] backupPbBytes,
            HashSet<string> targetConversationIds,
            Dictionary<string, string> projectRenames,
            Dictionary<string, ConflictResolutionStrategy> projectStrategies)
        {
            List<ProtoNode> localRoot = ProtoAstCodec.Decode(localPbBytes);
            List<ProtoNode> backupRoot = ProtoAstCodec.Decode(backupPbBytes);

            // Find existing local conversation UUIDs
            HashSet<string> existingLocalUuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < localRoot.Count; i++)
            {
                ConversationSummaryInfo info = SessionMetadataExtractor.ParseEntryNode(localRoot[i]);
                if (info != null && !string.IsNullOrEmpty(info.ConversationId))
                {
                    existingLocalUuids.Add(info.ConversationId);
                }
            }

            // Extract target backup entry nodes
            for (int i = 0; i < backupRoot.Count; i++)
            {
                ProtoNode backupEntry = backupRoot[i];
                ConversationSummaryInfo info = SessionMetadataExtractor.ParseEntryNode(backupEntry);
                if (info != null && !string.IsNullOrEmpty(info.ConversationId) && targetConversationIds.Contains(info.ConversationId))
                {
                    // Check project strategy if conflicted
                    if (projectStrategies != null && projectStrategies.ContainsKey(info.ProjectName))
                    {
                        ConflictResolutionStrategy strat = projectStrategies[info.ProjectName];
                        if (strat == ConflictResolutionStrategy.Skip)
                        {
                            continue;
                        }
                    }

                    // Check if rename is needed
                    if (projectRenames != null && projectRenames.ContainsKey(info.ProjectName))
                    {
                        string newProjName = projectRenames[info.ProjectName];
                        RemapEntryProject(backupEntry, newProjName);
                    }

                    // If existing in local, remove old node if overwrite
                    if (existingLocalUuids.Contains(info.ConversationId))
                    {
                        for (int k = localRoot.Count - 1; k >= 0; k--)
                        {
                            ConversationSummaryInfo localInfo = SessionMetadataExtractor.ParseEntryNode(localRoot[k]);
                            if (localInfo != null && string.Equals(localInfo.ConversationId, info.ConversationId, StringComparison.OrdinalIgnoreCase))
                            {
                                localRoot.RemoveAt(k);
                                break;
                            }
                        }
                    }

                    localRoot.Add(backupEntry);
                }
            }

            return ProtoAstCodec.Encode(localRoot);
        }

        private static void RemapEntryProject(ProtoNode entryNode, string newProjectName)
        {
            if (entryNode == null || entryNode.Children == null) return;
            for (int i = 0; i < entryNode.Children.Count; i++)
            {
                ProtoNode child = entryNode.Children[i];
                if (child.FieldNumber == 17 && child.Children != null) // Workspace Detail
                {
                    ProtoNode nameNode = child.FindChild(1);
                    if (nameNode != null)
                    {
                        nameNode.SetString(newProjectName);
                    }
                }
                else if (child.FieldNumber == 9) // Folder URI
                {
                    string oldUri = child.GetString();
                    if (!string.IsNullOrEmpty(oldUri))
                    {
                        string clean = oldUri.Replace('\\', '/').TrimEnd('/');
                        int lastSlash = clean.LastIndexOf('/');
                        if (lastSlash >= 0)
                        {
                            string newUri = clean.Substring(0, lastSlash + 1) + newProjectName;
                            child.SetString(newUri);
                        }
                    }
                }
            }
        }

        private static string ExtractUuidFromFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            string raw = fileName;
            if (raw.EndsWith(".db-wal", StringComparison.OrdinalIgnoreCase))
                raw = raw.Substring(0, raw.Length - 7);
            else if (raw.EndsWith(".db-shm", StringComparison.OrdinalIgnoreCase))
                raw = raw.Substring(0, raw.Length - 7);
            else if (raw.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                raw = raw.Substring(0, raw.Length - 3);

            return !string.IsNullOrEmpty(raw) ? raw : null;
        }
    }

    // =========================================================================
    // 8. POST-IMPORT HIGHLIGHTER & UI STATE CONTRACT (F8, F11)
    // =========================================================================
    public class HighlightTracker
    {
        public HashSet<string> HighlightedConversationIds;
        public HashSet<string> HighlightedProjectNames;

        public HighlightTracker()
        {
            HighlightedConversationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HighlightedProjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public void MarkImported(List<string> convIds, List<string> projNames)
        {
            if (convIds != null)
            {
                for (int i = 0; i < convIds.Count; i++)
                    HighlightedConversationIds.Add(convIds[i]);
            }
            if (projNames != null)
            {
                for (int i = 0; i < projNames.Count; i++)
                    HighlightedProjectNames.Add(projNames[i]);
            }
        }

        public bool IsConversationHighlighted(string convId)
        {
            return convId != null && HighlightedConversationIds.Contains(convId);
        }

        public bool IsProjectHighlighted(string projName)
        {
            return projName != null && HighlightedProjectNames.Contains(projName);
        }
    }
}
