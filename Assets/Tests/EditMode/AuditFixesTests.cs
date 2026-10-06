using NUnit.Framework;
using UnityEngine;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Fixes from the 2026-10-06 audit that are not covered by the shop and quest tests.
    /// </summary>
    [TestFixture]
    public class AuditFixesTests
    {
        private GameObject enemyObject;

        [TearDown]
        public void TearDown()
        {
            if (enemyObject != null) Object.DestroyImmediate(enemyObject);
        }

        [Test]
        public void EnemyCritical_DoublesTheDiceButNotTheBonus()
        {
            enemyObject = new GameObject("Audit_Witch");
            EnemyUnit witch = enemyObject.AddComponent<EnemyUnit>();
            witch.ConfigureDamageDice(2, 4, 3); // the Shadow Witch's shadow bolt

            int lowest = int.MaxValue;
            int highest = int.MinValue;
            for (int i = 0; i < 2000; i++)
            {
                int damage = witch.RollCriticalDamage();
                lowest = Mathf.Min(lowest, damage);
                highest = Mathf.Max(highest, damage);
            }

            Assert.GreaterOrEqual(lowest, 7, "4d4+3 can roll no lower than 7.");
            Assert.LessOrEqual(highest, 19, "A critical never reaches 22, which one-shot a full-health 25 HP rogue.");
        }
    }
}
