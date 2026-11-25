using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using AntigravityCompanion;

namespace AntigravityCompanion.Tests
{
    public static class TestRunner
    {
        private static int _passed = 0;
        private static int _failed = 0;
        private static int _assertions = 0;

        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("===============================================================================");
            Console.WriteLine("  Antigravity Companion V1.0 - Production Code Verification Suite");
            Console.WriteLine("===============================================================================");

            Stopwatch sw = Stopwatch.StartNew();

            Run("T01_ProtoCodec_RoundtripAndVarintBoundary", T01_ProtoCodec_RoundtripAndVarintBoundary);
            Run("T02_SessionMetadataExtractor_RealProtoCustomTitle", T02_SessionMetadataExtractor_RealProtoCustomTitle);
            Run("T03_SessionMetadataExtractor_RejectsFlatInvalidEntry", T03_SessionMetadataExtractor_RejectsFlatInvalidEntry);
            Run("T04_SmartProjectFilter_JunkAndSubagentIsolation", T04_SmartProjectFilter_JunkAndSubagentIsolation);
            Run("T05_StreamingZipIndexReader_InMemoryInspection", T05_StreamingZipIndexReader_InMemoryInspection);
            Run("T06_VisualDiffEngine_DiffAndCrossDriveMerge", T06_VisualDiffEngine_DiffAndCrossDriveMerge);
            Run("T07_IncrementalProtoShardMerger_LosslessMergeAndUriRemap", T07_IncrementalProtoShardMerger_LosslessMergeAndUriRemap);
            Run("T08_DirectStreamArchiver_MultiFormatPathRemap", T08_DirectStreamArchiver_MultiFormatPathRemap);
            Run("T09_F1_CascadingDeleteSessionProjectAndSubagents", T09_F1_CascadingDeleteSessionProjectAndSubagents);
            Run("T10_F1_OrphanGarbageCollector_ScanAndClean", T10_F1_OrphanGarbageCollector_ScanAndClean);
            Run("T11_F2_RenameSession_DualFieldPersistence", T11_F2_RenameSession_DualFieldPersistence);
            Run("T12_F2_MoveSessionAndRelocateProjectPath", T12_F2_MoveSessionAndRelocateProjectPath);
            Run("T13_F3_ExportSelectiveBackup_SlicedProtoAndRoundtrip", T13_F3_ExportSelectiveBackup_SlicedProtoAndRoundtrip);
            Run("T14_F4_TranscriptMarkdownExporter_LocalAndBackupZip", T14_F4_TranscriptMarkdownExporter_LocalAndBackupZip);
            Run("T15_V07_CapabilityScanner_FiveCategoriesAndJsonKeyLexer", T15_V07_CapabilityScanner_FiveCategoriesAndJsonKeyLexer);
            Run("T16_V07_RepositoryPushAndSelectiveImport_KeyLevelMerge", T16_V07_RepositoryPushAndSelectiveImport_KeyLevelMerge);
            Run("T17_V07_MissingPathDetectionAndRelocation", T17_V07_MissingPathDetectionAndRelocation);
            Run("T18_V07_SelectiveAgyArchive_CapabilitySliceRoundtrip", T18_V07_SelectiveAgyArchive_CapabilitySliceRoundtrip);
            Run("T19_ProtoAstCodec_StrictnessField15AndSharding", T19_ProtoAstCodec_StrictnessField15AndSharding);
            Run("T20_RealWinSqlite3_WalAndChinesePathRemap", T20_RealWinSqlite3_WalAndChinesePathRemap);
            Run("T21_StreamingZipIndexReader_ZipSlipAndSelectiveClosure", T21_StreamingZipIndexReader_ZipSlipAndSelectiveClosure);
            Run("T22_CapabilityRepo_PlaceholdersJsoncEncodedIdAndHooks", T22_CapabilityRepo_PlaceholdersJsoncEncodedIdAndHooks);
            Run("T23_TranscriptMarkdownExporter_StreamingTagsAndFullJsonl", T23_TranscriptMarkdownExporter_StreamingTagsAndFullJsonl);
            Run("T24_VisualDiffEngine_RenameCrossHostAndSameBasename", T24_VisualDiffEngine_RenameCrossHostAndSameBasename);
            Run("T25_WorkspaceService_ProjectJsonAndBackupSelectionPolicy", T25_WorkspaceService_ProjectJsonAndBackupSelectionPolicy);
            Run("T26_SessionMetadataExtractor_NanoEpochFixed64AndOutsideUri", T26_SessionMetadataExtractor_NanoEpochFixed64AndOutsideUri);
            Run("T27_VisualDiffEngine_CapMissingPathsLocalOnlyCountAndSig", T27_VisualDiffEngine_CapMissingPathsLocalOnlyCountAndSig);
            Run("T28_CapabilityRepo_EncodingBomMultilineJsonAndYaml", T28_CapabilityRepo_EncodingBomMultilineJsonAndYaml);
            Run("T29_SessionLifecycle_ProjectIdIsolationAndSubagentDelete", T29_SessionLifecycle_ProjectIdIsolationAndSubagentDelete);
            Run("T30_CapabilityRepo_ScriptPathRegexQuotedEnvAndSpaces", T30_CapabilityRepo_ScriptPathRegexQuotedEnvAndSpaces);
            Run("T31_CapabilityVault_RelocateCapabilityPath_LocalAndRepo", T31_CapabilityVault_RelocateCapabilityPath_LocalAndRepo);
            Run("T32_PbtxtAnnotationCodec_RoundTripAndCreate", T32_PbtxtAnnotationCodec_RoundTripAndCreate);
            Run("T33_SummarySqliteBridge_ThreeLayerAndHybridFts", T33_SummarySqliteBridge_ThreeLayerAndHybridFts);
            Run("T34_AutoTitleSynthesizer_RulesAndSubagentExclusion", T34_AutoTitleSynthesizer_RulesAndSubagentExclusion);
            Run("T35_BatchRenameAndRowLevelRollback_OrphanGc", T35_BatchRenameAndRowLevelRollback_OrphanGc);
            Run("T36_AiPolishExchangeAndAgyAnnotationRoundTrip", T36_AiPolishExchangeAndAgyAnnotationRoundTrip);
            Run("T37_ProjectAttribution_Field17Submessage_SqliteBackfill_MultiRootUris", T37_ProjectAttribution_Field17Submessage_SqliteBackfill_MultiRootUris);
            Run("T38_RenameRules_Field9To4Branch_TrailingBranchStrip_EmbeddedBracketFold_LowValue", T38_RenameRules_Field9To4Branch_TrailingBranchStrip_EmbeddedBracketFold_LowValue);
            Run("T39_CliBrainFallback_FirstPrompt_MarkdownExport_ShortestDistinguishingPath", T39_CliBrainFallback_FirstPrompt_MarkdownExport_ShortestDistinguishingPath);
            Run("T40_DirectionalCapabilityDiff_BaselineAndUtcMtime_LineLcsDiff_ReadOnlyGc", T40_DirectionalCapabilityDiff_BaselineAndUtcMtime_LineLcsDiff_ReadOnlyGc);
            Run("T41_LocalWebHost_TcpFraming_AuthSecurity_StaticResources_AndReadApis", T41_LocalWebHost_TcpFraming_AuthSecurity_StaticResources_AndReadApis);
            Run("T42_LocalWebHost_EndToEndMutationSandbox_RenameRollbackCapabilityAndGc", T42_LocalWebHost_EndToEndMutationSandbox_RenameRollbackCapabilityAndGc);
            Run("T43_P0_1_RelocateProjectPath_NestedGitFolderFolderUri", T43_P0_1_RelocateProjectPath_NestedGitFolderFolderUri);
            Run("T44_P0_2_P0_8_CapabilityDiff_ConflictNoBaselineAndArchiveIgnoresBaseline", T44_P0_2_P0_8_CapabilityDiff_ConflictNoBaselineAndArchiveIgnoresBaseline);
            Run("T45_P0_3_CliArtifactsIncludedAndBinDirectoriesExcluded", T45_P0_3_CliArtifactsIncludedAndBinDirectoriesExcluded);
            Run("T46_P0_4_WalLiveDbAtomicBackup_IntegrityCheckOk", T46_P0_4_WalLiveDbAtomicBackup_IntegrityCheckOk);
            Run("T47_P0_5_JsoncCommentsPreservedAndSingleTimestampedBackupPerBatch", T47_P0_5_JsoncCommentsPreservedAndSingleTimestampedBackupPerBatch);
            Run("T48_P0_6_P0_7_SelectiveImportSubagentClosureAndParentIdInSqlite", T48_P0_6_P0_7_SelectiveImportSubagentClosureAndParentIdInSqlite);
            Run("T49_P1_1_NotFullyIdleLockedVsActionableSeparation", T49_P1_1_NotFullyIdleLockedVsActionableSeparation);
            Run("T50_P1_2_SlashCommandStripCustomBracketPreserveAndHyphenDedup", T50_P1_2_SlashCommandStripCustomBracketPreserveAndHyphenDedup);
            Run("T51_P1_4_CapabilitiesRelocateFailureReturnsHttp400", T51_P1_4_CapabilitiesRelocateFailureReturnsHttp400);
            Run("T52_P1_6_Fts5ZeroHitLikeFallback", T52_P1_6_Fts5ZeroHitLikeFallback);
            Run("T53_P1_8_ImportedProjectIdRewriteAndOrphanPbtxtExcluded", T53_P1_8_ImportedProjectIdRewriteAndOrphanPbtxtExcluded);
            Run("T54_P1_10_SseDoneEventOkFalseOnImportError", T54_P1_10_SseDoneEventOkFalseOnImportError);
            Run("T55_P1_11_BusyJob409DoesNotKillIde", T55_P1_11_BusyJob409DoesNotKillIde);
            Run("T56_P2_1_PortScopedCookieIsolation", T56_P2_1_PortScopedCookieIsolation);
            Run("T57_P2_5_AsciiStatusLineAndExplorerPathTraversalRejected", T57_P2_5_AsciiStatusLineAndExplorerPathTraversalRejected);
            Run("T58_Stage2_ConfigJsonUserSettingsPreserveUnknownKeysAndEnums", T58_Stage2_ConfigJsonUserSettingsPreserveUnknownKeysAndEnums);
            Run("T59_Stage2_PluginMcpHookToggleRoundTripAndBackups", T59_Stage2_PluginMcpHookToggleRoundTripAndBackups);
            Run("T60_Stage2_FullOfficialScannerManifestsHooksWorkspaceAndMcpConflicts", T60_Stage2_FullOfficialScannerManifestsHooksWorkspaceAndMcpConflicts);
            Run("T61_Stage2_McpHealthDoctorStdioMockMissingAndTimeoutCleanup", T61_Stage2_McpHealthDoctorStdioMockMissingAndTimeoutCleanup);
            Run("T62_Stage2_InlineEditorStrictAllowlistSymlinkAndTokenBudget", T62_Stage2_InlineEditorStrictAllowlistSymlinkAndTokenBudget);
            Run("T63_Stage2_CockpitHttpApiEndToEndAndWorkspaceLaunchValidation", T63_Stage2_CockpitHttpApiEndToEndAndWorkspaceLaunchValidation);
            Run("T64_Stage3_StorageAnalyze_ByteAccountingAndTableBreakdown", T64_Stage3_StorageAnalyze_ByteAccountingAndTableBreakdown);
            Run("T65_Stage3_StorageSlim_Mode5CLiposuctionAndMode5AColdArchivePrune", T65_Stage3_StorageSlim_Mode5CLiposuctionAndMode5AColdArchivePrune);
            Run("T66_Stage3_BackupTierFastDailyVsFullForensicAndRestoreUserSettings", T66_Stage3_BackupTierFastDailyVsFullForensicAndRestoreUserSettings);
            Run("T67_Stage3_AntigravityCliAdapter_DiscoverParseTimeoutAndMissing", T67_Stage3_AntigravityCliAdapter_DiscoverParseTimeoutAndMissing);
            Run("T68_Stage3_HttpStorageAndCliEndpointsEndToEnd", T68_Stage3_HttpStorageAndCliEndpointsEndToEnd);
            Run("T69_Stage3_LegacyCliArchivePruneAndCompleteOrphanGc", T69_Stage3_LegacyCliArchivePruneAndCompleteOrphanGc);
            Run("T70_Stage4_ScheduledSnapshotService_SettingsPersistenceRunAndRollingCleanup", T70_Stage4_ScheduledSnapshotService_SettingsPersistenceRunAndRollingCleanup);
            Run("T71_Stage4_HttpCompanionSettingsAndSnapshotEndpointsEndToEnd", T71_Stage4_HttpCompanionSettingsAndSnapshotEndpointsEndToEnd);
            Run("T72_Stage4_HttpEmbeddedUiAssetsServed", T72_Stage4_HttpEmbeddedUiAssetsServed);
            Run("T73_Stage4_TrayHost_HotkeyParserAndMenuState", T73_Stage4_TrayHost_HotkeyParserAndMenuState);
            Run("T74_Stage4_CompanionCommandDispatcher_RegistrationParity", T74_Stage4_CompanionCommandDispatcher_RegistrationParity);
            Run("T75_Wave1_Remediation_SecurityJobObjectAndDefaults", T75_Wave1_Remediation_SecurityJobObjectAndDefaults);
            Run("T76_ACC02_JobObjectRepeatedDrainAndTimeoutCleanup", T76_ACC02_JobObjectRepeatedDrainAndTimeoutCleanup);
            Run("T77_ACC03_ScheduledSnapshotMutationLockAndIdeSkip", T77_ACC03_ScheduledSnapshotMutationLockAndIdeSkip);
            Run("T78_ACC04_CockpitSettingsAndToggleIdeRunningWarnings", T78_ACC04_CockpitSettingsAndToggleIdeRunningWarnings);
            Run("T79_ACC05_ACC09_StartupRollbackAndHardenedDefaults", T79_ACC05_ACC09_StartupRollbackAndHardenedDefaults);
            Run("T80_ACC06_ACC10_SecurityNormalizationAndTokenBudget", T80_ACC06_ACC10_SecurityNormalizationAndTokenBudget);
            Run("T81_Phase1_IdeCloseTimeoutEscapeHatchAndStorageSlimGuard", T81_Phase1_IdeCloseTimeoutEscapeHatchAndStorageSlimGuard);
            Run("T82_Phase1_ReleaseJobBroadcastsProgressEndFrame", T82_Phase1_ReleaseJobBroadcastsProgressEndFrame);
            Run("T83_Phase1_PerVaultSyncBaselineIsolation", T83_Phase1_PerVaultSyncBaselineIsolation);
            Run("T84_Phase1_ColdArchiveReadableAndRestorableEndToEnd", T84_Phase1_ColdArchiveReadableAndRestorableEndToEnd);
            Run("T85_Phase2_UnifiedSoftDeleteTrashAndRestoreEndToEnd", T85_Phase2_UnifiedSoftDeleteTrashAndRestoreEndToEnd);
            Run("T86_Phase2_ConfigBackupVersionHistoryAndRestore", T86_Phase2_ConfigBackupVersionHistoryAndRestore);
            Run("T87_Phase2_OnlineHotBackupSmartAutoFlushAndLiveSse", T87_Phase2_OnlineHotBackupSmartAutoFlushAndLiveSse);
            Run("T88_Phase2_TrayDebounceUnifiedOrphanEngineAndUiParity", T88_Phase2_TrayDebounceUnifiedOrphanEngineAndUiParity);
            Run("T89_Phase2_ReviewHardening_TrashVaultSafetySubdirWatchAndShellMatch", T89_Phase2_ReviewHardening_TrashVaultSafetySubdirWatchAndShellMatch);

            sw.Stop();
            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine(string.Format("  Summary: {0} Passed, {1} Failed ({2} Assertions, {3:F1}ms)",
                _passed, _failed, _assertions, sw.Elapsed.TotalMilliseconds));
            Console.WriteLine("===============================================================================");

            return _failed == 0 ? 0 : 1;
        }

        private static void Run(string name, Action action)
        {
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                action();
                sw.Stop();
                _passed++;
                Console.WriteLine(string.Format("  [PASS] {0,-56} ({1:F1}ms)", name, sw.Elapsed.TotalMilliseconds));
            }
            catch (Exception ex)
            {
                sw.Stop();
                _failed++;
                Console.WriteLine(string.Format("  [FAIL] {0,-56} ({1:F1}ms)\n         {2}", name, sw.Elapsed.TotalMilliseconds, ex.ToString()));
            }
        }

        private static void AssertTrue(bool cond, string msg)
        {
            _assertions++;
            if (!cond) throw new Exception("AssertTrue failed: " + msg);
        }

        private static void AssertEqual<T>(T expected, T actual, string msg)
        {
            _assertions++;
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception(string.Format("{0} (Expected: '{1}', Actual: '{2}')", msg, expected, actual));
        }

        private static ProtoNode BuildRealProtoEntry(string cid, string autoTitle, string customTitle, string folderUri, string projName, string projId, int steps, int userInteractions, int? field23)
        {
            ProtoNode entry = new ProtoNode(1, 2);
            entry.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(cid), IsModified = true });

            ProtoNode meta = new ProtoNode(2, 2) { IsModified = true };
            if (!string.IsNullOrEmpty(autoTitle))
                meta.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(autoTitle), IsModified = true });
            meta.Children.Add(new ProtoNode(2, 0) { VarintValue = (ulong)steps, IsModified = true });

            ProtoNode ts = new ProtoNode(3, 2) { IsModified = true };
            ts.Children.Add(new ProtoNode(1, 0) { VarintValue = 1756000000UL, IsModified = true });
            meta.Children.Add(ts);

            if (!string.IsNullOrEmpty(folderUri))
            {
                ProtoNode ws = new ProtoNode(9, 2) { IsModified = true };
                ws.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(folderUri), IsModified = true });
                meta.Children.Add(ws);
            }

            if (!string.IsNullOrEmpty(customTitle))
            {
                ProtoNode ct = new ProtoNode(15, 2) { IsModified = true };
                ct.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(customTitle), IsModified = true });
                meta.Children.Add(ct);
            }

            meta.Children.Add(new ProtoNode(16, 0) { VarintValue = (ulong)userInteractions, IsModified = true });

            if (!string.IsNullOrEmpty(projName) || !string.IsNullOrEmpty(projId) || !string.IsNullOrEmpty(folderUri))
            {
                ProtoNode cascade = new ProtoNode(17, 2) { IsModified = true };
                if (!string.IsNullOrEmpty(projName))
                    cascade.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(projName), IsModified = true });
                if (!string.IsNullOrEmpty(folderUri))
                    cascade.Children.Add(new ProtoNode(7, 2) { RawBytes = Encoding.UTF8.GetBytes(folderUri), IsModified = true });
                if (!string.IsNullOrEmpty(projId))
                    cascade.Children.Add(new ProtoNode(18, 2) { RawBytes = Encoding.UTF8.GetBytes(projId), IsModified = true });
                meta.Children.Add(cascade);
            }

            if (field23.HasValue)
            {
                meta.Children.Add(new ProtoNode(23, 0) { VarintValue = (ulong)field23.Value, IsModified = true });
            }

            entry.Children.Add(meta);
            entry.IsModified = true;
            return entry;
        }

        private static void T01_ProtoCodec_RoundtripAndVarintBoundary()
        {
            List<ProtoNode> nodes = new List<ProtoNode>
            {
                new ProtoNode(1, 0) { VarintValue = ulong.MaxValue },
                new ProtoNode(2, 1) { RawBytes = BitConverter.GetBytes(0x1122334455667788UL) },
                new ProtoNode(3, 2) { RawBytes = Encoding.UTF8.GetBytes("中文测试_UTF8_🚀") },
                new ProtoNode(4, 5) { RawBytes = BitConverter.GetBytes(0x12345678U) }
            };

            byte[] encoded = ProtoCodec.Encode(nodes);
            List<ProtoNode> decoded = ProtoCodec.Decode(encoded);

            AssertEqual(4, decoded.Count, "Decoded node count");
            AssertEqual(ulong.MaxValue, decoded[0].VarintValue, "Max UInt64 varint roundtrip");
            AssertEqual("中文测试_UTF8_🚀", decoded[2].GetString(), "UTF-8 string roundtrip");
        }

        private static void T02_SessionMetadataExtractor_RealProtoCustomTitle()
        {
            string cid = "11111111-2222-3333-4444-555555555555";
            ProtoNode entry = BuildRealProtoEntry(
                cid, "Initial English Summary", "【自定义中文标题】BMS架构精简",
                "file:///c%3A/Workspace/BMS_App", "BMS_App", "proj-uuid-1", 28, 5, 1
            );

            byte[] pb = ProtoCodec.Encode(new List<ProtoNode> { entry });
            List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(pb);

            AssertEqual(1, list.Count, "Should extract 1 conversation");
            AssertEqual(cid, list[0].ConversationId, "ConversationId match");
            AssertEqual("【自定义中文标题】BMS架构精简", list[0].DisplayTitle, "CustomTitle takes priority");
            AssertTrue(list[0].HasCustomTitle, "HasCustomTitle flag");
            AssertEqual("BMS_App", list[0].ProjectName, "ProjectName extracted");
            AssertEqual(28, list[0].StepCount, "StepCount extracted");
            AssertTrue(!list[0].IsSubagent, "Main session is not subagent");
        }

        private static void T03_SessionMetadataExtractor_RejectsFlatInvalidEntry()
        {
            // Flat node without Field 2 submessage should be rejected
            ProtoNode flat = new ProtoNode(1, 2) { IsModified = true };
            flat.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes("11111111-2222-3333-4444-555555555555"), IsModified = true });
            byte[] pb = ProtoCodec.Encode(new List<ProtoNode> { flat });

            List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(pb);
            AssertEqual(0, list.Count, "Flat non-real proto entry must be ignored");
        }

        private static void T04_SmartProjectFilter_JunkAndSubagentIsolation()
        {
            AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("subagent-reviewer-1"), "subagent-* is junk");
            AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("Recovered project"), "Recovered project is junk");
            AssertTrue(SmartProjectFilter.IsJunkOrEphemeral("default-cli-project"), "default-cli-project is junk");
            AssertTrue(!SmartProjectFilter.IsJunkOrEphemeral("Outside of Project"), "Outside of Project is preserved");
            AssertTrue(!SmartProjectFilter.IsJunkOrEphemeral("Antigravity_Companion"), "Normal project is preserved");

            ProtoNode subEntry = BuildRealProtoEntry(
                Guid.NewGuid().ToString(), "Subagent task", null,
                "file:///c:/Tools/App", "App", "p1", 6, 0, 0
            );
            byte[] pb = ProtoCodec.Encode(new List<ProtoNode> { subEntry });
            List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(pb);
            AssertEqual(1, list.Count, "Parsed 1 entry");
            AssertTrue(list[0].IsSubagent, "Field 23 == 0 identified as subagent");
        }

        private static void T05_StreamingZipIndexReader_InMemoryInspection()
        {
            string tempZip = Path.Combine(Path.GetTempPath(), "agy_test_" + Guid.NewGuid().ToString("N") + ".agy");
            try
            {
                string cid = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
                ProtoNode entry = BuildRealProtoEntry(
                    cid, "Auto title", "流式归档测试会话",
                    "file:///d:/OldPC/MyProj", "MyProj", "proj-01", 10, 2, 1
                );
                byte[] pbBytes = ProtoCodec.Encode(new List<ProtoNode> { entry });

                using (FileStream fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write))
                using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    using (StreamWriter sw = new StreamWriter(zip.CreateEntry("metadata.json").Open(), Encoding.UTF8))
                    {
                        sw.Write("{\"source_home\":\"D:\\\\Users\\\\OldUser\",\"version\":\"0.6\"}");
                    }
                    using (StreamWriter sw = new StreamWriter(zip.CreateEntry("data/config/projects/proj-01.json").Open(), Encoding.UTF8))
                    {
                        sw.Write("{\"id\":\"proj-01\",\"name\":\"MyProj\",\"folderUri\":\"file:///d:/OldPC/MyProj\"}");
                    }
                    using (Stream s = zip.CreateEntry("data/antigravity/agyhub_summaries_proto.pb").Open())
                    {
                        s.Write(pbBytes, 0, pbBytes.Length);
                    }
                    using (StreamWriter sw = new StreamWriter(zip.CreateEntry("data/antigravity/conversations/" + cid + ".db").Open(), Encoding.UTF8))
                    {
                        sw.Write("SQLITE_DUMMY");
                    }
                    using (StreamWriter sw = new StreamWriter(zip.CreateEntry("data/.geminirules").Open(), Encoding.UTF8))
                    {
                        sw.Write("# Rules");
                    }
                }

                BackupArchiveIndex idx = StreamingZipIndexReader.ReadIndex(tempZip);
                AssertEqual("D:\\Users\\OldUser", idx.SourceHome, "SourceHome parsed");
                AssertEqual(1, idx.Projects.Count, "1 project parsed");
                AssertEqual(1, idx.Conversations.Count, "1 conversation parsed");
                AssertEqual("流式归档测试会话", idx.Conversations[0].DisplayTitle, "Conversation title in zip");
                AssertTrue(idx.ExistingConvoDbFiles.Contains(cid), "DB file detected");
                AssertTrue(idx.HasRules, "Global rules detected");
            }
            finally
            {
                if (File.Exists(tempZip)) File.Delete(tempZip);
            }
        }

        private static void T06_VisualDiffEngine_DiffAndCrossDriveMerge()
        {
            string sharedCid = "11111111-1111-1111-1111-111111111111";
            string newCid = "22222222-2222-2222-2222-222222222222";

            List<ConversationSummaryInfo> localList = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo
                {
                    ConversationId = sharedCid,
                    DisplayTitle = "已对齐会话",
                    ProjectName = "CrossDriveProj",
                    FolderUri = "file:///c:/Users/NewPC/CrossDriveProj",
                    StepCount = 10,
                    UpdatedTime = new DateTime(2026, 8, 25, 12, 0, 0)
                }
            };

            // Same project on different drive in backup: should NOT be flagged as conflicted,
            // and should accurately reflect new conversation status.
            List<ConversationSummaryInfo> backupList = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo
                {
                    ConversationId = sharedCid,
                    DisplayTitle = "已对齐会话",
                    ProjectName = "CrossDriveProj",
                    FolderUri = "file:///d:/Workspace/OldPC/CrossDriveProj",
                    StepCount = 10,
                    UpdatedTime = new DateTime(2026, 8, 25, 12, 0, 0)
                },
                new ConversationSummaryInfo
                {
                    ConversationId = newCid,
                    DisplayTitle = "备份新增会话",
                    ProjectName = "CrossDriveProj",
                    FolderUri = "file:///d:/Workspace/OldPC/CrossDriveProj",
                    StepCount = 5,
                    UpdatedTime = new DateTime(2026, 8, 25, 13, 0, 0)
                }
            };

            List<ProjectDiffNode> diff = VisualDiffEngine.ComputeDiff(localList, backupList);
            AssertEqual(1, diff.Count, "1 merged project diff node");
            AssertEqual(DiffStatus.NewInBackup, diff[0].Status, "Cross-drive project with new session is NewInBackup");
            AssertEqual(1, diff[0].NewSessionCount, "1 new session detected");
        }

        private static void T07_IncrementalProtoShardMerger_LosslessMergeAndUriRemap()
        {
            string localCid = "aaaa1111-1111-1111-1111-111111111111";
            string backupCid = "bbbb2222-2222-2222-2222-222222222222";

            ProtoNode localEntry = BuildRealProtoEntry(
                localCid, "Local Auto", "本地原有会话",
                "file:///c:/Users/NewUser/Desktop/ProjA", "ProjA", "local-proj-id", 12, 3, 1
            );
            byte[] localPb = ProtoCodec.Encode(new List<ProtoNode> { localEntry });

            ProtoNode backupEntry = BuildRealProtoEntry(
                backupCid, "Backup Auto", "跨盘符导入会话",
                "file:///d:/OldDrive/Work/ProjA", "ProjA", "old-proj-id", 20, 4, 1
            );
            byte[] backupPb = ProtoCodec.Encode(new List<ProtoNode> { backupEntry });
            List<ConversationSummaryInfo> backupInfos = SessionMetadataExtractor.ExtractSummariesFromProto(backupPb);

            Dictionary<string, string> uriOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            uriOverrides["ProjA"] = "file:///c:/Users/NewUser/Desktop/ProjA";

            Dictionary<string, string> projNewIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            projNewIds["ProjA"] = "local-proj-id";

            byte[] mergedPb = IncrementalProtoShardMerger.MergeShards(
                localPb, backupInfos, "C:\\Users\\OldUser", "C:\\Users\\NewUser", uriOverrides, projNewIds
            );

            List<ConversationSummaryInfo> mergedList = SessionMetadataExtractor.ExtractSummariesFromProto(mergedPb);
            AssertEqual(2, mergedList.Count, "Merged pb contains both local and imported sessions");

            ConversationSummaryInfo imported = mergedList.Find(x => x.ConversationId == backupCid);
            AssertTrue(imported != null, "Imported session found");
            AssertEqual("跨盘符导入会话", imported.DisplayTitle, "Imported custom title preserved");
            AssertEqual("file:///c:/Users/NewUser/Desktop/ProjA", imported.FolderUri, "Cross-drive FolderUri remapped to local project URI");
            AssertEqual("local-proj-id", imported.ProjectId, "ProjectId synchronized to local project ID");
        }

        private static void T08_DirectStreamArchiver_MultiFormatPathRemap()
        {
            string src = "C:\\Users\\Alice";
            string dst = "D:\\Users\\Bob";
            string input = "p1=C:\\Users\\Alice\\Work; p2=C:/Users/Alice/Work; p3=C:\\\\Users\\\\Alice\\\\Work; p4=file:///c%3A%2FUsers%2FAlice/Work; p5=file:///C%3a%5cUsers%5cAlice; p6=file:///c%3A/Users/Alice/Work; p7=C:\\Users\\Alice_Twin\\Work";
            string output = DirectStreamArchiver.RemapAllPathFormats(input, src, dst);

            AssertTrue(!output.Contains("Alice\\Work") && !output.Contains("Alice/Work"), "All 6 path encodings remapped from Alice to Bob");
            AssertTrue(output.Contains("D:\\Users\\Bob\\Work"), "Windows backslash remapped");
            AssertTrue(output.Contains("D:/Users/Bob/Work"), "Forward slash remapped");
            AssertTrue(output.Contains("D:\\\\Users\\\\Bob\\\\Work"), "JSON escaped backslash remapped");
            AssertTrue(output.Contains("file:///d%3A/Users/Bob/Work"), "VSCode colon-encoded canonical lowercase-drive URI remapped");
            AssertTrue(output.Contains("C:\\Users\\Alice_Twin\\Work"), "Boundary-safe prefix avoids corrupting Alice_Twin");
        }

        private static void T09_F1_CascadingDeleteSessionProjectAndSubagents()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_f1_del_" + Guid.NewGuid().ToString("N"));
            try
            {
                string convDir = Path.Combine(sb, "antigravity", "conversations");
                string brainDir = Path.Combine(sb, "antigravity", "brain");
                string projDir = Path.Combine(sb, "config", "projects");
                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(brainDir);
                Directory.CreateDirectory(projDir);

                string cidInvert = "11111111-1111-1111-1111-111111111111";
                string cidAdmin = "22222222-2222-2222-2222-222222222222";
                string cidSub = "33333333-3333-3333-3333-333333333333";
                string cidKeep = "44444444-4444-4444-4444-444444444444";

                ProtoNode nInvert = BuildRealProtoEntry(cidInvert, "Battery Priority Inverter Logic", null, "file:///c:/InvertProject", "InvertProject", "proj-invert", 10, 2, 1);
                ProtoNode nAdmin = BuildRealProtoEntry(cidAdmin, "hi", null, "file:///c:/Users/Administrator", "Administrator", "outside-of-project", 4, 1, 1);
                ProtoNode nSub = BuildRealProtoEntry(cidSub, "Subagent Research", null, "file:///c:/Users/Administrator", "Administrator", "outside-of-project", 6, 0, 0);
                // Set ParentConversationId (Field 17 -> Field 11) on nSub to cidAdmin
                ProtoNode subMeta = nSub.GetChild(2);
                ProtoNode subCascade = subMeta.GetChild(17);
                subCascade.Children.Add(new ProtoNode(11, 2) { RawBytes = Encoding.UTF8.GetBytes(cidAdmin), IsModified = true });

                ProtoNode nKeep = BuildRealProtoEntry(cidKeep, "Keep Session", null, "file:///c:/KeepProject", "KeepProject", "proj-keep", 15, 3, 1);

                string pbPath = Path.Combine(sb, "antigravity", "agyhub_summaries_proto.pb");
                byte[] initialPb = ProtoCodec.Encode(new List<ProtoNode> { nInvert, nAdmin, nSub, nKeep });
                File.WriteAllBytes(pbPath, initialPb);

                File.WriteAllText(Path.Combine(projDir, "proj-invert.json"), "{\"id\":\"proj-invert\",\"name\":\"InvertProject\",\"folderUri\":\"file:///c:/InvertProject\"}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(projDir, "proj-keep.json"), "{\"id\":\"proj-keep\",\"name\":\"KeepProject\",\"folderUri\":\"file:///c:/KeepProject\"}", Encoding.UTF8);

                string[] allCids = new string[] { cidInvert, cidAdmin, cidSub, cidKeep };
                for (int i = 0; i < allCids.Length; i++)
                {
                    File.WriteAllText(Path.Combine(convDir, allCids[i] + ".db"), "DUMMY_SQLITE_DATA", Encoding.UTF8);
                    File.WriteAllText(Path.Combine(convDir, allCids[i] + ".db-wal"), "DUMMY_WAL", Encoding.UTF8);
                    string b = Path.Combine(brainDir, allCids[i]);
                    Directory.CreateDirectory(b);
                    File.WriteAllText(Path.Combine(b, "note.md"), "# Note", Encoding.UTF8);
                }

                Dictionary<string, ProjectMetadata> localProjects = new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
                localProjects["proj-invert"] = new ProjectMetadata { Id = "proj-invert", Name = "InvertProject", FolderUri = "file:///c:/InvertProject" };
                localProjects["proj-keep"] = new ProjectMetadata { Id = "proj-keep", Name = "KeepProject", FolderUri = "file:///c:/KeepProject" };

                List<ConversationSummaryInfo> summaries = SessionMetadataExtractor.ExtractSummaries(initialPb, localProjects);
                AssertEqual(4, summaries.Count, "4 initial sessions extracted");

                HashSet<string> targetConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { cidAdmin };
                HashSet<string> targetProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "InvertProject" };

                DeletionResult res = LocalWorkspaceManager.DeleteSessionsAndProjects(sb, summaries, localProjects, targetConvIds, targetProjects);

                AssertEqual(3, res.DeletedSessionCount, "Should delete InvertProject session + cidAdmin + cascaded subagent cidSub");
                AssertEqual(6, res.DeletedDbFilesCount, "Should delete 3 .db and 3 .db-wal files");
                AssertEqual(3, res.DeletedBrainDirsCount, "Should delete 3 brain directories");
                AssertEqual(1, res.DeletedProjectJsonCount, "Should delete proj-invert.json");
                AssertTrue(res.FreedBytes > 0, "FreedBytes > 0");
                AssertTrue(Directory.GetFiles(Path.GetDirectoryName(pbPath), Path.GetFileName(pbPath) + ".bak.*").Length >= 1, ".pb.bak.* timestamped backup created");

                List<ConversationSummaryInfo> remaining = SessionMetadataExtractor.ExtractSummaries(File.ReadAllBytes(pbPath), localProjects);
                AssertEqual(1, remaining.Count, "Only 1 session remains in .pb");
                AssertEqual(cidKeep, remaining[0].ConversationId, "cidKeep preserved in .pb");
                AssertTrue(File.Exists(Path.Combine(convDir, cidKeep + ".db")), "cidKeep.db preserved");
                AssertTrue(Directory.Exists(Path.Combine(brainDir, cidKeep)), "cidKeep brain preserved");
                AssertTrue(File.Exists(Path.Combine(projDir, "proj-keep.json")), "proj-keep.json preserved");
                AssertTrue(!File.Exists(Path.Combine(projDir, "proj-invert.json")), "proj-invert.json deleted");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T10_F1_OrphanGarbageCollector_ScanAndClean()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_f1_gc_" + Guid.NewGuid().ToString("N"));
            try
            {
                string convDir = Path.Combine(sb, "antigravity", "conversations");
                string brainDir = Path.Combine(sb, "antigravity", "brain");
                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(brainDir);

                string cidActive = "aaaaaaaa-1111-1111-1111-111111111111";
                string cidOrphan1 = "bbbbbbbb-2222-2222-2222-222222222222";
                string cidOrphan2 = "cccccccc-3333-3333-3333-333333333333";

                ProtoNode nActive = BuildRealProtoEntry(cidActive, "Active Session", null, "file:///c:/App", "App", "p1", 5, 1, 1);
                string pbPath = Path.Combine(sb, "antigravity", "agyhub_summaries_proto.pb");
                File.WriteAllBytes(pbPath, ProtoCodec.Encode(new List<ProtoNode> { nActive }));

                // Active files
                File.WriteAllText(Path.Combine(convDir, cidActive + ".db"), "ACTIVE_DB", Encoding.UTF8);
                Directory.CreateDirectory(Path.Combine(brainDir, cidActive));

                // Orphan files
                File.WriteAllText(Path.Combine(convDir, cidOrphan1 + ".db"), "ORPHAN1_DB_CONTENT", Encoding.UTF8);
                File.WriteAllText(Path.Combine(convDir, cidOrphan1 + ".db-wal"), "ORPHAN1_WAL", Encoding.UTF8);
                string bOrphan1 = Path.Combine(brainDir, cidOrphan1);
                Directory.CreateDirectory(bOrphan1);
                File.WriteAllText(Path.Combine(bOrphan1, "artifact.md"), "ORPHAN_ARTIFACT", Encoding.UTF8);

                string bOrphan2 = Path.Combine(brainDir, cidOrphan2);
                Directory.CreateDirectory(bOrphan2);
                File.WriteAllText(Path.Combine(bOrphan2, "log.txt"), "ORPHAN2_LOG", Encoding.UTF8);

                OrphanScanResult scan = LocalWorkspaceManager.ScanOrphanedData(sb);
                AssertEqual(2, scan.OrphanSessionIds.Count, "2 orphan session UUIDs detected");
                AssertEqual(2, scan.OrphanDbFiles.Count, "2 orphan db files (.db + .db-wal)");
                AssertEqual(2, scan.OrphanBrainDirs.Count, "2 orphan brain dirs");
                AssertTrue(scan.TotalOrphanBytes > 0, "Orphan byte size calculated");

                DeletionResult cleaned = LocalWorkspaceManager.CleanOrphanedData(scan);
                AssertEqual(2, cleaned.DeletedDbFilesCount, "Cleaned 2 orphan db files");
                AssertEqual(2, cleaned.DeletedBrainDirsCount, "Cleaned 2 orphan brain dirs");
                AssertTrue(File.Exists(Path.Combine(convDir, cidActive + ".db")), "Active DB untouched");
                AssertTrue(Directory.Exists(Path.Combine(brainDir, cidActive)), "Active brain untouched");

                OrphanScanResult reScan = LocalWorkspaceManager.ScanOrphanedData(sb);
                AssertEqual(0, reScan.TotalOrphanItems, "Zero orphans after cleanup");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T11_F2_RenameSession_DualFieldPersistence()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_f2_ren_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(sb, "antigravity"));
                string cid = "12345678-1234-1234-1234-1234567890ab";
                ProtoNode entry = BuildRealProtoEntry(cid, "Original Auto Summary", null, "file:///c:/App", "App", "p1", 8, 2, 1);
                string pbPath = Path.Combine(sb, "antigravity", "agyhub_summaries_proto.pb");
                File.WriteAllBytes(pbPath, ProtoCodec.Encode(new List<ProtoNode> { entry }));

                bool ok = LocalWorkspaceManager.RenameSession(sb, cid, "【F2重命名】三电平逆变器死区补偿");
                AssertTrue(ok, "RenameSession returned true");

                List<ConversationSummaryInfo> list = SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(pbPath));
                AssertEqual(1, list.Count, "1 session after rename");
                AssertEqual("【F2重命名】三电平逆变器死区补偿", list[0].DisplayTitle, "DisplayTitle updated");
                AssertEqual("【F2重命名】三电平逆变器死区补偿", list[0].CustomTitle, "Field 15 CustomTitle updated");
                AssertEqual("【F2重命名】三电平逆变器死区补偿", list[0].AutoSummaryTitle, "Field 1 AutoSummaryTitle synchronized");
                AssertTrue(list[0].HasCustomTitle, "HasCustomTitle is true");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static byte[] ReadTrajectoryMetadataBlobFromSqlite(string dbPath)
        {
            IntPtr h = IntPtr.Zero;
            IntPtr stmt = IntPtr.Zero;
            try
            {
                AssertEqual(0, t_sqlite3_open_v2(Utf8Z(dbPath), out h, 0x00000001, IntPtr.Zero), "Open SQLite DB readonly: " + dbPath);
                AssertEqual(0, t_sqlite3_prepare_v2(h, Utf8Z("SELECT data FROM trajectory_metadata_blob WHERE id='main';"), -1, out stmt, IntPtr.Zero), "Prepare SELECT trajectory_metadata_blob");
                AssertEqual(100, t_sqlite3_step(stmt), "SELECT returns SQLITE_ROW");
                int len = t_sqlite3_column_bytes(stmt, 0);
                IntPtr ptr = t_sqlite3_column_blob(stmt, 0);
                AssertTrue(len > 0 && ptr != IntPtr.Zero, "SQLite trajectory_metadata_blob is non-empty");
                byte[] buf = new byte[len];
                Marshal.Copy(ptr, buf, 0, len);
                return buf;
            }
            finally
            {
                if (stmt != IntPtr.Zero) try { t_sqlite3_finalize(stmt); } catch {}
                if (h != IntPtr.Zero) try { t_sqlite3_close(h); } catch {}
            }
        }

        private static void T12_F2_MoveSessionAndRelocateProjectPath()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_f2_mov_中文_" + Guid.NewGuid().ToString("N"));
            IntPtr dbHandle = IntPtr.Zero;
            IntPtr stmt = IntPtr.Zero;
            try
            {
                Directory.CreateDirectory(Path.Combine(sb, "antigravity", "conversations"));
                Directory.CreateDirectory(Path.Combine(sb, "config", "projects"));

                string cid = "87654321-4321-4321-4321-ba0987654321";
                ProtoNode entry = BuildRealProtoEntry(cid, "Adhoc Session", null, "file:///c:/Users/Administrator", "Administrator", "outside-of-project", 12, 3, 1);
                byte[] origBytes = ProtoCodec.Encode(new List<ProtoNode> { entry });
                string pbPath = Path.Combine(sb, "antigravity", "agyhub_summaries_proto.pb");
                File.WriteAllBytes(pbPath, origBytes);

                // 同时构造真实 SQLite 会话数据库（含中文根路径），验证 Move 与 Relocate 同步更新 .db (E-07)
                string dbPath = Path.Combine(sb, "antigravity", "conversations", cid + ".db");
                AssertEqual(0, t_sqlite3_open_v2(Utf8Z(dbPath), out dbHandle, 0x00000002 | 0x00000004, IntPtr.Zero), "T12 winsqlite3_open_v2 creates local .db under Chinese path");
                AssertEqual(0, t_sqlite3_exec(dbHandle, Utf8Z("CREATE TABLE trajectory_metadata_blob(id TEXT PRIMARY KEY, data BLOB);"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "T12 create trajectory_metadata_blob schema");
                AssertEqual(0, t_sqlite3_prepare_v2(dbHandle, Utf8Z("INSERT INTO trajectory_metadata_blob(id, data) VALUES('main', ?);"), -1, out stmt, IntPtr.Zero), "T12 prepare INSERT blob");
                GCHandle pin = GCHandle.Alloc(origBytes, GCHandleType.Pinned);
                try
                {
                    AssertEqual(0, t_sqlite3_bind_blob(stmt, 1, pin.AddrOfPinnedObject(), origBytes.Length, new IntPtr(-1)), "T12 bind protobuf blob");
                    AssertEqual(101, t_sqlite3_step(stmt), "T12 step INSERT SQLITE_DONE");
                }
                finally
                {
                    if (stmt != IntPtr.Zero) { t_sqlite3_finalize(stmt); stmt = IntPtr.Zero; }
                    if (pin.IsAllocated) pin.Free();
                }
                t_sqlite3_close(dbHandle);
                dbHandle = IntPtr.Zero;

                // 1. Move session from 'Administrator' to 'BMS_Controller'
                int moved = LocalWorkspaceManager.MoveSessionsToProject(
                    sb, new List<string> { cid }, "BMS_Controller", "proj-bms-99", "C:\\Work\\BMS_Controller"
                );
                AssertEqual(1, moved, "1 session moved");
                AssertEqual(0, LocalWorkspaceManager.LastFailedSqliteCids.Count, "No SQLite failure during MoveSessionsToProject");
                AssertTrue(File.Exists(Path.Combine(sb, "config", "projects", "proj-bms-99.json")), "Project JSON auto-created");

                List<ConversationSummaryInfo> listAfterMove = SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(pbPath));
                AssertEqual("BMS_Controller", listAfterMove[0].ProjectName, "ProjectName updated to BMS_Controller");
                AssertEqual("proj-bms-99", listAfterMove[0].ProjectId, "ProjectId updated to proj-bms-99");
                AssertEqual("file:///c%3A/Work/BMS_Controller", listAfterMove[0].FolderUri, "FolderUri updated to canonical VS Code lowercase-drive URI");

                List<ConversationSummaryInfo> dbAfterMove = SessionMetadataExtractor.ExtractSummariesFromProto(ReadTrajectoryMetadataBlobFromSqlite(dbPath));
                AssertEqual(1, dbAfterMove.Count, "SQLite blob after Move decodes cleanly");
                AssertEqual("proj-bms-99", dbAfterMove[0].ProjectId, "SQLite ProjectId updated to proj-bms-99");
                AssertEqual("file:///c%3A/Work/BMS_Controller", dbAfterMove[0].FolderUri, "SQLite FolderUri updated after Move");

                // 2. Relocate 'BMS_Controller' physical path to 'D:\NewDisk\BMS_V2'
                int relocated = LocalWorkspaceManager.RelocateProjectPath(
                    sb, "proj-bms-99", "BMS_Controller", "D:\\NewDisk\\BMS_V2", listAfterMove
                );
                AssertEqual(1, relocated, "1 session relocated");
                AssertEqual(0, LocalWorkspaceManager.LastFailedSqliteCids.Count, "No SQLite failure during RelocateProjectPath");

                List<ConversationSummaryInfo> listAfterReloc = SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(pbPath));
                AssertEqual("file:///d%3A/NewDisk/BMS_V2", listAfterReloc[0].FolderUri, "FolderUri relocated to canonical lowercase-drive URI in .pb");
                string projJson = File.ReadAllText(Path.Combine(sb, "config", "projects", "proj-bms-99.json"), Encoding.UTF8);
                AssertTrue(projJson.Contains("file:///d%3A/NewDisk/BMS_V2"), "Project JSON canonical folderUri updated");

                List<ConversationSummaryInfo> dbAfterReloc = SessionMetadataExtractor.ExtractSummariesFromProto(ReadTrajectoryMetadataBlobFromSqlite(dbPath));
                AssertEqual(1, dbAfterReloc.Count, "SQLite blob after Relocate decodes cleanly");
                AssertEqual("proj-bms-99", dbAfterReloc[0].ProjectId, "SQLite ProjectId preserved after Relocate");
                AssertEqual("file:///d%3A/NewDisk/BMS_V2", dbAfterReloc[0].FolderUri, "SQLite FolderUri updated to D:\\NewDisk\\BMS_V2");
            }
            finally
            {
                if (stmt != IntPtr.Zero) try { t_sqlite3_finalize(stmt); } catch {}
                if (dbHandle != IntPtr.Zero) try { t_sqlite3_close(dbHandle); } catch {}
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T13_F3_ExportSelectiveBackup_SlicedProtoAndRoundtrip()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_f3_exp_" + Guid.NewGuid().ToString("N"));
            string outZip = Path.Combine(Path.GetTempPath(), "agy_f3_selective_" + Guid.NewGuid().ToString("N") + ".agy");
            try
            {
                string convDir = Path.Combine(sb, "antigravity", "conversations");
                string brainDir = Path.Combine(sb, "antigravity", "brain");
                string projDir = Path.Combine(sb, "config", "projects");
                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(brainDir);
                Directory.CreateDirectory(projDir);

                string cid1 = "11110000-0000-0000-0000-000000000001";
                string cid2 = "22220000-0000-0000-0000-000000000002";

                ProtoNode n1 = BuildRealProtoEntry(cid1, "Selected Session", "导出选中会话", "file:///c:/Proj1", "Proj1", "p-1", 10, 2, 1);
                ProtoNode n2 = BuildRealProtoEntry(cid2, "Unselected Session", "未选中会话", "file:///c:/Proj2", "Proj2", "p-2", 20, 4, 1);

                string pbPath = Path.Combine(sb, "antigravity", "agyhub_summaries_proto.pb");
                byte[] fullPb = ProtoCodec.Encode(new List<ProtoNode> { n1, n2 });
                File.WriteAllBytes(pbPath, fullPb);

                File.WriteAllText(Path.Combine(projDir, "p-1.json"), "{\"id\":\"p-1\",\"name\":\"Proj1\",\"folderUri\":\"file:///c:/Proj1\"}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(projDir, "p-2.json"), "{\"id\":\"p-2\",\"name\":\"Proj2\",\"folderUri\":\"file:///c:/Proj2\"}", Encoding.UTF8);

                CreateMinimalValidSqliteDb(Path.Combine(convDir, cid1 + ".db"), cid1);
                CreateMinimalValidSqliteDb(Path.Combine(convDir, cid2 + ".db"), cid2);
                Directory.CreateDirectory(Path.Combine(brainDir, cid1));
                File.WriteAllText(Path.Combine(brainDir, cid1, "plan.md"), "# Plan 1", Encoding.UTF8);
                Directory.CreateDirectory(Path.Combine(brainDir, cid2));
                File.WriteAllText(Path.Combine(brainDir, cid2, "plan.md"), "# Plan 2", Encoding.UTF8);

                Dictionary<string, ProjectMetadata> projects = new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
                projects["p-1"] = new ProjectMetadata { Id = "p-1", Name = "Proj1", FolderUri = "file:///c:/Proj1" };
                projects["p-2"] = new ProjectMetadata { Id = "p-2", Name = "Proj2", FolderUri = "file:///c:/Proj2" };

                List<ConversationSummaryInfo> all = SessionMetadataExtractor.ExtractSummaries(fullPb, projects);
                List<ConversationSummaryInfo> selectedOnly = new List<ConversationSummaryInfo> { all[0] };

                int count = LocalWorkspaceManager.ExportSelectiveBackup(sb, "C:\\Users\\TestUser", outZip, selectedOnly, projects, false);
                AssertTrue(count >= 4, "Exported selective entries count");

                BackupArchiveIndex idx = StreamingZipIndexReader.ReadIndex(outZip);
                AssertEqual(1, idx.Conversations.Count, "Sliced .pb in selective .agy contains ONLY 1 conversation");
                AssertEqual(cid1, idx.Conversations[0].ConversationId, "Selected conversation ID matches");
                AssertEqual("导出选中会话", idx.Conversations[0].DisplayTitle, "Selected custom title preserved");
                AssertEqual(1, idx.Projects.Count, "Only Proj1 config included in selective backup");
                AssertTrue(idx.ExistingConvoDbFiles.Contains(cid1) && !idx.ExistingConvoDbFiles.Contains(cid2), "Only cid1.db packed");
                AssertTrue(idx.ExistingBrainDirs.Contains(cid1) && !idx.ExistingBrainDirs.Contains(cid2), "Only cid1 brain packed");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
                if (File.Exists(outZip)) File.Delete(outZip);
            }
        }

        private static void T14_F4_TranscriptMarkdownExporter_LocalAndBackupZip()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_f4_md_" + Guid.NewGuid().ToString("N"));
            string zipPath = Path.Combine(Path.GetTempPath(), "agy_f4_zip_" + Guid.NewGuid().ToString("N") + ".agy");
            try
            {
                string cid = "99998888-7777-6666-5555-444433332222";
                string logsDir = Path.Combine(sb, "antigravity", "brain", cid, ".system_generated", "logs");
                Directory.CreateDirectory(logsDir);

                string jsonl =
                    "{\"step_index\": 0, \"source\": \"USER_EXPLICIT\", \"type\": \"USER_INPUT\", \"created_at\": \"2026-11-06T06:00:00Z\", \"content\": \"<USER_REQUEST>\\n请实现会话删除与Markdown导出功能\\n</USER_REQUEST>\\n<ADDITIONAL_METADATA>noise_to_strip</ADDITIONAL_METADATA>\"}\n" +
                    "{\"step_index\": 5, \"source\": \"MODEL\", \"type\": \"PLANNER_RESPONSE\", \"created_at\": \"2026-11-06T06:01:00Z\", \"thinking\": \"正在设计 Protobuf AST 节点过滤与 JSONL 解析器\", \"content\": \"已完成 **F1-F4** 全部核心模块开发。\"}";

                string transcriptFile = Path.Combine(logsDir, "transcript.jsonl");
                File.WriteAllText(transcriptFile, jsonl, Encoding.UTF8);

                string artifactFile = Path.Combine(sb, "antigravity", "brain", cid, "implementation_plan.md");
                File.WriteAllText(artifactFile, "# Implementation Plan\n- Feature F1\n- Feature F4", Encoding.UTF8);

                ConversationSummaryInfo session = new ConversationSummaryInfo
                {
                    ConversationId = cid,
                    DisplayTitle = "F4对话导出测试会话",
                    ProjectName = "Antigravity_Companion",
                    FolderUri = "file:///c:/Tools/Antigravity_Companion",
                    UpdatedTime = new DateTime(2026, 11, 6, 15, 0, 0),
                    StepCount = 6,
                    UserInteractions = 1
                };

                // 1. Test Local Export to Markdown
                string localMdOut = Path.Combine(sb, "exported_local.md");
                string mdLocal = TranscriptMarkdownExporter.ExportLocalSessionToMarkdown(session, sb, localMdOut);
                AssertTrue(File.Exists(localMdOut), "Local Markdown file created");
                AssertTrue(mdLocal.Contains("F4对话导出测试会话"), "Contains session title");
                AssertTrue(mdLocal.Contains("请实现会话删除与Markdown导出功能"), "Contains user request");
                AssertTrue(!mdLocal.Contains("noise_to_strip"), "Stripped <ADDITIONAL_METADATA> noise");
                AssertTrue(mdLocal.Contains("正在设计 Protobuf AST 节点过滤与 JSONL 解析器"), "Contains model thinking");
                AssertTrue(mdLocal.Contains("已完成 **F1-F4** 全部核心模块开发。"), "Contains model response");
                AssertTrue(mdLocal.Contains("# Implementation Plan"), "Contains brain artifact markdown");

                // 2. Test Direct Streaming Export from Backup .agy Zip (without importing!)
                using (FileStream fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
                using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    using (StreamWriter sw = new StreamWriter(zip.CreateEntry("data/antigravity/brain/" + cid + "/.system_generated/logs/transcript.jsonl").Open(), Encoding.UTF8))
                    {
                        sw.Write(jsonl);
                    }
                    using (StreamWriter sw = new StreamWriter(zip.CreateEntry("data/antigravity/brain/" + cid + "/implementation_plan.md").Open(), Encoding.UTF8))
                    {
                        sw.Write("# Implementation Plan\n- Feature F1\n- Feature F4");
                    }
                }

                string zipMdOut = Path.Combine(sb, "exported_from_zip.md");
                string mdZip = TranscriptMarkdownExporter.ExportBackupSessionToMarkdown(session, zipPath, zipMdOut);
                AssertTrue(File.Exists(zipMdOut), "Backup Zip Markdown file created");
                AssertTrue(mdZip.Contains("请实现会话删除与Markdown导出功能"), "Zip stream Markdown contains user request");
                AssertTrue(mdZip.Contains("已完成 **F1-F4** 全部核心模块开发。"), "Zip stream Markdown contains model response");
                AssertTrue(mdZip.Contains("# Implementation Plan"), "Zip stream Markdown contains artifact");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
                if (File.Exists(zipPath)) File.Delete(zipPath);
            }
        }

        private static void T15_V07_CapabilityScanner_FiveCategoriesAndJsonKeyLexer()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_v07_scan_" + Guid.NewGuid().ToString("N"));
            string homeA = "C:\\Users\\Alice";
            try
            {
                string geminiA = Path.Combine(sb, ".gemini");
                Directory.CreateDirectory(geminiA);

                // 1. Root Rule: GEMINI.md + config/rules/coding-style.md
                File.WriteAllText(Path.Combine(geminiA, "GEMINI.md"), "# Global Rule\nAlways use UTF-8.", Encoding.UTF8);
                Directory.CreateDirectory(Path.Combine(geminiA, "config", "rules"));
                File.WriteAllText(Path.Combine(geminiA, "config", "rules", "coding-style.md"), "# Coding Style\nNo tabs.", Encoding.UTF8);

                // 2. Plugin: codegraph + config.json registration
                string pDir = Path.Combine(geminiA, "config", "plugins", "codegraph");
                Directory.CreateDirectory(Path.Combine(pDir, "rules"));
                File.WriteAllText(Path.Combine(pDir, "plugin.json"), "{\"name\":\"codegraph\",\"displayName\":\"CodeGraph Indexer\",\"description\":\"AST graph plugin\"}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(pDir, "rules", "AGENTS.md"), "# CodeGraph Rule", Encoding.UTF8);
                File.WriteAllText(Path.Combine(geminiA, "config", "config.json"), "{\"plugins\":{\"codegraph\":{\"enabled\":true,\"path\":\"C:/Users/Alice/.gemini/config/plugins/codegraph\"}}}", Encoding.UTF8);

                // 3. Standalone Skill: my-custom-skill
                string sDir = Path.Combine(geminiA, "config", "skills", "my-custom-skill");
                Directory.CreateDirectory(sDir);
                File.WriteAllText(Path.Combine(sDir, "SKILL.md"), "---\nname: my-custom-skill\ndescription: Custom verification skill\n---\n# Instructions", Encoding.UTF8);

                // 4. MCP Servers: 2 servers in mcp_config.json + schema dir for evo-kernel
                string mcpJson = "{\r\n" +
                    "  \"mcpServers\": {\r\n" +
                    "    \"evo-kernel\": {\r\n" +
                    "      \"command\": \"node\",\r\n" +
                    "      \"args\": [\"C:/Users/Alice/.gemini/evo-kernel/dist/index.js\"]\r\n" +
                    "    },\r\n" +
                    "    \"sqlite-explorer\": {\r\n" +
                    "      \"command\": \"npx\",\r\n" +
                    "      \"args\": [\"-y\", \"@modelcontextprotocol/server-sqlite\", \"-- note: { braces } and \\\"quotes\\\" inside string\"]\r\n" +
                    "    }\r\n" +
                    "  }\r\n" +
                    "}";
                File.WriteAllText(Path.Combine(geminiA, "config", "mcp_config.json"), mcpJson, Encoding.UTF8);
                string evoSchemaDir = Path.Combine(geminiA, "antigravity", "mcp", "evo-kernel");
                Directory.CreateDirectory(evoSchemaDir);
                File.WriteAllText(Path.Combine(evoSchemaDir, "evo_search.json"), "{\"name\":\"evo_search\"}", Encoding.UTF8);

                // 5. Hooks: 1 hook in hooks.json + companion script in config/
                string hookScriptPath = Path.Combine(geminiA, "config", "evo_antigravity_hook.mjs");
                File.WriteAllText(hookScriptPath, "console.log('hook running');", Encoding.UTF8);
                string hooksJson = "{\r\n" +
                    "  \"evo-evolution-reminder\": {\r\n" +
                    "    \"PreInvocation\": [\"node \\\"C:/Users/Alice/.gemini/config/evo_antigravity_hook.mjs\\\" PreInvocation\"]\r\n" +
                    "  }\r\n" +
                    "}";
                File.WriteAllText(Path.Combine(geminiA, "config", "hooks.json"), hooksJson, Encoding.UTF8);

                List<CapabilityItem> caps = CapabilityRepositoryManager.ScanLocalCapabilities(geminiA, homeA);
                AssertEqual(7, caps.Count, "Should scan 7 distinct capability items (1 plugin, 1 skill, 2 rules, 2 MCPs, 1 hook)");

                CapabilityItem mcpEvo = caps.Find(x => x.Kind == CapabilityKind.McpServer && x.Id == "evo-kernel");
                AssertTrue(mcpEvo != null, "evo-kernel MCP scanned");
                AssertTrue(mcpEvo.RawJsonFragment.Contains("{{HOME}}/.gemini/evo-kernel/dist/index.js"), "MCP path normalized to {{HOME}}");
                AssertTrue(mcpEvo.FilesMap.ContainsKey("schemas/evo_search.json"), "MCP schema bundled");

                CapabilityItem mcpSqlite = caps.Find(x => x.Kind == CapabilityKind.McpServer && x.Id == "sqlite-explorer");
                AssertTrue(mcpSqlite != null, "sqlite-explorer MCP with braces/quotes in string literal parsed cleanly");

                CapabilityItem hookEvo = caps.Find(x => x.Kind == CapabilityKind.Hook && x.Id == "evo-evolution-reminder");
                AssertTrue(hookEvo != null, "evo-evolution-reminder Hook scanned");
                AssertEqual(1, hookEvo.CompanionScriptNames.Count, "Companion script evo_antigravity_hook.mjs auto-bundled");
                AssertTrue(hookEvo.FilesMap.ContainsKey("scripts/evo_antigravity_hook.mjs"), "Hook script stored in FilesMap");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T16_V07_RepositoryPushAndSelectiveImport_KeyLevelMerge()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_v07_merge_" + Guid.NewGuid().ToString("N"));
            string homeA = "C:\\Users\\MachineA";
            string homeB = "D:\\Users\\MachineB";
            try
            {
                string geminiA = Path.Combine(sb, "MachineA_gemini");
                string repoDir = Path.Combine(sb, "Repository");
                string geminiB = Path.Combine(sb, "MachineB_gemini");
                Directory.CreateDirectory(Path.Combine(geminiA, "config"));
                Directory.CreateDirectory(Path.Combine(geminiB, "config"));

                // Machine A has MCP "evo-kernel" and Hook "evo-hook"
                File.WriteAllText(Path.Combine(geminiA, "config", "mcp_config.json"),
                    "{\"mcpServers\":{\"evo-kernel\":{\"command\":\"node\",\"args\":[\"C:/Users/MachineA/.gemini/evo/index.js\"]}}}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(geminiA, "config", "evo_hook.mjs"), "// hook script from MachineA", Encoding.UTF8);
                File.WriteAllText(Path.Combine(geminiA, "config", "hooks.json"),
                    "{\"evo-hook\":{\"PreInvocation\":[\"node \\\"C:/Users/MachineA/.gemini/config/evo_hook.mjs\\\"\"]}}", Encoding.UTF8);

                // Push Machine A's capabilities into plain-text Repository/
                List<CapabilityItem> capsA = CapabilityRepositoryManager.ScanLocalCapabilities(geminiA, homeA);
                AssertEqual(2, capsA.Count, "2 capabilities on Machine A");
                int pushed = CapabilityRepositoryManager.PushCapabilitiesToRepository(repoDir, homeA, capsA);
                AssertTrue(pushed >= 3, "Pushed MCP + Hook + script into Repository/");
                AssertTrue(File.Exists(Path.Combine(repoDir, "manifest.json")), "Repository manifest.json created");

                string repoMcpEntry = File.ReadAllText(Path.Combine(repoDir, "mcp", "evo-kernel", "server_entry.json"), Encoding.UTF8);
                AssertTrue(repoMcpEntry.Contains("{{HOME}}"), "Repository server_entry.json uses {{HOME}} placeholder");
                AssertTrue(!repoMcpEntry.Contains("MachineA"), "Repository server_entry.json stripped MachineA username");

                // Machine B ALREADY has its own MCP "existing-mcp" and its own Hook "existing-hook"
                File.WriteAllText(Path.Combine(geminiB, "config", "mcp_config.json"),
                    "{\"mcpServers\":{\"existing-mcp\":{\"command\":\"python\",\"args\":[\"server.py\"]}}}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(geminiB, "config", "hooks.json"),
                    "{\"existing-hook\":{\"PostInvocation\":[\"echo done\"]}}", Encoding.UTF8);

                // Scan Repository/ from Machine B's perspective and verify cross-machine signature stability
                List<CapabilityItem> repoCapsOnB = CapabilityRepositoryManager.ScanRepositoryCapabilities(repoDir, homeB, geminiB);
                AssertEqual(2, repoCapsOnB.Count, "2 capabilities scanned from Repository/");

                // Import both into Machine B
                int imported = CapabilityRepositoryManager.ImportCapabilitiesToLocal(geminiB, homeB, repoCapsOnB);
                AssertTrue(imported >= 3, "Imported MCP + Hook + companion script to Machine B");

                // Verify Machine B's mcp_config.json now has BOTH "existing-mcp" AND "evo-kernel" with D:/Users/MachineB path!
                string mergedMcpB = File.ReadAllText(Path.Combine(geminiB, "config", "mcp_config.json"), Encoding.UTF8);
                Dictionary<string, string> mcpEntriesB = CapabilityVaultService.ExtractObjectEntries(mergedMcpB, "mcpServers");
                AssertEqual(2, mcpEntriesB.Count, "Key-level JSON merge preserved existing-mcp and added evo-kernel");
                AssertTrue(mcpEntriesB.ContainsKey("existing-mcp"), "existing-mcp untouched");
                AssertTrue(mcpEntriesB.ContainsKey("evo-kernel"), "evo-kernel merged in");
                AssertTrue(mcpEntriesB["evo-kernel"].Contains("D:/Users/MachineB/.gemini/evo/index.js"), "{{HOME}} expanded to D:/Users/MachineB");

                // Verify Machine B's hooks.json has BOTH "existing-hook" AND "evo-hook", plus companion script in config/
                string mergedHooksB = File.ReadAllText(Path.Combine(geminiB, "config", "hooks.json"), Encoding.UTF8);
                Dictionary<string, string> hookEntriesB = CapabilityVaultService.ExtractObjectEntries(mergedHooksB, null);
                AssertEqual(2, hookEntriesB.Count, "Key-level JSON merge preserved existing-hook and added evo-hook");
                AssertTrue(File.Exists(Path.Combine(geminiB, "config", "evo_hook.mjs")), "Companion hook script unpacked to Machine B config/");

                // Verify ContentSignature is identical between Machine B Local and Repository
                List<CapabilityItem> localCapsB = CapabilityRepositoryManager.ScanLocalCapabilities(geminiB, homeB);
                List<CapabilityDiffNode> diffB = VisualDiffEngine.ComputeCapabilityDiff(localCapsB, repoCapsOnB);
                CapabilityDiffNode evoDiff = diffB.Find(x => x.RemoteItem != null && x.RemoteItem.Id == "evo-kernel");
                AssertTrue(evoDiff != null && evoDiff.Status == DiffStatus.Aligned, "Cross-machine ContentSignature matches (Aligned) after {{HOME}} expansion");

                // Delete single MCP "evo-kernel" from Machine B and verify "existing-mcp" remains
                CapabilityItem toDel = localCapsB.Find(x => x.Kind == CapabilityKind.McpServer && x.Id == "evo-kernel");
                CapabilityRepositoryManager.DeleteCapabilitiesFromLocal(geminiB, new List<CapabilityItem> { toDel });
                Dictionary<string, string> afterDelMcp = CapabilityVaultService.ExtractObjectEntries(
                    File.ReadAllText(Path.Combine(geminiB, "config", "mcp_config.json"), Encoding.UTF8), "mcpServers");
                AssertEqual(1, afterDelMcp.Count, "Only 1 MCP remains after deleting evo-kernel");
                AssertTrue(afterDelMcp.ContainsKey("existing-mcp"), "Sibling existing-mcp still intact");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T17_V07_MissingPathDetectionAndRelocation()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_v07_reloc_" + Guid.NewGuid().ToString("N"));
            try
            {
                string geminiDir = Path.Combine(sb, ".gemini");
                Directory.CreateDirectory(Path.Combine(geminiDir, "config"));

                string missingScript = Path.Combine(sb, "NonExistentDir", "missing_server.js").Replace('\\', '/');
                string realScript = Path.Combine(sb, "RealServer", "active_server.js");
                Directory.CreateDirectory(Path.GetDirectoryName(realScript));
                File.WriteAllText(realScript, "// active mcp server", Encoding.UTF8);

                File.WriteAllText(Path.Combine(geminiDir, "config", "mcp_config.json"),
                    "{\"mcpServers\":{\"custom-mcp\":{\"command\":\"node\",\"args\":[\"" + missingScript + "\"]}}}", Encoding.UTF8);

                List<CapabilityItem> caps1 = CapabilityRepositoryManager.ScanLocalCapabilities(geminiDir, sb);
                AssertEqual(1, caps1.Count, "1 MCP scanned");
                AssertTrue(caps1[0].HasMissingPaths, "Missing script path detected");
                AssertEqual(1, caps1[0].MissingLocalPaths.Count, "1 missing path recorded");

                // Relocate missingScript -> realScript
                bool relocated = CapabilityRepositoryManager.RelocateCapabilityPath(
                    geminiDir, null, true, sb, caps1[0], caps1[0].MissingLocalPaths[0], realScript.Replace('\\', '/')
                );
                AssertTrue(relocated, "RelocateCapabilityPath succeeded");

                List<CapabilityItem> caps2 = CapabilityRepositoryManager.ScanLocalCapabilities(geminiDir, sb);
                AssertTrue(!caps2[0].HasMissingPaths, "No missing paths after relocation to real script");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T18_V07_SelectiveAgyArchive_CapabilitySliceRoundtrip()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_v07_zip_" + Guid.NewGuid().ToString("N"));
            string outZip = Path.Combine(Path.GetTempPath(), "agy_v07_caps_" + Guid.NewGuid().ToString("N") + ".agy");
            try
            {
                string geminiDir = Path.Combine(sb, ".gemini");
                Directory.CreateDirectory(Path.Combine(geminiDir, "config", "skills", "skill-alpha"));
                Directory.CreateDirectory(Path.Combine(geminiDir, "config", "skills", "skill-beta"));
                File.WriteAllText(Path.Combine(geminiDir, "config", "skills", "skill-alpha", "SKILL.md"), "# Skill Alpha", Encoding.UTF8);
                File.WriteAllText(Path.Combine(geminiDir, "config", "skills", "skill-beta", "SKILL.md"), "# Skill Beta", Encoding.UTF8);
                File.WriteAllText(Path.Combine(geminiDir, "config", "mcp_config.json"),
                    "{\"mcpServers\":{\"mcp-one\":{\"command\":\"node\"},\"mcp-two\":{\"command\":\"python\"}}}", Encoding.UTF8);

                List<CapabilityItem> allLocal = CapabilityRepositoryManager.ScanLocalCapabilities(geminiDir, sb);
                AssertEqual(4, allLocal.Count, "2 skills + 2 MCP servers on local");

                // Select ONLY skill-alpha and mcp-one (0 sessions!) to export into .agy
                List<CapabilityItem> selectedCaps = new List<CapabilityItem>
                {
                    allLocal.Find(x => x.Kind == CapabilityKind.Skill && x.Id == "skill-alpha"),
                    allLocal.Find(x => x.Kind == CapabilityKind.McpServer && x.Id == "mcp-one")
                };

                int packed = LocalWorkspaceManager.ExportSelectiveBackup(
                    geminiDir, sb, outZip,
                    new List<ConversationSummaryInfo>(),
                    new Dictionary<string, ProjectMetadata>(),
                    false,
                    selectedCaps
                );
                AssertTrue(packed >= 2, "Packed selective capabilities into .agy without sessions");

                BackupArchiveIndex idx = StreamingZipIndexReader.ReadIndex(outZip);
                AssertEqual(0, idx.Conversations.Count, "0 conversations in capability-only .agy");
                AssertEqual(2, idx.Capabilities.Count, "Exactly 2 selected capabilities in .agy");
                AssertTrue(idx.Capabilities.Exists(x => x.Kind == CapabilityKind.Skill && x.Id == "skill-alpha"), "skill-alpha present in .agy");
                AssertTrue(!idx.Capabilities.Exists(x => x.Kind == CapabilityKind.Skill && x.Id == "skill-beta"), "skill-beta excluded from .agy");
                AssertTrue(idx.Capabilities.Exists(x => x.Kind == CapabilityKind.McpServer && x.Id == "mcp-one"), "mcp-one present in .agy");
                AssertTrue(!idx.Capabilities.Exists(x => x.Kind == CapabilityKind.McpServer && x.Id == "mcp-two"), "mcp-two excluded from .agy");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
                if (File.Exists(outZip)) File.Delete(outZip);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // P0 + P1 Regression Tests (T19 ~ T25)
        // ══════════════════════════════════════════════════════════════════════

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, IntPtr zVfs);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_close(IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_exec", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_exec(IntPtr db, byte[] sql, IntPtr callback, IntPtr arg, IntPtr errmsg);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_prepare_v2(IntPtr db, byte[] zSql, int nByte, out IntPtr ppStmt, IntPtr pzTail);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_blob", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_bind_blob(IntPtr pStmt, int iCol, IntPtr zData, int nData, IntPtr xDel);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_step(IntPtr pStmt);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_blob", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr t_sqlite3_column_blob(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_bytes", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_column_bytes(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_finalize(IntPtr pStmt);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_text", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_bind_text(IntPtr pStmt, int iCol, IntPtr zData, int nByte, IntPtr xDel);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_int", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_bind_int(IntPtr pStmt, int iCol, int value);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr t_sqlite3_column_text(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_int", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_column_int(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_type", CallingConvention = CallingConvention.Winapi)]
        private static extern int t_sqlite3_column_type(IntPtr pStmt, int iCol);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_errmsg", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr t_sqlite3_errmsg(IntPtr db);

        private static byte[] Utf8Z(string s)
        {
            byte[] raw = Encoding.UTF8.GetBytes(s ?? string.Empty);
            byte[] res = new byte[raw.Length + 1];
            Buffer.BlockCopy(raw, 0, res, 0, raw.Length);
            return res;
        }

        private static void T19_ProtoAstCodec_StrictnessField15AndSharding()
        {
            List<ProtoNode> parsed;
            AssertTrue(!ProtoCodec.TryDecodeStrict(Encoding.UTF8.GetBytes("not a valid protobuf wire stream!"), out parsed),
                "TryDecodeStrict rejects invalid wire bytes");
            AssertTrue(!ProtoCodec.TryDecodeStrict(new byte[] { 0x0A, 0xFF, 0x01 }, out parsed),
                "TryDecodeStrict rejects truncated length-delimited field");

            string cid = "aaaa1111-2222-3333-4444-555566667777";
            ProtoNode validEntry = BuildRealProtoEntry(cid, "Auto Title", null, "file:///c%3A/Users/Old/Proj", "Proj", "proj-old", 14, 5, 1);
            byte[] encoded = ProtoCodec.Encode(new List<ProtoNode> { validEntry });
            AssertTrue(ProtoCodec.TryDecodeStrict(encoded, out parsed) && parsed.Count == 1,
                "TryDecodeStrict accepts well-formed proto entry");

            // 验证 Field 15 写入及 MergeShards 去重 + 跨机 URI 与工程重绑定 (C-01 / C-04)
            ConversationSummaryInfo backupInfo = SessionMetadataExtractor.ExtractSummariesFromProto(encoded)[0];
            backupInfo.CustomTitle = "【回归测试】用户自定义标题";
            backupInfo.DisplayTitle = backupInfo.CustomTitle;
            backupInfo.HasCustomTitle = true;

            byte[] duplicateLocal = ProtoCodec.Encode(new List<ProtoNode> { validEntry, validEntry });
            Dictionary<string, string> overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            overrides["Proj"] = "file:///d%3A/Work/NewProj";
            Dictionary<string, string> projIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            projIds["proj-old"] = "proj-new-99";
            projIds["Proj"] = "proj-new-99";

            byte[] merged = IncrementalProtoShardMerger.MergeShards(
                duplicateLocal,
                new List<ConversationSummaryInfo> { backupInfo },
                "C:\\Users\\Old",
                "D:\\Work",
                overrides,
                projIds);

            List<ConversationSummaryInfo> after = SessionMetadataExtractor.ExtractSummariesFromProto(merged);
            AssertEqual(1, after.Count, "Duplicate local proto entries deduplicated to 1 on merge");
            AssertEqual("【回归测试】用户自定义标题", after[0].CustomTitle, "Field 15 CustomTitle persisted on merge");
            AssertEqual("proj-new-99", after[0].ProjectId, "ProjectId rebound on merge");
            AssertEqual("file:///d%3A/Work/NewProj", after[0].FolderUri, "FolderUri overridden to target workspace");
        }

        private static void T20_RealWinSqlite3_WalAndChinesePathRemap()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_sql_wal_" + Guid.NewGuid().ToString("N"));
            IntPtr srcHandle = IntPtr.Zero;
            IntPtr verifyHandle = IntPtr.Zero;
            try
            {
                string srcDir = Path.Combine(sb, "源目录");
                string dstDir = Path.Combine(sb, "中文目标工作区", "子工程");
                Directory.CreateDirectory(srcDir);
                Directory.CreateDirectory(dstDir);

                string cid = "99998888-7777-6666-5555-444433332222";
                string srcDb = Path.Combine(srcDir, cid + ".db");
                string dstDb = Path.Combine(dstDir, cid + ".db");

                ProtoNode origEntry = BuildRealProtoEntry(cid, "Old DB Title", null, "file:///c%3A/Users/OldDev/MyApp", "MyApp", "p-src", 5, 2, 1);
                byte[] origBlob = ProtoCodec.Encode(new List<ProtoNode> { origEntry });

                int rc = t_sqlite3_open_v2(Utf8Z(srcDb), out srcHandle, 0x00000002 | 0x00000004, IntPtr.Zero); // READWRITE | CREATE
                AssertEqual(0, rc, "winsqlite3_open_v2 created source DB");

                AssertEqual(0, t_sqlite3_exec(srcHandle, Utf8Z("PRAGMA journal_mode=WAL;"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Enable WAL mode");
                AssertEqual(0, t_sqlite3_exec(srcHandle, Utf8Z("PRAGMA wal_autocheckpoint=0;"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Disable autocheckpoint");
                AssertEqual(0, t_sqlite3_exec(srcHandle, Utf8Z("CREATE TABLE trajectory_metadata_blob(id TEXT PRIMARY KEY, data BLOB);"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Create schema");
                AssertEqual(0, t_sqlite3_exec(srcHandle, Utf8Z("PRAGMA wal_checkpoint(TRUNCATE);"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Checkpoint empty table schema to main db");

                IntPtr stmt = IntPtr.Zero;
                AssertEqual(0, t_sqlite3_prepare_v2(srcHandle, Utf8Z("INSERT INTO trajectory_metadata_blob(id, data) VALUES('main', ?);"), -1, out stmt, IntPtr.Zero), "Prepare INSERT blob");
                GCHandle pin = GCHandle.Alloc(origBlob, GCHandleType.Pinned);
                try
                {
                    AssertEqual(0, t_sqlite3_bind_blob(stmt, 1, pin.AddrOfPinnedObject(), origBlob.Length, new IntPtr(-1)), "Bind protobuf blob");
                    AssertEqual(101, t_sqlite3_step(stmt), "Step INSERT SQLITE_DONE");
                }
                finally
                {
                    if (stmt != IntPtr.Zero) t_sqlite3_finalize(stmt);
                    if (pin.IsAllocated) pin.Free();
                }

                string srcWal = srcDb + "-wal";
                AssertTrue(File.Exists(srcWal) && new FileInfo(srcWal).Length > 32, "Uncheckpointed WAL contains committed frame");

                // 模拟备份与提取场景：仅复制 .db + .db-wal 至含中文路径，故意不复制 .db-shm
                using (FileStream fs = new FileStream(srcDb, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (FileStream os = new FileStream(dstDb, FileMode.Create, FileAccess.Write, FileShare.None))
                    fs.CopyTo(os);
                using (FileStream fs = new FileStream(srcWal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (FileStream os = new FileStream(dstDb + "-wal", FileMode.Create, FileAccess.Write, FileShare.None))
                    fs.CopyTo(os);

                t_sqlite3_close(srcHandle);
                srcHandle = IntPtr.Zero;

                Dictionary<string, string> uriOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                uriOverrides["MyApp"] = "file:///d%3A/Users/NewDev/MyAppRenamed";

                bool remapOk = SqliteMetadataUpdater.RemapSqliteDbMetadata(
                    dstDb, "C:\\Users\\OldDev", "D:\\Users\\NewDev", uriOverrides, "MyApp", "p-target", "WAL重映射标题");
                AssertTrue(remapOk, "RemapSqliteDbMetadata succeeds on WAL + Chinese UTF-8 path without -shm (" + SqliteMetadataUpdater.LastError + ")");

                AssertEqual(0, t_sqlite3_open_v2(Utf8Z(dstDb), out verifyHandle, 0x00000001, IntPtr.Zero), "Reopen remapped Chinese-path SQLite DB readonly");
                IntPtr selStmt = IntPtr.Zero;
                byte[] updatedBlob = null;
                try
                {
                    AssertEqual(0, t_sqlite3_prepare_v2(verifyHandle, Utf8Z("SELECT data FROM trajectory_metadata_blob WHERE id='main';"), -1, out selStmt, IntPtr.Zero), "Prepare SELECT verification");
                    AssertEqual(100, t_sqlite3_step(selStmt), "SELECT returns SQLITE_ROW (WAL replayed cleanly)");
                    int len = t_sqlite3_column_bytes(selStmt, 0);
                    IntPtr ptr = t_sqlite3_column_blob(selStmt, 0);
                    AssertTrue(len > 0 && ptr != IntPtr.Zero, "Verification blob is non-empty");
                    updatedBlob = new byte[len];
                    Marshal.Copy(ptr, updatedBlob, 0, len);
                }
                finally
                {
                    if (selStmt != IntPtr.Zero) t_sqlite3_finalize(selStmt);
                }

                List<ConversationSummaryInfo> parsedAfter = SessionMetadataExtractor.ExtractSummariesFromProto(updatedBlob);
                AssertEqual(1, parsedAfter.Count, "Remapped SQLite blob decodes cleanly as 1 summary");
                AssertEqual("p-target", parsedAfter[0].ProjectId, "SQLite ProjectId updated");
                AssertEqual("file:///d%3A/Users/NewDev/MyAppRenamed", parsedAfter[0].FolderUri, "SQLite FolderUri updated");
                AssertEqual("WAL重映射标题", parsedAfter[0].DisplayTitle, "SQLite title updated");

                // 失败路径显式返回 false 且附带错误信息
                AssertTrue(!SqliteMetadataUpdater.RemapSqliteDbMetadata(Path.Combine(sb, "missing.db"), "C:\\a", "D:\\b", null, "X"),
                    "Missing SQLite file returns false fail-closed");
                AssertTrue(!string.IsNullOrEmpty(SqliteMetadataUpdater.LastError), "LastError populated on missing SQLite file");
            }
            finally
            {
                if (srcHandle != IntPtr.Zero) try { t_sqlite3_close(srcHandle); } catch {}
                if (verifyHandle != IntPtr.Zero) try { t_sqlite3_close(verifyHandle); } catch {}
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T21_StreamingZipIndexReader_ZipSlipAndSelectiveClosure()
        {
            AssertTrue(!CapabilityRepositoryManager.IsSafeRelativePath("../../Windows/System32/evil.dll"), "Rejects parent traversal");
            AssertTrue(!CapabilityRepositoryManager.IsSafeRelativePath("C:\\abs\\file.txt"), "Rejects rooted Windows path");
            AssertTrue(!CapabilityRepositoryManager.IsSafeRelativePath("CON/file.txt"), "Rejects Windows reserved device name segment");
            AssertTrue(CapabilityRepositoryManager.IsSafeRelativePath("assets/icons/logo.png"), "Accepts normal nested relative path");

            bool zipSlipBlocked = false;
            try
            {
                CapabilityRepositoryManager.CombineAndEnsureUnderRoot("C:\\SafeRoot", "..\\escape.txt");
            }
            catch (InvalidOperationException)
            {
                zipSlipBlocked = true;
            }
            AssertTrue(zipSlipBlocked, "CombineAndEnsureUnderRoot throws on Zip-Slip traversal");

            // 同名多工程精确按 ProjectId 隔离删除 (C-10)
            string sb = Path.Combine(Path.GetTempPath(), "agy_multiproj_" + Guid.NewGuid().ToString("N"));
            try
            {
                string convDir = Path.Combine(sb, "antigravity", "conversations");
                string brainDir = Path.Combine(sb, "antigravity", "brain");
                string projDir = Path.Combine(sb, "config", "projects");
                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(brainDir);
                Directory.CreateDirectory(projDir);

                string cid1 = "11110000-1111-1111-1111-111111111111";
                string cid2 = "22220000-2222-2222-2222-222222222222";
                ProtoNode p1 = BuildRealProtoEntry(cid1, "SharedName A1", null, "file:///c%3A/DiskA/SharedApp", "SharedApp", "id-disk-a", 4, 1, 1);
                ProtoNode p2 = BuildRealProtoEntry(cid2, "SharedName B2", null, "file:///d%3A/DiskB/SharedApp", "SharedApp", "id-disk-b", 6, 2, 1);

                string pbPath = Path.Combine(sb, "antigravity", "agyhub_summaries_proto.pb");
                File.WriteAllBytes(pbPath, ProtoCodec.Encode(new List<ProtoNode> { p1, p2 }));
                File.WriteAllText(Path.Combine(projDir, "id-disk-a.json"), "{\"id\":\"id-disk-a\",\"name\":\"SharedApp\",\"folderUri\":\"file:///c%3A/DiskA/SharedApp\"}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(projDir, "id-disk-b.json"), "{\"id\":\"id-disk-b\",\"name\":\"SharedApp\",\"folderUri\":\"file:///d%3A/DiskB/SharedApp\"}", Encoding.UTF8);
                CreateMinimalValidSqliteDb(Path.Combine(convDir, cid1 + ".db"), cid1);
                CreateMinimalValidSqliteDb(Path.Combine(convDir, cid2 + ".db"), cid2);

                Dictionary<string, ProjectMetadata> localProjects = new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
                localProjects["id-disk-a"] = new ProjectMetadata { Id = "id-disk-a", Name = "SharedApp", FolderUri = "file:///c%3A/DiskA/SharedApp" };
                localProjects["id-disk-b"] = new ProjectMetadata { Id = "id-disk-b", Name = "SharedApp", FolderUri = "file:///d%3A/DiskB/SharedApp" };

                List<ConversationSummaryInfo> allSums = SessionMetadataExtractor.ExtractSummaries(File.ReadAllBytes(pbPath), localProjects);
                DeletionResult delRes = LocalWorkspaceManager.DeleteSessionsAndProjects(
                    sb,
                    allSums,
                    localProjects,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    null,
                    new HashSet<string>(new string[] { "id-disk-a" }, StringComparer.OrdinalIgnoreCase),
                    null);

                AssertEqual(1, delRes.DeletedProjectJsonCount, "Only 1 matching project JSON deleted");
                AssertTrue(!File.Exists(Path.Combine(projDir, "id-disk-a.json")), "Targeted id-disk-a.json deleted");
                AssertTrue(File.Exists(Path.Combine(projDir, "id-disk-b.json")), "Same-name sibling id-disk-b.json preserved");
                AssertTrue(!File.Exists(Path.Combine(convDir, cid1 + ".db")), "cid1.db deleted");
                AssertTrue(File.Exists(Path.Combine(convDir, cid2 + ".db")), "Sibling SharedApp cid2.db preserved");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T22_CapabilityRepo_PlaceholdersJsoncEncodedIdAndHooks()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_cap_edge_" + Guid.NewGuid().ToString("N"));
            try
            {
                // 1. ExpandHomePlaceholder JSON 模式双反斜杠与文本模式原始反斜杠 (C-12)
                string homeWithSpaces = "C:\\Users\\John Doe";
                string jsonTpl = "{\"args\": [\"{{HOME}}\\\\scripts\\\\run.py\"]}";
                string expandedJson = CapabilityPathNormalizer.ExpandHomePlaceholder(jsonTpl, homeWithSpaces, true);
                AssertTrue(expandedJson.Contains("C:\\\\Users\\\\John Doe\\\\scripts\\\\run.py"), "JSON placeholder expansion keeps double backslashes valid");
                string parsedVal = UnifiedJson.PatchObjectKeyPreservingComments("{\"mcpServers\":{}}", "mcpServers", "s1", expandedJson);
                AssertTrue(parsedVal.Contains("John Doe"), "Upserted JSON remains parseable");

                string textExpanded = CapabilityPathNormalizer.ExpandHomePlaceholder("{{HOME}}\\bin\\tool.exe", homeWithSpaces, false);
                AssertEqual("C:\\Users\\John Doe\\bin\\tool.exe", textExpanded, "Plain text placeholder keeps single backslash");

                // 2. JSONC 注释、尾逗号与残缺 JSON 失败封闭保护 (C-13 / C-26)
                string jsonc = "{\n  // line comment\n  \"plugins\": {\n    \"ok\\u0040plugin\": { \"enabled\": true, /* block */ }\n  },\n}";
                Dictionary<string, string> plugMap = CapabilityVaultService.ExtractObjectEntries(jsonc, "plugins");
                AssertTrue(plugMap.ContainsKey("ok@plugin"), "JSONC + unicode key unescaped cleanly");

                bool failClosed = false;
                try
                {
                    UnifiedJson.PatchObjectKeyPreservingComments("{ \"plugins\": { \"broken\": ", "plugins", "k", "{}");
                }
                catch (FormatException)
                {
                    failClosed = true;
                }
                AssertTrue(failClosed, "Corrupted JSON triggers fail-closed FormatException in PatchObjectKeyPreservingComments");

                // 3. @scope/pkg 安全目录编码往返 (C-14) - 含带 ':' 与不带 ':' 的 @scope/pkg 全链路往返
                string scopedId = "@my-org/super-skill:v1";
                string encodedDir = CapabilityRepositoryManager.EncodeSafeCapabilityFolderName(scopedId);
                AssertTrue(CapabilityRepositoryManager.IsSafeRelativePath(encodedDir), "Encoded scoped folder name is safe relative path");
                AssertEqual(scopedId, CapabilityRepositoryManager.DecodeSafeCapabilityFolderName(encodedDir), "Scoped folder name roundtrips losslessly");

                string scopedSlashOnlyId = "@my-org/super-skill";
                string encodedSlashOnly = CapabilityRepositoryManager.EncodeSafeCapabilityFolderName(scopedSlashOnlyId);
                AssertTrue(encodedSlashOnly.StartsWith("_enc_", StringComparison.Ordinal) && !encodedSlashOnly.Contains("/"),
                    "@scope/pkg without colon encodes to single flat _enc_ folder");

                string repoRoot = Path.Combine(sb, "Repo");
                string capGemini = Path.Combine(sb, ".gemini_scoped");
                string capZip = Path.Combine(sb, "scoped_cap.agy");
                string repoSkillDir = Path.Combine(repoRoot, "skills", encodedSlashOnly);
                Directory.CreateDirectory(repoSkillDir);
                File.WriteAllText(Path.Combine(repoSkillDir, "SKILL.md"), "# Scoped Skill\nWorks across machines: {{HOME}}/bin", Encoding.UTF8);

                List<CapabilityItem> repoScanned = CapabilityRepositoryManager.ScanRepositoryCapabilities(repoRoot, sb, capGemini);
                CapabilityItem repoScoped = repoScanned.Find(x => x.Kind == CapabilityKind.Skill && x.Id == scopedSlashOnlyId);
                AssertTrue(repoScoped != null, "@scope/pkg (without colon) scanned back from Repository");

                CapabilityRepositoryManager.ImportCapabilitiesToLocal(capGemini, sb, new List<CapabilityItem> { repoScoped });
                string localSkillsRoot = Path.Combine(capGemini, "config", "skills");
                AssertTrue(!Directory.Exists(Path.Combine(localSkillsRoot, "@my-org")), "No 2-level nested @my-org/ directory created locally");
                AssertTrue(Directory.Exists(Path.Combine(localSkillsRoot, encodedSlashOnly)), "Single flat encoded folder created locally");

                List<CapabilityItem> localScanned = CapabilityRepositoryManager.ScanLocalCapabilities(capGemini, sb);
                CapabilityItem localScoped = localScanned.Find(x => x.Kind == CapabilityKind.Skill && x.Id == scopedSlashOnlyId);
                AssertTrue(localScoped != null, "@scope/pkg scanned back cleanly from local environment");

                List<CapabilityDiffNode> scopedDiff = VisualDiffEngine.ComputeCapabilityDiff(localScanned, repoScanned);
                CapabilityDiffNode sd = scopedDiff.Find(x => x.Id == scopedSlashOnlyId);
                AssertTrue(sd != null && sd.Status == DiffStatus.Aligned, "@scope/pkg is Aligned between local and repository");

                LocalWorkspaceManager.ExportSelectiveBackup(capGemini, sb, capZip, null, null, false, new List<CapabilityItem> { localScoped });
                BackupArchiveIndex capZipIndex = StreamingZipIndexReader.ReadIndex(capZip);
                CapabilityItem zipIndexScoped = capZipIndex.Capabilities.Find(x => x.Kind == CapabilityKind.Skill && x.Id == scopedSlashOnlyId);
                AssertTrue(zipIndexScoped != null, "@scope/pkg exported and indexed cleanly in .agy StreamingZipIndexReader");
                using (ZipArchive zCap = ZipFile.OpenRead(capZip))
                {
                    List<CapabilityItem> zipScanned = CapabilityRepositoryManager.ScanZipArchiveCapabilities(zCap, sb, sb, capGemini);
                    CapabilityItem zipScoped = zipScanned.Find(x => x.Kind == CapabilityKind.Skill && x.Id == scopedSlashOnlyId);
                    AssertTrue(zipScoped != null, "@scope/pkg decoded by ScanZipArchiveCapabilities");
                    AssertEqual(localScoped.ContentSignature, zipScoped.ContentSignature, "@scope/pkg ContentSignature preserved across .agy archive roundtrip");
                }

                int delScoped = CapabilityRepositoryManager.DeleteCapabilitiesFromLocal(capGemini, new List<CapabilityItem> { localScoped });
                AssertEqual(1, delScoped, "DeleteCapabilitiesFromLocal removes encoded @scope/pkg skill cleanly");
                AssertTrue(!Directory.Exists(Path.Combine(localSkillsRoot, encodedSlashOnly)), "Encoded @scope/pkg folder physically deleted");

                // 4. Hook 脚本引用计数删除 + 排除 node_modules / secrets (C-28 / E-01)
                string geminiDir = Path.Combine(sb, ".gemini");
                string cfgDir = Path.Combine(geminiDir, "config");
                Directory.CreateDirectory(cfgDir);
                string sharedPy = Path.Combine(cfgDir, "shared_guard.py");
                string uniquePy = Path.Combine(cfgDir, "only_h1.py");
                File.WriteAllText(sharedPy, "# shared", Encoding.UTF8);
                File.WriteAllText(uniquePy, "# unique", Encoding.UTF8);

                string hooksJson = string.Format(
                    "{{\r\n  \"h1\": {{ \"command\": \"python \\\"{0}\\\" && python \\\"{1}\\\"\" }},\r\n  \"h2\": {{ \"command\": \"python \\\"{0}\\\"\" }}\r\n}}",
                    UnifiedJson.EscapeString(sharedPy),
                    UnifiedJson.EscapeString(uniquePy));
                File.WriteAllText(Path.Combine(cfgDir, "hooks.json"), hooksJson, Encoding.UTF8);

                // 同时放置大文件 (>1MB) 技能和 node_modules/.env 排除项
                string skillDir = Path.Combine(cfgDir, "skills", "big-skill");
                Directory.CreateDirectory(Path.Combine(skillDir, "node_modules", "pkg"));
                File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "# Big Skill", Encoding.UTF8);
                File.WriteAllText(Path.Combine(skillDir, ".env.local"), "SECRET=123", Encoding.UTF8);
                File.WriteAllText(Path.Combine(skillDir, "node_modules", "pkg", "index.js"), "ignored", Encoding.UTF8);
                byte[] bigPayload = new byte[1200 * 1024];
                bigPayload[0] = (byte)'B';
                bigPayload[bigPayload.Length - 1] = (byte)'Z';
                File.WriteAllBytes(Path.Combine(skillDir, "weights.bin"), bigPayload);

                List<CapabilityItem> caps = CapabilityRepositoryManager.ScanLocalCapabilities(geminiDir, sb);
                CapabilityItem skill = caps.Find(x => x.Kind == CapabilityKind.Skill && x.Id == "big-skill");
                AssertTrue(skill != null, "Big skill scanned");
                AssertTrue(!skill.FilesMap.ContainsKey(".env.local") && !skill.SourceFilePaths.ContainsKey(".env.local"), ".env.local excluded");
                AssertTrue(!skill.FilesMap.ContainsKey("node_modules/pkg/index.js") && !skill.SourceFilePaths.ContainsKey("node_modules/pkg/index.js"), "node_modules excluded");
                AssertTrue(skill.SourceFilePaths.ContainsKey("weights.bin") && skill.FilesMap.ContainsKey("weights.bin") && skill.FilesMap["weights.bin"] == null, ">1MB file uses lazy SourceFilePaths with null FilesMap byte placeholder");
                AssertEqual(bigPayload.Length, skill.GetFileBytes("weights.bin").Length, "Lazy large file reads exact length via GetFileBytes");

                CapabilityItem h1 = caps.Find(x => x.Kind == CapabilityKind.Hook && x.Id == "h1");
                CapabilityItem h2 = caps.Find(x => x.Kind == CapabilityKind.Hook && x.Id == "h2");
                AssertTrue(h1 != null && h2 != null, "Both hooks discovered");

                CapabilityRepositoryManager.DeleteCapabilitiesFromLocal(geminiDir, new List<CapabilityItem> { h1 });
                AssertTrue(!File.Exists(uniquePy), "Unique script only_h1.py deleted with h1");
                AssertTrue(File.Exists(sharedPy), "Shared script shared_guard.py preserved because h2 still references it");

                CapabilityRepositoryManager.DeleteCapabilitiesFromLocal(geminiDir, new List<CapabilityItem> { h2 });
                AssertTrue(!File.Exists(sharedPy), "Shared script deleted once last referencing hook h2 is deleted");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T23_TranscriptMarkdownExporter_StreamingTagsAndFullJsonl()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_tx_stream_" + Guid.NewGuid().ToString("N"));
            try
            {
                string cid = "12121212-3434-5656-7878-909090909090";
                string brain = Path.Combine(sb, "antigravity", "brain", cid, ".system_generated", "logs");
                Directory.CreateDirectory(brain);

                File.WriteAllText(Path.Combine(brain, "transcript.jsonl"),
                    "{\"step_index\":1,\"type\":\"USER_INPUT\",\"content\":\"COMPACT_TRUNCATED\"}\n", Encoding.UTF8);

                string fullJsonl =
                    "{\"step_index\":1,\"type\":\"USER_INPUT\",\"created_at\":\"2026-11-01T10:00:00Z\",\"content\":\"<USER_REQUEST>\\nPlease parse <USER_REQUEST> safely inside text</USER_REQUEST>\\n</USER_REQUEST>\"}\n" +
                    "{\"step_index\":2,\"type\":\"PLANNER_RESPONSE\",\"created_at\":\"2026-11-01T10:00:05Z\",\"thinking\":\"Nested </details> and <details> inside thought with brace }\",\"content\":\"Done!\"}\n";
                File.WriteAllText(Path.Combine(brain, "transcript_full.jsonl"), fullJsonl, Encoding.UTF8);

                ConversationSummaryInfo s = new ConversationSummaryInfo
                {
                    ConversationId = cid,
                    DisplayTitle = "Streaming Tag Safety",
                    ProjectName = "TestProj"
                };

                string outMd = Path.Combine(sb, "out.md");
                string md = TranscriptMarkdownExporter.ExportLocalSessionToMarkdown(s, sb, outMd);

                AssertTrue(!md.Contains("COMPACT_TRUNCATED"), "Prefers transcript_full.jsonl over compact transcript.jsonl");
                AssertTrue(md.Contains("Please parse <USER_REQUEST> safely inside text"), "LastIndexOf closing tag preserves literal inner content");
                AssertTrue(md.Contains("&lt;/details&gt;") && md.Contains("&lt;details&gt;"), "Escapes <details> HTML tags inside thinking block");

                // 路径穿越 CID 防护 (C-22)
                bool blockedTraversal = false;
                try
                {
                    TranscriptMarkdownExporter.ExportLocalSessionToMarkdown(
                        new ConversationSummaryInfo { ConversationId = "../../Windows", DisplayTitle = "bad" },
                        sb,
                        Path.Combine(sb, "bad.md"));
                }
                catch (ArgumentException)
                {
                    blockedTraversal = true;
                }
                AssertTrue(blockedTraversal, "Blocks path-traversal ConversationId");

                // 有界 Markdown 预览截断验证 (Minor Rec #2)
                string hugePayload = new string('X', 2600);
                string hugeJsonl = "{\"step_index\":1,\"type\":\"USER_INPUT\",\"created_at\":\"2026-11-01T10:00:00Z\",\"content\":\"" + hugePayload + "\"}\n";
                File.WriteAllText(Path.Combine(brain, "transcript_full.jsonl"), hugeJsonl, Encoding.UTF8);
                bool isTruncated;
                string preview = TranscriptMarkdownExporter.PreviewLocalSessionMarkdown(s, sb, 1024, out isTruncated);
                AssertTrue(isTruncated, "PreviewLocalSessionMarkdown sets isTruncated when exceeding limit");
                AssertTrue(preview != null && preview.Length <= 1024, "PreviewLocalSessionMarkdown strictly bounds output <= 1024 chars");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T24_VisualDiffEngine_RenameCrossHostAndSameBasename()
        {
            string cidShared = "55556666-7777-8888-9999-000011112222";
            DateTime t0 = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

            List<ConversationSummaryInfo> local = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo
                {
                    ConversationId = cidShared,
                    ProjectId = "p-loc",
                    ProjectName = "Companion_V2",
                    FolderUri = "file:///d%3A/Users/Bob/Desktop/Repo",
                    DisplayTitle = "Aligned Session",
                    StepCount = 10,
                    UserInteractions = 3,
                    UpdatedTime = t0
                },
                new ConversationSummaryInfo
                {
                    ConversationId = "66667777-8888-9999-0000-111122223333",
                    ProjectId = "outside-of-project",
                    ProjectName = "Outside of Project",
                    FolderUri = "file:///d%3A/Users/Bob",
                    DisplayTitle = "Scratch Question",
                    UpdatedTime = t0
                }
            };

            List<ConversationSummaryInfo> backup = new List<ConversationSummaryInfo>
            {
                new ConversationSummaryInfo
                {
                    ConversationId = cidShared,
                    ProjectId = "p-bak",
                    ProjectName = "Companion_V1",
                    FolderUri = "file:///c%3A/Users/Alice/Desktop/Repo",
                    DisplayTitle = "Aligned Session",
                    StepCount = 10,
                    UserInteractions = 3,
                    UpdatedTime = t0
                },
                new ConversationSummaryInfo
                {
                    ConversationId = "77778888-9999-0000-1111-222233334444",
                    ProjectId = "p-bak",
                    ProjectName = "Companion_V1",
                    FolderUri = "file:///c%3A/Users/Alice/Desktop/Repo",
                    DisplayTitle = "New Session From Backup",
                    StepCount = 4,
                    UserInteractions = 1,
                    UpdatedTime = t0
                }
            };

            List<ProjectDiffNode> diffs = VisualDiffEngine.ComputeDiff(local, backup, "C:\\Users\\Alice", "D:\\Users\\Bob");
            ProjectDiffNode renamedNode = diffs.Find(x => x.BackupConversations.Count == 2);
            AssertTrue(renamedNode != null, "Renamed cross-machine project merged into a single ProjectDiffNode");
            AssertTrue(!string.IsNullOrEmpty(renamedNode.RenamedFromOrTo), "RenamedFromOrTo annotated on merged project");
            AssertEqual(DiffStatus.NewInBackup, renamedNode.Status, "Merged renamed project with 1 new session marked NewInBackup");
            AssertEqual(1, renamedNode.NewSessionCount, "NewSessionCount == 1");
            AssertTrue(renamedNode.GetStatusBadge().Contains("工程已改名"), "Badge clearly shows project rename + new session count");

            // 纯改名无新增会话场景应标注为 DiffStatus.Modified
            List<ConversationSummaryInfo> backupRenamedOnly = new List<ConversationSummaryInfo> { backup[0] };
            List<ProjectDiffNode> diffsModified = VisualDiffEngine.ComputeDiff(local, backupRenamedOnly, "C:\\Users\\Alice", "D:\\Users\\Bob");
            ProjectDiffNode pureRenamedNode = diffsModified.Find(x => string.Equals(x.ProjectName, "Companion_V1", StringComparison.OrdinalIgnoreCase));
            AssertTrue(pureRenamedNode != null, "Pure-renamed project matched");
            AssertEqual(DiffStatus.Modified, pureRenamedNode.Status, "Pure-renamed project with zero new sessions marked Modified");

            // 确认未重复产出独立的 LocalOnly 幽灵工程节点
            int localDupCount = 0;
            for (int i = 0; i < diffs.Count; i++)
            {
                if (string.Equals(diffs[i].ProjectName, "Companion_V2", StringComparison.OrdinalIgnoreCase) && diffs[i].Status == DiffStatus.LocalOnly)
                    localDupCount++;
            }
            AssertEqual(0, localDupCount, "No duplicate LocalOnly project node created after rename match");

            // 已对齐会话不应被父工程改名连带标红为 ProjectDiff
            ConversationDiffDetail evalAligned = VisualDiffEngine.EvaluateConversationDiff(backup[0], local[0], renamedNode);
            AssertTrue(!evalAligned.ProjectDiff && !evalAligned.IsModified, "Aligned session under merged renamed project is not falsely flagged Modified");
        }

        private static void T25_WorkspaceService_ProjectJsonAndBackupSelectionPolicy()
        {
            // 1. GenerateStandardProjectJson 安全转义 + 最小沙箱策略 (C-09 / C-30)
            string pj = WorkspaceService.GenerateStandardProjectJson("id\"1", "App\\Name\"2", "C:\\Work\\中文项目");
            AssertTrue(pj.Contains("\"sandboxMode\": true"), "Fallback project JSON defaults to sandboxMode: true");
            AssertTrue(!pj.Contains("AGENT_SETTING_POLICY_ALLOW") && !pj.Contains("CASCADE_COMMANDS_AUTO_EXECUTION_EAGER") && !pj.Contains("ARTIFACT_REVIEW_MODE_TURBO"),
                "Fallback project JSON omits auto-allow/eager/turbo policies");
            Dictionary<string, string> pjFields = CapabilityVaultService.ExtractObjectEntries(pj, null);
            AssertEqual("\"App\\\\Name\\\"2\"", pjFields["name"].Trim(), "Project name properly escaped in raw JSON token");
            AssertEqual("App\\Name\"2", TranscriptMarkdownExporter.ExtractJsonStringField(pj, "name"), "Project name roundtrips cleanly through JSON decoder");
            string decodedUri = DirectStreamArchiver.UnescapeUriString("file:///d%3A/Repo/Exist%20Proj").Substring(8).Replace('/', '\\');
            AssertTrue(string.Equals("D:\\Repo\\Exist Proj", decodedUri, StringComparison.OrdinalIgnoreCase), "FileUri decodes to Windows path: " + decodedUri);

            // 2. ShouldAutoSelectBackupSession 防旧备份误覆盖 (X-03)
            DateTime baseTime = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Local);
            ConversationSummaryInfo locSess = new ConversationSummaryInfo
            {
                ConversationId = "sess-auto-1",
                DisplayTitle = "Old Title",
                ProjectName = "Proj",
                StepCount = 20,
                UserInteractions = 5,
                UpdatedTime = baseTime
            };
            AssertTrue(BackupSelectionPolicy.ShouldAutoSelectBackupSession(locSess, null, null), "Missing local session is auto-selected");

            ConversationSummaryInfo olderFewerBackup = new ConversationSummaryInfo
            {
                ConversationId = "sess-auto-1",
                DisplayTitle = "Renamed Elsewhere",
                ProjectName = "Proj",
                StepCount = 12,
                UserInteractions = 5,
                UpdatedTime = baseTime.AddHours(1)
            };
            AssertTrue(!BackupSelectionPolicy.ShouldAutoSelectBackupSession(olderFewerBackup, locSess, null),
                "Fewer-step backup is NOT auto-selected even with newer timestamp/title change");

            ConversationSummaryInfo equalButOlderBackup = new ConversationSummaryInfo
            {
                ConversationId = "sess-auto-1",
                DisplayTitle = "Old Title",
                ProjectName = "OtherProj",
                StepCount = 20,
                UserInteractions = 5,
                UpdatedTime = baseTime.AddSeconds(-10)
            };
            AssertTrue(!BackupSelectionPolicy.ShouldAutoSelectBackupSession(equalButOlderBackup, locSess, null),
                "Equal-count older backup with project/title formatting diff is NOT auto-selected");

            ConversationSummaryInfo titleOnlyDiffSameTime = new ConversationSummaryInfo
            {
                ConversationId = "sess-auto-1",
                DisplayTitle = "Different Format Title",
                ProjectName = "Proj",
                StepCount = 20,
                UserInteractions = 5,
                UpdatedTime = baseTime.AddSeconds(1)
            };
            AssertTrue(!BackupSelectionPolicy.ShouldAutoSelectBackupSession(titleOnlyDiffSameTime, locSess, null),
                "Equal-count within 2s jitter with only title diff is NOT auto-selected");

            ConversationSummaryInfo moreStepsBackup = new ConversationSummaryInfo
            {
                ConversationId = "sess-auto-1",
                DisplayTitle = "Old Title",
                ProjectName = "Proj",
                StepCount = 21,
                UserInteractions = 4,
                UpdatedTime = baseTime.AddMinutes(-5)
            };
            AssertTrue(BackupSelectionPolicy.ShouldAutoSelectBackupSession(moreStepsBackup, locSess, null),
                "Strictly more steps in backup IS auto-selected");

            ConversationSummaryInfo equalCountsNewerTimeBackup = new ConversationSummaryInfo
            {
                ConversationId = "sess-auto-1",
                DisplayTitle = "Old Title",
                ProjectName = "Proj",
                StepCount = 20,
                UserInteractions = 5,
                UpdatedTime = baseTime.AddSeconds(5)
            };
            AssertTrue(BackupSelectionPolicy.ShouldAutoSelectBackupSession(equalCountsNewerTimeBackup, locSess, null),
                "Equal counts with strictly newer timestamp (>2s) IS auto-selected");

            // 5. 同名不同路径双工程 ID / URI 隔离重映射 (C-05/C-10/C-25)
            string cidA = "aaaaaaaa-1111-1111-1111-111111111111";
            string cidB = "bbbbbbbb-2222-2222-2222-222222222222";
            ProtoNode nAppA = BuildRealProtoEntry(cidA, "App A Session", null, "file:///c%3A/Users/Alice/WorkA/App", "App", "pid-bak-a", 3, 1, 1);
            ProtoNode nAppB = BuildRealProtoEntry(cidB, "App B Session", null, "file:///c%3A/Users/Alice/WorkB/App", "App", "pid-bak-b", 4, 1, 1);
            byte[] pbTwoApps = ProtoCodec.Encode(new List<ProtoNode> { nAppA, nAppB });
            List<ConversationSummaryInfo> twoAppSums = SessionMetadataExtractor.ExtractSummariesFromProto(pbTwoApps);

            Dictionary<string, string> idOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            idOverrides["ID:pid-bak-a"] = "target-id-a";
            idOverrides["ID:pid-bak-b"] = "target-id-b";

            Dictionary<string, string> uriOverridesById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            uriOverridesById["ID:pid-bak-a"] = "file:///d%3A/Users/Bob/WorkA/App";
            uriOverridesById["ID:target-id-a"] = "file:///d%3A/Users/Bob/WorkA/App";
            uriOverridesById["ID:pid-bak-b"] = "file:///d%3A/Users/Bob/WorkB/App";
            uriOverridesById["ID:target-id-b"] = "file:///d%3A/Users/Bob/WorkB/App";

            byte[] mergedTwoApps = IncrementalProtoShardMerger.MergeShards(
                new byte[0], twoAppSums, "C:\\Users\\Alice", "D:\\Users\\Bob", uriOverridesById, idOverrides);
            List<ConversationSummaryInfo> mergedSums = SessionMetadataExtractor.ExtractSummariesFromProto(mergedTwoApps);
            ConversationSummaryInfo resA = mergedSums.Find(x => x.ConversationId == cidA);
            ConversationSummaryInfo resB = mergedSums.Find(x => x.ConversationId == cidB);
            AssertTrue(resA != null && resB != null, "Both same-basename projects merged cleanly");
            AssertEqual("target-id-a", resA.ProjectId, "Same-basename Project A keeps target-id-a");
            AssertEqual("target-id-b", resB.ProjectId, "Same-basename Project B keeps target-id-b");
            AssertEqual("file:///d%3A/Users/Bob/WorkA/App", resA.FolderUri, "Same-basename Project A keeps WorkA URI");
            AssertEqual("file:///d%3A/Users/Bob/WorkB/App", resB.FolderUri, "Same-basename Project B keeps WorkB URI");
        }

        // ══════════════════════════════════════════════════════════════════════
        // P2 + P3 Regression Tests (T26 ~ T31)
        // ══════════════════════════════════════════════════════════════════════

        private static void T26_SessionMetadataExtractor_NanoEpochFixed64AndOutsideUri()
        {
            long nanoEpoch = 1762776000000000000L; // 2025-11-10T12:00:00Z
            string cid = "33334444-5555-6666-7777-888899990000";

            ProtoNode f1 = new ProtoNode(1, 2);
            f1.SetString(cid);

            ProtoNode meta = new ProtoNode(2, 2);
            ProtoNode mTitle = new ProtoNode(1, 2);
            mTitle.SetString("Nano Fixed64 Session");
            meta.Children.Add(mTitle);

            ProtoNode mFixedTime = new ProtoNode(3, 1);
            mFixedTime.RawBytes = BitConverter.GetBytes(nanoEpoch);
            meta.Children.Add(mFixedTime);

            ProtoNode mCreated = new ProtoNode(7, 2);
            mCreated.Children.Add(new ProtoNode(1, 0) { VarintValue = (ulong)nanoEpoch });
            meta.Children.Add(mCreated);

            ProtoNode mWs = new ProtoNode(9, 2);
            ProtoNode mWsUri = new ProtoNode(1, 2);
            mWsUri.SetString("file:///d%3A/Temp/ShouldBeCleared");
            mWs.Children.Add(mWsUri);
            meta.Children.Add(mWs);

            ProtoNode mCasc = new ProtoNode(17, 2);
            ProtoNode cName = new ProtoNode(1, 2);
            cName.SetString("WrongProjectName");
            ProtoNode cUri = new ProtoNode(7, 2);
            cUri.SetString("file:///d%3A/Temp/ShouldBeCleared");
            ProtoNode cPid = new ProtoNode(18, 2);
            cPid.SetString("outside-of-project");
            mCasc.Children.Add(cName);
            mCasc.Children.Add(cUri);
            mCasc.Children.Add(cPid);
            meta.Children.Add(mCasc);

            ProtoNode entry = new ProtoNode(1, 2);
            entry.Children.Add(f1);
            entry.Children.Add(meta);

            byte[] pbBytes = ProtoCodec.Encode(new List<ProtoNode> { entry });
            List<ConversationSummaryInfo> parsed = SessionMetadataExtractor.ExtractSummariesFromProto(pbBytes);
            AssertEqual(1, parsed.Count, "Parsed 1 entry with Fixed64 + nanosecond timestamp");

            DateTime expectedUtc = new DateTime(2025, 11, 10, 12, 0, 0, DateTimeKind.Utc);
            AssertEqual(expectedUtc, parsed[0].UpdatedTime.ToUniversalTime(), "Fixed64 nanosecond timestamp parsed to exact UTC time");
            AssertEqual(expectedUtc, parsed[0].CreatedTime.ToUniversalTime(), "Nested Varint nanosecond timestamp parsed to exact UTC time");
            AssertEqual("outside-of-project", parsed[0].ProjectId, "outside-of-project ProjectId preserved");
            AssertEqual("Outside of Project", parsed[0].ProjectName, "outside-of-project ProjectName normalized");
            AssertTrue(string.IsNullOrEmpty(parsed[0].FolderUri), "Stale FolderUri cleared for outside-of-project session");

            AssertEqual("Drive_C", SessionMetadataExtractor.ExtractProjectNameFromUri("file:///c:/"), "Drive root c:/ becomes Drive_C");
            AssertEqual("Drive_D", SessionMetadataExtractor.ExtractProjectNameFromUri("file:///d%3A"), "Encoded drive root d%3A becomes Drive_D");
            AssertEqual("MyApp", SessionMetadataExtractor.ExtractProjectNameFromUri("file:///e%3A/Work/MyApp/"), "Trailing-slash workspace URI extracts folder basename");
        }

        private static void T27_VisualDiffEngine_CapMissingPathsLocalOnlyCountAndSig()
        {
            // 1. ContentSignature fallback when callers don't precompute signatures + missing path badges
            CapabilityItem localRule = new CapabilityItem { Kind = CapabilityKind.Rule, Id = "r1", DisplayName = "r1", ContentSignature = null };
            localRule.FilesMap["_root/GEMINI.md"] = Encoding.UTF8.GetBytes("# Shared Rule\n");
            CapabilityItem remoteSame = new CapabilityItem { Kind = CapabilityKind.Rule, Id = "r1", DisplayName = "r1", ContentSignature = null };
            remoteSame.FilesMap["_root/GEMINI.md"] = Encoding.UTF8.GetBytes("# Shared Rule\n");

            List<CapabilityDiffNode> alignedDiff = VisualDiffEngine.ComputeCapabilityDiff(
                new List<CapabilityItem> { localRule },
                new List<CapabilityItem> { remoteSame });
            AssertEqual(1, alignedDiff.Count, "1 merged capability diff node");
            AssertEqual(DiffStatus.Aligned, alignedDiff[0].Status, "Missing ContentSignature computes automatically and matches Aligned");
            AssertEqual("[已对齐]", alignedDiff[0].GetStatusBadge(), "Aligned capability without missing paths has clean badge");

            remoteSame.FilesMap["_root/GEMINI.md"] = Encoding.UTF8.GetBytes("# Modified Rule\n");
            remoteSame.ContentSignature = null;
            remoteSame.MissingLocalPaths.Add("C:\\missing\\remote.py");
            List<CapabilityDiffNode> modRemoteMiss = VisualDiffEngine.ComputeCapabilityDiff(
                new List<CapabilityItem> { localRule },
                new List<CapabilityItem> { remoteSame });
            AssertEqual(DiffStatus.Modified, modRemoteMiss[0].Status, "Changed content with null signatures detects Modified");
            AssertTrue(modRemoteMiss[0].GetStatusBadge().Contains("路径缺失"), "Remote missing path annotated in badge");

            localRule.MissingLocalPaths.Add("C:\\missing\\local.py");
            AssertTrue(modRemoteMiss[0].GetStatusBadge().Contains("双侧路径缺失"), "Both missing paths annotated in badge");
            modRemoteMiss[0].RemoteItem.MissingLocalPaths.Clear();
            AssertTrue(modRemoteMiss[0].GetStatusBadge().Contains("本机路径缺失"), "Local-only missing path annotated in badge");

            // 2. ProjectDiffNode.LocalOnlySessionCount & single-side MinValue time guard
            DateTime t0 = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
            ConversationSummaryInfo cLocalShared = new ConversationSummaryInfo
            {
                ConversationId = "11112222-3333-4444-5555-666677778888",
                ProjectId = "p-loc",
                ProjectName = "RepoProj",
                FolderUri = "file:///d%3A/Work/RepoProj",
                DisplayTitle = "Shared",
                StepCount = 5,
                UpdatedTime = t0
            };
            ConversationSummaryInfo cLocalExtra = new ConversationSummaryInfo
            {
                ConversationId = "22223333-4444-5555-6666-777788889999",
                ProjectId = "p-loc",
                ProjectName = "RepoProj",
                FolderUri = "file:///d%3A/Work/RepoProj",
                DisplayTitle = "Local Only Session",
                StepCount = 2,
                UpdatedTime = t0
            };
            ConversationSummaryInfo cBackupAligned = new ConversationSummaryInfo
            {
                ConversationId = "11112222-3333-4444-5555-666677778888",
                ProjectId = "p-bak",
                ProjectName = "RepoProj",
                FolderUri = "file:///c%3A/Work/RepoProj",
                DisplayTitle = "Shared",
                StepCount = 5,
                UpdatedTime = t0
            };

            List<ProjectDiffNode> pDiffs = VisualDiffEngine.ComputeDiff(
                new List<ConversationSummaryInfo> { cLocalShared, cLocalExtra },
                new List<ConversationSummaryInfo> { cBackupAligned },
                "C:\\Work",
                "D:\\Work");
            AssertEqual(1, pDiffs.Count, "RepoProj merged across machines");
            AssertEqual(DiffStatus.Aligned, pDiffs[0].Status, "Aligned project with local-only extra session keeps Aligned status");
            AssertEqual(1, pDiffs[0].LocalOnlySessionCount, "LocalOnlySessionCount tracks 1 local-only session under Aligned project");
            AssertTrue(pDiffs[0].GetStatusBadge().Contains("本机另含 1 条独有"), "Badge displays local-only session count");

            ConversationSummaryInfo cBackupMinTime = new ConversationSummaryInfo
            {
                ConversationId = "11112222-3333-4444-5555-666677778888",
                ProjectId = "p-bak",
                ProjectName = "RepoProj",
                DisplayTitle = "Shared",
                StepCount = 5,
                UpdatedTime = DateTime.MinValue
            };
            ConversationDiffDetail evalSingleMinTime = VisualDiffEngine.EvaluateConversationDiff(cBackupMinTime, cLocalShared, pDiffs[0]);
            AssertTrue(evalSingleMinTime.TimeDiff && evalSingleMinTime.IsModified, "Single-side DateTime.MinValue flags TimeDiff/Modified");

            ConversationSummaryInfo cLocalMinTime = new ConversationSummaryInfo
            {
                ConversationId = "11112222-3333-4444-5555-666677778888",
                ProjectId = "p-loc",
                ProjectName = "RepoProj",
                DisplayTitle = "Shared",
                StepCount = 5,
                UpdatedTime = DateTime.MinValue
            };
            ConversationDiffDetail evalBothMinTime = VisualDiffEngine.EvaluateConversationDiff(cBackupMinTime, cLocalMinTime, pDiffs[0]);
            AssertTrue(!evalBothMinTime.TimeDiff && !evalBothMinTime.IsModified, "Both-side DateTime.MinValue does not flag TimeDiff/Modified");
        }

        private static void T28_CapabilityRepo_EncodingBomMultilineJsonAndYaml()
        {
            // 1. Multiline JSON entry reindentation in FormatObjectEntries
            string baseJson = "{\r\n  \"mcpServers\": {}\r\n}";
            string multiFrag = "{\r\n  \"command\": \"node\",\r\n  \"args\": [\r\n    \"a.js\",\r\n    \"b.js\"\r\n  ]\r\n}";
            string mergedJson = UnifiedJson.PatchObjectKeyPreservingComments(baseJson, "mcpServers", "multi-srv", multiFrag);
            AssertTrue(mergedJson.Contains("      \"args\": ["), "Nested multiline array reindented to 6 spaces");
            Dictionary<string, string> parsedMcp = CapabilityVaultService.ExtractObjectEntries(mergedJson, "mcpServers");
            AssertTrue(parsedMcp.ContainsKey("multi-srv") && parsedMcp["multi-srv"].Contains("\"b.js\""), "Multiline JSON entry roundtrips through ExtractObjectEntries");

            // 2. UTF-8 BOM + Raw GBK byte preservation + YAML block/folded/quoted scalars + zero leftover .tmp files
            string sb = Path.Combine(Path.GetTempPath(), "agy_cap_enc_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeSrc = Path.Combine(sb, "srcHome");
                string geminiSrc = Path.Combine(homeSrc, ".gemini");
                string rulesDirSrc = Path.Combine(geminiSrc, "config", "rules");
                string skillsDirSrc = Path.Combine(geminiSrc, "config", "skills");
                Directory.CreateDirectory(rulesDirSrc);
                Directory.CreateDirectory(skillsDirSrc);

                // Root rule with UTF-8 BOM and physical home reference
                byte[] utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF };
                byte[] rootRuleBody = Encoding.UTF8.GetBytes("# 全局规则\r\ntools=" + homeSrc.Replace('\\', '/') + "/tools\r\n");
                byte[] rootRuleBytes = new byte[utf8Bom.Length + rootRuleBody.Length];
                Buffer.BlockCopy(utf8Bom, 0, rootRuleBytes, 0, utf8Bom.Length);
                Buffer.BlockCopy(rootRuleBody, 0, rootRuleBytes, utf8Bom.Length, rootRuleBody.Length);
                File.WriteAllBytes(Path.Combine(geminiSrc, "GEMINI.md"), rootRuleBytes);

                // Legacy invalid-UTF8 GBK bytes ("中文\n" in CP936: D6 D0 CE C4 0A)
                byte[] gbkBytes = new byte[] { 0xD6, 0xD0, 0xCE, 0xC4, 0x0A };
                File.WriteAllBytes(Path.Combine(rulesDirSrc, "legacy-gbk.md"), gbkBytes);

                // Skill 1: BOM + YAML literal block scalar |-
                string sBlockDir = Path.Combine(skillsDirSrc, "yaml-block");
                Directory.CreateDirectory(sBlockDir);
                byte[] sBlockBody = Encoding.UTF8.GetBytes("---\nname: yaml-block\ndescription: |-\n  Line one\n  Line two\n---\n# Body\n");
                byte[] sBlockBytes = new byte[utf8Bom.Length + sBlockBody.Length];
                Buffer.BlockCopy(utf8Bom, 0, sBlockBytes, 0, utf8Bom.Length);
                Buffer.BlockCopy(sBlockBody, 0, sBlockBytes, utf8Bom.Length, sBlockBody.Length);
                File.WriteAllBytes(Path.Combine(sBlockDir, "SKILL.md"), sBlockBytes);

                // Skill 2: YAML folded scalar >
                string sFoldedDir = Path.Combine(skillsDirSrc, "yaml-folded");
                Directory.CreateDirectory(sFoldedDir);
                File.WriteAllText(Path.Combine(sFoldedDir, "SKILL.md"), "---\ndescription: >\n  Folded alpha\n  Folded beta\n---\n", new UTF8Encoding(false));

                // Skill 3: YAML single-quoted scalar
                string sQuotedDir = Path.Combine(skillsDirSrc, "yaml-quoted");
                Directory.CreateDirectory(sQuotedDir);
                File.WriteAllText(Path.Combine(sQuotedDir, "SKILL.md"), "---\ndescription: 'Single quoted desc'\n---\n", new UTF8Encoding(false));

                List<CapabilityItem> localCaps = CapabilityRepositoryManager.ScanLocalCapabilities(geminiSrc, homeSrc);
                CapabilityItem blockCap = localCaps.Find(x => x.Kind == CapabilityKind.Skill && x.Id == "yaml-block");
                CapabilityItem foldedCap = localCaps.Find(x => x.Kind == CapabilityKind.Skill && x.Id == "yaml-folded");
                CapabilityItem quotedCap = localCaps.Find(x => x.Kind == CapabilityKind.Skill && x.Id == "yaml-quoted");
                CapabilityItem gbkRuleCap = localCaps.Find(x => x.Kind == CapabilityKind.Rule && x.Id == "rules/legacy-gbk.md");
                CapabilityItem bomRootRuleCap = localCaps.Find(x => x.Kind == CapabilityKind.Rule && x.Id == "GEMINI.md");

                AssertEqual("Line one Line two", blockCap.Description, "YAML literal |- scalar description parsed after stripping BOM");
                AssertEqual("Folded alpha Folded beta", foldedCap.Description, "YAML folded > scalar description parsed");
                AssertEqual("Single quoted desc", quotedCap.Description, "YAML single-quoted scalar description unquoted");
                AssertTrue(gbkRuleCap != null && gbkRuleCap.FilesMap.ContainsKey("config/legacy-gbk.md"), "GBK rule scanned");
                byte[] scannedGbk = gbkRuleCap.FilesMap["config/legacy-gbk.md"];
                AssertEqual(gbkBytes.Length, scannedGbk.Length, "Raw GBK byte length preserved on scan");
                for (int i = 0; i < gbkBytes.Length; i++) AssertEqual((int)gbkBytes[i], (int)scannedGbk[i], "GBK byte[" + i + "] preserved verbatim");

                byte[] scannedBomRule = bomRootRuleCap.FilesMap["_root/GEMINI.md"];
                AssertTrue(scannedBomRule.Length >= 3 && scannedBomRule[0] == 0xEF && scannedBomRule[1] == 0xBB && scannedBomRule[2] == 0xBF, "UTF-8 BOM preserved when normalizing root rule");

                string repoDir = Path.Combine(sb, "Repo");
                int pushed = CapabilityRepositoryManager.PushCapabilitiesToRepository(repoDir, homeSrc, localCaps);
                AssertTrue(pushed >= 5, "Pushed rules and YAML skills to Repository");

                string homeDst = Path.Combine(sb, "dstHome");
                string geminiDst = Path.Combine(homeDst, ".gemini");
                List<CapabilityItem> repoCaps = CapabilityRepositoryManager.ScanRepositoryCapabilities(repoDir, homeDst, geminiDst);
                int imported = CapabilityRepositoryManager.ImportCapabilitiesToLocal(geminiDst, homeDst, repoCaps);
                AssertTrue(imported >= 5, "Imported capabilities cleanly to destination home");

                byte[] dstRootBytes = File.ReadAllBytes(Path.Combine(geminiDst, "GEMINI.md"));
                AssertTrue(dstRootBytes.Length >= 3 && dstRootBytes[0] == 0xEF && dstRootBytes[1] == 0xBB && dstRootBytes[2] == 0xBF, "Imported GEMINI.md preserves UTF-8 BOM");
                string dstRootText = Encoding.UTF8.GetString(dstRootBytes, 3, dstRootBytes.Length - 3);
                AssertTrue(dstRootText.Contains(homeDst.Replace('\\', '/') + "/tools"), "Imported GEMINI.md expanded {{HOME}} cleanly");

                byte[] dstGbkBytes = File.ReadAllBytes(Path.Combine(geminiDst, "config", "rules", "legacy-gbk.md"));
                AssertEqual(gbkBytes.Length, dstGbkBytes.Length, "Imported GBK rule preserves exact byte count");
                for (int i = 0; i < gbkBytes.Length; i++) AssertEqual((int)gbkBytes[i], (int)dstGbkBytes[i], "Imported GBK byte[" + i + "] preserved without U+FFFD");

                AssertEqual(0, Directory.GetFiles(repoDir, "*.tmp.*", SearchOption.AllDirectories).Length, "Zero stale .tmp files left in Repo");
                AssertEqual(0, Directory.GetFiles(geminiDst, "*.tmp.*", SearchOption.AllDirectories).Length, "Zero stale .tmp files left in Destination");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T29_SessionLifecycle_ProjectIdIsolationAndSubagentDelete()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t29_" + Guid.NewGuid().ToString("N"));
            try
            {
                string geminiDir = Path.Combine(sb, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                string convDir = Path.Combine(agDir, "conversations");
                string brainDir = Path.Combine(agDir, "brain");
                string projDir = Path.Combine(geminiDir, "config", "projects");
                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(brainDir);
                Directory.CreateDirectory(projDir);

                string cid1 = "a2900001-0000-4000-8000-000000000001";
                string cid1Sub = "a2900002-0000-4000-8000-000000000002";
                string cid2 = "b2900003-0000-4000-8000-000000000003";
                string pid1 = "pid29001";
                string pid2 = "pid29002";

                // Same project display name "App", two distinct project IDs
                File.WriteAllText(Path.Combine(projDir, pid1 + ".json"),
                    WorkspaceService.GenerateStandardProjectJson(pid1, "App", "D:\\WorkA\\App"), Encoding.UTF8);
                File.WriteAllText(Path.Combine(projDir, pid2 + ".json"),
                    WorkspaceService.GenerateStandardProjectJson(pid2, "App", "D:\\WorkB\\App"), Encoding.UTF8);

                ProtoNode n1 = BuildRealProtoEntry(cid1, "Root A", null, "file:///d%3A/WorkA/App", "App", pid1, 6, 2, 1);
                ProtoNode n1Sub = BuildRealProtoEntry(cid1Sub, "Sub A", null, "file:///d%3A/WorkA/App", "App", pid1, 3, 1, 0);
                n1Sub.GetChild(2).GetChild(17).Children.Add(new ProtoNode(11, 2) { RawBytes = Encoding.UTF8.GetBytes(cid1), IsModified = true });
                ProtoNode n2 = BuildRealProtoEntry(cid2, "Root B", null, "file:///d%3A/WorkB/App", "App", pid2, 4, 1, 1);

                byte[] pb = ProtoCodec.Encode(new List<ProtoNode> { n1, n1Sub, n2 });
                File.WriteAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb"), pb);
                CreateMinimalValidSqliteDb(Path.Combine(convDir, cid1 + ".db"), cid1);
                CreateMinimalValidSqliteDb(Path.Combine(convDir, cid1Sub + ".db"), cid1Sub);
                CreateMinimalValidSqliteDb(Path.Combine(convDir, cid2 + ".db"), cid2);

                Dictionary<string, ProjectMetadata> projects = WorkspaceService.LoadLocalProjects(geminiDir);
                List<ConversationSummaryInfo> sums = SessionMetadataExtractor.ExtractSummaries(pb, projects);

                DeletionResult del = SessionLifecycleService.DeleteSessionsAndProjects(
                    geminiDir,
                    sums,
                    projects,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { cid1 },
                    null,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { pid1 },
                    null);

                AssertTrue(del.DeletedSessionCount >= 2, "Deleting parent session cid1 cascades to subagent cid1Sub");
                AssertTrue(!File.Exists(Path.Combine(convDir, cid1 + ".db")) && !File.Exists(Path.Combine(convDir, cid1Sub + ".db")),
                    "Parent and subagent .db removed");
                AssertTrue(File.Exists(Path.Combine(convDir, cid2 + ".db")), "Same-name sibling project pid2 conversation untouched");
                AssertTrue(!File.Exists(Path.Combine(projDir, pid1 + ".json")) && File.Exists(Path.Combine(projDir, pid2 + ".json")),
                    "Deleted pid1.json while isolating same-name pid2.json");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T30_CapabilityRepo_ScriptPathRegexQuotedEnvAndSpaces()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_path_rx_" + Guid.NewGuid().ToString("N"));
            string prevEnv = Environment.GetEnvironmentVariable("AGY_TEST_MISSING_DIR");
            try
            {
                string spaceDir = Path.Combine(sb, "dir with space");
                Directory.CreateDirectory(spaceDir);
                string dQuotedPy = Path.Combine(spaceDir, "run tool.py");
                string sQuotedPy = Path.Combine(spaceDir, "single tool.py");
                File.WriteAllText(dQuotedPy, "# ok", Encoding.UTF8);
                File.WriteAllText(sQuotedPy, "# ok", Encoding.UTF8);

                Environment.SetEnvironmentVariable("AGY_TEST_MISSING_DIR", "Z:\\AgyEnvMissing");

                string cmdLine = string.Format(
                    "\"{0}\" '{1}' \"~/missing_tilde.py\" \"%AGY_TEST_MISSING_DIR%\\missing_env.py\" --extra=Z:\\NonExistent\\missing_unquoted.py",
                    dQuotedPy, sQuotedPy);
                string jsonFrag = "{\"command\": \"" + UnifiedJson.EscapeString(cmdLine) + "\"}";

                List<string> referenced;
                List<string> missing;
                CapabilityPathNormalizer.DetectReferencedAndMissingPaths(
                    jsonFrag,
                    "Z:\\AgySpacelessHome",
                    Path.Combine(sb, ".gemini"),
                    null,
                    out referenced,
                    out missing);

                AssertEqual(5, referenced.Count, "Extracted all 5 referenced paths (double-quoted, single-quoted, tilde, %ENV%, =unquoted)");
                AssertEqual(3, missing.Count, "Identified exact 3 missing paths while confirming existing spaced script paths");
                AssertTrue(missing.Contains("Z:\\AgySpacelessHome\\missing_tilde.py"), "Tilde ~/ expanded with spaceless home");
                AssertTrue(missing.Contains("Z:\\AgyEnvMissing\\missing_env.py"), "%ENV% variable expanded");
                AssertTrue(missing.Contains("Z:\\NonExistent\\missing_unquoted.py"), "--flag=unquoted path matched");
            }
            finally
            {
                Environment.SetEnvironmentVariable("AGY_TEST_MISSING_DIR", prevEnv);
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T31_CapabilityVault_RelocateCapabilityPath_LocalAndRepo()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t31_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string cfgDir = Path.Combine(geminiDir, "config");
                string repoDir = Path.Combine(sb, "Repo");
                Directory.CreateDirectory(cfgDir);
                Directory.CreateDirectory(Path.Combine(repoDir, "mcps"));

                string oldPy = Path.Combine(sb, "old_dir", "tool.py");
                string newPy = Path.Combine(sb, "new_dir", "tool.py");
                Directory.CreateDirectory(Path.GetDirectoryName(newPy));
                File.WriteAllText(newPy, "# tool", Encoding.UTF8);

                string mcpPath = Path.Combine(cfgDir, "mcp_config.json");
                File.WriteAllText(mcpPath,
                    "{\r\n  // keep comment\r\n  \"mcpServers\": {\r\n    \"srv-1\": { \"command\": \"python\", \"args\": [\"" + UnifiedJson.EscapeString(oldPy) + "\"] }\r\n  }\r\n}",
                    Encoding.UTF8);

                List<CapabilityItem> localCaps = CapabilityVaultService.ScanLocalCapabilities(geminiDir, homeDir);
                CapabilityItem srvLocal = localCaps.Find(x => x.Kind == CapabilityKind.McpServer && x.Id == "srv-1");
                AssertTrue(srvLocal != null, "Local MCP srv-1 scanned");
                bool okLocal = CapabilityVaultService.RelocateCapabilityPath(geminiDir, repoDir, true, homeDir, srvLocal, oldPy, newPy);
                AssertTrue(okLocal, "RelocateCapabilityPath returns true for local MCP");
                string updatedLocal = File.ReadAllText(mcpPath, Encoding.UTF8);
                AssertTrue(updatedLocal.Contains("// keep comment") && !updatedLocal.Contains("old_dir"), "Local relocate updates path and preserves JSONC comments");

                CapabilityVaultService.PushCapabilitiesToRepository(repoDir, homeDir, new List<CapabilityItem> { CapabilityVaultService.ScanLocalCapabilities(geminiDir, homeDir).Find(x => x.Id == "srv-1") });
                List<CapabilityItem> repoCaps = CapabilityVaultService.ScanRepositoryCapabilities(repoDir, homeDir, geminiDir);
                CapabilityItem srvRepo = repoCaps.Find(x => x.Id == "srv-1");
                string thirdPy = Path.Combine(sb, "third_dir", "tool.py");
                Directory.CreateDirectory(Path.GetDirectoryName(thirdPy));
                File.WriteAllText(thirdPy, "# tool3", Encoding.UTF8);
                bool okRepo = CapabilityVaultService.RelocateCapabilityPath(geminiDir, repoDir, false, homeDir, srvRepo, newPy, thirdPy);
                AssertTrue(okRepo, "RelocateCapabilityPath updates repository fragment");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Three-Layer Rename, SQLite FTS5 Bridge & Workbench Tests (T32 ~ T36)
        // ══════════════════════════════════════════════════════════════════════

        private static string T_GetSqliteErr(IntPtr db)
        {
            if (db == IntPtr.Zero) return "db is null";
            IntPtr ptr = t_sqlite3_errmsg(db);
            if (ptr == IntPtr.Zero) return "unknown";
            int len = 0;
            while (len < 1024 && Marshal.ReadByte(ptr, len) != 0) len++;
            byte[] buf = new byte[len];
            Marshal.Copy(ptr, buf, 0, len);
            return Encoding.UTF8.GetString(buf);
        }

        private static int T_BindUtf8Text(IntPtr stmt, int col, string text)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(text ?? string.Empty);
            byte[] pinned = utf8.Length > 0 ? utf8 : new byte[] { 0 };
            GCHandle pin = GCHandle.Alloc(pinned, GCHandleType.Pinned);
            try
            {
                return t_sqlite3_bind_text(stmt, col, pin.AddrOfPinnedObject(), utf8.Length, new IntPtr(-1));
            }
            finally
            {
                if (pin.IsAllocated) pin.Free();
            }
        }

        private static string T_ReadColText(IntPtr stmt, int col)
        {
            if (t_sqlite3_column_type(stmt, col) == 5) return null;
            IntPtr ptr = t_sqlite3_column_text(stmt, col);
            if (ptr == IntPtr.Zero) return string.Empty;
            int len = t_sqlite3_column_bytes(stmt, col);
            if (len <= 0) return string.Empty;
            byte[] buf = new byte[len];
            Marshal.Copy(ptr, buf, 0, len);
            return Encoding.UTF8.GetString(buf);
        }

        private static byte[] BuildRawSummaryMetaBytes(string autoTitle, string customTitle, string branchHint, int stepCount, int? extraField22)
        {
            List<ProtoNode> metaChildren = new List<ProtoNode>();
            if (!string.IsNullOrEmpty(autoTitle))
                metaChildren.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(autoTitle), IsModified = true });
            metaChildren.Add(new ProtoNode(2, 0) { VarintValue = (ulong)stepCount, IsModified = true });

            ProtoNode f9 = new ProtoNode(9, 2) { IsModified = true };
            f9.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes("file:///d%3A/Work/Inverter"), IsModified = true });
            if (!string.IsNullOrEmpty(branchHint))
            {
                ProtoNode f3 = new ProtoNode(3, 2) { IsModified = true };
                f3.Children.Add(new ProtoNode(4, 2) { RawBytes = Encoding.UTF8.GetBytes(branchHint), IsModified = true });
                f9.Children.Add(f3);
            }
            metaChildren.Add(f9);

            if (!string.IsNullOrEmpty(customTitle))
            {
                ProtoNode f15 = new ProtoNode(15, 2) { IsModified = true };
                f15.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(customTitle), IsModified = true });
                ProtoNode ts = new ProtoNode(7, 2) { IsModified = true };
                ts.Children.Add(new ProtoNode(1, 0) { VarintValue = 1756000000UL, IsModified = true });
                f15.Children.Add(ts);
                metaChildren.Add(f15);
            }

            if (extraField22.HasValue)
            {
                metaChildren.Add(new ProtoNode(22, 0) { VarintValue = (ulong)extraField22.Value, IsModified = true });
            }

            return ProtoCodec.Encode(metaChildren);
        }

        private static IntPtr OpenAndCreateSummaryWalDb(string dbPath)
        {
            IntPtr db;
            int rc = t_sqlite3_open_v2(Utf8Z(dbPath), out db, 0x00000002 | 0x00000004, IntPtr.Zero);
            AssertEqual(0, rc, "winsqlite3_open_v2 creates summary DB: " + dbPath);

            AssertEqual(0, t_sqlite3_exec(db, Utf8Z("PRAGMA journal_mode=WAL;"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Enable WAL on summary DB: " + T_GetSqliteErr(db));
            AssertEqual(0, t_sqlite3_exec(db, Utf8Z("PRAGMA wal_autocheckpoint=0;"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Disable autocheckpoint");

            string summariesDdl =
                "CREATE TABLE conversation_summaries (" +
                "conversation_id TEXT PRIMARY KEY NOT NULL," +
                "title TEXT NOT NULL," +
                "preview TEXT NOT NULL," +
                "step_count INTEGER NOT NULL," +
                "last_modified_time TEXT NOT NULL," +
                "workspace_uris TEXT NOT NULL," +
                "status TEXT NOT NULL," +
                "source TEXT NOT NULL," +
                "project_id TEXT NOT NULL," +
                "agent_name TEXT NOT NULL," +
                "parent_conversation_id TEXT NOT NULL," +
                "nesting_depth INTEGER NOT NULL," +
                "battle_id TEXT NOT NULL," +
                "winning_conversation_id TEXT NOT NULL," +
                "not_fully_idle INTEGER NOT NULL," +
                "killed INTEGER NOT NULL," +
                "last_user_input_time TEXT NOT NULL," +
                "last_user_input_step_index INTEGER NOT NULL," +
                "app_data_dir TEXT NOT NULL," +
                "raw_summary BLOB NOT NULL," +
                "group_id TEXT NOT NULL" +
                ");";
            AssertEqual(0, t_sqlite3_exec(db, Utf8Z(summariesDdl), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Create conversation_summaries DDL: " + T_GetSqliteErr(db));

            string ftsDdl =
                "CREATE VIRTUAL TABLE conversations_fts USING fts5(" +
                "conversation_id UNINDEXED," +
                "chunk_index UNINDEXED," +
                "title," +
                "workspace," +
                "project," +
                "user_body," +
                "agent_body," +
                "tokenize='unicode61'" +
                ");";
            AssertEqual(0, t_sqlite3_exec(db, Utf8Z(ftsDdl), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Create conversations_fts unicode61 DDL: " + T_GetSqliteErr(db));

            string parentsDdl =
                "CREATE TABLE conversation_parents (" +
                "conversation_id TEXT NOT NULL," +
                "parent_id TEXT NOT NULL," +
                "PRIMARY KEY(conversation_id, parent_id)" +
                ");";
            AssertEqual(0, t_sqlite3_exec(db, Utf8Z(parentsDdl), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Create conversation_parents DDL: " + T_GetSqliteErr(db));
            AssertEqual(0, t_sqlite3_exec(db, Utf8Z("PRAGMA wal_checkpoint(TRUNCATE);"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Checkpoint DDL");
            return db;
        }

        private static void InsertSummaryDbRow(
            IntPtr db,
            string cid,
            string title,
            string preview,
            int stepCount,
            string workspaceUris,
            string status,
            string projectId,
            string agentName,
            string parentCid,
            int nestingDepth,
            int notFullyIdle,
            byte[] rawSummary)
        {
            string sql =
                "INSERT INTO conversation_summaries (" +
                "conversation_id, title, preview, step_count, last_modified_time, workspace_uris, status, source, project_id, agent_name, " +
                "parent_conversation_id, nesting_depth, battle_id, winning_conversation_id, not_fully_idle, killed, last_user_input_time, " +
                "last_user_input_step_index, app_data_dir, raw_summary, group_id" +
                ") VALUES (?, ?, ?, ?, '2026-11-18T08:00:00.000Z', ?, ?, '', ?, ?, ?, ?, '', '', ?, 0, '2026-11-18T08:00:00.000Z', 0, 'antigravity', ?, '');";

            IntPtr stmt = IntPtr.Zero;
            byte[] raw = rawSummary ?? new byte[0];
            byte[] pinned = raw.Length > 0 ? raw : new byte[] { 0 };
            GCHandle pin = GCHandle.Alloc(pinned, GCHandleType.Pinned);
            try
            {
                AssertEqual(0, t_sqlite3_prepare_v2(db, Utf8Z(sql), -1, out stmt, IntPtr.Zero), "Prepare INSERT conversation_summaries: " + T_GetSqliteErr(db));
                AssertEqual(0, T_BindUtf8Text(stmt, 1, cid), "Bind cid");
                AssertEqual(0, T_BindUtf8Text(stmt, 2, title), "Bind title");
                AssertEqual(0, T_BindUtf8Text(stmt, 3, preview), "Bind preview");
                AssertEqual(0, t_sqlite3_bind_int(stmt, 4, stepCount), "Bind step_count");
                AssertEqual(0, T_BindUtf8Text(stmt, 5, workspaceUris), "Bind workspace_uris");
                AssertEqual(0, T_BindUtf8Text(stmt, 6, status), "Bind status");
                AssertEqual(0, T_BindUtf8Text(stmt, 7, projectId), "Bind project_id");
                AssertEqual(0, T_BindUtf8Text(stmt, 8, agentName), "Bind agent_name");
                AssertEqual(0, T_BindUtf8Text(stmt, 9, parentCid), "Bind parent_conversation_id");
                AssertEqual(0, t_sqlite3_bind_int(stmt, 10, nestingDepth), "Bind nesting_depth");
                AssertEqual(0, t_sqlite3_bind_int(stmt, 11, notFullyIdle), "Bind not_fully_idle");
                AssertEqual(0, t_sqlite3_bind_blob(stmt, 12, pin.AddrOfPinnedObject(), raw.Length, new IntPtr(-1)), "Bind raw_summary");
                AssertEqual(101, t_sqlite3_step(stmt), "Step INSERT conversation_summaries: " + T_GetSqliteErr(db));
            }
            finally
            {
                if (stmt != IntPtr.Zero) t_sqlite3_finalize(stmt);
                if (pin.IsAllocated) pin.Free();
            }
        }

        private static void InsertFtsDbRow(
            IntPtr db,
            string cid,
            int chunkIndex,
            string title,
            string workspace,
            string project,
            string userBody,
            string agentBody)
        {
            string sql = "INSERT INTO conversations_fts (conversation_id, chunk_index, title, workspace, project, user_body, agent_body) VALUES (?, ?, ?, ?, ?, ?, ?);";
            IntPtr stmt = IntPtr.Zero;
            try
            {
                AssertEqual(0, t_sqlite3_prepare_v2(db, Utf8Z(sql), -1, out stmt, IntPtr.Zero), "Prepare INSERT conversations_fts: " + T_GetSqliteErr(db));
                AssertEqual(0, T_BindUtf8Text(stmt, 1, cid), "Bind fts cid");
                AssertEqual(0, t_sqlite3_bind_int(stmt, 2, chunkIndex), "Bind chunk_index");
                AssertEqual(0, T_BindUtf8Text(stmt, 3, title), "Bind fts title");
                AssertEqual(0, T_BindUtf8Text(stmt, 4, workspace), "Bind fts workspace");
                AssertEqual(0, T_BindUtf8Text(stmt, 5, project), "Bind fts project");
                AssertEqual(0, T_BindUtf8Text(stmt, 6, userBody), "Bind fts user_body");
                AssertEqual(0, T_BindUtf8Text(stmt, 7, agentBody), "Bind fts agent_body");
                AssertEqual(101, t_sqlite3_step(stmt), "Step INSERT conversations_fts: " + T_GetSqliteErr(db));
            }
            finally
            {
                if (stmt != IntPtr.Zero) t_sqlite3_finalize(stmt);
            }
        }

        private static void InsertParentDbRow(IntPtr db, string cid, string parentId)
        {
            string sql = "INSERT INTO conversation_parents (conversation_id, parent_id) VALUES (?, ?);";
            IntPtr stmt = IntPtr.Zero;
            try
            {
                AssertEqual(0, t_sqlite3_prepare_v2(db, Utf8Z(sql), -1, out stmt, IntPtr.Zero), "Prepare INSERT conversation_parents");
                AssertEqual(0, T_BindUtf8Text(stmt, 1, cid), "Bind child cid");
                AssertEqual(0, T_BindUtf8Text(stmt, 2, parentId), "Bind parent cid");
                AssertEqual(101, t_sqlite3_step(stmt), "Step INSERT conversation_parents");
            }
            finally
            {
                if (stmt != IntPtr.Zero) t_sqlite3_finalize(stmt);
            }
        }

        private static string QuerySingleText(string dbPath, string sql, string param1)
        {
            IntPtr db = IntPtr.Zero;
            IntPtr stmt = IntPtr.Zero;
            try
            {
                AssertEqual(0, t_sqlite3_open_v2(Utf8Z(dbPath), out db, 0x00000001, IntPtr.Zero), "Open readonly for QuerySingleText");
                AssertEqual(0, t_sqlite3_prepare_v2(db, Utf8Z(sql), -1, out stmt, IntPtr.Zero), "Prepare QuerySingleText");
                if (param1 != null) AssertEqual(0, T_BindUtf8Text(stmt, 1, param1), "Bind param1");
                int rc = t_sqlite3_step(stmt);
                if (rc == 100) return T_ReadColText(stmt, 0);
                return null;
            }
            finally
            {
                if (stmt != IntPtr.Zero) t_sqlite3_finalize(stmt);
                if (db != IntPtr.Zero) t_sqlite3_close(db);
            }
        }

        private static int QuerySingleInt(string dbPath, string sql, string param1)
        {
            IntPtr db = IntPtr.Zero;
            IntPtr stmt = IntPtr.Zero;
            try
            {
                AssertEqual(0, t_sqlite3_open_v2(Utf8Z(dbPath), out db, 0x00000001, IntPtr.Zero), "Open readonly for QuerySingleInt");
                AssertEqual(0, t_sqlite3_prepare_v2(db, Utf8Z(sql), -1, out stmt, IntPtr.Zero), "Prepare QuerySingleInt");
                if (param1 != null) AssertEqual(0, T_BindUtf8Text(stmt, 1, param1), "Bind param1");
                int rc = t_sqlite3_step(stmt);
                if (rc == 100) return t_sqlite3_column_int(stmt, 0);
                return 0;
            }
            finally
            {
                if (stmt != IntPtr.Zero) t_sqlite3_finalize(stmt);
                if (db != IntPtr.Zero) t_sqlite3_close(db);
            }
        }

        private static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        private static void CopyDirectoryRecursive(string srcDir, string dstDir)
        {
            Directory.CreateDirectory(dstDir);
            string[] files = Directory.GetFiles(srcDir);
            for (int i = 0; i < files.Length; i++)
            {
                File.Copy(files[i], Path.Combine(dstDir, Path.GetFileName(files[i])), true);
            }
            string[] dirs = Directory.GetDirectories(srcDir);
            for (int i = 0; i < dirs.Length; i++)
            {
                CopyDirectoryRecursive(dirs[i], Path.Combine(dstDir, Path.GetFileName(dirs[i])));
            }
        }

        private static void T32_PbtxtAnnotationCodec_RoundTripAndCreate()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_pbtxt_codec_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(sb);

                // 1. Preserve all non-title fields verbatim (both trailing and leading/trailing layouts)
                string tailFields = "pinned:true archived:false archival_status_timestamp:{seconds:1750000000 nanos:500} last_user_view_time:{seconds:1760000000 nanos:999}";
                string existingLeadingTitle = "title:\"Original Title\" " + tailFields;
                string updatedLeading = PbtxtAnnotationCodec.UpsertTitle(existingLeadingTitle, "【Companion】Updated Title");
                AssertEqual("【Companion】Updated Title", PbtxtAnnotationCodec.ExtractTitle(updatedLeading), "ExtractTitle returns updated title");
                AssertTrue(updatedLeading.EndsWith(" " + tailFields, StringComparison.Ordinal), "Preserves pinned/archived/timestamps verbatim after title");

                string existingMiddleTitle = "pinned:true\ntitle: \"Old Mid\"\narchived:false\narchival_status_timestamp:{seconds:1750000000}\nlast_user_view_time:{seconds:1760000000}";
                string updatedMiddle = PbtxtAnnotationCodec.UpsertTitle(existingMiddleTitle, "Mid Replaced");
                AssertEqual(
                    "pinned:true\ntitle: \"Mid Replaced\"\narchived:false\narchival_status_timestamp:{seconds:1750000000}\nlast_user_view_time:{seconds:1760000000}",
                    updatedMiddle,
                    "Preserves prefix and suffix around middle title verbatim");

                // 2. Missing title: prepending and empty/null creation
                string noTitleContent = "pinned:true archived:false archival_status_timestamp:{seconds:1751111111} last_user_view_time:{seconds:1761111111}";
                string prepended = PbtxtAnnotationCodec.UpsertTitle(noTitleContent, "【3KU】Prepended Title");
                AssertEqual("title:\"【3KU】Prepended Title\" " + noTitleContent, prepended, "Prepends title: when missing and keeps all existing fields verbatim");
                AssertEqual("【3KU】Prepended Title", PbtxtAnnotationCodec.ExtractTitle(prepended), "Extracts prepended title");

                string brandNew = PbtxtAnnotationCodec.UpsertTitle(null, "Created Title");
                AssertEqual("title:\"Created Title\"", brandNew, "Creates title-only pbtxt from null input");

                // 3. Escaping quotes, backslashes, and newlines
                string trickyTitle = "Fix \"quoted\" \\\\slash\\path\nLine2\r\n\tTabbed";
                string escapedPbtxt = PbtxtAnnotationCodec.UpsertTitle(tailFields, trickyTitle);
                AssertTrue(escapedPbtxt.Contains("\\\"quoted\\\"") && escapedPbtxt.Contains("\\\\\\\\slash\\\\path") && escapedPbtxt.Contains("\\nLine2\\r\\n\\tTabbed"),
                    "Escapes quotes, backslashes, CR/LF, and tabs");
                AssertEqual(trickyTitle, PbtxtAnnotationCodec.ExtractTitle(escapedPbtxt), "Round-trips quotes/backslashes/newlines cleanly");

                // 4. Multi-byte UTF-8 octal decoding (NOT corrupted char-cast!)
                string octalPbtxt = "title:\"\\343\\200\\2203KU\\343\\200\\221 \\346\\257\\215\\347\\272\\277\" pinned:true";
                AssertEqual("【3KU】 母线", PbtxtAnnotationCodec.ExtractTitle(octalPbtxt), "Decodes multi-byte UTF-8 octal sequences to exact CJK/bracket chars");

                // 5. Assert UTF-8 without BOM when persisted
                string pbtxtFile = Path.Combine(sb, "annot_test.pbtxt");
                LocalWorkspaceManager.WriteUtf8TextAtomicWithBackup(pbtxtFile, prepended, false, null);
                byte[] diskBytes = File.ReadAllBytes(pbtxtFile);
                AssertTrue(diskBytes.Length > 3, "Written pbtxt file is non-empty");
                AssertTrue(!(diskBytes[0] == 0xEF && diskBytes[1] == 0xBB && diskBytes[2] == 0xBF), "Written pbtxt has NO UTF-8 BOM");
                string strictDecoded = new UTF8Encoding(false, true).GetString(diskBytes);
                AssertEqual("【3KU】Prepended Title", PbtxtAnnotationCodec.ExtractTitle(strictDecoded), "Disk pbtxt decodes strictly as UTF-8 without BOM");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T33_SummarySqliteBridge_ThreeLayerAndHybridFts()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_sum_bridge_" + Guid.NewGuid().ToString("N"));
            IntPtr db = IntPtr.Zero;
            try
            {
                Directory.CreateDirectory(sb);
                string dbPath = Path.Combine(sb, "conversation_summaries.db");
                string backupDbPath = Path.Combine(sb, "backup_copy", "summary_backup.db");

                string cid1 = "11111111-1111-4111-8111-111111111111";
                string cid2 = "22222222-2222-4222-8222-222222222222";
                string cid3 = "33333333-3333-4333-8333-333333333333";
                string cidNew = "44444444-4444-4444-8444-444444444444";

                byte[] raw1Orig = BuildRawSummaryMetaBytes("Orig Auto 1", null, "Dev_zy", 15, 777);
                byte[] raw2Orig = BuildRawSummaryMetaBytes("Orig Auto 2", "Orig Custom 2", "Dev_YouNeng_ChuanYin", 42, 888);
                byte[] raw3Orig = BuildRawSummaryMetaBytes("Orig Auto 3", null, null, 7, null);

                db = OpenAndCreateSummaryWalDb(dbPath);
                InsertSummaryDbRow(db, cid1, "Old Title 1", "Old Preview 1", 15, "[\"file:///d%3A/Work/Inverter\"]",
                    "CASCADE_RUN_STATUS_IDLE", "proj-inv", "", "", 0, 0, raw1Orig);
                InsertSummaryDbRow(db, cid2, "Old Title 2", "Old Preview 2", 42, "[\"file:///d%3A/Work/Inverter\"]",
                    "CASCADE_RUN_STATUS_RUNNING", "proj-inv", "research", cid1, 1, 1, raw2Orig);
                InsertSummaryDbRow(db, cid3, "Old Title 3", "Old Preview 3", 7, "[\"file:///d%3A/Work/App\"]",
                    "CASCADE_RUN_STATUS_IDLE", "proj-app", "", "", 0, 0, raw3Orig);

                InsertFtsDbRow(db, cid1, -1, "Old Title 1", "file:///d%3A/Work/Inverter", "Inverter", "", "");
                InsertFtsDbRow(db, cid1, 0, "Old Title 1", "file:///d%3A/Work/Inverter", "Inverter",
                    "Check sBusDeratingChargeCurr and Dev_zy branch /plan behavior", "Agent note 0x41A3");
                InsertFtsDbRow(db, cid2, -1, "Old Title 2", "file:///d%3A/Work/Inverter", "Inverter", "", "");
                InsertFtsDbRow(db, cid2, 0, "Old Title 2", "file:///d%3A/Work/Inverter", "Inverter",
                    "系统触发母线高压保护故障，进度100%完成，符号foo:bar*已记录", "Flag a_b! active");
                InsertFtsDbRow(db, cid3, -1, "Old Title 3", "file:///d%3A/Work/App", "App", "", "");
                InsertFtsDbRow(db, cid3, 0, "Old Title 3", "file:///d%3A/Work/App", "App",
                    "Progress 100 percent only, Flag axb! decoy", "");

                InsertParentDbRow(db, cid2, cid1);

                t_sqlite3_close(db);
                db = IntPtr.Zero;

                // 1. ReadSummaryDbSnapshot reads agent_name, not_fully_idle, FirstPromptSnippet, GitBranchHint
                Dictionary<string, SummaryDbRowInfo> snap = SummarySqliteBridge.ReadSummaryDbSnapshot(dbPath);
                AssertEqual(3, snap.Count, "ReadSummaryDbSnapshot loads all 3 rows");
                AssertEqual("", snap[cid1].AgentName, "cid1 agent_name empty");
                AssertTrue(!snap[cid1].NotFullyIdle, "cid1 not_fully_idle == false");
                AssertTrue(snap[cid1].FirstPromptSnippet.Contains("sBusDeratingChargeCurr"), "cid1 FirstPromptSnippet read from FTS chunk_index=0");
                AssertEqual("Dev_zy", snap[cid1].GitBranchHint, "cid1 GitBranchHint decoded from raw_summary Field 9->3->4");

                AssertEqual("research", snap[cid2].AgentName, "cid2 agent_name == research");
                AssertTrue(snap[cid2].NotFullyIdle, "cid2 not_fully_idle == true");
                AssertEqual("Dev_YouNeng_ChuanYin", snap[cid2].GitBranchHint, "cid2 GitBranchHint decoded");

                // 2. UpdateSummaryTitlesTransaction & RestoreSummaryRowsTransaction
                Dictionary<string, string> renameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                renameMap[cid1] = "【3KU·Dev_zy】New Title 1";
                renameMap[cid2] = "【3KU】New Title 2";

                Dictionary<string, SummaryDbRollbackEntry> rbMap;
                List<string> updWarnings;
                bool updOk = SummarySqliteBridge.UpdateSummaryTitlesTransaction(dbPath, renameMap, out rbMap, out updWarnings);
                AssertTrue(updOk, "UpdateSummaryTitlesTransaction succeeded (" + SummarySqliteBridge.LastError + ")");
                AssertEqual(2, rbMap.Count, "Rollback entries produced for both updated rows");

                Dictionary<string, SummaryDbRowInfo> snapAfter = SummarySqliteBridge.ReadSummaryDbSnapshot(dbPath);
                AssertEqual("【3KU·Dev_zy】New Title 1", snapAfter[cid1].Title, "cid1 title updated");
                AssertEqual("【3KU·Dev_zy】New Title 1", snapAfter[cid1].Preview, "cid1 preview updated");
                AssertEqual("【3KU】New Title 2", snapAfter[cid2].Title, "cid2 (with existing Field 15) title updated");
                AssertEqual("Dev_zy", snapAfter[cid1].GitBranchHint, "cid1 non-title GitBranchHint preserved in raw_summary");

                List<ProtoNode> raw1Nodes = ProtoCodec.Decode(snapAfter[cid1].RawSummaryBytes);
                ProtoCodec.DecodeMetaContainerList(raw1Nodes);
                ProtoNode raw1F1 = raw1Nodes.Find(x => x.FieldNumber == 1);
                ProtoNode raw1F2 = raw1Nodes.Find(x => x.FieldNumber == 2);
                ProtoNode raw1F15 = raw1Nodes.Find(x => x.FieldNumber == 15);
                ProtoNode raw1F22 = raw1Nodes.Find(x => x.FieldNumber == 22);
                AssertEqual("【3KU·Dev_zy】New Title 1", raw1F1.GetString(), "raw_summary Field 1 updated");
                AssertEqual(15, raw1F2.GetInt32(), "raw_summary Field 2 step_count preserved");
                AssertEqual("【3KU·Dev_zy】New Title 1", raw1F15.GetChild(1).GetString(), "raw_summary Field 15->1 updated");
                AssertEqual(777, raw1F22.GetInt32(), "raw_summary non-title sentinel Field 22 preserved");

                AssertEqual("【3KU·Dev_zy】New Title 1", QuerySingleText(dbPath, "SELECT title FROM conversations_fts WHERE conversation_id = ? AND chunk_index = -1;", cid1),
                    "conversations_fts chunk_index=-1 title updated");
                AssertEqual("Old Title 1", QuerySingleText(dbPath, "SELECT title FROM conversations_fts WHERE conversation_id = ? AND chunk_index = 0;", cid1),
                    "conversations_fts chunk_index=0 left untouched");

                List<string> rbWarnings;
                bool rbOk = SummarySqliteBridge.RestoreSummaryRowsTransaction(dbPath, rbMap, out rbWarnings);
                AssertTrue(rbOk, "RestoreSummaryRowsTransaction succeeded (" + SummarySqliteBridge.LastError + ")");
                Dictionary<string, SummaryDbRowInfo> snapRestored = SummarySqliteBridge.ReadSummaryDbSnapshot(dbPath);
                AssertEqual("Old Title 1", snapRestored[cid1].Title, "Restored cid1 title");
                AssertEqual("Old Preview 1", snapRestored[cid1].Preview, "Restored cid1 preview");
                AssertTrue(ByteArraysEqual(raw1Orig, snapRestored[cid1].RawSummaryBytes), "Restored cid1 raw_summary byte-for-byte");
                AssertTrue(ByteArraysEqual(raw2Orig, snapRestored[cid2].RawSummaryBytes), "Restored cid2 raw_summary byte-for-byte");
                AssertEqual("Old Title 1", QuerySingleText(dbPath, "SELECT title FROM conversations_fts WHERE conversation_id = ? AND chunk_index = -1;", cid1),
                    "Restored conversations_fts chunk_index=-1 title");

                // 3. Hybrid FTS5 + LIKE routing & operator/wildcard safety
                HashSet<string> hitFts = SummarySqliteBridge.SearchConversationsFullText(dbPath, "sBusDeratingChargeCurr");
                AssertTrue(SummarySqliteBridge.LastError == null, "FTS5 ASCII identifier query produced no error");
                AssertEqual(1, hitFts.Count, "ASCII identifier hits exact 1 session via FTS5");
                AssertTrue(hitFts.Contains(cid1), "ASCII identifier hits cid1");

                HashSet<string> hitCjk = SummarySqliteBridge.SearchConversationsFullText(dbPath, "母线高压");
                AssertTrue(SummarySqliteBridge.LastError == null, "CJK substring query produced no error");
                AssertEqual(1, hitCjk.Count, "CJK substring hits exact 1 session via LIKE fallback");
                AssertTrue(hitCjk.Contains(cid2), "CJK substring hits cid2 inside unsegmented CJK phrase");

                string[] operatorQueries = new string[] { "Dev_zy", "0x41A3", "/plan", "foo:bar*" };
                string[] expectedOpCids = new string[] { cid1, cid1, cid1, cid2 };
                for (int i = 0; i < operatorQueries.Length; i++)
                {
                    HashSet<string> opHits = SummarySqliteBridge.SearchConversationsFullText(dbPath, operatorQueries[i]);
                    AssertTrue(SummarySqliteBridge.LastError == null, "Special query '" + operatorQueries[i] + "' did not error: " + SummarySqliteBridge.LastError);
                    AssertTrue(opHits.Contains(expectedOpCids[i]), "Special query '" + operatorQueries[i] + "' matched expected session");
                }

                HashSet<string> pctHits = SummarySqliteBridge.SearchConversationsFullText(dbPath, "100%");
                AssertTrue(SummarySqliteBridge.LastError == null, "Percent query produced no error");
                AssertEqual(1, pctHits.Count, "Literal '%' matches ONLY '100%', not '100 percent'");
                AssertTrue(pctHits.Contains(cid2) && !pctHits.Contains(cid3), "Literal '%' did not falsely wildcard cid3");

                HashSet<string> underHits = SummarySqliteBridge.SearchConversationsFullText(dbPath, "a_b!");
                AssertTrue(SummarySqliteBridge.LastError == null, "Underscore literal query produced no error");
                AssertEqual(1, underHits.Count, "Literal '_' matches ONLY 'a_b!', not 'axb!'");
                AssertTrue(underHits.Contains(cid2) && !underHits.Contains(cid3), "Literal '_' did not falsely wildcard cid3");

                HashSet<string> singleUnderHits = SummarySqliteBridge.SearchConversationsFullText(dbPath, "_");
                AssertTrue(SummarySqliteBridge.LastError == null, "Single '_' query produced no error");
                AssertTrue(singleUnderHits.Contains(cid1) && singleUnderHits.Contains(cid2) && !singleUnderHits.Contains(cid3),
                    "Single '_' matches literal '_' rows only and does not wildcard cid3");

                HashSet<string> doubleUnderHits = SummarySqliteBridge.SearchConversationsFullText(dbPath, "__");
                AssertTrue(SummarySqliteBridge.LastError == null, "Double '__' underscore-only query produced no error");
                AssertEqual(0, doubleUnderHits.Count, "Double '__' routes to escaped LIKE and does not wildcard any 2-char string");

                // 4. UpsertSessionsInSummaryDb (existing preserve + new NOT NULL insert), BackupDatabaseToFile (WAL online), DeleteSessionsFromSummaryDb
                byte[] newRowRaw = BuildRawSummaryMetaBytes("Inserted Auto", null, "Dev_New", 3, null);
                List<SummaryDbUpsertItem> upserts = new List<SummaryDbUpsertItem>
                {
                    new SummaryDbUpsertItem
                    {
                        ConversationId = cid2,
                        Title = "Upserted Title 2",
                        Preview = "Upserted Preview 2",
                        ProjectId = "proj-moved",
                        WorkspaceUrisJson = "[\"file:///e%3A/Moved\"]",
                        FtsWorkspaceToken = "file:///e%3A/Moved",
                        ProjectName = "MovedProj"
                    },
                    new SummaryDbUpsertItem
                    {
                        ConversationId = cidNew,
                        Title = "Brand New Inserted",
                        Preview = "Brand New Preview",
                        StepCount = 3,
                        WorkspaceUrisJson = "[\"file:///e%3A/New\"]",
                        ProjectId = "proj-new",
                        ParentConversationId = cid1,
                        NestingDepth = 1,
                        RawSummaryBytes = newRowRaw
                    }
                };
                List<string> upsertWarns;
                int upserted = SummarySqliteBridge.UpsertSessionsInSummaryDb(dbPath, upserts, out upsertWarns);
                AssertEqual(2, upserted, "UpsertSessionsInSummaryDb updated existing and inserted new NOT NULL row");

                Dictionary<string, SummaryDbRowInfo> snapUpserted = SummarySqliteBridge.ReadSummaryDbSnapshot(dbPath);
                AssertEqual(4, snapUpserted.Count, "Snapshot contains 4 rows after upsert");
                AssertEqual("Upserted Title 2", snapUpserted[cid2].Title, "Existing row title updated by upsert");
                AssertEqual(42, snapUpserted[cid2].StepCount, "Existing row step_count preserved by upsert");
                AssertEqual("CASCADE_RUN_STATUS_RUNNING", snapUpserted[cid2].Status, "Existing row status preserved by upsert");
                AssertTrue(snapUpserted[cid2].NotFullyIdle, "Existing row not_fully_idle preserved by upsert");
                AssertEqual("research", snapUpserted[cid2].AgentName, "Existing row agent_name preserved by upsert");
                AssertEqual("Brand New Inserted", snapUpserted[cidNew].Title, "New row inserted satisfying all NOT NULL columns");

                // Open WAL writer with uncheckpointed frame to verify BackupDatabaseToFile merges live WAL into standalone copy
                AssertEqual(0, t_sqlite3_open_v2(Utf8Z(dbPath), out db, 0x00000002, IntPtr.Zero), "Reopen DB to create live WAL frame");
                AssertEqual(0, t_sqlite3_exec(db, Utf8Z("PRAGMA wal_autocheckpoint=0;"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Disable autocheckpoint");
                InsertParentDbRow(db, cidNew, cid1);
                InsertSummaryDbRow(db, cidNew + "z", "WAL Uncheckpointed Row", "WAL Prev", 9, "[]",
                    "CASCADE_RUN_STATUS_IDLE", "proj-wal", "", "", 0, 0, raw3Orig);
                // Re-insert parent for cidNew -> cid1 already done above; delete helper row after backup or use cidNew itself
                AssertTrue(File.Exists(dbPath + "-wal") && new FileInfo(dbPath + "-wal").Length > 32, "Uncheckpointed WAL frame exists before online backup");

                bool bkOk = SummarySqliteBridge.BackupDatabaseToFile(dbPath, backupDbPath);
                AssertTrue(bkOk, "BackupDatabaseToFile succeeded with SQLITE_DONE (" + SummarySqliteBridge.LastError + ")");
                t_sqlite3_close(db);
                db = IntPtr.Zero;

                AssertTrue(File.Exists(backupDbPath) && !File.Exists(backupDbPath + "-wal"), "Standalone backup DB exists without needing -wal");
                Dictionary<string, SummaryDbRowInfo> snapBk = SummarySqliteBridge.ReadSummaryDbSnapshot(backupDbPath);
                AssertTrue(snapBk.ContainsKey(cidNew + "z"), "Standalone backup DB includes WAL-only committed row");

                // DeleteSessionsFromSummaryDb cascades across conversation_summaries, conversations_fts, conversation_parents
                List<string> delWarns;
                int delCnt = SummarySqliteBridge.DeleteSessionsFromSummaryDb(dbPath, new string[] { cidNew, cid2 }, out delWarns);
                AssertEqual(2, delCnt, "DeleteSessionsFromSummaryDb deleted 2 rows from conversation_summaries");
                AssertEqual(0, QuerySingleInt(dbPath, "SELECT COUNT(*) FROM conversation_summaries WHERE conversation_id = ?;", cid2), "cid2 removed from conversation_summaries");
                AssertEqual(0, QuerySingleInt(dbPath, "SELECT COUNT(*) FROM conversations_fts WHERE conversation_id = ?;", cid2), "cid2 removed from conversations_fts");
                AssertEqual(0, QuerySingleInt(dbPath, "SELECT COUNT(*) FROM conversation_parents WHERE conversation_id = ? OR parent_id = ?;", cid2), "cid2 removed from conversation_parents");
                AssertEqual(0, QuerySingleInt(dbPath, "SELECT COUNT(*) FROM conversation_parents WHERE conversation_id = ?;", cidNew), "cidNew removed from conversation_parents");
            }
            finally
            {
                if (db != IntPtr.Zero) try { t_sqlite3_close(db); } catch {}
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static RenameRulesConfig BuildSampleRenameRulesForTests()
        {
            RenameRulesConfig cfg = RenameRulesConfig.CreateDefault();
            cfg.ProjectAliases["INV3K_5K_VMH"] = "3KU";
            cfg.ProjectAliases["EvoKernel"] = "Evo";
            cfg.ProjectAliases["Antigravity_Companion"] = "Companion";

            MultiRepoRule inv = new MultiRepoRule();
            inv.ProjectName = "Inverter";
            inv.DefaultModel = "Inverter";
            inv.BranchRegexes.Add("在\\s*(?:[0-9A-Za-z_\\-/.]+\\s*的\\s*)?([A-Za-z][A-Za-z0-9_\\-\\u4e00-\\u9fa5]{1,36})\\s*分支");
            inv.BranchRegexes.Add("分支\\s*[:：]\\s*([A-Za-z][A-Za-z0-9_\\-\\u4e00-\\u9fa5]{1,36})");
            inv.ModelMatchers.Add(new ModelMatcherRule { Model = "3/6KU", Keywords = new List<string> { "3/6KU", "3/6k" } });
            inv.ModelMatchers.Add(new ModelMatcherRule { Model = "3KUbak", Keywords = new List<string> { "3KUbak" } });
            inv.ModelMatchers.Add(new ModelMatcherRule { Model = "UPS 1-3K", Keywords = new List<string> { "UPS 1-3K", "CT1K" } });
            inv.ModelMatchers.Add(new ModelMatcherRule { Model = "UPS 6-10K", Keywords = new List<string> { "UPS 6-10K", "6-10k-ups" } });
            inv.ModelMatchers.Add(new ModelMatcherRule { Model = "6KU", Keywords = new List<string> { "6KU", "vmhct6ku" } });
            inv.ModelMatchers.Add(new ModelMatcherRule { Model = "3KU", Keywords = new List<string> { "3KU" } });
            cfg.MultiRepoRules.Add(inv);
            return cfg;
        }

        private static void T34_AutoTitleSynthesizer_RulesAndSubagentExclusion()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_synth_rules_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(sb);
                RenameRulesConfig rules = BuildSampleRenameRulesForTests();
                string prefix, cleaned, title;

                // 1. Multi-repo matcher ordering for Inverter
                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s1", ProjectName = "Inverter", DisplayTitle = "untitled" },
                    "排查 3/6KU 逆变器过载保护", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【3/6KU】", prefix, "3/6KU matches before 3KU");

                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s2", ProjectName = "Inverter", DisplayTitle = "untitled" },
                    "排查 3KUbak 串口通讯超时", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【3KUbak】", prefix, "3KUbak matches before 3KU");

                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s3", ProjectName = "Inverter", DisplayTitle = "untitled" },
                    "UPS 1-3K CT1K 充电电流限幅", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【UPS 1-3K】", prefix, "UPS 1-3K matcher works");

                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s4", ProjectName = "Inverter", DisplayTitle = "untitled" },
                    "UPS 6-10K 6-10k-ups 输出校准", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【UPS 6-10K】", prefix, "UPS 6-10K matcher works");

                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s5", ProjectName = "Inverter", DisplayTitle = "untitled" },
                    "3KU 母线电容均压测试", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【3KU】", prefix, "3KU matcher works");

                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s5b", ProjectName = "Inverter", DisplayTitle = "untitled" },
                    "vmhct6ku 并机同步", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【6KU】", prefix, "6KU matcher works");

                // 2. Branch regex extraction & glued Chinese prose guard
                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s6", ProjectName = "Inverter", DisplayTitle = "untitled" },
                    "在 3KU 的 Dev_zy 分支修复母线过压", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【3KU·Dev_zy】", prefix, "Extracts Dev_zy branch");

                ProtoNode branchMeta = new ProtoNode(2, 2) { IsModified = true };
                branchMeta.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes("untitled"), IsModified = true });
                ProtoNode branchF9 = new ProtoNode(9, 2) { IsModified = true };
                branchF9.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes("file:///d%3A/Work/Inverter"), IsModified = true });
                ProtoNode branchF3 = new ProtoNode(3, 2) { IsModified = true };
                branchF3.Children.Add(new ProtoNode(4, 2) { RawBytes = Encoding.UTF8.GetBytes("Dev_zy"), IsModified = true });
                branchF9.Children.Add(branchF3);
                branchMeta.Children.Add(branchF9);
                ProtoNode branchEntry = new ProtoNode(1, 2) { IsModified = true };
                branchEntry.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes("aaaaaaaa-0000-4000-8000-000000000001"), IsModified = true });
                branchEntry.Children.Add(branchMeta);
                ConversationSummaryInfo protoBranchSess = new ConversationSummaryInfo
                {
                    ConversationId = "aaaaaaaa-0000-4000-8000-000000000001",
                    ProjectName = "Inverter",
                    DisplayTitle = "untitled",
                    FirstPromptSnippet = "3KU 母线故障",
                    RawProtoEntry = branchEntry
                };
                AutoTitleSynthesizer.SynthesizeTitle(protoBranchSess, rules, out prefix, out cleaned, out title);
                AssertEqual("【3KU·Dev_zy】", prefix, "5-param SynthesizeTitle falls back to RawProtoEntry Field 9->3->4 branch hint");

                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s7", ProjectName = "Inverter", DisplayTitle = "untitled" },
                    "基于 3KU 分支: Dev_YouNeng_ChuanYin 调试通讯", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【3KU·Dev_YouNeng_ChuanYin】", prefix, "Extracts Dev_YouNeng_ChuanYin branch");

                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s8", ProjectName = "Inverter", DisplayTitle = "untitled" },
                    "在 Customization_客户A_2026 分支适配 3KU 参数", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【3KU·Customization_客户A_2026】", prefix, "Extracts Customization_* branch");

                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s9", ProjectName = "Inverter", DisplayTitle = "untitled" },
                    "在当前分支排查 3KU 母线过压", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【3KU】", prefix, "Does NOT capture glued Chinese prose verb '排查' as branch");

                // 3. Single-repo aliases
                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s10", ProjectName = "EvoKernel", DisplayTitle = "untitled" },
                    "优化检索排序", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【Evo】", prefix, "EvoKernel maps to 【Evo】 alias");

                AutoTitleSynthesizer.SynthesizeTitle(
                    new ConversationSummaryInfo { ConversationId = "s10b", ProjectName = "Antigravity_Companion", DisplayTitle = "untitled" },
                    "新增批量重命名测试", null, rules, out prefix, out cleaned, out title);
                AssertEqual("【Companion】", prefix, "Antigravity_Companion maps to 【Companion】 alias");

                // 4. Idempotent re-synthesis stripping existing 【...】 when PreserveExistingTag=false
                RenameRulesConfig stripRules = BuildSampleRenameRulesForTests();
                stripRules.PreserveExistingTag = false;
                stripRules.KeepExistingBracketedPrefix = false;
                ConversationSummaryInfo reSynthSession = new ConversationSummaryInfo
                {
                    ConversationId = "s11",
                    ProjectName = "Inverter",
                    DisplayTitle = "【旧标签】修复母线过压保护逻辑"
                };
                string prefix1, cleaned1, title1;
                AutoTitleSynthesizer.SynthesizeTitle(reSynthSession, "3KU 修复母线过压保护逻辑", null, stripRules, out prefix1, out cleaned1, out title1);
                AssertEqual("【3KU】修复母线过压保护逻辑", title1, "Strips old 【...】 prefix before applying synthesized prefix");

                reSynthSession.DisplayTitle = title1;
                reSynthSession.CustomTitle = title1;
                string prefix2, cleaned2, title2;
                AutoTitleSynthesizer.SynthesizeTitle(reSynthSession, "3KU 修复母线过压保护逻辑", null, stripRules, out prefix2, out cleaned2, out title2);
                AssertEqual(title1, title2, "Re-synthesis is idempotent without stacking brackets");

                // 5. Prompt noise stripping
                string noisyPrompt =
                    "<ADDITIONAL_METADATA>\ncwd: C:\\Temp\n</ADDITIONAL_METADATA>" +
                    "<USER_REQUEST>/plan @[conversation:\"9999\"] \"C:\\Work\\Proj\\src\\drv_bus.c\" ```c\n" +
                    "> - **修复母线高压保护**\n```</USER_REQUEST>";
                string cleanedNoise = AutoTitleSynthesizer.CleanFirstPrompt(noisyPrompt);
                AssertEqual("drv_bus.c 修复母线高压保护", cleanedNoise, "CleanFirstPrompt strips metadata tags, slash cmds, mentions, path prefixes, fences, and markdown prefixes");

                // 6. OverlayLocalThreeLayerMetadata subagent exclusion & .pbtxt > .db > .pb priority
                string geminiDir = Path.Combine(sb, ".gemini");
                string annotDir = Path.Combine(geminiDir, "antigravity", "annotations");
                Directory.CreateDirectory(annotDir);

                string cidSelf = "aaaaaaaa-1111-4111-8111-111111111111";
                string cidResearch = "bbbbbbbb-2222-4222-8222-222222222222";
                string cidL1 = "cccccccc-3333-4333-8333-333333333333";
                string cidL2 = "dddddddd-4444-4444-8444-444444444444";
                string cidL3 = "eeeeeeee-5555-4555-8555-555555555555";

                LocalWorkspaceManager.WriteUtf8TextAtomicWithBackup(
                    Path.Combine(annotDir, cidL1 + ".pbtxt"),
                    "title:\"L1_Pbtxt_Title\" pinned:true",
                    false, null);

                Dictionary<string, SummaryDbRowInfo> dbSnap = new Dictionary<string, SummaryDbRowInfo>(StringComparer.OrdinalIgnoreCase);
                dbSnap[cidSelf] = new SummaryDbRowInfo { ConversationId = cidSelf, AgentName = "self", Title = "SubSelf" };
                dbSnap[cidResearch] = new SummaryDbRowInfo { ConversationId = cidResearch, AgentName = "research", Title = "SubResearch" };
                dbSnap[cidL1] = new SummaryDbRowInfo { ConversationId = cidL1, Title = "L2_Db_Title" };
                dbSnap[cidL2] = new SummaryDbRowInfo { ConversationId = cidL2, Title = "L2_Db_Title" };
                dbSnap[cidL3] = new SummaryDbRowInfo { ConversationId = cidL3, Title = "" };

                List<ConversationSummaryInfo> overlayList = new List<ConversationSummaryInfo>
                {
                    new ConversationSummaryInfo { ConversationId = cidSelf, DisplayTitle = "AutoSelf" },
                    new ConversationSummaryInfo { ConversationId = cidResearch, DisplayTitle = "AutoResearch" },
                    new ConversationSummaryInfo { ConversationId = cidL1, AutoSummaryTitle = "L3_F1", CustomTitle = "L3_F15", DisplayTitle = "L3_F15", HasCustomTitle = true },
                    new ConversationSummaryInfo { ConversationId = cidL2, AutoSummaryTitle = "L3_F1", CustomTitle = "L3_F15", DisplayTitle = "L3_F15", HasCustomTitle = true },
                    new ConversationSummaryInfo { ConversationId = cidL3, AutoSummaryTitle = "L3_F1", CustomTitle = "L3_F15", DisplayTitle = "L3_F15", HasCustomTitle = true }
                };

                SessionMetadataExtractor.OverlayLocalThreeLayerMetadata(overlayList, geminiDir, dbSnap);

                AssertTrue(overlayList[0].IsSubagent && overlayList[0].AgentName == "self", "agent_name='self' marked IsSubagent=true");
                AssertTrue(overlayList[1].IsSubagent && overlayList[1].AgentName == "research", "agent_name='research' marked IsSubagent=true");
                AssertEqual("L1_Pbtxt_Title", overlayList[2].DisplayTitle, "Layer 1 .pbtxt overrides Layer 2 .db and Layer 3 .pb");
                AssertEqual("L2_Db_Title", overlayList[3].DisplayTitle, "Layer 2 .db overrides Layer 3 .pb when .pbtxt absent");
                AssertEqual("L3_F15", overlayList[4].DisplayTitle, "Layer 3 .pb Field 15 preserved when L1 & L2 titles empty");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T35_BatchRenameAndRowLevelRollback_OrphanGc()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_batch_rb_" + Guid.NewGuid().ToString("N"));
            IntPtr db = IntPtr.Zero;
            try
            {
                string geminiDir = Path.Combine(sb, "live", ".gemini");
                string antigravityDir = Path.Combine(geminiDir, "antigravity");
                string annotDir = Path.Combine(antigravityDir, "annotations");
                string convDir = Path.Combine(antigravityDir, "conversations");
                string brainDir = Path.Combine(antigravityDir, "brain");
                Directory.CreateDirectory(annotDir);
                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(brainDir);

                string cidA = "11111111-1111-4111-8111-111111111111"; // has .pbtxt (pinned:true), only Field 1 in .pb
                string cidB = "22222222-2222-4222-8222-222222222222"; // no .pbtxt initially, has Field 15 in .pb
                string cidC = "33333333-3333-4333-8333-333333333333"; // running session
                string cidD = "44444444-4444-4444-8444-444444444444"; // post-rename added session

                string origPbtxtA = "title:\"Old A Pbtxt\" pinned:true archived:false last_user_view_time:{seconds:1750000000}";
                LocalWorkspaceManager.WriteUtf8TextAtomicWithBackup(Path.Combine(annotDir, cidA + ".pbtxt"), origPbtxtA, false, null);

                byte[] rawDbA = BuildRawSummaryMetaBytes("Old A Pb", null, "Dev_A", 10, null);
                byte[] rawDbB = BuildRawSummaryMetaBytes("Old B Auto", "Old B Custom", "Dev_B", 20, null);
                byte[] rawDbC = BuildRawSummaryMetaBytes("Old C Running", null, null, 5, null);

                string dbPath = Path.Combine(antigravityDir, "conversation_summaries.db");
                db = OpenAndCreateSummaryWalDb(dbPath);
                InsertSummaryDbRow(db, cidA, "Old A Db", "Old A Prev", 10, "[\"file:///d%3A/Work/P1\"]",
                    "CASCADE_RUN_STATUS_IDLE", "p1", "", "", 0, 0, rawDbA);
                InsertSummaryDbRow(db, cidB, "Old B Db", "Old B Prev", 20, "[\"file:///d%3A/Work/P1\"]",
                    "CASCADE_RUN_STATUS_IDLE", "p1", "", "", 0, 0, rawDbB);
                InsertSummaryDbRow(db, cidC, "Old C Running", "Old C Prev", 5, "[\"file:///d%3A/Work/P1\"]",
                    "CASCADE_RUN_STATUS_RUNNING", "p1", "", "", 0, 1, rawDbC);
                InsertFtsDbRow(db, cidA, -1, "Old A Db", "file:///d%3A/Work/P1", "P1", "", "");
                InsertFtsDbRow(db, cidB, -1, "Old B Db", "file:///d%3A/Work/P1", "P1", "", "");
                InsertFtsDbRow(db, cidC, -1, "Old C Running", "file:///d%3A/Work/P1", "P1", "", "");
                t_sqlite3_close(db);
                db = IntPtr.Zero;

                ProtoNode nodeA = BuildRealProtoEntry(cidA, "Old A Pb", null, "file:///d%3A/Work/P1", "P1", "p1", 10, 2, 1);
                ProtoNode nodeB = BuildRealProtoEntry(cidB, "Old B Auto", "Old B Custom", "file:///d%3A/Work/P1", "P1", "p1", 20, 4, 1);
                ProtoNode nodeC = BuildRealProtoEntry(cidC, "Old C Running", null, "file:///d%3A/Work/P1", "P1", "p1", 5, 1, 1);
                string pbPath = Path.Combine(antigravityDir, "agyhub_summaries_proto.pb");
                File.WriteAllBytes(pbPath, ProtoCodec.Encode(new List<ProtoNode> { nodeA, nodeB, nodeC }));

                // 1. Run RenameSessionsBatch with allowHotWriteSkipRunning = true
                Dictionary<string, string> batch = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                batch[cidA] = "【P1】Renamed A";
                batch[cidB] = "【P1】Renamed B";
                batch[cidC] = "【P1】Should Skip Running C";

                RenameBatchResult renameRes;
                bool batchOk = LocalWorkspaceManager.RenameSessionsBatch(geminiDir, batch, true, out renameRes);
                AssertTrue(batchOk, "RenameSessionsBatch succeeded");
                AssertEqual(2, renameRes.RenamedCount, "Renamed A & B while skipping running C");
                AssertTrue(renameRes.Layer1Updated && renameRes.Layer2Updated && renameRes.Layer3Updated, "All 3 layers updated");
                AssertEqual(1, renameRes.SkippedRunningIds.Count, "1 running session skipped");
                AssertTrue(string.Equals(renameRes.SkippedRunningIds[0], cidC, StringComparison.OrdinalIgnoreCase), "SkippedRunningIds contains cidC");

                // Check L1 (.pbtxt): A keeps pinned:true, B created without BOM, C not created
                string postPbtxtA = File.ReadAllText(Path.Combine(annotDir, cidA + ".pbtxt"), Encoding.UTF8);
                AssertEqual("【P1】Renamed A", PbtxtAnnotationCodec.ExtractTitle(postPbtxtA), "L1 A renamed");
                AssertTrue(postPbtxtA.Contains("pinned:true"), "L1 A preserved pinned:true");
                byte[] bytesB = File.ReadAllBytes(Path.Combine(annotDir, cidB + ".pbtxt"));
                AssertTrue(!(bytesB.Length >= 3 && bytesB[0] == 0xEF && bytesB[1] == 0xBB && bytesB[2] == 0xBF), "L1 B created without BOM");
                AssertEqual("【P1】Renamed B", PbtxtAnnotationCodec.ExtractTitle(Encoding.UTF8.GetString(bytesB)), "L1 B renamed");
                AssertTrue(!File.Exists(Path.Combine(annotDir, cidC + ".pbtxt")), "L1 C not touched");

                // Check L2 (.db) & L3 (.pb) & manifest exclusion of C
                Dictionary<string, SummaryDbRowInfo> dbPost = SummarySqliteBridge.ReadSummaryDbSnapshot(dbPath);
                AssertEqual("【P1】Renamed A", dbPost[cidA].Title, "L2 A renamed");
                AssertEqual("【P1】Renamed B", dbPost[cidB].Title, "L2 B renamed");
                AssertEqual("Old C Running", dbPost[cidC].Title, "L2 C untouched");

                List<ConversationSummaryInfo> pbPost = SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(pbPath));
                AssertEqual("【P1】Renamed A", pbPost.Find(x => x.ConversationId == cidA).DisplayTitle, "L3 A renamed");
                AssertEqual("【P1】Renamed B", pbPost.Find(x => x.ConversationId == cidB).DisplayTitle, "L3 B renamed");
                AssertEqual("Old C Running", pbPost.Find(x => x.ConversationId == cidC).DisplayTitle, "L3 C untouched");

                string manifestRaw = File.ReadAllText(Path.Combine(renameRes.BackupSnapshotDir, "rename_manifest.json"), Encoding.UTF8);
                AssertTrue(manifestRaw.Contains(cidA) && manifestRaw.Contains(cidB) && !manifestRaw.Contains(cidC),
                    "rename_manifest.json includes A & B and excludes skipped running C");

                string snapFolderName = Path.GetFileName(renameRes.BackupSnapshotDir);

                // Helper to append post-rename Session D into both .pb and .db of a sandbox
                Action<string> appendSessionD = delegate(string targetGeminiDir)
                {
                    string targetAgDir = Path.Combine(targetGeminiDir, "antigravity");
                    string targetPb = Path.Combine(targetAgDir, "agyhub_summaries_proto.pb");
                    string targetDb = Path.Combine(targetAgDir, "conversation_summaries.db");

                    List<ProtoNode> nodes = ProtoCodec.Decode(File.ReadAllBytes(targetPb));
                    nodes.Add(BuildRealProtoEntry(cidD, "Post-Rename Session D", null, "file:///d%3A/Work/P1", "P1", "p1", 3, 1, 1));
                    File.WriteAllBytes(targetPb, ProtoCodec.Encode(nodes));

                    byte[] rawD = BuildRawSummaryMetaBytes("Post-Rename Session D", null, null, 3, null);
                    List<string> w;
                    int cnt = SummarySqliteBridge.UpsertSessionsInSummaryDb(targetDb, new List<SummaryDbUpsertItem>
                    {
                        new SummaryDbUpsertItem
                        {
                            ConversationId = cidD,
                            Title = "Post-Rename Session D",
                            Preview = "D Preview",
                            StepCount = 3,
                            WorkspaceUrisJson = "[\"file:///d%3A/Work/P1\"]",
                            ProjectId = "p1",
                            RawSummaryBytes = rawD
                        }
                    }, out w);
                    AssertEqual(1, cnt, "Appended post-rename Session D to DB");
                };

                // 2a. Rollback Sub-Sandbox 1: Session D added AFTER rename; Session B still exists -> deletes B's created .pbtxt
                string geminiKeepB = Path.Combine(sb, "rb_keep_b", ".gemini");
                CopyDirectoryRecursive(geminiDir, geminiKeepB);
                appendSessionD(geminiKeepB);
                string snapKeepB = Path.Combine(RenameBackupManager.GetBackupRoot(geminiKeepB), snapFolderName);

                RenameRollbackResult rbRes1;
                bool rbOk1 = RenameBackupManager.RollbackSnapshot(geminiKeepB, snapKeepB, false, out rbRes1);
                AssertTrue(rbOk1 && rbRes1.Success, "RollbackSnapshot (keep B) succeeded: " + rbRes1.ErrorMessage);
                AssertEqual(1, rbRes1.RestoredPbtxtCount, "Restored A's .pbtxt");
                AssertEqual(1, rbRes1.DeletedPbtxtCount, "Deleted B's newly created .pbtxt because had_pbtxt == false");
                AssertEqual(0, rbRes1.SkippedOrphanPbtxtCount, "0 skipped orphan pbtxt when B still exists");

                string rbPbtxtA = File.ReadAllText(Path.Combine(geminiKeepB, "antigravity", "annotations", cidA + ".pbtxt"), Encoding.UTF8);
                AssertEqual(origPbtxtA, rbPbtxtA, "L1 A restored verbatim with pinned:true");
                AssertTrue(!File.Exists(Path.Combine(geminiKeepB, "antigravity", "annotations", cidB + ".pbtxt")), "L1 B's created .pbtxt deleted on rollback");

                Dictionary<string, SummaryDbRowInfo> rbDb1 = SummarySqliteBridge.ReadSummaryDbSnapshot(Path.Combine(geminiKeepB, "antigravity", "conversation_summaries.db"));
                AssertEqual("Old A Db", rbDb1[cidA].Title, "L2 A restored");
                AssertTrue(ByteArraysEqual(rawDbA, rbDb1[cidA].RawSummaryBytes), "L2 A raw_summary restored byte-for-byte");
                AssertEqual("Old A Db", QuerySingleText(Path.Combine(geminiKeepB, "antigravity", "conversation_summaries.db"),
                    "SELECT title FROM conversations_fts WHERE conversation_id = ? AND chunk_index = -1;", cidA), "L2 A FTS chunk_index=-1 title restored");
                AssertEqual("Old B Db", rbDb1[cidB].Title, "L2 B restored");
                AssertEqual("Post-Rename Session D", rbDb1[cidD].Title, "Post-rename Session D preserved intact in .db");

                List<ProtoNode> rbPbNodes1 = ProtoCodec.Decode(File.ReadAllBytes(Path.Combine(geminiKeepB, "antigravity", "agyhub_summaries_proto.pb")));
                AssertEqual(4, rbPbNodes1.Count, "Post-rename Session D preserved in .pb (4 total entries)");
                ProtoNode rbNodeA = rbPbNodes1[0];
                ProtoCodec.DecodeSummaryEntryTree(rbNodeA);
                ProtoNode rbMetaA = rbNodeA.GetChild(2);
                AssertEqual("Old A Pb", rbMetaA.GetChild(1).GetString(), "L3 A Field 1 restored");
                AssertTrue(rbMetaA.GetChild(15) == null, "L3 A Field 15 removed on rollback because had_pb_f15 == false");

                List<ConversationSummaryInfo> rbParsed1 = SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(Path.Combine(geminiKeepB, "antigravity", "agyhub_summaries_proto.pb")));
                AssertEqual("Old B Custom", rbParsed1.Find(x => x.ConversationId == cidB).DisplayTitle, "L3 B Field 15 custom title restored");
                AssertEqual("Post-Rename Session D", rbParsed1.Find(x => x.ConversationId == cidD).DisplayTitle, "Post-rename Session D preserved intact in .pb");

                // 2b. Rollback Sub-Sandbox 2: Session D added AND Session B deleted AFTER rename -> must NOT recreate orphan .pbtxt for B
                string geminiDelB = Path.Combine(sb, "rb_del_b", ".gemini");
                CopyDirectoryRecursive(geminiDir, geminiDelB);
                appendSessionD(geminiDelB);
                LocalWorkspaceManager.DeleteSessionsAndProjects(
                    geminiDelB,
                    SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(Path.Combine(geminiDelB, "antigravity", "agyhub_summaries_proto.pb"))),
                    null,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { cidB },
                    null);
                AssertTrue(!File.Exists(Path.Combine(geminiDelB, "antigravity", "annotations", cidB + ".pbtxt")), "Precondition: B deleted");

                string snapDelB = Path.Combine(RenameBackupManager.GetBackupRoot(geminiDelB), snapFolderName);
                RenameRollbackResult rbRes2;
                bool rbOk2 = RenameBackupManager.RollbackSnapshot(geminiDelB, snapDelB, false, out rbRes2);
                AssertTrue(rbOk2 && rbRes2.Success, "RollbackSnapshot (deleted B) succeeded: " + rbRes2.ErrorMessage);
                AssertEqual(1, rbRes2.SkippedOrphanPbtxtCount, "Skipped recreating .pbtxt for deleted Session B");
                AssertTrue(!File.Exists(Path.Combine(geminiDelB, "antigravity", "annotations", cidB + ".pbtxt")), "Did NOT recreate orphan .pbtxt for deleted Session B");
                AssertEqual("Old A Db", SummarySqliteBridge.ReadSummaryDbSnapshot(Path.Combine(geminiDelB, "antigravity", "conversation_summaries.db"))[cidA].Title, "Session A restored in DB");
                AssertTrue(SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(Path.Combine(geminiDelB, "antigravity", "agyhub_summaries_proto.pb"))).Exists(x => x.ConversationId == cidD),
                    "Post-rename Session D still intact in .pb after rollback");

                // 3. ScanOrphanedData union truth-set (.pb U .db) protects DB-only session, flags true UUID orphan .pbtxt, ignores non-UUID files
                string geminiOrphan = Path.Combine(sb, "orphan_gc", ".gemini");
                string orphAg = Path.Combine(geminiOrphan, "antigravity");
                string orphAnnot = Path.Combine(orphAg, "annotations");
                string orphConv = Path.Combine(orphAg, "conversations");
                string orphBrain = Path.Combine(orphAg, "brain");
                Directory.CreateDirectory(orphAnnot);
                Directory.CreateDirectory(orphConv);
                Directory.CreateDirectory(orphBrain);

                string cidActivePb = "55555555-5555-4555-8555-555555555555";
                string cidDbOnly = "66666666-6666-4666-8666-666666666666";
                string cidTrueOrphan = "77777777-7777-4777-8777-777777777777";

                File.WriteAllBytes(Path.Combine(orphAg, "agyhub_summaries_proto.pb"),
                    ProtoCodec.Encode(new List<ProtoNode> { BuildRealProtoEntry(cidActivePb, "Active PB", null, "file:///d%3A/P", "P", "p", 1, 1, 1) }));

                string orphDbPath = Path.Combine(orphAg, "conversation_summaries.db");
                db = OpenAndCreateSummaryWalDb(orphDbPath);
                InsertSummaryDbRow(db, cidDbOnly, "DB Only Session", "Prev", 4, "[]", "CASCADE_RUN_STATUS_IDLE", "p", "", "", 0, 0, rawDbA);
                t_sqlite3_close(db);
                db = IntPtr.Zero;

                string dbOnlyPbtxt = Path.Combine(orphAnnot, cidDbOnly + ".pbtxt");
                string dbOnlyConvDb = Path.Combine(orphConv, cidDbOnly + ".db");
                string dbOnlyBrain = Path.Combine(orphBrain, cidDbOnly);
                Directory.CreateDirectory(dbOnlyBrain);
                File.WriteAllText(dbOnlyPbtxt, "title:\"DB Only\"", Encoding.UTF8);
                File.WriteAllText(dbOnlyConvDb, "db-bytes", Encoding.UTF8);
                File.WriteAllText(Path.Combine(dbOnlyBrain, "note.md"), "keep", Encoding.UTF8);

                string orphanPbtxt = Path.Combine(orphAnnot, cidTrueOrphan + ".pbtxt");
                string nonUuidPbtxt = Path.Combine(orphAnnot, "not-a-uuid-notes.pbtxt");
                File.WriteAllText(orphanPbtxt, "title:\"Orphan\"", Encoding.UTF8);
                File.WriteAllText(nonUuidPbtxt, "title:\"Non-UUID\"", Encoding.UTF8);

                OrphanScanResult scan = LocalWorkspaceManager.ScanOrphanedData(geminiOrphan);
                AssertEqual(1, scan.OrphanAnnotationFiles.Count, "Flags exact 1 true orphan UUID .pbtxt");
                AssertTrue(string.Equals(Path.GetFullPath(orphanPbtxt), Path.GetFullPath(scan.OrphanAnnotationFiles[0]), StringComparison.OrdinalIgnoreCase),
                    "Flagged file is cidTrueOrphan.pbtxt");
                AssertEqual(0, scan.OrphanDbFiles.Count, "DB-only session .db protected by union truth-set");
                AssertEqual(0, scan.OrphanBrainDirs.Count, "DB-only session brain dir protected by union truth-set");
                AssertTrue(!scan.OrphanSessionIds.Contains(cidDbOnly), "cidDbOnly not marked orphan");
                AssertTrue(scan.OrphanSessionIds.Contains(cidTrueOrphan), "cidTrueOrphan marked orphan");

                DeletionResult gcRes = LocalWorkspaceManager.CleanOrphanedData(scan);
                AssertEqual(1, gcRes.DeletedAnnotationsCount, "Deleted 1 orphan .pbtxt");
                AssertTrue(!File.Exists(orphanPbtxt), "True orphan .pbtxt deleted");
                AssertTrue(File.Exists(dbOnlyPbtxt) && File.Exists(dbOnlyConvDb) && Directory.Exists(dbOnlyBrain), "DB-only session assets preserved");
                AssertTrue(File.Exists(nonUuidPbtxt), "Non-UUID .pbtxt file ignored and preserved");
            }
            finally
            {
                if (db != IntPtr.Zero) try { t_sqlite3_close(db); } catch {}
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T36_AiPolishExchangeAndAgyAnnotationRoundTrip()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_polish_agy_" + Guid.NewGuid().ToString("N"));
            string zipAnnot = Path.Combine(Path.GetTempPath(), "agy_annot_rt_" + Guid.NewGuid().ToString("N") + ".agy");
            string zipLegacy = Path.Combine(Path.GetTempPath(), "agy_legacy_rt_" + Guid.NewGuid().ToString("N") + ".agy");
            try
            {
                // 1. AiPolishPromptExchange.ParseAiClipboardJson
                string cidUnique = "12345678-aaaa-4bbb-8ccc-111111111111";
                string cidAmb1 = "abcdef99-1111-4111-8111-111111111111";
                string cidAmb2 = "abcdef99-2222-4222-8222-222222222222";
                List<SessionRenameCandidate> rows = new List<SessionRenameCandidate>
                {
                    new SessionRenameCandidate { ConversationId = cidUnique, ProjectName = "Inverter", OldTitle = "Old1", SuggestedPrefix = "【3KU】" },
                    new SessionRenameCandidate { ConversationId = cidAmb1, ProjectName = "EvoKernel", OldTitle = "Old2", SuggestedPrefix = "【Evo】" },
                    new SessionRenameCandidate { ConversationId = cidAmb2, ProjectName = "EvoKernel", OldTitle = "Old3", SuggestedPrefix = "【Evo】" }
                };

                string prompt = AiPolishPromptExchange.BuildPolishPrompt(rows);
                AssertTrue(prompt.Contains(cidUnique) && prompt.Contains("```json"), "BuildPolishPrompt generates fenced JSON prompt");

                string fencedClipboard =
                    "Here is the result:\n" +
                    "```json\n" +
                    "{\"items\": [{\"id\": \"12345678\", \"new_title\": \"【3KU】短ID匹配\\n单行归整\"}]}\n" +
                    "```\n";
                Dictionary<string, string> parsed;
                string err;
                bool okValid = AiPolishPromptExchange.ParseAiClipboardJson(fencedClipboard, rows, out parsed, out err);
                AssertTrue(okValid, "ParseAiClipboardJson strips ```json fence and resolves unambiguous 8-char ID: " + err);
                AssertEqual("【3KU】短ID匹配 单行归整", parsed[cidUnique], "Resolves full UUID and normalizes newlines");

                string ambiguousClipboard = "[{\"id\": \"abcdef99\", \"new_title\": \"【Evo】歧义ID\"}]";
                bool okAmb = AiPolishPromptExchange.ParseAiClipboardJson(ambiguousClipboard, rows, out parsed, out err);
                AssertTrue(!okAmb && !string.IsNullOrEmpty(err), "Rejects ambiguous 8-char short ID cleanly");

                string emptyTitleClipboard = "[{\"id\": \"12345678\", \"new_title\": \"  \\r\\n  \"}]";
                bool okEmpty = AiPolishPromptExchange.ParseAiClipboardJson(emptyTitleClipboard, rows, out parsed, out err);
                AssertTrue(!okEmpty && !string.IsNullOrEmpty(err), "Rejects empty/whitespace new_title cleanly");

                string shortIdClipboard = "[{\"id\": \"1234567\", \"new_title\": \"Too Short\"}]";
                bool okShort = AiPolishPromptExchange.ParseAiClipboardJson(shortIdClipboard, rows, out parsed, out err);
                AssertTrue(!okShort && !string.IsNullOrEmpty(err), "Rejects <8-char short ID cleanly");

                // 2. ExportSelectiveBackup -> StreamingZipIndexReader.ReadIndex with annotations/*.pbtxt & legacy .agy without annotations/
                string homeDir = Path.Combine(sb, "UserHome");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                string annotDir = Path.Combine(agDir, "annotations");
                string convDir = Path.Combine(agDir, "conversations");
                string projDir = Path.Combine(geminiDir, "config", "projects");
                Directory.CreateDirectory(annotDir);
                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(projDir);

                string cidWithAnnot = "88888888-1111-4111-8111-111111111111";
                string cidLegacy = "99999999-2222-4222-8222-222222222222";
                string projId = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";

                File.WriteAllText(Path.Combine(projDir, projId + ".json"),
                    WorkspaceService.GenerateStandardProjectJson(projId, "Inverter", "D:\\Work\\Inverter"), Encoding.UTF8);

                LocalWorkspaceManager.WriteUtf8TextAtomicWithBackup(
                    Path.Combine(annotDir, cidWithAnnot + ".pbtxt"),
                    "title:\"\\343\\200\\2203KU\\343\\200\\221 \\346\\240\\207\\346\\263\\250\\346\\240\\207\\351\\242\\230\" pinned:true",
                    false, null);

                File.WriteAllText(Path.Combine(convDir, cidWithAnnot + ".db"), "dummy-db-1", Encoding.UTF8);
                File.WriteAllText(Path.Combine(convDir, cidLegacy + ".db"), "dummy-db-2", Encoding.UTF8);

                ProtoNode pbNodeAnnot = BuildRealProtoEntry(cidWithAnnot, "Auto In PB", null, "file:///d%3A/Work/Inverter", "Inverter", projId, 6, 2, 1);
                ProtoNode pbNodeLegacy = BuildRealProtoEntry(cidLegacy, "Auto Legacy", "Legacy PB F15 Title", "file:///d%3A/Work/Inverter", "Inverter", projId, 8, 3, 1);
                byte[] pbBytes = ProtoCodec.Encode(new List<ProtoNode> { pbNodeAnnot, pbNodeLegacy });
                File.WriteAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb"), pbBytes);

                List<ConversationSummaryInfo> allSum = SessionMetadataExtractor.ExtractSummaries(pbBytes, null);
                ConversationSummaryInfo sAnnot = allSum.Find(x => x.ConversationId == cidWithAnnot);
                ConversationSummaryInfo sLegacy = allSum.Find(x => x.ConversationId == cidLegacy);

                int packedAnnot = LocalWorkspaceManager.ExportSelectiveBackup(
                    geminiDir, homeDir, zipAnnot, new List<ConversationSummaryInfo> { sAnnot }, null, false, null);
                AssertTrue(packedAnnot >= 4, "ExportSelectiveBackup packed .pb, .db, project, and .pbtxt");

                using (ZipArchive za = ZipFile.OpenRead(zipAnnot))
                {
                    AssertTrue(za.GetEntry("data/antigravity/annotations/" + cidWithAnnot + ".pbtxt") != null,
                        "Archive contains data/antigravity/annotations/<cid>.pbtxt");
                }

                BackupArchiveIndex idxAnnot = StreamingZipIndexReader.ReadIndex(zipAnnot);
                AssertEqual(1, idxAnnot.Conversations.Count, "ReadIndex reads 1 conversation from annotation archive");
                AssertEqual("【3KU】 标注标题", idxAnnot.Conversations[0].DisplayTitle, "ReadIndex overlays .pbtxt custom title over .pb auto title");
                AssertTrue(idxAnnot.Conversations[0].HasCustomTitle, "ReadIndex marks HasCustomTitle = true from .pbtxt");
                AssertEqual(0, idxAnnot.Capabilities.Count, "annotations/*.pbtxt NOT misclassified as Capabilities");
                AssertTrue(!idxAnnot.HasRules && !idxAnnot.HasSkills && !idxAnnot.HasMcp, "No false capability flags from annotations/*.pbtxt");

                int packedLegacy = LocalWorkspaceManager.ExportSelectiveBackup(
                    geminiDir, homeDir, zipLegacy, new List<ConversationSummaryInfo> { sLegacy }, null, false, null);
                AssertTrue(packedLegacy >= 3, "ExportSelectiveBackup packed legacy session without annotations/");

                BackupArchiveIndex idxLegacy = StreamingZipIndexReader.ReadIndex(zipLegacy);
                AssertEqual(1, idxLegacy.Conversations.Count, "Legacy .agy lacking annotations/ still reads cleanly");
                AssertEqual("Legacy PB F15 Title", idxLegacy.Conversations[0].DisplayTitle, "Legacy .agy falls back to .pb Field 15 custom title");
                AssertEqual(0, idxLegacy.Capabilities.Count, "Legacy .agy has 0 capabilities");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
                if (File.Exists(zipAnnot)) File.Delete(zipAnnot);
                if (File.Exists(zipLegacy)) File.Delete(zipLegacy);
            }
        }

        private static ProtoNode BuildField17BinarySubmsgEntry(
            string cid,
            string autoTitle,
            byte[] rawField17Child1Bytes,
            string projId,
            int steps)
        {
            ProtoNode entry = new ProtoNode(1, 2);
            entry.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(cid), IsModified = true });

            ProtoNode meta = new ProtoNode(2, 2) { IsModified = true };
            if (!string.IsNullOrEmpty(autoTitle))
                meta.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(autoTitle), IsModified = true });
            meta.Children.Add(new ProtoNode(2, 0) { VarintValue = (ulong)steps, IsModified = true });

            ProtoNode ts = new ProtoNode(3, 2) { IsModified = true };
            ts.Children.Add(new ProtoNode(1, 0) { VarintValue = 1756000000UL, IsModified = true });
            meta.Children.Add(ts);

            ProtoNode cascade = new ProtoNode(17, 2) { IsModified = true };
            if (rawField17Child1Bytes != null)
                cascade.Children.Add(new ProtoNode(1, 2) { RawBytes = rawField17Child1Bytes, IsModified = true });
            if (!string.IsNullOrEmpty(projId))
                cascade.Children.Add(new ProtoNode(18, 2) { RawBytes = Encoding.UTF8.GetBytes(projId), IsModified = true });
            meta.Children.Add(cascade);

            entry.Children.Add(meta);
            entry.IsModified = true;
            return entry;
        }

        private static byte[] BuildRawSummaryWithField9To4Branches(string autoTitle, string repoUri, string branchPrimary, string branchSecondary, int steps)
        {
            List<ProtoNode> nodes = new List<ProtoNode>();
            if (!string.IsNullOrEmpty(autoTitle))
                nodes.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(autoTitle), IsModified = true });
            nodes.Add(new ProtoNode(2, 0) { VarintValue = (ulong)steps, IsModified = true });

            if (!string.IsNullOrEmpty(branchPrimary))
            {
                ProtoNode ws1 = new ProtoNode(9, 2) { IsModified = true };
                if (!string.IsNullOrEmpty(repoUri))
                {
                    ws1.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(repoUri), IsModified = true });
                    ProtoNode repo1 = new ProtoNode(3, 2) { IsModified = true };
                    repo1.Children.Add(new ProtoNode(1, 2) { RawBytes = Encoding.UTF8.GetBytes(repoUri), IsModified = true });
                    ws1.Children.Add(repo1);
                }
                ws1.Children.Add(new ProtoNode(4, 2) { RawBytes = Encoding.UTF8.GetBytes(branchPrimary), IsModified = true });
                nodes.Add(ws1);
            }

            if (!string.IsNullOrEmpty(branchSecondary))
            {
                ProtoNode ws2 = new ProtoNode(9, 2) { IsModified = true };
                ws2.Children.Add(new ProtoNode(4, 2) { RawBytes = Encoding.UTF8.GetBytes(branchSecondary), IsModified = true });
                nodes.Add(ws2);
            }

            return ProtoCodec.Encode(nodes);
        }

        private sealed class RawHttpTestResponse
        {
            public int StatusCode;
            public string StatusLine = string.Empty;
            public Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public string SetCookie = string.Empty;
            public string BodyText = string.Empty;
            public int BodyByteCount;
        }

        private static RawHttpTestResponse SendRawTcpHttp(
            int port,
            string hostHeader,
            string method,
            string pathAndQuery,
            Dictionary<string, string> extraHeaders,
            string jsonBody)
        {
            byte[] bodyBytes = string.IsNullOrEmpty(jsonBody) ? new byte[0] : Encoding.UTF8.GetBytes(jsonBody);
            StringBuilder req = new StringBuilder();
            req.AppendFormat("{0} {1} HTTP/1.1\r\n", method, pathAndQuery);
            req.AppendFormat("Host: {0}\r\n", string.IsNullOrEmpty(hostHeader) ? ("127.0.0.1:" + port) : hostHeader);
            if (extraHeaders != null)
            {
                foreach (KeyValuePair<string, string> kvp in extraHeaders)
                    req.AppendFormat("{0}: {1}\r\n", kvp.Key, kvp.Value);
            }
            if (bodyBytes.Length > 0)
            {
                req.Append("Content-Type: application/json; charset=utf-8\r\n");
                req.AppendFormat("Content-Length: {0}\r\n", bodyBytes.Length);
            }
            req.Append("Connection: close\r\n\r\n");

            byte[] hdrBytes = Encoding.ASCII.GetBytes(req.ToString());
            using (TcpClient client = new TcpClient())
            {
                client.ReceiveTimeout = 10000;
                client.SendTimeout = 10000;
                client.Connect(IPAddress.Loopback, port);
                using (NetworkStream ns = client.GetStream())
                {
                    ns.Write(hdrBytes, 0, hdrBytes.Length);
                    if (bodyBytes.Length > 0) ns.Write(bodyBytes, 0, bodyBytes.Length);
                    ns.Flush();

                    using (MemoryStream ms = new MemoryStream())
                    {
                        byte[] buf = new byte[8192];
                        int r;
                        while ((r = ns.Read(buf, 0, buf.Length)) > 0)
                            ms.Write(buf, 0, r);

                        byte[] rawAll = ms.ToArray();
                        int sepIdx = -1;
                        for (int i = 0; i <= rawAll.Length - 4; i++)
                        {
                            if (rawAll[i] == 13 && rawAll[i + 1] == 10 && rawAll[i + 2] == 13 && rawAll[i + 3] == 10)
                            {
                                sepIdx = i;
                                break;
                            }
                        }
                        if (sepIdx < 0) throw new Exception("Invalid raw HTTP response (no CRLFCRLF)");

                        string headerText = Encoding.ASCII.GetString(rawAll, 0, sepIdx);
                        string[] lines = headerText.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                        RawHttpTestResponse resp = new RawHttpTestResponse();
                        resp.StatusLine = lines.Length > 0 ? lines[0] : string.Empty;
                        string[] statusParts = lines[0].Split(' ');
                        resp.StatusCode = statusParts.Length >= 2 ? int.Parse(statusParts[1]) : 0;

                        for (int i = 1; i < lines.Length; i++)
                        {
                            int colon = lines[i].IndexOf(':');
                            if (colon <= 0) continue;
                            string k = lines[i].Substring(0, colon).Trim();
                            string v = lines[i].Substring(colon + 1).Trim();
                            resp.Headers[k] = v;
                            if (string.Equals(k, "Set-Cookie", StringComparison.OrdinalIgnoreCase))
                                resp.SetCookie = v;
                        }

                        int bodyStart = sepIdx + 4;
                        resp.BodyByteCount = rawAll.Length - bodyStart;
                        resp.BodyText = resp.BodyByteCount > 0 ? Encoding.UTF8.GetString(rawAll, bodyStart, resp.BodyByteCount) : string.Empty;
                        return resp;
                    }
                }
            }
        }

        private static void T37_ProjectAttribution_Field17Submessage_SqliteBackfill_MultiRootUris()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t37_" + Guid.NewGuid().ToString("N"));
            IntPtr db = IntPtr.Zero;
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                string projDir = Path.Combine(geminiDir, "config", "projects");
                Directory.CreateDirectory(agDir);
                Directory.CreateDirectory(projDir);

                string projInvId = "11111111-aaaa-4aaa-8aaa-111111111111";
                string multiProjJson = "{\r\n" +
                    "  \"projectId\": \"" + projInvId + "\",\r\n" +
                    "  \"name\": \"Inverter\",\r\n" +
                    "  \"folderUri\": \"file:///d%3A/Work/RootPrimary\",\r\n" +
                    "  \"folderUris\": [\"file:///d%3A/Work/RootPrimary\", \"file:///d%3A/Work/RootSecondary\"]\r\n" +
                    "}";
                File.WriteAllText(Path.Combine(projDir, projInvId + ".json"), multiProjJson, Encoding.UTF8);

                string cidBinF17 = "aaaa1111-1111-4111-8111-111111111111";
                string cidMultiRoot = "bbbb2222-2222-4222-8222-222222222222";
                string cidSqlBackfill = "cccc3333-3333-4333-8333-333333333333";

                byte[] garbageSubmsgBytes = new byte[] { 0x0A, 0x04, 0xFF, 0xFE, 0x01, 0x80 };
                ProtoNode eBin = BuildField17BinarySubmsgEntry(cidBinF17, "Binary F17 Session", garbageSubmsgBytes, projInvId, 5);
                ProtoNode eMulti = BuildRealProtoEntry(cidMultiRoot, "Multi Root Session", null, "file:///d%3A/Work/RootSecondary", null, null, 7, 2, 1);
                ProtoNode eBackfill = BuildRealProtoEntry(cidSqlBackfill, "SQLite Backfill Session", null, null, null, null, 4, 1, 1);

                byte[] pbBytes = ProtoCodec.Encode(new List<ProtoNode> { eBin, eMulti, eBackfill });
                File.WriteAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb"), pbBytes);

                string dbPath = Path.Combine(agDir, "conversation_summaries.db");
                byte[] rawEmpty = BuildRawSummaryMetaBytes("SQLite Backfill Session", null, null, 4, null);
                db = OpenAndCreateSummaryWalDb(dbPath);
                InsertSummaryDbRow(db, cidSqlBackfill, "SQLite Backfill Session", "Prev", 4, "[]",
                    "CASCADE_RUN_STATUS_IDLE", projInvId, "", "", 0, 0, rawEmpty);
                t_sqlite3_close(db);
                db = IntPtr.Zero;

                Dictionary<string, ProjectMetadata> localProjs;
                List<string> warnings;
                List<ConversationSummaryInfo> sums = WorkspaceService.LoadLocalThreeLayerSummaries(geminiDir, out localProjs, out warnings);

                AssertEqual(3, sums.Count, "All 3 entries parsed without IsValidCascadeContainer rejecting binary F17->1");
                AssertTrue(localProjs[projInvId].FolderUris.Count == 2, "ProjectMetadata.FolderUris retains both multi-root URIs");

                ConversationSummaryInfo sBin = sums.Find(x => x.ConversationId == cidBinF17);
                AssertTrue(sBin != null, "Binary F17 session extracted");
                AssertEqual(projInvId, sBin.ProjectId, "Preserves F18 ProjectId");
                AssertEqual("Inverter", sBin.ProjectName, "Resolves canonical ProjectName from projects JSON without garbled F17->1 bytes");

                ConversationSummaryInfo sMulti = sums.Find(x => x.ConversationId == cidMultiRoot);
                AssertTrue(sMulti != null, "Multi-root session extracted");
                AssertEqual(projInvId, sMulti.ProjectId, "Matches secondary root URI in folderUris to ProjectId");
                AssertEqual("Inverter", sMulti.ProjectName, "Matches secondary root URI to Inverter");

                ConversationSummaryInfo sSql = sums.Find(x => x.ConversationId == cidSqlBackfill);
                AssertTrue(sSql != null, "SQLite backfill session extracted");
                AssertEqual(projInvId, sSql.ProjectId, "Backfills ProjectId from SQLite row");
                AssertEqual("Inverter", sSql.ProjectName, "Backfills ProjectName after SQLite ProjectId overlay");
            }
            finally
            {
                if (db != IntPtr.Zero) try { t_sqlite3_close(db); } catch { }
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T38_RenameRules_Field9To4Branch_TrailingBranchStrip_EmbeddedBracketFold_LowValue()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t38_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(sb);
                RenameRulesConfig rules = BuildSampleRenameRulesForTests();

                // 1. Field 9->4 multi-branch extraction + trailing '分支' stripping + model keyword matching
                byte[] rawSummary = BuildRawSummaryWithField9To4Branches(
                    "Auto", "file:///d%3A/Work/3KU", "Dev_TianKeNaNeng2分支", "Dev_Other6KU分支", 6);
                List<KeyValuePair<string, string>> repoBranches = SummarySqliteBridge.ExtractGitBranchCandidatesFromRawSummary(rawSummary);
                AssertEqual(2, repoBranches.Count, "Extracts both Field 9->4 branches cleanly");
                AssertEqual("Dev_TianKeNaNeng2分支", repoBranches[0].Value, "Primary Field 9->4 branch preserved before strip");

                ConversationSummaryInfo s1 = new ConversationSummaryInfo
                {
                    ConversationId = "10000001-1111-4111-8111-111111111111",
                    ProjectName = "Inverter",
                    DisplayTitle = "PayGo 【Dev_TianKeNaNeng2】",
                    FirstPromptSnippet = "请排查 3KU 机器的 PayGo 问题",
                    GitBranchHint = repoBranches[0].Value,
                    RepoBranchHints = repoBranches,
                    StepCount = 6
                };

                string pfx1, cln1, title1;
                AutoTitleSynthesizer.SynthesizeTitle(s1, rules, out pfx1, out cln1, out title1);
                AssertEqual("【3KU·Dev_TianKeNaNeng2】", pfx1, "Strips trailing '分支' and selects matching branch");
                AssertEqual("【3KU·Dev_TianKeNaNeng2】PayGo", title1, "Folds trailing embedded 【Dev_TianKeNaNeng2】 tag without double brackets");

                // 2. Prose emphasis brackets preserved inside title body
                ConversationSummaryInfo s2 = new ConversationSummaryInfo
                {
                    ConversationId = "10000002-1111-4111-8111-111111111111",
                    ProjectName = "INV3K_5K_VMH",
                    DisplayTitle = "排查【母线过压】问题",
                    FirstPromptSnippet = "分析母线过压",
                    StepCount = 4
                };
                string pfx2, cln2, title2;
                AutoTitleSynthesizer.SynthesizeTitle(s2, rules, out pfx2, out cln2, out title2);
                AssertEqual("【3KU】排查【母线过压】问题", title2, "Preserves prose emphasis brackets while applying alias prefix");

                // 3. Low-value / 0-step detection
                ConversationSummaryInfo sLow = new ConversationSummaryInfo
                {
                    ConversationId = "10000003-1111-4111-8111-111111111111",
                    ProjectName = "INV3K_5K_VMH",
                    DisplayTitle = "你好",
                    FirstPromptSnippet = "hi",
                    StepCount = 0
                };
                SessionRenameCandidate cLow = new SessionRenameCandidate
                {
                    ConversationId = sLow.ConversationId,
                    ProjectName = sLow.ProjectName,
                    OldTitle = sLow.DisplayTitle,
                    FirstPromptSnippet = sLow.FirstPromptSnippet,
                    StepCount = sLow.StepCount,
                    Summary = sLow
                };
                AutoTitleSynthesizer.SynthesizeCandidate(cLow, rules);
                AssertTrue(cLow.IsLowValue, "0-step generic '你好' / 'hi' marked IsLowValue");
                AssertTrue(!s1.Equals(null) && !AutoTitleSynthesizer.IsLowValueSession(s1, cln1), "Substantive 6-step session not marked low-value");

                // 4. Rule serialization & sandbox preview
                string json = AutoTitleSynthesizer.SerializeRulesToJson(rules);
                RenameRulesConfig rtCfg;
                string parseErr;
                AssertTrue(AutoTitleSynthesizer.TryParseRulesFromJson(json, out rtCfg, out parseErr), "SerializeRulesToJson round-trips cleanly: " + parseErr);
                List<RuleSandboxItem> sandbox = AutoTitleSynthesizer.PreviewRulesOnSessions(rtCfg, new List<ConversationSummaryInfo> { s1, s2, sLow }, 10);
                AssertEqual(3, sandbox.Count, "PreviewRulesOnSessions returns 3 sandbox rows");
                AssertEqual("【3KU·Dev_TianKeNaNeng2】PayGo", sandbox[0].SuggestedTitle, "Sandbox reflects synthesized title");
                AssertTrue(sandbox[2].IsLowValue, "Sandbox surfaces low-value flag");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T39_CliBrainFallback_FirstPrompt_MarkdownExport_ShortestDistinguishingPath()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t39_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string cidCli = "cli11111-2222-4333-8444-555555555555";
                string cliLogs = Path.Combine(geminiDir, "antigravity-cli", "brain", cidCli, ".system_generated", "logs");
                Directory.CreateDirectory(cliLogs);

                string jsonl =
                    "{\"type\":\"USER_INPUT\",\"content\":\"Check antigravity-cli UART handshake timeout\"}\r\n" +
                    "{\"type\":\"PLANNER_RESPONSE\",\"content\":\"Analyzed UART timer registers in CLI workspace.\"}\r\n";
                File.WriteAllText(Path.Combine(cliLogs, "transcript_full.jsonl"), jsonl, Encoding.UTF8);

                ConversationSummaryInfo cliSum = new ConversationSummaryInfo
                {
                    ConversationId = cidCli,
                    ProjectId = "default-cli-project",
                    ProjectName = "default-cli-project",
                    DisplayTitle = "CLI UART Timeout",
                    StepCount = 3,
                    UpdatedTime = DateTime.Now
                };

                List<ConversationSummaryInfo> list = new List<ConversationSummaryInfo> { cliSum };
                SessionMetadataExtractor.OverlayLocalThreeLayerMetadata(list, geminiDir, null, null);
                AssertTrue(cliSum.IsCliSession, "default-cli-project marks IsCliSession = true");
                AssertTrue(!string.IsNullOrEmpty(cliSum.FirstPromptSnippet) && cliSum.FirstPromptSnippet.Contains("UART handshake"),
                    "Extracts first prompt from antigravity-cli/brain fallback");

                bool trunc;
                string md = TranscriptMarkdownExporter.PreviewLocalSessionMarkdown(cliSum, geminiDir, 50000, out trunc);
                AssertTrue(md.Contains("Check antigravity-cli UART handshake timeout"), "Markdown preview includes CLI USER_INPUT");
                AssertTrue(md.Contains("Analyzed UART timer registers"), "Markdown preview includes CLI PLANNER_RESPONSE");

                // Shortest Distinguishing Parent Paths for duplicate basenames
                List<string> dupPaths = new List<string>
                {
                    "C:\\Users\\Admin\\Desktop\\vmh_ct3k_center_ctr\\INV3K_5K_VMH",
                    "C:\\Users\\Admin\\Desktop\\Inverter\\vmh_3ku_bak\\vmh_ct3k_center_ctr\\INV3K_5K_VMH"
                };
                List<string> labels = WorkspaceService.ComputeShortestDistinguishingLabels(dupPaths, "INV3K_5K_VMH");
                AssertEqual(2, labels.Count, "Returns distinguishing label per duplicate workspace");
                AssertEqual("vmh_ct3k_center_ctr", labels[0], "Shortest distinguishing label strips common prefix & trailing basename (1)");
                AssertEqual("Inverter/vmh_3ku_bak/vmh_ct3k_center_ctr", labels[1], "Shortest distinguishing label retains middle folder (2)");

                // BuildProjectDisplayGroups separates duplicate formal projects, multi-root flag, and AUX:CLI group
                string p1 = "p1111111-1111-4111-8111-111111111111";
                string p2 = "p2222222-2222-4222-8222-222222222222";
                Dictionary<string, ProjectMetadata> projs = new Dictionary<string, ProjectMetadata>(StringComparer.OrdinalIgnoreCase);
                projs[p1] = new ProjectMetadata
                {
                    Id = p1,
                    Name = "INV3K_5K_VMH",
                    FolderUri = LocalWorkspaceManager.ToFileUri(dupPaths[0]),
                    FolderUris = new List<string> { LocalWorkspaceManager.ToFileUri(dupPaths[0]), "file:///d%3A/Work/ExtraRoot" }
                };
                projs[p2] = new ProjectMetadata
                {
                    Id = p2,
                    Name = "INV3K_5K_VMH",
                    FolderUri = LocalWorkspaceManager.ToFileUri(dupPaths[1]),
                    FolderUris = new List<string> { LocalWorkspaceManager.ToFileUri(dupPaths[1]) }
                };

                List<ConversationSummaryInfo> allSums = new List<ConversationSummaryInfo>
                {
                    new ConversationSummaryInfo { ConversationId = "f1111111-1111-4111-8111-111111111111", ProjectId = p1, ProjectName = "INV3K_5K_VMH", FolderUri = projs[p1].FolderUri, UpdatedTime = DateTime.Now },
                    new ConversationSummaryInfo { ConversationId = "f2222222-2222-4222-8222-222222222222", ProjectId = p2, ProjectName = "INV3K_5K_VMH", FolderUri = projs[p2].FolderUri, UpdatedTime = DateTime.Now.AddMinutes(-5) },
                    cliSum
                };
                ProjectGroupsSnapshot grpSnap = WorkspaceService.BuildProjectDisplayGroups(allSums, projs, homeDir);
                AssertEqual(2, grpSnap.FormalProjectsCount, "Counts 2 distinct formal project groups despite identical basename");
                ProjectDisplayGroup auxCli = grpSnap.Groups.Find(x => x.BucketKey == "AUX:CLI");
                AssertTrue(auxCli != null && auxCli.PrimaryCount == 1, "Groups CLI sessions under AUX:CLI");
                ProjectDisplayGroup multiGrp = grpSnap.Groups.Find(x => x.ProjectId == p1);
                AssertTrue(multiGrp != null && multiGrp.IsMultiRoot && multiGrp.IsDuplicateBasename && multiGrp.ShortPath.Contains("多目录 · 2 个根目录"),
                    "Formal multi-root + duplicate group tagged accurately");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T40_DirectionalCapabilityDiff_BaselineAndUtcMtime_LineLcsDiff_ReadOnlyGc()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t40_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(sb);

                // 1. Baseline-backed directional diff
                CapabilityItem loc1 = new CapabilityItem { Kind = CapabilityKind.Skill, Id = "s-loc-new", ContentSignature = "SIG_NEW_LOCAL", LastModified = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
                CapabilityItem rem1 = new CapabilityItem { Kind = CapabilityKind.Skill, Id = "s-loc-new", ContentSignature = "SIG_BASE_1", LastModified = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc) };
                CapabilityItem loc2 = new CapabilityItem { Kind = CapabilityKind.Rule, Id = "r-rem-new", ContentSignature = "SIG_BASE_2", LastModified = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc) };
                CapabilityItem rem2 = new CapabilityItem { Kind = CapabilityKind.Rule, Id = "r-rem-new", ContentSignature = "SIG_NEW_REM", LastModified = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
                CapabilityItem loc3 = new CapabilityItem { Kind = CapabilityKind.McpServer, Id = "m-both", ContentSignature = "SIG_A", LastModified = DateTime.UtcNow };
                CapabilityItem rem3 = new CapabilityItem { Kind = CapabilityKind.McpServer, Id = "m-both", ContentSignature = "SIG_B", LastModified = DateTime.UtcNow };

                // 2. Baseline-less UTC mtime fallback
                DateTime tOld = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
                DateTime tNew = tOld.AddMinutes(5);
                CapabilityItem loc4 = new CapabilityItem { Kind = CapabilityKind.Hook, Id = "h-utc", ContentSignature = "H_L", LastModified = tOld };
                CapabilityItem rem4 = new CapabilityItem { Kind = CapabilityKind.Hook, Id = "h-utc", ContentSignature = "H_R", LastModified = tNew };

                Dictionary<string, string> baseMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                baseMap[loc1.UniqueKey] = "SIG_BASE_1";
                baseMap[loc2.UniqueKey] = "SIG_BASE_2";
                baseMap[loc3.UniqueKey] = "SIG_BASE_3";

                List<CapabilityDiffNode> diffs = VisualDiffEngine.ComputeCapabilityDiff(
                    new List<CapabilityItem> { loc1, loc2, loc3, loc4 },
                    new List<CapabilityItem> { rem1, rem2, rem3, rem4 },
                    baseMap);

                AssertEqual(CapabilityDiffDirection.LocalNewer, diffs.Find(x => x.Id == "s-loc-new").Direction, "Baseline detects LocalNewer even if remote mtime is newer");
                AssertEqual(CapabilityDiffDirection.RemoteNewer, diffs.Find(x => x.Id == "r-rem-new").Direction, "Baseline detects RemoteNewer");
                AssertEqual(CapabilityDiffDirection.BothModified, diffs.Find(x => x.Id == "m-both").Direction, "Baseline detects BothModified");
                CapabilityDiffNode noBaseNode = diffs.Find(x => x.Id == "h-utc");
                AssertEqual(CapabilityDiffDirection.Conflict, noBaseNode.Direction, "Classifies baseline-less diff as Conflict (no_baseline_review)");
                AssertEqual("no_baseline_review", noBaseNode.DirectionReason, "Sets DirectionReason to no_baseline_review");

                // 3. Line-level LCS diff + bounded truncation guard
                bool smallTrunc;
                List<TextDiffLine> lines = VisualDiffEngine.ComputeLineDiff("alpha\nbeta\ngamma", "alpha\nbeta2\ndelta\ngamma", out smallTrunc);
                AssertTrue(!smallTrunc, "Small diff is not truncated");
                AssertTrue(lines.Exists(x => x.Type == "equal" && x.Text == "alpha"), "Identifies common prefix");
                AssertTrue(lines.Exists(x => x.Type == "delete" && x.Text == "beta"), "Identifies deleted line");
                AssertTrue(lines.Exists(x => x.Type == "insert" && x.Text == "beta2"), "Identifies inserted line");

                StringBuilder bigL = new StringBuilder();
                StringBuilder bigR = new StringBuilder();
                for (int i = 0; i < 1100; i++)
                {
                    bigL.AppendLine("L_" + i);
                    bigR.AppendLine("R_" + i);
                }
                bool bigTrunc;
                List<TextDiffLine> bigLines = VisualDiffEngine.ComputeLineDiff(bigL.ToString(), bigR.ToString(), out bigTrunc);
                AssertTrue(bigTrunc && bigLines.Count > 0, "Bounded guard truncates >1000-line changed middle cleanly");

                // 4. ScanOrphanedData works with shared open handle on .pb
                string geminiDir = Path.Combine(sb, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                string annotDir = Path.Combine(agDir, "annotations");
                Directory.CreateDirectory(annotDir);
                string activeCid = "77777777-1111-4111-8111-111111111111";
                string orphanCid = "88888888-2222-4222-8222-222222222222";
                string pbPath = Path.Combine(agDir, "agyhub_summaries_proto.pb");
                File.WriteAllBytes(pbPath, ProtoCodec.Encode(new List<ProtoNode> { BuildRealProtoEntry(activeCid, "Active", null, "file:///d%3A/P", "P", "p", 1, 1, 1) }));
                File.WriteAllText(Path.Combine(annotDir, orphanCid + ".pbtxt"), "title:\"Orphan\"", Encoding.UTF8);

                using (FileStream sharedLock = new FileStream(pbPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
                {
                    OrphanScanResult scan = LocalWorkspaceManager.ScanOrphanedData(geminiDir);
                    AssertEqual(1, scan.OrphanAnnotationFiles.Count, "Read-only GC scans orphan .pbtxt while .pb is held open with ReadWrite sharing");
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T41_LocalWebHost_TcpFraming_AuthSecurity_StaticResources_AndReadApis()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t41_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                string repoDir = Path.Combine(sb, "Repo");
                string rulesDir = Path.Combine(repoDir, "rules", "_root");
                Directory.CreateDirectory(agDir);
                Directory.CreateDirectory(rulesDir);

                string cid = "c4100001-1111-4111-8111-111111111111";
                File.WriteAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb"),
                    ProtoCodec.Encode(new List<ProtoNode> { BuildRealProtoEntry(cid, "中文会话测试标题", null, "file:///d%3A/Work/ProjA", "ProjA", "pA", 5, 2, 1) }));
                File.WriteAllText(Path.Combine(geminiDir, "GEMINI.md"), "# Local Rule\nLine 1\n", Encoding.UTF8);
                File.WriteAllText(Path.Combine(rulesDir, "GEMINI.md"), "# Remote Rule\nLine 1\nLine 2\n", Encoding.UTF8);

                string token = "t41_secret_token_9876";
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, repoDir, 0, token))
                {
                    host.Start();
                    AssertTrue(host.Port > 0, "LocalWebHost binds to dynamic loopback port");

                    // 1. Missing token -> 403
                    RawHttpTestResponse unauth = SendRawTcpHttp(host.Port, null, "GET", "/", null, null);
                    AssertEqual(403, unauth.StatusCode, "Rejects GET / without token");

                    // 2. Host header rebinding check -> 403
                    RawHttpTestResponse badHost = SendRawTcpHttp(host.Port, "evil.example.com:80", "GET", "/api/overview",
                        new Dictionary<string, string> { { "X-AGY-Token", token } }, null);
                    AssertEqual(403, badHost.StatusCode, "Rejects non-loopback Host header (DNS rebinding guard)");

                    string sidName = "agy_sid_" + host.Port;

                    // 3. CSRF origin guard on cookie-only POST without X-AGY-Token -> 403
                    RawHttpTestResponse badOrigin = SendRawTcpHttp(host.Port, null, "POST", "/api/refresh",
                        new Dictionary<string, string> { { "Cookie", sidName + "=" + token }, { "Origin", "https://attacker.local" } }, "{}");
                    AssertEqual(403, badOrigin.StatusCode, "Rejects POST with cookie + cross-origin Origin header");

                    // 4. Bootstrap GET /?t=token sets HttpOnly SameSite=Strict port-scoped cookie & CSP headers + serves embedded SPA HTML
                    RawHttpTestResponse boot = SendRawTcpHttp(host.Port, null, "GET", "/?t=" + token, null, null);
                    AssertEqual(200, boot.StatusCode, "GET /?t=token succeeds");
                    AssertTrue(boot.SetCookie.Contains(sidName + "=" + token) && boot.SetCookie.Contains("HttpOnly") && boot.SetCookie.Contains("SameSite=Strict"),
                        "Sets port-scoped SameSite=Strict HttpOnly cookie");
                    string csp;
                    AssertTrue(boot.Headers.TryGetValue("Content-Security-Policy", out csp) &&
                        csp.Contains("script-src 'self'") && csp.Contains("style-src 'self' 'unsafe-inline'"),
                        "Sends hardened CSP header allowing inline styles but blocking inline scripts");
                    AssertTrue(boot.BodyText.Contains("Antigravity Companion"), "Serves embedded index.html");

                    // 5. Cookie-only reload (F5) + static CSS/JS + /api/auth/session
                    Dictionary<string, string> cookieHdr = new Dictionary<string, string> { { "Cookie", sidName + "=" + token } };
                    RawHttpTestResponse reload = SendRawTcpHttp(host.Port, null, "GET", "/", cookieHdr, null);
                    AssertEqual(200, reload.StatusCode, "Cookie-only reload GET / succeeds");
                    AssertEqual(200, SendRawTcpHttp(host.Port, null, "GET", "/styles.css", cookieHdr, null).StatusCode, "Cookie GET /styles.css succeeds");
                    AssertEqual(200, SendRawTcpHttp(host.Port, null, "GET", "/app.js", cookieHdr, null).StatusCode, "Cookie GET /app.js succeeds");

                    RawHttpTestResponse sessTok = SendRawTcpHttp(host.Port, null, "GET", "/api/auth/session", cookieHdr, null);
                    AssertEqual(200, sessTok.StatusCode, "GET /api/auth/session succeeds");
                    IDictionary<string, object> tokJson = UnifiedJson.Parse(sessTok.BodyText) as IDictionary<string, object>;
                    AssertEqual(token, UnifiedJson.GetString(tokJson, "token", ""), "Restores session token for X-AGY-Token header");

                    // 6. Read APIs & UTF-8 Content-Length byte exactness with Chinese characters
                    Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                    RawHttpTestResponse ov = SendRawTcpHttp(host.Port, null, "GET", "/api/overview", apiHdr, null);
                    AssertEqual(200, ov.StatusCode, "GET /api/overview succeeds");
                    AssertEqual(int.Parse(ov.Headers["Content-Length"]), ov.BodyByteCount, "Content-Length matches exact UTF-8 byte count");

                    RawHttpTestResponse sess = SendRawTcpHttp(host.Port, null, "GET", "/api/sessions", apiHdr, null);
                    AssertEqual(200, sess.StatusCode, "GET /api/sessions succeeds");
                    AssertTrue(sess.BodyText.Contains("中文会话测试标题"), "UTF-8 Chinese session title transmitted without truncation");

                    RawHttpTestResponse cands = SendRawTcpHttp(host.Port, null, "GET", "/api/rename/candidates", apiHdr, null);
                    AssertEqual(200, cands.StatusCode, "GET /api/rename/candidates succeeds");

                    RawHttpTestResponse caps = SendRawTcpHttp(host.Port, null, "GET", "/api/capabilities", apiHdr, null);
                    AssertEqual(200, caps.StatusCode, "GET /api/capabilities succeeds");

                    RawHttpTestResponse diff = SendRawTcpHttp(host.Port, null, "GET", "/api/capabilities/diff?key=Rule:GEMINI.md", apiHdr, null);
                    AssertEqual(200, diff.StatusCode, "GET /api/capabilities/diff succeeds for Rule:GEMINI.md");
                    AssertTrue(diff.BodyText.Contains("Remote Rule"), "Line diff response includes modified rule lines");

                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T42_LocalWebHost_EndToEndMutationSandbox_RenameRollbackCapabilityAndGc()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t42_" + Guid.NewGuid().ToString("N"));
            string zipPath = Path.Combine(sb, "seed_backup.agy");
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                string annotDir = Path.Combine(agDir, "annotations");
                string repoDir = Path.Combine(sb, "Repo");
                string projDir = Path.Combine(geminiDir, "config", "projects");
                Directory.CreateDirectory(annotDir);
                Directory.CreateDirectory(repoDir);
                Directory.CreateDirectory(projDir);

                string cid = "c4200001-1111-4111-8111-111111111111";
                string orphanCid = "c4200009-9999-4999-8999-999999999999";
                string projId = "p4200001-1111-4111-8111-111111111111";
                File.WriteAllText(Path.Combine(projDir, projId + ".json"),
                    WorkspaceService.GenerateStandardProjectJson(projId, "Inverter", "D:\\Work\\Inverter"), Encoding.UTF8);

                byte[] pbBytes = ProtoCodec.Encode(new List<ProtoNode>
                {
                    BuildRealProtoEntry(cid, "Original Sandbox Session", null, "file:///d%3A/Work/Inverter", "Inverter", projId, 6, 2, 1)
                });
                File.WriteAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb"), pbBytes);
                File.WriteAllText(Path.Combine(annotDir, orphanCid + ".pbtxt"), "title:\"OrphanNote\"", Encoding.UTF8);
                File.WriteAllText(Path.Combine(geminiDir, "GEMINI.md"), "# Seed Rule V1\n", Encoding.UTF8);

                List<ConversationSummaryInfo> seedList = SessionMetadataExtractor.ExtractSummariesFromProto(pbBytes);
                LocalWorkspaceManager.ExportSelectiveBackup(geminiDir, homeDir, zipPath, seedList, null, false, null);

                string token = "t42_secret_token_5432";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };

                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, repoDir, 0, token))
                {
                    host.Start();

                    // 1. Rename execute -> history -> rollback
                    string renPayload = "{\"strategy\":\"HotWritePersistentLayers\",\"items\":[{\"cid\":\"" + cid + "\",\"newTitle\":\"【3KU】Renamed Via HTTP\"}]}";
                    RawHttpTestResponse renResp = SendRawTcpHttp(host.Port, null, "POST", "/api/rename/execute", apiHdr, renPayload);
                    AssertEqual(200, renResp.StatusCode, "POST /api/rename/execute returns 200: " + renResp.BodyText);
                    IDictionary<string, object> renObj = UnifiedJson.Parse(renResp.BodyText) as IDictionary<string, object>;
                    AssertEqual(1, UnifiedJson.GetInt(renObj, "renamedCount", 0), "Renamed 1 session");

                    RawHttpTestResponse histResp = SendRawTcpHttp(host.Port, null, "GET", "/api/rename/history", apiHdr, null);
                    AssertEqual(200, histResp.StatusCode, "GET /api/rename/history returns 200");
                    IDictionary<string, object> histObj = UnifiedJson.Parse(histResp.BodyText) as IDictionary<string, object>;
                    List<object> snaps = histObj != null && histObj.ContainsKey("snapshots") ? UnifiedJson.AsList(histObj["snapshots"]) : null;
                    AssertTrue(snaps != null && snaps.Count == 1, "Snapshot history contains 1 record");
                    Dictionary<string, object> snapDict = snaps[0] as Dictionary<string, object>;
                    string snapDir = UnifiedJson.GetString(snapDict, "snapshotDir", "");
                    AssertTrue(!string.IsNullOrEmpty(snapDir) && UnifiedJson.GetString(snapDict, "previewSummary", "").Contains("Renamed Via HTTP"),
                        "History snapshot preview lists old -> new title");

                    string rbPayload = UnifiedJson.Serialize(new Dictionary<string, object> { { "strategy", "HotWritePersistentLayers" }, { "snapshotDir", snapDir } }, false);
                    RawHttpTestResponse rbResp = SendRawTcpHttp(host.Port, null, "POST", "/api/rename/rollback", apiHdr, rbPayload);
                    AssertEqual(200, rbResp.StatusCode, "POST /api/rename/rollback returns 200: " + rbResp.BodyText);

                    // 2. AI file prompt export & JSON load endpoints
                    string promptOut = Path.Combine(sb, "ai_prompt.txt");
                    string savePromptPayload = UnifiedJson.Serialize(new Dictionary<string, object> { { "cids", new List<object> { cid } }, { "outputPath", promptOut } }, false);
                    RawHttpTestResponse spResp = SendRawTcpHttp(host.Port, null, "POST", "/api/rename/ai-save-prompt", apiHdr, savePromptPayload);
                    AssertEqual(200, spResp.StatusCode, "POST /api/rename/ai-save-prompt returns 200");
                    AssertTrue(File.Exists(promptOut) && File.ReadAllText(promptOut, Encoding.UTF8).Contains(cid), "Saved AI prompt file contains cid");

                    string aiJsonIn = Path.Combine(sb, "ai_result.json");
                    File.WriteAllText(aiJsonIn, "{\"items\":[{\"id\":\"" + cid + "\",\"new_title\":\"【3KU】AI 文件回填标题\"}]}", Encoding.UTF8);
                    string loadJsonPayload = UnifiedJson.Serialize(new Dictionary<string, object> { { "cids", new List<object> { cid } }, { "inputPath", aiJsonIn } }, false);
                    RawHttpTestResponse ljResp = SendRawTcpHttp(host.Port, null, "POST", "/api/rename/ai-load-json", apiHdr, loadJsonPayload);
                    AssertEqual(200, ljResp.StatusCode, "POST /api/rename/ai-load-json returns 200: " + ljResp.BodyText);
                    AssertTrue(ljResp.BodyText.Contains("AI 文件回填标题"), "Parsed AI JSON file returns mapped title");

                    // 3. Capabilities Push -> Aligned -> Edit Repo -> Import
                    string capBody = "{\"keys\":[\"Rule:GEMINI.md\"]}";
                    AssertEqual(200, SendRawTcpHttp(host.Port, null, "POST", "/api/capabilities/push", apiHdr, capBody).StatusCode, "Push Rule:GEMINI.md returns 200");
                    RawHttpTestResponse capListAfterPush = SendRawTcpHttp(host.Port, null, "GET", "/api/capabilities", apiHdr, null);
                    AssertTrue(capListAfterPush.BodyText.Contains("\"direction\":\"Aligned\""), "Rule is Aligned after push");

                    string repoRulePath = Path.Combine(repoDir, "rules", "_root", "GEMINI.md");
                    File.WriteAllText(repoRulePath, "# Seed Rule V2 From Repo\n", Encoding.UTF8);
                    AssertEqual(200, SendRawTcpHttp(host.Port, null, "POST", "/api/refresh", apiHdr, "{}").StatusCode, "POST /api/refresh picks up external repo edits");
                    AssertEqual(200, SendRawTcpHttp(host.Port, null, "POST", "/api/capabilities/import", apiHdr, "{\"keys\":[\"Rule:GEMINI.md\"],\"source\":\"repo\"}").StatusCode,
                        "Import Rule:GEMINI.md from repo returns 200");
                    AssertTrue(File.ReadAllText(Path.Combine(geminiDir, "GEMINI.md"), Encoding.UTF8).Contains("V2 From Repo"),
                        "Local GEMINI.md updated from repo import");

                    // 4. GC Scan (+ conditional Clean if IDE not running on test host)
                    RawHttpTestResponse gcScanResp = SendRawTcpHttp(host.Port, null, "POST", "/api/gc/scan", apiHdr, "{}");
                    AssertEqual(200, gcScanResp.StatusCode, "POST /api/gc/scan returns 200");
                    IDictionary<string, object> gcScanObj = UnifiedJson.Parse(gcScanResp.BodyText) as IDictionary<string, object>;
                    AssertEqual(1, UnifiedJson.GetInt(gcScanObj, "totalItems", 0), "GC scan detects 1 orphan annotation");

                    RawHttpTestResponse gcCleanResp = SendRawTcpHttp(host.Port, null, "POST", "/api/gc/clean", apiHdr, "{\"closeIdeIfRunning\":false}");
                    if (ProcessGuardService.IsAntigravityRunning())
                        AssertEqual(409, gcCleanResp.StatusCode, "Refuses live GC clean while host IDE is running and closeIdeIfRunning=false");
                    else
                        AssertEqual(200, gcCleanResp.StatusCode, "Cleans orphan annotation cleanly when IDE not running");

                    // 5. Open .agy backup archive & read current backup index
                    string openBkPayload = UnifiedJson.Serialize(new Dictionary<string, object> { { "archivePath", zipPath } }, false);
                    RawHttpTestResponse bkOpenResp = SendRawTcpHttp(host.Port, null, "POST", "/api/backup/open", apiHdr, openBkPayload);
                    AssertEqual(200, bkOpenResp.StatusCode, "POST /api/backup/open returns 200: " + bkOpenResp.BodyText);
                    RawHttpTestResponse bkCurResp = SendRawTcpHttp(host.Port, null, "GET", "/api/backup/current", apiHdr, null);
                    AssertTrue(bkCurResp.BodyText.Contains("\"loaded\":true") && bkCurResp.BodyText.Contains("Original Sandbox Session"),
                        "GET /api/backup/current serves opened .agy summary");

                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // V1.0 P0/P1/P2 Regression Helpers & Suite (T43 ~ T57)
        // ══════════════════════════════════════════════════════════════════════

        private static void CreateMinimalValidSqliteDb(string dbPath, string markerValue)
        {
            string dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            IntPtr db = IntPtr.Zero;
            try
            {
                AssertEqual(0, t_sqlite3_open_v2(Utf8Z(dbPath), out db, 0x00000002 | 0x00000004, IntPtr.Zero),
                    "CreateMinimalValidSqliteDb open: " + dbPath);
                AssertEqual(0, t_sqlite3_exec(db, Utf8Z("CREATE TABLE IF NOT EXISTS trajectory_metadata_blob (k TEXT PRIMARY KEY, v TEXT);"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero),
                    "CreateMinimalValidSqliteDb DDL: " + T_GetSqliteErr(db));
                IntPtr stmt = IntPtr.Zero;
                AssertEqual(0, t_sqlite3_prepare_v2(db, Utf8Z("INSERT OR REPLACE INTO trajectory_metadata_blob (k, v) VALUES ('id', ?);"), -1, out stmt, IntPtr.Zero),
                    "CreateMinimalValidSqliteDb prepare");
                try
                {
                    AssertEqual(0, T_BindUtf8Text(stmt, 1, markerValue ?? string.Empty), "CreateMinimalValidSqliteDb bind");
                    AssertEqual(101, t_sqlite3_step(stmt), "CreateMinimalValidSqliteDb insert");
                }
                finally
                {
                    if (stmt != IntPtr.Zero) t_sqlite3_finalize(stmt);
                }
            }
            finally
            {
                if (db != IntPtr.Zero) t_sqlite3_close(db);
            }
        }

        private static string ReadSseUntilEvent(NetworkStream stream, string targetEvent, int timeoutMs)
        {
            byte[] buf = new byte[4096];
            StringBuilder acc = new StringBuilder();
            Stopwatch sw = Stopwatch.StartNew();
            stream.ReadTimeout = 500;
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                int n = 0;
                try
                {
                    n = stream.Read(buf, 0, buf.Length);
                }
                catch (IOException)
                {
                    continue;
                }
                if (n <= 0) break;
                acc.Append(Encoding.UTF8.GetString(buf, 0, n));
                string cur = acc.ToString();
                if (cur.IndexOf("event: " + targetEvent, StringComparison.Ordinal) >= 0)
                {
                    int idx = cur.IndexOf("event: " + targetEvent, StringComparison.Ordinal);
                    if (cur.IndexOf("\n\n", idx, StringComparison.Ordinal) >= 0)
                        return cur;
                }
            }
            return acc.ToString();
        }

        private static void T43_P0_1_RelocateProjectPath_NestedGitFolderFolderUri()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t43_" + Guid.NewGuid().ToString("N"));
            try
            {
                string geminiDir = Path.Combine(sb, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                string projDir = Path.Combine(geminiDir, "config", "projects");
                Directory.CreateDirectory(agDir);
                Directory.CreateDirectory(projDir);

                string pidA = "proj-nested-a";
                string pidB = "proj-nested-b";
                string cid = "c4300001-1111-4111-8111-111111111111";
                string oldUriA = "file:///d%3A/OldRoot/AlphaProj";
                string newPhysicalA = "E:\\NewRoot\\AlphaProj";
                string expectedUriA = SessionLifecycleService.ToFileUri(newPhysicalA);
                string uriB = "file:///e%3A/NewRoot/BetaProj";

                string nestedProjA =
                    "{\r\n  \"id\": \"" + pidA + "\",\r\n  \"name\": \"AlphaProj\",\r\n" +
                    "  \"projectResources\": {\r\n    \"resources\": [\r\n      {\r\n        \"gitFolder\": {\r\n          \"folderUri\": \"" + oldUriA + "\"\r\n        }\r\n      }\r\n    ]\r\n  }\r\n}";
                string nestedProjB =
                    "{\r\n  \"id\": \"" + pidB + "\",\r\n  \"name\": \"BetaProj\",\r\n" +
                    "  \"projectResources\": {\r\n    \"resources\": [\r\n      {\r\n        \"gitFolder\": {\r\n          \"folderUri\": \"" + uriB + "\"\r\n        }\r\n      }\r\n    ]\r\n  }\r\n}";

                string projAFile = Path.Combine(projDir, pidA + ".json");
                string projBFile = Path.Combine(projDir, pidB + ".json");
                File.WriteAllText(projAFile, nestedProjA, Encoding.UTF8);
                File.WriteAllText(projBFile, nestedProjB, Encoding.UTF8);

                ProtoNode pNode = BuildRealProtoEntry(cid, "P0-1 Session", null, oldUriA, "AlphaProj", pidA, 5, 2, 1);
                File.WriteAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb"), ProtoCodec.Encode(new List<ProtoNode> { pNode }));

                string summaryDb = Path.Combine(agDir, "conversation_summaries.db");
                IntPtr db = OpenAndCreateSummaryWalDb(summaryDb);
                try
                {
                    InsertSummaryDbRow(db, cid, "P0-1 Session", "preview", 5, "[\"" + oldUriA + "\"]", "DONE", pidA, "", "", 0, 0, ProtoCodec.Encode(pNode.Children));
                }
                finally
                {
                    t_sqlite3_close(db);
                }

                string annotDir = Path.Combine(agDir, "annotations");
                Directory.CreateDirectory(annotDir);
                File.WriteAllText(Path.Combine(annotDir, cid + ".pbtxt"), PbtxtAnnotationCodec.UpsertTitle(string.Empty, "P0-1 Session"), Encoding.UTF8);

                List<ConversationSummaryInfo> sums = SessionMetadataExtractor.ExtractSummaries(
                    File.ReadAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb")),
                    WorkspaceService.LoadLocalProjects(geminiDir));

                int relocCount = SessionLifecycleService.RelocateProjectPath(geminiDir, pidA, "AlphaProj", newPhysicalA, sums);
                AssertEqual(1, relocCount, "RelocateProjectPath updated 1 session");

                string updatedJsonA = File.ReadAllText(projAFile, Encoding.UTF8);
                List<string> urisA = DirectStreamArchiver.ExtractAllProjectFolderUris(updatedJsonA);
                AssertEqual(1, urisA.Count, "RelocateProjectPath keeps exactly 1 folderUri in nested projectResources.resources[0].gitFolder.folderUri");
                AssertEqual(expectedUriA.ToLowerInvariant(), urisA[0].ToLowerInvariant(), "Nested gitFolder.folderUri updated to new path");
                AssertTrue(updatedJsonA.Contains("\"gitFolder\"") && !updatedJsonA.Contains("OldRoot"), "Preserved gitFolder nesting and removed OldRoot URI");

                string sqlUrisAfterReloc = QuerySingleText(summaryDb, "SELECT workspace_uris FROM conversation_summaries WHERE conversation_id = ?;", cid);
                AssertTrue(sqlUrisAfterReloc != null && sqlUrisAfterReloc.IndexOf("NewRoot", StringComparison.OrdinalIgnoreCase) >= 0,
                    "SQLite workspace_uris updated after RelocateProjectPath");

                List<ConversationSummaryInfo> afterReloc = SessionMetadataExtractor.ExtractSummaries(
                    File.ReadAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb")),
                    WorkspaceService.LoadLocalProjects(geminiDir));
                AssertEqual("AlphaProj", afterReloc[0].ProjectName, "ProjectName remains AlphaProj after relocate");

                int moved = SessionLifecycleService.MoveSessionsToProject(geminiDir, new List<string> { cid }, "BetaProj", pidB, uriB);
                AssertEqual(1, moved, "MoveSessionsToProject moved 1 session to BetaProj");

                string afterMoveJsonB = File.ReadAllText(projBFile, Encoding.UTF8);
                List<string> urisB = DirectStreamArchiver.ExtractAllProjectFolderUris(afterMoveJsonB);
                AssertEqual(1, urisB.Count, "Target BetaProj retains single nested folderUri");

                List<ConversationSummaryInfo> afterMove = SessionMetadataExtractor.ExtractSummaries(
                    File.ReadAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb")),
                    WorkspaceService.LoadLocalProjects(geminiDir));
                AssertEqual(pidB, afterMove[0].ProjectId, "Proto entry projectId updated to BetaProj");
                AssertEqual(uriB.ToLowerInvariant(), (afterMove[0].FolderUri ?? string.Empty).ToLowerInvariant(), "Proto workspace URI updated to BetaProj nested folderUri");
                AssertEqual(pidB, QuerySingleText(summaryDb, "SELECT project_id FROM conversation_summaries WHERE conversation_id = ?;", cid),
                    "SQLite project_id updated to BetaProj after MoveSessionsToProject");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T44_P0_2_P0_8_CapabilityDiff_ConflictNoBaselineAndArchiveIgnoresBaseline()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t44_" + Guid.NewGuid().ToString("N"));
            string zipPath = Path.Combine(sb, "cap_nobase.agy");
            try
            {
                DateTime t1 = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
                CapabilityItem loc = new CapabilityItem { Kind = CapabilityKind.Rule, Id = "GEMINI.md", DisplayName = "GEMINI.md", ContentSignature = "SIG_LOC", RawJsonFragment = "# Local", LastModified = t1 };
                CapabilityItem rem = new CapabilityItem { Kind = CapabilityKind.Rule, Id = "GEMINI.md", DisplayName = "GEMINI.md", ContentSignature = "SIG_REM", RawJsonFragment = "# Remote", LastModified = t1.AddHours(2) };

                // P0-2: Null baseline => Conflict (no_baseline_review) regardless of mtime skew
                List<CapabilityDiffNode> noBaseDiff = VisualDiffEngine.ComputeCapabilityDiff(new List<CapabilityItem> { loc }, new List<CapabilityItem> { rem }, null);
                AssertEqual(1, noBaseDiff.Count, "1 diff node");
                AssertEqual(CapabilityDiffDirection.Conflict, noBaseDiff[0].Direction, "P0-2: Baseline-less diff yields Conflict");
                AssertEqual("no_baseline_review", noBaseDiff[0].DirectionReason, "P0-2: DirectionReason is no_baseline_review");

                // P0-8: Host with a vault baseline must still ignore _capBaseline when loading a .agy archive
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string repoDir = Path.Combine(sb, "Repo");
                string archHome = Path.Combine(sb, "ArchHome");
                string archGemini = Path.Combine(archHome, ".gemini");
                Directory.CreateDirectory(geminiDir);
                Directory.CreateDirectory(repoDir);
                Directory.CreateDirectory(archGemini);

                File.WriteAllText(Path.Combine(geminiDir, "GEMINI.md"), "# Local Seed\n", Encoding.UTF8);
                File.WriteAllText(Path.Combine(archGemini, "GEMINI.md"), "# Archive Modified\n", Encoding.UTF8);

                List<CapabilityItem> archCaps = CapabilityVaultService.ScanLocalCapabilities(archGemini, archHome);
                LocalWorkspaceManager.ExportSelectiveBackup(archGemini, archHome, zipPath, null, null, false, archCaps);

                // Seed a baseline entry claiming GEMINI.md matches archive's signature (would wrongly flag LocalNewer if vault baseline leaked into archive diff)
                Dictionary<string, string> fakeBaseline = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                fakeBaseline["Rule:GEMINI.md"] = archCaps[0].ContentSignature;
                CapabilitySyncBaselineStore.Save(Path.Combine(geminiDir, ".companion_cap_sync_state.json"), fakeBaseline);

                string token = "t44_token_1234";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, repoDir, 0, token))
                {
                    host.Start();
                    string openPayload = UnifiedJson.Serialize(new Dictionary<string, object> { { "archivePath", zipPath } }, false);
                    RawHttpTestResponse openResp = SendRawTcpHttp(host.Port, null, "POST", "/api/backup/open", apiHdr, openPayload);
                    AssertEqual(200, openResp.StatusCode, "Open .agy returns 200");

                    RawHttpTestResponse bkCur = SendRawTcpHttp(host.Port, null, "GET", "/api/backup/current", apiHdr, null);
                    AssertEqual(200, bkCur.StatusCode, "GET /api/backup/current returns 200");
                    AssertTrue(bkCur.BodyText.Contains("\"direction\":\"Conflict\"") && bkCur.BodyText.Contains("no_baseline_review"),
                        "P0-8: Loaded .agy archive capability diff ignores vault baseline and classifies diff as Conflict/no_baseline_review");
                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T45_P0_3_CliArtifactsIncludedAndBinDirectoriesExcluded()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t45_" + Guid.NewGuid().ToString("N"));
            string fullZip = Path.Combine(sb, "full.agy");
            string selZip = Path.Combine(sb, "sel.agy");
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string cliDir = Path.Combine(geminiDir, "antigravity-cli");
                string agDir = Path.Combine(geminiDir, "antigravity");
                string cliBrainCid = "b4500001-0000-4000-8000-000000000001";
                Directory.CreateDirectory(Path.Combine(cliDir, "conversations"));
                Directory.CreateDirectory(Path.Combine(cliDir, "brain", cliBrainCid));
                Directory.CreateDirectory(Path.Combine(cliDir, "bin"));
                Directory.CreateDirectory(Path.Combine(agDir, "bin"));
                Directory.CreateDirectory(Path.Combine(agDir, "conversations"));

                CreateMinimalValidSqliteDb(Path.Combine(cliDir, "conversations", cliBrainCid + ".db"), cliBrainCid);
                File.WriteAllText(Path.Combine(cliDir, "settings.json"), "{\"cliMode\":true}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(cliDir, "brain", cliBrainCid, "notes.md"), "# CLI Brain Note", Encoding.UTF8);
                File.WriteAllText(Path.Combine(cliDir, "bin", "antigravity-cli.exe"), "BIN1", Encoding.UTF8);
                File.WriteAllText(Path.Combine(agDir, "bin", "rg.exe"), "BIN2", Encoding.UTF8);

                string cid = "c4500001-0000-4000-8000-000000000001";
                byte[] pb = ProtoCodec.Encode(new List<ProtoNode> { BuildRealProtoEntry(cid, "S45", null, "file:///c%3A/P", "P", "p1", 2, 1, 1) });
                File.WriteAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb"), pb);
                CreateMinimalValidSqliteDb(Path.Combine(agDir, "conversations", cid + ".db"), cid);

                int fileCount;
                List<string> warnings;
                ArchiveTransferService.CreateFullBackup(geminiDir, homeDir, new List<ConversationSummaryInfo>(), fullZip, null, out fileCount, out warnings);

                using (ZipArchive zFull = ZipFile.OpenRead(fullZip))
                {
                    bool hasCliDb = false;
                    bool hasCliBrain = false;
                    bool hasCliSettings = false;
                    bool hasBinFile = false;
                    for (int i = 0; i < zFull.Entries.Count; i++)
                    {
                        string fn = zFull.Entries[i].FullName.Replace('\\', '/');
                        if (fn == "data/antigravity-cli/conversations/" + cliBrainCid + ".db") hasCliDb = true;
                        if (fn.StartsWith("data/antigravity-cli/brain/", StringComparison.OrdinalIgnoreCase)) hasCliBrain = true;
                        if (fn == "data/antigravity-cli/settings.json") hasCliSettings = true;
                        if (fn.IndexOf("antigravity-cli/bin/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            fn.IndexOf("antigravity/bin/", StringComparison.OrdinalIgnoreCase) >= 0) hasBinFile = true;
                    }
                    AssertTrue(hasCliDb, "Full backup includes data/antigravity-cli/conversations/<cid>.db");
                    AssertTrue(hasCliBrain, "Full backup includes data/antigravity-cli/brain/");
                    AssertTrue(hasCliSettings, "Full backup includes data/antigravity-cli/settings.json");
                    AssertTrue(!hasBinFile, "Full backup strictly excludes antigravity-cli/bin/ and antigravity/bin/");
                }

                // Selective backup also excludes bin/ and can include CLI settings when requested
                LocalWorkspaceManager.ExportSelectiveBackup(geminiDir, homeDir, selZip, new List<ConversationSummaryInfo>(), null, true, null);
                using (ZipArchive zSel = ZipFile.OpenRead(selZip))
                {
                    bool selHasBin = false;
                    for (int i = 0; i < zSel.Entries.Count; i++)
                    {
                        string fn = zSel.Entries[i].FullName.Replace('\\', '/');
                        if (fn.Contains("/bin/")) selHasBin = true;
                    }
                    AssertTrue(!selHasBin, "Selective backup also excludes bin/");
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T46_P0_4_WalLiveDbAtomicBackup_IntegrityCheckOk()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t46_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(sb);
                string liveDb = Path.Combine(sb, "live.db");
                string backupDb = Path.Combine(sb, "backup.db");

                IntPtr db = IntPtr.Zero;
                try
                {
                    AssertEqual(0, t_sqlite3_open_v2(Utf8Z(liveDb), out db, 0x00000002 | 0x00000004, IntPtr.Zero), "Open live DB");
                    AssertEqual(0, t_sqlite3_exec(db, Utf8Z("PRAGMA journal_mode=WAL; CREATE TABLE items (id INTEGER PRIMARY KEY, val TEXT);"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero),
                        "Enable WAL & create table");
                    AssertEqual(0, t_sqlite3_exec(db, Utf8Z("INSERT INTO items (id, val) VALUES (1, 'committed'), (2, 'wal_uncheckpointed_row');"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero),
                        "Insert WAL rows without checkpoint while keeping connection open");

                    string err;
                    bool ok = SqliteOnlineBackup.BackupDatabaseAtomic(liveDb, backupDb, out err);
                    AssertTrue(ok, "SqliteOnlineBackup.BackupDatabaseAtomic succeeds on open WAL DB: " + err);
                }
                finally
                {
                    if (db != IntPtr.Zero) t_sqlite3_close(db);
                }

                string detail;
                bool integrityOk = SqliteOnlineBackup.RunIntegrityCheck(backupDb, out detail);
                AssertTrue(integrityOk && string.Equals(detail, "ok", StringComparison.OrdinalIgnoreCase),
                    "PRAGMA integrity_check == ok on backed up WAL DB (got: " + detail + ")");

                string val2 = QuerySingleText(backupDb, "SELECT val FROM items WHERE id = 2;", null);
                AssertEqual("wal_uncheckpointed_row", val2, "Uncheckpointed -wal row preserved in atomic backup DB");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T47_P0_5_JsoncCommentsPreservedAndSingleTimestampedBackupPerBatch()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t47_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string cfgDir = Path.Combine(geminiDir, "config");
                Directory.CreateDirectory(cfgDir);

                string mcpFile = Path.Combine(cfgDir, "mcp_config.json");
                File.WriteAllText(mcpFile,
                    "{\r\n  // Header JSONC comment must stay\r\n  \"mcpServers\": {\r\n    \"keep-me\": { /* inline comment */ \"command\": \"node\", \"args\": [\"a.js\"] }\r\n  }\r\n}\r\n",
                    Encoding.UTF8);

                List<CapabilityItem> batchCaps = new List<CapabilityItem>
                {
                    new CapabilityItem { Kind = CapabilityKind.McpServer, Id = "srv-a", DisplayName = "srv-a", RawJsonFragment = "{\"command\":\"python\",\"args\":[\"s1.py\"]}" },
                    new CapabilityItem { Kind = CapabilityKind.McpServer, Id = "srv-b", DisplayName = "srv-b", RawJsonFragment = "{\"command\":\"python\",\"args\":[\"s2.py\"]}" }
                };

                int count = CapabilityVaultService.ImportCapabilitiesToLocal(geminiDir, homeDir, batchCaps);
                AssertEqual(2, count, "Imported 2 MCP capabilities in one batch");

                string after = File.ReadAllText(mcpFile, Encoding.UTF8);
                AssertTrue(after.Contains("// Header JSONC comment must stay"), "Line JSONC comment preserved");
                AssertTrue(after.Contains("/* inline comment */"), "Block JSONC comment preserved");
                Dictionary<string, string> entries = CapabilityVaultService.ExtractObjectEntries(after, "mcpServers");
                AssertEqual(3, entries.Count, "Existing keep-me + srv-a + srv-b all present");

                string[] bakFiles = Directory.GetFiles(cfgDir, "mcp_config.json.bak.*", SearchOption.TopDirectoryOnly);
                AssertEqual(1, bakFiles.Length, "P0-5: Exactly ONE timestamped .bak.* file created for batch mutation");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T48_P0_6_P0_7_SelectiveImportSubagentClosureAndParentIdInSqlite()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t48_" + Guid.NewGuid().ToString("N"));
            string zipPath = Path.Combine(sb, "closure.agy");
            try
            {
                string srcHome = Path.Combine(sb, "SrcHome");
                string srcGemini = Path.Combine(srcHome, ".gemini");
                string srcAg = Path.Combine(srcGemini, "antigravity");
                Directory.CreateDirectory(Path.Combine(srcAg, "conversations"));
                Directory.CreateDirectory(Path.Combine(srcAg, "brain"));

                string dstHome = Path.Combine(sb, "DstHome");
                string dstGemini = Path.Combine(dstHome, ".gemini");
                string dstAg = Path.Combine(dstGemini, "antigravity");
                Directory.CreateDirectory(Path.Combine(dstAg, "conversations"));

                string cidParent = "p4800001-0000-4000-8000-000000000001";
                string cidSub = "s4800002-0000-4000-8000-000000000002";

                ProtoNode nPar = BuildRealProtoEntry(cidParent, "Parent Session", null, "file:///c%3A/Work/P48", "P48", "pid-48", 10, 3, 1);
                ProtoNode nSub = BuildRealProtoEntry(cidSub, "Subagent Task", null, "file:///c%3A/Work/P48", "P48", "pid-48", 4, 1, 0);
                nSub.GetChild(2).GetChild(17).Children.Add(new ProtoNode(11, 2) { RawBytes = Encoding.UTF8.GetBytes(cidParent), IsModified = true });
                byte[] srcPb = ProtoCodec.Encode(new List<ProtoNode> { nPar, nSub });
                File.WriteAllBytes(Path.Combine(srcAg, "agyhub_summaries_proto.pb"), srcPb);
                CreateMinimalValidSqliteDb(Path.Combine(srcAg, "conversations", cidParent + ".db"), cidParent);
                CreateMinimalValidSqliteDb(Path.Combine(srcAg, "conversations", cidSub + ".db"), cidSub);

                List<ConversationSummaryInfo> srcSums = SessionMetadataExtractor.ExtractSummariesFromProto(srcPb);
                LocalWorkspaceManager.ExportSelectiveBackup(srcGemini, srcHome, zipPath, srcSums, null, false, null);

                // Seed target SQLite DB at antigravity/conversation_summaries.db
                string targetSummaryDb = Path.Combine(dstAg, "conversation_summaries.db");
                IntPtr db = OpenAndCreateSummaryWalDb(targetSummaryDb);
                t_sqlite3_close(db);

                BackupArchiveIndex idx = StreamingZipIndexReader.ReadIndex(zipPath, dstHome, dstGemini);
                ConversationSummaryInfo parentOnly = idx.Conversations.Find(x => x.ConversationId == cidParent);
                AssertTrue(parentOnly != null, "Parent session indexed from .agy");

                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    GeminiPath = dstGemini,
                    HomePath = dstHome,
                    SourceMode = RightPaneSourceMode.BackupArchive,
                    LoadedBackupPath = zipPath,
                    BackupIndex = idx,
                    SelectedConversations = new List<ConversationSummaryInfo> { parentOnly }
                };
                OperationResult<SelectiveImportResult> impRes = ArchiveTransferService.PerformSelectiveImportResult(req, null);
                AssertTrue(impRes.Ok, "Selective import succeeded: " + impRes.ErrorMessage);
                AssertTrue(impRes.Data.ImportedConvIds.Contains(cidParent) && impRes.Data.ImportedConvIds.Contains(cidSub),
                    "P0-7: Importing parent auto-expanded subagent closure");

                string gotParentInSql = QuerySingleText(targetSummaryDb, "SELECT parent_conversation_id FROM conversation_summaries WHERE conversation_id = ?;", cidSub);
                AssertEqual(cidParent, gotParentInSql, "P0-6: Subagent parent_conversation_id persisted in SQLite conversation_summaries");

                // Also verify LocalWebHost /api/backup/current groups subagent under parent
                string token = "t48_tok_9876";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(dstHome, dstGemini, Path.Combine(sb, "Repo"), 0, token))
                {
                    host.Start();
                    SendRawTcpHttp(host.Port, null, "POST", "/api/backup/open", apiHdr,
                        UnifiedJson.Serialize(new Dictionary<string, object> { { "archivePath", zipPath } }, false));
                    RawHttpTestResponse curResp = SendRawTcpHttp(host.Port, null, "GET", "/api/backup/current", apiHdr, null);
                    AssertEqual(200, curResp.StatusCode, "GET /api/backup/current returns 200");
                    AssertTrue(curResp.BodyText.Contains("\"childCount\":1"), "P0-7: Backup session grouping reports childCount:1 on parent");
                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T49_P1_1_NotFullyIdleLockedVsActionableSeparation()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t49_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                Directory.CreateDirectory(agDir);

                string cidIdle = "i4900001-0000-4000-8000-000000000001";
                string cidLocked = "l4900002-0000-4000-8000-000000000002";

                ProtoNode nIdle = BuildRealProtoEntry(cidIdle, "Idle Untagged Session", null, "file:///c%3A/P", "Proj", "p49", 5, 1, 1);
                ProtoNode nLocked = BuildRealProtoEntry(cidLocked, "Active Locked Session", null, "file:///c%3A/P", "Proj", "p49", 8, 2, 1);
                File.WriteAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb"), ProtoCodec.Encode(new List<ProtoNode> { nIdle, nLocked }));

                string summaryDb = Path.Combine(agDir, "conversation_summaries.db");
                IntPtr db = OpenAndCreateSummaryWalDb(summaryDb);
                try
                {
                    InsertSummaryDbRow(db, cidIdle, "Idle Untagged Session", "p", 5, "file:///c%3A/P", "DONE", "p49", "", "", 0, 0, ProtoCodec.Encode(nIdle.Children));
                    InsertSummaryDbRow(db, cidLocked, "Active Locked Session", "p", 8, "file:///c%3A/P", "RUNNING", "p49", "", "", 0, 1, ProtoCodec.Encode(nLocked.Children));
                }
                finally
                {
                    t_sqlite3_close(db);
                }

                string token = "t49_tok_1122";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, Path.Combine(sb, "Repo"), 0, token))
                {
                    host.Start();
                    RawHttpTestResponse ov = SendRawTcpHttp(host.Port, null, "GET", "/api/overview", apiHdr, null);
                    AssertEqual(200, ov.StatusCode, "GET /api/overview returns 200");
                    IDictionary<string, object> ovObj = UnifiedJson.Parse(ov.BodyText) as IDictionary<string, object>;
                    IDictionary<string, object> countsObj = ovObj != null && ovObj.ContainsKey("counts") ? UnifiedJson.AsDict(ovObj["counts"]) : null;
                    AssertTrue(countsObj != null, "Overview contains counts dictionary");
                    AssertEqual(1, UnifiedJson.GetInt(countsObj, "untaggedActionableCount", -1), "P1-1: Overview counts.untaggedActionableCount == 1");
                    AssertEqual(1, UnifiedJson.GetInt(countsObj, "untaggedLockedActiveCount", -1), "P1-1: Overview counts.untaggedLockedActiveCount == 1");
                    List<object> lockedOv = ovObj.ContainsKey("lockedSessions") ? UnifiedJson.AsList(ovObj["lockedSessions"]) : null;
                    AssertTrue(lockedOv != null && lockedOv.Count == 1 && ov.BodyText.Contains(cidLocked), "P1-1: Overview exposes lockedSessions with cidLocked");

                    RawHttpTestResponse cands = SendRawTcpHttp(host.Port, null, "GET", "/api/rename/candidates", apiHdr, null);
                    AssertEqual(200, cands.StatusCode, "GET /api/rename/candidates returns 200");
                    IDictionary<string, object> cObj = UnifiedJson.Parse(cands.BodyText) as IDictionary<string, object>;
                    AssertEqual(1, UnifiedJson.GetInt(cObj, "actionableCount", -1), "P1-1: Candidates actionableCount == 1");
                    AssertEqual(1, UnifiedJson.GetInt(cObj, "lockedActiveCount", -1), "P1-1: Candidates lockedActiveCount == 1");
                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T50_P1_2_SlashCommandStripCustomBracketPreserveAndHyphenDedup()
        {
            RenameRulesConfig rules = new RenameRulesConfig();
            rules.PreserveExistingTag = true;
            rules.KeepExistingBracketedPrefix = true;

            string pref1, cleaned1, title1;
            AutoTitleSynthesizer.SynthesizeTitle(
                new ConversationSummaryInfo { ConversationId = "s50-1", ProjectName = "my-app", DisplayTitle = "" },
                "/custom-audit Check memory leaks in worker",
                null,
                rules,
                out pref1,
                out cleaned1,
                out title1);
            AssertTrue(!cleaned1.Contains("/custom-audit") && cleaned1.Contains("Check memory leaks"),
                "P1-2: Arbitrary slash command stripped from first prompt");

            string pref2, cleaned2, title2;
            AutoTitleSynthesizer.SynthesizeTitle(
                new ConversationSummaryInfo { ConversationId = "s50-2", ProjectName = "my-app", DisplayTitle = "【CustomTag】Important Handcrafted Title" },
                "Some later prompt",
                null,
                rules,
                out pref2,
                out cleaned2,
                out title2);
            AssertEqual("【CustomTag】Important Handcrafted Title", title2,
                "P1-2: Meaningful custom 【...】 title preserved intact");

            string pref3, cleaned3, title3;
            AutoTitleSynthesizer.SynthesizeTitle(
                new ConversationSummaryInfo { ConversationId = "s50-3", ProjectName = "my-app", DisplayTitle = "my-app: Fix build failure" },
                "my-app: Fix build failure",
                null,
                rules,
                out pref3,
                out cleaned3,
                out title3);
            AssertEqual("【my-app】Fix build failure", title3,
                "P1-2: Hyphenated project prefix deduplicated before prefixing");
        }

        private static void T51_P1_4_CapabilitiesRelocateFailureReturnsHttp400()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t51_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string cfgDir = Path.Combine(geminiDir, "config");
                Directory.CreateDirectory(cfgDir);
                File.WriteAllText(Path.Combine(cfgDir, "mcp_config.json"),
                    "{\"mcpServers\":{\"srv-existing\":{\"command\":\"python\",\"args\":[\"C:\\\\existing_tool.py\"]}}}",
                    Encoding.UTF8);

                string token = "t51_tok_400";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, Path.Combine(sb, "Repo"), 0, token))
                {
                    host.Start();
                    string payload = "{\"key\":\"McpServer:srv-existing\",\"target\":\"local\",\"oldPath\":\"C:\\\\wrong_mismatch.py\",\"newPath\":\"C:\\\\new.py\"}";
                    RawHttpTestResponse resp = SendRawTcpHttp(host.Port, null, "POST", "/api/capabilities/relocate", apiHdr, payload);
                    AssertEqual(400, resp.StatusCode, "P1-4: Failed capability relocate returns HTTP 400");
                    AssertTrue(resp.BodyText.Contains("\"ok\":false") && resp.BodyText.Contains("\"error\""),
                        "P1-4: 400 response contains {ok:false, error:...}");

                    // Also verify "target":"repo" fallback honors repo scope (not silently routed to local)
                    string repoPayload = "{\"key\":\"McpServer:srv-existing\",\"target\":\"repo\",\"oldPath\":\"C:\\\\wrong.py\",\"newPath\":\"C:\\\\new.py\"}";
                    RawHttpTestResponse repoResp = SendRawTcpHttp(host.Port, null, "POST", "/api/capabilities/relocate", apiHdr, repoPayload);
                    AssertEqual(404, repoResp.StatusCode, "Relocate with target=repo checks repo pool rather than local");
                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T52_P1_6_Fts5ZeroHitLikeFallback()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t52_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(sb);
                string dbPath = Path.Combine(sb, "state.vscdb");
                string cid = "f5200001-0000-4000-8000-000000000001";
                IntPtr db = OpenAndCreateSummaryWalDb(dbPath);
                try
                {
                    ProtoNode n = BuildRealProtoEntry(cid, "AlphaBeta99Token Gamma", null, "file:///c%3A/P", "Proj", "p52", 3, 1, 1);
                    InsertSummaryDbRow(db, cid, "AlphaBeta99Token Gamma", "preview", 3, "file:///c%3A/P", "DONE", "p52", "", "", 0, 0, ProtoCodec.Encode(n.Children));
                    InsertFtsDbRow(db, cid, 0, "AlphaBeta99Token Gamma", "C:/P", "Proj", "AlphaBeta99Token", "Gamma");
                }
                finally
                {
                    t_sqlite3_close(db);
                }

                // FTS5 unicode61 tokenizes "AlphaBeta99Token" as one token -> searching substring "Beta99" gives 0 FTS5 hits -> triggers LIKE fallback!
                string err;
                HashSet<string> hits = SummarySqliteBridge.SearchConversationsFullText(dbPath, "Beta99", out err);
                AssertTrue(hits != null && hits.Contains(cid), "P1-6: FTS5 0-hit query falls back to LIKE and finds cid (err=" + err + ")");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T53_P1_8_ImportedProjectIdRewriteAndOrphanPbtxtExcluded()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t53_" + Guid.NewGuid().ToString("N"));
            string zipPath = Path.Combine(sb, "p1_8.agy");
            try
            {
                string srcHome = Path.Combine(sb, "SrcHome");
                string srcGemini = Path.Combine(srcHome, ".gemini");
                string srcAg = Path.Combine(srcGemini, "antigravity");
                string srcAnnot = Path.Combine(srcAg, "annotations");
                string srcProjDir = Path.Combine(srcGemini, "config", "projects");
                Directory.CreateDirectory(Path.Combine(srcAg, "conversations"));
                Directory.CreateDirectory(srcAnnot);
                Directory.CreateDirectory(srcProjDir);

                string dstHome = Path.Combine(sb, "DstHome");
                string dstGemini = Path.Combine(dstHome, ".gemini");
                string dstAg = Path.Combine(dstGemini, "antigravity");
                string dstProjDir = Path.Combine(dstGemini, "config", "projects");
                Directory.CreateDirectory(Path.Combine(dstAg, "conversations"));
                Directory.CreateDirectory(dstProjDir);

                string cidKeep = "k5300001-0000-4000-8000-000000000001";
                string cidOrphan = "o5300002-0000-4000-8000-000000000002";
                string bakPid = "bak-pid-53";
                string targetPid = "local-target-pid-53";

                File.WriteAllText(Path.Combine(srcProjDir, bakPid + ".json"),
                    WorkspaceService.GenerateStandardProjectJson(bakPid, "SameProj", "D:\\MachineA\\SameProj"), Encoding.UTF8);

                // Seed local project with the SAME project name on different local path & different local ID so import maps bakPid -> targetPid
                File.WriteAllText(Path.Combine(dstProjDir, targetPid + ".json"),
                    WorkspaceService.GenerateStandardProjectJson(targetPid, "SameProj", "D:\\MachineB\\SameProj"), Encoding.UTF8);

                ProtoNode nKeep = BuildRealProtoEntry(cidKeep, "Kept Session", null, "file:///d%3A/MachineA/SameProj", "SameProj", bakPid, 3, 1, 1);
                byte[] pb = ProtoCodec.Encode(new List<ProtoNode> { nKeep });
                File.WriteAllBytes(Path.Combine(srcAg, "agyhub_summaries_proto.pb"), pb);
                CreateMinimalValidSqliteDb(Path.Combine(srcAg, "conversations", cidKeep + ".db"), cidKeep);
                File.WriteAllText(Path.Combine(srcAnnot, cidKeep + ".pbtxt"), PbtxtAnnotationCodec.UpsertTitle(string.Empty, "Kept Session"), Encoding.UTF8);
                File.WriteAllText(Path.Combine(srcAnnot, cidOrphan + ".pbtxt"), PbtxtAnnotationCodec.UpsertTitle(string.Empty, "Orphan Annotation"), Encoding.UTF8);

                // Pack full backup so zip contains both the project JSON and the orphan .pbtxt
                int fc;
                List<string> warns;
                ArchiveTransferService.CreateFullBackup(srcGemini, srcHome, new List<ConversationSummaryInfo> { SessionMetadataExtractor.ExtractSummariesFromProto(pb)[0] }, zipPath, null, out fc, out warns);

                BackupArchiveIndex idx = StreamingZipIndexReader.ReadIndex(zipPath, dstHome, dstGemini);
                Dictionary<string, ProjectMetadata> locProjs = WorkspaceService.LoadLocalProjects(dstGemini);
                SelectiveImportRequest req = new SelectiveImportRequest
                {
                    GeminiPath = dstGemini,
                    HomePath = dstHome,
                    SourceMode = RightPaneSourceMode.BackupArchive,
                    LoadedBackupPath = zipPath,
                    BackupIndex = idx,
                    LocalProjects = locProjs,
                    SelectedConversations = new List<ConversationSummaryInfo>(idx.Conversations)
                };
                OperationResult<SelectiveImportResult> imp = ArchiveTransferService.PerformSelectiveImportResult(req, null);
                AssertTrue(imp.Ok, "P1-8 Selective import ok: " + imp.ErrorMessage);

                AssertTrue(!File.Exists(Path.Combine(dstGemini, "antigravity", "annotations", cidOrphan + ".pbtxt")),
                    "P1-8: Unselected orphan .pbtxt excluded from selective import");
                AssertTrue(File.Exists(Path.Combine(dstGemini, "antigravity", "annotations", cidKeep + ".pbtxt")),
                    "Selected session .pbtxt imported");

                string[] projFiles = Directory.GetFiles(dstProjDir, "*.json");
                for (int i = 0; i < projFiles.Length; i++)
                {
                    string expectedId = Path.GetFileNameWithoutExtension(projFiles[i]);
                    Dictionary<string, object> pDict = UnifiedJson.Parse(File.ReadAllText(projFiles[i], Encoding.UTF8)) as Dictionary<string, object>;
                    AssertEqual(expectedId, UnifiedJson.GetString(pDict, "id", ""), "P1-8: Project JSON internal id matches filename/targetId");
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T54_P1_10_SseDoneEventOkFalseOnImportError()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t54_" + Guid.NewGuid().ToString("N"));
            string zipPath = Path.Combine(sb, "p1_10.agy");
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string srcHome = Path.Combine(sb, "SrcHome");
                string srcGemini = Path.Combine(srcHome, ".gemini");
                Directory.CreateDirectory(geminiDir);
                Directory.CreateDirectory(srcGemini);
                File.WriteAllText(Path.Combine(srcGemini, "GEMINI.md"), "# Seed Rule\n", Encoding.UTF8);

                List<CapabilityItem> caps = CapabilityVaultService.ScanLocalCapabilities(srcGemini, srcHome);
                LocalWorkspaceManager.ExportSelectiveBackup(srcGemini, srcHome, zipPath, null, null, false, caps);

                string token = "t54_tok_sse";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, Path.Combine(sb, "Repo"), 0, token))
                {
                    host.Start();

                    RawHttpTestResponse openResp = SendRawTcpHttp(host.Port, null, "POST", "/api/backup/open", apiHdr,
                        UnifiedJson.Serialize(new Dictionary<string, object> { { "archivePath", zipPath } }, false));
                    AssertEqual(200, openResp.StatusCode, "Open backup succeeded");

                    // Connect SSE listener before deleting archive & starting failing import
                    using (TcpClient sseClient = new TcpClient("127.0.0.1", host.Port))
                    {
                        sseClient.ReceiveTimeout = 5000;
                        NetworkStream sseStream = sseClient.GetStream();
                        byte[] reqBytes = Encoding.ASCII.GetBytes(
                            "GET /api/events HTTP/1.1\r\nHost: 127.0.0.1:" + host.Port + "\r\nX-AGY-Token: " + token + "\r\nConnection: keep-alive\r\n\r\n");
                        sseStream.Write(reqBytes, 0, reqBytes.Length);
                        sseStream.Flush();

                        string hello = ReadSseUntilEvent(sseStream, "connected", 3000);
                        AssertTrue(hello.Contains("event: connected"), "SSE connected handshake received");

                        // Pass capKey absent from loaded index so PerformSelectiveImportResult fails with ErrorMessage inside background job
                        RawHttpTestResponse impStart = SendRawTcpHttp(host.Port, null, "POST", "/api/backup/import", apiHdr,
                            "{\"capKeys\":[\"Rule:NonExistentCap\"],\"cids\":[],\"closeIdeIfRunning\":false}");
                        AssertEqual(202, impStart.StatusCode, "Background import job accepted (202 Accepted)");

                        string doneText = ReadSseUntilEvent(sseStream, "job_done", 5000);
                        AssertTrue(doneText.Contains("event: job_done"), "P1-10: Received SSE job_done event");

                        int evIdx = doneText.LastIndexOf("event: job_done", StringComparison.Ordinal);
                        int dataIdx = doneText.IndexOf("data:", evIdx, StringComparison.Ordinal);
                        int lineEnd = doneText.IndexOf('\n', dataIdx);
                        string jsonLine = (lineEnd > dataIdx ? doneText.Substring(dataIdx + 5, lineEnd - dataIdx - 5) : doneText.Substring(dataIdx + 5)).Trim();
                        IDictionary<string, object> envObj = UnifiedJson.Parse(jsonLine) as IDictionary<string, object>;
                        IDictionary<string, object> payloadObj = (envObj != null && envObj.ContainsKey("payload")) ? UnifiedJson.AsDict(envObj["payload"]) : envObj;
                        AssertTrue(payloadObj != null && !UnifiedJson.GetBool(payloadObj, "ok", true), "P1-10: job_done ok == false on error");
                        AssertTrue(!string.IsNullOrEmpty(UnifiedJson.GetString(payloadObj, "errorMessage", "")), "P1-10: job_done includes errorMessage");
                    }
                    host.Stop();
                }

                // P2-2: Verify SseHub ClientQueueState bounded capacity (128) drops oldest on overflow
                SseHub.ClientQueueState qState = new SseHub.ClientQueueState(null, null);
                for (int i = 0; i < SseHub.MaxClientQueueCapacity + 15; i++)
                {
                    qState.EnqueueBounded(Encoding.UTF8.GetBytes("event: ping\ndata: " + i + "\n\n"));
                }
                List<byte[]> drained = qState.DrainPending();
                AssertEqual(SseHub.MaxClientQueueCapacity, drained.Count, "P2-2: SSE queue stays bounded at 128 frames");
                AssertEqual(15L, qState.DroppedFrames, "P2-2: DroppedFrames tracks 15 evicted oldest frames");
                AssertTrue(Encoding.UTF8.GetString(drained[0]).Contains("data: 15"), "P2-2: Oldest 0..14 frames dropped, frame 15 retained first");
                qState.CloseQuietly();
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T55_P1_11_BusyJob409DoesNotKillIde()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t55_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                Directory.CreateDirectory(geminiDir);

                string token = "t55_tok_busy";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, Path.Combine(sb, "Repo"), 0, token))
                {
                    host.Start();
                    AssertTrue(host.TryAcquireJob("simulated_job", "Busy test"), "Acquired active job lock");
                    try
                    {
                        // Even with closeIdeIfRunning=true & forceKill=true, busy job returns 409 immediately BEFORE EnsureIdeClosedIfNeeded
                        RawHttpTestResponse busyFull = SendRawTcpHttp(host.Port, null, "POST", "/api/backup/full", apiHdr,
                            "{\"outputPath\":\"" + UnifiedJson.EscapeString(Path.Combine(sb, "out.agy")) + "\",\"closeIdeIfRunning\":true,\"forceKill\":true}");
                        AssertEqual(409, busyFull.StatusCode, "P1-11: Busy job returns HTTP 409 immediately without attempting IDE kill");

                        RawHttpTestResponse busyDel = SendRawTcpHttp(host.Port, null, "POST", "/api/sessions/delete", apiHdr,
                            "{\"cids\":[\"c5500001-0000-4000-8000-000000000001\"],\"closeIdeIfRunning\":true,\"forceKill\":true}");
                        AssertEqual(409, busyDel.StatusCode, "P1-11: Write endpoint checks TryAcquireJob before IDE close");
                    }
                    finally
                    {
                        host.ReleaseJob();
                    }
                    host.Stop();
                }

                // P2-2: Verify SseHub ClientQueueState bounded capacity (128) drops oldest on overflow
                SseHub.ClientQueueState qState = new SseHub.ClientQueueState(null, null);
                for (int i = 0; i < SseHub.MaxClientQueueCapacity + 15; i++)
                {
                    qState.EnqueueBounded(Encoding.UTF8.GetBytes("event: ping\ndata: " + i + "\n\n"));
                }
                List<byte[]> drained = qState.DrainPending();
                AssertEqual(SseHub.MaxClientQueueCapacity, drained.Count, "P2-2: SSE queue stays bounded at 128 frames");
                AssertEqual(15L, qState.DroppedFrames, "P2-2: DroppedFrames tracks 15 evicted oldest frames");
                AssertTrue(Encoding.UTF8.GetString(drained[0]).Contains("data: 15"), "P2-2: Oldest 0..14 frames dropped, frame 15 retained first");
                qState.CloseQuietly();
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T56_P2_1_PortScopedCookieIsolation()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t56_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                Directory.CreateDirectory(geminiDir);

                string tokA = "token_host_A_1111";
                string tokB = "token_host_B_2222";
                using (LocalWebHost hostA = new LocalWebHost(homeDir, geminiDir, Path.Combine(sb, "RepoA"), 0, tokA))
                using (LocalWebHost hostB = new LocalWebHost(homeDir, geminiDir, Path.Combine(sb, "RepoB"), 0, tokB))
                {
                    hostA.Start();
                    hostB.Start();

                    RawHttpTestResponse bootA = SendRawTcpHttp(hostA.Port, null, "GET", "/?t=" + tokA, null, null);
                    RawHttpTestResponse bootB = SendRawTcpHttp(hostB.Port, null, "GET", "/?t=" + tokB, null, null);
                    AssertTrue(bootA.SetCookie.Contains("agy_sid_" + hostA.Port + "=" + tokA), "Host A sets port-scoped cookie");
                    AssertTrue(bootB.SetCookie.Contains("agy_sid_" + hostB.Port + "=" + tokB), "Host B sets distinct port-scoped cookie");

                    // Browser shares cookies across localhost ports in one Cookie header -> both hosts extract only their own port cookie
                    string combinedCookie = "agy_sid_" + hostA.Port + "=" + tokA + "; agy_sid_" + hostB.Port + "=" + tokB;
                    RawHttpTestResponse sessA = SendRawTcpHttp(hostA.Port, null, "GET", "/api/auth/session",
                        new Dictionary<string, string> { { "Cookie", combinedCookie } }, null);
                    RawHttpTestResponse sessB = SendRawTcpHttp(hostB.Port, null, "GET", "/api/auth/session",
                        new Dictionary<string, string> { { "Cookie", combinedCookie } }, null);
                    AssertEqual(200, sessA.StatusCode, "Host A accepts combined cookie header");
                    AssertEqual(200, sessB.StatusCode, "Host B accepts combined cookie header");
                    AssertTrue(sessA.BodyText.Contains(tokA) && !sessA.BodyText.Contains(tokB), "Host A resolves tokA");
                    AssertTrue(sessB.BodyText.Contains(tokB) && !sessB.BodyText.Contains(tokA), "Host B resolves tokB");

                    // Host A cookie alone is rejected by Host B
                    RawHttpTestResponse crossHost = SendRawTcpHttp(hostB.Port, null, "GET", "/api/overview",
                        new Dictionary<string, string> { { "Cookie", "agy_sid_" + hostA.Port + "=" + tokA } }, null);
                    AssertEqual(403, crossHost.StatusCode, "P2-1: Host A cookie is rejected by Host B (403 Forbidden)");

                    hostA.Stop();
                    hostB.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T57_P2_5_AsciiStatusLineAndExplorerPathTraversalRejected()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t57_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                Directory.CreateDirectory(geminiDir);

                string token = "t57_tok_sec";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, Path.Combine(sb, "Repo"), 0, token))
                {
                    host.Start();

                    // Check strict ASCII status reason phrases
                    RawHttpTestResponse err400 = SendRawTcpHttp(host.Port, null, "POST", "/api/capabilities/relocate", apiHdr,
                        "{\"key\":\"McpServer:x\",\"oldPath\":\"\",\"newPath\":\"y\"}");
                    AssertEqual(400, err400.StatusCode, "Invalid relocate returns 400");
                    AssertEqual("HTTP/1.1 400 Bad Request", err400.StatusLine, "P2-5: ASCII status line 400 Bad Request");

                    RawHttpTestResponse trav1 = SendRawTcpHttp(host.Port, null, "POST", "/api/native/open-in-explorer", apiHdr,
                        "{\"path\":\"..\\\\..\\\\Windows\\\\System32\\\\cmd.exe\"}");
                    AssertTrue(trav1.StatusCode == 400 || trav1.StatusCode == 403, "P2-5: Relative path traversal rejected (400/403)");
                    AssertTrue(trav1.BodyText.Contains("\"ok\":false"), "P2-5: Rejection body has ok:false");

                    RawHttpTestResponse outsideRoot = SendRawTcpHttp(host.Port, null, "POST", "/api/native/open-in-explorer", apiHdr,
                        "{\"path\":\"C:\\\\Windows\\\\System32\\\\notepad.exe\"}");
                    AssertTrue(outsideRoot.StatusCode == 400 || outsideRoot.StatusCode == 403, "P2-5: Out-of-root system path rejected by explorer endpoint");

                    // Verify directory trailing backslash does not produce \" quote-escape in explorer arguments, and file uses /select,
                    string testFile = Path.Combine(geminiDir, "probe_target.txt");
                    File.WriteAllText(testFile, "ok", Encoding.UTF8);
                    string canOut, expArgs, expErr;
                    int expCode;
                    AssertTrue(ApiRouter.TryValidateExplorerTarget(geminiDir + "\\", new string[] { homeDir }, out canOut, out expArgs, out expCode, out expErr),
                        "Allowed directory with trailing backslash validates");
                    AssertTrue(!expArgs.EndsWith("\\\""), "P2-5: Directory explorer args strip trailing backslash before closing quote");
                    AssertTrue(ApiRouter.TryValidateExplorerTarget(testFile, new string[] { homeDir }, out canOut, out expArgs, out expCode, out expErr),
                        "Allowed file validates for explorer /select");
                    AssertTrue(expArgs.StartsWith("/select,\"", StringComparison.Ordinal) && expArgs.EndsWith("\"", StringComparison.Ordinal),
                        "P2-5: File explorer args properly quote /select, target");

                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T58_Stage2_ConfigJsonUserSettingsPreserveUnknownKeysAndEnums()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t58_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string cfgDir = Path.Combine(geminiDir, "config");
                Directory.CreateDirectory(cfgDir);

                string configPath = Path.Combine(cfgDir, "config.json");
                string origJson =
                    "{\r\n" +
                    "  // Preserve header JSONC comment\r\n" +
                    "  \"userSettings\": {\r\n" +
                    "    \"autoExecutionPolicy\": \"CASCADE_COMMANDS_AUTO_EXECUTION_OFF\",\r\n" +
                    "    \"artifactReviewMode\": \"ARTIFACT_REVIEW_MODE_EXPERIMENTAL_CUSTOM\",\r\n" +
                    "    \"enableTerminalSandbox\": false,\r\n" +
                    "    \"customThemeSeedsLight\": { \"primary\": \"#CB4B16\", \"background\": \"#FAF4E5\" },\r\n" +
                    "    \"voiceName\": \"Fola\",\r\n" +
                    "    \"orchestratorExecutorModel\": \"MODEL_PLACEHOLDER_M526\"\r\n" +
                    "  },\r\n" +
                    "  \"sidecars\": {\r\n" +
                    "    \"swarm/swarm_board\": { \"projectId\": \"p-58\", \"schemaVersion\": 1 }\r\n" +
                    "  }\r\n" +
                    "}\r\n";
                File.WriteAllText(configPath, origJson, Encoding.UTF8);

                Dictionary<string, object> updates = new Dictionary<string, object>();
                updates["autoExecutionPolicy"] = "CASCADE_COMMANDS_AUTO_EXECUTION_EAGER";
                updates["enableTerminalSandbox"] = true;
                updates["themeMode"] = "THEME_MODE_DARK";

                string bakPath;
                string err;
                bool ok = AntigravityConfigService.PatchUserSettings(geminiDir, updates, out bakPath, out err);
                AssertTrue(ok, "T58: PatchUserSettings succeeded: " + err);
                AssertTrue(!string.IsNullOrEmpty(bakPath) && File.Exists(bakPath), "T58: Created timestamped backup file");

                string[] bakFiles = Directory.GetFiles(cfgDir, "config.json.bak.*", SearchOption.TopDirectoryOnly);
                AssertEqual(1, bakFiles.Length, "T58: Exactly 1 timestamped config.json.bak.* created per batch");

                string afterText = File.ReadAllText(configPath, Encoding.UTF8);
                AssertTrue(afterText.Contains("// Preserve header JSONC comment"), "T58: JSONC comment preserved");

                CockpitSnapshot snap = AntigravityConfigService.ScanCockpit(geminiDir, homeDir, null);
                AssertEqual("CASCADE_COMMANDS_AUTO_EXECUTION_EAGER", snap.UserSettings.AutoExecutionPolicy, "T58: autoExecutionPolicy updated");
                AssertEqual("ARTIFACT_REVIEW_MODE_EXPERIMENTAL_CUSTOM", snap.UserSettings.ArtifactReviewMode, "T58: Unrecognized enum preserved untouched");
                AssertTrue(snap.UserSettings.EnableTerminalSandbox, "T58: enableTerminalSandbox updated to true");
                AssertEqual("THEME_MODE_DARK", snap.UserSettings.ThemeMode, "T58: themeMode inserted");
                AssertEqual("Fola", UnifiedJson.GetString(snap.UserSettings.RawSettings, "voiceName", ""), "T58: Unknown string key voiceName preserved");
                AssertEqual("MODEL_PLACEHOLDER_M526", UnifiedJson.GetString(snap.UserSettings.RawSettings, "orchestratorExecutorModel", ""), "T58: Unknown key orchestratorExecutorModel preserved");
                Dictionary<string, object> themeObj = UnifiedJson.AsDict(snap.UserSettings.RawSettings["customThemeSeedsLight"]);
                AssertTrue(themeObj != null && UnifiedJson.GetString(themeObj, "primary", "") == "#CB4B16", "T58: Unknown nested object customThemeSeedsLight preserved");
                AssertEqual(1, snap.Sidecars.Count, "T58: Sidecars section visible after settings patch");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T59_Stage2_PluginMcpHookToggleRoundTripAndBackups()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t59_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string cfgDir = Path.Combine(geminiDir, "config");
                Directory.CreateDirectory(cfgDir);

                string cfgPath = Path.Combine(cfgDir, "config.json");
                File.WriteAllText(cfgPath,
                    "{\r\n  // Config comment\r\n  \"plugins\": {\r\n    \"gdocs\": {\r\n      \"enabled\": false,\r\n      \"installedFrom\": { \"version\": \"0.0.9\" }\r\n    }\r\n  }\r\n}\r\n",
                    Encoding.UTF8);

                string mcpPath = Path.Combine(cfgDir, "mcp_config.json");
                File.WriteAllText(mcpPath,
                    "{\r\n  // MCP comment\r\n  \"mcpServers\": {\r\n    \"srv-59\": { \"command\": \"node\", \"args\": [\"a.js\"], \"disabled\": false }\r\n  }\r\n}\r\n",
                    Encoding.UTF8);

                string hooksPath = Path.Combine(cfgDir, "hooks.json");
                File.WriteAllText(hooksPath,
                    "{\r\n  \"guard-hook\": {\r\n    \"enabled\": true,\r\n    \"PreToolUse\": [\r\n      { \"matcher\": \"Edit|Write\", \"hooks\": [ { \"type\": \"command\", \"command\": \"node check.js\", \"timeout\": 5 } ] }\r\n    ]\r\n  }\r\n}\r\n",
                    Encoding.UTF8);

                bool resEnabled;
                string targetFile, bakPath, err;
                int httpCode;

                // 1. Plugin toggle (config.json-only gdocs: false -> true -> false)
                AssertTrue(AntigravityConfigService.ToggleCapability(geminiDir, "plugin", "gdocs", true, null, out resEnabled, out targetFile, out bakPath, out httpCode, out err),
                    "T59: Enable config-only plugin gdocs: " + err);
                AssertTrue(resEnabled, "T59: gdocs enabled == true");
                CockpitSnapshot s1 = AntigravityConfigService.ScanCockpit(geminiDir, homeDir, null);
                ScannedPluginInfo gdocs1 = s1.Plugins.Find(delegate(ScannedPluginInfo p) { return p.Id == "gdocs"; });
                AssertTrue(gdocs1 != null && gdocs1.Enabled && !gdocs1.HasFolder && gdocs1.InConfigJson, "T59: gdocs reflected as enabled without folder");
                AssertTrue(File.ReadAllText(cfgPath, Encoding.UTF8).Contains("// Config comment"), "T59: config.json comment preserved");

                AssertTrue(AntigravityConfigService.ToggleCapability(geminiDir, "plugin", "gdocs", false, null, out resEnabled, out targetFile, out bakPath, out httpCode, out err),
                    "T59: Disable gdocs roundtrip");
                AssertTrue(!resEnabled, "T59: gdocs enabled == false");

                // 2. MCP toggle (disabled: false -> true -> false)
                AssertTrue(AntigravityConfigService.ToggleCapability(geminiDir, "mcp", "srv-59", false, null, out resEnabled, out targetFile, out bakPath, out httpCode, out err),
                    "T59: Disable MCP srv-59: " + err);
                AssertTrue(!resEnabled, "T59: srv-59 enabled == false");
                CockpitSnapshot s2 = AntigravityConfigService.ScanCockpit(geminiDir, homeDir, null);
                ScannedMcpServerInfo mcp2 = s2.McpServers.Find(delegate(ScannedMcpServerInfo m) { return m.Id == "srv-59"; });
                AssertTrue(mcp2 != null && mcp2.Disabled && !mcp2.Enabled, "T59: mcpServers.srv-59.disabled == true");
                AssertTrue(File.ReadAllText(mcpPath, Encoding.UTF8).Contains("// MCP comment"), "T59: mcp_config.json comment preserved");

                AssertTrue(AntigravityConfigService.ToggleCapability(geminiDir, "mcp", "srv-59", true, null, out resEnabled, out targetFile, out bakPath, out httpCode, out err),
                    "T59: Re-enable MCP srv-59");
                AssertTrue(resEnabled, "T59: srv-59 re-enabled");

                // 3. Nested Hook toggle (enabled: true -> false -> true)
                AssertTrue(AntigravityConfigService.ToggleCapability(geminiDir, "hook", "guard-hook", false, null, out resEnabled, out targetFile, out bakPath, out httpCode, out err),
                    "T59: Disable nested hook guard-hook: " + err);
                AssertTrue(!resEnabled, "T59: guard-hook enabled == false");
                CockpitSnapshot s3 = AntigravityConfigService.ScanCockpit(geminiDir, homeDir, null);
                ScannedHookInfo hk3 = s3.Hooks.Find(delegate(ScannedHookInfo h) { return h.Id == "guard-hook"; });
                AssertTrue(hk3 != null && !hk3.Enabled && hk3.Events.Count == 1 && hk3.Events[0].Matcher == "Edit|Write",
                    "T59: Nested hook disabled while preserving PreToolUse matcher & hooks[]");

                AssertTrue(AntigravityConfigService.ToggleCapability(geminiDir, "hook", "guard-hook", true, null, out resEnabled, out targetFile, out bakPath, out httpCode, out err),
                    "T59: Re-enable nested hook guard-hook");
                AssertTrue(resEnabled, "T59: guard-hook re-enabled");

                // 4. Plugin-scoped root hooks.json (without named group) + wrapper hooks.json with >2 root keys + plugins.json-only plugin toggle
                string plugHookDir = Path.Combine(cfgDir, "plugins", "root-hook-plug");
                Directory.CreateDirectory(plugHookDir);
                string plugHookPath = Path.Combine(plugHookDir, "hooks.json");
                File.WriteAllText(plugHookPath, "{\"PreToolUse\":[{\"matcher\":\"Bash\",\"hooks\":[{\"type\":\"command\",\"command\":\"echo hi\"}]}]}", Encoding.UTF8);
                CockpitSnapshot s4 = AntigravityConfigService.ScanCockpit(geminiDir, homeDir, null);
                ScannedHookInfo phk = s4.Hooks.Find(delegate(ScannedHookInfo h) { return h.SourcePath == plugHookPath; });
                AssertTrue(phk != null && phk.Id == "root-hook-plug" && phk.Enabled, "T59: Plugin root hooks.json scanned with pluginId");
                AssertTrue(AntigravityConfigService.ToggleCapability(geminiDir, "hook", "root-hook-plug", false, plugHookPath, out resEnabled, out targetFile, out bakPath, out httpCode, out err),
                    "T59: Toggle plugin root hooks.json succeeded: " + err);
                AssertTrue(!resEnabled, "T59: Plugin root hooks.json toggled to false");

                string wrapHookPath = Path.Combine(cfgDir, "wrap_hooks.json");
                File.WriteAllText(wrapHookPath, "{\"description\":\"3 keys\",\"enabled\":true,\"hooks\":{\"enabled\":true,\"PreToolUse\":[{\"matcher\":\"Read\",\"hooks\":[{\"type\":\"command\",\"command\":\"echo r\"}]}]}}", Encoding.UTF8);
                AssertTrue(AntigravityConfigService.ToggleCapability(geminiDir, "hook", "_root", false, wrapHookPath, out resEnabled, out targetFile, out bakPath, out httpCode, out err),
                    "T59: Toggle wrapper hooks.json (>2 root keys) succeeded: " + err);
                AssertTrue(!resEnabled, "T59: Wrapper hooks.json toggled to false");

                File.WriteAllText(Path.Combine(cfgDir, "plugins.json"), "{\"entries\":{\"manifest-only-plug\":{\"enabled\":true}}}", Encoding.UTF8);
                AssertTrue(AntigravityConfigService.ToggleCapability(geminiDir, "plugin", "manifest-only-plug", false, null, out resEnabled, out targetFile, out bakPath, out httpCode, out err),
                    "T59: Toggle plugins.json-only plugin succeeded: " + err);
                AssertTrue(!resEnabled, "T59: manifest-only-plug toggled to false");

                AssertTrue(Directory.GetFiles(cfgDir, "config.json.bak.*").Length >= 1, "T59: config.json .bak.* created");
                AssertTrue(Directory.GetFiles(cfgDir, "mcp_config.json.bak.*").Length >= 1, "T59: mcp_config.json .bak.* created");
                AssertTrue(Directory.GetFiles(cfgDir, "hooks.json.bak.*").Length >= 1, "T59: hooks.json .bak.* created");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T60_Stage2_FullOfficialScannerManifestsHooksWorkspaceAndMcpConflicts()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t60_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string cfgDir = Path.Combine(geminiDir, "config");
                string projDir = Path.Combine(cfgDir, "projects");
                string wsRoot = Path.Combine(sb, "MyWorkspace");
                Directory.CreateDirectory(cfgDir);
                Directory.CreateDirectory(projDir);
                Directory.CreateDirectory(Path.Combine(geminiDir, "antigravity-cli"));
                Directory.CreateDirectory(Path.Combine(cfgDir, "rules"));
                Directory.CreateDirectory(Path.Combine(cfgDir, "skills", "global-skill"));
                Directory.CreateDirectory(Path.Combine(cfgDir, "skills", "blocked-skill"));
                Directory.CreateDirectory(Path.Combine(cfgDir, "plugins", "my-plug", "rules"));
                Directory.CreateDirectory(Path.Combine(cfgDir, "plugins", "my-plug", "skills", "plug-skill"));
                Directory.CreateDirectory(Path.Combine(cfgDir, "sidecars"));
                Directory.CreateDirectory(Path.Combine(wsRoot, ".agents", "rules"));
                Directory.CreateDirectory(Path.Combine(wsRoot, ".agents", "skills", "ws-skill"));

                // config.json with config-only plugin + sidecars
                File.WriteAllText(Path.Combine(cfgDir, "config.json"),
                    "{\"plugins\":{\"modern-web-guidance-plugin\":{\"enabled\":false}},\"sidecars\":{\"board\":{\"projectId\":\"p60\",\"schemaVersion\":1}},\"userSettings\":{\"themeMode\":\"THEME_MODE_DARK\"}}",
                    Encoding.UTF8);

                // skills.json & plugins.json
                File.WriteAllText(Path.Combine(cfgDir, "skills.json"),
                    "{\"inherits\":true,\"entries\":{\"global-skill\":{\"enabled\":true},\"manifest-only-skill\":{\"enabled\":true}},\"exclude\":[\"blocked-skill\"]}",
                    Encoding.UTF8);
                File.WriteAllText(Path.Combine(cfgDir, "plugins.json"),
                    "{\"include_only\":[\"my-plug\",\"modern-web-guidance-plugin\"]}",
                    Encoding.UTF8);

                // Global rules & skills
                File.WriteAllText(Path.Combine(geminiDir, "GEMINI.md"), "# Global Root Rule\n", Encoding.UTF8);
                File.WriteAllText(Path.Combine(cfgDir, "rules", "g-extra.md"), "# Extra Rule\n", Encoding.UTF8);
                File.WriteAllText(Path.Combine(cfgDir, "skills", "global-skill", "SKILL.md"), "---\nname: global-skill\ndescription: Global skill desc\n---\nBody", Encoding.UTF8);
                File.WriteAllText(Path.Combine(cfgDir, "skills", "blocked-skill", "SKILL.md"), "---\nname: blocked-skill\n---\nBlocked", Encoding.UTF8);

                // Plugin internals
                File.WriteAllText(Path.Combine(cfgDir, "plugins", "my-plug", "plugin.json"),
                    "{\"name\":\"my-plug\",\"displayName\":\"My Plugin\",\"version\":\"1.2.0\",\"description\":\"Desc\"}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(cfgDir, "plugins", "my-plug", "rules", "p.md"), "# Plugin Rule\n", Encoding.UTF8);
                File.WriteAllText(Path.Combine(cfgDir, "plugins", "my-plug", "skills", "plug-skill", "SKILL.md"), "# Plug Skill\n", Encoding.UTF8);
                File.WriteAllText(Path.Combine(cfgDir, "plugins", "my-plug", "mcp_config.json"),
                    "{\"mcpServers\":{\"shared-mcp\":{\"command\":\"node\",\"args\":[\"p.js\"]}}}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(cfgDir, "plugins", "my-plug", "hooks.json"),
                    "{\"plug-hook\":{\"PreInvocation\":[{\"type\":\"command\",\"command\":\"echo pre\",\"timeout\":3}],\"Stop\":[{\"type\":\"command\",\"command\":\"echo stop\"}]}}",
                    Encoding.UTF8);

                // Global hooks (nested PreToolUse matcher+hooks[] + flat SessionStart)
                File.WriteAllText(Path.Combine(cfgDir, "hooks.json"),
                    "{\"enabled\":true,\"PreToolUse\":[{\"matcher\":\"Bash\",\"hooks\":[{\"type\":\"command\",\"command\":\"node bash_guard.js\"}]}],\"SessionStart\":[{\"type\":\"command\",\"command\":\"node init.js\"}]}",
                    Encoding.UTF8);

                // Global mcp_config.json + CLI settings.json (duplicate MCP ID "shared-mcp") + antigravity-cli/settings.json
                File.WriteAllText(Path.Combine(cfgDir, "mcp_config.json"),
                    "{\"mcpServers\":{\"shared-mcp\":{\"url\":\"http://127.0.0.1:9999/mcp\"}}}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(geminiDir, "settings.json"),
                    "{\"mcpServers\":{\"shared-mcp\":{\"command\":\"shared-mcp\",\"args\":[\"--mcp\"]}}}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(geminiDir, "antigravity-cli", "settings.json"),
                    "{\"colorScheme\":\"dark\",\"trustedWorkspaces\":[\"" + UnifiedJson.EscapeString(wsRoot) + "\"]}", Encoding.UTF8);

                // Registered project + workspace .agents/ AND .agent/ + AGENTS.md + GEMINI.md + manifests + hooks
                Directory.CreateDirectory(Path.Combine(wsRoot, ".agent", "rules"));
                File.WriteAllText(Path.Combine(projDir, "proj-60.json"),
                    WorkspaceService.GenerateStandardProjectJson("proj-60", "MyWorkspace", wsRoot), Encoding.UTF8);
                File.WriteAllText(Path.Combine(wsRoot, "GEMINI.md"), "# WS GEMINI\n", Encoding.UTF8);
                File.WriteAllText(Path.Combine(wsRoot, "AGENTS.md"), "# WS AGENTS\n", Encoding.UTF8);
                File.WriteAllText(Path.Combine(wsRoot, ".agents", "rules", "ws.md"), "# WS Rule\n", Encoding.UTF8);
                File.WriteAllText(Path.Combine(wsRoot, ".agent", "rules", "legacy.md"), "# WS Legacy Rule\n", Encoding.UTF8);
                File.WriteAllText(Path.Combine(wsRoot, ".agents", "skills", "ws-skill", "SKILL.md"), "# WS Skill\n", Encoding.UTF8);
                File.WriteAllText(Path.Combine(wsRoot, "skills.json"), "{\"inherits\":false,\"entries\":{\"ws-skill\":{\"enabled\":true}}}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(wsRoot, "hooks.json"), "{\"description\":\"ws\",\"enabled\":true,\"hooks\":{\"PostToolUse\":[{\"matcher\":\"Write\",\"hooks\":[{\"type\":\"command\",\"command\":\"echo ws\"}]}]}}", Encoding.UTF8);

                CockpitSnapshot snap = AntigravityConfigService.ScanCockpit(geminiDir, homeDir, null);

                // Assertions
                AssertTrue(snap.Plugins.Exists(delegate(ScannedPluginInfo p) { return p.Id == "modern-web-guidance-plugin" && !p.HasFolder && !p.Enabled; }),
                    "T60: config.json-only plugin discovered");
                AssertTrue(snap.Plugins.Exists(delegate(ScannedPluginInfo p) { return p.Id == "my-plug" && p.HasFolder && p.DisplayName == "My Plugin" && p.Enabled; }),
                    "T60: Folder plugin with plugin.json metadata discovered");
                AssertTrue(snap.Skills.Exists(delegate(ScannedSkillInfo s) { return s.Id == "blocked-skill" && !s.Enabled && s.ManifestStatus == "excluded"; }),
                    "T60: skills.json exclude applied to blocked-skill");
                AssertTrue(snap.Skills.Exists(delegate(ScannedSkillInfo s) { return s.Id == "manifest-only-skill" && s.Enabled; }),
                    "T60: skills.json entries discovered");
                AssertTrue(snap.Skills.Exists(delegate(ScannedSkillInfo s) { return s.Id == "ws-skill" && s.Scope == "project:MyWorkspace"; }),
                    "T60: Workspace .agents/skills/ws-skill discovered");
                AssertTrue(snap.Rules.Exists(delegate(ScannedRuleInfo r) { return r.SourcePath.EndsWith("AGENTS.md", StringComparison.OrdinalIgnoreCase); }),
                    "T60: Workspace AGENTS.md discovered");
                AssertTrue(snap.Rules.Exists(delegate(ScannedRuleInfo r) { return r.SourcePath.EndsWith("legacy.md", StringComparison.OrdinalIgnoreCase); }),
                    "T60: Workspace .agent/rules/legacy.md discovered alongside .agents/");
                AssertTrue(snap.Hooks.Exists(delegate(ScannedHookInfo h) { return h.Scope == "global" && h.Events.Count == 2; }),
                    "T60: Nested PreToolUse + flat SessionStart hooks parsed");
                AssertTrue(snap.Hooks.Exists(delegate(ScannedHookInfo h) { return h.Scope == "project:MyWorkspace" && h.Events.Count == 1 && h.Events[0].EventName == "PostToolUse"; }),
                    "T60: Workspace hooks.json wrapper with >2 root keys parsed");
                AssertTrue(snap.Hooks.Exists(delegate(ScannedHookInfo h) { return h.Id == "plug-hook" && h.Events.Count == 2; }),
                    "T60: Plugin PreInvocation + Stop hooks parsed");
                AssertTrue(snap.CliSettings.ContainsKey("settingsJson") && snap.CliSettings.ContainsKey("antigravityCliSettingsJson"),
                    "T60: Both CLI settings files loaded");
                AssertTrue(snap.Workspaces.Count == 1 && snap.Workspaces[0].HasAgentsDir && snap.Workspaces[0].HasAgentsMd && snap.Workspaces[0].HasGeminiMd && snap.Workspaces[0].HasHooksJson,
                    "T60: Workspace .agents/, AGENTS.md, GEMINI.md, hooks.json detected");

                McpConflictInfo conflict = snap.McpConflicts.Find(delegate(McpConflictInfo c) { return c.Id == "shared-mcp"; });
                AssertTrue(conflict != null && conflict.Count == 3, "T60: Duplicate MCP server ID 'shared-mcp' flagged across global + plugin + CLI");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T61_Stage2_McpHealthDoctorStdioMockMissingAndTimeoutCleanup()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t61_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string cfgDir = Path.Combine(geminiDir, "config");
                Directory.CreateDirectory(cfgDir);

                string healthyScript = Path.Combine(sb, "healthy_mcp.ps1");
                File.WriteAllText(healthyScript,
                    "$line = [Console]::In.ReadLine()\r\n" +
                    "if ($line -and $line.Contains('initialize')) {\r\n" +
                    "  [Console]::Out.WriteLine('{\"jsonrpc\":\"2.0\",\"method\":\"notifications/message\",\"params\":{\"level\":\"info\",\"data\":\"booting\"}}')\r\n" +
                    "  [Console]::Out.WriteLine('{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"serverInfo\":{\"name\":\"mock-doctor-mcp\",\"version\":\"2.1.0\"}}}')\r\n" +
                    "  [Console]::Out.Flush()\r\n" +
                    "  Start-Sleep -Seconds 10\r\n" +
                    "}\r\n",
                    Encoding.UTF8);

                string pidFile = Path.Combine(sb, "hang.pid");
                string hangScript = Path.Combine(sb, "hang_mcp.ps1");
                File.WriteAllText(hangScript,
                    "[System.IO.File]::WriteAllText('" + pidFile.Replace("'", "''") + "', $PID.ToString())\r\n" +
                    "Start-Sleep -Seconds 30\r\n",
                    Encoding.UTF8);

                // Extensionless POSIX shim + .cmd wrapper in same directory (Windows must prefer .cmd over extensionless shim!)
                string shimDir = Path.Combine(sb, "shimbin");
                Directory.CreateDirectory(shimDir);
                File.WriteAllText(Path.Combine(shimDir, "mockshim"), "#!/bin/sh\necho posix\n", Encoding.UTF8);
                File.WriteAllText(Path.Combine(shimDir, "mockshim.cmd"),
                    "@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" + healthyScript + "\"\r\n",
                    Encoding.UTF8);

                // Disabled plugin MCP should be skipped when includeDisabled == false
                string disPlugDir = Path.Combine(cfgDir, "plugins", "disabled-plug");
                Directory.CreateDirectory(disPlugDir);
                File.WriteAllText(Path.Combine(cfgDir, "config.json"),
                    "{\"plugins\":{\"disabled-plug\":{\"enabled\":false}}}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(disPlugDir, "mcp_config.json"),
                    "{\"mcpServers\":{\"disabled-plug-srv\":{\"command\":\"nonexistent_should_not_run\"}}}", Encoding.UTF8);

                string missingScript = Path.Combine(sb, "does_not_exist.ps1");

                // Spin up a local TcpListener to verify ProbeHttpMcpServer (200 healthy + 503 error)
                TcpListener httpListener = new TcpListener(IPAddress.Loopback, 0);
                httpListener.Start();
                int httpPort = ((IPEndPoint)httpListener.LocalEndpoint).Port;
                System.Threading.Thread httpThread = new System.Threading.Thread(delegate()
                {
                    for (int reqIdx = 0; reqIdx < 2; reqIdx++)
                    {
                        try
                        {
                            using (TcpClient c = httpListener.AcceptTcpClient())
                            using (NetworkStream ns = c.GetStream())
                            {
                                c.ReceiveTimeout = 2000;
                                byte[] buf = new byte[8192];
                                MemoryStream reqMs = new MemoryStream();
                                int headerEnd = -1;
                                int contentLen = 0;
                                while (reqMs.Length < 16384)
                                {
                                    int r = ns.Read(buf, 0, buf.Length);
                                    if (r <= 0) break;
                                    reqMs.Write(buf, 0, r);
                                    byte[] all = reqMs.ToArray();
                                    string soFar = Encoding.ASCII.GetString(all);
                                    headerEnd = soFar.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                                    if (headerEnd >= 0)
                                    {
                                        string[] lines = soFar.Substring(0, headerEnd).Split(new string[] { "\r\n" }, StringSplitOptions.None);
                                        for (int li = 1; li < lines.Length; li++)
                                        {
                                            if (lines[li].StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                                                int.TryParse(lines[li].Substring(15).Trim(), out contentLen);
                                        }
                                        if (all.Length >= headerEnd + 4 + contentLen) break;
                                    }
                                }
                                string reqTxt = Encoding.ASCII.GetString(reqMs.ToArray());
                                string body = reqTxt.Contains("/healthy")
                                    ? "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":\"2024-11-05\",\"serverInfo\":{\"name\":\"http-mock-mcp\",\"version\":\"3.0\"}}}"
                                    : "{\"error\":\"unavailable\"}";
                                string status = reqTxt.Contains("/healthy") ? "200 OK" : "503 Service Unavailable";
                                byte[] bBytes = Encoding.UTF8.GetBytes(body);
                                string hdr = "HTTP/1.1 " + status + "\r\nContent-Type: application/json\r\nContent-Length: " + bBytes.Length + "\r\nConnection: close\r\n\r\n";
                                byte[] hBytes = Encoding.ASCII.GetBytes(hdr);
                                ns.Write(hBytes, 0, hBytes.Length);
                                ns.Write(bBytes, 0, bBytes.Length);
                                ns.Flush();
                            }
                        }
                        catch { }
                    }
                });
                httpThread.IsBackground = true;
                httpThread.Start();

                string mcpJson =
                    "{\r\n" +
                    "  \"mcpServers\": {\r\n" +
                    "    \"healthy-mock\": { \"command\": \"powershell.exe\", \"args\": [\"-NoProfile\", \"-ExecutionPolicy\", \"Bypass\", \"-File\", \"" + UnifiedJson.EscapeString(healthyScript) + "\"] },\r\n" +
                    "    \"cmd-shim-mock\": { \"command\": \"" + UnifiedJson.EscapeString(Path.Combine(shimDir, "mockshim")) + "\", \"args\": [\"@scope/pkg-arg\"] },\r\n" +
                    "    \"missing-cmd\": { \"command\": \"nonexistent_mcp_cmd_xyz_61\", \"args\": [] },\r\n" +
                    "    \"missing-arg\": { \"command\": \"powershell.exe\", \"args\": [\"-File\", \"" + UnifiedJson.EscapeString(missingScript) + "\"] },\r\n" +
                    "    \"hanging-mock\": { \"command\": \"powershell.exe\", \"args\": [\"-NoProfile\", \"-ExecutionPolicy\", \"Bypass\", \"-File\", \"" + UnifiedJson.EscapeString(hangScript) + "\"] },\r\n" +
                    "    \"http-healthy\": { \"url\": \"http://127.0.0.1:" + httpPort + "/healthy\" },\r\n" +
                    "    \"http-503\": { \"url\": \"http://127.0.0.1:" + httpPort + "/fail\" },\r\n" +
                    "    \"http-bad-url\": { \"type\": \"http\", \"url\": \"ftp://invalid-scheme\" }\r\n" +
                    "  }\r\n" +
                    "}\r\n";
                File.WriteAllText(Path.Combine(cfgDir, "mcp_config.json"), mcpJson, Encoding.UTF8);

                List<McpHealthCheckResult> results = AntigravityConfigService.RunMcpHealthCheck(geminiDir, homeDir, null, null, false, 3000);
                httpListener.Stop();
                AssertEqual(9, results.Count, "T61: Doctor returned all 9 MCP entries (8 global + 1 disabled plugin skipped)");

                McpHealthCheckResult rHealthy = results.Find(delegate(McpHealthCheckResult r) { return r.Id == "healthy-mock"; });
                AssertTrue(rHealthy != null && rHealthy.Healthy && rHealthy.Status == "healthy", "T61: healthy-mock ignored notification & passed initialize handshake (" + (rHealthy != null ? rHealthy.Error : "null") + ")");
                AssertEqual("mock-doctor-mcp", rHealthy.ServerName, "T61: Captured serverInfo.name");
                AssertEqual("2.1.0", rHealthy.ServerVersion, "T61: Captured serverInfo.version");

                McpHealthCheckResult rCmdShim = results.Find(delegate(McpHealthCheckResult r) { return r.Id == "cmd-shim-mock"; });
                AssertTrue(rCmdShim != null && rCmdShim.Healthy && rCmdShim.Status == "healthy", "T61: Resolved .cmd over extensionless POSIX shim and allowed @scope/pkg-arg (" + (rCmdShim != null ? rCmdShim.Error : "null") + ")");

                McpHealthCheckResult rDisPlug = results.Find(delegate(McpHealthCheckResult r) { return r.Id == "disabled-plug-srv"; });
                AssertTrue(rDisPlug != null && rDisPlug.Disabled && rDisPlug.Status == "disabled", "T61: Disabled plugin MCP skipped cleanly");

                McpHealthCheckResult rHttpOk = results.Find(delegate(McpHealthCheckResult r) { return r.Id == "http-healthy"; });
                AssertTrue(rHttpOk != null && rHttpOk.Healthy && rHttpOk.Reachable && rHttpOk.HttpStatusCode == 200 && rHttpOk.ServerName == "http-mock-mcp",
                    "T61: Streamable HTTP 200 probe verified with serverInfo (" + (rHttpOk != null ? (rHttpOk.Status + ": " + rHttpOk.Error) : "null") + ")");

                McpHealthCheckResult rHttp503 = results.Find(delegate(McpHealthCheckResult r) { return r.Id == "http-503"; });
                AssertTrue(rHttp503 != null && !rHttp503.Healthy && rHttp503.Reachable && rHttp503.HttpStatusCode == 503,
                    "T61: Streamable HTTP 503 probe reported reachable=true, http_503");

                McpHealthCheckResult rHttpBad = results.Find(delegate(McpHealthCheckResult r) { return r.Id == "http-bad-url"; });
                AssertTrue(rHttpBad != null && !rHttpBad.Healthy && !rHttpBad.Reachable && rHttpBad.Status == "invalid_url",
                    "T61: Invalid HTTP URL scheme rejected as invalid_url");

                McpHealthCheckResult rMissCmd = results.Find(delegate(McpHealthCheckResult r) { return r.Id == "missing-cmd"; });
                AssertTrue(rMissCmd != null && !rMissCmd.Healthy && rMissCmd.Status == "missing_command", "T61: missing-cmd reported missing_command");

                McpHealthCheckResult rMissArg = results.Find(delegate(McpHealthCheckResult r) { return r.Id == "missing-arg"; });
                AssertTrue(rMissArg != null && !rMissArg.Healthy && rMissArg.Status == "missing_script", "T61: missing-arg reported missing_script");

                McpHealthCheckResult rHang = results.Find(delegate(McpHealthCheckResult r) { return r.Id == "hanging-mock"; });
                AssertTrue(rHang != null && !rHang.Healthy && rHang.Status == "timeout", "T61: hanging-mock timed out at 3s");

                // Confirm hanging child process was forcefully terminated on timeout
                if (File.Exists(pidFile))
                {
                    int hangPid;
                    if (int.TryParse(File.ReadAllText(pidFile).Trim(), out hangPid))
                    {
                        bool stillAlive = false;
                        try
                        {
                            using (Process p = Process.GetProcessById(hangPid))
                            {
                                stillAlive = !p.HasExited;
                            }
                        }
                        catch { stillAlive = false; }
                        AssertTrue(!stillAlive, "T61: Timed-out MCP child process terminated cleanly");
                    }
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T62_Stage2_InlineEditorStrictAllowlistSymlinkAndTokenBudget()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t62_" + Guid.NewGuid().ToString("N"));
            string linkPath = null;
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string cfgDir = Path.Combine(geminiDir, "config");
                string projDir = Path.Combine(cfgDir, "projects");
                string vaultDir = Path.Combine(sb, "Vault");
                string wsRoot = Path.Combine(sb, "Proj62");
                string outsideDir = Path.Combine(sb, "OutsideSecret");
                Directory.CreateDirectory(cfgDir);
                Directory.CreateDirectory(projDir);
                Directory.CreateDirectory(vaultDir);
                Directory.CreateDirectory(Path.Combine(wsRoot, ".agents", "rules"));
                Directory.CreateDirectory(outsideDir);

                File.WriteAllText(Path.Combine(projDir, "p62.json"),
                    WorkspaceService.GenerateStandardProjectJson("p62", "Proj62", wsRoot), Encoding.UTF8);

                string geminiMd = Path.Combine(geminiDir, "GEMINI.md");
                File.WriteAllText(geminiMd, "# Initial Global Rule\n", Encoding.UTF8);
                string wsRuleMd = Path.Combine(wsRoot, ".agents", "rules", "team.md");
                File.WriteAllText(wsRuleMd, "# Team Rule\n", Encoding.UTF8);

                // Sensitive files inside .gemini that MUST be rejected
                string oauthFile = Path.Combine(geminiDir, "oauth_creds.json");
                File.WriteAllText(oauthFile, "{\"secret\":\"nope\"}", Encoding.UTF8);
                string tokenFile = Path.Combine(cfgDir, "user_token.json");
                File.WriteAllText(tokenFile, "{\"token\":\"nope\"}", Encoding.UTF8);

                // Outside / non-allowed project files
                string outsideFile = Path.Combine(outsideDir, "outside.json");
                File.WriteAllText(outsideFile, "{\"a\":1}", Encoding.UTF8);
                string pkgJson = Path.Combine(wsRoot, "package.json");
                File.WriteAllText(pkgJson, "{\"name\":\"app\"}", Encoding.UTF8);

                // 1. Allowed reads & saves
                EditorFileResult r1 = AntigravityConfigService.ReadAllowedCustomizationFile(geminiDir, homeDir, vaultDir, geminiMd);
                AssertTrue(r1.Ok && r1.Content.Contains("Initial Global Rule"), "T62: Allowed reading GEMINI.md");

                // Default referenceTokenThreshold == 0 produces no red alarm; explicit threshold 1500 flags > 1500
                AssertEqual(100, AntigravityConfigService.EstimateTokenCount(new string('A', 400)), "T62: 400 ASCII chars -> 100 tokens");
                AssertEqual(100, AntigravityConfigService.EstimateTokenCount(new string('中', 150)), "T62: 150 CJK chars -> 100 tokens");

                string bigRule = new string('A', 6100); // ceil(6100 / 4.0) = 1525 > 1500
                EditorFileResult sDefault = AntigravityConfigService.SaveAllowedCustomizationFile(geminiDir, homeDir, vaultDir, wsRuleMd, bigRule);
                AssertTrue(sDefault.Ok && sDefault.EstimatedTokens == 1525 && !sDefault.OverBudget, "T62: Default threshold=0 does not flag OverBudget");
                EditorFileResult s1 = AntigravityConfigService.SaveAllowedCustomizationFile(geminiDir, homeDir, vaultDir, wsRuleMd, bigRule, 1500);
                AssertTrue(s1.Ok && s1.EstimatedTokens > 1500 && s1.OverBudget, "T62: Saved project .agents/ .md and flagged > 1500 tokens when threshold=1500");
                AssertTrue(File.Exists(s1.BackupPath), "T62: Created backup on editor save");

                CockpitSnapshot snapDefault = AntigravityConfigService.ScanCockpit(geminiDir, homeDir, vaultDir);
                AssertEqual(0, snapDefault.OverBudgetCount, "T62: Default ScanCockpit has 0 over-budget alerts");
                CockpitSnapshot snap = AntigravityConfigService.ScanCockpit(geminiDir, homeDir, vaultDir, 1500);
                AssertTrue(snap.OverBudgetCount >= 1 && snap.OverBudgetFiles.Exists(delegate(ScannedRuleInfo r) { return r.SourcePath == wsRuleMd && r.LineCount >= 1 && r.ByteCount == 6100; }),
                    "T62: Cockpit token budget audit flagged over-budget rule with LineCount and ByteCount");

                // 2. Rejections: .. traversal, oauth_creds.json, *token*.json, outside root, non-.agents project file, invalid JSON
                string travPath = geminiDir + "\\..\\OutsideSecret\\outside.json";
                EditorFileResult rTrav = AntigravityConfigService.ReadAllowedCustomizationFile(geminiDir, homeDir, vaultDir, travPath);
                AssertTrue(!rTrav.Ok && rTrav.HttpStatusCode == 400, "T62: Rejected .. path traversal");

                EditorFileResult rOauth = AntigravityConfigService.ReadAllowedCustomizationFile(geminiDir, homeDir, vaultDir, oauthFile);
                AssertTrue(!rOauth.Ok && rOauth.HttpStatusCode == 403, "T62: Rejected oauth_creds.json (403)");

                EditorFileResult rTok = AntigravityConfigService.ReadAllowedCustomizationFile(geminiDir, homeDir, vaultDir, tokenFile);
                AssertTrue(!rTok.Ok && rTok.HttpStatusCode == 403, "T62: Rejected *token*.json (403)");

                EditorFileResult rOut = AntigravityConfigService.ReadAllowedCustomizationFile(geminiDir, homeDir, vaultDir, outsideFile);
                AssertTrue(!rOut.Ok && rOut.HttpStatusCode == 403, "T62: Rejected outside-root file (403)");

                EditorFileResult rPkg = AntigravityConfigService.ReadAllowedCustomizationFile(geminiDir, homeDir, vaultDir, pkgJson);
                AssertTrue(!rPkg.Ok && rPkg.HttpStatusCode == 403, "T62: Rejected non-customization project package.json (403)");

                EditorFileResult sBadJson = AntigravityConfigService.SaveAllowedCustomizationFile(geminiDir, homeDir, vaultDir, Path.Combine(cfgDir, "config.json"), "{ invalid json");
                AssertTrue(!sBadJson.Ok && sBadJson.HttpStatusCode == 400, "T62: Invalid JSON rejected before save (400)");

                // 3. Symlink/junction escape check inside .gemini
                linkPath = Path.Combine(cfgDir, "escape_link");
                ProcessStartInfo mk = new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + linkPath + "\" \"" + outsideDir + "\"");
                mk.UseShellExecute = false;
                mk.CreateNoWindow = true;
                using (Process p = Process.Start(mk))
                {
                    p.WaitForExit(3000);
                }
                if (Directory.Exists(linkPath))
                {
                    EditorFileResult rLink = AntigravityConfigService.ReadAllowedCustomizationFile(geminiDir, homeDir, vaultDir, Path.Combine(linkPath, "outside.json"));
                    AssertTrue(!rLink.Ok && rLink.HttpStatusCode == 403, "T62: Rejected directory junction escape inside .gemini (403)");
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(linkPath) && Directory.Exists(linkPath))
                {
                    try { Directory.Delete(linkPath, false); } catch { }
                }
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T63_Stage2_CockpitHttpApiEndToEndAndWorkspaceLaunchValidation()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t63_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string cfgDir = Path.Combine(geminiDir, "config");
                string projDir = Path.Combine(cfgDir, "projects");
                string wsRoot = Path.Combine(sb, "Ws63");
                Directory.CreateDirectory(cfgDir);
                Directory.CreateDirectory(projDir);
                Directory.CreateDirectory(Path.Combine(wsRoot, ".agents", "rules"));

                File.WriteAllText(Path.Combine(wsRoot, "skills.json"), "{\"entries\":{}}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(wsRoot, ".agents", "rules", "r63.md"), "# Ws Rule\n", Encoding.UTF8);

                File.WriteAllText(Path.Combine(projDir, "p63.json"),
                    WorkspaceService.GenerateStandardProjectJson("p63", "Ws63", wsRoot), Encoding.UTF8);

                File.WriteAllText(Path.Combine(cfgDir, "config.json"),
                    "{\"plugins\":{\"gdocs\":{\"enabled\":false}},\"userSettings\":{\"autoExecutionPolicy\":\"CASCADE_COMMANDS_AUTO_EXECUTION_OFF\",\"customKeep\":123}}",
                    Encoding.UTF8);

                string mockPs = Path.Combine(sb, "mock_http_mcp.ps1");
                File.WriteAllText(mockPs,
                    "$l=[Console]::In.ReadLine(); [Console]::Out.WriteLine('{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":\"2024-11-05\",\"serverInfo\":{\"name\":\"http-e2e-mcp\",\"version\":\"1.0\"}}}'); [Console]::Out.Flush()",
                    Encoding.UTF8);
                File.WriteAllText(Path.Combine(cfgDir, "mcp_config.json"),
                    "{\"mcpServers\":{\"e2e-srv\":{\"command\":\"powershell.exe\",\"args\":[\"-NoProfile\",\"-ExecutionPolicy\",\"Bypass\",\"-File\",\"" + UnifiedJson.EscapeString(mockPs) + "\"],\"disabled\":false}}}",
                    Encoding.UTF8);

                string geminiMd = Path.Combine(geminiDir, "GEMINI.md");
                File.WriteAllText(geminiMd, "# E2E Rule\n", Encoding.UTF8);

                string token = "t63_tok_e2e";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, Path.Combine(sb, "Vault"), 0, token))
                {
                    host.Start();

                    // 1. GET /api/cockpit/summary
                    RawHttpTestResponse sumResp = SendRawTcpHttp(host.Port, null, "GET", "/api/cockpit/summary", apiHdr, null);
                    AssertEqual(200, sumResp.StatusCode, "T63: GET /api/cockpit/summary returns 200");
                    AssertTrue(sumResp.BodyText.Contains("\"gdocs\"") && sumResp.BodyText.Contains("\"e2e-srv\""), "T63: Summary contains plugin and MCP");
                    AssertTrue(sumResp.BodyText.Contains("\"skillsJsonPath\"") && sumResp.BodyText.Contains("\"pluginJsonPath\""), "T63: Summary serializes exact manifest paths");
                    AssertTrue(sumResp.BodyText.Contains(UnifiedJson.EscapeString(Path.Combine(wsRoot, "skills.json"))), "T63: Summary preserves root skills.json path even when .agents exists");

                    // 2. POST /api/cockpit/settings
                    RawHttpTestResponse setResp = SendRawTcpHttp(host.Port, null, "POST", "/api/cockpit/settings", apiHdr,
                        "{\"userSettings\":{\"autoExecutionPolicy\":\"CASCADE_COMMANDS_AUTO_EXECUTION_AUTO\",\"useAiCredits\":true}}");
                    AssertEqual(200, setResp.StatusCode, "T63: POST /api/cockpit/settings returns 200");
                    AssertTrue(setResp.BodyText.Contains("CASCADE_COMMANDS_AUTO_EXECUTION_AUTO") && setResp.BodyText.Contains("\"customKeep\":123"),
                        "T63: Settings updated and customKeep preserved");

                    // 3. POST /api/cockpit/toggle
                    RawHttpTestResponse togResp = SendRawTcpHttp(host.Port, null, "POST", "/api/cockpit/toggle", apiHdr,
                        "{\"kind\":\"plugin\",\"id\":\"gdocs\",\"enabled\":true}");
                    AssertEqual(200, togResp.StatusCode, "T63: POST /api/cockpit/toggle returns 200");
                    AssertTrue(togResp.BodyText.Contains("\"enabled\":true"), "T63: Plugin toggled to enabled=true");

                    // 4. POST /api/mcp/doctor
                    RawHttpTestResponse docResp = SendRawTcpHttp(host.Port, null, "POST", "/api/mcp/doctor", apiHdr,
                        "{\"id\":\"e2e-srv\"}");
                    AssertEqual(200, docResp.StatusCode, "T63: POST /api/mcp/doctor returns 200");
                    AssertTrue(docResp.BodyText.Contains("\"healthy\":true") && docResp.BodyText.Contains("http-e2e-mcp"),
                        "T63: MCP doctor verified e2e-srv healthy via HTTP");

                    // 5. GET /api/editor/file & POST /api/editor/save & 403 block
                    RawHttpTestResponse edGet = SendRawTcpHttp(host.Port, null, "GET", "/api/editor/file?path=" + Uri.EscapeDataString(geminiMd), apiHdr, null);
                    AssertEqual(200, edGet.StatusCode, "T63: GET /api/editor/file returns 200");
                    AssertTrue(edGet.BodyText.Contains("# E2E Rule"), "T63: Returned GEMINI.md content");

                    RawHttpTestResponse edSave = SendRawTcpHttp(host.Port, null, "POST", "/api/editor/save", apiHdr,
                        UnifiedJson.Serialize(new Dictionary<string, object> { { "path", geminiMd }, { "content", "# Updated E2E Rule\n" } }, false));
                    AssertEqual(200, edSave.StatusCode, "T63: POST /api/editor/save returns 200");
                    AssertTrue(File.ReadAllText(geminiMd, Encoding.UTF8).Contains("Updated E2E Rule"), "T63: Disk GEMINI.md updated");

                    RawHttpTestResponse edBlock = SendRawTcpHttp(host.Port, null, "GET", "/api/editor/file?path=" + Uri.EscapeDataString(Path.Combine(geminiDir, "oauth_creds.json")), apiHdr, null);
                    AssertEqual(403, edBlock.StatusCode, "T63: GET /api/editor/file rejects oauth_creds.json with 403");

                    RawHttpTestResponse edNullContent = SendRawTcpHttp(host.Port, null, "POST", "/api/editor/save", apiHdr,
                        "{\"path\":\"" + UnifiedJson.EscapeString(geminiMd) + "\",\"content\":null}");
                    AssertEqual(400, edNullContent.StatusCode, "T63: POST /api/editor/save rejects null content with 400");

                    RawHttpTestResponse edAds = SendRawTcpHttp(host.Port, null, "GET", "/api/editor/file?path=" + Uri.EscapeDataString(geminiMd + ":ads.json"), apiHdr, null);
                    AssertEqual(400, edAds.StatusCode, "T63: GET /api/editor/file rejects NTFS ADS colon with 400");

                    // 6. WorkspaceLauncherService path resolution, directory validation, and launch error paths
                    string fakeIdeBin = Path.Combine(geminiDir, "antigravity-ide", "bin");
                    string fakeLocalApp = Path.Combine(sb, "LocalApp");
                    string fakeExeDir = Path.Combine(fakeLocalApp, "Programs", "antigravity");
                    Directory.CreateDirectory(fakeIdeBin);
                    Directory.CreateDirectory(fakeExeDir);
                    File.WriteAllText(Path.Combine(fakeIdeBin, "antigravity-ide.cmd"), "@echo off\r\n", Encoding.UTF8);
                    File.WriteAllText(Path.Combine(fakeExeDir, "Antigravity.exe"), "MZ", Encoding.UTF8);
                    ResolvedLaunchTools resolvedTools = WorkspaceLauncherService.ResolveLaunchTools(geminiDir, fakeLocalApp, "");
                    AssertTrue(!string.IsNullOrEmpty(resolvedTools.AntigravityIdeCmd) && resolvedTools.AntigravityIdeCmd.EndsWith("antigravity-ide.cmd", StringComparison.OrdinalIgnoreCase),
                        "T63: Resolved antigravity-ide.cmd via .gemini/antigravity-ide/bin");
                    AssertTrue(!string.IsNullOrEmpty(resolvedTools.AntigravityExe) && resolvedTools.AntigravityExe.EndsWith("Antigravity.exe", StringComparison.OrdinalIgnoreCase),
                        "T63: Resolved Antigravity.exe via LOCALAPPDATA\\Programs\\antigravity");

                    string valDir;
                    int valCode;
                    string valErr;
                    AssertTrue(WorkspaceLauncherService.ValidateWorkspaceDirectory(geminiDir, homeDir, Path.Combine(sb, "Vault"), wsRoot, out valDir, out valCode, out valErr),
                        "T63: Registered workspace directory validated");
                    AssertTrue(!WorkspaceLauncherService.ValidateWorkspaceDirectory(geminiDir, homeDir, Path.Combine(sb, "Vault"), Path.Combine(wsRoot, "missing_sub"), out valDir, out valCode, out valErr) && valCode == 404,
                        "T63: Missing workspace directory returns 404");

                    RawHttpTestResponse wsBadTool = SendRawTcpHttp(host.Port, null, "POST", "/api/workspace/launch", apiHdr,
                        "{\"path\":\"" + UnifiedJson.EscapeString(wsRoot) + "\",\"tool\":\"unknown_tool\"}");
                    AssertEqual(400, wsBadTool.StatusCode, "T63: POST /api/workspace/launch rejects unknown tool with 400");

                    RawHttpTestResponse wsBlock = SendRawTcpHttp(host.Port, null, "POST", "/api/workspace/launch", apiHdr,
                        "{\"path\":\"C:\\\\Windows\\\\System32\",\"tool\":\"explorer\"}");
                    AssertEqual(403, wsBlock.StatusCode, "T63: POST /api/workspace/launch rejects outside-root directory with 403");

                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Stage 3 Regression Tests (T64 ~ T69)
        // ══════════════════════════════════════════════════════════════════════

        private static void CreateSampleLiposuctionDb(
            string dbPath,
            string cid,
            int execRows,
            int execBytesPerRow,
            int stepRows)
        {
            string dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            IntPtr db = IntPtr.Zero;
            try
            {
                AssertEqual(0, t_sqlite3_open_v2(Utf8Z(dbPath), out db, 0x00000002 | 0x00000004, IntPtr.Zero), "Open sample liposuction DB");
                string ddl =
                    "CREATE TABLE trajectory_meta (trajectory_id TEXT PRIMARY KEY, cascade_id TEXT, trajectory_type INTEGER, source INTEGER);" +
                    "CREATE TABLE steps (idx INTEGER PRIMARY KEY, step_type TEXT, content TEXT, data TEXT, step_payload BLOB);" +
                    "CREATE TABLE gen_metadata (idx INTEGER PRIMARY KEY, data BLOB, size INTEGER NOT NULL DEFAULT 0);" +
                    "CREATE TABLE executor_metadata (idx INTEGER PRIMARY KEY, data BLOB);" +
                    "CREATE TABLE parent_references (idx INTEGER PRIMARY KEY, data BLOB);" +
                    "CREATE TABLE trajectory_metadata_blob (id TEXT PRIMARY KEY, data BLOB);";
                AssertEqual(0, t_sqlite3_exec(db, Utf8Z(ddl), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Create sample schema: " + T_GetSqliteErr(db));

                AssertEqual(0, t_sqlite3_exec(db, Utf8Z("INSERT INTO trajectory_meta VALUES ('" + cid + "', '" + cid + "', 1, 1);"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Insert trajectory_meta");
                AssertEqual(0, t_sqlite3_exec(db, Utf8Z("INSERT INTO gen_metadata VALUES (0, zeroblob(64), 64);"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Insert gen_metadata");
                AssertEqual(0, t_sqlite3_exec(db, Utf8Z("INSERT INTO parent_references VALUES (0, zeroblob(16));"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Insert parent_references");

                ProtoNode entry = BuildRealProtoEntry(cid, "Subagent Liposuction Sample", null, "file:///c%3A/Work/Sample", "SampleProj", "pid-sample", stepRows, 1, 0);
                byte[] metaBlob = ProtoCodec.Encode(new List<ProtoNode> { entry });
                IntPtr mStmt = IntPtr.Zero;
                byte[] pinMeta = metaBlob.Length > 0 ? metaBlob : new byte[] { 0 };
                GCHandle hMeta = GCHandle.Alloc(pinMeta, GCHandleType.Pinned);
                try
                {
                    AssertEqual(0, t_sqlite3_prepare_v2(db, Utf8Z("INSERT INTO trajectory_metadata_blob (id, data) VALUES ('main', ?);"), -1, out mStmt, IntPtr.Zero), "Prep meta blob");
                    AssertEqual(0, t_sqlite3_bind_blob(mStmt, 1, hMeta.AddrOfPinnedObject(), metaBlob.Length, new IntPtr(-1)), "Bind meta blob");
                    AssertEqual(101, t_sqlite3_step(mStmt), "Step meta blob");
                }
                finally
                {
                    if (mStmt != IntPtr.Zero) t_sqlite3_finalize(mStmt);
                    if (hMeta.IsAllocated) hMeta.Free();
                }

                AssertEqual(0, t_sqlite3_exec(db, Utf8Z("BEGIN TRANSACTION;"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Begin tx");
                for (int i = 0; i < stepRows; i++)
                {
                    string stType = (i % 2 == 0) ? "USER_INPUT" : "PLANNER_RESPONSE";
                    string stContent = (i % 2 == 0) ? ("User prompt #" + i) : ("Assistant reply #" + i);
                    IntPtr sStmt = IntPtr.Zero;
                    AssertEqual(0, t_sqlite3_prepare_v2(db, Utf8Z("INSERT INTO steps (idx, step_type, content, data, step_payload) VALUES (?, ?, ?, ?, zeroblob(32));"), -1, out sStmt, IntPtr.Zero), "Prep step");
                    try
                    {
                        t_sqlite3_bind_int(sStmt, 1, i);
                        T_BindUtf8Text(sStmt, 2, stType);
                        T_BindUtf8Text(sStmt, 3, stContent);
                        T_BindUtf8Text(sStmt, 4, "{\"content\":\"" + stContent + "\"}");
                        AssertEqual(101, t_sqlite3_step(sStmt), "Insert step row");
                    }
                    finally
                    {
                        t_sqlite3_finalize(sStmt);
                    }
                }

                byte[] execPayload = new byte[execBytesPerRow];
                for (int i = 0; i < execPayload.Length; i++) execPayload[i] = (byte)('A' + (i % 26));
                byte[] pinExec = execPayload.Length > 0 ? execPayload : new byte[] { 0 };
                GCHandle hExec = GCHandle.Alloc(pinExec, GCHandleType.Pinned);
                try
                {
                    for (int i = 0; i < execRows; i++)
                    {
                        IntPtr eStmt = IntPtr.Zero;
                        AssertEqual(0, t_sqlite3_prepare_v2(db, Utf8Z("INSERT INTO executor_metadata (idx, data) VALUES (?, ?);"), -1, out eStmt, IntPtr.Zero), "Prep exec");
                        try
                        {
                            t_sqlite3_bind_int(eStmt, 1, i);
                            t_sqlite3_bind_blob(eStmt, 2, hExec.AddrOfPinnedObject(), execPayload.Length, new IntPtr(-1));
                            AssertEqual(101, t_sqlite3_step(eStmt), "Insert exec row");
                        }
                        finally
                        {
                            t_sqlite3_finalize(eStmt);
                        }
                    }
                }
                finally
                {
                    if (hExec.IsAllocated) hExec.Free();
                }
                AssertEqual(0, t_sqlite3_exec(db, Utf8Z("COMMIT;"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "Commit tx");
            }
            finally
            {
                if (db != IntPtr.Zero) t_sqlite3_close(db);
            }
        }

        private static void T64_Stage3_StorageAnalyze_ByteAccountingAndTableBreakdown()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t64_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                string cliDir = Path.Combine(geminiDir, "antigravity-cli");
                string projDir = Path.Combine(geminiDir, "config", "projects");
                Directory.CreateDirectory(Path.Combine(agDir, "conversations"));
                Directory.CreateDirectory(Path.Combine(agDir, "brain"));
                Directory.CreateDirectory(Path.Combine(agDir, "annotations"));
                Directory.CreateDirectory(Path.Combine(cliDir, "conversations"));
                Directory.CreateDirectory(projDir);

                string cidRoot = "a6400001-0000-4000-8000-000000000001";
                string cidSub = "b6400002-0000-4000-8000-000000000002";
                string cidOut = "c6400003-0000-4000-8000-000000000003";
                string cidCli = "d6400004-0000-4000-8000-000000000004";
                string cidOrphan = "e6400005-0000-4000-8000-000000000005";

                File.WriteAllText(Path.Combine(projDir, "pid-64.json"),
                    WorkspaceService.GenerateStandardProjectJson("pid-64", "Proj64", "C:\\Work\\Proj64"), Encoding.UTF8);

                ProtoNode nRoot = BuildRealProtoEntry(cidRoot, "Root 64", null, "file:///c%3A/Work/Proj64", "Proj64", "pid-64", 4, 1, 1);
                ProtoNode nSub = BuildRealProtoEntry(cidSub, "Sub 64", null, "file:///c%3A/Work/Proj64", "Proj64", "pid-64", 6, 1, 0);
                nSub.GetChild(2).GetChild(17).Children.Add(new ProtoNode(11, 2) { RawBytes = Encoding.UTF8.GetBytes(cidRoot), IsModified = true });
                ProtoNode nOut = BuildRealProtoEntry(cidOut, "Outside 64", null, null, null, "outside-of-project", 2, 1, 1);
                File.WriteAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb"), ProtoCodec.Encode(new List<ProtoNode> { nRoot, nSub, nOut }));

                ProtoNode nCli = BuildRealProtoEntry(cidCli, "CLI 64", null, null, "default-cli-project", "default-cli-project", 3, 1, 1);
                File.WriteAllBytes(Path.Combine(cliDir, "agyhub_summaries_proto.pb"), ProtoCodec.Encode(new List<ProtoNode> { nCli }));

                IntPtr sumDb = OpenAndCreateSummaryWalDb(Path.Combine(agDir, "conversation_summaries.db"));
                try
                {
                    InsertSummaryDbRow(sumDb, cidRoot, "Root 64", "p", 4, "[\"file:///c%3A/Work/Proj64\"]", "CASCADE_RUN_STATUS_IDLE", "pid-64", "", "", 0, 0, ProtoCodec.Encode(nRoot.Children));
                    InsertSummaryDbRow(sumDb, cidSub, "Sub 64", "p", 6, "[\"file:///c%3A/Work/Proj64\"]", "CASCADE_RUN_STATUS_IDLE", "pid-64", "explorer", cidRoot, 1, 0, ProtoCodec.Encode(nSub.Children));
                    InsertSummaryDbRow(sumDb, cidOut, "Outside 64", "p", 2, "[]", "CASCADE_RUN_STATUS_IDLE", "outside-of-project", "", "", 0, 0, ProtoCodec.Encode(nOut.Children));
                }
                finally
                {
                    t_sqlite3_close(sumDb);
                }

                string rootDbPath = Path.Combine(agDir, "conversations", cidRoot + ".db");
                string subDbPath = Path.Combine(agDir, "conversations", cidSub + ".db");
                string outDbPath = Path.Combine(agDir, "conversations", cidOut + ".db");
                string cliDbPath = Path.Combine(cliDir, "conversations", cidCli + ".db");

                CreateSampleLiposuctionDb(rootDbPath, cidRoot, 1, 512, 4);
                CreateSampleLiposuctionDb(subDbPath, cidSub, 25, 16384, 6);
                CreateSampleLiposuctionDb(outDbPath, cidOut, 0, 0, 2);
                CreateSampleLiposuctionDb(cliDbPath, cidCli, 0, 0, 3);

                Directory.CreateDirectory(Path.Combine(agDir, "brain", cidRoot));
                string notesPath64 = Path.Combine(agDir, "brain", cidRoot, "notes.md");
                File.WriteAllText(notesPath64, new string('B', 1200), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(agDir, "annotations", cidRoot + ".pbtxt"), "title:\"Root 64\"", Encoding.UTF8);
                File.WriteAllText(Path.Combine(agDir, "annotations", cidOrphan + ".pbtxt"), "title:\"Orphan 64\"", Encoding.UTF8);

                StorageAnalysisReport rep = StorageSlimService.AnalyzeStorage(homeDir, geminiDir, 10, 7.0, 0.1);
                AssertEqual(new FileInfo(rootDbPath).Length + new FileInfo(outDbPath).Length, rep.RootDbsBytes, "T64: Exact RootDbsBytes");
                AssertEqual(new FileInfo(subDbPath).Length, rep.SubagentDbsBytes, "T64: Exact SubagentDbsBytes");
                AssertEqual(new FileInfo(cliDbPath).Length, rep.CliDbsBytes, "T64: Exact CliDbsBytes");
                AssertEqual(new FileInfo(notesPath64).Length, rep.BrainDirsBytes, "T64: Exact BrainDirsBytes");
                AssertEqual(new FileInfo(Path.Combine(agDir, "annotations", cidRoot + ".pbtxt")).Length, rep.AnnotationsBytes, "T64: Exact AnnotationsBytes");
                AssertEqual(new FileInfo(Path.Combine(agDir, "annotations", cidOrphan + ".pbtxt")).Length, rep.OrphansBytes, "T64: Exact OrphansBytes");

                ProjectStorageBreakdown p64 = rep.Projects.Find(delegate(ProjectStorageBreakdown p) { return p.ProjectId == "pid-64"; });
                ProjectStorageBreakdown pOut = rep.Projects.Find(delegate(ProjectStorageBreakdown p) { return p.ProjectId == "outside-of-project"; });
                AssertTrue(p64 != null && pOut != null, "T64: Both Proj64 and outside-of-project buckets accounted");
                AssertEqual(new FileInfo(outDbPath).Length, pOut.RootDbsBytes, "T64: Outside-of-project DB bytes match");

                AssertTrue(rep.TopOffenders.Count >= 4 && rep.TopOffenders[0].ConversationId == cidSub, "T64: Largest offender is subagent DB");
                AssertTrue(rep.TopOffenders[0].HasTableBreakdown && rep.TopOffenders[0].HasExecutorMetadataTable, "T64: Top offender has SQLite table breakdown");
                AssertEqual(25L, rep.TopOffenders[0].ExecutorMetadataRows, "T64: executor_metadata row count == 25");
                AssertEqual(25L * 16384L, rep.TopOffenders[0].ExecutorMetadataBytes, "T64: executor_metadata payload bytes exact");
                AssertEqual(6L, rep.TopOffenders[0].StepsRows, "T64: steps row count == 6");
                AssertTrue(rep.TopOffenders[0].StepsBytes > 0, "T64: steps payload bytes > 0");
                AssertTrue(rep.TopOffenders[0].EligibleForSlim, "T64: Eligible subagent flagged EligibleForSlim");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T65_Stage3_StorageSlim_Mode5CLiposuctionAndMode5AColdArchivePrune()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t65_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                string convDir = Path.Combine(agDir, "conversations");
                string brainDir = Path.Combine(agDir, "brain");
                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(brainDir);

                string cidParent = "p6500001-0000-4000-8000-000000000001";
                string cidSub5c = "s6500002-0000-4000-8000-000000000002";
                string cidSub5a = "a6500003-0000-4000-8000-000000000003";
                string cidActive = "l6500004-0000-4000-8000-000000000004";
                string cidRecent = "r6500005-0000-4000-8000-000000000005";

                ProtoNode nPar = BuildRealProtoEntry(cidParent, "Parent 65", null, "file:///c%3A/P65", "P65", "pid-65", 4, 1, 1);
                ProtoNode nSub5c = BuildRealProtoEntry(cidSub5c, "Sub 5C", null, "file:///c%3A/P65", "P65", "pid-65", 6, 1, 0);
                nSub5c.GetChild(2).GetChild(17).Children.Add(new ProtoNode(11, 2) { RawBytes = Encoding.UTF8.GetBytes(cidParent), IsModified = true });
                ProtoNode nSub5a = BuildRealProtoEntry(cidSub5a, "Sub 5A", null, "file:///c%3A/P65", "P65", "pid-65", 4, 1, 0);
                nSub5a.GetChild(2).GetChild(17).Children.Add(new ProtoNode(11, 2) { RawBytes = Encoding.UTF8.GetBytes(cidParent), IsModified = true });
                ProtoNode nAct = BuildRealProtoEntry(cidActive, "Active Sub", null, "file:///c%3A/P65", "P65", "pid-65", 4, 1, 0);
                nAct.GetChild(2).GetChild(17).Children.Add(new ProtoNode(11, 2) { RawBytes = Encoding.UTF8.GetBytes(cidParent), IsModified = true });
                ProtoNode nRec = BuildRealProtoEntry(cidRecent, "Recent Sub", null, "file:///c%3A/P65", "P65", "pid-65", 4, 1, 0);
                nRec.GetChild(2).GetChild(17).Children.Add(new ProtoNode(11, 2) { RawBytes = Encoding.UTF8.GetBytes(cidParent), IsModified = true });
                ulong nowEpoch = (ulong)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                nRec.GetChild(2).GetChild(3).GetChild(1).VarintValue = nowEpoch - 3600UL; // 1 hour ago (< 7 days)

                File.WriteAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb"),
                    ProtoCodec.Encode(new List<ProtoNode> { nPar, nSub5c, nSub5a, nAct, nRec }));

                IntPtr sumDb = OpenAndCreateSummaryWalDb(Path.Combine(agDir, "conversation_summaries.db"));
                try
                {
                    InsertSummaryDbRow(sumDb, cidParent, "Parent 65", "p", 4, "[\"file:///c%3A/P65\"]", "CASCADE_RUN_STATUS_IDLE", "pid-65", "", "", 0, 0, ProtoCodec.Encode(nPar.Children));
                    InsertSummaryDbRow(sumDb, cidSub5c, "Sub 5C", "p", 6, "[\"file:///c%3A/P65\"]", "CASCADE_RUN_STATUS_IDLE", "pid-65", "coder", cidParent, 1, 0, ProtoCodec.Encode(nSub5c.Children));
                    InsertSummaryDbRow(sumDb, cidSub5a, "Sub 5A", "p", 4, "[\"file:///c%3A/P65\"]", "IDLE", "pid-65", "coder", cidParent, 1, 0, ProtoCodec.Encode(nSub5a.Children));
                    InsertSummaryDbRow(sumDb, cidActive, "Active Sub", "p", 4, "[\"file:///c%3A/P65\"]", "CASCADE_RUN_STATUS_IDLE", "pid-65", "coder", cidParent, 1, 1, ProtoCodec.Encode(nAct.Children));
                    InsertSummaryDbRow(sumDb, cidRecent, "Recent Sub", "p", 4, "[\"file:///c%3A/P65\"]", "CASCADE_RUN_STATUS_IDLE", "pid-65", "coder", cidParent, 1, 0, ProtoCodec.Encode(nRec.Children));
                }
                finally
                {
                    t_sqlite3_close(sumDb);
                }

                string parDb = Path.Combine(convDir, cidParent + ".db");
                string sub5cDb = Path.Combine(convDir, cidSub5c + ".db");
                string sub5aDb = Path.Combine(convDir, cidSub5a + ".db");
                string actDb = Path.Combine(convDir, cidActive + ".db");
                string recDb = Path.Combine(convDir, cidRecent + ".db");

                CreateSampleLiposuctionDb(parDb, cidParent, 60, 100 * 1024, 4); // >5 MB root session (must NOT be touched)
                CreateSampleLiposuctionDb(sub5cDb, cidSub5c, 60, 100 * 1024, 6); // >5 MB aged idle subagent
                CreateSampleLiposuctionDb(sub5aDb, cidSub5a, 60, 100 * 1024, 4); // >5 MB aged idle subagent
                CreateSampleLiposuctionDb(actDb, cidActive, 60, 100 * 1024, 4); // >5 MB not_fully_idle=1 (must NOT be touched)
                CreateSampleLiposuctionDb(recDb, cidRecent, 60, 100 * 1024, 4); // >5 MB <7d (must NOT be touched)

                string sub5aBrain = Path.Combine(brainDir, cidSub5a);
                Directory.CreateDirectory(sub5aBrain);
                File.WriteAllText(Path.Combine(sub5aBrain, "notes.md"), "# Keep Brain", Encoding.UTF8);

                long parBeforeSize = new FileInfo(parDb).Length;
                long actBeforeSize = new FileInfo(actDb).Length;
                long recBeforeSize = new FileInfo(recDb).Length;
                long sub5cBeforeSize = new FileInfo(sub5cDb).Length;

                // 1. Pre-flight sandbox compatibility verification on sub5cDb
                string preReport;
                AssertTrue(StorageSlimService.VerifyLiposuctionCompatibility(sub5cDb, out preReport), "T65: Pre-flight VerifyLiposuctionCompatibility passes: " + preReport);
                AssertEqual(sub5cBeforeSize, new FileInfo(sub5cDb).Length, "T65: Pre-flight never mutates original sample DB");

                // Render Markdown BEFORE liposuction
                ConversationSummaryInfo sub5cSummary = new ConversationSummaryInfo { ConversationId = cidSub5c, DisplayTitle = "Sub 5C", ProjectName = "P65", StepCount = 6 };
                bool trunc;
                string mdBefore = TranscriptMarkdownExporter.PreviewLocalSessionMarkdown(sub5cSummary, geminiDir, 4 * 1024 * 1024, out trunc);

                // 2. Execute Mode 5C (Liposuction) targeting cidSub5c (and testing default filter rejects parent/active/recent when TargetCids=null? Target cidSub5c + check skipped)
                StorageSlimRequest req5c = new StorageSlimRequest();
                req5c.Mode = StorageSlimMode.Liposuction;
                req5c.TargetCids = new List<string> { cidSub5c, cidParent, cidActive, cidRecent };
                req5c.MinAgeDays = 7.0;
                req5c.MinSizeMb = 5.0;
                StorageSlimResult res5c = StorageSlimService.ExecuteStorageSlim(homeDir, geminiDir, req5c);

                AssertTrue(res5c.Ok, "T65: Mode 5C succeeded: " + res5c.ErrorMessage);
                AssertEqual(1, res5c.ProcessedCount, "T65: Mode 5C processed ONLY eligible cidSub5c");
                AssertEqual(3, res5c.SkippedCount, "T65: Mode 5C skipped parent, active(not_fully_idle=1), and recent");
                AssertTrue(File.Exists(res5c.ColdArchivePath), "T65: Mandatory cold archive .agy created before liposuction");

                using (ZipArchive coldZip = ZipFile.OpenRead(res5c.ColdArchivePath))
                {
                    ZipArchiveEntry archivedSub = coldZip.GetEntry("data/antigravity/conversations/" + cidSub5c + ".db");
                    AssertTrue(archivedSub != null && archivedSub.Length >= sub5cBeforeSize - 65536, "T65: Cold archive contains full untouched original subagent DB");
                    AssertTrue(coldZip.GetEntry("manifest.json") != null, "T65: Cold archive contains manifest.json");
                }

                long sub5cAfterSize = new FileInfo(sub5cDb).Length;
                AssertTrue(sub5cAfterSize < sub5cBeforeSize / 10, "T65: Liposuction shrank DB dramatically (" + sub5cBeforeSize + " -> " + sub5cAfterSize + ")");

                string intDetail;
                AssertTrue(SqliteOnlineBackup.RunIntegrityCheck(sub5cDb, out intDetail) && intDetail == "ok", "T65: PRAGMA integrity_check == ok after liposuction");
                AssertEqual("6", QuerySingleText(sub5cDb, "SELECT COUNT(*) FROM steps;", null), "T65: steps row count 100% preserved");
                AssertEqual("0", QuerySingleText(sub5cDb, "SELECT COUNT(*) FROM executor_metadata;", null), "T65: executor_metadata cleared");

                string mdAfter = TranscriptMarkdownExporter.PreviewLocalSessionMarkdown(sub5cSummary, geminiDir, 4 * 1024 * 1024, out trunc);
                AssertEqual(mdBefore, mdAfter, "T65: Markdown export output 100% identical after liposuction");

                // Verify root, active, and recent untouched
                AssertEqual(parBeforeSize, new FileInfo(parDb).Length, "T65: Root parent DB untouched");
                AssertEqual(actBeforeSize, new FileInfo(actDb).Length, "T65: Active not_fully_idle=1 DB untouched");
                AssertEqual(recBeforeSize, new FileInfo(recDb).Length, "T65: Recent (<7d) DB untouched");

                // 3. Execute Mode 5A (ColdArchivePrune) on cidSub5a
                StorageSlimRequest req5a = new StorageSlimRequest();
                req5a.Mode = StorageSlimMode.ColdArchivePrune;
                req5a.TargetCids = new List<string> { cidSub5a };
                req5a.MinAgeDays = 7.0;
                req5a.MinSizeMb = 5.0;
                StorageSlimResult res5a = StorageSlimService.ExecuteStorageSlim(homeDir, geminiDir, req5a);
                AssertTrue(res5a.Ok && res5a.ProcessedCount == 1, "T65: Mode 5A ColdArchivePrune processed cidSub5a");
                AssertTrue(File.Exists(res5a.ColdArchivePath), "T65: Mode 5A created cold archive .agy");
                AssertTrue(!File.Exists(sub5aDb), "T65: Mode 5A deleted local subagent .db");
                AssertTrue(File.Exists(parDb), "T65: Mode 5A preserved parent session .db");
                AssertTrue(File.Exists(Path.Combine(sub5aBrain, "notes.md")), "T65: Mode 5A preserved brain/ directory");

                // 4. Regression: NOT_FULLY_IDLE status string must NOT pass IsIdleRunStatus
                AssertTrue(!StorageSlimService.IsIdleRunStatus("NOT_FULLY_IDLE"), "T65: NOT_FULLY_IDLE rejected as non-idle");
                AssertTrue(StorageSlimService.IsIdleRunStatus("CASCADE_RUN_STATUS_IDLE"), "T65: CASCADE_RUN_STATUS_IDLE accepted as idle");
                AssertTrue(StorageSlimService.IsIdleRunStatus("IDLE"), "T65: IDLE accepted as idle");

                // 5. Regression: Trigger referencing executor_metadata blocks liposuction cleanly (no mutation)
                string trigDb = Path.Combine(sb, "trig_sub.db");
                CreateSampleLiposuctionDb(trigDb, cidSub5c, 5, 1024, 3);
                string trigErr;
                AssertTrue(SqliteOnlineBackup.ExecuteNonQuery(trigDb,
                    "CREATE TRIGGER tr_exec_guard AFTER DELETE ON executor_metadata BEGIN SELECT 1; END;", out trigErr),
                    "T65: Created trigger on executor_metadata for negative test: " + trigErr);
                string trigReport;
                AssertTrue(!StorageSlimService.VerifyLiposuctionCompatibility(trigDb, out trigReport) &&
                           trigReport.Contains("Trigger"),
                    "T65: VerifyLiposuctionCompatibility rejects DB with trigger on executor_metadata");
                AssertEqual("5", QuerySingleText(trigDb, "SELECT COUNT(*) FROM executor_metadata;", null),
                    "T65: Trigger-protected original DB remains untouched");

                // 6. Regression: Locked -wal sidecar prevents atomic swap without corrupting DB
                string lockSubDb = Path.Combine(convDir, cidSub5c + ".db");
                long lockedBeforeSize = new FileInfo(lockSubDb).Length;
                string lockWal = lockSubDb + "-wal";
                File.WriteAllBytes(lockWal, new byte[0]);
                using (FileStream walLock = new FileStream(lockWal, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    StorageSlimRequest reqLocked = new StorageSlimRequest();
                    reqLocked.Mode = StorageSlimMode.Liposuction;
                    reqLocked.TargetCids = new List<string> { cidSub5c };
                    reqLocked.MinAgeDays = 7.0;
                    reqLocked.MinSizeMb = 0.001;
                    reqLocked.RunPreFlight = false;
                    StorageSlimResult resLocked = StorageSlimService.ExecuteStorageSlim(homeDir, geminiDir, reqLocked);
                    AssertTrue(resLocked.Ok && resLocked.ProcessedCount == 0 && resLocked.SkippedCount == 1,
                        "T65: Locked -wal skips swap cleanly without corrupting DB (" + resLocked.ErrorMessage + ")");
                    AssertEqual(lockedBeforeSize, new FileInfo(lockSubDb).Length, "T65: Original DB unchanged when -wal locked");
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T66_Stage3_BackupTierFastDailyVsFullForensicAndRestoreUserSettings()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t66_" + Guid.NewGuid().ToString("N"));
            string fastZip = Path.Combine(sb, "fast.agy");
            string fullZip = Path.Combine(sb, "full.agy");
            try
            {
                string srcHome = Path.Combine(sb, "SrcHome");
                string srcGemini = Path.Combine(srcHome, ".gemini");
                string srcAg = Path.Combine(srcGemini, "antigravity");
                string srcCli = Path.Combine(srcGemini, "antigravity-cli");
                string srcCfg = Path.Combine(srcGemini, "config");
                Directory.CreateDirectory(Path.Combine(srcAg, "conversations"));
                Directory.CreateDirectory(Path.Combine(srcAg, "bin"));
                Directory.CreateDirectory(Path.Combine(srcCli, "conversations"));
                Directory.CreateDirectory(Path.Combine(srcCli, "bin"));
                Directory.CreateDirectory(srcCfg);

                string cidRoot = "r6600001-0000-4000-8000-000000000001";
                string cidSub = "s6600002-0000-4000-8000-000000000002";
                string cidCli = "c6600003-0000-4000-8000-000000000003";

                ProtoNode nRoot = BuildRealProtoEntry(cidRoot, "Root 66", null, "file:///c%3A/P66", "P66", "pid-66", 3, 1, 1);
                ProtoNode nSub = BuildRealProtoEntry(cidSub, "Sub 66", null, "file:///c%3A/P66", "P66", "pid-66", 3, 1, 0);
                nSub.GetChild(2).GetChild(17).Children.Add(new ProtoNode(11, 2) { RawBytes = Encoding.UTF8.GetBytes(cidRoot), IsModified = true });
                byte[] idePb = ProtoCodec.Encode(new List<ProtoNode> { nRoot, nSub });
                File.WriteAllBytes(Path.Combine(srcAg, "agyhub_summaries_proto.pb"), idePb);

                ProtoNode nCli = BuildRealProtoEntry(cidCli, "CLI 66", null, null, "default-cli-project", "default-cli-project", 2, 1, 1);
                byte[] cliPb = ProtoCodec.Encode(new List<ProtoNode> { nCli });
                File.WriteAllBytes(Path.Combine(srcCli, "agyhub_summaries_proto.pb"), cliPb);

                CreateMinimalValidSqliteDb(Path.Combine(srcAg, "conversations", cidRoot + ".db"), cidRoot);
                CreateMinimalValidSqliteDb(Path.Combine(srcAg, "conversations", cidSub + ".db"), cidSub);
                CreateMinimalValidSqliteDb(Path.Combine(srcCli, "conversations", cidCli + ".db"), cidCli);
                File.WriteAllText(Path.Combine(srcAg, "bin", "helper.exe"), "BIN", Encoding.UTF8);
                File.WriteAllText(Path.Combine(srcCli, "bin", "cli.exe"), "BIN", Encoding.UTF8);
                File.WriteAllText(Path.Combine(srcGemini, "settings.json"), "{\"cliTheme\":\"dark\"}", Encoding.UTF8);
                File.WriteAllText(Path.Combine(srcCfg, "config.json"),
                    "{\r\n  \"userSettings\": {\r\n    \"themeMode\": \"THEME_MODE_DARK\",\r\n    \"enableTerminalSandbox\": true\r\n  },\r\n  \"plugins\": {\r\n    \"plug-a\": { \"enabled\": true, \"installedFrom\": { \"version\": \"1.0\" } },\r\n    \"plug-b\": { \"enabled\": false }\r\n  }\r\n}\r\n",
                    Encoding.UTF8);

                List<ConversationSummaryInfo> sums = new List<ConversationSummaryInfo>();
                sums.AddRange(SessionMetadataExtractor.ExtractSummariesFromProto(idePb));
                List<ConversationSummaryInfo> cliSums = SessionMetadataExtractor.ExtractSummariesFromProto(cliPb);
                cliSums[0].IsCliSession = true;
                sums.AddRange(cliSums);

                int fcFast, fcFull;
                List<string> wFast, wFull;
                ArchiveTransferService.CreateFullBackup(srcGemini, srcHome, sums, fastZip, BackupTier.FastDaily, null, out fcFast, out wFast);
                ArchiveTransferService.CreateFullBackup(srcGemini, srcHome, sums, fullZip, BackupTier.FullForensic, null, out fcFull, out wFull);

                BackupArchiveIndex fastIdx = StreamingZipIndexReader.ReadIndex(fastZip);
                AssertEqual("FastDaily", fastIdx.BackupTier, "T66: FastDaily manifest tier recorded");
                AssertTrue(fastIdx.ExistingConvoDbFiles.Contains(cidRoot), "T66: FastDaily includes root IDE DB");
                AssertTrue(fastIdx.ExistingConvoDbFiles.Contains(cidCli), "T66: FastDaily includes CLI DB");
                AssertTrue(!fastIdx.ExistingConvoDbFiles.Contains(cidSub), "T66: FastDaily excludes subagent DB");

                BackupArchiveIndex fullIdx = StreamingZipIndexReader.ReadIndex(fullZip);
                AssertEqual("FullForensic", fullIdx.BackupTier, "T66: FullForensic manifest tier recorded");
                AssertTrue(fullIdx.ExistingConvoDbFiles.Contains(cidSub), "T66: FullForensic includes subagent DB");

                using (ZipArchive zFull = ZipFile.OpenRead(fullZip))
                {
                    bool hasBin = false;
                    for (int i = 0; i < zFull.Entries.Count; i++)
                    {
                        if (zFull.Entries[i].FullName.IndexOf("/bin/", StringComparison.OrdinalIgnoreCase) >= 0) hasBin = true;
                    }
                    AssertTrue(!hasBin, "T66: FullForensic still excludes bin/");
                }

                // RestoreUserSettings merge into local config.json preserving local unknown keys & JSONC comments
                string dstHome = Path.Combine(sb, "DstHome");
                string dstGemini = Path.Combine(dstHome, ".gemini");
                string dstCfgDir = Path.Combine(dstGemini, "config");
                Directory.CreateDirectory(dstCfgDir);
                string dstCfgPath = Path.Combine(dstCfgDir, "config.json");
                File.WriteAllText(dstCfgPath,
                    "{\r\n  // Keep local JSONC comment\r\n  \"userSettings\": {\r\n    \"themeMode\": \"THEME_MODE_LIGHT\",\r\n    \"localUnknownKey\": \"keep-intact\"\r\n  },\r\n  \"plugins\": {\r\n    \"plug-a\": { \"enabled\": false, \"installedFrom\": { \"version\": \"2.5.0\" }, \"localFlag\": 99 },\r\n    \"local-only-plug\": { \"enabled\": true }\r\n  }\r\n}\r\n",
                    Encoding.UTF8);

                SelectiveImportRequest impReq = new SelectiveImportRequest
                {
                    GeminiPath = dstGemini,
                    HomePath = dstHome,
                    SourceMode = RightPaneSourceMode.BackupArchive,
                    LoadedBackupPath = fastZip,
                    BackupIndex = fastIdx,
                    RestoreUserSettings = true
                };
                OperationResult<SelectiveImportResult> impRes = ArchiveTransferService.PerformSelectiveImportResult(impReq, null);
                AssertTrue(impRes.Ok, "T66: RestoreUserSettings selective import succeeded: " + impRes.ErrorMessage);

                string afterCfgText = File.ReadAllText(dstCfgPath, Encoding.UTF8);
                AssertTrue(afterCfgText.Contains("// Keep local JSONC comment"), "T66: Local JSONC comment preserved");
                AssertTrue(Directory.GetFiles(dstCfgDir, "config.json.bak.*").Length == 1, "T66: Timestamped config.json.bak.* created");

                Dictionary<string, object> parsedCfg = UnifiedJson.AsDict(UnifiedJson.Parse(afterCfgText));
                Dictionary<string, object> us = UnifiedJson.AsDict(parsedCfg["userSettings"]);
                AssertEqual("THEME_MODE_DARK", UnifiedJson.GetString(us, "themeMode", ""), "T66: userSettings.themeMode restored");
                AssertTrue(UnifiedJson.GetBool(us, "enableTerminalSandbox", false), "T66: userSettings.enableTerminalSandbox restored");
                AssertEqual("keep-intact", UnifiedJson.GetString(us, "localUnknownKey", ""), "T66: Local unknown userSettings key preserved");

                Dictionary<string, object> plugs = UnifiedJson.AsDict(parsedCfg["plugins"]);
                Dictionary<string, object> plugA = UnifiedJson.AsDict(plugs["plug-a"]);
                Dictionary<string, object> plugB = UnifiedJson.AsDict(plugs["plug-b"]);
                Dictionary<string, object> plugLocal = UnifiedJson.AsDict(plugs["local-only-plug"]);
                AssertTrue(UnifiedJson.GetBool(plugA, "enabled", false), "T66: plug-a enabled flag merged");
                AssertEqual(99, UnifiedJson.GetInt(plugA, "localFlag", 0), "T66: plug-a local fields preserved");
                AssertTrue(!UnifiedJson.GetBool(plugB, "enabled", true), "T66: plug-b disabled flag merged");
                AssertTrue(UnifiedJson.GetBool(plugLocal, "enabled", false), "T66: local-only-plug preserved");

                // Null-object resilience: local config.json has userSettings: null & plugins: { plug-a: null }
                string nullHome = Path.Combine(sb, "NullHome");
                string nullGemini = Path.Combine(nullHome, ".gemini");
                string nullCfgDir = Path.Combine(nullGemini, "config");
                Directory.CreateDirectory(nullCfgDir);
                File.WriteAllText(Path.Combine(nullCfgDir, "config.json"),
                    "{\"userSettings\":null,\"plugins\":{\"plug-a\":null}}", Encoding.UTF8);
                SelectiveImportRequest nullImpReq = new SelectiveImportRequest
                {
                    GeminiPath = nullGemini,
                    HomePath = nullHome,
                    SourceMode = RightPaneSourceMode.BackupArchive,
                    LoadedBackupPath = fastZip,
                    BackupIndex = fastIdx,
                    RestoreUserSettings = true
                };
                OperationResult<SelectiveImportResult> nullImpRes = ArchiveTransferService.PerformSelectiveImportResult(nullImpReq, null);
                AssertTrue(nullImpRes.Ok, "T66: RestoreUserSettings handles null userSettings/plugin objects cleanly: " + nullImpRes.ErrorMessage);
                Dictionary<string, object> nullParsed = UnifiedJson.AsDict(UnifiedJson.Parse(File.ReadAllText(Path.Combine(nullCfgDir, "config.json"), Encoding.UTF8)));
                AssertEqual("THEME_MODE_DARK", UnifiedJson.GetString(UnifiedJson.AsDict(nullParsed["userSettings"]), "themeMode", ""),
                    "T66: Null userSettings initialized and merged");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T67_Stage3_AntigravityCliAdapter_DiscoverParseTimeoutAndMissing()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t67_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(sb);

                // 1. Missing CLI returns clean CliNotFound without throwing
                CliAvailabilityResult missingAvail = AntigravityCliAdapter.CheckCliAvailability(Path.Combine(sb, "missing_agy.cmd"), "", "", "");
                AssertTrue(!missingAvail.Available && !string.IsNullOrEmpty(missingAvail.VersionOrHint), "T67: Missing CLI availability reports unavailable + hint");

                List<SessionRenameCandidate> batch = new List<SessionRenameCandidate>
                {
                    new SessionRenameCandidate
                    {
                        ConversationId = "11111111-2222-3333-4444-555555555555",
                        ProjectName = "Alpha",
                        OldTitle = "hello",
                        SuggestedPrefix = "【Alpha】",
                        CleanedPrompt = "Fix memory leak in worker"
                    }
                };

                CliBatchSuggestionResult notFoundRes = AntigravityCliAdapter.GenerateBatchSuggestionsViaCli(batch, Path.Combine(sb, "missing_agy.cmd"), 5, "", "", "");
                AssertTrue(!notFoundRes.Ok && notFoundRes.Status == CliSuggestStatus.CliNotFound, "T67: Missing CLI returns CliNotFound status cleanly");

                // 2. Mock agy.cmd returning JSON wrapper ({"result": "[...]"})
                string mockWrapPs = Path.Combine(sb, "mock_wrap.ps1");
                File.WriteAllText(mockWrapPs,
                    "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8\r\n" +
                    "if ($args -contains '--version') { [Console]::Out.WriteLine('agy 1.2.3-mock'); exit 0 }\r\n" +
                    "$stdinAll = [Console]::In.ReadToEnd()\r\n" +
                    "$inner = '[{\"cid\":\"11111111-2222-3333-4444-555555555555\",\"title\":\"【Alpha】修复内存泄漏\",\"summary\":\"排查工作线程内存问题\"}]'\r\n" +
                    "$wrap = @{ result = $inner } | ConvertTo-Json -Compress\r\n" +
                    "[Console]::Out.WriteLine($wrap)\r\n",
                    Encoding.UTF8);
                string agyWrapCmd = Path.Combine(sb, "agy.cmd");
                File.WriteAllText(agyWrapCmd,
                    "@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" + mockWrapPs + "\" %*\r\n",
                    new UTF8Encoding(false));

                CliAvailabilityResult okAvail = AntigravityCliAdapter.CheckCliAvailability(agyWrapCmd);
                AssertTrue(okAvail.Available && okAvail.ResolvedPath == Path.GetFullPath(agyWrapCmd), "T67: Discovers custom agy.cmd");

                CliBatchSuggestionResult wrapRes = AntigravityCliAdapter.GenerateBatchSuggestionsViaCli(batch, agyWrapCmd, 15);
                AssertTrue(wrapRes.Ok && wrapRes.Status == CliSuggestStatus.Success && wrapRes.Suggestions.Count == 1,
                    "T67: Parsed wrapper JSON output: " + wrapRes.ErrorOrHint);
                AssertEqual("【Alpha】修复内存泄漏", wrapRes.Suggestions[0].Title, "T67: Parsed title matches");
                AssertEqual("排查工作线程内存问题", wrapRes.Suggestions[0].Summary, "T67: Parsed summary matches");

                // 3. Mock agy.cmd returning fenced ```json block inside wrapper/text
                string mockFencePs = Path.Combine(sb, "mock_fence.ps1");
                File.WriteAllText(mockFencePs,
                    "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8\r\n" +
                    "$null = [Console]::In.ReadToEnd()\r\n" +
                    "[Console]::Out.WriteLine('Here are the suggestions:')\r\n" +
                    "[Console]::Out.WriteLine('```json')\r\n" +
                    "[Console]::Out.WriteLine('[{\"cid\":\"11111111-2222-3333-4444-555555555555\",\"title\":\"【Alpha】代码块建议\",\"summary\":\"Fenced JSON summary\"}]')\r\n" +
                    "[Console]::Out.WriteLine('```')\r\n",
                    Encoding.UTF8);
                string agyFenceCmd = Path.Combine(sb, "agy_fence.cmd");
                File.WriteAllText(agyFenceCmd,
                    "@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" + mockFencePs + "\" %*\r\n",
                    new UTF8Encoding(false));

                CliBatchSuggestionResult fenceRes = AntigravityCliAdapter.GenerateBatchSuggestionsViaCli(batch, agyFenceCmd, 15);
                AssertTrue(fenceRes.Ok && fenceRes.Suggestions.Count == 1, "T67: Parsed fenced ```json block output: " + fenceRes.ErrorOrHint);
                AssertEqual("【Alpha】代码块建议", fenceRes.Suggestions[0].Title, "T67: Fenced JSON title parsed");

                // 3b. NDJSON / multi-object stdout (earlier log JSON object precedes final result JSON object)
                string mockNdjsonPs = Path.Combine(sb, "mock_ndjson.ps1");
                File.WriteAllText(mockNdjsonPs,
                    "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8\r\n" +
                    "$null = [Console]::In.ReadToEnd()\r\n" +
                    "[Console]::Out.WriteLine('{\"type\":\"init\",\"session\":\"abc\"}')\r\n" +
                    "[Console]::Out.WriteLine('{\"type\":\"result\",\"result\":\"[{\\\"cid\\\":\\\"11111111-2222-3333-4444-555555555555\\\",\\\"title\\\":\\\"【Alpha】NDJSON流解析\\\",\\\"summary\\\":\\\"Multi-object NDJSON\\\"}]\"}')\r\n",
                    Encoding.UTF8);
                string agyNdjsonCmd = Path.Combine(sb, "agy_ndjson.cmd");
                File.WriteAllText(agyNdjsonCmd,
                    "@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" + mockNdjsonPs + "\" %*\r\n",
                    new UTF8Encoding(false));
                CliBatchSuggestionResult ndjsonRes = AntigravityCliAdapter.GenerateBatchSuggestionsViaCli(batch, agyNdjsonCmd, 15);
                AssertTrue(ndjsonRes.Ok && ndjsonRes.Suggestions.Count == 1, "T67: Parsed multi-object NDJSON output: " + ndjsonRes.ErrorOrHint);
                AssertEqual("【Alpha】NDJSON流解析", ndjsonRes.Suggestions[0].Title, "T67: NDJSON title parsed");

                // 4. Timeout kills spawned child process tree
                string pidFile = Path.Combine(sb, "agy_hang.pid");
                string mockHangPs = Path.Combine(sb, "mock_hang.ps1");
                File.WriteAllText(mockHangPs,
                    "[System.IO.File]::WriteAllText('" + pidFile.Replace("'", "''") + "', $PID.ToString())\r\n" +
                    "Start-Sleep -Seconds 30\r\n",
                    Encoding.UTF8);
                string agyHangCmd = Path.Combine(sb, "agy_hang.cmd");
                File.WriteAllText(agyHangCmd,
                    "@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" + mockHangPs + "\" %*\r\n",
                    new UTF8Encoding(false));

                CliBatchSuggestionResult timeoutRes = AntigravityCliAdapter.GenerateBatchSuggestionsViaCli(batch, agyHangCmd, 2);
                AssertTrue(!timeoutRes.Ok && timeoutRes.Status == CliSuggestStatus.Timeout, "T67: Hanging CLI returns Timeout status");

                if (File.Exists(pidFile))
                {
                    int hangPid;
                    if (int.TryParse(File.ReadAllText(pidFile).Trim(), out hangPid))
                    {
                        bool stillAlive = false;
                        try
                        {
                            using (Process p = Process.GetProcessById(hangPid))
                            {
                                stillAlive = !p.HasExited;
                            }
                        }
                        catch { stillAlive = false; }
                        AssertTrue(!stillAlive, "T67: Timed-out agy CLI child process terminated cleanly");
                    }
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T68_Stage3_HttpStorageAndCliEndpointsEndToEnd()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t68_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                string convDir = Path.Combine(agDir, "conversations");
                string projDir = Path.Combine(geminiDir, "config", "projects");
                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(projDir);

                string cidRoot = "r6800001-0000-4000-8000-000000000001";
                string cidSub = "s6800002-0000-4000-8000-000000000002";

                File.WriteAllText(Path.Combine(projDir, "pid-68.json"),
                    WorkspaceService.GenerateStandardProjectJson("pid-68", "Proj68", "C:\\Work\\Proj68"), Encoding.UTF8);

                ProtoNode nRoot = BuildRealProtoEntry(cidRoot, "new chat", null, "file:///c%3A/Work/Proj68", "Proj68", "pid-68", 4, 1, 1);
                ProtoNode nSub = BuildRealProtoEntry(cidSub, "Sub 68", null, "file:///c%3A/Work/Proj68", "P68", "pid-68", 5, 1, 0);
                nSub.GetChild(2).GetChild(17).Children.Add(new ProtoNode(11, 2) { RawBytes = Encoding.UTF8.GetBytes(cidRoot), IsModified = true });
                File.WriteAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb"), ProtoCodec.Encode(new List<ProtoNode> { nRoot, nSub }));

                IntPtr sumDb = OpenAndCreateSummaryWalDb(Path.Combine(agDir, "conversation_summaries.db"));
                try
                {
                    InsertSummaryDbRow(sumDb, cidRoot, "new chat", "p", 4, "[\"file:///c%3A/Work/Proj68\"]", "CASCADE_RUN_STATUS_IDLE", "pid-68", "", "", 0, 0, ProtoCodec.Encode(nRoot.Children));
                    InsertSummaryDbRow(sumDb, cidSub, "Sub 68", "p", 5, "[\"file:///c%3A/Work/Proj68\"]", "CASCADE_RUN_STATUS_IDLE", "pid-68", "coder", cidRoot, 1, 0, ProtoCodec.Encode(nSub.Children));
                }
                finally
                {
                    t_sqlite3_close(sumDb);
                }

                CreateSampleLiposuctionDb(Path.Combine(convDir, cidRoot + ".db"), cidRoot, 1, 128, 4);
                string subDbPath = Path.Combine(convDir, cidSub + ".db");
                CreateSampleLiposuctionDb(subDbPath, cidSub, 60, 100 * 1024, 5);
                long subBefore = new FileInfo(subDbPath).Length;

                // Mock agy.cmd for HTTP CLI status & suggest tests
                string mockPs = Path.Combine(sb, "mock_http_agy.ps1");
                File.WriteAllText(mockPs,
                    "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8\r\n" +
                    "if ($args -contains '--version') { [Console]::Out.WriteLine('agy-http 1.0'); exit 0 }\r\n" +
                    "$null = [Console]::In.ReadToEnd()\r\n" +
                    "[Console]::Out.WriteLine('[{\"cid\":\"" + cidRoot + "\",\"title\":\"【Proj68】HTTP建议标题\",\"summary\":\"CLI 摘要\"}]')\r\n",
                    Encoding.UTF8);
                string mockCmd = Path.Combine(sb, "agy.cmd");
                File.WriteAllText(mockCmd,
                    "@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" + mockPs + "\" %*\r\n",
                    new UTF8Encoding(false));

                string token = "t68_tok_e2e";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, Path.Combine(sb, "Repo"), 0, token))
                {
                    host.Start();

                    // 1. GET /api/storage/analyze
                    RawHttpTestResponse anResp = SendRawTcpHttp(host.Port, null, "GET", "/api/storage/analyze?minAgeDays=7&minSizeMb=5", apiHdr, null);
                    AssertEqual(200, anResp.StatusCode, "T68: GET /api/storage/analyze returns 200");
                    AssertTrue(anResp.BodyText.Contains(cidSub) && anResp.BodyText.Contains("\"eligibleForSlim\":true"),
                        "T68: Storage analyze surfaces eligible subagent DB");

                    // 2. POST /api/storage/slim (ACC-01: blocks when IDE running unless closed, succeeds when IDE not running)
                    host.IdeRunningOverride = delegate() { return true; };
                    RawHttpTestResponse slimIde409 = SendRawTcpHttp(host.Port, null, "POST", "/api/storage/slim", apiHdr,
                        "{\"mode\":\"liposuction\",\"minAgeDays\":7,\"minSizeMb\":5,\"verifyPreFlight\":true}");
                    AssertEqual(409, slimIde409.StatusCode, "T68: POST /api/storage/slim returns 409 IDE_RUNNING when IDE running and closeIdeIfRunning=false");
                    AssertTrue(slimIde409.BodyText.Contains("\"code\":\"IDE_RUNNING\""), "T68: Slim 409 body indicates code=IDE_RUNNING");

                    host.IdeRunningOverride = delegate() { return false; };
                    RawHttpTestResponse slimResp = SendRawTcpHttp(host.Port, null, "POST", "/api/storage/slim", apiHdr,
                        "{\"mode\":\"liposuction\",\"minAgeDays\":7,\"minSizeMb\":5,\"verifyPreFlight\":true}");
                    AssertEqual(200, slimResp.StatusCode, "T68: POST /api/storage/slim returns 200: " + slimResp.BodyText);
                    AssertTrue(slimResp.BodyText.Contains("\"processedCount\":1") && slimResp.BodyText.Contains("\"preFlightPassed\":true"),
                        "T68: Storage slim processed 1 subagent with pre-flight pass");
                    AssertTrue(new FileInfo(subDbPath).Length < subBefore / 10, "T68: Subagent DB shrank via HTTP slim");
                    host.IdeRunningOverride = null;

                    // 3. GET /api/rename/cli-status
                    RawHttpTestResponse cliStatResp = SendRawTcpHttp(host.Port, null, "GET", "/api/rename/cli-status?customCliPath=" + Uri.EscapeDataString(mockCmd), apiHdr, null);
                    AssertEqual(200, cliStatResp.StatusCode, "T68: GET /api/rename/cli-status returns 200");
                    AssertTrue(cliStatResp.BodyText.Contains("\"available\":true") && cliStatResp.BodyText.Contains("agy-http 1.0"),
                        "T68: CLI status reports available with version");

                    // 4. POST /api/rename/cli-suggest
                    RawHttpTestResponse cliSugResp = SendRawTcpHttp(host.Port, null, "POST", "/api/rename/cli-suggest", apiHdr,
                        "{\"cids\":[\"" + cidRoot + "\"],\"customCliPath\":\"" + UnifiedJson.EscapeString(mockCmd) + "\",\"timeoutSec\":15}");
                    AssertEqual(200, cliSugResp.StatusCode, "T68: POST /api/rename/cli-suggest returns 200");
                    AssertTrue(cliSugResp.BodyText.Contains("\"ok\":true") && cliSugResp.BodyText.Contains("HTTP建议标题"),
                        "T68: CLI suggest returned structured suggestion via HTTP");

                    // 5. HTTP 409 busy-job lock contention on /api/storage/slim and /api/rename/cli-suggest
                    AssertTrue(host.TryAcquireJob("t68_lock", "Contention test"), "T68: Acquired simulated job lock");
                    try
                    {
                        RawHttpTestResponse slim409 = SendRawTcpHttp(host.Port, null, "POST", "/api/storage/slim", apiHdr,
                            "{\"mode\":\"liposuction\"}");
                        AssertEqual(409, slim409.StatusCode, "T68: POST /api/storage/slim returns 409 when job busy");

                        RawHttpTestResponse cli409 = SendRawTcpHttp(host.Port, null, "POST", "/api/rename/cli-suggest", apiHdr,
                            "{\"cids\":[\"" + cidRoot + "\"]}");
                        AssertEqual(409, cli409.StatusCode, "T68: POST /api/rename/cli-suggest returns 409 when job busy");
                    }
                    finally
                    {
                        host.ReleaseJob();
                    }

                    // 6. HTTP POST /api/backup/full (tier=fast vs tier=full) and POST /api/backup/import (restoreUserSettings=true)
                    string cfgDir68 = Path.Combine(geminiDir, "config");
                    Directory.CreateDirectory(cfgDir68);
                    string cfgPath68 = Path.Combine(cfgDir68, "config.json");
                    File.WriteAllText(cfgPath68,
                        "{\"userSettings\":{\"themeMode\":\"THEME_MODE_DARK\",\"fromArchive\":true},\"plugins\":{\"plug-68\":{\"enabled\":true}}}",
                        Encoding.UTF8);

                    string fastHttpZip = Path.Combine(sb, "http_fast.agy");
                    string fullHttpZip = Path.Combine(sb, "http_full.agy");

                    // Always verify IDE_RUNNING 409 guard via IdeRunningOverride=true (zero environment-conditional skips)
                    host.IdeRunningOverride = delegate() { return true; };
                    RawHttpTestResponse bkIde409 = SendRawTcpHttp(host.Port, null, "POST", "/api/backup/full", apiHdr,
                        "{\"outputPath\":\"" + UnifiedJson.EscapeString(fastHttpZip) + "\",\"tier\":\"fast\"}");
                    AssertEqual(409, bkIde409.StatusCode, "T68: POST /api/backup/full returns 409 IDE_RUNNING when IdeRunningOverride=true");
                    AssertTrue(bkIde409.BodyText.Contains("\"code\":\"IDE_RUNNING\""), "T68: Backup 409 payload indicates code=IDE_RUNNING");
                    AssertTrue(!File.Exists(fastHttpZip), "T68: Blocked POST /api/backup/full did not create archive file");

                    // Always verify full backup/open/import flow via IdeRunningOverride=false
                    host.IdeRunningOverride = delegate() { return false; };
                    RawHttpTestResponse bkFast = SendRawTcpHttp(host.Port, null, "POST", "/api/backup/full", apiHdr,
                        "{\"outputPath\":\"" + UnifiedJson.EscapeString(fastHttpZip) + "\",\"tier\":\"fast\"}");
                    AssertEqual(202, bkFast.StatusCode, "T68: POST /api/backup/full (tier=fast) returns 202");
                    for (int w = 0; w < 100 && !File.Exists(fastHttpZip); w++) System.Threading.Thread.Sleep(50);
                    for (int w = 0; w < 100; w++)
                    {
                        RawHttpTestResponse jr = SendRawTcpHttp(host.Port, null, "GET", "/api/jobs/current", apiHdr, null);
                        if (jr.BodyText.Contains("\"busy\":false")) break;
                        System.Threading.Thread.Sleep(50);
                    }
                    BackupArchiveIndex fastHttpIdx = StreamingZipIndexReader.ReadIndex(fastHttpZip);
                    AssertEqual("FastDaily", fastHttpIdx.BackupTier, "T68: HTTP fast backup recorded FastDaily tier");
                    AssertTrue(fastHttpIdx.ExistingConvoDbFiles.Contains(cidRoot) && !fastHttpIdx.ExistingConvoDbFiles.Contains(cidSub),
                        "T68: HTTP FastDaily excluded subagent DB");

                    RawHttpTestResponse bkFull = SendRawTcpHttp(host.Port, null, "POST", "/api/backup/full", apiHdr,
                        "{\"outputPath\":\"" + UnifiedJson.EscapeString(fullHttpZip) + "\",\"tier\":\"full\"}");
                    AssertEqual(202, bkFull.StatusCode, "T68: POST /api/backup/full (tier=full) returns 202");
                    for (int w = 0; w < 100; w++)
                    {
                        RawHttpTestResponse jr = SendRawTcpHttp(host.Port, null, "GET", "/api/jobs/current", apiHdr, null);
                        if (jr.BodyText.Contains("\"busy\":false") && File.Exists(fullHttpZip)) break;
                        System.Threading.Thread.Sleep(50);
                    }
                    BackupArchiveIndex fullHttpIdx = StreamingZipIndexReader.ReadIndex(fullHttpZip);
                    AssertEqual("FullForensic", fullHttpIdx.BackupTier, "T68: HTTP full backup recorded FullForensic tier");
                    AssertTrue(fullHttpIdx.ExistingConvoDbFiles.Contains(cidSub), "T68: HTTP FullForensic included subagent DB");

                    // Mutate local config.json, open fastHttpZip, verify import guard on IDE running, then restore via POST /api/backup/import with restoreUserSettings=true
                    File.WriteAllText(cfgPath68,
                        "{\"userSettings\":{\"themeMode\":\"THEME_MODE_LIGHT\",\"localOnlyKeep\":\"yes\"},\"plugins\":{\"plug-68\":{\"enabled\":false}}}",
                        Encoding.UTF8);
                    RawHttpTestResponse openFast = SendRawTcpHttp(host.Port, null, "POST", "/api/backup/open", apiHdr,
                        "{\"archivePath\":\"" + UnifiedJson.EscapeString(fastHttpZip) + "\"}");
                    AssertEqual(200, openFast.StatusCode, "T68: POST /api/backup/open succeeded");
                    AssertTrue(openFast.BodyText.Contains("\"backupTier\":\"FastDaily\""), "T68: /api/backup/open returns backupTier");

                    host.IdeRunningOverride = delegate() { return true; };
                    RawHttpTestResponse impIde409 = SendRawTcpHttp(host.Port, null, "POST", "/api/backup/import", apiHdr,
                        "{\"cids\":[],\"capKeys\":[],\"restoreUserSettings\":true}");
                    AssertEqual(409, impIde409.StatusCode, "T68: POST /api/backup/import (restoreUserSettings=true) returns 409 IDE_RUNNING when IDE running");
                    AssertTrue(impIde409.BodyText.Contains("\"code\":\"IDE_RUNNING\""), "T68: Backup import 409 payload indicates code=IDE_RUNNING");

                    host.IdeRunningOverride = delegate() { return false; };
                    RawHttpTestResponse impRestore = SendRawTcpHttp(host.Port, null, "POST", "/api/backup/import", apiHdr,
                        "{\"cids\":[],\"capKeys\":[],\"restoreUserSettings\":true}");
                    AssertEqual(202, impRestore.StatusCode, "T68: POST /api/backup/import (restoreUserSettings=true) returns 202");
                    bool restoreJobDone = false;
                    for (int w = 0; w < 100; w++)
                    {
                        RawHttpTestResponse jr = SendRawTcpHttp(host.Port, null, "GET", "/api/jobs/current", apiHdr, null);
                        if (jr.BodyText.Contains("\"busy\":false"))
                        {
                            restoreJobDone = true;
                            break;
                        }
                        System.Threading.Thread.Sleep(50);
                    }
                    AssertTrue(restoreJobDone, "T68: HTTP restoreUserSettings background job completed");
                    Dictionary<string, object> restoredCfg = UnifiedJson.AsDict(UnifiedJson.Parse(File.ReadAllText(cfgPath68, Encoding.UTF8)));
                    Dictionary<string, object> restoredUs = UnifiedJson.AsDict(restoredCfg["userSettings"]);
                    Dictionary<string, object> restoredPlug = UnifiedJson.AsDict(UnifiedJson.AsDict(restoredCfg["plugins"])["plug-68"]);
                    AssertEqual("THEME_MODE_DARK", UnifiedJson.GetString(restoredUs, "themeMode", ""), "T68: HTTP restoreUserSettings updated themeMode");
                    AssertTrue(UnifiedJson.GetBool(restoredUs, "fromArchive", false), "T68: HTTP restoreUserSettings merged archive fromArchive key");
                    AssertEqual("yes", UnifiedJson.GetString(restoredUs, "localOnlyKeep", ""), "T68: HTTP restoreUserSettings preserved localOnlyKeep");
                    AssertTrue(UnifiedJson.GetBool(restoredPlug, "enabled", false), "T68: HTTP restoreUserSettings updated plugin enabled flag");
                    host.IdeRunningOverride = null;

                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T69_Stage3_LegacyCliArchivePruneAndCompleteOrphanGc()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t69_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                string cliDir = Path.Combine(geminiDir, "antigravity-cli");
                Directory.CreateDirectory(Path.Combine(agDir, "conversations"));
                Directory.CreateDirectory(Path.Combine(agDir, "brain"));
                Directory.CreateDirectory(Path.Combine(agDir, "annotations"));
                Directory.CreateDirectory(cliDir);

                string cidActive = "a6900001-0000-4000-8000-000000000001";
                string cidOrphan = "f6900002-0000-4000-8000-000000000002";
                string cidLegacy = "c6900003-0000-4000-8000-000000000003";

                ProtoNode nActive = BuildRealProtoEntry(cidActive, "Active 69", null, "file:///c%3A/P69", "P69", "pid-69", 3, 1, 1);
                File.WriteAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb"), ProtoCodec.Encode(new List<ProtoNode> { nActive }));

                string activeDb = Path.Combine(agDir, "conversations", cidActive + ".db");
                CreateMinimalValidSqliteDb(activeDb, cidActive);
                File.WriteAllText(Path.Combine(agDir, "annotations", cidActive + ".pbtxt"), "title:\"Active 69\"", Encoding.UTF8);
                Directory.CreateDirectory(Path.Combine(agDir, "brain", cidActive));
                File.WriteAllText(Path.Combine(agDir, "brain", cidActive, "plan.md"), "# Keep", Encoding.UTF8);

                // Leftover active .db.bak and stale .db-journal + orphan .pbtxt + orphan brain/ + orphan .db.bak
                string activeBak = Path.Combine(agDir, "conversations", cidActive + ".db.bak");
                string activeJournal = Path.Combine(agDir, "conversations", cidActive + ".db-journal");
                string orphanDbBak = Path.Combine(agDir, "conversations", cidOrphan + ".db.bak");
                string orphanPbtxt = Path.Combine(agDir, "annotations", cidOrphan + ".pbtxt");
                string orphanBrain = Path.Combine(agDir, "brain", cidOrphan);

                File.WriteAllText(activeBak, "BAK", Encoding.UTF8);
                File.WriteAllText(activeJournal, "JRN", Encoding.UTF8);
                File.WriteAllText(orphanDbBak, "OBAK", Encoding.UTF8);
                File.WriteAllText(orphanPbtxt, "title:\"Orphan\"", Encoding.UTF8);
                Directory.CreateDirectory(orphanBrain);
                File.WriteAllText(Path.Combine(orphanBrain, "old.md"), "# Orphan", Encoding.UTF8);

                // Legacy antigravity-cli/conversation_summaries.db
                string legacyCliDbPath = Path.Combine(cliDir, "conversation_summaries.db");
                IntPtr ldb = OpenAndCreateSummaryWalDb(legacyCliDbPath);
                try
                {
                    ProtoNode nLeg = BuildRealProtoEntry(cidLegacy, "Legacy Ghost", null, null, "default-cli-project", "default-cli-project", 2, 1, 1);
                    InsertSummaryDbRow(ldb, cidLegacy, "Legacy Ghost", "ghost", 2, "[]", "CASCADE_RUN_STATUS_IDLE", "default-cli-project", "", "", 0, 0, ProtoCodec.Encode(nLeg.Children));
                }
                finally
                {
                    t_sqlite3_close(ldb);
                }

                StorageAnalysisReport rep = StorageSlimService.AnalyzeStorage(homeDir, geminiDir);
                AssertTrue(rep.LegacyCliArchive.Exists && rep.LegacyCliArchive.EntryCount == 1, "T69: AnalyzeStorage surfaced legacy CLI archive ghost entry");
                AssertTrue(rep.Orphans.OrphanDbBakFiles.Count == 2, "T69: AnalyzeStorage detected both active leftover and orphan .db.bak");
                AssertTrue(rep.Orphans.OrphanDbJournalFiles.Count == 1, "T69: AnalyzeStorage detected stale .db-journal");
                AssertTrue(rep.Orphans.OrphanAnnotationFiles.Count == 1, "T69: AnalyzeStorage detected orphan .pbtxt");
                AssertTrue(rep.Orphans.OrphanBrainDirs.Count == 1, "T69: AnalyzeStorage detected orphan brain dir");

                StorageSlimRequest gcReq = new StorageSlimRequest();
                gcReq.Mode = StorageSlimMode.OrphanGcOnly;
                gcReq.CleanOrphans = true;
                gcReq.PruneLegacyCliArchive = true;
                StorageSlimResult gcRes = StorageSlimService.ExecuteStorageSlim(homeDir, geminiDir, gcReq);

                AssertTrue(gcRes.Ok && gcRes.LegacyCliPruned, "T69: Legacy CLI archive pruned");
                AssertTrue(File.Exists(gcRes.LegacyCliArchivePath), "T69: Legacy CLI cold archive .agy created before prune");
                using (ZipArchive legZip = ZipFile.OpenRead(gcRes.LegacyCliArchivePath))
                {
                    AssertTrue(legZip.GetEntry("data/antigravity-cli/conversation_summaries.db") != null, "T69: Legacy CLI cold archive contains conversation_summaries.db");
                }
                AssertTrue(!File.Exists(legacyCliDbPath), "T69: Legacy antigravity-cli/conversation_summaries.db deleted after cold archive");

                AssertTrue(!File.Exists(activeBak), "T69: Leftover .db.bak cleaned");
                AssertTrue(!File.Exists(activeJournal), "T69: Stale .db-journal cleaned");
                AssertTrue(!File.Exists(orphanDbBak), "T69: Orphan .db.bak cleaned");
                AssertTrue(!File.Exists(orphanPbtxt), "T69: Orphan .pbtxt cleaned");
                AssertTrue(!Directory.Exists(orphanBrain), "T69: Orphan brain/ cleaned");

                // Active session assets preserved intact
                AssertTrue(File.Exists(activeDb), "T69: Active .db preserved");
                AssertTrue(File.Exists(Path.Combine(agDir, "annotations", cidActive + ".pbtxt")), "T69: Active .pbtxt preserved");
                AssertTrue(File.Exists(Path.Combine(agDir, "brain", cidActive, "plan.md")), "T69: Active brain preserved");
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T70_Stage4_ScheduledSnapshotService_SettingsPersistenceRunAndRollingCleanup()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t70_" + Guid.NewGuid().ToString("N"));
            string testRegSubKey = @"Software\AntigravityCompanionTest_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                Directory.CreateDirectory(Path.Combine(agDir, "conversations"));

                string cidRoot = "a7000001-0000-4000-8000-000000000001";
                string cidSub = "b7000002-0000-4000-8000-000000000002";
                ProtoNode nRoot = BuildRealProtoEntry(cidRoot, "Root 70", null, "file:///c%3A/P70", "P70", "pid-70", 3, 1, 1);
                ProtoNode nSub = BuildRealProtoEntry(cidSub, "Sub 70", null, "file:///c%3A/P70", "P70", "pid-70", 4, 1, 0);
                nSub.GetChild(2).GetChild(17).Children.Add(new ProtoNode(11, 2) { RawBytes = Encoding.UTF8.GetBytes(cidRoot), IsModified = true });
                File.WriteAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb"), ProtoCodec.Encode(new List<ProtoNode> { nRoot, nSub }));
                CreateMinimalValidSqliteDb(Path.Combine(agDir, "conversations", cidRoot + ".db"), cidRoot);
                CreateMinimalValidSqliteDb(Path.Combine(agDir, "conversations", cidSub + ".db"), cidSub);

                string settingsPath = Path.Combine(sb, "custom_companion_settings.json");
                string snapDir = Path.Combine(sb, "snapshots");

                bool busyToggle = true;
                using (ScheduledSnapshotService svc = new ScheduledSnapshotService(
                    homeDir,
                    geminiDir,
                    settingsPath,
                    delegate(string jt, string msg) { return !busyToggle; },
                    delegate() { },
                    null))
                {
                    CompanionSettings cfg = new CompanionSettings();
                    cfg.Enabled = false;
                    cfg.IntervalHours = 6;
                    cfg.TargetDirectory = snapDir;
                    cfg.RetainCount = 2;
                    cfg.Tier = "fast";
                    cfg.MinimizeToTray = true;
                    cfg.GlobalHotkey = "Ctrl+Shift+A";
                    cfg.CustomAgyCliPath = @"C:\Tools\agy.cmd";
                    cfg.VaultPath = Path.Combine(sb, "MyVault");
                    cfg.StartWithWindows = true;
                    svc.UpdateSettings(cfg);

                    AssertTrue(File.Exists(settingsPath), "T70: Settings file persisted to override path");

                    // Busy job skips cleanly without creating archive
                    SnapshotRunResult busyRes = svc.RunSnapshotNow();
                    AssertTrue(!busyRes.Ok && busyRes.SkippedBusy, "T70: Skips cleanly when TryAcquireJob reports busy");
                    AssertTrue(!Directory.Exists(snapDir) || Directory.GetFiles(snapDir, "snapshot_*.agy").Length == 0,
                        "T70: No snapshot created when busy");

                    // Now allow job acquisition and run 3 snapshots to verify retainCount=2 rolling cleanup
                    busyToggle = false;
                    SnapshotRunResult r1 = svc.RunSnapshotNow();
                    AssertTrue(r1.Ok && File.Exists(r1.FilePath), "T70: First FastDaily snapshot succeeded: " + r1.ErrorMessage);
                    System.Threading.Thread.Sleep(30);
                    SnapshotRunResult r2 = svc.RunSnapshotNow();
                    AssertTrue(r2.Ok && File.Exists(r2.FilePath), "T70: Second snapshot succeeded");
                    System.Threading.Thread.Sleep(30);
                    SnapshotRunResult r3 = svc.RunSnapshotNow();
                    AssertTrue(r3.Ok && File.Exists(r3.FilePath), "T70: Third snapshot succeeded");
                    AssertTrue(r3.DeletedOldCount >= 1, "T70: Third snapshot pruned older snapshot beyond retainCount=2");

                    string[] remaining = Directory.GetFiles(snapDir, "snapshot_*.agy");
                    AssertEqual(2, remaining.Length, "T70: retainCount=2 keeps exact 2 newest snapshot_*.agy files");

                    BackupArchiveIndex idx = StreamingZipIndexReader.ReadIndex(r3.FilePath);
                    AssertEqual("FastDaily", idx.BackupTier, "T70: Snapshot recorded FastDaily tier");
                    AssertTrue(idx.ExistingConvoDbFiles.Contains(cidRoot), "T70: Snapshot includes root IDE DB");
                    AssertTrue(!idx.ExistingConvoDbFiles.Contains(cidSub), "T70: FastDaily snapshot excludes subagent DB");
                }

                // Reload settings from disk in a fresh service instance
                using (ScheduledSnapshotService svcReload = new ScheduledSnapshotService(homeDir, geminiDir, settingsPath))
                {
                    CompanionSettings loaded = svcReload.GetSettings();
                    AssertEqual(6, loaded.IntervalHours, "T70: Reloaded IntervalHours");
                    AssertEqual(2, loaded.RetainCount, "T70: Reloaded RetainCount");
                    AssertEqual("Ctrl+Shift+A", loaded.GlobalHotkey, "T70: Reloaded GlobalHotkey");
                    AssertEqual(@"C:\Tools\agy.cmd", loaded.CustomAgyCliPath, "T70: Reloaded CustomAgyCliPath");
                    AssertTrue(!string.IsNullOrEmpty(loaded.LastRunUtc) && loaded.LastSizeBytes > 0, "T70: Reloaded LastRunUtc & LastSizeBytes");
                }

                // Isolated HKCU registry Run-key verification
                bool regSet = ScheduledSnapshotService.SetStartWithWindowsInKey(
                    Microsoft.Win32.Registry.CurrentUser, testRegSubKey, "AntigravityCompanion", true, @"C:\App\Antigravity_Companion.exe");
                AssertTrue(regSet, "T70: Wrote isolated HKCU Run test value");
                AssertTrue(ScheduledSnapshotService.IsStartWithWindowsEnabledInKey(
                    Microsoft.Win32.Registry.CurrentUser, testRegSubKey, "AntigravityCompanion"),
                    "T70: Verified Run test value enabled");
                bool regClear = ScheduledSnapshotService.SetStartWithWindowsInKey(
                    Microsoft.Win32.Registry.CurrentUser, testRegSubKey, "AntigravityCompanion", false, null);
                AssertTrue(regClear && !ScheduledSnapshotService.IsStartWithWindowsEnabledInKey(
                    Microsoft.Win32.Registry.CurrentUser, testRegSubKey, "AntigravityCompanion"),
                    "T70: Verified Run test value removed");
            }
            finally
            {
                try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(testRegSubKey, false); } catch { }
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T71_Stage4_HttpCompanionSettingsAndSnapshotEndpointsEndToEnd()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t71_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                Directory.CreateDirectory(Path.Combine(agDir, "conversations"));

                string cid = "a7100001-0000-4000-8000-000000000001";
                ProtoNode n1 = BuildRealProtoEntry(cid, "HTTP Snap 71", null, "file:///c%3A/P71", "P71", "pid-71", 2, 1, 1);
                File.WriteAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb"), ProtoCodec.Encode(new List<ProtoNode> { n1 }));
                CreateMinimalValidSqliteDb(Path.Combine(agDir, "conversations", cid + ".db"), cid);

                string token = "t71_tok_e2e";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, Path.Combine(sb, "Vault"), 0, token))
                {
                    host.Start();

                    // 1. GET /api/settings/companion
                    RawHttpTestResponse getDef = SendRawTcpHttp(host.Port, null, "GET", "/api/settings/companion", apiHdr, null);
                    AssertEqual(200, getDef.StatusCode, "T71: GET /api/settings/companion returns 200");
                    AssertTrue(getDef.BodyText.Contains("\"intervalHours\":12") &&
                               getDef.BodyText.Contains("\"globalHotkey\":\"\"") &&
                               getDef.BodyText.Contains("\"minimizeToTray\":false") &&
                               getDef.BodyText.Contains("\"ruleTokenReferenceThreshold\":0"),
                        "T71: Default companion settings returned with hardened Wave 1 defaults");

                    // 2. Invalid hotkey rejected with 400
                    RawHttpTestResponse badHk = SendRawTcpHttp(host.Port, null, "POST", "/api/settings/companion", apiHdr,
                        "{\"globalHotkey\":\"InvalidOnlyKey\"}");
                    AssertEqual(400, badHk.StatusCode, "T71: Invalid globalHotkey rejected with HTTP 400");

                    // 3. POST /api/settings/companion valid update + clearing hotkey back to empty
                    string customSnapDir = Path.Combine(sb, "CustomSnaps");
                    RawHttpTestResponse postCfg = SendRawTcpHttp(host.Port, null, "POST", "/api/settings/companion", apiHdr,
                        "{\"enabled\":true,\"intervalHours\":6,\"retainCount\":3,\"tier\":\"fast\",\"minimizeToTray\":false,\"globalHotkey\":\"Ctrl+Alt+K\",\"ruleTokenReferenceThreshold\":1500,\"targetDirectory\":\"" + UnifiedJson.EscapeString(customSnapDir) + "\"}");
                    AssertEqual(200, postCfg.StatusCode, "T71: POST /api/settings/companion returns 200: " + postCfg.BodyText);
                    AssertTrue(postCfg.BodyText.Contains("\"intervalHours\":6") && postCfg.BodyText.Contains("\"globalHotkey\":\"Ctrl+Alt+K\"") && postCfg.BodyText.Contains("\"ruleTokenReferenceThreshold\":1500"),
                        "T71: Updated companion settings echoed back");

                    RawHttpTestResponse clearHk = SendRawTcpHttp(host.Port, null, "POST", "/api/settings/companion", apiHdr,
                        "{\"globalHotkey\":\"\"}");
                    AssertEqual(200, clearHk.StatusCode, "T71: Clearing globalHotkey to empty returns 200");
                    AssertTrue(clearHk.BodyText.Contains("\"globalHotkey\":\"\""), "T71: Empty globalHotkey persisted without Ctrl+Alt+A fallback");

                    // 4. POST /api/snapshots/run-now
                    RawHttpTestResponse runNow = SendRawTcpHttp(host.Port, null, "POST", "/api/snapshots/run-now", apiHdr, "{}");
                    AssertEqual(200, runNow.StatusCode, "T71: POST /api/snapshots/run-now returns 200: " + runNow.BodyText);
                    Dictionary<string, object> runDict = UnifiedJson.AsDict(UnifiedJson.Parse(runNow.BodyText));
                    string snapFile = UnifiedJson.GetString(runDict, "filePath", string.Empty);
                    AssertTrue(UnifiedJson.GetBool(runDict, "ok", false) && !string.IsNullOrEmpty(snapFile) && File.Exists(snapFile),
                        "T71: Snapshot archive created on disk");

                    // 5. GET /api/snapshots/status
                    RawHttpTestResponse stResp = SendRawTcpHttp(host.Port, null, "GET", "/api/snapshots/status", apiHdr, null);
                    AssertEqual(200, stResp.StatusCode, "T71: GET /api/snapshots/status returns 200");
                    AssertTrue(stResp.BodyText.Contains("\"existingCount\":1") && stResp.BodyText.Contains(Path.GetFileName(snapFile)),
                        "T71: Snapshot status reports created archive");

                    // 6. Busy job contention returns 409
                    AssertTrue(host.TryAcquireJob("t71_lock", "Lock test"), "T71: Acquired host job lock");
                    try
                    {
                        RawHttpTestResponse busyResp = SendRawTcpHttp(host.Port, null, "POST", "/api/snapshots/run-now", apiHdr, "{}");
                        AssertEqual(409, busyResp.StatusCode, "T71: POST /api/snapshots/run-now returns 409 when busy");
                    }
                    finally
                    {
                        host.ReleaseJob();
                    }

                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T72_Stage4_HttpEmbeddedUiAssetsServed()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t72_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                Directory.CreateDirectory(Path.Combine(geminiDir, "antigravity"));

                string token = "t72_tok_ui";
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, Path.Combine(sb, "Vault"), 0, token))
                {
                    host.Start();

                    RawHttpTestResponse boot = SendRawTcpHttp(host.Port, null, "GET", "/?t=" + token, null, null);
                    AssertEqual(200, boot.StatusCode, "T72: GET /?t=token returns 200");
                    AssertTrue(boot.BodyText.Contains("Antigravity Companion V1.0"), "T72: index.html has V1.0 title");
                    AssertTrue(boot.BodyText.Contains("/js/views/backup.js"), "T72: index.html references modular views");

                    string sidName = "agy_sid_" + host.Port;
                    Dictionary<string, string> cookieHdr = new Dictionary<string, string> { { "Cookie", sidName + "=" + token } };

                    string[] assetPaths = new string[]
                    {
                        "/index.html",
                        "/styles.css",
                        "/app.js",
                        "/js/api.js",
                        "/js/state.js",
                        "/js/components.js",
                        "/js/views/overview.js",
                        "/js/views/cockpit.js",
                        "/js/views/sessions.js",
                        "/js/views/rename.js",
                        "/js/views/storage.js",
                        "/js/views/vault.js",
                        "/js/views/backup.js",
                        "/js/views/settings.js"
                    };

                    for (int i = 0; i < assetPaths.Length; i++)
                    {
                        string ap = assetPaths[i];
                        RawHttpTestResponse r = SendRawTcpHttp(host.Port, null, "GET", ap, cookieHdr, null);
                        AssertEqual(200, r.StatusCode, "T72: Asset " + ap + " returns HTTP 200");
                        AssertTrue(r.BodyByteCount > 100, "T72: Asset " + ap + " is non-empty (" + r.BodyByteCount + " bytes)");
                        AssertTrue(!r.BodyText.Contains("Missing UI asset"), "T72: Asset " + ap + " loaded from embedded resource without missing fallback");
                    }

                    RawHttpTestResponse vsResp = SendRawTcpHttp(host.Port, null, "GET", "/api/vault/status", cookieHdr, null);
                    AssertEqual(200, vsResp.StatusCode, "T72: GET /api/vault/status returns 200");
                    AssertTrue(vsResp.BodyText.Contains("\"vaultPath\""), "T72: Vault status includes vaultPath");

                    RawHttpTestResponse favResp = SendRawTcpHttp(host.Port, null, "GET", "/favicon.ico", null, null);
                    AssertEqual(204, favResp.StatusCode, "T72: GET /favicon.ico returns 204 No Content cleanly");

                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T73_Stage4_TrayHost_HotkeyParserAndMenuState()
        {
            uint mods, vk;
            AssertTrue(TrayHost.TryParseHotkey("Ctrl+Alt+A", out mods, out vk), "T73: Parses Ctrl+Alt+A");
            AssertEqual(TrayHost.MOD_CONTROL | TrayHost.MOD_ALT, mods, "T73: Ctrl+Alt modifiers exact");
            AssertEqual((uint)'A', vk, "T73: Key 'A' virtual key exact");

            AssertTrue(TrayHost.TryParseHotkey("ctrl + shift + f12", out mods, out vk), "T73: Parses ctrl + shift + f12");
            AssertEqual(TrayHost.MOD_CONTROL | TrayHost.MOD_SHIFT, mods, "T73: Ctrl+Shift modifiers exact");
            AssertEqual((uint)0x7B, vk, "T73: F12 virtual key exact");

            AssertTrue(TrayHost.TryParseHotkey("Win+Alt+Space", out mods, out vk), "T73: Parses Win+Alt+Space");
            AssertEqual(TrayHost.MOD_WIN | TrayHost.MOD_ALT, mods, "T73: Win+Alt modifiers exact");
            AssertEqual((uint)0x20, vk, "T73: Space virtual key exact");

            AssertTrue(TrayHost.TryParseHotkey("F9", out mods, out vk) && mods == 0 && vk == 0x78, "T73: Standalone F9 allowed");
            AssertTrue(!TrayHost.TryParseHotkey("", out mods, out vk), "T73: Empty hotkey rejected");
            AssertTrue(!TrayHost.TryParseHotkey("A", out mods, out vk), "T73: Single letter without modifier rejected");
            AssertTrue(!TrayHost.TryParseHotkey("Ctrl+", out mods, out vk), "T73: Trailing plus rejected");
            AssertTrue(!TrayHost.TryParseHotkey("Ctrl+NotAValidKeyName", out mods, out vk), "T73: Unknown key rejected");
            AssertTrue(!TrayHost.TryParseHotkey("Ctrl+A+B", out mods, out vk), "T73: Multiple non-modifier keys rejected");

            string sb = Path.Combine(Path.GetTempPath(), "agy_t73_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                Directory.CreateDirectory(geminiDir);
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, Path.Combine(sb, "Vault"), 0, "t73_tok"))
                using (TrayHost tray = new TrayHost(host, "http://127.0.0.1:12345/?t=t73_tok", false, false))
                {
                    string[] items = tray.MenuItemTexts;
                    AssertEqual(4, items.Length, "T73: Tray menu exposes 4 action items");
                    AssertEqual("打开驾驶舱", items[0], "T73: Menu item 0 matches");
                    AssertEqual("立即执行极速快照", items[1], "T73: Menu item 1 matches");
                    AssertEqual("开机自启动", items[2], "T73: Menu item 2 matches");
                    AssertEqual("退出", items[3], "T73: Menu item 3 matches");
                    AssertTrue(!tray.StartupMenuChecked, "T73: Startup menu initially unchecked");

                    CompanionSettings cfg = host.SnapshotService.GetSettings();
                    cfg.StartWithWindows = true;
                    host.SnapshotService.UpdateSettings(cfg);
                    tray.RefreshMenuState();
                    AssertTrue(tray.StartupMenuChecked, "T73: Startup menu checked state updates after RefreshMenuState");
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T74_Stage4_CompanionCommandDispatcher_RegistrationParity()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t74_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                string cfgDir = Path.Combine(geminiDir, "config");
                string projDir = Path.Combine(cfgDir, "projects");
                string vaultDir = Path.Combine(sb, "Vault");
                Directory.CreateDirectory(agDir);
                Directory.CreateDirectory(cfgDir);
                Directory.CreateDirectory(projDir);
                Directory.CreateDirectory(vaultDir);

                // Seed plugin in config.json, MCP in mcp_config.json, and 1 session in proto + project json
                File.WriteAllText(Path.Combine(cfgDir, "config.json"),
                    "{\"plugins\":{\"plug-74\":{\"enabled\":false}},\"userSettings\":{\"autoExecutionPolicy\":\"CASCADE_COMMANDS_AUTO_EXECUTION_OFF\"}}",
                    Encoding.UTF8);
                string mcpFile = Path.Combine(cfgDir, "mcp_config.json");
                File.WriteAllText(mcpFile,
                    "{\"mcpServers\":{\"srv-74\":{\"command\":\"python\",\"args\":[\"tool74.py\"]}}}",
                    Encoding.UTF8);

                string cid74 = "c7400001-1111-4111-8111-111111111111";
                string projId74 = "p7400001-1111-4111-8111-111111111111";
                File.WriteAllText(Path.Combine(projDir, projId74 + ".json"),
                    WorkspaceService.GenerateStandardProjectJson(projId74, "Proj74", "D:\\Work\\Proj74"), Encoding.UTF8);
                byte[] pbBytes74 = ProtoCodec.Encode(new List<ProtoNode>
                {
                    BuildRealProtoEntry(cid74, "Original T74 Title", null, "file:///d%3A/Work/Proj74", "Proj74", projId74, 4, 1, 1)
                });
                File.WriteAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb"), pbBytes74);

                string token74 = "t74_tok";
                Dictionary<string, string> apiHdr74 = new Dictionary<string, string> { { "X-AGY-Token", token74 } };

                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, vaultDir, 0, token74))
                {
                    host.Start();
                    CompanionCommandDispatcher disp = host.Dispatcher;
                    AssertTrue(disp != null, "T74: Dispatcher non-null");

                    string[] expectedCmds = new string[]
                    {
                        "overview.get",
                        "sessions.list",
                        "capabilities.list",
                        "rename.candidates",
                        "gc.scan",
                        "sessions.search",
                        "cockpit.summary",
                        "mcp.doctor",
                        "editor.read",
                        "storage.analyze",
                        "settings.companion.get",
                        "snapshots.status",
                        "vault.status",
                        "cockpit.settings",
                        "cockpit.toggle",
                        "storage.slim",
                        "settings.companion.update",
                        "rename.commit",
                        "vault.push",
                        "vault.import",
                        "backup.full",
                        "backup.import"
                    };

                    for (int i = 0; i < expectedCmds.Length; i++)
                    {
                        AssertTrue(disp.HasCommand(expectedCmds[i]), "T74: Command registered -> " + expectedCmds[i]);
                    }

                    OperationResult<Dictionary<string, object>> stRes = disp.Execute("settings.companion.get", null);
                    AssertTrue(stRes.Ok && stRes.Data.ContainsKey("globalHotkey"), "T74: settings.companion.get executes cleanly");

                    OperationResult<Dictionary<string, object>> snapRes = disp.Execute("snapshots.status", null);
                    AssertTrue(snapRes.Ok && snapRes.Data.ContainsKey("retainCount"), "T74: snapshots.status executes cleanly");

                    OperationResult<Dictionary<string, object>> stoRes = disp.Execute("storage.analyze", null);
                    AssertTrue(stoRes.Ok && stoRes.Data.ContainsKey("totalBytes"), "T74: storage.analyze executes cleanly");

                    OperationResult<Dictionary<string, object>> vRes = disp.Execute("vault.status", null);
                    AssertTrue(vRes.Ok && vRes.Data.ContainsKey("vaultPath"), "T74: vault.status executes cleanly");

                    // 1. cockpit.settings (400 + valid write)
                    OperationResult<Dictionary<string, object>> emptySetRes = disp.Execute("cockpit.settings", null);
                    AssertTrue(!emptySetRes.Ok && emptySetRes.Data != null && UnifiedJson.GetInt(emptySetRes.Data, "httpStatusCode", 0) == 400,
                        "T74: cockpit.settings rejects empty args with 400");

                    host.IdeRunningOverride = delegate() { return false; };
                    Dictionary<string, object> cockArgs = new Dictionary<string, object>();
                    Dictionary<string, object> usPatch = new Dictionary<string, object>();
                    usPatch["autoExecutionPolicy"] = "CASCADE_COMMANDS_AUTO_EXECUTION_AUTO";
                    cockArgs["userSettings"] = usPatch;
                    OperationResult<Dictionary<string, object>> cockWriteRes = disp.Execute("cockpit.settings", cockArgs);
                    AssertTrue(cockWriteRes.Ok && UnifiedJson.GetBool(cockWriteRes.Data, "ok", false),
                        "T74: cockpit.settings executes cleanly through Dispatcher: " + cockWriteRes.ErrorMessage);
                    string cfgFile74 = Path.Combine(cfgDir, "config.json");
                    AssertTrue(File.Exists(cfgFile74) && File.ReadAllText(cfgFile74, Encoding.UTF8).Contains("CASCADE_COMMANDS_AUTO_EXECUTION_AUTO"),
                        "T74: cockpit.settings persisted config.json via Dispatcher");

                    // 2. cockpit.toggle (400 + 404 + IDE running warning + case-insensitive Enabled key)
                    OperationResult<Dictionary<string, object>> badTog = disp.Execute("cockpit.toggle", null);
                    AssertTrue(!badTog.Ok && badTog.Data != null && UnifiedJson.GetInt(badTog.Data, "httpStatusCode", 0) == 400,
                        "T74: cockpit.toggle rejects empty args with 400");

                    Dictionary<string, object> missingTogArgs = new Dictionary<string, object>();
                    missingTogArgs["kind"] = "plugin";
                    missingTogArgs["id"] = "non-existent-plugin";
                    OperationResult<Dictionary<string, object>> missingTog = disp.Execute("cockpit.toggle", missingTogArgs);
                    AssertTrue(!missingTog.Ok && missingTog.Data != null && UnifiedJson.GetInt(missingTog.Data, "httpStatusCode", 0) == 404,
                        "T74: cockpit.toggle returns 404 for unknown capability");

                    host.IdeRunningOverride = delegate() { return true; };
                    Dictionary<string, object> togOnArgs = new Dictionary<string, object>();
                    togOnArgs["kind"] = "plugin";
                    togOnArgs["id"] = "plug-74";
                    togOnArgs["Enabled"] = true;
                    OperationResult<Dictionary<string, object>> togOnRes = disp.Execute("cockpit.toggle", togOnArgs);
                    AssertTrue(togOnRes.Ok &&
                               UnifiedJson.GetBool(togOnRes.Data, "ok", false) &&
                               UnifiedJson.GetBool(togOnRes.Data, "enabled", false) &&
                               UnifiedJson.GetBool(togOnRes.Data, "ideRunning", false) &&
                               !string.IsNullOrEmpty(UnifiedJson.GetString(togOnRes.Data, "ideRunningWarning", "")),
                        "T74: cockpit.toggle enables plugin with IDE running warning via Dispatcher: " + togOnRes.ErrorMessage);
                    host.IdeRunningOverride = delegate() { return false; };

                    // 3. settings.companion.update (valid + invalid)
                    Dictionary<string, object> compArgs = new Dictionary<string, object>();
                    compArgs["intervalHours"] = 8;
                    compArgs["ruleTokenReferenceThreshold"] = 1800;
                    OperationResult<Dictionary<string, object>> compUpdRes = disp.Execute("settings.companion.update", compArgs);
                    AssertTrue(compUpdRes.Ok && UnifiedJson.GetBool(compUpdRes.Data, "ok", false) &&
                               UnifiedJson.GetInt(compUpdRes.Data, "intervalHours", 0) == 8 &&
                               UnifiedJson.GetInt(compUpdRes.Data, "ruleTokenReferenceThreshold", 0) == 1800,
                        "T74: settings.companion.update executes cleanly through Dispatcher: " + compUpdRes.ErrorMessage);

                    Dictionary<string, object> badCompArgs = new Dictionary<string, object>();
                    badCompArgs["intervalHours"] = 0;
                    OperationResult<Dictionary<string, object>> badCompRes = disp.Execute("settings.companion.update", badCompArgs);
                    AssertTrue(!badCompRes.Ok && badCompRes.Data != null && UnifiedJson.GetInt(badCompRes.Data, "httpStatusCode", 0) == 400,
                        "T74: settings.companion.update rejects invalid intervalHours with 400");

                    // 4. storage.slim (JOB_BUSY 409 + IDE_RUNNING 409 + valid 200)
                    AssertTrue(host.TryAcquireJob("t74_busy", "Busy check"), "T74: Acquired host job lock");
                    try
                    {
                        OperationResult<Dictionary<string, object>> busySlim = disp.Execute("storage.slim", new Dictionary<string, object>());
                        AssertTrue(!busySlim.Ok && busySlim.Data != null &&
                                   UnifiedJson.GetInt(busySlim.Data, "httpStatusCode", 0) == 409 &&
                                   UnifiedJson.GetString(busySlim.Data, "code", "") == "JOB_BUSY",
                            "T74: storage.slim returns 409 JOB_BUSY through Dispatcher when job lock held");
                    }
                    finally
                    {
                        host.ReleaseJob();
                    }

                    host.IdeRunningOverride = delegate() { return true; };
                    OperationResult<Dictionary<string, object>> slimIdeRes = disp.Execute("storage.slim", new Dictionary<string, object>());
                    AssertTrue(!slimIdeRes.Ok && slimIdeRes.Data != null &&
                               UnifiedJson.GetInt(slimIdeRes.Data, "httpStatusCode", 0) == 409 &&
                               UnifiedJson.GetString(slimIdeRes.Data, "code", "") == "IDE_RUNNING" &&
                               UnifiedJson.GetInt(slimIdeRes.Data, "parentCount", -1) == 1 &&
                               UnifiedJson.GetInt(slimIdeRes.Data, "lsCount", -1) == 0,
                        "T74: storage.slim returns structured 409 IDE_RUNNING via Dispatcher");

                    host.IdeRunningOverride = delegate() { return false; };
                    OperationResult<Dictionary<string, object>> slimOkRes = disp.Execute("storage.slim", new Dictionary<string, object>());
                    AssertTrue(slimOkRes.Ok && UnifiedJson.GetBool(slimOkRes.Data, "ok", false),
                        "T74: storage.slim executes cleanly when IDE stopped: " + slimOkRes.ErrorMessage);

                    // 5. rename.commit (400 + IDE_RUNNING 409 + valid single/items execution)
                    OperationResult<Dictionary<string, object>> badRename = disp.Execute("rename.commit", null);
                    AssertTrue(!badRename.Ok && badRename.Data != null && UnifiedJson.GetInt(badRename.Data, "httpStatusCode", 0) == 400,
                        "T74: rename.commit rejects empty args with 400");

                    host.IdeRunningOverride = delegate() { return true; };
                    Dictionary<string, object> renIdeArgs = new Dictionary<string, object>();
                    renIdeArgs["strategy"] = "auto";
                    renIdeArgs["cid"] = cid74;
                    renIdeArgs["newTitle"] = "【P74】Dispatcher Blocked";
                    OperationResult<Dictionary<string, object>> renIdeRes = disp.Execute("rename.commit", renIdeArgs);
                    AssertTrue(!renIdeRes.Ok && renIdeRes.Data != null &&
                               UnifiedJson.GetInt(renIdeRes.Data, "httpStatusCode", 0) == 409 &&
                               UnifiedJson.GetString(renIdeRes.Data, "code", "") == "IDE_RUNNING" &&
                               UnifiedJson.GetInt(renIdeRes.Data, "parentCount", -1) == 1 &&
                               UnifiedJson.GetInt(renIdeRes.Data, "lsCount", -1) == 0,
                        "T74: rename.commit returns structured 409 IDE_RUNNING via Dispatcher");

                    host.IdeRunningOverride = delegate() { return false; };
                    Dictionary<string, object> renOkArgs = new Dictionary<string, object>();
                    Dictionary<string, object> renItem = new Dictionary<string, object>();
                    renItem["cid"] = cid74;
                    renItem["newTitle"] = "【P74】Renamed Via Dispatcher";
                    renOkArgs["items"] = new List<object> { renItem };
                    OperationResult<Dictionary<string, object>> renOkRes = disp.Execute("rename.commit", renOkArgs);
                    AssertTrue(renOkRes.Ok && UnifiedJson.GetBool(renOkRes.Data, "ok", false) &&
                               UnifiedJson.GetInt(renOkRes.Data, "renamedCount", 0) == 1,
                        "T74: rename.commit executes cleanly via Dispatcher: " + renOkRes.ErrorMessage);

                    // 6. vault.push & 7. vault.import (400 + 404 + real round-trip)
                    OperationResult<Dictionary<string, object>> badPush = disp.Execute("vault.push", null);
                    AssertTrue(!badPush.Ok && badPush.Data != null && UnifiedJson.GetInt(badPush.Data, "httpStatusCode", 0) == 400,
                        "T74: vault.push rejects empty keys with 400");

                    Dictionary<string, object> missPushArgs = new Dictionary<string, object>();
                    missPushArgs["keys"] = new List<object> { "McpServer:missing-74" };
                    OperationResult<Dictionary<string, object>> missPush = disp.Execute("vault.push", missPushArgs);
                    AssertTrue(!missPush.Ok && missPush.Data != null && UnifiedJson.GetInt(missPush.Data, "httpStatusCode", 0) == 404,
                        "T74: vault.push returns 404 for unknown key");

                    Dictionary<string, object> pushArgs = new Dictionary<string, object>();
                    pushArgs["keys"] = new List<object> { "McpServer:srv-74" };
                    OperationResult<Dictionary<string, object>> pushRes = disp.Execute("vault.push", pushArgs);
                    AssertTrue(pushRes.Ok && UnifiedJson.GetBool(pushRes.Data, "ok", false) &&
                               UnifiedJson.GetInt(pushRes.Data, "written", 0) >= 1,
                        "T74: vault.push writes McpServer:srv-74 to Vault via Dispatcher: " + pushRes.ErrorMessage);

                    OperationResult<Dictionary<string, object>> badImp = disp.Execute("vault.import", null);
                    AssertTrue(!badImp.Ok && badImp.Data != null && UnifiedJson.GetInt(badImp.Data, "httpStatusCode", 0) == 400,
                        "T74: vault.import rejects empty keys with 400");

                    // Clear local mcp_config.json then import srv-74 back via vault.import
                    File.WriteAllText(mcpFile, "{\"mcpServers\":{}}", Encoding.UTF8);
                    Dictionary<string, object> impArgs = new Dictionary<string, object>();
                    impArgs["keys"] = new List<object> { "McpServer:srv-74" };
                    impArgs["source"] = "repo";
                    OperationResult<Dictionary<string, object>> impRes = disp.Execute("vault.import", impArgs);
                    AssertTrue(impRes.Ok && UnifiedJson.GetBool(impRes.Data, "ok", false) &&
                               UnifiedJson.GetInt(impRes.Data, "written", 0) >= 1 &&
                               File.ReadAllText(mcpFile, Encoding.UTF8).Contains("srv-74"),
                        "T74: vault.import restores McpServer:srv-74 from Vault via Dispatcher: " + impRes.ErrorMessage);

                    // 8. backup.full (400 missing outputPath + 400 traversal + 409 IDE_RUNNING + 202 async archive)
                    OperationResult<Dictionary<string, object>> badBk = disp.Execute("backup.full", new Dictionary<string, object>());
                    AssertTrue(!badBk.Ok && badBk.Data != null && UnifiedJson.GetInt(badBk.Data, "httpStatusCode", 0) == 400,
                        "T74: backup.full requires outputPath without hanging on UI dialog");

                    Dictionary<string, object> travBkArgs = new Dictionary<string, object>();
                    travBkArgs["outputPath"] = Path.Combine(sb, "..\\evil74.agy");
                    OperationResult<Dictionary<string, object>> travBk = disp.Execute("backup.full", travBkArgs);
                    AssertTrue(!travBk.Ok && travBk.Data != null && UnifiedJson.GetInt(travBk.Data, "httpStatusCode", 0) == 400,
                        "T74: backup.full rejects .. traversal path with 400");

                    string bkOut74 = Path.Combine(sb, "t74_full.agy");
                    host.IdeRunningOverride = delegate() { return true; };
                    Dictionary<string, object> bkIdeArgs = new Dictionary<string, object>();
                    bkIdeArgs["outputPath"] = bkOut74;
                    bkIdeArgs["closeIdeIfRunning"] = false;
                    OperationResult<Dictionary<string, object>> bkIdeRes = disp.Execute("backup.full", bkIdeArgs);
                    AssertTrue(!bkIdeRes.Ok && bkIdeRes.Data != null &&
                               UnifiedJson.GetInt(bkIdeRes.Data, "httpStatusCode", 0) == 409 &&
                               UnifiedJson.GetString(bkIdeRes.Data, "code", "") == "IDE_RUNNING",
                        "T74: backup.full returns 409 IDE_RUNNING via Dispatcher");

                    host.IdeRunningOverride = delegate() { return false; };
                    Dictionary<string, object> bkOkArgs = new Dictionary<string, object>();
                    bkOkArgs["outputPath"] = bkOut74;
                    bkOkArgs["tier"] = "fast";
                    OperationResult<Dictionary<string, object>> bkOkRes = disp.Execute("backup.full", bkOkArgs);
                    AssertTrue(bkOkRes.Ok && UnifiedJson.GetBool(bkOkRes.Data, "accepted", false) &&
                               UnifiedJson.GetInt(bkOkRes.Data, "httpStatusCode", 0) == 202,
                        "T74: backup.full returns 202 accepted via Dispatcher: " + bkOkRes.ErrorMessage);

                    for (int poll = 0; poll < 80; poll++)
                    {
                        RawHttpTestResponse jr = SendRawTcpHttp(host.Port, null, "GET", "/api/jobs/current", apiHdr74, null);
                        if (jr.BodyText.Contains("\"busy\":false")) break;
                        System.Threading.Thread.Sleep(50);
                    }
                    AssertTrue(File.Exists(bkOut74), "T74: backup.full created .agy archive on disk via Dispatcher");

                    // 9. backup.import (400 empty + 400 no backup loaded + 409 IDE_RUNNING + 202 async import)
                    OperationResult<Dictionary<string, object>> emptyBkImp = disp.Execute("backup.import", null);
                    AssertTrue(!emptyBkImp.Ok && emptyBkImp.Data != null && UnifiedJson.GetInt(emptyBkImp.Data, "httpStatusCode", 0) == 400,
                        "T74: backup.import rejects empty selection with 400");

                    Dictionary<string, object> noBkImpArgs = new Dictionary<string, object>();
                    noBkImpArgs["cids"] = new List<object> { cid74 };
                    OperationResult<Dictionary<string, object>> noBkImp = disp.Execute("backup.import", noBkImpArgs);
                    AssertTrue(!noBkImp.Ok && noBkImp.Data != null && UnifiedJson.GetInt(noBkImp.Data, "httpStatusCode", 0) == 400,
                        "T74: backup.import returns 400 when no archive is loaded");

                    RawHttpTestResponse openBkResp = SendRawTcpHttp(host.Port, null, "POST", "/api/backup/open", apiHdr74,
                        UnifiedJson.Serialize(new Dictionary<string, object> { { "archivePath", bkOut74 } }, false));
                    AssertEqual(200, openBkResp.StatusCode, "T74: Opened backup archive for backup.import test");

                    host.IdeRunningOverride = delegate() { return true; };
                    Dictionary<string, object> bkImpIdeArgs = new Dictionary<string, object>();
                    bkImpIdeArgs["cids"] = new List<object> { cid74 };
                    bkImpIdeArgs["closeIdeIfRunning"] = false;
                    OperationResult<Dictionary<string, object>> bkImpIdeRes = disp.Execute("backup.import", bkImpIdeArgs);
                    AssertTrue(!bkImpIdeRes.Ok && bkImpIdeRes.Data != null &&
                               UnifiedJson.GetInt(bkImpIdeRes.Data, "httpStatusCode", 0) == 409 &&
                               UnifiedJson.GetString(bkImpIdeRes.Data, "code", "") == "IDE_RUNNING" &&
                               UnifiedJson.GetInt(bkImpIdeRes.Data, "parentCount", -1) == 1 &&
                               UnifiedJson.GetInt(bkImpIdeRes.Data, "lsCount", -1) == 0,
                        "T74: backup.import returns structured 409 IDE_RUNNING via Dispatcher");

                    host.IdeRunningOverride = delegate() { return false; };
                    Dictionary<string, object> bkImpOkArgs = new Dictionary<string, object>();
                    bkImpOkArgs["capKeys"] = new List<object> { "McpServer:srv-74" };
                    OperationResult<Dictionary<string, object>> bkImpOkRes = disp.Execute("backup.import", bkImpOkArgs);
                    AssertTrue(bkImpOkRes.Ok && UnifiedJson.GetBool(bkImpOkRes.Data, "accepted", false) &&
                               UnifiedJson.GetInt(bkImpOkRes.Data, "httpStatusCode", 0) == 202,
                        "T74: backup.import returns 202 accepted via Dispatcher: " + bkImpOkRes.ErrorMessage);

                    for (int poll = 0; poll < 80; poll++)
                    {
                        RawHttpTestResponse jr = SendRawTcpHttp(host.Port, null, "GET", "/api/jobs/current", apiHdr74, null);
                        if (jr.BodyText.Contains("\"busy\":false")) break;
                        System.Threading.Thread.Sleep(50);
                    }

                    host.IdeRunningOverride = null;

                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T75_Wave1_Remediation_SecurityJobObjectAndDefaults()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t75_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string cfgDir = Path.Combine(geminiDir, "config");
                string projDir = Path.Combine(cfgDir, "projects");
                string vaultDir = Path.Combine(sb, "Vault");
                string coldDir = Path.Combine(geminiDir, "companion-cold-archives");
                string regWsDir = Path.Combine(sb, "RegWs75");
                string metaWsDir = Path.Combine(sb, "Proj&75");
                Directory.CreateDirectory(cfgDir);
                Directory.CreateDirectory(projDir);
                Directory.CreateDirectory(vaultDir);
                Directory.CreateDirectory(coldDir);
                Directory.CreateDirectory(regWsDir);
                Directory.CreateDirectory(metaWsDir);
                Directory.CreateDirectory(Path.Combine(geminiDir, "antigravity"));

                string homeSecretFile = Path.Combine(homeDir, "home_secret.txt");
                File.WriteAllText(homeSecretFile, "not allowed via explorer", Encoding.UTF8);
                string coldFile = Path.Combine(coldDir, "cold.agy");
                File.WriteAllText(coldFile, "cold", Encoding.UTF8);

                File.WriteAllText(Path.Combine(projDir, "p75a.json"),
                    WorkspaceService.GenerateStandardProjectJson("p75a", "RegWs75", regWsDir), Encoding.UTF8);
                File.WriteAllText(Path.Combine(projDir, "p75b.json"),
                    WorkspaceService.GenerateStandardProjectJson("p75b", "Proj&75", metaWsDir), Encoding.UTF8);

                File.WriteAllText(Path.Combine(cfgDir, "config.json"),
                    "{\"plugins\":{\"plug-75\":{\"enabled\":false}},\"userSettings\":{\"autoExecutionPolicy\":\"CASCADE_COMMANDS_AUTO_EXECUTION_OFF\"}}",
                    Encoding.UTF8);

                // 1. A6: WorkspaceLauncherService.NormalizeDirArg & HomePath removal & .cmd metacharacter rejection (400)
                AssertEqual(@"D:\", WorkspaceLauncherService.NormalizeDirArg(@"D:\"), "T75: NormalizeDirArg preserves drive root backslash");
                AssertEqual(@"D:\", WorkspaceLauncherService.NormalizeDirArg(@"D:/"), "T75: NormalizeDirArg normalizes forward slash on drive root");
                AssertEqual(@"D:\Work\Proj", WorkspaceLauncherService.NormalizeDirArg(@"D:\Work\Proj\"), "T75: NormalizeDirArg strips trailing slash on subdirectory");

                string valDir;
                int valCode;
                string valErr;
                AssertTrue(!WorkspaceLauncherService.ValidateWorkspaceDirectory(geminiDir, homeDir, vaultDir, homeDir, out valDir, out valCode, out valErr) && valCode == 403,
                    "T75: A6 blocks HomePath from allowed workspace roots (403)");
                AssertTrue(WorkspaceLauncherService.ValidateWorkspaceDirectory(geminiDir, homeDir, vaultDir, regWsDir, out valDir, out valCode, out valErr),
                    "T75: Registered workspace allowed");

                string ideBinDir = Path.Combine(geminiDir, "antigravity-ide", "bin");
                Directory.CreateDirectory(ideBinDir);
                File.WriteAllText(Path.Combine(ideBinDir, "antigravity-ide.cmd"), "@echo off\r\n", Encoding.UTF8);
                WorkspaceLaunchResult metaRes = WorkspaceLauncherService.LaunchWorkspace(geminiDir, homeDir, vaultDir, metaWsDir, "cli");
                AssertTrue(!metaRes.Ok && metaRes.HttpStatusCode == 400, "T75: A6 rejects & metacharacter in .cmd CLI launch with HTTP 400");
                string emptyLocalApp = Path.Combine(sb, "EmptyLocalApp");
                Directory.CreateDirectory(emptyLocalApp);
                WorkspaceLaunchResult metaIdeCmdRes = WorkspaceLauncherService.LaunchWorkspace(geminiDir, homeDir, vaultDir, metaWsDir, "ide", emptyLocalApp, "");
                AssertTrue(!metaIdeCmdRes.Ok && metaIdeCmdRes.HttpStatusCode == 400, "T75: A6 rejects & metacharacter when IDE falls back to .cmd with HTTP 400");

                string bigGeminiMd = Path.Combine(geminiDir, "GEMINI.md");
                File.WriteAllText(bigGeminiMd, new string('中', 2400) + "\r\nsecond line", Encoding.UTF8);

                // 2. A6 (Explorer), A4 (Cockpit IDE warning), A5 (startupRollback), B5 (HTTP ruleTokenReferenceThreshold)
                string token = "t75_tok";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, vaultDir, 0, token))
                {
                    host.Start();

                    RawHttpTestResponse expHome = SendRawTcpHttp(host.Port, null, "POST", "/api/native/open-in-explorer", apiHdr,
                        "{\"path\":\"" + UnifiedJson.EscapeString(homeSecretFile) + "\"}");
                    AssertEqual(403, expHome.StatusCode, "T75: A6 open-in-explorer rejects HomePath file with 403");

                    RawHttpTestResponse cockDefSum = SendRawTcpHttp(host.Port, null, "GET", "/api/cockpit/summary", apiHdr, null);
                    AssertEqual(200, cockDefSum.StatusCode, "T75: GET /api/cockpit/summary returns 200");
                    AssertTrue(cockDefSum.BodyText.Contains("\"threshold\":0") &&
                               cockDefSum.BodyText.Contains("\"overBudgetCount\":0") &&
                               cockDefSum.BodyText.Contains("\"lineCount\":2") &&
                               cockDefSum.BodyText.Contains("\"byteCount\":"),
                        "T75: B5 Default threshold=0 reports 0 overBudgetCount and includes lineCount/byteCount");

                    // A5: Startup registry failure rolls back startWithWindows and returns startupRollback=true
                    host.SetStartWithWindowsOverride = delegate(bool en, string exe) { return false; };
                    RawHttpTestResponse rbResp = SendRawTcpHttp(host.Port, null, "POST", "/api/settings/companion", apiHdr,
                        "{\"startWithWindows\":true,\"ruleTokenReferenceThreshold\":1500}");
                    AssertEqual(200, rbResp.StatusCode, "T75: A5 companion settings returns 200 with rollback payload");
                    AssertTrue(rbResp.BodyText.Contains("\"startupRollback\":true") &&
                               rbResp.BodyText.Contains("\"startupError\"") &&
                               rbResp.BodyText.Contains("\"startWithWindows\":false") &&
                               rbResp.BodyText.Contains("\"ruleTokenReferenceThreshold\":1500"),
                        "T75: A5 rolls back startWithWindows to false and persists ruleTokenReferenceThreshold=1500");
                    host.SetStartWithWindowsOverride = null;

                    RawHttpTestResponse cockThreshSum = SendRawTcpHttp(host.Port, null, "GET", "/api/cockpit/summary", apiHdr, null);
                    AssertEqual(200, cockThreshSum.StatusCode, "T75: GET /api/cockpit/summary with threshold=1500 returns 200");
                    AssertTrue(cockThreshSum.BodyText.Contains("\"threshold\":1500") &&
                               cockThreshSum.BodyText.Contains("\"overBudgetCount\":1"),
                        "T75: B5 threshold=1500 flags overBudgetCount=1");

                    host.IdeRunningOverride = delegate() { return true; };
                    RawHttpTestResponse cockSetWarn = SendRawTcpHttp(host.Port, null, "POST", "/api/cockpit/settings", apiHdr,
                        "{\"userSettings\":{\"autoExecutionPolicy\":\"CASCADE_COMMANDS_AUTO_EXECUTION_AUTO\"}}");
                    AssertEqual(200, cockSetWarn.StatusCode, "T75: Cockpit settings returns 200");
                    AssertTrue(cockSetWarn.BodyText.Contains("\"ideRunning\":true") && cockSetWarn.BodyText.Contains("重启 IDE"),
                        "T75: A4 settings includes ideRunning=true and ideRunningWarning");

                    RawHttpTestResponse cockTogWarn = SendRawTcpHttp(host.Port, null, "POST", "/api/cockpit/toggle", apiHdr,
                        "{\"kind\":\"plugin\",\"id\":\"plug-75\",\"enabled\":true}");
                    AssertEqual(200, cockTogWarn.StatusCode, "T75: Cockpit toggle returns 200");
                    AssertTrue(cockTogWarn.BodyText.Contains("\"ideRunning\":true") && cockTogWarn.BodyText.Contains("重启 IDE"),
                        "T75: A4 toggle includes ideRunning=true and ideRunningWarning");

                    host.IdeRunningOverride = delegate() { return false; };
                    RawHttpTestResponse cockSetOk = SendRawTcpHttp(host.Port, null, "POST", "/api/cockpit/settings", apiHdr,
                        "{\"userSettings\":{\"autoExecutionPolicy\":\"CASCADE_COMMANDS_AUTO_EXECUTION_OFF\"}}");
                    AssertEqual(200, cockSetOk.StatusCode, "T75: Cockpit settings returns 200 when IDE stopped");
                    AssertTrue(cockSetOk.BodyText.Contains("\"ideRunning\":false") && cockSetOk.BodyText.Contains("\"ideRunningWarning\":\"\""),
                        "T75: A4 settings returns ideRunning=false and empty warning when IDE stopped");

                    // 3. B1 (TrayHost does not register hotkey when GlobalHotkey is empty)
                    using (TrayHost tray = new TrayHost(host, "http://127.0.0.1:" + host.Port + "/?t=" + token, false, false))
                    {
                        AssertTrue(!tray.HotkeyRegistered && string.IsNullOrEmpty(tray.HotkeyError),
                            "T75: B1 TrayHost does not register hotkey when default GlobalHotkey is empty");
                    }

                    host.Stop();
                }

                // 4. A3 (Scheduled FullForensic snapshot skips safely when IDE running + _mutationLock mutual exclusion)
                string snapSettingsPath = Path.Combine(sb, "snap75_settings.json");
                string snapOutDir = Path.Combine(sb, "snap75_out");
                object mutLock = new object();
                using (ScheduledSnapshotService snapSvc = new ScheduledSnapshotService(
                    homeDir, geminiDir, snapSettingsPath, null, null, null, mutLock, delegate() { return true; }))
                {
                    CompanionSettings scfg = snapSvc.GetSettings();
                    scfg.Enabled = true;
                    scfg.Tier = "full";
                    scfg.TargetDirectory = snapOutDir;
                    snapSvc.UpdateSettings(scfg);

                    SnapshotRunResult schedRes = snapSvc.ExecuteSnapshotInternal(true);
                    AssertTrue(!schedRes.Ok && schedRes.SkippedBusy && schedRes.ErrorMessage.Contains("IDE"),
                        "T75: A3 Scheduled FullForensic snapshot skips cleanly when IDE is running");
                    AssertTrue(!Directory.Exists(snapOutDir),
                        "T75: A3 Skipped scheduled snapshot did not create target directory or archive");

                    // Multi-threaded _mutationLock contention check: snapshot waits until mutation thread releases mutLock
                    System.Threading.ManualResetEventSlim lockAcquired = new System.Threading.ManualResetEventSlim(false);
                    System.Threading.ManualResetEventSlim releaseLock = new System.Threading.ManualResetEventSlim(false);
                    long lockReleasedTicks = 0;
                    long snapFinishedTicks = 0;
                    SnapshotRunResult manualRes = null;

                    System.Threading.Thread blocker = new System.Threading.Thread(delegate()
                    {
                        lock (mutLock)
                        {
                            lockAcquired.Set();
                            releaseLock.Wait(2000);
                            System.Threading.Interlocked.Exchange(ref lockReleasedTicks, Stopwatch.GetTimestamp());
                        }
                    });
                    blocker.Start();
                    AssertTrue(lockAcquired.Wait(2000), "T75: Blocker acquired _mutationLock");

                    System.Threading.Thread snapThread = new System.Threading.Thread(delegate()
                    {
                        manualRes = snapSvc.ExecuteSnapshotInternal(false);
                        System.Threading.Interlocked.Exchange(ref snapFinishedTicks, Stopwatch.GetTimestamp());
                    });
                    snapThread.Start();
                    System.Threading.Thread.Sleep(150);
                    AssertTrue(System.Threading.Interlocked.Read(ref snapFinishedTicks) == 0, "T75: A3 Snapshot blocked while _mutationLock held");
                    releaseLock.Set();
                    blocker.Join(3000);
                    snapThread.Join(10000);
                    AssertTrue(manualRes != null && manualRes.Ok && System.Threading.Interlocked.Read(ref snapFinishedTicks) >= System.Threading.Interlocked.Read(ref lockReleasedTicks),
                        "T75: A3 Snapshot completed only after _mutationLock released");
                }

                // 5. A2 (Job Object drains .cmd -> async detached powershell.exe child tree before returning & cleans up on timeout)
                string cliDir = Path.Combine(sb, "cli75");
                Directory.CreateDirectory(cliDir);
                string cid75 = "a7500001-0000-4000-8000-000000000001";
                List<SessionRenameCandidate> cand75 = new List<SessionRenameCandidate>
                {
                    new SessionRenameCandidate { ConversationId = cid75, ProjectName = "P75", OldTitle = "old", FirstPromptSnippet = "test" }
                };

                string drainMarker = Path.Combine(cliDir, "drained_async.txt");
                string drainPs = Path.Combine(cliDir, "drain_async.ps1");
                File.WriteAllText(drainPs,
                    "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8\r\n" +
                    "$null = [Console]::In.ReadToEnd()\r\n" +
                    "[Console]::Out.WriteLine('[{\"cid\":\"" + cid75 + "\",\"title\":\"[P75] Job Drained\",\"summary\":\"ok\"}]')\r\n" +
                    "[Console]::Out.Flush()\r\n" +
                    "Start-Sleep -Milliseconds 250\r\n" +
                    "[System.IO.File]::WriteAllText('" + drainMarker.Replace("'", "''") + "', 'done')\r\n",
                    Encoding.UTF8);
                string drainCmd = Path.Combine(cliDir, "agy.cmd");
                File.WriteAllText(drainCmd,
                    "@echo off\r\n" +
                    "if \"%~1\"==\"--version\" (\r\n" +
                    "  echo agy 75\r\n" +
                    "  exit /b 0\r\n" +
                    ")\r\n" +
                    "start /b \"\" powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" + drainPs + "\"\r\n" +
                    "exit /b 0\r\n",
                    new UTF8Encoding(false));

                CliBatchSuggestionResult drainRes = AntigravityCliAdapter.GenerateBatchSuggestionsViaCli(cand75, drainCmd, 15, "", "", "");
                AssertTrue(drainRes.Ok && drainRes.Suggestions.Count == 1, "T75: A2 CLI async child drain test succeeded: " + drainRes.ErrorOrHint);
                AssertTrue(File.Exists(drainMarker), "T75: A2 Job Object waited for detached start /b powershell.exe to finish after cmd.exe exited");

                string hangPidFile = Path.Combine(cliDir, "hang75.pid");
                string hangPs = Path.Combine(cliDir, "hang75.ps1");
                File.WriteAllText(hangPs,
                    "[System.IO.File]::WriteAllText('" + hangPidFile.Replace("'", "''") + "', $PID.ToString())\r\n" +
                    "Start-Sleep -Seconds 30\r\n",
                    Encoding.UTF8);
                string hangCmd = Path.Combine(cliDir, "agy_hang.cmd");
                File.WriteAllText(hangCmd,
                    "@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" + hangPs + "\" %*\r\n",
                    new UTF8Encoding(false));

                CliBatchSuggestionResult timeoutRes = AntigravityCliAdapter.GenerateBatchSuggestionsViaCli(cand75, hangCmd, 1, "", "", "");
                AssertTrue(!timeoutRes.Ok && timeoutRes.Status == CliSuggestStatus.Timeout, "T75: A2 CLI timeout detected");
                if (File.Exists(hangPidFile))
                {
                    int hPid;
                    if (int.TryParse(File.ReadAllText(hangPidFile).Trim(), out hPid))
                    {
                        bool alive = false;
                        try
                        {
                            using (Process hp = Process.GetProcessById(hPid))
                            {
                                alive = !hp.HasExited;
                            }
                        }
                        catch { alive = false; }
                        AssertTrue(!alive, "T75: A2 Job Object terminated child powershell.exe on timeout");
                    }
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T76_ACC02_JobObjectRepeatedDrainAndTimeoutCleanup()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t76_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sb);
            try
            {
                string cid76 = "a7600001-0000-4000-8000-000000000001";
                List<SessionRenameCandidate> cand76 = new List<SessionRenameCandidate>
                {
                    new SessionRenameCandidate { ConversationId = cid76, ProjectName = "P76", OldTitle = "old", FirstPromptSnippet = "drain test" }
                };

                // 3 consecutive iterations of .cmd -> start /b powershell.exe + immediate Directory.Delete (0 IOException leak)
                for (int iter = 0; iter < 3; iter++)
                {
                    string iterDir = Path.Combine(sb, "drain_iter_" + iter);
                    Directory.CreateDirectory(iterDir);
                    string marker = Path.Combine(iterDir, "drained_" + iter + ".txt");
                    string psPath = Path.Combine(iterDir, "worker.ps1");
                    File.WriteAllText(psPath,
                        "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8\r\n" +
                        "$null = [Console]::In.ReadToEnd()\r\n" +
                        "[Console]::Out.WriteLine('[{\"cid\":\"" + cid76 + "\",\"title\":\"[P76] Iter " + iter + "\",\"summary\":\"ok\"}]')\r\n" +
                        "[Console]::Out.Flush()\r\n" +
                        "Start-Sleep -Milliseconds 150\r\n" +
                        "[System.IO.File]::WriteAllText('" + marker.Replace("'", "''") + "', 'done')\r\n",
                        Encoding.UTF8);

                    string cmdPath = Path.Combine(iterDir, "agy.cmd");
                    File.WriteAllText(cmdPath,
                        "@echo off\r\n" +
                        "if \"%~1\"==\"--version\" (\r\n" +
                        "  echo agy 76." + iter + "\r\n" +
                        "  exit /b 0\r\n" +
                        ")\r\n" +
                        "start /b \"\" powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" + psPath + "\"\r\n" +
                        "exit /b 0\r\n",
                        new UTF8Encoding(false));

                    CliBatchSuggestionResult res = AntigravityCliAdapter.GenerateBatchSuggestionsViaCli(cand76, cmdPath, 15, "", "", "");
                    AssertTrue(res.Ok && res.Suggestions.Count == 1,
                        "T76: Iteration " + iter + " drained detached start /b powershell.exe: " + res.ErrorOrHint);
                    AssertTrue(File.Exists(marker),
                        "T76: Iteration " + iter + " detached child completed marker write before return");

                    // Immediate delete with zero sleep/retry: any lingering child process or open handle throws IOException
                    bool deleteClean = false;
                    try
                    {
                        Directory.Delete(iterDir, true);
                        deleteClean = !Directory.Exists(iterDir);
                    }
                    catch (IOException ioEx)
                    {
                        AssertTrue(false, "T76: Iteration " + iter + " IOException handle leak on immediate delete: " + ioEx.Message);
                    }
                    AssertTrue(deleteClean, "T76: Iteration " + iter + " temp dir deleted immediately with 0 handle leaks");
                }

                // Timeout termination of multi-level process tree (cmd -> powershell -> grandchild cmd/ping) + immediate temp directory deletion
                string toDir = Path.Combine(sb, "timeout_iter");
                Directory.CreateDirectory(toDir);
                string hangPidFile = Path.Combine(toDir, "hang76.pid");
                string grandPidFile = Path.Combine(toDir, "grand76.pid");
                string hangPs = Path.Combine(toDir, "hang.ps1");
                File.WriteAllText(hangPs,
                    "[System.IO.File]::WriteAllText('" + hangPidFile.Replace("'", "''") + "', $PID.ToString())\r\n" +
                    "$gc = Start-Process -FilePath 'cmd.exe' -WindowStyle Hidden -PassThru -ArgumentList '/c','ping -n 30 127.0.0.1 >nul'\r\n" +
                    "[System.IO.File]::WriteAllText('" + grandPidFile.Replace("'", "''") + "', $gc.Id.ToString())\r\n" +
                    "Start-Sleep -Seconds 30\r\n",
                    Encoding.UTF8);
                string hangCmd = Path.Combine(toDir, "agy_hang.cmd");
                File.WriteAllText(hangCmd,
                    "@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" + hangPs + "\" %*\r\n",
                    new UTF8Encoding(false));

                CliBatchSuggestionResult toRes = AntigravityCliAdapter.GenerateBatchSuggestionsViaCli(cand76, hangCmd, 4, "", "", "");
                AssertTrue(!toRes.Ok && toRes.Status == CliSuggestStatus.Timeout, "T76: Timeout status returned");
                AssertTrue(File.Exists(hangPidFile), "T76: Child powershell.exe started and wrote PID before timeout");
                AssertTrue(File.Exists(grandPidFile), "T76: Grandchild cmd.exe started and wrote PID before timeout");

                string[] pidFilesToCheck = new string[] { hangPidFile, grandPidFile };
                for (int pi = 0; pi < pidFilesToCheck.Length; pi++)
                {
                    int hPid;
                    AssertTrue(int.TryParse(File.ReadAllText(pidFilesToCheck[pi]).Trim(), out hPid), "T76: Parsed PID from " + Path.GetFileName(pidFilesToCheck[pi]));
                    bool alive = false;
                    try
                    {
                        using (Process hp = Process.GetProcessById(hPid))
                        {
                            alive = !hp.HasExited;
                        }
                    }
                    catch { alive = false; }
                    AssertTrue(!alive, "T76: Process tree member killed before timeout return (" + Path.GetFileName(pidFilesToCheck[pi]) + ")");
                }

                bool toDeleteClean = false;
                try
                {
                    Directory.Delete(toDir, true);
                    toDeleteClean = !Directory.Exists(toDir);
                }
                catch (IOException ioEx)
                {
                    AssertTrue(false, "T76: IOException handle leak on immediate delete after timeout: " + ioEx.Message);
                }
                AssertTrue(toDeleteClean, "T76: Timeout temp dir deleted immediately with 0 handle leaks");
            }
            finally
            {
                if (Directory.Exists(sb))
                {
                    try { Directory.Delete(sb, true); } catch { }
                }
            }
        }

        private static void T77_ACC03_ScheduledSnapshotMutationLockAndIdeSkip()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t77_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agDir = Path.Combine(geminiDir, "antigravity");
                Directory.CreateDirectory(Path.Combine(agDir, "conversations"));

                string cidRoot = "a7700001-0000-4000-8000-000000000001";
                string cidSub = "b7700002-0000-4000-8000-000000000002";
                ProtoNode nRoot = BuildRealProtoEntry(cidRoot, "Root 77", null, "file:///c%3A/P77", "P77", "pid-77", 3, 1, 1);
                ProtoNode nSub = BuildRealProtoEntry(cidSub, "Sub 77", null, "file:///c%3A/P77", "P77", "pid-77", 4, 1, 0);
                nSub.GetChild(2).GetChild(17).Children.Add(new ProtoNode(11, 2) { RawBytes = Encoding.UTF8.GetBytes(cidRoot), IsModified = true });
                File.WriteAllBytes(Path.Combine(agDir, "agyhub_summaries_proto.pb"), ProtoCodec.Encode(new List<ProtoNode> { nRoot, nSub }));
                CreateMinimalValidSqliteDb(Path.Combine(agDir, "conversations", cidRoot + ".db"), cidRoot);
                CreateMinimalValidSqliteDb(Path.Combine(agDir, "conversations", cidSub + ".db"), cidSub);

                // 1. Direct ScheduledSnapshotService: FullForensic skipped when isIdeRunning()==true (no dir created); FastDaily NOT skipped
                bool simIdeRunning = true;
                object mutLock = new object();
                string settingsPath = Path.Combine(sb, "snap77.json");
                string fullSnapDir = Path.Combine(sb, "FullSnapsNeverCreatedWhenSkipped");
                string fastSnapDir = Path.Combine(sb, "FastSnapsAllowedWhenIdeRunning");

                using (ScheduledSnapshotService svc = new ScheduledSnapshotService(
                    homeDir, geminiDir, settingsPath, null, null, null, mutLock, delegate() { return simIdeRunning; }))
                {
                    CompanionSettings cfg = svc.GetSettings();
                    cfg.Enabled = true;
                    cfg.Tier = "full";
                    cfg.TargetDirectory = fullSnapDir;
                    svc.UpdateSettings(cfg);

                    SnapshotRunResult skipRes = svc.ExecuteSnapshotInternal(true);
                    AssertTrue(!skipRes.Ok && skipRes.SkippedBusy, "T77: Scheduled FullForensic skipped when isIdeRunning()==true");
                    AssertTrue(!Directory.Exists(fullSnapDir), "T77: Target directory was NOT created when FullForensic skipped");
                    AssertEqual(string.Empty, svc.GetSettings().LastFilePath, "T77: LastFilePath remained empty after skip");

                    // FastDaily scheduled snapshot proceeds even while isIdeRunning()==true
                    cfg.Tier = "fast";
                    cfg.TargetDirectory = fastSnapDir;
                    svc.UpdateSettings(cfg);
                    SnapshotRunResult fastRes = svc.ExecuteSnapshotInternal(true);
                    AssertTrue(fastRes.Ok && File.Exists(fastRes.FilePath), "T77: Scheduled FastDaily succeeds while IDE is running: " + fastRes.ErrorMessage);
                    BackupArchiveIndex fastIdx = StreamingZipIndexReader.ReadIndex(fastRes.FilePath);
                    AssertEqual("FastDaily", fastIdx.BackupTier, "T77: FastDaily tier verified");

                    // FullForensic succeeds once IDE stops
                    simIdeRunning = false;
                    cfg.Tier = "full";
                    cfg.TargetDirectory = fullSnapDir;
                    svc.UpdateSettings(cfg);
                    SnapshotRunResult fullOkRes = svc.ExecuteSnapshotInternal(true);
                    AssertTrue(fullOkRes.Ok && File.Exists(fullOkRes.FilePath), "T77: Scheduled FullForensic runs when isIdeRunning()==false: " + fullOkRes.ErrorMessage);
                    BackupArchiveIndex fullIdx = StreamingZipIndexReader.ReadIndex(fullOkRes.FilePath);
                    AssertEqual("FullForensic", fullIdx.BackupTier, "T77: FullForensic tier verified");
                    AssertTrue(fullIdx.ExistingConvoDbFiles.Contains(cidSub), "T77: FullForensic included subagent DB");

                    // MutationLock mutual exclusion: snapshot blocks while external mutation thread holds mutLock
                    System.Threading.ManualResetEventSlim acquired = new System.Threading.ManualResetEventSlim(false);
                    System.Threading.ManualResetEventSlim release = new System.Threading.ManualResetEventSlim(false);
                    long releaseTicks = 0;
                    long snapDoneTicks = 0;
                    SnapshotRunResult contendedRes = null;

                    System.Threading.Thread mutThread = new System.Threading.Thread(delegate()
                    {
                        lock (mutLock)
                        {
                            acquired.Set();
                            release.Wait(2000);
                            System.Threading.Interlocked.Exchange(ref releaseTicks, Stopwatch.GetTimestamp());
                        }
                    });
                    mutThread.Start();
                    AssertTrue(acquired.Wait(2000), "T77: Mutation thread acquired MutationLock");

                    System.Threading.Thread snapThread = new System.Threading.Thread(delegate()
                    {
                        contendedRes = svc.RunSnapshotNow();
                        System.Threading.Interlocked.Exchange(ref snapDoneTicks, Stopwatch.GetTimestamp());
                    });
                    snapThread.Start();
                    System.Threading.Thread.Sleep(150);
                    AssertTrue(System.Threading.Interlocked.Read(ref snapDoneTicks) == 0, "T77: RunSnapshotNow blocked while MutationLock held");

                    release.Set();
                    mutThread.Join(3000);
                    snapThread.Join(10000);
                    AssertTrue(contendedRes != null && contendedRes.Ok &&
                               System.Threading.Interlocked.Read(ref snapDoneTicks) >= System.Threading.Interlocked.Read(ref releaseTicks),
                        "T77: RunSnapshotNow completed strictly after MutationLock release");
                }

                // 2. Verify LocalWebHost wires IdeRunningOverride and MutationLock into host.SnapshotService
                string hostSnapDir = Path.Combine(sb, "HostFullSnapDir");
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, Path.Combine(sb, "Vault"), 0, "t77_tok"))
                {
                    host.Start();
                    host.IdeRunningOverride = delegate() { return true; };
                    CompanionSettings hcfg = host.SnapshotService.GetSettings();
                    hcfg.Enabled = true;
                    hcfg.Tier = "full";
                    hcfg.TargetDirectory = hostSnapDir;
                    host.SnapshotService.UpdateSettings(hcfg);

                    SnapshotRunResult hostSkip = host.SnapshotService.ExecuteSnapshotInternal(true);
                    AssertTrue(!hostSkip.Ok && hostSkip.SkippedBusy, "T77: LocalWebHost.SnapshotService respects IdeRunningOverride and skips FullForensic");
                    AssertTrue(!Directory.Exists(hostSnapDir), "T77: LocalWebHost.SnapshotService did not create target directory when skipped");

                    host.IdeRunningOverride = delegate() { return false; };
                    System.Threading.ManualResetEventSlim hostAcquired = new System.Threading.ManualResetEventSlim(false);
                    System.Threading.ManualResetEventSlim hostRelease = new System.Threading.ManualResetEventSlim(false);
                    long hostRelTicks = 0;
                    long hostSnapTicks = 0;
                    SnapshotRunResult hostMutRes = null;

                    System.Threading.Thread hostMutThread = new System.Threading.Thread(delegate()
                    {
                        lock (host.MutationLock)
                        {
                            hostAcquired.Set();
                            hostRelease.Wait(2000);
                            System.Threading.Interlocked.Exchange(ref hostRelTicks, Stopwatch.GetTimestamp());
                        }
                    });
                    hostMutThread.Start();
                    AssertTrue(hostAcquired.Wait(2000), "T77: Acquired LocalWebHost.MutationLock");

                    System.Threading.Thread hostSnapThread = new System.Threading.Thread(delegate()
                    {
                        hostMutRes = host.SnapshotService.RunSnapshotNow();
                        System.Threading.Interlocked.Exchange(ref hostSnapTicks, Stopwatch.GetTimestamp());
                    });
                    hostSnapThread.Start();
                    System.Threading.Thread.Sleep(150);
                    AssertTrue(System.Threading.Interlocked.Read(ref hostSnapTicks) == 0, "T77: host.SnapshotService blocked while host.MutationLock held");
                    hostRelease.Set();
                    hostMutThread.Join(3000);
                    hostSnapThread.Join(10000);
                    AssertTrue(hostMutRes != null && hostMutRes.Ok &&
                               System.Threading.Interlocked.Read(ref hostSnapTicks) >= System.Threading.Interlocked.Read(ref hostRelTicks),
                        "T77: host.SnapshotService completed strictly after host.MutationLock release");

                    host.IdeRunningOverride = null;
                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T78_ACC04_CockpitSettingsAndToggleIdeRunningWarnings()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t78_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string cfgDir = Path.Combine(geminiDir, "config");
                Directory.CreateDirectory(Path.Combine(geminiDir, "antigravity"));
                Directory.CreateDirectory(cfgDir);

                string cfgPath = Path.Combine(cfgDir, "config.json");
                File.WriteAllText(cfgPath,
                    "{\"plugins\":{\"plug-78\":{\"enabled\":false}},\"userSettings\":{\"autoExecutionPolicy\":\"CASCADE_COMMANDS_AUTO_EXECUTION_OFF\"}}",
                    Encoding.UTF8);
                string mcpPath = Path.Combine(cfgDir, "mcp_config.json");
                File.WriteAllText(mcpPath,
                    "{\"mcpServers\":{\"srv-78\":{\"command\":\"python\",\"args\":[\"srv.py\"],\"disabled\":false}}}",
                    Encoding.UTF8);

                string token = "t78_tok";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, Path.Combine(sb, "Vault"), 0, token))
                {
                    host.Start();

                    // Case 1: IdeRunningOverride == true
                    host.IdeRunningOverride = delegate() { return true; };

                    RawHttpTestResponse setOn = SendRawTcpHttp(host.Port, null, "POST", "/api/cockpit/settings", apiHdr,
                        "{\"userSettings\":{\"autoExecutionPolicy\":\"CASCADE_COMMANDS_AUTO_EXECUTION_AUTO\"}}");
                    AssertEqual(200, setOn.StatusCode, "T78: POST /api/cockpit/settings (IDE running) returns 200");
                    Dictionary<string, object> setOnDict = UnifiedJson.AsDict(UnifiedJson.Parse(setOn.BodyText));
                    AssertTrue(UnifiedJson.GetBool(setOnDict, "ok", false), "T78: settings ok=true when IDE running");
                    AssertTrue(UnifiedJson.GetBool(setOnDict, "ideRunning", false), "T78: settings ideRunning=true when IDE running");
                    AssertTrue(!string.IsNullOrEmpty(UnifiedJson.GetString(setOnDict, "ideRunningWarning", "")),
                        "T78: settings returns non-empty ideRunningWarning when IDE running");
                    AssertTrue(File.ReadAllText(cfgPath, Encoding.UTF8).Contains("CASCADE_COMMANDS_AUTO_EXECUTION_AUTO"),
                        "T78: settings persisted config.json while warning");

                    RawHttpTestResponse togPlugOn = SendRawTcpHttp(host.Port, null, "POST", "/api/cockpit/toggle", apiHdr,
                        "{\"kind\":\"plugin\",\"id\":\"plug-78\",\"enabled\":true}");
                    AssertEqual(200, togPlugOn.StatusCode, "T78: POST /api/cockpit/toggle plugin (IDE running) returns 200");
                    Dictionary<string, object> togPlugOnDict = UnifiedJson.AsDict(UnifiedJson.Parse(togPlugOn.BodyText));
                    AssertTrue(UnifiedJson.GetBool(togPlugOnDict, "ok", false) && UnifiedJson.GetBool(togPlugOnDict, "enabled", false),
                        "T78: plugin toggle succeeded");
                    AssertTrue(UnifiedJson.GetBool(togPlugOnDict, "ideRunning", false), "T78: plugin toggle ideRunning=true");
                    AssertTrue(!string.IsNullOrEmpty(UnifiedJson.GetString(togPlugOnDict, "ideRunningWarning", "")),
                        "T78: plugin toggle returns ideRunningWarning when IDE running");

                    RawHttpTestResponse togMcpOff = SendRawTcpHttp(host.Port, null, "POST", "/api/cockpit/toggle", apiHdr,
                        "{\"kind\":\"mcp\",\"id\":\"srv-78\",\"enabled\":false}");
                    AssertEqual(200, togMcpOff.StatusCode, "T78: POST /api/cockpit/toggle mcp (IDE running) returns 200");
                    Dictionary<string, object> togMcpOffDict = UnifiedJson.AsDict(UnifiedJson.Parse(togMcpOff.BodyText));
                    AssertTrue(UnifiedJson.GetBool(togMcpOffDict, "ok", false) && !UnifiedJson.GetBool(togMcpOffDict, "enabled", true),
                        "T78: mcp toggle disabled succeeded");
                    AssertTrue(UnifiedJson.GetBool(togMcpOffDict, "ideRunning", false), "T78: mcp toggle ideRunning=true");
                    AssertTrue(!string.IsNullOrEmpty(UnifiedJson.GetString(togMcpOffDict, "ideRunningWarning", "")),
                        "T78: mcp toggle returns ideRunningWarning when IDE running");

                    // Case 2: IdeRunningOverride == false
                    host.IdeRunningOverride = delegate() { return false; };

                    RawHttpTestResponse setOff = SendRawTcpHttp(host.Port, null, "POST", "/api/cockpit/settings", apiHdr,
                        "{\"userSettings\":{\"autoExecutionPolicy\":\"CASCADE_COMMANDS_AUTO_EXECUTION_OFF\"}}");
                    AssertEqual(200, setOff.StatusCode, "T78: POST /api/cockpit/settings (IDE stopped) returns 200");
                    Dictionary<string, object> setOffDict = UnifiedJson.AsDict(UnifiedJson.Parse(setOff.BodyText));
                    AssertTrue(!UnifiedJson.GetBool(setOffDict, "ideRunning", true), "T78: settings ideRunning=false when IDE stopped");
                    AssertEqual(string.Empty, UnifiedJson.GetString(setOffDict, "ideRunningWarning", "NON_EMPTY"),
                        "T78: settings ideRunningWarning empty when IDE stopped");

                    RawHttpTestResponse togPlugOff = SendRawTcpHttp(host.Port, null, "POST", "/api/cockpit/toggle", apiHdr,
                        "{\"kind\":\"plugin\",\"id\":\"plug-78\",\"enabled\":false}");
                    AssertEqual(200, togPlugOff.StatusCode, "T78: POST /api/cockpit/toggle plugin (IDE stopped) returns 200");
                    Dictionary<string, object> togPlugOffDict = UnifiedJson.AsDict(UnifiedJson.Parse(togPlugOff.BodyText));
                    AssertTrue(!UnifiedJson.GetBool(togPlugOffDict, "ideRunning", true), "T78: plugin toggle ideRunning=false when IDE stopped");
                    AssertEqual(string.Empty, UnifiedJson.GetString(togPlugOffDict, "ideRunningWarning", "NON_EMPTY"),
                        "T78: plugin toggle ideRunningWarning empty when IDE stopped");

                    RawHttpTestResponse togMcpOn = SendRawTcpHttp(host.Port, null, "POST", "/api/cockpit/toggle", apiHdr,
                        "{\"kind\":\"mcp\",\"id\":\"srv-78\",\"enabled\":true}");
                    AssertEqual(200, togMcpOn.StatusCode, "T78: POST /api/cockpit/toggle mcp (IDE stopped) returns 200");
                    Dictionary<string, object> togMcpOnDict = UnifiedJson.AsDict(UnifiedJson.Parse(togMcpOn.BodyText));
                    AssertTrue(!UnifiedJson.GetBool(togMcpOnDict, "ideRunning", true), "T78: mcp toggle ideRunning=false when IDE stopped");
                    AssertEqual(string.Empty, UnifiedJson.GetString(togMcpOnDict, "ideRunningWarning", "NON_EMPTY"),
                        "T78: mcp toggle ideRunningWarning empty when IDE stopped");

                    host.IdeRunningOverride = null;
                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T79_ACC05_ACC09_StartupRollbackAndHardenedDefaults()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t79_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                Directory.CreateDirectory(Path.Combine(geminiDir, "antigravity"));

                // ACC-09: Direct CompanionSettings constructor, NormalizeSettings, and FromDictionary defaults
                CompanionSettings defCtor = new CompanionSettings();
                AssertEqual(false, defCtor.MinimizeToTray, "T79: ACC-09 default MinimizeToTray=false");
                AssertEqual(string.Empty, defCtor.GlobalHotkey, "T79: ACC-09 default GlobalHotkey=\"\"");
                AssertEqual(false, defCtor.Enabled, "T79: ACC-09 default Enabled=false");
                AssertEqual(false, defCtor.StartWithWindows, "T79: ACC-09 default StartWithWindows=false");
                AssertEqual(0, defCtor.RuleTokenReferenceThreshold, "T79: ACC-09 default RuleTokenReferenceThreshold=0");

                CompanionSettings defNorm = ScheduledSnapshotService.NormalizeSettings(null, geminiDir);
                AssertEqual(false, defNorm.MinimizeToTray, "T79: NormalizeSettings(null) MinimizeToTray=false");
                AssertEqual(string.Empty, defNorm.GlobalHotkey, "T79: NormalizeSettings(null) GlobalHotkey=\"\"");
                AssertEqual(false, defNorm.Enabled, "T79: NormalizeSettings(null) Enabled=false");
                AssertEqual(false, defNorm.StartWithWindows, "T79: NormalizeSettings(null) StartWithWindows=false");
                AssertEqual(0, defNorm.RuleTokenReferenceThreshold, "T79: NormalizeSettings(null) RuleTokenReferenceThreshold=0");

                CompanionSettings defDict = ScheduledSnapshotService.FromDictionary(new Dictionary<string, object>(), geminiDir);
                AssertEqual(false, defDict.MinimizeToTray, "T79: FromDictionary({}) MinimizeToTray=false");
                AssertEqual(string.Empty, defDict.GlobalHotkey, "T79: FromDictionary({}) GlobalHotkey=\"\"");
                AssertEqual(false, defDict.Enabled, "T79: FromDictionary({}) Enabled=false");
                AssertEqual(false, defDict.StartWithWindows, "T79: FromDictionary({}) StartWithWindows=false");
                AssertEqual(0, defDict.RuleTokenReferenceThreshold, "T79: FromDictionary({}) RuleTokenReferenceThreshold=0");

                Dictionary<string, object> defToDict = ScheduledSnapshotService.ToDictionary(defCtor);
                AssertEqual(false, UnifiedJson.GetBool(defToDict, "minimizeToTray", true), "T79: ToDictionary default minimizeToTray=false");
                AssertEqual(string.Empty, UnifiedJson.GetString(defToDict, "globalHotkey", "x"), "T79: ToDictionary default globalHotkey=\"\"");
                AssertEqual(false, UnifiedJson.GetBool(defToDict, "enabled", true), "T79: ToDictionary default enabled=false");
                AssertEqual(false, UnifiedJson.GetBool(defToDict, "startWithWindows", true), "T79: ToDictionary default startWithWindows=false");
                AssertEqual(0, UnifiedJson.GetInt(defToDict, "ruleTokenReferenceThreshold", -1), "T79: ToDictionary default ruleTokenReferenceThreshold=0");

                // Invalid key inputs to SetStartWithWindowsInKey fail gracefully, and isolated HKCU test subkey roundtrips cleanly
                AssertTrue(!ScheduledSnapshotService.SetStartWithWindowsInKey(null, "sub", "val", true, @"C:\app.exe"),
                    "T79: SetStartWithWindowsInKey handles null rootKey safely");
                AssertTrue(!ScheduledSnapshotService.SetStartWithWindowsInKey(Microsoft.Win32.Registry.CurrentUser, "", "val", true, @"C:\app.exe"),
                    "T79: SetStartWithWindowsInKey handles empty subKey safely");
                string testRegSubKey = @"Software\AntigravityCompanion_T79_" + Guid.NewGuid().ToString("N");
                try
                {
                    AssertTrue(ScheduledSnapshotService.SetStartWithWindowsInKey(Microsoft.Win32.Registry.CurrentUser, testRegSubKey, "TestVal", true, @"C:\Test\AntigravityCompanion.exe"),
                        "T79: SetStartWithWindowsInKey wrote isolated HKCU test key");
                    AssertTrue(ScheduledSnapshotService.IsStartWithWindowsEnabledInKey(Microsoft.Win32.Registry.CurrentUser, testRegSubKey, "TestVal"),
                        "T79: IsStartWithWindowsEnabledInKey detected written value");
                    AssertTrue(ScheduledSnapshotService.SetStartWithWindowsInKey(Microsoft.Win32.Registry.CurrentUser, testRegSubKey, "TestVal", false, @"C:\Test\AntigravityCompanion.exe"),
                        "T79: SetStartWithWindowsInKey removed isolated HKCU test value");
                    AssertTrue(!ScheduledSnapshotService.IsStartWithWindowsEnabledInKey(Microsoft.Win32.Registry.CurrentUser, testRegSubKey, "TestVal"),
                        "T79: IsStartWithWindowsEnabledInKey returned false after removal");
                }
                finally
                {
                    try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(testRegSubKey, false); } catch { }
                }

                string token = "t79_tok";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, Path.Combine(sb, "Vault"), 0, token))
                {
                    host.Start();

                    // ACC-09 via HTTP GET /api/settings/companion
                    RawHttpTestResponse getDef = SendRawTcpHttp(host.Port, null, "GET", "/api/settings/companion", apiHdr, null);
                    AssertEqual(200, getDef.StatusCode, "T79: GET /api/settings/companion returns 200");
                    Dictionary<string, object> defPayload = UnifiedJson.AsDict(UnifiedJson.Parse(getDef.BodyText));
                    AssertEqual(false, UnifiedJson.GetBool(defPayload, "minimizeToTray", true), "T79: HTTP default minimizeToTray=false");
                    AssertEqual(string.Empty, UnifiedJson.GetString(defPayload, "globalHotkey", "x"), "T79: HTTP default globalHotkey=\"\"");
                    AssertEqual(false, UnifiedJson.GetBool(defPayload, "enabled", true), "T79: HTTP default enabled=false");
                    AssertEqual(false, UnifiedJson.GetBool(defPayload, "startWithWindows", true), "T79: HTTP default startWithWindows=false");
                    AssertEqual(0, UnifiedJson.GetInt(defPayload, "ruleTokenReferenceThreshold", -1), "T79: HTTP default ruleTokenReferenceThreshold=0");
                    AssertEqual(false, UnifiedJson.GetBool(defPayload, "hotkeyRegistered", true), "T79: HTTP default hotkeyRegistered=false");

                    // ACC-05 Direction 1: false -> true fails, rolls back to false while preserving other fields
                    int hookCalls = 0;
                    host.SetStartWithWindowsOverride = delegate(bool en, string exe) { hookCalls++; return false; };
                    RawHttpTestResponse failOn = SendRawTcpHttp(host.Port, null, "POST", "/api/settings/companion", apiHdr,
                        "{\"startWithWindows\":true,\"retainCount\":7}");
                    AssertEqual(200, failOn.StatusCode, "T79: POST /api/settings/companion rollback (false->true) returns 200");
                    Dictionary<string, object> failOnDict = UnifiedJson.AsDict(UnifiedJson.Parse(failOn.BodyText));
                    AssertEqual(1, hookCalls, "T79: SetStartWithWindowsOverride invoked");
                    AssertTrue(UnifiedJson.GetBool(failOnDict, "startupRollback", false), "T79: startupRollback=true on false->true failure");
                    AssertTrue(!string.IsNullOrEmpty(UnifiedJson.GetString(failOnDict, "startupError", "")), "T79: startupError populated");
                    AssertEqual(false, UnifiedJson.GetBool(failOnDict, "startWithWindows", true), "T79: startWithWindows rolled back to false");
                    AssertEqual(7, UnifiedJson.GetInt(failOnDict, "retainCount", 0), "T79: non-startup field retainCount=7 persisted");

                    string diskSettings = File.ReadAllText(host.SnapshotService.SettingsFilePath, Encoding.UTF8);
                    Dictionary<string, object> diskDict = UnifiedJson.AsDict(UnifiedJson.Parse(diskSettings));
                    AssertEqual(false, UnifiedJson.GetBool(diskDict, "startWithWindows", true), "T79: Disk settings file persisted rolled-back startWithWindows=false");

                    // ACC-05 Direction 2: enable succeeds (false -> true), then disabling (true -> false) fails and rolls back to true
                    host.SetStartWithWindowsOverride = delegate(bool en, string exe) { return true; };
                    RawHttpTestResponse okOn = SendRawTcpHttp(host.Port, null, "POST", "/api/settings/companion", apiHdr,
                        "{\"startWithWindows\":true}");
                    AssertEqual(200, okOn.StatusCode, "T79: Enabling startWithWindows succeeds when registry call succeeds");
                    Dictionary<string, object> okOnDict = UnifiedJson.AsDict(UnifiedJson.Parse(okOn.BodyText));
                    AssertEqual(true, UnifiedJson.GetBool(okOnDict, "startWithWindows", false), "T79: startWithWindows=true persisted");
                    AssertEqual(false, UnifiedJson.GetBool(okOnDict, "startupRollback", false), "T79: No rollback when registry call succeeds");

                    host.SetStartWithWindowsOverride = delegate(bool en, string exe) { return false; };
                    RawHttpTestResponse failOff = SendRawTcpHttp(host.Port, null, "POST", "/api/settings/companion", apiHdr,
                        "{\"startWithWindows\":false}");
                    AssertEqual(200, failOff.StatusCode, "T79: POST /api/settings/companion rollback (true->false) returns 200");
                    Dictionary<string, object> failOffDict = UnifiedJson.AsDict(UnifiedJson.Parse(failOff.BodyText));
                    AssertTrue(UnifiedJson.GetBool(failOffDict, "startupRollback", false), "T79: startupRollback=true on true->false failure");
                    AssertTrue(!string.IsNullOrEmpty(UnifiedJson.GetString(failOffDict, "startupError", "")), "T79: startupError populated on true->false failure");
                    AssertEqual(true, UnifiedJson.GetBool(failOffDict, "startWithWindows", false), "T79: startWithWindows rolled back to previous true");

                    string diskSettings2 = File.ReadAllText(host.SnapshotService.SettingsFilePath, Encoding.UTF8);
                    Dictionary<string, object> diskDict2 = UnifiedJson.AsDict(UnifiedJson.Parse(diskSettings2));
                    AssertEqual(true, UnifiedJson.GetBool(diskDict2, "startWithWindows", false), "T79: Disk settings file persisted rolled-back startWithWindows=true");

                    host.SetStartWithWindowsOverride = null;
                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static void T80_ACC06_ACC10_SecurityNormalizationAndTokenBudget()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t80_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string cfgDir = Path.Combine(geminiDir, "config");
                string projDir = Path.Combine(cfgDir, "projects");
                string vaultDir = Path.Combine(sb, "Vault");

                string someRandomInHome = Path.Combine(homeDir, "SomeRandomFolder");
                string someRandomOutside = Path.Combine(sb, "OutsideRandomFolder");
                string regWsDir = Path.Combine(sb, "RegWs80");
                string varWsDir = Path.Combine(sb, "Proj%VAR%80");
                string ampWsDir = Path.Combine(sb, "Proj&80");
                string emptyLocalApp = Path.Combine(sb, "EmptyLocalApp");

                Directory.CreateDirectory(Path.Combine(geminiDir, "antigravity"));
                Directory.CreateDirectory(projDir);
                Directory.CreateDirectory(vaultDir);
                Directory.CreateDirectory(someRandomInHome);
                Directory.CreateDirectory(someRandomOutside);
                Directory.CreateDirectory(regWsDir);
                Directory.CreateDirectory(varWsDir);
                Directory.CreateDirectory(ampWsDir);
                Directory.CreateDirectory(emptyLocalApp);

                File.WriteAllText(Path.Combine(projDir, "p80reg.json"),
                    WorkspaceService.GenerateStandardProjectJson("p80reg", "RegWs80", regWsDir), Encoding.UTF8);
                File.WriteAllText(Path.Combine(projDir, "p80var.json"),
                    WorkspaceService.GenerateStandardProjectJson("p80var", "Proj%VAR%80", varWsDir), Encoding.UTF8);
                File.WriteAllText(Path.Combine(projDir, "p80amp.json"),
                    WorkspaceService.GenerateStandardProjectJson("p80amp", "Proj&80", ampWsDir), Encoding.UTF8);

                string ideBinDir = Path.Combine(geminiDir, "antigravity-ide", "bin");
                Directory.CreateDirectory(ideBinDir);
                File.WriteAllText(Path.Combine(ideBinDir, "antigravity-ide.cmd"), "@echo off\r\n", Encoding.UTF8);

                // ACC-06: Drive root normalization & explorer drive root handling
                AssertEqual(@"D:\", WorkspaceLauncherService.NormalizeDirArg(@"D:\"), "T80: NormalizeDirArg preserves D:\\");
                AssertEqual(@"D:\", WorkspaceLauncherService.NormalizeDirArg(@"D:/"), "T80: NormalizeDirArg converts D:/ to D:\\");
                AssertEqual(@"d:\", WorkspaceLauncherService.NormalizeDirArg(@"d:\"), "T80: NormalizeDirArg preserves lowercase drive root d:\\");
                AssertEqual(@"D:\Sub\Dir", WorkspaceLauncherService.NormalizeDirArg(@"D:\Sub\Dir\"), "T80: NormalizeDirArg strips trailing slash on non-root");

                string exCanon, exArgs, exErr;
                int exCode;
                AssertTrue(ApiRouter.TryValidateExplorerTarget(@"D:\", new string[] { @"D:\" }, out exCanon, out exArgs, out exCode, out exErr),
                    "T80: TryValidateExplorerTarget allows registered drive root");
                AssertEqual(@"D:\", exArgs, "T80: TryValidateExplorerTarget preserves drive root backslash in explorerArgs");
                AssertTrue(ApiRouter.TryValidateExplorerTarget(@"D:/", new string[] { @"D:\" }, out exCanon, out exArgs, out exCode, out exErr),
                    "T80: TryValidateExplorerTarget allows D:/ normalized against D:\\");
                AssertEqual(@"D:\", exArgs, "T80: TryValidateExplorerTarget normalizes D:/ to D:\\");

                // ACC-06: .cmd %VAR% and & metacharacter detection & rejection (400)
                AssertTrue(WorkspaceLauncherService.ContainsUnsafeCmdMetacharacters(@"C:\Work\Proj%VAR%"),
                    "T80: Detects %VAR% metacharacter");
                AssertTrue(WorkspaceLauncherService.ContainsUnsafeCmdMetacharacters(@"C:\Work\Proj&80"),
                    "T80: Detects & metacharacter");

                WorkspaceLaunchResult varCliRes = WorkspaceLauncherService.LaunchWorkspace(geminiDir, homeDir, vaultDir, varWsDir, "cli");
                AssertTrue(!varCliRes.Ok && varCliRes.HttpStatusCode == 400, "T80: Rejects %VAR% in .cmd CLI launch with HTTP 400");

                WorkspaceLaunchResult varIdeCmdRes = WorkspaceLauncherService.LaunchWorkspace(geminiDir, homeDir, vaultDir, varWsDir, "ide", emptyLocalApp, "");
                AssertTrue(!varIdeCmdRes.Ok && varIdeCmdRes.HttpStatusCode == 400, "T80: Rejects %VAR% in IDE .cmd fallback with HTTP 400");

                WorkspaceLaunchResult ampCliRes = WorkspaceLauncherService.LaunchWorkspace(geminiDir, homeDir, vaultDir, ampWsDir, "cli");
                AssertTrue(!ampCliRes.Ok && ampCliRes.HttpStatusCode == 400, "T80: Rejects & in .cmd CLI launch with HTTP 400");

                WorkspaceLaunchResult ampIdeCmdRes = WorkspaceLauncherService.LaunchWorkspace(geminiDir, homeDir, vaultDir, ampWsDir, "ide", emptyLocalApp, "");
                AssertTrue(!ampIdeCmdRes.Ok && ampIdeCmdRes.HttpStatusCode == 400, "T80: Rejects & in IDE .cmd fallback with HTTP 400");

                // ACC-10: CJK & ASCII EstimateTokenCount unit tests + threshold=0 vs exact boundary vs custom threshold
                AssertEqual(0, AntigravityConfigService.EstimateTokenCount(null), "T80: EstimateTokenCount(null) == 0");
                AssertEqual(0, AntigravityConfigService.EstimateTokenCount(""), "T80: EstimateTokenCount(\"\") == 0");
                AssertEqual(1, AntigravityConfigService.EstimateTokenCount("a"), "T80: EstimateTokenCount 1 ASCII char == 1");
                AssertEqual(1, AntigravityConfigService.EstimateTokenCount("中"), "T80: EstimateTokenCount 1 CJK char == 1");
                AssertEqual(100, AntigravityConfigService.EstimateTokenCount(new string('a', 400)), "T80: EstimateTokenCount 400 ASCII chars == 100");
                AssertEqual(100, AntigravityConfigService.EstimateTokenCount(new string('中', 150)), "T80: EstimateTokenCount 150 CJK chars == 100");
                AssertEqual(30, AntigravityConfigService.EstimateTokenCount(new string('中', 30) + new string('a', 40)),
                    "T80: EstimateTokenCount mixed 30 CJK + 40 ASCII == 30");

                string geminiMdPath = Path.Combine(geminiDir, "GEMINI.md");
                File.WriteAllText(geminiMdPath, new string('中', 300), Encoding.UTF8); // 300 CJK chars => 200 tokens

                CockpitSnapshot snapZero = AntigravityConfigService.ScanCockpit(geminiDir, homeDir, vaultDir, 0);
                AssertEqual(0, snapZero.OverBudgetCount, "T80: ScanCockpit threshold=0 yields OverBudgetCount=0");
                AssertTrue(snapZero.Rules.Count == 1 && !snapZero.Rules[0].OverBudget && snapZero.Rules[0].EstimatedTokens == 200,
                    "T80: ScanCockpit threshold=0 rule OverBudget=false with EstimatedTokens=200");

                CockpitSnapshot snapEqual = AntigravityConfigService.ScanCockpit(geminiDir, homeDir, vaultDir, 200);
                AssertEqual(0, snapEqual.OverBudgetCount, "T80: ScanCockpit exact threshold==tokens (200==200) is NOT OverBudget");

                CockpitSnapshot snapOver1 = AntigravityConfigService.ScanCockpit(geminiDir, homeDir, vaultDir, 199);
                AssertEqual(1, snapOver1.OverBudgetCount, "T80: ScanCockpit threshold=199 flags 200 tokens as OverBudget");

                CockpitSnapshot snapCustom = AntigravityConfigService.ScanCockpit(geminiDir, homeDir, vaultDir, 150);
                AssertEqual(1, snapCustom.OverBudgetCount, "T80: ScanCockpit threshold=150 yields OverBudgetCount=1");
                AssertTrue(snapCustom.Rules.Count == 1 && snapCustom.Rules[0].OverBudget,
                    "T80: ScanCockpit threshold=150 rule OverBudget=true");

                EditorFileResult efZero = AntigravityConfigService.ReadAllowedCustomizationFile(geminiDir, homeDir, vaultDir, geminiMdPath, 0);
                AssertTrue(efZero.Ok && !efZero.OverBudget && efZero.EstimatedTokens == 200,
                    "T80: ReadAllowedCustomizationFile threshold=0 OverBudget=false");
                EditorFileResult efEqual = AntigravityConfigService.ReadAllowedCustomizationFile(geminiDir, homeDir, vaultDir, geminiMdPath, 200);
                AssertTrue(efEqual.Ok && !efEqual.OverBudget && efEqual.EstimatedTokens == 200,
                    "T80: ReadAllowedCustomizationFile exact threshold==tokens (200==200) OverBudget=false");
                EditorFileResult efCustom = AntigravityConfigService.ReadAllowedCustomizationFile(geminiDir, homeDir, vaultDir, geminiMdPath, 150);
                AssertTrue(efCustom.Ok && efCustom.OverBudget && efCustom.EstimatedTokens == 200,
                    "T80: ReadAllowedCustomizationFile threshold=150 OverBudget=true");

                // HTTP E2E for ACC-06 (403 on unregistered SomeRandomFolder & 400 on %VAR%/&) + ACC-10 (HTTP threshold toggling)
                string token = "t80_tok";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, vaultDir, 0, token))
                {
                    host.Start();

                    // Unregistered %USERPROFILE%\SomeRandomFolder (and homeDir itself) -> 403 on BOTH /api/workspace/launch & /api/native/open-in-explorer
                    RawHttpTestResponse launchHomeRoot = SendRawTcpHttp(host.Port, null, "POST", "/api/workspace/launch", apiHdr,
                        "{\"path\":\"" + UnifiedJson.EscapeString(homeDir) + "\",\"tool\":\"explorer\"}");
                    AssertEqual(403, launchHomeRoot.StatusCode, "T80: POST /api/workspace/launch rejects HomePath itself with 403");

                    RawHttpTestResponse launchHomeRand = SendRawTcpHttp(host.Port, null, "POST", "/api/workspace/launch", apiHdr,
                        "{\"path\":\"" + UnifiedJson.EscapeString(someRandomInHome) + "\",\"tool\":\"explorer\"}");
                    AssertEqual(403, launchHomeRand.StatusCode, "T80: POST /api/workspace/launch rejects Home\\SomeRandomFolder with 403");

                    RawHttpTestResponse launchOutRand = SendRawTcpHttp(host.Port, null, "POST", "/api/workspace/launch", apiHdr,
                        "{\"path\":\"" + UnifiedJson.EscapeString(someRandomOutside) + "\",\"tool\":\"explorer\"}");
                    AssertEqual(403, launchOutRand.StatusCode, "T80: POST /api/workspace/launch rejects OutsideRandomFolder with 403");

                    RawHttpTestResponse expHomeRand = SendRawTcpHttp(host.Port, null, "POST", "/api/native/open-in-explorer", apiHdr,
                        "{\"path\":\"" + UnifiedJson.EscapeString(someRandomInHome) + "\"}");
                    AssertEqual(403, expHomeRand.StatusCode, "T80: POST /api/native/open-in-explorer rejects Home\\SomeRandomFolder with 403");

                    RawHttpTestResponse expOutRand = SendRawTcpHttp(host.Port, null, "POST", "/api/native/open-in-explorer", apiHdr,
                        "{\"path\":\"" + UnifiedJson.EscapeString(someRandomOutside) + "\"}");
                    AssertEqual(403, expOutRand.StatusCode, "T80: POST /api/native/open-in-explorer rejects OutsideRandomFolder with 403");

                    // Registered .cmd %VAR% / & metacharacter rejection via HTTP
                    RawHttpTestResponse launchVarHttp = SendRawTcpHttp(host.Port, null, "POST", "/api/workspace/launch", apiHdr,
                        "{\"path\":\"" + UnifiedJson.EscapeString(varWsDir) + "\",\"tool\":\"cli\"}");
                    AssertEqual(400, launchVarHttp.StatusCode, "T80: HTTP POST /api/workspace/launch rejects %VAR% in .cmd path with 400");

                    RawHttpTestResponse launchAmpHttp = SendRawTcpHttp(host.Port, null, "POST", "/api/workspace/launch", apiHdr,
                        "{\"path\":\"" + UnifiedJson.EscapeString(ampWsDir) + "\",\"tool\":\"cli\"}");
                    AssertEqual(400, launchAmpHttp.StatusCode, "T80: HTTP POST /api/workspace/launch rejects & in .cmd path with 400");

                    // ACC-10 via HTTP: threshold=0 -> overBudget=false; custom threshold=150 -> overBudget=true
                    RawHttpTestResponse sum0 = SendRawTcpHttp(host.Port, null, "GET", "/api/cockpit/summary", apiHdr, null);
                    AssertEqual(200, sum0.StatusCode, "T80: GET /api/cockpit/summary (threshold=0) returns 200");
                    Dictionary<string, object> sum0Dict = UnifiedJson.AsDict(UnifiedJson.Parse(sum0.BodyText));
                    Dictionary<string, object> tb0 = UnifiedJson.AsDict(sum0Dict["tokenBudget"]);
                    AssertEqual(0, UnifiedJson.GetInt(tb0, "threshold", -1), "T80: HTTP summary default threshold=0");
                    AssertEqual(0, UnifiedJson.GetInt(tb0, "overBudgetCount", -1), "T80: HTTP summary threshold=0 overBudgetCount=0");

                    RawHttpTestResponse updThresh = SendRawTcpHttp(host.Port, null, "POST", "/api/settings/companion", apiHdr,
                        "{\"ruleTokenReferenceThreshold\":150}");
                    AssertEqual(200, updThresh.StatusCode, "T80: Set ruleTokenReferenceThreshold=150 returns 200");

                    RawHttpTestResponse sum150 = SendRawTcpHttp(host.Port, null, "GET", "/api/cockpit/summary", apiHdr, null);
                    AssertEqual(200, sum150.StatusCode, "T80: GET /api/cockpit/summary (threshold=150) returns 200");
                    Dictionary<string, object> sum150Dict = UnifiedJson.AsDict(UnifiedJson.Parse(sum150.BodyText));
                    Dictionary<string, object> tb150 = UnifiedJson.AsDict(sum150Dict["tokenBudget"]);
                    AssertEqual(150, UnifiedJson.GetInt(tb150, "threshold", -1), "T80: HTTP summary custom threshold=150");
                    AssertEqual(1, UnifiedJson.GetInt(tb150, "overBudgetCount", -1), "T80: HTTP summary threshold=150 overBudgetCount=1");

                    RawHttpTestResponse ed150 = SendRawTcpHttp(host.Port, null, "GET",
                        "/api/editor/file?path=" + Uri.EscapeDataString(geminiMdPath), apiHdr, null);
                    AssertEqual(200, ed150.StatusCode, "T80: GET /api/editor/file returns 200");
                    Dictionary<string, object> ed150Dict = UnifiedJson.AsDict(UnifiedJson.Parse(ed150.BodyText));
                    AssertEqual(200, UnifiedJson.GetInt(ed150Dict, "estimatedTokens", 0), "T80: HTTP editor/file estimatedTokens=200");
                    AssertEqual(true, UnifiedJson.GetBool(ed150Dict, "overBudget", false), "T80: HTTP editor/file overBudget=true at threshold=150");

                    RawHttpTestResponse edSave150 = SendRawTcpHttp(host.Port, null, "POST", "/api/editor/save", apiHdr,
                        "{\"path\":\"" + UnifiedJson.EscapeString(geminiMdPath) + "\",\"content\":\"" + new string('中', 300) + "\"}");
                    AssertEqual(200, edSave150.StatusCode, "T80: POST /api/editor/save (threshold=150) returns 200");
                    Dictionary<string, object> edSave150Dict = UnifiedJson.AsDict(UnifiedJson.Parse(edSave150.BodyText));
                    AssertEqual(true, UnifiedJson.GetBool(edSave150Dict, "overBudget", false), "T80: HTTP editor/save overBudget=true at threshold=150");

                    RawHttpTestResponse resetThresh = SendRawTcpHttp(host.Port, null, "POST", "/api/settings/companion", apiHdr,
                        "{\"ruleTokenReferenceThreshold\":0}");
                    AssertEqual(200, resetThresh.StatusCode, "T80: Reset ruleTokenReferenceThreshold=0 returns 200");
                    RawHttpTestResponse edSave0 = SendRawTcpHttp(host.Port, null, "POST", "/api/editor/save", apiHdr,
                        "{\"path\":\"" + UnifiedJson.EscapeString(geminiMdPath) + "\",\"content\":\"" + new string('中', 300) + "\"}");
                    AssertEqual(200, edSave0.StatusCode, "T80: POST /api/editor/save (threshold=0) returns 200");
                    Dictionary<string, object> edSave0Dict = UnifiedJson.AsDict(UnifiedJson.Parse(edSave0.BodyText));
                    AssertEqual(false, UnifiedJson.GetBool(edSave0Dict, "overBudget", true), "T80: HTTP editor/save overBudget=false when threshold=0");

                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // T81: Phase 1 — IDE_CLOSE_TIMEOUT two-step force-kill escape hatch
        // ══════════════════════════════════════════════════════════════════════
        private static void T81_Phase1_IdeCloseTimeoutEscapeHatchAndStorageSlimGuard()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t81_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agyDir = Path.Combine(geminiDir, "antigravity");
                string convDir = Path.Combine(agyDir, "conversations");
                string vaultDir = Path.Combine(sb, "Vault");
                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(vaultDir);

                string cid = "11111111-1111-1111-1111-111111111181";
                ProtoNode entry = BuildRealProtoEntry(cid, "Initial Title", string.Empty, "file:///c:/proj81", "Proj81", "pid-81", 4, 2, null);
                File.WriteAllBytes(Path.Combine(agyDir, "agyhub_summaries_proto.pb"), ProtoCodec.Encode(new List<ProtoNode> { entry }));
                CreateMinimalValidSqliteDb(Path.Combine(convDir, cid + ".db"), "t81");
                // Seed an orphan so gc/clean has real work
                File.WriteAllText(Path.Combine(convDir, "deadbeef-dead-beef-dead-beef00000081.db.bak"), "orphan bak", Encoding.UTF8);

                string token = "t81_tok_escape";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, vaultDir, 0, token))
                {
                    bool simulatedRunning = true;
                    host.IdeRunningOverride = delegate() { return simulatedRunning; };
                    host.CloseIdeOverride = delegate(bool forceKill)
                    {
                        if (!forceKill) return "IDE_CLOSE_TIMEOUT: Simulated graceful close timeout";
                        simulatedRunning = false;
                        return null;
                    };
                    host.Start();

                    // 1. /api/storage/slim without closeIdeIfRunning -> 409 IDE_RUNNING
                    RawHttpTestResponse slimBlock = SendRawTcpHttp(host.Port, null, "POST", "/api/storage/slim", apiHdr,
                        "{\"mode\":\"orphanGcOnly\",\"cleanOrphans\":true}");
                    AssertEqual(409, slimBlock.StatusCode, "T81: /api/storage/slim blocks with 409 when IDE running");
                    Dictionary<string, object> slimBlockDict = UnifiedJson.AsDict(UnifiedJson.Parse(slimBlock.BodyText));
                    AssertEqual("IDE_RUNNING", UnifiedJson.GetString(slimBlockDict, "code", string.Empty), "T81: /api/storage/slim code=IDE_RUNNING");

                    // 2. /api/storage/slim with closeIdeIfRunning=true, forceKill=false -> 409 IDE_CLOSE_TIMEOUT + canForceKill=true
                    RawHttpTestResponse slimTimeout = SendRawTcpHttp(host.Port, null, "POST", "/api/storage/slim", apiHdr,
                        "{\"mode\":\"orphanGcOnly\",\"cleanOrphans\":true,\"closeIdeIfRunning\":true,\"forceKill\":false}");
                    AssertEqual(409, slimTimeout.StatusCode, "T81: /api/storage/slim returns 409 on graceful close timeout");
                    Dictionary<string, object> slimTimeoutDict = UnifiedJson.AsDict(UnifiedJson.Parse(slimTimeout.BodyText));
                    AssertEqual("IDE_CLOSE_TIMEOUT", UnifiedJson.GetString(slimTimeoutDict, "code", string.Empty), "T81: /api/storage/slim code=IDE_CLOSE_TIMEOUT");
                    AssertEqual(true, UnifiedJson.GetBool(slimTimeoutDict, "canForceKill", false), "T81: /api/storage/slim canForceKill=true");

                    // 3. /api/storage/slim with forceKill=true -> 200 OK
                    RawHttpTestResponse slimForce = SendRawTcpHttp(host.Port, null, "POST", "/api/storage/slim", apiHdr,
                        "{\"mode\":\"orphanGcOnly\",\"cleanOrphans\":true,\"closeIdeIfRunning\":true,\"forceKill\":true}");
                    AssertEqual(200, slimForce.StatusCode, "T81: /api/storage/slim succeeds with forceKill=true");
                    AssertEqual(false, simulatedRunning, "T81: forceKill override flipped simulated IDE off");

                    // 4. /api/ide/close timeout then forceKill
                    simulatedRunning = true;
                    RawHttpTestResponse closeTimeout = SendRawTcpHttp(host.Port, null, "POST", "/api/ide/close", apiHdr, "{\"forceKill\":false}");
                    AssertEqual(409, closeTimeout.StatusCode, "T81: /api/ide/close returns 409 on graceful timeout");
                    Dictionary<string, object> closeTimeoutDict = UnifiedJson.AsDict(UnifiedJson.Parse(closeTimeout.BodyText));
                    AssertEqual("IDE_CLOSE_TIMEOUT", UnifiedJson.GetString(closeTimeoutDict, "code", string.Empty), "T81: /api/ide/close code=IDE_CLOSE_TIMEOUT");
                    AssertEqual(true, UnifiedJson.GetBool(closeTimeoutDict, "canForceKill", false), "T81: /api/ide/close canForceKill=true");

                    RawHttpTestResponse closeForce = SendRawTcpHttp(host.Port, null, "POST", "/api/ide/close", apiHdr, "{\"forceKill\":true}");
                    AssertEqual(200, closeForce.StatusCode, "T81: /api/ide/close forceKill=true returns 200");

                    // 5. /api/gc/clean timeout then forceKill
                    simulatedRunning = true;
                    RawHttpTestResponse gcTimeout = SendRawTcpHttp(host.Port, null, "POST", "/api/gc/clean", apiHdr,
                        "{\"closeIdeIfRunning\":true,\"forceKill\":false}");
                    AssertEqual(409, gcTimeout.StatusCode, "T81: /api/gc/clean returns 409 IDE_CLOSE_TIMEOUT");
                    Dictionary<string, object> gcTimeoutDict = UnifiedJson.AsDict(UnifiedJson.Parse(gcTimeout.BodyText));
                    AssertEqual("IDE_CLOSE_TIMEOUT", UnifiedJson.GetString(gcTimeoutDict, "code", string.Empty), "T81: /api/gc/clean code=IDE_CLOSE_TIMEOUT");

                    // 6. /api/rename/execute CloseAndFullWrite timeout
                    RawHttpTestResponse renTimeout = SendRawTcpHttp(host.Port, null, "POST", "/api/rename/execute", apiHdr,
                        "{\"strategy\":\"CloseAndFullWrite\",\"forceKill\":false,\"items\":[{\"cid\":\"" + cid + "\",\"newTitle\":\"New81\"}]}");
                    AssertEqual(409, renTimeout.StatusCode, "T81: /api/rename/execute CloseAndFullWrite returns 409 IDE_CLOSE_TIMEOUT");
                    Dictionary<string, object> renTimeoutDict = UnifiedJson.AsDict(UnifiedJson.Parse(renTimeout.BodyText));
                    AssertEqual("IDE_CLOSE_TIMEOUT", UnifiedJson.GetString(renTimeoutDict, "code", string.Empty), "T81: /api/rename/execute code=IDE_CLOSE_TIMEOUT");
                    AssertEqual(true, UnifiedJson.GetBool(renTimeoutDict, "canForceKill", false), "T81: /api/rename/execute canForceKill=true");

                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // T82: Phase 1 — ReleaseJob broadcasts SSE progress busy:false frame
        // ══════════════════════════════════════════════════════════════════════
        private static void T82_Phase1_ReleaseJobBroadcastsProgressEndFrame()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t82_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string vaultDir = Path.Combine(sb, "Vault");
                Directory.CreateDirectory(geminiDir);
                Directory.CreateDirectory(vaultDir);

                string token = "t82_tok_sse_end";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, vaultDir, 0, token))
                {
                    host.IdeRunningOverride = delegate() { return false; };
                    host.Start();

                    using (TcpClient sseClient = new TcpClient("127.0.0.1", host.Port))
                    {
                        sseClient.ReceiveTimeout = 5000;
                        NetworkStream sseStream = sseClient.GetStream();
                        byte[] reqBytes = Encoding.ASCII.GetBytes(
                            "GET /api/events HTTP/1.1\r\nHost: 127.0.0.1:" + host.Port + "\r\nX-AGY-Token: " + token + "\r\nConnection: keep-alive\r\n\r\n");
                        sseStream.Write(reqBytes, 0, reqBytes.Length);
                        sseStream.Flush();

                        string hello = ReadSseUntilEvent(sseStream, "connected", 3000);
                        AssertTrue(hello.Contains("event: connected"), "T82: SSE connected received");

                        // Trigger synchronous endpoint that acquires & releases job without calling BroadcastJobDone
                        RawHttpTestResponse gcResp = SendRawTcpHttp(host.Port, null, "POST", "/api/gc/clean", apiHdr, "{}");
                        AssertEqual(200, gcResp.StatusCode, "T82: /api/gc/clean returned 200");

                        StringBuilder collected = new StringBuilder();
                        DateTime deadline = DateTime.UtcNow.AddMilliseconds(4000);
                        byte[] buf = new byte[4096];
                        bool sawBusyFalse = false;
                        while (DateTime.UtcNow < deadline)
                        {
                            if (sseStream.DataAvailable)
                            {
                                int n = sseStream.Read(buf, 0, buf.Length);
                                if (n > 0)
                                {
                                    collected.Append(Encoding.UTF8.GetString(buf, 0, n));
                                    string cur = collected.ToString();
                                    if (cur.IndexOf("\"busy\":false", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                        cur.IndexOf("\"percent\":100", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        sawBusyFalse = true;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                System.Threading.Thread.Sleep(25);
                            }
                        }
                        AssertTrue(sawBusyFalse, "T82: ReleaseJob() broadcast SSE progress frame with busy:false and percent:100. Got: " + collected.ToString());
                    }

                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // T83: Phase 1 — Per-Vault sync baseline isolation
        // ══════════════════════════════════════════════════════════════════════
        private static void T83_Phase1_PerVaultSyncBaselineIsolation()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t83_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string vaultA = Path.Combine(sb, "VaultA");
                string vaultB = Path.Combine(sb, "VaultB");
                Directory.CreateDirectory(geminiDir);
                Directory.CreateDirectory(Path.Combine(vaultA, "rules"));
                Directory.CreateDirectory(Path.Combine(vaultB, "rules"));

                string baseStore = Path.Combine(geminiDir, ".companion_sync_baseline.json");
                string scopedA = CapabilitySyncBaselineStore.GetVaultScopedStorePath(baseStore, vaultA);
                string scopedB = CapabilitySyncBaselineStore.GetVaultScopedStorePath(baseStore, vaultB);
                AssertTrue(!string.Equals(scopedA, scopedB, StringComparison.OrdinalIgnoreCase), "T83: Scoped baseline paths for VaultA and VaultB differ");
                AssertTrue(scopedA.EndsWith(".json", StringComparison.OrdinalIgnoreCase), "T83: Scoped baseline path ends with .json");

                File.WriteAllText(Path.Combine(geminiDir, "GEMINI.md"), "# Local Version A\n", Encoding.UTF8);
                string repoRuleA = CapabilityVaultService.ResolveRepoRulePath(vaultA, "GEMINI.md");
                string repoRuleB = CapabilityVaultService.ResolveRepoRulePath(vaultB, "GEMINI.md");
                Directory.CreateDirectory(Path.GetDirectoryName(repoRuleA));
                Directory.CreateDirectory(Path.GetDirectoryName(repoRuleB));
                File.WriteAllText(repoRuleA, "# Local Version A\n", Encoding.UTF8);
                File.WriteAllText(repoRuleB, "# Vault B Different Version\n", Encoding.UTF8);

                string token = "t83_tok_vault_iso";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, vaultA, 0, token))
                {
                    host.Start();
                    string hostScopedA = host.BaselinePath;

                    // Push to VaultA -> creates VaultA-scoped baseline file
                    RawHttpTestResponse pushA = SendRawTcpHttp(host.Port, null, "POST", "/api/capabilities/push", apiHdr,
                        "{\"keys\":[\"Rule:GEMINI.md\"]}");
                    AssertEqual(200, pushA.StatusCode, "T83: Push GEMINI.md to VaultA returns 200");
                    AssertTrue(File.Exists(hostScopedA), "T83: VaultA-scoped baseline file created: " + hostScopedA);

                    // Switch to VaultB -> MUST NOT inherit VaultA's baseline (should be NeedsReview/Conflict, not false RemoteNewer!)
                    RawHttpTestResponse swB = SendRawTcpHttp(host.Port, null, "POST", "/api/vault/select", apiHdr,
                        "{\"vaultPath\":\"" + UnifiedJson.EscapeString(vaultB) + "\"}");
                    AssertEqual(200, swB.StatusCode, "T83: Switch to VaultB returns 200");
                    string hostScopedB = host.BaselinePath;
                    AssertTrue(!string.Equals(hostScopedA, hostScopedB, StringComparison.OrdinalIgnoreCase), "T83: Host effective baseline path changed on Vault switch");
                    AssertTrue(!File.Exists(hostScopedB), "T83: VaultB-scoped baseline file NOT yet created");

                    RawHttpTestResponse capsB = SendRawTcpHttp(host.Port, null, "GET", "/api/capabilities", apiHdr, null);
                    AssertEqual(200, capsB.StatusCode, "T83: GET /api/capabilities on VaultB returns 200");
                    Dictionary<string, object> capsBDict = UnifiedJson.AsDict(UnifiedJson.Parse(capsB.BodyText));
                    List<object> itemsB = UnifiedJson.AsList(capsBDict["items"]);
                    Dictionary<string, object> gemItem = null;
                    for (int i = 0; i < itemsB.Count; i++)
                    {
                        Dictionary<string, object> row = UnifiedJson.AsDict(itemsB[i]);
                        if (string.Equals(UnifiedJson.GetString(row, "key", string.Empty), "Rule:GEMINI.md", StringComparison.OrdinalIgnoreCase))
                        {
                            gemItem = row;
                            break;
                        }
                    }
                    AssertTrue(gemItem != null, "T83: Found Rule:GEMINI.md in VaultB diff");
                    string reasonB = UnifiedJson.GetString(gemItem, "directionReason", string.Empty);
                    AssertEqual("no_baseline_review", reasonB, "T83: VaultB has isolated baseline so directionReason is no_baseline_review (not VaultA baseline)");

                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // T84: Phase 1 — Cold Archive (.agy) readable & restorable E2E + snapshots
        // ══════════════════════════════════════════════════════════════════════
        private static void T84_Phase1_ColdArchiveReadableAndRestorableEndToEnd()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t84_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agyDir = Path.Combine(geminiDir, "antigravity");
                string convDir = Path.Combine(agyDir, "conversations");
                string cliDir = Path.Combine(geminiDir, "antigravity-cli");
                string vaultDir = Path.Combine(sb, "Vault");
                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(cliDir);
                Directory.CreateDirectory(vaultDir);

                string parentCid = "22222222-2222-2222-2222-222222222284";
                string subCid = "33333333-3333-3333-3333-333333333384";
                string subDbPath = Path.Combine(convDir, subCid + ".db");
                CreateSampleLiposuctionDb(subDbPath, subCid, 4, 2048, 3);
                File.SetLastWriteTimeUtc(subDbPath, DateTime.UtcNow.AddDays(-15));
                byte[] origSubBytes = File.ReadAllBytes(subDbPath);

                // Seed master .pb index with parent + subagent (field23=0, field17.11=parentCid)
                ProtoNode nPar84 = BuildRealProtoEntry(parentCid, "Parent 84", null, "file:///c%3A/p84", "P84", "pid84", 5, 2, 1);
                ProtoNode nSub84 = BuildRealProtoEntry(subCid, "Old Subagent 84", null, "file:///c%3A/p84", "P84", "pid84", 3, 1, 0);
                nSub84.GetChild(2).GetChild(17).Children.Add(new ProtoNode(11, 2) { RawBytes = Encoding.UTF8.GetBytes(parentCid), IsModified = true });
                ulong oldEpoch84 = (ulong)(DateTime.UtcNow.AddDays(-15) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                nSub84.GetChild(2).GetChild(3).GetChild(1).VarintValue = oldEpoch84;
                File.WriteAllBytes(Path.Combine(agyDir, "agyhub_summaries_proto.pb"),
                    ProtoCodec.Encode(new List<ProtoNode> { nPar84, nSub84 }));

                // Create summary DB with parent + subagent
                string sumDbPath = Path.Combine(agyDir, "conversation_summaries.db");
                IntPtr db = OpenAndCreateSummaryWalDb(sumDbPath);
                try
                {
                    InsertSummaryDbRow(db, parentCid, "Parent 84", "preview", 5, "[\"file:///c%3A/p84\"]", "IDLE", "pid84", string.Empty, string.Empty, 0, 0, ProtoCodec.Encode(nPar84.Children));
                    InsertSummaryDbRow(db, subCid, "Old Subagent 84", "sub preview", 3, "[\"file:///c%3A/p84\"]", "IDLE", "pid84", "worker", parentCid, 1, 0, ProtoCodec.Encode(nSub84.Children));
                    t_sqlite3_exec(db, Utf8Z("PRAGMA wal_checkpoint(TRUNCATE);"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                }
                finally { t_sqlite3_close(db); }

                // Create legacy CLI summary DB
                string legacyCliDb = Path.Combine(cliDir, "conversation_summaries.db");
                CreateMinimalValidSqliteDb(legacyCliDb, "legacy_cli_marker_84");
                byte[] origLegacyBytes = File.ReadAllBytes(legacyCliDb);

                // Execute Mode 5A cold archive prune + legacy CLI archive prune
                StorageSlimRequest slimReq = new StorageSlimRequest();
                slimReq.Mode = StorageSlimMode.ColdArchivePrune;
                slimReq.TargetCids.Add(subCid);
                slimReq.MinAgeDays = 1.0;
                slimReq.MinSizeMb = 0.0;
                slimReq.PruneLegacyCliArchive = true;
                StorageSlimResult slimRes = StorageSlimService.ExecuteStorageSlim(homeDir, geminiDir, slimReq);
                AssertTrue(slimRes.Ok, "T84: Mode5A + LegacyCLI cold archive succeeded: " + slimRes.ErrorMessage);
                AssertTrue(File.Exists(slimRes.ColdArchivePath), "T84: Subagent cold archive .agy exists");
                AssertTrue(File.Exists(slimRes.LegacyCliArchivePath), "T84: Legacy CLI cold archive .agy exists");
                AssertTrue(!File.Exists(subDbPath), "T84: Subagent .db pruned after cold archiving");
                AssertTrue(!File.Exists(legacyCliDb), "T84: Legacy CLI DB pruned after cold archiving");

                string token = "t84_tok_cold_restore";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, vaultDir, 0, token))
                {
                    host.IdeRunningOverride = delegate() { return false; };
                    host.Start();

                    // Check /api/storage/cold-archives & /api/snapshots/status list the cold archives
                    RawHttpTestResponse listCold = SendRawTcpHttp(host.Port, null, "GET", "/api/storage/cold-archives", apiHdr, null);
                    AssertEqual(200, listCold.StatusCode, "T84: GET /api/storage/cold-archives returns 200");
                    Dictionary<string, object> listColdDict = UnifiedJson.AsDict(UnifiedJson.Parse(listCold.BodyText));
                    AssertTrue(UnifiedJson.GetInt(listColdDict, "count", 0) >= 2, "T84: ListColdArchives found >= 2 archives");

                    RawHttpTestResponse snapStat = SendRawTcpHttp(host.Port, null, "GET", "/api/snapshots/status", apiHdr, null);
                    AssertEqual(200, snapStat.StatusCode, "T84: GET /api/snapshots/status returns 200");
                    Dictionary<string, object> snapDict = UnifiedJson.AsDict(UnifiedJson.Parse(snapStat.BodyText));
                    AssertTrue(UnifiedJson.GetInt(snapDict, "coldArchivesCount", 0) >= 2, "T84: /api/snapshots/status exposes coldArchivesCount");

                    // Open subagent cold archive via /api/backup/open (using archivePath)
                    RawHttpTestResponse openSub = SendRawTcpHttp(host.Port, null, "POST", "/api/backup/open", apiHdr,
                        "{\"archivePath\":\"" + UnifiedJson.EscapeString(slimRes.ColdArchivePath) + "\"}");
                    AssertEqual(200, openSub.StatusCode, "T84: Open subagent cold archive returns 200");
                    Dictionary<string, object> openSubDict = UnifiedJson.AsDict(UnifiedJson.Parse(openSub.BodyText));
                    AssertEqual(true, UnifiedJson.GetBool(openSubDict, "isColdArchive", false), "T84: Subagent archive has isColdArchive=true");
                    AssertEqual("companion-cold-archive", UnifiedJson.GetString(openSubDict, "coldArchiveType", string.Empty), "T84: coldArchiveType matches");
                    AssertEqual(1, UnifiedJson.GetInt(openSubDict, "conversationsCount", 0), "T84: Cold archive lists 1 subagent conversation");

                    // Restore subagent .db via /api/backup/import + wait SSE job_done
                    using (TcpClient sseClient = new TcpClient("127.0.0.1", host.Port))
                    {
                        sseClient.ReceiveTimeout = 5000;
                        NetworkStream sseStream = sseClient.GetStream();
                        byte[] reqBytes = Encoding.ASCII.GetBytes(
                            "GET /api/events HTTP/1.1\r\nHost: 127.0.0.1:" + host.Port + "\r\nX-AGY-Token: " + token + "\r\nConnection: keep-alive\r\n\r\n");
                        sseStream.Write(reqBytes, 0, reqBytes.Length);
                        sseStream.Flush();
                        ReadSseUntilEvent(sseStream, "connected", 3000);

                        RawHttpTestResponse impSub = SendRawTcpHttp(host.Port, null, "POST", "/api/backup/import", apiHdr,
                            "{\"cids\":[\"" + subCid + "\"],\"capKeys\":[]}");
                        AssertEqual(202, impSub.StatusCode, "T84: Cold archive subagent restore accepted (202)");
                        string doneSub = ReadSseUntilEvent(sseStream, "job_done", 5000);
                        AssertTrue(doneSub.Contains("\"ok\":true"), "T84: Subagent cold archive restore job_done ok:true");
                        AssertTrue(File.Exists(subDbPath), "T84: Subagent .db file restored on disk");
                        AssertEqual(origSubBytes.Length, new FileInfo(subDbPath).Length, "T84: Restored subagent .db size matches");

                        // Open legacy CLI cold archive via /api/backup/open (using path alias)
                        RawHttpTestResponse openLeg = SendRawTcpHttp(host.Port, null, "POST", "/api/backup/open", apiHdr,
                            "{\"path\":\"" + UnifiedJson.EscapeString(slimRes.LegacyCliArchivePath) + "\"}");
                        AssertEqual(200, openLeg.StatusCode, "T84: Open legacy CLI cold archive returns 200");
                        Dictionary<string, object> openLegDict = UnifiedJson.AsDict(UnifiedJson.Parse(openLeg.BodyText));
                        AssertEqual(true, UnifiedJson.GetBool(openLegDict, "isColdArchive", false), "T84: Legacy CLI archive isColdArchive=true");
                        AssertEqual(true, UnifiedJson.GetBool(openLegDict, "hasLegacyCliSummaryDb", false), "T84: hasLegacyCliSummaryDb=true");

                        RawHttpTestResponse impLeg = SendRawTcpHttp(host.Port, null, "POST", "/api/backup/import", apiHdr,
                            "{\"cids\":[],\"capKeys\":[],\"restoreLegacyCliArchive\":true}");
                        AssertEqual(202, impLeg.StatusCode, "T84: Legacy CLI cold archive restore accepted (202)");
                        string doneLeg = ReadSseUntilEvent(sseStream, "job_done", 5000);
                        AssertTrue(doneLeg.Contains("\"ok\":true"), "T84: Legacy CLI restore job_done ok:true");
                        AssertTrue(File.Exists(legacyCliDb), "T84: Legacy CLI conversation_summaries.db restored on disk");
                        AssertEqual(origLegacyBytes.Length, new FileInfo(legacyCliDb).Length, "T84: Restored legacy CLI DB matches original size");
                    }

                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // T85: Phase 2 — Unified Soft-Delete Trash & Restore E2E + Explorer Allowlist
        // ══════════════════════════════════════════════════════════════════════
        private static void T85_Phase2_UnifiedSoftDeleteTrashAndRestoreEndToEnd()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t85_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agyDir = Path.Combine(geminiDir, "antigravity");
                string convDir = Path.Combine(agyDir, "conversations");
                string brainDir = Path.Combine(agyDir, "brain");
                string annotDir = Path.Combine(agyDir, "annotations");
                string projDir = Path.Combine(geminiDir, "config", "projects");
                string vaultDir = Path.Combine(sb, "Vault");
                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(brainDir);
                Directory.CreateDirectory(annotDir);
                Directory.CreateDirectory(projDir);
                Directory.CreateDirectory(vaultDir);

                string cid = "44444444-4444-4444-4444-444444444485";
                string pid = "proj-uuid-85";
                string wsFolder = Path.Combine(sb, "MyProj85");
                Directory.CreateDirectory(wsFolder);
                string wsUri = SessionLifecycleService.ToFileUri(wsFolder);

                File.WriteAllText(Path.Combine(projDir, pid + ".json"),
                    "{\"name\":\"MyProj85\",\"folderUri\":\"" + UnifiedJson.EscapeString(wsUri) + "\"}", Encoding.UTF8);
                ProtoNode entry = BuildRealProtoEntry(cid, "Trash Test Session 85", string.Empty, wsUri, "MyProj85", pid, 6, 3, null);
                File.WriteAllBytes(Path.Combine(agyDir, "agyhub_summaries_proto.pb"), ProtoCodec.Encode(new List<ProtoNode> { entry }));

                string dbFile = Path.Combine(convDir, cid + ".db");
                CreateMinimalValidSqliteDb(dbFile, "session85");
                string cBrainDir = Path.Combine(brainDir, cid);
                Directory.CreateDirectory(cBrainDir);
                string notePath = Path.Combine(cBrainDir, "note.md");
                File.WriteAllText(notePath, "Brain artifact 85", Encoding.UTF8);
                string pbtxtPath = Path.Combine(annotDir, cid + ".pbtxt");
                File.WriteAllText(pbtxtPath, "title: \"Trash Test Session 85\"\n", Encoding.UTF8);

                string sumDbPath = Path.Combine(agyDir, "conversation_summaries.db");
                IntPtr db = OpenAndCreateSummaryWalDb(sumDbPath);
                try
                {
                    InsertSummaryDbRow(db, cid, "Trash Test Session 85", "preview85", 6, "[\"" + wsUri + "\"]", "IDLE", pid, string.Empty, string.Empty, 0, 0, new byte[] { 9, 8, 7 });
                    t_sqlite3_exec(db, Utf8Z("PRAGMA wal_checkpoint(TRUNCATE);"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                }
                finally { t_sqlite3_close(db); }

                string gemMd = Path.Combine(geminiDir, "GEMINI.md");
                File.WriteAllText(gemMd, "# Global Rule 85\n", Encoding.UTF8);

                string token = "t85_tok_trash";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, vaultDir, 0, token))
                {
                    bool ideRunning85 = false;
                    host.IdeRunningOverride = delegate() { return ideRunning85; };
                    host.Start();

                    // 1. Soft-delete session -> moved to Trash
                    RawHttpTestResponse delResp = SendRawTcpHttp(host.Port, null, "POST", "/api/sessions/delete", apiHdr,
                        "{\"cids\":[\"" + cid + "\"]}");
                    AssertEqual(200, delResp.StatusCode, "T85: Soft-delete session returns 200");
                    Dictionary<string, object> delDict = UnifiedJson.AsDict(UnifiedJson.Parse(delResp.BodyText));
                    string trashId = UnifiedJson.GetString(delDict, "trashId", string.Empty);
                    AssertTrue(!string.IsNullOrEmpty(trashId), "T85: Soft-delete returns non-empty trashId");
                    AssertTrue(!File.Exists(dbFile), "T85: Session .db removed from active dir");
                    AssertTrue(!Directory.Exists(cBrainDir), "T85: Brain dir removed from active dir");
                    AssertTrue(!File.Exists(pbtxtPath), "T85: .pbtxt removed from active dir");

                    // 2. /api/trash/list shows the deleted session
                    RawHttpTestResponse listResp = SendRawTcpHttp(host.Port, null, "GET", "/api/trash/list", apiHdr, null);
                    AssertEqual(200, listResp.StatusCode, "T85: GET /api/trash/list returns 200");
                    Dictionary<string, object> listDict = UnifiedJson.AsDict(UnifiedJson.Parse(listResp.BodyText));
                    AssertEqual(1, UnifiedJson.GetInt(listDict, "totalCount", 0), "T85: Trash contains 1 item");

                    // 3. Restoring session while IDE running without closeIdeIfRunning -> 409 IDE_RUNNING
                    ideRunning85 = true;
                    RawHttpTestResponse resBlocked = SendRawTcpHttp(host.Port, null, "POST", "/api/trash/restore", apiHdr,
                        "{\"id\":\"" + UnifiedJson.EscapeString(trashId) + "\"}");
                    AssertEqual(409, resBlocked.StatusCode, "T85: Restoring session from trash blocks with 409 when IDE running");

                    // 4. Restore when IDE stopped -> ALL files, .pb entry & SQLite row back!
                    ideRunning85 = false;
                    RawHttpTestResponse resOk = SendRawTcpHttp(host.Port, null, "POST", "/api/trash/restore", apiHdr,
                        "{\"id\":\"" + UnifiedJson.EscapeString(trashId) + "\"}");
                    AssertEqual(200, resOk.StatusCode, "T85: Restore session from trash returns 200");
                    AssertTrue(File.Exists(dbFile), "T85: Session .db restored");
                    AssertTrue(File.Exists(notePath), "T85: Brain artifact note.md restored");
                    AssertTrue(File.Exists(pbtxtPath), "T85: .pbtxt annotation restored");
                    HashSet<string> liveIds = SummarySqliteBridge.ReadAllConversationIds(sumDbPath);
                    AssertTrue(liveIds.Contains(cid), "T85: SQLite conversation_summaries row restored");
                    RawHttpTestResponse sessAfter = SendRawTcpHttp(host.Port, null, "GET", "/api/sessions", apiHdr, null);
                    AssertTrue(sessAfter.BodyText.Contains("Trash Test Session 85"), "T85: Restored session visible in /api/sessions");

                    // 5. Soft-delete local capability -> restore -> back on disk
                    RawHttpTestResponse delCap = SendRawTcpHttp(host.Port, null, "POST", "/api/capabilities/delete-local", apiHdr,
                        "{\"keys\":[\"Rule:GEMINI.md\"]}");
                    AssertEqual(200, delCap.StatusCode, "T85: Soft-delete local capability returns 200");
                    string capTrashId = UnifiedJson.GetString(UnifiedJson.AsDict(UnifiedJson.Parse(delCap.BodyText)), "trashId", string.Empty);
                    AssertTrue(!string.IsNullOrEmpty(capTrashId), "T85: Local capability delete returns trashId");
                    AssertTrue(!File.Exists(gemMd), "T85: GEMINI.md removed from active dir");

                    RawHttpTestResponse resCap = SendRawTcpHttp(host.Port, null, "POST", "/api/trash/restore", apiHdr,
                        "{\"id\":\"" + UnifiedJson.EscapeString(capTrashId) + "\"}");
                    AssertEqual(200, resCap.StatusCode, "T85: Restore local capability from trash returns 200");
                    AssertTrue(File.Exists(gemMd) && File.ReadAllText(gemMd, Encoding.UTF8).Contains("Global Rule 85"), "T85: GEMINI.md content restored");

                    // 6. Security: path traversal in trashId rejected with 400
                    RawHttpTestResponse trav = SendRawTcpHttp(host.Port, null, "POST", "/api/trash/restore", apiHdr,
                        "{\"id\":\"../outside\"}");
                    AssertEqual(400, trav.StatusCode, "T85: Path traversal trashId rejected with 400");

                    // 7. Explorer allowlist includes companion-trash & companion-snapshots (404 not 403 for non-existent probe path inside root)
                    string probeTrashPath = Path.Combine(TrashAndBackupService.GetTrashRoot(geminiDir), "nonexistent_probe_85.txt");
                    RawHttpTestResponse expTrash = SendRawTcpHttp(host.Port, null, "POST", "/api/native/open-in-explorer", apiHdr,
                        "{\"path\":\"" + UnifiedJson.EscapeString(probeTrashPath) + "\"}");
                    AssertEqual(404, expTrash.StatusCode, "T85: companion-trash passes Explorer allowlist (404 missing file, not 403 forbidden)");

                    string probeSnapPath = Path.Combine(geminiDir, "companion-snapshots", "nonexistent_snap_85.agy");
                    RawHttpTestResponse expSnap = SendRawTcpHttp(host.Port, null, "POST", "/api/native/open-in-explorer", apiHdr,
                        "{\"path\":\"" + UnifiedJson.EscapeString(probeSnapPath) + "\"}");
                    AssertEqual(404, expSnap.StatusCode, "T85: companion-snapshots passes Explorer allowlist (404 missing file, not 403 forbidden)");

                    // 8. Purge all
                    RawHttpTestResponse purgeAll = SendRawTcpHttp(host.Port, null, "POST", "/api/trash/purge", apiHdr, "{\"all\":true}");
                    AssertEqual(200, purgeAll.StatusCode, "T85: Empty trash returns 200");

                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // T86: Phase 2 — .bak.* Config Version History & 1-Click Restore
        // ══════════════════════════════════════════════════════════════════════
        private static void T86_Phase2_ConfigBackupVersionHistoryAndRestore()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t86_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string vaultDir = Path.Combine(sb, "Vault");
                Directory.CreateDirectory(geminiDir);
                Directory.CreateDirectory(vaultDir);

                string gemMd = Path.Combine(geminiDir, "GEMINI.md");
                File.WriteAllText(gemMd, "# Version One Content\n", Encoding.UTF8);

                string outsideBak = Path.Combine(sb, "secret.json.bak.20261125_000000");
                File.WriteAllText(outsideBak, "{\"secret\":true}", Encoding.UTF8);

                string token = "t86_tok_bak";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, vaultDir, 0, token))
                {
                    host.Start();

                    // Save v2 via editor -> auto-generates .bak.* containing v1
                    RawHttpTestResponse saveV2 = SendRawTcpHttp(host.Port, null, "POST", "/api/editor/save", apiHdr,
                        "{\"path\":\"" + UnifiedJson.EscapeString(gemMd) + "\",\"content\":\"# Version Two Content\\n\"}");
                    AssertEqual(200, saveV2.StatusCode, "T86: Save v2 returns 200");
                    AssertTrue(File.ReadAllText(gemMd, Encoding.UTF8).Contains("Version Two"), "T86: GEMINI.md now contains Version Two");

                    // List backups for GEMINI.md
                    RawHttpTestResponse listBak = SendRawTcpHttp(host.Port, null, "GET",
                        "/api/config-backups/list?path=" + Uri.EscapeDataString(gemMd), apiHdr, null);
                    AssertEqual(200, listBak.StatusCode, "T86: GET /api/config-backups/list returns 200");
                    Dictionary<string, object> listDict = UnifiedJson.AsDict(UnifiedJson.Parse(listBak.BodyText));
                    List<object> items = UnifiedJson.AsList(listDict["items"]);
                    AssertTrue(items != null && items.Count >= 1, "T86: At least 1 .bak.* backup listed for GEMINI.md");

                    Dictionary<string, object> bakItem = UnifiedJson.AsDict(items[0]);
                    string bakPath = UnifiedJson.GetString(bakItem, "backupPath", string.Empty);
                    AssertEqual("GEMINI.md", UnifiedJson.GetString(bakItem, "originalFileName", string.Empty), "T86: originalFileName is GEMINI.md");
                    AssertTrue(UnifiedJson.GetString(bakItem, "preview", string.Empty).Contains("Version One"), "T86: backup preview shows Version One");

                    // Restore v1 from .bak.*
                    RawHttpTestResponse resBak = SendRawTcpHttp(host.Port, null, "POST", "/api/config-backups/restore", apiHdr,
                        "{\"backupPath\":\"" + UnifiedJson.EscapeString(bakPath) + "\"}");
                    AssertEqual(200, resBak.StatusCode, "T86: POST /api/config-backups/restore returns 200");
                    Dictionary<string, object> resDict = UnifiedJson.AsDict(UnifiedJson.Parse(resBak.BodyText));
                    string safetyBak = UnifiedJson.GetString(resDict, "safetyBackupPath", string.Empty);
                    AssertTrue(File.ReadAllText(gemMd, Encoding.UTF8).Contains("Version One"), "T86: GEMINI.md restored to Version One");
                    AssertTrue(!string.IsNullOrEmpty(safetyBak) && File.Exists(safetyBak), "T86: Safety backup created before restore");
                    AssertTrue(File.ReadAllText(safetyBak, Encoding.UTF8).Contains("Version Two"), "T86: Safety backup preserves Version Two");

                    // Security: outside-allowlist .bak.* rejected with 403
                    RawHttpTestResponse badBak = SendRawTcpHttp(host.Port, null, "POST", "/api/config-backups/restore", apiHdr,
                        "{\"backupPath\":\"" + UnifiedJson.EscapeString(outsideBak) + "\"}");
                    AssertEqual(403, badBak.StatusCode, "T86: Outside-allowlist config backup restore rejected with 403");

                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // T87: Phase 2 — Online Hot Backup + SmartAuto Flush + Live SSE Events
        // ══════════════════════════════════════════════════════════════════════
        private static void T87_Phase2_OnlineHotBackupSmartAutoFlushAndLiveSse()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t87_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agyDir = Path.Combine(geminiDir, "antigravity");
                string convDir = Path.Combine(agyDir, "conversations");
                string annotDir = Path.Combine(agyDir, "annotations");
                string cfgDir = Path.Combine(geminiDir, "config");
                string vaultDir = Path.Combine(sb, "Vault");
                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(annotDir);
                Directory.CreateDirectory(cfgDir);
                Directory.CreateDirectory(vaultDir);

                string cid = "55555555-5555-5555-5555-555555555587";
                string pbPath = Path.Combine(agyDir, "agyhub_summaries_proto.pb");
                ProtoNode entry = BuildRealProtoEntry(cid, "Original Title 87", string.Empty, "file:///c:/proj87", "Proj87", "pid87", 5, 2, null);
                File.WriteAllBytes(pbPath, ProtoCodec.Encode(new List<ProtoNode> { entry }));
                CreateMinimalValidSqliteDb(Path.Combine(convDir, cid + ".db"), "t87");
                File.WriteAllText(Path.Combine(annotDir, cid + ".pbtxt"), "title: \"Original Title 87\"\n", Encoding.UTF8);

                string sumDbPath = Path.Combine(agyDir, "conversation_summaries.db");
                IntPtr db = OpenAndCreateSummaryWalDb(sumDbPath);
                try
                {
                    InsertSummaryDbRow(db, cid, "Original Title 87", "p87", 5, "[\"file:///c:/proj87\"]", "IDLE", "pid87", string.Empty, string.Empty, 0, 0, new byte[] { 1 });
                    t_sqlite3_exec(db, Utf8Z("PRAGMA wal_checkpoint(TRUNCATE);"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                }
                finally { t_sqlite3_close(db); }

                string token = "t87_tok_hot";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, vaultDir, 0, token))
                {
                    bool ideRunning87 = true;
                    host.IdeRunningOverride = delegate() { return ideRunning87; };
                    host.Start();

                    using (TcpClient sseClient = new TcpClient("127.0.0.1", host.Port))
                    {
                        sseClient.ReceiveTimeout = 6000;
                        NetworkStream sseStream = sseClient.GetStream();
                        byte[] reqBytes = Encoding.ASCII.GetBytes(
                            "GET /api/events HTTP/1.1\r\nHost: 127.0.0.1:" + host.Port + "\r\nX-AGY-Token: " + token + "\r\nConnection: keep-alive\r\n\r\n");
                        sseStream.Write(reqBytes, 0, reqBytes.Length);
                        sseStream.Flush();
                        ReadSseUntilEvent(sseStream, "connected", 3000);

                        // 1. Online hot backup succeeds with 202 while IDE is running!
                        string hotFullAgy = Path.Combine(sb, "hot_full_87.agy");
                        RawHttpTestResponse hotFull = SendRawTcpHttp(host.Port, null, "POST", "/api/backup/full", apiHdr,
                            "{\"outputPath\":\"" + UnifiedJson.EscapeString(hotFullAgy) + "\",\"tier\":\"fast\",\"onlineHotBackup\":true}");
                        AssertEqual(202, hotFull.StatusCode, "T87: /api/backup/full with onlineHotBackup=true succeeds (202) while IDE running");
                        string doneHot1 = ReadSseUntilEvent(sseStream, "job_done", 6000);
                        AssertTrue(doneHot1.Contains("\"ok\":true"), "T87: Online hot full backup completed ok:true");
                        AssertTrue(File.Exists(hotFullAgy), "T87: Online hot full backup .agy created");

                        string hotSelAgy = Path.Combine(sb, "hot_sel_87.agy");
                        RawHttpTestResponse hotSel = SendRawTcpHttp(host.Port, null, "POST", "/api/backup/selective-export", apiHdr,
                            "{\"cids\":[\"" + cid + "\"],\"capKeys\":[],\"outputPath\":\"" + UnifiedJson.EscapeString(hotSelAgy) + "\",\"onlineHotBackup\":true}");
                        AssertEqual(202, hotSel.StatusCode, "T87: /api/backup/selective-export with onlineHotBackup=true succeeds (202) while IDE running");
                        string doneHot2 = ReadSseUntilEvent(sseStream, "job_done", 6000);
                        AssertTrue(doneHot2.Contains("\"ok\":true"), "T87: Online hot selective export completed ok:true");
                        AssertTrue(File.Exists(hotSelAgy), "T87: Online hot selective .agy created");

                        // 2. SmartAuto rename while IDE running -> hot writes L1/L2, queues L3 .pb flush!
                        RawHttpTestResponse smartRen = SendRawTcpHttp(host.Port, null, "POST", "/api/sessions/rename-single", apiHdr,
                            "{\"cid\":\"" + cid + "\",\"newTitle\":\"SmartAuto Flushed Title 87\",\"strategy\":\"SmartAuto\"}");
                        AssertEqual(200, smartRen.StatusCode, "T87: SmartAuto rename succeeds (200) while IDE running without prompt");
                        Dictionary<string, object> renDict = UnifiedJson.AsDict(UnifiedJson.Parse(smartRen.BodyText));
                        AssertEqual(true, UnifiedJson.GetBool(renDict, "deferredToPbFlush", false), "T87: deferredToPbFlush=true");
                        AssertEqual(1, UnifiedJson.GetInt(renDict, "pendingPbSyncCount", 0), "T87: pendingPbSyncCount=1");
                        AssertTrue(File.ReadAllText(Path.Combine(annotDir, cid + ".pbtxt"), Encoding.UTF8).Contains("SmartAuto Flushed Title 87"),
                            "T87: Layer 1 .pbtxt immediately updated");

                        // Verify .pb STILL has old title before IDE closes
                        List<ConversationSummaryInfo> beforeFlush = SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(pbPath));
                        AssertTrue(beforeFlush.Count == 1 && beforeFlush[0].DisplayTitle == "Original Title 87",
                            "T87: Layer 3 .pb untouched while IDE still running");

                        // 3. Simulate IDE exit -> CheckIdeStatusAndAutoFlushNow broadcasts ide_status & auto-flushes Layer 3 .pb!
                        ideRunning87 = false;
                        host.CheckIdeStatusAndAutoFlushNow();

                        string ideEv = ReadSseUntilEvent(sseStream, "ide_status", 4000);
                        AssertTrue(ideEv.Contains("\"running\":false"), "T87: Live SSE ide_status running:false received");
                        AssertEqual(0, TrashAndBackupService.GetPendingPbSyncCount(geminiDir), "T87: Pending .pb flush queue drained to 0 on IDE exit");

                        List<ConversationSummaryInfo> afterFlush = SessionMetadataExtractor.ExtractSummariesFromProto(File.ReadAllBytes(pbPath));
                        AssertTrue(afterFlush.Count == 1 && afterFlush[0].DisplayTitle == "SmartAuto Flushed Title 87",
                            "T87: Layer 3 .pb automatically updated on IDE exit!");

                        // 4. FileSystemWatcher debounced fs_changed SSE broadcast
                        System.Threading.Thread.Sleep(900); // Allow self-write suppression window to expire
                        File.WriteAllText(Path.Combine(cfgDir, "external_change_87.json"), "{\"ok\":true}", Encoding.UTF8);
                        string fsEv = ReadSseUntilEvent(sseStream, "fs_changed", 4000);
                        AssertTrue(fsEv.Contains("event: fs_changed"), "T87: FileSystemWatcher pushed debounced fs_changed SSE event");
                    }

                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // T88: Phase 2 — Tray Debounce + Unified Orphan Engine + Embedded UI Parity
        // ══════════════════════════════════════════════════════════════════════
        private static void T88_Phase2_TrayDebounceUnifiedOrphanEngineAndUiParity()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t88_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agyDir = Path.Combine(geminiDir, "antigravity");
                string convDir = Path.Combine(agyDir, "conversations");
                string brainDir = Path.Combine(agyDir, "brain");
                string annotDir = Path.Combine(agyDir, "annotations");
                string vaultDir = Path.Combine(sb, "Vault");
                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(brainDir);
                Directory.CreateDirectory(annotDir);
                Directory.CreateDirectory(vaultDir);

                // Seed 1 live conversation so orphan scanner master index exists (safety guard requires live .pb index)
                string liveCid = "77777777-7777-7777-7777-777777777788";
                ProtoNode liveEntry = BuildRealProtoEntry(liveCid, "Live Session 88", string.Empty, "file:///c:/p88", "Proj88", "pid88", 3, 1, null);
                File.WriteAllBytes(Path.Combine(agyDir, "agyhub_summaries_proto.pb"), ProtoCodec.Encode(new List<ProtoNode> { liveEntry }));
                CreateMinimalValidSqliteDb(Path.Combine(convDir, liveCid + ".db"), "live88");

                // Seed 1 orphan across ALL 5 file/folder types including .db.bak and .db-journal
                string orphCid = "66666666-6666-6666-6666-666666666688";
                File.WriteAllText(Path.Combine(convDir, orphCid + ".db"), "orphan db", Encoding.UTF8);
                File.WriteAllText(Path.Combine(convDir, orphCid + ".db.bak"), "orphan bak", Encoding.UTF8);
                File.WriteAllText(Path.Combine(convDir, orphCid + ".db-journal"), "orphan journal", Encoding.UTF8);
                Directory.CreateDirectory(Path.Combine(brainDir, orphCid));
                File.WriteAllText(Path.Combine(brainDir, orphCid, "a.txt"), "brain", Encoding.UTF8);
                File.WriteAllText(Path.Combine(annotDir, orphCid + ".pbtxt"), "title: \"orph\"\n", Encoding.UTF8);

                string token = "t88_tok_parity";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, vaultDir, 0, token))
                {
                    host.Start();

                    // 1. TrayHost double-click debounce check
                    using (TrayHost tray = new TrayHost(host, "http://127.0.0.1:" + host.Port + "/?t=" + token, false, false))
                    {
                        AssertEqual(true, tray.TryAcquireOpenCockpitDebounce(1000, 800), "T88: First tray click allowed");
                        AssertEqual(false, tray.TryAcquireOpenCockpitDebounce(1200, 800), "T88: Second click within 800ms suppressed");
                        AssertEqual(false, tray.TryAcquireOpenCockpitDebounce(1799, 800), "T88: Click at +799ms still suppressed");
                        AssertEqual(true, tray.TryAcquireOpenCockpitDebounce(1800, 800), "T88: Click at +800ms allowed");
                    }

                    // 2. Unified orphan engine parity: /api/gc/scan == /api/storage/analyze orphans (both count .db.bak and .db-journal)
                    RawHttpTestResponse gcScan = SendRawTcpHttp(host.Port, null, "POST", "/api/gc/scan", apiHdr, "{}");
                    AssertEqual(200, gcScan.StatusCode, "T88: /api/gc/scan returns 200");
                    Dictionary<string, object> gcDict = UnifiedJson.AsDict(UnifiedJson.Parse(gcScan.BodyText));

                    RawHttpTestResponse stoScan = SendRawTcpHttp(host.Port, null, "POST", "/api/storage/analyze", apiHdr, "{\"topN\":10}");
                    AssertEqual(200, stoScan.StatusCode, "T88: /api/storage/analyze returns 200");
                    Dictionary<string, object> stoDict = UnifiedJson.AsDict(UnifiedJson.Parse(stoScan.BodyText));
                    Dictionary<string, object> stoOrph = UnifiedJson.AsDict(stoDict["orphans"]);

                    int gcTotal = UnifiedJson.GetInt(gcDict, "totalItems", -1);
                    int stoTotal = UnifiedJson.GetInt(stoOrph, "totalItems", -2);
                    AssertEqual(5, gcTotal, "T88: /api/gc/scan detects all 5 orphan artifacts (.db, .db.bak, .db-journal, brain, .pbtxt)");
                    AssertEqual(stoTotal, gcTotal, "T88: /api/gc/scan totalItems matches /api/storage/analyze orphans.totalItems");
                    AssertEqual(1, UnifiedJson.GetInt(gcDict, "orphanDbBakCount", 0), "T88: /api/gc/scan exposes orphanDbBakCount=1");
                    AssertEqual(1, UnifiedJson.GetInt(gcDict, "orphanDbJournalCount", 0), "T88: /api/gc/scan exposes orphanDbJournalCount=1");

                    // 3. Embedded UI asset & Phase 1/2 structural parity checks
                    RawHttpTestResponse idxResp = SendRawTcpHttp(host.Port, null, "GET", "/?t=" + token, null, null);
                    AssertEqual(200, idxResp.StatusCode, "T88: GET / returns 200");
                    AssertTrue(idxResp.BodyText.Contains("工作台") && idxResp.BodyText.Contains("对话管理") &&
                        idxResp.BodyText.Contains("扩展与配置") && idxResp.BodyText.Contains("空间清理") &&
                        idxResp.BodyText.Contains("备份与同步") && idxResp.BodyText.Contains("回收站") &&
                        idxResp.BodyText.Contains("应用设置"), "T88: index.html renders 5+1 navigation IA");
                    AssertTrue(idxResp.BodyText.Contains("disconnectBanner") && idxResp.BodyText.Contains("staleDataBanner"),
                        "T88: index.html includes disconnectBanner and staleDataBanner");
                    AssertTrue(idxResp.BodyText.Contains("/js/views/trash.js"), "T88: index.html loads /js/views/trash.js");

                    RawHttpTestResponse trashJs = SendRawTcpHttp(host.Port, null, "GET", "/js/views/trash.js", apiHdr, null);
                    AssertEqual(200, trashJs.StatusCode, "T88: Embedded /js/views/trash.js served with 200");
                    AssertTrue(trashJs.BodyText.Contains("/api/trash/restore") && trashJs.BodyText.Contains("/api/config-backups/restore"),
                        "T88: trash.js wires both trash and config-backup restore endpoints");

                    RawHttpTestResponse compJs = SendRawTcpHttp(host.Port, null, "GET", "/js/components.js", apiHdr, null);
                    AssertEqual(200, compJs.StatusCode, "T88: Embedded /js/components.js served");
                    AssertTrue(compJs.BodyText.Contains("IDE_CLOSE_TIMEOUT") && compJs.BodyText.Contains("handleIdeConflictError"),
                        "T88: components.js handles IDE_CLOSE_TIMEOUT escape hatch");
                    // Only closeDrawer should set drawerDirtyCheck = null (1 occurrence), NOT post-save in openInlineEditorDrawer
                    int compResetCount = CountOccurrences(compJs.BodyText, "AGY.state.drawerDirtyCheck = null");
                    AssertEqual(1, compResetCount, "T88: components.js resets drawerDirtyCheck only on closeDrawer, not after save");

                    RawHttpTestResponse renJs = SendRawTcpHttp(host.Port, null, "GET", "/js/views/rename.js", apiHdr, null);
                    AssertEqual(200, renJs.StatusCode, "T88: Embedded /js/views/rename.js served");
                    AssertEqual(0, CountOccurrences(renJs.BodyText, "AGY.state.drawerDirtyCheck = null"),
                        "T88: rename.js never destroys drawerDirtyCheck on save");

                    RawHttpTestResponse apiJs = SendRawTcpHttp(host.Port, null, "GET", "/js/api.js", apiHdr, null);
                    AssertTrue(apiJs.BodyText.Contains("withButtonBusy"), "T88: api.js exports withButtonBusy");

                    RawHttpTestResponse appJs = SendRawTcpHttp(host.Port, null, "GET", "/app.js", apiHdr, null);
                    AssertTrue(appJs.BodyText.Contains("Promise.allSettled") && appJs.BodyText.Contains("hasUnsavedWork"),
                        "T88: app.js uses Promise.allSettled and hasUnsavedWork dirty guard");

                    RawHttpTestResponse bkJs = SendRawTcpHttp(host.Port, null, "GET", "/js/views/backup.js", apiHdr, null);
                    AssertTrue(bkJs.BodyText.Contains("onlineHotBackup: true") && bkJs.BodyText.Contains("existingSnapshots"),
                        "T88: backup.js uses onlineHotBackup and renders existingSnapshots");

                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // T89: Review Hardening — Cross-Vault Trash Preservation, Project+Subagent
        //      Restore, Corrupt .bak Rejection, Exact deferredToPbFlush,
        //      Subdirectory FileSystemWatcher & Strict WebShell Window Matching
        // ══════════════════════════════════════════════════════════════════════
        private static void T89_Phase2_ReviewHardening_TrashVaultSafetySubdirWatchAndShellMatch()
        {
            string sb = Path.Combine(Path.GetTempPath(), "agy_t89_" + Guid.NewGuid().ToString("N"));
            try
            {
                string homeDir = Path.Combine(sb, "Home");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string agyDir = Path.Combine(geminiDir, "antigravity");
                string convDir = Path.Combine(agyDir, "conversations");
                string annotDir = Path.Combine(agyDir, "annotations");
                string cfgDir = Path.Combine(geminiDir, "config");
                string projDir = Path.Combine(cfgDir, "projects");
                string rulesDir = Path.Combine(cfgDir, "rules");
                string vaultA = Path.Combine(sb, "VaultA");
                string vaultB = Path.Combine(sb, "VaultB");
                string wsFolder = Path.Combine(sb, "Proj89");

                Directory.CreateDirectory(convDir);
                Directory.CreateDirectory(annotDir);
                Directory.CreateDirectory(projDir);
                Directory.CreateDirectory(rulesDir);
                Directory.CreateDirectory(Path.Combine(vaultA, "rules"));
                Directory.CreateDirectory(Path.Combine(vaultB, "rules"));
                Directory.CreateDirectory(wsFolder);

                // 1. WebShellLauncher strict window matching checks
                AssertTrue(WebShellLauncher.IsMatchingWebShellWindow("msedge", "Chrome_WidgetWin_1", "Antigravity Companion V1.0"),
                    "T89: Matches Edge --app WebShell window");
                AssertTrue(WebShellLauncher.IsMatchingWebShellWindow("chrome", "Chrome_WidgetWin_1", "Antigravity 伴侣"),
                    "T89: Matches Chrome --app WebShell window");
                AssertTrue(!WebShellLauncher.IsMatchingWebShellWindow("msedge", "Chrome_WidgetWin_1", "Antigravity Companion V1.0 - Personal - Microsoft Edge"),
                    "T89: Rejects regular Edge tabbed browser window");
                AssertTrue(!WebShellLauncher.IsMatchingWebShellWindow("chrome", "Chrome_WidgetWin_1", "Antigravity Companion - Google Chrome"),
                    "T89: Rejects regular Chrome tabbed browser window");
                AssertTrue(!WebShellLauncher.IsMatchingWebShellWindow("Code", "Chrome_WidgetWin_1", "Antigravity Companion - Visual Studio Code"),
                    "T89: Rejects VS Code window");
                AssertTrue(!WebShellLauncher.IsMatchingWebShellWindow("notepad", "Notepad", "Antigravity Companion.txt - Notepad"),
                    "T89: Rejects Notepad window");

                // Seed VaultA rule for cross-vault trash restore safety test
                string vaultARule = CapabilityVaultService.ResolveRepoRulePath(vaultA, "rules/rule89.md");
                Directory.CreateDirectory(Path.GetDirectoryName(vaultARule));
                File.WriteAllText(vaultARule, "# VaultA Rule 89\n", Encoding.UTF8);

                // Seed config.json + corrupt .bak.*
                string cfgJson = Path.Combine(cfgDir, "config.json");
                File.WriteAllText(cfgJson, "{\"userSettings\":{\"themeMode\":\"THEME_MODE_LIGHT\"}}", Encoding.UTF8);
                string badBak = Path.Combine(cfgDir, "config.json.bak.20261125_010101");
                File.WriteAllText(badBak, "{ BROKEN JSON !!!", Encoding.UTF8);

                // Seed formal project with parent + subagent + one SQLite-only session (not in .pb)
                string pid89 = "proj-uuid-89";
                string parentCid = "89000000-0000-0000-0000-000000000001";
                string subCid = "89000000-0000-0000-0000-000000000002";
                string sqliteOnlyCid = "89000000-0000-0000-0000-000000000003";
                string wsUri = SessionLifecycleService.ToFileUri(wsFolder);

                string projJsonPath = Path.Combine(projDir, pid89 + ".json");
                File.WriteAllText(projJsonPath,
                    "{\"id\":\"" + pid89 + "\",\"name\":\"Proj89\",\"projectResources\":{\"resources\":[{\"gitFolder\":{\"folderUri\":\"" + UnifiedJson.EscapeString(wsUri) + "\"}}]}}",
                    Encoding.UTF8);

                ProtoNode nPar = BuildRealProtoEntry(parentCid, "Parent 89", null, wsUri, "Proj89", pid89, 5, 2, 1);
                ProtoNode nSub = BuildRealProtoEntry(subCid, "Subagent 89", null, wsUri, "Proj89", pid89, 2, 1, 0);
                nSub.GetChild(2).GetChild(17).Children.Add(new ProtoNode(11, 2) { RawBytes = Encoding.UTF8.GetBytes(parentCid), IsModified = true });
                File.WriteAllBytes(Path.Combine(agyDir, "agyhub_summaries_proto.pb"), ProtoCodec.Encode(new List<ProtoNode> { nPar, nSub }));

                CreateMinimalValidSqliteDb(Path.Combine(convDir, parentCid + ".db"), "p89");
                CreateMinimalValidSqliteDb(Path.Combine(convDir, subCid + ".db"), "s89");
                CreateMinimalValidSqliteDb(Path.Combine(convDir, sqliteOnlyCid + ".db"), "sq89");
                File.WriteAllText(Path.Combine(annotDir, sqliteOnlyCid + ".pbtxt"), "title: \"SqliteOnly89\"\n", Encoding.UTF8);

                string sumDbPath = Path.Combine(agyDir, "conversation_summaries.db");
                IntPtr db = OpenAndCreateSummaryWalDb(sumDbPath);
                try
                {
                    InsertSummaryDbRow(db, parentCid, "Parent 89", "prev", 5, "[\"" + wsUri + "\"]", "IDLE", pid89, string.Empty, string.Empty, 0, 0, ProtoCodec.Encode(nPar.Children));
                    InsertSummaryDbRow(db, subCid, "Subagent 89", "subprev", 2, "[\"" + wsUri + "\"]", "IDLE", pid89, "worker", parentCid, 1, 0, ProtoCodec.Encode(nSub.Children));
                    InsertSummaryDbRow(db, sqliteOnlyCid, "SqliteOnly89", "sqprev", 3, "[\"" + wsUri + "\"]", "IDLE", pid89, string.Empty, string.Empty, 0, 0, new byte[] { 1 });
                    t_sqlite3_exec(db, Utf8Z("PRAGMA wal_checkpoint(TRUNCATE);"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                }
                finally { t_sqlite3_close(db); }

                string token = "t89_tok_hardening";
                Dictionary<string, string> apiHdr = new Dictionary<string, string> { { "X-AGY-Token", token } };
                using (LocalWebHost host = new LocalWebHost(homeDir, geminiDir, vaultA, 0, token))
                {
                    bool ideRunning89 = false;
                    host.IdeRunningOverride = delegate() { return ideRunning89; };
                    host.Start();

                    // 2. Corrupt JSON .bak.* rejected with 400 and preserves config.json
                    RawHttpTestResponse corruptResp = SendRawTcpHttp(host.Port, null, "POST", "/api/config-backups/restore", apiHdr,
                        "{\"backupPath\":\"" + UnifiedJson.EscapeString(badBak) + "\"}");
                    AssertEqual(400, corruptResp.StatusCode, "T89: Corrupt JSON .bak.* rejected with 400");
                    AssertTrue(File.ReadAllText(cfgJson, Encoding.UTF8).Contains("THEME_MODE_LIGHT"), "T89: Valid config.json preserved after corrupt .bak rejection");

                    // 3. Cross-Vault Trash Restore Protection: delete from VaultA -> switch to VaultB -> restore MUST FAIL AND KEEP TRASH!
                    RawHttpTestResponse delVaultCap = SendRawTcpHttp(host.Port, null, "POST", "/api/capabilities/delete-repo", apiHdr,
                        "{\"keys\":[\"Rule:rules/rule89.md\"]}");
                    AssertEqual(200, delVaultCap.StatusCode, "T89: Soft-delete VaultA rule returns 200");
                    string capTrashId = UnifiedJson.GetString(UnifiedJson.AsDict(UnifiedJson.Parse(delVaultCap.BodyText)), "trashId", string.Empty);
                    AssertTrue(!string.IsNullOrEmpty(capTrashId) && !File.Exists(vaultARule), "T89: VaultA rule moved to trash");

                    RawHttpTestResponse swB = SendRawTcpHttp(host.Port, null, "POST", "/api/vault/select", apiHdr,
                        "{\"vaultPath\":\"" + UnifiedJson.EscapeString(vaultB) + "\"}");
                    AssertEqual(200, swB.StatusCode, "T89: Switch to VaultB returns 200");

                    RawHttpTestResponse blockedRestore = SendRawTcpHttp(host.Port, null, "POST", "/api/trash/restore", apiHdr,
                        "{\"id\":\"" + UnifiedJson.EscapeString(capTrashId) + "\"}");
                    AssertEqual(400, blockedRestore.StatusCode, "T89: Cross-vault trash restore blocked with 400");
                    string trashItemDir = Path.Combine(TrashAndBackupService.GetTrashRoot(geminiDir), capTrashId);
                    AssertTrue(Directory.Exists(trashItemDir), "T89: Blocked trash restore PRESERVES trash directory (zero data loss!)");

                    // Switch back to VaultA -> restore succeeds!
                    SendRawTcpHttp(host.Port, null, "POST", "/api/vault/select", apiHdr,
                        "{\"vaultPath\":\"" + UnifiedJson.EscapeString(vaultA) + "\"}");
                    RawHttpTestResponse okRestore = SendRawTcpHttp(host.Port, null, "POST", "/api/trash/restore", apiHdr,
                        "{\"id\":\"" + UnifiedJson.EscapeString(capTrashId) + "\"}");
                    AssertEqual(200, okRestore.StatusCode, "T89: Switching back to VaultA restores rule89.md with 200");
                    AssertTrue(File.Exists(vaultARule) && File.ReadAllText(vaultARule, Encoding.UTF8).Contains("VaultA Rule 89"),
                        "T89: VaultA rule89.md content intact after restore");

                    // 4. Rename non-.pb session when IDE is closed -> MUST NOT falsely set deferredToPbFlush=true!
                    RawHttpTestResponse renNoPb = SendRawTcpHttp(host.Port, null, "POST", "/api/sessions/rename-single", apiHdr,
                        "{\"cid\":\"" + sqliteOnlyCid + "\",\"newTitle\":\"SqliteOnly89 Renamed\",\"strategy\":\"SmartAuto\"}");
                    AssertEqual(200, renNoPb.StatusCode, "T89: Rename SQLite-only session returns 200");
                    Dictionary<string, object> renNoPbDict = UnifiedJson.AsDict(UnifiedJson.Parse(renNoPb.BodyText));
                    AssertEqual(false, UnifiedJson.GetBool(renNoPbDict, "deferredToPbFlush", true), "T89: Non-.pb session rename when IDE closed has deferredToPbFlush=false");
                    AssertEqual(0, UnifiedJson.GetInt(renNoPbDict, "pendingPbSyncCount", -1), "T89: pendingPbSyncCount stays 0 when IDE closed");

                    // 5. SmartAuto rename while IDE running enqueues pending; deleting that session clears pending!
                    ideRunning89 = true;
                    RawHttpTestResponse renDef = SendRawTcpHttp(host.Port, null, "POST", "/api/sessions/rename-single", apiHdr,
                        "{\"cid\":\"" + parentCid + "\",\"newTitle\":\"Parent 89 Pending\",\"strategy\":\"SmartAuto\"}");
                    AssertEqual(200, renDef.StatusCode, "T89: SmartAuto rename while IDE running succeeds");
                    AssertEqual(1, TrashAndBackupService.GetPendingPbSyncCount(geminiDir), "T89: Pending .pb queue has 1 entry");

                    // Delete entire formal project (parent + subagent) when IDE stopped -> clears pending AND restores cleanly!
                    ideRunning89 = false;
                    RawHttpTestResponse delProj = SendRawTcpHttp(host.Port, null, "POST", "/api/sessions/delete", apiHdr,
                        "{\"projectIds\":[\"" + pid89 + "\"]}");
                    AssertEqual(200, delProj.StatusCode, "T89: Soft-delete formal project returns 200");
                    string projTrashId = UnifiedJson.GetString(UnifiedJson.AsDict(UnifiedJson.Parse(delProj.BodyText)), "trashId", string.Empty);
                    AssertEqual(0, TrashAndBackupService.GetPendingPbSyncCount(geminiDir), "T89: Deleting session clears its stale PendingPbSync entry");
                    AssertTrue(!File.Exists(projJsonPath) && !File.Exists(Path.Combine(convDir, subCid + ".db")), "T89: Project JSON and subagent DB removed");

                    RawHttpTestResponse resProj = SendRawTcpHttp(host.Port, null, "POST", "/api/trash/restore", apiHdr,
                        "{\"id\":\"" + UnifiedJson.EscapeString(projTrashId) + "\"}");
                    AssertEqual(200, resProj.StatusCode, "T89: Restoring formal project from trash returns 200");
                    AssertTrue(File.Exists(projJsonPath), "T89: projects/<pid>.json restored");
                    AssertTrue(File.Exists(Path.Combine(convDir, parentCid + ".db")) && File.Exists(Path.Combine(convDir, subCid + ".db")),
                        "T89: Both parent and subagent .db restored");
                    Dictionary<string, SummaryDbRowInfo> snapAfter = SummarySqliteBridge.ReadSummaryDbSnapshot(sumDbPath);
                    AssertTrue(snapAfter.ContainsKey(subCid) && string.Equals(snapAfter[subCid].ParentConversationId, parentCid, StringComparison.OrdinalIgnoreCase),
                        "T89: Restored subagent preserves ParentConversationId link in SQLite");

                    // 6. FileSystemWatcher watches config/rules/ AND Vault/rules/ subdirectories!
                    using (TcpClient sseClient = new TcpClient("127.0.0.1", host.Port))
                    {
                        sseClient.ReceiveTimeout = 6000;
                        NetworkStream sseStream = sseClient.GetStream();
                        byte[] reqBytes = Encoding.ASCII.GetBytes(
                            "GET /api/events HTTP/1.1\r\nHost: 127.0.0.1:" + host.Port + "\r\nX-AGY-Token: " + token + "\r\nConnection: keep-alive\r\n\r\n");
                        sseStream.Write(reqBytes, 0, reqBytes.Length);
                        sseStream.Flush();
                        ReadSseUntilEvent(sseStream, "connected", 3000);

                        System.Threading.Thread.Sleep(950); // Allow self-write suppression window to expire
                        File.WriteAllText(Path.Combine(rulesDir, "subdir_rule_89.md"), "# Subdir Rule Change\n", Encoding.UTF8);
                        string fsEv1 = ReadSseUntilEvent(sseStream, "fs_changed", 4500);
                        AssertTrue(fsEv1.Contains("event: fs_changed"), "T89: FileSystemWatcher detects subdirectory edits under config/rules/");

                        System.Threading.Thread.Sleep(950);
                        File.WriteAllText(Path.Combine(vaultA, "rules", "subdir_vault_89.md"), "# Vault Subdir Change\n", Encoding.UTF8);
                        string fsEv2 = ReadSseUntilEvent(sseStream, "fs_changed", 4500);
                        AssertTrue(fsEv2.Contains("event: fs_changed"), "T89: FileSystemWatcher detects subdirectory edits under Vault/rules/");
                    }

                    host.Stop();
                }
            }
            finally
            {
                if (Directory.Exists(sb)) Directory.Delete(sb, true);
            }
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) return 0;
            int count = 0;
            int idx = 0;
            while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += needle.Length;
            }
            return count;
        }
    }
}
