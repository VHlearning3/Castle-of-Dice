using System;
using UnityEngine;

namespace CastleOfTheD20.Tests.E2E.Common
{
    /// <summary>
    /// Custom assertion exception for E2E tests with formatted failure diagnostics.
    /// </summary>
    public class E2EAssertionException : Exception
    {
        public E2EAssertionException(string message) : base(message) { }
    }

    /// <summary>
    /// Lightweight assertion library providing strict verification and clear diagnostics.
    /// Works across Editor, runtime, batchmode, and standalone runners.
    /// </summary>
    public static class E2EAudioAssert
    {
        public static void IsTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new E2EAssertionException($"Assertion FAILED (Expected True): {message}");
            }
        }

        public static void IsFalse(bool condition, string message)
        {
            if (condition)
            {
                throw new E2EAssertionException($"Assertion FAILED (Expected False): {message}");
            }
        }

        public static void AreEqual<T>(T expected, T actual, string message)
        {
            if (!Equals(expected, actual))
            {
                throw new E2EAssertionException($"Assertion FAILED: {message}. Expected: <{expected}>, Actual: <{actual}>");
            }
        }

        public static void AreNotEqual<T>(T notExpected, T actual, string message)
        {
            if (Equals(notExpected, actual))
            {
                throw new E2EAssertionException($"Assertion FAILED: {message}. Expected NOT <{notExpected}>, but was <{actual}>");
            }
        }

        public static void AreApproximatelyEqual(float expected, float actual, float tolerance, string message)
        {
            if (Mathf.Abs(expected - actual) > tolerance)
            {
                throw new E2EAssertionException($"Assertion FAILED: {message}. Expected: {expected:F4} (±{tolerance:F4}), Actual: {actual:F4}, Diff: {Mathf.Abs(expected - actual):F4}");
            }
        }

        public static void IsNotNull(object obj, string message)
        {
            if (obj == null || (obj is UnityEngine.Object uObj && uObj == null))
            {
                throw new E2EAssertionException($"Assertion FAILED (Expected Not Null): {message}");
            }
        }

        public static void IsNull(object obj, string message)
        {
            if (obj != null && (!(obj is UnityEngine.Object uObj) || uObj != null))
            {
                throw new E2EAssertionException($"Assertion FAILED (Expected Null): {message}. Object is <{obj}>");
            }
        }

        public static void Fail(string message)
        {
            throw new E2EAssertionException($"Explicit Failure: {message}");
        }
    }
}
