using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using AntigravityCompanion;

namespace AntigravityCompanion.Tests
{
    public static class TestRunner
    {
        private static int _passed = 0;
        private static int _failed = 0;
        private static int _assertions = 0;

        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("===============================================================================");
            Console.WriteLine("  Antigravity Companion V0.6 - Production Code Verification Suite");
            Console.WriteLine("===============================================================================");

            Stopwatch sw = Stopwatch.StartNew();

            Run("T01_ProtoCodec_RoundtripAndVarintBoundary", T01_ProtoCodec_RoundtripAndVarintBoundary);
            Run("T02_SessionMetadataExtractor_RealProtoCustomTitle", T02_SessionMetadataExtractor_RealProtoCustomTitle);
            Run("T03_SessionMetadataExtractor_RejectsFlatInvalidEntry", T03_SessionMetadataExtractor_RejectsFlatInvalidEntry);
            Run("T04_SmartProjectFilter_JunkAndSubagentIsolation", T04_SmartProjectFilter_JunkAndSubagentIsolation);
            Run("T05_StreamingZipIndexReader_InMemoryInspection", T05_StreamingZipIndexReader_InMemoryInspection);
            Run("T06_VisualDiffEngine_DiffAndCrossDriveMerge", T06_VisualDiffEngine_DiffAndCrossDriveMerge);
            Run("T07_IncrementalProtoShardMerger_LosslessMergeAndUriRemap", T07_IncrementalProtoShardMerger_LosslessMergeAndUriRemap);
            Run("T08_DirectStreamArchiver_MultiFormatPathRemap", T08_DirectStreamArchiver_MultiFormatPathRemap);
            Run("T09_F1_CascadingDeleteSessionProjectAndSubagents", T09_F1_CascadingDeleteSessionProjectAndSubagents);
            Run("T10_F1_OrphanGarbageCollector_ScanAndClean", T10_F1_OrphanGarbageCollector_ScanAndClean);
            Run("T11_F2_RenameSession_DualFieldPersistence", T11_F2_RenameSession_DualFieldPersistence);
            Run("T12_F2_MoveSessionAndRelocateProjectPath", T12_F2_MoveSessionAndRelocateProjectPath);
            Run("T13_F3_ExportSelectiveBackup_SlicedProtoAndRoundtrip", T13_F3_ExportSelectiveBackup_SlicedProtoAndRoundtrip);
            Run("T14_F4_TranscriptMarkdownExporter_LocalAndBackupZip", T14_F4_TranscriptMarkdownExporter_LocalAndBackupZip);

            sw.Stop();
            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine(string.Format("  Summary: {0} Passed, {1} Failed ({2} Assertions, {3:F1}ms)",
                _passed, _failed, _assertions, sw.Elapsed.TotalMilliseconds));
            Console.WriteLine("===============================================================================");

            return _failed == 0 ? 0 : 1;
        }

