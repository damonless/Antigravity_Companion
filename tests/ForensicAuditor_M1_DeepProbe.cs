using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using AntigravityCompanion;

namespace AntigravityCompanion.ForensicAudit
{
    class Program
    {
        static int _passCount = 0;
        static int _failCount = 0;

        static void Assert(bool condition, string checkName, string details)
        {
            if (condition)
            {
                _passCount++;
                Console.WriteLine( [PASS]  + checkName +  --  + details);
            }
            else
            {
                _failCount++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine( [FAIL]  + checkName +  --  + details);
                Console.ResetColor();
            }
        }

        static int Main(string[] args)
        {
            Console.WriteLine(===================================================================);
            Console.WriteLine( FORENSIC INTEGRITY AUDIT: MILESTONE 1 (DEEP PROBE));
            Console.WriteLine(===================================================================);

            // 1. Varint Wire-Format Boundary Probing
            TestVarintBoundaries();

            // 2. Wire-Type Precision & Roundtrip
            TestWireTypesRoundtrip();

            // 3. Dynamic Deep Nested AST Reconstruction
            TestDeepAstReconstruction();

            // 4. Custom Title Priority (Field 15 vs Field 1)
            TestCustomTitlePriority();

            // 5. Streaming Zip Inspection on Real Archive
            TestRealArchiveStreaming();

            // 6. Shard Merger Deduplication & Splicing
            TestShardMergerFidelity();

            // 7. Path Remapping in Proto AST
            TestPathRemapperFidelity();

            Console.WriteLine(===================================================================);
            Console.WriteLine(string.Format( AUDIT RESULT: {0} Passed, {1} Failed, _passCount, _failCount));
            Console.WriteLine(===================================================================);

            return _failCount == 0 ? 0 : 1;
        }

        static void TestVarintBoundaries()
        {
            ulong[] testVals = new ulong[] {
                0UL, 1UL, 127UL, 128UL, 255UL, 300UL, 16383UL, 16384UL,
                0x7FFFFFFFUL, 0xFFFFFFFFUL, 0x7FFFFFFFFFFFFFFFUL, 0xFFFFFFFFFFFFFFFFUL
            };

            bool allMatch = true;
            foreach (ulong val in testVals)
            {
                byte[] encoded = ProtoCodec.EncodeVarint(val);
                ulong decoded;
                int bytesRead = ProtoCodec.ReadVarint(encoded, 0, encoded.Length, out decoded);
                if (bytesRead != encoded.Length || decoded != val)
                {
                    allMatch = false;
                    break;
                }
            }
            Assert(allMatch, AUDIT-01: Varint Boundary Fidelity, 12 extreme boundary values encoded & decoded with 100% bit-exact accuracy);

            // Malformed varint check (all 0x80 bytes)
            byte[] infinite = new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80 };
            ulong dummy;
            int read = ProtoCodec.ReadVarint(infinite, 0, infinite.Length, out dummy);
            Assert(read == -1, AUDIT-02: Infinite Varint Protection, Buffer overrun guarded: returns -1 on unclosed varint stream);
        }

        static void TestWireTypesRoundtrip()
        {
            List<ProtoNode> nodes = new List<ProtoNode>();
            
            // Varint (Wire 0)
            ProtoNode n0 = new ProtoNode(1, 0);
            n0.VarintValue = 1234567890123456789UL;
            nodes.Add(n0);

            // Fixed64 (Wire 1)
            ProtoNode n1 = new ProtoNode(2, 1);
            n1.Fixed64Value = 0xFEDCBA9876543210UL;
            nodes.Add(n1);

            // Length-Delimited (Wire 2) - String with Chinese & Emoji
            ProtoNode n2 = new ProtoNode(3, 2);
            n2.SetString(Forensic Audit 审计测试 🚀✨ 2026);
            nodes.Add(n2);

            // Fixed32 (Wire 5)
            ProtoNode n5 = new ProtoNode(4, 5);
            n5.Fixed32Value = 0x89ABCDEF;
            nodes.Add(n5);

            byte[] wireBytes = ProtoCodec.Encode(nodes);
            List<ProtoNode> decoded = ProtoCodec.Decode(wireBytes);

            bool match = decoded.Count == 4 &&
                         decoded[0].VarintValue == 1234567890123456789UL &&
                         decoded[1].Fixed64Value == 0xFEDCBA9876543210UL &&
                         decoded[2].GetString() == Forensic Audit 审计测试 🚀✨ 2026 &&
                         decoded[3].Fixed32Value == 0x89ABCDEF;

            Assert(match, AUDIT-03: Multi-WireType Codec Fidelity, Wire types 0, 1, 2, 5 encoded & decoded with zero loss);
        }

