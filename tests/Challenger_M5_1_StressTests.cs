using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using AntigravityCompanion;

namespace AntigravityCompanion.Tests
{
    public class Challenger_M5_1_StressTests
    {
        private static int passedTests = 0;
        private static int failedTests = 0;
        private static int totalAssertions = 0;
        private static List<string> failureDetails = new List<string>();

        public static int Main(string[] args)
        {
            Console.WriteLine("===============================================================================");
            Console.WriteLine("  CHALLENGER 1 (MILESTONE 5): TIER 5 ADVERSARIAL STRESS SUITE");
            Console.WriteLine("  Target: Data & Storage Engine (Protobuf AST, Varints, Unicode, Shards, Atomic)");
            Console.WriteLine("  Environment: Native C# 5 / .NET 4.0/4.5+ BCL (MainForm.cs)");
            Console.WriteLine("===============================================================================\n");

            Stopwatch totalSw = Stopwatch.StartNew();

            // ═══════════════════════════════════════════════════════════════════
            // VECTOR 1: WIRE-FORMAT CORRUPTIONS & FUZZING
            // ═══════════════════════════════════════════════════════════════════
            Console.WriteLine(">>> [VECTOR 1] Wire-Format Corruptions, Malformed Encodings & Fuzzing");
            RunTest("V1_01_TruncatedTag_SingleByteBoundary", Test_V1_01_TruncatedTag_SingleByteBoundary);
            RunTest("V1_02_TruncatedPayloads_AllWireTypes", Test_V1_02_TruncatedPayloads_AllWireTypes);
            RunTest("V1_03_InvalidAndDeprecatedWireTypes", Test_V1_03_InvalidAndDeprecatedWireTypes);
            RunTest("V1_04_CorruptedMagicAndTrailingGarbage", Test_V1_04_CorruptedMagicAndTrailingGarbage);
            RunTest("V1_05_NestedMalformedSubmessageSafety", Test_V1_05_NestedMalformedSubmessageSafety);
            RunTest("V1_06_MassiveBitflipFuzzing_3000Iterations", Test_V1_06_MassiveBitflipFuzzing_3000Iterations);

            // ═══════════════════════════════════════════════════════════════════
            // VECTOR 2: 10-BYTE VARINTS & INTEGER BOUNDARIES
            // ═══════════════════════════════════════════════════════════════════
            Console.WriteLine("\n>>> [VECTOR 2] 10-Byte Varints, Overrun Attacks & Boundary Decoding");
            RunTest("V2_01_Canonical10ByteVarint_UlongMaxValue", Test_V2_01_Canonical10ByteVarint_UlongMaxValue);
            RunTest("V2_02_InfiniteVarintOverrun_MsbLoopDefense", Test_V2_02_InfiniteVarintOverrun_MsbLoopDefense);
            RunTest("V2_03_EndlessVarintOverrun_1000BytesMsb", Test_V2_03_EndlessVarintOverrun_1000BytesMsb);
            RunTest("V2_04_NonCanonicalZeroVarintEncodings", Test_V2_04_NonCanonicalZeroVarintEncodings);
            RunTest("V2_05_StandardAndHighFieldNumbers_UpTo28Bits", Test_V2_05_StandardAndHighFieldNumbers_UpTo28Bits);
            RunTest("V2_06_ExtremeFieldNumber_29BitOverflowProbe", Test_V2_06_ExtremeFieldNumber_29BitOverflowProbe);
            RunTest("V2_07_Negative64BitIntegerVarintEncoding", Test_V2_07_Negative64BitIntegerVarintEncoding);

            // ═══════════════════════════════════════════════════════════════════
            // VECTOR 3: LARGE UTF-8 MULTIBYTE STRINGS & UNICODE RESILIENCE
            // ═══════════════════════════════════════════════════════════════════
            Console.WriteLine("\n>>> [VECTOR 3] Large Multibyte UTF-8 Strings, Emojis & Malformed Bytes");
            RunTest("V3_01_FourByteUtf8EmojisAndSurrogatePairs", Test_V3_01_FourByteUtf8EmojisAndSurrogatePairs);
            RunTest("V3_02_ComplexMultilingualMatrixAndPriority", Test_V3_02_ComplexMultilingualMatrixAndPriority);
            RunTest("V3_03_EmbeddedNullsAndControlCharsPreservation", Test_V3_03_EmbeddedNullsAndControlCharsPreservation);
            RunTest("V3_04_InvalidUtf8SequencesGracefulFallback", Test_V3_04_InvalidUtf8SequencesGracefulFallback);
            RunTest("V3_05_MultiMegabyteStringPayload_10MB", Test_V3_05_MultiMegabyteStringPayload_10MB);

            // ═══════════════════════════════════════════════════════════════════
            // VECTOR 4: DUPLICATE SESSION MERGING & SHARD CONFLICT RESOLUTION
            // ═══════════════════════════════════════════════════════════════════
            Console.WriteLine("\n>>> [VECTOR 4] Duplicate Session Merges & Shard Conflict Logic");
            RunTest("V4_01_DuplicateUuidMerge_OverwritePreservation", Test_V4_01_DuplicateUuidMerge_OverwritePreservation);
            RunTest("V4_02_DuplicateUuid_CustomTitleVsAutoSummaryPriority", Test_V4_02_DuplicateUuid_CustomTitleVsAutoSummaryPriority);
            RunTest("V4_03_MalformedAndEmptyShardsIsolation", Test_V4_03_MalformedAndEmptyShardsIsolation);
            RunTest("V4_04_LargeScaleMerge_1000Shards50PercentDuplicates", Test_V4_04_LargeScaleMerge_1000Shards50PercentDuplicates);
            RunTest("V4_05_SkipConflictStrategyPreservesLocalState", Test_V4_05_SkipConflictStrategyPreservesLocalState);
            RunTest("V4_06_HierarchicalLayoutProjectRemapProbe", Test_V4_06_HierarchicalLayoutProjectRemapProbe);

            // ═══════════════════════════════════════════════════════════════════
            // VECTOR 5: ATOMIC WRITE-BACK PERMISSIONS & FAILURE RECOVERY
            // ═══════════════════════════════════════════════════════════════════
            Console.WriteLine("\n>>> [VECTOR 5] Atomic Write-Back Permissions & Resilient Persistence");
            RunTest("V5_01_ReadOnlyFileHandling_ExceptionAndNoTmpLeak", Test_V5_01_ReadOnlyFileHandling_ExceptionAndNoTmpLeak);
            RunTest("V5_02_ExclusivelyLockedFile_CleanupAndResilience", Test_V5_02_ExclusivelyLockedFile_CleanupAndResilience);
            RunTest("V5_03_DeeplyNestedNonExistentDirectoryCreation", Test_V5_03_DeeplyNestedNonExistentDirectoryCreation);
            RunTest("V5_04_MultiThreadedConcurrentAtomicSave_NoCollisions", Test_V5_04_MultiThreadedConcurrentAtomicSave_NoCollisions);
            RunTest("V5_05_BackupRotationFidelity_MultiGeneration", Test_V5_05_BackupRotationFidelity_MultiGeneration);

            totalSw.Stop();

            Console.WriteLine("\n===============================================================================");
            Console.WriteLine(string.Format("  CHALLENGER 1 (M5) SUMMARY: {0} Passed, {1} Failed | {2} Assertions", passedTests, failedTests, totalAssertions));
            Console.WriteLine(string.Format("  Total Duration: {0:F2}ms ({1:F2}s)", totalSw.Elapsed.TotalMilliseconds, totalSw.Elapsed.TotalSeconds));
            Console.WriteLine("===============================================================================");

            if (failureDetails.Count > 0)
            {
                Console.WriteLine("\n>>> DETECTED ADVERSARIAL VULNERABILITY / DEFECT SUMMARY:");
                for (int i = 0; i < failureDetails.Count; i++)
                {
                    Console.WriteLine(string.Format("  [{0}] {1}", i + 1, failureDetails[i]));
                }
            }

            if (failedTests == 0)
            {
                Console.WriteLine("\n>>> EMPIRICAL VERDICT: [APPROVE] Tier 5 Data & Storage Engine satisfies all adversarial tests!");
                return 0;
            }
            else
            {
                Console.WriteLine("\n>>> EMPIRICAL VERDICT: [DEFECTS DETECTED] Data & Storage Engine has empirical failure modes!");
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
                Console.WriteLine(string.Format("  [PASS] {0} ({1:F2}ms)", testName.PadRight(65), sw.Elapsed.TotalMilliseconds));
                passedTests++;
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("  [FAIL] {0}", testName));
                Console.WriteLine("         Exception: " + ex.GetType().Name + ": " + ex.Message);
                Console.WriteLine("         Stack: " + ex.StackTrace);
                failureDetails.Add(testName + " -> " + ex.Message);
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
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception(string.Format("Assertion Failed: {0} (Expected: '{1}', Actual: '{2}')", message, expected, actual));
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

        private static string ComputeMd5(byte[] data)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(data);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // IMPLEMENTATION OF TEST METHODS
        // ═══════════════════════════════════════════════════════════════════════

        #region Vector 1: Wire-Format Corruptions & Fuzzing

        private static void Test_V1_01_TruncatedTag_SingleByteBoundary()
        {
            // A tag with MSB set (indicating multi-byte tag) but stream ends abruptly
            byte[] truncatedTagStream = new byte[] { 0x80 }; // MSB set, no continuation byte
            List<ProtoNode> nodes = ProtoAstCodec.Decode(truncatedTagStream);
            Assert(nodes != null, "Decoder must return non-null list for truncated tag");
            AssertEqual(0, nodes.Count, "Truncated tag should yield 0 valid nodes");

            // 1 valid node followed by a truncated tag
            byte[] validPlusTruncated = new byte[] { 0x08, 0x01, 0x80 }; // Field 1 Varint=1, then incomplete tag 0x80
            nodes = ProtoAstCodec.Decode(validPlusTruncated);
            AssertEqual(1, nodes.Count, "Decoder must yield 1 valid node before truncated tag");
            AssertEqual(1, nodes[0].FieldNumber, "Field number must be 1");
            AssertEqual((ulong)1, nodes[0].VarintValue, "Varint value must be 1");
        }

        private static void Test_V1_02_TruncatedPayloads_AllWireTypes()
        {
            // WireType 0 (Varint): Tag Field 1 Wire 0 (0x08), then truncated Varint with MSB set
            byte[] truncVarint = new byte[] { 0x08, 0x80 };
            List<ProtoNode> nodes = ProtoAstCodec.Decode(truncVarint);
            AssertEqual(0, nodes.Count, "Truncated varint payload must break gracefully without node emission");

            // WireType 1 (Fixed64): Tag Field 1 Wire 1 (0x09), only 4 bytes instead of 8
            byte[] truncFixed64 = new byte[] { 0x09, 0x11, 0x22, 0x33, 0x44 };
            nodes = ProtoAstCodec.Decode(truncFixed64);
            AssertEqual(0, nodes.Count, "Truncated fixed64 must terminate parsing gracefully");

            // WireType 2 (Length-Delimited): Tag Field 1 Wire 2 (0x0A), length says 50 bytes, only 5 bytes present
            byte[] truncLengthDelimited = new byte[] { 0x0A, 0x32, 0x01, 0x02, 0x03, 0x04, 0x05 };
            nodes = ProtoAstCodec.Decode(truncLengthDelimited);
            AssertEqual(1, nodes.Count, "Partial length-delimited payload emits node with available bytes");
            AssertEqual(5, nodes[0].RawBytes.Length, "RawBytes must capture remaining 5 bytes safely");

            // WireType 5 (Fixed32): Tag Field 1 Wire 5 (0x0D), only 2 bytes instead of 4
            byte[] truncFixed32 = new byte[] { 0x0D, 0xAA, 0xBB };
            nodes = ProtoAstCodec.Decode(truncFixed32);
            AssertEqual(0, nodes.Count, "Truncated fixed32 must terminate parsing gracefully");
        }

        private static void Test_V1_03_InvalidAndDeprecatedWireTypes()
        {
            // Invalid wire types: 3 (groups start), 4 (groups end), 6 (invalid), 7 (invalid)
            int[] invalidWireTypes = new int[] { 3, 4, 6, 7 };
            foreach (int wt in invalidWireTypes)
            {
                byte tag = (byte)((1 << 3) | wt);
                byte[] stream = new byte[] { tag, 0x01, 0x02, 0x03 };
                List<ProtoNode> nodes = ProtoAstCodec.Decode(stream);
                AssertEqual(0, nodes.Count, "Invalid wire type " + wt + " must be safely rejected without exception");
            }

            // Valid node followed by invalid wire type
            byte validTag = (byte)((1 << 3) | 0); // Field 1 Varint=42
            byte invalidTag = (byte)((2 << 3) | 6); // Field 2 WireType 6
            byte[] mixedStream = new byte[] { validTag, 42, invalidTag, 0x01, 0x02 };
            List<ProtoNode> mixedNodes = ProtoAstCodec.Decode(mixedStream);
            AssertEqual(1, mixedNodes.Count, "Prior valid node must be preserved when encountering invalid wire type");
            AssertEqual(1, mixedNodes[0].FieldNumber, "Field number 1");
            AssertEqual((ulong)42, mixedNodes[0].VarintValue, "Varint value 42");
        }

        private static void Test_V1_04_CorruptedMagicAndTrailingGarbage()
        {
            // Create a valid protobuf message
            ProtoNode node1 = new ProtoNode(1, 2);
            node1.SetString("Valid conversation metadata payload");
            byte[] validBytes = ProtoAstCodec.Encode(new List<ProtoNode> { node1 });

            // Append 10,000 bytes of pseudo-random non-protobuf noise
            byte[] noisyBytes = new byte[validBytes.Length + 10000];
            Buffer.BlockCopy(validBytes, 0, noisyBytes, 0, validBytes.Length);
            Random rnd = new Random(12345);
            for (int i = validBytes.Length; i < noisyBytes.Length; i++)
            {
                noisyBytes[i] = (byte)rnd.Next(0, 256);
            }

            List<ProtoNode> decoded = ProtoAstCodec.Decode(noisyBytes);
            Assert(decoded.Count >= 1, "Must decode the valid initial node despite trailing garbage");
            AssertEqual("Valid conversation metadata payload", decoded[0].GetString(), "First node payload integrity");
        }

        private static void Test_V1_05_NestedMalformedSubmessageSafety()
        {
            // Length-delimited entry that looks like a submessage but is internally corrupted
            // Outer node: Field 1 Wire 2, length 10
            // Inside: Field 1 Wire 0 with truncated varint
            byte[] corruptedSub = new byte[] { 0x0A, 0x04, 0x08, 0x80, 0x80, 0x80 }; // length 4, inner varint truncated
            List<ProtoNode> nodes = ProtoAstCodec.Decode(corruptedSub);
            AssertEqual(1, nodes.Count, "Outer node must be preserved");
            Assert(nodes[0].Children == null || nodes[0].Children.Count == 0, "Corrupted submessage must safely fall back to raw bytes");
            AssertEqual(4, nodes[0].RawBytes.Length, "RawBytes must preserve the 4 bytes exactly");
        }

        private static void Test_V1_06_MassiveBitflipFuzzing_3000Iterations()
        {
            // Construct a multi-tier protobuf payload
            ProtoNode root = new ProtoNode(1, 2);
            root.Children = new List<ProtoNode>();
            ProtoNode f1 = new ProtoNode(1, 2); f1.SetString("session-uuid-12345");
            ProtoNode f2 = new ProtoNode(2, 2);
            f2.Children = new List<ProtoNode>();
            ProtoNode title = new ProtoNode(1, 2); title.SetString("Auto Summary");
            ProtoNode custom = new ProtoNode(15, 2);
            custom.Children = new List<ProtoNode> { new ProtoNode(1, 2) };
            custom.Children[0].SetString("用户自定义标题");
            f2.Children.Add(title);
            f2.Children.Add(custom);
            root.Children.Add(f1);
            root.Children.Add(f2);

            byte[] goldenBytes = ProtoAstCodec.Encode(new List<ProtoNode> { root });
            Random rnd = new Random(42);

            for (int iter = 0; iter < 3000; iter++)
            {
                byte[] mutated = (byte[])goldenBytes.Clone();
                int numCorruptions = rnd.Next(1, 10);
                for (int c = 0; c < numCorruptions; c++)
                {
                    int pos = rnd.Next(0, mutated.Length);
                    mutated[pos] = (byte)rnd.Next(0, 256);
                }

                // Execute decoder and extractor on mutated stream - must never crash or throw unhandled exceptions
                try
                {
                    List<ProtoNode> res = ProtoAstCodec.Decode(mutated);
                    if (res != null && res.Count > 0)
                    {
                        var info = SessionMetadataExtractor.ParseEntryNode(res[0]);
                        Assert(info != null, "Extracted info must not be null");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("Fuzzing crashed at iteration " + iter + ": " + ex.Message, ex);
                }
            }
            Assert(true, "3000 fuzzing iterations completed without crash");
        }

        #endregion

        #region Vector 2: 10-Byte Varints & Integer Boundaries

        private static void Test_V2_01_Canonical10ByteVarint_UlongMaxValue()
        {
            // ulong.MaxValue is 0xFFFFFFFFFFFFFFFF
            // In 7-bit chunks: 9 bytes of 0xFF (with MSB 0x80 set) + 1 byte of 0x01
            byte[] canonicalUlongMax = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 };
            ulong val;
            int bytesRead = ProtoCodec.ReadVarint(canonicalUlongMax, 0, canonicalUlongMax.Length, out val);
            AssertEqual(10, bytesRead, "10-byte varint should consume 10 bytes");
            AssertEqual(ulong.MaxValue, val, "Decoded value must equal ulong.MaxValue");

            // Test roundtrip encoding
            byte[] encoded = ProtoCodec.EncodeVarint(ulong.MaxValue);
            AssertEqual(10, encoded.Length, "Encoded ulong.MaxValue must be exactly 10 bytes");
            Assert(ByteArraysEqual(canonicalUlongMax, encoded), "Encoded ulong.MaxValue must match canonical byte sequence");
        }

        private static void Test_V2_02_InfiniteVarintOverrun_MsbLoopDefense()
        {
            // 10-byte sequence where 10th byte also has MSB (0x80) set -> invalid varint exceeding 64 bits
            byte[] invalid10Byte = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x81 };
            ulong val;
            int bytesRead = ProtoCodec.ReadVarint(invalid10Byte, 0, invalid10Byte.Length, out val);
            AssertEqual(-1, bytesRead, "Varint with MSB set after 64 bits must return -1 (failure)");
        }

