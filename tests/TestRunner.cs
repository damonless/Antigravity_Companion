using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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
            Console.WriteLine("  Antigravity Companion V0.8 - Production Code Verification Suite");
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
            Run("T15_V07_CapabilityScanner_FiveCategoriesAndJsonKeyLexer", T15_V07_CapabilityScanner_FiveCategoriesAndJsonKeyLexer);
            Run("T16_V07_RepositoryPushAndSelectiveImport_KeyLevelMerge", T16_V07_RepositoryPushAndSelectiveImport_KeyLevelMerge);
            Run("T17_V07_MissingPathDetectionAndRelocation", T17_V07_MissingPathDetectionAndRelocation);
            Run("T18_V07_SelectiveAgyArchive_CapabilitySliceRoundtrip", T18_V07_SelectiveAgyArchive_CapabilitySliceRoundtrip);
            Run("T19_ProtoAstCodec_StrictnessField15AndSharding", T19_ProtoAstCodec_StrictnessField15AndSharding);
            Run("T20_RealWinSqlite3_WalAndChinesePathRemap", T20_RealWinSqlite3_WalAndChinesePathRemap);
            Run("T21_StreamingZipIndexReader_ZipSlipAndSelectiveClosure", T21_StreamingZipIndexReader_ZipSlipAndSelectiveClosure);
            Run("T22_CapabilityRepo_PlaceholdersJsoncEncodedIdAndHooks", T22_CapabilityRepo_PlaceholdersJsoncEncodedIdAndHooks);
            Run("T23_TranscriptMarkdownExporter_StreamingTagsAndFullJsonl", T23_TranscriptMarkdownExporter_StreamingTagsAndFullJsonl);
            Run("T24_VisualDiffEngine_RenameCrossHostAndSameBasename", T24_VisualDiffEngine_RenameCrossHostAndSameBasename);
            Run("T25_MainForm_PureHelpers_SelectionMoveAndProjectJson", T25_MainForm_PureHelpers_SelectionMoveAndProjectJson);
            Run("T26_SessionMetadataExtractor_NanoEpochFixed64AndOutsideUri", T26_SessionMetadataExtractor_NanoEpochFixed64AndOutsideUri);
            Run("T27_VisualDiffEngine_CapMissingPathsLocalOnlyCountAndSig", T27_VisualDiffEngine_CapMissingPathsLocalOnlyCountAndSig);
            Run("T28_CapabilityRepo_EncodingBomMultilineJsonAndYaml", T28_CapabilityRepo_EncodingBomMultilineJsonAndYaml);
            Run("T29_MainForm_FilteredSetsAndHiddenDeletionGuard", T29_MainForm_FilteredSetsAndHiddenDeletionGuard);
            Run("T30_CapabilityRepo_ScriptPathRegexQuotedEnvAndSpaces", T30_CapabilityRepo_ScriptPathRegexQuotedEnvAndSpaces);
            Run("T31_MainForm_RelocateCapPathSelectionState", T31_MainForm_RelocateCapPathSelectionState);
            Run("T32_PbtxtAnnotationCodec_RoundTripAndCreate", T32_PbtxtAnnotationCodec_RoundTripAndCreate);
            Run("T33_SummarySqliteBridge_ThreeLayerAndHybridFts", T33_SummarySqliteBridge_ThreeLayerAndHybridFts);
            Run("T34_AutoTitleSynthesizer_RulesAndSubagentExclusion", T34_AutoTitleSynthesizer_RulesAndSubagentExclusion);
            Run("T35_BatchRenameAndRowLevelRollback_OrphanGc", T35_BatchRenameAndRowLevelRollback_OrphanGc);
            Run("T36_AiPolishExchangeAndAgyAnnotationRoundTrip", T36_AiPolishExchangeAndAgyAnnotationRoundTrip);
            Run("T37_ProjectAttribution_Field17Submessage_SqliteBackfill_MultiRootUris", T37_ProjectAttribution_Field17Submessage_SqliteBackfill_MultiRootUris);
            Run("T38_RenameRules_Field9To4Branch_TrailingBranchStrip_EmbeddedBracketFold_LowValue", T38_RenameRules_Field9To4Branch_TrailingBranchStrip_EmbeddedBracketFold_LowValue);
            Run("T39_CliBrainFallback_FirstPrompt_MarkdownExport_ShortestDistinguishingPath", T39_CliBrainFallback_FirstPrompt_MarkdownExport_ShortestDistinguishingPath);
            Run("T40_DirectionalCapabilityDiff_BaselineAndUtcMtime_LineLcsDiff_ReadOnlyGc", T40_DirectionalCapabilityDiff_BaselineAndUtcMtime_LineLcsDiff_ReadOnlyGc);
            Run("T41_LocalWebHost_TcpFraming_AuthSecurity_StaticResources_AndReadApis", T41_LocalWebHost_TcpFraming_AuthSecurity_StaticResources_AndReadApis);
            Run("T42_LocalWebHost_EndToEndMutationSandbox_RenameRollbackCapabilityAndGc", T42_LocalWebHost_EndToEndMutationSandbox_RenameRollbackCapabilityAndGc);

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
                Console.WriteLine(string.Format("  [FAIL] {0,-56} ({1:F1}ms)\n         {2}", name, sw.Elapsed.TotalMilliseconds, ex.ToString()));
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
            string input = "p1=C:\\Users\\Alice\\Work; p2=C:/Users/Alice/Work; p3=C:\\\\Users\\\\Alice\\\\Work; p4=file:///c%3A%2FUsers%2FAlice/Work; p5=file:///C%3a%5cUsers%5cAlice; p6=file:///c%3A/Users/Alice/Work; p7=C:\\Users\\Alice_Twin\\Work";
            string output = DirectStreamArchiver.RemapAllPathFormats(input, src, dst);

            AssertTrue(!output.Contains("Alice\\Work") && !output.Contains("Alice/Work"), "All 6 path encodings remapped from Alice to Bob");
            AssertTrue(output.Contains("D:\\Users\\Bob\\Work"), "Windows backslash remapped");
            AssertTrue(output.Contains("D:/Users/Bob/Work"), "Forward slash remapped");
            AssertTrue(output.Contains("D:\\\\Users\\\\Bob\\\\Work"), "JSON escaped backslash remapped");
            AssertTrue(output.Contains("file:///d%3A/Users/Bob/Work"), "VSCode colon-encoded canonical lowercase-drive URI remapped");
            AssertTrue(output.Contains("C:\\Users\\Alice_Twin\\Work"), "Boundary-safe prefix avoids corrupting Alice_Twin");
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

        private static byte[] ReadTrajectoryMetadataBlobFromSqlite(string dbPath)
        {
            IntPtr h = IntPtr.Zero;
            IntPtr stmt = IntPtr.Zero;
            try
            {
                AssertEqual(0, t_sqlite3_open_v2(Utf8Z(dbPath), out h, 0x00000001, IntPtr.Zero), "Open SQLite DB readonly: " + dbPath);
                AssertEqual(0, t_sqlite3_prepare_v2(h, Utf8Z("SELECT data FROM trajectory_metadata_blob WHERE id='main';"), -1, out stmt, IntPtr.Zero), "Prepare SELECT trajectory_metadata_blob");
                AssertEqual(100, t_sqlite3_step(stmt), "SELECT returns SQLITE_ROW");
                int len = t_sqlite3_column_bytes(stmt, 0);
                IntPtr ptr = t_sqlite3_column_blob(stmt, 0);
                AssertTrue(len > 0 && ptr != IntPtr.Zero, "SQLite trajectory_metadata_blob is non-empty");
                byte[] buf = new byte[len];
                Marshal.Copy(ptr, buf, 0, len);
                return buf;
            }
            finally
            {
                if (stmt != IntPtr.Zero) try { t_sqlite3_finalize(stmt); } catch {}
                if (h != IntPtr.Zero) try { t_sqlite3_close(h); } catch {}
            }
        }

        private static void T12_F2_MoveSessionAndRelocateProjectPath()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_f2_mov_中文_" + Guid.NewGuid().ToString("N"));
            IntPtr dbHandle = IntPtr.Zero;
            IntPtr stmt = IntPtr.Zero;
            try
            {
                Directory.CreateDirectory(Path.Combine(sb, "antigravity", "conversations"));
                Directory.CreateDirectory(Path.Combine(sb, "config", "projects"));

                string cid = "87654321-4321-4321-4321-ba0987654321";
                ProtoNode entry = BuildRealProtoEntry(cid, "Adhoc Session", null, "file:///c:/Users/Administrator", "Administrator", "outside-of-project", 12, 3, 1);
                byte[] origBytes = ProtoCodec.Encode(new List<ProtoNode> { entry });
                string pbPath = Path.Combine(sb, "antigravity", "agyhub_summaries_proto.pb");
                File.WriteAllBytes(pbPath, origBytes);

                // 同时构造真实 SQLite 会话数据库（含中文根路径），验证 Move 与 Relocate 同步更新 .db (E-07)
                string dbPath = Path.Combine(sb, "antigravity", "conversations", cid + ".db");
                AssertEqual(0, t_sqlite3_open_v2(Utf8Z(dbPath), out dbHandle, 0x00000002 | 0x00000004, IntPtr.Zero), "T12 winsqlite3_open_v2 creates local .db under Chinese path");
                AssertEqual(0, t_sqlite3_exec(dbHandle, Utf8Z("CREATE TABLE trajectory_metadata_blob(id TEXT PRIMARY KEY, data BLOB);"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "T12 create trajectory_metadata_blob schema");
                AssertEqual(0, t_sqlite3_prepare_v2(dbHandle, Utf8Z("INSERT INTO trajectory_metadata_blob(id, data) VALUES('main', ?);"), -1, out stmt, IntPtr.Zero), "T12 prepare INSERT blob");
                GCHandle pin = GCHandle.Alloc(origBytes, GCHandleType.Pinned);
                try
                {
                    AssertEqual(0, t_sqlite3_bind_blob(stmt, 1, pin.AddrOfPinnedObject(), origBytes.Length, new IntPtr(-1)), "T12 bind protobuf blob");
                    AssertEqual(101, t_sqlite3_step(stmt), "T12 step INSERT SQLITE_DONE");
                }
                finally
                {
                    if (stmt != IntPtr.Zero) { t_sqlite3_finalize(stmt); stmt = IntPtr.Zero; }
                    if (pin.IsAllocated) pin.Free();
                }
                t_sqlite3_close(dbHandle);
                dbHandle = IntPtr.Zero;

                // 1. Move session from 'Administrator' to 'BMS_Controller'
                int moved = LocalWorkspaceManager.MoveSessionsToProject(
                    sb, new List<string> { cid }, "BMS_Controller", "proj-bms-99", "C:\\Work\\BMS_Controller"
                );
                AssertEqual(1, moved, "1 session moved");
                AssertEqual(0, LocalWorkspaceManager.LastFailedSqliteCids.Count, "No SQLite failure during MoveSessionsToProject");
                AssertTrue(File.Exists(Path.Combine(sb, "config", "projects", "proj-bms-99.json")), "Project JSON auto-created");

                List<ConversationSummaryInfo> listAfterMove = SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(pbPath));
                AssertEqual("BMS_Controller", listAfterMove[0].ProjectName, "ProjectName updated to BMS_Controller");
                AssertEqual("proj-bms-99", listAfterMove[0].ProjectId, "ProjectId updated to proj-bms-99");
                AssertEqual("file:///c%3A/Work/BMS_Controller", listAfterMove[0].FolderUri, "FolderUri updated to canonical VS Code lowercase-drive URI");

                List<ConversationSummaryInfo> dbAfterMove = SessionMetadataExtractor.ExtractSummariesFromProto(ReadTrajectoryMetadataBlobFromSqlite(dbPath));
                AssertEqual(1, dbAfterMove.Count, "SQLite blob after Move decodes cleanly");
                AssertEqual("proj-bms-99", dbAfterMove[0].ProjectId, "SQLite ProjectId updated to proj-bms-99");
                AssertEqual("file:///c%3A/Work/BMS_Controller", dbAfterMove[0].FolderUri, "SQLite FolderUri updated after Move");

                // 2. Relocate 'BMS_Controller' physical path to 'D:\NewDisk\BMS_V2'
                int relocated = LocalWorkspaceManager.RelocateProjectPath(
                    sb, "proj-bms-99", "BMS_Controller", "D:\\NewDisk\\BMS_V2", listAfterMove
                );
                AssertEqual(1, relocated, "1 session relocated");
                AssertEqual(0, LocalWorkspaceManager.LastFailedSqliteCids.Count, "No SQLite failure during RelocateProjectPath");

                List<ConversationSummaryInfo> listAfterReloc = SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(pbPath));
                AssertEqual("file:///d%3A/NewDisk/BMS_V2", listAfterReloc[0].FolderUri, "FolderUri relocated to canonical lowercase-drive URI in .pb");
                string projJson = File.ReadAllText(Path.Combine(sb, "config", "projects", "proj-bms-99.json"), Encoding.UTF8);
                AssertTrue(projJson.Contains("file:///d%3A/NewDisk/BMS_V2"), "Project JSON canonical folderUri updated");

                List<ConversationSummaryInfo> dbAfterReloc = SessionMetadataExtractor.ExtractSummariesFromProto(ReadTrajectoryMetadataBlobFromSqlite(dbPath));
                AssertEqual(1, dbAfterReloc.Count, "SQLite blob after Relocate decodes cleanly");
                AssertEqual("proj-bms-99", dbAfterReloc[0].ProjectId, "SQLite ProjectId preserved after Relocate");
                AssertEqual("file:///d%3A/NewDisk/BMS_V2", dbAfterReloc[0].FolderUri, "SQLite FolderUri updated to D:\\NewDisk\\BMS_V2");
            }
            finally
            {
                if (stmt != IntPtr.Zero) try { t_sqlite3_finalize(stmt); } catch {}
                if (dbHandle != IntPtr.Zero) try { t_sqlite3_close(dbHandle); } catch {}
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

        private static void T15_V07_CapabilityScanner_FiveCategoriesAndJsonKeyLexer()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_v07_scan_" + Guid.NewGuid().ToString("N"));
            string homeA = "C:\\Users\\Alice";
            try
            {
                string geminiA = Path.Combine(sb, ".gemini");
                Directory.CreateDirectory(geminiA);

                // 1. Root Rule: GEMINI.md + config/rules/coding-style.md
                File.WriteAllText(Path.Combine(geminiA, "GEMINI.md"), "# Global Rule\nAlways use UTF-8.", Encoding.UTF8);
                Directory.CreateDirectory(Path.Combine(geminiA, "config", "rules"));
                File.WriteAllText(Path.Combine(geminiA, "config", "rules", "coding-style.md"), "# Coding Style\nNo tabs.", Encoding.UTF8);

                // 2. Plugin: codegraph + config.json registration
                string pDir = Path.Combine(geminiA, "config", "plugins", "codegraph");
                Directory.CreateDirectory(Path.Combine(pDir, "rules"));
                File.WriteAllText(Path.Combine(pDir, "plugin.json"), "{\"name\":\"codegraph\",\"displayName\":\"CodeGraph Indexer\",\"description\":\"AST graph plugin\"}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(pDir, "rules", "AGENTS.md"), "# CodeGraph Rule", Encoding.UTF8);
                File.WriteAllText(Path.Combine(geminiA, "config", "config.json"), "{\"plugins\":{\"codegraph\":{\"enabled\":true,\"path\":\"C:/Users/Alice/.gemini/config/plugins/codegraph\"}}}", Encoding.UTF8);

                // 3. Standalone Skill: my-custom-skill
                string sDir = Path.Combine(geminiA, "config", "skills", "my-custom-skill");
                Directory.CreateDirectory(sDir);
                File.WriteAllText(Path.Combine(sDir, "SKILL.md"), "---\nname: my-custom-skill\ndescription: Custom verification skill\n---\n# Instructions", Encoding.UTF8);

                // 4. MCP Servers: 2 servers in mcp_config.json + schema dir for evo-kernel
                string mcpJson = "{\r\n" +
                    "  \"mcpServers\": {\r\n" +
                    "    \"evo-kernel\": {\r\n" +
                    "      \"command\": \"node\",\r\n" +
                    "      \"args\": [\"C:/Users/Alice/.gemini/evo-kernel/dist/index.js\"]\r\n" +
                    "    },\r\n" +
                    "    \"sqlite-explorer\": {\r\n" +
                    "      \"command\": \"npx\",\r\n" +
                    "      \"args\": [\"-y\", \"@modelcontextprotocol/server-sqlite\", \"-- note: { braces } and \\\"quotes\\\" inside string\"]\r\n" +
                    "    }\r\n" +
                    "  }\r\n" +
                    "}";
                File.WriteAllText(Path.Combine(geminiA, "config", "mcp_config.json"), mcpJson, Encoding.UTF8);
                string evoSchemaDir = Path.Combine(geminiA, "antigravity", "mcp", "evo-kernel");
                Directory.CreateDirectory(evoSchemaDir);
                File.WriteAllText(Path.Combine(evoSchemaDir, "evo_search.json"), "{\"name\":\"evo_search\"}", Encoding.UTF8);

                // 5. Hooks: 1 hook in hooks.json + companion script in config/
                string hookScriptPath = Path.Combine(geminiA, "config", "evo_antigravity_hook.mjs");
                File.WriteAllText(hookScriptPath, "console.log('hook running');", Encoding.UTF8);
                string hooksJson = "{\r\n" +
                    "  \"evo-evolution-reminder\": {\r\n" +
                    "    \"PreInvocation\": [\"node \\\"C:/Users/Alice/.gemini/config/evo_antigravity_hook.mjs\\\" PreInvocation\"]\r\n" +
                    "  }\r\n" +
                    "}";
                File.WriteAllText(Path.Combine(geminiA, "config", "hooks.json"), hooksJson, Encoding.UTF8);

                List<CapabilityItem> caps = CapabilityRepositoryManager.ScanLocalCapabilities(geminiA, homeA);
                AssertEqual(7, caps.Count, "Should scan 7 distinct capability items (1 plugin, 1 skill, 2 rules, 2 MCPs, 1 hook)");

                CapabilityItem mcpEvo = caps.Find(x => x.Kind == CapabilityKind.McpServer && x.Id == "evo-kernel");
                AssertTrue(mcpEvo != null, "evo-kernel MCP scanned");
                AssertTrue(mcpEvo.RawJsonFragment.Contains("{{HOME}}/.gemini/evo-kernel/dist/index.js"), "MCP path normalized to {{HOME}}");
                AssertTrue(mcpEvo.FilesMap.ContainsKey("schemas/evo_search.json"), "MCP schema bundled");

                CapabilityItem mcpSqlite = caps.Find(x => x.Kind == CapabilityKind.McpServer && x.Id == "sqlite-explorer");
                AssertTrue(mcpSqlite != null, "sqlite-explorer MCP with braces/quotes in string literal parsed cleanly");

                CapabilityItem hookEvo = caps.Find(x => x.Kind == CapabilityKind.Hook && x.Id == "evo-evolution-reminder");
                AssertTrue(hookEvo != null, "evo-evolution-reminder Hook scanned");
                AssertEqual(1, hookEvo.CompanionScriptNames.Count, "Companion script evo_antigravity_hook.mjs auto-bundled");
                AssertTrue(hookEvo.FilesMap.ContainsKey("scripts/evo_antigravity_hook.mjs"), "Hook script stored in FilesMap");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T16_V07_RepositoryPushAndSelectiveImport_KeyLevelMerge()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_v07_merge_" + Guid.NewGuid().ToString("N"));
            string homeA = "C:\\Users\\MachineA";
            string homeB = "D:\\Users\\MachineB";
            try
            {
                string geminiA = Path.Combine(sb, "MachineA_gemini");
                string repoDir = Path.Combine(sb, "Repository");
                string geminiB = Path.Combine(sb, "MachineB_gemini");
                Directory.CreateDirectory(Path.Combine(geminiA, "config"));
                Directory.CreateDirectory(Path.Combine(geminiB, "config"));

                // Machine A has MCP "evo-kernel" and Hook "evo-hook"
                File.WriteAllText(Path.Combine(geminiA, "config", "mcp_config.json"),
                    "{\"mcpServers\":{\"evo-kernel\":{\"command\":\"node\",\"args\":[\"C:/Users/MachineA/.gemini/evo/index.js\"]}}}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(geminiA, "config", "evo_hook.mjs"), "// hook script from MachineA", Encoding.UTF8);
                File.WriteAllText(Path.Combine(geminiA, "config", "hooks.json"),
                    "{\"evo-hook\":{\"PreInvocation\":[\"node \\\"C:/Users/MachineA/.gemini/config/evo_hook.mjs\\\"\"]}}", Encoding.UTF8);

                // Push Machine A's capabilities into plain-text Repository/
                List<CapabilityItem> capsA = CapabilityRepositoryManager.ScanLocalCapabilities(geminiA, homeA);
                AssertEqual(2, capsA.Count, "2 capabilities on Machine A");
                int pushed = CapabilityRepositoryManager.PushCapabilitiesToRepository(repoDir, homeA, capsA);
                AssertTrue(pushed >= 3, "Pushed MCP + Hook + script into Repository/");
                AssertTrue(File.Exists(Path.Combine(repoDir, "manifest.json")), "Repository manifest.json created");

                string repoMcpEntry = File.ReadAllText(Path.Combine(repoDir, "mcp", "evo-kernel", "server_entry.json"), Encoding.UTF8);
                AssertTrue(repoMcpEntry.Contains("{{HOME}}"), "Repository server_entry.json uses {{HOME}} placeholder");
                AssertTrue(!repoMcpEntry.Contains("MachineA"), "Repository server_entry.json stripped MachineA username");

                // Machine B ALREADY has its own MCP "existing-mcp" and its own Hook "existing-hook"
                File.WriteAllText(Path.Combine(geminiB, "config", "mcp_config.json"),
                    "{\"mcpServers\":{\"existing-mcp\":{\"command\":\"python\",\"args\":[\"server.py\"]}}}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(geminiB, "config", "hooks.json"),
                    "{\"existing-hook\":{\"PostInvocation\":[\"echo done\"]}}", Encoding.UTF8);

                // Scan Repository/ from Machine B's perspective and verify cross-machine signature stability
                List<CapabilityItem> repoCapsOnB = CapabilityRepositoryManager.ScanRepositoryCapabilities(repoDir, homeB, geminiB);
                AssertEqual(2, repoCapsOnB.Count, "2 capabilities scanned from Repository/");

                // Import both into Machine B
                int imported = CapabilityRepositoryManager.ImportCapabilitiesToLocal(geminiB, homeB, repoCapsOnB);
                AssertTrue(imported >= 3, "Imported MCP + Hook + companion script to Machine B");

                // Verify Machine B's mcp_config.json now has BOTH "existing-mcp" AND "evo-kernel" with D:/Users/MachineB path!
                string mergedMcpB = File.ReadAllText(Path.Combine(geminiB, "config", "mcp_config.json"), Encoding.UTF8);
                Dictionary<string, string> mcpEntriesB = JsonKeyManipulator.ExtractObjectEntries(mergedMcpB, "mcpServers");
                AssertEqual(2, mcpEntriesB.Count, "Key-level JSON merge preserved existing-mcp and added evo-kernel");
                AssertTrue(mcpEntriesB.ContainsKey("existing-mcp"), "existing-mcp untouched");
                AssertTrue(mcpEntriesB.ContainsKey("evo-kernel"), "evo-kernel merged in");
                AssertTrue(mcpEntriesB["evo-kernel"].Contains("D:/Users/MachineB/.gemini/evo/index.js"), "{{HOME}} expanded to D:/Users/MachineB");

                // Verify Machine B's hooks.json has BOTH "existing-hook" AND "evo-hook", plus companion script in config/
                string mergedHooksB = File.ReadAllText(Path.Combine(geminiB, "config", "hooks.json"), Encoding.UTF8);
                Dictionary<string, string> hookEntriesB = JsonKeyManipulator.ExtractObjectEntries(mergedHooksB, null);
                AssertEqual(2, hookEntriesB.Count, "Key-level JSON merge preserved existing-hook and added evo-hook");
                AssertTrue(File.Exists(Path.Combine(geminiB, "config", "evo_hook.mjs")), "Companion hook script unpacked to Machine B config/");

                // Verify ContentSignature is identical between Machine B Local and Repository
                List<CapabilityItem> localCapsB = CapabilityRepositoryManager.ScanLocalCapabilities(geminiB, homeB);
                List<CapabilityDiffNode> diffB = VisualDiffEngine.ComputeCapabilityDiff(localCapsB, repoCapsOnB);
                CapabilityDiffNode evoDiff = diffB.Find(x => x.RemoteItem != null && x.RemoteItem.Id == "evo-kernel");
                AssertTrue(evoDiff != null && evoDiff.Status == DiffStatus.Aligned, "Cross-machine ContentSignature matches (Aligned) after {{HOME}} expansion");

                // Delete single MCP "evo-kernel" from Machine B and verify "existing-mcp" remains
                CapabilityItem toDel = localCapsB.Find(x => x.Kind == CapabilityKind.McpServer && x.Id == "evo-kernel");
                CapabilityRepositoryManager.DeleteCapabilitiesFromLocal(geminiB, new List<CapabilityItem> { toDel });
                Dictionary<string, string> afterDelMcp = JsonKeyManipulator.ExtractObjectEntries(
                    File.ReadAllText(Path.Combine(geminiB, "config", "mcp_config.json"), Encoding.UTF8), "mcpServers");
                AssertEqual(1, afterDelMcp.Count, "Only 1 MCP remains after deleting evo-kernel");
                AssertTrue(afterDelMcp.ContainsKey("existing-mcp"), "Sibling existing-mcp still intact");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T17_V07_MissingPathDetectionAndRelocation()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_v07_reloc_" + Guid.NewGuid().ToString("N"));
            try
            {
                string geminiDir = Path.Combine(sb, ".gemini");
                Directory.CreateDirectory(Path.Combine(geminiDir, "config"));

                string missingScript = Path.Combine(sb, "NonExistentDir", "missing_server.js").Replace('\\', '/');
                string realScript = Path.Combine(sb, "RealServer", "active_server.js");
                Directory.CreateDirectory(Path.GetDirectoryName(realScript));
                File.WriteAllText(realScript, "// active mcp server", Encoding.UTF8);

                File.WriteAllText(Path.Combine(geminiDir, "config", "mcp_config.json"),
                    "{\"mcpServers\":{\"custom-mcp\":{\"command\":\"node\",\"args\":[\"" + missingScript + "\"]}}}", Encoding.UTF8);

                List<CapabilityItem> caps1 = CapabilityRepositoryManager.ScanLocalCapabilities(geminiDir, sb);
                AssertEqual(1, caps1.Count, "1 MCP scanned");
                AssertTrue(caps1[0].HasMissingPaths, "Missing script path detected");
                AssertEqual(1, caps1[0].MissingLocalPaths.Count, "1 missing path recorded");

                // Relocate missingScript -> realScript
                bool relocated = CapabilityRepositoryManager.RelocateCapabilityPath(
                    geminiDir, null, true, sb, caps1[0], caps1[0].MissingLocalPaths[0], realScript.Replace('\\', '/')
                );
                AssertTrue(relocated, "RelocateCapabilityPath succeeded");

                List<CapabilityItem> caps2 = CapabilityRepositoryManager.ScanLocalCapabilities(geminiDir, sb);
                AssertTrue(!caps2[0].HasMissingPaths, "No missing paths after relocation to real script");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T18_V07_SelectiveAgyArchive_CapabilitySliceRoundtrip()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_v07_zip_" + Guid.NewGuid().ToString("N"));
            string outZip = Path.Combine(Path.GetTempPath(), "agy_v07_caps_" + Guid.NewGuid().ToString("N") + ".agy");
            try
            {
                string geminiDir = Path.Combine(sb, ".gemini");
                Directory.CreateDirectory(Path.Combine(geminiDir, "config", "skills", "skill-alpha"));
                Directory.CreateDirectory(Path.Combine(geminiDir, "config", "skills", "skill-beta"));
                File.WriteAllText(Path.Combine(geminiDir, "config", "skills", "skill-alpha", "SKILL.md"), "# Skill Alpha", Encoding.UTF8);
                File.WriteAllText(Path.Combine(geminiDir, "config", "skills", "skill-beta", "SKILL.md"), "# Skill Beta", Encoding.UTF8);
                File.WriteAllText(Path.Combine(geminiDir, "config", "mcp_config.json"),
                    "{\"mcpServers\":{\"mcp-one\":{\"command\":\"node\"},\"mcp-two\":{\"command\":\"python\"}}}", Encoding.UTF8);

                List<CapabilityItem> allLocal = CapabilityRepositoryManager.ScanLocalCapabilities(geminiDir, sb);
                AssertEqual(4, allLocal.Count, "2 skills + 2 MCP servers on local");

                // Select ONLY skill-alpha and mcp-one (0 sessions!) to export into .agy
                List<CapabilityItem> selectedCaps = new List<CapabilityItem>
                {
                    allLocal.Find(x => x.Kind == CapabilityKind.Skill && x.Id == "skill-alpha"),
                    allLocal.Find(x => x.Kind == CapabilityKind.McpServer && x.Id == "mcp-one")
                };

                int packed = LocalWorkspaceManager.ExportSelectiveBackup(
                    geminiDir, sb, outZip,
                    new List<ConversationSummaryInfo>(),
                    new Dictionary<string, ProjectMetadata>(),
                    false,
                    selectedCaps
                );
                AssertTrue(packed >= 2, "Packed selective capabilities into .agy without sessions");

                BackupArchiveIndex idx = StreamingZipIndexReader.ReadIndex(outZip);
                AssertEqual(0, idx.Conversations.Count, "0 conversations in capability-only .agy");
                AssertEqual(2, idx.Capabilities.Count, "Exactly 2 selected capabilities in .agy");
                AssertTrue(idx.Capabilities.Exists(x => x.Kind == CapabilityKind.Skill && x.Id == "skill-alpha"), "skill-alpha present in .agy");
                AssertTrue(!idx.Capabilities.Exists(x => x.Kind == CapabilityKind.Skill && x.Id == "skill-beta"), "skill-beta excluded from .agy");
                AssertTrue(idx.Capabilities.Exists(x => x.Kind == CapabilityKind.McpServer && x.Id == "mcp-one"), "mcp-one present in .agy");
                AssertTrue(!idx.Capabilities.Exists(x => x.Kind == CapabilityKind.McpServer && x.Id == "mcp-two"), "mcp-two excluded from .agy");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
                if (File.Exists(outZip)) File.Delete(outZip);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // P0 + P1 Regression Tests (T19 ~ T25)
        // ══════════════════════════════════════════════════════════════════════

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, IntPtr zVfs);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_close(IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_exec", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_exec(IntPtr db, byte[] sql, IntPtr callback, IntPtr arg, IntPtr errmsg);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_prepare_v2(IntPtr db, byte[] zSql, int nByte, out IntPtr ppStmt, IntPtr pzTail);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_blob", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_bind_blob(IntPtr pStmt, int iCol, IntPtr zData, int nData, IntPtr xDel);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_step(IntPtr pStmt);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_blob", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr t_sqlite3_column_blob(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_bytes", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_column_bytes(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_finalize(IntPtr pStmt);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_text", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_bind_text(IntPtr pStmt, int iCol, IntPtr zData, int nByte, IntPtr xDel);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_int", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_bind_int(IntPtr pStmt, int iCol, int value);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr t_sqlite3_column_text(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_int", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_column_int(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_type", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_column_type(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_errmsg", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr t_sqlite3_errmsg(IntPtr db);

        private static byte[] Utf8Z(string s)
        {
            byte[] raw = Encoding.UTF8.GetBytes(s ?? string.Empty);
            byte[] res = new byte[raw.Length + 1];
            Buffer.BlockCopy(raw, 0, res, 0, raw.Length);
            return res;
        }

        private static void T19_ProtoAstCodec_StrictnessField15AndSharding()
        {
            List<ProtoNode> parsed;
            AssertTrue(!ProtoCodec.TryDecodeStrict(Encoding.UTF8.GetBytes("not a valid protobuf wire stream!"), out parsed),
                "TryDecodeStrict rejects invalid wire bytes");
            AssertTrue(!ProtoCodec.TryDecodeStrict(new byte[] { 0x0A, 0xFF, 0x01 }, out parsed),
                "TryDecodeStrict rejects truncated length-delimited field");

            string cid = "aaaa1111-2222-3333-4444-555566667777";
            ProtoNode validEntry = BuildRealProtoEntry(cid, "Auto Title", null, "file:///c%3A/Users/Old/Proj", "Proj", "proj-old", 14, 5, 1);
            byte[] encoded = ProtoCodec.Encode(new List<ProtoNode> { validEntry });
            AssertTrue(ProtoCodec.TryDecodeStrict(encoded, out parsed) && parsed.Count == 1,
                "TryDecodeStrict accepts well-formed proto entry");

            // 验证 Field 15 写入及 MergeShards 去重 + 跨机 URI 与工程重绑定 (C-01 / C-04)
            ConversationSummaryInfo backupInfo = SessionMetadataExtractor.ExtractSummariesFromProto(encoded)[0];
            backupInfo.CustomTitle = "【回归测试】用户自定义标题";
            backupInfo.DisplayTitle = backupInfo.CustomTitle;
            backupInfo.HasCustomTitle = true;

            byte[] duplicateLocal = ProtoCodec.Encode(new List<ProtoNode> { validEntry, validEntry });
            Dictionary<string, string> overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            overrides["Proj"] = "file:///d%3A/Work/NewProj";
            Dictionary<string, string> projIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            projIds["proj-old"] = "proj-new-99";
            projIds["Proj"] = "proj-new-99";

            byte[] merged = IncrementalProtoShardMerger.MergeShards(
                duplicateLocal,
                new List<ConversationSummaryInfo> { backupInfo },
                "C:\\Users\\Old",
                "D:\\Work",
                overrides,
                projIds);

            List<ConversationSummaryInfo> after = SessionMetadataExtractor.ExtractSummariesFromProto(merged);
            AssertEqual(1, after.Count, "Duplicate local proto entries deduplicated to 1 on merge");
            AssertEqual("【回归测试】用户自定义标题", after[0].CustomTitle, "Field 15 CustomTitle persisted on merge");
            AssertEqual("proj-new-99", after[0].ProjectId, "ProjectId rebound on merge");
            AssertEqual("file:///d%3A/Work/NewProj", after[0].FolderUri, "FolderUri overridden to target workspace");
        }

        private static void T20_RealWinSqlite3_WalAndChinesePathRemap()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_sql_wal_" + Guid.NewGuid().ToString("N"));
            IntPtr srcHandle = IntPtr.Zero;
            IntPtr verifyHandle = IntPtr.Zero;
            try
            {
                string srcDir = Path.Combine(sb, "源目录");
                string dstDir = Path.Combine(sb, "中文目标工作区", "子工程");
                Directory.CreateDirectory(srcDir);
                Directory.CreateDirectory(dstDir);

                string cid = "99998888-7777-6666-5555-444433332222";
                string srcDb = Path.Combine(srcDir, cid + ".db");
                string dstDb = Path.Combine(dstDir, cid + ".db");

                ProtoNode origEntry = BuildRealProtoEntry(cid, "Old DB Title", null, "file:///c%3A/Users/OldDev/MyApp", "MyApp", "p-src", 5, 2, 1);
                byte[] origBlob = ProtoCodec.Encode(new List<ProtoNode> { origEntry });

                int rc = t_sqlite3_open_v2(Utf8Z(srcDb), out srcHandle, 0x00000002 | 0x00000004, IntPtr.Zero); // READWRITE | CREATE
                AssertEqual(0, rc, "winsqlite3_open_v2 created source DB");

                AssertEqual(0, t_sqlite3_exec(srcHandle, Utf8Z("PRAGMA journal_mode=WAL;"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Enable WAL mode");
                AssertEqual(0, t_sqlite3_exec(srcHandle, Utf8Z("PRAGMA wal_autocheckpoint=0;"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Disable autocheckpoint");
                AssertEqual(0, t_sqlite3_exec(srcHandle, Utf8Z("CREATE TABLE trajectory_metadata_blob(id TEXT PRIMARY KEY, data BLOB);"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Create schema");
                AssertEqual(0, t_sqlite3_exec(srcHandle, Utf8Z("PRAGMA wal_checkpoint(TRUNCATE);"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Checkpoint empty table schema to main db");

                IntPtr stmt = IntPtr.Zero;
                AssertEqual(0, t_sqlite3_prepare_v2(srcHandle, Utf8Z("INSERT INTO trajectory_metadata_blob(id, data) VALUES('main', ?);"), -1, out stmt, IntPtr.Zero), "Prepare INSERT blob");
                GCHandle pin = GCHandle.Alloc(origBlob, GCHandleType.Pinned);
                try
                {
                    AssertEqual(0, t_sqlite3_bind_blob(stmt, 1, pin.AddrOfPinnedObject(), origBlob.Length, new IntPtr(-1)), "Bind protobuf blob");
                    AssertEqual(101, t_sqlite3_step(stmt), "Step INSERT SQLITE_DONE");
                }
                finally
                {
                    if (stmt != IntPtr.Zero) t_sqlite3_finalize(stmt);
                    if (pin.IsAllocated) pin.Free();
                }

                string srcWal = srcDb + "-wal";
                AssertTrue(File.Exists(srcWal) && new FileInfo(srcWal).Length > 32, "Uncheckpointed WAL contains committed frame");

                // 模拟备份与提取场景：仅复制 .db + .db-wal 至含中文路径，故意不复制 .db-shm
                using (FileStream fs = new FileStream(srcDb, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (FileStream os = new FileStream(dstDb, FileMode.Create, FileAccess.Write, FileShare.None))
                    fs.CopyTo(os);
                using (FileStream fs = new FileStream(srcWal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (FileStream os = new FileStream(dstDb + "-wal", FileMode.Create, FileAccess.Write, FileShare.None))
                    fs.CopyTo(os);

                t_sqlite3_close(srcHandle);
                srcHandle = IntPtr.Zero;

                Dictionary<string, string> uriOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                uriOverrides["MyApp"] = "file:///d%3A/Users/NewDev/MyAppRenamed";

                bool remapOk = SqliteMetadataUpdater.RemapSqliteDbMetadata(
                    dstDb, "C:\\Users\\OldDev", "D:\\Users\\NewDev", uriOverrides, "MyApp", "p-target", "WAL重映射标题");
                AssertTrue(remapOk, "RemapSqliteDbMetadata succeeds on WAL + Chinese UTF-8 path without -shm (" + SqliteMetadataUpdater.LastError + ")");

                AssertEqual(0, t_sqlite3_open_v2(Utf8Z(dstDb), out verifyHandle, 0x00000001, IntPtr.Zero), "Reopen remapped Chinese-path SQLite DB readonly");
                IntPtr selStmt = IntPtr.Zero;
                byte[] updatedBlob = null;
                try
                {
                    AssertEqual(0, t_sqlite3_prepare_v2(verifyHandle, Utf8Z("SELECT data FROM trajectory_metadata_blob WHERE id='main';"), -1, out selStmt, IntPtr.Zero), "Prepare SELECT verification");
                    AssertEqual(100, t_sqlite3_step(selStmt), "SELECT returns SQLITE_ROW (WAL replayed cleanly)");
                    int len = t_sqlite3_column_bytes(selStmt, 0);
                    IntPtr ptr = t_sqlite3_column_blob(selStmt, 0);
                    AssertTrue(len > 0 && ptr != IntPtr.Zero, "Verification blob is non-empty");
                    updatedBlob = new byte[len];
                    Marshal.Copy(ptr, updatedBlob, 0, len);
                }
                finally
                {
                    if (selStmt != IntPtr.Zero) t_sqlite3_finalize(selStmt);
                }

                List<ConversationSummaryInfo> parsedAfter = SessionMetadataExtractor.ExtractSummariesFromProto(updatedBlob);
                AssertEqual(1, parsedAfter.Count, "Remapped SQLite blob decodes cleanly as 1 summary");
                AssertEqual("p-target", parsedAfter[0].ProjectId, "SQLite ProjectId updated");
                AssertEqual("file:///d%3A/Users/NewDev/MyAppRenamed", parsedAfter[0].FolderUri, "SQLite FolderUri updated");
                AssertEqual("WAL重映射标题", parsedAfter[0].DisplayTitle, "SQLite title updated");

                // 失败路径显式返回 false 且附带错误信息
                AssertTrue(!SqliteMetadataUpdater.RemapSqliteDbMetadata(Path.Combine(sb, "missing.db"), "C:\\a", "D:\\b", null, "X"),
                    "Missing SQLite file returns false fail-closed");
                AssertTrue(!string.IsNullOrEmpty(SqliteMetadataUpdater.LastError), "LastError populated on missing SQLite file");
            }
            finally
            {
                if (srcHandle != IntPtr.Zero) try { t_sqlite3_close(srcHandle); } catch {}
                if (verifyHandle != IntPtr.Zero) try { t_sqlite3_close(verifyHandle); } catch {}
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T21_StreamingZipIndexReader_ZipSlipAndSelectiveClosure()
        {
            AssertTrue(!CapabilityRepositoryManager.IsSafeRelativePath("../../Windows/System32/evil.dll"), "Rejects parent traversal");
            AssertTrue(!CapabilityRepositoryManager.IsSafeRelativePath("C:\\abs\\file.txt"), "Rejects rooted Windows path");
            AssertTrue(!CapabilityRepositoryManager.IsSafeRelativePath("CON/file.txt"), "Rejects Windows reserved device name segment");
            AssertTrue(CapabilityRepositoryManager.IsSafeRelativePath("assets/icons/logo.png"), "Accepts normal nested relative path");

            bool zipSlipBlocked = false;
            try
            {
                CapabilityRepositoryManager.CombineAndEnsureUnderRoot("C:\\SafeRoot", "..\\escape.txt");
            }
            catch (InvalidOperationException)
            {
                zipSlipBlocked = true;
            }
            AssertTrue(zipSlipBlocked, "CombineAndEnsureUnderRoot throws on Zip-Slip traversal");

            // 同名多工程精确按 ProjectId 隔离删除 (C-10)
            string sb = Path.Combine(Path.GetTempPath(), "agy_multiproj_" + Guid.NewGuid().ToString("N"));
            try
            {
                string convDir = Path.Combine(sb, "antigravity", "conversations");
                string brainDir = Path.Combine(sb, "antigravity", "brain");
                string projDir = Path.Combine(sb, "config", "projects");
                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(brainDir);
                Directory.CreateDirectory(projDir);

                string cid1 = "11110000-1111-1111-1111-111111111111";
                string cid2 = "22220000-2222-2222-2222-222222222222";
                ProtoNode p1 = BuildRealProtoEntry(cid1, "SharedName A1", null, "file:///c%3A/DiskA/SharedApp", "SharedApp", "id-disk-a", 4, 1, 1);
                ProtoNode p2 = BuildRealProtoEntry(cid2, "SharedName B2", null, "file:///d%3A/DiskB/SharedApp", "SharedApp", "id-disk-b", 6, 2, 1);

                string pbPath = Path.Combine(sb, "antigravity", "agyhub_summaries_proto.pb");
                File.WriteAllBytes(pbPath, ProtoCodec.Encode(new List<ProtoNode> { p1, p2 }));
                File.WriteAllText(Path.Combine(projDir, "id-disk-a.json"), "{\"id\":\"id-disk-a\",\"name\":\"SharedApp\",\"folderUri\":\"file:///c%3A/DiskA/SharedApp\"}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(projDir, "id-disk-b.json"), "{\"id\":\"id-disk-b\",\"name\":\"SharedApp\",\"folderUri\":\"file:///d%3A/DiskB/SharedApp\"}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(convDir, cid1 + ".db"), "DB1", Encoding.UTF8);
                File.WriteAllText(Path.Combine(convDir, cid2 + ".db"), "DB2", Encoding.UTF8);

                Dictionary<string, ProjectMetadata> localProjects = new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
                localProjects["id-disk-a"] = new ProjectMetadata { Id = "id-disk-a", Name = "SharedApp", FolderUri = "file:///c%3A/DiskA/SharedApp" };
                localProjects["id-disk-b"] = new ProjectMetadata { Id = "id-disk-b", Name = "SharedApp", FolderUri = "file:///d%3A/DiskB/SharedApp" };

                List<ConversationSummaryInfo> allSums = SessionMetadataExtractor.ExtractSummaries(File.ReadAllBytes(pbPath), localProjects);
                DeletionResult delRes = LocalWorkspaceManager.DeleteSessionsAndProjects(
                    sb,
                    allSums,
                    localProjects,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    null,
                    new HashSet<string>(new string[] { "id-disk-a" }, StringComparer.OrdinalIgnoreCase),
                    null);

                AssertEqual(1, delRes.DeletedProjectJsonCount, "Only 1 matching project JSON deleted");
                AssertTrue(!File.Exists(Path.Combine(projDir, "id-disk-a.json")), "Targeted id-disk-a.json deleted");
                AssertTrue(File.Exists(Path.Combine(projDir, "id-disk-b.json")), "Same-name sibling id-disk-b.json preserved");
                AssertTrue(!File.Exists(Path.Combine(convDir, cid1 + ".db")), "cid1.db deleted");
                AssertTrue(File.Exists(Path.Combine(convDir, cid2 + ".db")), "Sibling SharedApp cid2.db preserved");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T22_CapabilityRepo_PlaceholdersJsoncEncodedIdAndHooks()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_cap_edge_" + Guid.NewGuid().ToString("N"));
            try
            {
                // 1. ExpandHomePlaceholder JSON 模式双反斜杠与文本模式原始反斜杠 (C-12)
                string homeWithSpaces = "C:\\Users\\John Doe";
                string jsonTpl = "{\"args\": [\"{{HOME}}\\\\scripts\\\\run.py\"]}";
                string expandedJson = CapabilityPathNormalizer.ExpandHomePlaceholder(jsonTpl, homeWithSpaces, true);
                AssertTrue(expandedJson.Contains("C:\\\\Users\\\\John Doe\\\\scripts\\\\run.py"), "JSON placeholder expansion keeps double backslashes valid");
                string parsedVal = JsonKeyManipulator.UpsertObjectEntry("{\"mcpServers\":{}}", "mcpServers", "s1", expandedJson);
                AssertTrue(parsedVal.Contains("John Doe"), "Upserted JSON remains parseable");

                string textExpanded = CapabilityPathNormalizer.ExpandHomePlaceholder("{{HOME}}\\bin\\tool.exe", homeWithSpaces, false);
                AssertEqual("C:\\Users\\John Doe\\bin\\tool.exe", textExpanded, "Plain text placeholder keeps single backslash");

                // 2. JSONC 注释、尾逗号与残缺 JSON 失败封闭保护 (C-13 / C-26)
                string jsonc = "{\n  // line comment\n  \"plugins\": {\n    \"ok\\u0040plugin\": { \"enabled\": true, /* block */ }\n  },\n}";
                Dictionary<string, string> plugMap = JsonKeyManipulator.ExtractObjectEntries(jsonc, "plugins");
                AssertTrue(plugMap.ContainsKey("ok@plugin"), "JSONC + unicode key unescaped cleanly");

                bool failClosed = false;
                try
                {
                    JsonKeyManipulator.UpsertObjectEntry("{ \"plugins\": { \"broken\": ", "plugins", "k", "{}");
                }
                catch (FormatException)
                {
                    failClosed = true;
                }
                AssertTrue(failClosed, "Corrupted JSON triggers fail-closed FormatException in UpsertObjectEntry");

                // 3. @scope/pkg 安全目录编码往返 (C-14) - 含带 ':' 与不带 ':' 的 @scope/pkg 全链路往返
                string scopedId = "@my-org/super-skill:v1";
                string encodedDir = CapabilityRepositoryManager.EncodeSafeCapabilityFolderName(scopedId);
                AssertTrue(CapabilityRepositoryManager.IsSafeRelativePath(encodedDir), "Encoded scoped folder name is safe relative path");
                AssertEqual(scopedId, CapabilityRepositoryManager.DecodeSafeCapabilityFolderName(encodedDir), "Scoped folder name roundtrips losslessly");

                string scopedSlashOnlyId = "@my-org/super-skill";
                string encodedSlashOnly = CapabilityRepositoryManager.EncodeSafeCapabilityFolderName(scopedSlashOnlyId);
                AssertTrue(encodedSlashOnly.StartsWith("_enc_", StringComparison.Ordinal) && !encodedSlashOnly.Contains("/"),
                    "@scope/pkg without colon encodes to single flat _enc_ folder");

                string repoRoot = Path.Combine(sb, "Repo");
                string capGemini = Path.Combine(sb, ".gemini_scoped");
                string capZip = Path.Combine(sb, "scoped_cap.agy");
                string repoSkillDir = Path.Combine(repoRoot, "skills", encodedSlashOnly);
                Directory.CreateDirectory(repoSkillDir);
                File.WriteAllText(Path.Combine(repoSkillDir, "SKILL.md"), "# Scoped Skill\nWorks across machines: {{HOME}}/bin", Encoding.UTF8);

                List<CapabilityItem> repoScanned = CapabilityRepositoryManager.ScanRepositoryCapabilities(repoRoot, sb, capGemini);
                CapabilityItem repoScoped = repoScanned.Find(x => x.Kind == CapabilityKind.Skill && x.Id == scopedSlashOnlyId);
                AssertTrue(repoScoped != null, "@scope/pkg (without colon) scanned back from Repository");

                CapabilityRepositoryManager.ImportCapabilitiesToLocal(capGemini, sb, new List<CapabilityItem> { repoScoped });
                string localSkillsRoot = Path.Combine(capGemini, "config", "skills");
                AssertTrue(!Directory.Exists(Path.Combine(localSkillsRoot, "@my-org")), "No 2-level nested @my-org/ directory created locally");
                AssertTrue(Directory.Exists(Path.Combine(localSkillsRoot, encodedSlashOnly)), "Single flat encoded folder created locally");

                List<CapabilityItem> localScanned = CapabilityRepositoryManager.ScanLocalCapabilities(capGemini, sb);
                CapabilityItem localScoped = localScanned.Find(x => x.Kind == CapabilityKind.Skill && x.Id == scopedSlashOnlyId);
                AssertTrue(localScoped != null, "@scope/pkg scanned back cleanly from local environment");

                List<CapabilityDiffNode> scopedDiff = VisualDiffEngine.ComputeCapabilityDiff(localScanned, repoScanned);
                CapabilityDiffNode sd = scopedDiff.Find(x => x.Id == scopedSlashOnlyId);
                AssertTrue(sd != null && sd.Status == DiffStatus.Aligned, "@scope/pkg is Aligned between local and repository");

                LocalWorkspaceManager.ExportSelectiveBackup(capGemini, sb, capZip, null, null, false, new List<CapabilityItem> { localScoped });
                BackupArchiveIndex capZipIndex = StreamingZipIndexReader.ReadIndex(capZip);
                CapabilityItem zipIndexScoped = capZipIndex.Capabilities.Find(x => x.Kind == CapabilityKind.Skill && x.Id == scopedSlashOnlyId);
                AssertTrue(zipIndexScoped != null, "@scope/pkg exported and indexed cleanly in .agy StreamingZipIndexReader");
                using (ZipArchive zCap = ZipFile.OpenRead(capZip))
                {
                    List<CapabilityItem> zipScanned = CapabilityRepositoryManager.ScanZipArchiveCapabilities(zCap, sb, sb, capGemini);
                    CapabilityItem zipScoped = zipScanned.Find(x => x.Kind == CapabilityKind.Skill && x.Id == scopedSlashOnlyId);
                    AssertTrue(zipScoped != null, "@scope/pkg decoded by ScanZipArchiveCapabilities");
                    AssertEqual(localScoped.ContentSignature, zipScoped.ContentSignature, "@scope/pkg ContentSignature preserved across .agy archive roundtrip");
                }

                int delScoped = CapabilityRepositoryManager.DeleteCapabilitiesFromLocal(capGemini, new List<CapabilityItem> { localScoped });
                AssertEqual(1, delScoped, "DeleteCapabilitiesFromLocal removes encoded @scope/pkg skill cleanly");
                AssertTrue(!Directory.Exists(Path.Combine(localSkillsRoot, encodedSlashOnly)), "Encoded @scope/pkg folder physically deleted");

                // 4. Hook 脚本引用计数删除 + 排除 node_modules / secrets (C-28 / E-01)
                string geminiDir = Path.Combine(sb, ".gemini");
                string cfgDir = Path.Combine(geminiDir, "config");
                Directory.CreateDirectory(cfgDir);
                string sharedPy = Path.Combine(cfgDir, "shared_guard.py");
                string uniquePy = Path.Combine(cfgDir, "only_h1.py");
                File.WriteAllText(sharedPy, "# shared", Encoding.UTF8);
                File.WriteAllText(uniquePy, "# unique", Encoding.UTF8);

                string hooksJson = string.Format(
                    "{{\r\n  \"h1\": {{ \"command\": \"python \\\"{0}\\\" && python \\\"{1}\\\"\" }},\r\n  \"h2\": {{ \"command\": \"python \\\"{0}\\\"\" }}\r\n}}",
                    JsonKeyManipulator.EscapeJsonString(sharedPy),
                    JsonKeyManipulator.EscapeJsonString(uniquePy));
                File.WriteAllText(Path.Combine(cfgDir, "hooks.json"), hooksJson, Encoding.UTF8);

                // 同时放置大文件 (>1MB) 技能和 node_modules/.env 排除项
                string skillDir = Path.Combine(cfgDir, "skills", "big-skill");
                Directory.CreateDirectory(Path.Combine(skillDir, "node_modules", "pkg"));
                File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "# Big Skill", Encoding.UTF8);
                File.WriteAllText(Path.Combine(skillDir, ".env.local"), "SECRET=123", Encoding.UTF8);
                File.WriteAllText(Path.Combine(skillDir, "node_modules", "pkg", "index.js"), "ignored", Encoding.UTF8);
                byte[] bigPayload = new byte[1200 * 1024];
                bigPayload[0] = (byte)'B';
                bigPayload[bigPayload.Length - 1] = (byte)'Z';
                File.WriteAllBytes(Path.Combine(skillDir, "weights.bin"), bigPayload);

                List<CapabilityItem> caps = CapabilityRepositoryManager.ScanLocalCapabilities(geminiDir, sb);
                CapabilityItem skill = caps.Find(x => x.Kind == CapabilityKind.Skill && x.Id == "big-skill");
                AssertTrue(skill != null, "Big skill scanned");
                AssertTrue(!skill.FilesMap.ContainsKey(".env.local") && !skill.SourceFilePaths.ContainsKey(".env.local"), ".env.local excluded");
                AssertTrue(!skill.FilesMap.ContainsKey("node_modules/pkg/index.js") && !skill.SourceFilePaths.ContainsKey("node_modules/pkg/index.js"), "node_modules excluded");
                AssertTrue(skill.SourceFilePaths.ContainsKey("weights.bin") && skill.FilesMap.ContainsKey("weights.bin") && skill.FilesMap["weights.bin"] == null, ">1MB file uses lazy SourceFilePaths with null FilesMap byte placeholder");
                AssertEqual(bigPayload.Length, skill.GetFileBytes("weights.bin").Length, "Lazy large file reads exact length via GetFileBytes");

                CapabilityItem h1 = caps.Find(x => x.Kind == CapabilityKind.Hook && x.Id == "h1");
                CapabilityItem h2 = caps.Find(x => x.Kind == CapabilityKind.Hook && x.Id == "h2");
                AssertTrue(h1 != null && h2 != null, "Both hooks discovered");

                CapabilityRepositoryManager.DeleteCapabilitiesFromLocal(geminiDir, new List<CapabilityItem> { h1 });
                AssertTrue(!File.Exists(uniquePy), "Unique script only_h1.py deleted with h1");
                AssertTrue(File.Exists(sharedPy), "Shared script shared_guard.py preserved because h2 still references it");

                CapabilityRepositoryManager.DeleteCapabilitiesFromLocal(geminiDir, new List<CapabilityItem> { h2 });
                AssertTrue(!File.Exists(sharedPy), "Shared script deleted once last referencing hook h2 is deleted");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T23_TranscriptMarkdownExporter_StreamingTagsAndFullJsonl()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_tx_stream_" + Guid.NewGuid().ToString("N"));
            try
            {
                string cid = "12121212-3434-5656-7878-909090909090";
                string brain = Path.Combine(sb, "antigravity", "brain", cid, ".system_generated", "logs");
                Directory.CreateDirectory(brain);

                File.WriteAllText(Path.Combine(brain, "transcript.jsonl"),
                    "{\"step_index\":1,\"type\":\"USER_INPUT\",\"content\":\"COMPACT_TRUNCATED\"}\n", Encoding.UTF8);

                string fullJsonl =
                    "{\"step_index\":1,\"type\":\"USER_INPUT\",\"created_at\":\"2026-11-01T10:00:00Z\",\"content\":\"<USER_REQUEST>\\nPlease parse <USER_REQUEST> safely inside text</USER_REQUEST>\\n</USER_REQUEST>\"}\n" +
                    "{\"step_index\":2,\"type\":\"PLANNER_RESPONSE\",\"created_at\":\"2026-11-01T10:00:05Z\",\"thinking\":\"Nested </details> and <details> inside thought with brace }\",\"content\":\"Done!\"}\n";
                File.WriteAllText(Path.Combine(brain, "transcript_full.jsonl"), fullJsonl, Encoding.UTF8);

                ConversationSummaryInfo s = new ConversationSummaryInfo
                {
                    ConversationId = cid,
                    DisplayTitle = "Streaming Tag Safety",
                    ProjectName = "TestProj"
                };

                string outMd = Path.Combine(sb, "out.md");
                string md = TranscriptMarkdownExporter.ExportLocalSessionToMarkdown(s, sb, outMd);

                AssertTrue(!md.Contains("COMPACT_TRUNCATED"), "Prefers transcript_full.jsonl over compact transcript.jsonl");
                AssertTrue(md.Contains("Please parse <USER_REQUEST> safely inside text"), "LastIndexOf closing tag preserves literal inner content");
                AssertTrue(md.Contains("&lt;/details&gt;") && md.Contains("&lt;details&gt;"), "Escapes <details> HTML tags inside thinking block");

                // 路径穿越 CID 防护 (C-22)
                bool blockedTraversal = false;
                try
                {
                    TranscriptMarkdownExporter.ExportLocalSessionToMarkdown(
                        new ConversationSummaryInfo { ConversationId = "../../Windows", DisplayTitle = "bad" },
                        sb,
                        Path.Combine(sb, "bad.md"));
                }
                catch (ArgumentException)
                {
                    blockedTraversal = true;
                }
                AssertTrue(blockedTraversal, "Blocks path-traversal ConversationId");

                // 有界 Markdown 预览截断验证 (Minor Rec #2)
                string hugePayload = new string('X', 2600);
                string hugeJsonl = "{\"step_index\":1,\"type\":\"USER_INPUT\",\"created_at\":\"2026-11-01T10:00:00Z\",\"content\":\"" + hugePayload + "\"}\n";
                File.WriteAllText(Path.Combine(brain, "transcript_full.jsonl"), hugeJsonl, Encoding.UTF8);
                bool isTruncated;
                string preview = TranscriptMarkdownExporter.PreviewLocalSessionMarkdown(s, sb, 1024, out isTruncated);
                AssertTrue(isTruncated, "PreviewLocalSessionMarkdown sets isTruncated when exceeding limit");
                AssertTrue(preview != null && preview.Length <= 1024, "PreviewLocalSessionMarkdown strictly bounds output <= 1024 chars");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T24_VisualDiffEngine_RenameCrossHostAndSameBasename()
        {
            string cidShared = "55556666-7777-8888-9999-000011112222";
            DateTime t0 = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

            List<ConversationSummaryInfo> local = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo
                {
                    ConversationId = cidShared,
                    ProjectId = "p-loc",
                    ProjectName = "Companion_V2",
                    FolderUri = "file:///d%3A/Users/Bob/Desktop/Repo",
                    DisplayTitle = "Aligned Session",
                    StepCount = 10,
                    UserInteractions = 3,
                    UpdatedTime = t0
                },
                new ConversationSummaryInfo
                {
                    ConversationId = "66667777-8888-9999-0000-111122223333",
                    ProjectId = "outside-of-project",
                    ProjectName = "Outside of Project",
                    FolderUri = "file:///d%3A/Users/Bob",
                    DisplayTitle = "Scratch Question",
                    UpdatedTime = t0
                }
            };

            List<ConversationSummaryInfo> backup = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo
                {
                    ConversationId = cidShared,
                    ProjectId = "p-bak",
                    ProjectName = "Companion_V1",
                    FolderUri = "file:///c%3A/Users/Alice/Desktop/Repo",
                    DisplayTitle = "Aligned Session",
                    StepCount = 10,
                    UserInteractions = 3,
                    UpdatedTime = t0
                },
                new ConversationSummaryInfo
                {
                    ConversationId = "77778888-9999-0000-1111-222233334444",
                    ProjectId = "p-bak",
                    ProjectName = "Companion_V1",
                    FolderUri = "file:///c%3A/Users/Alice/Desktop/Repo",
                    DisplayTitle = "New Session From Backup",
                    StepCount = 4,
                    UserInteractions = 1,
                    UpdatedTime = t0
                }
            };

            List<ProjectDiffNode> diffs = VisualDiffEngine.ComputeDiff(local, backup, "C:\\Users\\Alice", "D:\\Users\\Bob");
            ProjectDiffNode renamedNode = diffs.Find(x => x.BackupConversations.Count == 2);
            AssertTrue(renamedNode != null, "Renamed cross-machine project merged into a single ProjectDiffNode");
            AssertTrue(!string.IsNullOrEmpty(renamedNode.RenamedFromOrTo), "RenamedFromOrTo annotated on merged project");
            AssertEqual(DiffStatus.NewInBackup, renamedNode.Status, "Merged renamed project with 1 new session marked NewInBackup");
            AssertEqual(1, renamedNode.NewSessionCount, "NewSessionCount == 1");
            AssertTrue(renamedNode.GetStatusBadge().Contains("工程已改名"), "Badge clearly shows project rename + new session count");

            // 纯改名无新增会话场景应标注为 DiffStatus.Modified
            List<ConversationSummaryInfo> backupRenamedOnly = new List<ConversationSummaryInfo> { backup[0] };
            List<ProjectDiffNode> diffsModified = VisualDiffEngine.ComputeDiff(local, backupRenamedOnly, "C:\\Users\\Alice", "D:\\Users\\Bob");
            ProjectDiffNode pureRenamedNode = diffsModified.Find(x => string.Equals(x.ProjectName, "Companion_V1", StringComparison.OrdinalIgnoreCase));
            AssertTrue(pureRenamedNode != null, "Pure-renamed project matched");
            AssertEqual(DiffStatus.Modified, pureRenamedNode.Status, "Pure-renamed project with zero new sessions marked Modified");

            // 确认未重复产出独立的 LocalOnly 幽灵工程节点
            int localDupCount = 0;
            for (int i = 0; i < diffs.Count; i++)
            {
                if (string.Equals(diffs[i].ProjectName, "Companion_V2", StringComparison.OrdinalIgnoreCase) && diffs[i].Status == DiffStatus.LocalOnly)
                    localDupCount++;
            }
            AssertEqual(0, localDupCount, "No duplicate LocalOnly project node created after rename match");

            // 已对齐会话不应被父工程改名连带标红为 ProjectDiff
            ConversationDiffDetail evalAligned = VisualDiffEngine.EvaluateConversationDiff(backup[0], local[0], renamedNode);
            AssertTrue(!evalAligned.ProjectDiff && !evalAligned.IsModified, "Aligned session under merged renamed project is not falsely flagged Modified");
        }

        private static void T25_MainForm_PureHelpers_SelectionMoveAndProjectJson()
        {
            // 1. GenerateStandardProjectJson 安全转义 + 最小沙箱策略 (C-09 / C-30)
            string pj = MainForm.GenerateStandardProjectJson("id\"1", "App\\Name\"2", "C:\\Work\\中文项目");
            AssertTrue(pj.Contains("\"sandboxMode\": true"), "Fallback project JSON defaults to sandboxMode: true");
            AssertTrue(!pj.Contains("AGENT_SETTING_POLICY_ALLOW") && !pj.Contains("CASCADE_COMMANDS_AUTO_EXECUTION_EAGER") && !pj.Contains("ARTIFACT_REVIEW_MODE_TURBO"),
                "Fallback project JSON omits auto-allow/eager/turbo policies");
            Dictionary<string, string> pjFields = JsonKeyManipulator.ExtractObjectEntries(pj, null);
            AssertEqual("\"App\\\\Name\\\"2\"", pjFields["name"].Trim(), "Project name properly escaped in raw JSON token");
            AssertEqual("App\\Name\"2", TranscriptMarkdownExporter.ExtractJsonStringField(pj, "name"), "Project name roundtrips cleanly through JSON decoder");

            // 2. ResolveLocalActionTargets 右键单节点 vs 批量勾选语义与多工程 ID 隔离 (X-02 / C-10)
            ConversationSummaryInfo cChecked = new ConversationSummaryInfo { ConversationId = "c-checked", ProjectId = "pid-1", ProjectName = "App" };
            ConversationSummaryInfo cRightClicked = new ConversationSummaryInfo { ConversationId = "c-rclick", ProjectId = "pid-2", ProjectName = "App" };
            CapabilityItem capChecked = new CapabilityItem { Kind = CapabilityKind.Skill, Id = "sk-checked", DisplayName = "sk-checked" };

            MainForm.LocalSelectionSnapshot snap = new MainForm.LocalSelectionSnapshot();
            snap.Capabilities.Add(new MainForm.LocalCapEntry { Item = capChecked, Checked = true, Selected = false });
            snap.Projects.Add(new MainForm.LocalProjectEntry
            {
                BucketKey = "ID:pid-1", ProjectId = "pid-1", ProjectName = "App", Checked = true, Selected = false,
                Sessions = new List<MainForm.LocalSessionEntry>
                {
                    new MainForm.LocalSessionEntry { Summary = cChecked, Checked = true, Selected = false }
                }
            });
            snap.Projects.Add(new MainForm.LocalProjectEntry
            {
                BucketKey = "ID:pid-2", ProjectId = "pid-2", ProjectName = "App", Checked = false, Selected = true,
                Sessions = new List<MainForm.LocalSessionEntry>
                {
                    new MainForm.LocalSessionEntry { Summary = cRightClicked, Checked = false, Selected = false }
                }
            });

            MainForm.LocalActionTargets singleRightClickTargets = MainForm.ResolveLocalActionTargets(snap, true, false, new List<ConversationSummaryInfo> { cChecked, cRightClicked });
            AssertEqual(0, singleRightClickTargets.Capabilities.Count, "Right-click project ignores checked capability");
            AssertEqual(1, singleRightClickTargets.ProjectIds.Count, "Right-click targets ONLY selected pid-2 project");
            AssertTrue(singleRightClickTargets.ProjectIds.Contains("pid-2") && !singleRightClickTargets.ProjectIds.Contains("pid-1"), "Same-name pid-1 excluded on right-click pid-2");
            AssertEqual(1, singleRightClickTargets.ConversationIds.Count, "Only pid-2 sessions collected");
            AssertTrue(singleRightClickTargets.ConversationIds.Contains("c-rclick"), "c-rclick collected");

            MainForm.LocalActionTargets batchTargets = MainForm.ResolveLocalActionTargets(snap, false, false, new List<ConversationSummaryInfo> { cChecked, cRightClicked });
            AssertEqual(1, batchTargets.Capabilities.Count, "Batch mode reads checked capability");
            AssertTrue(batchTargets.ProjectIds.Contains("pid-1") && !batchTargets.ProjectIds.Contains("pid-2"), "Batch mode targets checked pid-1, ignoring right-clicked pid-2");

            // 3. ComputeMoveDialogProjectState (X-04)
            Dictionary<string, ProjectMetadata> nameMap = new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
            nameMap["ExistProj"] = new ProjectMetadata { Id = "p-ex", Name = "ExistProj", FolderUri = "file:///d%3A/Repo/Exist%20Proj" };

            MainForm.MoveDialogProjectState stExist = MainForm.ComputeMoveDialogProjectState("ExistProj", nameMap, "C:\\Users\\Me");
            AssertEqual("D:\\Repo\\Exist Proj", stExist.FolderDisplayPath, "Existing project FolderUri decoded to readable path");
            AssertTrue(stExist.FolderEnabled, "Existing project enables folder box");

            MainForm.MoveDialogProjectState stCustom = MainForm.ComputeMoveDialogProjectState("BrandNewProj", nameMap, "C:\\Users\\Me");
            AssertEqual("C:\\Users\\Me\\BrandNewProj", stCustom.FolderDisplayPath, "Typed new project computes home-based physical path");

            MainForm.MoveDialogProjectState stOutside = MainForm.ComputeMoveDialogProjectState("Outside of Project", nameMap, "C:\\Users\\Me");
            AssertTrue(stOutside.IsOutsideOfProject && !stOutside.FolderEnabled && stOutside.FolderDisplayPath == string.Empty,
                "Outside of Project disables folder box and clears path");

            // 4. ShouldAutoSelectBackupSession 防旧备份误覆盖 (X-03)
            DateTime baseTime = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Local);
            ConversationSummaryInfo locSess = new ConversationSummaryInfo
            {
                ConversationId = "sess-auto-1",
                DisplayTitle = "Old Title",
                ProjectName = "Proj",
                StepCount = 20,
                UserInteractions = 5,
                UpdatedTime = baseTime
            };
            AssertTrue(MainForm.ShouldAutoSelectBackupSession(locSess, null, null), "Missing local session is auto-selected");

            ConversationSummaryInfo olderFewerBackup = new ConversationSummaryInfo
            {
                ConversationId = "sess-auto-1",
                DisplayTitle = "Renamed Elsewhere",
                ProjectName = "Proj",
                StepCount = 12,
                UserInteractions = 5,
                UpdatedTime = baseTime.AddHours(1)
            };
            AssertTrue(!MainForm.ShouldAutoSelectBackupSession(olderFewerBackup, locSess, null),
                "Fewer-step backup is NOT auto-selected even with newer timestamp/title change");

            ConversationSummaryInfo equalButOlderBackup = new ConversationSummaryInfo
            {
                ConversationId = "sess-auto-1",
                DisplayTitle = "Old Title",
                ProjectName = "OtherProj",
                StepCount = 20,
                UserInteractions = 5,
                UpdatedTime = baseTime.AddSeconds(-10)
            };
            AssertTrue(!MainForm.ShouldAutoSelectBackupSession(equalButOlderBackup, locSess, null),
                "Equal-count older backup with project/title formatting diff is NOT auto-selected");

            ConversationSummaryInfo titleOnlyDiffSameTime = new ConversationSummaryInfo
            {
                ConversationId = "sess-auto-1",
                DisplayTitle = "Different Format Title",
                ProjectName = "Proj",
                StepCount = 20,
                UserInteractions = 5,
                UpdatedTime = baseTime.AddSeconds(1)
            };
            AssertTrue(!MainForm.ShouldAutoSelectBackupSession(titleOnlyDiffSameTime, locSess, null),
                "Equal-count within 2s jitter with only title diff is NOT auto-selected");

            ConversationSummaryInfo moreStepsBackup = new ConversationSummaryInfo
            {
                ConversationId = "sess-auto-1",
                DisplayTitle = "Old Title",
                ProjectName = "Proj",
                StepCount = 21,
                UserInteractions = 4,
                UpdatedTime = baseTime.AddMinutes(-5)
            };
            AssertTrue(MainForm.ShouldAutoSelectBackupSession(moreStepsBackup, locSess, null),
                "Strictly more steps in backup IS auto-selected");

            ConversationSummaryInfo equalCountsNewerTimeBackup = new ConversationSummaryInfo
            {
                ConversationId = "sess-auto-1",
                DisplayTitle = "Old Title",
                ProjectName = "Proj",
                StepCount = 20,
                UserInteractions = 5,
                UpdatedTime = baseTime.AddSeconds(5)
            };
            AssertTrue(MainForm.ShouldAutoSelectBackupSession(equalCountsNewerTimeBackup, locSess, null),
                "Equal counts with strictly newer timestamp (>2s) IS auto-selected");

            // 5. 同名不同路径双工程 ID / URI 隔离重映射 (C-05/C-10/C-25)
            string cidA = "aaaaaaaa-1111-1111-1111-111111111111";
            string cidB = "bbbbbbbb-2222-2222-2222-222222222222";
            ProtoNode nAppA = BuildRealProtoEntry(cidA, "App A Session", null, "file:///c%3A/Users/Alice/WorkA/App", "App", "pid-bak-a", 3, 1, 1);
            ProtoNode nAppB = BuildRealProtoEntry(cidB, "App B Session", null, "file:///c%3A/Users/Alice/WorkB/App", "App", "pid-bak-b", 4, 1, 1);
            byte[] pbTwoApps = ProtoCodec.Encode(new List<ProtoNode> { nAppA, nAppB });
            List<ConversationSummaryInfo> twoAppSums = SessionMetadataExtractor.ExtractSummariesFromProto(pbTwoApps);

            Dictionary<string, string> idOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            idOverrides["ID:pid-bak-a"] = "target-id-a";
            idOverrides["ID:pid-bak-b"] = "target-id-b";

            Dictionary<string, string> uriOverridesById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            uriOverridesById["ID:pid-bak-a"] = "file:///d%3A/Users/Bob/WorkA/App";
            uriOverridesById["ID:target-id-a"] = "file:///d%3A/Users/Bob/WorkA/App";
            uriOverridesById["ID:pid-bak-b"] = "file:///d%3A/Users/Bob/WorkB/App";
            uriOverridesById["ID:target-id-b"] = "file:///d%3A/Users/Bob/WorkB/App";

            byte[] mergedTwoApps = IncrementalProtoShardMerger.MergeShards(
                new byte[0], twoAppSums, "C:\\Users\\Alice", "D:\\Users\\Bob", uriOverridesById, idOverrides);
            List<ConversationSummaryInfo> mergedSums = SessionMetadataExtractor.ExtractSummariesFromProto(mergedTwoApps);
            ConversationSummaryInfo resA = mergedSums.Find(x => x.ConversationId == cidA);
            ConversationSummaryInfo resB = mergedSums.Find(x => x.ConversationId == cidB);
            AssertTrue(resA != null && resB != null, "Both same-basename projects merged cleanly");
            AssertEqual("target-id-a", resA.ProjectId, "Same-basename Project A keeps target-id-a");
            AssertEqual("target-id-b", resB.ProjectId, "Same-basename Project B keeps target-id-b");
            AssertEqual("file:///d%3A/Users/Bob/WorkA/App", resA.FolderUri, "Same-basename Project A keeps WorkA URI");
            AssertEqual("file:///d%3A/Users/Bob/WorkB/App", resB.FolderUri, "Same-basename Project B keeps WorkB URI");
        }

        // ══════════════════════════════════════════════════════════════════════
        // P2 + P3 Regression Tests (T26 ~ T31)
        // ══════════════════════════════════════════════════════════════════════

        private static void T26_SessionMetadataExtractor_NanoEpochFixed64AndOutsideUri()
        {
            long nanoEpoch = 1762776000000000000L; // 2025-11-10T12:00:00Z
            string cid = "33334444-5555-6666-7777-888899990000";

            ProtoNode f1 = new ProtoNode(1, 2);
            f1.SetString(cid);

            ProtoNode meta = new ProtoNode(2, 2);
            ProtoNode mTitle = new ProtoNode(1, 2);
            mTitle.SetString("Nano Fixed64 Session");
            meta.Children.Add(mTitle);

            ProtoNode mFixedTime = new ProtoNode(3, 1);
            mFixedTime.RawBytes = BitConverter.GetBytes(nanoEpoch);
            meta.Children.Add(mFixedTime);

            ProtoNode mCreated = new ProtoNode(7, 2);
            mCreated.Children.Add(new ProtoNode(1, 0) { VarintValue = (ulong)nanoEpoch });
            meta.Children.Add(mCreated);

            ProtoNode mWs = new ProtoNode(9, 2);
            ProtoNode mWsUri = new ProtoNode(1, 2);
            mWsUri.SetString("file:///d%3A/Temp/ShouldBeCleared");
            mWs.Children.Add(mWsUri);
            meta.Children.Add(mWs);

            ProtoNode mCasc = new ProtoNode(17, 2);
            ProtoNode cName = new ProtoNode(1, 2);
            cName.SetString("WrongProjectName");
            ProtoNode cUri = new ProtoNode(7, 2);
            cUri.SetString("file:///d%3A/Temp/ShouldBeCleared");
            ProtoNode cPid = new ProtoNode(18, 2);
            cPid.SetString("outside-of-project");
            mCasc.Children.Add(cName);
            mCasc.Children.Add(cUri);
            mCasc.Children.Add(cPid);
            meta.Children.Add(mCasc);

            ProtoNode entry = new ProtoNode(1, 2);
            entry.Children.Add(f1);
            entry.Children.Add(meta);

            byte[] pbBytes = ProtoCodec.Encode(new List<ProtoNode> { entry });
            List<ConversationSummaryInfo> parsed = SessionMetadataExtractor.ExtractSummariesFromProto(pbBytes);
            AssertEqual(1, parsed.Count, "Parsed 1 entry with Fixed64 + nanosecond timestamp");

            DateTime expectedUtc = new DateTime(2025, 11, 10, 12, 0, 0, DateTimeKind.Utc);
            AssertEqual(expectedUtc, parsed[0].UpdatedTime.ToUniversalTime(), "Fixed64 nanosecond timestamp parsed to exact UTC time");
            AssertEqual(expectedUtc, parsed[0].CreatedTime.ToUniversalTime(), "Nested Varint nanosecond timestamp parsed to exact UTC time");
            AssertEqual("outside-of-project", parsed[0].ProjectId, "outside-of-project ProjectId preserved");
            AssertEqual("Outside of Project", parsed[0].ProjectName, "outside-of-project ProjectName normalized");
            AssertTrue(string.IsNullOrEmpty(parsed[0].FolderUri), "Stale FolderUri cleared for outside-of-project session");

            AssertEqual("Drive_C", SessionMetadataExtractor.ExtractProjectNameFromUri("file:///c:/"), "Drive root c:/ becomes Drive_C");
            AssertEqual("Drive_D", SessionMetadataExtractor.ExtractProjectNameFromUri("file:///d%3A"), "Encoded drive root d%3A becomes Drive_D");
            AssertEqual("MyApp", SessionMetadataExtractor.ExtractProjectNameFromUri("file:///e%3A/Work/MyApp/"), "Trailing-slash workspace URI extracts folder basename");
        }

        private static void T27_VisualDiffEngine_CapMissingPathsLocalOnlyCountAndSig()
        {
            // 1. ContentSignature fallback when callers don't precompute signatures + missing path badges
            CapabilityItem localRule = new CapabilityItem { Kind = CapabilityKind.Rule, Id = "r1", DisplayName = "r1", ContentSignature = null };
            localRule.FilesMap["_root/GEMINI.md"] = Encoding.UTF8.GetBytes("# Shared Rule\n");
            CapabilityItem remoteSame = new CapabilityItem { Kind = CapabilityKind.Rule, Id = "r1", DisplayName = "r1", ContentSignature = null };
            remoteSame.FilesMap["_root/GEMINI.md"] = Encoding.UTF8.GetBytes("# Shared Rule\n");

            List<CapabilityDiffNode> alignedDiff = VisualDiffEngine.ComputeCapabilityDiff(
                new List<CapabilityItem> { localRule },
                new List<CapabilityItem> { remoteSame });
            AssertEqual(1, alignedDiff.Count, "1 merged capability diff node");
            AssertEqual(DiffStatus.Aligned, alignedDiff[0].Status, "Missing ContentSignature computes automatically and matches Aligned");
            AssertEqual("[已对齐]", alignedDiff[0].GetStatusBadge(), "Aligned capability without missing paths has clean badge");

            remoteSame.FilesMap["_root/GEMINI.md"] = Encoding.UTF8.GetBytes("# Modified Rule\n");
            remoteSame.ContentSignature = null;
            remoteSame.MissingLocalPaths.Add("C:\\missing\\remote.py");
            List<CapabilityDiffNode> modRemoteMiss = VisualDiffEngine.ComputeCapabilityDiff(
                new List<CapabilityItem> { localRule },
                new List<CapabilityItem> { remoteSame });
            AssertEqual(DiffStatus.Modified, modRemoteMiss[0].Status, "Changed content with null signatures detects Modified");
            AssertTrue(modRemoteMiss[0].GetStatusBadge().Contains("路径缺失"), "Remote missing path annotated in badge");

            localRule.MissingLocalPaths.Add("C:\\missing\\local.py");
            AssertTrue(modRemoteMiss[0].GetStatusBadge().Contains("双侧路径缺失"), "Both missing paths annotated in badge");
            modRemoteMiss[0].RemoteItem.MissingLocalPaths.Clear();
            AssertTrue(modRemoteMiss[0].GetStatusBadge().Contains("本机路径缺失"), "Local-only missing path annotated in badge");

            // 2. ProjectDiffNode.LocalOnlySessionCount & single-side MinValue time guard
            DateTime t0 = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
            ConversationSummaryInfo cLocalShared = new ConversationSummaryInfo
            {
                ConversationId = "11112222-3333-4444-5555-666677778888",
                ProjectId = "p-loc",
                ProjectName = "RepoProj",
                FolderUri = "file:///d%3A/Work/RepoProj",
                DisplayTitle = "Shared",
                StepCount = 5,
                UpdatedTime = t0
            };
            ConversationSummaryInfo cLocalExtra = new ConversationSummaryInfo
            {
                ConversationId = "22223333-4444-5555-6666-777788889999",
                ProjectId = "p-loc",
                ProjectName = "RepoProj",
                FolderUri = "file:///d%3A/Work/RepoProj",
                DisplayTitle = "Local Only Session",
                StepCount = 2,
                UpdatedTime = t0
            };
            ConversationSummaryInfo cBackupAligned = new ConversationSummaryInfo
            {
                ConversationId = "11112222-3333-4444-5555-666677778888",
                ProjectId = "p-bak",
                ProjectName = "RepoProj",
                FolderUri = "file:///c%3A/Work/RepoProj",
                DisplayTitle = "Shared",
                StepCount = 5,
                UpdatedTime = t0
            };

            List<ProjectDiffNode> pDiffs = VisualDiffEngine.ComputeDiff(
                new List<ConversationSummaryInfo> { cLocalShared, cLocalExtra },
                new List<ConversationSummaryInfo> { cBackupAligned },
                "C:\\Work",
                "D:\\Work");
            AssertEqual(1, pDiffs.Count, "RepoProj merged across machines");
            AssertEqual(DiffStatus.Aligned, pDiffs[0].Status, "Aligned project with local-only extra session keeps Aligned status");
            AssertEqual(1, pDiffs[0].LocalOnlySessionCount, "LocalOnlySessionCount tracks 1 local-only session under Aligned project");
            AssertTrue(pDiffs[0].GetStatusBadge().Contains("本机另含 1 条独有"), "Badge displays local-only session count");

            ConversationSummaryInfo cBackupMinTime = new ConversationSummaryInfo
            {
                ConversationId = "11112222-3333-4444-5555-666677778888",
                ProjectId = "p-bak",
                ProjectName = "RepoProj",
                DisplayTitle = "Shared",
                StepCount = 5,
                UpdatedTime = DateTime.MinValue
            };
            ConversationDiffDetail evalSingleMinTime = VisualDiffEngine.EvaluateConversationDiff(cBackupMinTime, cLocalShared, pDiffs[0]);
            AssertTrue(evalSingleMinTime.TimeDiff && evalSingleMinTime.IsModified, "Single-side DateTime.MinValue flags TimeDiff/Modified");

            ConversationSummaryInfo cLocalMinTime = new ConversationSummaryInfo
            {
                ConversationId = "11112222-3333-4444-5555-666677778888",
                ProjectId = "p-loc",
                ProjectName = "RepoProj",
                DisplayTitle = "Shared",
                StepCount = 5,
                UpdatedTime = DateTime.MinValue
            };
            ConversationDiffDetail evalBothMinTime = VisualDiffEngine.EvaluateConversationDiff(cBackupMinTime, cLocalMinTime, pDiffs[0]);
            AssertTrue(!evalBothMinTime.TimeDiff && !evalBothMinTime.IsModified, "Both-side DateTime.MinValue does not flag TimeDiff/Modified");
        }

        private static void T28_CapabilityRepo_EncodingBomMultilineJsonAndYaml()
        {
            // 1. Multiline JSON entry reindentation in FormatObjectEntries
            string baseJson = "{\r\n  \"mcpServers\": {}\r\n}";
            string multiFrag = "{\r\n  \"command\": \"node\",\r\n  \"args\": [\r\n    \"a.js\",\r\n    \"b.js\"\r\n  ]\r\n}";
            string mergedJson = JsonKeyManipulator.UpsertObjectEntry(baseJson, "mcpServers", "multi-srv", multiFrag);
            AssertTrue(mergedJson.Contains("      \"args\": ["), "Nested multiline array reindented to 6 spaces");
            Dictionary<string, string> parsedMcp = JsonKeyManipulator.ExtractObjectEntries(mergedJson, "mcpServers");
            AssertTrue(parsedMcp.ContainsKey("multi-srv") && parsedMcp["multi-srv"].Contains("\"b.js\""), "Multiline JSON entry roundtrips through ExtractObjectEntries");

            // 2. UTF-8 BOM + Raw GBK byte preservation + YAML block/folded/quoted scalars + zero leftover .tmp files
            string sb = Path.Combine(Path.GetTempPath(), "agy_cap_enc_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeSrc = Path.Combine(sb, "srcHome");
                string geminiSrc = Path.Combine(homeSrc, ".gemini");
                string rulesDirSrc = Path.Combine(geminiSrc, "config", "rules");
                string skillsDirSrc = Path.Combine(geminiSrc, "config", "skills");
                Directory.CreateDirectory(rulesDirSrc);
                Directory.CreateDirectory(skillsDirSrc);

                // Root rule with UTF-8 BOM and physical home reference
                byte[] utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF };
                byte[] rootRuleBody = Encoding.UTF8.GetBytes("# 全局规则\r\ntools=" + homeSrc.Replace('\\', '/') + "/tools\r\n");
                byte[] rootRuleBytes = new byte[utf8Bom.Length + rootRuleBody.Length];
                Buffer.BlockCopy(utf8Bom, 0, rootRuleBytes, 0, utf8Bom.Length);
                Buffer.BlockCopy(rootRuleBody, 0, rootRuleBytes, utf8Bom.Length, rootRuleBody.Length);
                File.WriteAllBytes(Path.Combine(geminiSrc, "GEMINI.md"), rootRuleBytes);

                // Legacy invalid-UTF8 GBK bytes ("中文\n" in CP936: D6 D0 CE C4 0A)
                byte[] gbkBytes = new byte[] { 0xD6, 0xD0, 0xCE, 0xC4, 0x0A };
                File.WriteAllBytes(Path.Combine(rulesDirSrc, "legacy-gbk.md"), gbkBytes);

                // Skill 1: BOM + YAML literal block scalar |-
                string sBlockDir = Path.Combine(skillsDirSrc, "yaml-block");
                Directory.CreateDirectory(sBlockDir);
                byte[] sBlockBody = Encoding.UTF8.GetBytes("---\nname: yaml-block\ndescription: |-\n  Line one\n  Line two\n---\n# Body\n");
                byte[] sBlockBytes = new byte[utf8Bom.Length + sBlockBody.Length];
                Buffer.BlockCopy(utf8Bom, 0, sBlockBytes, 0, utf8Bom.Length);
                Buffer.BlockCopy(sBlockBody, 0, sBlockBytes, utf8Bom.Length, sBlockBody.Length);
                File.WriteAllBytes(Path.Combine(sBlockDir, "SKILL.md"), sBlockBytes);

                // Skill 2: YAML folded scalar >
                string sFoldedDir = Path.Combine(skillsDirSrc, "yaml-folded");
                Directory.CreateDirectory(sFoldedDir);
                File.WriteAllText(Path.Combine(sFoldedDir, "SKILL.md"), "---\ndescription: >\n  Folded alpha\n  Folded beta\n---\n", new UTF8Encoding(false));

                // Skill 3: YAML single-quoted scalar
                string sQuotedDir = Path.Combine(skillsDirSrc, "yaml-quoted");
                Directory.CreateDirectory(sQuotedDir);
                File.WriteAllText(Path.Combine(sQuotedDir, "SKILL.md"), "---\ndescription: 'Single quoted desc'\n---\n", new UTF8Encoding(false));

                List<CapabilityItem> localCaps = CapabilityRepositoryManager.ScanLocalCapabilities(geminiSrc, homeSrc);
                CapabilityItem blockCap = localCaps.Find(x => x.Kind == CapabilityKind.Skill && x.Id == "yaml-block");
                CapabilityItem foldedCap = localCaps.Find(x => x.Kind == CapabilityKind.Skill && x.Id == "yaml-folded");
                CapabilityItem quotedCap = localCaps.Find(x => x.Kind == CapabilityKind.Skill && x.Id == "yaml-quoted");
                CapabilityItem gbkRuleCap = localCaps.Find(x => x.Kind == CapabilityKind.Rule && x.Id == "rules/legacy-gbk.md");
                CapabilityItem bomRootRuleCap = localCaps.Find(x => x.Kind == CapabilityKind.Rule && x.Id == "GEMINI.md");

                AssertEqual("Line one Line two", blockCap.Description, "YAML literal |- scalar description parsed after stripping BOM");
                AssertEqual("Folded alpha Folded beta", foldedCap.Description, "YAML folded > scalar description parsed");
                AssertEqual("Single quoted desc", quotedCap.Description, "YAML single-quoted scalar description unquoted");
                AssertTrue(gbkRuleCap != null && gbkRuleCap.FilesMap.ContainsKey("config/legacy-gbk.md"), "GBK rule scanned");
                byte[] scannedGbk = gbkRuleCap.FilesMap["config/legacy-gbk.md"];
                AssertEqual(gbkBytes.Length, scannedGbk.Length, "Raw GBK byte length preserved on scan");
                for (int i = 0; i < gbkBytes.Length; i++) AssertEqual((int)gbkBytes[i], (int)scannedGbk[i], "GBK byte[" + i + "] preserved verbatim");

                byte[] scannedBomRule = bomRootRuleCap.FilesMap["_root/GEMINI.md"];
                AssertTrue(scannedBomRule.Length >= 3 && scannedBomRule[0] == 0xEF && scannedBomRule[1] == 0xBB && scannedBomRule[2] == 0xBF, "UTF-8 BOM preserved when normalizing root rule");

                string repoDir = Path.Combine(sb, "Repo");
                int pushed = CapabilityRepositoryManager.PushCapabilitiesToRepository(repoDir, homeSrc, localCaps);
                AssertTrue(pushed >= 5, "Pushed rules and YAML skills to Repository");

                string homeDst = Path.Combine(sb, "dstHome");
                string geminiDst = Path.Combine(homeDst, ".gemini");
                List<CapabilityItem> repoCaps = CapabilityRepositoryManager.ScanRepositoryCapabilities(repoDir, homeDst, geminiDst);
                int imported = CapabilityRepositoryManager.ImportCapabilitiesToLocal(geminiDst, homeDst, repoCaps);
                AssertTrue(imported >= 5, "Imported capabilities cleanly to destination home");

                byte[] dstRootBytes = File.ReadAllBytes(Path.Combine(geminiDst, "GEMINI.md"));
                AssertTrue(dstRootBytes.Length >= 3 && dstRootBytes[0] == 0xEF && dstRootBytes[1] == 0xBB && dstRootBytes[2] == 0xBF, "Imported GEMINI.md preserves UTF-8 BOM");
                string dstRootText = Encoding.UTF8.GetString(dstRootBytes, 3, dstRootBytes.Length - 3);
                AssertTrue(dstRootText.Contains(homeDst.Replace('\\', '/') + "/tools"), "Imported GEMINI.md expanded {{HOME}} cleanly");

                byte[] dstGbkBytes = File.ReadAllBytes(Path.Combine(geminiDst, "config", "rules", "legacy-gbk.md"));
                AssertEqual(gbkBytes.Length, dstGbkBytes.Length, "Imported GBK rule preserves exact byte count");
                for (int i = 0; i < gbkBytes.Length; i++) AssertEqual((int)gbkBytes[i], (int)dstGbkBytes[i], "Imported GBK byte[" + i + "] preserved without U+FFFD");

                AssertEqual(0, Directory.GetFiles(repoDir, "*.tmp.*", SearchOption.AllDirectories).Length, "Zero stale .tmp files left in Repo");
                AssertEqual(0, Directory.GetFiles(geminiDst, "*.tmp.*", SearchOption.AllDirectories).Length, "Zero stale .tmp files left in Destination");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T29_MainForm_FilteredSetsAndHiddenDeletionGuard()
        {
            ConversationSummaryInfo cVis = new ConversationSummaryInfo
            {
                ConversationId = "c-vis-001",
                ProjectId = "proj-alpha",
                ProjectName = "Alpha"
            };
            ConversationSummaryInfo cHid = new ConversationSummaryInfo
            {
                ConversationId = "c-hid-002",
                ProjectId = "proj-alpha",
                ProjectName = "Alpha"
            };
            List<ConversationSummaryInfo> allLocalSummaries = new List<ConversationSummaryInfo> { cVis, cHid };

            CapabilityItem capVis = new CapabilityItem { Kind = CapabilityKind.Skill, Id = "vis-sk", DisplayName = "vis-sk" };
            CapabilityItem capHid = new CapabilityItem { Kind = CapabilityKind.Skill, Id = "hid-sk", DisplayName = "hid-sk" };

            // Search filter hides c-hid-002 while Alpha appears checked visibly with only 1 child
            MainForm.LocalSelectionSnapshot snapFiltered = new MainForm.LocalSelectionSnapshot();
            snapFiltered.Capabilities.Add(new MainForm.LocalCapEntry { Item = capVis, Checked = true });
            snapFiltered.Projects.Add(new MainForm.LocalProjectEntry
            {
                BucketKey = "ID:proj-alpha",
                ProjectId = "proj-alpha",
                ProjectName = "Alpha",
                Checked = true,
                Sessions = new List<MainForm.LocalSessionEntry>
                {
                    new MainForm.LocalSessionEntry { Summary = cVis, Checked = true }
                }
            });

            MainForm.LocalActionTargets filteredTargets = MainForm.ResolveLocalActionTargets(snapFiltered, false, false, allLocalSummaries);
            AssertEqual(1, filteredTargets.Capabilities.Count, "Visible capability collected");
            AssertEqual(1, filteredTargets.ConversationIds.Count, "Filtered partial selection collects visible c-vis-001");
            AssertEqual(0, filteredTargets.ProjectIds.Count, "Refuses to delete whole proj-alpha while c-hid-002 is hidden by search filter");

            // When all sessions in bucket are visible & checked, whole project can be removed
            MainForm.LocalActionTargets unfilteredTargets = MainForm.ResolveLocalActionTargets(snapFiltered, false, false, new List<ConversationSummaryInfo> { cVis });
            AssertTrue(unfilteredTargets.ProjectIds.Contains("proj-alpha"), "Unfiltered complete project selection allows deleting proj-alpha");

            // ResolveLocalTargetsFromSets merges hidden checked items and detects hidden counts
            HashSet<string> checkedLocalCaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            checkedLocalCaps.Add(capVis.UniqueKey);
            checkedLocalCaps.Add(capHid.UniqueKey);
            HashSet<string> checkedLocalSessions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            checkedLocalSessions.Add("c-vis-001");
            checkedLocalSessions.Add("c-hid-002");
            HashSet<string> checkedLocalBuckets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            checkedLocalBuckets.Add("ID:proj-alpha");

            int hiddenCaps;
            int hiddenConvs;
            MainForm.LocalActionTargets mergedTargets = MainForm.ResolveLocalTargetsFromSets(
                filteredTargets,
                new List<CapabilityItem> { capVis, capHid },
                allLocalSummaries,
                checkedLocalCaps,
                checkedLocalSessions,
                checkedLocalBuckets,
                out hiddenCaps,
                out hiddenConvs);
            AssertEqual(1, hiddenCaps, "Detects 1 hidden checked capability");
            AssertEqual(1, hiddenConvs, "Detects 1 hidden checked conversation");
            AssertTrue(mergedTargets.ProjectIds.Contains("proj-alpha"), "Allows project deletion only after confirming all hidden+visible sessions checked");

            HashSet<string> partialSessions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            partialSessions.Add("c-vis-001");
            int dummyHidCap;
            int dummyHidConv;
            MainForm.LocalActionTargets partialMerged = MainForm.ResolveLocalTargetsFromSets(
                filteredTargets,
                new List<CapabilityItem> { capVis },
                allLocalSummaries,
                checkedLocalCaps,
                partialSessions,
                checkedLocalBuckets,
                out dummyHidCap,
                out dummyHidConv);
            // Notice: because checkedLocalBuckets contains "ID:proj-alpha", ResolveLocalTargetsFromSets selects c-hid-002 via whole-bucket check;
            // when checkedLocalBuckets is empty (partial check), proj-alpha must NOT be marked for whole-project deletion.
            MainForm.LocalActionTargets noBucketMerged = MainForm.ResolveLocalTargetsFromSets(
                filteredTargets,
                new List<CapabilityItem> { capVis },
                allLocalSummaries,
                checkedLocalCaps,
                partialSessions,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                out dummyHidCap,
                out dummyHidConv);
            AssertEqual(0, dummyHidConv, "Partial session check without bucket flag keeps hidden conv count at 0");
            AssertEqual(0, noBucketMerged.ProjectIds.Count, "Partial session check keeps proj-alpha intact");

            // ApplyCheckAllOrNone preserves hidden on SelectAll, clears all on DeselectAll
            HashSet<string> checkSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            checkSet.Add("hidden-k");
            MainForm.ApplyCheckAllOrNone(checkSet, new string[] { "vis-k1", "vis-k2" }, true);
            AssertEqual(3, checkSet.Count, "SelectAll keeps hidden selection and adds visible items");
            MainForm.ApplyCheckAllOrNone(checkSet, new string[] { "vis-k1" }, false);
            AssertEqual(0, checkSet.Count, "DeselectAll clears both visible and hidden selections");

            // ComputeSelectionStats & ResolveBackupImportSelectionFromSets hidden accounting
            CapabilityDiffNode cdVis = new CapabilityDiffNode { Kind = CapabilityKind.Rule, Id = "r-vis", UniqueKey = "Rule:r-vis", RemoteItem = capVis };
            CapabilityDiffNode cdHid = new CapabilityDiffNode { Kind = CapabilityKind.Rule, Id = "r-hid", UniqueKey = "Rule:r-hid", RemoteItem = capHid };
            ProjectDiffNode pDiff = new ProjectDiffNode("Alpha");
            pDiff.BackupConversations.Add(cVis);
            pDiff.BackupConversations.Add(cHid);

            HashSet<string> bakCapSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bakCapSet.Add("Rule:r-vis");
            bakCapSet.Add("Rule:r-hid");
            HashSet<string> bakSessSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bakSessSet.Add("c-vis-001");
            bakSessSet.Add("c-hid-002");
            HashSet<string> bakProjSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bakProjSet.Add("Alpha");

            MainForm.SelectionStatsSummary stats = MainForm.ComputeSelectionStats(
                new List<CapabilityDiffNode> { cdVis, cdHid },
                new List<ProjectDiffNode> { pDiff },
                bakCapSet, bakSessSet, bakProjSet,
                1, 1);
            AssertEqual(2, stats.TotalSelectedCapabilities, "Total selected capabilities == 2");
            AssertEqual(2, stats.TotalSelectedConversations, "Total selected conversations == 2");
            AssertEqual(2, stats.VisibleSelectedCount, "Visible selected count == 2");
            AssertEqual(2, stats.HiddenSelectedCount, "Hidden selected count == 2");

            HashSet<string> visCapKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            visCapKeys.Add("Rule:r-vis");
            HashSet<string> visSessIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            visSessIds.Add("c-vis-001");
            HashSet<string> visProjNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            visProjNames.Add("Alpha");

            MainForm.BackupImportSelectionResult importSel = MainForm.ResolveBackupImportSelectionFromSets(
                new List<CapabilityDiffNode> { cdVis, cdHid },
                new List<ProjectDiffNode> { pDiff },
                bakCapSet, bakSessSet, bakProjSet,
                visCapKeys, visSessIds, visProjNames);
            AssertEqual(1, importSel.HiddenCapabilitiesCount, "ResolveBackupImportSelectionFromSets reports 1 hidden capability");
            AssertEqual(1, importSel.HiddenConversationsCount, "ResolveBackupImportSelectionFromSets reports 1 hidden conversation");
        }

        private static void T30_CapabilityRepo_ScriptPathRegexQuotedEnvAndSpaces()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_path_rx_" + Guid.NewGuid().ToString("N"));
            string prevEnv = Environment.GetEnvironmentVariable("AGY_TEST_MISSING_DIR");
            try
            {
                string spaceDir = Path.Combine(sb, "dir with space");
                Directory.CreateDirectory(spaceDir);
                string dQuotedPy = Path.Combine(spaceDir, "run tool.py");
                string sQuotedPy = Path.Combine(spaceDir, "single tool.py");
                File.WriteAllText(dQuotedPy, "# ok", Encoding.UTF8);
                File.WriteAllText(sQuotedPy, "# ok", Encoding.UTF8);

                Environment.SetEnvironmentVariable("AGY_TEST_MISSING_DIR", "Z:\\AgyEnvMissing");

                string cmdLine = string.Format(
                    "\"{0}\" '{1}' \"~/missing_tilde.py\" \"%AGY_TEST_MISSING_DIR%\\missing_env.py\" --extra=Z:\\NonExistent\\missing_unquoted.py",
                    dQuotedPy, sQuotedPy);
                string jsonFrag = "{\"command\": \"" + JsonKeyManipulator.EscapeJsonString(cmdLine) + "\"}";

                List<string> referenced;
                List<string> missing;
                CapabilityPathNormalizer.DetectReferencedAndMissingPaths(
                    jsonFrag,
                    "Z:\\AgySpacelessHome",
                    Path.Combine(sb, ".gemini"),
                    null,
                    out referenced,
                    out missing);

                AssertEqual(5, referenced.Count, "Extracted all 5 referenced paths (double-quoted, single-quoted, tilde, %ENV%, =unquoted)");
                AssertEqual(3, missing.Count, "Identified exact 3 missing paths while confirming existing spaced script paths");
                AssertTrue(missing.Contains("Z:\\AgySpacelessHome\\missing_tilde.py"), "Tilde ~/ expanded with spaceless home");
                AssertTrue(missing.Contains("Z:\\AgyEnvMissing\\missing_env.py"), "%ENV% variable expanded");
                AssertTrue(missing.Contains("Z:\\NonExistent\\missing_unquoted.py"), "--flag=unquoted path matched");
            }
            finally
            {
                Environment.SetEnvironmentVariable("AGY_TEST_MISSING_DIR", prevEnv);
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T31_MainForm_RelocateCapPathSelectionState()
        {
            AssertEqual(
                "D:\\Old\\b.py",
                MainForm.ComputeRelocatePathOnSelectionChanged("C:\\Old\\a.py", "C:\\Old\\a.py", "D:\\Old\\b.py"),
                "Auto-updates unedited path when user switches referenced script");
            AssertEqual(
                "D:\\Old\\b.py",
                MainForm.ComputeRelocatePathOnSelectionChanged("C:\\Old\\a.py", "   ", "D:\\Old\\b.py"),
                "Populates empty path box when user switches referenced script");
            AssertEqual(
                "E:\\Custom\\edited.py",
                MainForm.ComputeRelocatePathOnSelectionChanged("C:\\Old\\a.py", "E:\\Custom\\edited.py", "D:\\Old\\b.py"),
                "Preserves manually edited path instead of overwriting on selection change");
        }

        // ══════════════════════════════════════════════════════════════════════
        // Three-Layer Rename, SQLite FTS5 Bridge & Workbench Tests (T32 ~ T36)
        // ══════════════════════════════════════════════════════════════════════

        private static string T_GetSqliteErr(IntPtr db)
        {
            if (db == IntPtr.Zero) return "db is null";
            IntPtr ptr = t_sqlite3_errmsg(db);
            if (ptr == IntPtr.Zero) return "unknown";
            int len = 0;
            while (len < 1024 && Marshal.ReadByte(ptr, len) != 0) len++;
            byte[] buf = new byte[len];
            Marshal.Copy(ptr, buf, 0, len);
            return Encoding.UTF8.GetString(buf);
        }

        private static int T_BindUtf8Text(IntPtr stmt, int col, string text)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(text ?? string.Empty);
            byte[] pinned = utf8.Length > 0 ? utf8 : new byte[] { 0 };
            GCHandle pin = GCHandle.Alloc(pinned, GCHandleType.Pinned);
            try
            {
                return t_sqlite3_bind_text(stmt, col, pin.AddrOfPinnedObject(), utf8.Length, new IntPtr(-1));
            }
            finally
            {
                if (pin.IsAllocated) pin.Free();
            }
        }

        private static string T_ReadColText(IntPtr stmt, int col)
        {
            if (t_sqlite3_column_type(stmt, col) == 5) return null;
            IntPtr ptr = t_sqlite3_column_text(stmt, col);
            if (ptr == IntPtr.Zero) return string.Empty;
            int len = t_sqlite3_column_bytes(stmt, col);
            if (len <= 0) return string.Empty;
            byte[] buf = new byte[len];
            Marshal.Copy(ptr, buf, 0, len);
            return Encoding.UTF8.GetString(buf);
        }

        private static byte[] BuildRawSummaryMetaBytes(string autoTitle, string customTitle, string branchHint, int stepCount, int? extraField22)
        {
            List<ProtoNode> metaChildren = new List<ProtoNode>();
            if (!string.IsNullOrEmpty(autoTitle))
                metaChildren.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(autoTitle), IsModified = true });
            metaChildren.Add(new ProtoNode(2, 0) { VarintValue = (ulong)stepCount, IsModified = true });

            ProtoNode f9 = new ProtoNode(9, 2) { IsModified = true };
            f9.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes("file:///d%3A/Work/Inverter"), IsModified = true });
            if (!string.IsNullOrEmpty(branchHint))
            {
                ProtoNode f3 = new ProtoNode(3, 2) { IsModified = true };
                f3.Children.Add(new ProtoNode(4, 2) { RawBytes = Encoding.UTF8.GetBytes(branchHint), IsModified = true });
                f9.Children.Add(f3);
            }
            metaChildren.Add(f9);

            if (!string.IsNullOrEmpty(customTitle))
            {
                ProtoNode f15 = new ProtoNode(15, 2) { IsModified = true };
                f15.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(customTitle), IsModified = true });
                ProtoNode ts = new ProtoNode(7, 2) { IsModified = true };
                ts.Children.Add(new ProtoNode(1, 0) { VarintValue = 1756000000UL, IsModified = true });
                f15.Children.Add(ts);
                metaChildren.Add(f15);
            }

            if (extraField22.HasValue)
            {
                metaChildren.Add(new ProtoNode(22, 0) { VarintValue = (ulong)extraField22.Value, IsModified = true });
            }

            return ProtoCodec.Encode(metaChildren);
        }

        private static IntPtr OpenAndCreateSummaryWalDb(string dbPath)
        {
            IntPtr db;
            int rc = t_sqlite3_open_v2(Utf8Z(dbPath), out db, 0x00000002 | 0x00000004, IntPtr.Zero);
            AssertEqual(0, rc, "winsqlite3_open_v2 creates summary DB: " + dbPath);

            AssertEqual(0, t_sqlite3_exec(db, Utf8Z("PRAGMA journal_mode=WAL;"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Enable WAL on summary DB: " + T_GetSqliteErr(db));
            AssertEqual(0, t_sqlite3_exec(db, Utf8Z("PRAGMA wal_autocheckpoint=0;"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Disable autocheckpoint");

            string summariesDdl =
                "CREATE TABLE conversation_summaries (" +
                "conversation_id TEXT PRIMARY KEY NOT NULL," +
                "title TEXT NOT NULL," +
                "preview TEXT NOT NULL," +
                "step_count INTEGER NOT NULL," +
                "last_modified_time TEXT NOT NULL," +
                "workspace_uris TEXT NOT NULL," +
                "status TEXT NOT NULL," +
                "source TEXT NOT NULL," +
                "project_id TEXT NOT NULL," +
                "agent_name TEXT NOT NULL," +
                "parent_conversation_id TEXT NOT NULL," +
                "nesting_depth INTEGER NOT NULL," +
                "battle_id TEXT NOT NULL," +
                "winning_conversation_id TEXT NOT NULL," +
                "not_fully_idle INTEGER NOT NULL," +
                "killed INTEGER NOT NULL," +
                "last_user_input_time TEXT NOT NULL," +
                "last_user_input_step_index INTEGER NOT NULL," +
                "app_data_dir TEXT NOT NULL," +
                "raw_summary BLOB NOT NULL," +
                "group_id TEXT NOT NULL" +
                ");";
            AssertEqual(0, t_sqlite3_exec(db, Utf8Z(summariesDdl), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Create conversation_summaries DDL: " + T_GetSqliteErr(db));

            string ftsDdl =
                "CREATE VIRTUAL TABLE conversations_fts USING fts5(" +
                "conversation_id UNINDEXED," +
                "chunk_index UNINDEXED," +
                "title," +
                "workspace," +
                "project," +
                "user_body," +
                "agent_body," +
                "tokenize='unicode61'" +
                ");";
            AssertEqual(0, t_sqlite3_exec(db, Utf8Z(ftsDdl), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Create conversations_fts unicode61 DDL: " + T_GetSqliteErr(db));

            string parentsDdl =
                "CREATE TABLE conversation_parents (" +
                "conversation_id TEXT NOT NULL," +
                "parent_id TEXT NOT NULL," +
                "PRIMARY KEY(conversation_id, parent_id)" +
                ");";
            AssertEqual(0, t_sqlite3_exec(db, Utf8Z(parentsDdl), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Create conversation_parents DDL: " + T_GetSqliteErr(db));
            AssertEqual(0, t_sqlite3_exec(db, Utf8Z("PRAGMA wal_checkpoint(TRUNCATE);"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Checkpoint DDL");
            return db;
        }

        private static void InsertSummaryDbRow(
            IntPtr db,
            string cid,
            string title,
            string preview,
            int stepCount,
            string workspaceUris,
            string status,
            string projectId,
            string agentName,
            string parentCid,
            int nestingDepth,
            int notFullyIdle,
            byte[] rawSummary)
        {
            string sql =
                "INSERT INTO conversation_summaries (" +
                "conversation_id, title, preview, step_count, last_modified_time, workspace_uris, status, source, project_id, agent_name, " +
                "parent_conversation_id, nesting_depth, battle_id, winning_conversation_id, not_fully_idle, killed, last_user_input_time, " +
                "last_user_input_step_index, app_data_dir, raw_summary, group_id" +
                ") VALUES (?, ?, ?, ?, '2026-11-18T08:00:00.000Z', ?, ?, '', ?, ?, ?, ?, '', '', ?, 0, '2026-11-18T08:00:00.000Z', 0, 'antigravity', ?, '');";

            IntPtr stmt = IntPtr.Zero;
            byte[] raw = rawSummary ?? new byte[0];
            byte[] pinned = raw.Length > 0 ? raw : new byte[] { 0 };
            GCHandle pin = GCHandle.Alloc(pinned, GCHandleType.Pinned);
            try
            {
                AssertEqual(0, t_sqlite3_prepare_v2(db, Utf8Z(sql), -1, out stmt, IntPtr.Zero), "Prepare INSERT conversation_summaries: " + T_GetSqliteErr(db));
                AssertEqual(0, T_BindUtf8Text(stmt, 1, cid), "Bind cid");
                AssertEqual(0, T_BindUtf8Text(stmt, 2, title), "Bind title");
                AssertEqual(0, T_BindUtf8Text(stmt, 3, preview), "Bind preview");
                AssertEqual(0, t_sqlite3_bind_int(stmt, 4, stepCount), "Bind step_count");
                AssertEqual(0, T_BindUtf8Text(stmt, 5, workspaceUris), "Bind workspace_uris");
                AssertEqual(0, T_BindUtf8Text(stmt, 6, status), "Bind status");
                AssertEqual(0, T_BindUtf8Text(stmt, 7, projectId), "Bind project_id");
                AssertEqual(0, T_BindUtf8Text(stmt, 8, agentName), "Bind agent_name");
                AssertEqual(0, T_BindUtf8Text(stmt, 9, parentCid), "Bind parent_conversation_id");
                AssertEqual(0, t_sqlite3_bind_int(stmt, 10, nestingDepth), "Bind nesting_depth");
                AssertEqual(0, t_sqlite3_bind_int(stmt, 11, notFullyIdle), "Bind not_fully_idle");
                AssertEqual(0, t_sqlite3_bind_blob(stmt, 12, pin.AddrOfPinnedObject(), raw.Length, new IntPtr(-1)), "Bind raw_summary");
                AssertEqual(101, t_sqlite3_step(stmt), "Step INSERT conversation_summaries: " + T_GetSqliteErr(db));
            }
            finally
            {
                if (stmt != IntPtr.Zero) t_sqlite3_finalize(stmt);
                if (pin.IsAllocated) pin.Free();
            }
        }

        private static void InsertFtsDbRow(
            IntPtr db,
            string cid,
            int chunkIndex,
            string title,
            string workspace,
            string project,
            string userBody,
            string agentBody)
        {
            string sql = "INSERT INTO conversations_fts (conversation_id, chunk_index, title, workspace, project, user_body, agent_body) VALUES (?, ?, ?, ?, ?, ?, ?);";
            IntPtr stmt = IntPtr.Zero;
            try
            {
                AssertEqual(0, t_sqlite3_prepare_v2(db, Utf8Z(sql), -1, out stmt, IntPtr.Zero), "Prepare INSERT conversations_fts: " + T_GetSqliteErr(db));
                AssertEqual(0, T_BindUtf8Text(stmt, 1, cid), "Bind fts cid");
                AssertEqual(0, t_sqlite3_bind_int(stmt, 2, chunkIndex), "Bind chunk_index");
                AssertEqual(0, T_BindUtf8Text(stmt, 3, title), "Bind fts title");
                AssertEqual(0, T_BindUtf8Text(stmt, 4, workspace), "Bind fts workspace");
                AssertEqual(0, T_BindUtf8Text(stmt, 5, project), "Bind fts project");
                AssertEqual(0, T_BindUtf8Text(stmt, 6, userBody), "Bind fts user_body");
                AssertEqual(0, T_BindUtf8Text(stmt, 7, agentBody), "Bind fts agent_body");
                AssertEqual(101, t_sqlite3_step(stmt), "Step INSERT conversations_fts: " + T_GetSqliteErr(db));
            }
            finally
            {
                if (stmt != IntPtr.Zero) t_sqlite3_finalize(stmt);
            }
        }

        private static void InsertParentDbRow(IntPtr db, string cid, string parentId)
        {
            string sql = "INSERT INTO conversation_parents (conversation_id, parent_id) VALUES (?, ?);";
            IntPtr stmt = IntPtr.Zero;
            try
            {
                AssertEqual(0, t_sqlite3_prepare_v2(db, Utf8Z(sql), -1, out stmt, IntPtr.Zero), "Prepare INSERT conversation_parents");
                AssertEqual(0, T_BindUtf8Text(stmt, 1, cid), "Bind child cid");
                AssertEqual(0, T_BindUtf8Text(stmt, 2, parentId), "Bind parent cid");
                AssertEqual(101, t_sqlite3_step(stmt), "Step INSERT conversation_parents");
            }
            finally
            {
                if (stmt != IntPtr.Zero) t_sqlite3_finalize(stmt);
            }
        }

        private static string QuerySingleText(string dbPath, string sql, string param1)
        {
            IntPtr db = IntPtr.Zero;
            IntPtr stmt = IntPtr.Zero;
            try
            {
                AssertEqual(0, t_sqlite3_open_v2(Utf8Z(dbPath), out db, 0x00000001, IntPtr.Zero), "Open readonly for QuerySingleText");
                AssertEqual(0, t_sqlite3_prepare_v2(db, Utf8Z(sql), -1, out stmt, IntPtr.Zero), "Prepare QuerySingleText");
                if (param1 != null) AssertEqual(0, T_BindUtf8Text(stmt, 1, param1), "Bind param1");
                int rc = t_sqlite3_step(stmt);
                if (rc == 100) return T_ReadColText(stmt, 0);
                return null;
            }
            finally
            {
                if (stmt != IntPtr.Zero) t_sqlite3_finalize(stmt);
                if (db != IntPtr.Zero) t_sqlite3_close(db);
            }
        }

        private static int QuerySingleInt(string dbPath, string sql, string param1)
        {
            IntPtr db = IntPtr.Zero;
            IntPtr stmt = IntPtr.Zero;
            try
            {
                AssertEqual(0, t_sqlite3_open_v2(Utf8Z(dbPath), out db, 0x00000001, IntPtr.Zero), "Open readonly for QuerySingleInt");
                AssertEqual(0, t_sqlite3_prepare_v2(db, Utf8Z(sql), -1, out stmt, IntPtr.Zero), "Prepare QuerySingleInt");
                if (param1 != null) AssertEqual(0, T_BindUtf8Text(stmt, 1, param1), "Bind param1");
                int rc = t_sqlite3_step(stmt);
                if (rc == 100) return t_sqlite3_column_int(stmt, 0);
                return 0;
            }
            finally
            {
                if (stmt != IntPtr.Zero) t_sqlite3_finalize(stmt);
                if (db != IntPtr.Zero) t_sqlite3_close(db);
            }
        }

        private static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        private static void CopyDirectoryRecursive(string srcDir, string dstDir)
        {
            Directory.CreateDirectory(dstDir);
            string[] files = Directory.GetFiles(srcDir);
            for (int i = 0; i < files.Length; i++)
            {
                File.Copy(files[i], Path.Combine(dstDir, Path.GetFileName(files[i])), true);
            }
            string[] dirs = Directory.GetDirectories(srcDir);
            for (int i = 0; i < dirs.Length; i++)
            {
                CopyDirectoryRecursive(dirs[i], Path.Combine(dstDir, Path.GetFileName(dirs[i])));
            }
        }

        private static void T32_PbtxtAnnotationCodec_RoundTripAndCreate()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_pbtxt_codec_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(sb);

                // 1. Preserve all non-title fields verbatim (both trailing and leading/trailing layouts)
                string tailFields = "pinned:true archived:false archival_status_timestamp:{seconds:1750000000 nanos:500} last_user_view_time:{seconds:1760000000 nanos:999}";
                string existingLeadingTitle = "title:\"Original Title\" " + tailFields;
                string updatedLeading = PbtxtAnnotationCodec.UpsertTitle(existingLeadingTitle, "【Companion】Updated Title");
                AssertEqual("【Companion】Updated Title", PbtxtAnnotationCodec.ExtractTitle(updatedLeading), "ExtractTitle returns updated title");
                AssertTrue(updatedLeading.EndsWith(" " + tailFields, StringComparison.Ordinal), "Preserves pinned/archived/timestamps verbatim after title");

                string existingMiddleTitle = "pinned:true\ntitle: \"Old Mid\"\narchived:false\narchival_status_timestamp:{seconds:1750000000}\nlast_user_view_time:{seconds:1760000000}";
                string updatedMiddle = PbtxtAnnotationCodec.UpsertTitle(existingMiddleTitle, "Mid Replaced");
                AssertEqual(
                    "pinned:true\ntitle: \"Mid Replaced\"\narchived:false\narchival_status_timestamp:{seconds:1750000000}\nlast_user_view_time:{seconds:1760000000}",
                    updatedMiddle,
                    "Preserves prefix and suffix around middle title verbatim");

                // 2. Missing title: prepending and empty/null creation
                string noTitleContent = "pinned:true archived:false archival_status_timestamp:{seconds:1751111111} last_user_view_time:{seconds:1761111111}";
                string prepended = PbtxtAnnotationCodec.UpsertTitle(noTitleContent, "【3KU】Prepended Title");
                AssertEqual("title:\"【3KU】Prepended Title\" " + noTitleContent, prepended, "Prepends title: when missing and keeps all existing fields verbatim");
                AssertEqual("【3KU】Prepended Title", PbtxtAnnotationCodec.ExtractTitle(prepended), "Extracts prepended title");

                string brandNew = PbtxtAnnotationCodec.UpsertTitle(null, "Created Title");
                AssertEqual("title:\"Created Title\"", brandNew, "Creates title-only pbtxt from null input");

                // 3. Escaping quotes, backslashes, and newlines
                string trickyTitle = "Fix \"quoted\" \\\\slash\\path\nLine2\r\n\tTabbed";
                string escapedPbtxt = PbtxtAnnotationCodec.UpsertTitle(tailFields, trickyTitle);
                AssertTrue(escapedPbtxt.Contains("\\\"quoted\\\"") && escapedPbtxt.Contains("\\\\\\\\slash\\\\path") && escapedPbtxt.Contains("\\nLine2\\r\\n\\tTabbed"),
                    "Escapes quotes, backslashes, CR/LF, and tabs");
                AssertEqual(trickyTitle, PbtxtAnnotationCodec.ExtractTitle(escapedPbtxt), "Round-trips quotes/backslashes/newlines cleanly");

                // 4. Multi-byte UTF-8 octal decoding (NOT corrupted char-cast!)
                string octalPbtxt = "title:\"\\343\\200\\2203KU\\343\\200\\221 \\346\\257\\215\\347\\272\\277\" pinned:true";
                AssertEqual("【3KU】 母线", PbtxtAnnotationCodec.ExtractTitle(octalPbtxt), "Decodes multi-byte UTF-8 octal sequences to exact CJK/bracket chars");

                // 5. Assert UTF-8 without BOM when persisted
                string pbtxtFile = Path.Combine(sb, "annot_test.pbtxt");
                LocalWorkspaceManager.WriteUtf8TextAtomicWithBackup(pbtxtFile, prepended, false, null);
                byte[] diskBytes = File.ReadAllBytes(pbtxtFile);
                AssertTrue(diskBytes.Length > 3, "Written pbtxt file is non-empty");
                AssertTrue(!(diskBytes[0] == 0xEF && diskBytes[1] == 0xBB && diskBytes[2] == 0xBF), "Written pbtxt has NO UTF-8 BOM");
                string strictDecoded = new UTF8Encoding(false, true).GetString(diskBytes);
                AssertEqual("【3KU】Prepended Title", PbtxtAnnotationCodec.ExtractTitle(strictDecoded), "Disk pbtxt decodes strictly as UTF-8 without BOM");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T33_SummarySqliteBridge_ThreeLayerAndHybridFts()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_sum_bridge_" + Guid.NewGuid().ToString("N"));
            IntPtr db = IntPtr.Zero;
            try
            {
                Directory.CreateDirectory(sb);
                string dbPath = Path.Combine(sb, "conversation_summaries.db");
                string backupDbPath = Path.Combine(sb, "backup_copy", "summary_backup.db");

                string cid1 = "11111111-1111-4111-8111-111111111111";
                string cid2 = "22222222-2222-4222-8222-222222222222";
                string cid3 = "33333333-3333-4333-8333-333333333333";
                string cidNew = "44444444-4444-4444-8444-444444444444";

                byte[] raw1Orig = BuildRawSummaryMetaBytes("Orig Auto 1", null, "Dev_zy", 15, 777);
                byte[] raw2Orig = BuildRawSummaryMetaBytes("Orig Auto 2", "Orig Custom 2", "Dev_YouNeng_ChuanYin", 42, 888);
                byte[] raw3Orig = BuildRawSummaryMetaBytes("Orig Auto 3", null, null, 7, null);

                db = OpenAndCreateSummaryWalDb(dbPath);
                InsertSummaryDbRow(db, cid1, "Old Title 1", "Old Preview 1", 15, "[\"file:///d%3A/Work/Inverter\"]",
                    "CASCADE_RUN_STATUS_IDLE", "proj-inv", "", "", 0, 0, raw1Orig);
                InsertSummaryDbRow(db, cid2, "Old Title 2", "Old Preview 2", 42, "[\"file:///d%3A/Work/Inverter\"]",
                    "CASCADE_RUN_STATUS_RUNNING", "proj-inv", "research", cid1, 1, 1, raw2Orig);
                InsertSummaryDbRow(db, cid3, "Old Title 3", "Old Preview 3", 7, "[\"file:///d%3A/Work/App\"]",
                    "CASCADE_RUN_STATUS_IDLE", "proj-app", "", "", 0, 0, raw3Orig);

                InsertFtsDbRow(db, cid1, -1, "Old Title 1", "file:///d%3A/Work/Inverter", "Inverter", "", "");
                InsertFtsDbRow(db, cid1, 0, "Old Title 1", "file:///d%3A/Work/Inverter", "Inverter",
                    "Check sBusDeratingChargeCurr and Dev_zy branch /plan behavior", "Agent note 0x41A3");
                InsertFtsDbRow(db, cid2, -1, "Old Title 2", "file:///d%3A/Work/Inverter", "Inverter", "", "");
                InsertFtsDbRow(db, cid2, 0, "Old Title 2", "file:///d%3A/Work/Inverter", "Inverter",
                    "系统触发母线高压保护故障，进度100%完成，符号foo:bar*已记录", "Flag a_b! active");
                InsertFtsDbRow(db, cid3, -1, "Old Title 3", "file:///d%3A/Work/App", "App", "", "");
                InsertFtsDbRow(db, cid3, 0, "Old Title 3", "file:///d%3A/Work/App", "App",
                    "Progress 100 percent only, Flag axb! decoy", "");

                InsertParentDbRow(db, cid2, cid1);

                t_sqlite3_close(db);
                db = IntPtr.Zero;

                // 1. ReadSummaryDbSnapshot reads agent_name, not_fully_idle, FirstPromptSnippet, GitBranchHint
                Dictionary<string, SummaryDbRowInfo> snap = SummarySqliteBridge.ReadSummaryDbSnapshot(dbPath);
                AssertEqual(3, snap.Count, "ReadSummaryDbSnapshot loads all 3 rows");
                AssertEqual("", snap[cid1].AgentName, "cid1 agent_name empty");
                AssertTrue(!snap[cid1].NotFullyIdle, "cid1 not_fully_idle == false");
                AssertTrue(snap[cid1].FirstPromptSnippet.Contains("sBusDeratingChargeCurr"), "cid1 FirstPromptSnippet read from FTS chunk_index=0");
                AssertEqual("Dev_zy", snap[cid1].GitBranchHint, "cid1 GitBranchHint decoded from raw_summary Field 9->3->4");

                AssertEqual("research", snap[cid2].AgentName, "cid2 agent_name == research");
                AssertTrue(snap[cid2].NotFullyIdle, "cid2 not_fully_idle == true");
                AssertEqual("Dev_YouNeng_ChuanYin", snap[cid2].GitBranchHint, "cid2 GitBranchHint decoded");

                // 2. UpdateSummaryTitlesTransaction & RestoreSummaryRowsTransaction
                Dictionary<string, string> renameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                renameMap[cid1] = "【3KU·Dev_zy】New Title 1";
                renameMap[cid2] = "【3KU】New Title 2";

                Dictionary<string, SummaryDbRollbackEntry> rbMap;
                List<string> updWarnings;
                bool updOk = SummarySqliteBridge.UpdateSummaryTitlesTransaction(dbPath, renameMap, out rbMap, out updWarnings);
                AssertTrue(updOk, "UpdateSummaryTitlesTransaction succeeded (" + SummarySqliteBridge.LastError + ")");
                AssertEqual(2, rbMap.Count, "Rollback entries produced for both updated rows");

                Dictionary<string, SummaryDbRowInfo> snapAfter = SummarySqliteBridge.ReadSummaryDbSnapshot(dbPath);
                AssertEqual("【3KU·Dev_zy】New Title 1", snapAfter[cid1].Title, "cid1 title updated");
                AssertEqual("【3KU·Dev_zy】New Title 1", snapAfter[cid1].Preview, "cid1 preview updated");
                AssertEqual("【3KU】New Title 2", snapAfter[cid2].Title, "cid2 (with existing Field 15) title updated");
                AssertEqual("Dev_zy", snapAfter[cid1].GitBranchHint, "cid1 non-title GitBranchHint preserved in raw_summary");

                List<ProtoNode> raw1Nodes = ProtoCodec.Decode(snapAfter[cid1].RawSummaryBytes);
                ProtoCodec.DecodeMetaContainerList(raw1Nodes);
                ProtoNode raw1F1 = raw1Nodes.Find(x => x.FieldNumber == 1);
                ProtoNode raw1F2 = raw1Nodes.Find(x => x.FieldNumber == 2);
                ProtoNode raw1F15 = raw1Nodes.Find(x => x.FieldNumber == 15);
                ProtoNode raw1F22 = raw1Nodes.Find(x => x.FieldNumber == 22);
                AssertEqual("【3KU·Dev_zy】New Title 1", raw1F1.GetString(), "raw_summary Field 1 updated");
                AssertEqual(15, raw1F2.GetInt32(), "raw_summary Field 2 step_count preserved");
                AssertEqual("【3KU·Dev_zy】New Title 1", raw1F15.GetChild(1).GetString(), "raw_summary Field 15->1 updated");
                AssertEqual(777, raw1F22.GetInt32(), "raw_summary non-title sentinel Field 22 preserved");

                AssertEqual("【3KU·Dev_zy】New Title 1", QuerySingleText(dbPath, "SELECT title FROM conversations_fts WHERE conversation_id = ? AND chunk_index = -1;", cid1),
                    "conversations_fts chunk_index=-1 title updated");
                AssertEqual("Old Title 1", QuerySingleText(dbPath, "SELECT title FROM conversations_fts WHERE conversation_id = ? AND chunk_index = 0;", cid1),
                    "conversations_fts chunk_index=0 left untouched");

                List<string> rbWarnings;
                bool rbOk = SummarySqliteBridge.RestoreSummaryRowsTransaction(dbPath, rbMap, out rbWarnings);
                AssertTrue(rbOk, "RestoreSummaryRowsTransaction succeeded (" + SummarySqliteBridge.LastError + ")");
                Dictionary<string, SummaryDbRowInfo> snapRestored = SummarySqliteBridge.ReadSummaryDbSnapshot(dbPath);
                AssertEqual("Old Title 1", snapRestored[cid1].Title, "Restored cid1 title");
                AssertEqual("Old Preview 1", snapRestored[cid1].Preview, "Restored cid1 preview");
                AssertTrue(ByteArraysEqual(raw1Orig, snapRestored[cid1].RawSummaryBytes), "Restored cid1 raw_summary byte-for-byte");
                AssertTrue(ByteArraysEqual(raw2Orig, snapRestored[cid2].RawSummaryBytes), "Restored cid2 raw_summary byte-for-byte");
                AssertEqual("Old Title 1", QuerySingleText(dbPath, "SELECT title FROM conversations_fts WHERE conversation_id = ? AND chunk_index = -1;", cid1),
                    "Restored conversations_fts chunk_index=-1 title");

                // 3. Hybrid FTS5 + LIKE routing & operator/wildcard safety
                HashSet<string> hitFts = SummarySqliteBridge.SearchConversationsFullText(dbPath, "sBusDeratingChargeCurr");
                AssertTrue(SummarySqliteBridge.LastError == null, "FTS5 ASCII identifier query produced no error");
                AssertEqual(1, hitFts.Count, "ASCII identifier hits exact 1 session via FTS5");
                AssertTrue(hitFts.Contains(cid1), "ASCII identifier hits cid1");

                HashSet<string> hitCjk = SummarySqliteBridge.SearchConversationsFullText(dbPath, "母线高压");
                AssertTrue(SummarySqliteBridge.LastError == null, "CJK substring query produced no error");
                AssertEqual(1, hitCjk.Count, "CJK substring hits exact 1 session via LIKE fallback");
                AssertTrue(hitCjk.Contains(cid2), "CJK substring hits cid2 inside unsegmented CJK phrase");

                string[] operatorQueries = new string[] { "Dev_zy", "0x41A3", "/plan", "foo:bar*" };
                string[] expectedOpCids = new string[] { cid1, cid1, cid1, cid2 };
                for (int i = 0; i < operatorQueries.Length; i++)
                {
                    HashSet<string> opHits = SummarySqliteBridge.SearchConversationsFullText(dbPath, operatorQueries[i]);
                    AssertTrue(SummarySqliteBridge.LastError == null, "Special query '" + operatorQueries[i] + "' did not error: " + SummarySqliteBridge.LastError);
                    AssertTrue(opHits.Contains(expectedOpCids[i]), "Special query '" + operatorQueries[i] + "' matched expected session");
                }

                HashSet<string> pctHits = SummarySqliteBridge.SearchConversationsFullText(dbPath, "100%");
                AssertTrue(SummarySqliteBridge.LastError == null, "Percent query produced no error");
                AssertEqual(1, pctHits.Count, "Literal '%' matches ONLY '100%', not '100 percent'");
                AssertTrue(pctHits.Contains(cid2) && !pctHits.Contains(cid3), "Literal '%' did not falsely wildcard cid3");

                HashSet<string> underHits = SummarySqliteBridge.SearchConversationsFullText(dbPath, "a_b!");
                AssertTrue(SummarySqliteBridge.LastError == null, "Underscore literal query produced no error");
                AssertEqual(1, underHits.Count, "Literal '_' matches ONLY 'a_b!', not 'axb!'");
                AssertTrue(underHits.Contains(cid2) && !underHits.Contains(cid3), "Literal '_' did not falsely wildcard cid3");

                HashSet<string> singleUnderHits = SummarySqliteBridge.SearchConversationsFullText(dbPath, "_");
                AssertTrue(SummarySqliteBridge.LastError == null, "Single '_' query produced no error");
                AssertTrue(singleUnderHits.Contains(cid1) && singleUnderHits.Contains(cid2) && !singleUnderHits.Contains(cid3),
                    "Single '_' matches literal '_' rows only and does not wildcard cid3");

                HashSet<string> doubleUnderHits = SummarySqliteBridge.SearchConversationsFullText(dbPath, "__");
                AssertTrue(SummarySqliteBridge.LastError == null, "Double '__' underscore-only query produced no error");
                AssertEqual(0, doubleUnderHits.Count, "Double '__' routes to escaped LIKE and does not wildcard any 2-char string");

                // 4. UpsertSessionsInSummaryDb (existing preserve + new NOT NULL insert), BackupDatabaseToFile (WAL online), DeleteSessionsFromSummaryDb
                byte[] newRowRaw = BuildRawSummaryMetaBytes("Inserted Auto", null, "Dev_New", 3, null);
                List<SummaryDbUpsertItem> upserts = new List<SummaryDbUpsertItem>
                {
                    new SummaryDbUpsertItem
                    {
                        ConversationId = cid2,
                        Title = "Upserted Title 2",
                        Preview = "Upserted Preview 2",
                        ProjectId = "proj-moved",
                        WorkspaceUrisJson = "[\"file:///e%3A/Moved\"]",
                        FtsWorkspaceToken = "file:///e%3A/Moved",
                        ProjectName = "MovedProj"
                    },
                    new SummaryDbUpsertItem
                    {
                        ConversationId = cidNew,
                        Title = "Brand New Inserted",
                        Preview = "Brand New Preview",
                        StepCount = 3,
                        WorkspaceUrisJson = "[\"file:///e%3A/New\"]",
                        ProjectId = "proj-new",
                        ParentConversationId = cid1,
                        NestingDepth = 1,
                        RawSummaryBytes = newRowRaw
                    }
                };
                List<string> upsertWarns;
                int upserted = SummarySqliteBridge.UpsertSessionsInSummaryDb(dbPath, upserts, out upsertWarns);
                AssertEqual(2, upserted, "UpsertSessionsInSummaryDb updated existing and inserted new NOT NULL row");

                Dictionary<string, SummaryDbRowInfo> snapUpserted = SummarySqliteBridge.ReadSummaryDbSnapshot(dbPath);
                AssertEqual(4, snapUpserted.Count, "Snapshot contains 4 rows after upsert");
                AssertEqual("Upserted Title 2", snapUpserted[cid2].Title, "Existing row title updated by upsert");
                AssertEqual(42, snapUpserted[cid2].StepCount, "Existing row step_count preserved by upsert");
                AssertEqual("CASCADE_RUN_STATUS_RUNNING", snapUpserted[cid2].Status, "Existing row status preserved by upsert");
                AssertTrue(snapUpserted[cid2].NotFullyIdle, "Existing row not_fully_idle preserved by upsert");
                AssertEqual("research", snapUpserted[cid2].AgentName, "Existing row agent_name preserved by upsert");
                AssertEqual("Brand New Inserted", snapUpserted[cidNew].Title, "New row inserted satisfying all NOT NULL columns");

                // Open WAL writer with uncheckpointed frame to verify BackupDatabaseToFile merges live WAL into standalone copy
                AssertEqual(0, t_sqlite3_open_v2(Utf8Z(dbPath), out db, 0x00000002, IntPtr.Zero), "Reopen DB to create live WAL frame");
                AssertEqual(0, t_sqlite3_exec(db, Utf8Z("PRAGMA wal_autocheckpoint=0;"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Disable autocheckpoint");
                InsertParentDbRow(db, cidNew, cid1);
                InsertSummaryDbRow(db, cidNew + "z", "WAL Uncheckpointed Row", "WAL Prev", 9, "[]",
                    "CASCADE_RUN_STATUS_IDLE", "proj-wal", "", "", 0, 0, raw3Orig);
                // Re-insert parent for cidNew -> cid1 already done above; delete helper row after backup or use cidNew itself
                AssertTrue(File.Exists(dbPath + "-wal") && new FileInfo(dbPath + "-wal").Length > 32, "Uncheckpointed WAL frame exists before online backup");

                bool bkOk = SummarySqliteBridge.BackupDatabaseToFile(dbPath, backupDbPath);
                AssertTrue(bkOk, "BackupDatabaseToFile succeeded with SQLITE_DONE (" + SummarySqliteBridge.LastError + ")");
                t_sqlite3_close(db);
                db = IntPtr.Zero;

                AssertTrue(File.Exists(backupDbPath) && !File.Exists(backupDbPath + "-wal"), "Standalone backup DB exists without needing -wal");
                Dictionary<string, SummaryDbRowInfo> snapBk = SummarySqliteBridge.ReadSummaryDbSnapshot(backupDbPath);
                AssertTrue(snapBk.ContainsKey(cidNew + "z"), "Standalone backup DB includes WAL-only committed row");

                // DeleteSessionsFromSummaryDb cascades across conversation_summaries, conversations_fts, conversation_parents
                List<string> delWarns;
                int delCnt = SummarySqliteBridge.DeleteSessionsFromSummaryDb(dbPath, new string[] { cidNew, cid2 }, out delWarns);
                AssertEqual(2, delCnt, "DeleteSessionsFromSummaryDb deleted 2 rows from conversation_summaries");
                AssertEqual(0, QuerySingleInt(dbPath, "SELECT COUNT(*) FROM conversation_summaries WHERE conversation_id = ?;", cid2), "cid2 removed from conversation_summaries");
                AssertEqual(0, QuerySingleInt(dbPath, "SELECT COUNT(*) FROM conversations_fts WHERE conversation_id = ?;", cid2), "cid2 removed from conversations_fts");
                AssertEqual(0, QuerySingleInt(dbPath, "SELECT COUNT(*) FROM conversation_parents WHERE conversation_id = ? OR parent_id = ?;", cid2), "cid2 removed from conversation_parents");
                AssertEqual(0, QuerySingleInt(dbPath, "SELECT COUNT(*) FROM conversation_parents WHERE conversation_id = ?;", cidNew), "cidNew removed from conversation_parents");
            }
            finally
            {
                if (db != IntPtr.Zero) try { t_sqlite3_close(db); } catch {}
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T34_AutoTitleSynthesizer_RulesAndSubagentExclusion()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_synth_rules_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(sb);
                RenameRulesConfig rules = RenameRulesConfig.CreateDefault();
                string prefix, cleaned, title;

                // 1. Multi-repo matcher ordering for Inverter
                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s1", ProjectName = "Inverter", DisplayTitle = "untitled" },
                    "排查 3/6KU 逆变器过载保护", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【3/6KU】", prefix, "3/6KU matches before 3KU");

                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s2", ProjectName = "Inverter", DisplayTitle = "untitled" },
                    "排查 3KUbak 串口通讯超时", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【3KUbak】", prefix, "3KUbak matches before 3KU");

                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s3", ProjectName = "Inverter", DisplayTitle = "untitled" },
                    "UPS 1-3K CT1K 充电电流限幅", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【UPS 1-3K】", prefix, "UPS 1-3K matcher works");

                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s4", ProjectName = "Inverter", DisplayTitle = "untitled" },
                    "UPS 6-10K 6-10k-ups 输出校准", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【UPS 6-10K】", prefix, "UPS 6-10K matcher works");

                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s5", ProjectName = "Inverter", DisplayTitle = "untitled" },
                    "3KU 母线电容均压测试", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【3KU】", prefix, "3KU matcher works");

                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s5b", ProjectName = "Inverter", DisplayTitle = "untitled" },
                    "vmhct6ku 并机同步", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【6KU】", prefix, "6KU matcher works");

                // 2. Branch regex extraction & glued Chinese prose guard
                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s6", ProjectName = "Inverter", DisplayTitle = "untitled" },
                    "在 3KU 的 Dev_zy 分支修复母线过压", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【3KU·Dev_zy】", prefix, "Extracts Dev_zy branch");

                ProtoNode branchMeta = new ProtoNode(2, 2) { IsModified = true };
                branchMeta.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes("untitled"), IsModified = true });
                ProtoNode branchF9 = new ProtoNode(9, 2) { IsModified = true };
                branchF9.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes("file:///d%3A/Work/Inverter"), IsModified = true });
                ProtoNode branchF3 = new ProtoNode(3, 2) { IsModified = true };
                branchF3.Children.Add(new ProtoNode(4, 2) { RawBytes = Encoding.UTF8.GetBytes("Dev_zy"), IsModified = true });
                branchF9.Children.Add(branchF3);
                branchMeta.Children.Add(branchF9);
                ProtoNode branchEntry = new ProtoNode(1, 2) { IsModified = true };
                branchEntry.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes("aaaaaaaa-0000-4000-8000-000000000001"), IsModified = true });
                branchEntry.Children.Add(branchMeta);
                ConversationSummaryInfo protoBranchSess = new ConversationSummaryInfo
                {
                    ConversationId = "aaaaaaaa-0000-4000-8000-000000000001",
                    ProjectName = "Inverter",
                    DisplayTitle = "untitled",
                    FirstPromptSnippet = "3KU 母线故障",
                    RawProtoEntry = branchEntry
                };
                AutoTitleSynthesizer.SynthesizeTitle(protoBranchSess, rules, out prefix, out cleaned, out title);
                AssertEqual("【3KU·Dev_zy】", prefix, "5-param SynthesizeTitle falls back to RawProtoEntry Field 9->3->4 branch hint");

                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s7", ProjectName = "Inverter", DisplayTitle = "untitled" },
                    "基于 3KU 分支: Dev_YouNeng_ChuanYin 调试通讯", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【3KU·Dev_YouNeng_ChuanYin】", prefix, "Extracts Dev_YouNeng_ChuanYin branch");

                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s8", ProjectName = "Inverter", DisplayTitle = "untitled" },
                    "在 Customization_客户A_2026 分支适配 3KU 参数", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【3KU·Customization_客户A_2026】", prefix, "Extracts Customization_* branch");

                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s9", ProjectName = "Inverter", DisplayTitle = "untitled" },
                    "在当前分支排查 3KU 母线过压", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【3KU】", prefix, "Does NOT capture glued Chinese prose verb '排查' as branch");

                // 3. Single-repo aliases
                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s10", ProjectName = "EvoKernel", DisplayTitle = "untitled" },
                    "优化检索排序", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【Evo】", prefix, "EvoKernel maps to 【Evo】 alias");

                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s10b", ProjectName = "Antigravity_Companion", DisplayTitle = "untitled" },
                    "新增批量重命名测试", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【Companion】", prefix, "Antigravity_Companion maps to 【Companion】 alias");

                // 4. Idempotent re-synthesis stripping existing 【...】
                ConversationSummaryInfo reSynthSession = new ConversationSummaryInfo
                {
                    ConversationId = "s11",
                    ProjectName = "Inverter",
                    DisplayTitle = "【旧标签】修复母线过压保护逻辑"
                };
                string prefix1, cleaned1, title1;
                AutoTitleSynthesizer.SynthesizeTitle(reSynthSession, "3KU 修复母线过压保护逻辑", null, rules, out prefix1, out cleaned1, out title1);
                AssertEqual("【3KU】修复母线过压保护逻辑", title1, "Strips old 【...】 prefix before applying synthesized prefix");

                reSynthSession.DisplayTitle = title1;
                reSynthSession.CustomTitle = title1;
                string prefix2, cleaned2, title2;
                AutoTitleSynthesizer.SynthesizeTitle(reSynthSession, "3KU 修复母线过压保护逻辑", null, rules, out prefix2, out cleaned2, out title2);
                AssertEqual(title1, title2, "Re-synthesis is idempotent without stacking brackets");

                // 5. Prompt noise stripping
                string noisyPrompt =
                    "<ADDITIONAL_METADATA>\ncwd: C:\\Temp\n</ADDITIONAL_METADATA>" +
                    "<USER_REQUEST>/plan @[conversation:\"9999\"] \"C:\\Work\\Proj\\src\\drv_bus.c\" ```c\n" +
                    "> - **修复母线高压保护**\n```</USER_REQUEST>";
                string cleanedNoise = AutoTitleSynthesizer.CleanFirstPrompt(noisyPrompt);
                AssertEqual("drv_bus.c 修复母线高压保护", cleanedNoise, "CleanFirstPrompt strips metadata tags, slash cmds, mentions, path prefixes, fences, and markdown prefixes");

                // 6. OverlayLocalThreeLayerMetadata subagent exclusion & .pbtxt > .db > .pb priority
                string geminiDir = Path.Combine(sb, ".gemini");
                string annotDir = Path.Combine(geminiDir, "antigravity", "annotations");
                Directory.CreateDirectory(annotDir);

                string cidSelf = "aaaaaaaa-1111-4111-8111-111111111111";
                string cidResearch = "bbbbbbbb-2222-4222-8222-222222222222";
                string cidL1 = "cccccccc-3333-4333-8333-333333333333";
                string cidL2 = "dddddddd-4444-4444-8444-444444444444";
                string cidL3 = "eeeeeeee-5555-4555-8555-555555555555";

                LocalWorkspaceManager.WriteUtf8TextAtomicWithBackup(
                    Path.Combine(annotDir, cidL1 + ".pbtxt"),
                    "title:\"L1_Pbtxt_Title\" pinned:true",
                    false, null);

                Dictionary<string, SummaryDbRowInfo> dbSnap = new Dictionary<string, SummaryDbRowInfo>(StringComparer.OrdinalIgnoreCase);
                dbSnap[cidSelf] = new SummaryDbRowInfo { ConversationId = cidSelf, AgentName = "self", Title = "SubSelf" };
                dbSnap[cidResearch] = new SummaryDbRowInfo { ConversationId = cidResearch, AgentName = "research", Title = "SubResearch" };
                dbSnap[cidL1] = new SummaryDbRowInfo { ConversationId = cidL1, Title = "L2_Db_Title" };
                dbSnap[cidL2] = new SummaryDbRowInfo { ConversationId = cidL2, Title = "L2_Db_Title" };
                dbSnap[cidL3] = new SummaryDbRowInfo { ConversationId = cidL3, Title = "" };

                List<ConversationSummaryInfo> overlayList = new List<ConversationSummaryInfo>
                {
                    new ConversationSummaryInfo { ConversationId = cidSelf, DisplayTitle = "AutoSelf" },
                    new ConversationSummaryInfo { ConversationId = cidResearch, DisplayTitle = "AutoResearch" },
                    new ConversationSummaryInfo { ConversationId = cidL1, AutoSummaryTitle = "L3_F1", CustomTitle = "L3_F15", DisplayTitle = "L3_F15", HasCustomTitle = true },
                    new ConversationSummaryInfo { ConversationId = cidL2, AutoSummaryTitle = "L3_F1", CustomTitle = "L3_F15", DisplayTitle = "L3_F15", HasCustomTitle = true },
                    new ConversationSummaryInfo { ConversationId = cidL3, AutoSummaryTitle = "L3_F1", CustomTitle = "L3_F15", DisplayTitle = "L3_F15", HasCustomTitle = true }
                };

                SessionMetadataExtractor.OverlayLocalThreeLayerMetadata(overlayList, geminiDir, dbSnap);

                AssertTrue(overlayList[0].IsSubagent && overlayList[0].AgentName == "self", "agent_name='self' marked IsSubagent=true");
                AssertTrue(overlayList[1].IsSubagent && overlayList[1].AgentName == "research", "agent_name='research' marked IsSubagent=true");
                AssertEqual("L1_Pbtxt_Title", overlayList[2].DisplayTitle, "Layer 1 .pbtxt overrides Layer 2 .db and Layer 3 .pb");
                AssertEqual("L2_Db_Title", overlayList[3].DisplayTitle, "Layer 2 .db overrides Layer 3 .pb when .pbtxt absent");
                AssertEqual("L3_F15", overlayList[4].DisplayTitle, "Layer 3 .pb Field 15 preserved when L1 & L2 titles empty");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T35_BatchRenameAndRowLevelRollback_OrphanGc()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_batch_rb_" + Guid.NewGuid().ToString("N"));
            IntPtr db = IntPtr.Zero;
            try
            {
                string geminiDir = Path.Combine(sb, "live", ".gemini");
                string antigravityDir = Path.Combine(geminiDir, "antigravity");
                string annotDir = Path.Combine(antigravityDir, "annotations");
                string convDir = Path.Combine(antigravityDir, "conversations");
                string brainDir = Path.Combine(antigravityDir, "brain");
                Directory.CreateDirectory(annotDir);
                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(brainDir);

                string cidA = "11111111-1111-4111-8111-111111111111"; // has .pbtxt (pinned:true), only Field 1 in .pb
                string cidB = "22222222-2222-4222-8222-222222222222"; // no .pbtxt initially, has Field 15 in .pb
                string cidC = "33333333-3333-4333-8333-333333333333"; // running session
                string cidD = "44444444-4444-4444-8444-444444444444"; // post-rename added session

                string origPbtxtA = "title:\"Old A Pbtxt\" pinned:true archived:false last_user_view_time:{seconds:1750000000}";
                LocalWorkspaceManager.WriteUtf8TextAtomicWithBackup(Path.Combine(annotDir, cidA + ".pbtxt"), origPbtxtA, false, null);

                byte[] rawDbA = BuildRawSummaryMetaBytes("Old A Pb", null, "Dev_A", 10, null);
                byte[] rawDbB = BuildRawSummaryMetaBytes("Old B Auto", "Old B Custom", "Dev_B", 20, null);
                byte[] rawDbC = BuildRawSummaryMetaBytes("Old C Running", null, null, 5, null);

                string dbPath = Path.Combine(antigravityDir, "conversation_summaries.db");
                db = OpenAndCreateSummaryWalDb(dbPath);
                InsertSummaryDbRow(db, cidA, "Old A Db", "Old A Prev", 10, "[\"file:///d%3A/Work/P1\"]",
                    "CASCADE_RUN_STATUS_IDLE", "p1", "", "", 0, 0, rawDbA);
                InsertSummaryDbRow(db, cidB, "Old B Db", "Old B Prev", 20, "[\"file:///d%3A/Work/P1\"]",
                    "CASCADE_RUN_STATUS_IDLE", "p1", "", "", 0, 0, rawDbB);
                InsertSummaryDbRow(db, cidC, "Old C Running", "Old C Prev", 5, "[\"file:///d%3A/Work/P1\"]",
                    "CASCADE_RUN_STATUS_RUNNING", "p1", "", "", 0, 1, rawDbC);
                InsertFtsDbRow(db, cidA, -1, "Old A Db", "file:///d%3A/Work/P1", "P1", "", "");
                InsertFtsDbRow(db, cidB, -1, "Old B Db", "file:///d%3A/Work/P1", "P1", "", "");
                InsertFtsDbRow(db, cidC, -1, "Old C Running", "file:///d%3A/Work/P1", "P1", "", "");
                t_sqlite3_close(db);
                db = IntPtr.Zero;

                ProtoNode nodeA = BuildRealProtoEntry(cidA, "Old A Pb", null, "file:///d%3A/Work/P1", "P1", "p1", 10, 2, 1);
                ProtoNode nodeB = BuildRealProtoEntry(cidB, "Old B Auto", "Old B Custom", "file:///d%3A/Work/P1", "P1", "p1", 20, 4, 1);
                ProtoNode nodeC = BuildRealProtoEntry(cidC, "Old C Running", null, "file:///d%3A/Work/P1", "P1", "p1", 5, 1, 1);
                string pbPath = Path.Combine(antigravityDir, "agyhub_summaries_proto.pb");
                File.WriteAllBytes(pbPath, ProtoCodec.Encode(new List<ProtoNode> { nodeA, nodeB, nodeC }));

                // 1. Run RenameSessionsBatch with allowHotWriteSkipRunning = true
                Dictionary<string, string> batch = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                batch[cidA] = "【P1】Renamed A";
                batch[cidB] = "【P1】Renamed B";
                batch[cidC] = "【P1】Should Skip Running C";

                RenameBatchResult renameRes;
                bool batchOk = LocalWorkspaceManager.RenameSessionsBatch(geminiDir, batch, true, out renameRes);
                AssertTrue(batchOk, "RenameSessionsBatch succeeded");
                AssertEqual(2, renameRes.RenamedCount, "Renamed A & B while skipping running C");
                AssertTrue(renameRes.Layer1Updated && renameRes.Layer2Updated && renameRes.Layer3Updated, "All 3 layers updated");
                AssertEqual(1, renameRes.SkippedRunningIds.Count, "1 running session skipped");
                AssertTrue(string.Equals(renameRes.SkippedRunningIds[0], cidC, StringComparison.OrdinalIgnoreCase), "SkippedRunningIds contains cidC");

                // Check L1 (.pbtxt): A keeps pinned:true, B created without BOM, C not created
                string postPbtxtA = File.ReadAllText(Path.Combine(annotDir, cidA + ".pbtxt"), Encoding.UTF8);
                AssertEqual("【P1】Renamed A", PbtxtAnnotationCodec.ExtractTitle(postPbtxtA), "L1 A renamed");
                AssertTrue(postPbtxtA.Contains("pinned:true"), "L1 A preserved pinned:true");
                byte[] bytesB = File.ReadAllBytes(Path.Combine(annotDir, cidB + ".pbtxt"));
                AssertTrue(!(bytesB.Length >= 3 && bytesB[0] == 0xEF && bytesB[1] == 0xBB && bytesB[2] == 0xBF), "L1 B created without BOM");
                AssertEqual("【P1】Renamed B", PbtxtAnnotationCodec.ExtractTitle(Encoding.UTF8.GetString(bytesB)), "L1 B renamed");
                AssertTrue(!File.Exists(Path.Combine(annotDir, cidC + ".pbtxt")), "L1 C not touched");

                // Check L2 (.db) & L3 (.pb) & manifest exclusion of C
                Dictionary<string, SummaryDbRowInfo> dbPost = SummarySqliteBridge.ReadSummaryDbSnapshot(dbPath);
                AssertEqual("【P1】Renamed A", dbPost[cidA].Title, "L2 A renamed");
                AssertEqual("【P1】Renamed B", dbPost[cidB].Title, "L2 B renamed");
                AssertEqual("Old C Running", dbPost[cidC].Title, "L2 C untouched");

                List<ConversationSummaryInfo> pbPost = SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(pbPath));
                AssertEqual("【P1】Renamed A", pbPost.Find(x => x.ConversationId == cidA).DisplayTitle, "L3 A renamed");
                AssertEqual("【P1】Renamed B", pbPost.Find(x => x.ConversationId == cidB).DisplayTitle, "L3 B renamed");
                AssertEqual("Old C Running", pbPost.Find(x => x.ConversationId == cidC).DisplayTitle, "L3 C untouched");

                string manifestRaw = File.ReadAllText(Path.Combine(renameRes.BackupSnapshotDir, "rename_manifest.json"), Encoding.UTF8);
                AssertTrue(manifestRaw.Contains(cidA) && manifestRaw.Contains(cidB) && !manifestRaw.Contains(cidC),
                    "rename_manifest.json includes A & B and excludes skipped running C");

                string snapFolderName = Path.GetFileName(renameRes.BackupSnapshotDir);

                // Helper to append post-rename Session D into both .pb and .db of a sandbox
                Action<string> appendSessionD = delegate(string targetGeminiDir)
                {
                    string targetAgDir = Path.Combine(targetGeminiDir, "antigravity");
                    string targetPb = Path.Combine(targetAgDir, "agyhub_summaries_proto.pb");
                    string targetDb = Path.Combine(targetAgDir, "conversation_summaries.db");

                    List<ProtoNode> nodes = ProtoCodec.Decode(File.ReadAllBytes(targetPb));
                    nodes.Add(BuildRealProtoEntry(cidD, "Post-Rename Session D", null, "file:///d%3A/Work/P1", "P1", "p1", 3, 1, 1));
                    File.WriteAllBytes(targetPb, ProtoCodec.Encode(nodes));

                    byte[] rawD = BuildRawSummaryMetaBytes("Post-Rename Session D", null, null, 3, null);
                    List<string> w;
                    int cnt = SummarySqliteBridge.UpsertSessionsInSummaryDb(targetDb, new List<SummaryDbUpsertItem>
                    {
                        new SummaryDbUpsertItem
                        {
                            ConversationId = cidD,
                            Title = "Post-Rename Session D",
                            Preview = "D Preview",
                            StepCount = 3,
                            WorkspaceUrisJson = "[\"file:///d%3A/Work/P1\"]",
                            ProjectId = "p1",
                            RawSummaryBytes = rawD
                        }
                    }, out w);
                    AssertEqual(1, cnt, "Appended post-rename Session D to DB");
                };

                // 2a. Rollback Sub-Sandbox 1: Session D added AFTER rename; Session B still exists -> deletes B's created .pbtxt
                string geminiKeepB = Path.Combine(sb, "rb_keep_b", ".gemini");
                CopyDirectoryRecursive(geminiDir, geminiKeepB);
                appendSessionD(geminiKeepB);
                string snapKeepB = Path.Combine(RenameBackupManager.GetBackupRoot(geminiKeepB), snapFolderName);

                RenameRollbackResult rbRes1;
                bool rbOk1 = RenameBackupManager.RollbackSnapshot(geminiKeepB, snapKeepB, false, out rbRes1);
                AssertTrue(rbOk1 && rbRes1.Success, "RollbackSnapshot (keep B) succeeded: " + rbRes1.ErrorMessage);
                AssertEqual(1, rbRes1.RestoredPbtxtCount, "Restored A's .pbtxt");
                AssertEqual(1, rbRes1.DeletedPbtxtCount, "Deleted B's newly created .pbtxt because had_pbtxt == false");
                AssertEqual(0, rbRes1.SkippedOrphanPbtxtCount, "0 skipped orphan pbtxt when B still exists");

                string rbPbtxtA = File.ReadAllText(Path.Combine(geminiKeepB, "antigravity", "annotations", cidA + ".pbtxt"), Encoding.UTF8);
                AssertEqual(origPbtxtA, rbPbtxtA, "L1 A restored verbatim with pinned:true");
                AssertTrue(!File.Exists(Path.Combine(geminiKeepB, "antigravity", "annotations", cidB + ".pbtxt")), "L1 B's created .pbtxt deleted on rollback");

                Dictionary<string, SummaryDbRowInfo> rbDb1 = SummarySqliteBridge.ReadSummaryDbSnapshot(Path.Combine(geminiKeepB, "antigravity", "conversation_summaries.db"));
                AssertEqual("Old A Db", rbDb1[cidA].Title, "L2 A restored");
                AssertTrue(ByteArraysEqual(rawDbA, rbDb1[cidA].RawSummaryBytes), "L2 A raw_summary restored byte-for-byte");
                AssertEqual("Old A Db", QuerySingleText(Path.Combine(geminiKeepB, "antigravity", "conversation_summaries.db"),
                    "SELECT title FROM conversations_fts WHERE conversation_id = ? AND chunk_index = -1;", cidA), "L2 A FTS chunk_index=-1 title restored");
                AssertEqual("Old B Db", rbDb1[cidB].Title, "L2 B restored");
                AssertEqual("Post-Rename Session D", rbDb1[cidD].Title, "Post-rename Session D preserved intact in .db");

                List<ProtoNode> rbPbNodes1 = ProtoCodec.Decode(File.ReadAllBytes(Path.Combine(geminiKeepB, "antigravity", "agyhub_summaries_proto.pb")));
                AssertEqual(4, rbPbNodes1.Count, "Post-rename Session D preserved in .pb (4 total entries)");
                ProtoNode rbNodeA = rbPbNodes1[0];
                ProtoCodec.DecodeSummaryEntryTree(rbNodeA);
                ProtoNode rbMetaA = rbNodeA.GetChild(2);
                AssertEqual("Old A Pb", rbMetaA.GetChild(1).GetString(), "L3 A Field 1 restored");
                AssertTrue(rbMetaA.GetChild(15) == null, "L3 A Field 15 removed on rollback because had_pb_f15 == false");

                List<ConversationSummaryInfo> rbParsed1 = SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(Path.Combine(geminiKeepB, "antigravity", "agyhub_summaries_proto.pb")));
                AssertEqual("Old B Custom", rbParsed1.Find(x => x.ConversationId == cidB).DisplayTitle, "L3 B Field 15 custom title restored");
                AssertEqual("Post-Rename Session D", rbParsed1.Find(x => x.ConversationId == cidD).DisplayTitle, "Post-rename Session D preserved intact in .pb");

                // 2b. Rollback Sub-Sandbox 2: Session D added AND Session B deleted AFTER rename -> must NOT recreate orphan .pbtxt for B
                string geminiDelB = Path.Combine(sb, "rb_del_b", ".gemini");
                CopyDirectoryRecursive(geminiDir, geminiDelB);
                appendSessionD(geminiDelB);
                LocalWorkspaceManager.DeleteSessionsAndProjects(
                    geminiDelB,
                    SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(Path.Combine(geminiDelB, "antigravity", "agyhub_summaries_proto.pb"))),
                    null,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { cidB },
                    null);
                AssertTrue(!File.Exists(Path.Combine(geminiDelB, "antigravity", "annotations", cidB + ".pbtxt")), "Precondition: B deleted");

                string snapDelB = Path.Combine(RenameBackupManager.GetBackupRoot(geminiDelB), snapFolderName);
                RenameRollbackResult rbRes2;
                bool rbOk2 = RenameBackupManager.RollbackSnapshot(geminiDelB, snapDelB, false, out rbRes2);
                AssertTrue(rbOk2 && rbRes2.Success, "RollbackSnapshot (deleted B) succeeded: " + rbRes2.ErrorMessage);
                AssertEqual(1, rbRes2.SkippedOrphanPbtxtCount, "Skipped recreating .pbtxt for deleted Session B");
                AssertTrue(!File.Exists(Path.Combine(geminiDelB, "antigravity", "annotations", cidB + ".pbtxt")), "Did NOT recreate orphan .pbtxt for deleted Session B");
                AssertEqual("Old A Db", SummarySqliteBridge.ReadSummaryDbSnapshot(Path.Combine(geminiDelB, "antigravity", "conversation_summaries.db"))[cidA].Title, "Session A restored in DB");
                AssertTrue(SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(Path.Combine(geminiDelB, "antigravity", "agyhub_summaries_proto.pb"))).Exists(x => x.ConversationId == cidD),
                    "Post-rename Session D still intact in .pb after rollback");

                // 3. ScanOrphanedData union truth-set (.pb U .db) protects DB-only session, flags true UUID orphan .pbtxt, ignores non-UUID files
                string geminiOrphan = Path.Combine(sb, "orphan_gc", ".gemini");
                string orphAg = Path.Combine(geminiOrphan, "antigravity");
                string orphAnnot = Path.Combine(orphAg, "annotations");
                string orphConv = Path.Combine(orphAg, "conversations");
                string orphBrain = Path.Combine(orphAg, "brain");
                Directory.CreateDirectory(orphAnnot);
                Directory.CreateDirectory(orphConv);
                Directory.CreateDirectory(orphBrain);

                string cidActivePb = "55555555-5555-4555-8555-555555555555";
                string cidDbOnly = "66666666-6666-4666-8666-666666666666";
                string cidTrueOrphan = "77777777-7777-4777-8777-777777777777";

                File.WriteAllBytes(Path.Combine(orphAg, "agyhub_summaries_proto.pb"),
                    ProtoCodec.Encode(new List<ProtoNode> { BuildRealProtoEntry(cidActivePb, "Active PB", null, "file:///d%3A/P", "P", "p", 1, 1, 1) }));

                string orphDbPath = Path.Combine(orphAg, "conversation_summaries.db");
                db = OpenAndCreateSummaryWalDb(orphDbPath);
                InsertSummaryDbRow(db, cidDbOnly, "DB Only Session", "Prev", 4, "[]", "CASCADE_RUN_STATUS_IDLE", "p", "", "", 0, 0, rawDbA);
                t_sqlite3_close(db);
                db = IntPtr.Zero;

                string dbOnlyPbtxt = Path.Combine(orphAnnot, cidDbOnly + ".pbtxt");
                string dbOnlyConvDb = Path.Combine(orphConv, cidDbOnly + ".db");
                string dbOnlyBrain = Path.Combine(orphBrain, cidDbOnly);
                Directory.CreateDirectory(dbOnlyBrain);
                File.WriteAllText(dbOnlyPbtxt, "title:\"DB Only\"", Encoding.UTF8);
                File.WriteAllText(dbOnlyConvDb, "db-bytes", Encoding.UTF8);
                File.WriteAllText(Path.Combine(dbOnlyBrain, "note.md"), "keep", Encoding.UTF8);

                string orphanPbtxt = Path.Combine(orphAnnot, cidTrueOrphan + ".pbtxt");
                string nonUuidPbtxt = Path.Combine(orphAnnot, "not-a-uuid-notes.pbtxt");
                File.WriteAllText(orphanPbtxt, "title:\"Orphan\"", Encoding.UTF8);
                File.WriteAllText(nonUuidPbtxt, "title:\"Non-UUID\"", Encoding.UTF8);

                OrphanScanResult scan = LocalWorkspaceManager.ScanOrphanedData(geminiOrphan);
                AssertEqual(1, scan.OrphanAnnotationFiles.Count, "Flags exact 1 true orphan UUID .pbtxt");
                AssertTrue(string.Equals(Path.GetFullPath(orphanPbtxt), Path.GetFullPath(scan.OrphanAnnotationFiles[0]), StringComparison.OrdinalIgnoreCase),
                    "Flagged file is cidTrueOrphan.pbtxt");
                AssertEqual(0, scan.OrphanDbFiles.Count, "DB-only session .db protected by union truth-set");
                AssertEqual(0, scan.OrphanBrainDirs.Count, "DB-only session brain dir protected by union truth-set");
                AssertTrue(!scan.OrphanSessionIds.Contains(cidDbOnly), "cidDbOnly not marked orphan");
                AssertTrue(scan.OrphanSessionIds.Contains(cidTrueOrphan), "cidTrueOrphan marked orphan");

                DeletionResult gcRes = LocalWorkspaceManager.CleanOrphanedData(scan);
                AssertEqual(1, gcRes.DeletedAnnotationsCount, "Deleted 1 orphan .pbtxt");
                AssertTrue(!File.Exists(orphanPbtxt), "True orphan .pbtxt deleted");
                AssertTrue(File.Exists(dbOnlyPbtxt) && File.Exists(dbOnlyConvDb) && Directory.Exists(dbOnlyBrain), "DB-only session assets preserved");
                AssertTrue(File.Exists(nonUuidPbtxt), "Non-UUID .pbtxt file ignored and preserved");
            }
            finally
            {
                if (db != IntPtr.Zero) try { t_sqlite3_close(db); } catch {}
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T36_AiPolishExchangeAndAgyAnnotationRoundTrip()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_polish_agy_" + Guid.NewGuid().ToString("N"));
            string zipAnnot = Path.Combine(Path.GetTempPath(), "agy_annot_rt_" + Guid.NewGuid().ToString("N") + ".agy");
            string zipLegacy = Path.Combine(Path.GetTempPath(), "agy_legacy_rt_" + Guid.NewGuid().ToString("N") + ".agy");
            try
            {
                // 1. AiPolishPromptExchange.ParseAiClipboardJson
                string cidUnique = "12345678-aaaa-4bbb-8ccc-111111111111";
                string cidAmb1 = "abcdef99-1111-4111-8111-111111111111";
                string cidAmb2 = "abcdef99-2222-4222-8222-222222222222";
                List<SessionRenameCandidate> rows = new List<SessionRenameCandidate>
                {
                    new SessionRenameCandidate { ConversationId = cidUnique, ProjectName = "Inverter", OldTitle = "Old1", SuggestedPrefix = "【3KU】" },
                    new SessionRenameCandidate { ConversationId = cidAmb1, ProjectName = "EvoKernel", OldTitle = "Old2", SuggestedPrefix = "【Evo】" },
                    new SessionRenameCandidate { ConversationId = cidAmb2, ProjectName = "EvoKernel", OldTitle = "Old3", SuggestedPrefix = "【Evo】" }
                };

                string prompt = AiPolishPromptExchange.BuildPolishPrompt(rows);
                AssertTrue(prompt.Contains(cidUnique) && prompt.Contains("```json"), "BuildPolishPrompt generates fenced JSON prompt");

                string fencedClipboard =
                    "Here is the result:\n" +
                    "```json\n" +
                    "{\"items\": [{\"id\": \"12345678\", \"new_title\": \"【3KU】短ID匹配\\n单行归整\"}]}\n" +
                    "```\n";
                Dictionary<string, string> parsed;
                string err;
                bool okValid = AiPolishPromptExchange.ParseAiClipboardJson(fencedClipboard, rows, out parsed, out err);
                AssertTrue(okValid, "ParseAiClipboardJson strips ```json fence and resolves unambiguous 8-char ID: " + err);
                AssertEqual("【3KU】短ID匹配 单行归整", parsed[cidUnique], "Resolves full UUID and normalizes newlines");

                string ambiguousClipboard = "[{\"id\": \"abcdef99\", \"new_title\": \"【Evo】歧义ID\"}]";
                bool okAmb = AiPolishPromptExchange.ParseAiClipboardJson(ambiguousClipboard, rows, out parsed, out err);
                AssertTrue(!okAmb && !string.IsNullOrEmpty(err), "Rejects ambiguous 8-char short ID cleanly");

                string emptyTitleClipboard = "[{\"id\": \"12345678\", \"new_title\": \"  \\r\\n  \"}]";
                bool okEmpty = AiPolishPromptExchange.ParseAiClipboardJson(emptyTitleClipboard, rows, out parsed, out err);
                AssertTrue(!okEmpty && !string.IsNullOrEmpty(err), "Rejects empty/whitespace new_title cleanly");

                string shortIdClipboard = "[{\"id\": \"1234567\", \"new_title\": \"Too Short\"}]";
                bool okShort = AiPolishPromptExchange.ParseAiClipboardJson(shortIdClipboard, rows, out parsed, out err);
                AssertTrue(!okShort && !string.IsNullOrEmpty(err), "Rejects <8-char short ID cleanly");

                // 2. ExportSelectiveBackup -> StreamingZipIndexReader.ReadIndex with annotations/*.pbtxt & legacy .agy without annotations/
                string homeDir = Path.Combine(sb, "UserHome");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                string annotDir = Path.Combine(agDir, "annotations");
                string convDir = Path.Combine(agDir, "conversations");
                string projDir = Path.Combine(geminiDir, "config", "projects");
                Directory.CreateDirectory(annotDir);
                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(projDir);

                string cidWithAnnot = "88888888-1111-4111-8111-111111111111";
                string cidLegacy = "99999999-2222-4222-8222-222222222222";
                string projId = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";

                File.WriteAllText(Path.Combine(projDir, projId + ".json"),
                    MainForm.GenerateStandardProjectJson(projId, "Inverter", "D:\\Work\\Inverter"), Encoding.UTF8);

                LocalWorkspaceManager.WriteUtf8TextAtomicWithBackup(
                    Path.Combine(annotDir, cidWithAnnot + ".pbtxt"),
                    "title:\"\\343\\200\\2203KU\\343\\200\\221 \\346\\240\\207\\346\\263\\250\\346\\240\\207\\351\\242\\230\" pinned:true",
                    false, null);

                File.WriteAllText(Path.Combine(convDir, cidWithAnnot + ".db"), "dummy-db-1", Encoding.UTF8);
                File.WriteAllText(Path.Combine(convDir, cidLegacy + ".db"), "dummy-db-2", Encoding.UTF8);

                ProtoNode pbNodeAnnot = BuildRealProtoEntry(cidWithAnnot, "Auto In PB", null, "file:///d%3A/Work/Inverter", "Inverter", projId, 6, 2, 1);
                ProtoNode pbNodeLegacy = BuildRealProtoEntry(cidLegacy, "Auto Legacy", "Legacy PB F15 Title", "file:///d%3A/Work/Inverter", "Inverter", projId, 8, 3, 1);
                byte[] pbBytes = ProtoCodec.Encode(new List<ProtoNode> { pbNodeAnnot, pbNodeLegacy });
                File.WriteAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb"), pbBytes);

                List<ConversationSummaryInfo> allSum = SessionMetadataExtractor.ExtractSummaries(pbBytes, null);
                ConversationSummaryInfo sAnnot = allSum.Find(x => x.ConversationId == cidWithAnnot);
                ConversationSummaryInfo sLegacy = allSum.Find(x => x.ConversationId == cidLegacy);

                int packedAnnot = LocalWorkspaceManager.ExportSelectiveBackup(
                    geminiDir, homeDir, zipAnnot, new List<ConversationSummaryInfo> { sAnnot }, null, false, null);
                AssertTrue(packedAnnot >= 4, "ExportSelectiveBackup packed .pb, .db, project, and .pbtxt");

                using (ZipArchive za = ZipFile.OpenRead(zipAnnot))
                {
                    AssertTrue(za.GetEntry("data/antigravity/annotations/" + cidWithAnnot + ".pbtxt") != null,
                        "Archive contains data/antigravity/annotations/<cid>.pbtxt");
                }

                BackupArchiveIndex idxAnnot = StreamingZipIndexReader.ReadIndex(zipAnnot);
                AssertEqual(1, idxAnnot.Conversations.Count, "ReadIndex reads 1 conversation from annotation archive");
                AssertEqual("【3KU】 标注标题", idxAnnot.Conversations[0].DisplayTitle, "ReadIndex overlays .pbtxt custom title over .pb auto title");
                AssertTrue(idxAnnot.Conversations[0].HasCustomTitle, "ReadIndex marks HasCustomTitle = true from .pbtxt");
                AssertEqual(0, idxAnnot.Capabilities.Count, "annotations/*.pbtxt NOT misclassified as Capabilities");
                AssertTrue(!idxAnnot.HasRules && !idxAnnot.HasSkills && !idxAnnot.HasMcp, "No false capability flags from annotations/*.pbtxt");

                int packedLegacy = LocalWorkspaceManager.ExportSelectiveBackup(
                    geminiDir, homeDir, zipLegacy, new List<ConversationSummaryInfo> { sLegacy }, null, false, null);
                AssertTrue(packedLegacy >= 3, "ExportSelectiveBackup packed legacy session without annotations/");

                BackupArchiveIndex idxLegacy = StreamingZipIndexReader.ReadIndex(zipLegacy);
                AssertEqual(1, idxLegacy.Conversations.Count, "Legacy .agy lacking annotations/ still reads cleanly");
                AssertEqual("Legacy PB F15 Title", idxLegacy.Conversations[0].DisplayTitle, "Legacy .agy falls back to .pb Field 15 custom title");
                AssertEqual(0, idxLegacy.Capabilities.Count, "Legacy .agy has 0 capabilities");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
                if (File.Exists(zipAnnot)) File.Delete(zipAnnot);
                if (File.Exists(zipLegacy)) File.Delete(zipLegacy);
            }
        }

        private static ProtoNode BuildField17BinarySubmsgEntry(
            string cid,
            string autoTitle,
            byte[] rawField17Child1Bytes,
            string projId,
            int steps)
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

            ProtoNode cascade = new ProtoNode(17, 2) { IsModified = true };
            if (rawField17Child1Bytes != null)
                cascade.Children.Add(new ProtoNode(1, 2) { RawBytes = rawField17Child1Bytes, IsModified = true });
            if (!string.IsNullOrEmpty(projId))
                cascade.Children.Add(new ProtoNode(18, 2) { RawBytes = Encoding.UTF8.GetBytes(projId), IsModified = true });
            meta.Children.Add(cascade);

            entry.Children.Add(meta);
            entry.IsModified = true;
            return entry;
        }

        private static byte[] BuildRawSummaryWithField9To4Branches(string autoTitle, string repoUri, string branchPrimary, string branchSecondary, int steps)
        {
            List<ProtoNode> nodes = new List<ProtoNode>();
            if (!string.IsNullOrEmpty(autoTitle))
                nodes.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(autoTitle), IsModified = true });
            nodes.Add(new ProtoNode(2, 0) { VarintValue = (ulong)steps, IsModified = true });

            if (!string.IsNullOrEmpty(branchPrimary))
            {
                ProtoNode ws1 = new ProtoNode(9, 2) { IsModified = true };
                if (!string.IsNullOrEmpty(repoUri))
                {
                    ws1.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(repoUri), IsModified = true });
                    ProtoNode repo1 = new ProtoNode(3, 2) { IsModified = true };
                    repo1.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(repoUri), IsModified = true });
                    ws1.Children.Add(repo1);
                }
                ws1.Children.Add(new ProtoNode(4, 2) { RawBytes = Encoding.UTF8.GetBytes(branchPrimary), IsModified = true });
                nodes.Add(ws1);
            }

            if (!string.IsNullOrEmpty(branchSecondary))
            {
                ProtoNode ws2 = new ProtoNode(9, 2) { IsModified = true };
                ws2.Children.Add(new ProtoNode(4, 2) { RawBytes = Encoding.UTF8.GetBytes(branchSecondary), IsModified = true });
                nodes.Add(ws2);
            }

            return ProtoCodec.Encode(nodes);
        }

        private sealed class RawHttpTestResponse
        {
            public int StatusCode;
            public Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public string SetCookie = string.Empty;
            public string BodyText = string.Empty;
            public int BodyByteCount;
        }

        private static RawHttpTestResponse SendRawTcpHttp(
            int port,
            string hostHeader,
            string method,
            string pathAndQuery,
            Dictionary<string, string> extraHeaders,
            string jsonBody)
        {
            byte[] bodyBytes = string.IsNullOrEmpty(jsonBody) ? new byte[0] : Encoding.UTF8.GetBytes(jsonBody);
            StringBuilder req = new StringBuilder();
            req.AppendFormat("{0} {1} HTTP/1.1\r\n", method, pathAndQuery);
            req.AppendFormat("Host: {0}\r\n", string.IsNullOrEmpty(hostHeader) ? ("127.0.0.1:" + port) : hostHeader);
            if (extraHeaders != null)
            {
                foreach (KeyValuePair<string, string> kvp in extraHeaders)
                    req.AppendFormat("{0}: {1}\r\n", kvp.Key, kvp.Value);
            }
            if (bodyBytes.Length > 0)
            {
                req.Append("Content-Type: application/json; charset=utf-8\r\n");
                req.AppendFormat("Content-Length: {0}\r\n", bodyBytes.Length);
            }
            req.Append("Connection: close\r\n\r\n");

            byte[] hdrBytes = Encoding.ASCII.GetBytes(req.ToString());
            using (TcpClient client = new TcpClient())
            {
                client.ReceiveTimeout = 10000;
                client.SendTimeout = 10000;
                client.Connect(IPAddress.Loopback, port);
                using (NetworkStream ns = client.GetStream())
                {
                    ns.Write(hdrBytes, 0, hdrBytes.Length);
                    if (bodyBytes.Length > 0) ns.Write(bodyBytes, 0, bodyBytes.Length);
                    ns.Flush();

                    using (MemoryStream ms = new MemoryStream())
                    {
                        byte[] buf = new byte[8192];
                        int r;
                        while ((r = ns.Read(buf, 0, buf.Length)) > 0)
                            ms.Write(buf, 0, r);

                        byte[] rawAll = ms.ToArray();
                        int sepIdx = -1;
                        for (int i = 0; i <= rawAll.Length - 4; i++)
                        {
                            if (rawAll[i] == 13 && rawAll[i + 1] == 10 && rawAll[i + 2] == 13 && rawAll[i + 3] == 10)
                            {
                                sepIdx = i;
                                break;
                            }
                        }
                        if (sepIdx < 0) throw new Exception("Invalid raw HTTP response (no CRLFCRLF)");

                        string headerText = Encoding.ASCII.GetString(rawAll, 0, sepIdx);
                        string[] lines = headerText.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                        RawHttpTestResponse resp = new RawHttpTestResponse();
                        string[] statusParts = lines[0].Split(' ');
                        resp.StatusCode = statusParts.Length >= 2 ? int.Parse(statusParts[1]) : 0;

                        for (int i = 1; i < lines.Length; i++)
                        {
                            int colon = lines[i].IndexOf(':');
                            if (colon <= 0) continue;
                            string k = lines[i].Substring(0, colon).Trim();
                            string v = lines[i].Substring(colon + 1).Trim();
                            resp.Headers[k] = v;
                            if (string.Equals(k, "Set-Cookie", StringComparison.OrdinalIgnoreCase))
                                resp.SetCookie = v;
                        }

                        int bodyStart = sepIdx + 4;
                        resp.BodyByteCount = rawAll.Length - bodyStart;
                        resp.BodyText = resp.BodyByteCount > 0 ? Encoding.UTF8.GetString(rawAll, bodyStart, resp.BodyByteCount) : string.Empty;
                        return resp;
                    }
                }
            }
        }

        private static void T37_ProjectAttribution_Field17Submessage_SqliteBackfill_MultiRootUris()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t37_" + Guid.NewGuid().ToString("N"));
            IntPtr db = IntPtr.Zero;
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                string projDir = Path.Combine(geminiDir, "config", "projects");
                Directory.CreateDirectory(agDir);
                Directory.CreateDirectory(projDir);

                string projInvId = "11111111-aaaa-4aaa-8aaa-111111111111";
                string multiProjJson = "{\r\n" +
                    "  \"projectId\": \"" + projInvId + "\",\r\n" +
                    "  \"name\": \"Inverter\",\r\n" +
                    "  \"folderUri\": \"file:///d%3A/Work/RootPrimary\",\r\n" +
                    "  \"folderUris\": [\"file:///d%3A/Work/RootPrimary\", \"file:///d%3A/Work/RootSecondary\"]\r\n" +
                    "}";
                File.WriteAllText(Path.Combine(projDir, projInvId + ".json"), multiProjJson, Encoding.UTF8);

                string cidBinF17 = "aaaa1111-1111-4111-8111-111111111111";
                string cidMultiRoot = "bbbb2222-2222-4222-8222-222222222222";
                string cidSqlBackfill = "cccc3333-3333-4333-8333-333333333333";

                byte[] garbageSubmsgBytes = new byte[] { 0x0A, 0x04, 0xFF, 0xFE, 0x01, 0x80 };
                ProtoNode eBin = BuildField17BinarySubmsgEntry(cidBinF17, "Binary F17 Session", garbageSubmsgBytes, projInvId, 5);
                ProtoNode eMulti = BuildRealProtoEntry(cidMultiRoot, "Multi Root Session", null, "file:///d%3A/Work/RootSecondary", null, null, 7, 2, 1);
                ProtoNode eBackfill = BuildRealProtoEntry(cidSqlBackfill, "SQLite Backfill Session", null, null, null, null, 4, 1, 1);

                byte[] pbBytes = ProtoCodec.Encode(new List<ProtoNode> { eBin, eMulti, eBackfill });
                File.WriteAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb"), pbBytes);

                string dbPath = Path.Combine(agDir, "conversation_summaries.db");
                byte[] rawEmpty = BuildRawSummaryMetaBytes("SQLite Backfill Session", null, null, 4, null);
                db = OpenAndCreateSummaryWalDb(dbPath);
                InsertSummaryDbRow(db, cidSqlBackfill, "SQLite Backfill Session", "Prev", 4, "[]",
                    "CASCADE_RUN_STATUS_IDLE", projInvId, "", "", 0, 0, rawEmpty);
                t_sqlite3_close(db);
                db = IntPtr.Zero;

                Dictionary<string, ProjectMetadata> localProjs;
                List<string> warnings;
                List<ConversationSummaryInfo> sums = WorkspaceService.LoadLocalThreeLayerSummaries(geminiDir, out localProjs, out warnings);

                AssertEqual(3, sums.Count, "All 3 entries parsed without IsValidCascadeContainer rejecting binary F17->1");
                AssertTrue(localProjs[projInvId].FolderUris.Count == 2, "ProjectMetadata.FolderUris retains both multi-root URIs");

                ConversationSummaryInfo sBin = sums.Find(x => x.ConversationId == cidBinF17);
                AssertTrue(sBin != null, "Binary F17 session extracted");
                AssertEqual(projInvId, sBin.ProjectId, "Preserves F18 ProjectId");
                AssertEqual("Inverter", sBin.ProjectName, "Resolves canonical ProjectName from projects JSON without garbled F17->1 bytes");

                ConversationSummaryInfo sMulti = sums.Find(x => x.ConversationId == cidMultiRoot);
                AssertTrue(sMulti != null, "Multi-root session extracted");
                AssertEqual(projInvId, sMulti.ProjectId, "Matches secondary root URI in folderUris to ProjectId");
                AssertEqual("Inverter", sMulti.ProjectName, "Matches secondary root URI to Inverter");

                ConversationSummaryInfo sSql = sums.Find(x => x.ConversationId == cidSqlBackfill);
                AssertTrue(sSql != null, "SQLite backfill session extracted");
                AssertEqual(projInvId, sSql.ProjectId, "Backfills ProjectId from SQLite row");
                AssertEqual("Inverter", sSql.ProjectName, "Backfills ProjectName after SQLite ProjectId overlay");
            }
            finally
            {
                if (db != IntPtr.Zero) try { t_sqlite3_close(db); } catch { }
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T38_RenameRules_Field9To4Branch_TrailingBranchStrip_EmbeddedBracketFold_LowValue()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t38_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(sb);
                RenameRulesConfig rules = RenameRulesConfig.CreateDefault();

                // 1. Field 9->4 multi-branch extraction + trailing '分支' stripping + model keyword matching
                byte[] rawSummary = BuildRawSummaryWithField9To4Branches(
                    "Auto", "file:///d%3A/Work/3KU", "Dev_TianKeNaNeng2分支", "Dev_Other6KU分支", 6);
                List<KeyValuePair<string, string>> repoBranches = SummarySqliteBridge.ExtractGitBranchCandidatesFromRawSummary(rawSummary);
                AssertEqual(2, repoBranches.Count, "Extracts both Field 9->4 branches cleanly");
                AssertEqual("Dev_TianKeNaNeng2分支", repoBranches[0].Value, "Primary Field 9->4 branch preserved before strip");

                ConversationSummaryInfo s1 = new ConversationSummaryInfo
                {
                    ConversationId = "10000001-1111-4111-8111-111111111111",
                    ProjectName = "Inverter",
                    DisplayTitle = "PayGo 【Dev_TianKeNaNeng2】",
                    FirstPromptSnippet = "请排查 3KU 机器的 PayGo 问题",
                    GitBranchHint = repoBranches[0].Value,
                    RepoBranchHints = repoBranches,
                    StepCount = 6
                };

                string pfx1, cln1, title1;
                AutoTitleSynthesizer.SynthesizeTitle(s1, rules, out pfx1, out cln1, out title1);
                AssertEqual("【3KU·Dev_TianKeNaNeng2】", pfx1, "Strips trailing '分支' and selects matching branch");
                AssertEqual("【3KU·Dev_TianKeNaNeng2】PayGo", title1, "Folds trailing embedded 【Dev_TianKeNaNeng2】 tag without double brackets");

                // 2. Prose emphasis brackets preserved inside title body
                ConversationSummaryInfo s2 = new ConversationSummaryInfo
                {
                    ConversationId = "10000002-1111-4111-8111-111111111111",
                    ProjectName = "INV3K_5K_VMH",
                    DisplayTitle = "排查【母线过压】问题",
                    FirstPromptSnippet = "分析母线过压",
                    StepCount = 4
                };
                string pfx2, cln2, title2;
                AutoTitleSynthesizer.SynthesizeTitle(s2, rules, out pfx2, out cln2, out title2);
                AssertEqual("【3KU】排查【母线过压】问题", title2, "Preserves prose emphasis brackets while applying alias prefix");

                // 3. Low-value / 0-step detection
                ConversationSummaryInfo sLow = new ConversationSummaryInfo
                {
                    ConversationId = "10000003-1111-4111-8111-111111111111",
                    ProjectName = "INV3K_5K_VMH",
                    DisplayTitle = "你好",
                    FirstPromptSnippet = "hi",
                    StepCount = 0
                };
                SessionRenameCandidate cLow = new SessionRenameCandidate
                {
                    ConversationId = sLow.ConversationId,
                    ProjectName = sLow.ProjectName,
                    OldTitle = sLow.DisplayTitle,
                    FirstPromptSnippet = sLow.FirstPromptSnippet,
                    StepCount = sLow.StepCount,
                    Summary = sLow
                };
                AutoTitleSynthesizer.SynthesizeCandidate(cLow, rules);
                AssertTrue(cLow.IsLowValue, "0-step generic '你好' / 'hi' marked IsLowValue");
                AssertTrue(!s1.Equals(null) && !AutoTitleSynthesizer.IsLowValueSession(s1, cln1), "Substantive 6-step session not marked low-value");

                // 4. Rule serialization & sandbox preview
                string json = AutoTitleSynthesizer.SerializeRulesToJson(rules);
                RenameRulesConfig rtCfg;
                string parseErr;
                AssertTrue(AutoTitleSynthesizer.TryParseRulesFromJson(json, out rtCfg, out parseErr), "SerializeRulesToJson round-trips cleanly: " + parseErr);
                List<RuleSandboxItem> sandbox = AutoTitleSynthesizer.PreviewRulesOnSessions(rtCfg, new List<ConversationSummaryInfo> { s1, s2, sLow }, 10);
                AssertEqual(3, sandbox.Count, "PreviewRulesOnSessions returns 3 sandbox rows");
                AssertEqual("【3KU·Dev_TianKeNaNeng2】PayGo", sandbox[0].SuggestedTitle, "Sandbox reflects synthesized title");
                AssertTrue(sandbox[2].IsLowValue, "Sandbox surfaces low-value flag");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T39_CliBrainFallback_FirstPrompt_MarkdownExport_ShortestDistinguishingPath()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t39_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string cidCli = "cli11111-2222-4333-8444-555555555555";
                string cliLogs = Path.Combine(geminiDir, "antigravity-cli", "brain", cidCli, ".system_generated", "logs");
                Directory.CreateDirectory(cliLogs);

                string jsonl =
                    "{\"type\":\"USER_INPUT\",\"content\":\"Check antigravity-cli UART handshake timeout\"}\r\n" +
                    "{\"type\":\"PLANNER_RESPONSE\",\"content\":\"Analyzed UART timer registers in CLI workspace.\"}\r\n";
                File.WriteAllText(Path.Combine(cliLogs, "transcript_full.jsonl"), jsonl, Encoding.UTF8);

                ConversationSummaryInfo cliSum = new ConversationSummaryInfo
                {
                    ConversationId = cidCli,
                    ProjectId = "default-cli-project",
                    ProjectName = "default-cli-project",
                    DisplayTitle = "CLI UART Timeout",
                    StepCount = 3,
                    UpdatedTime = DateTime.Now
                };

                List<ConversationSummaryInfo> list = new List<ConversationSummaryInfo> { cliSum };
                SessionMetadataExtractor.OverlayLocalThreeLayerMetadata(list, geminiDir, null, null);
                AssertTrue(cliSum.IsCliSession, "default-cli-project marks IsCliSession = true");
                AssertTrue(!string.IsNullOrEmpty(cliSum.FirstPromptSnippet) && cliSum.FirstPromptSnippet.Contains("UART handshake"),
                    "Extracts first prompt from antigravity-cli/brain fallback");

                bool trunc;
                string md = TranscriptMarkdownExporter.PreviewLocalSessionMarkdown(cliSum, geminiDir, 50000, out trunc);
                AssertTrue(md.Contains("Check antigravity-cli UART handshake timeout"), "Markdown preview includes CLI USER_INPUT");
                AssertTrue(md.Contains("Analyzed UART timer registers"), "Markdown preview includes CLI PLANNER_RESPONSE");

                // Shortest Distinguishing Parent Paths for duplicate basenames
                List<string> dupPaths = new List<string>
                {
                    "C:\\Users\\Admin\\Desktop\\vmh_ct3k_center_ctr\\INV3K_5K_VMH",
                    "C:\\Users\\Admin\\Desktop\\Inverter\\vmh_3ku_bak\\vmh_ct3k_center_ctr\\INV3K_5K_VMH"
                };
                List<string> labels = WorkspaceService.ComputeShortestDistinguishingLabels(dupPaths, "INV3K_5K_VMH");
                AssertEqual(2, labels.Count, "Returns distinguishing label per duplicate workspace");
                AssertEqual("vmh_ct3k_center_ctr", labels[0], "Shortest distinguishing label strips common prefix & trailing basename (1)");
                AssertEqual("Inverter/vmh_3ku_bak/vmh_ct3k_center_ctr", labels[1], "Shortest distinguishing label retains middle folder (2)");

                // BuildProjectDisplayGroups separates duplicate formal projects, multi-root flag, and AUX:CLI group
                string p1 = "p1111111-1111-4111-8111-111111111111";
                string p2 = "p2222222-2222-4222-8222-222222222222";
                Dictionary<string, ProjectMetadata> projs = new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
                projs[p1] = new ProjectMetadata
                {
                    Id = p1,
                    Name = "INV3K_5K_VMH",
                    FolderUri = LocalWorkspaceManager.ToFileUri(dupPaths[0]),
                    FolderUris = new List<string> { LocalWorkspaceManager.ToFileUri(dupPaths[0]), "file:///d%3A/Work/ExtraRoot" }
                };
                projs[p2] = new ProjectMetadata
                {
                    Id = p2,
                    Name = "INV3K_5K_VMH",
                    FolderUri = LocalWorkspaceManager.ToFileUri(dupPaths[1]),
                    FolderUris = new List<string> { LocalWorkspaceManager.ToFileUri(dupPaths[1]) }
                };

                List<ConversationSummaryInfo> allSums = new List<ConversationSummaryInfo>
                {
                    new ConversationSummaryInfo { ConversationId = "f1111111-1111-4111-8111-111111111111", ProjectId = p1, ProjectName = "INV3K_5K_VMH", FolderUri = projs[p1].FolderUri, UpdatedTime = DateTime.Now },
                    new ConversationSummaryInfo { ConversationId = "f2222222-2222-4222-8222-222222222222", ProjectId = p2, ProjectName = "INV3K_5K_VMH", FolderUri = projs[p2].FolderUri, UpdatedTime = DateTime.Now.AddMinutes(-5) },
                    cliSum
                };
                ProjectGroupsSnapshot grpSnap = WorkspaceService.BuildProjectDisplayGroups(allSums, projs, homeDir);
                AssertEqual(2, grpSnap.FormalProjectsCount, "Counts 2 distinct formal project groups despite identical basename");
                ProjectDisplayGroup auxCli = grpSnap.Groups.Find(x => x.BucketKey == "AUX:CLI");
                AssertTrue(auxCli != null && auxCli.PrimaryCount == 1, "Groups CLI sessions under AUX:CLI");
                ProjectDisplayGroup multiGrp = grpSnap.Groups.Find(x => x.ProjectId == p1);
                AssertTrue(multiGrp != null && multiGrp.IsMultiRoot && multiGrp.IsDuplicateBasename && multiGrp.ShortPath.Contains("多目录 · 2 个根目录"),
                    "Formal multi-root + duplicate group tagged accurately");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T40_DirectionalCapabilityDiff_BaselineAndUtcMtime_LineLcsDiff_ReadOnlyGc()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t40_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(sb);

                // 1. Baseline-backed directional diff
                CapabilityItem loc1 = new CapabilityItem { Kind = CapabilityKind.Skill, Id = "s-loc-new", ContentSignature = "SIG_NEW_LOCAL", LastModified = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
                CapabilityItem rem1 = new CapabilityItem { Kind = CapabilityKind.Skill, Id = "s-loc-new", ContentSignature = "SIG_BASE_1", LastModified = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc) };
                CapabilityItem loc2 = new CapabilityItem { Kind = CapabilityKind.Rule, Id = "r-rem-new", ContentSignature = "SIG_BASE_2", LastModified = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc) };
                CapabilityItem rem2 = new CapabilityItem { Kind = CapabilityKind.Rule, Id = "r-rem-new", ContentSignature = "SIG_NEW_REM", LastModified = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
                CapabilityItem loc3 = new CapabilityItem { Kind = CapabilityKind.McpServer, Id = "m-both", ContentSignature = "SIG_A", LastModified = DateTime.UtcNow };
                CapabilityItem rem3 = new CapabilityItem { Kind = CapabilityKind.McpServer, Id = "m-both", ContentSignature = "SIG_B", LastModified = DateTime.UtcNow };

                // 2. Baseline-less UTC mtime fallback
                DateTime tOld = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
                DateTime tNew = tOld.AddMinutes(5);
                CapabilityItem loc4 = new CapabilityItem { Kind = CapabilityKind.Hook, Id = "h-utc", ContentSignature = "H_L", LastModified = tOld };
                CapabilityItem rem4 = new CapabilityItem { Kind = CapabilityKind.Hook, Id = "h-utc", ContentSignature = "H_R", LastModified = tNew };

                Dictionary<string, string> baseMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                baseMap[loc1.UniqueKey] = "SIG_BASE_1";
                baseMap[loc2.UniqueKey] = "SIG_BASE_2";
                baseMap[loc3.UniqueKey] = "SIG_BASE_3";

                List<CapabilityDiffNode> diffs = VisualDiffEngine.ComputeCapabilityDiff(
                    new List<CapabilityItem> { loc1, loc2, loc3, loc4 },
                    new List<CapabilityItem> { rem1, rem2, rem3, rem4 },
                    baseMap);

                AssertEqual(CapabilityDiffDirection.LocalNewer, diffs.Find(x => x.Id == "s-loc-new").Direction, "Baseline detects LocalNewer even if remote mtime is newer");
                AssertEqual(CapabilityDiffDirection.RemoteNewer, diffs.Find(x => x.Id == "r-rem-new").Direction, "Baseline detects RemoteNewer");
                AssertEqual(CapabilityDiffDirection.BothModified, diffs.Find(x => x.Id == "m-both").Direction, "Baseline detects BothModified");
                AssertEqual(CapabilityDiffDirection.RemoteNewer, diffs.Find(x => x.Id == "h-utc").Direction, "Falls back to UTC mtime when no baseline exists");

                // 3. Line-level LCS diff + bounded truncation guard
                bool smallTrunc;
                List<TextDiffLine> lines = VisualDiffEngine.ComputeLineDiff("alpha\nbeta\ngamma", "alpha\nbeta2\ndelta\ngamma", out smallTrunc);
                AssertTrue(!smallTrunc, "Small diff is not truncated");
                AssertTrue(lines.Exists(x => x.Type == "equal" && x.Text == "alpha"), "Identifies common prefix");
                AssertTrue(lines.Exists(x => x.Type == "delete" && x.Text == "beta"), "Identifies deleted line");
                AssertTrue(lines.Exists(x => x.Type == "insert" && x.Text == "beta2"), "Identifies inserted line");

                StringBuilder bigL = new StringBuilder();
                StringBuilder bigR = new StringBuilder();
                for (int i = 0; i < 1100; i++)
                {
                    bigL.AppendLine("L_" + i);
                    bigR.AppendLine("R_" + i);
                }
                bool bigTrunc;
                List<TextDiffLine> bigLines = VisualDiffEngine.ComputeLineDiff(bigL.ToString(), bigR.ToString(), out bigTrunc);
                AssertTrue(bigTrunc && bigLines.Count > 0, "Bounded guard truncates >1000-line changed middle cleanly");

                // 4. ScanOrphanedData works with shared open handle on .pb
                string geminiDir = Path.Combine(sb, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                string annotDir = Path.Combine(agDir, "annotations");
                Directory.CreateDirectory(annotDir);
                string activeCid = "77777777-1111-4111-8111-111111111111";
                string orphanCid = "88888888-2222-4222-8222-222222222222";
                string pbPath = Path.Combine(agDir, "agyhub_summaries_proto.pb");
                File.WriteAllBytes(pbPath, ProtoCodec.Encode(new List<ProtoNode> { BuildRealProtoEntry(activeCid, "Active", null, "file:///d%3A/P", "P", "p", 1, 1, 1) }));
                File.WriteAllText(Path.Combine(annotDir, orphanCid + ".pbtxt"), "title:\"Orphan\"", Encoding.UTF8);

                using (FileStream sharedLock = new FileStream(pbPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
                {
                    OrphanScanResult scan = LocalWorkspaceManager.ScanOrphanedData(geminiDir);
                    AssertEqual(1, scan.OrphanAnnotationFiles.Count, "Read-only GC scans orphan .pbtxt while .pb is held open with ReadWrite sharing");
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T41_LocalWebHost_TcpFraming_AuthSecurity_StaticResources_AndReadApis()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t41_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                string repoDir = Path.Combine(sb, "Repo");
                string rulesDir = Path.Combine(repoDir, "rules", "_root");
                Directory.CreateDirectory(agDir);
                Directory.CreateDirectory(rulesDir);

                string cid = "c4100001-1111-4111-8111-111111111111";
                File.WriteAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb"),
                    ProtoCodec.Encode(new List<ProtoNode> { BuildRealProtoEntry(cid, "中文会话测试标题", null, "file:///d%3A/Work/ProjA", "ProjA", "pA", 5, 2, 1) }));
                File.WriteAllText(Path.Combine(geminiDir, "GEMINI.md"), "# Local Rule\nLine 1\n", Encoding.UTF8);
                File.WriteAllText(Path.Combine(rulesDir, "GEMINI.md"), "# Remote Rule\nLine 1\nLine 2\n", Encoding.UTF8);

                string token = "t41_secret_token_9876";
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, repoDir, 0, token))
                {
                    host.Start();
                    AssertTrue(host.Port > 0, "LocalWebHost binds to dynamic loopback port");

                    // 1. Missing token -> 403
                    RawHttpTestResponse unauth = SendRawTcpHttp(host.Port, null, "GET", "/", null, null);
                    AssertEqual(403, unauth.StatusCode, "Rejects GET / without token");

                    // 2. Host header rebinding check -> 403
                    RawHttpTestResponse badHost = SendRawTcpHttp(host.Port, "evil.example.com:80", "GET", "/api/overview",
                        new Dictionary<string, string> { { "X-AGY-Token", token } }, null);
                    AssertEqual(403, badHost.StatusCode, "Rejects non-loopback Host header (DNS rebinding guard)");

                    // 3. CSRF origin guard on cookie-only POST without X-AGY-Token -> 403
                    RawHttpTestResponse badOrigin = SendRawTcpHttp(host.Port, null, "POST", "/api/refresh",
                        new Dictionary<string, string> { { "Cookie", "agy_sid=" + token }, { "Origin", "https://attacker.local" } }, "{}");
                    AssertEqual(403, badOrigin.StatusCode, "Rejects POST with cookie + cross-origin Origin header");

                    // 4. Bootstrap GET /?t=token sets HttpOnly SameSite=Strict cookie & CSP headers + serves embedded SPA HTML
                    RawHttpTestResponse boot = SendRawTcpHttp(host.Port, null, "GET", "/?t=" + token, null, null);
                    AssertEqual(200, boot.StatusCode, "GET /?t=token succeeds");
                    AssertTrue(boot.SetCookie.Contains("agy_sid=" + token) && boot.SetCookie.Contains("HttpOnly") && boot.SetCookie.Contains("SameSite=Strict"),
                        "Sets SameSite=Strict HttpOnly cookie");
                    string csp;
                    AssertTrue(boot.Headers.TryGetValue("Content-Security-Policy", out csp) &&
                        csp.Contains("script-src 'self'") && csp.Contains("style-src 'self' 'unsafe-inline'"),
                        "Sends hardened CSP header allowing inline styles but blocking inline scripts");
                    AssertTrue(boot.BodyText.Contains("Antigravity Companion"), "Serves embedded index.html");

                    // 5. Cookie-only reload (F5) + static CSS/JS + /api/auth/session
                    Dictionary<string, string> cookieHdr = new Dictionary<string, string> { { "Cookie", "agy_sid=" + token } };
                    RawHttpTestResponse reload = SendRawTcpHttp(host.Port, null, "GET", "/", cookieHdr, null);
                    AssertEqual(200, reload.StatusCode, "Cookie-only reload GET / succeeds");
                    AssertEqual(200, SendRawTcpHttp(host.Port, null, "GET", "/styles.css", cookieHdr, null).StatusCode, "Cookie GET /styles.css succeeds");
                    AssertEqual(200, SendRawTcpHttp(host.Port, null, "GET", "/app.js", cookieHdr, null).StatusCode, "Cookie GET /app.js succeeds");

                    RawHttpTestResponse sessTok = SendRawTcpHttp(host.Port, null, "GET", "/api/auth/session", cookieHdr, null);
                    AssertEqual(200, sessTok.StatusCode, "GET /api/auth/session succeeds");
                    IDictionary<string, object> tokJson = MiniJson.Parse(sessTok.BodyText) as IDictionary<string, object>;
                    AssertEqual(token, MiniJson.GetString(tokJson, "token", ""), "Restores session token for X-AGY-Token header");

                    // 6. Read APIs & UTF-8 Content-Length byte exactness with Chinese characters
                    Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                    RawHttpTestResponse ov = SendRawTcpHttp(host.Port, null, "GET", "/api/overview", apiHdr, null);
                    AssertEqual(200, ov.StatusCode, "GET /api/overview succeeds");
                    AssertEqual(int.Parse(ov.Headers["Content-Length"]), ov.BodyByteCount, "Content-Length matches exact UTF-8 byte count");

                    RawHttpTestResponse sess = SendRawTcpHttp(host.Port, null, "GET", "/api/sessions", apiHdr, null);
                    AssertEqual(200, sess.StatusCode, "GET /api/sessions succeeds");
                    AssertTrue(sess.BodyText.Contains("中文会话测试标题"), "UTF-8 Chinese session title transmitted without truncation");

                    RawHttpTestResponse cands = SendRawTcpHttp(host.Port, null, "GET", "/api/rename/candidates", apiHdr, null);
                    AssertEqual(200, cands.StatusCode, "GET /api/rename/candidates succeeds");

                    RawHttpTestResponse caps = SendRawTcpHttp(host.Port, null, "GET", "/api/capabilities", apiHdr, null);
                    AssertEqual(200, caps.StatusCode, "GET /api/capabilities succeeds");

                    RawHttpTestResponse diff = SendRawTcpHttp(host.Port, null, "GET", "/api/capabilities/diff?key=Rule:GEMINI.md", apiHdr, null);
                    AssertEqual(200, diff.StatusCode, "GET /api/capabilities/diff succeeds for Rule:GEMINI.md");
                    AssertTrue(diff.BodyText.Contains("Remote Rule"), "Line diff response includes modified rule lines");

                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T42_LocalWebHost_EndToEndMutationSandbox_RenameRollbackCapabilityAndGc()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t42_" + Guid.NewGuid().ToString("N"));
            string zipPath = Path.Combine(sb, "seed_backup.agy");
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                string annotDir = Path.Combine(agDir, "annotations");
                string repoDir = Path.Combine(sb, "Repo");
                string projDir = Path.Combine(geminiDir, "config", "projects");
                Directory.CreateDirectory(annotDir);
                Directory.CreateDirectory(repoDir);
                Directory.CreateDirectory(projDir);

                string cid = "c4200001-1111-4111-8111-111111111111";
                string orphanCid = "c4200009-9999-4999-8999-999999999999";
                string projId = "p4200001-1111-4111-8111-111111111111";
                File.WriteAllText(Path.Combine(projDir, projId + ".json"),
                    WorkspaceService.GenerateStandardProjectJson(projId, "Inverter", "D:\\Work\\Inverter"), Encoding.UTF8);

                byte[] pbBytes = ProtoCodec.Encode(new List<ProtoNode>
                {
                    BuildRealProtoEntry(cid, "Original Sandbox Session", null, "file:///d%3A/Work/Inverter", "Inverter", projId, 6, 2, 1)
                });
                File.WriteAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb"), pbBytes);
                File.WriteAllText(Path.Combine(annotDir, orphanCid + ".pbtxt"), "title:\"OrphanNote\"", Encoding.UTF8);
                File.WriteAllText(Path.Combine(geminiDir, "GEMINI.md"), "# Seed Rule V1\n", Encoding.UTF8);

                List<ConversationSummaryInfo> seedList = SessionMetadataExtractor.ExtractSummariesFromProto(pbBytes);
                LocalWorkspaceManager.ExportSelectiveBackup(geminiDir, homeDir, zipPath, seedList, null, false, null);

                string token = "t42_secret_token_5432";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };

                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, repoDir, 0, token))
                {
                    host.Start();

                    // 1. Rename execute -> history -> rollback
                    string renPayload = "{\"strategy\":\"HotWritePersistentLayers\",\"items\":[{\"cid\":\"" + cid + "\",\"newTitle\":\"【3KU】Renamed Via HTTP\"}]}";
                    RawHttpTestResponse renResp = SendRawTcpHttp(host.Port, null, "POST", "/api/rename/execute", apiHdr, renPayload);
                    AssertEqual(200, renResp.StatusCode, "POST /api/rename/execute returns 200: " + renResp.BodyText);
                    IDictionary<string, object> renObj = MiniJson.Parse(renResp.BodyText) as IDictionary<string, object>;
                    AssertEqual(1, MiniJson.GetInt(renObj, "renamedCount", 0), "Renamed 1 session");

                    RawHttpTestResponse histResp = SendRawTcpHttp(host.Port, null, "GET", "/api/rename/history", apiHdr, null);
                    AssertEqual(200, histResp.StatusCode, "GET /api/rename/history returns 200");
                    IDictionary<string, object> histObj = MiniJson.Parse(histResp.BodyText) as IDictionary<string, object>;
                    List<object> snaps = MiniJson.GetList(histObj, "snapshots");
                    AssertTrue(snaps != null && snaps.Count == 1, "Snapshot history contains 1 record");
                    Dictionary<string, object> snapDict = snaps[0] as Dictionary<string, object>;
                    string snapDir = MiniJson.GetString(snapDict, "snapshotDir", "");
                    AssertTrue(!string.IsNullOrEmpty(snapDir) && MiniJson.GetString(snapDict, "previewSummary", "").Contains("Renamed Via HTTP"),
                        "History snapshot preview lists old -> new title");

                    string rbPayload = MiniJson.Serialize(new Dictionary<string, object> { { "strategy", "HotWritePersistentLayers" }, { "snapshotDir", snapDir } }, false);
                    RawHttpTestResponse rbResp = SendRawTcpHttp(host.Port, null, "POST", "/api/rename/rollback", apiHdr, rbPayload);
                    AssertEqual(200, rbResp.StatusCode, "POST /api/rename/rollback returns 200: " + rbResp.BodyText);

                    // 2. AI file prompt export & JSON load endpoints
                    string promptOut = Path.Combine(sb, "ai_prompt.txt");
                    string savePromptPayload = MiniJson.Serialize(new Dictionary<string, object> { { "cids", new List<object> { cid } }, { "outputPath", promptOut } }, false);
                    RawHttpTestResponse spResp = SendRawTcpHttp(host.Port, null, "POST", "/api/rename/ai-save-prompt", apiHdr, savePromptPayload);
                    AssertEqual(200, spResp.StatusCode, "POST /api/rename/ai-save-prompt returns 200");
                    AssertTrue(File.Exists(promptOut) && File.ReadAllText(promptOut, Encoding.UTF8).Contains(cid), "Saved AI prompt file contains cid");

                    string aiJsonIn = Path.Combine(sb, "ai_result.json");
                    File.WriteAllText(aiJsonIn, "{\"items\":[{\"id\":\"" + cid + "\",\"new_title\":\"【3KU】AI 文件回填标题\"}]}", Encoding.UTF8);
                    string loadJsonPayload = MiniJson.Serialize(new Dictionary<string, object> { { "cids", new List<object> { cid } }, { "inputPath", aiJsonIn } }, false);
                    RawHttpTestResponse ljResp = SendRawTcpHttp(host.Port, null, "POST", "/api/rename/ai-load-json", apiHdr, loadJsonPayload);
                    AssertEqual(200, ljResp.StatusCode, "POST /api/rename/ai-load-json returns 200: " + ljResp.BodyText);
                    AssertTrue(ljResp.BodyText.Contains("AI 文件回填标题"), "Parsed AI JSON file returns mapped title");

                    // 3. Capabilities Push -> Aligned -> Edit Repo -> Import
                    string capBody = "{\"keys\":[\"Rule:GEMINI.md\"]}";
                    AssertEqual(200, SendRawTcpHttp(host.Port, null, "POST", "/api/capabilities/push", apiHdr, capBody).StatusCode, "Push Rule:GEMINI.md returns 200");
                    RawHttpTestResponse capListAfterPush = SendRawTcpHttp(host.Port, null, "GET", "/api/capabilities", apiHdr, null);
                    AssertTrue(capListAfterPush.BodyText.Contains("\"direction\":\"Aligned\""), "Rule is Aligned after push");

                    string repoRulePath = Path.Combine(repoDir, "rules", "_root", "GEMINI.md");
                    File.WriteAllText(repoRulePath, "# Seed Rule V2 From Repo\n", Encoding.UTF8);
                    AssertEqual(200, SendRawTcpHttp(host.Port, null, "POST", "/api/refresh", apiHdr, "{}").StatusCode, "POST /api/refresh picks up external repo edits");
                    AssertEqual(200, SendRawTcpHttp(host.Port, null, "POST", "/api/capabilities/import", apiHdr, "{\"keys\":[\"Rule:GEMINI.md\"],\"source\":\"repo\"}").StatusCode,
                        "Import Rule:GEMINI.md from repo returns 200");
                    AssertTrue(File.ReadAllText(Path.Combine(geminiDir, "GEMINI.md"), Encoding.UTF8).Contains("V2 From Repo"),
                        "Local GEMINI.md updated from repo import");

                    // 4. GC Scan (+ conditional Clean if IDE not running on test host)
                    RawHttpTestResponse gcScanResp = SendRawTcpHttp(host.Port, null, "POST", "/api/gc/scan", apiHdr, "{}");
                    AssertEqual(200, gcScanResp.StatusCode, "POST /api/gc/scan returns 200");
                    IDictionary<string, object> gcScanObj = MiniJson.Parse(gcScanResp.BodyText) as IDictionary<string, object>;
                    AssertEqual(1, MiniJson.GetInt(gcScanObj, "totalItems", 0), "GC scan detects 1 orphan annotation");

                    RawHttpTestResponse gcCleanResp = SendRawTcpHttp(host.Port, null, "POST", "/api/gc/clean", apiHdr, "{\"closeIdeIfRunning\":false}");
                    if (ProcessGuardService.IsAntigravityRunning())
                        AssertEqual(409, gcCleanResp.StatusCode, "Refuses live GC clean while host IDE is running and closeIdeIfRunning=false");
                    else
                        AssertEqual(200, gcCleanResp.StatusCode, "Cleans orphan annotation cleanly when IDE not running");

                    // 5. Open .agy backup archive & read current backup index
                    string openBkPayload = MiniJson.Serialize(new Dictionary<string, object> { { "archivePath", zipPath } }, false);
                    RawHttpTestResponse bkOpenResp = SendRawTcpHttp(host.Port, null, "POST", "/api/backup/open", apiHdr, openBkPayload);
                    AssertEqual(200, bkOpenResp.StatusCode, "POST /api/backup/open returns 200: " + bkOpenResp.BodyText);
                    RawHttpTestResponse bkCurResp = SendRawTcpHttp(host.Port, null, "GET", "/api/backup/current", apiHdr, null);
                    AssertTrue(bkCurResp.BodyText.Contains("\"loaded\":true") && bkCurResp.BodyText.Contains("Original Sandbox Session"),
                        "GET /api/backup/current serves opened .agy summary");

                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }
    }
}