        static void TestDeepAstReconstruction()
        {
            // Build 5-level nested message
            ProtoNode root = new ProtoNode(1, 2);
            ProtoNode level2 = new ProtoNode(2, 2);
            ProtoNode level3 = new ProtoNode(3, 2);
            ProtoNode level4 = new ProtoNode(4, 2);
            ProtoNode leaf = new ProtoNode(5, 2);
            leaf.SetString(Deepest Leaf Value);

            level4.Children = new List<ProtoNode> { leaf };
            level3.Children = new List<ProtoNode> { level4 };
            level2.Children = new List<ProtoNode> { level3 };
            root.Children = new List<ProtoNode> { level2 };

            byte[] serialized = ProtoCodec.Encode(new List<ProtoNode> { root });
            List<ProtoNode> decoded = ProtoCodec.Decode(serialized);

            bool pass = decoded.Count == 1 &&
                        decoded[0].GetChild(2) != null &&
                        decoded[0].GetChild(2).GetChild(3) != null &&
                        decoded[0].GetChild(2).GetChild(3).GetChild(4) != null &&
                        decoded[0].GetChild(2).GetChild(3).GetChild(4).GetChild(5) != null &&
                        decoded[0].GetChild(2).GetChild(3).GetChild(4).GetChild(5).GetString() == Deepest Leaf Value;

            Assert(pass, AUDIT-04: Deep Recursive AST Reconstruction, 5-level deep submessage reconstructed faithfully);
        }

        static void TestCustomTitlePriority()
        {
            // Construct a real-world agyhub entry with both Field 1 (English auto-summary) and Field 15 (Chinese custom title)
            ProtoNode entry = new ProtoNode(1, 2);
            entry.Children = new List<ProtoNode>();

            ProtoNode f1 = new ProtoNode(1, 2);
            f1.SetString(test-convo-uuid-999);
            entry.Children.Add(f1);

            ProtoNode f2 = new ProtoNode(2, 2);
            f2.Children = new List<ProtoNode>();

            // Field 1 Auto Summary
            ProtoNode autoSum = new ProtoNode(1, 2);
            autoSum.SetString(Default English Summary That Should Be Ignored);
            f2.Children.Add(autoSum);

            // Field 15 Custom Title
            ProtoNode f15 = new ProtoNode(15, 2);
            f15.Children = new List<ProtoNode>();
            ProtoNode customTitle = new ProtoNode(1, 2);
            customTitle.SetString(【最终重命名】BMS上位机核心通信协议重构);
            f15.Children.Add(customTitle);
            f2.Children.Add(f15);

            entry.Children.Add(f2);

            ConversationSummaryInfo info = SessionMetadataExtractor.ParseEntryNode(entry);

            bool pass = info.HasCustomTitle &&
                        info.DisplayTitle == 【最终重命名】BMS上位机核心通信协议重构 &&
                        info.AutoSummaryTitle == Default English Summary That Should Be Ignored &&
                        info.CustomTitle == 【最终重命名】BMS上位机核心通信协议重构;

            Assert(pass, AUDIT-05: Field 15 Custom Title 100% Priority, Custom title overrides English auto summary without fallback);
        }

        static void TestRealArchiveStreaming()
        {
            string backupPath = antigravity_backup_20260821.agy;
            if (!File.Exists(backupPath))
            {
                Assert(false, AUDIT-06: Real Backup File Check, antigravity_backup_20260821.agy not found);
                return;
            }

            Stopwatch sw = Stopwatch.StartNew();
            BackupArchiveIndex index = StreamingZipIndexReader.ReadIndex(backupPath);
            sw.Stop();

            bool pass = index != null &&
                        index.Conversations.Count > 0 &&
                        index.Projects.Count > 0 &&
                        sw.ElapsedMilliseconds < 500;

            Assert(pass, AUDIT-06: In-Memory Streaming Zip Reader on 336MB Archive,
                string.Format(Parsed {0} conversations, {1} projects in {2}ms (RAM-only stream),
                index.Conversations.Count, index.Projects.Count, sw.ElapsedMilliseconds));
        }

