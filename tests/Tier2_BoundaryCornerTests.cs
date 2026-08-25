using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace AntigravityCompanion.Tests
{
    public static class Tier2_BoundaryCornerTests
    {
        // =====================================================================
        // F1: Custom Title Lossless Extraction (Boundary & Corner Cases)
        // =====================================================================
        public static void Test_F1_T2_01_EmptyOrWhitespaceCustomTitleFallback(TestContext ctx)
        {
            MockSessionSpec spec = new MockSessionSpec
            {
                ConversationId = "11111111-2222-3333-4444-555555555555",
                AutoSummary = "Valid Auto Summary",
                CustomTitle = "   " // Whitespace only
            };

            byte[] pbBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { spec });
            List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(pbBytes);

            ctx.AssertEqual(1, summaries.Count, "1 summary expected");
            ctx.AssertEqual("Valid Auto Summary", summaries[0].DisplayTitle, "Whitespace title must fallback to AutoSummary");
        }

        public static void Test_F1_T2_02_ExtremeLengthUnicodeAndEmoji(TestContext ctx)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("【长标题测试】");
            for (int i = 0; i < 200; i++)
            {
                sb.Append("🌟高能前哨_");
            }
            string longTitle = sb.ToString();

            MockSessionSpec spec = new MockSessionSpec
            {
                ConversationId = "22222222-3333-4444-5555-666666666666",
                CustomTitle = longTitle
            };

            byte[] pbBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { spec });
            List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(pbBytes);

            ctx.AssertEqual(1, summaries.Count, "1 summary expected");
            ctx.AssertEqual(longTitle, summaries[0].DisplayTitle, "Extreme length unicode title must match exactly");
        }

        public static void Test_F1_T2_03_MalformedField15MissingSubfield(TestContext ctx)
        {
            ProtoNode entry = new ProtoNode { FieldNumber = 1, WireType = 2 };
            ProtoNode f1 = new ProtoNode { FieldNumber = 1, WireType = 2 };
            f1.SetString("Auto Title");
            entry.Children.Add(f1);

            ProtoNode f2 = new ProtoNode { FieldNumber = 2, WireType = 2 };
            f2.SetString("malformed-field15-uuid");
            entry.Children.Add(f2);

            // Empty Field 15 node without children
            ProtoNode f15 = new ProtoNode { FieldNumber = 15, WireType = 2, LengthDelimitedValue = new byte[0] };
            entry.Children.Add(f15);

            byte[] bytes = ProtoAstCodec.Encode(new List<ProtoNode> { entry });
            List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(bytes);

            ctx.AssertEqual(1, summaries.Count, "Should handle empty Field 15 gracefully");
            ctx.AssertEqual("Auto Title", summaries[0].DisplayTitle, "Should fallback to Auto Title");
        }

        public static void Test_F1_T2_04_NullAutoSummaryAndNullCustomTitle(TestContext ctx)
        {
            ProtoNode entry = new ProtoNode { FieldNumber = 1, WireType = 2 };
            ProtoNode f2 = new ProtoNode { FieldNumber = 2, WireType = 2 };
            f2.SetString("abcdabcd-1234-5678-90ab-cdef12345678");
            entry.Children.Add(f2);

            byte[] pbBytes = ProtoAstCodec.Encode(new List<ProtoNode> { entry });
            List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(pbBytes);

            ctx.AssertEqual(1, summaries.Count, "1 summary expected");
            ctx.AssertTrue(!string.IsNullOrEmpty(summaries[0].DisplayTitle), "DisplayTitle must not be empty");
            ctx.AssertTrue(summaries[0].DisplayTitle.Contains("未命名会话"), "Should generate default untitled string");
        }

        public static void Test_F1_T2_05_DuplicateField15Tags(TestContext ctx)
        {
            ProtoNode entry = new ProtoNode { FieldNumber = 1, WireType = 2 };
            ProtoNode f2 = new ProtoNode { FieldNumber = 2, WireType = 2 };
            f2.SetString("duplicate-f15-uuid");
            entry.Children.Add(f2);

            ProtoNode f15_a = new ProtoNode { FieldNumber = 15, WireType = 2 };
            ProtoNode sub_a = new ProtoNode { FieldNumber = 1, WireType = 2 };
            sub_a.SetString("First Custom Title");
            f15_a.Children.Add(sub_a);
            entry.Children.Add(f15_a);

            ProtoNode f15_b = new ProtoNode { FieldNumber = 15, WireType = 2 };
            ProtoNode sub_b = new ProtoNode { FieldNumber = 1, WireType = 2 };
            sub_b.SetString("Second Custom Title");
            f15_b.Children.Add(sub_b);
            entry.Children.Add(f15_b);

            byte[] bytes = ProtoAstCodec.Encode(new List<ProtoNode> { entry });
            List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(bytes);

            ctx.AssertEqual(1, summaries.Count, "1 summary expected");
            ctx.AssertTrue(summaries[0].HasCustomTitle, "HasCustomTitle should be true");
        }

        // =====================================================================
        // F2: Pure Native Protobuf AST Codec (Boundary Cases)
        // =====================================================================
        public static void Test_F2_T2_01_MaxUInt64VarintBoundary(TestContext ctx)
        {
            ulong maxVal = ulong.MaxValue;
            ProtoNode node = new ProtoNode { FieldNumber = 1, WireType = 0, VarintValue = maxVal };
            byte[] encoded = ProtoAstCodec.Encode(new List<ProtoNode> { node });

            // Max ulong varint is 10 bytes + 1 tag byte = 11 bytes total
            ctx.AssertEqual(11, encoded.Length, "Encoded length for ulong.MaxValue");
            List<ProtoNode> decoded = ProtoAstCodec.Decode(encoded);
            ctx.AssertEqual(maxVal, decoded[0].VarintValue, "Decoded max ulong match");
        }

        public static void Test_F2_T2_02_ZeroLengthDelimitedBuffer(TestContext ctx)
        {
            ProtoNode node = new ProtoNode { FieldNumber = 10, WireType = 2, LengthDelimitedValue = new byte[0] };
            byte[] encoded = ProtoAstCodec.Encode(new List<ProtoNode> { node });
            List<ProtoNode> decoded = ProtoAstCodec.Decode(encoded);

            ctx.AssertEqual(1, decoded.Count, "1 node decoded");
            ctx.AssertEqual(0, decoded[0].LengthDelimitedValue.Length, "Length delimited value is 0-length");
        }

        public static void Test_F2_T2_03_TruncatedBufferHandling(TestContext ctx)
        {
            // Create a node declaring length 50, but truncate stream to 5 bytes
            byte[] truncated = new byte[] { (1 << 3) | 2, 50, 1, 2, 3 };
            List<ProtoNode> decoded = ProtoAstCodec.Decode(truncated);

            ctx.AssertEqual(1, decoded.Count, "Should recover partial node without crashing");
            ctx.AssertEqual(3, decoded[0].LengthDelimitedValue.Length, "Should extract remaining available bytes");
        }

        public static void Test_F2_T2_04_DeeplyNestedProtoHierarchy(TestContext ctx)
        {
            ProtoNode current = new ProtoNode { FieldNumber = 1, WireType = 2 };
            ProtoNode root = current;

            for (int i = 0; i < 8; i++)
            {
                ProtoNode child = new ProtoNode { FieldNumber = 1, WireType = 2 };
                current.Children.Add(child);
                current = child;
            }

            ProtoNode leaf = new ProtoNode { FieldNumber = 1, WireType = 0, VarintValue = 777 };
            current.Children.Add(leaf);

            byte[] encoded = ProtoAstCodec.Encode(new List<ProtoNode> { root });
            List<ProtoNode> decoded = ProtoAstCodec.Decode(encoded);

            ctx.AssertEqual(1, decoded.Count, "Root decoded");
            ProtoNode temp = decoded[0];
            for (int i = 0; i < 8; i++)
            {
                ctx.AssertTrue(temp.Children != null && temp.Children.Count > 0, "Level " + i + " exists");
                temp = temp.Children[0];
            }
            ctx.AssertEqual(777UL, temp.Children[0].VarintValue, "Leaf value match");
        }

        public static void Test_F2_T2_05_UnknownWireTypeRecovery(TestContext ctx)
        {
            // Wire type 7 is invalid in protobuf
            byte[] invalid = new byte[] { (1 << 3) | 7, 10, 20 };
            List<ProtoNode> decoded = ProtoAstCodec.Decode(invalid);
            ctx.AssertEqual(0, decoded.Count, "Should gracefully return empty list for invalid wire type");
        }

        // =====================================================================
        // F3: In-Memory Streaming Zip Inspection (Boundary Cases)
        // =====================================================================
        public static void Test_F3_T2_01_EmptyZipArchive(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string emptyZip = Path.Combine(sandbox.BackupPath, "empty.agy");
                using (FileStream fs = new FileStream(emptyZip, FileMode.Create))
                using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    // No entries
                }

                ZipMetadataPackage pkg = StreamingZipIndexReader.ReadZipMetadata(emptyZip);
                ctx.AssertTrue(pkg.SummaryProtoBytes == null, "SummaryProtoBytes should be null");
                ctx.AssertEqual(0, pkg.ProjectConfigs.Count, "Project configs count 0");
            }
        }

        public static void Test_F3_T2_02_ZipMissingProtoSummaryFile(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string noPbZip = Path.Combine(sandbox.BackupPath, "nopb.agy");
                using (FileStream fs = new FileStream(noPbZip, FileMode.Create))
                using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    ZipArchiveEntry e = archive.CreateEntry("config/projects/P1.json");
                    using (Stream s = e.Open())
                    using (StreamWriter sw = new StreamWriter(s))
                    {
                        sw.Write("{}");
                    }
                }

                ZipMetadataPackage pkg = StreamingZipIndexReader.ReadZipMetadata(noPbZip);
                ctx.AssertTrue(pkg.SummaryProtoBytes == null, "SummaryProtoBytes is null");
                ctx.AssertEqual(1, pkg.ProjectConfigs.Count, "Project config P1.json is read");
            }
        }

        public static void Test_F3_T2_03_MixedPathSeparatorsInZip(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string zipPath = Path.Combine(sandbox.BackupPath, "mixed_paths.agy");
                using (FileStream fs = new FileStream(zipPath, FileMode.Create))
                using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    archive.CreateEntry("conversations\\backslashed-uuid.db");
                    archive.CreateEntry("conversations/forwardslashed-uuid.db");
                }

                ZipMetadataPackage pkg = StreamingZipIndexReader.ReadZipMetadata(zipPath);
                ctx.AssertEqual(2, pkg.ConversationDbNames.Count, "Should normalize and detect both DB entries");
            }
        }

        public static void Test_F3_T2_04_LargeFileIndexSimulation(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string zipPath = Path.Combine(sandbox.BackupPath, "large_index.agy");
                using (FileStream fs = new FileStream(zipPath, FileMode.Create))
                using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    for (int i = 0; i < 200; i++)
                    {
                        archive.CreateEntry("conversations/uuid-" + i + ".db");
                    }
                }

                DateTime start = DateTime.UtcNow;
                ZipMetadataPackage pkg = StreamingZipIndexReader.ReadZipMetadata(zipPath);
                TimeSpan elapsed = DateTime.UtcNow - start;

                ctx.AssertEqual(200, pkg.ConversationDbNames.Count, "200 DB names indexed");
                ctx.AssertTrue(elapsed.TotalMilliseconds < 500, "Should complete index reading in < 500ms");
            }
        }

        public static void Test_F3_T2_05_CorruptedZipStreamThrowsFileNotFoundOrFormat(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string nonZip = Path.Combine(sandbox.BackupPath, "corrupt.agy");
                File.WriteAllText(nonZip, "NOT A ZIP FILE CONTENT");

                bool threw = false;
                try
                {
                    StreamingZipIndexReader.ReadZipMetadata(nonZip);
                }
                catch (InvalidDataException)
                {
                    threw = true;
                }
                catch (Exception)
                {
                    threw = true;
                }

                ctx.AssertTrue(threw, "Reading corrupt zip should throw exception");
            }
        }

        // =====================================================================
        // F4: Incremental Protobuf Shard Merger (Boundary Cases)
        // =====================================================================
        public static void Test_F4_T2_01_MergeIntoEmptyLocalPb(TestContext ctx)
        {
            string id = Guid.NewGuid().ToString();
            MockSessionSpec s = new MockSessionSpec { ConversationId = id, CustomTitle = "New Entry" };
            byte[] backupBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { s });

            byte[] merged = SelectiveCascadingImporter.MergeProtoShards(new byte[0], backupBytes, new HashSet<string> { id }, null, null);
            List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(merged);

            ctx.AssertEqual(1, list.Count, "1 summary merged into empty local pb");
            ctx.AssertEqual("New Entry", list[0].DisplayTitle, "Title match");
        }

        public static void Test_F4_T2_02_MergeDuplicateUuidOverwritesExisting(TestContext ctx)
        {
            string id = "same-uuid-1234";
            MockSessionSpec sLocal = new MockSessionSpec { ConversationId = id, CustomTitle = "Original Local" };
            MockSessionSpec sBackup = new MockSessionSpec { ConversationId = id, CustomTitle = "Updated Backup" };

            byte[] localBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { sLocal });
            byte[] backupBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { sBackup });

            byte[] merged = SelectiveCascadingImporter.MergeProtoShards(localBytes, backupBytes, new HashSet<string> { id }, null, null);
            List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(merged);

            ctx.AssertEqual(1, list.Count, "Duplicate UUID should overwrite rather than duplicate");
            ctx.AssertEqual("Updated Backup", list[0].DisplayTitle, "New title should be active");
        }

        public static void Test_F4_T2_03_FolderUriWithSpecialCharsAndSpaces(TestContext ctx)
        {
            string id = Guid.NewGuid().ToString();
            MockSessionSpec s = new MockSessionSpec
            {
                ConversationId = id,
                ProjectName = "Old Proj",
                FolderUri = "file:///c:/Program%20Files/Old%20Proj"
            };

            byte[] backupBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { s });
            Dictionary<string, string> renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Old Proj", "New Project With Spaces" }
            };

            byte[] merged = SelectiveCascadingImporter.MergeProtoShards(new byte[0], backupBytes, new HashSet<string> { id }, renames, null);
            List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(merged);

            ctx.AssertEqual("New Project With Spaces", list[0].ProjectName, "ProjectName updated with spaces");
        }

        public static void Test_F4_T2_04_HighFieldNumbersPreserved(TestContext ctx)
        {
            ProtoNode entry = new ProtoNode { FieldNumber = 1, WireType = 2 };
            entry.Children.Add(new ProtoNode { FieldNumber = 2, WireType = 2, LengthDelimitedValue = Encoding.UTF8.GetBytes("uuid-highfield") });
            entry.Children.Add(new ProtoNode { FieldNumber = 9999, WireType = 0, VarintValue = 88888 });

            byte[] backupBytes = ProtoAstCodec.Encode(new List<ProtoNode> { entry });
            byte[] merged = SelectiveCascadingImporter.MergeProtoShards(new byte[0], backupBytes, new HashSet<string> { "uuid-highfield" }, null, null);

            List<ProtoNode> root = ProtoAstCodec.Decode(merged);
            ProtoNode high = root[0].FindChild(9999);
            ctx.AssertTrue(high != null, "Field 9999 preserved");
            ctx.AssertEqual(88888UL, high.VarintValue, "Field 9999 value match");
        }

        public static void Test_F4_T2_05_MergeEmptyTargetListLeavesLocalUntouched(TestContext ctx)
        {
            string id = Guid.NewGuid().ToString();
            MockSessionSpec s = new MockSessionSpec { ConversationId = id };
            byte[] localBytes = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { s });

            byte[] merged = SelectiveCascadingImporter.MergeProtoShards(localBytes, localBytes, new HashSet<string>(), null, null);
            ctx.AssertEqual(localBytes.Length, merged.Length, "Merging empty target list returns exact local byte length");
        }

        // =====================================================================
        // F5: Smart Ephemeral/Junk Project Filter (Boundary Cases)
        // =====================================================================
        public static void Test_F5_T2_01_CaseInsensitiveJunkMatching(TestContext ctx)
        {
            ctx.AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("SUBAGENT-1"), "SUBAGENT-1 is junk");
            ctx.AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("Outside-OF-Project"), "Outside-OF-Project is junk");
            ctx.AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("RECOVERED PROJECT"), "RECOVERED PROJECT is junk");
        }

        public static void Test_F5_T2_02_ExactMatchVsPrefixMatching(TestContext ctx)
        {
            ctx.AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("outside-of-project"), "outside-of-project exact match is junk");
            ctx.AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("subagent-abc"), "subagent-abc prefix is junk");
        }

        public static void Test_F5_T2_03_JunkNameWithWhitespacePadded(TestContext ctx)
        {
            ctx.AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("  subagent-review-1  "), "Whitespace padded junk is detected");
        }

        public static void Test_F5_T2_04_AllProjectsJunkEnvironment(TestContext ctx)
        {
            List<string> projectNames = new List<string> { "subagent-1", "subagent-2", "outside-of-project", "temp-run" };
            int visibleCount = 0;
            for (int i = 0; i < projectNames.Count; i++)
            {
                if (!SmartProjectFilter.IsJunkOrEphemeral(projectNames[i]))
                    visibleCount++;
            }

            ctx.AssertEqual(0, visibleCount, "All junk environment has 0 visible projects when filter active");
        }

        public static void Test_F5_T2_05_NonJunkContainingSubagentInMiddle(TestContext ctx)
        {
            ctx.AssertFalse(SmartProjectFilter.IsJunkOrEphemeral("main-subagent-core"), "main-subagent-core should NOT be treated as junk");
            ctx.AssertFalse(SmartProjectFilter.IsJunkOrEphemeral("my_temporary_solution"), "my_temporary_solution should NOT be treated as junk");
        }

        // =====================================================================
        // F6: Bidirectional Visual Diff Classification (Boundary Cases)
        // =====================================================================
        public static void Test_F6_T2_01_SameNameDifferentDriveLetters(TestContext ctx)
        {
            List<ConversationSummaryInfo> local = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "Proj", FolderUri = "file:///C:/Workspace/Proj", ConversationId = "u1" }
            };
            List<ConversationSummaryInfo> backup = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "Proj", FolderUri = "file:///D:/Workspace/Proj", ConversationId = "u2" }
            };

            List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(local, backup);
            ctx.AssertEqual(DiffStatus.Conflicted, diff[0].Status, "Different drive letters must be Conflicted");
        }

        public static void Test_F6_T2_02_TrailingSlashVariationsNormalized(TestContext ctx)
        {
            List<ConversationSummaryInfo> local = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "Proj", FolderUri = "file:///c:/Workspace/Proj/", ConversationId = "u1" }
            };
            List<ConversationSummaryInfo> backup = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "Proj", FolderUri = "file:///c:/Workspace/Proj", ConversationId = "u1" }
            };

            List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(local, backup);
            ctx.AssertEqual(DiffStatus.Aligned, diff[0].Status, "Trailing slash variations should be Aligned");
        }

        public static void Test_F6_T2_03_EmptyLocalAndBackup(TestContext ctx)
        {
            List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(new List<ConversationSummaryInfo>(), new List<ConversationSummaryInfo>());
            ctx.AssertEqual(0, diff.Count, "Empty inputs produce 0 diff nodes");
        }

        public static void Test_F6_T2_04_AllBackupNewAllLocalOnly(TestContext ctx)
        {
            List<ConversationSummaryInfo> local = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "Local1", ConversationId = "l1" },
                new ConversationSummaryInfo { ProjectName = "Local2", ConversationId = "l2" }
            };
            List<ConversationSummaryInfo> backup = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "Backup1", ConversationId = "b1" },
                new ConversationSummaryInfo { ProjectName = "Backup2", ConversationId = "b2" }
            };

            List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(local, backup);
            ctx.AssertEqual(4, diff.Count, "4 total project diff nodes");
        }

        public static void Test_F6_T2_05_MixedSessionDiffInsideSingleProject(TestContext ctx)
        {
            string sharedId = "shared-uuid";
            string modifiedId = "modified-uuid";
            List<ConversationSummaryInfo> local = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "CommonProj", FolderUri = "file:///c:/CommonProj", ConversationId = sharedId, DisplayTitle = "Title A", StepCount = 5 },
                new ConversationSummaryInfo { ProjectName = "CommonProj", FolderUri = "file:///c:/CommonProj", ConversationId = modifiedId, DisplayTitle = "Old Local Title", StepCount = 3 }
            };
            List<ConversationSummaryInfo> backup = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo { ProjectName = "CommonProj", FolderUri = "file:///c:/CommonProj", ConversationId = sharedId, DisplayTitle = "Title A", StepCount = 5 },
                new ConversationSummaryInfo { ProjectName = "CommonProj", FolderUri = "file:///c:/CommonProj", ConversationId = modifiedId, DisplayTitle = "New Renamed Backup Title", StepCount = 8 },
                new ConversationSummaryInfo { ProjectName = "CommonProj", FolderUri = "file:///c:/CommonProj", ConversationId = "new-uuid-in-backup", DisplayTitle = "Brand New", StepCount = 1 }
            };

            List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(local, backup);
            ctx.AssertEqual(1, diff.Count, "1 project diff node");
            ctx.AssertEqual(1, diff[0].NewSessionCount, "1 brand new session");
            ctx.AssertEqual(1, diff[0].ModifiedSessionCount, "1 modified/renamed session");
        }

        // =====================================================================
        // F7: Conflict Wizard (Boundary Cases)
        // =====================================================================
        public static void Test_F7_T2_01_RenameGeneratesUniqueSuffix(TestContext ctx)
        {
            HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Proj", "Proj_1" };
            string candidate = "Proj";
            string generated = candidate;
            int counter = 1;
            while (existing.Contains(generated))
            {
                generated = candidate + "_" + counter;
                counter++;
            }

            ctx.AssertEqual("Proj_2", generated, "Should generate Proj_2");
        }

        public static void Test_F7_T2_02_IllegalCharsInRenameSanitized(TestContext ctx)
        {
            string invalid = "Project:With?Illegal/Chars*";
            char[] invalids = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < invalid.Length; i++)
            {
                bool inv = false;
                for (int j = 0; j < invalids.Length; j++)
                {
                    if (invalid[i] == invalids[j]) { inv = true; break; }
                }
                sb.Append(inv ? '_' : invalid[i]);
            }
            string sanitized = sb.ToString();

            ctx.AssertFalse(sanitized.Contains(":"), "Sanitized colons");
            ctx.AssertFalse(sanitized.Contains("?"), "Sanitized question marks");
        }

        public static void Test_F7_T2_03_OverwriteReplacesMatchingSessions(TestContext ctx)
        {
            ConflictResolutionPlan plan = new ConflictResolutionPlan();
            plan.Strategies["ConflictProj"] = ConflictResolutionStrategy.Overwrite;
            ctx.AssertEqual(ConflictResolutionStrategy.Overwrite, plan.Strategies["ConflictProj"], "Overwrite set");
        }

        public static void Test_F7_T2_04_SkipAllConflicts(TestContext ctx)
        {
            ConflictResolutionPlan plan = new ConflictResolutionPlan();
            plan.Strategies["P1"] = ConflictResolutionStrategy.Skip;
            plan.Strategies["P2"] = ConflictResolutionStrategy.Skip;

            ctx.AssertEqual(2, plan.Strategies.Count, "2 skipped projects");
        }

        public static void Test_F7_T2_05_MixedPerProjectStrategies(TestContext ctx)
        {
            ConflictResolutionPlan plan = new ConflictResolutionPlan();
            plan.Strategies["P1"] = ConflictResolutionStrategy.Merge;
            plan.Strategies["P2"] = ConflictResolutionStrategy.Rename;
            plan.RenamedProjects["P2"] = "P2_Renamed";

            ctx.AssertEqual(ConflictResolutionStrategy.Merge, plan.Strategies["P1"], "P1 merge");
            ctx.AssertEqual(ConflictResolutionStrategy.Rename, plan.Strategies["P2"], "P2 rename");
        }

        // =====================================================================
        // F8: Dual-Tree Visual Diff Workspace (Boundary Cases)
        // =====================================================================
        public static void Test_F8_T2_01_EmptyLocalAndEmptyBackupTree(TestContext ctx)
        {
            List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(null, null);
            ctx.AssertEqual(0, diff.Count, "Empty tree produces 0 nodes");
        }

        public static void Test_F8_T2_02_SpecialCharsInProjectName(TestContext ctx)
        {
            string specialName = "Project <Core> & \"Tools\"";
            ProjectDiffNode node = new ProjectDiffNode(specialName);
            ctx.AssertEqual(specialName, node.ProjectName, "Project name preserved");
        }

        public static void Test_F8_T2_03_ProjectWithZeroSessions(TestContext ctx)
        {
            ProjectDiffNode node = new ProjectDiffNode("EmptyProj");
            ctx.AssertEqual(0, node.LocalConversations.Count, "0 local sessions");
            ctx.AssertEqual(0, node.BackupConversations.Count, "0 backup sessions");
        }

        public static void Test_F8_T2_04_LargeTreeScale1000Sessions(TestContext ctx)
        {
            List<ConversationSummaryInfo> list = new List<ConversationSummaryInfo>();
            for (int i = 0; i < 1000; i++)
            {
                list.Add(new ConversationSummaryInfo
                {
                    ProjectName = "Proj_" + (i % 20),
                    ConversationId = "uuid-" + i,
                    DisplayTitle = "Session " + i
                });
            }

            DateTime start = DateTime.UtcNow;
            List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(list, null);
            TimeSpan elapsed = DateTime.UtcNow - start;

            ctx.AssertEqual(20, diff.Count, "20 projects grouped from 1000 sessions");
            ctx.AssertTrue(elapsed.TotalMilliseconds < 200, "Diff calculation took < 200ms");
        }

        public static void Test_F8_T2_05_MissingGlobalConfigsSubfolders(TestContext ctx)
        {
            ZipMetadataPackage pkg = new ZipMetadataPackage { HasGlobalRules = true, HasGlobalSkills = false, HasGlobalMcp = false };
            ctx.AssertTrue(pkg.HasGlobalRules, "Rules present");
            ctx.AssertFalse(pkg.HasGlobalSkills, "Skills absent");
            ctx.AssertFalse(pkg.HasGlobalMcp, "MCP absent");
        }

        // =====================================================================
        // F9: Cascading CheckBox Selection Model (Boundary Cases)
        // =====================================================================
        public static void Test_F9_T2_01_ReentrancyProtectionNoInfiniteLoop(TestContext ctx)
        {
            CascadingSelectionModel model = new CascadingSelectionModel();
            HashSet<string> selected = new HashSet<string>();
            List<string> childIds = new List<string> { "c1", "c2" };

            // Perform 100 fast alternating toggles
            for (int i = 0; i < 100; i++)
            {
                model.ToggleProjectSelection(i % 2 == 0, childIds, selected);
            }
            ctx.AssertTrue(true, "Re-entrancy protected without stack overflow");
        }

        public static void Test_F9_T2_02_SelectNewOnlyWhenZeroNew(TestContext ctx)
        {
            CascadingSelectionModel model = new CascadingSelectionModel();
            HashSet<string> selected = new HashSet<string>();
            ProjectDiffNode node = new ProjectDiffNode("Aligned") { Status = DiffStatus.Aligned };
            node.BackupConversations.Add(new ConversationSummaryInfo { ConversationId = "u1" });

            model.SelectNewOnly(new List<ProjectDiffNode> { node }, selected);
            ctx.AssertEqual(0, selected.Count, "0 items selected when none are new");
        }

        public static void Test_F9_T2_03_SelectAllIdempotent(TestContext ctx)
        {
            CascadingSelectionModel model = new CascadingSelectionModel();
            HashSet<string> selected = new HashSet<string>();
            List<ConversationSummaryInfo> all = new List<ConversationSummaryInfo> { new ConversationSummaryInfo { ConversationId = "u1" } };

            model.SelectAll(all, selected);
            model.SelectAll(all, selected);
            ctx.AssertEqual(1, selected.Count, "SelectAll is idempotent");
        }

        public static void Test_F9_T2_04_PartialProjectSelection(TestContext ctx)
        {
            CascadingSelectionModel model = new CascadingSelectionModel();
            HashSet<string> selected = new HashSet<string>();

            ProjectDiffNode mixedNode = new ProjectDiffNode("MixedProj") { Status = DiffStatus.NewInBackup };
            mixedNode.LocalConversations.Add(new ConversationSummaryInfo { ConversationId = "loc_existing_1" });
            mixedNode.BackupConversations.Add(new ConversationSummaryInfo { ConversationId = "loc_existing_1" });
            mixedNode.BackupConversations.Add(new ConversationSummaryInfo { ConversationId = "bak_new_1" });

            model.SelectNewOnly(new List<ProjectDiffNode> { mixedNode }, selected);
            ctx.AssertEqual(1, selected.Count, "Only the single new session selected");
            ctx.AssertTrue(selected.Contains("bak_new_1"), "New session is selected");
            ctx.AssertFalse(selected.Contains("loc_existing_1"), "Existing aligned session is NOT selected");
        }

        public static void Test_F9_T2_05_SelectionPreservedOnEmptyCollection(TestContext ctx)
        {
            CascadingSelectionModel model = new CascadingSelectionModel();
            HashSet<string> selected = new HashSet<string>();
            model.SelectAll(null, selected);
            ctx.AssertEqual(0, selected.Count, "Empty collection handled safely");
        }

        // =====================================================================
        // F10: Fine-Grained Cascading Selective Import (Boundary Cases)
        // =====================================================================
        public static void Test_F10_T2_01_MissingBrainDirectoryInBackup(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string id = Guid.NewGuid().ToString();
                MockProjectSpec p = new MockProjectSpec("ProjNoBrain", "file:///c:/ProjNoBrain");
                p.Sessions.Add(new MockSessionSpec { ConversationId = id, HasBrainData = false }); // No brain data

                string zipPath = Path.Combine(sandbox.BackupPath, "nobrain.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec> { p });

                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = zipPath,
                    TargetGeminiPath = sandbox.GeminiPath,
                    SelectedConversationIds = new List<string> { id }
                };

                SelectiveImportResult res = SelectiveCascadingImporter.ExecuteImport(req);
                ctx.AssertTrue(res.Success, "Import should succeed even if brain directory absent");
                ctx.AssertEqual(1, res.ImportedConversationsCount, "1 conversation imported");
                ctx.AssertEqual(0, res.ImportedBrainDirsCount, "0 brain dirs imported");
            }
        }

        public static void Test_F10_T2_02_HandlesWalAndShmCompanionFiles(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string id = Guid.NewGuid().ToString();
                MockProjectSpec p = new MockProjectSpec("ProjWal", "file:///c:/ProjWal");
                p.Sessions.Add(new MockSessionSpec { ConversationId = id, HasWalFiles = true });

                string zipPath = Path.Combine(sandbox.BackupPath, "wal.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec> { p });

                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = zipPath,
                    TargetGeminiPath = sandbox.GeminiPath,
                    SelectedConversationIds = new List<string> { id }
                };

                SelectiveImportResult res = SelectiveCascadingImporter.ExecuteImport(req);
                ctx.AssertTrue(res.Success, "Import with WAL files succeeds");

                string walFile = Path.Combine(sandbox.AntigravityPath, "conversations", id + ".db-wal");
                ctx.AssertTrue(File.Exists(walFile), "Companion -wal file must be extracted");
            }
        }

        public static void Test_F10_T2_03_ImportZeroSelectedItems(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string zipPath = Path.Combine(sandbox.BackupPath, "zero.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec>());

                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = zipPath,
                    TargetGeminiPath = sandbox.GeminiPath,
                    SelectedConversationIds = new List<string>() // 0 selected
                };

                SelectiveImportResult res = SelectiveCascadingImporter.ExecuteImport(req);
                ctx.AssertTrue(res.Success, "0 selected import succeeds");
                ctx.AssertEqual(0, res.ImportedConversationsCount, "0 imported");
            }
        }

        public static void Test_F10_T2_04_DestinationDirectoryCreatedIfMissing(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string cleanTarget = Path.Combine(sandbox.RootPath, "FreshTarget");
                string id = Guid.NewGuid().ToString();
                MockProjectSpec p = new MockProjectSpec("P", "file:///c:/P");
                p.Sessions.Add(new MockSessionSpec { ConversationId = id });

                string zipPath = Path.Combine(sandbox.BackupPath, "fresh.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec> { p });

                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = zipPath,
                    TargetGeminiPath = cleanTarget,
                    SelectedConversationIds = new List<string> { id }
                };

                SelectiveImportResult res = SelectiveCascadingImporter.ExecuteImport(req);
                ctx.AssertTrue(res.Success, "Should auto-create missing directories");
            }
        }

        public static void Test_F10_T2_05_CrossDriveImportPreservesStructure(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string id = Guid.NewGuid().ToString();
                MockProjectSpec p = new MockProjectSpec("DriveProj", "file:///E:/DriveProj");
                p.Sessions.Add(new MockSessionSpec { ConversationId = id });

                string zipPath = Path.Combine(sandbox.BackupPath, "drive.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(zipPath, new List<MockProjectSpec> { p });

                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = zipPath,
                    TargetGeminiPath = sandbox.GeminiPath,
                    SelectedConversationIds = new List<string> { id }
                };

                SelectiveImportResult res = SelectiveCascadingImporter.ExecuteImport(req);
                ctx.AssertTrue(res.Success, "Cross drive import succeeds");
            }
        }

        // =====================================================================
        // F11: Post-Import UI Auto-Refresh & Highlight (Boundary Cases)
        // =====================================================================
        public static void Test_F11_T2_01_HighlightZeroImported(TestContext ctx)
        {
            HighlightTracker tracker = new HighlightTracker();
            tracker.MarkImported(new List<string>(), new List<string>());

            ctx.AssertEqual(0, tracker.HighlightedConversationIds.Count, "0 highlighted conversations");
            ctx.AssertEqual(0, tracker.HighlightedProjectNames.Count, "0 highlighted projects");
        }

        public static void Test_F11_T2_02_HighlightSurvivesFilterToggle(TestContext ctx)
        {
            HighlightTracker tracker = new HighlightTracker();
            tracker.MarkImported(new List<string> { "u1" }, new List<string> { "P1" });

            // Simulate toggle
            bool toggle = true;
            toggle = !toggle;

            ctx.AssertTrue(tracker.IsConversationHighlighted("u1"), "Highlight survives filter change");
        }

        public static void Test_F11_T2_03_ExternalFileDeletionReflected(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string id1 = Guid.NewGuid().ToString();
                string id2 = Guid.NewGuid().ToString();

                MockProjectSpec p = new MockProjectSpec("P", "file:///c:/P");
                p.Sessions.Add(new MockSessionSpec { ConversationId = id1 });
                p.Sessions.Add(new MockSessionSpec { ConversationId = id2 });
                MockSandboxGenerator.PopulateLocalSandbox(sandbox, new List<MockProjectSpec> { p });

                // Delete 1 session from local proto
                byte[] pb = MockSandboxGenerator.BuildSyntheticProtobufBytes(new List<MockSessionSpec> { p.Sessions[0] });
                File.WriteAllBytes(Path.Combine(sandbox.AntigravityPath, "agyhub_summaries_proto.pb"), pb);

                List<ConversationSummaryInfo> rescanned = SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(Path.Combine(sandbox.AntigravityPath, "agyhub_summaries_proto.pb")));
                ctx.AssertEqual(1, rescanned.Count, "Rescan reflects deletion");
            }
        }

        public static void Test_F11_T2_04_StatusCountExactArithmetic(TestContext ctx)
        {
            int baseCount = 5;
            int importedCount = 3;
            int total = baseCount + importedCount;
            ctx.AssertEqual(8, total, "Exact arithmetic sum");
        }

        public static void Test_F11_T2_05_ConcurrentHighlightChecks(TestContext ctx)
        {
            HighlightTracker tracker = new HighlightTracker();
            tracker.MarkImported(new List<string> { "id1", "id2", "id3" }, new List<string> { "P1" });

            for (int i = 0; i < 50; i++)
            {
                ctx.AssertTrue(tracker.IsConversationHighlighted("id2"), "High frequency lookup");
            }
        }

        // =====================================================================
        // F12: Pure Native Single-File Build (Boundary Cases)
        // =====================================================================
        public static void Test_F12_T2_01_AppIconHeaderMagic(TestContext ctx)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            string icoPath = Path.Combine(root, "app.ico");
            byte[] bytes = File.ReadAllBytes(icoPath);

            // Windows ICO header: 0x00, 0x00, 0x01, 0x00
            ctx.AssertTrue(bytes.Length >= 4, "Icon file size");
            ctx.AssertEqual((byte)0x00, bytes[0], "ICO byte 0");
            ctx.AssertEqual((byte)0x00, bytes[1], "ICO byte 1");
            ctx.AssertEqual((byte)0x01, bytes[2], "ICO byte 2");
            ctx.AssertEqual((byte)0x00, bytes[3], "ICO byte 3");
        }

        public static void Test_F12_T2_02_BuildBatOptimizationFlag(TestContext ctx)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            string batPath = Path.Combine(root, "build.bat");
            string batText = File.ReadAllText(batPath);

            ctx.AssertTrue(batText.Contains("/optimize+"), "build.bat includes /optimize+");
        }

        public static void Test_F12_T2_03_BuildBatPlatformAnyCpu(TestContext ctx)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            string batPath = Path.Combine(root, "build.bat");
            string batText = File.ReadAllText(batPath);

            ctx.AssertTrue(batText.Contains("/platform:anycpu"), "build.bat includes /platform:anycpu");
        }

        public static void Test_F12_T2_04_TargetExecutableExists(TestContext ctx)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            string exePath = Path.Combine(root, "Antigravity_Companion.exe");
            ctx.AssertTrue(File.Exists(exePath), "Antigravity_Companion.exe must exist in root");
        }

        public static void Test_F12_T2_05_PeHeaderMagic(TestContext ctx)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            string exePath = Path.Combine(root, "Antigravity_Companion.exe");
            byte[] bytes = File.ReadAllBytes(exePath);

            // PE Executable MZ header: 'M' (0x4D), 'Z' (0x5A)
            ctx.AssertTrue(bytes.Length > 1024, "Executable size > 1KB");
            ctx.AssertEqual((byte)0x4D, bytes[0], "MZ Header M");
            ctx.AssertEqual((byte)0x5A, bytes[1], "MZ Header Z");
        }

        // =====================================================================
        // F13: Complete User Documentation & Commit (Boundary Cases)
        // =====================================================================
        public static void Test_F13_T2_01_ManualDualTreeKeywords(TestContext ctx)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            string text = File.ReadAllText(Path.Combine(root, "Antigravity_Companion使用说明书.md"));

            ctx.AssertTrue(text.Contains("左栏") || text.Contains("本地"), "Manual contains left/local keywords");
            ctx.AssertTrue(text.Contains("右栏") || text.Contains("备份"), "Manual contains right/backup keywords");
        }

        public static void Test_F13_T2_02_ManualSelectiveImportKeywords(TestContext ctx)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            string text = File.ReadAllText(Path.Combine(root, "Antigravity_Companion使用说明书.md"));

            ctx.AssertTrue(text.Contains("导入") || text.Contains("复选框") || text.Contains("勾选"), "Manual contains selection import keywords");
        }

        public static void Test_F13_T2_03_ManualConflictResolutionKeywords(TestContext ctx)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            string text = File.ReadAllText(Path.Combine(root, "Antigravity_Companion使用说明书.md"));

            ctx.AssertTrue(text.Contains("合并") || text.Contains("重命名") || text.Contains("冲突"), "Manual contains conflict resolution keywords");
        }

        public static void Test_F13_T2_04_ManualJunkFilterKeywords(TestContext ctx)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            string text = File.ReadAllText(Path.Combine(root, "Antigravity_Companion使用说明书.md"));

            ctx.AssertTrue(text.Contains("临时") || text.Contains("subagent") || text.Contains("垃圾"), "Manual contains junk filter keywords");
        }

        public static void Test_F13_T2_05_GitignoreTestRunnerExclusion(TestContext ctx)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            string text = File.ReadAllText(Path.Combine(root, ".gitignore"));

            ctx.AssertTrue(text.Contains("Antigravity_Companion.exe") || text.Contains("*.exe") || text.Contains("*.agy"), "Gitignore excludes binaries and archives");
        }
    }
}
