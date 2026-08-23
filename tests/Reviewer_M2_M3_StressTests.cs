using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using AntigravityCompanion;

namespace AntigravityCompanion.Tests
{
    class Reviewer_M2_M3_StressTests
    {
        static int Main(string[] args)
        {
            Console.WriteLine("===============================================================================");
            Console.WriteLine("  REVIEWER ADVERSARIAL STRESS TEST SUITE — MILESTONE 2 & MILESTONE 3");
            Console.WriteLine("  Dual-Tree Visual Diff, Smart Filter, Conflict Wizard & Selective Import UI");
            Console.WriteLine("===============================================================================");

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
                    Console.WriteLine("         Exception: " + ex.GetType().Name + ": " + ex.Message);
                    Console.WriteLine("         Stack: " + ex.StackTrace);
                    failed++;
                }
            };

            // ─────────────────────────────────────────────────────────────────
            // 1. SMART PROJECT FILTER ADVERSARIAL CASES
            // ─────────────────────────────────────────────────────────────────
            runTest("M2_ADV_01_SmartFilter_JunkDetectionBoundaries", delegate()
            {
                // Null and Empty
                if (!SmartProjectFilter.IsJunkOrEphemeral(null)) throw new Exception("Null should be junk");
                if (!SmartProjectFilter.IsJunkOrEphemeral("")) throw new Exception("Empty should be junk");

                // Standard Junk Prefixes
                string[] junkNames = new string[] {
                    "subagent-review-123", "SUBAGENT_TASK_456", "temp-run", "TEMP_STORAGE",
                    "recovered project", "Recovered Project 2026", "recovered workspace",
                    "outside-of-project", "OUTSIDE_OF_PROJECT", "default-cli-project", "DEFAULT_CLI_PROJECT"
                };
                foreach (string j in junkNames)
                {
                    if (!SmartProjectFilter.IsJunkOrEphemeral(j))
                        throw new Exception("Expected junk for: " + j);
                    if (!SmartProjectFilter.IsJunkOrEphemeral("  " + j + "  "))
                        throw new Exception("Expected junk for whitespace-padded: " + j);
                }

                // Legitimate names with junk substrings (MUST NOT BE JUNK)
                string[] legitNames = new string[] {
                    "my-subagent-core", "project_template", "temperature_monitor",
                    "recovery_service", "outside_box_thinking", "cli-project-manager"
                };
                foreach (string leg in legitNames)
                {
                    if (SmartProjectFilter.IsJunkOrEphemeral(leg))
                        throw new Exception("False positive junk detection for: " + leg);
                }

                // URI Junk detection
                if (!SmartProjectFilter.IsTemporaryOrJunkProject("ValidProject", "file:///C:/Users/Admin/outside-of-project/foo"))
                    throw new Exception("Expected junk from URI");
                if (!SmartProjectFilter.IsTemporaryOrJunkProject("ValidProject", "file:///C:/Users/Admin/.system_generated/bar"))
                    throw new Exception("Expected junk from system_generated URI");
                if (SmartProjectFilter.IsTemporaryOrJunkProject("ValidProject", "file:///C:/Users/Admin/Desktop/Tools/Antigravity"))
                    throw new Exception("False positive from clean URI");
            });