        static void TestShardMergerFidelity()
        {
            // Build two independent shards
            List<IncrementalProtoShardMerger.ShardItem> shards = new List<IncrementalProtoShardMerger.ShardItem>();
            
            ProtoNode n1 = new ProtoNode(1, 2);
            n1.Children = new List<ProtoNode>();
            ProtoNode id1 = new ProtoNode(1, 2); id1.SetString(convo-uuid-A); n1.Children.Add(id1);
            ProtoNode title1 = new ProtoNode(2, 2); title1.SetString(Session A); n1.Children.Add(title1);

            ProtoNode n2 = new ProtoNode(1, 2);
            n2.Children = new List<ProtoNode>();
            ProtoNode id2 = new ProtoNode(1, 2); id2.SetString(convo-uuid-B); n2.Children.Add(id2);
            ProtoNode title2 = new ProtoNode(2, 2); title2.SetString(Session B); n2.Children.Add(title2);

            byte[] b1 = ProtoCodec.Encode(new List<ProtoNode> { n1 });
            byte[] b2 = ProtoCodec.Encode(new List<ProtoNode> { n2 });

            shards.Add(new IncrementalProtoShardMerger.ShardItem { ConversationId = convo-uuid-A, RawBytes = b1, AstNode = n1 });
            shards.Add(new IncrementalProtoShardMerger.ShardItem { ConversationId = convo-uuid-B, RawBytes = b2, AstNode = n2 });

            byte[] merged = IncrementalProtoShardMerger.MergeShards(null, shards);
            List<IncrementalProtoShardMerger.ShardItem> extracted = IncrementalProtoShardMerger.ExtractShards(merged);

            bool pass = extracted.Count == 2 &&
                        extracted[0].ConversationId == convo-uuid-A &&
                        extracted[1].ConversationId == convo-uuid-B;

            Assert(pass, AUDIT-07: Shard Splicing & Extraction, Merged 2 shards into composite proto and re-extracted independently);

            // Test collision / idempotency
            ProtoNode n1_updated = new ProtoNode(1, 2);
            n1_updated.Children = new List<ProtoNode>();
            ProtoNode id1_up = new ProtoNode(1, 2); id1_up.SetString(convo-uuid-A); n1_updated.Children.Add(id1_up);
            ProtoNode title1_up = new ProtoNode(2, 2); title1_up.SetString(Session A Updated); n1_updated.Children.Add(title1_up);
            byte[] b1_up = ProtoCodec.Encode(new List<ProtoNode> { n1_updated });

            List<IncrementalProtoShardMerger.ShardItem> updateShards = new List<IncrementalProtoShardMerger.ShardItem> {
                new IncrementalProtoShardMerger.ShardItem { ConversationId = convo-uuid-A, RawBytes = b1_up, AstNode = n1_updated }
            };

            byte[] merged2 = IncrementalProtoShardMerger.MergeShards(merged, updateShards);
            List<IncrementalProtoShardMerger.ShardItem> extracted2 = IncrementalProtoShardMerger.ExtractShards(merged2);

            bool passIdempotent = extracted2.Count == 2 &&
                                 extracted2[0].ConversationId == convo-uuid-A &&
                                 extracted2[0].AstNode.GetChild(2).GetString() == Session A Updated;

            Assert(passIdempotent, AUDIT-08: Shard Merger Collision & Idempotency, Updated existing UUID without duplicating or corrupting adjacent entries);
        }

        static void TestPathRemapperFidelity()
        {
            ProtoNode node = new ProtoNode(1, 2);
            node.Children = new List<ProtoNode>();
            ProtoNode uriNode = new ProtoNode(9, 2);
            uriNode.SetString(file:///c:/Users/OldUser/ProjectAlpha);
            node.Children.Add(uriNode);

            WorkspacePathRemapper.RemapPathInShard(node, c:\\Users\\OldUser\\ProjectAlpha, d:\\NewWork\\ProjectAlpha);

            string newUri = node.GetChild(9).GetString();
            bool pass = newUri.Contains(d:/NewWork/ProjectAlpha) || newUri.Contains(d:\\NewWork\\ProjectAlpha) || newUri.Contains(file:///d:/NewWork/ProjectAlpha);

            Assert(pass, AUDIT-09: Workspace Path Remapping in Proto AST, Remapped OldUser URI to NewWork URI inside ProtoNode AST);
        }
    }
}
