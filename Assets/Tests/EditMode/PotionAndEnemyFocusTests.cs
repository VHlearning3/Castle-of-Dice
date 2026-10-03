#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Economy;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// HUD fixes: the bottom-left potion slots (drink rules in and out of combat) and lighting up an enemy's
    /// stat card when its model or card is pointed at or clicked.
    /// </summary>
    [TestFixture]
    public class PotionAndEnemyFocusTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        private PlayerDataSO previousSession;
        private InventoryManager inventory;
        private ItemSO smallPotion;
        private ItemSO largePotion;
        private PlayerUnit hero;
        private GridManager grid;

        [SetUp]
        public void SetUp()
        {
            previousSession = PlayerDataSO.Session;
            PlayerDataSO.Session = null;
            EnemyFocus.Clear();

            smallPotion = CreateItem(ShopManager.SMALL_POTION_ID, "Small Health Potion", 15);
            largePotion = CreateItem(ShopManager.GREATER_POTION_ID, "Greater Health Potion", 35);

            GameObject invGo = Spawn("Test_Inventory");
            inventory = invGo.AddComponent<InventoryManager>();
            SetField(inventory, "itemCatalog", new List<ItemSO> { smallPotion, largePotion });
            SetStaticProperty(typeof(InventoryManager), "Instance", inventory);
            inventory.RestoreFromSave(gold: 0, scrapMetal: 0, rerollScrolls: 0);
            inventory.RestoreItems(null, null);

            GameObject gridGo = Spawn("Test_GridManager");
            grid = gridGo.AddComponent<GridManager>();
            GridManager.Instance = grid;
            grid.GenerateGridAt(Vector3.zero, 12, 12, 1.6f);

            GameObject tmGo = Spawn("Test_TurnManager");
            tmGo.AddComponent<TurnManager>();
            TurnManager.EnsureInstance();

            GameObject heroGo = Spawn("Test_Hero");
            heroGo.AddComponent<StatusEffectController>();
            hero = heroGo.AddComponent<PlayerUnit>();
            hero.InitializeUnit();
            CharacterClassSO warrior = ScriptableObject.CreateInstance<CharacterClassSO>();
            assets.Add(warrior);
            warrior.Initialize(CharacterClassType.Warrior, "Sir Roland", "Frontline", 30, 14, 4, 3,
                new List<AbilitySO> { ScriptableObject.CreateInstance<AbilitySO>() });
            hero.SetCharacterClass(warrior);
            hero.MoveToTile(grid.GetTileAt(new Vector2Int(2, 2)));
            hero.HasActedThisTurn = false;
            hero.HasMovedThisTurn = false;
        }

        [TearDown]
        public void TearDown()
        {
            EnemyFocus.Clear();
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
            PlayerDataSO.Session = previousSession;
        }

        #region Potion Rules

        [Test]
        public void CheckUse_CoversEveryRule()
        {
            Assert.AreEqual(PotionUseCheck.Ready, PotionQuickBar.CheckUse(1, true, 10, 30, false, false, true), "Outside combat the action flag does not matter");
            Assert.AreEqual(PotionUseCheck.NoPotion, PotionQuickBar.CheckUse(0, true, 10, 30, false, false, false));
            Assert.AreEqual(PotionUseCheck.FullHealth, PotionQuickBar.CheckUse(2, true, 30, 30, false, false, false));
            Assert.AreEqual(PotionUseCheck.NoHero, PotionQuickBar.CheckUse(2, false, 0, 30, false, false, false));
            Assert.AreEqual(PotionUseCheck.NotYourTurn, PotionQuickBar.CheckUse(2, true, 10, 30, true, false, false));
            Assert.AreEqual(PotionUseCheck.ActionUsed, PotionQuickBar.CheckUse(2, true, 10, 30, true, true, true));
            Assert.AreEqual(PotionUseCheck.Ready, PotionQuickBar.CheckUse(2, true, 10, 30, true, true, false));
        }

        [Test]
        public void Exploration_DrinkingHeals_UsesOnePotion_AndIsFree()
        {
            inventory.AddItem(smallPotion, 2);
            hero.TakeDamage(20);

            Assert.AreEqual(PotionUseCheck.Ready, PotionQuickBar.TryDrink(smallPotion, hero));

            Assert.AreEqual(25, hero.CurrentHP);
            Assert.AreEqual(1, inventory.GetItemCount(smallPotion));
            Assert.IsFalse(hero.HasActedThisTurn, "Outside combat a potion costs no action");
        }

        [Test]
        public void FullHealth_KeepsThePotion()
        {
            inventory.AddItem(largePotion, 1);

            Assert.AreEqual(PotionUseCheck.FullHealth, PotionQuickBar.TryDrink(largePotion, hero));
            Assert.AreEqual(1, inventory.GetItemCount(largePotion));
        }

        [Test]
        public void Combat_DrinkingSpendsTheAction_ButNotTheMove()
        {
            SetCombatActive(true);
            inventory.AddItem(largePotion, 2);
            hero.TakeDamage(25);

            Assert.AreEqual(PotionUseCheck.Ready, PotionQuickBar.TryDrink(largePotion, hero));
            Assert.AreEqual(30, hero.CurrentHP, "Healing is capped at max HP");
            Assert.IsTrue(hero.HasActedThisTurn);
            Assert.IsFalse(hero.HasMovedThisTurn);

            hero.TakeDamage(10);
            Assert.AreEqual(PotionUseCheck.ActionUsed, PotionQuickBar.TryDrink(largePotion, hero));
            Assert.AreEqual(1, inventory.GetItemCount(largePotion), "A refused drink keeps the potion");
        }

        [Test]
        public void Combat_EnemyTurn_CannotDrink()
        {
            SetCombatActive(true);
            SetField(TurnManager.Instance, "currentState", TurnState.EnemyTurn);
            inventory.AddItem(smallPotion, 1);
            hero.TakeDamage(10);

            Assert.AreEqual(PotionUseCheck.NotYourTurn, PotionQuickBar.TryDrink(smallPotion, hero));
            Assert.AreEqual(1, inventory.GetItemCount(smallPotion));
        }

        [Test]
        public void QuickDrink_PrefersSmall_ThenLarge()
        {
            inventory.AddItem(smallPotion, 1);
            inventory.AddItem(largePotion, 1);
            hero.TakeDamage(25);

            PotionQuickBar.TryDrinkQuick(hero);
            Assert.AreEqual(0, inventory.GetItemCount(smallPotion));
            Assert.AreEqual(1, inventory.GetItemCount(largePotion));

            hero.TakeDamage(10);
            PotionQuickBar.TryDrinkQuick(hero);
            Assert.AreEqual(0, inventory.GetItemCount(largePotion));
        }

        [Test]
        public void Bar_BuildsTwoSlots_InTheBottomLeftCorner_OnlyOnce()
        {
            GameObject canvasGo = Spawn("Test_Canvas");
            RectTransform canvasRect = canvasGo.AddComponent<RectTransform>();

            PotionQuickBar bar = PotionQuickBar.EnsureBar(canvasRect, null, null, null);
            PotionQuickBar.EnsureBar(canvasRect, null, null, null);

            RectTransform barRect = (RectTransform)bar.transform;
            Assert.AreEqual(Vector2.zero, barRect.anchorMin);
            Assert.AreEqual(Vector2.zero, barRect.anchorMax);
            Assert.AreEqual(1, CountChildren(canvasRect, PotionQuickBar.BarObjectName), "The bar is not duplicated");
            Assert.IsNotNull(bar.SmallSlotButton);
            Assert.IsNotNull(bar.LargeSlotButton);
            Assert.AreEqual(2, bar.transform.childCount);
        }

        #endregion

        #region Enemy Focus

        [Test]
        public void Focus_HoveredCardBeatsHoveredModel_WhichBeatsClicked_AndDeadIsIgnored()
        {
            EnemyUnit rat = SpawnEnemy("Rat");
            EnemyUnit bat = SpawnEnemy("Bat");
            EnemyUnit wolf = SpawnEnemy("Wolf");

            EnemyFocus.Selected = rat;
            Assert.AreSame(rat, EnemyFocus.Highlighted);
            EnemyFocus.TileHovered = bat;
            Assert.AreSame(bat, EnemyFocus.Highlighted);
            EnemyFocus.CardHovered = wolf;
            Assert.AreSame(wolf, EnemyFocus.Highlighted);

            wolf.TakeDamage(99);
            Assert.AreSame(bat, EnemyFocus.Highlighted, "A dead enemy is never highlighted");
        }

        [Test]
        public void StatsHUD_LightsUpOnlyTheFocusedEnemysCard()
        {
            GameObject hudGo = Spawn("Test_PlayerHUD");
            hudGo.AddComponent<RectTransform>();
            CombatStatsHUD statsHUD = hudGo.AddComponent<CombatStatsHUD>();
            statsHUD.Build(null, null, null, null, null, null);

            EnemyUnit rat = SpawnEnemy("Rat");
            EnemyUnit bat = SpawnEnemy("Bat");
            statsHUD.RefreshEnemies(new List<CombatUnit> { rat, bat }, null, true);

            statsHUD.RefreshFocus(bat);
            Assert.IsFalse(statsHUD.IsCardHighlighted(0));
            Assert.IsTrue(statsHUD.IsCardHighlighted(1));
            Assert.AreSame(bat, statsHUD.GetCardEnemy(1));

            statsHUD.RefreshFocus(null);
            Assert.IsFalse(statsHUD.IsCardHighlighted(1));
            Assert.IsNotNull(statsHUD.EnemyStrip.Find("Foe_Card_0").GetComponent<EnemyCardPointer>(), "Cards answer to hover and clicks");
        }

        [Test]
        public void ClickingEnemyModel_SelectsIt_ClickingEmptyGround_Clears()
        {
            EnemyUnit rat = SpawnEnemy("Rat");
            rat.MoveToTile(grid.GetTileAt(new Vector2Int(6, 6)));
            SetCombatActive(true);
            AddActiveUnit(hero);
            AddActiveUnit(rat);
            hero.HasActedThisTurn = true; // no auto-attack, only the selection is under test

            GameObject uiGo = Spawn("Test_CombatUI");
            CombatUIController combatUI = uiGo.AddComponent<CombatUIController>();
            MethodInfo click = typeof(CombatUIController).GetMethod("HandleTileClicked", BindingFlags.Instance | BindingFlags.NonPublic);

            click.Invoke(combatUI, new object[] { grid.GetTileAt(new Vector2Int(6, 6)) });
            Assert.AreSame(rat, EnemyFocus.Selected);

            click.Invoke(combatUI, new object[] { grid.GetTileAt(new Vector2Int(9, 1)) });
            Assert.IsNull(EnemyFocus.Selected);
        }

        #endregion

        #region Helpers

        private GameObject Spawn(string name)
        {
            GameObject go = new GameObject(name);
            spawned.Add(go);
            return go;
        }

        private EnemyUnit SpawnEnemy(string name)
        {
            GameObject go = Spawn("Test_" + name);
            go.AddComponent<StatusEffectController>();
            EnemyUnit enemy = go.AddComponent<EnemyUnit>();
            enemy.ConfigureStats(name, 10, 12, 3, 2);
            return enemy;
        }

        private ItemSO CreateItem(string id, string name, int heal)
        {
            ItemSO item = ScriptableObject.CreateInstance<ItemSO>();
            item.Initialize(id, name, "", ItemType.Consumable, 15, 7, heal, true);
            assets.Add(item);
            return item;
        }

        private static void SetCombatActive(bool active)
        {
            SetField(TurnManager.Instance, "isCombatActive", active);
        }

        private static void AddActiveUnit(CombatUnit unit)
        {
            FieldInfo field = typeof(TurnManager).GetField("activeUnits", BindingFlags.Instance | BindingFlags.NonPublic);
            ((List<CombatUnit>)field.GetValue(TurnManager.Instance)).Add(unit);
        }

        private static int CountChildren(Transform parent, string name)
        {
            int count = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == name) count++;
            }
            return count;
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
#endif
