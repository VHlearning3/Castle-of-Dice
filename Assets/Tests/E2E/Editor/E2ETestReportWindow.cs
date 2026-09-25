using UnityEngine;
using UnityEditor;
using CastleOfTheD20.Tests.E2E.Common;

namespace CastleOfTheD20.Tests.E2E.Editor
{
    /// <summary>
    /// Interactive Unity Editor Window for E2E testing dashboard.
    /// Provides one-click execution of any test tier and live pass/fail breakdown.
    /// </summary>
    public class E2ETestReportWindow : EditorWindow
    {
        private Vector2 scrollPos;
        private string filterText = "";
        private TestTier selectedTierFilter = 0; // 0 = All

        [MenuItem("CastleOfDice/Tests/Open E2E Test Dashboard", false, 1)]
        [MenuItem("Tools/Castle of Dice/Open E2E Test Dashboard", false, 100)]
        public static void OpenWindow()
        {
            var win = GetWindow<E2ETestReportWindow>("E2E Test Dashboard");
            win.minSize = new Vector2(550, 600);
            win.Show();
        }

        private void OnGUI()
        {
            GUILayout.Space(8);
            GUILayout.Label("Castle of Dice — E2E Test Suite Dashboard", EditorStyles.boldLabel);
            GUILayout.Label("Covers Tiers 1-5 (Feature coverage, boundaries, flows, scenarios, and hardening).", EditorStyles.miniLabel);
            GUILayout.Space(8);

            // Controls
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
            if (GUILayout.Button("▶ Run All E2E Tests (Tiers 1-5)", GUILayout.Height(35)))
            {
                E2ETestRunner.RunAllTests(silentSuccess: true);
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Run Tier 1 Only", GUILayout.Height(35)))
            {
                E2ETestRunner.RunTestsForTiers(new[] { TestTier.Tier1_FeatureCoverage });
            }
            if (GUILayout.Button("Run Tier 2 Only", GUILayout.Height(35)))
            {
                E2ETestRunner.RunTestsForTiers(new[] { TestTier.Tier2_BoundaryCornerCases });
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Run Tier 3 (Cross-Feature)", GUILayout.Height(28)))
            {
                E2ETestRunner.RunTestsForTiers(new[] { TestTier.Tier3_CrossFeatureCombinations });
            }
            if (GUILayout.Button("Run Tier 4 (Real-World Scenarios)", GUILayout.Height(28)))
            {
                E2ETestRunner.RunTestsForTiers(new[] { TestTier.Tier4_RealWorldScenarios });
            }
            if (GUILayout.Button("Run Tier 5 (Hardening)", GUILayout.Height(28)))
            {
                E2ETestRunner.RunTestsForTiers(new[] { TestTier.Tier5_AdversarialHardening });
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(12);

            // Summary Section
            var summary = E2ETestRunner.LastSummary;
            if (summary == null)
            {
                EditorGUILayout.HelpBox("No test run recorded yet. Click 'Run All E2E Tests' above to execute the suite.", MessageType.Info);
                return;
            }

            // Summary Bar
            Color barColor = summary.failed == 0 ? new Color(0.1f, 0.6f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
            GUI.color = barColor;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.color = Color.white;

            string statusMsg = summary.failed == 0
                ? $"✓ ALL TESTS PASSED: {summary.passed} / {summary.totalTests} ({summary.totalDurationMs:F1}ms)"
                : $"✗ TESTS FAILED: {summary.failed} Failed, {summary.passed} Passed of {summary.totalTests} ({summary.totalDurationMs:F1}ms)";

            GUILayout.Label(statusMsg, EditorStyles.boldLabel);
            GUILayout.Label($"Last Run: {summary.timestamp}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Filter Bar
            EditorGUILayout.BeginHorizontal();
            filterText = EditorGUILayout.TextField("Filter:", filterText);
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                filterText = "";
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Test List
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            foreach (var test in summary.results)
            {
                if (!string.IsNullOrEmpty(filterText) &&
                    !test.testName.ToLower().Contains(filterText.ToLower()) &&
                    !test.featureId.ToLower().Contains(filterText.ToLower()))
                {
                    continue;
                }

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                GUI.color = test.passed ? Color.green : Color.red;
                GUILayout.Label(test.passed ? "[PASS]" : "[FAIL]", GUILayout.Width(50));
                GUI.color = Color.white;

                GUILayout.Label($"[{test.featureId}] {test.testName}", GUILayout.Width(300));
                GUILayout.Label($"{test.durationMs:F1}ms", GUILayout.Width(60));
                GUILayout.Label(test.description, EditorStyles.miniLabel);

                EditorGUILayout.EndHorizontal();

                if (!test.passed && !string.IsNullOrEmpty(test.errorMessage))
                {
                    EditorGUILayout.HelpBox(test.errorMessage, MessageType.Error);
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
