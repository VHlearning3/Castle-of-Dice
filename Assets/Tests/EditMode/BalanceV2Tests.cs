using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Data;
using CastleOfTheD20.Economy;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Vili's balance picks of 2026-10-08: dearer potions, a smaller starting purse, the Rogue's Backstab Sneak
    /// Attack, Sir Roland's great sword, a +15 Giant's Elixir and a harder-hitting Gargoyle King.
    /// </summary>
    [TestFixture]
    public class BalanceV2Tests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            }
            spawned.Clear();
        }

        private static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, path);
            return asset;
        }

        [Test]
        public void Potions_CostMore_AndStillSellForLess()
        {
            ItemSO small = Load<ItemSO>("Assets/Data/Item_Potion_Health.asset");
            ItemSO greater = Load<ItemSO>("Assets/Data/Item_GreaterPotion.asset");
            Assert.AreEqual(25, small.BuyPriceGold);
            Assert.AreEqual(45, greater.BuyPriceGold);
            Assert.Less(small.SellPriceGold, small.BuyPriceGold);
            Assert.Less(greater.SellPriceGold, greater.BuyPriceGold);

            int pipPrice = -1;
            foreach (var stock in HubDialogues.MerchantStock)
            {
                if (stock.id == ShopManager.GREATER_POTION_ID) pipPrice = stock.price;
            }
            Assert.AreEqual(40, pipPrice, "Pip sells the Greater Health Potion for 40 gold.");
        }

        [Test]
        public void NewAdventure_StartsWithFiftyGold()
        {
            Assert.AreEqual(50, InventoryManager.StartingGold);
        }

        [Test]
        public void SwordSlash_IsAGreatSword_2d6()
        {
            AbilitySO slash = Load<AbilitySO>("Assets/Data/Ability_Warrior_SwordSlash.asset");
            Assert.AreEqual("2d6 + 3", slash.GetDamageFormula(attributeBonus: 3, flatBonus: 0));
        }

        [Test]
        public void GiantElixir_GrantsFifteen()
        {
            Assert.AreEqual(15, GiantElixirInteraction.ElixirMaxHPBonus);
            StringAssert.Contains("+15", GiantElixirInteraction.DrinkPrompt);
        }

        [Test]
        public void GargoyleKing_Hits2d8Plus2_AndQuakes3d6()
        {
            GameObject go = new GameObject("King");
            spawned.Add(go);
            GargoyleKingBoss king = go.AddComponent<GargoyleKingBoss>();
            king.InitializeUnit();

            Assert.AreEqual("2d8+2", king.DamageFormula);
            Assert.AreEqual(11, king.AttackDamage);
            Assert.AreEqual(3, GargoyleKingBoss.QuakeDiceCount);
            Assert.AreEqual(6, GargoyleKingBoss.QuakeDiceSides);
        }

        [Test]
        public void SneakAttack_Is2d6()
        {
            Assert.AreEqual(2, AbilityExecutor.SneakAttackDiceCount);
            Assert.AreEqual(6, AbilityExecutor.SneakAttackDiceSides);
        }
    }
}