        private static void Test_V2_03_EndlessVarintOverrun_1000BytesMsb()
        {
            // 1,000 continuous bytes of 0x80
            byte[] endlessMsb = new byte[1000];
            for (int i = 0; i < endlessMsb.Length; i++) endlessMsb[i] = 0x80;

            ulong val;
            int bytesRead = ProtoCodec.ReadVarint(endlessMsb, 0, endlessMsb.Length, out val);
            AssertEqual(-1, bytesRead, "Endless MSB sequence must terminate safely within 10 bytes and return -1");
        }

        private static void Test_V2_04_NonCanonicalZeroVarintEncodings()
        {
            // 1-byte canonical zero: 0x00
            ulong val1;
            int len1 = ProtoCodec.ReadVarint(new byte[] { 0x00 }, 0, 1, out val1);
            AssertEqual(1, len1, "Canonical zero length 1");
            AssertEqual((ulong)0, val1, "Canonical zero value");

            // 2-byte non-canonical zero: 0x80, 0x00
            ulong val2;
            int len2 = ProtoCodec.ReadVarint(new byte[] { 0x80, 0x00 }, 0, 2, out val2);
            AssertEqual(2, len2, "2-byte zero length 2");
            AssertEqual((ulong)0, val2, "2-byte zero value");

            // 5-byte non-canonical zero: 0x80, 0x80, 0x80, 0x80, 0x00
            ulong val5;
            int len5 = ProtoCodec.ReadVarint(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x00 }, 0, 5, out val5);
            AssertEqual(5, len5, "5-byte zero length 5");
            AssertEqual((ulong)0, val5, "5-byte zero value");

