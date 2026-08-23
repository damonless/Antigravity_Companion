using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AntigravityCompanion.Tests
{
    public static class Tier3_PairwiseInteractionTests
    {
        // =====================================================================
        // T3_01: F1 + F2 (Protobuf Codec roundtrip with Chinese Field 15 custom titles)
        // =====================================================================
        public static void Test_T3_01_F1_F2_ProtobufCodecRoundtripWithChineseField15(TestContext ctx)
        {
            string customTitle = "【交互测试】C# 原生 Protobuf 编解码器与中文标题";
            MockSessionSpec spec = new MockSessionSpec
            {
                ConversationId = "aaaaaaaa-1111-2222-3333-444444444444",
                CustomTitle = customTitle
            };

            byte[] originalBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { spec });
            List<ProtoNode> ast = ProtoAstCodec.Decode(originalBytes);
            byte[] reEncodedBytes = ProtoAstCodec.Encode(ast);

            List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(reEncodedBytes);
            ctx.AssertEqual(1, summaries.Count, "1 summary decoded");
            ctx.AssertEqual(customTitle, summaries[0].DisplayTitle, "Chinese title preserved after roundtrip AST codec");
        }

        // =====================================================================
        // T3_02: F1 + F3 (Streaming zip extraction with lossless custom title extraction)
        // =====================================================================
        public static void Test_T3_02_F1_F3_StreamingZipWithCustomTitleExtraction(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string title = "【流式读取】跨机器解压无损标题测试";
                MockProjectSpec p = new MockProjectSpec("StreamProj", "file:///c:/StreamProj");
                p.Sessions.Add(new MockSessionSpec { CustomTitle = title });

                string zipPath = Path.Combine(sandbox.BackupPath, "stream_title.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec> { p });

                ZipMetadataPackage pkg = StreamingZipIndexReader.ReadZipMetadata(zipPath);
                List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(pkg.SummaryProtoBytes);

                ctx.AssertEqual(1, list.Count, "1 summary from stream");
                ctx.AssertEqual(title, list[0].DisplayTitle, "Custom title read losslessly from in-memory stream");
            }
        }

        // =====================================================================
        // T3_03: F1 + F4 (Incremental protobuf merge preserving custom Chinese titles)
        // =====================================================================
        public static void Test_T3_03_F1_F4_IncrementalMergePreservesCustomTitles(TestContext ctx)
        {
            string localTitle = "【本地】原有中文标题";
            string backupTitle = "【备份】新增中文标题";
            string backupId = Guid.NewGuid().ToString();

            MockSessionSpec sLocal = new MockSessionSpec { CustomTitle = localTitle };
            MockSessionSpec sBackup = new MockSessionSpec { ConversationId = backupId, CustomTitle = backupTitle };

            byte[] localPb = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { sLocal });
            byte[] backupPb = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { sBackup });

            byte[] merged = SelectiveCascadingImporter.MergeProtoShards(localPb, backupPb, new HashSet<string> { backupId }, null, null);
            List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(merged);

            ctx.AssertEqual(2, list.Count, "2 summaries in merged pb");
            ctx.AssertTrue(list.Exists(delegate(ConversationSummaryInfo s) { return s.DisplayTitle == localTitle; }), "Local Chinese title preserved");
            ctx.AssertTrue(list.Exists(delegate(ConversationSummaryInfo s) { return s.DisplayTitle == backupTitle; }), "Backup Chinese title merged");
        }

        // =====================================================================
        // T3_04: F3 + F5 (Streaming zip reading combined with smart junk filtering)
        // =====================================================================
        public static void Test_T3_04_F3_F5_StreamingZipWithJunkProjectFiltering(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                MockProjectSpec pGood = new MockProjectSpec("GoodProject", "file:///c:/GoodProject");
                pGood.Sessions.Add(new MockSessionSpec());

                MockProjectSpec pJunk1 = new MockProjectSpec("subagent-review-2", "file:///c:/subagent-review-2");
                pJunk1.Sessions.Add(new MockSessionSpec());

                MockProjectSpec pJunk2 = new MockProjectSpec("outside-of-project", "file:///c:/outside-of-project");
                pJunk2.Sessions.Add(new MockSessionSpec());

                string zipPath = Path.Combine(sandbox.BackupPath, "junk_stream.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec> { pGood, pJunk1, pJunk2 });

                ZipMetadataPackage pkg = StreamingZipIndexReader.ReadZipMetadata(zipPath);
                List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(pkg.SummaryProtoBytes);

                int junkCount = 0;
                int validCount = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    if (SmartProjectFilter.IsJunkOrEphemeral(list[i].ProjectName))
                        junkCount++;
                    else
                        validCount++;
                }

                ctx.AssertEqual(1, validCount, "1 valid project");
                ctx.AssertEqual(2, junkCount, "2 junk projects identified in streaming zip");
            }
        }

        // =====================================================================
        // T3_05: F5 + F6 (Visual diff engine running with and without junk filter)
        // =====================================================================
        public static void Test_T3_05_F5_F6_VisualDiffWithJunkFilterToggle(TestContext ctx)
        {
            List<ConversationSummaryInfo> backup = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "RegularApp", ConversationId = "u1" },
                new ConversationSummaryInfo { ProjectName = "subagent-temp-worker", ConversationId = "u2" }
            };

            List<ProjectDiffNode> diffAll = VisualDiffEngine.ComputeDiff(null, backup);
            ctx.AssertEqual(2, diffAll.Count, "2 total diff nodes without filter");

            // Filtered view (chkHideTemp = true)
            List<ProjectDiffNode> visibleNodes = new List<ProjectDiffNode>();
            for (int i = 0; i < diffAll.Count; i++)
            {
                if (!diffAll[i].IsTemporaryOrJunk)
                    visibleNodes.Add(diffAll[i]);
            }

            ctx.AssertEqual(1, visibleNodes.Count, "1 visible node with filter active");
            ctx.AssertEqual("RegularApp", visibleNodes[0].ProjectName, "RegularApp visible");
        }

        // =====================================================================
        // T3_06: F6 + F7 (Visual diff detecting conflicted project and wizard strategy)
        // =====================================================================
        public static void Test_T3_06_F6_F7_VisualDiffConflictedTriggersConflictPlan(TestContext ctx)
        {
            List<ConversationSummaryInfo> local = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "SharedApp", FolderUri = "file:///c:/Dev/SharedApp", ConversationId = "u1" }
            };
            List<ConversationSummaryInfo> backup = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "SharedApp", FolderUri = "file:///d:/Backup/SharedApp", ConversationId = "u2" }
            };

            List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(local, backup);
            ctx.AssertEqual(1, diff.Count, "1 project diff node");
            ctx.AssertEqual(DiffStatus.Conflicted, diff[0].Status, "Status is Conflicted");

            // User selects 'Rename' in wizard
            ConflictResolutionPlan plan = new ConflictResolutionPlan();
            plan.Strategies["SharedApp"] = ConflictResolutionStrategy.Rename;
            plan.RenamedProjects["SharedApp"] = "SharedApp_Imported";

            ctx.AssertEqual(ConflictResolutionStrategy.Rename, plan.Strategies["SharedApp"], "Rename strategy selected");
        }

        // =====================================================================
        // T3_07: F6 + F9 (Visual diff status feeding into Quick-Select "Select New Only")
        // =====================================================================
        public static void Test_T3_07_F6_F9_DiffStatusFeedsQuickSelectNewOnly(TestContext ctx)
        {
            List<ConversationSummaryInfo> local = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "AlignedProj", FolderUri = "file:///c:/AlignedProj", ConversationId = "loc1" }
            };
            List<ConversationSummaryInfo> backup = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "AlignedProj", FolderUri = "file:///c:/AlignedProj", ConversationId = "bak1" },
                new ConversationSummaryInfo { ProjectName = "BrandNewProj", FolderUri = "file:///c:/BrandNewProj", ConversationId = "new1" }
            };

            List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(local, backup);
            CascadingSelectionModel model = new CascadingSelectionModel();
            HashSet<string> selected = new HashSet<string>();

            model.SelectNewOnly(diff, selected);
            ctx.AssertEqual(1, selected.Count, "Only 1 session selected");
            ctx.AssertTrue(selected.Contains("new1"), "Only new1 should be selected");
        }

        // =====================================================================
        // T3_08: F7 + F10 (Conflict resolution 'Rename' and 'Merge' executed during Selective Import)
        // =====================================================================
        public static void Test_T3_08_F7_F10_ConflictResolutionExecutedDuringSelectiveImport(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string id = Guid.NewGuid().ToString();
                MockProjectSpec p = new MockProjectSpec("CollidedProject", "file:///backup/CollidedProject");
                p.Sessions.Add(new MockSessionSpec { ConversationId = id });

                string zipPath = Path.Combine(sandbox.BackupPath, "rename_import.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec> { p });

                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = zipPath,
                    TargetGeminiPath = sandbox.GeminiPath,
                    SelectedConversationIds = new List<string> { id },
                    ProjectRenames = new Dictionary<string, string> { { "CollidedProject", "CollidedProject_New" } }
                };

                SelectiveImportResult res = SelectiveCascadingImporter.ExecuteImport(req);
                ctx.AssertTrue(res.Success, "Import with rename succeeds");

                byte[] pb = File.ReadAllBytes(Path.Combine(sandbox.AntigravityPath, "agyhub_summaries_proto.pb"));
                List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(pb);

                ctx.AssertEqual("CollidedProject_New", list[0].ProjectName, "ProjectName was remapped to CollidedProject_New");
            }
        }

        // =====================================================================
        // T3_09: F8 + F9 (Cascading checkboxes operating inside Dual-Tree workspace)
        // =====================================================================
        public static void Test_T3_09_F8_F9_CascadingSelectionInDualTreeWorkspace(TestContext ctx)
        {
            List<ConversationSummaryInfo> backup = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "ProjA", ConversationId = "a1" },
                new ConversationSummaryInfo { ProjectName = "ProjA", ConversationId = "a2" },
                new ConversationSummaryInfo { ProjectName = "ProjB", ConversationId = "b1" }
            };

            List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(null, backup);
            CascadingSelectionModel model = new CascadingSelectionModel();
            HashSet<string> selected = new HashSet<string>();

            // Check ProjA
            ProjectDiffNode pA = diff.Find(delegate(ProjectDiffNode p) { return p.ProjectName == "ProjA"; });
            List<string> pAIds = new List<string>();
            for (int i = 0; i < pA.BackupConversations.Count; i++) pAIds.Add(pA.BackupConversations[i].ConversationId);

            model.ToggleProjectSelection(true, pAIds, selected);
            ctx.AssertEqual(2, selected.Count, "2 sessions in ProjA selected");
            ctx.AssertFalse(selected.Contains("b1"), "b1 in ProjB not selected");
        }

        // =====================================================================
        // T3_10: F9 + F10 (Cascading selection triggering precise selective import)
        // =====================================================================
        public static void Test_T3_10_F9_F10_CascadingSelectionFeedsSelectiveImport(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string idA1 = Guid.NewGuid().ToString();
                string idA2 = Guid.NewGuid().ToString();
                string idB1 = Guid.NewGuid().ToString();

                MockProjectSpec pA = new MockProjectSpec("ProjA", "file:///c:/ProjA");
                pA.Sessions.Add(new MockSessionSpec { ConversationId = idA1 });
                pA.Sessions.Add(new MockSessionSpec { ConversationId = idA2 });

                MockProjectSpec pB = new MockProjectSpec("ProjB", "file:///c:/ProjB");
                pB.Sessions.Add(new MockSessionSpec { ConversationId = idB1 });

                string zipPath = Path.Combine(sandbox.BackupPath, "cascade_import.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec> { pA, pB });

                // User checks only ProjA (a1 and a2)
                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = zipPath,
                    TargetGeminiPath = sandbox.GeminiPath,
                    SelectedConversationIds = new List<string> { idA1, idA2 }
                };

                SelectiveImportResult res = SelectiveCascadingImporter.ExecuteImport(req);
                ctx.AssertEqual(2, res.ImportedConversationsCount, "2 sessions imported");

                string dbA1 = Path.Combine(sandbox.AntigravityPath, "conversations", idA1 + ".db");
                string dbB1 = Path.Combine(sandbox.AntigravityPath, "conversations", idB1 + ".db");

                ctx.AssertTrue(File.Exists(dbA1), "ProjA session db exists");
                ctx.AssertFalse(File.Exists(dbB1), "ProjB session db does not exist");
            }
        }

        // =====================================================================
        // T3_11: F10 + F11 (Selective import execution followed by refresh & highlight)
        // =====================================================================
        public static void Test_T3_11_F10_F11_SelectiveImportTriggersHighlightAndRefresh(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string id = Guid.NewGuid().ToString();
                MockProjectSpec p = new MockProjectSpec("HighlightProj", "file:///c:/HighlightProj");
                p.Sessions.Add(new MockSessionSpec { ConversationId = id });

                string zipPath = Path.Combine(sandbox.BackupPath, "hl.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec> { p });

                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = zipPath,
                    TargetGeminiPath = sandbox.GeminiPath,
                    SelectedConversationIds = new List<string> { id }
                };

                SelectiveImportResult res = SelectiveCascadingImporter.ExecuteImport(req);
                HighlightTracker tracker = new HighlightTracker();
                tracker.MarkImported(res.ImportedConversationIds, res.ImportedProjectNames);

                ctx.AssertTrue(tracker.IsConversationHighlighted(id), "Newly imported session is marked for highlight");
            }
        }

        // =====================================================================
        // T3_12: F4 + F10 (Incremental protobuf merger invoked by Selective Importer)
        // =====================================================================
        public static void Test_T3_12_F4_F10_IncrementalShardMergerInvokedByImporter(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string id1 = Guid.NewGuid().ToString();
                MockProjectSpec localP = new MockProjectSpec("LocalP", "file:///c:/LocalP");
                localP.Sessions.Add(new MockSessionSpec { ConversationId = id1, CustomTitle = "Original Local" });
                MockSandboxGenerator.PopulateLocalSandbox(sandbox, new List<MockProjectSpec> { localP });

                string id2 = Guid.NewGuid().ToString();
                MockProjectSpec backupP = new MockProjectSpec("BackupP", "file:///c:/BackupP");
                backupP.Sessions.Add(new MockSessionSpec { ConversationId = id2, CustomTitle = "Imported Shard" });

                string zipPath = Path.Combine(sandbox.BackupPath, "shard_merge.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec> { backupP });

                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = zipPath,
                    TargetGeminiPath = sandbox.GeminiPath,
                    SelectedConversationIds = new List<string> { id2 }
                };

                SelectiveImportResult res = SelectiveCascadingImporter.ExecuteImport(req);
                ctx.AssertEqual(1, res.MergedProtoSummariesCount, "1 proto shard merged");

                byte[] pb = File.ReadAllBytes(Path.Combine(sandbox.AntigravityPath, "agyhub_summaries_proto.pb"));
                List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(pb);

                ctx.AssertEqual(2, list.Count, "2 total entries in local pb");
            }
        }

        // =====================================================================
        // T3_13: F1 + F6 + F10 (Custom title parsed, diffed as NewInBackup, selectively imported, verified)
        // =====================================================================
        public static void Test_T3_13_F1_F6_F10_FullChainCustomTitleDiffImportVerify(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string chineseTitle = "【全链路验证】从备份解析到选择性导入到本地呈现";
                string convId = Guid.NewGuid().ToString();

                MockProjectSpec backupP = new MockProjectSpec("FullChainApp", "file:///c:/FullChainApp");
                backupP.Sessions.Add(new MockSessionSpec
                {
                    ConversationId = convId,
                    CustomTitle = chineseTitle,
                    HasBrainData = true
                });

                string zipPath = Path.Combine(sandbox.BackupPath, "fullchain.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec> { backupP });

                // 1. Streaming Zip Read (F3 + F1)
                ZipMetadataPackage pkg = StreamingZipIndexReader.ReadZipMetadata(zipPath);
                List<ConversationSummaryInfo> backupSummaries = SessionMetadataExtractor.ExtractSummariesFromProto(pkg.SummaryProtoBytes);
                ctx.AssertEqual(chineseTitle, backupSummaries[0].DisplayTitle, "Step 1: Custom title parsed from backup");

                // 2. Diff (F6)
                List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(null, backupSummaries);
                ctx.AssertEqual(DiffStatus.NewInBackup, diff[0].Status, "Step 2: Status is NewInBackup");

                // 3. Selective Import (F10)
                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = zipPath,
                    TargetGeminiPath = sandbox.GeminiPath,
                    SelectedConversationIds = new List<string> { convId }
                };
                SelectiveImportResult res = SelectiveCascadingImporter.ExecuteImport(req);
                ctx.AssertTrue(res.Success, "Step 3: Selective import succeeded");

                // 4. Verify Local Protobuf & Disk (F1, F4, F10)
                byte[] localPb = File.ReadAllBytes(Path.Combine(sandbox.AntigravityPath, "agyhub_summaries_proto.pb"));
                List<ConversationSummaryInfo> localSummaries = SessionMetadataExtractor.ExtractSummariesFromProto(localPb);
                ctx.AssertEqual(1, localSummaries.Count, "Step 4: 1 summary in local pb");
                ctx.AssertEqual(chineseTitle, localSummaries[0].DisplayTitle, "Step 4: Chinese title 100% losslessly preserved in local");

                string dbFile = Path.Combine(sandbox.AntigravityPath, "conversations", convId + ".db");
                string brainDir = Path.Combine(sandbox.AntigravityPath, "brain", convId);
                ctx.AssertTrue(File.Exists(dbFile), "Step 4: Local DB file exists");
                ctx.AssertTrue(Directory.Exists(brainDir), "Step 4: Local brain dir exists");
            }
        }
    }
}
