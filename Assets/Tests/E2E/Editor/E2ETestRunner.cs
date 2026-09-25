using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using CastleOfTheD20.Tests.E2E.Common;
using CastleOfTheD20.Tests.E2E.Tier1_FeatureCoverage;
using CastleOfTheD20.Tests.E2E.Tier2_BoundaryCornerCases;
using CastleOfTheD20.Tests.E2E.Tier3_CrossFeatureCombinations;
using CastleOfTheD20.Tests.E2E.Tier4_RealWorldScenarios;
using CastleOfTheD20.Tests.E2E.Tier5_AdversarialHardening;

namespace CastleOfTheD20.Tests.E2E.Editor
{
    [Serializable]
    public class TestRunRecord
    {
        public string testName;
        public string tier;
        public string featureId;
        public string description;
        public bool passed;
        public string errorMessage;
        public float durationMs;
    }

    [Serializable]
    public class TestSuiteSummary
    {
        public string timestamp;
        public int totalTests;
        public int passed;
        public int failed;
        public float totalDurationMs;
        public List<TestRunRecord> results = new List<TestRunRecord>();
    }

    /// <summary>
    /// Master E2E Test Runner for Castle of Dice.
    /// Provides Editor Menu triggers, batchmode execution, file-trigger automation,
    /// and structured JSON/Text reporting.
    /// </summary>
    [InitializeOnLoad]
    public static class E2ETestRunner
    {
        private static readonly string TriggerPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "run_tests.trigger");
        public static TestSuiteSummary LastSummary { get; private set; }

        static E2ETestRunner()
        {
            EditorApplication.update += CheckFileTrigger;
        }

        private static void CheckFileTrigger()
        {
            if (File.Exists(TriggerPath))
            {
                try
                {
                    File.Delete(TriggerPath);
                }
                catch
                {
                    // Ignore deletion locks
                }

                UnityEngine.Debug.Log("[E2ETestRunner] Trigger detected from CLI/script. Refreshing asset database and executing full E2E test suite...");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                RunAllTests(silentSuccess: false);
            }
        }

        #region Menu Items

        [MenuItem("CastleOfDice/Tests/Run All E2E Tests (Tiers 1-5)", false, 10)]
        public static void RunAllTestsMenu()
        {
            RunAllTests(silentSuccess: false);
        }

        [MenuItem("CastleOfDice/Tests/Run Tier 1 Feature Tests", false, 20)]
        public static void RunTier1Menu()
        {
            RunTestsForTiers(new[] { TestTier.Tier1_FeatureCoverage });
        }

        [MenuItem("CastleOfDice/Tests/Run Tier 2 Boundary Tests", false, 21)]
        public static void RunTier2Menu()
        {
            RunTestsForTiers(new[] { TestTier.Tier2_BoundaryCornerCases });
        }

        [MenuItem("CastleOfDice/Tests/Run Tier 3 Cross-Feature Tests", false, 22)]
        public static void RunTier3Menu()
        {
            RunTestsForTiers(new[] { TestTier.Tier3_CrossFeatureCombinations });
        }

        [MenuItem("CastleOfDice/Tests/Run Tier 4 Real-World Tests", false, 23)]
        public static void RunTier4Menu()
        {
            RunTestsForTiers(new[] { TestTier.Tier4_RealWorldScenarios });
        }

        [MenuItem("CastleOfDice/Tests/Run Tier 5 Hardening Tests", false, 24)]
        public static void RunTier5Menu()
        {
            RunTestsForTiers(new[] { TestTier.Tier5_AdversarialHardening });
        }

        #endregion

        #region Batchmode Execution

        public static void RunAllTestsBatchmode()
        {
            UnityEngine.Debug.Log("==================================================================");
            UnityEngine.Debug.Log("  CASTLE OF DICE — BATCHMODE E2E TEST RUNNER STARTING");
            UnityEngine.Debug.Log("==================================================================");

            TestSuiteSummary summary = ExecuteTestSuite(null);
            ExportResults(summary);

            if (summary.failed > 0)
            {
                UnityEngine.Debug.LogError($"[E2ETestRunner] BATCHMODE FAILED: {summary.failed} / {summary.totalTests} tests failed!");
                EditorApplication.Exit(1);
            }
            else
            {
                UnityEngine.Debug.Log($"[E2ETestRunner] BATCHMODE SUCCESS: All {summary.totalTests} tests passed in {summary.totalDurationMs:F1}ms!");
                EditorApplication.Exit(0);
            }
        }

        #endregion

        #region Core Runner Engine

        public static TestSuiteSummary RunAllTests(bool silentSuccess = false)
        {
            TestSuiteSummary summary = ExecuteTestSuite(null);
            ExportResults(summary);
            LastSummary = summary;

            if (summary.failed > 0)
            {
                EditorUtility.DisplayDialog("E2E Tests Failed",
                    $"E2E Test Run Completed with Failures:\nPassed: {summary.passed}\nFailed: {summary.failed}\nDuration: {summary.totalDurationMs:F1}ms\n\nSee Console and TestResults_E2E.txt for details.",
                    "OK");
            }
            else if (!silentSuccess)
            {
                UnityEngine.Debug.Log($"<color=#00FF00><b>[E2ETestRunner] SUCCESS: All {summary.totalTests} tests passed ({summary.totalDurationMs:F1}ms)!</b></color>");
            }

            return summary;
        }

