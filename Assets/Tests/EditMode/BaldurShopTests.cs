using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Baldur's shop redesign: tiered upgrade prices with a +3 cap, the widened stock
    /// (two potions, poison vial, reroll rune), selling loot back and the poison coating.
    /// </summary>
    [TestFixture]
    public class BaldurShopTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        private PlayerDataSO sessionData;
        private InventoryManager inventory;
        private ShopManager shop;
        private PlayerUnit hero;

        private ItemSO smallPotion;
        private ItemSO greaterPotion;
        private ItemSO poisonVial;
        private ItemSO rerollRune;
        private ItemSO blade;
        private ItemSO armor;
        private ItemSO questHerb;

        [SetUp]
        public void SetUp()
        {
            sessionData = ScriptableObject.CreateInstance<PlayerDataSO>();
            PlayerDataSO.Session = sessionData;

            smallPotion = CreateItem(ShopManager.SMALL_POTION_ID, "Small Health Potion", ItemType.Consumable, ShopManager.HEALTH_POTION_PRICE, 7, 15, true);
            greaterPotion = CreateItem(ShopManager.GREATER_POTION_ID, "Greater Health Potion", ItemType.Consumable, ShopManager.GREATER_POTION_PRICE, 12, 35, true);
            poisonVial = CreateItem(ShopManager.POISON_VIAL_ID, "Poison Vial", ItemType.Consumable, ShopManager.POISON_VIAL_PRICE, 15, 5, true);
            rerollRune = CreateItem(ShopManager.REROLL_RUNE_ID, "Rune of Fate (D20 Reroll)", ItemType.QuestItem, ShopManager.REROLL_RUNE_PRICE, 35, 1, false);
            blade = CreateItem(ShopManager.SHARPENED_BLADE_ID, "Sharpened Blade", ItemType.WeaponUpgrade, 50, 20, 1, false);
            armor = CreateItem(ShopManager.RUNIC_ARMOR_ID, "Runic Armor", ItemType.ArmorUpgrade, 50, 35, 1, false);
            questHerb = CreateItem("item_swamp_herb", "Castle Moat Blossom", ItemType.QuestItem, 0, 5, 0, false);

            inventory = CreateComponent<InventoryManager>("Inventory", runAwake: false);
            SetField(inventory, "itemCatalog", new List<ItemSO> { smallPotion, greaterPotion, poisonVial, rerollRune, blade, armor, questHerb });
            SetStaticProperty(typeof(InventoryManager), "Instance", inventory);
            inventory.RestoreFromSave(gold: 0, scrapMetal: 0, rerollScrolls: 0);
            inventory.RestoreItems(null, null);

            shop = CreateComponent<ShopManager>("Shop", runAwake: false);
            SetStaticProperty(typeof(ShopManager), "Instance", shop);

            hero = CreateComponent<PlayerUnit>("Hero");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            spawned.Clear();

            foreach (ScriptableObject asset in assets)
            {
                if (asset != null) Object.DestroyImmediate(asset);
            }
            assets.Clear();

            SetStaticProperty(typeof(InventoryManager), "Instance", null);
            SetStaticProperty(typeof(ShopManager), "Instance", null);
            PlayerDataSO.Session = null;
            if (sessionData != null) Object.DestroyImmediate(sessionData);
        }

        #region Upgrade Tiers

        [Test]
        public void UpgradePrice_RisesPerLevel_AndStopsAtThree()
        {
            Assert.AreEqual(50, ShopManager.GetUpgradePrice(0));
            Assert.AreEqual(75, ShopManager.GetUpgradePrice(1));
            Assert.AreEqual(100, ShopManager.GetUpgradePrice(2));
            Assert.AreEqual(-1, ShopManager.GetUpgradePrice(3), "Level 3 is the cap: nothing more to buy.");
            Assert.AreEqual(-1, ShopManager.GetUpgradePrice(7), "Old saves above the cap stay maxed.");
        }

        [Test]
        public void BuyingBlade_ChargesTieredPrice_AndRaisesDamageBonus()
        {
            inventory.AddGold(50 + 75 + 100);

            Assert.AreEqual(ShopPurchaseResult.Success, shop.TryBuyItem(blade, hero));
            Assert.AreEqual(1, hero.WeaponDamageBonus);
            Assert.AreEqual(175, inventory.CurrentGold);

            Assert.AreEqual(ShopPurchaseResult.Success, shop.TryBuyItem(blade, hero));
            Assert.AreEqual(2, hero.WeaponDamageBonus);
            Assert.AreEqual(100, inventory.CurrentGold);

            Assert.AreEqual(ShopPurchaseResult.Success, shop.TryBuyItem(blade, hero));
            Assert.AreEqual(3, hero.WeaponDamageBonus);
            Assert.AreEqual(0, inventory.CurrentGold);

            Assert.AreEqual(3, sessionData.WeaponDamageBonus, "Upgrades must reach the session data so they survive scene loads.");
        }

        [Test]
        public void BuyingArmor_PastLevelThree_IsRefused_AndKeepsGold()
        {
            inventory.AddGold(1000);
            for (int i = 0; i < ShopManager.MAX_UPGRADE_LEVEL; i++)
            {
                Assert.AreEqual(ShopPurchaseResult.Success, shop.TryBuyItem(armor, hero));
            }
            int goldAtCap = inventory.CurrentGold;

            Assert.AreEqual(ShopPurchaseResult.MaxLevel, shop.TryBuyItem(armor, hero));
            Assert.AreEqual(3, hero.ArmorClassBonus);
            Assert.AreEqual(goldAtCap, inventory.CurrentGold);
            Assert.AreEqual(-1, ShopManager.GetBuyPrice(armor, hero));
        }

        [Test]
        public void UpgradeLevels_AreTrackedSeparately()
        {
            inventory.AddGold(50 + 50);
            shop.TryBuyItem(blade, hero);
            shop.TryBuyItem(armor, hero);

            Assert.AreEqual(1, ShopManager.GetUpgradeLevel(blade, hero));
            Assert.AreEqual(1, ShopManager.GetUpgradeLevel(armor, hero));
            Assert.AreEqual(75, ShopManager.GetBuyPrice(blade, hero));
            Assert.AreEqual(75, ShopManager.GetBuyPrice(armor, hero));
        }

        [Test]
        public void Upgrade_IsNotAddedToInventory()
        {
            inventory.AddGold(50);
            shop.TryBuyItem(blade, hero);

            Assert.AreEqual(0, inventory.GetItemCount(blade));
        }

        #endregion

        #region Stock

        [Test]
        public void NotEnoughGold_IsRefused_AndNothingChanges()
        {
            inventory.AddGold(ShopManager.GREATER_POTION_PRICE - 1);

            Assert.AreEqual(ShopPurchaseResult.NotEnoughGold, shop.TryBuyItem(greaterPotion, hero));
            Assert.AreEqual(ShopManager.GREATER_POTION_PRICE - 1, inventory.CurrentGold);
            Assert.AreEqual(0, inventory.GetItemCount(greaterPotion));
        }

        [Test]
        public void PotionPrices_MatchSpec()
        {
            Assert.AreEqual(15, ShopManager.HEALTH_POTION_PRICE);
            Assert.AreEqual(25, ShopManager.GREATER_POTION_PRICE);
        }

        [Test]
        public void BuyingPotions_AddsThemToInventory_AtTheirPrice()
        {
            inventory.AddGold(40);

            Assert.IsTrue(shop.BuyItem(smallPotion, hero));
            Assert.IsTrue(shop.BuyItem(greaterPotion, hero));

            Assert.AreEqual(0, inventory.CurrentGold);
            Assert.AreEqual(1, inventory.GetItemCount(smallPotion));
            Assert.AreEqual(1, inventory.GetItemCount(greaterPotion));
        }

        [Test]
        public void BuyingRerollRune_AddsARerollScroll()
        {
            inventory.AddGold(ShopManager.REROLL_RUNE_PRICE);

            Assert.AreEqual(ShopPurchaseResult.Success, shop.TryBuyItem(rerollRune, hero));
            Assert.AreEqual(1, inventory.RerollScrollCount);
            Assert.IsTrue(inventory.HasRerollScroll);
        }

        [Test]
        public void BuyStock_ListsSixItems_InCatalog()
        {
            Assert.AreEqual(6, ShopManager.BuyStockOrder.Length);
            foreach (string id in ShopManager.BuyStockOrder)
            {
                Assert.IsNotNull(inventory.FindItemByID(id), $"Stock item '{id}' must resolve from the item catalog.");
            }
        }

        #endregion

        #region Selling

        [Test]
        public void Selling_PaysSellPrice_AndRemovesOne()
        {
            inventory.AddItem(greaterPotion, 2);

            Assert.IsTrue(shop.SellItem(greaterPotion));
            Assert.AreEqual(12, inventory.CurrentGold);
            Assert.AreEqual(1, inventory.GetItemCount(greaterPotion));
        }

        [Test]
        public void QuestItems_CannotBeSold()
        {
            inventory.AddItem(questHerb, 1);

            Assert.IsFalse(ShopManager.IsSellable(questHerb));
            Assert.IsFalse(shop.SellItem(questHerb));
            Assert.AreEqual(1, inventory.GetItemCount(questHerb));
            Assert.AreEqual(0, inventory.CurrentGold);
        }

        [Test]
        public void SellPrices_AreBelowBuyPrices()
        {
            foreach (ItemSO item in new[] { smallPotion, greaterPotion, poisonVial, rerollRune })
            {
                Assert.Less(item.SellPriceGold, item.BuyPriceGold, $"{item.ItemName} must not be resold for a profit.");
            }
        }

        [Test]
        public void ScrapConversion_StillPaysTenGoldEach()
        {
            inventory.AddScrapMetal(4);

            Assert.AreEqual(40, shop.ConvertScrapToGold());
            Assert.AreEqual(0, inventory.ScrapMetalCount);
            Assert.AreEqual(40, inventory.CurrentGold);
        }

        #endregion

        #region Poison Vial

        [Test]
        public void PoisonVial_IsUsedUpAtFightStart_AndBoostsOnlyTheFirstHit()
        {
            inventory.AddItem(poisonVial, 2);

            Assert.IsTrue(inventory.TryCoatWithPoisonVial(hero));
            Assert.AreEqual(1, inventory.GetItemCount(poisonVial));
            Assert.AreEqual(5, hero.PoisonCoatingBonus);

            Assert.IsFalse(inventory.TryCoatWithPoisonVial(hero), "An already coated blade must not use a second vial.");
            Assert.AreEqual(1, inventory.GetItemCount(poisonVial));

            Assert.AreEqual(5, hero.ConsumePoisonCoating());
            Assert.AreEqual(0, hero.ConsumePoisonCoating());
        }

        [Test]
        public void PoisonVial_WithoutVials_DoesNothing()
        {
            Assert.IsFalse(inventory.TryCoatWithPoisonVial(hero));
            Assert.AreEqual(0, hero.PoisonCoatingBonus);
        }

        [Test]
        public void UsingPoisonVial_CoatsInsteadOfHealing()
        {
            inventory.AddItem(poisonVial, 1);
            int hpBefore = hero.CurrentHP;

            Assert.IsTrue(inventory.UseItem(poisonVial, hero));
            Assert.AreEqual(hpBefore, hero.CurrentHP);
            Assert.AreEqual(5, hero.PoisonCoatingBonus);
            Assert.AreEqual(0, inventory.GetItemCount(poisonVial));
        }

        [Test]
        public void ClearPoisonCoating_RemovesUnusedCoating()
        {
            hero.ApplyPoisonCoating(5);
            hero.ClearPoisonCoating();

            Assert.AreEqual(0, hero.PoisonCoatingBonus);
        }

        #endregion

        #region Helpers

        private ItemSO CreateItem(string id, string name, ItemType type, int buy, int sell, int stat, bool consumable)
        {
            ItemSO item = ScriptableObject.CreateInstance<ItemSO>();
            item.Initialize(id, name, "", type, buy, sell, stat, consumable);
            assets.Add(item);
            return item;
        }

        private T CreateComponent<T>(string name, bool runAwake = true) where T : Component
        {
            GameObject go = new GameObject(name);
            spawned.Add(go);
            T component = go.AddComponent<T>();
            if (!runAwake) return component;

            // Edit Mode does not run Awake for AddComponent; invoke it like the engine would
            InvokeAwake(component);
            StatusEffectController effects = go.GetComponent<StatusEffectController>();
            if (effects != null) InvokeAwake(effects);
            return component;
        }

        private static void InvokeAwake(object target)
        {
            for (System.Type type = target.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
            {
                MethodInfo awake = type.GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (awake != null)
                {
                    awake.Invoke(target, null);
                    return;
                }
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void SetStaticProperty(System.Type type, string propertyName, object value)
        {
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            property?.SetValue(null, value);
        }

        #endregion
    }
}
