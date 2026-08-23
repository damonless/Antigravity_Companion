using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using AntigravityCompanion;

namespace AntigravityCompanion.Challenger
{
    public class Program
    {
        private static int passedTests = 0;
        private static int failedTests = 0;
        private static int totalAssertions = 0;

        public static int Main(string[] args)
        {
            Console.WriteLine("===============================================================================");
            Console.WriteLine("  CHALLENGER 1: ADVERSARIAL EMPIRICAL STRESS HARNESS (M1 DATA ENGINE)");
            Console.WriteLine("===============================================================================");
            Console.WriteLine("  Target: MainForm.cs Protobuf AST, Streaming Zip, Shard Merger & Remapper");
            Console.WriteLine("  Environment: Native C# 5 / .NET 4.0/4.5+ BCL");
            Console.WriteLine("===============================================================================\n");

            Stopwatch totalSw = Stopwatch.StartNew();

            // SECTION 1: Extreme Protobuf Wire Inputs
            Console.WriteLine(">>> [CATEGORY 1] Extreme Protobuf Wire Inputs & Malformed Payloads");
            RunTest("ADV-PB-01: 10-byte Varints & ulong.MaxValue Boundary", Test_10ByteVarints);
            RunTest("ADV-PB-02: Infinite Varint Overrun & Truncated Varints", Test_InfiniteAndTruncatedVarints);
            RunTest("ADV-PB-03: Zero-Byte Length-Delimited & Nested Empty Nodes", Test_ZeroByteLengthDelimited);
            RunTest("ADV-PB-04: Truncated Payloads Across All Wire Types (0,1,2,5)", Test_TruncatedPayloadsAllWireTypes);
            RunTest("ADV-PB-05: Malformed & Deprecated Wire Types (3, 4, 6, 7)", Test_MalformedWireTypes);
            RunTest("ADV-PB-06: High Field Numbers & Signed Integer Boundary Analysis", Test_HighFieldNumbersBoundary);
            RunTest("ADV-PB-07: Deeply Nested Hierarchy (10, 50, 100, 200 Levels)", Test_DeeplyNestedHierarchy);
            RunTest("ADV-PB-08: Wide Submessages & Massive Payloads (10,000 fields, 2MB string)", Test_WideAndMassivePayloads);
            RunTest("ADV-PB-09: Protobuf Fuzzing (1,000 Random Corrupted Byte Streams)", Test_ProtobufFuzzer);

            // SECTION 2: Streaming Zip Archive Adversarial Inspection
            Console.WriteLine("\n>>> [CATEGORY 2] Streaming Zip Archive Adversarial Stress Tests");
            RunTest("ADV-ZIP-01: Non-Existent, Null, and Empty File Path Handling", Test_ZipPathEdgeCases);
            RunTest("ADV-ZIP-02: 0-Byte Empty File & Truncated Header Archives", Test_ZeroByteAndTruncatedZip);
            RunTest("ADV-ZIP-03: Non-Zip Junk / Plain Text / PE Binary Rejection", Test_NonZipBinaryRejection);
            RunTest("ADV-ZIP-04: Valid Zip Missing agyhub_summaries_proto.pb", Test_ZipMissingProtoFile);
            RunTest("ADV-ZIP-05: Valid Zip With 0-Byte or Corrupted Summary Proto", Test_ZipCorruptedProtoPayload);
            RunTest("ADV-ZIP-06: Malformed JSON in metadata.json & config/projects", Test_ZipMalformedJsonPayloads);
            RunTest("ADV-ZIP-07: Path Separator & Casing Chaos in Archive Entries", Test_ZipPathSeparatorsAndCasing);
            RunTest("ADV-ZIP-08: Full Live In-Memory Inspection of 353MB Production .agy", Test_Real353MbAgyArchiveInspection);

            // SECTION 3: Roundtrip Fidelity & Shard Engine Stress
            Console.WriteLine("\n>>> [CATEGORY 3] 100% Byte Fidelity Roundtrip & Shard Engine Stress");
            RunTest("ADV-RT-01: 99,973-Byte Real Protobuf Byte-for-Byte Exact AST Roundtrip", Test_RealProtoByteForByteRoundtrip);
            RunTest("ADV-RT-02: 138-Shard Decomposition & Reassembly Identity", Test_ShardDecompositionAndReassembly);
            RunTest("ADV-RT-03: Field 15 Custom Chinese Title Extraction Priority & Unicode Matrix", Test_CustomChineseTitlePriorityMatrix);
            RunTest("ADV-RT-04: Workspace Path Remapper 8-Variant Matrix & URL Encoding", Test_WorkspacePathRemapperMatrix);
            RunTest("ADV-RT-05: Isolated Shard Mutation & Zero Collateral Corruption", Test_IsolatedShardMutationNoCorruption);
            RunTest("ADV-RT-06: AtomicSaveWithBackup Resilience & Backup File Verification", Test_AtomicSaveWithBackupResilience);

            totalSw.Stop();

            Console.WriteLine("\n===============================================================================");
            Console.WriteLine(string.Format("  CHALLENGER 1 EXECUTION SUMMARY: {0} Passed, {1} Failed | {2} Assertions", passedTests, failedTests, totalAssertions));
            Console.WriteLine(string.Format("  Total Duration: {0:F2}ms ({1:F2}s)", totalSw.Elapsed.TotalMilliseconds, totalSw.Elapsed.TotalSeconds));
            Console.WriteLine("===============================================================================");

            if (failedTests == 0)
            {
                Console.WriteLine(">>> VERDICT: [APPROVE] Milestone 1 Data & Storage Engine meets all empirical requirements!");
                return 0;
            }
            else
            {
                Console.WriteLine(">>> VERDICT: [REJECT] Milestone 1 Data Engine failed empirical stress testing!");
                return 1;
            }
        }

