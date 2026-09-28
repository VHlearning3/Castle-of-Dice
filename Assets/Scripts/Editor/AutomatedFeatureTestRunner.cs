#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CastleOfTheD20.Editor
{
    public static class AutomatedFeatureTestRunner
    {
        [MenuItem("CastleOfDice/Run Feature Test Suite")]
        public static void RunAllFeatureTests()
        {
            Debug.Log("<color=#00e676><b>[AutomatedFeatureTestRunner] Starting All Feature PlayMode/Unit Tests...</b></color>");

            Type[] testFixtureTypes = new Type[]
            {
                typeof(CastleOfTheD20.Tests.SFXPoolTests),
                typeof(CastleOfTheD20.Tests.DungeonMapUITests),
                typeof(CastleOfTheD20.Tests.MilestoneLevelUpTests),
                typeof(CastleOfTheD20.Tests.QuestHUDTrackerTests),
                typeof(CastleOfTheD20.Tests.CombatScrapDropTests),
                typeof(CastleOfTheD20.Tests.CombatRefactorTests)
            };

            int totalTests = 0;
            int passedTests = 0;
            int failedTests = 0;

            Stopwatch totalWatch = Stopwatch.StartNew();

            foreach (var fixtureType in testFixtureTypes)
            {
                Debug.Log($"<b>--- Running Fixture: {fixtureType.Name} ---</b>");

                MethodInfo setupMethod = null;
                MethodInfo teardownMethod = null;

                foreach (var method in fixtureType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (method.GetCustomAttribute<SetUpAttribute>() != null)
                        setupMethod = method;
                    if (method.GetCustomAttribute<TearDownAttribute>() != null)
                        teardownMethod = method;
                }

                var testMethods = fixtureType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

                foreach (var testMethod in testMethods)
                {
                    if (testMethod.GetCustomAttribute<TestAttribute>() == null)
                        continue;

                    totalTests++;
                    object fixtureInstance = Activator.CreateInstance(fixtureType);

                    try
                    {
                        // 1. Run [SetUp]
                        setupMethod?.Invoke(fixtureInstance, null);

                        // 2. Run [Test]
                        Stopwatch testWatch = Stopwatch.StartNew();
                        testMethod.Invoke(fixtureInstance, null);
                        testWatch.Stop();

                        passedTests++;
                        Debug.Log($"  <color=#00e676>[PASS]</color> {fixtureType.Name}.{testMethod.Name} ({testWatch.ElapsedMilliseconds} ms)");
                    }
                    catch (TargetInvocationException ex)
                    {
                        failedTests++;
                        Exception inner = ex.InnerException ?? ex;
                        Debug.LogError($"  <color=#ff5252>[FAIL]</color> {fixtureType.Name}.{testMethod.Name}: {inner.Message}\n{inner.StackTrace}");
                    }
                    catch (Exception ex)
                    {
                        failedTests++;
                        Debug.LogError($"  <color=#ff5252>[FAIL]</color> {fixtureType.Name}.{testMethod.Name}: {ex.Message}\n{ex.StackTrace}");
                    }
                    finally
                    {
                        // 3. Run [TearDown]
                        try
                        {
                            teardownMethod?.Invoke(fixtureInstance, null);
                        }
                        catch (Exception tdEx)
                        {
                            Debug.LogWarning($"[TearDown Warning] {fixtureType.Name}: {tdEx.Message}");
                        }
                    }
                }
            }

            totalWatch.Stop();

            if (failedTests == 0)
            {
                Debug.Log($"=== [FEATURE TEST SUITE RESULT] ALL {passedTests}/{totalTests} TESTS PASSED (0 FAILED) in {totalWatch.ElapsedMilliseconds} ms! ===");
            }
            else
            {
                Debug.LogError($"=== [FEATURE TEST SUITE RESULT] {failedTests}/{totalTests} TESTS FAILED in {totalWatch.ElapsedMilliseconds} ms! ===");
            }
        }
    }
}
#endif