            // ─────────────────────────────────────────────────────────────────
            // 2. VISUAL DIFF ENGINE 3-WAY MATRIX & PATH NORMALIZATION
            // ─────────────────────────────────────────────────────────────────
            runTest("M2_ADV_02_VisualDiffEngine_BidirectionalDiffMatrix", delegate()
            {
                List<ConversationSummaryInfo> localList = new List<ConversationSummaryInfo>();
                List<ConversationSummaryInfo> backupList = new List<ConversationSummaryInfo>();

                // 1. Aligned Project (Same name, equivalent URI with slash and case diff)
                localList.Add(new ConversationSummaryInfo {
                    ConversationId = "LOC-001",
                    ProjectName = "AlphaProject",
                    FolderUri = "file:///C:/Projects/Alpha",
                    DisplayTitle = "Local Session 1"
                });
                backupList.Add(new ConversationSummaryInfo {
                    ConversationId = "BKP-001",
                    ProjectName = "alphaproject", // Case diff
                    FolderUri = "file:///c:\\projects\\alpha\\", // Slashes & trailing slash diff
                    DisplayTitle = "Backup Session 1"
                });

                // 2. Conflicted Project (Same name, different URI)
                localList.Add(new ConversationSummaryInfo {
                    ConversationId = "LOC-002",
                    ProjectName = "BetaProject",
                    FolderUri = "file:///C:/Work/Beta",
                    DisplayTitle = "Beta Local"
                });
                backupList.Add(new ConversationSummaryInfo {
                    ConversationId = "BKP-002",
                    ProjectName = "BetaProject",
                    FolderUri = "file:///D:/Migration/Beta",
                    DisplayTitle = "Beta Backup"
                });

                // 3. New in Backup
                backupList.Add(new ConversationSummaryInfo {
                    ConversationId = "BKP-003",
                    ProjectName = "GammaNewProject",
                    FolderUri = "file:///E:/Gamma",
                    DisplayTitle = "Gamma Backup"
                });

                // 4. Local Only
                localList.Add(new ConversationSummaryInfo {
                    ConversationId = "LOC-004",
                    ProjectName = "DeltaLocalOnly",
                    FolderUri = "file:///C:/Delta",
                    DisplayTitle = "Delta Local"
                });

                List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(localList, backupList);
                if (diff.Count != 4) throw new Exception("Expected 4 diff nodes, got " + diff.Count);

                Dictionary<string, ProjectDiffNode> map = new Dictionary<string, ProjectDiffNode>(StringComparer.OrdinalIgnoreCase);
                foreach (var d in diff) map[d.ProjectName] = d;

                if (map["AlphaProject"].Status != DiffStatus.Aligned)
                    throw new Exception("AlphaProject should be Aligned, got " + map["AlphaProject"].Status);
                if (map["BetaProject"].Status != DiffStatus.Conflicted)
                    throw new Exception("BetaProject should be Conflicted, got " + map["BetaProject"].Status);
                if (map["GammaNewProject"].Status != DiffStatus.NewInBackup)
                    throw new Exception("GammaNewProject should be NewInBackup, got " + map["GammaNewProject"].Status);
                if (map["DeltaLocalOnly"].Status != DiffStatus.LocalOnly)
                    throw new Exception("DeltaLocalOnly should be LocalOnly, got " + map["DeltaLocalOnly"].Status);
            });

            // ─────────────────────────────────────────────────────────────────
            // 3. CASCADING SELECTION MODEL & QUICK SELECT LOGIC
            // ─────────────────────────────────────────────────────────────────
            runTest("M3_ADV_03_CascadingSelectionModel_ReentrancyAndQuickSelect", delegate()
            {
                CascadingSelectionModel model = new CascadingSelectionModel();
                HashSet<string> selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                List<ConversationSummaryInfo> all = new List<ConversationSummaryInfo> {
                    new ConversationSummaryInfo { ConversationId = "UUID-1", ProjectName = "P1" },
                    new ConversationSummaryInfo { ConversationId = "UUID-2", ProjectName = "P1" },
                    new ConversationSummaryInfo { ConversationId = "UUID-3", ProjectName = "P2" }
                };

                // Select All
                model.SelectAll(all, selected);
                if (selected.Count != 3) throw new Exception("SelectAll failed, count=" + selected.Count);

                // Select None
                model.SelectNone(selected);
                if (selected.Count != 0) throw new Exception("SelectNone failed, count=" + selected.Count);

                // Select New Only
                List<ProjectDiffNode> diffNodes = new List<ProjectDiffNode>();
                ProjectDiffNode pNew = new ProjectDiffNode("NewProj") { Status = DiffStatus.NewInBackup };
                pNew.BackupConversations.Add(new ConversationSummaryInfo { ConversationId = "NEW-1" });
                pNew.BackupConversations.Add(new ConversationSummaryInfo { ConversationId = "NEW-2" });
                diffNodes.Add(pNew);

                ProjectDiffNode pAligned = new ProjectDiffNode("AlignedProj") { Status = DiffStatus.Aligned };
                pAligned.BackupConversations.Add(new ConversationSummaryInfo { ConversationId = "ALN-1" });
                diffNodes.Add(pAligned);

                model.SelectNewOnly(diffNodes, selected);
                if (selected.Count != 2) throw new Exception("SelectNewOnly count mismatch: " + selected.Count);
                if (!selected.Contains("NEW-1") || !selected.Contains("NEW-2")) throw new Exception("SelectNewOnly items mismatch");
                if (selected.Contains("ALN-1")) throw new Exception("SelectNewOnly included aligned item");
            });