            // 10-byte non-canonical zero: 9 x 0x80 + 1 x 0x00
            byte[] tenByteZero = new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x00 };
            ulong val10;
            int len10 = ProtoCodec.ReadVarint(tenByteZero, 0, 10, out val10);
            AssertEqual(10, len10, "10-byte zero length 10");
            AssertEqual((ulong)0, val10, "10-byte zero value");
        }

        private static void Test_V2_05_StandardAndHighFieldNumbers_UpTo28Bits()
        {
            // Test standard and high field numbers within 28-bit shift safety (< 268,435,456)
            int[] testFieldNumbers = new int[] { 1, 2, 15, 17, 100, 1000, 100000, 268435455 };
            foreach (int fn in testFieldNumbers)
            {
                ProtoNode node = new ProtoNode(fn, 0);
                node.VarintValue = 12345UL;

                byte[] encoded = ProtoAstCodec.Encode(new List<ProtoNode> { node });
                List<ProtoNode> decoded = ProtoAstCodec.Decode(encoded);

                AssertEqual(1, decoded.Count, "Decoded count 1 for field " + fn);
                AssertEqual(fn, decoded[0].FieldNumber, "Field number roundtrip for " + fn);
                AssertEqual(12345UL, decoded[0].VarintValue, "Varint value roundtrip for " + fn);
            }
        }

        private static void Test_V2_06_ExtremeFieldNumber_29BitOverflowProbe()
        {
            // Max allowed Protobuf field number is (1 << 29) - 1 = 536,870,911 (0x1FFFFFFF)
            int maxFieldNum = 536870911;
            ProtoNode node = new ProtoNode(maxFieldNum, 0);
            node.VarintValue = 123456789UL;

            byte[] encoded = ProtoAstCodec.Encode(new List<ProtoNode> { node });
            List<ProtoNode> decoded = ProtoAstCodec.Decode(encoded);

            AssertEqual(1, decoded.Count, "Decoded count 1");
            
            // Empirical probe: Check if signed 32-bit shift in (node.FieldNumber << 3) caused overflow
            if (decoded[0].FieldNumber != maxFieldNum)
            {
                throw new Exception(string.Format("Protobuf Tag Encoding Bug: Field number {0} (>= 2^28) overflows signed 32-bit shift (node.FieldNumber << 3) yielding decoded field number {1}", maxFieldNum, decoded[0].FieldNumber));
            }
        }

        private static void Test_V2_07_Negative64BitIntegerVarintEncoding()
        {
            // Negative signed int64 (-1) encoded as 64-bit two's complement varint (ulong.MaxValue)
            ProtoNode node = new ProtoNode(1, 0);
            node.SetInt64(-1L);
            AssertEqual((ulong)0xFFFFFFFFFFFFFFFF, node.VarintValue, "VarintValue stores two's complement");
            AssertEqual(-1L, node.GetInt64(), "GetInt64 recovers -1L");

            byte[] encoded = ProtoAstCodec.Encode(new List<ProtoNode> { node });
            List<ProtoNode> decoded = ProtoAstCodec.Decode(encoded);
            AssertEqual(1, decoded.Count, "Decoded node count");
            AssertEqual(-1L, decoded[0].GetInt64(), "Decoded GetInt64 recovers -1L");

            // Int32 boundaries: Int32.MinValue (-2147483648)
            node.SetInt32(int.MinValue);
            AssertEqual(int.MinValue, node.GetInt32(), "GetInt32 recovers int.MinValue");
        }

        #endregion

        #region Vector 3: Large Multibyte UTF-8 Strings & Unicode Resilience

        private static void Test_V3_01_FourByteUtf8EmojisAndSurrogatePairs()
        {
            string emojiString = "🚀 Rocket 🌟 Star 🎉 Party 🤖 Robot 💥 Boom 🧠 Brain 🔥 Fire";
            ProtoNode node = new ProtoNode(15, 2);
            node.SetString(emojiString);

            byte[] rawBytes = node.RawBytes;
            byte[] expectedBytes = Encoding.UTF8.GetBytes(emojiString);
            Assert(ByteArraysEqual(expectedBytes, rawBytes), "RawBytes must match UTF-8 encoding of emoji string");

            byte[] encoded = ProtoAstCodec.Encode(new List<ProtoNode> { node });
            List<ProtoNode> decoded = ProtoAstCodec.Decode(encoded);

            AssertEqual(1, decoded.Count, "Decoded count 1");
            AssertEqual(emojiString, decoded[0].GetString(), "Decoded emoji string must match original perfectly");
        }

        private static void Test_V3_02_ComplexMultilingualMatrixAndPriority()
        {
            // Matrix of complex multilingual titles
            string[] testTitles = new string[]
            {
                "【重构】Antigravity 伴侣双树架构迁移 2026",
                "日本語のタイトル：プロトコルバッファの復元と同期",
                "한국어 제목: 듀얼 트리 비주얼 비교 및 선택적 가져오기",
                "العربية: معالجة البيانات واستعادة الجلسات بدون فقدان",
                "עברית: בדיקת פרוטוקול בינארי וסינכרון נתונים",
                "हिन्दी: प्रोटोकॉल बफर और कस्टम शीर्षक निष्कर्षण",
                "Русский: Восстановление метаданных сессий IDE",
                "Ελληνικά: Δοκιμή ακεραιότητας μεταδεδομένων"
            };

            foreach (string title in testTitles)
            {
                ProtoNode entryNode = new ProtoNode(1, 2);
                entryNode.Children = new List<ProtoNode>();
                ProtoNode f1 = new ProtoNode(1, 2); f1.SetString(Guid.NewGuid().ToString());
                ProtoNode meta = new ProtoNode(2, 2);
                meta.Children = new List<ProtoNode>();

                // Field 1 Auto summary (English fallback)
                ProtoNode autoSum = new ProtoNode(1, 2);
                autoSum.SetString("Generic Auto Summary");
                meta.Children.Add(autoSum);

                // Field 15 Custom Title (Multilingual)
                ProtoNode custMeta = new ProtoNode(15, 2);
                custMeta.Children = new List<ProtoNode> { new ProtoNode(1, 2) };
                custMeta.Children[0].SetString(title);
                meta.Children.Add(custMeta);

                entryNode.Children.Add(f1);
                entryNode.Children.Add(meta);

                ConversationSummaryInfo info = SessionMetadataExtractor.ParseEntryNode(entryNode);
                Assert(info.HasCustomTitle, "Must detect custom title for " + title);
                AssertEqual(title, info.DisplayTitle, "DisplayTitle must match custom title for " + title);
                AssertEqual(title, info.CustomTitle, "CustomTitle must match exact multilingual text");
            }
        }

        private static void Test_V3_03_EmbeddedNullsAndControlCharsPreservation()
        {
            // String with embedded nulls and control characters
            string embeddedNullStr = "Prefix\0Null1\0\t\r\nSuffix";
            ProtoNode node = new ProtoNode(1, 2);
            node.SetString(embeddedNullStr);

            byte[] encoded = ProtoAstCodec.Encode(new List<ProtoNode> { node });
            List<ProtoNode> decoded = ProtoAstCodec.Decode(encoded);

            AssertEqual(1, decoded.Count, "Decoded count 1");
            AssertEqual(embeddedNullStr, decoded[0].GetString(), "Embedded nulls must be preserved without truncation");
            AssertEqual(embeddedNullStr.Length, decoded[0].GetString().Length, "String length must match exactly");
        }

        private static void Test_V3_04_InvalidUtf8SequencesGracefulFallback()
        {
            // Node containing invalid non-UTF-8 bytes (e.g. 0xFF, 0xFE, isolated continuation byte 0x80)
            byte[] invalidUtf8 = new byte[] { 0xFF, 0xFE, 0xC0, 0xAF, 0x80, 0x81 };
            ProtoNode node = new ProtoNode(1, 2);
            node.RawBytes = invalidUtf8;

            // GetString should not throw exception
            string str = node.GetString();
            Assert(str != null, "GetString must return a string (replacement characters or valid string) without exception");

            // Encode and Decode should preserve raw bytes byte-for-byte
            byte[] encoded = ProtoAstCodec.Encode(new List<ProtoNode> { node });
            List<ProtoNode> decoded = ProtoAstCodec.Decode(encoded);
            AssertEqual(1, decoded.Count, "Decoded count 1");
            Assert(ByteArraysEqual(invalidUtf8, decoded[0].RawBytes), "RawBytes must be preserved 100% byte-for-byte");
        }

        private static void Test_V3_05_MultiMegabyteStringPayload_10MB()
        {
            // Generate a 10MB UTF-8 text payload
            int targetSize = 10 * 1024 * 1024; // 10MB
            StringBuilder sb = new StringBuilder(targetSize);
            string chunk = "【Antigravity 伴侣超大规模字符串压力测试 — 数据引擎稳定性验证 2026】\n";
            while (sb.Length < targetSize)
            {
                sb.Append(chunk);
            }
            string massiveString = sb.ToString();

            ProtoNode node = new ProtoNode(1, 2);
            node.SetString(massiveString);

            byte[] encoded = ProtoAstCodec.Encode(new List<ProtoNode> { node });
            Assert(encoded.Length >= targetSize, "Encoded length must be >= 10MB");

            List<ProtoNode> decoded = ProtoAstCodec.Decode(encoded);
            AssertEqual(1, decoded.Count, "Decoded count 1");
            AssertEqual(massiveString.Length, decoded[0].GetString().Length, "Decoded string length must match 10MB");
            AssertEqual(ComputeMd5(Encoding.UTF8.GetBytes(massiveString)), ComputeMd5(decoded[0].RawBytes), "MD5 hash must match");
        }

        #endregion

        #region Vector 4: Duplicate Session Merging & Shard Conflict Logic

        private static void Test_V4_01_DuplicateUuidMerge_OverwritePreservation()
        {
            string uuid = Guid.NewGuid().ToString();

            // Local entry: old timestamp
            ProtoNode localEntry = CreateMockSessionProto(uuid, "Old Title Local", "C:\\OldProject", new DateTime(2026, 1, 1));
            byte[] localPb = ProtoAstCodec.Encode(new List<ProtoNode> { localEntry });

            // Backup entry: new timestamp and updated title
            ProtoNode backupEntry = CreateMockSessionProto(uuid, "New Title Backup", "D:\\NewProject", new DateTime(2026, 8, 20));
            byte[] backupPb = ProtoAstCodec.Encode(new List<ProtoNode> { backupEntry });

            HashSet<string> targetUuids = new HashSet<string> { uuid };
            byte[] mergedPb = SelectiveCascadingImporter.MergeProtoShards(localPb, backupPb, targetUuids, null, null);

            List<ProtoNode> mergedNodes = ProtoAstCodec.Decode(mergedPb);
            AssertEqual(1, mergedNodes.Count, "Merged dataset must contain exactly 1 entry (deduplicated)");

            ConversationSummaryInfo mergedInfo = SessionMetadataExtractor.ParseEntryNode(mergedNodes[0]);
            AssertEqual(uuid, mergedInfo.ConversationId, "UUID match");
            AssertEqual("New Title Backup", mergedInfo.DisplayTitle, "Backup entry must replace local entry");
        }

        private static void Test_V4_02_DuplicateUuid_CustomTitleVsAutoSummaryPriority()
        {
            string uuid = Guid.NewGuid().ToString();

            // Local entry: Only Auto Summary (Field 1)
            ProtoNode localEntry = new ProtoNode(1, 2);
            localEntry.Children = new List<ProtoNode>();
            ProtoNode f1 = new ProtoNode(1, 2); f1.SetString(uuid);
            ProtoNode meta1 = new ProtoNode(2, 2);
            meta1.Children = new List<ProtoNode> { new ProtoNode(1, 2) };
            meta1.Children[0].SetString("Local Generic Auto Summary");
            localEntry.Children.Add(f1); localEntry.Children.Add(meta1);
            byte[] localPb = ProtoAstCodec.Encode(new List<ProtoNode> { localEntry });

            // Backup entry: Has Field 15 Custom Title
            ProtoNode backupEntry = new ProtoNode(1, 2);
            backupEntry.Children = new List<ProtoNode>();
            ProtoNode f1b = new ProtoNode(1, 2); f1b.SetString(uuid);
            ProtoNode meta2 = new ProtoNode(2, 2);
            meta2.Children = new List<ProtoNode>();
            ProtoNode auto2 = new ProtoNode(1, 2); auto2.SetString("Backup Auto Summary");
            ProtoNode custom2 = new ProtoNode(15, 2);
            custom2.Children = new List<ProtoNode> { new ProtoNode(1, 2) };
            custom2.Children[0].SetString("【用户重命名】高优先级自定义标题");
            meta2.Children.Add(auto2); meta2.Children.Add(custom2);
            backupEntry.Children.Add(f1b); backupEntry.Children.Add(meta2);
            byte[] backupPb = ProtoAstCodec.Encode(new List<ProtoNode> { backupEntry });

            byte[] mergedPb = SelectiveCascadingImporter.MergeProtoShards(localPb, backupPb, new HashSet<string> { uuid }, null, null);
            List<ProtoNode> mergedNodes = ProtoAstCodec.Decode(mergedPb);
            AssertEqual(1, mergedNodes.Count, "Merged count 1");

            ConversationSummaryInfo info = SessionMetadataExtractor.ParseEntryNode(mergedNodes[0]);
            Assert(info.HasCustomTitle, "Must detect custom title");
            AssertEqual("【用户重命名】高优先级自定义标题", info.DisplayTitle, "Custom title must take 100% priority");
        }

        private static void Test_V4_03_MalformedAndEmptyShardsIsolation()
        {
            // Dataset with valid entry, corrupted entry, 0-byte entry, and entry with no UUID
            ProtoNode valid = CreateMockSessionProto("valid-uuid-001", "Valid Session", "C:\\Proj", DateTime.UtcNow);
            ProtoNode empty = new ProtoNode(1, 2); // empty length-delimited
            ProtoNode corrupted = new ProtoNode(1, 2);
            corrupted.RawBytes = new byte[] { 0xFF, 0xFF, 0xFF }; // garbage payload

            byte[] mixedPb = ProtoAstCodec.Encode(new List<ProtoNode> { valid, empty, corrupted });
            List<IncrementalProtoShardMerger.ShardItem> shards = IncrementalProtoShardMerger.ExtractShards(mixedPb);

            Assert(shards.Count >= 1, "Must extract at least the valid shard");
            AssertEqual("valid-uuid-001", shards[0].ConversationId, "First shard conversation ID match");
        }

        private static void Test_V4_04_LargeScaleMerge_1000Shards50PercentDuplicates()
        {
            List<ProtoNode> localList = new List<ProtoNode>();
            List<ProtoNode> backupList = new List<ProtoNode>();
            HashSet<string> targetUuids = new HashSet<string>();

            // 500 local sessions (ID 0..499)
            for (int i = 0; i < 500; i++)
            {
                string id = string.Format("session-uuid-{0:D4}", i);
                localList.Add(CreateMockSessionProto(id, "Local Title " + i, "C:\\LocalProj", DateTime.UtcNow.AddDays(-10)));
            }

            // 500 backup sessions: 250 duplicates (ID 250..499) and 250 brand new (ID 500..749)
            for (int i = 250; i < 750; i++)
            {
                string id = string.Format("session-uuid-{0:D4}", i);
                targetUuids.Add(id);
                backupList.Add(CreateMockSessionProto(id, "Backup Title " + i, "D:\\BackupProj", DateTime.UtcNow));
            }

            byte[] localPb = ProtoAstCodec.Encode(localList);
            byte[] backupPb = ProtoAstCodec.Encode(backupList);

            byte[] mergedPb = SelectiveCascadingImporter.MergeProtoShards(localPb, backupPb, targetUuids, null, null);
            List<ProtoNode> mergedNodes = ProtoAstCodec.Decode(mergedPb);

            // Total unique sessions should be exactly 750 (0..749)
            AssertEqual(750, mergedNodes.Count, "Merged session count must be exactly 750");

            // Verify duplicate ID 250 has the updated backup title
            for (int i = 0; i < mergedNodes.Count; i++)
            {
                ConversationSummaryInfo info = SessionMetadataExtractor.ParseEntryNode(mergedNodes[i]);
                if (info.ConversationId == "session-uuid-0250")
                {
                    AssertEqual("Backup Title 250", info.DisplayTitle, "Overwritten duplicate session title");
                }
                else if (info.ConversationId == "session-uuid-0010")
                {
                    AssertEqual("Local Title 10", info.DisplayTitle, "Untouched local session title");
                }
                else if (info.ConversationId == "session-uuid-0700")
                {
                    AssertEqual("Backup Title 700", info.DisplayTitle, "New backup session title");
                }
            }
        }

        private static void Test_V4_05_SkipConflictStrategyPreservesLocalState()
        {
            // Verify Skip strategy leaves local session intact and omits backup session
            string uuid = "skip-session-1";
            ProtoNode localEntry = CreateMockSessionProtoWithProj(uuid, "Original Local Session", "ProjSkip");
            ProtoNode backupEntry = CreateMockSessionProtoWithProj(uuid, "Incoming Backup Session", "ProjSkip");

            byte[] localPb = ProtoAstCodec.Encode(new List<ProtoNode> { localEntry });
            byte[] backupPb = ProtoAstCodec.Encode(new List<ProtoNode> { backupEntry });

            HashSet<string> targets = new HashSet<string> { uuid };
            Dictionary<string, ConflictResolutionStrategy> strats = new Dictionary<string, ConflictResolutionStrategy>
            {
                { "ProjSkip", ConflictResolutionStrategy.Skip }
            };

            byte[] mergedPb = SelectiveCascadingImporter.MergeProtoShards(localPb, backupPb, targets, null, strats);
            List<ProtoNode> mergedNodes = ProtoAstCodec.Decode(mergedPb);

            AssertEqual(1, mergedNodes.Count, "Total nodes count 1");
            ConversationSummaryInfo info = SessionMetadataExtractor.ParseEntryNode(mergedNodes[0]);
            AssertEqual("Original Local Session", info.DisplayTitle, "Skipped project must preserve local session state");
        }

        private static void Test_V4_06_HierarchicalLayoutProjectRemapProbe()
        {
            // Real-world Hierarchical layout: Field 17 (CascadeInfo) is nested inside Field 2 (metaSubmessage)
            ProtoNode entry = CreateMockSessionProtoWithProj("hier_s1", "Backup Hierarchical Session", "ProjOld");
            byte[] backupPb = ProtoAstCodec.Encode(new List<ProtoNode> { entry });
            byte[] localPb = new byte[0];

            HashSet<string> targets = new HashSet<string> { "hier_s1" };
            Dictionary<string, string> renames = new Dictionary<string, string> { { "ProjOld", "ProjRenamed_Hier" } };
            Dictionary<string, ConflictResolutionStrategy> strats = new Dictionary<string, ConflictResolutionStrategy>
            {
                { "ProjOld", ConflictResolutionStrategy.Rename }
            };

            byte[] mergedPb = SelectiveCascadingImporter.MergeProtoShards(localPb, backupPb, targets, renames, strats);
            List<ProtoNode> mergedNodes = ProtoAstCodec.Decode(mergedPb);
            AssertEqual(1, mergedNodes.Count, "Merged nodes count 1");

            ConversationSummaryInfo info = SessionMetadataExtractor.ParseEntryNode(mergedNodes[0]);

            if (info.ProjectName != "ProjRenamed_Hier")
            {
                throw new Exception(string.Format("Hierarchical AST Remap Defect: RemapEntryProject (MainForm.cs:940) iterates only top-level children of entryNode, failing to traverse into Field 2 (metaSubmessage) where Field 17 (CascadeInfo) resides in real-world protobuf files. Actual ProjectName remained: '{0}' instead of 'ProjRenamed_Hier'", info.ProjectName));
            }
        }

        #endregion

        #region Vector 5: Atomic Write-Back Permissions & Resilient Persistence

        private static void Test_V5_01_ReadOnlyFileHandling_ExceptionAndNoTmpLeak()
        {
            string testDir = Path.Combine(Path.GetTempPath(), "challenger_readonly_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);
            string targetFile = Path.Combine(testDir, "readonly_test.pb");

            try
            {
                // Write initial file and make it ReadOnly
                byte[] initial = new byte[] { 1, 2, 3, 4, 5 };
                File.WriteAllBytes(targetFile, initial);
                File.SetAttributes(targetFile, FileAttributes.ReadOnly);

                byte[] updated = new byte[] { 9, 9, 9 };
                bool threw = false;

                try
                {
                    IncrementalProtoShardMerger.AtomicSaveWithBackup(targetFile, updated);
                }
                catch (IOException)
                {
                    threw = true;
                }
                catch (UnauthorizedAccessException)
                {
                    threw = true;
                }

                Assert(threw, "Writing to ReadOnly destination must throw IOException or UnauthorizedAccessException");

                // Verify original file content is intact (1, 2, 3, 4, 5)
                File.SetAttributes(targetFile, FileAttributes.Normal);
                byte[] current = File.ReadAllBytes(targetFile);
                Assert(ByteArraysEqual(initial, current), "Original file content must not be corrupted");

                // Verify no lingering .tmp files exist in test directory
                string[] tmpFiles = Directory.GetFiles(testDir, "*.tmp");
                AssertEqual(0, tmpFiles.Length, "No orphaned .tmp files should be left behind");
            }
            finally
            {
                try
                {
                    if (File.Exists(targetFile)) File.SetAttributes(targetFile, FileAttributes.Normal);
                    if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
                }
                catch { }
            }
        }

        private static void Test_V5_02_ExclusivelyLockedFile_CleanupAndResilience()
        {
            string testDir = Path.Combine(Path.GetTempPath(), "challenger_locked_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);
            string targetFile = Path.Combine(testDir, "locked_test.pb");

            try
            {
                byte[] initial = new byte[] { 10, 20, 30 };
                File.WriteAllBytes(targetFile, initial);

                bool threw = false;
                // Exclusively lock the destination file
                using (FileStream lockFs = new FileStream(targetFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    try
                    {
                        IncrementalProtoShardMerger.AtomicSaveWithBackup(targetFile, new byte[] { 99, 99 });
                    }
                    catch (IOException)
                    {
                        threw = true;
                    }
                }

                Assert(threw, "Writing to exclusively locked file must throw IOException");

                // Verify no lingering .tmp files
                string[] tmpFiles = Directory.GetFiles(testDir, "*.tmp");
                AssertEqual(0, tmpFiles.Length, "No orphaned .tmp files left after locked file failure");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
                }
                catch { }
            }
        }

        private static void Test_V5_03_DeeplyNestedNonExistentDirectoryCreation()
        {
            string rootDir = Path.Combine(Path.GetTempPath(), "challenger_deep_" + Guid.NewGuid().ToString("N"));
            string deeplyNested = Path.Combine(rootDir, "level1", "level2", "level3", "deep_target.pb");

            try
            {
                byte[] data = new byte[] { 7, 8, 9, 10 };
                IncrementalProtoShardMerger.AtomicSaveWithBackup(deeplyNested, data);

                Assert(File.Exists(deeplyNested), "Deeply nested file must exist");
                Assert(ByteArraysEqual(data, File.ReadAllBytes(deeplyNested)), "Content match for deeply nested file");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(rootDir)) Directory.Delete(rootDir, true);
                }
                catch { }
            }
        }

        private static void Test_V5_04_MultiThreadedConcurrentAtomicSave_NoCollisions()
        {
            string testDir = Path.Combine(Path.GetTempPath(), "challenger_concurrent_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);

            try
            {
                int numThreads = 16;
                Thread[] threads = new Thread[numThreads];
                bool[] results = new bool[numThreads];
                Exception[] exceptions = new Exception[numThreads];

                for (int t = 0; t < numThreads; t++)
                {
                    int index = t;
                    threads[t] = new Thread(new ThreadStart(delegate()
                    {
                        try
                        {
                            string threadTarget = Path.Combine(testDir, string.Format("file_{0}.pb", index));
                            byte[] threadData = Encoding.UTF8.GetBytes("Thread Data Payload " + index);
                            IncrementalProtoShardMerger.AtomicSaveWithBackup(threadTarget, threadData);
                            byte[] readBack = File.ReadAllBytes(threadTarget);
                            results[index] = ByteArraysEqual(threadData, readBack);
                        }
                        catch (Exception ex)
                        {
                            exceptions[index] = ex;
                            results[index] = false;
                        }
                    }));
                }

                for (int t = 0; t < numThreads; t++) threads[t].Start();
                for (int t = 0; t < numThreads; t++) threads[t].Join(5000);

                for (int t = 0; t < numThreads; t++)
                {
                    Assert(results[t], "Thread " + t + " atomic save must succeed without collision (Err: " + (exceptions[t] != null ? exceptions[t].Message : "none") + ")");
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
                }
                catch { }
            }
        }

        private static void Test_V5_05_BackupRotationFidelity_MultiGeneration()
        {
            string testDir = Path.Combine(Path.GetTempPath(), "challenger_rotation_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);
            string targetFile = Path.Combine(testDir, "rotation.pb");
            string backupFile = targetFile + ".bak";

            try
            {
                byte[] gen1 = new byte[] { 1, 1, 1 };
                byte[] gen2 = new byte[] { 2, 2, 2, 2 };
                byte[] gen3 = new byte[] { 3, 3, 3, 3, 3 };

                // Generation 1 write
                IncrementalProtoShardMerger.AtomicSaveWithBackup(targetFile, gen1);
                Assert(File.Exists(targetFile), "Gen1 target exists");
                Assert(ByteArraysEqual(gen1, File.ReadAllBytes(targetFile)), "Gen1 content");

                // Generation 2 write
                IncrementalProtoShardMerger.AtomicSaveWithBackup(targetFile, gen2);
                Assert(ByteArraysEqual(gen2, File.ReadAllBytes(targetFile)), "Gen2 target content");
                Assert(File.Exists(backupFile), "Gen1 .bak exists");
                Assert(ByteArraysEqual(gen1, File.ReadAllBytes(backupFile)), "Gen1 .bak content");

                // Generation 3 write
                IncrementalProtoShardMerger.AtomicSaveWithBackup(targetFile, gen3);
                Assert(ByteArraysEqual(gen3, File.ReadAllBytes(targetFile)), "Gen3 target content");
                Assert(File.Exists(backupFile), "Gen2 .bak exists");
                Assert(ByteArraysEqual(gen2, File.ReadAllBytes(backupFile)), "Gen2 .bak content");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
                }
                catch { }
            }
        }

        #endregion

        #region Helper Mock Builders

        private static ProtoNode CreateMockSessionProto(string uuid, string displayTitle, string folderUri, DateTime updateTime)
        {
            ProtoNode entryNode = new ProtoNode(1, 2);
            entryNode.Children = new List<ProtoNode>();

            ProtoNode f1 = new ProtoNode(1, 2);
            f1.SetString(uuid);
            entryNode.Children.Add(f1);

            ProtoNode meta = new ProtoNode(2, 2);
            meta.Children = new List<ProtoNode>();

            ProtoNode autoTitle = new ProtoNode(1, 2);
            autoTitle.SetString(displayTitle);
            meta.Children.Add(autoTitle);

            ProtoNode stepCount = new ProtoNode(2, 0);
            stepCount.SetInt32(10);
            meta.Children.Add(stepCount);

            ProtoNode timeNode = new ProtoNode(3, 0);
            long epochSeconds = (long)(updateTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            timeNode.VarintValue = (ulong)epochSeconds;
            meta.Children.Add(timeNode);

            ProtoNode wsNode = new ProtoNode(9, 2);
            wsNode.Children = new List<ProtoNode> { new ProtoNode(1, 2) };
            wsNode.Children[0].SetString(folderUri);
            meta.Children.Add(wsNode);

            entryNode.Children.Add(meta);
            return entryNode;
        }

        private static ProtoNode CreateMockSessionProtoWithProj(string uuid, string displayTitle, string projectName)
        {
            ProtoNode entry = CreateMockSessionProto(uuid, displayTitle, "C:\\" + projectName, DateTime.UtcNow);
            ProtoNode meta = entry.GetChild(2);
            if (meta != null)
            {
                ProtoNode cascade = new ProtoNode(17, 2);
                cascade.Children = new List<ProtoNode>
                {
                    new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(projectName) }
                };
                meta.Children.Add(cascade);
            }
            return entry;
        }

        #endregion
    }
}
