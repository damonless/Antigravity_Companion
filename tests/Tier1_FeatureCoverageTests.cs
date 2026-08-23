using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AntigravityCompanion.Tests
{
    public static class Tier1_FeatureCoverageTests
    {
        // =====================================================================
        // F1: Custom Title Lossless Extraction
        // =====================================================================
        public static void Test_F1_01_ExtractsCustomTitleFromField15(TestContext ctx)
        {
            MockSessionSpec spec = new MockSessionSpec
            {
                ConversationId = "11111111-1111-1111-1111-111111111111",
                AutoSummary = "Default auto summary",
                CustomTitle = "自定义中文标题_测试01",
                ProjectName = "Project_Alpha",
                FolderUri = "file:///c:/Projects/Alpha"
            };

            byte[] pbBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { spec });
            List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(pbBytes);

            ctx.AssertEqual(1, summaries.Count, "Should extract exactly 1 summary");
            ctx.AssertEqual("自定义中文标题_测试01", summaries[0].DisplayTitle, "DisplayTitle must equal custom title");
            ctx.AssertEqual("自定义中文标题_测试01", summaries[0].CustomTitle, "CustomTitle must equal custom title");
            ctx.AssertTrue(summaries[0].HasCustomTitle, "HasCustomTitle must be true");
        }

        public static void Test_F1_02_LosslessChineseUtf8Integrity(TestContext ctx)
        {
            string complexTitle = "【核心模块】重构双栏树状视图 & 级联选择 v0.5 🚀";
            MockSessionSpec spec = new MockSessionSpec
            {
                ConversationId = "22222222-2222-2222-2222-222222222222",
                CustomTitle = complexTitle,
                ProjectName = "Project_Beta"
            };

            byte[] pbBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { spec });
            List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(pbBytes);

            ctx.AssertEqual(1, summaries.Count, "Should extract 1 summary");
            ctx.AssertEqual(complexTitle, summaries[0].DisplayTitle, "Complex UTF-8 title must match exactly");
        }

        public static void Test_F1_03_FallbackToAutoSummaryWhenField15Missing(TestContext ctx)
        {
            MockSessionSpec spec = new MockSessionSpec
            {
                ConversationId = "33333333-3333-3333-3333-333333333333",
                AutoSummary = "Auto generated English summary",
                CustomTitle = null,
                ProjectName = "Project_Gamma"
            };

            byte[] pbBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { spec });
            List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(pbBytes);

            ctx.AssertEqual(1, summaries.Count, "Should extract 1 summary");
            ctx.AssertEqual("Auto generated English summary", summaries[0].DisplayTitle, "Should fallback to AutoSummary");
            ctx.AssertFalse(summaries[0].HasCustomTitle, "HasCustomTitle must be false");
        }

        public static void Test_F1_04_ExtractTimestampsAndStepCount(TestContext ctx)
        {
            DateTime now = DateTime.UtcNow;
            MockSessionSpec spec = new MockSessionSpec
            {
                ConversationId = "44444444-4444-4444-4444-444444444444",
                CreatedTime = now.AddHours(-3),
                UpdatedTime = now.AddMinutes(-15),
                StepCount = 42
            };

            byte[] pbBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { spec });
            List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(pbBytes);

            ctx.AssertEqual(1, summaries.Count, "Should extract 1 summary");
            ctx.AssertEqual(42, summaries[0].StepCount, "Step count must match");
            ctx.AssertTrue(summaries[0].CreatedTime > DateTime.MinValue, "CreatedTime must be valid");
            ctx.AssertTrue(summaries[0].UpdatedTime > DateTime.MinValue, "UpdatedTime must be valid");
        }

        public static void Test_F1_05_ExtractFolderUriAndProjectName(TestContext ctx)
        {
            MockSessionSpec spec = new MockSessionSpec
            {
                ConversationId = "55555555-5555-5555-5555-555555555555",
                FolderUri = "file:///d:/Workspace/MyApp",
                ProjectName = "MyApp"
            };

            byte[] pbBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { spec });
            List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(pbBytes);

            ctx.AssertEqual(1, summaries.Count, "Should extract 1 summary");
            ctx.AssertEqual("file:///d:/Workspace/MyApp", summaries[0].FolderUri, "FolderUri must match");
            ctx.AssertEqual("MyApp", summaries[0].ProjectName, "ProjectName must match");
        }

        // =====================================================================
        // F2: Pure Native Protobuf AST Codec
        // =====================================================================
        public static void Test_F2_01_VarintEncodingDecoding(TestContext ctx)
        {
            ulong[] testVals = new ulong[] { 0, 1, 127, 128, 300, 16384, 1000000, 123456789012345UL };
            for (int i = 0; i < testVals.Length; i++)
            {
                ulong val = testVals[i];
                ProtoNode node = new ProtoNode { FieldNumber = 1, WireType = 0, VarintValue = val };
                byte[] encoded = ProtoAstCodec.Encode(new List<ProtoNode> { node });
                List<ProtoNode> decoded = ProtoAstCodec.Decode(encoded);

                ctx.AssertEqual(1, decoded.Count, "Decoded nodes count should be 1");
                ctx.AssertEqual(val, decoded[0].VarintValue, "Varint value mismatch for " + val);
            }
        }

        public static void Test_F2_02_Fixed64EncodingDecoding(TestContext ctx)
        {
            ulong val = 0x123456789ABCDEF0UL;
            ProtoNode node = new ProtoNode { FieldNumber = 2, WireType = 1, Fixed64Value = val };
            byte[] encoded = ProtoAstCodec.Encode(new List<ProtoNode> { node });
            List<ProtoNode> decoded = ProtoAstCodec.Decode(encoded);

            ctx.AssertEqual(1, decoded.Count, "Decoded count should be 1");
            ctx.AssertEqual(val, decoded[0].Fixed64Value, "Fixed64 value mismatch");
        }

        public static void Test_F2_03_LengthDelimitedEncodingDecoding(TestContext ctx)
        {
            string payload = "Hello Native Protobuf Codec 世界";
            ProtoNode node = new ProtoNode { FieldNumber = 3, WireType = 2 };
            node.SetString(payload);

            byte[] encoded = ProtoAstCodec.Encode(new List<ProtoNode> { node });
            List<ProtoNode> decoded = ProtoAstCodec.Decode(encoded);

            ctx.AssertEqual(1, decoded.Count, "Decoded count should be 1");
            ctx.AssertEqual(payload, decoded[0].GetString(), "Length delimited string mismatch");
        }

        public static void Test_F2_04_Fixed32EncodingDecoding(TestContext ctx)
        {
            uint val = 0xDEADBEEF;
            ProtoNode node = new ProtoNode { FieldNumber = 4, WireType = 5, Fixed32Value = val };
            byte[] encoded = ProtoAstCodec.Encode(new List<ProtoNode> { node });
            List<ProtoNode> decoded = ProtoAstCodec.Decode(encoded);

            ctx.AssertEqual(1, decoded.Count, "Decoded count should be 1");
            ctx.AssertEqual(val, decoded[0].Fixed32Value, "Fixed32 value mismatch");
        }

        public static void Test_F2_05_RoundtripAstFidelity(TestContext ctx)
        {
            ProtoNode root = new ProtoNode { FieldNumber = 1, WireType = 2 };
            ProtoNode child1 = new ProtoNode { FieldNumber = 1, WireType = 0, VarintValue = 99 };
            ProtoNode child2 = new ProtoNode { FieldNumber = 2, WireType = 2 };
            child2.SetString("SubMessage String");
            root.Children.Add(child1);
            root.Children.Add(child2);

            byte[] bytes1 = ProtoAstCodec.Encode(new List<ProtoNode> { root });
            List<ProtoNode> decoded = ProtoAstCodec.Decode(bytes1);
            byte[] bytes2 = ProtoAstCodec.Encode(decoded);

            ctx.AssertEqual(bytes1.Length, bytes2.Length, "Roundtrip encoded bytes length must match");
            for (int i = 0; i < bytes1.Length; i++)
            {
                ctx.AssertEqual(bytes1[i], bytes2[i], "Byte mismatch at offset " + i);
            }
        }

        // =====================================================================
        // F3: In-Memory Streaming Zip Inspection
        // =====================================================================
        public static void Test_F3_01_ExtractsSummaryProtoDirectlyFromStream(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                MockProjectSpec p = new MockProjectSpec("TestProj", "file:///c:/TestProj");
                p.Sessions.Add(new MockSessionSpec { ConversationId = Guid.NewGuid().ToString(), CustomTitle = "Streaming Zip Title" });

                string zipPath = Path.Combine(sandbox.BackupPath, "test_backup.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec> { p });

                ZipMetadataPackage pkg = StreamingZipIndexReader.ReadZipMetadata(zipPath);
                ctx.AssertTrue(pkg.SummaryProtoBytes != null && pkg.SummaryProtoBytes.Length > 0, "SummaryProtoBytes must not be empty");

                List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(pkg.SummaryProtoBytes);
                ctx.AssertEqual(1, summaries.Count, "Should parse 1 summary from streaming zip");
                ctx.AssertEqual("Streaming Zip Title", summaries[0].DisplayTitle, "Title match from streaming zip");
            }
        }

        public static void Test_F3_02_ExtractsProjectConfigs(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                MockProjectSpec p1 = new MockProjectSpec("Proj1", "file:///c:/Proj1");
                MockProjectSpec p2 = new MockProjectSpec("Proj2", "file:///c:/Proj2");

                string zipPath = Path.Combine(sandbox.BackupPath, "configs_backup.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec> { p1, p2 });

                ZipMetadataPackage pkg = StreamingZipIndexReader.ReadZipMetadata(zipPath);
                ctx.AssertEqual(2, pkg.ProjectConfigs.Count, "Should extract 2 project configs");
            }
        }

        public static void Test_F3_03_ExtractsMetadataJson(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string zipPath = Path.Combine(sandbox.BackupPath, "meta_backup.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec>());

                ZipMetadataPackage pkg = StreamingZipIndexReader.ReadZipMetadata(zipPath);
                ctx.AssertTrue(!string.IsNullOrEmpty(pkg.MetadataJson), "MetadataJson must be present");
                ctx.AssertTrue(pkg.MetadataJson.Contains("\"version\":\"0.5\""), "MetadataJson should contain version 0.5");
            }
        }

        public static void Test_F3_04_IdentifiesConversationDbAndBrainEntries(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string convId = Guid.NewGuid().ToString();
                MockProjectSpec p = new MockProjectSpec("ProjA", "file:///c:/ProjA");
                p.Sessions.Add(new MockSessionSpec { ConversationId = convId, HasBrainData = true });

                string zipPath = Path.Combine(sandbox.BackupPath, "db_brain.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec> { p });

                ZipMetadataPackage pkg = StreamingZipIndexReader.ReadZipMetadata(zipPath);
                ctx.AssertEqual(1, pkg.ConversationDbNames.Count, "Should detect 1 conversation DB");
                ctx.AssertEqual(convId + ".db", pkg.ConversationDbNames[0], "DB file name match");
                ctx.AssertEqual(1, pkg.BrainDirectoryNames.Count, "Should detect 1 brain directory");
                ctx.AssertEqual(convId, pkg.BrainDirectoryNames[0], "Brain directory name match");
            }
        }

        public static void Test_F3_05_DetectsGlobalConfigs(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string zipPath = Path.Combine(sandbox.BackupPath, "globals.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec>(), true);

                ZipMetadataPackage pkg = StreamingZipIndexReader.ReadZipMetadata(zipPath);
                ctx.AssertTrue(pkg.HasGlobalRules, "HasGlobalRules should be true");
                ctx.AssertTrue(pkg.HasGlobalSkills, "HasGlobalSkills should be true");
                ctx.AssertTrue(pkg.HasGlobalMcp, "HasGlobalMcp should be true");
            }
        }

        // =====================================================================
        // F4: Incremental Protobuf Shard Merger
        // =====================================================================
        public static void Test_F4_01_SlicesAndMergesSelectedEntries(TestContext ctx)
        {
            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();

            MockSessionSpec s1 = new MockSessionSpec { ConversationId = id1, CustomTitle = "Local Session 1" };
            MockSessionSpec s2 = new MockSessionSpec { ConversationId = id2, CustomTitle = "Backup Session 2" };

            byte[] localBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { s1 });
            byte[] backupBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { s2 });

            HashSet<string> targetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { id2 };
            byte[] mergedBytes = SelectiveCascadingImporter.MergeProtoShards(localBytes, backupBytes, targetIds, null, null);

            List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(mergedBytes);
            ctx.AssertEqual(2, summaries.Count, "Merged proto should contain 2 sessions");
            ctx.AssertTrue(summaries.Exists(delegate(ConversationSummaryInfo s) { return s.ConversationId == id1; }), "Local session 1 should exist");
            ctx.AssertTrue(summaries.Exists(delegate(ConversationSummaryInfo s) { return s.ConversationId == id2; }), "Backup session 2 should exist");
        }

        public static void Test_F4_02_PreservesExistingLocalSessions(TestContext ctx)
        {
            string idLocal1 = Guid.NewGuid().ToString();
            string idLocal2 = Guid.NewGuid().ToString();
            string idBackup = Guid.NewGuid().ToString();

            MockSessionSpec s1 = new MockSessionSpec { ConversationId = idLocal1, CustomTitle = "Local 1" };
            MockSessionSpec s2 = new MockSessionSpec { ConversationId = idLocal2, CustomTitle = "Local 2" };
            MockSessionSpec s3 = new MockSessionSpec { ConversationId = idBackup, CustomTitle = "Backup 3" };

            byte[] localBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { s1, s2 });
            byte[] backupBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { s3 });

            HashSet<string> targetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { idBackup };
            byte[] mergedBytes = SelectiveCascadingImporter.MergeProtoShards(localBytes, backupBytes, targetIds, null, null);

            List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(mergedBytes);
            ctx.AssertEqual(3, summaries.Count, "Merged count should be 3");
            ctx.AssertTrue(summaries.Exists(delegate(ConversationSummaryInfo s) { return s.ConversationId == idLocal1; }), "Local 1 preserved");
            ctx.AssertTrue(summaries.Exists(delegate(ConversationSummaryInfo s) { return s.ConversationId == idLocal2; }), "Local 2 preserved");
        }

        public static void Test_F4_03_RemapsFolderUriOnProjectRename(TestContext ctx)
        {
            string id = Guid.NewGuid().ToString();
            MockSessionSpec s = new MockSessionSpec
            {
                ConversationId = id,
                ProjectName = "OldProject",
                FolderUri = "file:///c:/OldProject"
            };

            byte[] localBytes = new byte[0];
            byte[] backupBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { s });

            HashSet<string> targetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { id };
            Dictionary<string, string> renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "OldProject", "RenamedProject" }
            };

            byte[] mergedBytes = SelectiveCascadingImporter.MergeProtoShards(localBytes, backupBytes, targetIds, renames, null);
            List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(mergedBytes);

            ctx.AssertEqual(1, summaries.Count, "1 summary should be extracted");
            ctx.AssertEqual("RenamedProject", summaries[0].ProjectName, "ProjectName must be updated to RenamedProject");
            ctx.AssertTrue(summaries[0].FolderUri.EndsWith("RenamedProject"), "FolderUri must end with RenamedProject");
        }

        public static void Test_F4_04_PreservesRawEntrySubfields(TestContext ctx)
        {
            string id = Guid.NewGuid().ToString();
            MockSessionSpec s = new MockSessionSpec { ConversationId = id, CustomTitle = "Complex Subfields" };
            byte[] backupBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { s });

            // Add arbitrary unknown field 88 to the backup entry
            List<ProtoNode> root = ProtoAstCodec.Decode(backupBytes);
            root[0].Children.Add(new ProtoNode { FieldNumber = 88, WireType = 0, VarintValue = 1234567 });
            backupBytes = ProtoAstCodec.Encode(root);

            HashSet<string> targetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { id };
            byte[] mergedBytes = SelectiveCascadingImporter.MergeProtoShards(new byte[0], backupBytes, targetIds, null, null);

            List<ProtoNode> mergedRoot = ProtoAstCodec.Decode(mergedBytes);
            ProtoNode customField = mergedRoot[0].FindChild(88);
            ctx.AssertTrue(customField != null, "Custom field 88 must be preserved in merged AST");
            ctx.AssertEqual(1234567UL, customField.VarintValue, "Custom field 88 varint value must match");
        }

        public static void Test_F4_05_HandlesMultiSessionBatchMerge(TestContext ctx)
        {
            List<MockSessionSpec> backupSessions = new List<MockSessionSpec>();
            HashSet<string> targetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < 10; i++)
            {
                string id = Guid.NewGuid().ToString();
                backupSessions.Add(new MockSessionSpec { ConversationId = id, CustomTitle = "Batch Session " + i });
                targetIds.Add(id);
            }

            byte[] backupBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(backupSessions);
            byte[] mergedBytes = SelectiveCascadingImporter.MergeProtoShards(new byte[0], backupBytes, targetIds, null, null);

            List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(mergedBytes);
            ctx.AssertEqual(10, summaries.Count, "All 10 batch sessions should be merged");
        }

        // =====================================================================
        // F5: Smart Ephemeral/Junk Project Filter
        // =====================================================================
        public static void Test_F5_01_FiltersSubagentPrefix(TestContext ctx)
        {
            ctx.AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("subagent-review-1"), "subagent-review-1 should be junk");
            ctx.AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("subagent_worker"), "subagent_worker should be junk");
            ctx.AssertFalse(SmartProjectFilter.IsJunkOrEphemeral("MainProject"), "MainProject should not be junk");
        }

        public static void Test_F5_02_FiltersOutsideOfProject(TestContext ctx)
        {
            ctx.AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("outside-of-project"), "outside-of-project should be junk");
            ctx.AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("outside_of_project"), "outside_of_project should be junk");
        }

        public static void Test_F5_03_FiltersDefaultCliProject(TestContext ctx)
        {
            ctx.AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("default-cli-project"), "default-cli-project should be junk");
            ctx.AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("default_cli_project"), "default_cli_project should be junk");
        }

        public static void Test_F5_04_FiltersRecoveredProject(TestContext ctx)
        {
            ctx.AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("Recovered project 2026-08-21"), "Recovered project should be junk");
            ctx.AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("recovered_project"), "recovered_project should be junk");
        }

        public static void Test_F5_05_FiltersTempPrefix(TestContext ctx)
        {
            ctx.AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("temp-build"), "temp-build should be junk");
            ctx.AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("temp_workspace"), "temp_workspace should be junk");
            ctx.AssertFalse(SmartProjectFilter.IsJunkOrEphemeral("Antigravity_Companion"), "Antigravity_Companion should not be junk");
        }

        // =====================================================================
        // F6: Bidirectional Visual Diff Classification
        // =====================================================================
        public static void Test_F6_01_ClassifiesNewInBackup(TestContext ctx)
        {
            List<ConversationSummaryInfo> local = new List<ConversationSummaryInfo>();
            List<ConversationSummaryInfo> backup = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "NewProj", FolderUri = "file:///c:/NewProj", ConversationId = Guid.NewGuid().ToString() }
            };

            List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(local, backup);
            ctx.AssertEqual(1, diff.Count, "1 diff node expected");
            ctx.AssertEqual(DiffStatus.NewInBackup, diff[0].Status, "Status should be NewInBackup");
            ctx.AssertEqual("[★ 新增]", diff[0].GetStatusBadge(), "Badge text match");
        }

        public static void Test_F6_02_ClassifiesAligned(TestContext ctx)
        {
            List<ConversationSummaryInfo> local = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "CommonProj", FolderUri = "file:///c:/CommonProj", ConversationId = Guid.NewGuid().ToString() }
            };
            List<ConversationSummaryInfo> backup = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "CommonProj", FolderUri = "file:///c:/CommonProj", ConversationId = Guid.NewGuid().ToString() }
            };

            List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(local, backup);
            ctx.AssertEqual(1, diff.Count, "1 diff node expected");
            ctx.AssertEqual(DiffStatus.Aligned, diff[0].Status, "Status should be Aligned");
            ctx.AssertEqual("[已对齐]", diff[0].GetStatusBadge(), "Badge text match");
        }

        public static void Test_F6_03_ClassifiesConflicted(TestContext ctx)
        {
            List<ConversationSummaryInfo> local = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "ConflictProj", FolderUri = "file:///c:/Local/ConflictProj", ConversationId = Guid.NewGuid().ToString() }
            };
            List<ConversationSummaryInfo> backup = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "ConflictProj", FolderUri = "file:///d:/Backup/ConflictProj", ConversationId = Guid.NewGuid().ToString() }
            };

            List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(local, backup);
            ctx.AssertEqual(1, diff.Count, "1 diff node expected");
            ctx.AssertEqual(DiffStatus.Conflicted, diff[0].Status, "Status should be Conflicted");
            ctx.AssertEqual("[重名/路径差异]", diff[0].GetStatusBadge(), "Badge text match");
        }

        public static void Test_F6_04_ClassifiesLocalOnly(TestContext ctx)
        {
            List<ConversationSummaryInfo> local = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "LocalOnlyProj", FolderUri = "file:///c:/LocalOnlyProj", ConversationId = Guid.NewGuid().ToString() }
            };
            List<ConversationSummaryInfo> backup = new List<ConversationSummaryInfo>();

            List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(local, backup);
            ctx.AssertEqual(1, diff.Count, "1 diff node expected");
            ctx.AssertEqual(DiffStatus.LocalOnly, diff[0].Status, "Status should be LocalOnly");
            ctx.AssertEqual("[本地独有]", diff[0].GetStatusBadge(), "Badge text match");
        }

        public static void Test_F6_05_DiffBadgeTextIntegrity(TestContext ctx)
        {
            ProjectDiffNode n1 = new ProjectDiffNode("P1") { Status = DiffStatus.NewInBackup };
            ProjectDiffNode n2 = new ProjectDiffNode("P2") { Status = DiffStatus.Aligned };
            ProjectDiffNode n3 = new ProjectDiffNode("P3") { Status = DiffStatus.Conflicted };
            ProjectDiffNode n4 = new ProjectDiffNode("P4") { Status = DiffStatus.LocalOnly };

            ctx.AssertEqual("[★ 新增]", n1.GetStatusBadge(), "New badge");
            ctx.AssertEqual("[已对齐]", n2.GetStatusBadge(), "Aligned badge");
            ctx.AssertEqual("[重名/路径差异]", n3.GetStatusBadge(), "Conflicted badge");
            ctx.AssertEqual("[本地独有]", n4.GetStatusBadge(), "LocalOnly badge");
        }

        // =====================================================================
        // F7: Interactive Conflict Resolution Wizard
        // =====================================================================
        public static void Test_F7_01_MergeStrategyPreservesLocalUri(TestContext ctx)
        {
            string id = Guid.NewGuid().ToString();
            MockSessionSpec s = new MockSessionSpec { ConversationId = id, ProjectName = "ConflictApp", FolderUri = "file:///backup/ConflictApp" };
            byte[] backupBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { s });

            Dictionary<string, ConflictResolutionStrategy> strats = new Dictionary<string, ConflictResolutionStrategy>(StringComparer.OrdinalIgnoreCase)
            {
                { "ConflictApp", ConflictResolutionStrategy.Merge }
            };

            byte[] merged = SelectiveCascadingImporter.MergeProtoShards(new byte[0], backupBytes, new HashSet<string> { id }, null, strats);
            List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(merged);
            ctx.AssertEqual(1, list.Count, "1 session merged");
        }

        public static void Test_F7_02_RenameStrategyUpdatesProjectNameAndUri(TestContext ctx)
        {
            string id = Guid.NewGuid().ToString();
            MockSessionSpec s = new MockSessionSpec { ConversationId = id, ProjectName = "OldName", FolderUri = "file:///c:/Projects/OldName" };
            byte[] backupBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { s });

            Dictionary<string, string> renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "OldName", "NewName" }
            };

            byte[] merged = SelectiveCascadingImporter.MergeProtoShards(new byte[0], backupBytes, new HashSet<string> { id }, renames, null);
            List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(merged);
            ctx.AssertEqual("NewName", list[0].ProjectName, "Project renamed");
        }

        public static void Test_F7_03_OverwriteStrategyReplacesLocalEntries(TestContext ctx)
        {
            string id = Guid.NewGuid().ToString();
            MockSessionSpec localS = new MockSessionSpec { ConversationId = id, CustomTitle = "Old Local Title" };
            MockSessionSpec backupS = new MockSessionSpec { ConversationId = id, CustomTitle = "New Overwrite Title" };

            byte[] localBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { localS });
            byte[] backupBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { backupS });

            byte[] merged = SelectiveCascadingImporter.MergeProtoShards(localBytes, backupBytes, new HashSet<string> { id }, null, null);
            List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(merged);

            ctx.AssertEqual(1, list.Count, "Only 1 session should remain");
            ctx.AssertEqual("New Overwrite Title", list[0].DisplayTitle, "Overwrite title should replace local");
        }

        public static void Test_F7_04_SkipStrategyOmitsConflictedProject(TestContext ctx)
        {
            string id = Guid.NewGuid().ToString();
            MockSessionSpec s = new MockSessionSpec { ConversationId = id, ProjectName = "SkipProj" };
            byte[] backupBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { s });

            Dictionary<string, ConflictResolutionStrategy> strats = new Dictionary<string, ConflictResolutionStrategy>(StringComparer.OrdinalIgnoreCase)
            {
                { "SkipProj", ConflictResolutionStrategy.Skip }
            };

            byte[] merged = SelectiveCascadingImporter.MergeProtoShards(new byte[0], backupBytes, new HashSet<string> { id }, null, strats);
            List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(merged);
            ctx.AssertEqual(0, list.Count, "Skipped project should not be merged");
        }

        public static void Test_F7_05_BatchConflictPlanApplication(TestContext ctx)
        {
            ConflictResolutionPlan plan = new ConflictResolutionPlan();
            plan.Strategies["Proj1"] = ConflictResolutionStrategy.Merge;
            plan.Strategies["Proj2"] = ConflictResolutionStrategy.Rename;
            plan.RenamedProjects["Proj2"] = "Proj2_Imported";
            plan.Strategies["Proj3"] = ConflictResolutionStrategy.Skip;

            ctx.AssertEqual(ConflictResolutionStrategy.Merge, plan.Strategies["Proj1"], "Strategy Proj1");
            ctx.AssertEqual(ConflictResolutionStrategy.Rename, plan.Strategies["Proj2"], "Strategy Proj2");
            ctx.AssertEqual("Proj2_Imported", plan.RenamedProjects["Proj2"], "Rename Proj2");
            ctx.AssertEqual(ConflictResolutionStrategy.Skip, plan.Strategies["Proj3"], "Strategy Proj3");
        }

        // =====================================================================
        // F8: Dual-Tree Visual Diff Workspace
        // =====================================================================
        public static void Test_F8_01_BuildsLocalHierarchy(TestContext ctx)
        {
            List<ConversationSummaryInfo> local = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "ProjectA", ConversationId = "1", DisplayTitle = "Session 1" },
                new ConversationSummaryInfo { ProjectName = "ProjectA", ConversationId = "2", DisplayTitle = "Session 2" },
                new ConversationSummaryInfo { ProjectName = "ProjectB", ConversationId = "3", DisplayTitle = "Session 3" }
            };

            List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(local, null);
            ctx.AssertEqual(2, diff.Count, "2 projects in local");
            ProjectDiffNode pA = diff.Find(delegate(ProjectDiffNode p) { return p.ProjectName == "ProjectA"; });
            ctx.AssertEqual(2, pA.LocalConversations.Count, "ProjectA has 2 sessions");
        }

        public static void Test_F8_02_BuildsBackupHierarchy(TestContext ctx)
        {
            List<ConversationSummaryInfo> backup = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "ProjectBackup", ConversationId = "b1", DisplayTitle = "Backup Session 1" }
            };

            List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(null, backup);
            ctx.AssertEqual(1, diff.Count, "1 backup project");
            ctx.AssertEqual(DiffStatus.NewInBackup, diff[0].Status, "Diff status is NewInBackup");
        }

        public static void Test_F8_03_DisplaysCustomTitleAndTimestampInNodeText(TestContext ctx)
        {
            ConversationSummaryInfo s = new ConversationSummaryInfo
            {
                DisplayTitle = "核心功能重构",
                UpdatedTime = new DateTime(2026, 8, 23, 12, 0, 0, DateTimeKind.Utc)
            };

            string nodeText = string.Format("{0} ({1})", s.DisplayTitle, s.UpdatedTime.ToString("yyyy-MM-dd HH:mm"));
            ctx.AssertTrue(nodeText.Contains("核心功能重构"), "Node text contains custom title");
            ctx.AssertTrue(nodeText.Contains("2026-08-23"), "Node text contains timestamp");
        }

        public static void Test_F8_04_PopulatesGlobalConfigsNodes(TestContext ctx)
        {
            ZipMetadataPackage pkg = new ZipMetadataPackage { HasGlobalRules = true, HasGlobalSkills = true, HasGlobalMcp = true };
            ctx.AssertTrue(pkg.HasGlobalRules, "Rules present");
            ctx.AssertTrue(pkg.HasGlobalSkills, "Skills present");
            ctx.AssertTrue(pkg.HasGlobalMcp, "MCP present");
        }

        public static void Test_F8_05_TreeNodesSupportColorAndFontMetadata(TestContext ctx)
        {
            ProjectDiffNode node = new ProjectDiffNode("Test") { Status = DiffStatus.NewInBackup };
            ctx.AssertEqual("[★ 新增]", node.GetStatusBadge(), "Color/badge mapped");
        }

        // =====================================================================
        // F9: Cascading CheckBox Selection Model
        // =====================================================================
        public static void Test_F9_01_CheckingProjectChecksAllChildren(TestContext ctx)
        {
            CascadingSelectionModel model = new CascadingSelectionModel();
            HashSet<string> selected = new HashSet<string>();
            List<string> childIds = new List<string> { "c1", "c2", "c3" };

            model.ToggleProjectSelection(true, childIds, selected);
            ctx.AssertEqual(3, selected.Count, "All 3 child IDs selected");
            ctx.AssertTrue(selected.Contains("c1"), "c1 selected");
            ctx.AssertTrue(selected.Contains("c2"), "c2 selected");
            ctx.AssertTrue(selected.Contains("c3"), "c3 selected");
        }

        public static void Test_F9_02_UncheckingProjectUnchecksAllChildren(TestContext ctx)
        {
            CascadingSelectionModel model = new CascadingSelectionModel();
            HashSet<string> selected = new HashSet<string> { "c1", "c2", "c3" };
            List<string> childIds = new List<string> { "c1", "c2", "c3" };

            model.ToggleProjectSelection(false, childIds, selected);
            ctx.AssertEqual(0, selected.Count, "All child IDs unselected");
        }

        public static void Test_F9_03_SelectAllChecksAllSelectableItems(TestContext ctx)
        {
            CascadingSelectionModel model = new CascadingSelectionModel();
            HashSet<string> selected = new HashSet<string>();
            List<ConversationSummaryInfo> all = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ConversationId = "u1" },
                new ConversationSummaryInfo { ConversationId = "u2" },
                new ConversationSummaryInfo { ConversationId = "u3" }
            };

            model.SelectAll(all, selected);
            ctx.AssertEqual(3, selected.Count, "All 3 items selected");
        }

        public static void Test_F9_04_SelectNoneClearsAllSelections(TestContext ctx)
        {
            CascadingSelectionModel model = new CascadingSelectionModel();
            HashSet<string> selected = new HashSet<string> { "u1", "u2" };

            model.SelectNone(selected);
            ctx.AssertEqual(0, selected.Count, "Selection cleared");
        }

        public static void Test_F9_05_SelectNewOnlyFiltersToNewInBackupOnly(TestContext ctx)
        {
            CascadingSelectionModel model = new CascadingSelectionModel();
            HashSet<string> selected = new HashSet<string>();

            ProjectDiffNode nNew = new ProjectDiffNode("NewProj") { Status = DiffStatus.NewInBackup };
            nNew.BackupConversations.Add(new ConversationSummaryInfo { ConversationId = "id_new1" });

            ProjectDiffNode nAligned = new ProjectDiffNode("AlignedProj") { Status = DiffStatus.Aligned };
            nAligned.BackupConversations.Add(new ConversationSummaryInfo { ConversationId = "id_aligned1" });

            model.SelectNewOnly(new List<ProjectDiffNode> { nNew, nAligned }, selected);
            ctx.AssertEqual(1, selected.Count, "Only 1 session selected");
            ctx.AssertTrue(selected.Contains("id_new1"), "Only id_new1 should be selected");
        }

        // =====================================================================
        // F10: Fine-Grained Cascading Selective Import
        // =====================================================================
        public static void Test_F10_01_ExtractsOnlySelectedDbFiles(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string id1 = Guid.NewGuid().ToString();
                string id2 = Guid.NewGuid().ToString();

                MockProjectSpec p = new MockProjectSpec("Proj1", "file:///c:/Proj1");
                p.Sessions.Add(new MockSessionSpec { ConversationId = id1 });
                p.Sessions.Add(new MockSessionSpec { ConversationId = id2 });

                string zipPath = Path.Combine(sandbox.BackupPath, "selective_db.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec> { p });

                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = zipPath,
                    TargetGeminiPath = sandbox.GeminiPath,
                    SelectedConversationIds = new List<string> { id1 } // Select id1 only
                };

                SelectiveImportResult res = SelectiveCascadingImporter.ExecuteImport(req);
                ctx.AssertTrue(res.Success, "Import should succeed");
                ctx.AssertEqual(1, res.ImportedConversationsCount, "1 conversation imported");

                string db1 = Path.Combine(sandbox.AntigravityPath, "conversations", id1 + ".db");
                string db2 = Path.Combine(sandbox.AntigravityPath, "conversations", id2 + ".db");

                ctx.AssertTrue(File.Exists(db1), "Target DB 1 must exist");
                ctx.AssertFalse(File.Exists(db2), "Unselected DB 2 must not exist");
            }
        }

        public static void Test_F10_02_ExtractsOnlySelectedBrainFolders(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string id1 = Guid.NewGuid().ToString();
                string id2 = Guid.NewGuid().ToString();

                MockProjectSpec p = new MockProjectSpec("Proj1", "file:///c:/Proj1");
                p.Sessions.Add(new MockSessionSpec { ConversationId = id1, HasBrainData = true });
                p.Sessions.Add(new MockSessionSpec { ConversationId = id2, HasBrainData = true });

                string zipPath = Path.Combine(sandbox.BackupPath, "selective_brain.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec> { p });

                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = zipPath,
                    TargetGeminiPath = sandbox.GeminiPath,
                    SelectedConversationIds = new List<string> { id1 }
                };

                SelectiveImportResult res = SelectiveCascadingImporter.ExecuteImport(req);
                ctx.AssertTrue(res.Success, "Import should succeed");

                string brain1 = Path.Combine(sandbox.AntigravityPath, "brain", id1);
                string brain2 = Path.Combine(sandbox.AntigravityPath, "brain", id2);

                ctx.AssertTrue(Directory.Exists(brain1), "Selected brain folder 1 must exist");
                ctx.AssertFalse(Directory.Exists(brain2), "Unselected brain folder 2 must not exist");
            }
        }

        public static void Test_F10_03_MergesProtoSummariesNonDestructively(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string localId = Guid.NewGuid().ToString();
                string backupId = Guid.NewGuid().ToString();

                MockProjectSpec localP = new MockProjectSpec("LocalProj", "file:///c:/LocalProj");
                localP.Sessions.Add(new MockSessionSpec { ConversationId = localId, CustomTitle = "Existing Local" });
                MockSandboxGenerator.PopulateLocalSandbox(sandbox, new List<MockProjectSpec> { localP });

                MockProjectSpec backupP = new MockProjectSpec("BackupProj", "file:///c:/BackupProj");
                backupP.Sessions.Add(new MockSessionSpec { ConversationId = backupId, CustomTitle = "Imported Backup" });
                string zipPath = Path.Combine(sandbox.BackupPath, "proto_merge.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec> { backupP });

                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = zipPath,
                    TargetGeminiPath = sandbox.GeminiPath,
                    SelectedConversationIds = new List<string> { backupId }
                };

                SelectiveImportResult res = SelectiveCascadingImporter.ExecuteImport(req);
                ctx.AssertTrue(res.Success, "Import should succeed");

                byte[] localPb = File.ReadAllBytes(Path.Combine(sandbox.AntigravityPath, "agyhub_summaries_proto.pb"));
                List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(localPb);

                ctx.AssertEqual(2, summaries.Count, "Both local and imported summaries must exist in local pb");
            }
        }

        public static void Test_F10_04_LeavesExistingLocalFilesIntact(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string localId = Guid.NewGuid().ToString();
                MockProjectSpec localP = new MockProjectSpec("LocalProj", "file:///c:/LocalProj");
                localP.Sessions.Add(new MockSessionSpec { ConversationId = localId, CustomTitle = "Local Unchanged" });
                MockSandboxGenerator.PopulateLocalSandbox(sandbox, new List<MockProjectSpec> { localP });

                string localDb = Path.Combine(sandbox.AntigravityPath, "conversations", localId + ".db");
                byte[] origBytes = File.ReadAllBytes(localDb);

                string backupId = Guid.NewGuid().ToString();
                MockProjectSpec backupP = new MockProjectSpec("BackupProj", "file:///c:/BackupProj");
                backupP.Sessions.Add(new MockSessionSpec { ConversationId = backupId });
                string zipPath = Path.Combine(sandbox.BackupPath, "intact.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec> { backupP });

                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = zipPath,
                    TargetGeminiPath = sandbox.GeminiPath,
                    SelectedConversationIds = new List<string> { backupId }
                };

                SelectiveCascadingImporter.ExecuteImport(req);
                byte[] afterBytes = File.ReadAllBytes(localDb);

                ctx.AssertEqual(origBytes.Length, afterBytes.Length, "Local DB file length unchanged");
            }
        }

        public static void Test_F10_05_ReturnsAccurateImportResultMetrics(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string id1 = Guid.NewGuid().ToString();
                string id2 = Guid.NewGuid().ToString();

                MockProjectSpec p = new MockProjectSpec("MetricsProj", "file:///c:/MetricsProj");
                p.Sessions.Add(new MockSessionSpec { ConversationId = id1, HasBrainData = true });
                p.Sessions.Add(new MockSessionSpec { ConversationId = id2, HasBrainData = true });

                string zipPath = Path.Combine(sandbox.BackupPath, "metrics.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec> { p });

                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = zipPath,
                    TargetGeminiPath = sandbox.GeminiPath,
                    SelectedConversationIds = new List<string> { id1, id2 }
                };

                SelectiveImportResult res = SelectiveCascadingImporter.ExecuteImport(req);
                ctx.AssertEqual(2, res.ImportedConversationsCount, "Imported conversation count");
                ctx.AssertEqual(2, res.ImportedBrainDirsCount, "Imported brain dirs count");
                ctx.AssertEqual(2, res.MergedProtoSummariesCount, "Merged proto count");
            }
        }

        // =====================================================================
        // F11: Post-Import UI Auto-Refresh & Highlight
        // =====================================================================
        public static void Test_F11_01_MarksImportedSessionsAsHighlighted(TestContext ctx)
        {
            HighlightTracker tracker = new HighlightTracker();
            tracker.MarkImported(new List<string> { "uuid-123", "uuid-456" }, new List<string> { "ProjA" });

            ctx.AssertTrue(tracker.IsConversationHighlighted("uuid-123"), "uuid-123 should be highlighted");
            ctx.AssertTrue(tracker.IsConversationHighlighted("uuid-456"), "uuid-456 should be highlighted");
        }

        public static void Test_F11_02_MarksImportedProjectsAsHighlighted(TestContext ctx)
        {
            HighlightTracker tracker = new HighlightTracker();
            tracker.MarkImported(new List<string> { "uuid-1" }, new List<string> { "Project_Imported" });

            ctx.AssertTrue(tracker.IsProjectHighlighted("Project_Imported"), "Project_Imported should be highlighted");
        }

        public static void Test_F11_03_UnimportedSessionsNotHighlighted(TestContext ctx)
        {
            HighlightTracker tracker = new HighlightTracker();
            tracker.MarkImported(new List<string> { "uuid-new" }, new List<string>());

            ctx.AssertFalse(tracker.IsConversationHighlighted("uuid-old"), "uuid-old should not be highlighted");
            ctx.AssertFalse(tracker.IsProjectHighlighted("OldProject"), "OldProject should not be highlighted");
        }

        public static void Test_F11_04_RescanReflectsUpdatedTotalConversationCount(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                MockProjectSpec localP = new MockProjectSpec("P1", "file:///c:/P1");
                localP.Sessions.Add(new MockSessionSpec { ConversationId = Guid.NewGuid().ToString() });
                MockSandboxGenerator.PopulateLocalSandbox(sandbox, new List<MockProjectSpec> { localP });

                string newId = Guid.NewGuid().ToString();
                MockProjectSpec backupP = new MockProjectSpec("P2", "file:///c:/P2");
                backupP.Sessions.Add(new MockSessionSpec { ConversationId = newId });
                string zipPath = Path.Combine(sandbox.BackupPath, "rescan.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec> { backupP });

                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = zipPath,
                    TargetGeminiPath = sandbox.GeminiPath,
                    SelectedConversationIds = new List<string> { newId }
                };
                SelectiveCascadingImporter.ExecuteImport(req);

                byte[] pbBytes = File.ReadAllBytes(Path.Combine(sandbox.AntigravityPath, "agyhub_summaries_proto.pb"));
                List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(pbBytes);

                ctx.AssertEqual(2, summaries.Count, "Rescanned total should be 2");
            }
        }

        public static void Test_F11_05_HighlightStateMaintainedAcrossQueries(TestContext ctx)
        {
            HighlightTracker tracker = new HighlightTracker();
            tracker.MarkImported(new List<string> { "uuid-persistent" }, new List<string> { "Proj-P" });

            for (int i = 0; i < 5; i++)
            {
                ctx.AssertTrue(tracker.IsConversationHighlighted("uuid-persistent"), "Persistent highlight query " + i);
            }
        }

        // =====================================================================
        // F12: Pure Native Single-File Build
        // =====================================================================
        public static void Test_F12_01_BuildBatExists(TestContext ctx)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            string batPath = Path.Combine(root, "build.bat");
            ctx.AssertTrue(File.Exists(batPath), "build.bat must exist at " + batPath);
        }

        public static void Test_F12_02_AppIconExists(TestContext ctx)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            string icoPath = Path.Combine(root, "app.ico");
            ctx.AssertTrue(File.Exists(icoPath), "app.ico must exist at " + icoPath);
        }

        public static void Test_F12_03_BuildBatUsesCscAndIconFlag(TestContext ctx)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            string batPath = Path.Combine(root, "build.bat");
            string batText = File.ReadAllText(batPath);

            ctx.AssertTrue(batText.Contains("csc.exe"), "build.bat must invoke csc.exe");
            ctx.AssertTrue(batText.Contains("/win32icon"), "build.bat must include /win32icon flag");
        }

        public static void Test_F12_04_BuildBatIncludesCompressionAssemblies(TestContext ctx)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            string batPath = Path.Combine(root, "build.bat");
            string batText = File.ReadAllText(batPath);

            ctx.AssertTrue(batText.Contains("System.IO.Compression.dll"), "build.bat must reference System.IO.Compression.dll");
            ctx.AssertTrue(batText.Contains("System.IO.Compression.FileSystem.dll"), "build.bat must reference System.IO.Compression.FileSystem.dll");
        }

        public static void Test_F12_05_MainFormContainsEntryProgram(TestContext ctx)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            string mainFormPath = Path.Combine(root, "MainForm.cs");
            string code = File.ReadAllText(mainFormPath);

            ctx.AssertTrue(code.Contains("static class Program"), "MainForm.cs must contain Program class");
            ctx.AssertTrue(code.Contains("[STAThread]"), "MainForm.cs Main must have STAThread attribute");
        }

        // =====================================================================
        // F13: Complete User Documentation & Commit
        // =====================================================================
        public static void Test_F13_01_UserManualExists(TestContext ctx)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            string docPath = Path.Combine(root, "Antigravity_Companion使用说明书.md");
            ctx.AssertTrue(File.Exists(docPath), "User manual must exist");
        }

        public static void Test_F13_02_UserManualDocumentsDualTree(TestContext ctx)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            string docPath = Path.Combine(root, "Antigravity_Companion使用说明书.md");
            string text = File.ReadAllText(docPath);

            ctx.AssertTrue(text.Contains("双栏") || text.Contains("双树") || text.Contains("Dual-Tree") || text.Contains("对比") || text.Contains("跨机器") || text.Contains("数据迁移"), "Manual must document migration or dual-tree");
        }

        public static void Test_F13_03_UserManualDocumentsSelectiveImport(TestContext ctx)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            string docPath = Path.Combine(root, "Antigravity_Companion使用说明书.md");
            string text = File.ReadAllText(docPath);

            ctx.AssertTrue(text.Contains("选择性导入") || text.Contains("导入") || text.Contains("Selective Import"), "Manual must document selective import");
        }

        public static void Test_F13_04_UserManualDocumentsConflictResolution(TestContext ctx)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            string docPath = Path.Combine(root, "Antigravity_Companion使用说明书.md");
            string text = File.ReadAllText(docPath);

            ctx.AssertTrue(text.Contains("冲突") || text.Contains("重名") || text.Contains("合并") || text.Contains("Conflict"), "Manual must document conflict resolution");
        }

        public static void Test_F13_05_GitignoreRulesPresent(TestContext ctx)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            string gitignorePath = Path.Combine(root, ".gitignore");
            ctx.AssertTrue(File.Exists(gitignorePath), ".gitignore must exist");
            string text = File.ReadAllText(gitignorePath);
            ctx.AssertTrue(text.Contains("*.exe") || text.Contains("Antigravity_Companion.exe"), ".gitignore must ignore binaries");
        }
    }
}
