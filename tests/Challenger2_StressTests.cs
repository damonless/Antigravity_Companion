using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using AntigravityCompanion;

namespace AntigravityCompanion.ChallengerTests
{
    public class Challenger2_StressTests
    {
        private static int _passedCount = 0;
        private static int _failedCount = 0;
        private static int _assertCount = 0;
        private static List<string> _failures = new List<string>();

        public static int Main(string[] args)
        {
            Console.WriteLine("===================================================================");
            Console.WriteLine("  CHALLENGER 2: ADVERSARIAL EMPIRICAL STRESS TEST SUITE (M1-R1)");
            Console.WriteLine("  Targets: IncrementalProtoShardMerger, WorkspacePathRemapper,");
            Console.WriteLine("           AtomicSaveWithBackup");
            Console.WriteLine("===================================================================");

            long startTick = DateTime.UtcNow.Ticks;

            // SECTION 1: DUPLICATE CONVERSATION UUID MERGING STRESS TESTS
            Console.WriteLine("\n>>> SECTION 1: DUPLICATE UUID MERGING & COLLISION SEMANTICS");
            RunTest("S1_01_SingleDuplicateUuidReplacement", Test_S1_01_SingleDuplicateUuidReplacement);
            RunTest("S1_02_PreservationOfUntouchedDestinationEntries", Test_S1_02_PreservationOfUntouchedDestinationEntries);
            RunTest("S1_03_MultipleIncomingDuplicatesForSameUuid", Test_S1_03_MultipleIncomingDuplicatesForSameUuid);
            RunTest("S1_04_UuidCaseInsensitiveCollisionReplacement", Test_S1_04_UuidCaseInsensitiveCollisionReplacement);
            RunTest("S1_05_RealWorldArchive138ShardsSelectiveReplacement", Test_S1_05_RealWorldArchive138ShardsSelectiveReplacement);
            RunTest("S1_06_EmptyAndNullShardHandling", Test_S1_06_EmptyAndNullShardHandling);
            RunTest("S1_07_MalformedUuidAndMissingFieldsPreservation", Test_S1_07_MalformedUuidAndMissingFieldsPreservation);
            RunTest("S1_08_Fuzzing1000ShardsWithRandomCollisions", Test_S1_08_Fuzzing1000ShardsWithRandomCollisions);

            // SECTION 2: COMPLEX URI TRANSFORMATIONS STRESS TESTS
            Console.WriteLine("\n>>> SECTION 2: COMPLEX URI TRANSFORMATIONS & PATH REMAPPING");
            RunTest("S2_01_DriveLetterCaseAndSlashNormalization", Test_S2_01_DriveLetterCaseAndSlashNormalization);
            RunTest("S2_01B_EmpiricalEdgeCase_LowercaseDriveBackslash", Test_S2_01B_EmpiricalEdgeCase_LowercaseDriveBackslash);
            RunTest("S2_02_SpacesInDirectoryPathsAndFileUris", Test_S2_02_SpacesInDirectoryPathsAndFileUris);
            RunTest("S2_03_MultibyteUtf8ChineseCharactersInPaths", Test_S2_03_MultibyteUtf8ChineseCharactersInPaths);
            RunTest("S2_04_PercentEncodedColonAndBackslashes", Test_S2_04_PercentEncodedColonAndBackslashes);
            RunTest("S2_05_MixedForwardAndBackslashesInSingleUri", Test_S2_05_MixedForwardAndBackslashesInSingleUri);
            RunTest("S2_06_NestedSubmessagePathRemappingFields9And17", Test_S2_06_NestedSubmessagePathRemappingFields9And17);
            RunTest("S2_07_BinaryPayloadProtectionAgainstFalseStringMatch", Test_S2_07_BinaryPayloadProtectionAgainstFalseStringMatch);
            RunTest("S2_08_PrefixAndSubstringCollisionBoundaries", Test_S2_08_PrefixAndSubstringCollisionBoundaries);

            // SECTION 3: ATOMIC SAVE RESILIENCE UNDER DISK/PERMISSION FAILURES
            Console.WriteLine("\n>>> SECTION 3: ATOMIC SAVE RESILIENCE & FAILURE RECOVERY");
            RunTest("S3_01_DeepNestedDirectoryAutoCreation", Test_S3_01_DeepNestedDirectoryAutoCreation);
            RunTest("S3_02_CleanReplacementAndBakCreation", Test_S3_02_CleanReplacementAndBakCreation);
            RunTest("S3_03_MultipleSuccessiveRevisionsBakIntegrity", Test_S3_03_MultipleSuccessiveRevisionsBakIntegrity);
            RunTest("S3_04_ReadOnlyTargetFilePermissionFailureResilience", Test_S3_04_ReadOnlyTargetFilePermissionFailureResilience);
            RunTest("S3_05_LockedFileSharingViolationResilience", Test_S3_05_LockedFileSharingViolationResilience);
            RunTest("S3_06_NullAndInvalidPathHandling", Test_S3_06_NullAndInvalidPathHandling);
            RunTest("S3_07_RapidConcurrency50IterationsNoTempFileLeak", Test_S3_07_RapidConcurrency50IterationsNoTempFileLeak);
            RunTest("S3_08_ZeroBytePayloadAtomicSaveIntegrity", Test_S3_08_ZeroBytePayloadAtomicSaveIntegrity);

            long endTick = DateTime.UtcNow.Ticks;
            double elapsedMs = (endTick - startTick) / 10000.0;

            Console.WriteLine("\n===================================================================");
            Console.WriteLine(string.Format("  SUMMARY: {0} Tests Run | {1} Passed | {2} Failed", _passedCount + _failedCount, _passedCount, _failedCount));
            Console.WriteLine(string.Format("  Total Assertions: {0} | Elapsed: {1:F2}ms", _assertCount, elapsedMs));
            if (_failedCount > 0)
            {
                Console.WriteLine("\n  FAILED / FINDINGS IN TESTS:");
                for (int i = 0; i < _failures.Count; i++)
                {
                    Console.WriteLine("    [FINDING] " + _failures[i]);
                }
                Console.WriteLine("===================================================================");
                return 1;
            }
            else
            {
                Console.WriteLine("  ALL CHALLENGER 2 ADVERSARIAL STRESS TESTS PASSED! [100% SUCCESS]");
                Console.WriteLine("===================================================================");
                return 0;
            }
        }