        private static void Run(string name, Action action)
        {
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                action();
                sw.Stop();
                _passed++;
                Console.WriteLine(string.Format("  [PASS] {0,-56} ({1:F1}ms)", name, sw.Elapsed.TotalMilliseconds));
            }
            catch (Exception ex)
            {
                sw.Stop();
                _failed++;
                Console.WriteLine(string.Format("  [FAIL] {0,-56} ({1:F1}ms)\n         {2}", name, sw.Elapsed.TotalMilliseconds, ex.Message));
            }
        }

        private static void AssertTrue(bool cond, string msg)
        {
            _assertions++;
            if (!cond) throw new Exception("AssertTrue failed: " + msg);
        }

        private static void AssertEqual<T>(T expected, T actual, string msg)
        {
            _assertions++;
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception(string.Format("{0} (Expected: '{1}', Actual: '{2}')", msg, expected, actual));
        }

        private static ProtoNode BuildRealProtoEntry(string cid, string autoTitle, string customTitle, string folderUri, string projName, string projId, int steps, int userInteractions, int? field23)
        {
            ProtoNode entry = new ProtoNode(1, 2);
            entry.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(cid), IsModified = true });

            ProtoNode meta = new ProtoNode(2, 2) { IsModified = true };
            if (!string.IsNullOrEmpty(autoTitle))
                meta.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(autoTitle), IsModified = true });
            meta.Children.Add(new ProtoNode(2, 0) { VarintValue = (ulong)steps, IsModified = true });

            ProtoNode ts = new ProtoNode(3, 2) { IsModified = true };
            ts.Children.Add(new ProtoNode(1, 0) { VarintValue = 1756000000UL, IsModified = true });
            meta.Children.Add(ts);

            if (!string.IsNullOrEmpty(folderUri))
            {
                ProtoNode ws = new ProtoNode(9, 2) { IsModified = true };
                ws.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(folderUri), IsModified = true });
                meta.Children.Add(ws);
            }

            if (!string.IsNullOrEmpty(customTitle))
            {
                ProtoNode ct = new ProtoNode(15, 2) { IsModified = true };
                ct.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(customTitle), IsModified = true });
                meta.Children.Add(ct);
            }

            meta.Children.Add(new ProtoNode(16, 0) { VarintValue = (ulong)userInteractions, IsModified = true });

            if (!string.IsNullOrEmpty(projName) || !string.IsNullOrEmpty(projId) || !string.IsNullOrEmpty(folderUri))
            {
                ProtoNode cascade = new ProtoNode(17, 2) { IsModified = true };
                if (!string.IsNullOrEmpty(projName))
                    cascade.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(projName), IsModified = true });
                if (!string.IsNullOrEmpty(folderUri))
                    cascade.Children.Add(new ProtoNode(7, 2) { RawBytes = Encoding.UTF8.GetBytes(folderUri), IsModified = true });
                if (!string.IsNullOrEmpty(projId))
                    cascade.Children.Add(new ProtoNode(18, 2) { RawBytes = Encoding.UTF8.GetBytes(projId), IsModified = true });
                meta.Children.Add(cascade);
            }

            if (field23.HasValue)
            {
                meta.Children.Add(new ProtoNode(23, 0) { VarintValue = (ulong)field23.Value, IsModified = true });
            }

            entry.Children.Add(meta);
            entry.IsModified = true;
            return entry;
        }

        private static void T01_ProtoCodec_RoundtripAndVarintBoundary()
        {
            List<ProtoNode> nodes = new List<ProtoNode>
            {
                new ProtoNode(1, 0) { VarintValue = ulong.MaxValue },
                new ProtoNode(2, 1) { RawBytes = BitConverter.GetBytes(0x1122334455667788UL) },
                new ProtoNode(3, 2) { RawBytes = Encoding.UTF8.GetBytes("中文测试_UTF8_🚀") },
                new ProtoNode(4, 5) { RawBytes = BitConverter.GetBytes(0x12345678U) }
            };

            byte[] encoded = ProtoCodec.Encode(nodes);
            List<ProtoNode> decoded = ProtoCodec.Decode(encoded);

            AssertEqual(4, decoded.Count, "Decoded node count");
            AssertEqual(ulong.MaxValue, decoded[0].VarintValue, "Max UInt64 varint roundtrip");
            AssertEqual("中文测试_UTF8_🚀", decoded[2].GetString(), "UTF-8 string roundtrip");
        }

        private static void T02_SessionMetadataExtractor_RealProtoCustomTitle()
        {
            string cid = "11111111-2222-3333-4444-555555555555";
            ProtoNode entry = BuildRealProtoEntry(
                cid, "Initial English Summary", "【自定义中文标题】BMS架构精简",
                "file:///c%3A/Workspace/BMS_App", "BMS_App", "proj-uuid-1", 28, 5, 1
            );

            byte[] pb = ProtoCodec.Encode(new List<ProtoNode> { entry });
            List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(pb);

            AssertEqual(1, list.Count, "Should extract 1 conversation");
            AssertEqual(cid, list[0].ConversationId, "ConversationId match");
            AssertEqual("【自定义中文标题】BMS架构精简", list[0].DisplayTitle, "CustomTitle takes priority");
            AssertTrue(list[0].HasCustomTitle, "HasCustomTitle flag");
            AssertEqual("BMS_App", list[0].ProjectName, "ProjectName extracted");
            AssertEqual(28, list[0].StepCount, "StepCount extracted");
            AssertTrue(!list[0].IsSubagent, "Main session is not subagent");
        }

        private static void T03_SessionMetadataExtractor_RejectsFlatInvalidEntry()
        {
            // Flat node without Field 2 submessage should be rejected
            ProtoNode flat = new ProtoNode(1, 2) { IsModified = true };
            flat.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes("11111111-2222-3333-4444-555555555555"), IsModified = true });
            byte[] pb = ProtoCodec.Encode(new List<ProtoNode> { flat });

            List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(pb);
            AssertEqual(0, list.Count, "Flat non-real proto entry must be ignored");
        }

        private static void T04_SmartProjectFilter_JunkAndSubagentIsolation()
        {
            AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("subagent-reviewer-1"), "subagent-* is junk");
            AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("Recovered project"), "Recovered project is junk");
            AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("default-cli-project"), "default-cli-project is junk");
            AssertTrue(!SmartProjectFilter.IsJunkOrEphemeral("Outside of Project"), "Outside of Project is preserved");
            AssertTrue(!SmartProjectFilter.IsJunkOrEphemeral("Antigravity_Companion"), "Normal project is preserved");

            ProtoNode subEntry = BuildRealProtoEntry(
                Guid.NewGuid().ToString(), "Subagent task", null,
                "file:///c:/Tools/App", "App", "p1", 6, 0, 0
            );
            byte[] pb = ProtoCodec.Encode(new List<ProtoNode> { subEntry });
            List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(pb);
            AssertEqual(1, list.Count, "Parsed 1 entry");
            AssertTrue(list[0].IsSubagent, "Field 23 == 0 identified as subagent");
        }

        private static void T05_StreamingZipIndexReader_InMemoryInspection()
        {
            string tempZip = Path.Combine(Path.GetTempPath(), "agy_test_" + Guid.NewGuid().ToString("N") + ".agy");
            try
            {
                string cid = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
                ProtoNode entry = BuildRealProtoEntry(
                    cid, "Auto title", "流式归档测试会话",
                    "file:///d:/OldPC/MyProj", "MyProj", "proj-01", 10, 2, 1
                );
                byte[] pbBytes = ProtoCodec.Encode(new List<ProtoNode> { entry });

                using (FileStream fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write))
                using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    using (StreamWriter sw = new StreamWriter(zip.CreateEntry("metadata.json").Open(), Encoding.UTF8))
                    {
                        sw.Write("{\"source_home\":\"D:\\\\Users\\\\OldUser\",\"version\":\"0.6\"}");
                    }
                    using (StreamWriter sw = new StreamWriter(zip.CreateEntry("data/config/projects/proj-01.json").Open(), Encoding.UTF8))
                    {
                        sw.Write("{\"id\":\"proj-01\",\"name\":\"MyProj\",\"folderUri\":\"file:///d:/OldPC/MyProj\"}");
                    }
                    using (Stream s = zip.CreateEntry("data/antigravity/agyhub_summaries_proto.pb").Open())
                    {
                        s.Write(pbBytes, 0, pbBytes.Length);
                    }
                    using (StreamWriter sw = new StreamWriter(zip.CreateEntry("data/antigravity/conversations/" + cid + ".db").Open(), Encoding.UTF8))
                    {
                        sw.Write("SQLITE_DUMMY");
                    }
                    using (StreamWriter sw = new StreamWriter(zip.CreateEntry("data/.geminirules").Open(), Encoding.UTF8))
                    {
                        sw.Write("# Rules");
                    }
                }

                BackupArchiveIndex idx = StreamingZipIndexReader.ReadIndex(tempZip);
                AssertEqual("D:\\Users\\OldUser", idx.SourceHome, "SourceHome parsed");
                AssertEqual(1, idx.Projects.Count, "1 project parsed");
                AssertEqual(1, idx.Conversations.Count, "1 conversation parsed");
                AssertEqual("流式归档测试会话", idx.Conversations[0].DisplayTitle, "Conversation title in zip");
                AssertTrue(idx.ExistingConvoDbFiles.Contains(cid), "DB file detected");
                AssertTrue(idx.HasRules, "Global rules detected");
            }
            finally
            {
                if (File.Exists(tempZip)) File.Delete(tempZip);
            }
        }

        private static void T06_VisualDiffEngine_DiffAndCrossDriveMerge()
        {
            string sharedCid = "11111111-1111-1111-1111-111111111111";
            string newCid = "22222222-2222-2222-2222-222222222222";

            List<ConversationSummaryInfo> localList = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo
                {
                    ConversationId = sharedCid,
                    DisplayTitle = "已对齐会话",
                    ProjectName = "CrossDriveProj",
                    FolderUri = "file:///c:/Users/NewPC/CrossDriveProj",
                    StepCount = 10,
                    UpdatedTime = new DateTime(2026, 8, 25, 12, 0, 0)
                }
            };

            // Same project on different drive in backup: should NOT be flagged as conflicted,
            // and should accurately reflect new conversation status.
            List<ConversationSummaryInfo> backupList = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo
                {
                    ConversationId = sharedCid,
                    DisplayTitle = "已对齐会话",
                    ProjectName = "CrossDriveProj",
                    FolderUri = "file:///d:/Workspace/OldPC/CrossDriveProj",
                    StepCount = 10,
                    UpdatedTime = new DateTime(2026, 8, 25, 12, 0, 0)
                },
                new ConversationSummaryInfo
                {
                    ConversationId = newCid,
                    DisplayTitle = "备份新增会话",
                    ProjectName = "CrossDriveProj",
                    FolderUri = "file:///d:/Workspace/OldPC/CrossDriveProj",
                    StepCount = 5,
                    UpdatedTime = new DateTime(2026, 8, 25, 13, 0, 0)
                }
            };

            List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(localList, backupList);
            AssertEqual(1, diff.Count, "1 merged project diff node");
            AssertEqual(DiffStatus.NewInBackup, diff[0].Status, "Cross-drive project with new session is NewInBackup");
            AssertEqual(1, diff[0].NewSessionCount, "1 new session detected");
        }

        private static void T07_IncrementalProtoShardMerger_LosslessMergeAndUriRemap()
        {
            string localCid = "aaaa1111-1111-1111-1111-111111111111";
            string backupCid = "bbbb2222-2222-2222-2222-222222222222";

            ProtoNode localEntry = BuildRealProtoEntry(
                localCid, "Local Auto", "本地原有会话",
                "file:///c:/Users/NewUser/Desktop/ProjA", "ProjA", "local-proj-id", 12, 3, 1
            );
            byte[] localPb = ProtoCodec.Encode(new List<ProtoNode> { localEntry });

            ProtoNode backupEntry = BuildRealProtoEntry(
                backupCid, "Backup Auto", "跨盘符导入会话",
                "file:///d:/OldDrive/Work/ProjA", "ProjA", "old-proj-id", 20, 4, 1
            );
            byte[] backupPb = ProtoCodec.Encode(new List<ProtoNode> { backupEntry });
            List<ConversationSummaryInfo> backupInfos = SessionMetadataExtractor.ExtractSummariesFromProto(backupPb);

            Dictionary<string, string> uriOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            uriOverrides["ProjA"] = "file:///c:/Users/NewUser/Desktop/ProjA";

            Dictionary<string, string> projNewIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            projNewIds["ProjA"] = "local-proj-id";

            byte[] mergedPb = IncrementalProtoShardMerger.MergeShards(
                localPb, backupInfos, "C:\\Users\\OldUser", "C:\\Users\\NewUser", uriOverrides, projNewIds
            );

            List<ConversationSummaryInfo> mergedList = SessionMetadataExtractor.ExtractSummariesFromProto(mergedPb);
            AssertEqual(2, mergedList.Count, "Merged pb contains both local and imported sessions");

            ConversationSummaryInfo imported = mergedList.Find(x => x.ConversationId == backupCid);
            AssertTrue(imported != null, "Imported session found");
            AssertEqual("跨盘符导入会话", imported.DisplayTitle, "Imported custom title preserved");
            AssertEqual("file:///c:/Users/NewUser/Desktop/ProjA", imported.FolderUri, "Cross-drive FolderUri remapped to local project URI");
            AssertEqual("local-proj-id", imported.ProjectId, "ProjectId synchronized to local project ID");
        }

        private static void T08_DirectStreamArchiver_MultiFormatPathRemap()
        {
            string src = "C:\\Users\\Alice";
            string dst = "D:\\Users\\Bob";
            string input = "p1=C:\\Users\\Alice\\Work; p2=C:/Users/Alice/Work; p3=C:\\\\Users\\\\Alice\\\\Work; p4=file:///c%3A%2FUsers%2FAlice/Work; p5=file:///C%3a%5cUsers%5cAlice; p6=file:///c%3A/Users/Alice/Work";
            string output = DirectStreamArchiver.RemapAllPathFormats(input, src, dst);

            AssertTrue(!output.Contains("Alice"), "All 6 path encodings remapped from Alice to Bob");
            AssertTrue(output.Contains("D:\\Users\\Bob\\Work"), "Windows backslash remapped");
            AssertTrue(output.Contains("D:/Users/Bob/Work"), "Forward slash remapped");
            AssertTrue(output.Contains("D:\\\\Users\\\\Bob\\\\Work"), "JSON escaped backslash remapped");
            AssertTrue(output.Contains("file:///D%3A/Users/Bob/Work"), "VSCode colon-encoded URI remapped");
        }

        private static void T09_F1_CascadingDeleteSessionProjectAndSubagents()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_f1_del_" + Guid.NewGuid().ToString("N"));
            try
            {
                string convDir = Path.Combine(sb, "antigravity", "conversations");
                string brainDir = Path.Combine(sb, "antigravity", "brain");
                string projDir = Path.Combine(sb, "config", "projects");
                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(brainDir);
                Directory.CreateDirectory(projDir);

                string cidInvert = "11111111-1111-1111-1111-111111111111";
                string cidAdmin = "22222222-2222-2222-2222-222222222222";
                string cidSub = "33333333-3333-3333-3333-333333333333";
                string cidKeep = "44444444-4444-4444-4444-444444444444";

                ProtoNode nInvert = BuildRealProtoEntry(cidInvert, "Battery Priority Inverter Logic", null, "file:///c:/InvertProject", "InvertProject", "proj-invert", 10, 2, 1);
                ProtoNode nAdmin = BuildRealProtoEntry(cidAdmin, "hi", null, "file:///c:/Users/Administrator", "Administrator", "outside-of-project", 4, 1, 1);
                ProtoNode nSub = BuildRealProtoEntry(cidSub, "Subagent Research", null, "file:///c:/Users/Administrator", "Administrator", "outside-of-project", 6, 0, 0);
                // Set ParentConversationId (Field 17 -> Field 11) on nSub to cidAdmin
                ProtoNode subMeta = nSub.GetChild(2);
                ProtoNode subCascade = subMeta.GetChild(17);
                subCascade.Children.Add(new ProtoNode(11, 2) { RawBytes = Encoding.UTF8.GetBytes(cidAdmin), IsModified = true });

                ProtoNode nKeep = BuildRealProtoEntry(cidKeep, "Keep Session", null, "file:///c:/KeepProject", "KeepProject", "proj-keep", 15, 3, 1);

                string pbPath = Path.Combine(sb, "antigravity", "agyhub_summaries_proto.pb");
                byte[] initialPb = ProtoCodec.Encode(new List<ProtoNode> { nInvert, nAdmin, nSub, nKeep });
                File.WriteAllBytes(pbPath, initialPb);

                File.WriteAllText(Path.Combine(projDir, "proj-invert.json"), "{\"id\":\"proj-invert\",\"name\":\"InvertProject\",\"folderUri\":\"file:///c:/InvertProject\"}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(projDir, "proj-keep.json"), "{\"id\":\"proj-keep\",\"name\":\"KeepProject\",\"folderUri\":\"file:///c:/KeepProject\"}", Encoding.UTF8);

                string[] allCids = new string[] { cidInvert, cidAdmin, cidSub, cidKeep };
                for (int i = 0; i < allCids.Length; i++)
                {
                    File.WriteAllText(Path.Combine(convDir, allCids[i] + ".db"), "DUMMY_SQLITE_DATA", Encoding.UTF8);
                    File.WriteAllText(Path.Combine(convDir, allCids[i] + ".db-wal"), "DUMMY_WAL", Encoding.UTF8);
                    string b = Path.Combine(brainDir, allCids[i]);
                    Directory.CreateDirectory(b);
                    File.WriteAllText(Path.Combine(b, "note.md"), "# Note", Encoding.UTF8);
                }

                Dictionary<string, ProjectMetadata> localProjects = new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
                localProjects["proj-invert"] = new ProjectMetadata { Id = "proj-invert", Name = "InvertProject", FolderUri = "file:///c:/InvertProject" };
                localProjects["proj-keep"] = new ProjectMetadata { Id = "proj-keep", Name = "KeepProject", FolderUri = "file:///c:/KeepProject" };

                List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummaries(initialPb, localProjects);
                AssertEqual(4, summaries.Count, "4 initial sessions extracted");

                HashSet<string> targetConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { cidAdmin };
                HashSet<string> targetProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "InvertProject" };

                DeletionResult res = LocalWorkspaceManager.DeleteSessionsAndProjects(sb, summaries, localProjects, targetConvIds, targetProjects);

                AssertEqual(3, res.DeletedSessionCount, "Should delete InvertProject session + cidAdmin + cascaded subagent cidSub");
                AssertEqual(6, res.DeletedDbFilesCount, "Should delete 3 .db and 3 .db-wal files");
                AssertEqual(3, res.DeletedBrainDirsCount, "Should delete 3 brain directories");
                AssertEqual(1, res.DeletedProjectJsonCount, "Should delete proj-invert.json");
                AssertTrue(res.FreedBytes > 0, "FreedBytes > 0");
                AssertTrue(File.Exists(pbPath + ".bak"), ".pb.bak backup created");

                List<ConversationSummaryInfo> remaining = SessionMetadataExtractor.ExtractSummaries(File.ReadAllBytes(pbPath), localProjects);
                AssertEqual(1, remaining.Count, "Only 1 session remains in .pb");
                AssertEqual(cidKeep, remaining[0].ConversationId, "cidKeep preserved in .pb");
                AssertTrue(File.Exists(Path.Combine(convDir, cidKeep + ".db")), "cidKeep.db preserved");
                AssertTrue(Directory.Exists(Path.Combine(brainDir, cidKeep)), "cidKeep brain preserved");
                AssertTrue(File.Exists(Path.Combine(projDir, "proj-keep.json")), "proj-keep.json preserved");
                AssertTrue(!File.Exists(Path.Combine(projDir, "proj-invert.json")), "proj-invert.json deleted");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T10_F1_OrphanGarbageCollector_ScanAndClean()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_f1_gc_" + Guid.NewGuid().ToString("N"));
            try
            {
                string convDir = Path.Combine(sb, "antigravity", "conversations");
                string brainDir = Path.Combine(sb, "antigravity", "brain");
                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(brainDir);

                string cidActive = "aaaaaaaa-1111-1111-1111-111111111111";
                string cidOrphan1 = "bbbbbbbb-2222-2222-2222-222222222222";
                string cidOrphan2 = "cccccccc-3333-3333-3333-333333333333";

                ProtoNode nActive = BuildRealProtoEntry(cidActive, "Active Session", null, "file:///c:/App", "App", "p1", 5, 1, 1);
                string pbPath = Path.Combine(sb, "antigravity", "agyhub_summaries_proto.pb");
                File.WriteAllBytes(pbPath, ProtoCodec.Encode(new List<ProtoNode> { nActive }));

                // Active files
                File.WriteAllText(Path.Combine(convDir, cidActive + ".db"), "ACTIVE_DB", Encoding.UTF8);
                Directory.CreateDirectory(Path.Combine(brainDir, cidActive));

                // Orphan files
                File.WriteAllText(Path.Combine(convDir, cidOrphan1 + ".db"), "ORPHAN1_DB_CONTENT", Encoding.UTF8);
                File.WriteAllText(Path.Combine(convDir, cidOrphan1 + ".db-wal"), "ORPHAN1_WAL", Encoding.UTF8);
                string bOrphan1 = Path.Combine(brainDir, cidOrphan1);
                Directory.CreateDirectory(bOrphan1);
                File.WriteAllText(Path.Combine(bOrphan1, "artifact.md"), "ORPHAN_ARTIFACT", Encoding.UTF8);

                string bOrphan2 = Path.Combine(brainDir, cidOrphan2);
                Directory.CreateDirectory(bOrphan2);
                File.WriteAllText(Path.Combine(bOrphan2, "log.txt"), "ORPHAN2_LOG", Encoding.UTF8);

                OrphanScanResult scan = LocalWorkspaceManager.ScanOrphanedData(sb);
                AssertEqual(2, scan.OrphanSessionIds.Count, "2 orphan session UUIDs detected");
                AssertEqual(2, scan.OrphanDbFiles.Count, "2 orphan db files (.db + .db-wal)");
                AssertEqual(2, scan.OrphanBrainDirs.Count, "2 orphan brain dirs");
                AssertTrue(scan.TotalOrphanBytes > 0, "Orphan byte size calculated");

                DeletionResult cleaned = LocalWorkspaceManager.CleanOrphanedData(scan);
                AssertEqual(2, cleaned.DeletedDbFilesCount, "Cleaned 2 orphan db files");
                AssertEqual(2, cleaned.DeletedBrainDirsCount, "Cleaned 2 orphan brain dirs");
                AssertTrue(File.Exists(Path.Combine(convDir, cidActive + ".db")), "Active DB untouched");
                AssertTrue(Directory.Exists(Path.Combine(brainDir, cidActive)), "Active brain untouched");

                OrphanScanResult reScan = LocalWorkspaceManager.ScanOrphanedData(sb);
                AssertEqual(0, reScan.TotalOrphanItems, "Zero orphans after cleanup");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T11_F2_RenameSession_DualFieldPersistence()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_f2_ren_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(sb, "antigravity"));
                string cid = "12345678-1234-1234-1234-1234567890ab";
                ProtoNode entry = BuildRealProtoEntry(cid, "Original Auto Summary", null, "file:///c:/App", "App", "p1", 8, 2, 1);
                string pbPath = Path.Combine(sb, "antigravity", "agyhub_summaries_proto.pb");
                File.WriteAllBytes(pbPath, ProtoCodec.Encode(new List<ProtoNode> { entry }));

                bool ok = LocalWorkspaceManager.RenameSession(sb, cid, "【F2重命名】三电平逆变器死区补偿");
                AssertTrue(ok, "RenameSession returned true");

                List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(pbPath));
                AssertEqual(1, list.Count, "1 session after rename");
                AssertEqual("【F2重命名】三电平逆变器死区补偿", list[0].DisplayTitle, "DisplayTitle updated");
                AssertEqual("【F2重命名】三电平逆变器死区补偿", list[0].CustomTitle, "Field 15 CustomTitle updated");
                AssertEqual("【F2重命名】三电平逆变器死区补偿", list[0].AutoSummaryTitle, "Field 1 AutoSummaryTitle synchronized");
                AssertTrue(list[0].HasCustomTitle, "HasCustomTitle is true");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T12_F2_MoveSessionAndRelocateProjectPath()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_f2_mov_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(sb, "antigravity", "conversations"));
                Directory.CreateDirectory(Path.Combine(sb, "config", "projects"));

                string cid = "87654321-4321-4321-4321-ba0987654321";
                ProtoNode entry = BuildRealProtoEntry(cid, "Adhoc Session", null, "file:///c:/Users/Administrator", "Administrator", "outside-of-project", 12, 3, 1);
                string pbPath = Path.Combine(sb, "antigravity", "agyhub_summaries_proto.pb");
                File.WriteAllBytes(pbPath, ProtoCodec.Encode(new List<ProtoNode> { entry }));

                // 1. Move session from 'Administrator' to 'BMS_Controller'
                int moved = LocalWorkspaceManager.MoveSessionsToProject(
                    sb, new List<string> { cid }, "BMS_Controller", "proj-bms-99", "C:\\Work\\BMS_Controller"
                );
                AssertEqual(1, moved, "1 session moved");
                AssertTrue(File.Exists(Path.Combine(sb, "config", "projects", "proj-bms-99.json")), "Project JSON auto-created");

                List<ConversationSummaryInfo> listAfterMove = SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(pbPath));
                AssertEqual("BMS_Controller", listAfterMove[0].ProjectName, "ProjectName updated to BMS_Controller");
                AssertEqual("proj-bms-99", listAfterMove[0].ProjectId, "ProjectId updated to proj-bms-99");
                AssertEqual("file:///C%3A/Work/BMS_Controller", listAfterMove[0].FolderUri, "FolderUri updated");

                // 2. Relocate 'BMS_Controller' physical path to 'D:\NewDisk\BMS_V2'
                int relocated = LocalWorkspaceManager.RelocateProjectPath(
                    sb, "proj-bms-99", "BMS_Controller", "D:\\NewDisk\\BMS_V2", listAfterMove
                );
                AssertEqual(1, relocated, "1 session relocated");

                List<ConversationSummaryInfo> listAfterReloc = SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(pbPath));
                AssertEqual("file:///D%3A/NewDisk/BMS_V2", listAfterReloc[0].FolderUri, "FolderUri relocated in .pb");
                string projJson = File.ReadAllText(Path.Combine(sb, "config", "projects", "proj-bms-99.json"), Encoding.UTF8);
                AssertTrue(projJson.Contains("file:///D%3A/NewDisk/BMS_V2"), "Project JSON folderUri updated");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T13_F3_ExportSelectiveBackup_SlicedProtoAndRoundtrip()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_f3_exp_" + Guid.NewGuid().ToString("N"));
            string outZip = Path.Combine(Path.GetTempPath(), "agy_f3_selective_" + Guid.NewGuid().ToString("N") + ".agy");
            try
            {
                string convDir = Path.Combine(sb, "antigravity", "conversations");
                string brainDir = Path.Combine(sb, "antigravity", "brain");
                string projDir = Path.Combine(sb, "config", "projects");
                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(brainDir);
                Directory.CreateDirectory(projDir);

                string cid1 = "11110000-0000-0000-0000-000000000001";
                string cid2 = "22220000-0000-0000-0000-000000000002";

                ProtoNode n1 = BuildRealProtoEntry(cid1, "Selected Session", "导出选中会话", "file:///c:/Proj1", "Proj1", "p-1", 10, 2, 1);
                ProtoNode n2 = BuildRealProtoEntry(cid2, "Unselected Session", "未选中会话", "file:///c:/Proj2", "Proj2", "p-2", 20, 4, 1);

                string pbPath = Path.Combine(sb, "antigravity", "agyhub_summaries_proto.pb");
                byte[] fullPb = ProtoCodec.Encode(new List<ProtoNode> { n1, n2 });
                File.WriteAllBytes(pbPath, fullPb);

                File.WriteAllText(Path.Combine(projDir, "p-1.json"), "{\"id\":\"p-1\",\"name\":\"Proj1\",\"folderUri\":\"file:///c:/Proj1\"}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(projDir, "p-2.json"), "{\"id\":\"p-2\",\"name\":\"Proj2\",\"folderUri\":\"file:///c:/Proj2\"}", Encoding.UTF8);

                File.WriteAllText(Path.Combine(convDir, cid1 + ".db"), "DB1", Encoding.UTF8);
                File.WriteAllText(Path.Combine(convDir, cid2 + ".db"), "DB2", Encoding.UTF8);
                Directory.CreateDirectory(Path.Combine(brainDir, cid1));
                File.WriteAllText(Path.Combine(brainDir, cid1, "plan.md"), "# Plan 1", Encoding.UTF8);
                Directory.CreateDirectory(Path.Combine(brainDir, cid2));
                File.WriteAllText(Path.Combine(brainDir, cid2, "plan.md"), "# Plan 2", Encoding.UTF8);

                Dictionary<string, ProjectMetadata> projects = new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
                projects["p-1"] = new ProjectMetadata { Id = "p-1", Name = "Proj1", FolderUri = "file:///c:/Proj1" };
                projects["p-2"] = new ProjectMetadata { Id = "p-2", Name = "Proj2", FolderUri = "file:///c:/Proj2" };

                List<ConversationSummaryInfo> all = SessionMetadataExtractor.ExtractSummaries(fullPb, projects);
                List<ConversationSummaryInfo> selectedOnly = new List<ConversationSummaryInfo> { all[0] };

                int count = LocalWorkspaceManager.ExportSelectiveBackup(sb, "C:\\Users\\TestUser", outZip, selectedOnly, projects, false);
                AssertTrue(count >= 4, "Exported selective entries count");

                BackupArchiveIndex idx = StreamingZipIndexReader.ReadIndex(outZip);
                AssertEqual(1, idx.Conversations.Count, "Sliced .pb in selective .agy contains ONLY 1 conversation");
                AssertEqual(cid1, idx.Conversations[0].ConversationId, "Selected conversation ID matches");
                AssertEqual("导出选中会话", idx.Conversations[0].DisplayTitle, "Selected custom title preserved");
                AssertEqual(1, idx.Projects.Count, "Only Proj1 config included in selective backup");
                AssertTrue(idx.ExistingConvoDbFiles.Contains(cid1) && !idx.ExistingConvoDbFiles.Contains(cid2), "Only cid1.db packed");
                AssertTrue(idx.ExistingBrainDirs.Contains(cid1) && !idx.ExistingBrainDirs.Contains(cid2), "Only cid1 brain packed");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
                if (File.Exists(outZip)) File.Delete(outZip);
            }
        }

        private static void T14_F4_TranscriptMarkdownExporter_LocalAndBackupZip()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_f4_md_" + Guid.NewGuid().ToString("N"));
            string zipPath = Path.Combine(Path.GetTempPath(), "agy_f4_zip_" + Guid.NewGuid().ToString("N") + ".agy");
            try
            {
                string cid = "99998888-7777-6666-5555-444433332222";
                string logsDir = Path.Combine(sb, "antigravity", "brain", cid, ".system_generated", "logs");
                Directory.CreateDirectory(logsDir);

                string jsonl =
                    "{\"step_index\": 0, \"source\": \"USER_EXPLICIT\", \"type\": \"USER_INPUT\", \"created_at\": \"2026-11-06T06:00:00Z\", \"content\": \"<USER_REQUEST>\\n请实现会话删除与Markdown导出功能\\n</USER_REQUEST>\\n<ADDITIONAL_METADATA>noise_to_strip</ADDITIONAL_METADATA>\"}\n" +
                    "{\"step_index\": 5, \"source\": \"MODEL\", \"type\": \"PLANNER_RESPONSE\", \"created_at\": \"2026-11-06T06:01:00Z\", \"thinking\": \"正在设计 Protobuf AST 节点过滤与 JSONL 解析器\", \"content\": \"已完成 **F1-F4** 全部核心模块开发。\"}";

                string transcriptFile = Path.Combine(logsDir, "transcript.jsonl");
                File.WriteAllText(transcriptFile, jsonl, Encoding.UTF8);

                string artifactFile = Path.Combine(sb, "antigravity", "brain", cid, "implementation_plan.md");
                File.WriteAllText(artifactFile, "# Implementation Plan\n- Feature F1\n- Feature F4", Encoding.UTF8);

                ConversationSummaryInfo session = new ConversationSummaryInfo
                {
                    ConversationId = cid,
                    DisplayTitle = "F4对话导出测试会话",
                    ProjectName = "Antigravity_Companion",
                    FolderUri = "file:///c:/Tools/Antigravity_Companion",
                    UpdatedTime = new DateTime(2026, 11, 6, 15, 0, 0),
                    StepCount = 6,
                    UserInteractions = 1
                };

                // 1. Test Local Export to Markdown
                string localMdOut = Path.Combine(sb, "exported_local.md");
                string mdLocal = TranscriptMarkdownExporter.ExportLocalSessionToMarkdown(session, sb, localMdOut);
                AssertTrue(File.Exists(localMdOut), "Local Markdown file created");
                AssertTrue(mdLocal.Contains("F4对话导出测试会话"), "Contains session title");
                AssertTrue(mdLocal.Contains("请实现会话删除与Markdown导出功能"), "Contains user request");
                AssertTrue(!mdLocal.Contains("noise_to_strip"), "Stripped <ADDITIONAL_METADATA> noise");
                AssertTrue(mdLocal.Contains("正在设计 Protobuf AST 节点过滤与 JSONL 解析器"), "Contains model thinking");
                AssertTrue(mdLocal.Contains("已完成 **F1-F4** 全部核心模块开发。"), "Contains model response");
                AssertTrue(mdLocal.Contains("# Implementation Plan"), "Contains brain artifact markdown");

                // 2. Test Direct Streaming Export from Backup .agy Zip (without importing!)
                using (FileStream fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
                using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    using (StreamWriter sw = new StreamWriter(zip.CreateEntry("data/antigravity/brain/" + cid + "/.system_generated/logs/transcript.jsonl").Open(), Encoding.UTF8))
                    {
                        sw.Write(jsonl);
                    }
                    using (StreamWriter sw = new StreamWriter(zip.CreateEntry("data/antigravity/brain/" + cid + "/implementation_plan.md").Open(), Encoding.UTF8))
                    {
                        sw.Write("# Implementation Plan\n- Feature F1\n- Feature F4");
                    }
                }

                string zipMdOut = Path.Combine(sb, "exported_from_zip.md");
                string mdZip = TranscriptMarkdownExporter.ExportBackupSessionToMarkdown(session, zipPath, zipMdOut);
                AssertTrue(File.Exists(zipMdOut), "Backup Zip Markdown file created");
                AssertTrue(mdZip.Contains("请实现会话删除与Markdown导出功能"), "Zip stream Markdown contains user request");
                AssertTrue(mdZip.Contains("已完成 **F1-F4** 全部核心模块开发。"), "Zip stream Markdown contains model response");
                AssertTrue(mdZip.Contains("# Implementation Plan"), "Zip stream Markdown contains artifact");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
                if (File.Exists(zipPath)) File.Delete(zipPath);
            }
        }
    }
}
