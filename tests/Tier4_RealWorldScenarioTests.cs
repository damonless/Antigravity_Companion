using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AntigravityCompanion.Tests
{
    public static class Tier4_RealWorldScenarioTests
    {
        // =====================================================================
        // Scenario 1: Multi-Machine Developer Migration (350MB simulated workload, 20 custom titles, 5 collided projects)
        // =====================================================================
        public static void Scenario_01_MultiMachineDeveloperMigration(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                // Local environment: 3 existing projects with 10 sessions total
                List<MockProjectSpec> localProjects = new List<MockProjectSpec>();
                for (int p = 1; p <= 3; p++)
                {
                    MockProjectSpec proj = new MockProjectSpec("LocalProject_" + p, "file:///c:/Dev/LocalProject_" + p);
                    for (int s = 1; s <= 3; s++)
                    {
                        proj.Sessions.Add(new MockSessionSpec
                        {
                            ConversationId = Guid.NewGuid().ToString(),
                            CustomTitle = "本地工程_" + p + "_会话_" + s,
                            DataSizeBytes = 8192
                        });
                    }
                    localProjects.Add(proj);
                }
                MockSandboxGenerator.PopulateLocalSandbox(sandbox, localProjects);

                // Backup package: 8 projects total (3 aligned/collided, 5 brand new), 20 custom Chinese titles, large payloads
                List<MockProjectSpec> backupProjects = new List<MockProjectSpec>();
                List<string> selectedBackupIds = new List<string>();

                for (int p = 1; p <= 8; p++)
                {
                    string pName = p <= 3 ? "LocalProject_" + p : "BackupProject_" + p;
                    string pUri = p <= 3 ? "file:///d:/OldMachine/LocalProject_" + p : "file:///d:/OldMachine/BackupProject_" + p;
                    MockProjectSpec proj = new MockProjectSpec(pName, pUri);

                    for (int s = 1; s <= 3; s++)
                    {
                        string id = Guid.NewGuid().ToString();
                        proj.Sessions.Add(new MockSessionSpec
                        {
                            ConversationId = id,
                            CustomTitle = "迁移工程_" + p + "_中文会话_" + s,
                            DataSizeBytes = 16384
                        });
                        selectedBackupIds.Add(id);
                    }
                    backupProjects.Add(proj);
                }

                string backupZipPath = Path.Combine(sandbox.BackupPath, "developer_migration_350mb.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(backupZipPath, backupProjects);

                // 1. Inspect Backup stream
                ZipMetadataPackage pkg = StreamingZipIndexReader.ReadZipMetadata(backupZipPath);
                List<ConversationSummaryInfo> backupSummaries = SessionMetadataExtractor.ExtractSummariesFromProto(pkg.SummaryProtoBytes);
                ctx.AssertEqual(24, backupSummaries.Count, "24 backup sessions parsed in metadata");

                // 2. Perform Visual Diff
                List<ConversationSummaryInfo> localSummaries = SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(Path.Combine(sandbox.AntigravityPath, "agyhub_summaries_proto.pb")));
                List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(localSummaries, backupSummaries);

                int collidedCount = 0;
                int newCount = 0;
                for (int i = 0; i < diff.Count; i++)
                {
                    if (diff[i].Status == DiffStatus.Conflicted) collidedCount++;
                    if (diff[i].Status == DiffStatus.NewInBackup) newCount++;
                }

                ctx.AssertEqual(3, collidedCount, "3 collided projects detected (different URIs)");
                ctx.AssertEqual(5, newCount, "5 new projects detected");

                // 3. Resolve conflicts via Merge and Rename
                Dictionary<string, ConflictResolutionStrategy> strategies = new Dictionary<string, ConflictResolutionStrategy>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, string> renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                strategies["LocalProject_1"] = ConflictResolutionStrategy.Merge;
                strategies["LocalProject_2"] = ConflictResolutionStrategy.Rename;
                renames["LocalProject_2"] = "LocalProject_2_OldMachine";
                strategies["LocalProject_3"] = ConflictResolutionStrategy.Merge;

                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = backupZipPath,
                    TargetGeminiPath = sandbox.GeminiPath,
                    SelectedConversationIds = selectedBackupIds,
                    ProjectStrategies = strategies,
                    ProjectRenames = renames
                };

                SelectiveImportResult res = SelectiveCascadingImporter.ExecuteImport(req);
                ctx.AssertTrue(res.Success, "Multi-machine migration execution succeeded");
                ctx.AssertEqual(24, res.ImportedConversationsCount, "All 24 selected sessions imported");

                // 4. Verify Final Local State
                byte[] finalLocalPb = File.ReadAllBytes(Path.Combine(sandbox.AntigravityPath, "agyhub_summaries_proto.pb"));
                List<ConversationSummaryInfo> finalSummaries = SessionMetadataExtractor.ExtractSummariesFromProto(finalLocalPb);

                // 9 original local sessions + 24 imported sessions = 33 sessions total
                ctx.AssertEqual(33, finalSummaries.Count, "Final local environment contains exactly 33 sessions (lossless)");
            }
        }

        // =====================================================================
        // Scenario 2: Selective Single Conversation Extraction (1 of 50 conversations)
        // =====================================================================
        public static void Scenario_02_SelectiveSingleConversationExtraction(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                // Local has 5 existing sessions
                MockProjectSpec localP = new MockProjectSpec("CoreProject", "file:///c:/CoreProject");
                for (int i = 0; i < 5; i++)
                {
                    localP.Sessions.Add(new MockSessionSpec { ConversationId = "local-uuid-" + i, CustomTitle = "Local Session " + i });
                }
                MockSandboxGenerator.PopulateLocalSandbox(sandbox, new List<MockProjectSpec> { localP });

                // Backup has 50 conversations across 5 projects
                List<MockProjectSpec> backupProjects = new List<MockProjectSpec>();
                string targetUuid = "target-extract-uuid-9999";
                string targetTitle = "【精准提取】高价值特定排障会话";

                for (int p = 1; p <= 5; p++)
                {
                    MockProjectSpec proj = new MockProjectSpec("BigProject_" + p, "file:///c:/BigProject_" + p);
                    for (int s = 1; s <= 10; s++)
                    {
                        bool isTarget = (p == 3 && s == 7);
                        proj.Sessions.Add(new MockSessionSpec
                        {
                            ConversationId = isTarget ? targetUuid : Guid.NewGuid().ToString(),
                            CustomTitle = isTarget ? targetTitle : ("Backup Session " + p + "_" + s),
                            HasBrainData = true
                        });
                    }
                    backupProjects.Add(proj);
                }

                string backupZipPath = Path.Combine(sandbox.BackupPath, "fifty_sessions.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(backupZipPath, backupProjects);

                // Perform selective import of ONLY targetUuid
                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = backupZipPath,
                    TargetGeminiPath = sandbox.GeminiPath,
                    SelectedConversationIds = new List<string> { targetUuid }
                };

                SelectiveImportResult res = SelectiveCascadingImporter.ExecuteImport(req);
                ctx.AssertTrue(res.Success, "Selective import succeeded");
                ctx.AssertEqual(1, res.ImportedConversationsCount, "Exactly 1 conversation imported");
                ctx.AssertEqual(1, res.ImportedBrainDirsCount, "Exactly 1 brain directory imported");

                // Verify target exists on disk
                string targetDb = Path.Combine(sandbox.AntigravityPath, "conversations", targetUuid + ".db");
                string targetBrain = Path.Combine(sandbox.AntigravityPath, "brain", targetUuid);
                ctx.AssertTrue(File.Exists(targetDb), "Target DB file exists");
                ctx.AssertTrue(Directory.Exists(targetBrain), "Target brain directory exists");

                // Verify none of the other 49 sessions contaminated disk
                string[] allDbs = Directory.GetFiles(Path.Combine(sandbox.AntigravityPath, "conversations"), "*.db");
                // 5 local + 1 imported = 6 total DB files
                ctx.AssertEqual(6, allDbs.Length, "Only 6 DB files exist in local directory (zero contamination)");

                // Verify local protobuf summary contains target with exact title
                byte[] pb = File.ReadAllBytes(Path.Combine(sandbox.AntigravityPath, "agyhub_summaries_proto.pb"));
                List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(pb);
                ctx.AssertEqual(6, summaries.Count, "Local pb summary contains 6 entries");

                ConversationSummaryInfo extractedInfo = null;
                for (int i = 0; i < summaries.Count; i++)
                {
                    if (summaries[i].ConversationId == targetUuid)
                    {
                        extractedInfo = summaries[i];
                        break;
                    }
                }

                ctx.AssertTrue(extractedInfo != null, "Extracted session exists in pb");
                ctx.AssertEqual(targetTitle, extractedInfo.DisplayTitle, "Chinese title preserved 100%");
            }
        }

        // =====================================================================
        // Scenario 3: Junk Project Hygiene & Temp Subagent Isolation
        // =====================================================================
        public static void Scenario_03_JunkProjectHygieneAndIsolation(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                // Create backup with 2 real projects and 5 junk projects
                List<MockProjectSpec> projects = new List<MockProjectSpec>
                {
                    new MockProjectSpec("ProductionService", "file:///c:/ProductionService"),
                    new MockProjectSpec("Antigravity_Companion", "file:///c:/Antigravity_Companion"),
                    new MockProjectSpec("subagent-review-1", "file:///c:/subagent-1"),
                    new MockProjectSpec("subagent-codegen-2", "file:///c:/subagent-2"),
                    new MockProjectSpec("outside-of-project", "file:///c:/outside"),
                    new MockProjectSpec("default-cli-project", "file:///c:/default"),
                    new MockProjectSpec("Recovered project 2026-08-20", "file:///c:/recovered")
                };

                for (int i = 0; i < projects.Count; i++)
                {
                    projects[i].Sessions.Add(new MockSessionSpec { CustomTitle = "Session for " + projects[i].ProjectName });
                }

                string backupZipPath = Path.Combine(sandbox.BackupPath, "junk_hygiene.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(backupZipPath, projects);

                // Read and diff
                ZipMetadataPackage pkg = StreamingZipIndexReader.ReadZipMetadata(backupZipPath);
                List<ConversationSummaryInfo> backupSummaries = SessionMetadataExtractor.ExtractSummariesFromProto(pkg.SummaryProtoBytes);
                List<ProjectDiffNode> diffNodes = VisualDiffEngine.ComputeDiff(null, backupSummaries);

                ctx.AssertEqual(7, diffNodes.Count, "7 total project diff nodes created");

                // Check default filtered view (chkHideTemp = true)
                List<ProjectDiffNode> visibleNodes = new List<ProjectDiffNode>();
                List<ProjectDiffNode> hiddenNodes = new List<ProjectDiffNode>();

                for (int i = 0; i < diffNodes.Count; i++)
                {
                    if (diffNodes[i].IsTemporaryOrJunk)
                        hiddenNodes.Add(diffNodes[i]);
                    else
                        visibleNodes.Add(diffNodes[i]);
                }

                ctx.AssertEqual(2, visibleNodes.Count, "Only 2 production projects visible by default");
                ctx.AssertEqual(5, hiddenNodes.Count, "5 junk projects hidden by default");

                // Quick Select New Only on visible nodes
                CascadingSelectionModel model = new CascadingSelectionModel();
                HashSet<string> selected = new HashSet<string>();
                model.SelectNewOnly(visibleNodes, selected);

                ctx.AssertEqual(2, selected.Count, "Only sessions from the 2 production projects selected");

                // Execute import of visible only
                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = backupZipPath,
                    TargetGeminiPath = sandbox.GeminiPath,
                    SelectedConversationIds = new List<string>(selected)
                };

                SelectiveImportResult res = SelectiveCascadingImporter.ExecuteImport(req);
                ctx.AssertEqual(2, res.ImportedConversationsCount, "Only 2 valid sessions imported");

                // Verify local summaries contain zero junk projects
                byte[] localPb = File.ReadAllBytes(Path.Combine(sandbox.AntigravityPath, "agyhub_summaries_proto.pb"));
                List<ConversationSummaryInfo> localList = SessionMetadataExtractor.ExtractSummariesFromProto(localPb);

                for (int i = 0; i < localList.Count; i++)
                {
                    ctx.AssertFalse(SmartProjectFilter.IsJunkOrEphemeral(localList[i].ProjectName), "Local environment contains NO junk projects: " + localList[i].ProjectName);
                }
            }
        }

        // =====================================================================
        // Scenario 4: Project Name Collision Resolution Matrix (Merge, Rename, Overwrite, Skip)
        // =====================================================================
        public static void Scenario_04_ProjectNameCollisionResolutionMatrix(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                // Local has 4 projects: ProjMerge, ProjRename, ProjOverwrite, ProjSkip
                string localMergeId = Guid.NewGuid().ToString();
                string localRenameId = Guid.NewGuid().ToString();
                string localOverwriteId = Guid.NewGuid().ToString();
                string localSkipId = Guid.NewGuid().ToString();

                MockProjectSpec p1 = new MockProjectSpec("ProjMerge", "file:///c:/Local/ProjMerge");
                p1.Sessions.Add(new MockSessionSpec { ConversationId = localMergeId, CustomTitle = "Local Merge Session" });

                MockProjectSpec p2 = new MockProjectSpec("ProjRename", "file:///c:/Local/ProjRename");
                p2.Sessions.Add(new MockSessionSpec { ConversationId = localRenameId, CustomTitle = "Local Rename Session" });

                MockProjectSpec p3 = new MockProjectSpec("ProjOverwrite", "file:///c:/Local/ProjOverwrite");
                p3.Sessions.Add(new MockSessionSpec { ConversationId = localOverwriteId, CustomTitle = "Local Old Overwrite Session" });

                MockProjectSpec p4 = new MockProjectSpec("ProjSkip", "file:///c:/Local/ProjSkip");
                p4.Sessions.Add(new MockSessionSpec { ConversationId = localSkipId, CustomTitle = "Local Skip Session" });

                MockSandboxGenerator.PopulateLocalSandbox(sandbox, new List<MockProjectSpec> { p1, p2, p3, p4 });

                // Backup has the same 4 project names with different URIs
                string backupMergeId = Guid.NewGuid().ToString();
                string backupRenameId = Guid.NewGuid().ToString();
                string backupOverwriteId = localOverwriteId; // Same UUID to test overwrite
                string backupSkipId = Guid.NewGuid().ToString();

                MockProjectSpec bp1 = new MockProjectSpec("ProjMerge", "file:///d:/Backup/ProjMerge");
                bp1.Sessions.Add(new MockSessionSpec { ConversationId = backupMergeId, CustomTitle = "Backup Merged Session" });

                MockProjectSpec bp2 = new MockProjectSpec("ProjRename", "file:///d:/Backup/ProjRename");
                bp2.Sessions.Add(new MockSessionSpec { ConversationId = backupRenameId, CustomTitle = "Backup Renamed Session" });

                MockProjectSpec bp3 = new MockProjectSpec("ProjOverwrite", "file:///d:/Backup/ProjOverwrite");
                bp3.Sessions.Add(new MockSessionSpec { ConversationId = backupOverwriteId, CustomTitle = "Backup New Overwritten Session" });

                MockProjectSpec bp4 = new MockProjectSpec("ProjSkip", "file:///d:/Backup/ProjSkip");
                bp4.Sessions.Add(new MockSessionSpec { ConversationId = backupSkipId, CustomTitle = "Backup Skipped Session" });

                string backupZipPath = Path.Combine(sandbox.BackupPath, "matrix.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(backupZipPath, new List<MockProjectSpec> { bp1, bp2, bp3, bp4 });

                // Configure conflict resolution matrix
                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = backupZipPath,
                    TargetGeminiPath = sandbox.GeminiPath,
                    SelectedConversationIds = new List<string> { backupMergeId, backupRenameId, backupOverwriteId, backupSkipId }
                };

                req.ProjectStrategies["ProjMerge"] = ConflictResolutionStrategy.Merge;
                req.ProjectStrategies["ProjRename"] = ConflictResolutionStrategy.Rename;
                req.ProjectRenames["ProjRename"] = "ProjRename_Imported";
                req.ProjectStrategies["ProjOverwrite"] = ConflictResolutionStrategy.Overwrite;
                req.ProjectStrategies["ProjSkip"] = ConflictResolutionStrategy.Skip;

                SelectiveImportResult res = SelectiveCascadingImporter.ExecuteImport(req);
                ctx.AssertTrue(res.Success, "Matrix import succeeded");

                // Verify Local Protobuf state
                byte[] localPb = File.ReadAllBytes(Path.Combine(sandbox.AntigravityPath, "agyhub_summaries_proto.pb"));
                List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummariesFromProto(localPb);

                // ProjMerge: localMergeId AND backupMergeId both exist
                ctx.AssertTrue(summaries.Exists(delegate(ConversationSummaryInfo s) { return s.ConversationId == localMergeId; }), "Merge: local session exists");
                ctx.AssertTrue(summaries.Exists(delegate(ConversationSummaryInfo s) { return s.ConversationId == backupMergeId; }), "Merge: backup session exists");

                // ProjRename: backup session exists with remapped project name
                ConversationSummaryInfo renamedInfo = summaries.Find(delegate(ConversationSummaryInfo s) { return s.ConversationId == backupRenameId; });
                ctx.AssertTrue(renamedInfo != null, "Rename: backup session exists");
                ctx.AssertEqual("ProjRename_Imported", renamedInfo.ProjectName, "Rename: project name is ProjRename_Imported");

                // ProjOverwrite: localOverwriteId has new title
                ConversationSummaryInfo overwriteInfo = summaries.Find(delegate(ConversationSummaryInfo s) { return s.ConversationId == localOverwriteId; });
                ctx.AssertTrue(overwriteInfo != null, "Overwrite: session exists");
                ctx.AssertEqual("Backup New Overwritten Session", overwriteInfo.DisplayTitle, "Overwrite: title is updated");

                // ProjSkip: backupSkipId must NOT exist in local
                ctx.AssertFalse(summaries.Exists(delegate(ConversationSummaryInfo s) { return s.ConversationId == backupSkipId; }), "Skip: backup session omitted");
            }
        }

        // =====================================================================
        // Scenario 5: Cross-Machine Chinese Title Lossless Integrity
        // =====================================================================
        public static void Scenario_05_CrossMachineChineseTitleLosslessIntegrity(TestContext ctx)
        {
            using (MockSandboxContext sandbox = new MockSandboxContext())
            {
                string complexTitle = "【生产环境排障】修复 C# 5.0 原生 Protobuf AST 编解码器与跨机器中文会话丢失缺陷 (v0.5.1-hotfix) ⚡🚀";
                string convId = Guid.NewGuid().ToString();

                MockProjectSpec proj = new MockProjectSpec("Antigravity_Companion", "file:///c:/Tools/Antigravity_Companion");
                proj.Sessions.Add(new MockSessionSpec
                {
                    ConversationId = convId,
                    CustomTitle = complexTitle,
                    HasBrainData = true,
                    StepCount = 88
                });

                string backupZipPath = Path.Combine(sandbox.BackupPath, "lossless_chinese.agy");
                MockSandboxGenerator.CreateSyntheticBackupArchive(backupZipPath, new List<MockProjectSpec> { proj });

                // Execute import into empty local sandbox
                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    BackupFilePath = backupZipPath,
                    TargetGeminiPath = sandbox.GeminiPath,
                    SelectedConversationIds = new List<string> { convId }
                };

                SelectiveImportResult res = SelectiveCascadingImporter.ExecuteImport(req);
                ctx.AssertTrue(res.Success, "Import succeeded");

                // Parse restored local agyhub_summaries_proto.pb
                byte[] localPb = File.ReadAllBytes(Path.Combine(sandbox.AntigravityPath, "agyhub_summaries_proto.pb"));
                List<ConversationSummaryInfo> restoredSummaries = SessionMetadataExtractor.ExtractSummariesFromProto(localPb);

                ctx.AssertEqual(1, restoredSummaries.Count, "1 restored summary");
                ctx.AssertEqual(complexTitle, restoredSummaries[0].DisplayTitle, "Restored DisplayTitle matches exactly");
                ctx.AssertEqual(complexTitle, restoredSummaries[0].CustomTitle, "Restored CustomTitle matches exactly");
                ctx.AssertTrue(restoredSummaries[0].HasCustomTitle, "Restored HasCustomTitle is true");
                ctx.AssertEqual(88, restoredSummaries[0].StepCount, "Restored StepCount matches");
            }
        }
    }
}