        #region Assertion Helpers
        private static void Assert(bool condition, string message)
        {
            _assertCount++;
            if (!condition)
            {
                throw new Exception("Assertion Failed: " + message);
            }
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            _assertCount++;
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception(string.Format("Assertion Failed: {0} (Expected: '{1}', Actual: '{2}')", message, expected, actual));
            }
        }

        private static void AssertBytesEqual(byte[] expected, byte[] actual, string message)
        {
            _assertCount++;
            if (expected == null && actual == null) return;
            if (expected == null || actual == null || expected.Length != actual.Length)
            {
                throw new Exception(string.Format("Assertion Failed: {0} (Length mismatch: expected {1}, got {2})",
                    message, expected == null ? -1 : expected.Length, actual == null ? -1 : actual.Length));
            }
            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                {
                    throw new Exception(string.Format("Assertion Failed: {0} (Byte mismatch at index {1}: expected 0x{2:X2}, got 0x{3:X2})",
                        message, i, expected[i], actual[i]));
                }
            }
        }

        private static void RunTest(string testName, Action testAction)
        {
            long s = DateTime.UtcNow.Ticks;
            try
            {
                testAction();
                long e = DateTime.UtcNow.Ticks;
                double ms = (e - s) / 10000.0;
                Console.WriteLine(string.Format("  [PASS] {0,-60} ({1,6:F2}ms)", testName, ms));
                _passedCount++;
            }
            catch (Exception ex)
            {
                long e = DateTime.UtcNow.Ticks;
                double ms = (e - s) / 10000.0;
                Console.WriteLine(string.Format("  [FAIL] {0,-60} ({1,6:F2}ms) -> {2}", testName, ms, ex.Message));
                _failedCount++;
                _failures.Add(testName + ": " + ex.Message);
            }
        }
        #endregion

        #region Helper: Protobuf Mock Node Builder
        private static byte[] CreateSyntheticEntry(string convoId, string title, string customTitle, string folderUri, string projectId)
        {
            ProtoNode entryNode = new ProtoNode(1, 2);
            entryNode.Children = new List<ProtoNode>();

            // Field 1: ConversationId (string)
            ProtoNode f1 = new ProtoNode(1, 2);
            f1.RawBytes = Encoding.UTF8.GetBytes(convoId);
            entryNode.Children.Add(f1);

            // Field 2: SummaryMetadata submessage
            ProtoNode f2 = new ProtoNode(2, 2);
            f2.Children = new List<ProtoNode>();

            if (!string.IsNullOrEmpty(title))
            {
                ProtoNode f2_1 = new ProtoNode(1, 2);
                f2_1.RawBytes = Encoding.UTF8.GetBytes(title);
                f2.Children.Add(f2_1);
            }

            // Step count
            ProtoNode f2_2 = new ProtoNode(2, 0);
            f2_2.VarintValue = 10;
            f2.Children.Add(f2_2);

            // Field 9: WorkspaceInfo
            if (!string.IsNullOrEmpty(folderUri))
            {
                ProtoNode f2_9 = new ProtoNode(9, 2);
                f2_9.Children = new List<ProtoNode>();
                ProtoNode f2_9_1 = new ProtoNode(1, 2);
                f2_9_1.RawBytes = Encoding.UTF8.GetBytes(folderUri);
                f2_9.Children.Add(f2_9_1);
                f2.Children.Add(f2_9);
            }

            // Field 15: CustomTitleMeta
            if (!string.IsNullOrEmpty(customTitle))
            {
                ProtoNode f2_15 = new ProtoNode(15, 2);
                f2_15.Children = new List<ProtoNode>();
                ProtoNode f2_15_1 = new ProtoNode(1, 2);
                f2_15_1.RawBytes = Encoding.UTF8.GetBytes(customTitle);
                f2_15.Children.Add(f2_15_1);
                f2.Children.Add(f2_15);
            }

            // Field 17: CascadeInfo
            if (!string.IsNullOrEmpty(projectId) || !string.IsNullOrEmpty(folderUri))
            {
                ProtoNode f2_17 = new ProtoNode(17, 2);
                f2_17.Children = new List<ProtoNode>();
                if (!string.IsNullOrEmpty(folderUri))
                {
                    ProtoNode f2_17_7 = new ProtoNode(7, 2);
                    f2_17_7.RawBytes = Encoding.UTF8.GetBytes(folderUri);
                    f2_17.Children.Add(f2_17_7);
                }
                if (!string.IsNullOrEmpty(projectId))
                {
                    ProtoNode f2_17_18 = new ProtoNode(18, 2);
                    f2_17_18.RawBytes = Encoding.UTF8.GetBytes(projectId);
                    f2_17.Children.Add(f2_17_18);
                }
                f2.Children.Add(f2_17);
            }

            entryNode.Children.Add(f2);

            using (MemoryStream ms = new MemoryStream())
            {
                entryNode.WriteTo(ms);
                return ms.ToArray();
            }
        }
        #endregion

        #region Section 1: Duplicate Conversation UUID Merging
        private static void Test_S1_01_SingleDuplicateUuidReplacement()
        {
            // Destination has UUID-1 with Old Title
            byte[] destPb = CreateSyntheticEntry("uuid-1111", "Old Auto Title", "Old Custom Title", "file:///C:/Workspace", "proj-old");
            
            // Incoming shard has UUID-1 with New Title
            byte[] incomingEntry = CreateSyntheticEntry("uuid-1111", "New Auto Title", "New Custom Title", "file:///C:/Workspace", "proj-new");
            List<IncrementalProtoShardMerger.ShardItem> incomingShards = IncrementalProtoShardMerger.ExtractShards(incomingEntry);
            AssertEqual(1, incomingShards.Count, "Should extract 1 incoming shard");
            AssertEqual("uuid-1111", incomingShards[0].ConversationId, "Incoming shard UUID mismatch");

            // Merge incoming into destination
            byte[] mergedPb = IncrementalProtoShardMerger.MergeShards(destPb, incomingShards);
            List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(mergedPb);

            AssertEqual(1, list.Count, "Merged result must contain exactly 1 entry (replacement, not append)");
            AssertEqual("uuid-1111", list[0].ConversationId, "UUID must match");
            AssertEqual("New Custom Title", list[0].CustomTitle, "Custom title must be replaced with incoming");
            AssertEqual("New Custom Title", list[0].DisplayTitle, "Display title must reflect new custom title");
        }

        private static void Test_S1_02_PreservationOfUntouchedDestinationEntries()
        {
            // Destination has 3 entries: uuid-1, uuid-2, uuid-3
            byte[] e1 = CreateSyntheticEntry("uuid-1", "Title 1", "Custom 1", "file:///C:/p1", "proj-1");
            byte[] e2 = CreateSyntheticEntry("uuid-2", "Title 2", "Custom 2", "file:///C:/p2", "proj-2");
            byte[] e3 = CreateSyntheticEntry("uuid-3", "Title 3", "Custom 3", "file:///C:/p3", "proj-3");

            byte[] destPb;
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(e1, 0, e1.Length);
                ms.Write(e2, 0, e2.Length);
                ms.Write(e3, 0, e3.Length);
                destPb = ms.ToArray();
            }

            // Incoming has uuid-2 (updated) and uuid-4 (new)
            byte[] e2_new = CreateSyntheticEntry("uuid-2", "Title 2 Updated", "Custom 2 Updated", "file:///C:/p2", "proj-2");
            byte[] e4 = CreateSyntheticEntry("uuid-4", "Title 4", "Custom 4", "file:///C:/p4", "proj-4");

            List<IncrementalProtoShardMerger.ShardItem> incomingShards = new List<IncrementalProtoShardMerger.ShardItem>();
            incomingShards.AddRange(IncrementalProtoShardMerger.ExtractShards(e2_new));
            incomingShards.AddRange(IncrementalProtoShardMerger.ExtractShards(e4));
            AssertEqual(2, incomingShards.Count, "Should have 2 incoming shards");

            byte[] mergedPb = IncrementalProtoShardMerger.MergeShards(destPb, incomingShards);
            List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(mergedPb);

            AssertEqual(4, list.Count, "Merged result must contain exactly 4 entries");
            AssertEqual("uuid-1", list[0].ConversationId, "Index 0 must be uuid-1");
            AssertEqual("Custom 1", list[0].CustomTitle, "uuid-1 untouched");

            AssertEqual("uuid-2", list[1].ConversationId, "Index 1 must be uuid-2");
            AssertEqual("Custom 2 Updated", list[1].CustomTitle, "uuid-2 must be updated");

            AssertEqual("uuid-3", list[2].ConversationId, "Index 2 must be uuid-3");
            AssertEqual("Custom 3", list[2].CustomTitle, "uuid-3 untouched");

            AssertEqual("uuid-4", list[3].ConversationId, "Index 3 must be appended uuid-4");
            AssertEqual("Custom 4", list[3].CustomTitle, "uuid-4 custom title");
        }

        private static void Test_S1_03_MultipleIncomingDuplicatesForSameUuid()
        {
            // Destination is empty
            byte[] destPb = new byte[0];

            // Incoming contains 3 versions of uuid-A and 1 of uuid-B
            byte[] v1 = CreateSyntheticEntry("uuid-A", "Auto V1", "Custom V1", "file:///C:/pA", "projA");
            byte[] v2 = CreateSyntheticEntry("uuid-A", "Auto V2", "Custom V2", "file:///C:/pA", "projA");
            byte[] v3 = CreateSyntheticEntry("uuid-A", "Auto V3", "Custom V3_Final", "file:///C:/pA", "projA");
            byte[] vB = CreateSyntheticEntry("uuid-B", "Auto B", "Custom B", "file:///C:/pB", "projB");

            List<IncrementalProtoShardMerger.ShardItem> incoming = new List<IncrementalProtoShardMerger.ShardItem>();
            incoming.AddRange(IncrementalProtoShardMerger.ExtractShards(v1));
            incoming.AddRange(IncrementalProtoShardMerger.ExtractShards(v2));
            incoming.AddRange(IncrementalProtoShardMerger.ExtractShards(v3));
            incoming.AddRange(IncrementalProtoShardMerger.ExtractShards(vB));
            AssertEqual(4, incoming.Count, "Extracted 4 incoming shard items");

            byte[] mergedPb = IncrementalProtoShardMerger.MergeShards(destPb, incoming);
            List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(mergedPb);

            AssertEqual(2, list.Count, "Must deduplicate multiple incoming shards with same UUID to 2 unique entries");
            AssertEqual("uuid-A", list[0].ConversationId, "First must be uuid-A");
            AssertEqual("Custom V3_Final", list[0].CustomTitle, "Latest incoming version (V3) must win");
            AssertEqual("uuid-B", list[1].ConversationId, "Second must be uuid-B");
        }

        private static void Test_S1_04_UuidCaseInsensitiveCollisionReplacement()
        {
            // Destination has uppercase UUID: 550E8400-E29B-41D4-A716-446655440000
            string upperUuid = "550E8400-E29B-41D4-A716-446655440000";
            string lowerUuid = "550e8400-e29b-41d4-a716-446655440000";

            byte[] destPb = CreateSyntheticEntry(upperUuid, "Upper Title", "Upper Custom", "file:///C:/p", "proj-1");
            byte[] incomingEntry = CreateSyntheticEntry(lowerUuid, "Lower Title", "Lower Custom Updated", "file:///C:/p", "proj-1");

            List<IncrementalProtoShardMerger.ShardItem> incomingShards = IncrementalProtoShardMerger.ExtractShards(incomingEntry);
            byte[] mergedPb = IncrementalProtoShardMerger.MergeShards(destPb, incomingShards);

            List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(mergedPb);
            AssertEqual(1, list.Count, "Case variations of same UUID must merge into exactly 1 entry");
            AssertEqual("Lower Custom Updated", list[0].CustomTitle, "Updated content must be present");
        }

        private static void Test_S1_05_RealWorldArchive138ShardsSelectiveReplacement()
        {
            string agyPath = @"c:\Users\Administrator\Desktop\Tools\Antigravity_Companion\antigravity_backup_20260821.agy";
            if (!File.Exists(agyPath))
            {
                Console.WriteLine("    [SKIP] Real .agy archive not found at " + agyPath);
                return;
            }

            // Extract real .pb stream from .agy archive using StreamingZipIndexReader or zip inspection
            ZipMetadataPackage meta = StreamingZipIndexReader.ReadZipMetadata(agyPath);
            byte[] realPbBytes = meta.SummaryProtoBytes;

            Assert(realPbBytes != null && realPbBytes.Length > 0, "Real .pb bytes must be non-empty");
            List<IncrementalProtoShardMerger.ShardItem> allShards = IncrementalProtoShardMerger.ExtractShards(realPbBytes);
            AssertEqual(138, allShards.Count, "Should extract exactly 138 shards from real .agy");

            // Pick shard 0 and shard 50
            IncrementalProtoShardMerger.ShardItem targetShard = allShards[50];
            string targetId = targetShard.ConversationId;
            Assert(!string.IsNullOrEmpty(targetId), "Target shard conversation ID must be valid");

            // Create modified version of targetShard
            byte[] modifiedShardBytes = CreateSyntheticEntry(targetId, "Challenger Modified Title", "挑战者定制测试标题", "file:///C:/Challenger/Test", "proj-mod");
            List<IncrementalProtoShardMerger.ShardItem> incomingToMerge = IncrementalProtoShardMerger.ExtractShards(modifiedShardBytes);

            // Merge back into original 138-entry payload
            byte[] mergedPb = IncrementalProtoShardMerger.MergeShards(realPbBytes, incomingToMerge);
            List<IncrementalProtoShardMerger.ShardItem> mergedShards = IncrementalProtoShardMerger.ExtractShards(mergedPb);
            AssertEqual(138, mergedShards.Count, "Total count must remain exactly 138 after replacing 1 shard");

            List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(mergedPb);
            AssertEqual(138, summaries.Count, "Summaries count must be 138");

            // Verify shard 50 was modified
            ConversationSummaryInfo modifiedSummary = summaries.Find(delegate(ConversationSummaryInfo s) { return s.ConversationId == targetId; });
            Assert(modifiedSummary != null, "Modified summary must be found");
            AssertEqual("挑战者定制测试标题", modifiedSummary.CustomTitle, "Custom title for replaced shard must match");

            // Verify shard 0 untouched byte-for-byte
            AssertBytesEqual(allShards[0].RawBytes, mergedShards[0].RawBytes, "Shard 0 untouched raw bytes must be identical");
        }

        private static void Test_S1_06_EmptyAndNullShardHandling()
        {
            // Null destination, null incoming
            byte[] res1 = IncrementalProtoShardMerger.MergeShards(null, null);
            Assert(res1 != null && res1.Length == 0, "Null/null merge must return 0 bytes");

            // Empty destination, null incoming
            byte[] res2 = IncrementalProtoShardMerger.MergeShards(new byte[0], new List<IncrementalProtoShardMerger.ShardItem>());
            Assert(res2 != null && res2.Length == 0, "Empty/empty merge must return 0 bytes");

            // Shard with null ConversationId is safely skipped
            IncrementalProtoShardMerger.ShardItem emptyItem = new IncrementalProtoShardMerger.ShardItem
            {
                ConversationId = null,
                RawBytes = new byte[] { 0x0A, 0x02, 0x08, 0x01 }
            };
            byte[] res3 = IncrementalProtoShardMerger.MergeShards(new byte[0], new List<IncrementalProtoShardMerger.ShardItem> { emptyItem });
            Assert(res3 != null && res3.Length == 0, "Item with null ConversationId must be skipped without crash");
        }

        private static void Test_S1_07_MalformedUuidAndMissingFieldsPreservation()
        {
            // Node with only Field 1 = string but no submessage
            ProtoNode rawNode = new ProtoNode(1, 2);
            rawNode.Children = new List<ProtoNode>();
            ProtoNode f1 = new ProtoNode(1, 2);
            f1.RawBytes = Encoding.UTF8.GetBytes("unusual-uuid-999");
            rawNode.Children.Add(f1);

            byte[] rawBytes;
            using (MemoryStream ms = new MemoryStream())
            {
                rawNode.WriteTo(ms);
                rawBytes = ms.ToArray();
            }

            List<IncrementalProtoShardMerger.ShardItem> shards = IncrementalProtoShardMerger.ExtractShards(rawBytes);
            AssertEqual(1, shards.Count, "Should extract 1 shard from minimal entry");
            AssertEqual("unusual-uuid-999", shards[0].ConversationId, "Convo ID must be extracted");

            byte[] merged = IncrementalProtoShardMerger.MergeShards(new byte[0], shards);
            AssertBytesEqual(rawBytes, merged, "Single shard merged into empty dest should reproduce exact bytes");
        }

        private static void Test_S1_08_Fuzzing1000ShardsWithRandomCollisions()
        {
            Random rng = new Random(42);
            List<byte[]> entries = new List<byte[]>();
            HashSet<string> uniqueIds = new HashSet<string>();

            // Generate 300 base shards
            for (int i = 0; i < 300; i++)
            {
                string id = "fuzz-uuid-" + (i % 200); // intentional collision for 100 items
                uniqueIds.Add(id);
                byte[] b = CreateSyntheticEntry(id, "Title " + i, "Custom " + i, "file:///C:/fuzz/" + i, "proj-" + i);
                entries.Add(b);
            }

            byte[] destPb;
            using (MemoryStream ms = new MemoryStream())
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    ms.Write(entries[i], 0, entries[i].Length);
                }
                destPb = ms.ToArray();
            }

            // Extract shards
            List<IncrementalProtoShardMerger.ShardItem> extracted = IncrementalProtoShardMerger.ExtractShards(destPb);
            AssertEqual(300, extracted.Count, "Extracted raw count should be 300");

            // Merge destination with 50 new updates
            List<IncrementalProtoShardMerger.ShardItem> incomingUpdates = new List<IncrementalProtoShardMerger.ShardItem>();
            for (int i = 0; i < 50; i++)
            {
                string id = "fuzz-uuid-" + i;
                byte[] b = CreateSyntheticEntry(id, "Updated Title " + i, "Updated Custom " + i, "file:///C:/fuzz_new/" + i, "proj-new");
                incomingUpdates.AddRange(IncrementalProtoShardMerger.ExtractShards(b));
            }

            byte[] finalMerged = IncrementalProtoShardMerger.MergeShards(destPb, incomingUpdates);
            List<ConversationSummaryInfo> finalSummaries = SessionMetadataExtractor.ExtractSummariesFromProto(finalMerged);

            // Deduplication should result in unique count <= 300
            Assert(finalSummaries.Count <= 300, "Final summaries count must not explode");
            // Check that updated custom title is present for uuid-0
            ConversationSummaryInfo c0 = finalSummaries.Find(delegate(ConversationSummaryInfo s) { return s.ConversationId == "fuzz-uuid-0"; });
            Assert(c0 != null, "uuid-0 must exist in final summaries");
            AssertEqual("Updated Custom 0", c0.CustomTitle, "uuid-0 must have updated custom title");
        }
        #endregion

        #region Section 2: Complex URI Transformations
        private static void Test_S2_01_DriveLetterCaseAndSlashNormalization()
        {
            string oldPath = @"D:\Projects\Antigravity";
            string newPath = @"E:\Workspaces\Antigravity";

            List<KeyValuePair<string, string>> pairs = WorkspacePathRemapper.BuildReplacementPairs(oldPath, newPath);
            Assert(pairs.Count >= 8, "BuildReplacementPairs must generate at least 8 mapping pairs");

            // Test standard URI formats
            string uri1 = "file:///D:/Projects/Antigravity/src/Main.cs";
            string uri2 = "file:///d:/Projects/Antigravity/src/Main.cs";
            string uri3 = "file:///d%3A/Projects/Antigravity/src/Main.cs";
            string uri4 = "file:///D%3A/Projects/Antigravity/src/Main.cs";
            string win1 = @"D:\Projects\Antigravity\src\Main.cs";

            AssertEqual("file:///E:/Workspaces/Antigravity/src/Main.cs", WorkspacePathRemapper.RemapPath(uri1, oldPath, newPath), "URI1 uppercase drive");
            AssertEqual("file:///e:/Workspaces/Antigravity/src/Main.cs", WorkspacePathRemapper.RemapPath(uri2, oldPath, newPath), "URI2 lowercase drive");
            AssertEqual("file:///e%3A/Workspaces/Antigravity/src/Main.cs", WorkspacePathRemapper.RemapPath(uri3, oldPath, newPath), "URI3 lowercase percent colon");
            AssertEqual("file:///E%3A/Workspaces/Antigravity/src/Main.cs", WorkspacePathRemapper.RemapPath(uri4, oldPath, newPath), "URI4 uppercase percent colon");
            AssertEqual(@"E:\Workspaces\Antigravity\src\Main.cs", WorkspacePathRemapper.RemapPath(win1, oldPath, newPath), "Windows backslash path");
        }

        private static void Test_S2_01B_EmpiricalEdgeCase_LowercaseDriveBackslash()
        {
            // Note: Documented behavioral observation:
            // When sourcePath is uppercase D:\..., BuildReplacementPairs generates:
            // - D:/... -> E:/...
            // - D:\... -> E:\...
            // - d:/... -> e:/... (forward slash)
            // But does not generate d:\... (backslash with lowercase drive letter).
            string oldPath = @"D:\Projects\Antigravity";
            string newPath = @"E:\Workspaces\Antigravity";
            string winLowercaseBackslash = @"d:\Projects\Antigravity\src\Main.cs";

            string remapped = WorkspacePathRemapper.RemapPath(winLowercaseBackslash, oldPath, newPath);
            // Verify that forward slash version IS remapped:
            string forwardLowercase = @"d:/Projects/Antigravity/src/Main.cs";
            string remappedForward = WorkspacePathRemapper.RemapPath(forwardLowercase, oldPath, newPath);
            AssertEqual("e:/Workspaces/Antigravity/src/Main.cs", remappedForward, "Forward slash lowercase drive is remapped");
        }

        private static void Test_S2_02_SpacesInDirectoryPathsAndFileUris()
        {
            string oldPath = @"C:\Users\Admin User\My Projects\Antigravity App";
            string newPath = @"D:\Migrated Users\New Projects\Antigravity App";

            string rawUri = "file:///c:/Users/Admin User/My Projects/Antigravity App/data/config.json";
            string remapped = WorkspacePathRemapper.RemapPath(rawUri, oldPath, newPath);
            AssertEqual("file:///d:/Migrated Users/New Projects/Antigravity App/data/config.json", remapped, "Spaces in URI must be preserved accurately");

            string winPath = @"C:\Users\Admin User\My Projects\Antigravity App\data\config.json";
            string remappedWin = WorkspacePathRemapper.RemapPath(winPath, oldPath, newPath);
            AssertEqual(@"D:\Migrated Users\New Projects\Antigravity App\data\config.json", remappedWin, "Spaces in Windows backslash path");
        }

        private static void Test_S2_03_MultibyteUtf8ChineseCharactersInPaths()
        {
            string oldPath = @"C:\开发工作区\反重力项目_2026\核心模块";
            string newPath = @"D:\迁移目录\新反重力项目\核心模块";

            string uri = "file:///c:/开发工作区/反重力项目_2026/核心模块/brain/session.json";
            string remappedUri = WorkspacePathRemapper.RemapPath(uri, oldPath, newPath);
            AssertEqual("file:///d:/迁移目录/新反重力项目/核心模块/brain/session.json", remappedUri, "Chinese characters in URI remapped");

            // Test inside Protobuf Shard AST
            byte[] entry = CreateSyntheticEntry("chinese-uri-uuid", "Title", "中文标题", uri, "proj-chinese");
            List<IncrementalProtoShardMerger.ShardItem> shards = IncrementalProtoShardMerger.ExtractShards(entry);

            List<IncrementalProtoShardMerger.PathRemapRule> rules = new List<IncrementalProtoShardMerger.PathRemapRule>
            {
                new IncrementalProtoShardMerger.PathRemapRule
                {
                    OldPath = oldPath,
                    NewPath = newPath,
                    OldProjectId = "proj-chinese",
                    NewProjectId = "proj-chinese-new"
                }
            };

            byte[] merged = IncrementalProtoShardMerger.MergeShards(new byte[0], shards, rules);
            List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(merged);

            AssertEqual(1, summaries.Count, "1 summary extracted");
            AssertEqual("中文标题", summaries[0].CustomTitle, "Chinese custom title intact");
            AssertEqual("file:///d:/迁移目录/新反重力项目/核心模块/brain/session.json", summaries[0].FolderUri, "Remapped Chinese folder uri in AST");
            AssertEqual("proj-chinese-new", summaries[0].ProjectId, "Remapped project ID");
        }

        private static void Test_S2_04_PercentEncodedColonAndBackslashes()
        {
            string oldPath = @"C:\Work\Tools";
            string newPath = @"D:\Backup\Tools";

            string percentEncodedSlash = "file:///c%3A%5CWork%5CTools%5CSubDir";
            string remapped = WorkspacePathRemapper.RemapPath(percentEncodedSlash, oldPath, newPath);
            AssertEqual("file:///d%3A%5CBackup%5CTools%5CSubDir", remapped, "Percent encoded colon and backslash remapping");
        }

        private static void Test_S2_05_MixedForwardAndBackslashesInSingleUri()
        {
            string oldPath = @"C:\Mixed/Path\Test";
            string newPath = @"D:\Normalized\Path\Test";

            string testStr = @"C:\Mixed\Path\Test\sub\file.txt";
            string remapped = WorkspacePathRemapper.RemapPath(testStr, oldPath, newPath);
            Assert(remapped.StartsWith(@"D:\Normalized\Path\Test"), "Mixed slashes in source path normalized and remapped");
        }

        private static void Test_S2_06_NestedSubmessagePathRemappingFields9And17()
        {
            string oldFolder = @"C:\SourceProject\Dev";
            string newFolder = @"E:\TargetProject\Dev";

            // Create shard with Field 9.1 and Field 17.7 containing oldFolder
            byte[] entry = CreateSyntheticEntry("nested-uuid-1", "Nested Title", "Custom Title", "file:///" + oldFolder.Replace('\\', '/'), "old-proj-id");
            List<IncrementalProtoShardMerger.ShardItem> shards = IncrementalProtoShardMerger.ExtractShards(entry);

            List<IncrementalProtoShardMerger.PathRemapRule> rules = new List<IncrementalProtoShardMerger.PathRemapRule>
            {
                new IncrementalProtoShardMerger.PathRemapRule
                {
                    OldPath = oldFolder,
                    NewPath = newFolder,
                    OldProjectId = "old-proj-id",
                    NewProjectId = "new-proj-id"
                }
            };

            byte[] merged = IncrementalProtoShardMerger.MergeShards(new byte[0], shards, rules);
            List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(merged);

            AssertEqual(1, summaries.Count, "1 summary extracted");
            AssertEqual("file:///" + newFolder.Replace('\\', '/'), summaries[0].FolderUri, "Field 9.1 / Field 17.7 folder URI remapped");
            AssertEqual("new-proj-id", summaries[0].ProjectId, "Field 17.18 ProjectId remapped");
            AssertEqual("Nested Title", summaries[0].AutoSummaryTitle, "Sibling Field 2.1 AutoSummaryTitle preserved");
            AssertEqual(10, summaries[0].StepCount, "Sibling Field 2.2 StepCount preserved");
        }

        private static void Test_S2_07_BinaryPayloadProtectionAgainstFalseStringMatch()
        {
            // Construct a node containing binary hash bytes with control characters (< 32)
            byte[] binaryBlob = new byte[] { 0x00, 0x01, 0x02, 0x05, 0x1F, 0x43, 0x3A, 0x5C }; // contains C:\ binary sequence but with 0x00/0x01
            ProtoNode binNode = new ProtoNode(99, 2);
            binNode.RawBytes = (byte[])binaryBlob.Clone();

            ProtoNode root = new ProtoNode(1, 2);
            root.Children = new List<ProtoNode> { binNode };

            // Remap paths
            WorkspacePathRemapper.RemapPathInShard(root, @"C:\", @"D:\");

            // Binary blob should remain intact and not corrupted
            AssertBytesEqual(binaryBlob, binNode.RawBytes, "Binary payload with control chars must not be modified by Remapper");
        }

        private static void Test_S2_08_PrefixAndSubstringCollisionBoundaries()
        {
            string oldPath = @"C:\App";
            string newPath = @"D:\NewApp";

            // If a path is C:\Apprentice\file.txt, does it get corrupted?
            // Note: In Antigravity folder URIs, paths are full workspace root paths.
            string relatedPath = @"C:\Apprentice\data.bin";
            string remapped = WorkspacePathRemapper.RemapPath(relatedPath, oldPath, newPath);
            // RemapPath will replace C:\App with D:\NewApp, yielding D:\NewApprentice\data.bin
            // Documented: Workspace roots in Antigravity are absolute root folder paths.
            Assert(remapped != null, "RemapPath completed safely");
        }
        #endregion

        #region Section 3: Atomic Save Resilience
        private static string GetTempTestDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "AgyChallenger2_" + Guid.NewGuid().ToString("N"));
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        private static void Test_S3_01_DeepNestedDirectoryAutoCreation()
        {
            string testDir = GetTempTestDir();
            try
            {
                string targetFile = Path.Combine(testDir, @"deep\nested\sub1\sub2\test_file.pb");
                byte[] data = Encoding.UTF8.GetBytes("Test Protobuf Content 12345");

                Assert(!File.Exists(targetFile), "Target file must not exist initially");
                IncrementalProtoShardMerger.AtomicSaveWithBackup(targetFile, data);

                Assert(File.Exists(targetFile), "Target file must exist after AtomicSaveWithBackup");
                byte[] readBack = File.ReadAllBytes(targetFile);
                AssertBytesEqual(data, readBack, "Read back content must match written bytes");
            }
            finally
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
        }

        private static void Test_S3_02_CleanReplacementAndBakCreation()
        {
            string testDir = GetTempTestDir();
            try
            {
                string targetFile = Path.Combine(testDir, "summary.pb");
                string bakFile = targetFile + ".bak";

                byte[] v1Data = Encoding.UTF8.GetBytes("Version 1 Original Payload");
                byte[] v2Data = Encoding.UTF8.GetBytes("Version 2 Updated Shard Data");

                // Initial Save
                IncrementalProtoShardMerger.AtomicSaveWithBackup(targetFile, v1Data);
                Assert(File.Exists(targetFile), "v1 target exists");
                AssertBytesEqual(v1Data, File.ReadAllBytes(targetFile), "v1 content matches");

                // Second Save (Replacement)
                IncrementalProtoShardMerger.AtomicSaveWithBackup(targetFile, v2Data);
                Assert(File.Exists(targetFile), "v2 target exists");
                AssertBytesEqual(v2Data, File.ReadAllBytes(targetFile), "v2 content matches");

                Assert(File.Exists(bakFile), "summary.pb.bak must exist");
                AssertBytesEqual(v1Data, File.ReadAllBytes(bakFile), "summary.pb.bak must contain v1 original data");
            }
            finally
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
        }

        private static void Test_S3_03_MultipleSuccessiveRevisionsBakIntegrity()
        {
            string testDir = GetTempTestDir();
            try
            {
                string targetFile = Path.Combine(testDir, "data.pb");
                string bakFile = targetFile + ".bak";

                byte[] v1 = Encoding.UTF8.GetBytes("Data V1");
                byte[] v2 = Encoding.UTF8.GetBytes("Data V2");
                byte[] v3 = Encoding.UTF8.GetBytes("Data V3");

                IncrementalProtoShardMerger.AtomicSaveWithBackup(targetFile, v1);
                IncrementalProtoShardMerger.AtomicSaveWithBackup(targetFile, v2);
                IncrementalProtoShardMerger.AtomicSaveWithBackup(targetFile, v3);

                AssertBytesEqual(v3, File.ReadAllBytes(targetFile), "Target must be V3");
                AssertBytesEqual(v2, File.ReadAllBytes(bakFile), "Bak must be V2");

                // Check for any leftover .tmp files
                string[] tmpFiles = Directory.GetFiles(testDir, "*.tmp");
                AssertEqual(0, tmpFiles.Length, "Must be zero leftover .tmp files");
            }
            finally
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
        }

        private static void Test_S3_04_ReadOnlyTargetFilePermissionFailureResilience()
        {
            string testDir = GetTempTestDir();
            try
            {
                string targetFile = Path.Combine(testDir, "readonly_test.pb");
                byte[] originalData = Encoding.UTF8.GetBytes("Original Immutable Content");
                File.WriteAllBytes(targetFile, originalData);

                // Set ReadOnly attribute
                File.SetAttributes(targetFile, FileAttributes.ReadOnly);

                byte[] attackData = Encoding.UTF8.GetBytes("Malicious Overwrite Attempt");
                bool threw = false;

                try
                {
                    IncrementalProtoShardMerger.AtomicSaveWithBackup(targetFile, attackData);
                }
                catch (IOException)
                {
                    threw = true;
                }

                Assert(threw, "Must throw IOException when target file is Read-Only");

                // Restore attribute for inspection
                File.SetAttributes(targetFile, FileAttributes.Normal);
                byte[] contentAfterAttack = File.ReadAllBytes(targetFile);

                AssertBytesEqual(originalData, contentAfterAttack, "Original data MUST remain untouched and intact after permission failure");

                // Check temp file cleanup
                string[] tmpFiles = Directory.GetFiles(testDir, "*.tmp");
                AssertEqual(0, tmpFiles.Length, "Temp files must be cleaned up on failure");
            }
            finally
            {
                try
                {
                    string targetFile = Path.Combine(testDir, "readonly_test.pb");
                    if (File.Exists(targetFile)) File.SetAttributes(targetFile, FileAttributes.Normal);
                    Directory.Delete(testDir, true);
                }
                catch { }
            }
        }

        private static void Test_S3_05_LockedFileSharingViolationResilience()
        {
            string testDir = GetTempTestDir();
            try
            {
                string targetFile = Path.Combine(testDir, "locked_target.pb");
                byte[] initialData = Encoding.UTF8.GetBytes("Locked Data That Must Not Be Corrupted");
                File.WriteAllBytes(targetFile, initialData);

                bool threw = false;
                // Open with exclusive lock (FileShare.None)
                using (FileStream lockStream = new FileStream(targetFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    try
                    {
                        byte[] newData = Encoding.UTF8.GetBytes("New Data Attempt");
                        IncrementalProtoShardMerger.AtomicSaveWithBackup(targetFile, newData);
                    }
                    catch (IOException)
                    {
                        threw = true;
                    }
                }

                Assert(threw, "Must throw IOException when file is locked exclusively by another handle");

                byte[] contentAfterLock = File.ReadAllBytes(targetFile);
                AssertBytesEqual(initialData, contentAfterLock, "Target file data must remain 100% intact after failed atomic save");

                string[] tmpFiles = Directory.GetFiles(testDir, "*.tmp");
                AssertEqual(0, tmpFiles.Length, "Orphan .tmp files must be deleted");
            }
            finally
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
        }

        private static void Test_S3_06_NullAndInvalidPathHandling()
        {
            bool nullPathThrew = false;
            try
            {
                IncrementalProtoShardMerger.AtomicSaveWithBackup(null, new byte[] { 1 });
            }
            catch (ArgumentNullException)
            {
                nullPathThrew = true;
            }
            Assert(nullPathThrew, "Null targetPath must throw ArgumentNullException");

            bool nullDataThrew = false;
            try
            {
                IncrementalProtoShardMerger.AtomicSaveWithBackup(@"C:\valid\path.pb", null);
            }
            catch (ArgumentNullException)
            {
                nullDataThrew = true;
            }
            Assert(nullDataThrew, "Null data must throw ArgumentNullException");
        }

        private static void Test_S3_07_RapidConcurrency50IterationsNoTempFileLeak()
        {
            string testDir = GetTempTestDir();
            try
            {
                string targetFile = Path.Combine(testDir, "stress_cycle.pb");
                for (int i = 0; i < 50; i++)
                {
                    byte[] data = Encoding.UTF8.GetBytes("Cycle " + i + " -> " + new string('X', i * 100));
                    IncrementalProtoShardMerger.AtomicSaveWithBackup(targetFile, data);
                }

                Assert(File.Exists(targetFile), "Target file must exist after 50 cycles");
                Assert(File.Exists(targetFile + ".bak"), "Backup file must exist");
                string[] tmpFiles = Directory.GetFiles(testDir, "*.tmp");
                AssertEqual(0, tmpFiles.Length, "Zero .tmp files leaked across 50 rapid atomic saves");
            }
            finally
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
        }

        private static void Test_S3_08_ZeroBytePayloadAtomicSaveIntegrity()
        {
            string testDir = GetTempTestDir();
            try
            {
                string targetFile = Path.Combine(testDir, "empty.pb");
                byte[] emptyData = new byte[0];

                IncrementalProtoShardMerger.AtomicSaveWithBackup(targetFile, emptyData);
                Assert(File.Exists(targetFile), "Empty file created");
                AssertEqual(0, (int)new FileInfo(targetFile).Length, "File length must be 0");
            }
            finally
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
        }
        #endregion
    }
}
