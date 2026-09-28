#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.Tests
{
    [TestFixture]
    public class CombatScrapDropTests
    {
        private GameObject turnManagerGo;
        private TurnManager turnManager;
        private GameObject inventoryGo;
        private InventoryManager inventoryManager;

        [SetUp]
        public void SetUp()
        {
            inventoryGo = new GameObject("Test_InventoryManager");
            inventoryManager = inventoryGo.AddComponent<InventoryManager>();

            turnManagerGo = new GameObject("Test_TurnManager");
            turnManager = turnManagerGo.AddComponent<TurnManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (turnManagerGo != null) Object.DestroyImmediate(turnManagerGo);
            if (inventoryGo != null) Object.DestroyImmediate(inventoryGo);
        }

        [Test]
        public void AwardCombatVictoryScrap_DropsBetween2And10Inclusive()
        {
            InventoryManager activeInventory = InventoryManager.Instance != null ? InventoryManager.Instance : inventoryManager;
            int startingScrap = activeInventory.ScrapMetalCount;
            int lastAwardedEventScrap = -1;
            TurnManager.OnCombatVictoryScrapAwarded += (amt) => lastAwardedEventScrap = amt;

            for (int i = 0; i < 50; i++)
            {
                int dropped = turnManager.AwardCombatVictoryScrap();
                Assert.GreaterOrEqual(dropped, 2, "Scrap metal drop must be at least 2.");
                Assert.LessOrEqual(dropped, 10, "Scrap metal drop must be at most 10.");
                Assert.AreEqual(dropped, lastAwardedEventScrap, "OnCombatVictoryScrapAwarded event must broadcast exact dropped scrap amount.");
            }

            Assert.Greater(activeInventory.ScrapMetalCount, startingScrap, "InventoryManager must accumulate dropped scrap.");
        }
    }
}
#endif