            // ─────────────────────────────────────────────────────────────────
            // 4. PROTO SHARD MERGING WITH RENAME & SKIP STRATEGIES
            // ─────────────────────────────────────────────────────────────────
            runTest("M3_ADV_04_MergeProtoShards_ConflictStrategiesIntegrity", delegate()
            {
                // Create local pb with 1 conversation in "OriginalProj"
                ProtoNode localEntry = new ProtoNode(1, 2);
                ProtoNode localId = new ProtoNode(1, 2); localId.SetString("CONV-LOCAL-1");
                ProtoNode localCascade = new ProtoNode(17, 2);
                ProtoNode localPName = new ProtoNode(1, 2); localPName.SetString("OriginalProj");
                localCascade.Children = new List<ProtoNode> { localPName };
                localEntry.Children = new List<ProtoNode> { localId, localCascade };
                byte[] localPbBytes = ProtoAstCodec.Encode(new List<ProtoNode> { localEntry });

                // Create backup pb with 2 conversations: 1 in "OriginalProj" (to rename) and 1 in "SkippedProj" (to skip)
                ProtoNode backupEntry1 = new ProtoNode(1, 2);
                ProtoNode bkpId1 = new ProtoNode(1, 2); bkpId1.SetString("CONV-BKP-RENAME");
                ProtoNode bkpCascade1 = new ProtoNode(17, 2);
                ProtoNode bkpPName1 = new ProtoNode(1, 2); bkpPName1.SetString("OriginalProj");
                bkpCascade1.Children = new List<ProtoNode> { bkpPName1 };
                backupEntry1.Children = new List<ProtoNode> { bkpId1, bkpCascade1 };

                ProtoNode backupEntry2 = new ProtoNode(1, 2);
                ProtoNode bkpId2 = new ProtoNode(1, 2); bkpId2.SetString("CONV-BKP-SKIP");
                ProtoNode bkpCascade2 = new ProtoNode(17, 2);
                ProtoNode bkpPName2 = new ProtoNode(1, 2); bkpPName2.SetString("SkippedProj");
                bkpCascade2.Children = new List<ProtoNode> { bkpPName2 };
                backupEntry2.Children = new List<ProtoNode> { bkpId2, bkpCascade2 };

                byte[] backupPbBytes = ProtoAstCodec.Encode(new List<ProtoNode> { backupEntry1, backupEntry2 });

                HashSet<string> targetIds = new HashSet<string> { "CONV-BKP-RENAME", "CONV-BKP-SKIP" };
                Dictionary<string, string> renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    { "OriginalProj", "OriginalProj_Renamed" }
                };
                Dictionary<string, ConflictResolutionStrategy> strats = new Dictionary<string, ConflictResolutionStrategy>(StringComparer.OrdinalIgnoreCase) {
                    { "OriginalProj", ConflictResolutionStrategy.Rename },
                    { "SkippedProj", ConflictResolutionStrategy.Skip }
                };

                byte[] mergedPbBytes = SelectiveCascadingImporter.MergeProtoShards(localPbBytes, backupPbBytes, targetIds, renames, strats);
                var mergedSummaries = SessionMetadataExtractor.ExtractSummariesFromProto(mergedPbBytes);

                if (mergedSummaries.Count != 2) throw new Exception("Expected exactly 2 summaries (1 local + 1 renamed), got " + mergedSummaries.Count);

                bool foundLocal = false;
                bool foundRenamed = false;
                foreach (var s in mergedSummaries)
                {
                    if (s.ConversationId == "CONV-LOCAL-1" && s.ProjectName == "OriginalProj") foundLocal = true;
                    if (s.ConversationId == "CONV-BKP-RENAME" && s.ProjectName == "OriginalProj_Renamed") foundRenamed = true;
                    if (s.ConversationId == "CONV-BKP-SKIP") throw new Exception("Skipped conversation was included!");
                }
                if (!foundLocal || !foundRenamed) throw new Exception("Merged summaries content verification failed");
            });

            // ─────────────────────────────────────────────────────────────────
            // 5. SURGICAL SELECTIVE CASCADING IMPORT E2E SANDBOX
            // ─────────────────────────────────────────────────────────────────
            runTest("M3_ADV_05_SelectiveCascadingImporter_SurgicalExtractionE2E", delegate()
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "AgyCompanion_E2E_Test_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                try
                {
                    string backupZipPath = Path.Combine(tempDir, "mock_backup.agy");
                    string targetGemini = Path.Combine(tempDir, "dest_gemini");
                    Directory.CreateDirectory(targetGemini);

                    // Build Mock .agy Zip
                    using (FileStream fs = new FileStream(backupZipPath, FileMode.Create))
                    using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
                    {
                        // metadata.json
                        var meta = zip.CreateEntry("metadata.json");
                        using (StreamWriter sw = new StreamWriter(meta.Open(), Encoding.UTF8))
                        {
                            sw.Write("{\"source_home\":\"C:\\\\OldHome\\\\User\",\"version\":\"0.5\"}");
                        }

                        // Project JSON
                        var p1Json = zip.CreateEntry("config/projects/proj1.json");
                        using (StreamWriter sw = new StreamWriter(p1Json.Open(), Encoding.UTF8))
                        {
                            sw.Write("{\"id\":\"proj1\",\"name\":\"ProjectAlpha\",\"folderUri\":\"file:///C:/OldHome/User/Projects/Alpha\"}");
                        }

                        // Proto Summaries
                        ProtoNode e1 = new ProtoNode(1, 2);
                        ProtoNode id1 = new ProtoNode(1, 2); id1.SetString("UUID-SELECTED-1");
                        ProtoNode c1 = new ProtoNode(17, 2);
                        ProtoNode pn1 = new ProtoNode(1, 2); pn1.SetString("ProjectAlpha");
                        c1.Children = new List<ProtoNode> { pn1 };
                        e1.Children = new List<ProtoNode> { id1, c1 };

                        ProtoNode e2 = new ProtoNode(1, 2);
                        ProtoNode id2 = new ProtoNode(1, 2); id2.SetString("UUID-UNSELECTED-2");
                        ProtoNode c2 = new ProtoNode(17, 2);
                        ProtoNode pn2 = new ProtoNode(1, 2); pn2.SetString("ProjectAlpha");
                        c2.Children = new List<ProtoNode> { pn2 };
                        e2.Children = new List<ProtoNode> { id2, c2 };

                        byte[] pbRaw = ProtoAstCodec.Encode(new List<ProtoNode> { e1, e2 });
                        var pbEntry = zip.CreateEntry("antigravity/agyhub_summaries_proto.pb");
                        using (Stream s = pbEntry.Open()) s.Write(pbRaw, 0, pbRaw.Length);

                        // Conversations
                        var db1 = zip.CreateEntry("antigravity/conversations/UUID-SELECTED-1.db");
                        using (StreamWriter sw = new StreamWriter(db1.Open())) sw.Write("SQLite DB Content 1");
                        var db1Wal = zip.CreateEntry("antigravity/conversations/UUID-SELECTED-1.db-wal");
                        using (StreamWriter sw = new StreamWriter(db1Wal.Open())) sw.Write("WAL Content 1");

                        var db2 = zip.CreateEntry("antigravity/conversations/UUID-UNSELECTED-2.db");
                        using (StreamWriter sw = new StreamWriter(db2.Open())) sw.Write("SQLite DB Content 2");

                        // Brain
                        var b1File = zip.CreateEntry("antigravity/brain/UUID-SELECTED-1/memory.json");
                        using (StreamWriter sw = new StreamWriter(b1File.Open())) sw.Write("Brain Memory 1");
                        var b2File = zip.CreateEntry("antigravity/brain/UUID-UNSELECTED-2/memory.json");
                        using (StreamWriter sw = new StreamWriter(b2File.Open())) sw.Write("Brain Memory 2");

                        // Annotations
                        var an1 = zip.CreateEntry("antigravity/annotations/UUID-SELECTED-1.pbtxt");
                        using (StreamWriter sw = new StreamWriter(an1.Open())) sw.Write("Annot 1");
                        var an2 = zip.CreateEntry("antigravity/annotations/UUID-UNSELECTED-2.pbtxt");
                        using (StreamWriter sw = new StreamWriter(an2.Open())) sw.Write("Annot 2");
                    }

                    // Pre-create local existing conversation
                    string localConvDir = Path.Combine(targetGemini, "antigravity", "conversations");
                    Directory.CreateDirectory(localConvDir);
                    File.WriteAllText(Path.Combine(localConvDir, "LOCAL-EXISTING.db"), "Pre-existing Local Content");

                    // Execute Selective Import for UUID-SELECTED-1 ONLY
                    SelectiveImportRequest req = new SelectiveImportRequest {
                        BackupFilePath = backupZipPath,
                        TargetGeminiPath = targetGemini,
                        SelectedConversationIds = new List<string> { "UUID-SELECTED-1" },
                        SelectedProjectNames = new List<string> { "ProjectAlpha" },
                        ImportGlobalRules = true
                    };

                    SelectiveImportResult result = SelectiveCascadingImporter.ExecuteImport(req);
                    if (!result.Success) throw new Exception("Import failed: " + string.Join("; ", result.Errors.ToArray()));
                    if (result.ImportedConversationsCount != 1) throw new Exception("Imported conversation count mismatch: " + result.ImportedConversationsCount);
                    if (result.ImportedBrainDirsCount != 1) throw new Exception("Imported brain dirs count mismatch: " + result.ImportedBrainDirsCount);

                    // Verify Extracted Files
                    string destDb1 = Path.Combine(targetGemini, "antigravity", "conversations", "UUID-SELECTED-1.db");
                    string destDb1Wal = Path.Combine(targetGemini, "antigravity", "conversations", "UUID-SELECTED-1.db-wal");
                    string destDb2 = Path.Combine(targetGemini, "antigravity", "conversations", "UUID-UNSELECTED-2.db");
                    string destBrain1 = Path.Combine(targetGemini, "antigravity", "brain", "UUID-SELECTED-1", "memory.json");
                    string destBrain2 = Path.Combine(targetGemini, "antigravity", "brain", "UUID-UNSELECTED-2", "memory.json");
                    string destAnnot1 = Path.Combine(targetGemini, "antigravity", "annotations", "UUID-SELECTED-1.pbtxt");
                    string destAnnot2 = Path.Combine(targetGemini, "antigravity", "annotations", "UUID-UNSELECTED-2.pbtxt");
                    string localExisting = Path.Combine(targetGemini, "antigravity", "conversations", "LOCAL-EXISTING.db");

                    if (!File.Exists(destDb1)) throw new Exception("Selected DB was not extracted!");
                    if (!File.Exists(destDb1Wal)) throw new Exception("Selected DB-WAL was not extracted!");
                    if (File.Exists(destDb2)) throw new Exception("CRITICAL: Unselected DB was wrongfully extracted!");

                    if (!File.Exists(destBrain1)) throw new Exception("Selected Brain was not extracted!");
                    if (File.Exists(destBrain2)) throw new Exception("CRITICAL: Unselected Brain was wrongfully extracted!");

                    if (!File.Exists(destAnnot1)) throw new Exception("Selected Annotation was not extracted!");
                    if (File.Exists(destAnnot2)) throw new Exception("CRITICAL: Unselected Annotation was wrongfully extracted!");

                    if (!File.Exists(localExisting) || File.ReadAllText(localExisting) != "Pre-existing Local Content")
                        throw new Exception("CRITICAL: Pre-existing local file was corrupted or lost!");
                }
                finally
                {
                    try { Directory.Delete(tempDir, true); } catch {}
                }
            });

            // ─────────────────────────────────────────────────────────────────
            // 6. HIGHLIGHT TRACKER CONCURRENCY & INTEGRITY
            // ─────────────────────────────────────────────────────────────────
            runTest("M3_ADV_06_HighlightTracker_ThreadSafetyAndLookups", delegate()
            {
                HighlightTracker tracker = new HighlightTracker();
                tracker.MarkImported(new List<string> { "uuid-abc-123", "UUID-XYZ-789" }, new List<string> { "ProjAlpha" });

                if (!tracker.IsConversationHighlighted("UUID-ABC-123")) throw new Exception("Case insensitive lookup failed");
                if (!tracker.IsConversationHighlighted("uuid-xyz-789")) throw new Exception("Case insensitive lookup failed");
                if (tracker.IsConversationHighlighted("uuid-nonexistent")) throw new Exception("Nonexistent conversation highlighted");
                if (tracker.IsConversationHighlighted(null)) throw new Exception("Null conversation highlighted");

                if (!tracker.IsProjectHighlighted("projalpha")) throw new Exception("Project lookup failed");
                if (tracker.IsProjectHighlighted("Nonexistent")) throw new Exception("Nonexistent project highlighted");

                // Concurrency test
                List<Thread> threads = new List<Thread>();
                for (int t = 0; t < 20; t++)
                {
                    int threadId = t;
                    Thread th = new Thread(delegate()
                    {
                        for (int i = 0; i < 50; i++)
                        {
                            tracker.MarkImported(new List<string> { "conv-" + threadId + "-" + i }, new List<string> { "proj-" + threadId });
                            tracker.IsConversationHighlighted("conv-" + threadId + "-" + i);
                        }
                    });
                    threads.Add(th);
                    th.Start();
                }
                foreach (var th in threads) th.Join();
            });

            // ─────────────────────────────────────────────────────────────────
            // 7. DOUBLE BUFFERED TREEVIEW STYLE FLAGS & P/INVOKE INTEGRITY
            // ─────────────────────────────────────────────────────────────────
            runTest("M2_ADV_07_DoubleBufferedTreeView_StyleFlagsVerification", delegate()
            {
                using (DoubleBufferedTreeView tv = new DoubleBufferedTreeView())
                {
                    if (tv.CheckBoxes) throw new Exception("Default CheckBoxes should be false");
                    tv.CheckBoxes = true;
                    if (!tv.CheckBoxes) throw new Exception("CheckBoxes setting failed");

                    TreeNode root = new TreeNode("Root");
                    TreeNode child = new TreeNode("Child");
                    root.Nodes.Add(child);
                    tv.Nodes.Add(root);

                    if (tv.Nodes.Count != 1) throw new Exception("Node count mismatch");
                }
            });

            Console.WriteLine("===============================================================================");
            Console.WriteLine(string.Format("  REVIEWER ADVERSARIAL SUMMARY: {0} Passed, {1} Failed", passed, failed));
            Console.WriteLine("===============================================================================");

            return failed == 0 ? 0 : 1;
        }
    }
}
