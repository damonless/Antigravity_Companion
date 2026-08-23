using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using AntigravityCompanion;

namespace AntigravityCompanion.Tests
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("===================================================================");
            Console.WriteLine("  Antigravity Companion M1 Data & Storage Engine Verification");
            Console.WriteLine("===================================================================");

            int passed = 0;
            int failed = 0;

            Action<string, Action> runTest = delegate(string name, Action testAction)
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

            runTest("TV-01%704: Varint Encoding/Decoding Boundaries", delegate()
            {
                ulong[] testValues = new ulong[]
                {
                    0, 1, 127, 128, 300, 16383, 16384,
                    uint.MaxValue, (ulong)uint.MaxValue + 1,
                    ulong.MaxValue - 1, ulong.MaxValue
                };

                for (int i = 0; i < testValues.Length; i++)
                {
                    ulong v = testValues[i];
                    byte[] encoded = ProtoCodec.EncodeVarint(v);
                    ulong decoded;
                    int readBytes = ProtoCodec.ReadVarint(encoded, 0, encoded.Length, out decoded);
                    if (readBytes <= 0 || decoded != v)
                    {
                        throw new Exception(string.Format("Varint mismatch for {0}: got {1}, bytes read {2}", v, decoded, readBytes));
                    }
                }
            });


            runTest("TV-05~06: Fixed32 and Fixed64 Precision", delegate()
            {
                ulong f64Val = 0x0123456789ABCDEFUL;
                uint f32Val = 0xDEADBEEFU;

                ProtoNode n64 = new ProtoNode { FieldNumber = 1, WireType = 1, Fixed64Value = f64Val };
                ProtoNode n32 = new ProtoNode { FieldNumber = 2, WireType = 5, Fixed32Value = f32Val };

                byte[] encoded = ProtoCodec.Encode(new List<ProtoNode> { n64, n32 });
                List<ProtoNode> decoded = ProtoCodec.Decode(encoded);

                if (decoded.Count != 2) throw new Exception("Expected 2 nodes, got " + decoded.Count);
                if (decoded[0].Fixed64Value != f64Val) throw new Exception("Fixed64 mismatch");
                if (decoded[1].Fixed32Value != f32Val) throw new Exception("Fixed32 mismatch");
            });


            runTest("TV-07: Multibyte UTF-8 Chinese and Emoji Strings", delegate()
            {
                string text = "【晸阳能源】电池充电策略优化�2026 🙀 🌝 ";
                ProtoNode node = new ProtoNode { FieldNumber = 3, WireType = 2 };
                node.SetString(text);

                byte[] encoded = ProtoCodec.Encode(new List<ProtoNode> { node });
                List<ProtoNode> decoded = ProtoCodec.Decode(encoded);

                if (decoded.Count != 1) throw new Exception("Expected 1 node");
                if (decoded[0].GetString() != text) throw new Exception("String mismatch: got " + decoded[0].GetString());
            });


            runTest("TV-09: 4-Level Nested Submessage AST Roundtrip", delegate()
            {
                ProtoNode root = new ProtoNode { FieldNumber = 1, WireType = 2 };
                ProtoNode child1 = new ProtoNode { FieldNumber = 1, WireType = 2 };
                child1.SetString("Level 1 String");

                ProtoNode child2 = new ProtoNode { FieldNumber = 2, WireType = 2 };
                ProtoNode subChild = new ProtoNode { FieldNumber = 15, WireType = 2 };
                ProtoNode titleNode = new ProtoNode { FieldNumber = 1, WireType = 2 };
                titleNode.SetString("深度嵌奧裊示稺竩大程");
                subChild.Children = new List<ProtoNode> { titleNode };
                child2.Children = new List<ProtoNode> { subChild };

                root.Children = new List<ProtoNode> { child1, child2 };

                byte[] bytes1 = ProtoCodec.Encode(new List<ProtoNode> { root });
                List<ProtoNode> decoded = ProtoCodec.Decode(bytes1);
                byte[] bytes2 = ProtoCodec.Encode(decoded);

                if (bytes1.Length != bytes2.Length) throw new Exception("Length mismatch in roundtrip");
                for (int i = 0; i < bytes1.Length; i++)
                {
                    if (bytes1[i] != bytes2[i]) throw new Exception("Byte mismatch at offset " + i);
                }
            });


            string realZipPath = "antigravity_backup_20260821.agy";
            byte[] realPbBytes = null;

            if (File.Exists(realZipPath))
            {
                runTest("TV-10A: StreamingZipIndexReader on Real 336MB .agy Archive", delegate()
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    BackupArchiveIndex index = StreamingZipIndexReader.ReadIndex(realZipPath);
                    sw.Stop();

                    Console.WriteLine(string.Format("         Extracted {0} conversations, {1} projects in {2:F2}ms (RAM only)",
                        index.Conversations.Count, index.Projects.Count, sw.Elapsed.TotalMilliseconds));

                    if (index.Conversations.Count == 0) throw new Exception("No conversations extracted");
                    if (index.Projects.Count == 0) throw new Exception("No projects extracted");

                    int customTitleCount = 0;
                    for (int i = 0; i < index.Conversations.Count; i++)
                    {
                        var c = index.Conversations[i];
                        if (c.HasCustomTitle)
                        {
                            customTitleCount++;
                            if (string.IsNullOrEmpty(c.DisplayTitle)) throw new Exception("DisplayTitle is empty for custom session: " + c.ConversationId);
                        }
                    }
                    Console.WriteLine(string.Format("         Found {0} custom renamed Chinese/user titles", customTitleCount));
                    if (customTitleCount == 0) throw new Exception("Expected at least 1 custom title in real backup archive");
                });


                runTest("TV-10B: 100% Byte-for-Byte Exact Roundtrip on Real Protobuf Stream", delegate()
                {
                    ZipMetadataPackage pkg = StreamingZipIndexReader.ReadZipMetadata(realZipPath);
                    realPbBytes = pkg.SummaryProtoBytes;

                    if (realPbBytes == null || realPbBytes.Length == 0) throw new Exception("SummaryProtoBytes is null");

                    Console.WriteLine(string.Format("         Original real .pb size: {0:N0} bytes", realPbBytes.Length));


                    List<ProtoNode> ast = ProtoCodec.Decode(realPbBytes);
                    byte[] reserialized = ProtoCodec.Encode(ast);

                    Console.WriteLine(string.Format("         Reserialized .pb size:  {0:N0} bytes", reserialized.Length));


                    if (realPbBytes.Length != reserialized.Length)
                    {
                        throw new Exception(string.Format("Size mismatch: original {0} vs reserialized {1}", realPbBytes.Length, reserialized.Length));
                    }

                    for (int i = 0; i < realPbBytes.Length; i++)
                    {
                        if (realPbBytes[i] != reserialized[i])
                        {
                            throw new Exception(string.Format("Byte difference at offset {0}: orig {1:X2} vs re-enc {2:X2}", i, realPbBytes[i], reserialized[i]));
                        }
                    }
                    Console.WriteLine("         [100% BYTE-FOR-BYTE IDENTICAL VERIFIED!]");
                });


                runTest("TV-11: IncrementalProtoShardMerger and WorkspacePathRemapper", delegate()
                {
                    List<IncrementalProtoShardMerger.ShardItem> shards = IncrementalProtoShardMerger.ExtractShards(realPbBytes);
                    if (shards.Count < 2) throw new Exception("Expected at least 2 shards");

                    Console.WriteLine(string.Format("         Extracted {0} independent binary shards", shards.Count));


                    var shard0 = shards[0];
                    string oldPath = @"C:\Users\Administrator\Desktop\Tools\";
                    string newPath = @"D:\Dev\Workspaces\Antigravity\";

                    List<IncrementalProtoShardMerger.PathRemapRule> rules = new List<IncrementalProtoShardMerger.PathRemapRule>
                    {
                        new IncrementalProtoShardMerger.PathRemapRule { OldPath = oldPath, NewPath = newPath }
                    };

                    byte[] merged = IncrementalProtoShardMerger.MergeShards(new byte[0], new List<IncrementalProtoShardMerger.ShardItem> { shard0 }, rules);
                    List<ConversationSummaryInfo> mergedSummaries = SessionMetadataExtractor.ExtractSummariesFromProto(merged);


                    if (mergedSummaries.Count != 1) throw new Exception("Expected 1 merged summary, got " + mergedSummaries.Count);
                    Console.WriteLine("         Merged Shard Title: " + mergedSummaries[0].DisplayTitle);
                });
            }

            Console.WriteLine("===================================================================");
            Console.WriteLine(string.Format("  Total Tests: {0}, Passed; {1}, Failed: {2}", passed + failed, passed, failed));
            Console.WriteLine("==================================================================");

            return failed == 0 ? 0 : 1;
        }
    }
}
