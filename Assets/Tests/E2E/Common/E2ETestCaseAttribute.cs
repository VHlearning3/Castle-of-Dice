using System;

namespace CastleOfTheD20.Tests.E2E.Common
{
    public enum TestTier
    {
        Tier1_FeatureCoverage = 1,
        Tier2_BoundaryCornerCases = 2,
        Tier3_CrossFeatureCombinations = 3,
        Tier4_RealWorldScenarios = 4,
        Tier5_AdversarialHardening = 5
    }

    /// <summary>
    /// Metadata attribute identifying an E2E test case, its tier, target feature, and description.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class E2ETestCaseAttribute : Attribute
    {
        public TestTier Tier { get; }
        public string FeatureId { get; }
        public string Description { get; }

        public E2ETestCaseAttribute(TestTier tier, string featureId, string description)
        {
            Tier = tier;
            FeatureId = featureId;
            Description = description;
        }
    }
}
