using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace AntigravityCompanion.Tests
{
    public class MockSessionSpec
    {
        public string ConversationId;
        public string ProjectName;
        public string FolderUri;
        public string AutoSummary;
        public string CustomTitle;
        public DateTime CreatedTime;
        public DateTime UpdatedTime;
        public int StepCount;
        public bool HasBrainData;
        public bool HasWalFiles;
        public int DataSizeBytes;

        public MockSessionSpec()
        {
            ConversationId = Guid.NewGuid().ToString();
            CreatedTime = DateTime.UtcNow.AddHours(-2);
            UpdatedTime = DateTime.UtcNow.AddMinutes(-10);
            StepCount = 12;
            HasBrainData = true;
            HasWalFiles = false;
            DataSizeBytes = 4096;
        }
    }

    public class MockProjectSpec
    {
        public string ProjectName;
        public string FolderUri;
        public List<MockSessionSpec> Sessions;
        public bool IsJunk;

        public MockProjectSpec(string name, string uri)
        {
            ProjectName = name;
            FolderUri = uri;
            Sessions = new List<MockSessionSpec>();
            IsJunk = SmartProjectFilter.IsJunkOrEphemeral(name);
        }
    }

    public class MockSandboxContext : IDisposable
    {
        public string RootPath { get; private set; }
        public string GeminiPath { get; private set; }
        public string AntigravityPath { get; private set; }
        public string ConfigPath { get; private set; }
        public string BackupPath { get; private set; }
        public List<MockProjectSpec> LocalProjects { get; private set; }
        public List<MockProjectSpec> BackupProjects { get; private set; }

        public MockSandboxContext()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "Antigravity_TestSandbox_" + Guid.NewGuid().ToString("N"));
            GeminiPath = Path.Combine(RootPath, ".gemini");
            AntigravityPath = Path.Combine(GeminiPath, "antigravity");
            ConfigPath = Path.Combine(GeminiPath, "config");
            BackupPath = Path.Combine(RootPath, "backups");

            LocalProjects = new List<MockProjectSpec>();
            BackupProjects = new List<MockProjectSpec>();

            Directory.CreateDirectory(RootPath);
            Directory.CreateDirectory(AntigravityPath);
            Directory.CreateDirectory(ConfigPath);
            Directory.CreateDirectory(BackupPath);
            Directory.CreateDirectory(Path.Combine(AntigravityPath, "conversations"));
            Directory.CreateDirectory(Path.Combine(AntigravityPath, "brain"));
            Directory.CreateDirectory(Path.Combine(ConfigPath, "projects"));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(RootPath))
                {
                    Directory.Delete(RootPath, true);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    public static class MockSandboxGenerator
    {
        public static byte[] BuildSyntheticProtobufBytes(List<MockSessionSpec> sessions)
        {
            List<ProtoNode> rootNodes = new List<ProtoNode>();
            if (sessions == null || sessions.Count == 0)
                return new byte[0];

            for (int i = 0; i < sessions.Count; i++)
            {
                MockSessionSpec spec = sessions[i];
                ProtoNode entryNode = new ProtoNode();
                entryNode.FieldNumber = 1; // Repeated session entry
                entryNode.WireType = 2;

                // Field 1: Auto Summary or Title
                if (spec.AutoSummary != null)
                {
                    ProtoNode f1 = new ProtoNode { FieldNumber = 1, WireType = 2 };
                    f1.SetString(spec.AutoSummary);
                    entryNode.Children.Add(f1);
                }

                // Field 2: Conversation UUID
                ProtoNode f2 = new ProtoNode { FieldNumber = 2, WireType = 2 };
                f2.SetString(spec.ConversationId);
                entryNode.Children.Add(f2);

                // Field 3: Created Time (Varint ms)
                ulong createdMs = (ulong)(spec.CreatedTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                ProtoNode f3 = new ProtoNode { FieldNumber = 3, WireType = 0, VarintValue = createdMs };
                entryNode.Children.Add(f3);

                // Field 4: Updated Time (Varint ms)
                ulong updatedMs = (ulong)(spec.UpdatedTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                ProtoNode f4 = new ProtoNode { FieldNumber = 4, WireType = 0, VarintValue = updatedMs };
                entryNode.Children.Add(f4);

                // Field 5: Step count
                ProtoNode f5 = new ProtoNode { FieldNumber = 5, WireType = 0, VarintValue = (ulong)spec.StepCount };
                entryNode.Children.Add(f5);

                // Field 9: Folder URI
                if (!string.IsNullOrEmpty(spec.FolderUri))
                {
                    ProtoNode f9 = new ProtoNode { FieldNumber = 9, WireType = 2 };
                    ProtoNode f9_sub1 = new ProtoNode { FieldNumber = 1, WireType = 2 };
                    f9_sub1.SetString(spec.FolderUri);
                    f9.Children.Add(f9_sub1);
                    entryNode.Children.Add(f9);
                }

                // Field 15: Custom Title Meta
                if (!string.IsNullOrEmpty(spec.CustomTitle))
                {
                    ProtoNode f15 = new ProtoNode { FieldNumber = 15, WireType = 2 };
                    ProtoNode f15_sub1 = new ProtoNode { FieldNumber = 1, WireType = 2 };
                    f15_sub1.SetString(spec.CustomTitle);
                    f15.Children.Add(f15_sub1);
                    entryNode.Children.Add(f15);
                }

                // Field 17: Workspace Detail
                if (!string.IsNullOrEmpty(spec.ProjectName) || !string.IsNullOrEmpty(spec.FolderUri))
                {
                    ProtoNode f17 = new ProtoNode { FieldNumber = 17, WireType = 2 };
                    if (!string.IsNullOrEmpty(spec.ProjectName))
                    {
                        ProtoNode f17_1 = new ProtoNode { FieldNumber = 1, WireType = 2 };
                        f17_1.SetString(spec.ProjectName);
                        f17.Children.Add(f17_1);
                    }
                    if (!string.IsNullOrEmpty(spec.FolderUri))
                    {
                        ProtoNode f17_7 = new ProtoNode { FieldNumber = 7, WireType = 2 };
                        f17_7.SetString(spec.FolderUri);
                        f17.Children.Add(f17_7);
                    }
                    entryNode.Children.Add(f17);
                }

                rootNodes.Add(entryNode);
            }

            return ProtoAstCodec.Encode(rootNodes);
        }

        public static void PopulateLocalSandbox(MockSandboxContext context, List<MockProjectSpec> projects)
        {
            context.LocalProjects.Clear();
            if (projects != null)
                context.LocalProjects.AddRange(projects);

            List<MockSessionSpec> allSessions = new List<MockSessionSpec>();
            for (int i = 0; i < context.LocalProjects.Count; i++)
            {
                MockProjectSpec p = context.LocalProjects[i];
                for (int j = 0; j < p.Sessions.Count; j++)
                {
                    MockSessionSpec s = p.Sessions[j];
                    if (string.IsNullOrEmpty(s.ProjectName)) s.ProjectName = p.ProjectName;
                    if (string.IsNullOrEmpty(s.FolderUri)) s.FolderUri = p.FolderUri;
                    allSessions.Add(s);

                    // Write Mock conversation db
                    string dbFile = Path.Combine(context.AntigravityPath, "conversations", s.ConversationId + ".db");
                    byte[] dbData = GenerateDummyBinary(s.DataSizeBytes > 0 ? s.DataSizeBytes : 1024, "MOCK_SQLITE_DB:" + s.ConversationId);
                    File.WriteAllBytes(dbFile, dbData);

                    if (s.HasWalFiles)
                    {
                        File.WriteAllBytes(dbFile + "-wal", Encoding.UTF8.GetBytes("MOCK_WAL:" + s.ConversationId));
                        File.WriteAllBytes(dbFile + "-shm", Encoding.UTF8.GetBytes("MOCK_SHM:" + s.ConversationId));
                    }

                    // Write Mock brain
                    if (s.HasBrainData)
                    {
                        string brainDir = Path.Combine(context.AntigravityPath, "brain", s.ConversationId);
                        Directory.CreateDirectory(brainDir);
                        File.WriteAllText(Path.Combine(brainDir, "knowledge.json"), "{\"title\":\"" + (s.CustomTitle ?? s.AutoSummary) + "\",\"id\":\"" + s.ConversationId + "\"}");
                        File.WriteAllText(Path.Combine(brainDir, "trajectory.json"), "{\"steps\":" + s.StepCount + "}");
                    }
                }

                // Write Project JSON
                string projJsonFile = Path.Combine(context.ConfigPath, "projects", SanitizeFileName(p.ProjectName) + ".json");
                File.WriteAllText(projJsonFile, "{\"name\":\"" + p.ProjectName + "\",\"uri\":\"" + p.FolderUri + "\"}");
            }

            // Write agyhub_summaries_proto.pb
            byte[] pbBytes = BuildSyntheticProtobufBytes(allSessions);
            File.WriteAllBytes(Path.Combine(context.AntigravityPath, "agyhub_summaries_proto.pb"), pbBytes);
        }

        public static string CreateSyntheticBackupArchive(string outputZipPath, List<MockProjectSpec> projects, bool includeGlobalConfigs = true)
        {
            if (File.Exists(outputZipPath))
                File.Delete(outputZipPath);

            string dir = Path.GetDirectoryName(outputZipPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            List<MockSessionSpec> allSessions = new List<MockSessionSpec>();
            if (projects != null)
            {
                for (int i = 0; i < projects.Count; i++)
                {
                    MockProjectSpec p = projects[i];
                    for (int j = 0; j < p.Sessions.Count; j++)
                    {
                        MockSessionSpec s = p.Sessions[j];
                        if (string.IsNullOrEmpty(s.ProjectName)) s.ProjectName = p.ProjectName;
                        if (string.IsNullOrEmpty(s.FolderUri)) s.FolderUri = p.FolderUri;
                        allSessions.Add(s);
                    }
                }
            }

            byte[] pbBytes = BuildSyntheticProtobufBytes(allSessions);

            using (FileStream fs = new FileStream(outputZipPath, FileMode.Create, FileAccess.Write))
            using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                // 1. metadata.json
                ZipArchiveEntry metaEntry = archive.CreateEntry("metadata.json", CompressionLevel.Fastest);
                using (Stream ms = metaEntry.Open())
                using (StreamWriter sw = new StreamWriter(ms, Encoding.UTF8))
                {
                    sw.Write("{\"version\":\"0.5\",\"timestamp\":\"" + DateTime.UtcNow.ToString("o") + "\",\"sessionsCount\":" + allSessions.Count + "}");
                }

                // 2. agyhub_summaries_proto.pb
                ZipArchiveEntry pbEntry = archive.CreateEntry("agyhub_summaries_proto.pb", CompressionLevel.Fastest);
                using (Stream ps = pbEntry.Open())
                {
                    ps.Write(pbBytes, 0, pbBytes.Length);
                }

                // 3. Project JSONs
                if (projects != null)
                {
                    for (int i = 0; i < projects.Count; i++)
                    {
                        MockProjectSpec p = projects[i];
                        string jsonEntryName = "config/projects/" + SanitizeFileName(p.ProjectName) + ".json";
                        ZipArchiveEntry pe = archive.CreateEntry(jsonEntryName, CompressionLevel.Fastest);
                        using (Stream s = pe.Open())
                        using (StreamWriter sw = new StreamWriter(s, Encoding.UTF8))
                        {
                            sw.Write("{\"name\":\"" + p.ProjectName + "\",\"uri\":\"" + p.FolderUri + "\"}");
                        }
                    }
                }

                // 4. Conversations & Brain
                for (int i = 0; i < allSessions.Count; i++)
                {
                    MockSessionSpec s = allSessions[i];
                    string convEntryName = "conversations/" + s.ConversationId + ".db";
                    ZipArchiveEntry ce = archive.CreateEntry(convEntryName, CompressionLevel.Fastest);
                    using (Stream cs = ce.Open())
                    {
                        byte[] dummy = GenerateDummyBinary(s.DataSizeBytes > 0 ? s.DataSizeBytes : 1024, "BACKUP_SQLITE:" + s.ConversationId);
                        cs.Write(dummy, 0, dummy.Length);
                    }

                    if (s.HasWalFiles)
                    {
                        ZipArchiveEntry walEntry = archive.CreateEntry(convEntryName + "-wal", CompressionLevel.Fastest);
                        using (Stream ws = walEntry.Open())
                        {
                            byte[] walBytes = Encoding.UTF8.GetBytes("WAL:" + s.ConversationId);
                            ws.Write(walBytes, 0, walBytes.Length);
                        }
                    }

                    if (s.HasBrainData)
                    {
                        string brainPrefix = "brain/" + s.ConversationId + "/";
                        ZipArchiveEntry kEntry = archive.CreateEntry(brainPrefix + "knowledge.json", CompressionLevel.Fastest);
                        using (Stream ks = kEntry.Open())
                        using (StreamWriter sw = new StreamWriter(ks, Encoding.UTF8))
                        {
                            sw.Write("{\"title\":\"" + (s.CustomTitle ?? s.AutoSummary) + "\",\"id\":\"" + s.ConversationId + "\"}");
                        }

                        ZipArchiveEntry tEntry = archive.CreateEntry(brainPrefix + "trajectory.json", CompressionLevel.Fastest);
                        using (Stream ts = tEntry.Open())
                        using (StreamWriter sw = new StreamWriter(ts, Encoding.UTF8))
                        {
                            sw.Write("{\"steps\":" + s.StepCount + "}");
                        }
                    }
                }

                // 5. Global Configs
                if (includeGlobalConfigs)
                {
                    ZipArchiveEntry rEntry = archive.CreateEntry("rules/GEMINI.md", CompressionLevel.Fastest);
                    using (Stream rs = rEntry.Open())
                    using (StreamWriter sw = new StreamWriter(rs, Encoding.UTF8))
                    {
                        sw.Write("# Global Rules\nTest rule content");
                    }

                    ZipArchiveEntry sEntry = archive.CreateEntry("skills/dual-agent/SKILL.md", CompressionLevel.Fastest);
                    using (Stream ss = sEntry.Open())
                    using (StreamWriter sw = new StreamWriter(ss, Encoding.UTF8))
                    {
                        sw.Write("# Skill: Dual Agent\nTest skill content");
                    }

                    ZipArchiveEntry mEntry = archive.CreateEntry("mcp/codegraph/codegraph_explore.json", CompressionLevel.Fastest);
                    using (Stream ms = mEntry.Open())
                    using (StreamWriter sw = new StreamWriter(ms, Encoding.UTF8))
                    {
                        sw.Write("{\"name\":\"codegraph_explore\"}");
                    }
                }
            }

            return outputZipPath;
        }

        private static byte[] GenerateDummyBinary(int size, string header)
        {
            byte[] buf = new byte[size];
            byte[] headerBytes = Encoding.UTF8.GetBytes(header);
            int copyLen = Math.Min(headerBytes.Length, size);
            Array.Copy(headerBytes, 0, buf, 0, copyLen);
            for (int i = copyLen; i < size; i++)
            {
                buf[i] = (byte)(i % 251);
            }
            return buf;
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "project";
            char[] invalids = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                bool invalid = false;
                for (int j = 0; j < invalids.Length; j++)
                {
                    if (name[i] == invalids[j]) { invalid = true; break; }
                }
                sb.Append(invalid ? '_' : name[i]);
            }
            return sb.ToString();
        }
    }
}