        public static TestSuiteSummary RunTestsForTiers(TestTier[] tiers)
        {
            HashSet<TestTier> targetTiers = new HashSet<TestTier>(tiers);
            TestSuiteSummary summary = ExecuteTestSuite(targetTiers);
            ExportResults(summary);
            LastSummary = summary;
            return summary;
        }

        private static TestSuiteSummary ExecuteTestSuite(HashSet<TestTier> tierFilter)
        {
            Type[] testClasses = new Type[]
            {
                typeof(Tier1_F1_MusicManagerCoreTests),
                typeof(Tier1_F2_StateAudioIntegrationTests),
                typeof(Tier1_F3_AttributionAndUITests),
                typeof(Tier1_F4_EditorToolingTests),
                typeof(Tier2_BoundaryTests),
                typeof(Tier3_CrossFeatureTests),
                typeof(Tier4_RealWorldScenarioTests),
                typeof(Tier5_AdversarialTests)
            };

            TestSuiteSummary summary = new TestSuiteSummary
            {
                timestamp = DateTime.UtcNow.ToString("o")
            };

            Stopwatch totalSw = Stopwatch.StartNew();

            foreach (Type testClass in testClasses)
            {
                MethodInfo[] methods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance);

                foreach (MethodInfo method in methods)
                {
                    E2ETestCaseAttribute attr = method.GetCustomAttribute<E2ETestCaseAttribute>();
                    if (attr == null) continue;

                    if (tierFilter != null && !tierFilter.Contains(attr.Tier))
                    {
                        continue;
                    }

                    TestRunRecord record = new TestRunRecord
                    {
                        testName = method.Name,
                        tier = attr.Tier.ToString(),
                        featureId = attr.FeatureId,
                        description = attr.Description
                    };

                    summary.totalTests++;

                    object testInstance = Activator.CreateInstance(testClass);
                    E2ETestSuite suiteInstance = testInstance as E2ETestSuite;

                    Stopwatch methodSw = Stopwatch.StartNew();
                    try
                    {
                        suiteInstance?.SetUp();
                        method.Invoke(testInstance, null);
                        suiteInstance?.TearDown();

                        record.passed = true;
                        summary.passed++;
                    }
                    catch (TargetInvocationException ex)
                    {
                        suiteInstance?.TearDown();
                        record.passed = false;
                        record.errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                        summary.failed++;
                        UnityEngine.Debug.LogError($"[E2ETestRunner] FAILED: {method.Name} ({attr.FeatureId}): {record.errorMessage}");
                    }
                    catch (Exception ex)
                    {
                        suiteInstance?.TearDown();
                        record.passed = false;
                        record.errorMessage = ex.Message;
                        summary.failed++;
                        UnityEngine.Debug.LogError($"[E2ETestRunner] FAILED: {method.Name} ({attr.FeatureId}): {record.errorMessage}");
                    }
                    finally
                    {
                        methodSw.Stop();
                        record.durationMs = (float)methodSw.Elapsed.TotalMilliseconds;
                    }

                    summary.results.Add(record);
                }
            }

            totalSw.Stop();
            summary.totalDurationMs = (float)totalSw.Elapsed.TotalMilliseconds;
            return summary;
        }

        private static void ExportResults(TestSuiteSummary summary)
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string jsonPath = Path.Combine(projectRoot, "TestResults_E2E.json");
            string txtPath = Path.Combine(projectRoot, "TestResults_E2E.txt");

            // Write JSON
            try
            {
                string json = JsonUtility.ToJson(summary, true);
                File.WriteAllText(jsonPath, json);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[E2ETestRunner] Failed writing TestResults_E2E.json: {ex.Message}");
            }

            // Write Human-Readable Report
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine("                   CASTLE OF DICE — E2E TEST RESULTS REPORT                    ");
                sb.AppendLine("================================================================================");
                sb.AppendLine($"Timestamp: {summary.timestamp}");
                sb.AppendLine($"Total Tests: {summary.totalTests} | Passed: {summary.passed} | Failed: {summary.failed}");
                sb.AppendLine($"Total Duration: {summary.totalDurationMs:F1} ms");
                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine(string.Format("{0,-45} | {1,-6} | {2,-8} | {3,-10}", "Test Name", "Tier", "Result", "Duration"));
                sb.AppendLine("--------------------------------------------------------------------------------");

                foreach (var res in summary.results)
                {
                    string status = res.passed ? "[PASS]" : "[FAIL]";
                    sb.AppendLine(string.Format("{0,-45} | {1,-6} | {2,-8} | {3,6:F1}ms",
                        res.testName.Length > 45 ? res.testName.Substring(0, 42) + "..." : res.testName,
                        res.tier.Replace("Tier", "T").Substring(0, Mathf.Min(6, res.tier.Length)),
                        status,
                        res.durationMs));

                    if (!res.passed && !string.IsNullOrEmpty(res.errorMessage))
                    {
                        sb.AppendLine($"    -> ERROR: {res.errorMessage}");
                    }
                }

                sb.AppendLine("================================================================================");
                File.WriteAllText(txtPath, sb.ToString());
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[E2ETestRunner] Failed writing TestResults_E2E.txt: {ex.Message}");
            }
        }

        #endregion
    }
}
