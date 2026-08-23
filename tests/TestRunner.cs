using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace AntigravityCompanion.Tests
{
    public class TestContext
    {
        public string CurrentTestName { get; set; }
        public int AssertionsPassed { get; private set; }
        public int AssertionsFailed { get; private set; }
        public List<string> FailureMessages { get; private set; }

        public TestContext(string testName)
        {
            CurrentTestName = testName;
            AssertionsPassed = 0;
            AssertionsFailed = 0;
            FailureMessages = new List<string>();
        }

        public void AssertTrue(bool condition, string message = null)
        {
            if (condition)
            {
                AssertionsPassed++;
            }
            else
            {
                AssertionsFailed++;
                string err = string.Format("[ASSERT FAIL] Expected TRUE but got FALSE. {0}", message ?? "");
                FailureMessages.Add(err);
                throw new TestAssertionException(err);
            }
        }

        public void AssertFalse(bool condition, string message = null)
        {
            if (!condition)
            {
                AssertionsPassed++;
            }
            else
            {
                AssertionsFailed++;
                string err = string.Format("[ASSERT FAIL] Expected FALSE but got TRUE. {0}", message ?? "");
                FailureMessages.Add(err);
                throw new TestAssertionException(err);
            }
        }

        public void AssertEqual<T>(T expected, T actual, string message = null)
        {
            if (EqualityComparer<T>.Default.Equals(expected, actual))
            {
                AssertionsPassed++;
            }
            else
            {
                AssertionsFailed++;
                string err = string.Format("[ASSERT FAIL] Expected: '{0}', Actual: '{1}'. {2}", expected, actual, message ?? "");
                FailureMessages.Add(err);
                throw new TestAssertionException(err);
            }
        }

        public void AssertNull(object obj, string message = null)
        {
            if (obj == null)
            {
                AssertionsPassed++;
            }
            else
            {
                AssertionsFailed++;
                string err = string.Format("[ASSERT FAIL] Expected NULL but was: '{0}'. {1}", obj, message ?? "");
                FailureMessages.Add(err);
                throw new TestAssertionException(err);
            }
        }

        public void AssertNotNull(object obj, string message = null)
        {
            if (obj != null)
            {
                AssertionsPassed++;
            }
            else
            {
                AssertionsFailed++;
                string err = string.Format("[ASSERT FAIL] Expected NOT NULL. {0}", message ?? "");
                FailureMessages.Add(err);
                throw new TestAssertionException(err);
            }
        }

        public void Fail(string message)
        {
            AssertionsFailed++;
            string err = string.Format("[FAIL] {0}", message ?? "Test explicitly failed.");
            FailureMessages.Add(err);
            throw new TestAssertionException(err);
        }
    }

    public class TestAssertionException : Exception
    {
        public TestAssertionException(string message) : base(message) { }
    }

    public class TestCaseDescriptor
    {
        public string Tier;
        public string Feature;
        public string Name;
        public Action<TestContext> Action;

        public TestCaseDescriptor(string tier, string feature, string name, Action<TestContext> action)
        {
            Tier = tier;
            Feature = feature;
            Name = name;
            Action = action;
        }
    }

    public class TestExecutionResult
    {
        public TestCaseDescriptor Descriptor;
        public bool Passed;
        public TimeSpan Duration;
        public int AssertionsCount;
        public string ErrorMessage;
        public string StackTrace;
    }

    public static class TestRunner
    {
        private static List<TestCaseDescriptor> _registry = new List<TestCaseDescriptor>();

        public static void Register(string tier, string feature, string name, Action<TestContext> action)
        {
            _registry.Add(new TestCaseDescriptor(tier, feature, name, action));
        }

        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("===============================================================================");
            Console.WriteLine("       Antigravity Companion V0.5 - Automated E2E Test Suite Runner            ");
            Console.WriteLine("===============================================================================");

            RegisterAllTests();

            // Parse Command Line Flags
            string tierFilter = null;
            string featureFilter = null;
            string nameFilter = null;
            bool verbose = false;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a.StartsWith("--tier=", StringComparison.OrdinalIgnoreCase))
                    tierFilter = a.Substring("--tier=".Length);
                else if (a.StartsWith("--feature=", StringComparison.OrdinalIgnoreCase))
                    featureFilter = a.Substring("--feature=".Length);
                else if (a.StartsWith("--filter=", StringComparison.OrdinalIgnoreCase))
                    nameFilter = a.Substring("--filter=".Length);
                else if (a.Equals("--verbose", StringComparison.OrdinalIgnoreCase) || a.Equals("-v", StringComparison.OrdinalIgnoreCase))
                    verbose = true;
                else if (a.Equals("--help", StringComparison.OrdinalIgnoreCase) || a.Equals("-h", StringComparison.OrdinalIgnoreCase))
                {
                    PrintHelp();
                    return 0;
                }
            }

            // Filter Tests
            List<TestCaseDescriptor> selectedTests = new List<TestCaseDescriptor>();
            for (int i = 0; i < _registry.Count; i++)
            {
                TestCaseDescriptor d = _registry[i];
                if (!string.IsNullOrEmpty(tierFilter))
                {
                    bool matchTier = d.Tier.Equals(tierFilter, StringComparison.OrdinalIgnoreCase) ||
                                     d.Tier.Equals("Tier " + tierFilter, StringComparison.OrdinalIgnoreCase) ||
                                     d.Tier.Equals("Tier" + tierFilter, StringComparison.OrdinalIgnoreCase) ||
                                     d.Tier.EndsWith(tierFilter, StringComparison.OrdinalIgnoreCase);
                    if (!matchTier) continue;
                }
                if (!string.IsNullOrEmpty(featureFilter) && !d.Feature.Equals(featureFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.IsNullOrEmpty(nameFilter) && d.Name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                selectedTests.Add(d);
            }

            Console.WriteLine(string.Format("[INFO] Discovered {0} test cases in registry. Executing {1} selected tests...", _registry.Count, selectedTests.Count));
            Console.WriteLine("-------------------------------------------------------------------------------");

            List<TestExecutionResult> results = new List<TestExecutionResult>();
            int totalAssertions = 0;
            int passedCount = 0;
            int failedCount = 0;
            Stopwatch overallStopwatch = Stopwatch.StartNew();

            string currentTier = "";

            for (int i = 0; i < selectedTests.Count; i++)
            {
                TestCaseDescriptor test = selectedTests[i];
                if (test.Tier != currentTier)
                {
                    currentTier = test.Tier;
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine(string.Format(">>> [{0}] Executing Verification Suite...", currentTier));
                    Console.ResetColor();
                }

                TestContext ctx = new TestContext(test.Name);
                TestExecutionResult res = new TestExecutionResult { Descriptor = test };
                Stopwatch sw = Stopwatch.StartNew();

                try
                {
                    test.Action(ctx);
                    sw.Stop();
                    res.Passed = true;
                    res.Duration = sw.Elapsed;
                    res.AssertionsCount = ctx.AssertionsPassed;
                    totalAssertions += ctx.AssertionsPassed;
                    passedCount++;

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("  [PASS] ");
                    Console.ResetColor();
                    Console.WriteLine(string.Format("{0,-65} ({1:F1}ms, {2} asserts)", test.Name, sw.Elapsed.TotalMilliseconds, ctx.AssertionsPassed));
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    res.Passed = false;
                    res.Duration = sw.Elapsed;
                    res.AssertionsCount = ctx.AssertionsPassed;
                    totalAssertions += ctx.AssertionsPassed;
                    failedCount++;

                    Exception actual = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                    res.ErrorMessage = actual.Message;
                    res.StackTrace = actual.StackTrace;

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("  [FAIL] ");
                    Console.ResetColor();
                    Console.WriteLine(string.Format("{0,-65} ({1:F1}ms)", test.Name, sw.Elapsed.TotalMilliseconds));
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine(string.Format("         Error: {0}", actual.Message));
                    if (verbose && !string.IsNullOrEmpty(actual.StackTrace))
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine("         " + actual.StackTrace.Replace("\n", "\n         "));
                    }
                    Console.ResetColor();
                }

                results.Add(res);
            }

            overallStopwatch.Stop();

            // Print Summary Table
            Console.WriteLine();
            Console.WriteLine("===============================================================================");
            Console.WriteLine("                           TEST EXECUTION SUMMARY                              ");
            Console.WriteLine("===============================================================================");

            // Tier breakdown summary
            Dictionary<string, int> tierTotal = new Dictionary<string, int>();
            Dictionary<string, int> tierPassed = new Dictionary<string, int>();

            for (int i = 0; i < results.Count; i++)
            {
                string t = results[i].Descriptor.Tier;
                if (!tierTotal.ContainsKey(t))
                {
                    tierTotal[t] = 0;
                    tierPassed[t] = 0;
                }
                tierTotal[t]++;
                if (results[i].Passed) tierPassed[t]++;
            }

            foreach (KeyValuePair<string, int> kvp in tierTotal)
            {
                string t = kvp.Key;
                int tot = kvp.Value;
                int pass = tierPassed[t];
                double pct = tot > 0 ? (pass * 100.0 / tot) : 100.0;
                string colorTag = pass == tot ? "[PASS]" : "[FAIL]";
                Console.WriteLine(string.Format("  {0,-12} : {1,3}/{2,3} Passed ({3,5:F1}%)  {4}", t, pass, tot, pct, colorTag));
            }

            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine(string.Format("  Total Tests       : {0}", selectedTests.Count));
            Console.WriteLine(string.Format("  Passed            : {0}", passedCount));
            Console.WriteLine(string.Format("  Failed            : {0}", failedCount));
            Console.WriteLine(string.Format("  Total Assertions  : {0}", totalAssertions));
            Console.WriteLine(string.Format("  Total Elapsed     : {0:F2}s", overallStopwatch.Elapsed.TotalSeconds));
            Console.WriteLine("===============================================================================");

            if (failedCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(string.Format(">>> RESULT: {0} TEST(S) FAILED!", failedCount));
                Console.ResetColor();
                return 1;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(">>> RESULT: ALL TESTS PASSED! (100% SUCCESS)");
                Console.ResetColor();
                return 0;
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Usage: TestRunner.exe [options]");
            Console.WriteLine("Options:");
            Console.WriteLine("  --tier=<1|2|3|4>     Run tests for specific Tier only");
            Console.WriteLine("  --feature=<F1..F13>  Run tests for specific Feature only");
            Console.WriteLine("  --filter=<substring> Filter test names containing substring");
            Console.WriteLine("  --verbose, -v        Enable verbose stack traces for failed tests");
            Console.WriteLine("  --help, -h           Show this help text");
        }

        private static void RegisterAllTests()
        {
            // ── Tier 1: Feature Coverage (65 tests) ──
            Register("Tier 1", "F1", "F1_T1_01_ExtractsCustomTitleFromField15", Tier1_FeatureCoverageTests.Test_F1_01_ExtractsCustomTitleFromField15);
            Register("Tier 1", "F1", "F1_T1_02_LosslessChineseUtf8Integrity", Tier1_FeatureCoverageTests.Test_F1_02_LosslessChineseUtf8Integrity);
            Register("Tier 1", "F1", "F1_T1_03_FallbackToAutoSummaryWhenField15Missing", Tier1_FeatureCoverageTests.Test_F1_03_FallbackToAutoSummaryWhenField15Missing);
            Register("Tier 1", "F1", "F1_T1_04_ExtractTimestampsAndStepCount", Tier1_FeatureCoverageTests.Test_F1_04_ExtractTimestampsAndStepCount);
            Register("Tier 1", "F1", "F1_T1_05_ExtractFolderUriAndProjectName", Tier1_FeatureCoverageTests.Test_F1_05_ExtractFolderUriAndProjectName);

            Register("Tier 1", "F2", "F2_T1_01_VarintEncodingDecoding", Tier1_FeatureCoverageTests.Test_F2_01_VarintEncodingDecoding);
            Register("Tier 1", "F2", "F2_T1_02_Fixed64EncodingDecoding", Tier1_FeatureCoverageTests.Test_F2_02_Fixed64EncodingDecoding);
            Register("Tier 1", "F2", "F2_T1_03_LengthDelimitedEncodingDecoding", Tier1_FeatureCoverageTests.Test_F2_03_LengthDelimitedEncodingDecoding);
            Register("Tier 1", "F2", "F2_T1_04_Fixed32EncodingDecoding", Tier1_FeatureCoverageTests.Test_F2_04_Fixed32EncodingDecoding);
            Register("Tier 1", "F2", "F2_T1_05_RoundtripAstFidelity", Tier1_FeatureCoverageTests.Test_F2_05_RoundtripAstFidelity);

            Register("Tier 1", "F3", "F3_T1_01_ExtractsSummaryProtoDirectlyFromStream", Tier1_FeatureCoverageTests.Test_F3_01_ExtractsSummaryProtoDirectlyFromStream);
            Register("Tier 1", "F3", "F3_T1_02_ExtractsProjectConfigs", Tier1_FeatureCoverageTests.Test_F3_02_ExtractsProjectConfigs);
            Register("Tier 1", "F3", "F3_T1_03_ExtractsMetadataJson", Tier1_FeatureCoverageTests.Test_F3_03_ExtractsMetadataJson);
            Register("Tier 1", "F3", "F3_T1_04_IdentifiesConversationDbAndBrainEntries", Tier1_FeatureCoverageTests.Test_F3_04_IdentifiesConversationDbAndBrainEntries);
            Register("Tier 1", "F3", "F3_T1_05_DetectsGlobalConfigs", Tier1_FeatureCoverageTests.Test_F3_05_DetectsGlobalConfigs);

            Register("Tier 1", "F4", "F4_T1_01_SlicesAndMergesSelectedEntries", Tier1_FeatureCoverageTests.Test_F4_01_SlicesAndMergesSelectedEntries);
            Register("Tier 1", "F4", "F4_T1_02_PreservesExistingLocalSessions", Tier1_FeatureCoverageTests.Test_F4_02_PreservesExistingLocalSessions);
            Register("Tier 1", "F4", "F4_T1_03_RemapsFolderUriOnProjectRename", Tier1_FeatureCoverageTests.Test_F4_03_RemapsFolderUriOnProjectRename);
            Register("Tier 1", "F4", "F4_T1_04_PreservesRawEntrySubfields", Tier1_FeatureCoverageTests.Test_F4_04_PreservesRawEntrySubfields);
            Register("Tier 1", "F4", "F4_T1_05_HandlesMultiSessionBatchMerge", Tier1_FeatureCoverageTests.Test_F4_05_HandlesMultiSessionBatchMerge);

            Register("Tier 1", "F5", "F5_T1_01_FiltersSubagentPrefix", Tier1_FeatureCoverageTests.Test_F5_01_FiltersSubagentPrefix);
            Register("Tier 1", "F5", "F5_T1_02_FiltersOutsideOfProject", Tier1_FeatureCoverageTests.Test_F5_02_FiltersOutsideOfProject);
            Register("Tier 1", "F5", "F5_T1_03_FiltersDefaultCliProject", Tier1_FeatureCoverageTests.Test_F5_03_FiltersDefaultCliProject);
            Register("Tier 1", "F5", "F5_T1_04_FiltersRecoveredProject", Tier1_FeatureCoverageTests.Test_F5_04_FiltersRecoveredProject);
            Register("Tier 1", "F5", "F5_T1_05_FiltersTempPrefix", Tier1_FeatureCoverageTests.Test_F5_05_FiltersTempPrefix);

            Register("Tier 1", "F6", "F6_T1_01_ClassifiesNewInBackup", Tier1_FeatureCoverageTests.Test_F6_01_ClassifiesNewInBackup);
            Register("Tier 1", "F6", "F6_T1_02_ClassifiesAligned", Tier1_FeatureCoverageTests.Test_F6_02_ClassifiesAligned);
            Register("Tier 1", "F6", "F6_T1_03_ClassifiesConflicted", Tier1_FeatureCoverageTests.Test_F6_03_ClassifiesConflicted);
            Register("Tier 1", "F6", "F6_T1_04_ClassifiesLocalOnly", Tier1_FeatureCoverageTests.Test_F6_04_ClassifiesLocalOnly);
            Register("Tier 1", "F6", "F6_T1_05_DiffBadgeTextIntegrity", Tier1_FeatureCoverageTests.Test_F6_05_DiffBadgeTextIntegrity);

            Register("Tier 1", "F7", "F7_T1_01_MergeStrategyPreservesLocalUri", Tier1_FeatureCoverageTests.Test_F7_01_MergeStrategyPreservesLocalUri);
            Register("Tier 1", "F7", "F7_T1_02_RenameStrategyUpdatesProjectNameAndUri", Tier1_FeatureCoverageTests.Test_F7_02_RenameStrategyUpdatesProjectNameAndUri);
            Register("Tier 1", "F7", "F7_T1_03_OverwriteStrategyReplacesLocalEntries", Tier1_FeatureCoverageTests.Test_F7_03_OverwriteStrategyReplacesLocalEntries);
            Register("Tier 1", "F7", "F7_T1_04_SkipStrategyOmitsConflictedProject", Tier1_FeatureCoverageTests.Test_F7_04_SkipStrategyOmitsConflictedProject);
            Register("Tier 1", "F7", "F7_T1_05_BatchConflictPlanApplication", Tier1_FeatureCoverageTests.Test_F7_05_BatchConflictPlanApplication);

            Register("Tier 1", "F8", "F8_T1_01_BuildsLocalHierarchy", Tier1_FeatureCoverageTests.Test_F8_01_BuildsLocalHierarchy);
            Register("Tier 1", "F8", "F8_T1_02_BuildsBackupHierarchy", Tier1_FeatureCoverageTests.Test_F8_02_BuildsBackupHierarchy);
            Register("Tier 1", "F8", "F8_T1_03_DisplaysCustomTitleAndTimestampInNodeText", Tier1_FeatureCoverageTests.Test_F8_03_DisplaysCustomTitleAndTimestampInNodeText);
            Register("Tier 1", "F8", "F8_T1_04_PopulatesGlobalConfigsNodes", Tier1_FeatureCoverageTests.Test_F8_04_PopulatesGlobalConfigsNodes);
            Register("Tier 1", "F8", "F8_T1_05_TreeNodesSupportColorAndFontMetadata", Tier1_FeatureCoverageTests.Test_F8_05_TreeNodesSupportColorAndFontMetadata);

            Register("Tier 1", "F9", "F9_T1_01_CheckingProjectChecksAllChildren", Tier1_FeatureCoverageTests.Test_F9_01_CheckingProjectChecksAllChildren);
            Register("Tier 1", "F9", "F9_T1_02_UncheckingProjectUnchecksAllChildren", Tier1_FeatureCoverageTests.Test_F9_02_UncheckingProjectUnchecksAllChildren);
            Register("Tier 1", "F9", "F9_T1_03_SelectAllChecksAllSelectableItems", Tier1_FeatureCoverageTests.Test_F9_03_SelectAllChecksAllSelectableItems);
            Register("Tier 1", "F9", "F9_T1_04_SelectNoneClearsAllSelections", Tier1_FeatureCoverageTests.Test_F9_04_SelectNoneClearsAllSelections);
            Register("Tier 1", "F9", "F9_T1_05_SelectNewOnlyFiltersToNewInBackupOnly", Tier1_FeatureCoverageTests.Test_F9_05_SelectNewOnlyFiltersToNewInBackupOnly);

            Register("Tier 1", "F10", "F10_T1_01_ExtractsOnlySelectedDbFiles", Tier1_FeatureCoverageTests.Test_F10_01_ExtractsOnlySelectedDbFiles);
            Register("Tier 1", "F10", "F10_T1_02_ExtractsOnlySelectedBrainFolders", Tier1_FeatureCoverageTests.Test_F10_02_ExtractsOnlySelectedBrainFolders);
            Register("Tier 1", "F10", "F10_T1_03_MergesProtoSummariesNonDestructively", Tier1_FeatureCoverageTests.Test_F10_03_MergesProtoSummariesNonDestructively);
            Register("Tier 1", "F10", "F10_T1_04_LeavesExistingLocalFilesIntact", Tier1_FeatureCoverageTests.Test_F10_04_LeavesExistingLocalFilesIntact);
            Register("Tier 1", "F10", "F10_T1_05_ReturnsAccurateImportResultMetrics", Tier1_FeatureCoverageTests.Test_F10_05_ReturnsAccurateImportResultMetrics);

            Register("Tier 1", "F11", "F11_T1_01_MarksImportedSessionsAsHighlighted", Tier1_FeatureCoverageTests.Test_F11_01_MarksImportedSessionsAsHighlighted);
            Register("Tier 1", "F11", "F11_T1_02_MarksImportedProjectsAsHighlighted", Tier1_FeatureCoverageTests.Test_F11_02_MarksImportedProjectsAsHighlighted);
            Register("Tier 1", "F11", "F11_T1_03_UnimportedSessionsNotHighlighted", Tier1_FeatureCoverageTests.Test_F11_03_UnimportedSessionsNotHighlighted);
            Register("Tier 1", "F11", "F11_T1_04_RescanReflectsUpdatedTotalConversationCount", Tier1_FeatureCoverageTests.Test_F11_04_RescanReflectsUpdatedTotalConversationCount);
            Register("Tier 1", "F11", "F11_T1_05_HighlightStateMaintainedAcrossQueries", Tier1_FeatureCoverageTests.Test_F11_05_HighlightStateMaintainedAcrossQueries);

            Register("Tier 1", "F12", "F12_T1_01_BuildBatExists", Tier1_FeatureCoverageTests.Test_F12_01_BuildBatExists);
            Register("Tier 1", "F12", "F12_T1_02_AppIconExists", Tier1_FeatureCoverageTests.Test_F12_02_AppIconExists);
            Register("Tier 1", "F12", "F12_T1_03_BuildBatUsesCscAndIconFlag", Tier1_FeatureCoverageTests.Test_F12_03_BuildBatUsesCscAndIconFlag);
            Register("Tier 1", "F12", "F12_T1_04_BuildBatIncludesCompressionAssemblies", Tier1_FeatureCoverageTests.Test_F12_04_BuildBatIncludesCompressionAssemblies);
            Register("Tier 1", "F12", "F12_T1_05_MainFormContainsEntryProgram", Tier1_FeatureCoverageTests.Test_F12_05_MainFormContainsEntryProgram);

            Register("Tier 1", "F13", "F13_T1_01_UserManualExists", Tier1_FeatureCoverageTests.Test_F13_01_UserManualExists);
            Register("Tier 1", "F13", "F13_T1_02_UserManualDocumentsDualTree", Tier1_FeatureCoverageTests.Test_F13_02_UserManualDocumentsDualTree);
            Register("Tier 1", "F13", "F13_T1_03_UserManualDocumentsSelectiveImport", Tier1_FeatureCoverageTests.Test_F13_03_UserManualDocumentsSelectiveImport);
            Register("Tier 1", "F13", "F13_T1_04_UserManualDocumentsConflictResolution", Tier1_FeatureCoverageTests.Test_F13_04_UserManualDocumentsConflictResolution);
            Register("Tier 1", "F13", "F13_T1_05_GitignoreRulesPresent", Tier1_FeatureCoverageTests.Test_F13_05_GitignoreRulesPresent);

            // ── Tier 2: Boundary & Corner Cases (65 tests) ──
            Register("Tier 2", "F1", "F1_T2_01_EmptyOrWhitespaceCustomTitleFallback", Tier2_BoundaryCornerTests.Test_F1_T2_01_EmptyOrWhitespaceCustomTitleFallback);
            Register("Tier 2", "F1", "F1_T2_02_ExtremeLengthUnicodeAndEmoji", Tier2_BoundaryCornerTests.Test_F1_T2_02_ExtremeLengthUnicodeAndEmoji);
            Register("Tier 2", "F1", "F1_T2_03_MalformedField15MissingSubfield", Tier2_BoundaryCornerTests.Test_F1_T2_03_MalformedField15MissingSubfield);
            Register("Tier 2", "F1", "F1_T2_04_NullAutoSummaryAndNullCustomTitle", Tier2_BoundaryCornerTests.Test_F1_T2_04_NullAutoSummaryAndNullCustomTitle);
            Register("Tier 2", "F1", "F1_T2_05_DuplicateField15Tags", Tier2_BoundaryCornerTests.Test_F1_T2_05_DuplicateField15Tags);

            Register("Tier 2", "F2", "F2_T2_01_MaxUInt64VarintBoundary", Tier2_BoundaryCornerTests.Test_F2_T2_01_MaxUInt64VarintBoundary);
            Register("Tier 2", "F2", "F2_T2_02_ZeroLengthDelimitedBuffer", Tier2_BoundaryCornerTests.Test_F2_T2_02_ZeroLengthDelimitedBuffer);
            Register("Tier 2", "F2", "F2_T2_03_TruncatedBufferHandling", Tier2_BoundaryCornerTests.Test_F2_T2_03_TruncatedBufferHandling);
            Register("Tier 2", "F2", "F2_T2_04_DeeplyNestedProtoHierarchy", Tier2_BoundaryCornerTests.Test_F2_T2_04_DeeplyNestedProtoHierarchy);
            Register("Tier 2", "F2", "F2_T2_05_UnknownWireTypeRecovery", Tier2_BoundaryCornerTests.Test_F2_T2_05_UnknownWireTypeRecovery);

            Register("Tier 2", "F3", "F3_T2_01_EmptyZipArchive", Tier2_BoundaryCornerTests.Test_F3_T2_01_EmptyZipArchive);
            Register("Tier 2", "F3", "F3_T2_02_ZipMissingProtoSummaryFile", Tier2_BoundaryCornerTests.Test_F3_T2_02_ZipMissingProtoSummaryFile);
            Register("Tier 2", "F3", "F3_T2_03_MixedPathSeparatorsInZip", Tier2_BoundaryCornerTests.Test_F3_T2_03_MixedPathSeparatorsInZip);
            Register("Tier 2", "F3", "F3_T2_04_LargeFileIndexSimulation", Tier2_BoundaryCornerTests.Test_F3_T2_04_LargeFileIndexSimulation);
            Register("Tier 2", "F3", "F3_T2_05_CorruptedZipStreamThrowsFileNotFoundOrFormat", Tier2_BoundaryCornerTests.Test_F3_T2_05_CorruptedZipStreamThrowsFileNotFoundOrFormat);

            Register("Tier 2", "F4", "F4_T2_01_MergeIntoEmptyLocalPb", Tier2_BoundaryCornerTests.Test_F4_T2_01_MergeIntoEmptyLocalPb);
            Register("Tier 2", "F4", "F4_T2_02_MergeDuplicateUuidOverwritesExisting", Tier2_BoundaryCornerTests.Test_F4_T2_02_MergeDuplicateUuidOverwritesExisting);
            Register("Tier 2", "F4", "F4_T2_03_FolderUriWithSpecialCharsAndSpaces", Tier2_BoundaryCornerTests.Test_F4_T2_03_FolderUriWithSpecialCharsAndSpaces);
            Register("Tier 2", "F4", "F4_T2_04_HighFieldNumbersPreserved", Tier2_BoundaryCornerTests.Test_F4_T2_04_HighFieldNumbersPreserved);
            Register("Tier 2", "F4", "F4_T2_05_MergeEmptyTargetListLeavesLocalUntouched", Tier2_BoundaryCornerTests.Test_F4_T2_05_MergeEmptyTargetListLeavesLocalUntouched);

            Register("Tier 2", "F5", "F5_T2_01_CaseInsensitiveJunkMatching", Tier2_BoundaryCornerTests.Test_F5_T2_01_CaseInsensitiveJunkMatching);
            Register("Tier 2", "F5", "F5_T2_02_ExactMatchVsPrefixMatching", Tier2_BoundaryCornerTests.Test_F5_T2_02_ExactMatchVsPrefixMatching);
            Register("Tier 2", "F5", "F5_T2_03_JunkNameWithWhitespacePadded", Tier2_BoundaryCornerTests.Test_F5_T2_03_JunkNameWithWhitespacePadded);
            Register("Tier 2", "F5", "F5_T2_04_AllProjectsJunkEnvironment", Tier2_BoundaryCornerTests.Test_F5_T2_04_AllProjectsJunkEnvironment);
            Register("Tier 2", "F5", "F5_T2_05_NonJunkContainingSubagentInMiddle", Tier2_BoundaryCornerTests.Test_F5_T2_05_NonJunkContainingSubagentInMiddle);

            Register("Tier 2", "F6", "F6_T2_01_SameNameDifferentDriveLetters", Tier2_BoundaryCornerTests.Test_F6_T2_01_SameNameDifferentDriveLetters);
            Register("Tier 2", "F6", "F6_T2_02_TrailingSlashVariationsNormalized", Tier2_BoundaryCornerTests.Test_F6_T2_02_TrailingSlashVariationsNormalized);
            Register("Tier 2", "F6", "F6_T2_03_EmptyLocalAndBackup", Tier2_BoundaryCornerTests.Test_F6_T2_03_EmptyLocalAndBackup);
            Register("Tier 2", "F6", "F6_T2_04_AllBackupNewAllLocalOnly", Tier2_BoundaryCornerTests.Test_F6_T2_04_AllBackupNewAllLocalOnly);
            Register("Tier 2", "F6", "F6_T2_05_MixedSessionDiffInsideSingleProject", Tier2_BoundaryCornerTests.Test_F6_T2_05_MixedSessionDiffInsideSingleProject);

            Register("Tier 2", "F7", "F7_T2_01_RenameGeneratesUniqueSuffix", Tier2_BoundaryCornerTests.Test_F7_T2_01_RenameGeneratesUniqueSuffix);
            Register("Tier 2", "F7", "F7_T2_02_IllegalCharsInRenameSanitized", Tier2_BoundaryCornerTests.Test_F7_T2_02_IllegalCharsInRenameSanitized);
            Register("Tier 2", "F7", "F7_T2_03_OverwriteReplacesMatchingSessions", Tier2_BoundaryCornerTests.Test_F7_T2_03_OverwriteReplacesMatchingSessions);
            Register("Tier 2", "F7", "F7_T2_04_SkipAllConflicts", Tier2_BoundaryCornerTests.Test_F7_T2_04_SkipAllConflicts);
            Register("Tier 2", "F7", "F7_T2_05_MixedPerProjectStrategies", Tier2_BoundaryCornerTests.Test_F7_T2_05_MixedPerProjectStrategies);

            Register("Tier 2", "F8", "F8_T2_01_EmptyLocalAndEmptyBackupTree", Tier2_BoundaryCornerTests.Test_F8_T2_01_EmptyLocalAndEmptyBackupTree);
            Register("Tier 2", "F8", "F8_T2_02_SpecialCharsInProjectName", Tier2_BoundaryCornerTests.Test_F8_T2_02_SpecialCharsInProjectName);
            Register("Tier 2", "F8", "F8_T2_03_ProjectWithZeroSessions", Tier2_BoundaryCornerTests.Test_F8_T2_03_ProjectWithZeroSessions);
            Register("Tier 2", "F8", "F8_T2_04_LargeTreeScale1000Sessions", Tier2_BoundaryCornerTests.Test_F8_T2_04_LargeTreeScale1000Sessions);
            Register("Tier 2", "F8", "F8_T2_05_MissingGlobalConfigsSubfolders", Tier2_BoundaryCornerTests.Test_F8_T2_05_MissingGlobalConfigsSubfolders);

            Register("Tier 2", "F9", "F9_T2_01_ReentrancyProtectionNoInfiniteLoop", Tier2_BoundaryCornerTests.Test_F9_T2_01_ReentrancyProtectionNoInfiniteLoop);
            Register("Tier 2", "F9", "F9_T2_02_SelectNewOnlyWhenZeroNew", Tier2_BoundaryCornerTests.Test_F9_T2_02_SelectNewOnlyWhenZeroNew);
            Register("Tier 2", "F9", "F9_T2_03_SelectAllIdempotent", Tier2_BoundaryCornerTests.Test_F9_T2_03_SelectAllIdempotent);
            Register("Tier 2", "F9", "F9_T2_04_PartialProjectSelection", Tier2_BoundaryCornerTests.Test_F9_T2_04_PartialProjectSelection);
            Register("Tier 2", "F9", "F9_T2_05_SelectionPreservedOnEmptyCollection", Tier2_BoundaryCornerTests.Test_F9_T2_05_SelectionPreservedOnEmptyCollection);

            Register("Tier 2", "F10", "F10_T2_01_MissingBrainDirectoryInBackup", Tier2_BoundaryCornerTests.Test_F10_T2_01_MissingBrainDirectoryInBackup);
            Register("Tier 2", "F10", "F10_T2_02_HandlesWalAndShmCompanionFiles", Tier2_BoundaryCornerTests.Test_F10_T2_02_HandlesWalAndShmCompanionFiles);
            Register("Tier 2", "F10", "F10_T2_03_ImportZeroSelectedItems", Tier2_BoundaryCornerTests.Test_F10_T2_03_ImportZeroSelectedItems);
            Register("Tier 2", "F10", "F10_T2_04_DestinationDirectoryCreatedIfMissing", Tier2_BoundaryCornerTests.Test_F10_T2_04_DestinationDirectoryCreatedIfMissing);
            Register("Tier 2", "F10", "F10_T2_05_CrossDriveImportPreservesStructure", Tier2_BoundaryCornerTests.Test_F10_T2_05_CrossDriveImportPreservesStructure);

            Register("Tier 2", "F11", "F11_T2_01_HighlightZeroImported", Tier2_BoundaryCornerTests.Test_F11_T2_01_HighlightZeroImported);
            Register("Tier 2", "F11", "F11_T2_02_HighlightSurvivesFilterToggle", Tier2_BoundaryCornerTests.Test_F11_T2_02_HighlightSurvivesFilterToggle);
            Register("Tier 2", "F11", "F11_T2_03_ExternalFileDeletionReflected", Tier2_BoundaryCornerTests.Test_F11_T2_03_ExternalFileDeletionReflected);
            Register("Tier 2", "F11", "F11_T2_04_StatusCountExactArithmetic", Tier2_BoundaryCornerTests.Test_F11_T2_04_StatusCountExactArithmetic);
            Register("Tier 2", "F11", "F11_T2_05_ConcurrentHighlightChecks", Tier2_BoundaryCornerTests.Test_F11_T2_05_ConcurrentHighlightChecks);

            Register("Tier 2", "F12", "F12_T2_01_AppIconHeaderMagic", Tier2_BoundaryCornerTests.Test_F12_T2_01_AppIconHeaderMagic);
            Register("Tier 2", "F12", "F12_T2_02_BuildBatOptimizationFlag", Tier2_BoundaryCornerTests.Test_F12_T2_02_BuildBatOptimizationFlag);
            Register("Tier 2", "F12", "F12_T2_03_BuildBatPlatformAnyCpu", Tier2_BoundaryCornerTests.Test_F12_T2_03_BuildBatPlatformAnyCpu);
            Register("Tier 2", "F12", "F12_T2_04_TargetExecutableExists", Tier2_BoundaryCornerTests.Test_F12_T2_04_TargetExecutableExists);
            Register("Tier 2", "F12", "F12_T2_05_PeHeaderMagic", Tier2_BoundaryCornerTests.Test_F12_T2_05_PeHeaderMagic);

            Register("Tier 2", "F13", "F13_T2_01_ManualDualTreeKeywords", Tier2_BoundaryCornerTests.Test_F13_T2_01_ManualDualTreeKeywords);
            Register("Tier 2", "F13", "F13_T2_02_ManualSelectiveImportKeywords", Tier2_BoundaryCornerTests.Test_F13_T2_02_ManualSelectiveImportKeywords);
            Register("Tier 2", "F13", "F13_T2_03_ManualConflictResolutionKeywords", Tier2_BoundaryCornerTests.Test_F13_T2_03_ManualConflictResolutionKeywords);
            Register("Tier 2", "F13", "F13_T2_04_ManualJunkFilterKeywords", Tier2_BoundaryCornerTests.Test_F13_T2_04_ManualJunkFilterKeywords);
            Register("Tier 2", "F13", "F13_T2_05_GitignoreTestRunnerExclusion", Tier2_BoundaryCornerTests.Test_F13_T2_05_GitignoreTestRunnerExclusion);

            // ── Tier 3: Pairwise & Cross-Feature Interactions (13 tests) ──
            Register("Tier 3", "F1+F2", "T3_01_F1_F2_ProtobufCodecRoundtripWithChineseField15", Tier3_PairwiseInteractionTests.Test_T3_01_F1_F2_ProtobufCodecRoundtripWithChineseField15);
            Register("Tier 3", "F1+F3", "T3_02_F1_F3_StreamingZipWithCustomTitleExtraction", Tier3_PairwiseInteractionTests.Test_T3_02_F1_F3_StreamingZipWithCustomTitleExtraction);
            Register("Tier 3", "F1+F4", "T3_03_F1_F4_IncrementalMergePreservesCustomTitles", Tier3_PairwiseInteractionTests.Test_T3_03_F1_F4_IncrementalMergePreservesCustomTitles);
            Register("Tier 3", "F3+F5", "T3_04_F3_F5_StreamingZipWithJunkProjectFiltering", Tier3_PairwiseInteractionTests.Test_T3_04_F3_F5_StreamingZipWithJunkProjectFiltering);
            Register("Tier 3", "F5+F6", "T3_05_F5_F6_VisualDiffWithJunkFilterToggle", Tier3_PairwiseInteractionTests.Test_T3_05_F5_F6_VisualDiffWithJunkFilterToggle);
            Register("Tier 3", "F6+F7", "T3_06_F6_F7_VisualDiffConflictedTriggersConflictPlan", Tier3_PairwiseInteractionTests.Test_T3_06_F6_F7_VisualDiffConflictedTriggersConflictPlan);
            Register("Tier 3", "F6+F9", "T3_07_F6_F9_DiffStatusFeedsQuickSelectNewOnly", Tier3_PairwiseInteractionTests.Test_T3_07_F6_F9_DiffStatusFeedsQuickSelectNewOnly);
            Register("Tier 3", "F7+F10", "T3_08_F7_F10_ConflictResolutionExecutedDuringSelectiveImport", Tier3_PairwiseInteractionTests.Test_T3_08_F7_F10_ConflictResolutionExecutedDuringSelectiveImport);
            Register("Tier 3", "F8+F9", "T3_09_F8_F9_CascadingSelectionInDualTreeWorkspace", Tier3_PairwiseInteractionTests.Test_T3_09_F8_F9_CascadingSelectionInDualTreeWorkspace);
            Register("Tier 3", "F9+F10", "T3_10_F9_F10_CascadingSelectionFeedsSelectiveImport", Tier3_PairwiseInteractionTests.Test_T3_10_F9_F10_CascadingSelectionFeedsSelectiveImport);
            Register("Tier 3", "F10+F11", "T3_11_F10_F11_SelectiveImportTriggersHighlightAndRefresh", Tier3_PairwiseInteractionTests.Test_T3_11_F10_F11_SelectiveImportTriggersHighlightAndRefresh);
            Register("Tier 3", "F4+F10", "T3_12_F4_F10_IncrementalShardMergerInvokedByImporter", Tier3_PairwiseInteractionTests.Test_T3_12_F4_F10_IncrementalShardMergerInvokedByImporter);
            Register("Tier 3", "F1+F6+F10", "T3_13_F1_F6_F10_FullChainCustomTitleDiffImportVerify", Tier3_PairwiseInteractionTests.Test_T3_13_F1_F6_F10_FullChainCustomTitleDiffImportVerify);

            // ── Tier 4: Real-World Workload Scenarios (5 scenarios) ──
            Register("Tier 4", "Multi-Feature", "T4_Scenario_01_MultiMachineDeveloperMigration", Tier4_RealWorldScenarioTests.Scenario_01_MultiMachineDeveloperMigration);
            Register("Tier 4", "Multi-Feature", "T4_Scenario_02_SelectiveSingleConversationExtraction", Tier4_RealWorldScenarioTests.Scenario_02_SelectiveSingleConversationExtraction);
            Register("Tier 4", "Multi-Feature", "T4_Scenario_03_JunkProjectHygieneAndIsolation", Tier4_RealWorldScenarioTests.Scenario_03_JunkProjectHygieneAndIsolation);
            Register("Tier 4", "Multi-Feature", "T4_Scenario_04_ProjectNameCollisionResolutionMatrix", Tier4_RealWorldScenarioTests.Scenario_04_ProjectNameCollisionResolutionMatrix);
            Register("Tier 4", "Multi-Feature", "T4_Scenario_05_CrossMachineChineseTitleLosslessIntegrity", Tier4_RealWorldScenarioTests.Scenario_05_CrossMachineChineseTitleLosslessIntegrity);
        }
    }
}