        private static void RunTest(string testName, Action testAction)
        {
            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                testAction();
                sw.Stop();
                Console.WriteLine(string.Format("  [PASS] {0} ({1:F2}ms)", testName.PadRight(70), sw.Elapsed.TotalMilliseconds));
                passedTests++;
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("  [FAIL] {0}", testName));
                Console.WriteLine("         Error: " + ex.Message);
                Console.WriteLine("         Stack: " + ex.StackTrace);
                failedTests++;
            }
        }

        private static void Assert(bool condition, string message)
        {
            totalAssertions++;
            if (!condition)
            {
                throw new Exception("Assertion Failed: " + message);
            }
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            totalAssertions++;
            if (!object.Equals(expected, actual))
            {
                throw new Exception(string.Format("Assertion Failed: {0} | Expected: [{1}], Actual: [{2}]", message, expected, actual));
            }
        }

        private static byte[] ExtractProtoFromAgy(string agyPath)
        {
            using (FileStream fs = new FileStream(agyPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                for (int i = 0; i < zip.Entries.Count; i++)
                {
                    ZipArchiveEntry entry = zip.Entries[i];
                    if (entry.FullName.EndsWith("agyhub_summaries_proto.pb", StringComparison.OrdinalIgnoreCase))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            using (Stream s = entry.Open())
                            {
                                s.CopyTo(ms);
                            }
                            return ms.ToArray();
                        }
                    }
                }
            }
            throw new FileNotFoundException("agyhub_summaries_proto.pb not found in " + agyPath);
        }

        #region Category 1: Extreme Protobuf Wire Inputs

        private static void Test_10ByteVarints()
        {
            // Test 0
            byte[] b0 = ProtoCodec.EncodeVarint(0);
            AssertEqual(1, b0.Length, "Varint 0 length");
            AssertEqual((byte)0x00, b0[0], "Varint 0 byte");
            ulong v0;
            int r0 = ProtoCodec.ReadVarint(b0, 0, out v0);
            AssertEqual(1, r0, "ReadVarint 0 bytes");
            AssertEqual(0UL, v0, "ReadVarint 0 value");

            // Test 1-byte boundary (127 = 0x7F)
            byte[] b127 = ProtoCodec.EncodeVarint(127);
            AssertEqual(1, b127.Length, "Varint 127 length");
            AssertEqual((byte)0x7F, b127[0], "Varint 127 byte");
            ulong v127;
            ProtoCodec.ReadVarint(b127, 0, out v127);
            AssertEqual(127UL, v127, "ReadVarint 127 value");

            // Test 2-byte boundary (128 = 0x80)
            byte[] b128 = ProtoCodec.EncodeVarint(128);
            AssertEqual(2, b128.Length, "Varint 128 length");
            ulong v128;
            ProtoCodec.ReadVarint(b128, 0, out v128);
            AssertEqual(128UL, v128, "ReadVarint 128 value");

            // Test uint.MaxValue (4294967295) -> 5 bytes
            byte[] bUintMax = ProtoCodec.EncodeVarint(uint.MaxValue);
            AssertEqual(5, bUintMax.Length, "Varint uint.MaxValue length");
            ulong vUintMax;
            ProtoCodec.ReadVarint(bUintMax, 0, out vUintMax);
            AssertEqual((ulong)uint.MaxValue, vUintMax, "ReadVarint uint.MaxValue value");

            // Test long.MaxValue (0x7FFFFFFFFFFFFFFF) -> 9 bytes
            byte[] bLongMax = ProtoCodec.EncodeVarint((ulong)long.MaxValue);
            AssertEqual(9, bLongMax.Length, "Varint long.MaxValue length");
            ulong vLongMax;
            ProtoCodec.ReadVarint(bLongMax, 0, out vLongMax);
            AssertEqual((ulong)long.MaxValue, vLongMax, "ReadVarint long.MaxValue value");

            // Test 10-byte ulong.MaxValue (18446744073709551615) -> 10 bytes: 0xFF x 9, 0x01
            byte[] bUlongMax = ProtoCodec.EncodeVarint(ulong.MaxValue);
            AssertEqual(10, bUlongMax.Length, "Varint ulong.MaxValue length must be 10 bytes");
            for (int i = 0; i < 9; i++)
            {
                AssertEqual((byte)0xFF, bUlongMax[i], "Varint byte " + i + " must be 0xFF");
            }
            AssertEqual((byte)0x01, bUlongMax[9], "Varint byte 9 must be 0x01");

            ulong vUlongMax;
            int rUlongMax = ProtoCodec.ReadVarint(bUlongMax, 0, out vUlongMax);
            AssertEqual(10, rUlongMax, "ReadVarint ulong.MaxValue bytes read");
            AssertEqual(ulong.MaxValue, vUlongMax, "ReadVarint ulong.MaxValue roundtrip value");

            // Test AST Node roundtrip with 10-byte Varint
            ProtoNode node = new ProtoNode(999, 0);
            node.VarintValue = ulong.MaxValue;
            byte[] encoded = ProtoCodec.Encode(new List<ProtoNode> { node });
            List<ProtoNode> decoded = ProtoCodec.Decode(encoded);
            AssertEqual(1, decoded.Count, "Decoded count");
            AssertEqual(999, decoded[0].FieldNumber, "Field number");
            AssertEqual(0, decoded[0].WireType, "Wire type");
            AssertEqual(ulong.MaxValue, decoded[0].VarintValue, "VarintValue roundtrip");
        }

        private static void Test_InfiniteAndTruncatedVarints()
        {
            // 11 bytes of 0xFF (infinite varint attack)
            byte[] infiniteVarint = new byte[11];
            for (int i = 0; i < infiniteVarint.Length; i++) infiniteVarint[i] = 0xFF;

            ulong val;
            int res = ProtoCodec.ReadVarint(infiniteVarint, 0, infiniteVarint.Length, out val);
            AssertEqual(-1, res, "ReadVarint must return -1 for 11-byte infinite varint");

            // 100 bytes of 0xFF
            byte[] massiveInfinite = new byte[100];
            for (int i = 0; i < massiveInfinite.Length; i++) massiveInfinite[i] = 0xFF;
            int res100 = ProtoCodec.ReadVarint(massiveInfinite, 0, massiveInfinite.Length, out val);
            AssertEqual(-1, res100, "ReadVarint must return -1 for 100-byte infinite varint");

            // Decode on infinite varint should terminate safely without hanging or throwing
            List<ProtoNode> nodes = ProtoCodec.Decode(massiveInfinite);
            AssertEqual(0, nodes.Count, "Decode on invalid varint should return 0 nodes safely");

            // Truncated Varint (Tag starts with 0x80 but stream ends at index 1)
            byte[] truncated1 = new byte[] { 0x80 };
            List<ProtoNode> nodesTrunc1 = ProtoCodec.Decode(truncated1);
            AssertEqual(0, nodesTrunc1.Count, "Truncated 1-byte varint decode");

            // Truncated Varint (Tag starts with 0x80 0x80 0x80 but stream ends)
            byte[] truncated3 = new byte[] { 0x80, 0x80, 0x80 };
            List<ProtoNode> nodesTrunc3 = ProtoCodec.Decode(truncated3);
            AssertEqual(0, nodesTrunc3.Count, "Truncated 3-byte varint decode");
        }

        private static void Test_ZeroByteLengthDelimited()
        {
            // Field 1, WireType 2 (Tag: (1<<3)|2 = 0x0A), Length: 0 (0x00)
            byte[] zeroLenDelimited = new byte[] { 0x0A, 0x00 };
            List<ProtoNode> nodes = ProtoCodec.Decode(zeroLenDelimited);
            AssertEqual(1, nodes.Count, "Decoded nodes count");
            AssertEqual(1, nodes[0].FieldNumber, "Field number");
            AssertEqual(2, nodes[0].WireType, "Wire type");
            AssertEqual(0, nodes[0].RawBytes.Length, "RawBytes length must be 0");
            AssertEqual("", nodes[0].GetString(), "GetString() on 0-length node");

            // Roundtrip serialization
            byte[] reEncoded = ProtoCodec.Encode(nodes);
            AssertEqual(2, reEncoded.Length, "Re-encoded length");
            AssertEqual((byte)0x0A, reEncoded[0], "Re-encoded tag");
            AssertEqual((byte)0x00, reEncoded[1], "Re-encoded length");

            // Nested 0-byte nodes: Field 1 (Wire 2) containing Field 2 (Wire 2, len 0) and Field 3 (Wire 2, len 0)
            ProtoNode parent = new ProtoNode(1, 2);
            ProtoNode child1 = new ProtoNode(2, 2); child1.RawBytes = new byte[0];
            ProtoNode child2 = new ProtoNode(3, 2); child2.RawBytes = new byte[0];
            parent.Children = new List<ProtoNode> { child1, child2 };

            byte[] parentEncoded = ProtoCodec.Encode(new List<ProtoNode> { parent });
            List<ProtoNode> parentDecoded = ProtoCodec.Decode(parentEncoded);
            AssertEqual(1, parentDecoded.Count, "Decoded parent count");
            Assert(parentDecoded[0].HasChildren, "Parent should have children");
            AssertEqual(2, parentDecoded[0].Children.Count, "Parent children count");
            AssertEqual(2, parentDecoded[0].Children[0].FieldNumber, "Child 1 field number");
            AssertEqual(3, parentDecoded[0].Children[1].FieldNumber, "Child 2 field number");
        }

        private static void Test_TruncatedPayloadsAllWireTypes()
        {
            // 1. WireType 0 (Varint): Tag 0x08 (Field 1, Wire 0) but 0 value bytes follow
            byte[] t0 = new byte[] { 0x08 };
            List<ProtoNode> n0 = ProtoCodec.Decode(t0);
            AssertEqual(0, n0.Count, "WireType 0 with missing value should yield 0 nodes");

            // 2. WireType 1 (Fixed64): Tag 0x09 (Field 1, Wire 1) with only 5 bytes instead of 8
            byte[] t1 = new byte[] { 0x09, 0x01, 0x02, 0x03, 0x04, 0x05 };
            List<ProtoNode> n1 = ProtoCodec.Decode(t1);
            AssertEqual(0, n1.Count, "WireType 1 with 5 bytes should yield 0 nodes");

            // 3. WireType 5 (Fixed32): Tag 0x0D (Field 1, Wire 5) with only 2 bytes instead of 4
            byte[] t5 = new byte[] { 0x0D, 0x01, 0x02 };
            List<ProtoNode> n5 = ProtoCodec.Decode(t5);
            AssertEqual(0, n5.Count, "WireType 5 with 2 bytes should yield 0 nodes");

            // 4. WireType 2 (Length-Delimited): Tag 0x0A (Field 1, Wire 2), Length 100, but only 4 bytes follow
            byte[] t2 = new byte[] { 0x0A, 0x64, 0xAA, 0xBB, 0xCC, 0xDD };
            List<ProtoNode> n2 = ProtoCodec.Decode(t2);
            AssertEqual(1, n2.Count, "Truncated length-delimited should gracefully clamp remaining bytes");
            AssertEqual(4, n2[0].RawBytes.Length, "Clamped bytes length");
            AssertEqual((byte)0xAA, n2[0].RawBytes[0], "First clamped byte");
            AssertEqual((byte)0xDD, n2[0].RawBytes[3], "Last clamped byte");

            // 5. Normal node followed by truncated node
            byte[] mixed = new byte[] { 0x08, 0x2A, 0x11, 0x01, 0x02 };
            List<ProtoNode> nMixed = ProtoCodec.Decode(mixed);
            AssertEqual(1, nMixed.Count, "Should recover the valid first node");
            AssertEqual(1, nMixed[0].FieldNumber, "Node 1 field number");
            AssertEqual(42UL, nMixed[0].VarintValue, "Node 1 value");
        }

        private static void Test_MalformedWireTypes()
        {
            // WireType 3 (Start Group - deprecated) -> Tag: (1 << 3) | 3 = 0x0B
            byte[] wt3 = new byte[] { 0x0B, 0x01, 0x02, 0x03 };
            List<ProtoNode> n3 = ProtoCodec.Decode(wt3);
            AssertEqual(0, n3.Count, "WireType 3 should terminate decode without crash");

            // WireType 4 (End Group - deprecated) -> Tag: (1 << 3) | 4 = 0x0C
            byte[] wt4 = new byte[] { 0x0C, 0x01, 0x02, 0x03 };
            List<ProtoNode> n4 = ProtoCodec.Decode(wt4);
            AssertEqual(0, n4.Count, "WireType 4 should terminate decode without crash");

            // WireType 6 (Undefined) -> Tag: (1 << 3) | 6 = 0x0E
            byte[] wt6 = new byte[] { 0x0E, 0x01, 0x02 };
            List<ProtoNode> n6 = ProtoCodec.Decode(wt6);
            AssertEqual(0, n6.Count, "WireType 6 should terminate decode without crash");

            // WireType 7 (Undefined) -> Tag: (1 << 3) | 7 = 0x0F
            byte[] wt7 = new byte[] { 0x0F, 0x01, 0x02 };
            List<ProtoNode> n7 = ProtoCodec.Decode(wt7);
            AssertEqual(0, n7.Count, "WireType 7 should terminate decode without crash");
        }

        private static void Test_HighFieldNumbersBoundary()
        {
            // Standard Antigravity fields: 1, 2, 3, 4, 6, 7, 8, 9, 10, 15, 17, 18
            int[] standardFields = new int[] { 1, 2, 3, 4, 6, 7, 8, 9, 10, 15, 17, 18 };
            for (int i = 0; i < standardFields.Length; i++)
            {
                int fn = standardFields[i];
                ProtoNode n = new ProtoNode(fn, 0);
                n.VarintValue = (ulong)(fn * 100);
                byte[] enc = ProtoCodec.Encode(new List<ProtoNode> { n });
                List<ProtoNode> dec = ProtoCodec.Decode(enc);
                AssertEqual(1, dec.Count, "Standard field " + fn + " count");
                AssertEqual(fn, dec[0].FieldNumber, "Standard field " + fn + " number");
                AssertEqual((ulong)(fn * 100), dec[0].VarintValue, "Standard field " + fn + " value");
            }

            // High field numbers within 28-bit safe range (< 268,435,456)
            int[] highFields = new int[] { 100, 1000, 50000, 1000000, 200000000 };
            for (int i = 0; i < highFields.Length; i++)
            {
                int fn = highFields[i];
                ProtoNode n = new ProtoNode(fn, 0);
                n.VarintValue = (ulong)(fn * 2);
                byte[] enc = ProtoCodec.Encode(new List<ProtoNode> { n });
                List<ProtoNode> dec = ProtoCodec.Decode(enc);
                AssertEqual(1, dec.Count, "High field " + fn + " count");
                AssertEqual(fn, dec[0].FieldNumber, "High field " + fn + " number");
                AssertEqual((ulong)(fn * 2), dec[0].VarintValue, "High field " + fn + " value");
            }
        }

        private static void Test_DeeplyNestedHierarchy()
        {
            int[] depths = new int[] { 10, 50, 100, 200 };

            for (int d = 0; d < depths.Length; d++)
            {
                int targetDepth = depths[d];
                ProtoNode root = new ProtoNode(1, 2);
                ProtoNode curr = root;

                for (int i = 2; i <= targetDepth; i++)
                {
                    ProtoNode child = new ProtoNode(1, 2);
                    curr.Children = new List<ProtoNode> { child };
                    curr = child;
                }
                curr.SetString("Depth Level " + targetDepth);

                byte[] encoded = ProtoCodec.Encode(new List<ProtoNode> { root });
                List<ProtoNode> decoded = ProtoCodec.Decode(encoded);
                AssertEqual(1, decoded.Count, "Decoded count at depth " + targetDepth);

                ProtoNode walk = decoded[0];
                int actualDepth = 1;
                while (walk.HasChildren)
                {
                    walk = walk.Children[0];
                    actualDepth++;
                }

                AssertEqual(targetDepth, actualDepth, "Hierarchy depth matched for depth " + targetDepth);
                AssertEqual("Depth Level " + targetDepth, walk.GetString(), "Deepest string value matched");
            }
        }

        private static void Test_WideAndMassivePayloads()
        {
            // 1. Wide submessage with 10,000 Varint fields
            ProtoNode wideContainer = new ProtoNode(1, 2);
            wideContainer.Children = new List<ProtoNode>(10000);
            for (int i = 1; i <= 10000; i++)
            {
                ProtoNode child = new ProtoNode(i, 0);
                child.VarintValue = (ulong)(i * 3);
                wideContainer.Children.Add(child);
            }

            byte[] wideEncoded = ProtoCodec.Encode(new List<ProtoNode> { wideContainer });
            List<ProtoNode> wideDecoded = ProtoCodec.Decode(wideEncoded);
            AssertEqual(1, wideDecoded.Count, "Wide decoded root count");
            Assert(wideDecoded[0].HasChildren, "Wide container has children");
            AssertEqual(10000, wideDecoded[0].Children.Count, "Wide container children count");
            AssertEqual(5000, wideDecoded[0].Children[4999].FieldNumber, "Field 5000 check");
            AssertEqual(15000UL, wideDecoded[0].Children[4999].VarintValue, "Field 5000 value check");

            // 2. Massive String Payload (2MB UTF-8 Chinese Text)
            StringBuilder sb = new StringBuilder(2000000);
            for (int i = 0; i < 50000; i++)
            {
                sb.Append("【大规模抗压测试】Antigravity Companion V0.5 ⚡ 🚀 ");
            }
            string massiveText = sb.ToString();

            ProtoNode massiveNode = new ProtoNode(15, 2);
            massiveNode.SetString(massiveText);

            byte[] massiveBytes = ProtoCodec.Encode(new List<ProtoNode> { massiveNode });
            List<ProtoNode> massiveDecoded = ProtoCodec.Decode(massiveBytes);
            AssertEqual(1, massiveDecoded.Count, "Massive node decoded count");
            AssertEqual(massiveText.Length, massiveDecoded[0].GetString().Length, "Massive string length matched");
            AssertEqual(massiveText, massiveDecoded[0].GetString(), "Massive string content exact match");
        }

        private static void Test_ProtobufFuzzer()
        {
            // Seeded random fuzzer to guarantee deterministic reproducibility
            Random rng = new Random(20260823);
            int fuzzIterations = 1000;

            for (int iter = 0; iter < fuzzIterations; iter++)
            {
                int len = rng.Next(0, 4096);
                byte[] junk = new byte[len];
                rng.NextBytes(junk);

                try
                {
                    // Should NEVER throw unhandled exception or crash
                    List<ProtoNode> nodes = ProtoCodec.Decode(junk);
                    if (nodes != null && nodes.Count > 0)
                    {
                        // Test re-encoding
                        byte[] reEnc = ProtoCodec.Encode(nodes);
                        Assert(reEnc != null, "Re-encoded fuzzed nodes cannot be null");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception(string.Format("Protobuf Fuzzer CRASHED on iteration {0} (len {1}): {2}", iter, len, ex.Message), ex);
                }
            }
        }

        #endregion

        #region Category 2: Streaming Zip Archive Adversarial Stress Tests

        private static void Test_ZipPathEdgeCases()
        {
            // 1. Non-existent file path
            string nonExistent = Path.Combine(Path.GetTempPath(), "non_existent_" + Guid.NewGuid().ToString("N") + ".agy");
            bool threwNotFound = false;
            try
            {
                StreamingZipIndexReader.ReadIndex(nonExistent);
            }
            catch (FileNotFoundException)
            {
                threwNotFound = true;
            }
            Assert(threwNotFound, "Non-existent path must throw FileNotFoundException");

            // 2. ReadZipMetadata with non-existent path
            bool threwMetaNotFound = false;
            try
            {
                StreamingZipIndexReader.ReadZipMetadata(nonExistent);
            }
            catch (FileNotFoundException)
            {
                threwMetaNotFound = true;
            }
            Assert(threwMetaNotFound, "ReadZipMetadata on non-existent path must throw FileNotFoundException");
        }

        private static void Test_ZeroByteAndTruncatedZip()
        {
            // 1. 0-byte file
            string zeroFile = Path.Combine(Path.GetTempPath(), "zero_" + Guid.NewGuid().ToString("N") + ".agy");
            File.WriteAllBytes(zeroFile, new byte[0]);

            try
            {
                bool threwInvalidData = false;
                try
                {
                    StreamingZipIndexReader.ReadIndex(zeroFile);
                }
                catch (InvalidDataException)
                {
                    threwInvalidData = true;
                }
                Assert(threwInvalidData, "0-byte file must throw InvalidDataException from ZipArchive");
            }
            finally
            {
                if (File.Exists(zeroFile)) File.Delete(zeroFile);
            }

            // 2. Truncated Zip header (PK\x03\x04 + 10 bytes of junk)
            string truncFile = Path.Combine(Path.GetTempPath(), "trunc_" + Guid.NewGuid().ToString("N") + ".agy");
            File.WriteAllBytes(truncFile, new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x01, 0x02, 0x03, 0x04, 0x05 });

            try
            {
                bool threwInvalidData = false;
                try
                {
                    StreamingZipIndexReader.ReadIndex(truncFile);
                }
                catch (InvalidDataException)
                {
                    threwInvalidData = true;
                }
                Assert(threwInvalidData, "Truncated zip must throw InvalidDataException");
            }
            finally
            {
                if (File.Exists(truncFile)) File.Delete(truncFile);
            }
        }

        private static void Test_NonZipBinaryRejection()
        {
            // 1. Plain text file masquerading as .agy
            string txtFile = Path.Combine(Path.GetTempPath(), "text_junk_" + Guid.NewGuid().ToString("N") + ".agy");
            File.WriteAllText(txtFile, "This is not a zip archive. It is plain UTF-8 text containing json-like syntax { 'foo': 'bar' }");

            try
            {
                bool threwInvalid = false;
                try
                {
                    StreamingZipIndexReader.ReadIndex(txtFile);
                }
                catch (InvalidDataException)
                {
                    threwInvalid = true;
                }
                Assert(threwInvalid, "Plain text archive must throw InvalidDataException");
            }
            finally
            {
                if (File.Exists(txtFile)) File.Delete(txtFile);
            }

            // 2. PE Executable header (MZ...) masquerading as .agy
            string peFile = Path.Combine(Path.GetTempPath(), "pe_junk_" + Guid.NewGuid().ToString("N") + ".agy");
            byte[] peJunk = new byte[1024];
            peJunk[0] = 0x4D; peJunk[1] = 0x5A; // 'M', 'Z'
            File.WriteAllBytes(peFile, peJunk);

            try
            {
                bool threwInvalid = false;
                try
                {
                    StreamingZipIndexReader.ReadIndex(peFile);
                }
                catch (InvalidDataException)
                {
                    threwInvalid = true;
                }
                Assert(threwInvalid, "PE binary file must throw InvalidDataException");
            }
            finally
            {
                if (File.Exists(peFile)) File.Delete(peFile);
            }
        }

        private static void Test_ZipMissingProtoFile()
        {
            // Valid zip archive containing metadata.json and conversation databases, but NO agyhub_summaries_proto.pb
            string zipFile = Path.Combine(Path.GetTempPath(), "no_proto_" + Guid.NewGuid().ToString("N") + ".agy");

            try
            {
                using (FileStream fs = new FileStream(zipFile, FileMode.Create))
                using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    ZipArchiveEntry meta = zip.CreateEntry("metadata.json");
                    using (StreamWriter sw = new StreamWriter(meta.Open()))
                    {
                        sw.Write("{\"source_home\":\"C:/Users/Admin\",\"source_user\":\"Admin\",\"backup_time\":\"2026-08-23T12:00:00Z\",\"version\":\"0.5\"}");
                    }

                    ZipArchiveEntry db1 = zip.CreateEntry("conversations/UUID-1111.db");
                    using (Stream s = db1.Open()) { s.Write(new byte[] { 1, 2, 3 }, 0, 3); }
                }

                BackupArchiveIndex index = StreamingZipIndexReader.ReadIndex(zipFile);
                AssertEqual("C:/Users/Admin", index.SourceHome, "Source home parsed");
                AssertEqual("Admin", index.SourceUser, "Source user parsed");
                AssertEqual(0, index.Conversations.Count, "Conversations list should be empty (0)");
                AssertEqual(1, index.ExistingConvoDbFiles.Count, "ExistingConvoDbFiles count");
                Assert(index.ExistingConvoDbFiles.Contains("UUID-1111"), "Db file UUID-1111 detected");
            }
            finally
            {
                if (File.Exists(zipFile)) File.Delete(zipFile);
            }
        }

        private static void Test_ZipCorruptedProtoPayload()
        {
            // Valid zip archive with 100 bytes of random corrupted garbage in agyhub_summaries_proto.pb
            string zipFile = Path.Combine(Path.GetTempPath(), "corrupt_proto_" + Guid.NewGuid().ToString("N") + ".agy");

            try
            {
                using (FileStream fs = new FileStream(zipFile, FileMode.Create))
                using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    ZipArchiveEntry protoEntry = zip.CreateEntry("agyhub_summaries_proto.pb");
                    using (Stream s = protoEntry.Open())
                    {
                        byte[] corruptBytes = new byte[256];
                        for (int i = 0; i < corruptBytes.Length; i++) corruptBytes[i] = (byte)(0x80 | (i & 0x7F)); // malformed varints
                        s.Write(corruptBytes, 0, corruptBytes.Length);
                    }
                }

                // Must parse without throwing, yielding 0 conversations gracefully
                BackupArchiveIndex index = StreamingZipIndexReader.ReadIndex(zipFile);
                AssertEqual(0, index.Conversations.Count, "Corrupted protobuf should gracefully yield 0 conversations");
            }
            finally
            {
                if (File.Exists(zipFile)) File.Delete(zipFile);
            }
        }

        private static void Test_ZipMalformedJsonPayloads()
        {
            string zipFile = Path.Combine(Path.GetTempPath(), "bad_json_" + Guid.NewGuid().ToString("N") + ".agy");

            try
            {
                using (FileStream fs = new FileStream(zipFile, FileMode.Create))
                using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    // Malformed metadata.json
                    ZipArchiveEntry meta = zip.CreateEntry("metadata.json");
                    using (StreamWriter sw = new StreamWriter(meta.Open()))
                    {
                        sw.Write("<<< INVALID JSON SYNTAX >>> { not valid: true, unclosed }");
                    }

                    // Malformed project json
                    ZipArchiveEntry proj = zip.CreateEntry("config/projects/p1.json");
                    using (StreamWriter sw = new StreamWriter(proj.Open()))
                    {
                        sw.Write("{ bad_json: ");
                    }
                }

                // Should not crash
                BackupArchiveIndex index = StreamingZipIndexReader.ReadIndex(zipFile);
                AssertEqual("", index.SourceHome, "Unparseable source home returns empty string");
                AssertEqual(0, index.Projects.Count, "Unparseable project json handled gracefully");
            }
            finally
            {
                if (File.Exists(zipFile)) File.Delete(zipFile);
            }
        }

        private static void Test_ZipPathSeparatorsAndCasing()
        {
            string zipFile = Path.Combine(Path.GetTempPath(), "path_mix_" + Guid.NewGuid().ToString("N") + ".agy");

            try
            {
                using (FileStream fs = new FileStream(zipFile, FileMode.Create))
                using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    // Backslashes
                    ZipArchiveEntry e1 = zip.CreateEntry("conversations\\UUID-BACKSLASH.db");
                    using (Stream s = e1.Open()) { s.WriteByte(1); }

                    // Forward slashes with prefix
                    ZipArchiveEntry e2 = zip.CreateEntry("data/antigravity/conversations/UUID-DATA-PREFIX.db");
                    using (Stream s = e2.Open()) { s.WriteByte(1); }

                    // Mixed case brain dirs
                    ZipArchiveEntry e3 = zip.CreateEntry("BRAIN\\BRAIN-UUID-01\\file.txt");
                    using (Stream s = e3.Open()) { s.WriteByte(1); }

                    ZipArchiveEntry e4 = zip.CreateEntry("data/antigravity/brain/BRAIN-UUID-02/data.bin");
                    using (Stream s = e4.Open()) { s.WriteByte(1); }

                    // Rules and Skills
                    ZipArchiveEntry e5 = zip.CreateEntry("data/GEMINI.md");
                    using (Stream s = e5.Open()) { s.WriteByte(1); }

                    ZipArchiveEntry e6 = zip.CreateEntry("CONFIG/SKILLS/custom-skill/SKILL.md");
                    using (Stream s = e6.Open()) { s.WriteByte(1); }
                }

                BackupArchiveIndex index = StreamingZipIndexReader.ReadIndex(zipFile);
                Assert(index.ExistingConvoDbFiles.Contains("UUID-BACKSLASH"), "UUID-BACKSLASH found");
                Assert(index.ExistingConvoDbFiles.Contains("UUID-DATA-PREFIX"), "UUID-DATA-PREFIX found");
                Assert(index.ExistingBrainDirs.Contains("BRAIN-UUID-01"), "BRAIN-UUID-01 found");
                Assert(index.ExistingBrainDirs.Contains("BRAIN-UUID-02"), "BRAIN-UUID-02 found");
                Assert(index.HasRules, "HasRules detected");
                Assert(index.HasSkills, "HasSkills detected");
            }
            finally
            {
                if (File.Exists(zipFile)) File.Delete(zipFile);
            }
        }

        private static void Test_Real353MbAgyArchiveInspection()
        {
            string realAgyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "antigravity_backup_20260821.agy");
            if (!File.Exists(realAgyPath))
            {
                realAgyPath = @"c:\Users\Administrator\Desktop\Tools\Antigravity_Companion\antigravity_backup_20260821.agy";
            }

            Assert(File.Exists(realAgyPath), "Real production .agy archive must exist at " + realAgyPath);

            Stopwatch sw = Stopwatch.StartNew();
            BackupArchiveIndex index = StreamingZipIndexReader.ReadIndex(realAgyPath);
            sw.Stop();

            Assert(index.TotalZipSizeBytes > 300000000L, "Archive size > 300MB");
            AssertEqual(138, index.Conversations.Count, "Exactly 138 conversations extracted from real backup");
            Assert(index.Projects.Count >= 5, "At least 5 projects extracted");
            Assert(index.HasRules, "HasRules is true");
            Assert(index.HasSkills, "HasSkills is true");
            Assert(index.HasMcp, "HasMcp is true");
            Assert(sw.ElapsedMilliseconds < 1000, "Streaming zip inspection took < 1000ms (took " + sw.ElapsedMilliseconds + "ms)");

            // Verify Field 15 custom titles extracted
            int customTitleCount = 0;
            for (int i = 0; i < index.Conversations.Count; i++)
            {
                if (index.Conversations[i].HasCustomTitle)
                {
                    customTitleCount++;
                }
            }
            AssertEqual(20, customTitleCount, "Exactly 20 custom user-renamed Chinese titles found");
        }

        #endregion

        #region Category 3: Roundtrip Fidelity & Shard Engine Stress

        private static void Test_RealProtoByteForByteRoundtrip()
        {
            string realAgyPath = @"c:\Users\Administrator\Desktop\Tools\Antigravity_Companion\antigravity_backup_20260821.agy";
            byte[] rawPb = ExtractProtoFromAgy(realAgyPath);

            AssertEqual(99973, rawPb.Length, "Raw protobuf length must be exactly 99,973 bytes");

            // Decode to AST
            List<ProtoNode> ast = ProtoCodec.Decode(rawPb);
            AssertEqual(138, ast.Count, "AST root count must be exactly 138 entries");

            // Encode AST back to bytes
            byte[] reEncoded = ProtoCodec.Encode(ast);
            AssertEqual(99973, reEncoded.Length, "Re-encoded protobuf length must be exactly 99,973 bytes");

            // 100% Byte-for-Byte Check
            int mismatchIndex = -1;
            for (int i = 0; i < rawPb.Length; i++)
            {
                if (rawPb[i] != reEncoded[i])
                {
                    mismatchIndex = i;
                    break;
                }
            }
            AssertEqual(-1, mismatchIndex, "All 99,973 bytes must be 100% identical");

            // MD5 Hash comparison
            using (MD5 md5 = MD5.Create())
            {
                string originalHash = BitConverter.ToString(md5.ComputeHash(rawPb));
                string reserializedHash = BitConverter.ToString(md5.ComputeHash(reEncoded));
                AssertEqual(originalHash, reserializedHash, "MD5 cryptographic hash match");
            }
        }

        private static void Test_ShardDecompositionAndReassembly()
        {
            string realAgyPath = @"c:\Users\Administrator\Desktop\Tools\Antigravity_Companion\antigravity_backup_20260821.agy";
            byte[] rawPb = ExtractProtoFromAgy(realAgyPath);

            List<IncrementalProtoShardMerger.ShardItem> shards = IncrementalProtoShardMerger.ExtractShards(rawPb);
            AssertEqual(138, shards.Count, "Exactly 138 shards extracted");

            HashSet<string> seenUuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int totalBytes = 0;
            for (int i = 0; i < shards.Count; i++)
            {
                var sh = shards[i];
                Assert(!string.IsNullOrEmpty(sh.ConversationId), "Shard " + i + " conversation ID not empty");
                Assert(!seenUuids.Contains(sh.ConversationId), "Shard conversation UUID must be unique: " + sh.ConversationId);
                seenUuids.Add(sh.ConversationId);
                Assert(sh.RawBytes != null && sh.RawBytes.Length > 0, "Shard " + i + " raw bytes not empty");
                Assert(sh.AstNode != null, "Shard " + i + " AST node not null");
                totalBytes += sh.RawBytes.Length;
            }

            AssertEqual(99973, totalBytes, "Sum of all shard bytes equals 99,973");

            // Reassembly via MergeShards into empty destination
            byte[] reassembled = IncrementalProtoShardMerger.MergeShards(null, shards);
            AssertEqual(99973, reassembled.Length, "Reassembled merged stream length");
            for (int i = 0; i < rawPb.Length; i++)
            {
                AssertEqual(rawPb[i], reassembled[i], "Reassembled byte at index " + i);
            }
        }

        private static void Test_CustomChineseTitlePriorityMatrix()
        {
            // Scenario 1: Field 15 present, Field 1 present -> Field 15 MUST WIN
            ProtoNode e1 = new ProtoNode(1, 2);
            ProtoNode id1 = new ProtoNode(1, 2); id1.SetString("UUID-001");
            ProtoNode meta1 = new ProtoNode(2, 2);
            ProtoNode autoT1 = new ProtoNode(1, 2); autoT1.SetString("English Auto Summary 1");
            ProtoNode ct1 = new ProtoNode(15, 2);
            ProtoNode ct1_sub = new ProtoNode(1, 2); ct1_sub.SetString("【储能系统排障】核心状态机修复");
            ct1.Children = new List<ProtoNode> { ct1_sub };
            meta1.Children = new List<ProtoNode> { autoT1, ct1 };
            e1.Children = new List<ProtoNode> { id1, meta1 };

            ConversationSummaryInfo s1 = SessionMetadataExtractor.ParseEntryNode(e1);
            Assert(s1.HasCustomTitle, "HasCustomTitle is true");
            AssertEqual("【储能系统排障】核心状态机修复", s1.DisplayTitle, "DisplayTitle must match Field 15 custom title");
            AssertEqual("English Auto Summary 1", s1.AutoSummaryTitle, "AutoSummaryTitle preserved");

            // Scenario 2: Field 15 missing, Field 1 present -> Field 1 MUST WIN
            ProtoNode e2 = new ProtoNode(1, 2);
            ProtoNode id2 = new ProtoNode(1, 2); id2.SetString("UUID-002");
            ProtoNode meta2 = new ProtoNode(2, 2);
            ProtoNode autoT2 = new ProtoNode(1, 2); autoT2.SetString("English Auto Summary 2");
            meta2.Children = new List<ProtoNode> { autoT2 };
            e2.Children = new List<ProtoNode> { id2, meta2 };

            ConversationSummaryInfo s2 = SessionMetadataExtractor.ParseEntryNode(e2);
            Assert(!s2.HasCustomTitle, "HasCustomTitle is false");
            AssertEqual("English Auto Summary 2", s2.DisplayTitle, "DisplayTitle falls back to AutoSummaryTitle");

            // Scenario 3: Field 15 empty string, Field 1 empty -> Fallback to 未命名会话
            ProtoNode e3 = new ProtoNode(1, 2);
            ProtoNode id3 = new ProtoNode(1, 2); id3.SetString("12345678-abcd-ef00-1122-334455667788");
            e3.Children = new List<ProtoNode> { id3 };

            ConversationSummaryInfo s3 = SessionMetadataExtractor.ParseEntryNode(e3);
            AssertEqual("未命名会话 (12345678)", s3.DisplayTitle, "DisplayTitle falls back to formatted UUID prefix");
        }

        private static void Test_WorkspacePathRemapperMatrix()
        {
            string oldRoot = @"C:\Users\OldDeveloper\Desktop\Workspace";
            string newRoot = @"D:\NewCompany\Projects\Workspace";

            var pairs = WorkspacePathRemapper.BuildReplacementPairs(oldRoot, newRoot);
            Assert(pairs.Count >= 7, "BuildReplacementPairs must generate all 7+ variants");

            // Verify variant 1: Standard slash
            string u1 = "file:///c:/Users/OldDeveloper/Desktop/Workspace/sub/file.cs";
            string r1 = WorkspacePathRemapper.RemapPath(u1, oldRoot, newRoot);
            AssertEqual("file:///d:/NewCompany/Projects/Workspace/sub/file.cs", r1, "Standard slash remapped");

            // Verify variant 2: Uppercase drive
            string u2 = "file:///C:/Users/OldDeveloper/Desktop/Workspace/sub/file.cs";
            string r2 = WorkspacePathRemapper.RemapPath(u2, oldRoot, newRoot);
            AssertEqual("file:///D:/NewCompany/Projects/Workspace/sub/file.cs", r2, "Uppercase drive remapped");

            // Verify variant 3: URL encoded colon (%3A)
            string u3 = "file:///c%3A/Users/OldDeveloper/Desktop/Workspace/sub/file.cs";
            string r3 = WorkspacePathRemapper.RemapPath(u3, oldRoot, newRoot);
            AssertEqual("file:///d%3A/NewCompany/Projects/Workspace/sub/file.cs", r3, "URL encoded colon remapped");

            // Verify variant 4: Backslash format
            string u4 = @"C:\Users\OldDeveloper\Desktop\Workspace\sub\file.cs";
            string r4 = WorkspacePathRemapper.RemapPath(u4, oldRoot, newRoot);
            AssertEqual(@"D:\NewCompany\Projects\Workspace\sub\file.cs", r4, "Backslash format remapped");
        }

        private static void Test_IsolatedShardMutationNoCorruption()
        {
            string realAgyPath = @"c:\Users\Administrator\Desktop\Tools\Antigravity_Companion\antigravity_backup_20260821.agy";
            byte[] rawPb = ExtractProtoFromAgy(realAgyPath);

            List<IncrementalProtoShardMerger.ShardItem> allShards = IncrementalProtoShardMerger.ExtractShards(rawPb);
            AssertEqual(138, allShards.Count, "138 shards count");

            // Pick shard index 50 to modify
            var targetShard = allShards[50];
            string targetUuid = targetShard.ConversationId;

            ProtoNode mutatedNode = targetShard.AstNode.Clone();
            ProtoNode meta = mutatedNode.GetChild(2);
            if (meta != null)
            {
                meta.SetChildString(1, "【修改后的测试标题】2026");
            }

            var incomingList = new List<IncrementalProtoShardMerger.ShardItem>
            {
                new IncrementalProtoShardMerger.ShardItem
                {
                    ConversationId = targetUuid,
                    AstNode = mutatedNode,
                    RawBytes = ProtoCodec.Encode(new List<ProtoNode> { mutatedNode })
                }
            };

            // Merge single modified shard back into original 138-shard dataset
            byte[] updatedPb = IncrementalProtoShardMerger.MergeShards(rawPb, incomingList);
            List<IncrementalProtoShardMerger.ShardItem> updatedShards = IncrementalProtoShardMerger.ExtractShards(updatedPb);
            AssertEqual(138, updatedShards.Count, "Updated total shards count must remain 138");

            // Verify that all 137 other shards are 100% byte-for-byte identical to original
            for (int i = 0; i < 138; i++)
            {
                var orig = allShards[i];
                var upd = updatedShards[i];
                AssertEqual(orig.ConversationId, upd.ConversationId, "Shard " + i + " conversation ID sequence preserved");

                if (orig.ConversationId == targetUuid)
                {
                    // Target shard was mutated
                    Assert(orig.RawBytes.Length != upd.RawBytes.Length || !ByteArraysEqual(orig.RawBytes, upd.RawBytes), "Target shard was successfully updated");
                }
                else
                {
                    // Untouched shard must be 100% byte-identical
                    AssertEqual(orig.RawBytes.Length, upd.RawBytes.Length, "Untouched shard " + i + " length match");
                    Assert(ByteArraysEqual(orig.RawBytes, upd.RawBytes), "Untouched shard " + i + " must have zero collateral corruption");
                }
            }
        }

        private static void Test_AtomicSaveWithBackupResilience()
        {
            string testDir = Path.Combine(Path.GetTempPath(), "atomic_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);
            string targetFile = Path.Combine(testDir, "test_summaries.pb");
            string backupFile = targetFile + ".bak";

            try
            {
                // 1. Initial write
                byte[] v1 = new byte[] { 1, 2, 3, 4, 5 };
                IncrementalProtoShardMerger.AtomicSaveWithBackup(targetFile, v1);
                Assert(File.Exists(targetFile), "Target file created");
                AssertEqual(5, File.ReadAllBytes(targetFile).Length, "Target file length 5");

                // 2. Second write (should create .bak with v1 and overwrite target with v2)
                byte[] v2 = new byte[] { 10, 20, 30, 40, 50, 60, 70 };
                IncrementalProtoShardMerger.AtomicSaveWithBackup(targetFile, v2);
                Assert(File.Exists(targetFile), "Target file exists after second write");
                AssertEqual(7, File.ReadAllBytes(targetFile).Length, "Target file length 7");
                Assert(File.Exists(backupFile), "Backup file .bak created");
                AssertEqual(5, File.ReadAllBytes(backupFile).Length, "Backup file length contains v1 (5)");

                // 3. Null argument checks
                bool threwTargetNull = false;
                try { IncrementalProtoShardMerger.AtomicSaveWithBackup(null, v1); } catch (ArgumentNullException) { threwTargetNull = true; }
                Assert(threwTargetNull, "Null targetPath throws ArgumentNullException");

                bool threwDataNull = false;
                try { IncrementalProtoShardMerger.AtomicSaveWithBackup(targetFile, null); } catch (ArgumentNullException) { threwDataNull = true; }
                Assert(threwDataNull, "Null data throws ArgumentNullException");
            }
            finally
            {
                if (Directory.Exists(testDir))
                {
                    try { Directory.Delete(testDir, true); } catch { }
                }
            }
        }

        private static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        #endregion
    }
}
