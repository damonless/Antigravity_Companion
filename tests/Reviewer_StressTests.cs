using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using AntigravityCompanion;

namespace AntigravityCompanion.StressTests
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("===================================================================");
            Console.WriteLine("  Adversarial Stress Test Suite for Milestone 1 (Reviewer 1)");
            Console.WriteLine("===================================================================");

            int passed = 0;
            int failed = 0;

            Action<string, Action> runStress = delegate(string name, Action testAction)
            {
                try
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    testAction();
                    sw.Stop();
                    Console.WriteLine(string.Format("  [PASS] {0} ({1:F2}ms)", name.PadRight(60), sw.Elapsed.TotalMilliseconds));
                    passed++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("  [FAIL] {0}", name));
                    Console.WriteLine("         Error: " + ex.Message);
                    Console.WriteLine(ex.StackTrace);
                    failed++;
                }
            };

            // 1. Corrupted Varint: 10 bytes of 0xFF (infinite varint attack)
            runStress("ADV-01: Malformed Infinite Varint Overrun", delegate()
            {
                byte[] malformed = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
                ulong val;
                int read = ProtoCodec.ReadVarint(malformed, 0, malformed.Length, out val);
                if (read != -1) throw new Exception("Expected -1 for 10-byte invalid varint, got " + read);

                List<ProtoNode> nodes = ProtoCodec.Decode(malformed);
                // Must not throw IndexOutOfRangeException or hang in infinite loop
            });

            // 2. Truncated Length-Delimited Payload
            runStress("ADV-02: Truncated Length-Delimited Payload", delegate()
            {
                // Tag: Field 1, WireType 2 (0x0A), Length: 1000, but only 4 bytes follow
                byte[] truncated = new byte[] { 0x0A, 0xE8, 0x07, 0x01, 0x02, 0x03, 0x04 };
                List<ProtoNode> nodes = ProtoCodec.Decode(truncated);
                if (nodes.Count != 1) throw new Exception("Expected 1 truncated node, got " + nodes.Count);
                if (nodes[0].RawBytes.Length != 4) throw new Exception("Expected 4 bytes clamped, got " + nodes[0].RawBytes.Length);
            });

            // 3. Zero-Byte and Null Inputs
            runStress("ADV-03: Zero-Byte & Null Input Resilience", delegate()
            {
                if (ProtoCodec.Decode(null).Count != 0) throw new Exception("Null decode failed");
                if (ProtoCodec.Decode(new byte[0]).Count != 0) throw new Exception("Empty decode failed");
                if (ProtoCodec.Encode(null).Length != 0) throw new Exception("Null encode failed");
                if (ProtoCodec.Encode(new List<ProtoNode>()).Length != 0) throw new Exception("Empty encode failed");
                
                var summaries = SessionMetadataExtractor.ExtractSummaries(null, null);
                if (summaries.Count != 0) throw new Exception("Null extract summaries failed");

                var shards = IncrementalProtoShardMerger.ExtractShards(null);
                if (shards.Count != 0) throw new Exception("Null extract shards failed");

                byte[] merged = IncrementalProtoShardMerger.MergeShards(null, null);
                if (merged.Length != 0) throw new Exception("Null merge shards failed");
            });

            // 4. 50-Level Deep Nested Submessages
            runStress("ADV-04: 50-Level Deep Nested Submessages", delegate()
            {
                ProtoNode root = new ProtoNode(1, 2);
                ProtoNode current = root;
                for (int i = 2; i <= 50; i++)
                {
                    ProtoNode next = new ProtoNode(1, 2);
                    current.Children = new List<ProtoNode> { next };
                    current = next;
                }
                current.SetString("Deepest Core Value 2026");

                byte[] encoded = ProtoCodec.Encode(new List<ProtoNode> { root });
                List<ProtoNode> decoded = ProtoCodec.Decode(encoded);
                if (decoded.Count != 1) throw new Exception("Expected 1 root node");
                
                ProtoNode walk = decoded[0];
                int depth = 1;
                while (walk.HasChildren)
                {
                    walk = walk.Children[0];
                    depth++;
                }
                if (depth != 50) throw new Exception("Expected depth 50, got " + depth);
                if (walk.GetString() != "Deepest Core Value 2026") throw new Exception("Value mismatch: " + walk.GetString());
            });

            // 5. UTF-8 Chinese, Japanese, Korean, Arabic, Emoji & RTL Strings
            runStress("ADV-05: Extreme Multilingual & RTL / Emoji Strings", delegate()
            {
                string extremeText = "【储能BMS】⚡ 超高压并网 2026 🚀 🌟 日本語テスト 한국어 اختبار اللغة العربية \u202Ereversed\u202C";
                ProtoNode node = new ProtoNode(15, 2);
                node.SetString(extremeText);

                byte[] encoded = ProtoCodec.Encode(new List<ProtoNode> { node });
                List<ProtoNode> decoded = ProtoCodec.Decode(encoded);
                if (decoded[0].GetString() != extremeText) throw new Exception("String roundtrip mismatch");
            });

            // 6. Path Remapping with All Path Formats & Case Variations
            runStress("ADV-06: Comprehensive Path Remapper Variant Matrix", delegate()
            {
                string oldRoot = @"C:\Users\Admin\Desktop\Tools";
                string newRoot = @"D:\Antigravity_Workspaces\NewFolder";

                var pairs = WorkspacePathRemapper.BuildReplacementPairs(oldRoot, newRoot);
                if (pairs.Count < 5) throw new Exception("Expected at least 5 variant pairs, got " + pairs.Count);

                string testUri = "file:///c%3A/Users/Admin/Desktop/Tools/project1";
                string remapped = WorkspacePathRemapper.RemapPath(testUri, oldRoot, newRoot);
                if (!remapped.Contains("D%3A") && !remapped.Contains("d%3A") && !remapped.Contains("d:/") && !remapped.Contains("D:/"))
                {
                    throw new Exception("Remap failed for encoded uri: " + remapped);
                }
            });

            // 7. Shard Merge Collision & Idempotency
            runStress("ADV-07: Shard Merger Collision & Idempotency", delegate()
            {
                // Create 3 shards
                ProtoNode e1 = new ProtoNode(1, 2);
                ProtoNode id1 = new ProtoNode(1, 2); id1.SetString("UUID-001");
                ProtoNode meta1 = new ProtoNode(2, 2);
                ProtoNode t1 = new ProtoNode(1, 2); t1.SetString("Auto Title 1");
                meta1.Children = new List<ProtoNode> { t1 };
                e1.Children = new List<ProtoNode> { id1, meta1 };

                byte[] pb1 = ProtoCodec.Encode(new List<ProtoNode> { e1 });
                var shards1 = IncrementalProtoShardMerger.ExtractShards(pb1);

                // Merge into empty
                byte[] merged1 = IncrementalProtoShardMerger.MergeShards(null, shards1);
                var summaries1 = SessionMetadataExtractor.ExtractSummariesFromProto(merged1);
                if (summaries1.Count != 1) throw new Exception("Expected 1 summary");

                // Merge same shard again (should overwrite/deduplicate, not duplicate)
                byte[] merged2 = IncrementalProtoShardMerger.MergeShards(merged1, shards1);
                var summaries2 = SessionMetadataExtractor.ExtractSummariesFromProto(merged2);
                if (summaries2.Count != 1) throw new Exception("Expected deduplication, got " + summaries2.Count);

                // Update title and merge (should update)
                t1.SetString("Updated Auto Title 1");
                byte[] pb1Updated = ProtoCodec.Encode(new List<ProtoNode> { e1 });
                var shardsUpdated = IncrementalProtoShardMerger.ExtractShards(pb1Updated);

                byte[] merged3 = IncrementalProtoShardMerger.MergeShards(merged2, shardsUpdated);
                var summaries3 = SessionMetadataExtractor.ExtractSummariesFromProto(merged3);
                if (summaries3.Count != 1) throw new Exception("Expected 1 summary");
                if (summaries3[0].DisplayTitle != "Updated Auto Title 1") throw new Exception("Title not updated: " + summaries3[0].DisplayTitle);
            });

            // 8. 1000 Shards High-Volume Scale Test
            runStress("ADV-08: 1000 Shards Merge Performance", delegate()
            {
                List<IncrementalProtoShardMerger.ShardItem> largeShards = new List<IncrementalProtoShardMerger.ShardItem>();
                for (int i = 0; i < 1000; i++)
                {
                    ProtoNode entry = new ProtoNode(1, 2);
                    ProtoNode id = new ProtoNode(1, 2); id.SetString("UUID-" + i.ToString("D4"));
                    ProtoNode meta = new ProtoNode(2, 2);
                    ProtoNode t = new ProtoNode(1, 2); t.SetString("Session Title " + i);
                    meta.Children = new List<ProtoNode> { t };
                    entry.Children = new List<ProtoNode> { id, meta };
                    byte[] raw = ProtoCodec.Encode(new List<ProtoNode> { entry });

                    largeShards.Add(new IncrementalProtoShardMerger.ShardItem
                    {
                        ConversationId = "UUID-" + i.ToString("D4"),
                        RawBytes = raw,
                        AstNode = entry
                    });
                }

                Stopwatch sw = Stopwatch.StartNew();
                byte[] merged = IncrementalProtoShardMerger.MergeShards(null, largeShards);
                sw.Stop();

                var summaries = SessionMetadataExtractor.ExtractSummariesFromProto(merged);
                if (summaries.Count != 1000) throw new Exception("Expected 1000 summaries, got " + summaries.Count);
                if (sw.ElapsedMilliseconds > 1000) throw new Exception("Merge took too long: " + sw.ElapsedMilliseconds + "ms");
            });

            Console.WriteLine("===================================================================");
            Console.WriteLine(string.Format("  Stress Results: {0} Passed, {1} Failed", passed, failed));
            Console.WriteLine("===================================================================");

            return failed == 0 ? 0 : 1;
        }
    }
}
